# Emulator/ — el Azahar que PermaLocke reparte

Esta carpeta lleva un **fork propio de Azahar**, el emulador de 3DS. PermaLocke lo prefiere
sobre cualquier Azahar que el jugador tenga instalado, y lo arranca en **modo portátil**: al
existir `Emulator/user/`, el emulador guarda ahí su configuración, sus mods y sus partidas, y no
toca la instalación que el jugador ya tuviera.

PermaLocke le activa solo el servidor RPC, que Azahar trae apagado de fábrica y que solo puede
cambiarse con la emulación detenida. Así ningún jugador tiene que tocar un ajuste.

## Por qué un fork y no el Azahar oficial

Los parches que PermaLocke necesita:

| Parche | Para qué |
|---|---|
| `NEW_LINEAR_HEAP` en la lista blanca de `HandleWriteMemory` | El Azahar oficial **se niega a escribir** en la región donde vive todo el estado del juego —equipo y mochila— y **responde OK igualmente**. Sin esto no hay Shedinja, ni cap de nivel, ni retirar Poké Balls, y encima la app creería que funcionan |
| Tipo de paquete RPC `SearchMemory` (5) | Busca en memoria dentro del emulador. 96 MB en 58 ms en vez de 6 s |
| Lecturas del RPC sin volcar la caché de la GPU (`ReadBlockNoFlush`) | Las lecturas y búsquedas del RPC volcaban la caché de la GPU desde su propio hilo, y eso **cerraba o congelaba Azahar** en mitad de una búsqueda de 64 MB, con OpenGL. Era lo que tumbaba el emulador a un amigo |

El primero está explicado en `docs/ARCHITECTURE.md` §15-16, el segundo en `docs/fork/01-search-memory.md` y el
último en `docs/fork/04-leer-sin-volcar.md`. Los binarios de esta carpeta salen del commit **`0dfe782`**
(2026-09-23), que los lleva todos.

## Licencia: GPLv3, y dónde está el fuente

Azahar es **GPLv3**, así que repartir este binario obliga a ofrecer su código fuente. Está aquí:

**https://github.com/Grenin430/azahar**

Ese repositorio contiene el fork completo, con los parches y su historial. Los binarios de esta
carpeta salen de su compilación en GitHub Actions, job `windows (msvc)`.

La licencia de Azahar es la suya y es independiente de la de PermaLocke, que también es GPLv3
pero por otro motivo (PKHeX.Core y pk3DS.Core). Ver `LICENSE` en la raíz.

## Cómo se repuebla esta carpeta

Los binarios **no están en el repositorio de PermaLocke**: son 100 MB que se reconstruyen desde
su propio repositorio, y meter binarios en git no aporta nada. Para dejarla lista:

1. Descargar el artefacto de `windows (msvc)` de GitHub Actions en `Grenin430/azahar`.
2. Descomprimirlo dentro de esta carpeta, de modo que `Emulator/azahar.exe` exista.

`PermaLocke.App` la copia entera al publicar, así que basta con tenerla poblada antes de
ejecutar `dotnet publish`. Si falta, la app **no falla**: avisa por el log y usa el Azahar que el
jugador tenga instalado, con la salvedad de que entonces las escrituras no surtirán efecto.

## El Visual C++ va con el emulador

El artefacto de GitHub Actions se compila con el Visual Studio más nuevo del servidor (herramientas 14.51 en el de
septiembre de 2026), y lo compilado con la 14.40 o posterior **se cierra** si carga un `msvcp140.dll` anterior, en
cuanto usa un cerrojo: fallo documentado por Microsoft. Con uno que ni siquiera tenga `msvcp140_atomic_wait.dll`, no
arranca. Así que `tools/publicar.ps1` copia al lado de `azahar.exe` las seis DLL del runtime —`msvcp140`, `_1`, `_2`,
`_atomic_wait`, `vcruntime140` y `_1`— desde el `System32` del PC que publica, comprobando que estén firmadas por
Microsoft y que su versión no sea menor que la del compilador de **ningún** binario de esta carpeta. Windows busca
primero en la carpeta del programa y ninguna es KnownDLL: medido, las seis se cargan desde aquí.

Microsoft permite distribuir estas DLL junto a la aplicación (despliegue local). No hacen falta en esta carpeta del
repositorio: las pone el script al publicar.
