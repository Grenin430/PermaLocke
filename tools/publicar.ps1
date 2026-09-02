<#
    Construye la carpeta que se le pasa a otro jugador.

    Lo que sale es una carpeta con TRES cosas dentro: PermaLocke.exe, Data\ y Emulator\.
    Todo lo demas (ROM\, Saves\, Randomized\, Logs\, Config\) lo crea la propia aplicacion la
    primera vez que arranca, asi que quien la recibe no tiene que preparar nada.

    Lo que NO sale, y no es un olvido:

      ROM\          La ROM es de Nintendo. Cada jugador vuelca la suya del cartucho que tenga y
                    la deja en ROM\. Repartirla seria repartir el juego.
      Randomized\   Se genera a partir de la ROM, asi que arrastra lo mismo. Ademas cada run
                    randomiza con SU seed: la carpeta de otro no sirve de nada.
      Data\sprites\ Los iconos salen del cartucho. La aplicacion los extrae de la ROM de cada
                    jugador la primera vez que se abre el gacha.
      Saves\        La partida y la run son personales. Incluirlas seria darle a otro tu partida.

    islas\ SI sale, y es la excepcion: son las 57 fotos de zona que salen al pasar el raton por
    un marcador. Sin ellas la tarjeta funciona, pero con el nombre y sin imagen.
    Data\marcadores.json y Data\fotos.json viajan con ellas: donde va cada marcador y que foto le
    toca a cada zona. Eso se coloca UNA vez y lo tienen los cinco.

    Uso:  pwsh -File tools\publicar.ps1  [-Destino "ruta"]
#>

param(
    [string]$Destino = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'PermaLocke para amigos')
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot

# La aplicacion bloquea sus propias DLL mientras corre.
Get-Process -Name 'PermaLocke.App', 'PermaLocke' -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $Destino) { Remove-Item -Recurse -Force $Destino }

Write-Host "Publicando en $Destino ..."

# Un solo fichero y autocontenido: quien lo recibe no instala .NET ni nada.
#
# ExcludeFromSingleFile en el .csproj es lo que mantiene a Azahar FUERA del paquete. Sin eso el
# empaquetador se traga azahar.exe y sus DLL, el exe pasa de 162 MB a 267 MB, y la aplicacion
# busca Emulator\azahar.exe al lado suyo y no encuentra nada. Se veria solo en el ordenador de
# quien la recibe.
dotnet publish (Join-Path $raiz 'src\PermaLocke.App') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
    -o $Destino --nologo -v quiet

if ($LASTEXITCODE -ne 0) { throw "La publicacion ha fallado." }

Rename-Item (Join-Path $Destino 'PermaLocke.App.exe') 'PermaLocke.exe'
Copy-Item (Join-Path $raiz 'tools\LEEME-para-jugadores.txt') (Join-Path $Destino 'LEEME.txt')
Copy-Item (Join-Path $raiz 'tools\LEEME-competicion.txt') (Join-Path $Destino 'LEEME - COMPETICION.txt')
Copy-Item (Join-Path $raiz 'tools\LEEME-EXPANSION.txt') (Join-Path $Destino 'LEEME - GEN 8 Y 9.txt')
Copy-Item (Join-Path $raiz "LICENSE") $Destino

# Las fotos de cada zona del mapa. Van en el reparto a peticion del jugador: sin ellas la tarjeta
# al pasar el raton sale con el nombre y sin imagen, que funciona pero es la mitad de la idea.
# Se copian aqui y no desde el csproj para no duplicar 14 MB en cada compilacion de desarrollo.
$fotos = Join-Path $raiz "islas"
if (Test-Path $fotos) { Copy-Item $fotos $Destino -Recurse }

# Se comprueba lo que se acaba de escribir, no lo que se pretendia escribir.
$fallos = @()
if (-not (Test-Path (Join-Path $Destino 'PermaLocke.exe')))          { $fallos += 'falta PermaLocke.exe' }
if (-not (Test-Path (Join-Path $Destino 'Emulator\azahar.exe')))     { $fallos += 'falta Emulator\azahar.exe' }
if (-not (Test-Path (Join-Path $Destino 'LEEME - COMPETICION.txt'))) { $fallos += 'falta la guia de la competicion' }
if (Test-Path (Join-Path $Destino 'Data\sprites'))                   { $fallos += 'se ha colado Data\sprites (son de Nintendo)' }
if (Test-Path (Join-Path $Destino 'Saves'))                          { $fallos += 'se ha colado Saves (es tu partida)' }
if (Test-Path (Join-Path $Destino 'ROM'))                            { $fallos += 'se ha colado ROM' }
# El mod de expansion son 2,5 GB de ficheros del juego: se lo baja cada uno, no se reparte.
if (Test-Path (Join-Path $Destino 'Expansion'))                      { $fallos += 'se ha colado Expansion (2,5 GB de ficheros del juego)' }
if (-not (Test-Path (Join-Path $Destino "LEEME - GEN 8 Y 9.txt")))   { $fallos += "falta la guia de la expansion" }

# El mapa se apoya en dos ficheros que SI viajan: donde va cada marcador y que foto le toca. Sin
# el primero la pantalla se queda en el tablero numerado; sin las fotos, las tarjetas salen vacias.
if (-not (Test-Path (Join-Path $Destino 'Data/marcadores.json'))) { $fallos += 'falta marcadores.json: el mapa saldria sin marcadores' }
if (-not (Test-Path (Join-Path $Destino 'Data/fotos.json')))      { $fallos += 'falta fotos.json' }

$fotosPublicadas = if (Test-Path (Join-Path $Destino "islas")) { (Get-ChildItem (Join-Path $Destino "islas") -Recurse -Filter *.png).Count } else { 0 }
if ($fotosPublicadas -lt 57) { $fallos += "solo hay $fotosPublicadas fotos de zona, esperaba 57" }

$jsons = (Get-ChildItem (Join-Path $Destino 'Data') -Filter *.json).Count
if ($jsons -lt 14) { $fallos += "solo hay $jsons ficheros de configuracion en Data, esperaba 14" }

if ($fallos.Count -gt 0) { throw ("La carpeta publicada NO esta bien: " + ($fallos -join '; ')) }

$mb = [math]::Round((Get-ChildItem $Destino -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Write-Host ""
Write-Host "Listo: $Destino  ($mb MB, $jsons ficheros de configuracion, $fotosPublicadas fotos de zona, emulador incluido)"
Write-Host "Comprimela y pasala. Quien la reciba solo tiene que dejar su ROM en ROM\."
