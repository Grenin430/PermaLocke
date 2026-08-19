# Emulator/ — el Azahar que PermaLocke reparte

Esta carpeta lleva un **fork propio de Azahar**, el emulador de 3DS. PermaLocke lo prefiere
sobre cualquier Azahar que el jugador tenga instalado, y lo arranca en **modo portátil**: al
existir `Emulator/user/`, el emulador guarda ahí su configuración, sus mods y sus partidas, y no
toca la instalación que el jugador ya tuviera.

PermaLocke le activa solo el servidor RPC, que Azahar trae apagado de fábrica y que solo puede
cambiarse con la emulación detenida. Así ningún jugador tiene que tocar un ajuste.

## Por qué un fork y no el Azahar oficial

Dos parches, los dos imprescindibles:

| Parche | Para qué |
|---|---|
| `NEW_LINEAR_HEAP` en la lista blanca de `HandleWriteMemory` | El Azahar oficial **se niega a escribir** en la región donde vive todo el estado del juego —equipo y mochila— y **responde OK igualmente**. Sin esto no hay Shedinja, ni cap de nivel, ni retirar Poké Balls, y encima la app creería que funcionan |
| Tipo de paquete RPC `SearchMemory` (5) | Busca en memoria dentro del emulador. 96 MB en 58 ms en vez de 6 s |

El primero está explicado en `docs/ARCHITECTURE.md` §15-16 y el segundo en `docs/fork/01-search-memory.md`.

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
