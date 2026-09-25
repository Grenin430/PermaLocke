---
tipo: referencia
revisado: 2026-09-24
---
# Comandos

Desde la raíz del repo (`C:\Users\javie\Desktop\Permalocke definitivo`), salvo que se diga otra cosa. Los marcados ✔ se ejecutaron en la sesión del 2026-09-23/24; el resto sale de `CLAUDE.md` o de los scripts.

## Compilar y probar
```bash
dotnet build src/PermaLocke.App -v q        # ✔ compilar solo la app, rápido
dotnet build -v q                           # ✔ la solución entera
dotnet test --no-build -v q                 # ✔ 1521 correctas (2026-09-23)
dotnet test tests/PermaLocke.App.Tests --filter HomeViewTests   # ✔
PERMALOCKE_SNAP_DIR="<dir>" dotnet test tests/PermaLocke.App.Tests --filter HomeViewTests   # ✔ PNG de los diálogos
```

## Publicar y desplegar en la carpeta de prueba del usuario
```bash
powershell -File tools/desplegar.ps1 -Prueba   # ✔ comprueba procesos, publica y copia el exe; avisa si Data difiere
powershell -File tools/desplegar.ps1 -Amigos   # publicar.ps1 y la vieja pasa a "(anterior)"; no borra nada
```
A mano, lo mismo:
```bash
# ✔ exe único autocontenido para pruebas
dotnet publish src/PermaLocke.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -o <scratchpad>/pub-pixel
```
- **Antes de copiar**, comprobar que no corre nada (PowerShell):
  `Get-Process | Where {($_.ProcessName -like 'PermaLocke*' -or $_.ProcessName -like 'azahar*') -and $_.Threads.Count -gt 0}`
- Después copiar `pub-pixel/PermaLocke.App.exe` a `C:\Users\javie\Desktop\PermaLocke prueba\PermaLocke.exe` (✔).
- **La carpeta de amigos no se toca sin permiso.**

## Distribución completa (no ejecutado en esta sesión)
```powershell
powershell -File tools/publicar.ps1 [-Destino ruta] [-SinZip] [-VisualCppInstaller ruta]   # por defecto .dist\PERMALOCKEEEE, más su zip
powershell -File tools/empaquetar.ps1 -Carpeta <publicada> -Zip <zip>   # se niega si la carpeta ya se abrió
```
- La carpeta publicada lleva:
  - el exe autocontenido;
  - `Data/`, `Emulator/` (fork, VC++ y `qt-config.ini` nuevo) y la expansión con su README (licencia CC BY-NC-ND);
  - `PermaLocke.local`, `RECOGER DIAGNOSTICO.cmd`, `Soporte/contenido.json` (hashes) y `EVOLUCIONES CAMBIADAS.txt`.
- No lleva ROM, saves ni configuración personal. Ver `docs/DISTRIBUCION-LOCAL.md`.

## Ver la app sin tocar el juego
```text
PermaLocke.App.exe --sin-juego [--seccion "ENTRENAR EV"] [--tamano grande] [--hora-alola 20:30]
```
- Sin vigilante, sin mutex y sin copia de la BD.
- Flags de ensayo: `--ensayar-muerte`, `--ensayar-killcam`.

## Dar tiradas gratis al jugador (auditado)
```bash
# ✔ 2026-09-24. Cada llamada es una tirada y un AdminAdjustment con motivo. Con PermaLocke y Azahar cerrados.
PERMALOCKE_ROOT="C:\\Users\\javie\\Desktop\\PermaLocke prueba" dotnet run --no-build --project tools/PermaLocke.Probe -- --credito <pocho|decente|bueno|"intercambio N"> "motivo"
```
- Sin `PERMALOCKE_ROOT` actúa sobre la raíz del repo.
- El disponible es lo ganado menos lo gastado, **y puede estar en negativo**: mirar la salida.

```bash
dotnet run --project tools/PermaLocke.RomTool -- informacion   # ✔ regenera Data/informacion.json (sección INFORMACIÓN)
```

## Sondas y herramienta de ROM
- La lista completa, con ejemplos, está en la sección «Comandos» de `CLAUDE.md`. Las más usadas:
  - `dotnet run --project tools/PermaLocke.Probe -- --run`: auditoría de puntos, muertes, cap y PID.
  - `… -- --pids [--probar|--arreglar]`, `--etapas [n]`, `--intercambiados [--arreglar]`, `--ev [--probar]`, `--recordar [--probar]`, `--ruleta --probar`.
  - `… -- --huevo`: distingue un huevo de un Huevo Malo.
  - `… -- --objeto-find "nombre"`: id de un objeto por su nombre.
  - `dotnet run --project tools/PermaLocke.RomTool -- evo-dump "<mod>/romfs/a/0/1/4" [método]` (✔; sin método solo da el resumen por método).
  - `quien-lleva <especie> <a/1/0/7>`, `entrenador <id> <a/1/0/7>`, `iniciales [<mod>]`, `randomize <seed> [--install]`.
- Mundo instalado del usuario: `Desktop\PermaLocke prueba\Emulator\user\load\mods\00040000001B5100\romfs`. Es una carpeta externa al repo: solo se lee.

## Regenerar el mapa de código del cerebro
```bash
sh "Obsidian/PermaLocke Brain/_herramientas/regenerar-mapa.sh"   # ✔ desde la raíz del repo, con Git Bash
```
- Reescribe `Mapa de código/*.md` y usa `_herramientas/mapa.awk`: por cada fichero, los tipos más la primera frase del `<summary>`.
- Hacerlo tras añadir o borrar ficheros. Es barato: solo lee con grep y awk.

## Añadidos 2026-09-25
```bash
powershell -ExecutionPolicy Bypass -File tools/desplegar.ps1 -Prueba   # ✔ sin Bypass lo bloquea la política de scripts
dotnet run --project tools/PermaLocke.RomTool -- iconos-nombres        # ✔ CSV especie;nombre;icono en %TEMP% para revisar sprites
dotnet run --project tools/PermaLocke.RomTool -- megas                 # ✔ ahora imprime el id de cada megapiedra
dotnet run --project tools/PermaLocke.RomTool -- clases "<mod>/romfs/a/1/0/6"   # ✔ clase de un entrenador por nombre (Sina = 85)
```
- Captura de la app sin que tape otra ventana: `PrintWindow` sobre el `MainWindowHandle` (PowerShell con Add-Type), `--sin-juego --seccion "X" --tamano grande`; clics con `SetCursorPos` + `mouse_event`.
