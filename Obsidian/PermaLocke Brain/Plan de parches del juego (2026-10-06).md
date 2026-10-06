---
tipo: plan
revisado: 2026-10-06
---
# Plan de parches del juego (2026-10-06)

Pedido por el organizador: meter las reglas del Nuzlocke **dentro del juego** tocando su código. Aprobado el plan y las
descargas (Ghidra 12.1.4 en `C:\Users\javie\tools`, fuera del repo; Keystone/Capstone por NuGet para el repo).
Torneo en curso: todo en el repo, nada desplegado ni publicado hasta que lo diga. Revisor antes de publicar.

## Qué se hace
1. **Buzón de eventos**: el juego escribe «combate en mapa X», «captura», «debilitado» en un sitio fijo que la app lee por
   RPC. Quita la detección frágil («no se identifica la ruta») y los barridos de memoria.
2. **Los caídos no se curan**: Centro Pokémon, cama y enfermeras se saltan a los marcados como caídos.
3. **Cap de nivel**: 0 de experiencia al nivel del cap. **3b: Caramelo Raro** «no tendría ningún efecto» si el nivel ya es el del cap.
4. **Zona gastada sin quitar balls**: la ball da el mensaje de combate contra entrenador; **un salvaje variocolor sí se puede capturar**.
5. **Cláusula de duplicados**: el salvaje se vuelve a tirar si su familia ya está en la run; si todas son repetidas, se deja.

Lo que hace hoy la app (retirar balls, volver a tumbar caídos, corregir niveles) se queda como red hasta probarlo jugando.

## Diseño
- **Bloque PermaLocke** en RAM fija (cueva de código/datos libre): la app escribe cap, zona gastada y mapa de familias
  (1025 especies → 128 bytes); el juego escribe el buzón (1). Los caídos llevan ya su marca en la partida (`DeathMarked`).
- `code.bin` del mod (Expansion USUM, base **USA 1.0**) lo reescribe además el randomizador por seed (MT, tutores): los
  parches se localizan **por patrón exacto y único** en el `code.bin` instalado, como `AlwaysShinyPatch`, y van en
  `exefs/code.ips` (Azahar lo aplica al arrancar). Sin patrón único, no se aplica.
- Combate y mochila viven en `.cro` (`Battle.cro`, `Bag.cro`, del mod); los de campo, del cartucho base. Parchear un
  `.cro` = sustituirlo en el romfs del mod (el mod ya lo hace con los suyos).
- Pruebas: siempre en copias; nunca en la partida real.

## Estado
- 2026-10-06: descargando Ghidra; `code.bin` y `.cro` copiados al scratchpad para analizar. Piloto: 3 y 3b.
- 2026-10-06: **3 y 3b hechos en código, sin probar en el juego.** `Randomizer/Rom/RulePatches.cs`:
  - Combate: `Battle.cro` 0x91F1C (`FUN_00091f00`, añadir experiencia del Pokémon de combate; llama a `SetExp` y `GetLevel`):
    `cmp r0,#0x64` → `bl` a la rutina «compara con el cap» en la cola libre 0x11A700 (sin reubicaciones ni referencias).
  - Caramelo Raro: `code.bin` 0x4410E8 (dentro de `FUN_00440f7c`, que llama `PokeTool::ITEM_RCV_RecoverCheck`; parámetro
    0x1E del objeto = sube nivel): `cmp r0,#0x64` → `bl` a la misma rutina en 0x5B9A80 (por `code.ips`). 0x5B99F8 se deja:
    un descriptor en datos (0x69CAD0) apunta al fin del código original.
  - Rutina: lee el cap de `RuleBlock.Cap` (0x6D3F14); 0 o >100 = 100. Bloque en el hueco escribible tras el BSS
    (0x6D3F04-0x6D4000), nadie lo usa (comprobado en code.bin y en los .cro del mod).
  - **Descartado parchear `SetExp`** (0x325A5C, limita a `GetMinExp(100)`): también crea Pokémon y bajaría a los rivales.
  - App: `rules.json` `gameRulePatches` (false por defecto) → `EmulatorLauncher` aplica/quita al pulsar JUGAR;
    `GameLinkMonitor.TellGameTheCap` escribe el cap (al cambiar y cada 10 s). Sonda: `--reglas-juego [quitar] [carpeta]`,
    `--reglas-juego cap N` (escribe el cap en el juego abierto).
  - Pruebas: `RulePatchesTests` (Capstone desensambla las instrucciones hechas a mano; parche/desparche sobre los ficheros
    del mod; ciclo completo en carpeta temporal conservando un `code.ips` ajeno).
  - Por ver jugando: que Azahar acepte el `Battle.cro` cambiado (el mod ya cambia los suyos sin tocar el CRR), y si al cap
    sigue saliendo «ganó N puntos de experiencia».
- Herramientas: Ghidra en `C:\Users\javie\tools\ghidra_12.1.4_PUBLIC`; proyectos y scripts (FindImm, Dis, DisAll, Decomp,
  Ctx) en el scratchpad de la sesión (`re/`). Nombres de funciones: tablas de importación de ZiouraS2/usum-re
  (offsets = offset en el fichero; dirección = +0x100000). Llamadas de un .cro: `bl` a un trampolín `ldr pc,[x]` 4 bytes
  antes de la «cro call address» de la tabla.
- Siguiente: 2, 4, 5 y 1 con el registro de escrituras del fork (`AzaharRpcClient.WatchWrites`): jugando el organizador,
  se mira quién escribe los PS de un caído al curar (2), la especie del salvaje (5) y los momentos del buzón (1). El 4 (ball
  en zona gastada) necesita la comprobación de «no se puede usar» de `Battle.cro`; aún sin localizar.
- 2026-10-06: **piloto probado por el organizador: funciona.** Al cap sale «X ha recibido N de experiencia» pero la barra
  no sube; él lo da por bueno, no se toca.
- **Decisión del organizador: ir limpiando.** Lo que la app hacía desde fuera solo para una regla que ahora hace el juego
  se quita. Primero, al encender `gameRulePatches` para todos: fuera la corrección del cap en memoria
  (`GameLinkMonitor.EnforceLevelCapAsync` escribiendo, `AzaharGameWriter.EnforceLevelCap`/`NeedsCapping`, `_cappedAt`);
  queda solo un aviso «X pasa del cap» sin escribir nada, por las vías que el parche no cubre (isla de entrenamiento del
  Poké Resort, regalos/intercambios de nivel alto, lo que da el Admin). Igual con 2, 4 y 5 cuando estén: fuera el volver a
  tumbar caídos, la retirada de balls y la cláusula de duplicados de la app, si solo servían para eso.
- 2026-10-06: **2 hecho y probado por el organizador** (Centro dos veces, Revivir desde la mochila: sigue a 0 PS).
  `code.bin` 0x322644 (`mov r5,r1` en la función por la que pasa todo guardado de PS de un Pokémon, 0x322638, llamada
  desde `CoreParam::SetHp`) → `bl` a `KeepFallenDown` (0x5B9AA4): si la constante de cifrado del Pokémon está en
  `RuleBlock.Fallen` (6 u32 en 0x6D3F30), guarda 0. App: `GameLinkMonitor.TellGameTheFallen` (desde
  `KeepFallenDownCoreAsync`, ECs de los caídos del equipo, al cambiar y cada 10 s). Con `gameRulePatches` ya **no sale el
  aviso «X sigue caído»** (pedido del organizador); el volver a tumbar queda en silencio, solo en el log, para el caído que
  llega curado antes que la lista (sacado de la caja y curado en el mismo segundo). Pendiente: cama/NPC no probados (misma
  función); un Revivir en un caído se gasta sin efecto (se podría rechazar como el Caramelo Raro).
- 2026-10-06: **5 hecho y probado por el organizador** (Ruta 2, todo repetido menos Honedge: solo salían Honedge).
  `code.bin` 0x3A7064 (`add r6,r4,r0,lsl #2` en la elección de hueco salvaje 0x3A6FCC, llamada por
  `PokeSet::SetNormalEncountData`; r0 = hueco tirado, r4 = tabla: cabecera de 0xC con las 10 tasas en los bytes 2-11) →
  `bl` a `RerollDupes` (0x5B9A00, en el margen tras el fin del código del cartucho, como las otras) con su ayudante
  `DupeTest` (0x5B9AE8). Si la especie del hueco está en `RuleBlock.Dupes` (bitset de 0x88 bytes en 0x6D3F50, especies
  < 1088; bit 0 = hueco vacío, puesto siempre que hay alguno), vuelve a tirar con `rand(n)` (0x3FBF68) solo entre los
  huecos no repetidos, con sus tasas; si todos lo son, se queda. Solo los 10 huecos normales (hierba, cueva, surf...):
  SOS, estáticos y regalos no pasan por aquí. App: `EncounterGuard.DuplicateSpeciesAsync` (mismas familias que
  `IsDuplicateAsync`: Pokédex de la partida, equipo, run) → `GameLinkMonitor.TellGameTheDupesAsync` cada 15 s. Sonda:
  `--reglas-juego duplicados [menos] N...`.
- Siguiente: 4 (balls en zona gastada, salvo variocolor) y 1 (buzón).
- 2026-10-06: **4 hecho y probado por el organizador** (sale el mensaje nuevo y la ball no se gasta). No es el mensaje
  del entrenador (ese sale DESPUÉS de lanzar, en el servidor del combate): es la comprobación del menú de combate ANTES de
  lanzar. `Battle.cro` 0xB32CC calcula el motivo en r4 (1 sin concentrar, 2 PC llena, 3 dos Pokémon, 4 fuera de vista,
  5-6 prueba, 7 reserva, 8 Necrozma fusionado) con banderas del combate (vtable +0x110) y se junta en 0xB33D0
  (`ldr r0,[r5,#0xd8]`) antes de 0x63690 (motivo → mensaje del texto 12: 0x3A-0x3F, 0x42, 0x87). Ahí `bl` a `RefuseBalls`
  (0x11A724, tras la rutina del cap): sin motivo propio, toma `RuleBlock.BallRefusal` (byte en 0x6D3F18). PermaLocke
  escribe 8 (`SpentZoneReason`) y la línea 135 del texto 12 español (`a/0/3/6`, la de Necrozma) pasa a «¡Ya has tenido tu
  encuentro en esta zona! Aquí no puedes atrapar más Pokémon.» (`RulePatches.SpentZoneText`, solo esa línea; al quitar,
  vuelve byte a byte). App: `EncounterGuard.RefuseInGameAsync` con `gameRulePatches`: escribe el motivo según la decisión
  de siempre (variocolor/captura permitida = 0), devuelve lo que se quitara antes y **no toca la mochila ni avisa** de
  balls quitadas/devueltas (pedido del organizador); solo avisa del variocolor. Sonda: `--reglas-juego balls N`.
  Solo español: otro idioma vería el texto de Necrozma.
- Siguiente: 1 (buzón).
- 2026-10-06: **1 resuelto sin parche: lectura directa.** GameManager en el puntero fijo 0x6A3984 (code.bin, `FUN_001048b4`),
  GameData en +0x24. GameData +0x62 = zona (`GetNowZoneID`), y +0x60 es un `FieldRecord` entero que sigue cada paso
  (probado: Ruta 3 → zona 6, Ruta 2 → zona 7, X cambia al andar). GameData +0xC = `PokeParty`: 6 punteros en orden de
  equipo, número en +0x18, cada miembro +4 → entrada autoritativa (stride 0x1E4). `FieldZoneReader` (zona) y
  `AzaharGameStateProvider.FromGame` (equipo) lo leen antes de buscar/barrer; si no vale, como antes. Sonda:
  `--reglas-juego zona`. El buzón de capturas/debilitados no hace falta: los contadores y las tablas de combate ya lo dan.
  Todo en `docs/ARCHITECTURE.md` §217.
- 2026-10-06: **partida en memoria** (GameData +4 = datos de partida vivos). Bloques medidos: EventWork +0x15DC, Pokédex
  +0x23E0, cajas +0x3B9C/+0x4188, récords +0x68120 (21/21). `LiveSave` + `SaveDex` (Pokédex viva, probado con Spoink sin
  guardar) + `BattleCounterReader.FromGame`. OJO: la sonda sin carpeta lee la partida del Azahar del sistema, no la de
  prueba: pasarle `"C:\Users\javie\Desktop\PermaLocke prueba"`. Siguiente: cajas vivas y banderas de evento (etapa del cap).
- 2026-10-06: **tablas de combate sin buscar**: siempre en 0x30002748/0x30009730; `AtKnownOrigins` (posición 12 = primer
  rival; entrenador 12-14 medido). Búsqueda de reserva. Velocidad del emulador forzada al 200 % al pulsar JUGAR
  (`AzaharInstallation.SetSpeedLimit`, `frame_limit`). Parches quitados de la carpeta de prueba.
- **Corrección futura (pedida por el organizador, 2026-10-06): experiencia de la isla del Poké Resort sin pasar del cap.**
  Es la vía que el parche del cap no cubre (la isla da experiencia fuera de combate; hoy solo queda el aviso de la app).
  Buscar dónde suma la isla la experiencia (Resort / `ResortSave`, bloque 15) y comparar con `CompareWithCap` como el
  Caramelo Raro.
- 2026-10-06: mochila por cadena (partida + 0xC), corrección del cap en memoria **quitada** (código, tests, interruptor),
  búsquedas de zona/equipo/mochila/contadores quedan de reserva. Variocolor en el juego descartado con datos (≤ 0,6 s).
