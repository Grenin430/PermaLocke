<# Creates a fresh, standalone distribution. Never deletes a destination or stops the player's app. #>
param(
    # Sin ella, .dist\PERMALOCKEEEE del repo (se calcula abajo: en Windows PowerShell 5.1 $PSScriptRoot esta vacio aqui).
    [string]$Destino,
    [string]$VisualCppInstaller,
    # Sin esto, al final se deja tambien un zip de la carpeta al lado, listo para compartir.
    [switch]$SinZip,
    # La version que llevara el programa (seccion 196). Sin ella, la del csproj de la app (<Version>), que es la que
    # hay que subir con cada release para que la carpeta de amigos y la actualizacion digan lo mismo.
    [string]$Version
)
$ErrorActionPreference = 'Stop'
# [IO.Path]::GetRelativePath no existe en el PowerShell de Windows (5.1), que es el que trae todo Windows.
function Relative([string]$base, [string]$full) { $full.Substring($base.TrimEnd('\').Length + 1) }
$raiz = Split-Path -Parent $PSScriptRoot
if (-not $Destino) { $Destino = Join-Path $raiz '.dist\PERMALOCKEEEE' }
$Destino = [IO.Path]::GetFullPath($Destino)
if (Test-Path -LiteralPath $Destino) { throw "El destino ya existe. Elige una carpeta nueva: $Destino" }
foreach ($required in @('Emulator\azahar.exe','Expansion\romfs\a\0\9\4','Expansion\exefs\code.bin','Expansion\README.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $raiz $required))) { throw "Falta $required" }
}
if ($VisualCppInstaller) {
    $signature = Get-AuthenticodeSignature -LiteralPath $VisualCppInstaller
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'El instalador de Visual C++ no tiene una firma valida de Microsoft.'
    }
}
Write-Host "Publicando version local autocontenida en $Destino"
dotnet publish (Join-Path $raiz 'src\PermaLocke.App') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none $(if ($Version) { "-p:Version=$Version" }) -o $Destino --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'La publicacion ha fallado.' }
Rename-Item -LiteralPath (Join-Path $Destino 'PermaLocke.App.exe') -NewName 'PermaLocke.exe'
Set-Content -LiteralPath (Join-Path $Destino 'PermaLocke.local') -Value 'Distribucion local independiente. Mantener junto a PermaLocke.exe.' -Encoding utf8
Copy-Item -LiteralPath (Join-Path $raiz 'Expansion') -Destination $Destino -Recurse
Copy-Item -LiteralPath (Join-Path $raiz 'Islas') -Destination $Destino -Recurse
Copy-Item -LiteralPath (Join-Path $raiz 'LICENSE') -Destination $Destino
# Para jugadores: como evoluciona ahora lo que pedia intercambio o un movimiento (seccion 151 de la documentacion).
Copy-Item -LiteralPath (Join-Path $raiz 'EVOLUCIONES CAMBIADAS.txt') -Destination $Destino
Copy-Item -LiteralPath (Join-Path $raiz 'Tiendas especiales.txt') -Destination (Join-Path $Destino 'TIENDAS.txt')
Copy-Item -LiteralPath (Join-Path $raiz 'tools\LEEME-local.txt') -Destination (Join-Path $Destino 'EMPIEZA AQUI.txt')
Copy-Item -LiteralPath (Join-Path $raiz 'tools\ACTUALIZAR-local.txt') -Destination (Join-Path $Destino 'ACTUALIZAR DESDE LA VERSION ANTERIOR.txt')
foreach ($folder in @('ROM','Config','Soporte','Emulator\user\config')) {
    New-Item -ItemType Directory -Path (Join-Path $Destino $folder) -Force | Out-Null
}
Set-Content -LiteralPath (Join-Path $Destino 'ROM\PON AQUI TU ROM.txt') -Value 'Pon aqui tu ROM desencriptada de Pokemon Ultra Luna (.3ds o .cci). No se incluye en el paquete.' -Encoding utf8
# Settings for a NEW player. No GPU index, controller GUID, save, shader cache or personal path is copied.
# Sin BOM: el PowerShell de Windows (5.1) lo pone con -Encoding utf8, y es Azahar quien lee este fichero.
$qtConfig = @"
[Renderer]
resolution_factor=1
resolution_factor\default=false
[Debugging]
enable_rpc_server=true
enable_rpc_server\default=false
[UI]
confirmClose=false
confirmClose\default=false
"@
[IO.File]::WriteAllText((Join-Path $Destino 'Emulator\user\config\qt-config.ini'), $qtConfig.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
# Visual C++ junto al emulador (seccion 168). azahar.exe se compila con el Visual Studio mas nuevo, y lo compilado con
# la 14.40 o posterior se cierra con un runtime anterior en cuanto usa un cerrojo: fallo documentado por Microsoft. Un
# amigo veia Azahar cerrarse; con un runtime anterior, lo normal si solo se lo instalaron juegos viejos, es justo lo que
# pasa. En su PC no se ha podido comprobar.
# Windows busca primero en la carpeta del programa y ninguna de estas es KnownDLL, asi que al lado del emulador
# mandan las nuestras: medido, las seis se cargan desde Emulator\ y no desde System32. Microsoft permite
# distribuirlas asi (despliegue local de la aplicacion).
$runtime = @('msvcp140.dll','msvcp140_1.dll','msvcp140_2.dll','msvcp140_atomic_wait.dll','vcruntime140.dll','vcruntime140_1.dll')
$emulatorDir = Join-Path $Destino 'Emulator'
function LinkerVersion([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    return [version]::new($bytes[$pe + 26], $bytes[$pe + 27])
}
$needed = [version]'14.0'
foreach ($binary in Get-ChildItem -LiteralPath $emulatorDir -Recurse -File | Where-Object { $_.Extension -in '.exe','.dll' -and $runtime -notcontains $_.Name.ToLower() }) {
    $linker = LinkerVersion $binary.FullName
    if ($linker.Major -eq 14 -and $linker -gt $needed) { $needed = $linker }
}
foreach ($name in $runtime) {
    $source = Join-Path $env:WINDIR "System32\$name"
    if (-not (Test-Path -LiteralPath $source)) { throw "Falta $name en este PC: instala el Visual C++ 2015-2022 x64 mas reciente antes de publicar." }
    $signature = Get-AuthenticodeSignature -LiteralPath $source
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "$name no tiene una firma valida de Microsoft."
    }
    $info = (Get-Item -LiteralPath $source).VersionInfo
    $version = [version]::new($info.FileMajorPart, $info.FileMinorPart)
    if ($version -lt $needed) {
        throw "$($name) es la $($version) y el emulador se compilo con la $($needed): actualiza el Visual C++ de este PC antes de publicar."
    }
    Copy-Item -LiteralPath $source -Destination $emulatorDir
}
Write-Host "Visual C++ $needed o posterior junto al emulador ($($runtime.Count) DLL)"
Copy-Item -LiteralPath (Join-Path $raiz 'tools\recoger-diagnostico.ps1') -Destination (Join-Path $Destino 'Soporte')
@"
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Soporte\recoger-diagnostico.ps1"
pause
"@ | Set-Content -LiteralPath (Join-Path $Destino 'RECOGER DIAGNOSTICO.cmd') -Encoding ascii
if ($VisualCppInstaller) {
    Copy-Item -LiteralPath $VisualCppInstaller -Destination (Join-Path $Destino 'Soporte\Instalar Visual C++ x64.exe')
}
# Include the exact corresponding application sources, including existing uncommitted work.
Add-Type -AssemblyName System.IO.Compression
# ZipFile vive en otro ensamblado en el PowerShell de Windows (5.1); en el 7 ya viene cargado.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceZip = [IO.Compression.ZipFile]::Open((Join-Path $Destino 'Soporte\Codigo fuente.zip'), [IO.Compression.ZipArchiveMode]::Create)
try {
    $sources = @()
    foreach ($folder in @('src','tests','third_party','tools')) {
        # Sin Admin ni los SQL del servidor: son solo del organizador y no se reparten, asi que la GPL no los pide.
        $sources += Get-ChildItem -LiteralPath (Join-Path $raiz $folder) -Recurse -File | Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' -and $_.FullName -notmatch '[\\/](PermaLocke\.Admin|supabase)[\\/]'
        }
    }
    $sources += Get-ChildItem -LiteralPath (Join-Path $raiz 'Data') -Filter '*.json' -File
    $sources += @('README.md','LICENSE','PermaLocke.slnx','.gitignore','docs/DISTRIBUCION-LOCAL.md') | ForEach-Object { Get-Item -LiteralPath (Join-Path $raiz $_) }
    foreach ($file in $sources) {
        $relative = (Relative $raiz $file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $sourceZip.Dispose() }
$requiredFiles = @('PermaLocke.exe','PermaLocke.local','EVOLUCIONES CAMBIADAS.txt','TIENDAS.txt','Emulator\azahar.exe','Emulator\plugins\platforms\qwindows.dll','Emulator\follower\Gen7FieldFollower.3gx','Emulator\follower\LEEME.txt','Expansion\exefs\code.bin','Expansion\romfs\a\0\9\4','Expansion\README.txt','Data\mapas.json','Data\marcadores.json','Data\fotos.json') + @($runtime | ForEach-Object { "Emulator\$_" })
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destino $file))) { throw "Falta en la distribucion: $file" }
}
foreach ($private in @('Saves','Logs','Randomized','Data\sprites','Config\sync.json','Config\jugador.json','Emulator\user\sdmc','Emulator\user\nand','Emulator\user\load','Emulator\user\shader')) {
    if (Test-Path -LiteralPath (Join-Path $Destino $private)) { throw "Se ha incluido informacion personal: $private" }
}
$manifest = @(Get-ChildItem -LiteralPath $Destino -Recurse -File | ForEach-Object {
    [ordered]@{ file = (Relative $Destino $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
ConvertTo-Json -InputObject $manifest -Depth 4 | Set-Content -LiteralPath (Join-Path $Destino 'Soporte\contenido.json') -Encoding utf8
$totalBytes = 0L
foreach ($entry in $manifest) { $totalBytes += $entry.bytes }
$mb = [math]::Round($totalBytes / 1MB)
Write-Host "LISTO: $Destino ($mb MB). .NET incluido, expansion incluida, sin datos del jugador."
# Un solo fichero para compartir (seccion 172). Drive parte en varios zips cualquier carpeta de mas de 2 GB, y quien
# extrae solo uno se queda sin la mitad de los ficheros; con un zip es una descarga y un "Extraer todo".
if (-not $SinZip) {
    & (Join-Path $PSScriptRoot 'empaquetar.ps1') -Carpeta $Destino
}
