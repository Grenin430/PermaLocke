$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$parent = Join-Path $root 'Diagnosticos'
$destination = Join-Path $parent ('informe-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$report = [ordered]@{ Fecha = (Get-Date).ToString('o'); Windows = [Environment]::OSVersion.VersionString; Procesador = $env:PROCESSOR_IDENTIFIER; Sistema64bits = [Environment]::Is64BitOperatingSystem }
try {
    $report.RAM_GB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
    $report.Graficas = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, DriverDate)
} catch { $report.ErrorHardware = $_.Exception.Message }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'equipo.json') -Encoding utf8
foreach ($source in @(@{Folder='Logs'; Pattern='*.log'; Prefix='app'}, @{Folder='Emulator\user\log'; Pattern='*.txt*'; Prefix='azahar'})) {
    $folder = Join-Path $root $source.Folder
    if (Test-Path -LiteralPath $folder) {
        Get-ChildItem -LiteralPath $folder -Filter $source.Pattern -File | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $destination ($source.Prefix + '-' + $_.Name))
        }
    }
}
$config = Join-Path $root 'Emulator\user\config\qt-config.ini'
if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination $destination }
# Per-game settings can override qt-config.ini. Copy only this game's settings.
$customConfig = Join-Path $root 'Emulator\user\config\custom\00040000001B5100.ini'
if (Test-Path -LiteralPath $customConfig) {
    Copy-Item -LiteralPath $customConfig -Destination (Join-Path $destination 'ajustes-ultra-luna.ini')
}
# Native crashes often leave their faulting module in Windows instead of Azahar's truncated log.
try {
    $faults = @(Get-WinEvent -FilterHashtable @{
        LogName='Application'; Id=1000,1001,1002; StartTime=(Get-Date).AddDays(-3)
    } -ErrorAction Stop | Where-Object { $_.Message -match '(?i)azahar\.exe|PermaLocke(?:\.App)?\.exe' } |
        Select-Object -First 15 TimeCreated,Id,ProviderName,Message)
    ConvertTo-Json -InputObject $faults -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'cierres-windows.json') -Encoding utf8
} catch {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $destination 'cierres-windows-no-disponibles.txt') -Encoding utf8
}
foreach ($relative in @('PermaLocke.exe','Emulator\azahar.exe','Expansion\exefs\code.bin','Data\mapas.json','Data\marcadores.json')) {
    $file = Join-Path $root $relative
    if (Test-Path -LiteralPath $file) {
        $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
        ($relative + ': ' + $hash.Hash) | Add-Content -LiteralPath (Join-Path $destination 'versiones.txt')
    }
}
# The reader's address cache and save-file metadata help distinguish a fresh installation from
# an established one. No save contents or ROM are included.
foreach ($name in @('registros-de-posicion.txt','mochila.txt','equipo.txt')) {
    $cache = Join-Path $root ('Saves\backup\' + $name)
    if (Test-Path -LiteralPath $cache) {
        Copy-Item -LiteralPath $cache -Destination (Join-Path $destination ('lectura-' + $name))
    }
}
$sdmc = Join-Path $root 'Emulator\user\sdmc'
if (Test-Path -LiteralPath $sdmc) {
    $saves = @(Get-ChildItem -LiteralPath $sdmc -Filter 'main' -File -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object { [ordered]@{ Ruta = $_.FullName.Substring($root.Length); Bytes = $_.Length; ModificadoUtc = $_.LastWriteTimeUtc.ToString('o') } })
    ConvertTo-Json -InputObject $saves -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'guardados-solo-datos-del-fichero.json') -Encoding utf8
}
Compress-Archive -LiteralPath $destination -DestinationPath ($destination + '.zip')
Write-Host "Informe listo: $destination.zip"
Write-Host 'No contiene ROM ni partidas. No se ha enviado a nadie.'
