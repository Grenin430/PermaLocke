<# Packs a published distribution into a single zip for sharing, after checking it is exactly what was published.
   Never overwrites a zip and never zips a folder somebody has opened: that is how a partida would travel. #>
param(
    [Parameter(Mandatory = $true)] [string]$Carpeta,
    [string]$Zip,
    # La carpeta que aparece al extraer. Con ella dentro, "Extraer todo" en C:\Juegos deja C:\Juegos\PermaLocke.
    [string]$NombreDentro = 'PermaLocke'
)
$ErrorActionPreference = 'Stop'
# [IO.Path]::GetRelativePath no existe en el PowerShell de Windows (5.1), que es el que trae todo Windows.
function Relative([string]$base, [string]$full) { $full.Substring($base.TrimEnd('\').Length + 1) }

$Carpeta = [IO.Path]::GetFullPath($Carpeta)
if (-not $Zip) { $Zip = "$Carpeta.zip" }
$Zip = [IO.Path]::GetFullPath($Zip)
if (Test-Path -LiteralPath $Zip) { throw "El zip ya existe. Elige otro nombre: $Zip" }

# Solo se empaqueta lo que publicar.ps1 dejo tal cual: cada fichero con el tamano y la huella que apunto en
# Soporte\contenido.json, ni uno de mas ni uno de menos. Abrir la carpeta crea Saves, Logs y Config\jugador.json, y
# un zip hecho de ahi le daria a un amigo la run y la partida de otro.
$manifestPath = Join-Path $Carpeta 'Soporte\contenido.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "No es una carpeta publicada: falta Soporte\contenido.json en $Carpeta" }
$expected = @{}
foreach ($entry in (Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json)) { $expected[$entry.file] = $entry }

$files = @(Get-ChildItem -LiteralPath $Carpeta -Recurse -File -Force)
$seen = @{}
foreach ($file in $files) {
    $relative = Relative $Carpeta $file.FullName
    if ($relative -eq 'Soporte\contenido.json') { continue }
    if (-not $expected.ContainsKey($relative)) {
        throw "Sobra $relative : la carpeta se ha abierto o tocado despues de publicarla. Publica una nueva con publicar.ps1."
    }
    $entry = $expected[$relative]
    if ($file.Length -ne [long]$entry.bytes -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "Ha cambiado $relative desde que se publico. Publica una nueva con publicar.ps1."
    }
    $seen[$relative] = $true
}
foreach ($relative in $expected.Keys) {
    if (-not $seen.ContainsKey($relative)) { throw "Falta $relative, que estaba al publicar." }
}

Add-Type -AssemblyName System.IO.Compression
# ZipFile vive en otro ensamblado en el PowerShell de Windows (5.1); en el 7 ya viene cargado.
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Se escribe a un temporal y se mueve al final: un zip cortado a medias con el nombre bueno se subiria igual.
$partial = "$Zip.parcial"
if (Test-Path -LiteralPath $partial) { throw "Queda un zip a medias de otra vez: $partial" }
Write-Host "Empaquetando $($files.Count) ficheros en $Zip"
$archive = [IO.Compression.ZipFile]::Open($partial, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $name = "$NombreDentro/" + (Relative $Carpeta $file.FullName).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }

# Se relee el zip antes de darlo por bueno: los mismos ficheros, con los mismos tamanos, y lo imprescindible dentro.
$check = [IO.Compression.ZipFile]::OpenRead($partial)
try {
    $inside = @{}
    foreach ($entry in $check.Entries) { $inside[$entry.FullName] = $entry.Length }
    if ($inside.Count -ne $files.Count) { throw "El zip tiene $($inside.Count) ficheros y la carpeta $($files.Count)." }
    foreach ($file in $files) {
        $name = "$NombreDentro/" + (Relative $Carpeta $file.FullName).Replace('\', '/')
        if (-not $inside.ContainsKey($name) -or $inside[$name] -ne $file.Length) { throw "En el zip no cuadra $name" }
    }
    foreach ($required in @('PermaLocke.exe', 'PermaLocke.local', 'EMPIEZA AQUI.txt', 'Emulator/azahar.exe', 'Expansion/exefs/code.bin')) {
        if (-not $inside.ContainsKey("$NombreDentro/$required")) { throw "Falta en el zip: $required" }
    }
} finally { $check.Dispose() }

Move-Item -LiteralPath $partial -Destination $Zip
$mb = [math]::Round((Get-Item -LiteralPath $Zip).Length / 1MB)
Write-Host "ZIP LISTO: $Zip ($mb MB). Dentro va la carpeta $($NombreDentro), que se extrae con 'Extraer todo' en C:\Juegos."
