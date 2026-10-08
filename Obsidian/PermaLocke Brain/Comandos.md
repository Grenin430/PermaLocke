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
PERMALOCKE_SNAP_DIR="<dir>" dotnet test tests/PermaLocke.App.Tests --filter HomeViewTests   # ✔ PNG de los diálogos y avisos
PERMALOCKE_SNAP_DIR="<dir>" dotnet test tests/PermaLocke.App.Tests --filter CapPlaqueTests   # ✔ PNG de la placa del cap
PERMALOCKE_SNAP_LIVE="<dir>" dotnet test tests/PermaLocke.App.Tests --filter HomeViewTests  # ✔ tira de fotogramas del aviso en ventana real
# ✔ prototipos de avisos (A-Q) y del cap (S-Z) sobre fotogramas del juego. PROTO_ASSETS = carpeta con bg1.png, bg3.png
# (capturas de la pantalla del juego) e icon*.png; vivía en el scratchpad (notif/), hay que recrearla en otra sesión
PERMALOCKE_PIXEL_DIR="<dir>" PERMALOCKE_PROTO_ASSETS="<assets>" dotnet test tools/PermaLocke.PixelCheck --filter NotificationPrototypes
```

## Publicar y desplegar en la carpeta de prueba del usuario
```bash
powershell -File tools/desplegar.ps1 -Prueba   # ✔ comprueba procesos, publica y copia el exe; avisa si Data difiere
powershell -File tools/desplegar.ps1 -Amigos   # ✔ 2026-09-25. publicar.ps1 y la vieja pasa a "(anterior)"; no borra nada
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

## Versión nueva para todos (desde 2026-09-26)
Subir `<Version>` en `src/PermaLocke.App/PermaLocke.App.csproj` + `docs/novedades/<versión>.md` y PR a main: al aceptarlo
publica la Action ([[Actualizaciones y publicación]]). A mano, si hiciera falta (✔ 1.0.1 así, 2026-09-26):
```powershell
powershell -ExecutionPolicy Bypass -File tools/publicar-actualizacion.ps1 -Version 1.0.1   # .dist\PermaLocke-actualizacion-1.0.1.zip
# y en GitHub > Releases > Draft a new release: tag v1.0.1, zip en «Attach binaries», Publish
```

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

## Reiniciar una run del torneo (empezar de cero de verdad)
Dos pasos en dos sitios, y el segundo lo hace **cada jugador** en su PC (§191):
1. **Organizador, en Admin:** AUDITORÍA → REINICIAR sobre la run del jugador. En el servidor la run queda archivada
   (`reiniciar_run`, `runs.activa = false`, apuntado en `reinicios`); no se borra y se puede REACTIVAR desde «Ver
   archivadas». Esto **no toca el PC del jugador**.
2. **Jugador, en su app:** con Azahar cerrado, HOME → **EMPEZAR DE CERO** (dos confirmaciones; borra la run local y la
   partida de Ultra Luna, guardando antes una copia) → se abre NUEVA RUN, que el servidor ya acepta → en Azahar,
   partida nueva desde el principio.

Desde el §191 la app lo avisa sola: en cada lectura de amigos (60 s) mira `runs.activa` de su run; si está archivada,
aviso encima del juego una vez y panel rojo «TU RUN SE HA REINICIADO» en JUGAR. Nunca borra nada por su cuenta.
Visto el 2026-09-26: el usuario reinició las dos runs desde Admin y su app «no parecía reiniciada», justo por esto.
Lo que ven los demás sí cambia al momento: ACTIVIDAD, logros, amigos y clasificación salen de la vista `ultima_run`,
que solo mira runs activas, así que el historial de una run archivada deja de salir sin borrar nada (visto el mismo
día). En la base de datos sigue, para la auditoría; `subidas`, `reinicios`, `fantasmas` y `lluvias` también guardan
sus filas.

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
dotnet run --project tools/PermaLocke.RomTool -- variocolor           # ✔ Data/variocolor.json + hojas de revisión (§180)
```
- Captura de la app sin que tape otra ventana: `PrintWindow` sobre el `MainWindowHandle` (PowerShell con Add-Type), `--sin-juego --seccion "X" --tamano grande`; clics con `SetCursorPos` + `mouse_event`.

## Probar los SQL de Supabase en local (2026-10-08)
Postgres 17.6 portable en `C:\Users\javie\tools\pg17` (datos en `pg17\datos`, log en `pg17\log.txt`). Nunca contra el torneo.
```bash
/c/Users/javie/tools/pg17/pgsql/bin/pg_ctl.exe -D /c/Users/javie/tools/pg17/datos -o "-p 5433" -l /c/Users/javie/tools/pg17/log.txt start
PATH="/c/Users/javie/tools/pg17/pgsql/bin:$PATH" PGPORT=5433 PGHOST=localhost sh tools/supabase/pruebas/probar.sh
```
`probar.sh` crea y borra `permalocke_pruebas`, aplica 01..NN y ejecuta `pruebas/NN-*.sql`; cada «bien:» es una negativa
esperada y un «MAL» es un fallo. Pararlo: `pg_ctl.exe -D ... stop`.

## Admin sin tocar el torneo (§240)
- `PermaLocke.Admin.exe --demo`: 32 jugadores inventados en memoria (`DemoServer`); lo que se manda se queda ahí.
- `PermaLocke.Admin.exe --demo --capturas <carpeta>`: PNG de todas las páginas, la ficha pestaña a pestaña, varios
  marcados, vacío, ventana mínima y dos confirmaciones; se cierra solo. Sin `--demo` lee el torneo de verdad (solo lectura).

## MT repetidas en el suelo (§241)
```bash
dotnet run --project tools/PermaLocke.RomTool -- fielditems                       # el cartucho: lista MT repetidas
dotnet run --project tools/PermaLocke.RomTool -- fielditems --dir "<mundo>"       # un mundo instalado (Randomized/seed-...)
```
