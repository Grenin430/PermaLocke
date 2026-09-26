<# El paquete de una actualizacion automatica (seccion 196): PermaLocke.exe y Data\*.json, nada mas.
   Se sube a una release de GitHub con la etiqueta v<Version>; las apps repartidas la ven al abrirse y se actualizan.

     powershell -ExecutionPolicy Bypass -File tools/publicar-actualizacion.ps1 -Version 1.1.0

   Deja PermaLocke-actualizacion-<Version>.zip en .dist\ (o en -Destino). Luego, en GitHub > Releases > Draft a new
   release: etiqueta v<Version>, titulo, notas (las ve el jugador al preguntarle), adjuntar el zip y Publish. Con la CLI:
     gh release create v<Version> .dist\PermaLocke-actualizacion-<Version>.zip --title "PermaLocke <Version>" --notes "..."
   El repositorio tiene que ser publico para que las apps lo lean sin contrasena.
   Nunca toca las carpetas del Escritorio ni la partida de nadie. #>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Destino = (Join-Path (Split-Path -Parent $PSScriptRoot) '.dist')
)
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'La version va como 1.2.3.' }

$raiz = Split-Path -Parent $PSScriptRoot
$zip = Join-Path $Destino "PermaLocke-actualizacion-$Version.zip"
if (Test-Path -LiteralPath $zip) { throw "Ya existe $zip" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) "permalocke-actualizacion-$Version"
if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $tmp 'Data') -Force | Out-Null

Write-Host "Publicando PermaLocke $Version..."
dotnet publish (Join-Path $raiz 'src\PermaLocke.App') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none "-p:Version=$Version" -o (Join-Path $tmp 'publish') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'La publicacion ha fallado.' }

Copy-Item -LiteralPath (Join-Path $tmp 'publish\PermaLocke.App.exe') -Destination (Join-Path $tmp 'PermaLocke.exe')
Remove-Item -LiteralPath (Join-Path $tmp 'publish') -Recurse -Force
Get-ChildItem -LiteralPath (Join-Path $raiz 'Data') -Filter '*.json' -File | Copy-Item -Destination (Join-Path $tmp 'Data')

New-Item -ItemType Directory -Path $Destino -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($tmp, $zip)
Remove-Item -LiteralPath $tmp -Recurse -Force

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
Write-Host "LISTO: $zip"
Write-Host "sha256: $hash"
Write-Host "Ahora: release v$Version en GitHub con ese zip adjunto (ver la cabecera de este script)."
