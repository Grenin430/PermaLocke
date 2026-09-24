# Despliega PermaLocke en las carpetas del Escritorio, con las comprobaciones de siempre.
#
#   powershell -File tools/desplegar.ps1 -Prueba     exe nuevo en "PermaLocke prueba" (su Data no se toca)
#   powershell -File tools/desplegar.ps1 -Amigos     carpeta y zip nuevos en "PermaLocke para amigos"
#   powershell -File tools/desplegar.ps1 -Prueba -Amigos
#
# Se niega si PermaLocke o Azahar estan abiertos. Nunca borra nada: la carpeta de amigos que habia pasa a
# "PermaLocke para amigos (anterior)", y si esa ya existe se para y pide que la quites tu.
param(
    [switch]$Prueba,
    [switch]$Amigos
)

$ErrorActionPreference = 'Stop'

if (-not $Prueba -and -not $Amigos) { throw 'Di que desplegar: -Prueba, -Amigos o los dos.' }

$raiz = Split-Path -Parent $PSScriptRoot
$escritorio = [Environment]::GetFolderPath('Desktop')

$abiertos = Get-Process | Where-Object { ($_.ProcessName -like 'PermaLocke*' -or $_.ProcessName -like 'azahar*') -and $_.Threads.Count -gt 0 }
if ($abiertos) { throw "Cierra antes: $(($abiertos.ProcessName | Sort-Object -Unique) -join ', ')" }

if ($Prueba) {
    $carpetaPrueba = Join-Path $escritorio 'PermaLocke prueba'
    if (-not (Test-Path -LiteralPath $carpetaPrueba)) { throw "No existe $carpetaPrueba" }

    $salida = Join-Path ([IO.Path]::GetTempPath()) 'permalocke-despliegue'
    Write-Host 'Publicando la app...'
    dotnet publish (Join-Path $raiz 'src\PermaLocke.App') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -o $salida --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion.' }

    Copy-Item -LiteralPath (Join-Path $salida 'PermaLocke.App.exe') -Destination (Join-Path $carpetaPrueba 'PermaLocke.exe') -Force
    Write-Host "LISTO: exe nuevo en $carpetaPrueba"

    # Su Data es suyo: solo se avisa de lo que difiere del repo, para copiarlo a mano si hace falta.
    $distintos = Get-ChildItem -LiteralPath (Join-Path $raiz 'Data') -Filter '*.json' | Where-Object {
        $suyo = Join-Path $carpetaPrueba "Data\$($_.Name)"
        -not (Test-Path -LiteralPath $suyo) -or (Get-FileHash -LiteralPath $suyo).Hash -ne (Get-FileHash -LiteralPath $_.FullName).Hash
    }
    if ($distintos) { Write-Host "OJO, Data distinto del repo (no copiado): $(($distintos.Name) -join ', ')" }
}

if ($Amigos) {
    $carpetaAmigos = Join-Path $escritorio 'PermaLocke para amigos'
    $nueva = "$carpetaAmigos (nueva)"
    $anterior = "$carpetaAmigos (anterior)"

    foreach ($ocupado in @($nueva, "$nueva.zip", $anterior, "$anterior.zip")) {
        if (Test-Path -LiteralPath $ocupado) { throw "Ya existe $ocupado. Quitalo tu antes (este script no borra nada)." }
    }

    & (Join-Path $PSScriptRoot 'publicar.ps1') -Destino $nueva
    if (-not (Test-Path -LiteralPath "$nueva.zip")) { throw 'publicar.ps1 no dejo el zip.' }

    if (Test-Path -LiteralPath $carpetaAmigos) { Rename-Item -LiteralPath $carpetaAmigos -NewName (Split-Path -Leaf $anterior) }
    if (Test-Path -LiteralPath "$carpetaAmigos.zip") { Rename-Item -LiteralPath "$carpetaAmigos.zip" -NewName "$(Split-Path -Leaf $anterior).zip" }
    Rename-Item -LiteralPath $nueva -NewName (Split-Path -Leaf $carpetaAmigos)
    Rename-Item -LiteralPath "$nueva.zip" -NewName "$(Split-Path -Leaf $carpetaAmigos).zip"

    Write-Host "LISTO: $carpetaAmigos y su zip. La que habia queda como '(anterior)'."
}
