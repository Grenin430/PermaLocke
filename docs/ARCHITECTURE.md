# PermaLocke — Arquitectura

Documento vivo. Fecha: 2026-08-17. Estado: **diseño, previo a Fase 1**.

Este documento distingue explícitamente entre lo que está **decidido**, lo que está
**investigado pero sin validar** y lo que es **desconocido**. Nada de lo marcado como
sin validar debe implementarse como si funcionara.

---

## 1. Contexto

PermaLocke gestiona un Nuzlocke competitivo de **Pokémon Ultra Luna** entre ~10 jugadores.
Cada jugador instala la app con su propia copia de la ROM vanilla, la app le genera una
randomización con **su propia seed**, y la app acompaña la partida: puntos, reglas, gacha,
tienda, logros y visor. Un administrador supervisa los puntos de todos.

Requisitos que condicionan el diseño, tal como los ha fijado el usuario:

| Requisito | Consecuencia arquitectónica |
|---|---|
| Se juega en emulador y la app detecta lo que pasa sola | Se necesita una capa de integración con el juego (`GameLink`) |
| Tienda y Miscelánea entregan objetos **reales** al juego | La integración es **bidireccional**: leer y escribir |
| Muerte → el Pokémon se convierte en Shedinja al salir del combate | Escritura en el juego, con timing acoplado al combate |
| Cap de nivel con corrección automática de EXP/nivel | Escritura frecuente, reactiva a eventos de combate |
| Se juega **sin internet**, se sincroniza después | Fuente de verdad **local**; la nube es un espejo |
| El admin solo necesita ver puntos | La sincronización puede ser mínima |
| Todos la misma ROM vanilla, seed distinta por jugador | La randomización corre en el cliente, no se distribuye ROM |

---

## 2. Qué se aprendió de BxnnyLocke

`Locke/` es una instalación de BxnnyLocke, la app de la competición en la que participa el
usuario. **No es software del usuario.** Se analizó únicamente a nivel de formatos y concepto;
no se copia código ni se redistribuyen sus binarios.

Composición observada:

- App **WinForms**, .NET 10, self-contained (~150 MB publicada).
- **Azahar** (fork de Citra) empaquetado dentro de la app, en `data/Emulador/`.
- El emulador está **instrumentado**: vuelca estado de juego en vivo a `user/rtp/p/`:

  | Fichero | Contenido |
  |---|---|
  | `party.txt` | Equipo: PID, especie, MetLocation, objeto, habilidad, movimientos, PP, shiny |
  | `wild.txt` | El Pokémon salvaje presente en pantalla |
  | `battle_slots.txt` | Combate en curso, incluido HP actual |
  | `MainPocket.txt` | Mochila (slot / itemID / cantidad) |
  | `visRou.bin` | Zonas visitadas (u16 LE; en la muestra, 49 entradas) |
  | `shd.pk7` | Un Pokémon en formato PK7 (260 bytes) |
  | `pts.json` | Puntos, ofuscado (no es JSON legible) |

- **PKHeX.Core** para parseo de Pokémon/save.
- **MySql.Data** → base de datos remota, competición centralizada.
- **AntiTamperUSUM.exe**, proceso vigilante independiente. Su log muestra detecciones
  repetidas de `EmuladorModificado` seguidas de cierre de la app.
- `data/user/rtp/logs_export.txt`: 127.000 líneas de un escaneo de memoria buscando un valor
  concreto entre 2.252 direcciones — herramienta de desarrollo para localizar punteros.

**Lección principal:** la detección automática no viene del save, viene de leer el juego en
vivo. Ese es el núcleo del problema técnico, y es donde se concentra el riesgo del proyecto.

---

## 3. Decisiones técnicas

### D1 — Stack: .NET 10 + WPF + MVVM

Decidido. WPF permite la estética de la referencia sin pelearse con WinForms. .NET 10 es LTS
y ya está instalado en la máquina del usuario (SDK 10.0.400).

### D2 — Licencia GPLv3

PKHeX.Core y pk3DS.Core son GPLv3 y son las únicas librerías serias para estos formatos.
Reimplementarlas desde cero no es razonable. PermaLocke será GPLv3 con fuentes públicas.

### D3 — NO se forkea el emulador (por ahora)

BxnnyLocke resuelve la integración modificando Azahar. Ese camino implica mantener un fork
en C++ y publicar su código (Azahar es GPLv3). Se descarta como punto de partida.

PermaLocke usará **Azahar sin modificar**, instalado por el jugador, y hablará con él desde
fuera. Si esto resulta inviable tras el spike (§6), se reevalúa.

### D4 — La fuente de verdad es local

SQLite local por jugador. La nube (Supabase) recibe únicamente un espejo de puntos y
ranking cuando hay conexión. Esto satisface "sin internet y que se sincronice".

### D5 — Randomización por LayeredFS, no por reconstrucción de ROM

Azahar soporta mods por capas en `<user>/load/mods/<TitleID>/`, con subcarpetas `romfs/`,
`exefs/`, `romfs_ext/`. Los ficheros del mod tienen precedencia sobre los del juego base.

En vez de reconstruir un `.3ds` de 4 GB por jugador, se generan solo los ficheros GARC
modificados (unos pocos MB). La ROM vanilla no se toca en absoluto y randomizar pasa de
minutos a segundos.

**Estado: investigado, sin validar con la ROM real.** Si LayeredFS fallara para USUM, el plan
B es reconstruir un `.3ds` completo, lo cual es más lento y más frágil.

---

## 4. Módulos

```
                    ┌───────────────────────┐
                    │   PermaLocke.Core     │
                    │  Run, Pokemon, Event  │
                    │  Points, Achievements │
                    │  Shop, Gacha, Ports   │
                    └───────────┬───────────┘
        ┌──────────────┬────────┴────────┬──────────────┐
        ▼              ▼                 ▼              ▼
     Rules        Randomizer         GameLink          Data
   RuleEngine    pk3DS.Core        Azahar link     SQLite + JSON
   IRule         LayeredFS         PKHeX.Core      + Sync
        └──────────────┴────────┬────────┴──────────────┘
                                ▼
                       Infrastructure
                    logging, hash, rutas, DI
                                ▲
                    ┌───────────┴───────────┐
                    ▼                       ▼
             PermaLocke.App          PermaLocke.Admin
                WPF jugador             WPF admin
```

`Core` define **puertos** (interfaces) que los demás módulos implementan. Core no conoce
SQLite, ni Azahar, ni WPF.

Puertos principales:

```csharp
IEventStore          // append-only de GameEvent
IRunRepository       // carga/guarda estado de run
IPointsService       // earn / spend / adjust, siempre con evento
IRuleEngine          // evalúa acciones
IGameStateProvider   // LEE el estado del juego
IGameStateWriter     // ESCRIBE en el juego
IRandomizerService   // genera la randomización
IClock, IRandomSource
```

---

## 5. Dominio, eventos y puntos

### Event sourcing ligero

El historial de eventos es **append-only** y es la fuente de verdad de los puntos. El saldo
mostrado es una proyección de la suma de eventos, no un número editable.

```csharp
record GameEvent(
    Guid Id, DateTimeOffset Timestamp, GameEventType Type,
    EventSource Source,        // Player | AutoDetect | Admin | System
    string Actor,
    string Description,
    int PointsDelta,
    Guid? PokemonId, string? LocationId, ulong? Seed,
    IReadOnlyDictionary<string, string> Data,
    string PreviousHash, string Hash);   // encadenado, ver §9
```

Tipos: `POINTS_EARNED`, `POINTS_SPENT`, `POINTS_ADJUSTED`, `POKEMON_CAUGHT`, `POKEMON_DIED`,
`POKEMON_RELEASED`, `POKEMON_TRADED`, `GACHA_ROLL`, `SHOP_PURCHASE`, `WONDER_TRADE`,
`ACHIEVEMENT_UNLOCKED`, `RULE_VIOLATION`, `RULE_EXCEPTION`, `ADMIN_ADJUSTMENT`, `RUN_CREATED`,
`RUN_COMPLETED`, `LEVEL_CAP_ENFORCED`, `GAME_STATE_SYNCED`.

Un ajuste administrativo **nunca** reescribe eventos previos: añade un `ADMIN_ADJUSTMENT` con
motivo obligatorio. El historial no se borra; como mucho se compensa.

### Puntos

`Data/rules.json` define recompensas y penalizaciones. Ningún valor de puntos vive en el
código ni en la UI. Reglas de negocio:

- El saldo no puede quedar negativo por una compra: `SpendPoints` falla y no genera evento
  de gasto, sino un rechazo.
- Un ajuste de admin **sí** puede dejar saldo negativo, si el admin lo confirma.

---

## 6. GameLink — la parte difícil

Este es **el riesgo principal del proyecto** y la razón por la que el resto de la arquitectura
se apoya en una abstracción en vez de en una implementación concreta.

```csharp
interface IGameStateProvider {
    GameLinkCapabilities Capabilities { get; }
    Task<GameSnapshot> ReadAsync(CancellationToken ct);
    IObservable<GameStateChange> Changes { get; }
}

[Flags] enum GameLinkCapabilities {
    None = 0, Party = 1, Boxes = 2, Bag = 4, Badges = 8,
    WildEncounter = 16, BattleState = 32, LiveUpdates = 64, Write = 128
}
```

La UI **consulta `Capabilities`** y desactiva con un mensaje claro lo que el proveedor activo
no soporta. Nunca finge.

### Implementaciones previstas

| Proveedor | Capacidades | Estado |
|---|---|---|
| `ManualProvider` | Todo, introducido a mano por el jugador | Implementable ya |
| `SaveFileProvider` (PKHeX.Core sobre el save de Azahar) | Party, Boxes, Bag, Badges. Sin live, sin combates | Implementable ya |
| `AzaharRpcProvider` (RPC oficial de Azahar) | Todo + live + escritura | **Transporte resuelto; faltan las direcciones** |

### El RPC de Azahar

Azahar hereda de Citra un servidor RPC documentado en `Azahar/scripting/citra.py`
(GPLv2+). Escucha **UDP en 127.0.0.1:45987** y expone cuatro operaciones:

| Id | Operación | Uso en PermaLocke |
|---|---|---|
| 1 | `ReadMemory(addr, size)` | Leer equipo, salvaje, combate, mochila, flags |
| 2 | `WriteMemory(addr, bytes)` | Shedinja, cap de nivel, entrega de objetos |
| 3 | `ProcessList()` | Confirmar que el título cargado es el correcto |
| 4 | `SetGetProcess(id)` | Fijar el proceso emulado sobre el que se opera |

Formato de petición: cabecera de 16 bytes `<u32 version=1, u32 requestId, u32 type, u32 dataSize>`
seguida de los datos. Máximo 1024 bytes de datos por paquete, así que las lecturas grandes se
trocean. La respuesta repite la cabecera y hay que validar `requestId` y `type`.

Direcciones: son **virtuales de 3DS**, las mismas que usa el juego. No hay que traducir nada
ni localizar la FCRAM dentro del proceso de Azahar.

Consecuencia: el transporte deja de ser un riesgo. `PermaLocke.GameLink` implementa un cliente
UDP en C# y nada más. Solo se lee el fichero `citra.py` como especificación del protocolo; no
se copia código.

### Búsqueda del mapa de memoria — hallazgos (2026-08-17)

Con `tools/PermaLocke.Probe` contra una partida real (entrenador `Grenin430`, Rowlet nivel 6,
24/24 PS):

| Región | Extensión legible |
|---|---|
| code | `0x00100000`–`0x00900000` (9 MB) |
| heap | `0x08000000`–`0x0A000000` (32 MB) |
| heap-alt | `0x0C000000`–`0x0E000000` (32 MB) |
| linear-alt | `0x14000000`–`0x1C000000` (128 MB, probable alias de linear) |
| linear | `0x30000000`–`0x34000000` (64 MB) |

Rendimiento: barrer los 96 MB de heap + linear tarda **6 segundos** por el RPC. La búsqueda de
valores es viable en tiempo real, no solo como herramienta de investigación.

Resultados:
- El **nombre del entrenador aparece en texto plano** 10 veces (UTF-16LE), ninguna dentro de una
  estructura de Pokémon.
- El **número de especie 722 aparece 308 veces** como u16 en las regiones mapeadas.
- Ninguna de esas posiciones parsea como PK7 válido probando **todos** los desplazamientos
  posibles del inicio del bloque.

**Causa de los fallos iniciales: no se fijaba el proceso.** El servidor responde a `ReadMemory`
aunque no se haya llamado a `SetGetProcess`, devolviendo bytes que no son fiablemente los del
juego; solo se detecta en el log del emulador (`No target process selected`). `AttachTo` lo fija
y **verifica** releyendo el proceso activo. Nunca leer sin fijar.

Aviso operativo: una ráfaga ininterrumpida de decenas de miles de peticiones tumbó el emulador.
`MemorySearch` cede el hilo cada 256 peticiones.

### Equipo localizado (2026-08-17)

Método: barrer heap y linear enteros a un búfer local, y ofrecer **cada offset alineado a 4** al
constructor de `PK7`, filtrando por `Sanity == 0` como pre-filtro barato y por nombre del
entrenador como criterio final. Sin suponer ningún offset.

```text
0x330128E4  #722 Rowlet  Nv.6  25/25  OT:Grenin430   ← hueco 0
0x330129E8  vacío                                     ← hueco 1
...                                                    huecos de 0x104 bytes
```

- El bloque del equipo son **6 huecos consecutivos de `SIZE_PARTY` (0x104) bytes**.
- `new PK7(bytes)` **detecta y descifra solo**: acepta tanto la forma cifrada como la descifrada.
  No hay que descifrar a mano.
- El barrido completo de 96 MB tarda ~6 s por RPC; el análisis local, unos segundos más.

**Estabilidad:** tras cerrar Azahar por completo y volver a cargar la partida, el equipo sigue en
`0x330128E4`. Comprobado una vez, con un solo Pokémon en el equipo y en la misma zona del juego.

No se asume que sea estable siempre. El lector **localiza por barrido al arrancar** (6 s), cachea
la dirección y **revalida** en cada lectura comprobando que el hueco 0 sigue conteniendo un
Pokémon coherente del entrenador de la run. Si deja de cuadrar, vuelve a barrer. Así da igual si
la dirección se mueve al cambiar de zona, al crecer el equipo o entre versiones del emulador.

### Lo que sigue sin resolver: el mapa de memoria de Ultra Luna

Saber **cómo** leer no es saber **qué** leer. Faltan las direcciones de las estructuras del
juego (bloque del equipo, encuentro salvaje, estado de combate, mochila, medallas y flags de
prueba). Esa investigación sigue pendiente y es lo que ahora limita las funciones en vivo.

Ventaja: el propio RPC sirve para investigarlas. Se puede barrer memoria y comparar lecturas
antes y después de un cambio conocido en el juego, que es exactamente lo que hacía el
`logs_export.txt` de BxnnyLocke, pero sin instrumentar el emulador.

### Verificación realizada (2026-08-17)

Probado contra la instalación real del usuario con Ultra Luna cargado, mediante
`tools/PermaLocke.Probe`:

```text
El servidor RPC responde. Procesos emulados: 1.
PID  TITLE ID           NOMBRE
11   00040000001B5100   momiji

Lectura de 32 bytes en 0x00100000:
00100000  07 00 00 EB 6A 11 00 EB 4B 1A 00 EB 85 11 00 EB
```

- `ProcessList` devuelve el proceso emulado con el title id correcto y el nombre interno del
  juego (`momiji`).
- `ReadMemory` devuelve código ARM real del ejecutable de Ultra Luna.
- `WriteMemory` acepta la petición sin error. **Matiz honesto:** la prueba escribió los mismos
  bytes que acababa de leer, así que demuestra que el servidor acepta la operación, no que el
  byte cambie de verdad. La confirmación definitiva llegará al escribir sobre una dirección
  conocida y comprobar el efecto en el juego.

**Requisito de configuración:** `enable_rpc_server` viene a `false` de fábrica y solo puede
activarse con la emulación detenida (`Configuración → Depuración → Activar servidor RPC`).
PermaLocke debe detectarlo y guiar al jugador, porque cada uno de los 10 participantes tendrá
que activarlo en su máquina. Sin él, la app cae al modo manual.

`GetProcess` devuelve `0xFFFFFFFF` cuando no se ha fijado proceso explícitamente; las lecturas
funcionan igual sobre el proceso activo.

**Hasta que este spike no produzca una lectura real verificada, quedan sin prometer:**
detección automática de encuentros salvajes, detección de muerte en combate, transformación
en Shedinja al salir del combate, aplicación del cap de nivel, entrega de objetos de la tienda
al vuelo, y detección instantánea de pruebas completadas.

**Plan B si el spike falla:** todo lo anterior se degrada a modo save — la app aplica los
cambios sobre el fichero de guardado con el emulador cerrado, y el jugador recibe la
instrucción de guardar y cerrar. Funciona, pero es peor experiencia. Se documentaría como tal
en lugar de simularlo.

### Escritura y seguridad de datos

Toda escritura sobre el save del jugador hace **backup previo con timestamp** en `Saves/backup/`.
Escritura sobre memoria: solo estructuras verificadas; ante cualquier lectura inconsistente se
aborta la operación y se registra en `Logs/`.

---

## 7. Rule Engine

Independiente, sin dependencias de UI ni de cómo se obtuvo el dato. La misma acción se valida
igual venga de entrada manual o de detección automática.

```csharp
interface IRule {
    string Id { get; }
    bool AppliesTo(GameAction action);
    RuleResult Evaluate(GameAction action, RuleContext context);
}

enum RuleOutcome { Allowed, Blocked, AllowedWithException, Warning }
enum RuleMode    { Disabled, WarnOnly, Block, BlockAndLog }
```

`RuleContext` expone el estado de la run (encuentros por zona, especies obtenidas, líneas
evolutivas, cap actual, Pokémon vivos/muertos) sin exponer servicios.

Reglas iniciales: `FirstEncounterRule`, `ShinyClauseRule`, `DupesClauseRule`,
`SpeciesClauseRule`, `GiftPokemonRule`, `StaticEncounterRule`, `LevelCapRule`, `DeathRule`.

Cada regla se configura por separado en `Data/rules.json`, incluido su `RuleMode`, además de
un modo global por defecto.

Tipos de encuentro (`WILD`, `GIFT`, `STATIC`, `LEGENDARY`, `STARTER`, `TRADE`, `FISHING`,
`SOS`, `SPECIAL`) son datos, no `if` repartidos: la configuración declara cuáles consumen el
encuentro de zona.

### Cap de nivel (especificación del usuario)

Cap = nivel del Pokémon más alto del Kahuna / Pokémon dominante de la etapa actual.
Comportamiento al superarlo:

1. Si se rompió el cap **con Caramelos Raros** → se borran todos los caramelos del inventario,
   y el nivel se corrige al salir del siguiente combate.
2. Si un Pokémon **en el cap** gana EXP → al salir del combate la EXP vuelve a 0.
3. Si aun así **sube de nivel** → se borran los caramelos y al salir del combate el nivel se
   baja en 1, volviendo al cap.

Los tres casos requieren escritura en el juego reactiva al fin del combate → dependen del
spike de §6. La tabla de caps por etapa vive en `Data/levelcaps.json`, editable por el admin.

### Muerte

Detección de HP 0 → al salir del combate, el Pokémon se transforma en **Shedinja nivel 1 sin
movimientos** (configurable; alternativa contemplada: huevo que nunca eclosiona). Genera
`POKEMON_DIED` y aplica la penalización de puntos configurada.

---

## 8. Randomización

```
ROM vanilla (Ultra Luna, .3ds)   ← nunca se modifica
        │
        ├─ validación: ¿decrypted? ¿título correcto? ¿hash conocido?
        ▼
   extracción de los GARC necesarios  (pk3DS.Core)
        ▼
   randomización determinista por seed
   (wild, trainers, starters, evos, abilities, items, moves)
        ▼
   escritura de los ficheros modificados en
   <Azahar>/load/mods/<TitleID>/romfs/...
        ▼
   run.json: seed, hash de ROM, versión de randomizador, opciones
```

`RandomizerService` orquesta; `WildEncounterRandomizer`, `TrainerRandomizer`,
`StarterRandomizer`, `EvolutionRandomizer`, `ItemRandomizer` son piezas independientes y
testeables que operan sobre estructuras ya parseadas, no sobre bytes crudos.

Determinismo: misma seed + mismas opciones + misma ROM ⇒ mismo resultado. Cada randomizador
recibe su propio `Random` derivado de la seed maestra con un salt fijo por módulo, para que
activar o desactivar un módulo no altere el resultado de los demás.

**Incógnitas a resolver antes de implementar:**
- ¿La ROM del usuario está desencriptada? Azahar la necesita así. Sin comprobar.
- API real de pk3DS.Core y su compatibilidad con .NET 10. Sin verificar — **no inventar firmas**.
- Title ID exacto de Ultra Luna en la instalación del jugador. Se leerá, no se supondrá.
- ¿LayeredFS surte efecto sobre los GARC de USUM? Sin validar.

---

## 9. Persistencia, sincronización e integridad

### Local

| Qué | Dónde | Formato |
|---|---|---|
| Configuración de la app | `Config/appsettings.json` | JSON |
| Reglas, tienda, gacha, logros, caps | `Data/*.json` | JSON, editable por admin |
| Estado de la run + eventos + Pokémon | `Saves/<run>/permalocke.db` | SQLite |
| Metadatos de la run | `Saves/<run>/run.json` | JSON legible |
| Logs | `Logs/permalocke-<fecha>.log` | texto |

SQLite desde el principio para eventos: son miles y necesitan filtrado (§ pantalla de
historial del Admin). JSON para lo que el usuario debe poder abrir y editar.

### Sincronización (Supabase, Postgres gratuito)

Modelo mínimo acorde a "el admin solo ve los puntos":

```
players(id, name, run_id, role)
run_summaries(run_id, player_id, points, alive, dead, encounters,
              violations, last_sync, app_version)
point_events(id, run_id, timestamp, type, delta, description)   -- opcional
```

Cola de salida local: los eventos pendientes se acumulan offline y se envían al reconectar.
Idempotencia por `event.Id` (GUID) para que un reenvío no duplique puntos.

### Integridad — qué protege y qué NO

Honestidad sobre esto, porque el usuario pidió expresamente no montar seguridad falsa:

**Sí puede hacer PermaLocke:**
- Encadenar los eventos por hash (`Hash = SHA256(PreviousHash + contenido)`) para detectar que
  alguien editó o borró un evento del historial.
- Hashear la ROM vanilla y la configuración de la run, y detectar que cambiaron.
- Detectar incoherencias: puntos que no cuadran con la suma de eventos, Pokémon vivos que el
  save dice muertos, encuentros duplicados en una zona.
- Registrar y reportar al admin todo lo anterior.

**No puede hacer PermaLocke:**
- Impedir que alguien edite su save con PKHeX, use savestates, cheats o un Azahar parcheado.
  Todo corre en la máquina del jugador; con suficiente empeño, todo es manipulable.
- Impedir que se reinstale la app y se empiece de cero.
- Garantizar que un cliente no envíe puntos falsos al servidor, si el cliente es quien los
  calcula.

La finalidad realista es **evitar trampas accidentales y manipulaciones triviales, y dejar
rastro de las deliberadas**, no ofrecer una protección criptográfica imposible.

---

## 10. Subsistemas de juego

**Gacha.** Banners configurables en `Data/gacha.json` (la referencia usa tres: 100 / 225 / 300
puntos). Cada banner define pool, rarezas, probabilidades, nivel, IVs, naturaleza, habilidad,
probabilidad de shiny y restricciones. La tirada usa un `IRandomSource` sembrado y registra la
seed en el evento → **reproducible y auditable**. Flujo: validar puntos → validar reglas →
gastar → generar → animar → registrar.

**Tienda.** `Data/shop.json`. Los productos son objetos reales del juego (Cinta Elección,
Chaleco Asalto, Master Ball…) con su ID. Comprar genera `SHOP_PURCHASE` + `POINTS_SPENT`, y
**encola una entrega** al juego que ejecuta `IGameStateWriter` cuando puede. Si el writer no
está disponible, la compra queda pendiente y se muestra como tal; no se finge la entrega.

**Logros.** `Data/achievements.json`. Reaccionan a eventos mediante condiciones declarativas
(contador, umbral, evento único), de forma que añadir logros no toque código. Se muestran con
progreso `x/y` y se cobran con un botón explícito, como en la referencia.

**Wonder Trade.** Simulado, con seed registrada, Pokémon enviado y recibido, y paso por el
Rule Engine. Preparado para conectar algo real más adelante.

**Visor.** Equipo + cajas del PC, con detalle de IVs/EVs/stats/naturaleza/habilidad, separación
entre vivos, muertos, obtenidos por gacha y por wonder trade, y marca visible cuando un
Pokémon entró por una excepción de reglas.

**PokéPaste.** Import/export en formato Showdown, con validación. PKHeX.Core aporta la
conversión; la exportación es texto plano al portapapeles.

---

## 11. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| El spike de memoria falla | Alto — se cae la detección automática | Plan B en modo save, documentado, no simulado |
| Las direcciones cambian al actualizar Azahar | Medio | Fijar versión soportada y detectarla; fallar con mensaje claro |
| LayeredFS no funciona en USUM | Medio | Plan B: reconstruir `.3ds` completo |
| pk3DS.Core no compila en .NET 10 o su API no encaja | Medio | Verificar antes de diseñar sobre ella; aislar tras `IRomDataSource` |
| La ROM está encriptada | Medio | Detectarlo y guiar al usuario; no intentar desencriptar |
| Escritura en memoria corrompe la partida | Alto | Backups automáticos, escrituras mínimas verificadas, abortar ante duda |
| GPLv3 obliga a publicar | Bajo | Asumido y documentado |

---

## 12. Fases

| Fase | Contenido | Depende de |
|---|---|---|
| **1** | Solución, arquitectura, WPF+MVVM, sidebar, HOME | — |
| **2** | Runs, seed, puntos, eventos, auditoría, JSON+SQLite | 1 |
| **3** | Rule Engine + First Encounter / Shiny / Dupes / Species / tipos de encuentro | 2 |
| **4** | Gacha, tienda, wonder trade, logros | 3 |
| **5** | Visor Pokémon, PokéPaste | 4 |
| **6** | PermaLocke.Admin completo | 2-5 |
| **7** | **Spike de GameLink** (memoria de Azahar) — decide el alcance real | puede adelantarse |
| **8** | Randomizador real con pk3DS.Core + LayeredFS | 7 opcional |
| **9** | Cap de nivel, muerte→Shedinja, entrega de objetos | 7 |
| **10** | Sincronización Supabase y ranking del admin | 2 |

Las fases 1-6 **no dependen** del spike: se construyen sobre `ManualProvider` y
`SaveFileProvider`, que son reales y funcionan. El spike puede adelantarse en cuanto haya
algo de infraestructura, porque su resultado determina cuánto de las fases 9 y 10 es posible.

No se avanza de fase dejando código roto en la anterior.

---

## 13. La zona de captura (2026-08-18)

Se intentó localizar el mapa actual con búsqueda diferencial de valor desconocido: foto de
memoria, el jugador cambia de zona, se comparan, y se filtra por los valores que vuelven a su
original al regresar. Tras dos pasadas quedaban **7.830 candidatos**: demasiado ruido
(contadores, posición, animaciones) para valer la pena.

**No hacía falta.** Cada Pokémon guarda su propio `MetLocation`, y PKHeX trae las tablas de
zonas de Ultra Sol/Luna (`GameStrings.GetLocationName`). Leído del equipo real:

```text
slot 0  #722 Rowlet   Nv.7  [8] Ruta 1   bola=4  nivel de encuentro=5
slot 1  #736 Grubbin  Nv.4  [8] Ruta 1   bola=4  nivel de encuentro=4
```

Es además el dato **correcto**, no solo el conveniente: el mapa actual dice dónde está el
jugador ahora, mientras que `MetLocation` dice dónde se capturó ese Pokémon, que es lo que
pregunta la regla de primer encuentro. Sigue siendo válido si la captura se registra más tarde.

Consecuencia: el diálogo de captura llega con especie, nivel, shiny y zona ya rellenos desde el
juego. El jugador solo confirma, porque las reglas pueden bloquear o advertir y esa decisión
debe verla.

Queda pendiente leer el mapa actual solo si algún día hace falta para algo distinto de las
capturas — por ejemplo, saber en qué prueba anda el jugador para el cap de nivel.

---

## 14. Requisito: impedir la captura, no solo registrarla

El usuario ha fijado el objetivo real: la app no debe pedir confirmación de cada captura, y
cuando una zona ya tiene su encuentro gastado, **el juego debe quedarse sin poder capturar** —
concretamente, deshabilitando el uso de Poké Balls.

Eso cambia el papel de la app: de auditor a árbitro. Y tiene tres consecuencias técnicas:

1. **Hace falta la zona actual, no `MetLocation`.** Para prevenir hay que saber dónde está el
   jugador *antes* de que capture. `MetLocation` solo sirve después. Vuelve a ser necesaria la
   búsqueda del mapa actual en memoria (§13), que se descartó por innecesaria para registrar.

2. **Hace falta escribir en la mochila.** Poner a cero la cantidad de Poké Balls mientras la
   zona esté gastada, y **restaurarla** al cambiar de zona. Exige localizar la mochila en
   memoria (BxnnyLocke la volcaba como `slot / itemID / cantidad`, así que es localizable) y
   llevar la cuenta de lo retirado para devolverlo intacto.

3. **La escritura deja de ser opcional.** Hoy está verificado que el servidor RPC *acepta*
   `WriteMemory`, pero no que el valor escrito surta efecto. Ese es el siguiente hito
   bloqueante: sin él no hay ni Shedinja, ni cap de nivel, ni tienda, ni esto.

Riesgo a tratar con cuidado: manipular la mochila del jugador puede destruirle objetos si se
escribe mal. Antes de tocarla hacen falta lectura verificada, escritura verificada sobre un
valor inocuo, y una copia de lo retirado que sobreviva a un cierre inesperado de la app.

---

## 15. Escritura: qué se puede y qué no (2026-08-18)

Verificado contra el juego real, con el código fuente de Azahar delante
(`src/core/rpc/rpc_server.cpp`).

`HandleWriteMemory` **solo escribe si la dirección cae en una lista blanca** de regiones, y en
caso contrario descarta la petición y **responde OK igualmente**. Por eso un cliente ingenuo cree
que la escritura funcionó:

| Región | Rango | Escribible |
|---|---|---|
| Process image | `0x00100000`–`0x04000000` | sí |
| Heap | `0x08000000`–`0x10000000` | sí |
| Linear heap clásico | `0x14000000`–`0x1C000000` | sí |
| N3DS extra RAM | `0x1E800000`–`0x1EC00000` | sí |
| New linear heap | `0x30000000`–`0x40000000` | **NO** |

Comprobado empíricamente: un byte en `0x081A6A20` cambió de 0 a 123 y se mantuvo; el mismo
intento sobre el equipo en `0x330129D4` no cambió nunca, ni siquiera a 0 ms.

**El bloque del equipo vive en `0x330128E4`, es decir en la región excluida.** Se buscó una
copia de los mismos bytes en heap y en linear clásico y no existe.

Consecuencias directas:

- **Transformar en Shedinja al morir: no es posible por esta vía.** Requiere escribir en el
  bloque del equipo.
- **Corregir nivel y EXP por el cap: tampoco**, por lo mismo.
- **Entregar objetos de la tienda y bloquear Poké Balls: pendiente de saber dónde vive la
  mochila.** Si está en heap o en linear clásico, sí es posible. Hay que localizarla.

Caminos si hiciera falta escribir en el equipo:
1. **Fichero de cheats de Azahar** (formato Action Replay). El emulador sí los aplica sobre
   cualquier región. Habría que generar el código y conseguir que se recargue en caliente.
2. **Fork de Azahar** ampliando la lista blanca: una línea de C++, pero implica mantener y
   distribuir un emulador propio bajo GPLv3.
3. **Plugin 3GX** dentro del 3DS emulado, como hace PokeReader.

Ninguno se ha probado. No prometer nada de esto hasta que lo esté.

### La mochila también está fuera de alcance

Buscando el par `itemID=4 (Poké Ball), cantidad=7` con la mochila real del jugador:

```text
heap (escribible)            0 coincidencias
linear clásico (escribible)  0 coincidencias
new linear (solo lectura)    48 coincidencias
```

Es decir: **todo el estado vivo del juego —equipo y mochila— reside en el new linear heap**, la
única región que Azahar se niega a escribir. Las regiones escribibles no contienen nada útil.

Conclusión firme: con **Azahar sin modificar no se puede escribir nada en la partida**. Ni
Shedinja, ni cap de nivel, ni entrega de objetos, ni retirar Poké Balls. La lectura, en cambio,
funciona por completo.

Esto explica retroactivamente por qué BxnnyLocke empaqueta su propio emulador instrumentado: no
es una decisión de comodidad, es que por el RPC estándar no se puede.

Opciones reales, ninguna probada todavía:

| Opción | Coste | Consecuencia |
|---|---|---|
| Fork de Azahar añadiendo `NEW_LINEAR_HEAP` a la lista blanca | Una línea de C++ + compilar + distribuir | Hay que repartir un emulador propio (GPLv3, con fuentes) |
| Fichero de cheats en formato Action Replay | Generar códigos y lograr recarga en caliente | Direcciones fijas; sin verificar que se recargue sin reiniciar |
| Plugin 3GX dentro del 3DS emulado | Alto: C++ para ARM | Es lo que hace PokeReader |

Mientras tanto, PermaLocke aplica las reglas **en la app**: avisa, bloquea el registro y deja
constancia auditable. Lo que no puede es impedirlo dentro del juego.

---

## 16. El fork funciona; la dirección no es la buena (2026-08-18)

El usuario compiló un fork de Azahar añadiendo `NEW_LINEAR_HEAP` a la lista blanca de
`HandleWriteMemory` (fork público: `github.com/Grenin430/azahar`, build de GitHub Actions,
job `windows (msvc)`).

**Resultado: la escritura llega.** Un byte en `0x330129D4` pasó de 53 a 20 y se mantuvo un
segundo entero, cosa imposible con el Azahar oficial. El bloqueo del emulador está superado.

**Pero escribir el bloque del equipo no cambia la partida.** Puestos los PS de Rowlet a 15, el
menú del juego seguía mostrando 27/30, y minutos después el propio bloque había vuelto a 27.

Conclusión: `0x330128E4` es **una copia que el juego refresca**, no la estructura autoritativa.
Sirve perfectamente para leer —refleja capturas, niveles y PS al momento— pero no para escribir.

Un barrido completo de `0x08000000-0x10000000` y `0x30000000-0x40000000` no encuentra ninguna
otra estructura PK7 con checksum válido. La copia buena, por tanto, no está en formato PK7
completo, o vive fuera de esas regiones.

Otro apunte del mismo experimento: el bloque en memoria está **cifrado**. Al modificar con PKHeX
hay que comparar en la forma cifrada (`WriteEncryptedDataParty`); comparar en la descifrada da un
offset que no existe en el juego y corrompe un byte ajeno. Ya ocurrió una vez.

**Siguiente técnica a probar:** búsqueda diferencial sobre los PS. El jugador recibe daño en
combate, se filtran las direcciones que cambian al valor nuevo, y se repite. Eso localiza la
variable autoritativa sin suponer nada de su formato. Requiere al jugador delante.

### Por qué la búsqueda por valor no encuentra la estructura buena

Búsqueda diferencial sobre los PS de Rowlet (27 → 25, con daño real en combate) sobre 384 MB:
**un único candidato superviviente**, `0x317F00A4`. Escribirlo (25 → 10) no cambió nada en
pantalla ni tras forzar el repintado.

O sea que también es una copia. Y tiene explicación: **el bloque del equipo está cifrado**, así
que los PS reales no existen en memoria como un `27` legible. Todo lo que devuelve una búsqueda
por valor plano es, por construcción, una copia auxiliar (HUD de combate, menú, etc.).

Estado de las dos vías probadas:

| Vía | Resultado |
|---|---|
| Escribir el bloque PK7 en `0x330128E4` | La escritura entra, el juego no la usa y luego la sobrescribe |
| Buscar los PS por valor y escribir ahí | Solo aparecen copias; escribirlas no afecta al juego |

Ideas por probar la próxima vez, en orden de coste:

1. **Buscar bloques en formato *stored* (0xE8 bytes), no *party* (0x104).** La copia residente
   del save guarda los Pokémon sin estadísticas de combate, así que el barrido actual —que lee
   0x104 y exige PS coherentes— los descarta a todos. Es el hueco más evidente que queda.
2. **Averiguar qué obliga al juego a releer el bloque**: guardar la partida, entrar en combate,
   cambiar de zona. Si algo lo hace releer, escribir antes de ese momento sí surtiría efecto.
3. **Escribir en las copias en el instante justo** (por ejemplo, la del HUD durante el combate),
   aceptando que es frágil.

Lo que **sí queda demostrado y no hay que repetir**: con el fork, `WriteMemory` entra en
`0x30000000-0x40000000`. El problema ya no es el emulador, es la puntería.

### Estructura autoritativa encontrada: `0x33F7FA44` (2026-08-18)

El barrido en formato **stored** (0xE8) encontró **seis copias** del equipo, con distintas
separaciones entre miembros:

```text
0x3002E258   0x3002EDFC   0x32992860   0x33F7FA44    ← miembros cada 0x1E4
0x3254EE60   0x330128E4                              ← miembros cada 0x104
```

Para identificar la buena sin probarlas una a una, se puso **un mote distinto en cada copia**
(`COPIA1`…`COPIA6`) y se miró cuál aparecía en el juego. Resultado: **`COPIA6`**, es decir
`0x33F7FA44`. Y apareció **sola, sin refrescar ninguna pantalla**: el juego la lee en vivo.

Todo se hizo con copia de seguridad previa de cada bloque y se restauró después.

Lo que esto habilita, porque vive dentro del bloque cifrado y PKHeX lo escribe entero con su
checksum: **especie, mote, nivel, experiencia, movimientos, objeto**. Es decir, la
transformación en Shedinja y la corrección de nivel/EXP del cap.

Ojo con un matiz: en esta estructura, los **PS y demás estadísticas de combate no están en la
cola estándar** (leerla como party de 0x104 da valores absurdos). La separación de 0x1E4 indica
un registro más grande cuyo formato aún no se ha mapeado. Para tocar PS habrá que localizar ese
campo aparte.

Método que funcionó, para repetirlo con la mochila: barrer en el formato correcto, encontrar
todas las copias, y **distinguirlas escribiendo un valor diferente en cada una**.

### Receta de muerte, verificada en el juego (2026-08-18)

Aplicada sobre un Pokémon real en `0x33F7FFF0` y comprobada en pantalla por el usuario:

| Campo | Valor | Cuándo se ve |
|---|---|---|
| Especie | 292 (Shedinja) | al instante |
| Mote | `MUERTO` | al instante |
| Movimientos | los cuatro a 0 | al instante |
| Habilidad | **0** → el juego muestra `-` | al instante |
| Nivel | 1 | **solo tras entrar y salir de un combate** |

La regla general que se desprende, y que hay que respetar al programarlo: **lo que vive dentro
del bloque cifrado se refleja de inmediato; el nivel y las estadísticas son campos derivados que
el juego recalcula al entrar en combate.**

Eso encaja con el requisito original —"que se transforme al salir del combate"— y con lo que el
usuario describió del cap de nivel en BxnnyLocke ("tiene que entrar en combate y salir para
actualizar el nivel"). No es una limitación: es el momento correcto de aplicarlo.

La receta vive en `Data/rules.json` bajo `death.transform`, no en el código.

### Cap de nivel, verificado en el juego (2026-08-18)

Las tres reglas que definió el usuario —romper el cap con Caramelos Raros, ganar experiencia
estando en el cap, y subir de nivel pasándose— **acaban todas en el mismo estado**: al cap y sin
progreso hacia el siguiente nivel. Por eso se resuelven con una sola operación:

```csharp
pokemon.CurrentLevel = (byte)cap;   // PKHeX recalcula EXP al mínimo de ese nivel
pokemon.RefreshChecksum();
```

Probado sobre el equipo real con cap 5: `Rowlet Nv.8 -> Nv.5, EXP 135`, confirmado en pantalla
por el usuario tras entrar y salir de un combate, y restaurado después desde la copia de
seguridad.

Detalle de implementación: la escritura compara las formas **cifradas** de original y modificado
y solo manda los bytes que difieren (5 en este caso). Escribir el bloque entero también valdría,
pero así la ventana de inconsistencia es mínima.

**Pendiente:** borrar los Caramelos Raros al romper el cap. Vive en la mochila, que todavía no
está localizada — el mismo bloqueo que impide retirar las Poké Balls.

### La mochila, localizada y verificada (2026-08-18)

Primer intento fallido: buscar `itemID` y `cantidad` como dos u16 seguidos. La intersección
entre "7 balls" y "6 balls" salió **vacía**, porque ese no es el formato.

**Formato real (gen 7):** cada objeto es **un u32 empaquetado** — el ID en los 10 bits bajos y
la cantidad en los 10 siguientes:

```text
valor = itemId | (cantidad << 10)

6 Poké Balls  →  4 | (6 << 10) = 0x1804  →  bytes 04 18 00 00
```

Con ese patrón aparecieron **6 copias**. Se identificó la buena con el mismo método que el
equipo —escribir una cantidad distinta en cada una y mirar cuál obedece el juego— y resultó ser
**`0x33011934`**, que mostró las 16 balls escritas.

Verificado además que **escribir cuatro ceros vacía el bolsillo**: el usuario confirmó que las
Poké Balls desaparecen de la mochila. Ese es el mecanismo que pide la regla de "primer encuentro
por zona": retirar las balls mientras la zona esté gastada y devolverlas al cambiar de zona.

Todo se hizo con copia de seguridad previa de los bytes y se restauró después.

Con esto quedan desbloqueadas las tres funciones que dependían de la mochila: **retirar Poké
Balls**, **borrar Caramelos Raros** al romper el cap, y **entregar objetos de la tienda**.

### El salvaje y el estado de combate (2026-08-18)

**Pokémon salvaje.** Barrido en formato stored filtrando por especie (sin filtro de entrenador:
un salvaje no lo lleva). Aparecen **5 copias**, y las cinco se actualizan al cambiar de
encuentro, con las mismas direcciones:

```text
0x3002F9A0   0x30030544   0x3254F4AC   0x32991CBC   0x32993404
```

**Trampa importante:** al salir del combate **conservan el último salvaje**. No sirven por sí
solas para saber si hay un encuentro en curso; solo para saber *qué* había. Guiarse solo por
ellas devolvería las Poké Balls paseando por el mapa.

**Estado de combate.** Búsqueda diferencial en tres rondas (fuera → dentro → fuera → dentro),
filtrando por valores pequeños, porque una bandera vale 0 o 1 y no 46904. Quedaron seis
candidatos, y **abrir la mochila en el mapa descartó tres**: el menú reutiliza esa memoria y
escribía basura encima (41856, 24587, 24087). Falso positivo clásico.

Los tres supervivientes, verificados en los cuatro estados:

| Dirección | Fuera de combate | En combate |
|---|---|---|
| `0x330D6CA8` | 1 | 0 |
| `0x330D6CEC` | 1 | 0 |
| `0x330DBB68` | 0 | **1** |

Se usarán los tres a la vez y se exigirá acuerdo entre ellos: si discrepan, PermaLocke asume que
no sabe el estado y no toca nada, en vez de arriesgarse a retirar o devolver objetos en el
momento equivocado.

Con esto queda cubierto el flujo del shiny: zona gastada → balls retiradas; empieza un combate →
se lee el salvaje; si es shiny → balls devueltas; acaba el combate → retiradas de nuevo.

---

## 17. Implementación en la app (2026-08-18)

`AzaharGameWriter` en `PermaLocke.GameLink` lleva a código lo verificado a mano: transformación
de muerte, cap de nivel y cantidad de objetos. Reglas que sigue:

- **Copia de seguridad obligatoria antes de cada escritura**, en `Saves/backup/`, con dirección
  y motivo en el nombre. Si el backup falla, **la escritura no se hace**: nunca se modifica lo
  que no se puede deshacer.
- Comparación en forma **cifrada** y envío únicamente de los bytes que difieren.
- Se rechaza escribir sobre un bloque cuyo checksum no valida.

### Dos correcciones que solo aparecieron ejecutando

1. **El salto entre miembros no identifica la copia autoritativa.** Cuatro de las seis usan
   `0x1E4`. La heurística inicial elegía una copia cualquiera.

   Solución adoptada: **no identificarla**. Las escrituras van **a todas las copias**. Son el
   mismo Pokémon, la autoritativa está entre ellas y las demás se refrescan desde ella, así que
   escribir en todas es seguro y suficiente.

2. **No todas las copias sirven para leer.** Solo algunas llevan estadísticas de combate, y los
   PS son la base de la detección de muertes. El proveedor evalúa las candidatas y se queda con
   **la que más miembros devuelve**, no con la primera que devuelve alguno.

Resultado contra el juego real: `Equipo localizado en 0x330128E4 (salto 0x104), 18 copias` y
`Conectado al juego. Equipo: 4 Pokémon`.

También se corrigió `GameWatcher`, que reventaba si dos Pokémon de la run compartían PID
(registrar el mismo dos veces). Ahora agrupa en vez de indexar directamente.

## 18. Cap de nivel y mochila en la app (2026-08-18)

**Cap de nivel.** `LevelCapTable` lee `Data/levelcaps.json`; el cap vigente es el de la etapa
que el jugador *va a afrontar* (sin pruebas superadas, la primera). `Run.ClearedStages` guarda
el progreso y solo se mueve por `ProgressService.AdvanceAsync`, que escribe evento.

El avance de etapa es **manual, con botón en HOME**. Detectarlo del juego exigiría flags de
prueba superada que nadie ha localizado, y mover el cap por una corazonada bajaría el nivel de
un Pokémon sin motivo. Se queda explícito hasta que existan esas flags.

`GameLinkMonitor` compara el equipo con el cap en cada ciclo y corrige lo que se pase, en todas
las copias, dejando un `LEVEL_CAP_ENFORCED` con nivel, cap y hueco.

**Mochila.** `BagLocator` busca el u32 empaquetado y devuelve todas las copias (7 con Poké Ball
x6). `BagService` retira y devuelve, y **apunta en disco lo retirado** (`Saves/backup/
objetos-retirados.txt`) antes de tocar nada: si PermaLocke se cierra con objetos retenidos, la
siguiente ejecución sabe lo que debe. Nadie se queda sin balls porque la app muriera.

### Lo que impide cerrar la regla de las Poké Balls

Retirarlas "cuando la zona está gastada" **necesita saber la zona actual**, y esa variable sigue
sin localizarse: la búsqueda diferencial de §13 se abandonó al descubrir que `MetLocation` daba
la zona de captura, que resolvía el registro pero no esto.

También hace falta que las direcciones de la bandera de combate (§ salvaje) se localicen solas
al arrancar; hoy se encontraron a mano y no hay localizador.

Hasta entonces, retirar y devolver funciona pero hay que dispararlo explícitamente.

---

## 19. Randomizador: estrategia validada en el juego (2026-08-18)

La Fase 8 estaba marcada como "investigada, sin validar". Ya no. La cadena completa —ROM →
RomFS → GARC → parcheo → LayeredFS → Azahar → partida— se ha ejecutado contra la ROM y la
partida reales, y el juego muestra los cambios.

### pk3DS.Core: no está en NuGet, y hay que recortarlo

```
dotnet package search pk3DS  →  No results found
```

Se vendoriza el fuente de `github.com/kwsch/pk3DS` (GPLv3 → GPLv3, compatible). El proyecto
original declara `net10.0-windows` con `UseWindowsForms`, lo que contaminaría
`PermaLocke.Randomizer`, que por norma no puede depender de UI.

**Comprobado compilando:** eliminando 12 ficheros que PermaLocke no usa —`BLZ`, `CRO`, `CTR`,
`NCCH`, `NCSD`, `RomFS`, `SMDH`, `ETC1`, `Exheader`, `ImageUtil`, `CTR/Images/`,
`Structures/TypeChart`— el resto compila como **`net10.0` puro**, sin WinForms ni
System.Drawing. Quedan 11.677 líneas y 3 warnings (CA2022 en `LZSS`/`SARC`).

Todo lo eliminado es empaquetado de ROM e imágenes. Nada de ello hace falta para randomizar
GARCs, y el extractor de RomFS de pk3DS además **es inservible sin interfaz**:
`ExtractFileFromRomFS` hace `PB_Show.InvokeRequired` sin comprobar null, así que revienta si se
le pasa `null`, y solo sabe extraer los 3,5 GB enteros.

### Lector propio de RomFS

`RomFsReader` (NCSD → NCCH → IVFC → tablas de directorios y ficheros) abre la ROM en solo
lectura, indexa y extrae ficheros sueltos:

```
RomFS @0x646000, 747 ficheros indexados en 9 ms
22 GARC extraídos en 561 ms
ficheros bajo a/ = 333   ← el número exacto con el que pk3DS identifica USUM
```

`ProgramId` leído de la ROM: `00040000001B5100`, `CTR-P-A2BA`, flag NoCrypto activo.

### Mapa de GARCs de Ultra Luna, con tamaños reales

| Contenido | Ruta | Tamaño |
|---|---|---|
| move / eggmove / levelup / evolution / megaevo | `a/0/1/1`–`a/0/1/5` | 32–88 KB |
| personal | `a/0/1/7` | 184 KB |
| item | `a/0/1/9` | 54 KB |
| gametext (10 idiomas) | `a/0/3/0`–`a/0/3/9` | 1,3–3,0 MB |
| zonedata / worlddata | `a/0/7/7`, `a/0/9/1` | 10 / 67 KB |
| trclass / trdata / trpoke | `a/1/0/5`–`a/1/0/7` | 9–50 KB |
| encounterstatic (**incluye los 3 iniciales**) | `a/1/5/9` | 20 KB |
| **encdata** | **`a/0/8/3`** | **460 MB** |

`a/0/8/2` existe con 0 bytes: es así como `GameConfig` distingue Ultra Sol de Ultra Luna.

Los iniciales son los **tres primeros gifts** de `encounterstatic` (`EncounterGift7`, 0x14 bytes
por entrada). No hacen falta CROs, que es la vía que pk3DS advierte que no funciona por
redirección de ficheros. Las **MTs**, en cambio, viven en el `code.bin` del ExeFS: camino
distinto, más frágil, sin validar.

`encdata` son 3.696 subficheros —11 por área × 336 áreas— de los que solo el nº 9 de cada 11
lleva encuentros; los otros 10 son datos de mapa. Por eso pesa lo que pesa y por eso pk3DS lo
abre con `LazyGARC`.

### La regla que salió de los fallos: parchear bytes, no reconstruir estructuras

El primer intento usó los escritores de pk3DS y **el juego se quedó sin encuentros salvajes**.
LayeredFS no tenía nada que ver: el log confirmaba la carga del fichero.

`Area7.GetDayNightTableBinary` no es fiel al original:

```
entradas 3..7 del área 9 empiezan por  06 00 00 00   →  las reescribe a  00 00 00 00
cabecera del mini: 128 bytes                          →  la reescribe a  80
área 9: 13.952 bytes  →  13.904
```

Pierde un campo que en vanilla no es cero y encoge la cabecera. Lo mismo, en otro sitio: el
round-trip de `TextFile` **corrompe las dos variantes japonesas** del gametext, que vuelven con
0 líneas manteniendo el tamaño en bytes. En silencio.

De ahí las dos normas del randomizador:

1. **Se parchean bytes en su sitio sobre el payload descomprimido**, nunca se regenera la
   estructura con los escritores de pk3DS. Se lee, se cambian los campos concretos, y todo lo
   demás —cabeceras, relleno, campos desconocidos— se queda como estaba.
2. **Escribir, releer y verificar.** Si el fichero no vuelve exactamente como debe, se restaura
   el vanilla y se registra. No se publica lo que no se ha releído.

El contenedor GARC sí hay que reempaquetarlo cuando sus subficheros están comprimidos, que es el
caso de `encdata` (LZ11: 896 bytes en disco → 13.952 descomprimidos). **Ese reempaquetado está
verificado: el juego lo acepta.** No sale byte a byte igual al original, pero funciona.

Resultado del parcheo quirúrgico sobre `encdata`:

```
subficheros que difieren: 10 de 3696
área 9: 13.952 vs 13.952 bytes, cabecera mini idéntica
```

### Verificación en el juego (2026-08-18)

Mods en `<Azahar>/load/mods/00040000001B5100/romfs/`, partida real del usuario:

| Cambio | Fichero | Resultado en pantalla |
|---|---|---|
| Ruta 1 y 10-17 → solo Magikarp | `a/0/8/3` (460 MB) | **Salen Magikarp** ✔ |
| Magikarp → `XXXXXXXX` | `a/0/3/x` (8 idiomas) | **"¡Un XXXXXXXX salvaje!"** ✔ |

Del código de Azahar (`file_sys/ncch_container.cpp`, idéntico en el fork del usuario):
LayeredFS se activa solo con que exista `load/mods/<ProgramId>/romfs/`, **no hay ajuste que
encender**, y deja rastro en el log: `LayeredFS replacement file in use for /a/0/8/3`. Ese log es
la vía barata de comprobar que un mod se cargó sin depender de mirar la pantalla.

**Trampa a recordar:** el nombre que muestra la pantalla del equipo es el **mote guardado en la
partida**, no el de la ROM. Un Pokémon capturado antes del mod sigue mostrando su nombre viejo.
Solo los generados después —un salvaje, por ejemplo— leen el nombre de la ROM.

### Coste, y la decisión que implica

| Qué se randomiza | Tamaño del mod | Tiempo |
|---|---|---|
| Todo menos salvajes | < 1 MB | ~1 s |
| Salvajes | **460 MB** | ~10 s, con **~2,5 GB de RAM de pico** |

Sigue siendo mucho mejor que reconstruir un `.3ds` de 4 GB, pero conviene saberlo antes de
decidir el alcance: randomizar salvajes multiplica por 500 el tamaño de lo que hay que generar y
guardar en la máquina de cada jugador.

### Lo que sigue sin validar

| Tema | Estado |
|---|---|
| Randomización de salvajes y textos vía LayeredFS | **VERIFICADA en el juego** |
| Iniciales (3 primeros gifts de `encounterstatic`) | Estructura conocida, **sin probar en juego** |
| Entrenadores, personal, movimientos, evoluciones, objetos | Estructuras conocidas, **sin probar en juego** |
| MTs (`code.bin` del ExeFS) | **Sin investigar a fondo**, camino aparte |
| Determinismo por seed entre módulos | Sin implementar |

---

## 20. Randomizador: qué está implementado (2026-08-18)

Motor y tests primero; la pantalla en `PermaLocke.App` viene después, cuando el motor esté
probado contra el juego.

| Pieza | Dónde | Estado |
|---|---|---|
| pk3DS.Core recortado | `third_party/pk3DS.Core/` | compila `net10.0`, 0 warnings |
| Lector de RomFS | `Randomizer/Rom/RomFsReader.cs` | 747 ficheros en 9 ms, solo lectura |
| Parcheo de GARC en sitio | `Randomizer/Rom/GarcPatcher.cs` | verificado con lector independiente |
| Espacio de trabajo | `Randomizer/Rom/RomWorkspace.cs` | extrae 31 ficheros en ~350 ms |
| Carpeta de mods | `Randomizer/Output/LayeredFsMod.cs` | escribe, revierte y limpia |
| Aleatoriedad determinista | `Core/Services/SeededRandomSource.cs` | SplitMix64, secuencia fijada en test |
| Tabla de encuentros | `Randomizer/Modules/EncounterTable7.cs` | test de regresión del fallo de pk3DS |
| Pool de especies | `Randomizer/Modules/SpeciesPool.cs` | prohibidas, fuerza similar, 1-a-1 |
| Salvajes | `Randomizer/Modules/WildEncounterRandomizer.cs` | **funcionando contra la ROM real** |
| Iniciales, fósiles y estáticos | `Randomizer/Modules/StaticEncounterRandomizer.cs` | **funcionando**, parcheo en sitio sin reempaquetar |
| Entrenadores | `Randomizer/Modules/TrainerRandomizer.cs` | **funcionando**, parcheo en sitio sin reempaquetar |
| Datos de Pokémon | `Randomizer/Modules/PokemonDataRandomizer.cs` | **funcionando**, parcheo en sitio sin reempaquetar |
| Tiendas especiales | `Randomizer/Modules/ShopRandomizer.cs` | **funcionando** sobre `Shop.cro` |
| Configuración | `Data/randomizer.json` | editable por el admin |
| Herramienta | `tools/PermaLocke.RomTool` | `inspect`, `names`, `randomize`, `dump` |

Ejecución real, seed 20260818: **45.848 huecos en 74 zonas, 460 MB, 1,9 s**. Releído con el
lector de pk3DS: **0 huecos con especie prohibida** de 5.656, niveles y porcentajes intactos.

`SeededRandomSource` deriva una fuente por módulo a partir de un salt con nombre
(`"wild-encounters"`), para que activar o desactivar un módulo no desplace los resultados de los
demás. **Los salts no pueden cambiar** una vez empezada una competición: son lo que hace que una
seed reproduzca un mundo. El hash del salt es FNV-1a, nunca `string.GetHashCode`, que es
aleatorio por proceso.

### Lo que falta, y se dice en el informe

`RandomizerService.NotImplemented` devuelve un paso **PENDIENTE** por cada módulo que la
configuración pide y todavía no existe, con test que lo cubre. Un jugador nunca cree que su
partida está randomizada de una forma en la que no lo está.

| Módulo | Fichero | Estado |
|---|---|---|




| Objetos del suelo | scripts `ZS`/`ZI` de cada zona | formato localizado, **instrucción sin identificar** — ver más abajo |
| MTs | `code.bin` del ExeFS | sin investigar |

### Iniciales, fósiles y estáticos (2026-08-18)

Todo vive en `a/1/5/9`, 20 KB, y sus **seis subficheros están sin comprimir**, así que se
parchean en sitio de verdad: el contenedor GARC no se reempaqueta nunca.

| Subfichero | Contenido | Entrada | Entradas |
|---|---|---|---|
| 0 | Iniciales (0-2), los once fósiles (3-13), regalos, totems | 0x14 | 35 |
| 1 | Encuentros estáticos | 0x38 | 252 |
| 4 | Intercambios internos | 0x34 | 7 |

Decisiones que salieron de mirar los datos reales:

- **La forma se pone a 0 al cambiar de especie.** Un índice de forma válido para la especie
  vieja no tiene por qué existir en la nueva.
- **De un intercambio solo se randomiza lo que recibes** (0x0), no lo que te exigen (0x2):
  cambiar el requisito dejaría intercambios imposibles de completar.
- **Los tres iniciales se tiran juntos** para que no te ofrezcan tres veces el mismo.
- **Cosmog (789) se deja intacto** por defecto. La historia lo entrega y luego exige que
  evolucione a Solgaleo o Lunala; que el juego aguante que lo cambies **no está probado**, y
  probarlo cuesta horas de partida. Está en `protectedSpecies` de `Data/randomizer.json`.

Ejecución real, seed 20260818, releída con el lector de pk3DS sobre el fichero generado:

```
regalos          35 entradas, 700 -> 700 bytes, 34 cambiadas
estáticos       252 entradas, 14112 -> 14112 bytes, 252 cambiadas
intercambios      7 entradas, 364 -> 364 bytes, 7 cambiadas
293 de 294 cambiadas; especies prohibidas coladas: 0
intacta: regalos entrada 18 = Cosmog (protegida)
iniciales: Rowlet -> Darumaka, Litten -> Bellsprout, Popplio -> Fletchling
```

Los tamaños idénticos confirman el parcheo en sitio. **Sin probar todavía dentro del juego.**

### Entrenadores (2026-08-18)

`trdata` (`a/1/0/6`) y `trpoke` (`a/1/0/7`), 653 subficheros cada uno, **ambos sin comprimir**.
Un subfichero por entrenador; el equipo es una tirada plana de entradas de 0x20 bytes.

Campos usados: nivel en 0x0E, especie u16 en 0x10, forma en 0x12, objeto en 0x14, los cuatro
movimientos desde 0x18.

Tres decisiones, todas sacadas de mirar los datos del cartucho:

- **El nivel no se toca nunca.** La tabla de caps de `Data/levelcaps.json` se construye a partir
  del Pokémon más alto del Kahuna de cada etapa. Mover un nivel de entrenador movería en
  silencio el cap que gobierna a los diez jugadores. Hay comprobación explícita de que salen 0
  niveles movidos.
- **Al cambiar la especie, los movimientos se ponen a cero** para que el juego genere el moveset
  del nuevo Pokémon. Esto no es una suposición: **518 de los 1.139** Pokémon de entrenador del
  cartucho ya vienen con los cuatro huecos a cero, así que el juego soporta ese estado. Sin
  esto, un Magikarp heredaría los movimientos elegidos para un Snorlax.
- **El objeto equipado se conserva.** 107 lo llevan, y una Baya o una Poción siguen valiendo sea
  cual sea la especie.

Detalle a no perder: **un subfichero de `trpoke` mide 6 bytes**, o sea que no contiene ni una
entrada completa. Se trata como equipo vacío en vez de reventar. Por eso el informe dice 652
entrenadores modificados y no 653.

Verificación sobre el fichero generado, leído con el lector de pk3DS:

```
1139 Pokémon, 1129 con especie nueva
equipos que cambiaron de tamaño: 0
NIVELES movidos: 0
objetos alterados: 0
especies prohibidas: 0
nivel más alto del juego: 74 (entrenador 159, el Red del Árbol de Combate)
```

Los 1.139 - 1.129 = 10 sin cambiar son tiradas que cayeron sobre la propia especie: el pool no
excluye al original y el conjunto de candidatos de fuerza similar es pequeño.

**Sin probar todavía dentro del juego.**

### Datos de Pokémon (2026-08-18)

Tipos, estadísticas base, habilidades, evoluciones y movimientos por nivel. Tres GARC, los tres
**sin comprimir**, y toda edición conserva el tamaño de su entrada: parcheo en sitio otra vez.

**Trampa de `personal` (`a/0/1/7`): tiene 977 subficheros, no 976.** Los 976 primeros son
entradas sueltas de 0x54 bytes y **el último es la tabla entera empaquetada** (976 × 0x54 =
81.984 bytes), que es la que lee pk3DS. Hay dos copias de cada especie. PermaLocke **escribe las
dos**, y comprueba después que coinciden: dejar una sin tocar sería el mismo tipo de fallo que
las seis copias del equipo en memoria.

Ofsets verificados: PS 0x00, At 0x01, Def 0x02, **Vel 0x03**, AtEsp 0x04, DefEsp 0x05, tipos
0x06 y 0x07, habilidades 0x18, 0x19 y 0x1A. Ojo con la velocidad, que va tercera y no última.

Decisiones:

- **Las estadísticas se barajan, no se sortean.** El total no cambia nunca. Es lo que mantiene
  con sentido el emparejamiento "por fuerza similar" de los otros tres módulos: si el total
  variara, comparar contra él dejaría de significar nada. Hay test de que el total sobrevive y
  de que los seis valores son exactamente los mismos.
- **Un mono-tipo sigue siendo mono-tipo.** El cartucho representa un solo tipo repitiéndolo en
  los dos huecos, y esa forma se respeta.
- **Un hueco de habilidad vacío se queda vacío.** Rellenarlo daría habilidades a Pokémon que no
  las tienen en ese hueco.
- **De una evolución solo se cambia el destino**, no el método, ni el argumento, ni el nivel:
  así se sigue evolucionando cuando y como se evolucionaba.
- **De un aprendizaje solo cambia el movimiento**, nunca el nivel ni el número de movimientos,
  que es lo que permite parchear sin cambiar el tamaño.
- **Este módulo se aplica el último.** Los otros emparejan por total de estadísticas base, y ese
  total tiene que ser el del cartucho.

Verificación sobre el fichero generado:

```
975 entradas (especies y formas)
totales de estadísticas movidos: 0
estadísticas a cero: 0
filas sueltas que no cuadran con la tabla empaquetada: 0
Rowlet:   Planta/Volador -> Tierra/Hielo   68/55/55/42/50/50 -> 50/50/68/55/42/55  (320 -> 320)
Magikarp: Agua/Agua      -> Veneno/Veneno  20/10/55/80/15/20 -> 20/55/15/20/80/10  (200 -> 200)
```

Prueba de que los salts por módulo funcionan: activar este módulo **no movió** los iniciales que
salían antes (Darumaka, Bellsprout, Fletchling).

**Sin probar todavía dentro del juego.**

### Tiendas especiales (2026-08-18)

Los inventarios viven en **`Shop.cro`** (40 KB en la raíz del RomFS), no en un GARC. Un CRO es un
módulo reubicable, y pk3DS avisa de que editarlos no funciona sin parchear la verificación RSA
del módulo RO. Eso vale para consola real; **Azahar no comprueba nada**:

```cpp
Result CROHelper::VerifyHash(u32 cro_size, VAddr crr) const {
    // TODO(wwylele): actually verify the hash
```

y `LoadCRR` está marcado `(STUBBED)`. Por eso aquí sí es viable.

Formato: en `0x52DD` hay un byte por inventario con cuántos objetos vende, y la lista termina en
el primer cero. Los objetos son u16 seguidos desde `0x50BC`. Hay **28 inventarios**: los ocho
primeros son el mostrador normal del centro Pokémon, que crece según pruebas superadas, y los
veinte restantes son los mostradores especiales.

Comportamiento pedido por el usuario:

- **Los ocho normales no se tocan.** Es de donde salen pociones y balls, y una Nuzlocke los
  necesita funcionando.
- **Las tiendas de MT se randomizan por otras MT**, sin repetir dentro de la misma tienda.
- **Las demás especiales se surten del objeto configurado en todos sus huecos**, Poké Ball (4)
  por defecto.

Una tienda se clasifica **por su contenido**, no por una lista de nombres: es de MT si todo lo
que vende es una MT. Coincide con las etiquetas de pk3DS (Heahea, Avenida Royal, Malie, Aldea
Marina y Konikoni) sin depender de ellas.

**Corrección a pk3DS:** su lista de objetos prohibidos llega hasta el id 427, herencia de la
sexta generación. Comprobado contra la lista de objetos del cartucho, **Ultra Luna tiene 92 MT,
ids 328 (MT01) a 419 (MT92)**; del 420 al 425 son MO01-MO06, las máquinas ocultas, que en
séptima generación no sirven de nada, y 426 y 427 no tienen nombre. Usar el rango de pk3DS
llenaría las tiendas de objetos inservibles. El módulo además comprueba en ejecución que cada
id del rango tiene nombre real antes de usarlo.

Resultado sobre el fichero generado, releído del disco:

```
 8 [especial] x 9: Poké Ball x9
12 [MT      ] x 5: MT66, MT43, MT04, MT33, MT87
13 [MT      ] x 4: MT34, MT44, MT92, MT24
18 [MT      ] x10: MT17, MT14, MT53, MT24, MT31, MT27, MT61, MT30, MT84, MT03
23 [especial] x 5: Poké Ball x5
0-7 [normal ]    : intactos
```

5 tiendas de MT randomizadas, 15 surtidas de Poké Balls, 135 huecos. **Sin probar dentro del
juego.**


### Objetos del suelo (2026-08-18)

Son las Poké Balls tiradas por el mapa: las normales llevan un objeto, **las doradas una MT**.

Primer intento fallido, y la lección que deja: se buscaron en los subficheros `ZS` y `ZI` de
cada zona, que son contenedores de script, y se concluyó que hacía falta desensamblar bytecode.
**Falso.** El usuario señaló que el Universal Pokémon Randomizer ZX ya lo tenía resuelto, y su
`Gen7RomHandler` (GPLv3) da la posición exacta. La lección es la misma que con pk3DS: antes de
declarar algo investigación original, mirar si alguien lo publicó ya.

Están en el **subfichero 0** de cada zona, no en el 7 ni el 8, y dos niveles más adentro:

```
encdata[zona * 11 + 0]        mini 'ED'  (datos de entorno)
    ED[10]   mini 'EI'   objetos: primer byte = cuántos, registros de 64 bytes, id u16 en +52
    ED[11]   mini 'EB'   bayas:   primer byte = cuántos, registros de 68 bytes desde +4,
                                  7 ids u16 desde +54
```

Comprobado contra el cartucho: **539 objetos en 116 de las 336 zonas, 40 de ellos MT**, y
**ningún id sin nombre real**, que es la señal de que los desplazamientos son correctos.

Se parchea **en sitio**, sin reempaquetar ningún mini. UPR sí los reempaqueta con su `PackMini`;
PermaLocke no puede permitírselo, porque el `PackMini` de pk3DS encoge la cabecera de 128 a 80
bytes y eso es justo lo que dejó el juego sin encuentros salvajes en el primer intento.

Reglas:

- **Las MT doradas se cambian por otras MT.**
- **Los objetos normales se barajan entre sí**, no se sortean de la lista completa. Así nunca
  aparece un id que el cartucho no colocara ya en alguna parte, y un objeto clave no puede
  convertirse en una Poción y atascar la historia sin que nadie lo note.

Encuentros salvajes y objetos del suelo comparten el mismo GARC de 460 MB, así que
`RandomizerService` lo carga una vez, aplica los dos módulos y lo guarda una vez.

## 21. El randomizador en la app (2026-08-18)

Sección `RANDOMIZADOR` en la barra lateral, `RandomizerViewModel` + `RandomizerView`, MVVM
estricto: la vista no tiene lógica y el view model solo llama a `RandomizerService`.

**Generar e instalar son dos botones distintos, a propósito.** Generar escribe en
`Randomized/seed-N/` y no toca el juego. Instalar copia a la carpeta de mods de Azahar y
**cambia el mundo de una partida que puede estar empezada**, así que pide confirmación explícita
(`IAppDialogs.Confirm`, nuevo) y avisa de que lo normal es hacerlo antes de empezar. Hay un
tercer botón para quitarlo y volver al juego original.

La pantalla **no decide qué se randomiza**: eso vive en `Data/randomizer.json`, como manda la
norma de configuración fuera del código. La pantalla lo dice en voz alta en vez de duplicar los
interruptores.

Al arrancar y cada vez que se abre la sección se relee todo: la run activa, si hay una ROM
compatible y desencriptada en `ROM/`, y si existe la carpeta de Azahar. Cuando falta algo se
dice cuál y dónde debería estar, y los botones se desactivan; no hay botones que fallen al
pulsarlos.

**Evento auditable:** generar una randomización añade un `RomRandomized` con la seed, los
módulos aplicados, el número de ficheros y los bytes. Nuevo valor en `GameEventType`. Instalar y
quitar no generan evento porque no cambian el estado de la run, solo qué ROM ve el emulador.

**Sin abrir todavía.** Compila sin warnings, pero nadie ha visto la pantalla funcionando.

### Herramientas de prueba en MISCELÁNEA (2026-08-18)

`MiscellaneousViewModel` deja de ser una sección vacía. Primera herramienta: escribir Caramelos
Raros en la mochila, para poder probar el cap de nivel sin jugar horas hasta reunirlos.

La mochila se localiza buscando en memoria el par objeto/cantidad empaquetado en un u32
(§18), así que **hace falta saber cuántos lleva el jugador ahora mismo**: es lo que hace el
hueco localizable. Con cero no hay nada que encontrar, porque un objeto que no llevas no ocupa
sitio en la mochila. La pantalla lo pide y lo explica en vez de fallar sin motivo aparente.

`BagService.SetCount` escribe la cantidad nueva en **todas las copias** de la mochila, igual que
el resto de escrituras (§17): son el mismo dato y la autoritativa está entre ellas.

Tope 1023, porque la cantidad ocupa diez bits. Exige el fork propio de Azahar; el oficial acepta
la escritura y no la aplica.

Genera un evento **`TestItemGranted`** con el objeto, la cantidad y en cuántas copias se
escribió. Es una herramienta que altera el juego, así que deja rastro como cualquier otra cosa
que cambie el estado.

---

## 22. La mochila, localizada por su estructura (2026-08-19)

El §18 daba la mochila por resuelta: se buscaba el u32 empaquetado del objeto y salían varias
copias, de las que una era la buena. Con eso se construyó el botón de Caramelos Raros de
MISCELÁNEA, y **el botón no era de fiar**: con la mochila del usuario sin ningún caramelo,
`BagLocator.LocateAny` devolvía `0x081D55B0 id=50 cantidad=92` y lo daba por bueno. Escribir
ahí habría metido un valor en memoria desconocida de una partida en curso, y el botón habría
dicho que funcionó.

### Por qué fallaba, que no era solo la heurística

Al mirarlo de cerca había **tres fallos independientes** y, debajo, un error de planteamiento:

| Fallo | Efecto |
|---|---|
| La máscara de la búsqueda nativa exigía los bits 20-31 a cero | Los bits 20-29 son el índice de hueco libre y el bit 30 es la marca de «nuevo». La búsqueda **descartaba entradas reales** |
| `AzaharRpcClient.SearchMemory` no pagina y el fork devuelve 255 aciertos como mucho | En una región con miles de coincidencias, la buena podía **no salir nunca** |
| La heurística de vecindario puntuaba «esto parece un bolsillo» | Aceptaba basura. Ese era el falso positivo |
| **Un objeto que no llevas no ocupa hueco en la mochila** | Buscar «el caramelo» era imposible por construcción cuando el jugador no tenía ninguno, que es justo el caso que el botón necesitaba resolver |

El planteamiento era el error: buscar un **valor** en vez de la **estructura**. Afinar la
heurística no converge, y se probó.

### El formato real, y la firma que lo identifica

El bloque de la mochila de Ultra Sol/Luna son **0xE28 bytes** con los siete bolsillos pegados,
exactamente el mismo `MyItem7USUM` que PKHeX escribe en el save:

| Bolsillo | Desplazamiento | Huecos | Bytes | Tope por hueco |
|---|---|---|---|---|
| Items | `+0x000` | 427 | 1708 | 999 |
| KeyItems | `+0x6AC` | 198 | 792 | 1 |
| TMHMs | `+0x9C4` | 108 | 432 | 1 |
| Medicine | `+0xB74` | 60 | 240 | 999 |
| Berries | `+0xC64` | 67 | 268 | 999 |
| ZCrystals | `+0xD70` | 35 | 140 | 1 |
| BattleItems | `+0xDFC` | 11 | 44 | 999 |

Y **justo detrás del bloque, en `base+0xE28`, hay una tabla con un puntero a cada bolsillo**.
En la partida real, con el bloque en `0x33011934`:

```text
0x3301275C  330124A8   -> Medicine     (base+0xB74)
0x33012760  33011934   -> Items        (base+0x000)
0x33012764  330122F8   -> TMHMs        (base+0x9C4)
0x33012768  33012598   -> Berries      (base+0xC64)
0x3301276C  33011FE0   -> KeyItems     (base+0x6AC)
0x33012770  330126A4   -> ZCrystals    (base+0xD70)
0x33012774  33012730   -> BattleItems  (base+0xDFC)
```

Esa tabla es la firma. Como está en `base+0xE28`, **la dirección de la tabla determina la
base**, y el criterio es binario: o las siete palabras apuntan cada una a su propio bolsillo, o
no es una mochila. No hay puntuación ni parecido. El orden de los punteros no se supone: se
exige que estén los siete, cada uno una vez.

Resultado del barrido sobre los 96 MB donde el juego guarda su estado: **un bloque, cero falsos
positivos**, en `0x33011934` — la misma dirección a la que había llegado a mano el §18. La
discrepancia que abrió esta investigación era del localizador, no del dato: las «7 copias» del
§18 eran seis palabras que casualmente valían lo mismo, y solo una era la mochila.

**Corrección al §17 y al §18.** La norma de «escribir en todas las copias» vale para el equipo,
donde no se sabía distinguir la autoritativa. Para la mochila **no**: hay un solo bloque y se
identifica, así que escribir en todas las coincidencias era escribir en memoria ajena.

### Detalles que salieron de mirar la mochila de verdad

- **Una entrada es un u32**: id en los bits 0-9, cantidad en los 10-19, **índice de hueco libre
  en los 20-29 y marca de «nuevo» en el bit 30**. El bit 31 no se usa. El código viejo
  reconstruía la palabra como `id | (cantidad << 10)` y **borraba los dos campos de arriba** en
  cada escritura. Ahora solo se cambia la cantidad y el resto de la palabra se conserva.
- **Los objetos clave no van compactados y se guardan con cantidad 0.** La Piedra Brillante
  estaba sola en el **hueco 197** de 198. Cortar la lectura en el primer hueco vacío, o exigir
  cantidad mayor que cero, se la habría comido. Y se mueve: en una lectura posterior la misma
  Piedra Brillante aparecía en el hueco 0, así que tampoco se puede cachear su posición.
- **Los desplazamientos no están copiados a mano.** `BagLayout` los deduce de PKHeX en tiempo
  de ejecución: marca el hueco 0 de cada bolsillo con un centinela distinto, vuelca el bloque
  con `PlayerBag7USUM.CopyTo` y mira dónde cae cada uno. Si PKHeX cambia el formato, revienta
  al construirse en vez de escribir en sitios equivocados. Hay test de que los siete salen
  donde se verificaron contra el juego.

### Verificación contra el juego real (2026-08-19)

Con la partida del usuario cargada en el fork de Azahar:

| Prueba | Resultado |
|---|---|
| Lectura de la mochila | Poké Ball x5, Tabla Terrax x1, Semilla Hierba x1, Poción x5, Piedra Brillante — **el usuario confirma que es exactamente lo que lleva** |
| Estabilidad de la dirección | `0x33011934` **tras cerrar y reabrir el emulador** |
| Escritura de un objeto que NO se lleva | Caramelo Raro x999 escrito en `0x330124AC`, hueco 1 de Medicinas, releído y correcto |
| Efecto en el juego | **El usuario ve los 999 caramelos en la mochila** |

Ese último caso es el que el código viejo no podía hacer: el bolsillo de medicinas no tenía
ninguna entrada de caramelos, así que no había nada que encontrar. Ahora se busca el bolsillo
que admite el objeto —lo dice PKHeX, no una lista a mano—, se coge su **primer hueco libre** y
se escribe ahí.

**Tabla Terrax y Semilla Hierba en la Ruta 1** no son un error de lectura: son objetos del
suelo randomizados, o sea la primera confirmación en partida de ese módulo del randomizador
(§20), que estaba marcado como sin probar.

### Escribir, releer y comprobar

`AzaharGameWriter.SetBagSlot` hace copia de seguridad de la palabra, escribe y **vuelve a
leer**. Si no ha cambiado, devuelve `NotApplied` y la interfaz lo dice tal cual. Es la misma
norma que el randomizador (§19), y aquí resuelve un problema concreto: **el Azahar oficial
acepta la escritura, no la aplica y responde OK** (§15). Sin releer, el botón mentiría en toda
instalación que no use el fork. `BagService` devuelve un `BagWriteOutcome` —`Ok`, `BagNotFound`,
`NotApplied`, `PocketFull`, `UnknownPocket`, `NothingToDo`— y la pantalla traduce eso, sin
inventarse un mensaje de éxito.

### Coste del barrido, y por qué casi nunca se hace

Barrer los 96 MB son **unos 5,5 s y 24.000 peticiones**. Dos cosas descubiertas midiendo:

1. **El emulador responde lecturas en todo `0x30000000-0x40000000`**, esté o no mapeado, así que
   barrer los 256 MB enteros cuesta 25 s para memoria que nunca ha tenido nada. `MemorySearch.
   LiveStateRegions` acota a los 96 MB donde sí ha aparecido estado vivo: equipo, mochila,
   salvaje y banderas de combate. `DefaultRegions` se deja como estaba porque lo usa el
   localizador del equipo, que funciona.
2. **El servidor RPC escribe una línea de log por cada respuesta.** El log de una sesión con
   barridos pesaba **104 MB**. Un barrido deja unos 15 MB. Candidato claro para el siguiente
   parche del fork: bajar esa línea a Debug.

Por eso `BagService` **guarda la dirección en `Saves/backup/mochila.txt`** y la próxima vez la
revalida con **una sola lectura de 28 bytes** (6 ms medidos, frente a los 5.500 del barrido).
Confiar en ella es seguro precisamente porque no se confía: la tabla de punteros tiene que
cuadrar antes de usarla, igual que si se acabara de encontrar. En uso normal ya no se barre.

Aviso operativo, que sigue vigente: durante esta sesión **el emulador se fue durante un
barrido**. No está demostrado que lo tumbara el barrido, pero el §6 ya avisaba de ello, así que
el localizador cede el hilo cada 512 lecturas y se barre lo menos posible.

### Por qué la búsqueda nativa del fork no sirve aquí

Da rabia, porque el parche `SearchMemory` es rápido, pero **la firma de la mochila es una
relación autorreferencial** —«estas siete palabras apuntan al bloque que termina donde están
ellas»— y el servidor solo sabe casar un patrón de bytes con máscara. No hay patrón fijo que
buscar. Además `SearchMemory` **trunca a 255 aciertos y el cliente no pagina**, así que sirve
para patrones muy específicos y **nunca para demostrar que algo no está**. Queda anotado en el
propio método.

### Lo que esto desbloquea y lo que falta

Desbloqueado y verificado: leer la mochila, cambiar la cantidad de un objeto, **añadir uno que
el jugador no lleva** y quitarlo del todo. Con eso quedan cubiertas las tres funciones que
dependían de la mochila: retirar Poké Balls, borrar Caramelos Raros al romper el cap y entregar
objetos de la tienda.

Sigue faltando, igual que en el §18, **la zona actual**, que es lo que decide *cuándo* retirar
las balls. Eso no lo resuelve esto.

---

## 23. La zona actual, resuelta (2026-08-19)

Retirar las Poké Balls cuando la zona ya gastó su encuentro (§14) necesita saber **dónde está el
jugador ahora**. El §13 lo intentó con búsqueda diferencial y lo dejó con 7.830 candidatos, y el
§18 lo dio por bloqueante. Este es el segundo intento, con un filtro nuevo.

### El filtro que faltaba

El ruido del §13 eran coordenadas, contadores y animaciones: cosas que cambian **solas**. El
paso nuevo es pedirle al jugador que **ande sin salir de la zona** y descartar todo lo que se
haya movido. Sobre eso van los pasos clásicos. La secuencia completa, en `Probe --zone`:

```text
snap      foto de referencia estando en la zona A
stable    tras andar dentro de A: fuera todo lo que se mueve solo
changed   tras pasar a la zona B: solo lo que cambió
back      tras volver a A: solo lo que recuperó su valor
show      lista los supervivientes
```

Ejecutado contra la partida real (Ruta 1 → Pueblo Lilii → Ruta 1), sobre 0x33000000+2 MB:

```text
snap      1.048.576 posiciones de 16 bits
stable      1.000.446
changed        25.635
back            5.566
```

### Lo que se encontró

Entre los supervivientes hay **cuatro direcciones que se mueven a la vez**, con el mismo valor,
y que además están duplicadas por pares a una distancia constante de 0x121E00:

```text
0x330DDCA8   0x330DDE48      (separadas 0x1A0)
0x331FFAA8   0x331FFC48      (las mismas, +0x121E00)
```

| Zona | Valor en las cuatro |
|---|---|
| Ruta 1 | **10** |
| Pueblo Lilii | **1** |

Estable en tres lecturas seguidas, y el contexto de las dos primeras es idéntico byte a byte, lo
que confirma que son dos registros del mismo tipo y no una casualidad:

```text
+0x00  0461BB94 / 0461BBC8   puntero, difiere en 0x34
+0x04  00000001
+0x08  00000001
+0x0C  000F0111
+0x10  <<< la zona
+0x14  000F0110
+0x20  00000026
```

Otras dos direcciones que salieron con el mismo valor en la Ruta 1 (`0x330F8BA8`, `0x330FB548`)
se desperdigaron a basura al cambiar de zona: eran ruido, y se descartan.

### Qué numera ese valor: el área de `encdata`

Se probaron cuatro tablas. Las tres primeras **no** encajan, y se dejan anotadas para que nadie
las vuelva a probar:

| Hipótesis | Resultado |
|---|---|
| Índice de `zonedata` (380 zonas) | **No.** La Ruta 1 son las zonas 0-5, 77 y 318-320; el valor medido es 10 |
| `WorldIndex` de `worlddata` | **No.** La Ruta 1 aparece con world 22, 27, 28, 33-40, 55 y 241-243 |
| `MetLocation`, la numeración de PKHeX y del propio Pokémon | **No.** Buscar en los 2 MB una posición que valiese 6 u 8 en la Ruta 1 y 24 en Pueblo Lilii da **cero coincidencias** |
| **Índice de área de `encdata`** (336 áreas) | **SÍ** |

Para descartar la primera se recorrieron por fuerza bruta todos los tamaños de entrada entre
0x40 y 0x60, todos los inicios hasta 0x400 y todos los campos: **ninguna combinación** hace que
la zona 10 sea Ruta 1 y la 1 sea Pueblo Lilii a la vez. No era un problema de desalineación.

La que encaja es la cuarta, y encaja exacto:

```text
area  10  ->  004 - Ruta 1          <- medido en la Ruta 1
area   1  ->  016 - Pueblo Lilii    <- medido en Pueblo Lilii
```

Es **la misma numeración que usa el randomizador**, la de las 336 áreas de `encdata` (§19), así
que la zona actual y las tablas de encuentros hablan el mismo idioma sin traducción alguna. El
nombre sale de `Area7.GetArray(encdata, zonedata, worlddata, metlist)`, que ya se usa para
volcar y verificar la randomización.

Trampa que costó un rato: hay que leer los GARC con `GetlzGARCData`, que descomprime, y no con
`GetGARCData`. `zonedata` viene en LZ11 (`11 B0 7C 00` → 31.920 bytes) y leerlo sin descomprimir
da 115 zonas con valores absurdos en vez de las 380 reales. Además `zonedata` trae **dos**
subficheros —zonas y mapa de mundos— y el de mundos no está en el GARC `worlddata`.

### Verificación a ciegas (2026-08-19)

La prueba final no fue comparar contra un dato conocido, sino **predecir**: con el jugador en un
sitio que no se le había preguntado, PermaLocke leyó las cuatro direcciones, las tradujo y dijo
`016 - Pueblo Lilii`. Acertó.

También se comprobó, cerrando y reabriendo el emulador, que:

- la mochila **sigue en `0x33011934`** y su tabla de punteros valida;
- las cuatro direcciones de zona **siguen donde estaban**, con el mismo valor en las cuatro;
- el delta entre la mochila y la primera de ellas es **0xCC374**.

### Lo que sigue sin resolver

**Localizar esas cuatro direcciones sin quemarlas.** Hoy son fijas, que es justo lo que este
proyecto no acepta. Sobreviven a un reinicio y están a un delta constante de la mochila, cuyo
bloque sí se sabe localizar por su estructura (§22), así que el camino es anclarlas ahí y exigir
que **las cuatro copias coincidan** antes de creerse el valor: si discrepan, PermaLocke asume que
no sabe dónde está el jugador y no toca nada, igual que se decidió con la bandera de combate
(§16). Falta comprobar ese delta en más de una sesión y en la máquina de otro jugador.

La vía del log de Azahar quedó **descartada**: subir `Service.FS` a Trace no produce ni una línea
—`LOG_TRACE` no está compilado en la build de release— y a nivel Debug el emulador solo registra
`ReadRomFS` al arrancar. Un cambio de zona no deja rastro: el log creció 173 KB y las líneas de
lectura siguieron siendo 4, todas del arranque. **No hace falta ningún parche nuevo al fork.**

### Anclada a la mochila, no a una dirección (2026-08-19)

`ZoneLocator` no busca nada: lee las cuatro copias **a un delta fijo del bloque de la mochila**,
que es la única estructura que se sabe localizar por sí sola (§22). Los deltas reproducen las
distancias medidas —0x1A0 entre los dos registros de un par, 0x121E00 entre los pares— y hay un
test que lo fija, de modo que un retoque descuidado salta.

```text
mochila + 0xCC374    mochila + 0xCC514
mochila + 0x1EE174   mochila + 0x1EE314
```

El ancla por sí sola no basta, así que una lectura solo se cree cuando **las cuatro copias
coinciden** y ninguna cae en memoria en blanco. Lo segundo importa más de lo que parece: el
**área 0 es un área real** (Ruta 1), así que un ancla equivocada que aterrice sobre ceros
diría con toda confianza que el jugador está en la Ruta 1.

La señal de vida costó un intento fallido y merece anotarse: las palabras **pegadas** al campo
de zona no sirven, porque cambian con la zona y llegan a valer cero. La de -0x04 tenía 0x000F0111
en Pueblo Lilii y **cero** en la Ruta 1, así que la primera versión se negó a dar una lectura que
era correcta. Lo que sí se mantiene en las dos zonas y en las cuatro copias es el asa del
registro, 0x10 por delante del campo, que nunca es cero. Ese es el criterio.

Cuando algo no cuadra, `ZoneService.CurrentArea` devuelve **null**, y null es una respuesta de
verdad, no un fallo que haya que rellenar con una suposición: la regla que retira las Poké Balls
no debe dispararse sobre una zona de la que PermaLocke no está seguro. Es la misma decisión que
se tomó con la bandera de combate en el §16.

Coste: una vez cacheada la mochila, saber la zona son **48 bytes de lectura**, así que se puede
consultar en cada ciclo del monitor sin pensárselo.


Verificado contra el juego, con el ancla ya en su sitio: en la Ruta 1 devuelve **área 10** y, tras
cambiar de zona sin decírselo, **área 1** — Pueblo Lilii. Las cuatro copias de acuerdo en ambos
casos, y **15 ms** por consulta frente a los 5.500 que costaría barrer.
`TryResolve` es una función pura y ahí están los once tests: cuatro copias de acuerdo, una copia
desincronizada, memoria en blanco que no debe leerse como área 0, un área que el cartucho no
tiene, y los deltas. Ninguno necesita el emulador. En la sonda, `Probe --zone now`.
### Estado de la regla de las Poké Balls

| Pieza | Estado |
|---|---|
| Retirar y devolver objetos de la mochila | **RESUELTA Y VERIFICADA** — §22 |
| La regla completa que junta las tres piezas | **IMPLEMENTADA Y APAGADA** — ver §24 |
| Saber a qué zona ha cambiado el jugador | **RESUELTA Y VERIFICADA** — área de `encdata`, predicción a ciegas acertada |
| Localizar las direcciones de zona sin quemarlas | **RESUELTA Y VERIFICADA** — ancladas a la mochila, siguen un cambio de zona en 15 ms; falta comprobarlo en otra máquina |
| Bandera de combate y salvaje | Encontradas a mano en el §16, **sin localizador** |

---

## 24. La regla de las Poké Balls, implementada (2026-08-19)

El §14 fijó el objetivo: que la app no audite la captura después, sino que **el juego se quede
sin poder capturar** cuando la zona ya gastó su encuentro. Con el §22 (mochila) y el §23 (zona)
resueltos, ya se puede.

### Las dos numeraciones y el puente entre ellas

Aquí se encuentran dos formas de nombrar una zona:

- el juego guarda el **índice de área de `encdata`** (§23);
- la run identifica una zona por el **nombre normalizado de su localización**, porque es lo que
  lleva encima un Pokémon capturado (`EncounterService.NormaliseLocationId`).

El puente es el **nombre**, no el número: `ZoneTable` traduce área → nombre, y ese nombre
normaliza al mismo id que la run guardó al registrar la captura. La tabla la genera
`PermaLocke.RomTool zones` leyendo el cartucho y se guarda en `Data/zones.json`; **no se escribe
a mano**, y la app no necesita la ROM en tiempo de ejecución.

### Las ocho áreas que no se pueden juzgar

De las 336 áreas, **328 cubren una sola localización y 8 cubren dos**. Las ocho tienen
encuentros, así que importan:

```text
área   0: Ruta 1 | Mar de Melemele          área  81: Colina del Recuerdo | Afueras de Akala
área   2: Mar de Melemele | Ciudad Hauoli    área 139: Aldea Tapu | Ruta 14
área   4: Ruta 3 | Bahía Kalae               área 140: Ruta 15 | Ruta 16
área  73: Resort Hanohano | Playa de Hanohano  área 225: Antiguo Paso de Poni | Arrecife de Poni
```

Que la Ruta 1 esté gastada no dice nada del Mar de Melemele, así que en esas ocho **la tabla se
niega a responder** y la regla no actúa. Son 8 de las 74 áreas con encuentros: se pierde
cobertura, pero se gana no vaciarle la mochila a nadie en la zona equivocada.

### Qué hace y qué no

`BallControlService` decide **cuándo**; no sabe nada de bloques de memoria ni de emuladores.
Habla por dos puertos de `Core`, `IZoneProvider` e `IItemWithholder`, implementados por
`ZoneService` y `BagService`. Los estados que puede devolver son explícitos:

| Resultado | Qué pasa |
|---|---|
| `Withheld` | La zona está gastada: se retiran las bolas y se apunta cuántas |
| `Returned` | El jugador ha salido a una zona libre: se devuelve exactamente lo retirado |
| `ZoneAvailable` | La zona conserva su encuentro; no se toca nada |
| `Unchanged` | Nada ha cambiado desde la última comprobación |
| `ZoneUnknown` | No se puede afirmar la zona; **no se toca nada** |
| `ZoneAmbiguous` | El área cubre dos localizaciones; **no se toca nada** |
| `Disabled` | La regla está apagada o no hay lista de bolas |

Los tres últimos son la parte importante, y la mayoría de los once tests van sobre ellos: no
escribir es el comportamiento correcto siempre que haya duda.

**Solo escribe cuando la respuesta cambia.** Quedarse quieto en una zona gastada no reescribe la
mochila en cada ciclo del monitor; hay test de ello. Y cada retirada y cada devolución dejan un
`BallsWithheld` / `BallsReturned` con la zona, el área y las cantidades: el jugador tiene derecho
a saber por qué se le ha vaciado la mochila.

### Qué bolas, y de dónde sale la lista

Las 26 bolas de Ultra Luna, en `Data/rules.json`. La lista no está escrita a mano: sale de la
lista de objetos del cartucho quedándose con los que acaban en «Ball» **en inglés y en español a
la vez**. Ese cruce es el que descarta la Bola de Humo, la Bola Luminosa y la Bola Férrea, que en
inglés son *Smoke Ball*, *Light Ball* e *Iron Ball* pero son objetos equipables, no bolas.

Sin lista en la configuración, la regla **se apaga sola** en lugar de decidir por su cuenta qué
quitarle al jugador.

### Estado

**Implementada y apagada.** `ballControl.enabled` está a `false` en `Data/rules.json`: compila,
tiene once tests y está cableada al monitor, pero **no se ha visto actuar contra el juego**. No
se enciende hasta probarla con el jugador delante.

Falta además la excepción del shiny que describe el §16 —devolver las bolas si el salvaje es
shiny y retirarlas al acabar el combate—, que necesita el salvaje y la bandera de combate, y
esas siguen encontradas a mano y sin localizador.

---

## 25. El emulador viaja con la app (2026-08-19)

Diez personas van a instalar esto, y el Azahar oficial **no sirve**: se niega a escribir en la
región donde vive el estado del juego y responde OK igualmente (§15). Pedirle a cada uno que se
compile un fork no es una opción, así que la app lo reparte.

`AzaharInstallation` ya tenía el mecanismo; lo que faltaba era la carpeta. Al arrancar busca
`Emulator\azahar.exe` **junto a su propio ejecutable**:

- si está, lo usa en **modo portátil** — la existencia de `Emulator\user\` hace que el emulador
  guarde ahí su configuración, sus mods y sus partidas, sin tocar la instalación que el jugador
  ya tuviera;
- y le **activa el servidor RPC**, que Azahar trae apagado y que solo puede cambiarse con la
  emulación detenida. Es el ajuste que cada jugador habría tenido que encontrar a mano.

Verificado sobre el publicado de verdad, no sobre la carpeta de compilación:

```text
Azahar propio encontrado en ...\Emulator\azahar.exe
Servidor RPC activado en ...\Emulator\user\config\qt-config.ini
```

El `qt-config.ini` se crea de cero con `enable_rpc_server=true` y su bandera `\default=false`,
que es la que impide que el emulador lo revierta al cerrarse.

### Qué se versiona y qué no

Los binarios del fork son 100 MB y **no están en el repositorio**: se reconstruyen desde
`github.com/Grenin430/azahar`. Lo que sí se versiona es `Emulator/LEEME.md`, con la procedencia,
los dos parches y el enlace al fuente. `PermaLocke.App.csproj` copia la carpeta entera **solo al
publicar** (`CopyToPublishDirectory`, nunca `CopyToOutputDirectory`): copiar 100 MB en cada
compilación de desarrollo no tendría sentido, y quien desarrolla ya tiene su Azahar abierto.

Si la carpeta está vacía la app **no falla**: avisa por el log y cae al Azahar instalado, con la
salvedad de que entonces las escrituras no surtirán efecto.

### Licencia

Azahar es GPLv3, así que repartir su binario obliga a ofrecer su fuente. Está en
`github.com/Grenin430/azahar` y el enlace viaja dentro del propio paquete, en `Emulator/LEEME.md`.
Es una obligación distinta de la de PermaLocke, que también es GPLv3 pero por PKHeX.Core y
pk3DS.Core.

Coste del paquete: **262 MB** — 161 de la app self-contained y 100 del emulador.

---

## 26. Fase 4: el gacha (2026-08-19)

Es en lo que se gastan los puntos, y sin ello la competición no tiene sentido. Hecho el motor,
la pantalla y la entrega al juego; los precios quedan a cero mientras no exista la fuente de
puntos, que son los logros.

### Los tiers son rangos de estadísticas, no listas

El diseño lo fijó el usuario y es mejor que el de la referencia:

| Banner | Reparto |
|---|---|
| POCHO | 15% T1 · **60% T2** · 25% T3 |
| DECENTE | 15% T2 · **60% T3** · 25% T4 |
| BUENO | 15% T3 · **60% T4** · 25% T5 |

| Tier | Total de estadísticas base |
|---|---|
| 1 | ≤ 400 |
| 2 | ≤ 490 |
| 3 | ≤ 535 |
| 4 | ≤ 590 |
| 5 | > 590, **40% legendario** |

Un tier es un **rango del total de estadísticas base**, no una lista de Pokémon. Ninguna especie
se queda fuera, no hay 807 ids que mantener, y —esto es lo importante— **sigue valiendo con la
ROM randomizada**, porque el módulo de datos de Pokémon baraja las estadísticas pero conserva el
total (§20). El suelo de cada tier es el techo del anterior, así que las bandas ni se solapan ni
dejan huecos.

`PermaLocke.RomTool species` genera `Data/species.json` leyendo el cartucho: 807 especies con su
total, si son especiales, sus habilidades y los 25 nombres de naturaleza. El reparto real:

```text
tier 1 (<=400)   327 especies      tier 4 (<=590)    46
tier 2 (<=490)   242               tier 5 (>590)     50
tier 3 (<=535)   142
```

**Aviso que salió de mirar los datos:** el tier 5 tiene 41 legendarios y solo **9** no
legendarios —los pseudolegendarios de 600—, así que el 60% no legendario se reparte entre nueve
Pokémon y saldrán muy repetidos. Se implementó como se pidió, pero conviene saberlo.

Los legendarios no pueden salir de los tiers baratos: su probabilidad es cero ahí **y** el pool
se parte por esa marca, así que ni por accidente.

### Reproducible, que es lo que separa un gacha de una promesa

La tirada se calcula con la **seed de la run** derivada por el número de tirada. Ambos viajan en
el evento `GachaRoll`, de modo que cualquiera puede recomputar la tirada número 7 de una run y
comprobar que dio lo que dice el historial. Nadie tiene que fiarse de lo que diga un jugador.

Un fallo real que esto destapó: `GachaPull` es un `record`, y un record compara los IV **por
referencia**. Recomputar una tirada nunca coincidía con la registrada, que es justo el único uso
para el que existe la comparación. Se le escribió `Equals` a mano.

Otra decisión: **no se cobra por un Pokémon que no se produjo**. La tirada se genera primero —es
pura y no toca nada— y solo después se gastan los puntos. Si el cobro falla, no se guarda nada.

Habilidades y nivel, a petición del usuario: la habilidad sale **al azar de entre todas las del
juego**, no de las de la especie, así que un Magikarp puede salir con Levitación; los IV son
aleatorios los seis; y todo llega a **nivel 1**, que subirlo es cosa del jugador.

Trampa que costó un rato: la lista de habilidades de `species.json` **no se filtra**, porque la
posición en ella *es* el id que usa el cartucho y quitar los huecos vacíos desplazaría todos los
ids a partir del primero.

### El Pokémon va al PC, y cómo se supo por dónde ir

El usuario pidió que el Pokémon apareciese en el juego, no solo en la run. La vía obvia era
localizar las cajas del PC en memoria, que es otra investigación como la del equipo o la
mochila, y encima con un agravante: **escribir un Pokémon en un hueco vacío nunca se ha hecho**.
Hasta ahora solo se han sobrescrito Pokémon existentes, y el escritor exige que el checksum del
bloque original valide. Equivocarse ahí corrompe la partida de otro, no la propia.

Antes de meterse en eso se miró **cómo lo hace BxnnyLocke**, que es material de referencia y
está permitido analizar (no copiar). La respuesta estaba en su carpeta de intercambio:

```text
Emulador/user/rtp/p/bckp/
    main_20260818_005359    445.440 bytes
    main_20260818_005511    445.440 bytes      (10 copias con marca de tiempo)
```

**445.440 bytes = 0x6CC00, exactamente el tamaño de un save de Ultra Luna**, y `main` es el
nombre del fichero de guardado de 3DS. Es decir: **no localizan las cajas, editan el save**, y
hacen copia con timestamp antes de tocarlo. Con PKHeX.Core, que ya se sabía que llevan.

Eso ahorró la investigación entera. `SaveBoxDelivery` hace lo mismo:

- busca el save bajo `sdmc/Nintendo 3DS/**/001b5100/**/main`, sin suponer los identificadores de
  consola, que Azahar genera y no siempre son ceros;
- **se niega a escribir si el juego está cargado** —lo pregunta por el RPC—, porque el emulador
  tiene el save en memoria y lo reescribiría encima, borrando el Pokémon;
- copia el save entero antes de tocarlo, y si la copia falla no escribe;
- construye el Pokémon como **del propio jugador**: mismo nombre de entrenador, mismo TID y SID,
  misma versión. Si no, el juego lo trataría como intercambiado y no obedecería;
- lo pone en el primer hueco libre y **relee el fichero** para confirmarlo. No se reporta como
  entregado nada que no se haya vuelto a ver en disco.

Verificado sobre una copia del save real, releída con PKHeX desde fuera:

```text
entrega -> Delivered: Slaking está en la caja 1, hueco 1
  especie 289  nivel 1  OT «Grenin430»  TID 842462
  habilidad 81 (Manto Níveo, que Slaking no tiene: es un gacha)
  movimientos 58/63/216/332      checksum válido: True
```

Los movimientos los deduce PKHeX del nivel. Si el análisis de legalidad se atraganta con un
Pokémon que el gacha ha hecho imposible, llega **sin movimientos** en vez de no llegar: el
jugador los enseña y sigue, que es un problema mucho menor que una entrega fallida.

**El precio, y es el mismo que paga la referencia: hay que cerrar el juego.**

### La animación

Pixel art de Lunala esquivando portales no se puede hacer sin assets: los sprites son de
Nintendo y no se redistribuyen, y séptima generación usa modelos 3D, no sprites. Sacarlos del
cartucho del propio jugador sería legítimo, pero los iconos están en formato de textura de 3DS y
al recortar pk3DS se eliminaron justo `ETC1`, `ImageUtil` y `CTR/Images/` (§19).

Así que la animación es **abstracta y generada por código**: el ultraespacio, cinco portales de
colores —uno por tier, con sus tokens en `Palette.xaml`— y una nave que los cruza durante 2,4
segundos mientras el portal que ha tocado se abre. Cero assets, y funciona en cualquier máquina.

Detalle de WPF que obligó a rehacerlo: **un `Storyboard` dentro de un `Style` no puede animar
otro elemento por nombre**, porque el estilo no tiene ámbito de nombres. Cada portal y la nave se
animan a sí mismos.

La espera de 2,6 segundos antes de revelar es presentación, no teatro: **la tirada ya está
decidida, guardada y entregada** antes de que empiece la cuenta.

### Estado

| Pieza | Estado |
|---|---|
| Motor, tiers, probabilidades | **HECHO**, 10 tests, probabilidades medidas sobre 20.000 tiradas |
| Reproducibilidad por seed | **HECHA** y con test |
| Entrega al PC del juego | **VERIFICADA** sobre una copia del save; falta hacerlo en la partida real |
| Pantalla y animación | **HECHAS**, sin ver funcionando todavía |
| Precios | **A CERO** mientras se prueba. Los reales son 100 / 225 / 300 |
| Logros, que son la fuente de puntos | **SIN EMPEZAR**: hoy no hay forma de ganar puntos |
| Tienda | Sin empezar |

---

## 27. Dos fallos que solo aparecieron apagando módulos (2026-08-19)

El usuario pidió que **las evoluciones y los tipos no se randomizaran**: si te toca un Rowlet
salvaje, quieres acabar con un Decidueye, y un Latios debe seguir siendo Dragón/Psíquico.
Apagar esos dos interruptores destapó dos fallos, y el segundo era grave.

### Apagar un módulo no quitaba su fichero del mod

`LayeredFsMod.Clear()` existía y **nadie lo llamaba**. Al regenerar con las evoluciones
apagadas, el fichero `a/0/1/4` de la generación anterior **seguía en la carpeta del mod**, el
juego lo cargaba tan contento, y el informe decía «0 evoluciones». Comprobado en el juego del
usuario:

```text
Rowlet    cartucho: -> Dartrix      su mod: -> Oshawott
Magikarp  cartucho: -> Gyarados     su mod: -> Pikipek
```

Es exactamente el tipo de mentira que la regla 3 prohíbe: la app afirmaba una cosa y el juego
hacía otra. Ahora `RandomizeAsync` **vacía la carpeta antes de escribir**, así que el mod
contiene exactamente lo que esta pasada ha generado y nada más. Un módulo apagado no deja
fichero, y el juego usa el del cartucho, que es lo correcto.

De rebote apareció el segundo: `PokemonDataRandomizer.VerifyAsync` releía siempre los tres
ficheros —personal, evoluciones y aprendizajes— y **reventaba la randomización entera** al no
encontrar el de un módulo apagado. Ahora salta lo que no se ha escrito.

### Una fuente aleatoria por aspecto, no una compartida

El §20 fijó que cada **módulo** deriva su fuente por un salt con nombre, para que activar o
desactivar uno no mueva los resultados de los demás. Dentro de `PokemonDataRandomizer` esa norma
no se había aplicado: tipos, estadísticas, habilidades, evoluciones y aprendizajes tiraban **de
la misma fuente en cadena**. Apagar los tipos desplazaba todo lo que venía detrás.

Eso significa que un jugador que cambiara un interruptor a mitad de partida se encontraría con
otras estadísticas y otras habilidades sin haberlas tocado. Ahora hay cinco fuentes derivadas:
`types`, `stats`, `abilities`, `evolutions` y `learnsets`.

### Lo que esto cambió en la partida en curso

Se regeneró e instaló con la misma seed. Comprobado releyendo el mod:

| Qué | Antes | Ahora |
|---|---|---|
| Tipos | Rowlet Tierra/Siniestro | **Planta/Volador**, como el cartucho |
| Evoluciones | Rowlet → Oshawott | **Sin fichero**: Rowlet → Dartrix |
| Estadísticas | barajadas | **barajadas de otra forma** |
| Habilidades | randomizadas | **randomizadas de otra forma** |
| Encuentros salvajes | — | **sin cambios**: van por otro módulo con su propio salt |

Las estadísticas y las habilidades cambian porque las fuentes derivadas son nuevas, y no hay
forma de evitarlo sin renunciar al arreglo. El usuario estaba en la Ruta 1, así que el momento
era el bueno.

### La lección

Los dos fallos son la misma familia: **el estado que sobrevive a un cambio de configuración**.
Un fichero que se queda de la vez anterior y una secuencia aleatoria compartida son las dos
formas que tenía el randomizador de arrastrar el pasado. Ninguna se veía sin apagar un módulo,
que es algo que hasta ahora nunca se había hecho.

---

## 28. Sprites de Pokémon: se pueden sacar de la ROM (2026-08-20)

El §26 daba los sprites por inviables: «séptima generación usa modelos 3D, y los iconos están en
formato de textura de 3DS, y al recortar pk3DS se eliminaron ETC1, ImageUtil y CTR/Images». Eso
era **cierto solo en parte**, y la parte falsa era justo la que bloqueaba.

### Dónde están y en qué formato

Barriendo los 333 ficheros bajo `a/` y mirando la firma de sus subficheros aparece el contenedor:

| Qué | Ruta | Contenido |
|---|---|---|
| **Iconos de Pokémon** | **`a/0/6/2`** | 1154 subficheros, LZ11 → BFLIM |
| Iconos de objetos | `a/0/6/1` | 769 subficheros, 32x32 |
| Retratos grandes | `a/2/7/3` | 1157 subficheros de 256x256, **estos sí ETC1A4** |

Un icono son **4096 bytes de píxeles + 40 de cola**. La cola es la firma `FLIM` seguida del
bloque `imag`, y ahí está todo lo que hace falta:

```text
+0x1C  ancho   0x40 (64)
+0x1E  alto    0x20 (32)
+0x20  alineación
+0x22  formato 7 = RGBA5551
+0x23  swizzle 4 = rotado 90°
+0x24  bytes de datos
```

**Son RGBA5551, no ETC1.** O sea que **no hace falta reincorporar nada de lo que se eliminó de
pk3DS**: el decodificador entero cabe en unas sesenta líneas y no arrastra ni `System.Drawing`
ni WPF, así que `PermaLocke.Randomizer` sigue cumpliendo la norma de no depender de UI.

### Las dos trampas del formato, y cómo se resolvieron

1. **Las texturas de 3DS van en mosaicos de 8x8 y dentro de cada mosaico en orden Morton**: los
   bits pares del índice son la X y los impares la Y. Con el orden equivocado el sprite sale
   reconocible pero descosido, que es lo peor que puede pasar porque parece que casi funciona.

   Para no decidirlo a ojo se midió: se probaron las ocho combinaciones (dimensiones almacenadas
   32x64 o 64x32 × mosaicos por filas o por columnas × Morton normal o traspuesta) puntuando cada
   una por la **discontinuidad en los bordes de mosaico**. La buena es la única que no deja
   costura: 1,17 frente a 3,09 de la siguiente. No es una opinión, es una medida.

2. **El swizzle 4 significa que la textura está guardada con los ejes cambiados**: los 64x32 que
   declara la cabecera viven en memoria como 32 de ancho por 64 de alto. Hay que destramar sobre
   las dimensiones almacenadas y **después** girar 90° en sentido antihorario. Sin ese giro todos
   los Pokémon salen tumbados.

Verificado mirando el resultado: Bulbasaur, Ivysaur, Venusaur y Mega Venusaur en los iconos 1 a
4; Pikachu en el 34; Staryu, Starmie, Mr. Mime, Scyther, Jynx, Electabuzz y Magmar seguidos en
los iconos 168 a 174.

### Lo que está implementado

| Pieza | Dónde |
|---|---|
| Lector y decodificador BFLIM | `Randomizer/Sprites/BflimTexture.cs` |
| Iconos del cartucho, con recorte del margen transparente | `Randomizer/Sprites/PokemonIconReader.cs` |
| Codificador PNG propio (zlib del framework, sin `System.Drawing`) | `Randomizer/Sprites/PngImage.cs` |
| Herramienta | `PermaLocke.RomTool sprites [--sheets]` |

```text
1154 iconos escritos en Data\sprites en 1362 ms (0 vacíos)
Tamaños más frecuentes tras recortar: 17x18 x35, 26x25 x28, 21x20 x17
```

Los sprites **no se versionan**: son de Nintendo. `Data/sprites/` está en `.gitignore` y cada
jugador los extrae de su propia ROM, exactamente igual que la randomización. Nueve tests cubren
la cola del BFLIM, el orden Morton, el recorrido de los mosaicos, el giro y el PNG.

### Lo que falta: la tabla especie → icono

Los iconos van **por especie en orden nacional, con las formas de cada una detrás**. El problema
es saber **cuántos iconos consume cada especie**, y el cartucho no lo dice en ninguna parte:

- La suma de `FormeCount` de las 807 especies da **1116**, y el contenedor tiene **1153** además
  del huevo. Sobran **37**.
- Y no sobran repartidos de forma regular: hay especies con **más** iconos que formas (**Pikachu
  tiene 10 y declara 8**) y especies con **menos** (Scatterbug y Spewpa declaran 20 formas cada
  uno). Ni siquiera el orden interno es el esperado: en las especies con forma de Alola, **la de
  Alola va primero** (Raichu de Alola en el 44, el normal en el 45).
- El campo `FormeSprite` de `personal`, que en sexta generación apuntaba a esto, **vale 0 en todas
  las entradas** de Ultra Luna.
- No hay tabla creciente de ~808 entradas ni en el RomFS ni en el `code.bin` del ExeFS —que, por
  cierto, **no está comprimido** y coincide byte a byte con lo que se lee en memoria en
  `0x00100000`, así que buscar ahí es barato y ya se hizo.
- Los textos `Forms` (1118 líneas) están indexados **por especie**, no por icono, así que tampoco
  sirven de puente.

Se intentaron cuatro alineamientos automáticos —conteo de formas, casi-duplicados por similitud
de bytes, color del Pokédex (que sí está en `personal`, byte 0x21 bits 0-5, y es correcto) y
similitud de paleta entre iconos consecutivos— y **ninguno da un resultado exacto**: aciertan
tramos largos y se desincronizan en puntos sueltos. Un mapeo «casi bueno» es justo lo que la
regla 3 prohíbe, porque enseñaría el sprite de otro Pokémon con toda la confianza del mundo.

**Estado: resuelto para las 807 mirando el contenedor tramo a tramo. Ver §30 y §30 bis.**
Las 1 a 649 salen de una cuenta que tiene que dar 866 exacto; las 158 restantes viven en un
bloque con otro orden y se identificaron una a una, con la comprobación de que el reparto cierre
sin iconos libres ni repetidos.

### Detalle que salió de paso: `a/0/9/2`

El fichero de 5 MB que BxnnyLocke randomiza y PermaLocke no toca, pendiente de identificar desde
hace sesiones, **no son los iconos**. Sus 759 subficheros llevan cadenas de depuración del
intérprete de scripts (`ccmode=%d Not Found`), así que es el contenedor de **scripts** del juego.
Encaja con que un randomizador lo toque: en séptima generación los iniciales y varios regalos se
entregan por script.

---

## 29. La animación del gacha, rehecha (2026-08-20)

La de §26 eran cinco bloques de XAML repetidos —treinta y cinco líneas cada uno, uno por tier—,
doce estrellas fijas y un rectángulo cruzando la pantalla. Funcionaba, pero era eso.

### Los portales salen ahora de la configuración

`GachaViewModel` expone una colección de `PortalViewModel`, uno por tier de `Data/gacha.json`, y
la vista los pinta con un `ItemsControl`. Añadir o quitar un tier cambia la animación **sin tocar
el XAML**, que es la misma norma que rige el resto de la app: los valores de juego viven en JSON.

Cada portal nombra su color de la paleta (`Tier3Brush`) y un `ResourceKeyConverter` lo resuelve
contra `Themes/Palette.xaml`. Así el view model no toca un `Brush` y los colores siguen todos en
el mismo sitio. Con `ConverterParameter=color` devuelve el `Color` en vez del pincel, que es lo
que necesitan los degradados.

### Qué se ve

- **Dos capas de estrellas** con paralaje, hechas con un `DrawingBrush` en mosaico. Ninguna
  imagen de por medio.
- **Los cinco portales laten** en reposo y el que toca **se abre**: el anillo engorda, se ilumina,
  late en grande y suelta un destello del color del tier.
- **La nave lleva estela**: dos trazos con degradado y un halo en la punta.
- **Un fogonazo del color del tier** recorre el panel al revelar.
- **La ficha del resultado entra con su propia animación** y dice de qué tier salió, con el borde
  y el título de ese color, y marca **SHINY** o **LEGENDARIO** cuando de verdad lo es.

### Dos detalles que sí importan

1. **Se apaga el portal antes de cada tirada.** Si no, dos tiradas seguidas del mismo tier no
   cambiarían `LastTier`, el `DataTrigger` no volvería a dispararse y el portal se quedaría
   quieto en la segunda. Es el tipo de fallo que solo aparece tirando dos veces con suerte.
2. **Las estrellas se mueven trasladando el rectángulo, no animando el pincel.** WPF congela los
   `Freezable` de un diccionario de recursos, así que animar el `Viewport` del `DrawingBrush`
   revienta en ejecución. Se mueve el `RenderTransform`, que siempre se puede.

Sigue en pie la trampa del §26: un `Storyboard` dentro de un `Style` no puede animar a otro
elemento por nombre. Un `DataTemplate` **sí** tiene ámbito de nombres propio, y por eso los
portales pueden animar sus tres capas internas desde `DataTemplate.Triggers`.

### Verificado en la app, no solo compilando

Se abrió la aplicación, se navegó a GACHA y se tiró dos veces:

```text
tirada 1: Leafeon SHINY, tier 3  -> portal azul abierto, ficha azul
tirada 2: Pyukumuku,     tier 2  -> portal verde abierto, ficha verde
```

Y de paso queda cerrado un pendiente del §26: **la entrega al PC del juego funciona sobre la
partida real**, no solo sobre una copia. El log lo dice y el jugador tiene los dos Pokémon en la
caja 1:

```text
SaveBoxDelivery: Copia de la partida guardada en main-20260820-062431.sav
SaveBoxDelivery: Leafeon entregado en la caja 1, hueco 19
SaveBoxDelivery: Pyukumuku entregado en la caja 1, hueco 20
```

También se corrigió un texto de la pantalla que se había quedado atrás: decía que el Pokémon
«todavía no se escribe dentro del juego» cuando lleva escribiéndose desde el §26.

---

## 30. La tabla especie → icono, construida a mano (2026-08-20)

El §28 dejó la extracción resuelta y el índice sin resolver: el cartucho no dice qué icono es de
qué especie, y ningún alineamiento automático daba un resultado **exacto**. Se construyó
mirando el contenedor tramo a tramo, con los iconos ya decodificados y numerados.

### Lo que resultó ser el orden

```text
icono 0          el huevo
iconos 1-866     especies 1-649, en orden nacional, cada una seguida de sus formas
iconos 867-1153  especies 650-807, en un orden que NO es el nacional
```

El corte es exacto y es lo que hace verificable toda la primera mitad: **las especies 1 a 649
ocupan exactamente 866 iconos**. El 866 es el último Genesect y el 867 es el primer Furfrou, así
que si la suma no da 866 la tabla está mal y `PokemonIconIndex.Build` **lanza** en vez de
devolver un mapa torcido.

### Por qué no bastaba con contar formas

Una especie no ocupa tantos iconos como formas declara. Hay de todo, y cada caso se comprobó
mirando el icono:

| Caso | Ejemplo |
|---|---|
| Más iconos que formas | Pikachu: **10 iconos, 8 formas** (el suyo y nueve gorras) |
| Muchos más | Unown: **46 iconos, 28 formas** |
| Menos | Mothim: **1 icono, 3 formas**; Arceus: **1 icono, 18 formas** |
| Pares idénticos | Clefairy, Dugtrio, Poliwhirl, Kingler, Croconaw, Sneasel, Roselia, Klink… son la variante de sexo, que el cartucho guarda como dibujo aparte aunque no sea una forma |

En total, 55 especies necesitan corrección. Están en `Adjustments`, dentro de
`Randomizer/Sprites/PokemonIconIndex.cs`, cada una con su comentario.

### La trampa de las formas de Alola

En las especies de Kanto con forma de Alola, **el icono de Alola va primero**: el 44 es el Raichu
de Alola y el 45 el normal. Coger el primero del grupo, que es lo natural, habría enseñado un
Raichu de Alola cada vez que el gacha diera un Raichu corriente. `NormalFormOffsets` corrige las
dieciocho, y en Dugtrio y Muk el desplazamiento es de **dos**, porque su forma de Alola tiene
además variante de sexo.

Excepción a la excepción que conviene no perder: **Exeggutor también pone Alola primero** (147
Alola, 148 normal), aunque a primera vista parezca lo contrario.

### Verificación

- La suma tiene que dar 866 y da 866.
- Tres rejillas completas releídas a ojo contra el orden nacional: **1-90**, **300-339** y
  **570-609**. Todas correctas, Pokémon a Pokémon.
- Diecisiete iconos verificados uno a uno están fijados en tests (`PokemonIconIndexTests`), junto
  con que ninguna especie comparte icono con otra.
- En la aplicación: Arcanine y Heracross salieron del gacha **con su sprite**.

## 30 bis. El segundo bloque, identificado entero (2026-08-20)

Las especies **650 a 807** ocupan los iconos 867 a 1153 en un orden que no es el nacional, ni el
de Kalos, ni el de Alola. Se buscó primero la tabla dentro del juego —en `personal`, en el RomFS,
en el `code.bin` sin comprimir, y como secuencia de 16 bits en todos los ficheros volcados— y
**no está**. Así que se identificaron los 287 iconos mirándolos.

### Lo que hizo el trabajo abarcable

El bloque **mantiene juntas las familias evolutivas y en orden**: Chespin, Quilladin y Chesnaught
caen en tres iconos seguidos. Así que se leyó familia a familia, no especie a especie. Las
familias no siempre son vecinas entre sí —de Noibat a Noivern hay cincuenta y seis iconos, y de
Pancham a Pangoro otros tantos—, pero cada una está entera.

Antes de mirar nada se firmaron los 1154 iconos y se agruparon por píxeles idénticos y por
silueta. Eso da gratis los límites de los grupos difíciles: los **20 Vivillon**, los **11 Furfrou**,
los 5+6+5 de Flabébé/Floette/Florges, los **28 de Minior** (14 meteoritos idénticos y 14 núcleos
de colores), los **4 de Mimikyu**, y las parejas idénticas que resultan ser las **formas Dominante**
de Gumshoos, Vikavolt, Ribombee, Araquanid, Lurantis, Salazzle, Togedemaru y Kommo-o.

### La comprobación que no perdona

El reparto tiene que **cerrar**: 158 especies, 287 iconos, ni uno libre ni uno usado dos veces.
Se llevó la cuenta de los tramos en una tabla aparte y se pidió el complemento.

Eso fue lo que cazó el único error de bulto. Togedemaru se había leído en 1024-1025 —dos iconos
que, mirados de cerca, son **dos dragones grises con placas doradas**, o sea Jangmo-o y
Hakamo-o— y al final quedaban seis iconos libres para seis especies, pero dos de esos iconos eran
**el mismo dibujo**, así que una especie se habría quedado sin sprite. Con Jangmo-o y Hakamo-o en
su sitio, Togedemaru cae en la pareja idéntica 1110-1111 —un erizo redondo con la cola en forma
de rayo— y el reparto cierra sin sobras.

`PokemonIconIndex.Build` hace esa misma cuenta y **lanza** si alguien edita mal la tabla: 158
entradas, 158 iconos distintos, todos entre 867 y 1153.

### Formas: cuál se enseña

Donde una especie tiene varios dibujos se lista **el corriente**, que no siempre es el primero
del grupo —la misma trampa que las formas de Alola—:

| Especie | Se enseña | Ojo |
|---|---|---|
| Furfrou | sin corte | es el **sexto** de sus once iconos, no el primero |
| Oricorio | estilo Apasionado (rojo) | el amarillo va antes |
| Lycanroc | diurno | |
| Wishiwashi | forma Individual | la forma Banco va antes |
| Minior | dentro del meteorito | los núcleos de colores van antes |
| Necrozma | normal | Melena Crepuscular, Alas del Alba y Ultra van **antes** |
| Magearna | blanca | la de color original va antes |
| Xerneas | modo Neutro | |

### Verificación

- El reparto cierra: 0 iconos libres, 0 solapes, 158 especies, 158 iconos distintos.
- Rejilla completa de las 158 en orden de Pokédex, releída entera: cada fila se lee como la
  Pokédex, que es exactamente lo que delataría un cambiazo.
- Las 807 especies resuelven a un PNG existente y no vacío en la caché que lee la aplicación.
- En la aplicación, tirando al gacha: **Crabominable** y **Minior** salieron con su sprite —y
  Minior con su meteorito, que es la forma correcta—, además de Meowstic, Heliolisk y Goomy.

Se probó también cruzar cada icono con el **color de Pokédex** que declara `personal`, pero el
color dominante de un icono de 32×32 con contorno negro no es fiable: Rowlet declara Marrón y su
icono se lee amarillo. Sirvió para una duda concreta —cuál de dos iconos era el morado— y no como
prueba. No se apoya nada en él.

### Cómo llegan los sprites a cada jugador

No se reparten: `PokemonSpriteService` los extrae de la ROM que el jugador ya tiene la primera
vez que se abre el gacha —1,4 s, 1154 PNG en `Data/sprites/`— y los cachea. Si no hay ROM, o si
algo falla, se anota en el log y la pantalla sigue funcionando sin dibujos. `Data/sprites/` está
en `.gitignore`.

Detalle menor y documentado: en las especies cuyo macho y hembra tienen dibujo distinto
(Frillish, Jellicent, Unfezant…) se usa el primero del grupo, que en Frillish resulta ser la
hembra. No se sabe cuál considera el juego el principal, así que no se elige por corazonada.

---

## 31. La ruleta del gacha (2026-08-20)

La animación del §29 se rehízo entera a petición del usuario, que eligió entre cuatro conceptos:
**ruleta de siluetas**, con **escalada de rareza con engaño** y **duración larga en los tiers
altos**. Es la primera animación del proyecto que usa los sprites del cartucho (§28) para algo
más que la ficha final.

### Qué se ve

1. Una tira de **118 celdas** cruza la pantalla a toda velocidad, con los iconos **a todo color**,
   y unas **rayas de velocidad** se encienden detrás mientras va lanzada.
2. La tira **acelera, cruza a velocidad constante y frena larguísimo**. La frenada ocupa casi la
   mitad de la tirada: se ve a los Pokémon pasar cada vez más despacio.
3. Un **desenfoque** que baja de 8 a 0 hace de velocímetro, y se retira **antes de los clics**,
   para que se pueda leer qué Pokémon pasa por el marcador en cada uno.
4. **El final a clics**: la rueda se planta **tres casillas antes** y avanza **de una en una**, con
   su pausa entre cada una y un golpe del marcador y un tirón del visor en cada avance. El último
   clic **se pasa de largo y vuelve**. Mientras tanto el visor se **acerca** un 6%.
5. Al parar: fogonazo del color del tier, **onda de choque**, **ráfaga de ocho rayos** alrededor de
   la casilla y una **sacudida** de todo el panel. La celda ganadora **crece con un rebote** y las
   demás bajan a opacidad 0,42.
6. La ficha del resultado entra con su propio golpe, y el **nombre un pelín después**, para que
   sean dos impactos y no uno. Si hay marca de shiny o legendario, **late tres veces** y cae una
   segunda onda encima de la primera.
7. La escalera de tiers de arriba **sube durante la tirada**, y cada subida trae su fogonazo, su
   golpe de marcador y su sacudida.

Duración: de **6 s en el tier 1 a 11 s en el tier 5**.

### Por qué el final es a clics

Una rueda que se desliza hasta pararse resuelve la tirada **en un solo instante**. Una que se
planta tres casillas antes y camina las tres últimas la resuelve **tres veces**, y en cada pausa
el jugador lee el Pokémon que hay bajo el marcador y piensa que es el suyo. Es el recurso de las
tragaperras de toda la vida y es de lejos lo que más aporta de todo lo que se ha añadido.

El número de clics es **el mismo para todos los tiers a propósito**. Hacer que los raros
clicasen más sería más vistoso, pero cantaría el resultado antes de que la rueda llegue, y
entonces el engaño del tier no engañaría a nadie.

### El engaño, y por qué no incumple la regla 3

La pantalla arranca encendida en el **tier más barato** y sube al real cuando la rueda va por el
80% —los dos tiers de arriba suben en **dos pasos**, para que un legendario se note venir—. Es
decir: durante unos segundos la pantalla enseña un tier que no es el que ha tocado.

Eso es presentación y solo presentación. **El Pokémon, los puntos y el evento se deciden, se
guardan y se entregan antes de que la rueda empiece a girar**; `LastTier` siempre tiene el tier
real y `DisplayedTier` es lo único que miente, durante tres segundos, sobre un resultado que ya
está escrito. Las siluetas que pasan tampoco son candidatas, y el texto de la pantalla lo dice.

La duración va de **6 s en el tier 1 a 11 s en el tier 5**, para que la espera larga solo llegue
cuando ha tocado algo bueno.

### Dónde vive cada cosa

| Pieza | Dónde |
|---|---|
| Qué celda gana, cuánto dura, cuándo sube el tier | `GachaViewModel.SpinAsync` |
| La tira, sembrada con la seed de la tirada | `GachaViewModel.BuildReel` |
| Aceleración, frenado, rebote, desenfoque y fogonazos | `GachaView.xaml.cs` |

El code-behind existe porque **dónde tiene que parar la rueda depende del ancho del visor en ese
momento**, y un Storyboard escrito en XAML no puede saberlo. No decide nada ni toca datos: el
view model dice *qué* y la vista *cómo*. La tira se siembra con la seed de la tirada, así que
recomputar una tirada la reproduce también visualmente.

### Dos fallos de WPF que costaron encontrar

1. **`OpacityMask` con un `ImageBrush` no pinta nada** sobre estos iconos. La ruleta salía
   completamente vacía aunque el log confirmara 45 de 46 celdas con icono. La solución es no
   depender del recorte: `PokemonSpriteService.GetShadow` **cocina la silueta en un bitmap**,
   conservando el alfa y tirando el color, y la celda muestra un `Image` normal.

2. **Un `ItemsControl` dentro de un `Grid` se recorta al ancho disponible.** Medía **662 px en
   vez de 3312**, así que en cuanto la tira se desplazaba se salía entera de la vista y no se veía
   ni el fondo de depuración que se le puso para comprobarlo. La tira va ahora dentro de un
   **`Canvas`**, que mide a sus hijos con espacio infinito.

Los dos daban el mismo síntoma —una banda vacía— por causas distintas, y ninguno de los dos
falla al compilar ni deja rastro en el log. Se encontraron midiendo desde el propio code-behind:
`viewport=664 stripW=662 items=46` fue lo que destapó el segundo.

### Tercer fallo: revelar por reloj y no por la rueda

La primera versión revelaba al ganador con un `Task.Delay` de la misma duración que la animación.
Parecía equivalente y no lo era: **construir 118 celdas lleva su tiempo**, así que la animación
arranca bastante después de la petición y termina igual de tarde. El reloj llegaba antes y el
Pokémon se revelaba **con la rueda todavía en marcha**, apareciendo media casilla fuera de su
propio marco. Muy fácil de confundir con un error de cálculo del punto de parada, que es donde se
buscó primero.

Ahora el aviso lo da la propia animación (`Completed`), y el view model espera a eso. Queda una
red de tres segundos por si la vista nunca llegó a arrancar —una pantalla que no se cuelga— y va
holgada a propósito: si la red saltara antes que la rueda, volvería el mismo síntoma.

Medido con el ganador ya parado: `centroCelda=333, centroVisor=332`. Un píxel, que es el borde
del marco.

### Cuarto fallo: la ráfaga se comió la ficha del resultado

La ráfaga de rayos mide 300 px y se metió como hija del `Grid` de la ruleta, que ocupa una fila
**`Auto`**. La fila pasó de medir 104 a medir 300, se comió los 392 px del panel y la fila `*` del
resultado se quedó **sin sitio**: la ficha con el nombre, la naturaleza y los IV **dejó de
aparecer**, sin error, sin excepción y sin una línea en el log.

Es la tercera vez que el mismo mecanismo muerde en esta pantalla —ya pasó con los rectángulos de
fondo y su `RowSpan`—, así que conviene tenerlo como norma: **en un `Grid` con filas `Auto`,
cualquier adorno grande hay que sacarlo del flujo o fijarle la altura**. Aquí se le fija a la de
la ruleta; la ráfaga se sigue dibujando fuera, porque ese `Grid` no recorta, y quien la recorta es
el panel, que es justo lo que se quería.

Se encontró comparando dos capturas: la ficha estaba en la versión anterior de la animación y no
en la nueva, con la línea de estado diciendo tan tranquila que el Pokémon ya estaba en la caja.

### Verificado en la aplicación

Con la aplicación abierta y tirando de verdad: la rueda gira, frena, hace sus tres clics, se pasa
y vuelve; el tier sube con su fogonazo; y al parar caen la onda, la ráfaga y la sacudida con el
Pokémon revelado dentro de su marco y la ficha completa debajo. Salieron así **Mudkip**,
**Skuntank** y **Crawdaunt**.

La subida **en dos pasos** también se ha visto, tirando del banner caro: la pantalla pasó por
**tier 4** antes de quedarse, y salió **Haxorus** de tier 4, con el morado del tier en el portal,
en el marco de la ruleta y en el borde de la ficha.

Lo que **no** se ha visto en vivo es la celebración de **shiny o legendario**, porque no ha
tocado ninguno; el camino está cableado y es el mismo que el resto, pero está sin ver.

---

## 32. El visor Pokémon: el PC de la partida (2026-08-21)

La base pedida: **ver literalmente el PC que hay en el juego**, y al pinchar un Pokémon, su ficha.
Nada más por ahora; lo demás se monta encima de esto.

### De dónde salen las cajas

Del **fichero de partida**, con PKHeX, por la misma razón que la entrega del gacha escribe ahí
(§26): es un formato fijo que PKHeX lleva años leyendo y se comporta igual en las diez máquinas
de la competición, mientras que localizar 960 huecos de caja en memoria sería una investigación
entera y nueva.

El precio es distinto —y menor— que en la entrega. **Leer no exige cerrar el juego**, porque no
se escribe nada. Lo que pasa es que el fichero contiene **lo último que el jugador guardó**, así
que cuando el emulador tiene el juego cargado la pantalla **lo dice**, en vez de colar un PC
desfasado como si fuera el de ahora mismo.

`PlayerSave` se sacó aparte justo para eso: la entrega y el visor tienen que estar de acuerdo en
dónde está la partida y en si el emulador la está reteniendo. Antes esa lógica vivía dentro de
`SaveBoxDelivery` y solo la usaba él.

### Qué se ve

Las 32 cajas, cada una con sus **30 huecos**, vacíos incluidos: la caja del juego tiene treinta
agujeros estén llenos o no, y pintar solo los ocupados la convertiría en una lista. Se pasa de
caja con ‹ y ›, y da la vuelta como en el juego. Al pinchar, la ficha: naturaleza, habilidad,
objeto, ball, entrenador, dónde y a qué nivel se encontró, los movimientos, y una tabla con la
estadística, su IV y su EV. La barra mide el IV sobre 31, y un 31 se pinta en verde para que se
vea sin leer el número.

### Detalles que costaron algo

**Un Pokémon en caja no lleva sus estadísticas de combate.** El juego se las calcula al sacarlo,
así que la ficha enseñaría seis ceros. El lector hace lo mismo en memoria —`ResetPartyStats`—
antes de describirlo. No se escribe nada en la partida: el visor no toca el fichero.

**El símbolo de sexo salía como un cuadrito.** Lo natural es sospechar de la codificación del
fichero fuente, y era falso: los bytes eran UTF-8 correcto. Era la **fuente del titular**, que no
tiene ese glifo. Ese `Run` va con `Segoe UI Symbol` y ya está. Los caracteres se construyen
además desde su punto de código, para que quede una variable menos en un camino que cruza el
fichero fuente, el compilador y el render.

**PKHeX no reconoce una partida en blanco escrita a disco.** Se probó con `new SAV7USUM()` y con
`BlankSaveFile.Get`, y en los dos casos `TryGetSaveFile` la rechaza al releerla: le faltan marcas
que solo pone la consola o el emulador. Así que el recorrido de las cajas se separó en un método
que acepta la partida ya abierta, y los tests lo ejercitan con una partida montada en memoria: sin
emulador, sin ROM y sin datos personales del jugador. Lo único que queda sin test es abrir el
fichero, que es trabajo de PKHeX.

### Lo que la base todavía no hace

| Cosa | Estado |
|---|---|
| Formas | Se enseña el icono de la forma corriente. Un Raichu de Alola sale como un Raichu normal |
| Shiny | Se marca con la palabra: el contenedor de iconos no trae variantes shiny |
| Equipo | No sale. Solo el PC |
| Cruce con la run | Ninguno: el visor no sabe todavía qué Pokémon están vivos, muertos o son del gacha |
| Buscar y filtrar | Sin empezar |
| Mover o editar | No, y no está previsto: el visor es de solo lectura |

### Verificado en la aplicación

Contra la partida real del jugador: **149 Pokémon** en el PC de Grenin430, la caja 1 llena
(30/30), la caja 5 a 29/30 con su hueco vacío a la vista, y las fichas de **Electivire** y
**Shieldon** completas —naturaleza, habilidad, ball, entrenador, estadísticas con sus IV y sus
movimientos—. Los iconos son los de §28, sacados de la ROM del propio jugador.

---

## 33. Wonder trade (2026-08-21)

Entregas un Pokémon y vuelve otro que vale **más o menos lo mismo**: entre un 8% menos y un 10%
más de total de estadísticas base. La banda es toda la mecánica —hace del intercambio una apuesta
sobre *qué* Pokémon toca y no una manera de mejorar el que tienes—, así que vive en
`Data/wondertrade.json` y no en el código.

### Las tres decisiones que no son negociables

**El recibido conserva el nivel del entregado.** Si no, bastaría con entregar un nivel 1 del gacha
para sacar un nivel 50. No es configurable a propósito.

**El total base sale de la ROM**, de `Data/species.json`, no de PKHeX: el randomizador baraja las
seis estadísticas pero conserva el total, así que la banda sigue valiendo con la ROM randomizada.
Los **tipos** sí salen de PKHeX, y es correcto porque en esta run los tipos no se randomizan.

**Se decide, se escribe y se registra antes de animar nada.** Igual que el gacha. Si la escritura
en la partida falla, no hay animación: no se le enseña al jugador un Pokémon que no tiene.

### La única escritura que destruye algo

Todo lo demás que PermaLocke escribe **añade**. Esto quita. De ahí tres guardarraíles:

1. **Se comprueba que el hueco sigue teniendo lo que la pantalla cree.** Las cajas se leen del
   fichero; si el jugador ha jugado desde entonces, ese hueco puede tener otra cosa. Si la especie
   no coincide, el intercambio se rechaza en vez de sobrescribir lo que haya.
2. **Copia entera de la partida** antes de tocarla, como en la entrega del gacha.
3. **Se relee del disco** después, y solo entonces se da por hecho.

Además exige el juego cerrado, y se avisa **al armar el intercambio**, no después de haber
elegido a quién entregar.

`PokemonBuilder` se sacó de `SaveBoxDelivery` para que el gacha y el intercambio construyan
exactamente el mismo tipo de Pokémon: el del propio jugador, con su nombre y sus ids. Con
cualquier otro, el juego lo trata como intercambiado y no obedece.

### La animación

Lo pedido, en este orden: el sprite del que se va **se encoge y entra en su Poké Ball**, la bola
se cierra con un apretón y un fogonazo, sale disparada a la derecha girando, y **a mitad de su
viaje entra otra por la izquierda**, de modo que las dos se cruzan en mitad del panel. La que
llega se sacude tres veces, como una bola con algo dentro.

Y entonces, antes del Pokémon, los tres avisos, uno a uno:

1. **El tipo** o los dos tipos, en su color de siempre.
2. **La generación**.
3. **El total de estadísticas base**, con la diferencia respecto a lo que se entregó.

Solo después la bola revienta y sale el Pokémon.

Las bolas están **dibujadas con formas** —dos medias esferas, la banda y el botón—, no son un
asset: es geometría, como los portales del gacha. Los colores de tipo son los de siempre y viven
en la vista, porque son presentación y no reglas.

El aviso de que la bola ha llegado sale del **final real del movimiento**, no de un reloj, que es
la lección del §31: con un reloj los tres avisos empezarían con la bola todavía en el aire.

### Trampa que salió probando: el orden de los IV

Escribir un Pokémon y leerlo de vuelta devolvía los IV permutados. No es un fallo: son dos
órdenes distintos y los dos correctos. La partida guarda **HP/Atk/Def/Spe/SpA/SpD**, que es el
orden que espera quien escribe; el visor los lee en el orden en que **el juego los enseña**,
HP/Atk/Def/SpA/SpD/Spe. Está documentado en los dos sitios. Mezclarlos no rompe nada visible
—el total no depende del orden— pero cambia dos estadísticas de sitio sin avisar.

### Estado

| Pieza | Estado |
|---|---|
| Motor: banda, reproducibilidad, evento auditable | **HECHO** — 16 tests |
| Escritura del intercambio en la partida | **VERIFICADA CONTRA UNA COPIA REAL** del save: Ursaring salió, Garchomp ocupó su hueco, el total de Pokémon no cambió, y repetir el intercambio se rechazó solo |
| Panel del visor: armar, elegir, ver la banda | **VISTO EN LA APP** — Decidueye, 530 de total, banda 487-583 |
| La animación | **SIN VER** — exige un intercambio de verdad, que destruye un Pokémon de la partida del jugador. No se ha hecho sin permiso |
| Coste en puntos | Ninguno, como el gacha mientras no existan los logros |

---

## 34. Iconos de objeto y de tipo: qué hay en el cartucho y qué no (2026-08-21)

Buscando las Poké Balls y los iconos de tipo para el wonder trade se barrió el RomFS entero. El
resultado sirve para más cosas que el intercambio —la tienda va a querer iconos de objeto— así que
queda escrito, incluido lo que **no** existe.

### El barrido

El RomFS tiene **747 ficheros**, todos bajo `a/x/y/z` y diez por carpeta, y solo dos son enormes
(`a/0/8` con 722 MB y `a/0/9` con 1,4 GB). Así que se puede recorrer casi entero: abrir cada
contenedor como GARC, descomprimir cada subfichero y mirar qué es. Herramientas de un solo uso, en
el scratchpad, no en el producto.

Contenedores con imágenes BFLIM sueltas:

| Contenedor | Qué es |
|---|---|
| `a/0/6/1` | **769 iconos de objeto**, 32x32 RGBA5551 |
| `a/0/6/2` | los 1154 iconos de Pokémon del §28 |
| `a/1/5/8` | 122 retratos de entrenador, 64x64 |
| `a/2/9/4` | las 15 judías Poké |
| `a/2/6/4` | 48 imágenes que salen mal decodificadas: otra distribución de mosaicos, sin resolver |

### Los iconos de objeto: `a/0/6/1`, índice = id del objeto menos uno

No hizo falta deducirlo: **los dieciséis primeros iconos son las dieciséis Poké Balls**, en el
orden en que el juego numera los objetos —Master, Ultra, Honor, Poké, Safari, Malla, Buceo, Nido,
Acopio, Turno, Lujo, Premier, Ocaso, Sanación, Rápida, Gloria—. Verlas en fila es la comprobación.

El contenedor **se acaba en el 768**, y ese último icono es el «?» que el juego enseña para un
objeto sin dibujo. Los objetos numerados por encima —los cristales Z están en el 776-793— **no
tienen icono aquí**.

`ItemIconReader` lo lee igual que el de Pokémon: LZ11 + BFLIM + recorte del margen transparente.
La aplicación extrae **solo las dieciséis bolas** a `Data/sprites/balls/`, porque las otras 753 no
las quiere nadie todavía.

### Los iconos de tipo: no existen

Se buscaron de tres maneras y ninguna los encontró:

1. **Como contenedor suelto**: no hay ningún GARC con 18 imágenes de tipo.
2. **Talladas de los ALYT**, que son las pantallas de interfaz: se recorrieron todos buscando la
   cabecera `FLIM` incrustada y midiendo cada imagen. Aparecen miles, pero **ninguna placa** —una
   imagen ancha y baja, de 32 a 128 por 10 a 32— repetida 18 veces.
3. **Por número**: se buscaron grupos de 16 a 22 imágenes del mismo tamaño en todo el RomFS.

Lo que sí hay, exactamente 18 y una por tipo, son los **cristales Z** (`a/1/5/5` y `a/1/4/2`,
32x32 RGBA5551). Pero **no están en orden de tipo** —el primero es amarillo y el Normalium es
blanco—, así que emparejarlos con su tipo sería adivinar por color. Los cristales tampoco están en
el contenedor de objetos, donde el id sí habría dado la respuesta sin dudas.

**Conclusión: el cartucho no guarda placas de tipo porque el juego las compone**, con un color de
fondo y el nombre del tipo escrito encima. Que es exactamente lo que hace la pantalla del
intercambio, así que no se pierde nada. Lo que no se va a hacer es colgar la etiqueta «icono del
cartucho» de una cosa emparejada a ojo por su color.

### La animación, en grande

Con la bola de verdad, la escena creció: bolas de 150 px, Pokémon de 200 y 260, barras de cine
arriba y abajo, rayas de velocidad de fondo, sacudida de toda la escena y onda de choque en cada
impacto, y al abrirse la bola **el color del tipo inunda el fondo** y se queda de ambiente. Los
tres avisos son ahora placas de 26 px que entran desde el triple de su tamaño, la generación a 30
y el total a **72**.

Detalle de WPF que se repite: el resplandor del tipo se pinta con el **relleno** del color y una
**OpacityMask con degradado**. Al revés no vale, porque un degradado no se puede colorear desde un
binding. Y la OpacityMask sí funciona aquí: la que no pinta nada es la de `ImageBrush` (§31).

### Y un fallo de distribución que costó ver

Los dos botones del visor se pusieron en una columna `Auto` con el resumen al lado en una `*`. En
una ventana estrecha los botones se comen el ancho, la otra columna se queda a **cero**, y el
resumen envuelve **letra a letra**: treinta y tres líneas, seiscientos píxeles de alto, y la caja
de arriba encogida a tres filas con barra de scroll. Sin error y sin log, otra vez. Ahora van en
un `WrapPanel` con el resumen debajo.

---

## 35. El intercambio no se veía: un record que compara por valor (2026-08-21)

Los cuatro primeros wonder trades del jugador funcionaron —Mew por Genesect, Giratina por Arceus,
Aromatisse por Lunatone, todos dentro de su banda y escritos en la partida— pero **la caja seguía
enseñando el Pokémon entregado**. Ni al terminar el intercambio ni pulsando «releer la partida».

La partida estaba bien. Lo que estaba mal era la pantalla.

### Por qué

El visor rellena los treinta huecos desde `OnSelectedBoxChanged`, y `LoadAsync` reasignaba
`SelectedBox` esperando que eso lo disparase. `BoxTabViewModel` es un **record**, así que compara
por valor: número, nombre y cuenta. Y un intercambio deja los tres **exactamente iguales** —la
caja 1 tenía 30 Pokémon antes y 30 después—, de modo que el objeto nuevo era **igual** al viejo,
`SetProperty` no notificaba nada, y la rejilla se quedaba con los huecos de la lectura anterior.

Es el mismo patrón que ya mordió dos veces en el gacha y una en el visor: **nada falla, nada se
registra, y lo que se ve es mentira**. Aquí además el caso concreto que lo destapa es justo el que
la función existe para cubrir, porque un intercambio es la única operación que cambia el contenido
de una caja sin cambiar su cuenta.

### El arreglo

Rellenar los huecos es ahora un método propio, `ShowBox`, y `LoadAsync` lo llama **a mano** al
terminar, sin depender de que ninguna propiedad haya «cambiado».

Se comprueba solo: `ShowBox` limpia la selección, así que pulsar «releer la partida» con un
Pokémon seleccionado tiene que dejar la ficha en blanco. Antes no la dejaba.

### De paso, en la animación

- Los **tres avisos se apagan** antes de que salga el Pokémon. Se quedaban puestos y la ficha
  final caía encima de ellos y de la bola.
- La ficha va sobre un **panel oscuro con el borde del color del tipo**: sobre el fondo claro del
  intercambio y sobre la bola no había quien leyera los datos.
- El fondo pasa a ser **azul y claro**, con rayos girando, en vez de negro.
- Las bolas viajan en **arco** y la que llega **cae y rebota** dos veces antes de quedarse, en vez
  de deslizarse en horizontal.
- `Reset` devuelve también la escala y la Y de la bola que llega. Sin eso, el segundo intercambio
  de una sesión empezaba con una bola gigante, porque el estallido del anterior la deja en 3,4.

No se ha podido copiar la animación del juego fotograma a fotograma: no hay forma de verla desde
aquí. Lo que hay es una reconstrucción de la secuencia conocida —fondo claro, bolas cruzándose en
arco, la que llega cae y se abre—, y queda pendiente de que el jugador diga en qué se separa.

---

## 36. Logros y penalizaciones (2026-08-21)

Por fin existe una manera de ganar puntos, y otra de perderlos.

### Lo que cuesta perder

| Regla | Coste |
|---|---|
| Cada Pokémon que cae | **−25** |
| Que caiga el equipo entero | **−100**, además de las muertes que lo componen |
| Tope de equipos caídos | **4** veces, o sea 400 como mucho |

Y **el saldo puede quedarse en negativo**. Eso no es un detalle: `PointsService.SpendAsync`
rechaza lo que no se puede pagar, que es correcto para una tienda y falso para una regla. Una
penalización no es una compra, así que `PenaltyService` escribe su evento con delta negativo y
ya está. Como el saldo es una proyección sobre el log, el negativo sale solo.

Los números viven en `Data/penalties.json`. Si el fichero falta, el catálogo cae a **los números
reales**, no a cero: un fichero perdido no puede volver las muertes gratis en silencio.

### Un equipo caído es un flanco, no un estado

El equipo se queda a 0 PS hasta que el jugador llega a un centro Pokémon, y el vigilante mira cada
tres segundos. Cobrar por el estado cobraría decenas de veces por un solo desastre. Así que se
cobra en el **flanco**: solo cuando el equipo pasa de tener a alguien en pie a no tener a nadie.

El indicador arranca en «había alguien en pie», de modo que la primera lectura tras abrir
PermaLocke solo puede inicializarlo, nunca cobrar. Lo que se paga por eso: **un equipo que cae con
la aplicación cerrada no se cobra como equipo caído**. Las muertes que lo componen sí, porque esas
se deducen de la run y no de una bandera en memoria. Queda dicho en vez de disimulado.

Pasado el tope, el evento **se sigue escribiendo** con delta cero. Ocurrió, así que la historia
tiene que decirlo; simplemente deja de costar. No registrar nada haría que el log mintiera.

### Los logros

El progreso es una **proyección sobre el historial**, igual que el saldo: se recalcula entero cada
vez que se abre la pantalla, así que no hay contador que pueda desviarse de lo que pasó.
Desbloquear y cobrar están separados —la pantalla enseña `3 / 5` y el jugador cobra con un botón—
para que el log diga de dónde salió cada punto y cuándo.

### Un logro que no se puede detectar no se borra: se marca

`Data/achievements.json` nombra un evento por logro. Si ese evento no existe en esta compilación,
el logro **se queda en la lista**, marcado como «sin detectar» y diciendo qué disparador pidió.

Es la diferencia entre una lista de la competición que se ve completa y una que ha perdido
entradas por el camino sin avisar. Hoy PermaLocke detecta capturas, muertes, tiradas de gacha,
intercambios, compras, equipos caídos, cap de nivel y randomizaciones. **No** detecta todavía
entrenadores derrotados ni pruebas superadas, y esos dos salen marcados en pantalla.

### Lo que no se ha hecho, y por qué

El usuario pidió replicar el apartado de logros de BxnnyLocke. **Su lista está compilada dentro de
`BxnnyLocke.dll`**: no hay ningún JSON, ninguna base de datos ni ningún fichero de configuración
en su instalación que la contenga —se buscó—. Sacarla exigiría descompilar su ensamblado, que es
justo lo que la regla 1 de este repositorio prohíbe: se analizan sus formatos, no se copia su
código. Así que la lista tiene que venir del jugador, y el motor está montado para que meterla sea
editar un JSON.

### Trampa de WPF, otra más

`ProgressBar.Value` enlaza **en dos direcciones por defecto**. Contra una propiedad calculada de
solo lectura eso no da un aviso: tira una `XamlParseException` en pleno *measure* y la ventana sale
**entera en negro**, sin barra lateral ni nada. Hay que poner `Mode=OneWay` a mano.

---

## 37. La lista de logros de la competición (2026-08-21)

El jugador pasó una captura de la pantalla de BxnnyLocke y dictó la lista. Son **21 logros**: las
doce pruebas del recorrido insular a 100 puntos cada una, el alto mando (campeón, 300; y de nuevo,
0/2, 300), las pegatinas a 25/50/100 con 75/125/200, y cuatro más —100 movimientos Z, 200 huidas,
1 variocolor y 100 entrenadores—. Los puntos de esos cuatro los puse yo sobre la misma escala y se
cambian en el JSON.

La pantalla se rehizo como la de la captura: **rejilla de tres columnas**, tarjeta con medalla,
descripción, `0/1`, puntos y botón.

### El problema de verdad: PermaLocke no ve casi nada de eso

De los 21, **ninguno** es detectable hoy. No hay evento de prueba superada, ni de pegatina, ni de
entrenador derrotado, ni de huida, ni de movimiento Z. Un motor puramente automático habría dejado
una pantalla de 21 tarjetas congeladas a cero.

Las dos salidas malas eran fingir la detección —regla 3— o esconder los logros que no se pueden
contar. La salida buena es **dejar que los marque el jugador**, y que el historial diga que fue él:

- Un logro **automático** cuenta un tipo de evento del log. No se puede tocar a mano, porque
  entonces habría dos números para lo mismo sin manera de saber cuál vale.
- Un logro **manual** —sin disparador, o con uno que esta compilación no conoce— sale con dos
  botones, `+1` y `COMPLETAR`. Cada pulsación escribe su propio `AchievementProgressed` con su
  delta, su fecha y `EventSource.Player`.

El progreso se reconstruye sumando esos deltas, igual que el saldo se reconstruye sumando los
puntos. No hay contador guardado en ningún sitio que pueda desviarse de la historia, y la
diferencia entre «lo vio PermaLocke» y «lo dijo el jugador» queda escrita en cada evento en vez de
perderse.

Cuando se instrumente la detección de pruebas o de entrenadores, basta con ponerle su `trigger` al
logro en el JSON: pasa a automático y los botones desaparecen solos.

### Las medallas no son cristales Z

La captura enseña cristales Z como medalla. El cartucho los tiene —18, en `a/1/5/5`— pero no en
orden de tipo y no en el contenedor de objetos, donde el id habría dado la respuesta (§34).
Emparejarlos sería adivinar por color, así que la medalla es un rombo dibujado con la inicial del
logro, y cambia de color al desbloquear y al cobrar.

### Nota sobre los puntos ya cobrados

La lista anterior era de prueba y el jugador llegó a cobrar cinco de sus logros: 145 puntos. Al
cambiar la lista, esos logros dejan de existir pero **los puntos se quedan**, porque el log es
inmutable y el saldo es su suma. Quitarlos es un ajuste de administrador, que también deja su
propio evento. Lo correcto es que cueste, no que se borre solo.

---

## 38. Que lo cuente el juego (2026-08-21)

Para que los logros avancen solos, lo primero era no contar nada. **Ultra Luna lleva sus propios
contadores desde el principio** —son los que enseña la ficha de entrenador— y PKHeX los expone en
`SAV7USUM.Records`, con la lista de nombres en `RecordLists.RecordList_7`: unos doscientos.

Volver a contarlos desde fuera, vigilando memoria, habría producido un segundo número que solo
puede ser peor que el del propio juego. Así que se leen los suyos.

### Los que sirven

| Récord | Qué es | Logro |
|---|---|---|
| 41 | Movimientos Z usados | Usa 100 movimientos Z |
| 46 | Huidas de combate | Huye 200 veces |
| 127 | Variocolor encontrados | Encuentra 1 variocolor |
| 5 | Combates contra entrenadores | Derrota a 100 entrenadores |

Se leen del **fichero de partida**, por el mismo camino que las cajas: no hace falta cerrar el
juego, pero el número es el de la **última vez que se guardó**, y la pantalla lo dice.

### La comprobación que da confianza

Los índices no se creyeron sin más. Contra la partida real: **168 Poké Balls usadas (récord 42) y
167 Pokémon capturados (récord 6)**. Dos contadores independientes que tienen que salir casi
iguales, y salen. Eso ancla la numeración.

El 46 dio **28 huidas**, coherente con 196 combates contra salvajes; el 5 dio **6 entrenadores**,
coherente con lo poco que lleva la run. Aun así el índice 5 no tiene un cruce propio, así que en el
JSON queda escrito qué récord lee cada logro y la invitación a compararlo con la ficha de
entrenador del juego: si no cuadra, se cambia un número y ya.

Ojo con el récord 3, «Total Battles»: dio 39 mientras el 4 daba 196 combates contra salvajes. No
suma, así que **no se usa** hasta entender qué cuenta de verdad.

### Tres orígenes, dicho en cada tarjeta

Cada logro dice de dónde sale su número, porque no son igual de fiables:

- **«Lo cuenta el juego»** — un récord del cartucho. Manda sobre todo lo demás.
- **«Lo cuenta PermaLocke»** — un tipo de evento del historial de la run.
- **«A mano»** — lo marca el jugador, y cada marca es un evento firmado por él.

Un logro con récord **no se puede marcar a mano**: dos números para el mismo hecho, sin manera de
saber cuál vale.

### Lo que sigue sin detectarse

**17 de los 21**: las doce pruebas, el alto mando y las tres de pegatinas. No están entre los
récords, porque no son contadores sino **banderas de evento** de la partida. Encontrar cuál es
cuál es otra investigación —del mismo tipo que la del equipo o la mochila— y no se ha hecho.
Mientras tanto se marcan a mano, que es honesto y funciona.

---

## 39. Las banderas de evento: medirlas, no adivinarlas (2026-08-21)

Quedaban 17 logros sin detectar —las doce pruebas, el alto mando y las pegatinas— y no están entre
los récords porque **no son contadores, son banderas de evento**. La partida guarda **4960
banderas y 1000 contadores**, ninguno etiquetado. En la partida del jugador hay 696 encendidas y
142 contadores distintos de cero.

### Lo que se pudo anclar ya

**Campeón**, con el récord **2**, «momento en que se completó la historia». Vale cero mientras no
lo seas y se llena en cuanto lo eres, así que no hay nada que interpretar. Lo confirma el Salón de
la Fama del propio save, que también está a cero: `Fame.First1 = 0`.

De los 21, se cuentan solos **cinco**: movimientos Z, huidas, variocolor, entrenadores y campeón.

### Lo que no se hizo, y por qué no

De las 4960 banderas, 138 contadores tienen un valor entre 1 y 12 en esta partida. Cualquiera
podría ser «pruebas superadas» y ninguno se puede distinguir mirándolo. Elegir uno sería
exactamente el mapeo casi-bueno que prohíbe la regla 3: repartiría puntos por la cosa equivocada
con toda la confianza del mundo.

Se intentó el atajo: comparar dos partidas del jugador con distinto avance. **Las copias de
BxnnyLocke no las lee PKHeX** —tienen el tamaño correcto, 445.440 bytes, pero no el formato que
espera—, así que no hay dos puntos de referencia que comparar.

### La herramienta: medir la diferencia

Si no se puede deducir, se mide. `PermaLocke.Probe` gana dos verbos:

```
PermaLocke.Probe --flags antes.txt          vuelca banderas y contadores de la partida
PermaLocke.Probe --flags-diff antes despues  dice qué cambió
```

El procedimiento para anclar cualquiera de los 16 que faltan:

1. Volcar **antes**.
2. Jugar hasta que pase la cosa —superar una prueba, coger una pegatina—.
3. **Guardar dentro del juego.** Sin eso el fichero no cambia y los dos volcados salen iguales; el
   propio volcado avisa si el juego está abierto.
4. Volcar **después** y comparar.

Entre una prueba y la siguiente se mueven un puñado de cosas, así que la lista sale corta y
legible. Lo que aparezca se escribe en `Data/achievements.json` y el logro pasa a contarse solo,
sin tocar código.

El volcado es texto plano a propósito: es **prueba**. Se guarda, se compara más tarde y se le
puede enseñar a otro.

### Por qué esto y no una lista de banderas sacada de internet

Porque no se puede verificar contra este cartucho sin hacer justo esta medición, y una bandera
equivocada no falla: reparte puntos en silencio. Medirla cuesta dos minutos de partida y deja el
número anclado con su evidencia al lado.

---

## 40. La primera prueba se ancla en el premio, no en la bandera (2026-08-21)

Se midió la primera prueba con el procedimiento del §39 y salió algo que conviene contar entero,
porque la primera medición fue **falsa** y solo se vio por casualidad.

### La medición que no era

El jugador dijo haber superado la primera prueba. El diff entre los dos volcados daba 24 banderas
encendidas, 3 apagadas y 11 contadores nuevos. Cualquiera de esas 24 podría haberse escrito en
`achievements.json` como «prueba superada» y el logro habría funcionado el resto de la partida.

No era ninguna. Lo que dice la partida de aquella sesión:

| | antes | después |
|---|---|---|
| Combates contra entrenadores | 6 | **11** |
| Combates contra salvajes | 196 | **197** |
| Movimientos Z usados | 0 | 0 |
| Cristales Z en la mochila | **0 de 35 huecos** | **0** |

Superar una prueba significa tumbar a los Pokémon de prueba y al Dominante, que son combates
**contra salvajes**, y el premio es un **cristal Z**. Había un solo combate salvaje y cero
cristales. Los cinco combates eran contra entrenadores, con una MT01 en la mochila: la Escuela de
Entrenadores, el paso justo anterior. La prueba llegó en la sesión siguiente, y entonces sí: +8
combates contra salvajes y el **Normastal Z (objeto 807)** entrando en un bolsillo vacío.

La lección no es «mira los cristales». Es que **un diff no dice qué pasó, dice qué cambió**, y hace
falta un hecho independiente que confirme que lo que se busca está dentro de la ventana medida.
Aquí ese hecho fue el bolsillo de cristales Z, que se lee sin filtrar por cantidad porque un objeto
clave puede estar con cantidad cero y seguir estando —ahí está la Piedra Brillante para probarlo—.

### Por qué el objeto y no la bandera

Con la medición buena quedaron **seis** banderas candidatas: encendidas por la prueba, nunca vistas
apagarse, y primeras de un bloque de doce vacío. Seis, no una. Elegir entre ellas sería tirar una
moneda con cinco caras malas, y ninguna avisaría del error.

Ninguno de los contadores del §38 se mueve al superar una prueba, y `Misc7.Stamps` tampoco: vale 1
antes y después, así que no es la Dominsignia pese al nombre que invita a creerlo.

Lo que sí deja la prueba es el premio, y el premio **el cartucho lo nombra**: 807, «Normastal Z».
De ahí la tercera fuente de progreso, junto al récord y al disparador:

```json
{ "id": "prueba-01", "target": 1, "points": 100, "item": 807 }
```

`GameRecordSnapshot` lleva ahora el conjunto de objetos de la mochila y `Achievement` un `Item`
opcional. Presencia, no cantidad: está o no está. **Solo vale para lo que el juego da y no quita**;
con un consumible el logro se encendería y se apagaría. La tarjeta lo dice en voz alta —«lo dice tu
mochila»— y, como es automático, no se puede marcar a mano.

Una partida ilegible deja el logro en cero, no en «hecho»: una mochila que no se puede leer no es
una mochila vacía, pero equivocarse hacia «no hecho» solo cuesta un botón, y hacia «hecho» regala
cien puntos.

### Estado

`prueba-01` se cuenta sola y verificado en la app: 1/1, listo para cobrar. Las once pruebas
restantes, las pegatinas y el segundo alto mando siguen a mano, y se anclarán igual, una medición
cada una. `Saves/banderas-prueba1.txt` es la línea base de la segunda.

Con la segunda prueba habrá además una segunda oportunidad para la bandera: de los seis bloques
candidatos, el que encienda una **segunda** bandera es el de las pruebas. Si eso cierra, las doce
se anclan de golpe; mientras no cierre, el objeto ya las cuenta bien.

### La segunda medición: la bandera no cierra, el objeto sí

La segunda prueba se midió igual, y sirvió para dos cosas.

La primera, anclarla: entró el **Lizastal Z (objeto 813)**. Los cristales de tipo son los objetos
807 a 824 en el orden de tipos del juego, y el cartucho los nombra, así que cada prueba futura se
ancla mirando cuál aparece. Nada de esto se sabe de memoria: se ve entrar entre dos volcados.

La segunda, enterrar la vía de la bandera. La predicción era que de los seis bloques candidatos,
el de las pruebas encendería una **segunda** bandera. El cruce da **21 parejas** plausibles —una
bandera de cada prueba a menos de 24 de distancia— y casi todas con banderas de la Escuela al
lado. No converge, y con más medidas tampoco tiene pinta de converger: son tramos densos de
banderas de historia, no una tabla dispersa de hitos.

Tampoco hay contador de pruebas. La primera encendió `work[51]` y `work[63]`, la segunda encendió
`work[75]` y `work[765]`: cuatro contadores distintos que valen 1, ninguno que vaya de 1 a 2. Si
hubiera un «pruebas superadas», habría subido.

Lo único que se movió de forma prometedora fue `stamps`, de **1 a 3**: un campo de bits al que se
le encendió el bit 1. El bit 0 ya estaba puesto antes de la primera prueba, así que **no** es
«prueba número N» y con una sola transición observada no se ancla nada. Queda como predicción
comprobable: si con la siguiente gran prueba pasa de 3 a 7, es el campo de las grandes pruebas y
las cuatro se anclan de golpe. Si no, se olvida.

Estado: `prueba-01` y `prueba-02` se cuentan solas y verificadas en la app, 1/1 las dos.

---

## 41. Nombres que faltaban y movimientos Z repartidos (2026-08-21)

Dos fallos que solo se ven jugando, y que tienen la misma forma: algo que parecía que el juego
resolvería solo y que en realidad hay que escribir.

### El nombre no es un respaldo, es un campo

Todo lo que PermaLocke entregaba —gacha y wonder trade— llegaba **sin nombre**. La suposición era
que un Pokémon sin mote enseña el nombre de su especie, y no: el mote es un campo dentro del
Pokémon y el juego enseña lo que haya, así que en blanco se ve en blanco.

`PokemonBuilder` lo escribe ahora, en el idioma de la partida, con `IsNicknamed` en **false** para
que el juego lo siga tratando como nombre de especie y lo renombre al evolucionar. Si el campo de
idioma no dice nada PKHeX devuelve cadena vacía, así que hay respaldo a español: escribir un
nombre vacío sería repetir el fallo con más pasos.

Eso arregla lo que venga. Lo ya entregado está escrito y ningún arreglo lo alcanza: en la partida
real, **150 de 155** Pokémon estaban sin nombre. De ahí `SaveNameRepair`, con su comando:

```
Probe --nombres              lista los que no tienen nombre, sin escribir nada
Probe --nombres --arreglar   los repara, con copia previa y relectura
```

Listar y arreglar son dos órdenes distintas a propósito: esto escribe en la partida del jugador,
así que primero lee lo que va a cambiar. Solo se toca lo que está **en blanco** —un mote que puso
el jugador se queda— y, como toda escritura, se copia la partida antes y se relee después: si al
releer queda alguno sin nombre, se dice, no se da por bueno.

### Movimientos Z en los aprendizajes

El randomizador repartía movimientos de 1 a `MaxMoveID` sin filtrar, y ahí dentro están los
movimientos Z. En el mod instalado: **1313 de 16052 aprendizajes**, un 8%, que es exactamente la
proporción de movimientos Z sobre el total. El juego los ofrece como cualquier otro y pegan por
cientos con un solo PP.

Cuáles son **no se escribe aquí, se lee de la ROM que se está randomizando**: todo movimiento Z
lleva `PP = 1` y ningún otro lo lleva, salvo Forcejeo y Esquema, que tampoco pintan nada en un
aprendizaje —Forcejeo es al que recurre el juego cuando no queda ninguno—. En Ultra Luna eso deja
fuera 55 de 728: los dieciocho de tipo con sus dos variantes cada uno, los exclusivos, Forcejeo y
Esquema.

Se probó la otra vía antes: cada movimiento guarda a qué movimiento Z se convierte, así que la
tentación era recoger esos. Da **18**, solo las variantes físicas, y se habrían colado los otros
35. La cuenta por PP los coge todos.

Verificado generando con la ROM real: **0 de 16052**. Los entrenadores lo heredan sin tocar nada,
porque el módulo de entrenadores no elige movimientos, devuelve el moveset al juego para que use
el aprendizaje.

Si la tabla de movimientos no se puede leer, el módulo **lanza** en vez de randomizar sin filtro:
repartir movimientos Z en silencio es peor que no randomizar los aprendizajes.

### Lo que esto no arregla

Un Pokémon que ya sabe un movimiento Z lo sigue sabiendo: está escrito en la partida. Y el mod
instalado sigue siendo el de antes; hace falta volver a generar con la misma seed y reinstalar.
Como cada módulo tiene su propia fuente aleatoria (§27), regenerar con la misma seed cambia **solo
los aprendizajes**: los salvajes, los entrenadores, las estadísticas y las tiendas salen idénticos.

---

## 42. Renombrar no es capturar, y las Dominsignias las cuenta el juego (2026-08-21)

### El fallo: 150 capturas que no ocurrieron

La reparación de nombres del §41 se ejecutó contra la partida real y funcionó: 150 Pokémon
recuperaron su nombre. También subió **capturas de 167 a 317, Poké Balls usadas de 168 a 318 y
combates contra salvajes de 212 a 362**.

PKHeX trata meter un Pokémon en una caja como **adquirirlo**: por defecto registra la entrada de
la Pokédex y mueve los contadores de la ficha de entrenador. Es lo correcto cuando un Pokémon
llega, y falso aquí, porque ya estaban en la caja. `EntityImportSettings` separa las tres cosas
—`UpdateToSaveFile`, `UpdatePokeDex`, `UpdateRecord`—, así que la reparación va ahora con las tres
en `Disable`, y hay dos tests que fallan si alguna se escapa.

Se deshizo comparando la partida contra la copia previa: **solo esos tres récords** habían
cambiado, la Pokédex seguía en 175 vistos y 128 capturados, y ninguna bandera, ni el dinero, ni
`stamps`. La copia era además del mismo minuto de juego, así que se restauró y se volvió a pasar
la reparación corregida. Comprobado después: la partida difiere de la copia **en nada** salvo los
nombres.

La entrega del gacha y el wonder trade tenían el mismo defecto y ahora usan
`PokemonBuilder.Handover`, que desactiva **solo los récords**: la Pokédex se queda, porque el
Pokémon sí es del jugador, pero nadie tiró una ball a una tirada de gacha.

### El mod anterior ya no se borra sin más

Reinstalar el mod vacía la carpeta, y tiene que hacerlo (§27). Pero esa carpeta **es el mundo en
el que alguien está jugando**, y una vez borrada no hay manera de demostrar que la reinstalación
no cambió nada. `Clear()` la mueve ahora a `load/permalocke-mod-anterior` —fuera de `mods`, para
no tentar al emulador— y el informe dice dónde quedó. Es un `Move`, no una copia: son 460 MB en el
mismo volumen.

De paso quedó demostrado que la generación es **determinista**: instalar dos veces seguidas con la
misma seed da los seis ficheros byte a byte idénticos.

Trampa que costó un susto: `Randomized/seed-<seed>/` era del 18 de agosto, anterior a apagar tipos
y evoluciones, así que comparar contra ella decía que `a/0/1/7` había cambiado. No era el mundo del
jugador, era una referencia caducada. Lo que se instaló tiene **0 tipos distintos del cartucho**,
que es exactamente lo que pide `randomizer.json`.

### Las Dominsignias: `work[169]`

La competición las llama «pegatinas», pero el cartucho reserva esa palabra para las pegatinas del
Fotoclub; lo que se busca por el mapa son **Dominsignias**, y hay 100.

`Misc7.Stamps` parecía la pista buena —pasó de 1 a 3 y el jugador dijo tener 2, que es su
popcount—, pero escribiéndole `0xDEADBEEF` y mirando qué bytes se movían resulta ser un campo de
**14 bits** en el bloque Misc, offset 0x008 bit 4. En 14 bits no caben 100. Descartado.

El bueno es `work[169]`, y lo dicen las cuatro medidas contra lo que el jugador fue diciendo:

| volcado | jugado | `work[169]` | lo que dijo el jugador |
|---|---|---|---|
| antes | 19:33 | 0 | — |
| después | 20:22 | **1** | «acabo de pillar la primera dominsignia» |
| prueba1 | 21:01 | 1 | habló de la prueba, no de Dominsignias |
| prueba2 | 21:34 | 1 | ídem |
| ahora | 21:38 | **2** | «he pillado otra y tengo 2, antes tenía 1» |

De ahí la cuarta fuente de progreso, `"work": 169`, junto al récord, el objeto y el disparador.
`GameRecordSnapshot` lleva ahora los mil contadores enteros: leerlos no cuesta nada y evita
mantener en código una lista de cuáles interesan, que ya la dice `Data/achievements.json`.

Esto vale además como retractación: en el §40 se dijo que aquella primera sesión no contenía nada
de lo que el jugador decía. Contenía su primera Dominsignia. Lo que no contenía era una prueba.

Un ancla de contador tiene una ventaja sobre una de bandera: **enseña un número que el jugador
puede comparar**. La tarjeta dice 2/25 y él sabe cuántas lleva. Si algún día no cuadra, se ve.

---

## 43. Los 21 logros se cuentan solos: el cartucho dice qué prueba da qué cristal (2026-08-21)

Quedaban once a mano: las diez pruebas restantes y el segundo alto mando. Se cerraron los tres
frentes, y el último salió de mirar donde no había mirado.

### Los nombres de los récords existían

PKHeX trae los **170 nombres** de los récords de gen 7 en `RecordLists.RecordList_7`, y nunca se
habían leído. Confirman los cinco ya anclados —41 «Z-Moves Used», 46 «Ran From Battles», 5
«Trainer Battles», 127 «Shiny Pokémon Encountered», 2 «Storyline Completed Time»— y traen dos que
faltaban:

- **72 «Stickers Collected»**, que vale 2 y es lo que la competición llama pegatinas. Sustituye al
  `work[169]` del §42, que se había anclado midiendo y daba el mismo número: gana el que tiene
  nombre, y que los dos coincidan es la mejor comprobación posible de ambos.
- **100 «Champion Title Defense»**, que es exactamente «derrota al alto mando de nuevo». El
  objetivo pasa de 2 a 1: una defensa del título es una segunda victoria.

No hay ningún récord de pruebas superadas, y ninguno de los mil contadores sube una vez por
prueba: se comprobaron los mil en los cuatro volcados, buscando la secuencia 0,0,1,2 y también
cualquiera que subiese uno en cada sesión de prueba. Cero resultados en ambas.

### El cartucho publica qué prueba da qué cristal

La idea era anclar cada prueba a su cristal Z, pero saber cuál da cuál era de memoria — y de
memoria no se ancla nada, que es justo lo que dice la norma 3. Lo pedido fue: *búscalo*.

Está en el **storytext**, `a/0/4/<idioma>`, un GARC que `GameFiles` no extraía porque el
randomizador no lo necesita. Los mensajes de «has obtenido X» usan una variable, pero los
**diálogos alrededor nombran el cristal en texto plano**, y con eso se cierran las doce:

| # | Prueba | Cristal | id | De dónde sale |
|---|---|---|---|---|
| 1 | Liam | Normastal Z | 807 | medido, y f714 |
| 2 | Gran Prueba de Kaudan | Lizastal Z | 813 | medido, y f623, f714 |
| 3 | Nereida | Hidrostal Z | 809 | f159 |
| 4 | Kiawe | Pirostal Z | 808 | f165, f180 |
| 5 | Lulú | Fitostal Z | 811 | f172 |
| 6 | Gran Prueba de Mayla | Litostal Z | 819 | f203 |
| 7 | Chris | Electrostal Z | 810 | f239, f258 |
| 8 | Zarala | Espectrostal Z | 820 | f261, f271 |
| 9 | Gran Prueba de Ula-Ula | Nictostal Z | 822 | f293 |
| 10 | Cañón de Poni | Dracostal Z | 821 | f353 |
| 11 | Rika | Feeristal Z | 824 | f354, f493 |
| 12 | Gran Prueba de Hela | Geostal Z | 815 | f346, y f389: «la última» |

La línea que lo ata todo es **f714**: «*pero si tienes un Normastal Z y un Lizastal Z... has
conseguido superar la prueba de Liam y la Gran Prueba de Kaudan*». Nombra los dos cristales y las
dos pruebas en la misma frase, y son **exactamente los dos que ya se habían medido** entrando en
la mochila. Dos anclas independientes que coinciden.

Salen **doce**, y la competición pide doce. Que cuadre no estaba garantizado.

### Por qué contar cristales habría estado mal

De los dieciocho cristales de tipo, seis **no** aparecen en esa tabla: Criostal, Toxistal,
Aerostal, Psicostal, Insectostal y Metalostal. El storytext los enseña regalados por PNJ —«toma
este Toxistal Z, por si te sirve de ayuda», «conseguí ese Cristal Z durante mi recorrido
insular»—. Un logro que contase cristales a secas se habría adelantado seis veces sin fallar ni
una, que es la forma exacta de error que la norma 3 prohíbe. Ahora está medido en vez de
sospechado.

### Estado

Los **21 logros se cuentan solos**. La pantalla ya no tiene un solo botón de marcar a mano, y el
resumen pasó de «15 se marcan a mano» a no mencionarlo. Un logro nuevo que PermaLocke no supiera
detectar seguiría saliendo a mano con sus botones: eso no se ha quitado, simplemente ya no lo usa
nadie.

---

## 44. El wonder trade devolvía peor: no era el sorteo, era el cartucho (2026-08-21)

El jugador dijo que cada intercambio salía igual o peor. Tenía razón, y la primera reacción —«el
sorteo estará sesgado»— era la equivocada.

### Lo que decían los 23 intercambios de la run

El log de eventos guarda cada intercambio con el total base entregado y el recibido, así que la
queja se puede medir en vez de opinar sobre ella:

| | |
|---|---|
| Suben | 3 |
| Igual | 6 |
| Bajan | **14** |
| Media | **−2,1%** |

Y seis salieron con el total **exactamente igual** al entregado, que es de donde venía la
sensación de «siempre el mismo BST».

### La causa

El sorteo elige uniformemente entre las especies de la banda, y eso es correcto. Lo que no es
uniforme es **cuántas especies hay en cada tramo**:

| total base | especies |
|---|---|
| 450-499 | 171 |
| 500-549 | 128 |
| 550-599 | 36 |
| 600-649 | 31 |
| 650-699 | 18 |
| 700-749 | **1** |

Por encima de 550 la población se desploma. Entregando un 600 —que es lo que el jugador estaba
entregando, con la caja llena de tiradas de gacha caras— la banda −8%/+10% daba 552-660, y ahí
dentro hay **67 especies: 35 por debajo, 31 clavadas en 600 y una sola por encima**. El 31 explica
los empates: en 600 se amontonan los pseudolegendarios y media caja de legendarios.

Con esa forma, una banda de +10% sobre un 600 no puede subir. No hay a dónde.

### El arreglo

La banda pasa a **−8% / +20%**, a petición del jugador. Sobre un 600 la banda es ahora 552-720 y
el reparto queda 35 por debajo, 31 iguales y **19 por encima**, con media esperada **+1,2%** en
lugar de −2,1%.

Aviso que conviene no callarse: **de 680 para arriba esto no cambia nada**. Con 720 la banda es
662-864 y el cartucho no tiene una sola especie por encima de 720, así que un Arceus solo puede
bajar. No es un fallo que se pueda arreglar con un número.

Como todo lo que decide el juego, el número vive en `Data/wondertrade.json` y no en el código, con
la medición escrita al lado para que se entienda por qué es asimétrico.

---

## 45. La tienda, y el índice de iconos de objeto que el §34 se dejó a medias (2026-08-21)

### El §34 estaba mal a partir del objeto 100

El §34 dio por bueno que el icono de un objeto es `id - 1` en `a/0/6/1`, y lo apoyó en las
dieciséis Poké Balls en fila. Las balls son los objetos 1 a 16, así que estaban dentro del único
tramo donde esa regla vale.

No puede valer entera, y la aritmética lo grita: el cartucho tiene **960 objetos y 769 iconos**.
Bloques enteros comparten dibujo. Las **cien MT ocupan veinte discos**, uno por tipo, y a partir
de ahí todo lo que viene detrás se desplaza ochenta huecos.

El desfase es una función escalonada que nadie publica, así que se **midió por zonas**, pintando
ventanas de iconos y reconociendo cosas que no se pueden confundir:

| zona | desfase | cómo se ancló |
|---|---|---|
| 1-100 | −1 | las dieciséis balls, las cinco vitaminas, el Caramelo Raro, los cuatro Abonos |
| ~149-234 | −18 | la primera baya, Garra Rápida, Campana Alivio, Moneda Amuleto |
| ~269-297 | −19 | Vidasfera, Toxisfera, Llamasfera, Banda Focus, Pañuelo Elegido |
| ~538-571 | −127 | Casco Dentado, Globo Helio, Tarjeta Roja, las diecisiete Gemas, las siete Plumas |
| ~640-660 | −135 | Chaleco Asalto, Holomisor, Carta Profesor, Patines, Tabla Duende |

`ItemIconIndex` guarda **una tabla de lo comprobado, no una fórmula**, y lanza para un objeto que
nadie ha mirado. Un icono equivocado no se nota: enseñaría una cosa mientras vende otra. Añadir
uno son dos minutos —pintar su vecindario y reconocerlo—.

Trampa de nombres que también salió de aquí: la competición llama a las cosas de otra manera que
el juego. *Cinta Elección* es **Cinta Elegida**, *Mineral Evolutivo* es **Mineral Evol**, y *Banda
Aguante* es la **Banda Focus (275)**, identificada por su sprite y no por su nombre. En
`Data/shop.json` manda el `id`; el nombre es lo que se enseña.

### La tienda

Dieciocho objetos, los precios de la competición, en `Data/shop.json`. Se paga con puntos de la
run y **el objeto se escribe en la mochila del juego**, por el bloque que el §22 ya localizaba por
su estructura, así que se puede entregar algo que el jugador no lleva.

La decisión que importa es el **orden: primero se entrega, después se cobra**. La entrega es el
paso que puede fallar por cosas de fuera —el emulador cerrado, el bolsillo lleno, la escritura
rechazada— y a un jugador cobrado por un objeto que no llegó no hay manera de devolverle los
puntos. Cobrando después, lo peor que pasa es un objeto regalado, que se ve en la mochila y en el
log. Hay un test que falla si alguien invierte ese orden.

La entrega, además, **relee la mochila** y solo se da por buena si el objeto está ahí con una
unidad más. Una compra que no se puede verificar no se cobra.

Los iconos se extraen de la ROM del propio jugador la primera vez, a `Data/sprites/items/`,
nombrados **por id de objeto y no por índice de icono**, precisamente porque los dos números no
son el mismo.

### La Master Ball que no salía

La extracción inicial recorría `ItemIconIndex.KnownItems`, que es la tabla de **lo medido**, y por
tanto se dejaba fuera todo lo que va por la regla directa. La Master Ball es el objeto **1**: tenía
índice conocido y no tenía fichero, así que la tarjeta salía con la inicial y sin dibujo. Ahora el
icono se extrae **cuando se pide**, uno a uno, así que añadir algo a la tienda le trae su sprite sin
que nadie borre una caché.

### La pantalla

Rejilla de seis, tarjeta con degradado y el sprite **al doble de tamaño con vecino más próximo**
sobre un halo radial: escalado suave convierte 32×32 del cartucho en una mancha, y sin fondo el
pixel art flota. El precio es un botón de acento que se vuelve rojo apagado cuando no llega el
saldo, y la tarjeta entera baja a opacidad 0,45, de modo que se ve qué se puede comprar sin leer
nada. Al pasar por encima, borde de acento y sombra del mismo color. El saldo va en su propio
panel con halo. Lo que ya llevas de cada objeto sale en una cápsula pequeña bajo el nombre, que es
lo que evita comprar de más.

### La tienda se colgaba con el emulador cerrado

Primera compra real: no pasó nada y **todos los botones se quedaron inhabilitados**. Tres causas
encadenadas, y ninguna era la compra en sí.

**Preguntar dieciocho veces.** El refresco pedía «cuántos llevas» objeto por objeto, y cada
pregunta puede acabar en localizar el bloque de la mochila, que es barrer noventa y seis megas de
memoria del juego por UDP. Con el emulador cerrado eso son dieciocho barridos que solo pueden
acabar en fracaso, uno detrás de otro, al abrir la pantalla. Ahora se lee la mochila **una vez** y
de ahí salen las dieciocho cuentas.

**Barrer sin preguntar antes si hay alguien.** El cliente RPC tiene `TryPing`, que cuesta un
datagrama y un tiempo de espera. No se usaba. Ahora es lo primero: sin respuesta no se barre nada
y se dice qué hacer.

**El botón que apaga a todos.** `AsyncRelayCommand` se deshabilita mientras se ejecuta, y las
dieciocho tarjetas comparten el mismo comando, así que un clic que no termina nunca deja la
rejilla entera muerta. Eso convierte un cuelgue en «la aplicación está rota». Sigue habiendo un
solo comando —dos compras a la vez no tendrían sentido—, pero ahora **no puede no terminar**: hay
un tope de veinte segundos, y agotarlo dice que no se ha comprado nada ni se ha cobrado nada.

Medido después: la pantalla abre en **4 s** con el emulador cerrado, en vez de no abrir, y un clic
contesta en el acto con el aviso, con el saldo intacto.

De paso se quitó una fragilidad: si la entrega fallaba, el servicio decidía si era «el juego no
está» mirando **si el texto del error contenía una frase**. Ahora `ItemDeliveryResult` lleva un
`GameReachable`, que es un dato y no una adivinanza sobre una cadena traducible.

### Verificada contra el juego

Compra real, con Azahar abierto y la partida cargada: **Master Ball por 300 puntos**. El historial
lo deja entero y en orden, que es exactamente el contrato:

```
21:24:07  ShopPurchase  {"objeto":"1","nombre":"Master Ball","precio":"300","llevaAhora":"1"}
21:24:07  PointsSpent   -300  Tienda: Master Ball.
```

El `llevaAhora` no es lo que se pidió escribir: es lo que la mochila **contestó al releerla**. Y el
cargo va después, como debe. La tienda queda verificada de punta a punta.

---

## 46. Los roles (2026-08-22)

Un rol es la forma de jugar la competición, y se elige **antes que nada**: antes de la ROM, antes
de la seed, antes de randomizar. No es orden estético. Parte del rol se cuece en el cartucho, así
que elegirlo después significaría randomizar otra vez, y eso le cambia el mundo a una run ya
empezada.

### Los tres

| | ganancia | pérdida | entrenadores | cap del jugador | Pokémon extra |
|---|---|---|---|---|---|
| NORMAL | ×1 | ×1 | +20% | +20% | +1 |
| CAGONETA | ×0,5 | **×0** | +20% | +20% | +1 |
| EXPERTO | ×1,5 | ×2 | **+27%** | +20% | +2 |

El +20% es el suelo de todos: la edición base de la competición. El experto sube los niveles de
los entrenadores siete puntos por encima **sin subir su propio cap**, que es exactamente de dónde
sale la dificultad: los rivales le sacan ventaja.

Todo vive en `Data/roles.json`. Añadir un rol es editar el fichero.

### Multiplicar puntos sin perder la cuenta

Los multiplicadores tocan lo que se **gana** y lo que se **pierde**, y nunca lo que se **gasta**:
una Master Ball cuesta 300 en los tres roles, porque gastar no es perder.

La cuenta no se hace en silencio. `RoleAdjusted` lleva juntos el número base, el multiplicador y
el resultado, y los tres acaban en el evento del historial:

```
base=25  rol=experto  multiplicador=2   →  −50 puntos por la muerte de Chesnaught (25 × 2 por el rol experto)
```

Sin eso, un jugador que ve «−50» no puede distinguir una penalización doblada de un fallo.

Un caso que había que decidir y no dejar al azar: **un rol que no se puede resolver**. Se cobra la
tarifa base y el evento lo dice, con `rol=desconocido`. Ni adivinar «normal» —que pagaría el doble
a un cagoneta— ni negarse a cobrar una muerte, que dejaría el historial mintiendo.

### Lo que el rol le hace al cartucho

Los niveles de los entrenadores suben por rol, **parcheando un byte en su sitio** (`0x0E` de cada
entrada de `trpoke`), que es lo único que la norma del §19 permite. El redondeo es hacia arriba a
propósito: los primeros entrenadores son de nivel 5, y redondear hacia abajo dejaría el +20% en
nada durante toda la primera isla.

El nivel lo sube **también a las especies protegidas**. Un Cosmog al nivel del cartucho en un juego
donde todo lo demás va un 20% por encima sería un regalo, no una protección.

### Lo que falta, y por qué no está

**El Pokémon extra en los combates importantes no está hecho**, y no por olvido. Añadir uno a un
equipo significa alargar su subfichero dentro del GARC, y `GarcPatcher.Write` **rechaza** un
tamaño distinto por diseño: la norma del §19 nació de dos fallos reales al regenerar estructuras.
Hacerlo bien exige reempaquetar `a/1/0/7` con offsets nuevos y tocar además el número de Pokémon
en `a/1/0/6`, y decidir **cuáles son los combates importantes**, que es otra investigación sobre
las clases de entrenador. Es un trabajo aparte, con su propia verificación, y se hace después.

Mientras tanto la pantalla del rol lo enseña —«+1 Pokémon en combates importantes»— porque es lo
que la competición dice que es el rol, y `Data/roles.json` lo guarda; simplemente todavía no lo
aplica nadie. Aquí queda escrito para que no se dé por hecho.

---

## 47. El Pokémon extra, y el cap que no se toca (2026-08-22)

### Corrección: el cap del jugador se queda como está

El §46 subía el cap del jugador un 20% junto con los entrenadores. No es eso: **el cap del jugador
es el de `Data/levelcaps.json`, tal cual lo dio la competición, y no se toca**. Lo que sube un 20%
son los niveles de los entrenadores. `capDelJugador` queda en 0 para los tres roles.

Esto hace la dificultad más limpia de explicar: todos juegan contra rivales un 20% por encima del
cartucho con su cap de siempre, y el experto los tiene un 27% por encima. Los tests lo fijan.

### Cuáles son los combates importantes

Lo primero era saberlo, y se sacó del cartucho: se listaron los 700 entrenadores con su clase, su
nombre y el tamaño de su equipo. La trampa está en que **la clase por nombre no sirve**: Giovanni y
los reclutas del Team Rainbow Rocket se llaman igual, y lo que los separa es el **id** — 206 el
jefe, 208 y 209 la tropa. Lo mismo con el Team Skull.

De ahí salen las 35 clases de `clasesImportantes`: kahunas, capitanes, alto mando, los dos rivales
(Gladio y Tilo), Guzmán, la Fundación Æther, Kukui y los seis jefes del Team Rainbow Rocket. Ni un
recluta. Está en `Data/roles.json` y hay un test que falla si alguien mete la clase 208.

### La única vez que hay que reempaquetar un GARC

Un equipo vive en `trpoke` como una tira de entradas de 0x20 bytes y nada más, así que un séptimo
Pokémon alarga el subfichero y **el contenedor entero hay que reempaquetarlo**. `GarcPatcher`
rechaza un tamaño distinto por diseño (§19), así que este módulo usa `LazyGARC`, que sí admite
subficheros nuevos, y escribe el fichero completo. Donde no se puede parchear en el sitio, la
seguridad viene de verificar: se releen los dos ficheros y se comprueba que **el equipo de cada
entrenador mide exactamente lo que su tabla declara**, que nadie pasa de seis, y que a quien se le
pidieron N acabó con N.

El añadido es una **copia del último Pokémon del equipo** con la especie cambiada. La entrada de
0x20 tiene campos que aquí nadie ha identificado; copiar a un vecino garantiza que todos ellos son
algo que el formato de ese entrenador ya contenía. Inventar una entrada desde ceros parecería
correcta y podría significar cualquier cosa. Hereda además el nivel, que es lo que impide que el
extra sea un regalo más débil que el resto.

Seis es el techo duro: la cuenta es un byte que el juego lee como tamaño de equipo, y un séptimo
miembro no existe en el motor de combate. Un jefe que ya va con seis **se deja y se cuenta**, nunca
se trunca.

### El fallo que casi se cuela

La primera generación salió con los equipos crecidos y **los niveles sin subir**. La causa: el
módulo nuevo llamaba a `Stage()` sobre `trpoke`, y `Stage()` copiaba el fichero vanilla encima del
que el módulo de entrenadores acababa de parchear. Los tamaños se veían bien y el informe decía
«1139 niveles subidos», pero el fichero final no los tenía.

`Stage()` es ahora **idempotente**: un segundo módulo que pide el mismo fichero recibe el que ya
está, parches incluidos. Volver a copiar la vanilla encima no es nunca lo que quiere quien llama;
para empezar de cero está `Revert()`. Era un fallo latente para cualquier módulo futuro.

Se descubrió porque la verificación no se hizo con el informe sino **leyendo los ficheros generados
por fuera**, con el lector de pk3DS, y mirando a los jefes uno a uno.

### Verificado contra la ROM real

`RomTool randomize <seed> --rol <id>` aplica el rol desde la línea de órdenes. Con el experto:

| | antes | después |
|---|---|---|
| Capitán Liam | 3, Nv51 | **5**, Nv65 |
| Kahuna Kaudan | 5, Nv63 | **6**, Nv80 |
| Kahuna Hela | 5, Nv68/69 | **6**, Nv86/88 |
| Giovanni | 5, Nv68/70 | **6**, Nv86/89 |
| Tilo (ya con seis) | 6 | 6, intacto |

Equipos que no cuadran con su cuenta: **0** de 653. Con el rol normal salen 87 añadidos en 87
combates —exactamente uno cada uno—; con el experto, 139, porque a los que ya iban con cinco solo
les cabía uno.

Lo que no se puede comprobar sin jugar: si algún combate con guion da por sentado un tamaño de
equipo concreto. No hay forma de saberlo leyendo ficheros.

---

## 48. Los Dominantes también suben, y la tabla de caps se explica sola (2026-08-22)

### El nivel de un estático estaba sin identificar

Los Pokémon dominantes **no son entrenadores**: viven en la tabla de estáticos de `a/1/5/9`, junto
a Necrozma, Solgaleo, Lunala, los Ultraentes, los Tapus y los legendarios del ultraumbral. Su nivel
está en el **byte 0x03** de una entrada de 0x38.

Se encontró buscando un byte que valga siempre entre 1 y 100 y varíe, y se confirmó contra cosas
cuyo nivel no admite duda: Solgaleo y Lunala a 60, el Dominante Gumshoos a 12, el Wishiwashi a 20,
el Salazzle a 22.

### La tabla de caps de la competición ES el jefe con el +20%

Con los niveles de los jefes en la mano se puede comprobar de dónde salió `Data/levelcaps.json`, y
la respuesta es limpia: **el cap de cada etapa es el nivel del jefe subido un 20%**.

| etapa | cap | jefe | cartucho | +20% |
|---|---|---|---|---|
| 1ª prueba | 14 | Dominante | 12 | **14** |
| 2ª prueba | 24 | Dominante Araquanid | 20 | **24** |
| 3ª prueba | 26 | Dominante Salazzle | 22 | **26** |
| 4ª prueba | 29 | Dominante Lurantis | 24 | **29** |
| Gran Prueba Mayla | 34 | Kahuna | 28 | **34** |
| 5ª prueba | 40 | Dominante Togedemaru | 33 | **40** |
| 6ª prueba | 42 | Dominante Mimikyu | 35 | **42** |
| Cañón de Poni | 59 | Dominante Kommo-o | 49 | **59** |
| 7ª prueba | 66 | Dominante Ribombee | 55 | **66** |

Nueve de catorce clavadas. Las que no: la 2ª (cap 20, +20% da 19 y +27% da 20), la 9ª (54 contra
53) y la 12ª (67 contra 65), diferencias de uno o dos puntos.

Eso convierte lo que parecían dos reglas —«tu cap como está» y «los entrenadores un 20% más»— en
**una sola cosa mirada desde los dos lados**: llegas a cada jefe exactamente a su nivel.

### Y por eso los Dominantes tenían que subir

Si el cap está calculado sobre el jefe subido y el Dominante se queda al nivel del cartucho, las
**ocho pruebas** quedan por debajo de lo que la propia tabla presupone: llegas a la primera a nivel
14 contra un Gumshoos de 12. Las cuatro grandes pruebas iban bien porque los kahunas son
entrenadores; las pruebas no.

Así que el porcentaje del rol se aplica ahora también a la tabla de estáticos, y con él suben los
Dominantes, Necrozma, Solgaleo, Lunala, los Ultraentes, los Tapus y los legendarios.

**Los regalos no.** Los iniciales, los fósiles, Código Cero, Magearna, Cosmog y los Dominantes que
regalan por las pegatinas viven en otra tabla, que ni siquiera lleva nivel. Subirle el nivel a lo
que te dan sería un premio, no una dificultad, y el propio formato lo impide: `LevelOffset` es null
en esas tablas y `SetLevel` no hace nada.

Por eso el número del rol dejó de llamarse `nivelEntrenadores` y pasa a ser **`nivelEnemigos`**: ya
no gobierna solo a los entrenadores. El nombre viejo se sigue leyendo, para que un fichero de antes
no cambie de significado en silencio.

### Verificado contra la ROM real, rol experto

| | cartucho | +27% |
|---|---|---|
| Dominante de la 1ª prueba | 12 | **15** |
| Dominante Kommo-o | 49 | **62** |
| Dominante Ribombee | 55 | **70** |
| Necrozma | 75 | **95** |
| Solgaleo / Lunala | 60 | **76** |
| Tapu Koko | 60 | **76** |

252 estáticos subidos, **0 con un nivel distinto del esperado**, y la tabla de regalos byte a byte
idéntica.

Aviso que conviene tener presente: los legendarios del ultraumbral, que en el cartucho salen todos
a 60, pasan a **76** con el experto. Eso está por encima del cap de la Liga (73), así que un experto
que capture uno no podrá usarlo hasta el rematch. Es consecuencia de la regla, no un fallo, pero
conviene saberlo antes de que pase.

### Corrección: hay catorce Dominantes, no ocho

La lista de Dominantes del apartado anterior se sacó filtrando por **nombres de especie que puse a
mano**, que es exactamente la forma de que se escape algo. Y se escaparon seis.

Enumerándolos por el cartucho salen **catorce**. La marca se encontró comparando los Dominantes
conocidos contra sus propios acompañantes de la misma prueba: el **byte 0x07 vale 2** en todos
ellos y 0 en cualquier acompañante. Hay además una segunda marca independiente, los tres bytes de
**0x21, que leen `FF-99-19`** — el aura que le sube las estadísticas.

| Dominante | Nv | dónde |
|---|---|---|
| Raticate (forma 2) | 12 | Cueva Sotobosque — **el de Ultra Luna** |
| Gumshoos | 12 | Cueva Sotobosque — el de Ultra Sol |
| Araquanid | 20 | Colina Cascada — **Ultra Luna** |
| Wishiwashi | 20 | Colina Cascada — Ultra Sol |
| Marowak (forma 2) | 22 | Área Volcánica — **Ultra Luna** |
| Salazzle | 22 | Área Volcánica — Ultra Sol |
| Lurantis | 24 | Jungla Umbría |
| Togedemaru | 33 | Observatorio — **Ultra Luna** |
| Vikavolt | 29 | Observatorio — Ultra Sol |
| Mimikyu (forma 2) | 35 | Supermercado |
| Kommo-o | 49 | Cañón de Poni |
| Ribombee | 55 | prueba de Rika |
| Gumshoos | 60 | revancha de postgame |
| Raticate | 60 | revancha de postgame |

Ocho son los de la partida que se juega, seis son los de la otra versión y las dos revanchas. Como
el porcentaje del rol se aplica a **toda** la tabla de estáticos, los catorce ya suben; lo que
estaba mal era la lista, no el comportamiento.

Las tres filas donde mi tabla de caps nombraba al Dominante de Ultra Sol —Gumshoos, Wishiwashi y
Salazzle— tienen el mismo nivel que el de Ultra Luna, así que **los caps no cambian**: 12, 20 y 22.

`StaticEncounterTable.IsTotem` exige **las dos marcas a la vez**, y no por gusto: Tapu Koko lleva el
byte de tipo de un Dominante y ningún aura. Sea lo que sea eso, no es un Dominante, y pedir las dos
lo deja fuera sin que nadie tenga que nombrarlo.

---

## 49. El cap de nivel, por fin en marcha (2026-08-22)

Desde la fase 3 había una limitación escrita en todas partes: `LevelCapRule` implementada y probada,
pero `RuleContext.LevelCap` siempre null porque **no existía seguimiento de etapa**. La etapa solo
avanzaba con un botón, «HE SUPERADO LA ETAPA», que hay que acordarse de pulsar.

Eso ya no hace falta. Las doce pruebas **se cuentan solas** desde el §43: en cuanto el cristal Z
entra en la mochila, el logro se marca. Así que la etapa se puede deducir en vez de recordarla.

`Data/levelcaps.json` gana un campo `logro` por etapa, y `ProgressService` calcula las etapas
superadas como **la más alta cuya logro está desbloqueado**. La más alta y no la cuenta: si la
detección ve la tercera prueba y no la segunda, el jugador está claramente pasada la tercera, y
contar dejaría el cap una etapa por detrás.

El botón se queda, y las dos fuentes se combinan con `Math.Max`. **La detección solo puede subir
el cap**: si se retrasa, o si el fichero de partida no se puede leer, la run cae en lo que se haya
pulsado. Un cap que bajase dejaría fuera de ley a un equipo que ya era legal.

Leer la etapa significa parsear el fichero de partida, y el enlace con el juego pide el cap cada
tres segundos, así que la detección se cachea **veinte segundos**: bastante corto para que una
prueba recién superada valga casi al instante, bastante largo para no releer medio mega en cada
tic.

Visto en la run real: la etapa pasó sola de la 1ª prueba a la **3ª**, con el cap en **24**, y HOME
dice de dónde sale el número — «2 etapas superadas, contadas por los logros».

### Lo que el cap alcanza, y lo que no

Lo que sube por encima **vuelve al cap solo**, escribiendo en la memoria del juego, y se ve al
entrar en el siguiente combate porque el nivel en pantalla es un campo derivado que el juego
recalcula entonces.

Pero alcanza solo hasta donde mira, y conviene decirlo claro:

- **Solo el equipo.** Un Pokémon por encima del cap guardado en una caja no se toca hasta que entra
  en el equipo. En combate no puede usarse, así que no rompe ninguna regla, pero ahí está.
- **Solo con la aplicación abierta y Azahar respondiendo.** Con PermaLocke cerrado nadie vigila. Es
  inherente: no hay forma de que un programa que no se está ejecutando corrija nada.

Por eso el Yveltal de nivel 100 de la run real llegó hasta ahí — se subió con caramelos raros
mientras el enlace estaba caído— y por eso el historial tiene un único `LevelCapEnforced` suelto:
la corrección funcionó la vez que la aplicación llegó a verlo.

---

## 50. La interfaz, rehecha entera (2026-08-22)

La aplicación funcionaba y se veía como lo que era: un tema oscuro plano, con paneles rectangulares
de borde cian de 1,5 px, cero jerarquía entre lo importante y el relleno, y los controles de Windows
—barras de desplazamiento, desplegables, casillas— saliendo en gris claro y abriendo un agujero en
mitad de la pantalla. Esta sección es un repaso **solo de presentación**: no se ha tocado ni una
regla, ni un servicio, ni un cálculo de puntos.

### Lo que cambia el tema, que es casi todo

`Themes/Palette.xaml` deja de ser una lista plana de colores y pasa a tener **profundidad**: suelo de
la aplicación, panel, panel elevado y línea de un píxel. Un tema oscuro con un solo color de fondo se
lee como un agujero; con cuatro escalones, una tarjeta parece una tarjeta sin necesidad de dibujarle
un borde grueso alrededor. De ahí salen `PanelBrush`, `HeaderBrush`, `SidebarBrush` y
`AppBackgroundBrush`, todos degradados muy suaves, y los tres radios con nombre para que toda la
aplicación redondee por las mismas cantidades.

`Themes/Controls.xaml` añade a los estilos que ya había una escala tipográfica (`StatValue`,
`StatLabel`, `CardTitle`, `Faint`), tres superficies (`Inset`, `Card`, `Chip`), un separador
(`Divider`) y tres botones con papel distinto: `PrimaryButton` —contorno de acento que se rellena al
pasar por encima—, `GhostButton` para lo secundario y `DangerButton`, que hoy solo usa el wonder
trade, que es lo único de PermaLocke que destruye algo.

Y **retempla los controles de Windows como estilos implícitos**, sin `x:Key`, para que ninguna vista
tenga que acordarse de pedirlos: `ScrollBar` (11 px, sin flechas, el pulgar se ilumina al tocarlo),
`ComboBox`, `ComboBoxItem`, `CheckBox`, `RadioButton`, `ProgressBar`, `TextBox` y `ToolTip`.

### Tres trampas que costaron dinero

**El `ComboBox` editable necesita `PART_EditableTextBox`.** WPF busca esa parte *por nombre* para
enganchar el texto y la búsqueda al escribir. Una plantilla propia sin ella no falla, no lanza y no
escribe nada en el log: el desplegable simplemente **deja de aceptar texto**. Y el selector de
especie del diálogo de captura se usa exactamente así, escribiendo. La caja va encima del botón con
fondo `Transparent`, que sí recibe clics, de modo que pinchar en el texto entra en la caja y pinchar
en la flecha se cuela al botón de abajo y abre la lista.

**Una pila horizontal mide el contenido con ancho infinito.** Las primeras plantillas de `CheckBox`
y `RadioButton` ponían la marca y el contenido en un `StackPanel` horizontal, así que un texto largo
dentro de un radio **nunca ajusta línea** y se sale de la ventana. Con eso se entendió por qué las
tarjetas de rol llevaban un `MaxWidth="400"` a mano. Ahora ambas plantillas son una rejilla de dos
columnas, `Auto` y `*`, el texto ajusta solo y el `MaxWidth` sobra.

**Un estilo con `x:Key` no hereda del implícito.** `Style="{StaticResource HpBar}"` sobre un
`ProgressBar` se salta la plantilla implícita entera, así que la barra vuelve a ser la de Windows.
Hay que decirlo: `BasedOn="{StaticResource {x:Type ProgressBar}}"`.

A la lista de siempre —`OpacityMask` con `ImageBrush` no pinta nada, un `ItemsControl` dentro de un
`Grid` se recorta al ancho disponible, `ProgressBar.Value` enlaza `TwoWay` por defecto y revienta
contra una propiedad calculada— se suma una cuarta que ya había mordido antes: poner a la vez el
atributo `Style="…"` y un bloque `<X.Style>` es `MC3024`.

### La barra de título

WPF no dibuja el marco de la ventana, así que una aplicación oscura se publicaba con una barra de
título blanca encima y cada diálogo abría otra. `Services/DarkFrame.cs` pide a la gestora de
ventanas el marco oscuro (`DwmSetWindowAttribute`, atributo 20, con el 19 de reserva para las
versiones antiguas) y lo aplican las tres ventanas. Es puramente cosmético: si la llamada falla, la
ventana abre igual que antes y no se le dice nada a nadie.

### Nada de enums en pantalla

El dominio está en inglés, como todo el código, pero el jugador lee el historial, la lista de islas y
el tipo de encuentro. `ToString()` sobre un enum colaba `LevelCapEnforced`, `InProgress` o `Wild` en
una pantalla en español. `Services/DisplayNames.cs` traduce una vez, en el borde: los 26 tipos de
evento, los tres estados de isla y los nueve tipos de encuentro. Un valor sin entrada **cae a su
propio nombre**, no a la cadena vacía: una etiqueta que nadie ha traducido tiene que parecer sin
traducir, no desaparecida. Donde el valor enlazado tiene que seguir siendo el enum —el
`SelectedItem` de un desplegable— lo hace `Converters/DisplayNameConverter.cs`, de una sola
dirección: de una etiqueta nunca se vuelve a un valor.

### Pantalla por pantalla

- **Marco.** Barra lateral con marca propia, más oscura que el contenido y separada por una línea de
  un píxel en vez de por un hueco, para que se lea como el marco de la ventana y no como otro panel.
  La sección elegida se marca con una barra en su borde izquierdo; con ocho secciones, ocho
  rectángulos rellenos son más ruido que una marca. Abajo, rol y puntos siempre a la vista. La
  cabecera trae ahora **título y subtítulo**: `SectionViewModel` gana una propiedad `Subtitle`, que
  es texto y nada más.
- **HOME.** Cuatro cifras arriba en tarjetas con una banda de color —vivos, muertos, encuentros y
  cap—, y debajo dos columnas. El equipo en vivo pasa de tres números en fila a **barra de PS por
  Pokémon**, verde, ámbar o roja; el umbral lo decide el view model en un sitio, no cuatro
  disparadores repetidos en XAML. El historial gana un punto de color y el delta en verde o rojo. Se
  aprovechó para enseñar el `IsShiny` que el view model ya traía y la vista se estaba callando.
- **GACHA.** Era una pila con barra de desplazamiento y el botón de TIRAR caía fuera de la ventana.
  Ahora es una rejilla de tres filas: escenario del ultraespacio con `MinHeight`, banners y barra de
  acción, todo a la vez en pantalla. La animación no se toca —el code-behind solo mide el ancho del
  visor, nunca el alto—, y el banner elegido se enciende entero, que es el que va a cobrar.
- **TIENDA, LOGROS, VISOR, RANDOMIZADOR, MISCELÁNEA.** Paneles anidados al mismo tono pasan a
  `Inset`, las tarjetas responden al ratón, y los párrafos largos de pie de pantalla —los de «de
  dónde salen estos números» y «cómo se decide la tirada»— se van al tooltip. No es por esconderlos:
  se leen una vez y estaban comiéndose tres filas de tarjetas.
- **Diálogos.** Crear run y registrar captura, con las tarjetas de rol como objetivo grande —se
  pulsa una vez en toda la run— y el veredicto de las reglas pintando el fondo además del borde.

### Cómo se comprobó

Con la aplicación real y **sin robar el foco**: `PrintWindow` con `PW_RENDERFULLCONTENT` dibuja una
ventana esté donde esté, y `SelectionItemPattern.Select` no necesita que esté delante. Los diálogos
modales se encuentran enumerando las ventanas visibles del proceso **que tienen dueño**, porque
`FindAll` sobre la raíz de UI Automation no los devolvía.

La pantalla de crear run solo aparece cuando no hay ninguna, así que para verla se montó una copia
de la aplicación en una carpeta temporal, con su `Data/` y su `Saves/` vacío. Como `AppPaths`
resuelve la raíz subiendo hasta el `.slnx`, una copia fuera del repositorio arranca sin runs. La
partida del jugador no se tocó en ningún momento.

Barrido estático de remate: las **100 claves** `StaticResource` que usan las vistas están todas
definidas. Una clave que falta no rompe la compilación, rompe la ventana al abrirla.

351 pruebas en verde y compilación sin avisos.

---

## 51. El equipo en el visor, y los EV editables (2026-08-22)

Dos cosas pedidas juntas y que se apoyan la una en la otra: el visor enseñaba las 32 cajas pero no
los seis que el jugador lleva encima, y los EV se veían como un número muerto en la esquina de la
tabla.

### El equipo es otro almacén, no una caja 33

En el save, el equipo y el PC son dos sitios distintos: seis huecos con las estadísticas de combate
guardadas frente a treinta sin ellas. Colarlo como una caja más habría funcionado en pantalla y
habría sido una bomba en cuanto algo escribiese, porque `SetBoxSlotAtIndex` y `SetPartySlotAtIndex`
no son la misma llamada.

Así que se marca: `BoxContents` gana `Slots` y `IsParty`, y `BoxedPokemon.Box` toma el centinela
**`PartyBox = -1`**. Negativo a propósito, para que el código que se olvide de mirar **no pueda caer
en la caja 0 sin enterarse**. El wonder trade, que es lo único que destruye algo, corta en la puerta
cualquier índice negativo aunque la pantalla ya no se lo ofrezca.

`SlotsPerBox` se queda para lo que era, pero la rejilla ahora dibuja `contents.Slots`: seis para el
equipo, treinta para una caja.

### Los dos techos son del juego, y se aplican de maneras distintas

`EvSpread` conoce **252 por estadística y 510 entre las seis**. No es una idea de reparto justo: la
generación 7 guarda cada EV en un byte y el juego no reparte más, de modo que pasarse deja un Pokémon
que el cartucho considera ilegal.

Los dos se hacen cumplir de forma distinta, y en eso está toda la gracia:

- **252 por estadística se recorta.** Nunca hay motivo para querer más, y recortar es inmediato y se
  entiende solo: escribes 999 y ves 252.
- **510 en total solo se comprueba.** Recortarlo decidiría **el orden en el que hay que trabajar**.

La segunda regla salió de usar la primera versión, que sí recortaba el total. El jugador tenía un
Pokémon con PS y Ataque a tope y quiso pasar esos EV a Velocidad: escribió 252 en Velocidad y le
salió 6, porque no quedaba presupuesto. Para conseguirlo había que saber que **primero** se vacía PS.
Nadie adivina eso, y el historial de la run lo dejó grabado con todas las letras — `Kommo-o:
4/6/8/1/2/7 → 4/252/244/1/2/7 (Ataque 6→252, Defensa 8→244)`, un 244 que nadie pidió.

Ahora repartir es libre: el reparto puede pasarse de 510 mientras se toca, `Over` dice por cuánto, el
panel se pone en rojo y **`GUARDAR` se apaga**. El corte está donde tiene que estar, en
`EvTrainingService`, que rechaza un reparto ilegal antes de abrir nada.

Leer tampoco recorta ya el total: un save editado por fuera se enseña **como está**, marcado como
ilegal, en vez de decidir por su cuenta de qué estadísticas robar.

Comprobado en la aplicación real, partiendo de un reparto de 510 justos:

```
0) de partida        4/252/244/1/2/7   = 510   GUARDAR=False
1) MÁX en Velocidad  4/252/244/1/2/252 = 755   GUARDAR=False
2) 0 en Defensa      4/252/0/1/2/252   = 511   GUARDAR=False
3) Ataque a 251      4/251/0/1/2/252   = 510   GUARDAR=True
```

Cada fila lleva su `MÁX` y su `0` al lado, porque mover 252 puntos de una estadística a otra son dos
gestos y con los dos botones a mano son dos clics.

### Lo que se escribe, y lo que no se toca

`SaveEvTrainer` sigue la misma disciplina que el wonder trade: comprueba que el hueco **sigue teniendo
el mismo Pokémon** —por **PID**, que es lo único que sobrevive a motes, niveles y evoluciones—, copia
la partida entera, escribe y **relee** antes de dar nada por bueno. Con
`PokemonBuilder.InPlace`, que es el `PutBack` del §42 sacado a un sitio común: sin él, PKHeX vuelve a
contar cada Pokémon devuelto a su hueco como una captura, una ball y un combate salvaje.

Y aquí el hallazgo que costó una medición. Recalcular las estadísticas de combate del equipo parecía
lo ordenado —el equipo sí las guarda, la caja no— hasta que la prueba sobre una copia de la partida
real dijo esto:

```
  estadísticas antes:   168/85/121/85/105/95
  estadísticas después: 151/85/121/85/105/95
```

Un Kommo-o perdiendo 17 PS por escribirle EV. La causa es que **PKHeX calcula con SU tabla de
estadísticas base**, y esta competición se juega con `shuffleBaseStats` activo: esa tabla no es la del
cartucho. Así que **no se tocan**. Los EV se guardan y la estadística se pone al día cuando el juego
la recalcule, que es exactamente lo que le pasa a un Pokémon que gana EV en combate antes de subir de
nivel. La misma medición repetida después sale `168/... → 168/...`.

De rebote, eso descubre algo que ya estaba pasando: las estadísticas que el visor enseña **de los
Pokémon en caja** están calculadas por PermaLocke con esas mismas bases vanilla, o sea que son una
estimación. No se puede arreglar —`Data/species.json` guarda el total, no las seis— pero sí se puede
decir, así que `BoxedPokemon.StatsAreComputed` lo marca y la ficha lo avisa. Las del equipo son las
que escribió el juego y no llevan aviso.

### Auditable, y sin precio

`GameEventType.EvsTrained`, al final del enum como manda el propio comentario del fichero.
`EvTrainingService` escribe primero y registra después, por la misma razón que la tienda: un historial
que dice cosas que no pasaron es peor que uno que va un instante por detrás. El evento guarda el antes,
el después y qué estadísticas se movieron.

**No cuesta puntos.** Entrenar es una edición del jugador sobre un Pokémon suyo, no una compra, y
ponerle precio sería inventarse una regla que la competición no acordó.

### Cómo se comprobó

`Probe --ev` lista los EV de la partida real sin tocarla. `Probe --ev --probar` hace el viaje
completo **sobre una copia**: escribe, cierra, relee con el lector de verdad —no con el verificador
interno— y borra la copia. Eso cubre lo que las pruebas unitarias no pueden, porque PKHeX no reconoce
un save en blanco escrito a disco y el único fichero que sirve es uno real.

En la aplicación, sobre la partida del jugador: el equipo sale con sus cinco, la ficha marca EN EL
EQUIPO, el recorte funciona escribiendo a mano, GUARDAR se habilita solo cuando hay algo que guardar y
DESHACER devuelve el reparto de la partida. **La escritura desde la interfaz se deja sin ejecutar a
propósito**: cambiaría los EV de un Pokémon del jugador con valores que nadie ha elegido, y el camino
de escritura ya está probado por el mismo código sobre una copia del mismo save.

24 pruebas nuevas. 376 en verde.

---

## 52. Darse objetos: dos botones y ninguna cifra que teclear (2026-08-22)

La herramienta de Caramelos Raros de MISCELÁNEA pedía **cuántos quieres** y escribía esa cifra como
cantidad absoluta. Servía para lo que se hizo —llegar rápido al cap de nivel— y era incómoda para lo
que se usa: darse un puñado más. Se sustituye por dos botones que **suman**.

- **+10 CARAMELOS RAROS**, diez más sobre lo que ya llevas cada vez que se pulsa.
- **AMULETO IRIS**, el objeto 632.

### Por la misma puerta que la tienda

Los dos van por `IItemDelivery.GiveAsync`, que es el camino que ya usa la tienda y que ya está
verificado en el juego real. Trae tres cosas gratis que la herramienta vieja no tenía: **hace ping
antes** de nada, **suma en vez de reemplazar**, y **relee la mochila antes de decir que sí**.

Ese ping importa más de lo que parece, y por poco se repite el error que colgó la tienda. La primera
versión preguntaba la capacidad del objeto antes de entregarlo, y preguntar la capacidad **localiza
la mochila**, que son 96 MB de memoria barridos. Con Azahar cerrado ese barrido no puede acabar más
que en fallo, y mientras tanto el botón se queda muerto. Ahora la capacidad solo se pregunta cuando
el ping ha dicho que hay alguien al otro lado: con el emulador cerrado los dos botones contestan en
**algo más de 400 ms** con «Azahar no responde», en vez de irse a barrer.

### Un objeto clave no es un objeto que se acumula

El Amuleto Iris va al bolsillo de objetos clave, y ese bolsillo admite **uno**. Sin decir nada, dar
uno a quien ya lo tiene se leería exactamente igual que una escritura que el emulador se ha comido:
se escribe, se relee, sale el mismo número. Por eso `BagService.CapacityFor` existe — dice cuánto
cabe— y el botón responde «ya lo llevas» antes de escribir nada.

### Que el id sea el objeto que se cree

Un id tecleado de memoria que caiga en otro objeto entrega la cosa equivocada y **no falla nunca**.
Contra eso hay dos cosas. La primera, que los ids se buscaron en la tabla del cartucho en vez de
recordarlos: `Probe --objeto-find "Amuleto Iris"` da **632**, con el Amuleto Oval en 631 al lado, que
es la pareja que los juegos usan desde la quinta generación. La segunda, que antes de escribir se
comprueba que el nombre que la tabla da para ese id es el que el botón dice, y si no cuadra no se
entrega nada.

Y en las pruebas quedan fijados los tres ids que la aplicación usa a mano —Caramelo Raro, Amuleto
Iris y Poké Ball—, su nombre y el bolsillo al que van, incluido que el de objetos clave tiene tope 1.

Como todo lo que toca el juego, cada entrega deja su `TestItemGranted` con lo que había antes y lo
que hay después.

---

## 53. El cap de nivel no se aplicaba, y el escritor no lo sabía (2026-08-22)

El jugador entró en combate, salió, y su Yveltal seguía a nivel 100 con el cap en 24. El historial
decía que se había corregido. Las dos cosas no pueden ser ciertas.

### Lo que se midió

Cada escritura guarda antes los bytes que reemplaza, así que la corrección de las 16:49 tenía su
copia en disco. `Probe --pk` la lee y `--cap 24` dice qué cambiaría:

```
antes:  EXP 1250000 (nivel 100), Stat_Level 100
tras poner CurrentLevel = 24:
        EXP 17280 (nivel 24), Stat_Level 24
        6 bytes cambiarian: 0x06, 0x07, 0x10, 0x11, 0x12, 0xEC
```

Dos cosas quedan claras de ahí. La primera, que **el contenido de la escritura era correcto**: un
Pokémon de equipo lleva el nivel **dos veces** —como experiencia dentro del bloque cifrado y como
`Stat_Level` en las estadísticas de combate que van detrás— y el asignador de `CurrentLevel` de
PKHeX escribe los dos, 0x10-0x12 y 0xEC. La sospecha inicial de que solo tocaba la experiencia era
falsa.

La segunda, que hubo **dos correcciones seguidas al mismo Pokémon**, a las 16:17 y a las 16:49, y
que la copia de las 16:49 mostraba EXP 1250000 otra vez. Es decir: la primera se deshizo entera.

### El fallo de verdad

`AzaharGameWriter.Modify` escribía byte a byte y devolvía `true` **sin releer nada**. Y ahí estaba
el problema, porque el hermano de al lado, `SetBagSlot`, sí relee — y lo hace desde el §22
justamente porque *«el Azahar oficial acepta la escritura y no la aplica»*. Una de las dos rutas de
escritura aprendió la lección y la otra no.

El resultado es lo peor que puede hacer este proyecto: `EnforceLevelCap` devolvía éxito, el monitor
escribía en el log «Corregido en 1 copias» y **añadía un `LevelCapEnforced` al historial de la run**,
todo sobre una escritura que nadie había comprobado. La regla 3 en una línea: un botón que aparenta
funcionar.

Ahora `Modify` relee el hueco y cuenta cuántos de los bytes que mandó están realmente puestos.
Compara **solo los bytes que tocó**, porque el resto de una entrada de equipo se mueve solo mientras
se juega —PS actuales, estado— y comparar los 260 daría un fallo cada vez que el jugador da un paso.
Devuelve `MemoryWriteResult`, que separa *escritos* de *verificados*, y `Applied` solo es cierto
cuando coinciden.

`EnforceLevelCap` añade encima la comprobación que de verdad importa, que no es que los bytes estén
sino que el Pokémon esté al nivel pedido **en los dos sitios**: relee el PK7 y mira `CurrentLevel` y
`Stat_Level`. Y `ApplyDeath` va por el mismo sitio, así que la transformación del muerto también
deja de poder mentir.

### Que se note cuando no funciona

El monitor ya no registra un evento que no ha verificado. Si ninguna copia acepta la escritura, sale
un aviso en el log y **HOME lo dice en rojo**: un cap que calla y no hace nada es peor que no tener
cap, porque el jugador se cree vigilado.

Y lleva cuenta de a quién ha corregido ya, por PID. Corregir dos veces al mismo Pokémon significa
que algo lo deshizo entre medias, y eso es información, no ruido: es la diferencia entre «el cap
funciona» y «el cap se pelea y pierde». Ahora el log y la pantalla lo distinguen.

### Lo que todavía no se sabe

Si la escritura sí cuaja y el juego la deshace después, falta saber **qué copia manda**. El equipo
aparece en memoria varias veces —`equipo.txt` recuerda ocho direcciones en dos familias, una con
salto 0x104 y otra con 0x1E4— y la corrección solo se aplicaba a una. `Probe --equipo` enseña ahora
las seis plazas de todas las copias con los dos niveles al lado, y `--cap N` escribe y dice cuáles
aceptan; volver a mirar después de un combate dice cuál manda.

Y un detalle del log que conviene no perder de vista: en las dos ventanas en que PermaLocke estuvo
conectado al juego ese día estuvo **11 segundos y 108 segundos**. El resto del rato la aplicación
decía «Azahar no responde». El cap solo vigila mientras hay enlace, así que antes de acusar a la
escritura hay que descartar que sencillamente no hubiera nadie mirando.

### La copia que el juego lee se caía de la lista de escritura

Con `Probe --equipo` contra el juego en marcha salió lo que faltaba:

```
0x330128E4  salto 0x104          0x33F7FA44  salto 0x1E4
   Kommo-o   EXP  24  Stat  24      Kommo-o   EXP  45  Stat 187  <-- NO CUADRAN
   Grubbin   EXP   8  Stat   8      Grubbin   EXP   8  Stat  14  <-- NO CUADRAN
   Ledyba    EXP   4  Stat   4      Ledyba    EXP  24  Stat  24
   Shedinja  EXP   3  Stat   3      Shedinja  EXP  24  Stat  24
   Yveltal   EXP  24  Stat  24      Yveltal   EXP 100  Stat  65  <-- NO CUADRAN
```

La de la izquierda tenía el cap puesto. La de la derecha seguía con **45 y 100**, los niveles de
antes. Y la de la derecha es, según el propio `PartyLayoutLocator`, la que manda:
`AuthoritativeStride = 0x1E4`, *«la estructura de la que el juego lee»*, averiguada en su día
escribiendo un mote distinto en cada copia y mirando cuál salía en pantalla.

O sea que la corrección iba a todas partes menos a donde hacía falta. La causa está en una línea del
proveedor:

```csharp
_allLayouts = [.. remembered.Where(layout => ReadParty(reader, layout).Count > 0)];
```

`ReadParty` va por `Pk7Reader.TryRead`, que exige que las estadísticas de combate sean coherentes
—unos PS máximos mayores que cero y unos PS actuales que no los superen—. En la estructura de 0x1E4
esas estadísticas **no están donde el lector las busca**: por eso su `Stat_Level` sale 187, 14 y 65.
Así que el lector la rechaza entera, el filtro la borra de la lista, y las escrituras se quedan en
las copias del bloque de partida, que el juego pisa en cuanto puede.

**Poder leerse y poder escribirse son cosas distintas.** Para escribir, la garantía no es que el
lector entienda la estructura, sino que el escritor exija un checksum de PK7 válido antes de tocar
nada —memoria al azar no lo pasa— y relea después. Con el filtro fuera, la corrección llega a las
dos y **se queda**: `EXP 45 → 24` y `EXP 100 → 24` en la estructura autoritativa, escrito y releído.

### Ocho copias que eran dos

De paso, el barrido arranca una candidata en cada cabecera de Pokémon que encuentra, así que un
equipo de cinco se convertía en cinco «copias»: la misma estructura vista desde el miembro 1, el 2,
el 3… Las ocho direcciones de `equipo.txt` eran **dos** estructuras y seis vistas de ellas.

No es cosmético. Las escrituras van a `SlotAddress(slot)` de cada layout, de modo que una vista que
empieza en el hueco 1 manda la corrección del hueco 4 al hueco 5: **otro Pokémon**.
`PartyLayoutLocator.Distinct` se queda con la dirección más temprana de cada tramo, y hay una prueba
con las ocho direcciones reales que exige que salgan dos.

### Lo que sigue abierto

En la estructura de 0x1E4, el byte 0xEC **no es** `Stat_Level` —el Grubbin, que nadie ha tocado, lee
14 ahí—, y la corrección lo escribe igualmente porque el asignador de PKHeX escribe los dos sitios de
una vez. Es una escritura en un campo sin identificar, con su copia de seguridad. Antes de estrechar
la escritura al bloque cifrado hay que ver qué enseña el juego en el siguiente combate: si el nivel
sale correcto, la escritura completa es la que funciona.

### El cap subió un Pokémon, y fue por confiar en el campo equivocado

Al arreglar §53 se añadió una condición que parecía prudente y era un desastre:

```csharp
if (current.CurrentLevel <= cap && current.Stat_Level <= cap) return Nothing;
```

Es decir: corregir si **cualquiera** de los dos niveles se pasa. Suena a cinturón y tirantes. Lo que
hace de verdad es corregir cuando el campo que no vale dice cualquier cosa.

Porque `Stat_Level` **solo significa algo en la estructura de equipo de verdad**. En las de salto
`0x1E4` esos 28 bytes finales pertenecen a otra cosa, y ahí `Stat_Level` devuelve 145, 187, 202, 250.
La copia de seguridad que el propio escritor guardó antes de tocar nada lo dice sin discusión:

```
33F7FE0C-20260822-172417-cap_de_nivel_24.bin
  especie        165 Ledyba          PID  544CA127
  EXP            91
  nivel por EXP  4      <- correcto
  Stat_Level     145    <- basura: ahi no esta el nivel
  PS             19579/45376         <- ni las estadisticas
```

Un Ledyba de nivel 4. La condición leyó 145, decidió que se pasaba del cap de 24, y **le escribió el
24**. El juego lo aplicó y lo evolucionó a Ledian. Un cap que sube un Pokémon es exactamente lo
contrario de un cap.

La partida guardada no llegó a enterarse —seguía con `Ledyba Nv.4`—, así que cerrar el emulador sin
guardar lo deshizo del todo. Ese es el único motivo por el que esto no costó una run.

**Lo que queda en su sitio a partir de ahora:**

- **Solo la experiencia decide.** Vive dentro del bloque cifrado y el checksum del Pokémon responde
  por ella en todas las estructuras. `Stat_Level` no se lee para decidir nada.
- **El PID por delante.** `NeedsCapping(slot, expectedPid, cap)` exige que el hueco contenga *ese*
  Pokémon. El monitor lo saca del equipo leído de la copia fiable, y con eso una estructura
  desalineada o vieja simplemente no se toca. Igual en la transformación del muerto, donde escribir
  en el hueco de al lado destruiría un Pokémon vivo.
- **No se escribe donde no se sabe qué hay.** `Modify` acepta un límite, y las estructuras que no
  tienen el salto de equipo se escriben solo hasta el final del bloque cifrado. El byte 0xEC de la
  estructura de `0x1E4` vuelve a ser de nadie.

Y una prueba con los números exactos del Ledyba —nivel 4, `Stat_Level` 145, PID 544CA127— que exige
que no se toque.

### La lección, que es más general

Este proyecto llevaba varias secciones repitiendo «medir, no adivinar» y aun así se coló. La forma
del fallo merece recordarse: **un campo leído de una estructura solo vale donde esa estructura está
identificada**. El mismo offset, en otro sitio, no es una lectura mala — es una lectura de otra cosa.
Añadir una condición «por si acaso» sobre un campo así no da seguridad, da un disparador aleatorio.

---

## 54. Por qué el enlace se moría a media sesión (2026-08-22)

El cap de nivel solo vigila mientras hay enlace, y el log decía que el día del incidente PermaLocke
estuvo conectado al juego **once segundos** una vez y **ciento ocho** la otra. Y no se caía y volvía:
se caía y **ya no volvía** en toda la sesión. Eso descarta un problema pasajero de red — un corte
intermitente parpadea— y apunta a algo que se rompe y se queda roto.

Estaba en `AzaharRpcClient.Send`, que hacía exactamente esto: mandar un datagrama, esperar uno, y
darlo por bueno.

### Dos defectos, y el segundo es el que mata

**Una sola conversación, muchos hablando.** El cliente es un *singleton* sobre un único socket UDP.
El sondeo pregunta cada segundo desde su tarea, y la tienda, las herramientas de mochila, el visor y
las sondas preguntan desde las suyas. Dos peticiones solapadas sobre el mismo socket y cada una lee
la respuesta de la otra: fallan las dos, y encima cada una se ha comido el datagrama que le hacía
falta a la contraria.

**Una respuesta que llega tarde no es la tuya.** UDP guarda lo que llegue, aunque llegue después del
plazo. El código leía ese datagrama viejo como si fuera la respuesta de ahora, veía un id que no
cuadraba y lanzaba. Y el siguiente igual, y el siguiente: el socket se queda **permanentemente una
respuesta por detrás**. Un hipo con el emulador ocupado en un combate se convertía en «Azahar no
responde» hasta reiniciar la aplicación. Eso es exactamente lo que cuenta el log.

### Lo que hace ahora

- **Un cerrojo** alrededor de mandar y recibir. Es un protocolo de petición y respuesta sobre un
  socket: serializar no es una precaución, es lo que el protocolo pide.
- **Se vacía la cola hasta encontrar la respuesta buena.** Un id que no cuadra ya no es un error, es
  una respuesta vieja: se tira y se sigue esperando. Ahí se acabó la desincronización.
- **Tres intentos.** Se repite con el **mismo id**, de modo que si la respuesta del primer intento
  llega tarde todavía sirve. Un datagrama perdido es un hipo, no una desconexión.
- Dos contadores públicos, `Retries` y `Discarded`, para poder mirar la salud del enlace en vez de
  suponerla.

### Comprobado

Con un servidor UDP falso que se porta mal a la carta: uno que se traga el primer datagrama y otro
que manda una respuesta ajena antes de cada respuesta buena. La prueba del descarte **se verificó que
falla con el comportamiento viejo**, desactivando solo esa línea, que es lo único que convierte una
prueba en una prueba.

Y contra el emulador real: la aplicación sondeando a 1 Hz mientras seis procesos de la sonda barrían
la memoria a la vez. Los seis contestaron y el enlace **no se cayó ni una vez** en los minutos
siguientes. Antes, esa misma concurrencia era justo lo que lo mataba.

---

## 55. La regla de las Poké Balls no actuaba porque nunca supo dónde estabas (2026-08-22)

El jugador entró en combate varias veces en la misma ruta y el juego le dejó capturar cada vez. La
regla que retira las Poké Balls cuando la zona ya ha gastado su encuentro estaba encendida en
`Data/rules.json` y no hacía nada.

El log lo decía **cada segundo**, desde hacía días:

```
[WRN] ZoneService: Las copias de la zona no concuerdan; PermaLocke no sabe dónde está el jugador
```

Y la regla, correctamente, no toca la mochila cuando no sabe dónde está el jugador. Así que no era
un fallo de la regla: era que la zona no se establecía **nunca**.

### Lo que había de verdad en esas cuatro direcciones

`Probe --zona` lee las cuatro copias y dice por qué se creen o no. Con la mochila exactamente donde
siempre ha estado, o sea con las distancias del §23 intactas:

```
Mochila en 0x33011934

  copia 0: 0x330DDCA8  asa 0461BB94  zona  32
  copia 1: 0x330DDE48  asa 00000002  zona 48074
  copia 2: 0x331FFAA8  asa 0463FF54  zona  32
  copia 3: 0x331FFC48  asa 00000002  zona 65418
```

Dos copias con un asa normal coinciden en la zona 32. Las otras dos tienen el asa a **2** y unos
números que no son zonas: `encdata` tiene 336 y ahí pone 48074 y 65418. Ese par sencillamente **no
está guardando un registro de zona**; el §23 supuso que los cuatro lo estarían siempre.

La regla original exigía que las cuatro coincidieran, así que un par que no es una zona contaba como
**desacuerdo** y tiraba la lectura entera. De ahí que no acertara ni una vez.

### Abstenerse no es discrepar

`TryResolve` ahora distingue las dos cosas. Una copia entra en la votación solo si **puede** ser una
zona: asa distinta de cero y número dentro de las 336. Las que no, se abstienen. Entre las que sí,
no se admite ninguna discrepancia —eso seguiría siendo no saber dónde está el jugador— y hacen falta
al menos **dos** de acuerdo, para que una sola lectura suelta no baste.

`LooksLive` por sí solo no servía: el asa de esas dos copias vale 2, que no es cero, así que pasaban
la prueba de vida y luego envenenaban la votación con un área imposible.

### Lo que corrobora que el ancla sigue buena

La zona 32 es, según `Data/zones.json`, **Pueblo Lilii**. El §23 midió Pueblo Lilii como área **1**.
No es contradicción: el cartucho reparte un mismo sitio entre varias áreas de `encdata` —Ruta 2
aparece como 5, 37, 57, 58 y 64—, así que 1 y 32 siendo los dos Pueblo Lilii es exactamente lo que
cabe esperar de un ancla que sigue apuntando a donde debe.

Aun así, el número que la regla va a usar para quitar y devolver objetos merece una confirmación en
el juego antes de darlo por bueno: quedarse quieto en un sitio con nombre y ver si `Probe --zona`
dice ese sitio.

### Y el ancla estaba muerta: lo que la relajación tapaba

La relajación de arriba se escribió sobre una lectura con el jugador **quieto**, y quieto todo
parecía coherente. `Probe --zona --vigilar` sigue las cuatro copias en bucle, así que basta con
andar. El jugador salió de la Ruta 2 y entró en el Centro Pokémon de Ciudad Hauoli:

```
21:33:03  0461BB94/32  00000002/48074  0463FF54/32  00000002/65418  -> 32 = Pueblo Lilii
21:34:59  0461BB94/1   0461BBC8/1      0463FF54/1   0463FF88/1      -> 1  = Pueblo Lilii
21:34:59  00010000/28  00010000/28     00010000/28  00010000/28     -> 28 = Cueva Sotobosque
21:35:01  0461BB94/32  0461BBC8/32     0463FF54/32  0463FF88/32     -> 32 = Pueblo Lilii
21:35:02  FF000000/0   FF000000/0      FF000000/0   FF000000/0      -> 0  = Ruta 1
21:35:08  BD565D86/54493  7FFF0835/22  BD69874A/51442  7FFF0835/22  -> 22 = Cementerio de Hauoli
...y a partir de ahí, fijo en 22
```

Las «asas» de las copias 0 y 2 pasan a ser `3D957735`, `BD565D86`, `3DBA2EEA`, `3E008EF3`. Leídas
como coma flotante son **0,073**, **−0,052**, **0,091** y **0,126**; la de las copias 1 y 3,
`7FFF0835`, es un **NaN**. Esa memoria ya no guarda un registro de zona: guarda posiciones o cámara.

**El ancla del §23 está muerta.** Las distancias desde el bloque de la mochila apuntaban a un
registro que el juego ha reutilizado para otra cosa, y el 32 que se leía era un resto.

### La relajación se revierte, y por qué importa

Con la regla estricta —las cuatro copias de acuerdo, vivas y en rango— todo lo de arriba se rechaza,
y **rechazar es la respuesta correcta**: el jugador estaba en el Centro Pokémon de Hauoli. Con la
relajada, dos copias «utilizables» coincidían en 22 y PermaLocke habría dicho con toda seguridad
«Cementerio de Hauoli» y actuado en consecuencia sobre la mochila.

La regla estricta rechazaba por un motivo impreciso y aun así acertaba. Para algo que confisca
objetos del jugador, acertar es lo único que cuenta, así que vuelve tal cual, con las lecturas
reales fijadas en las pruebas: las cuatro de la lectura quieta y las de coma flotante.

Es la segunda vez en el mismo día que relajo una comprobación apoyándome en una medida parcial —la
otra evolucionó un Ledyba—, y las dos veces la forma es la misma: **una medida tomada en una sola
situación no sostiene una regla que gobierna todas**. Antes de aflojar un guardia hay que moverse.

### Estado

La regla de las Poké Balls queda **apagada** en `Data/rules.json`, con el motivo escrito al lado.
Volver a encenderla exige localizar el campo de zona otra vez desde cero, que es una investigación
propia —del tamaño del §22 o del §23— y no un ajuste. `Probe --zona --vigilar` es la herramienta con
la que hacerla: se anda por el juego y sale de una pasada cada valor que toma el campo candidato.

---

## 56. Ninguna muerte se había contado nunca, y el motivo era un cero (2026-08-23)

El jugador pidió comprobar que **el cap cambia con cada prueba y que los muertos se cuentan bien**.
Lo primero salió bien. Lo segundo destapó un fallo de fondo que llevaba desde el principio.

### La auditoría

`Probe --run` pone el historial al lado de los totales, que es lo que faltaba para poder distinguir
un número equivocado de un número sorprendente:

```
POKÉMON REGISTRADOS: 186
  Alive       186
    Gacha         152   WonderTrade    28   Capture     6

MUERTES REGISTRADAS: 0
```

Con 186 Pokémon y meses de partida, cero muertes no es una racha.

### La pregunta correcta no era «¿ha muerto alguno?» sino «¿podría detectarse?»

`GameWatcher` empareja el equipo vivo con la run **por PID y por nada más**. Es la decisión
correcta —el PID sobrevive a motes, niveles y evoluciones— pero convierte el PID en un requisito:
sin él, un Pokémon puede caer delante de la aplicación y no pasa nada. Así que la auditoría pasó a
contar eso:

```
DETECTABLES POR EL VIGILANTE (emparejamiento por PID)
  con PID:      6
  sin PID:    180   <- estos no se pueden detectar muertos
    Gacha         152   WonderTrade    28
```

Los seis con PID son **las seis capturas de verdad**, que se registran leyendo la memoria del juego
y por tanto traen el PID puesto. Todo lo que ha entregado PermaLocke —el 97 %— era invisible.

### Dos mitades del mismo agujero

**Una: la run nunca guardaba el PID.** El gacha y el wonder trade guardan su `PokemonEntry` *antes*
de que el Pokémon exista: la tirada se decide y se cobra, y solo después la vista pide la entrega.
El PID nace al construir la entidad, así que cuando se guardaba la entrada todavía no había ninguno.

**Dos, y es peor: el Pokémon tampoco tenía.** `PokemonBuilder.Build` no ponía `PID`, y un `PK7`
nace a cero. O sea que no es que la run no supiera el PID: es que **149 de los 154 Pokémon de la
partida real compartían el mismo**, el cero. Los cinco que no eran los shiny, porque `SetShiny()`
toca el PID de paso.

Medido, no supuesto:

```
En la partida: 154 Pokémon, 149 con PID cero
```

### Lo que se arregla

- `PokemonBuilder` reparte **PID y constante de encriptación** aleatorios. El orden importa: se
  tira primero y se corrige el brillo después con `SetIsShiny`, porque un PID al azar sale shiny
  una vez de cada cuatro mil y un Pokémon no puede volverse shiny por accidente ni dejar de serlo.
- `DeliveryResult` devuelve el `Pid`. Una entrega que no lo dice produce un Pokémon que la run
  posee y no reconoce, que es exactamente el estado del que se viene.
- `PokemonIdentityService` guarda ese PID en la entrada y anota un evento **`PokemonDelivered`**.
  Es un tipo aparte de `GachaRoll` y `WonderTrade` a propósito: aquellos registran una *decisión* y
  este registra una *llegada*. La tirada ocurre aunque la partida no se pueda escribir; solo la
  llegada puede llevar el PID. Un PID de cero **no se guarda nunca**: emparejaría entre sí a todos
  los que no tienen.

### Y lo ya entregado, que ningún arreglo alcanza

Los 149 están escritos en las cajas. Para ellos, `SavePidRepair`, con la misma forma que la
reparación de nombres del §41: copia previa, escritura, **relectura**, y no se da por bueno nada que
no se haya vuelto a ver. Comprueba dos cosas al releer —que no queda **ninguno a cero** y que no hay
**ninguno repetido**—, porque repartir dos veces el mismo número reproduce el problema en pequeño.
Vuelve a su sitio con `PokemonBuilder.InPlace`, que es la lección del §42: al valor por defecto,
tocar ciento cincuenta Pokémon añadiría ciento cincuenta capturas y ciento cincuenta Poké Balls
usadas que nadie tiró.

La otra mitad —enseñarle a la run cuál es cuál— se empareja por **los seis IVs más el flag de
shiny**, que es todo lo que en un Pokémon no cambia nunca: el nivel sube, los EV se reparten, el
mote cambia y la especie evoluciona. Los IVs no están en `PokemonEntry`; están en el campo `ivs`
del evento que lo entregó, que es justo para lo que sirve un registro de eventos. Solo se acepta
una firma **única por los dos lados** —una entrada, un Pokémon en la partida—; lo ambiguo se cuenta
y se deja.

**Trampa que costó una pasada en falso:** hay dos órdenes de IVs vivos en el repositorio. La tirada
los guarda en HP/Atk/Def/**Vel**/SpA/SpD y el lector de cajas los expone en HP/Atk/Def/SpA/SpD/**Vel**.
Los dos son indistinguibles en un Pokémon cuyos IVs especiales y de velocidad coincidan, que es
justo cómo se esconde un fallo así: empareja unos pocos y falla el resto en silencio. La primera
medición dio **0 emparejados de 180**; con el orden bueno, 152.

### Herramientas

```
Probe --run                 la auditoría, ahora con la cobertura de PID
Probe --pids                qué falta, sin escribir nada
Probe --pids --probar       la reparación entera SOBRE UNA COPIA, y la copia se borra
Probe --pids --arreglar     la partida primero, la run después
```

`--probar` sobre la partida real, con el juego cerrado:

```
Antes:   149 de 157 Pokémon no tienen PID. Nada escrito todavía.
Escrito: 149 Pokémon reciben un PID propio, y al releer la partida no queda ninguno a cero ni repetido.
Releído: Los 157 Pokémon de la partida ya tienen PID. No hay nada que reparar.
```

### Lo que sigue sin estar resuelto

Que un Pokémon sea detectable no es que se detecte: el vigilante solo mira **el equipo**, y solo
**mientras la aplicación está abierta y Azahar responde**. Una muerte con PermaLocke cerrado sigue
sin contarse. Eso no es un fallo nuevo, es el alcance de la detección, y conviene tenerlo escrito.

---

## 57. POKE PASTE (2026-08-23)

Exportar lo que hay en la partida en el formato de `pokepast.es`. Solo exportar: leer un pegado
significaría **crear Pokémon a partir de texto**, y de dónde salió un Pokémon es lo único con lo que
una Nuzlocke no puede ser descuidada.

Dos reglas que, mal puestas, producen un pegado que se ve bien y entra mal:

- **En inglés.** El sitio se guía por los nombres ingleses, así que el visor pide la partida a un
  `SaveBoxReader` propio con idioma `en`. El resto de PermaLocke la lee en español porque es lo que
  lee el jugador; aquí no vale. Un pegado lleno de «Bola Sombra» y «Miedosa» entra vacío.
- **Solo lo que importa.** Los EV por encima de cero, los IV por debajo de 31, el nivel solo si no
  es 100. Una lista de seises ceros no es lo mismo que no decir nada, y `Pikachu (Pikachu)` es como
  un pegado delata que lo escribió una máquina que no comprobó si había mote.

---

## 58. Fuera los dos botones de HOME, y el contador manual a cero (2026-08-23)

Dos botones que hacían lo que ya hace otra cosa, y por eso estorbaban.

**ETAPA SUPERADA.** Desde el §49 la etapa **se deduce de los logros**: en cuanto el cristal Z de la
prueba entra en la mochila, el cap sube solo. El botón se dejó como red de seguridad «por si la
detección se retrasa», y lo que hace un botón así es adelantarla. En la run real se había pulsado
**seis** veces con **dos** pruebas detectadas, así que el cap en vigor era **40** en vez de 24: el
cap sale de `Math.Max(a mano, detectado)`, y la mano iba muy por delante.

**REGISTRAR CAPTURA.** Lo que hay en la partida se ve entero en el visor, y lo que aparece en el
equipo sin registrar lo anuncia el aviso de HOME con su propio botón. El manual solo servía para
registrar a ciegas.

### Deshacer seis pulsaciones sin tocar el fichero de la run

`run.json` tiene el número, y editarlo a mano habría sido justo el cambio silencioso de estado que
la regla 4 prohíbe. `Probe --etapas [n]` va por `ProgressService.AdvanceAsync`, o sea que la
corrección deja su evento como cualquier otra cosa, y relee el repositorio antes de darla por buena:

```
  etapas marcadas a mano:  6
  etapas por los logros:   2
  en vigor (la mayor):     6
  cap en vigor:            40

Etapas a mano: 6 -> 0
Cap en vigor:  24
```

Dos detalles que salieron de escribir la herramienta:

- `ClearedAsync` devuelve **la mayor** de las dos, así que en una run donde la mano va por delante
  las dos se leen iguales y no hay forma de ver cuál sostiene el cap. `DetectedAsync` pasa a ser
  pública para poder enseñarlas por separado; es lo único que distingue «lo detecté» de «lo
  pulsaste».
- El evento decía «Cap de nivel: 14» mientras HOME decía CAP 24, y las dos tenían razón: ese
  número es el del contador manual y el de HOME es el efectivo. Ahora la descripción dice **«Cap
  por marcas a mano»**, y también cuántas etapas se movieron, porque «etapa revertida» tras
  revertir seis deja al que lee contando.

---

## 59. El que se va también cuenta, y el «muerto» que no lo es (2026-08-23)

Con los PID ya repartidos —157 de 157 en la partida, sin ninguno a cero ni repetido— quedaban
**28 registros de la run sin PID**. La primera lectura fue «no están en la partida», que es cierta
pero no explica nada, así que la sonda pasó a decir por qué:

```
registrados sin PID: 28 de 187
Pokémon en la partida sin dueño: 2 de 157
de los que faltan, con IVs en el historial: 28 de 28
  Vanilluxe   Gacha   23/31/24/20/15/3|False   en la partida: 0
```

Tienen IVs guardados y **no hay ningún Pokémon en la partida con esa firma**, ni siquiera contando
los ya asignados. No es que el emparejamiento falle: es que esos Pokémon no existen. Son los que
el jugador **entregó en sus 29 wonder trades**.

### Un intercambio quita uno y solo se apuntaba el que llega

`WonderTradeService` daba de alta al que viene y no decía nada del que se va, de modo que su
registro se quedaba `Alive` para siempre. HOME contaba **187 vivos** cuando 28 de ellos no están en
el juego. Es el mismo error que una muerte sin registrar: un número que describe otra cosa
distinta de lo que dice.

`MarkGivenAsTradedAsync` lo cierra, y con dos decisiones que importan:

- **Después de escribir la partida, no dentro del intercambio.** Hasta que el save no está escrito
  no se ha ido nadie, y marcar antes deja al registro mintiendo si la escritura falla.
- **Por PID.** Que es lo que solo se pudo hacer desde el §56; sin él no hay manera de saber cuál de
  los tres Vanilluxe del jugador se fue.

### Lo ya intercambiado: se mide, no se supone

`Probe --intercambiados` cruza **la especie que el propio evento apuntó como entregada** contra los
registros vivos sin PID. Sobre la run real:

```
wonder trades en el historial: 29
registrados vivos y sin PID:   28

se pueden marcar como entregados: 26
en disputa (no se tocan):          2
  especie 487: el historial dice 2 entregado(s), y sin PID hay 1
  especie   3: el historial dice 1 entregado(s), y sin PID hay 0
```

Las dos en disputa se quedan como están. Elegir cuál de dos Giratina se fue sería inventar
historia, y el registro de eventos está encadenado por hash: lo que se escribe ahí no se quita.

### El «muerto» que había en el equipo

El jugador tenía un Pokémon muerto y preguntó si contaba. Barriendo la partida sale en el
**equipo, hueco 4**:

```
Shedinja "MUERTO" Nv.3  mov=0/0/0/0  PID CA63B17E
```

Esa es exactamente la marca de muerte de PermaLocke: `DeathTransform` convierte al Pokémon en
**Shedinja (292) apodado MUERTO**, nivel 1 y sin movimientos. O sea que **no se murió: se escribió**,
probando que la escritura en memoria funcionaba. La run no lo cuenta —cero eventos
`PokemonDied`— y hace bien: nada se debilitó.

Lo que no tiene arreglo es saber **quién era**. Es una escritura en memoria, y las copias que deja
son de los bytes reemplazados, no del Pokémon entero; y el respaldo completo de partida más antiguo
que hay en disco, del 2026-08-20 a la 01:40, **ya lleva el MUERTO puesto**. Lección para la
próxima: una prueba destructiva sobre la partida de alguien tiene que dejar por escrito qué
destruyó, no solo los bytes.

De paso se vio el otro Pokémon sin registrar de la partida: un **Kommo-o Nv.24** en el equipo, que
la run nunca ha llegado a conocer.

---

## 60. Premios de una sola vez (2026-08-23)

Un botón en MISCELÁNEA que, **con las doce pruebas superadas**, entrega **12 Hiperpociones y 12
Curas Totales**, y solo una vez.

### Qué decide cada cosa, y contra qué

Tres condiciones, y ninguna se apoya en una marca que alguien haya puesto en esta pantalla:

- **Ganado**: los doce logros `prueba-01`…`prueba-12` desbloqueados, que es cosa de
  `AchievementService` y sale del cartucho —cada prueba se detecta por el cristal Z que deja en la
  mochila, §43—.
- **No recogido**: que no haya un evento `RewardClaimed` con ese id en el historial. **Eso es lo que
  hace que «una vez» sea una vez.** Un booleano en el ViewModel no lo sería: basta cerrar la
  aplicación.
- **Entregable**: la mochila está ahí para escribir en ella, por el camino de la tienda —ping, suma
  a lo que ya llevas, y **relectura** antes de dar nada por bueno—.

`RewardClaimed` es un tipo de evento propio y no un `TestItemGranted` justamente porque **es el
cerrojo**: si el premio se registrase como las herramientas de pruebas, cualquier pulsación de los
Caramelos Raros parecería un premio recogido.

### El orden, y el caso raro que decide el diseño

Como en la tienda (§45): **primero se entrega, después se registra**. Un premio registrado antes de
una entrega que falla quema algo que solo se da una vez y no deja al jugador a quién reclamar.

El caso interesante es la **entrega a medias**: son dos objetos, van uno detrás de otro, y el
emulador puede desaparecer entre los dos. Ahí hay que elegir entre un jugador al que le deben doce
Curas Totales y un botón que se puede volver a pulsar para duplicar las Hiperpociones que ya dio.
En una competición **la segunda es peor**, así que media entrega **cuenta como recogida**, el evento
guarda exactamente qué llegó y qué no, y la pantalla lo dice en voz alta. Si no llega **nada**, no
se recoge nada: el premio sigue disponible.

### Configuración, no código

`Data/rewards.json`, como la tienda y las penalizaciones. Los ids de objeto son los del cartucho
—Hiperpoción **25**, Cura Total **27**, buscados con `Probe --objeto-find`— y **el nombre se
comprueba contra la tabla del juego antes de escribir nada** (§52): un id copiado de memoria que
caiga en otro objeto entrega otra cosa y no falla nunca. Añadir un premio es una entrada en el
fichero; no se toca código.

La tarjeta enseña siempre cuánto falta —«2/12 pruebas»— en vez de esconder el botón. Un premio que
no se ve no se persigue.

---

## 61. Los cristales Z, sacados del cartucho por fin (2026-08-23)

Las tarjetas de logro llevaban un rombo dibujado con la inicial del logro, y el §34 explicaba por
qué: los cristales Z del cartucho **no se podían emparejar con su tipo sin adivinar por color**.
El jugador pidió los dibujos de verdad, así que se ha vuelto a la pregunta con más herramientas.

### Dónde están, y qué dice el propio cartucho

No están en `a/0/6/1`: ese contenedor se acaba en el icono 769 y los cristales son los objetos
807-824. Están **incrustados en dos pantallas**, `a/1/5/5` y `a/1/4/2`, que son ficheros **ALYT**
—descripciones de interfaz— con sus imágenes dentro. Y ahí está el hallazgo que faltaba en el §34:
la tabla de ficheros de un ALYT trae **nombres**, y los dieciocho se llaman

```
item_807.bflim  item_808.bflim  …  item_824.bflim
```

O sea que el cartucho dice **qué objetos son**. Barriendo todas las pantallas del juego —3431
dibujos— esos dos ALYT son los únicos que los llevan.

Los ids se confirman desde fuera, y por partida doble: los nombres de objeto van
`Normastal, Pirostal, Hidrostal, Electrostal, Fitostal, Criostal, Lizastal, …`, que es el orden de
tipos; y **las doce pruebas de `Data/achievements.json` caen cada una en el tipo que su prueba es
de verdad** —Liam Normal 807, la Gran Prueba de Kaudan Lucha 813, Nereida Agua 809…—. Doce
comprobaciones independientes, doce aciertos.

### Lo que sigue sin poder medirse

**Un ALYT guarda su tabla de nombres y sus datos en órdenes distintos, y no publica el mapa.** Se
intentó de tres maneras: emparejar por posición —cuadra en número y da disparates, un
`Report_BG_All_00` de 32x32 y un `item_818` de 320x240—; recorrer los datos en orden de tabla —el
primer dato no es el primer nombre—; y leer las secciones `LTBL`/`LMTL`/`LFNL`, que son partes del
formato de interfaz y no un índice de ficheros.

Así que el **qué** está medido y el **cuál es cuál** no. La prueba de que no lo está: la primera
imagen de los datos es un cristal dorado y el primer nombre es el Normastal, que es pálido.

### Entonces se empareja por color, y se dice

Cada cristal es del color de su tipo. Los dieciocho, midiendo la media del 40 % de píxeles más
saturado, se reparten solos en familias: uno amarillo, uno rojo, dos verdes, tres marrones, dos
rosas, dos morados, **cinco azules**, uno casi gris y uno casi negro — que es exactamente el reparto
de los dieciocho tipos. Dentro de cada familia las decisiones están escritas en `ZCrystalIndex`:
el más pálido de todos es Normal y el azul más pálido es Volador, el más oscuro es Siniestro, el
verde más amarillento es Bicho, el morado más rojizo es Fantasma, y el más profundo de los dos
azules limpios es Dragón.

Es un ancla **más floja** que las del resto del repositorio y va dicha así, no disimulada. El §34
rechazó este mismo trabajo por eso; se hace ahora porque aquí **una imagen a un tono de distancia no
cuesta nada**, mientras que en el §45 —los iconos de la tienda— un icono equivocado le habría
enseñado al jugador un objeto que no era el que compraba. Cuando lo que está en juego es un dibujo
y no una decisión, el listón puede bajar; lo que no puede es bajar en silencio.

### Tallar un BFLIM de dentro de otra cosa

Un BFLIM no tiene cabecera: primero van los píxeles y al final una cola de 0x28 bytes. Para
sacarlos de un ALYT hay que buscar las colas, y **buscar las cuatro letras `FLIM` no vale**: salen
dentro de los píxeles de otras imágenes y cada falso positivo desplaza todo lo que viene detrás.
Una cola de verdad lleva además la marca de orden de bytes `FEFF`, un bloque `imag`, y un tamaño
declarado que tiene que ser exactamente sus píxeles más la cola. Con los tres, veintisiete colas
válidas; con solo las letras, veintisiete también, pero no las mismas.

De los dieciocho cristales se sabe cuáles son entre las demás imágenes de la misma pantalla porque
**todos tienen la misma forma**: 32x32, RGBA5551 y exactamente **168 píxeles opacos**. Cambian de
color y nada más.

### Y los que no son pruebas

`Data/achievements.json` gana un campo `icono`, que es el id de un objeto del cartucho. No es lo
mismo que `item`: `item` es lo que **desbloquea** el logro y `icono` lo que se **ve**. Coinciden en
las pruebas, y por eso las pruebas no lo llevan escrito.

- **Las Dominsignias no tienen sprite.** Se barrieron los 3431 dibujos de todas las pantallas del
  juego y no aparece ninguno: son objetos del mundo, no un icono de interfaz. Las tres pegatinas
  van con **perla, perla grande y pepita de oro**, que es la familia de cosas que se recogen por el
  mundo, y queda escrito en el propio JSON que son sustitutos.
- Campeón lleva **Master Ball** y defender el título **Gloria Ball**, que es la conmemorativa.
  Movimientos Z, un cristal; huidas, la **Cuerda Huida**; variocolor, **Polvo Estelar**;
  entrenadores, **Ataque X**.

El rombo dibujado **no se ha borrado**: sigue ahí para cuando no hay ROM de la que sacar iconos.
Una tarjeta con un hueco sería peor que una dibujada.

### De paso: el Poke Paste se queda solo con el equipo

Un pegado es un **equipo** —seis Pokémon que otro puede cargar y combatir— y una caja de treinta no
lo es. Para mirar el PC está el visor.

---

## 62. El rol LUDÓPATA y su ruleta (2026-08-23)

Un cuarto rol que no cambia ni un punto de lo que se gana o se pierde: lo que cambia es que
**después de cada hito hay que girar una rueda y vivir con lo que salga**. Una tirada por prueba,
tres por la liga y dos más por el rematch.

### Las tiradas se deben, no se ofrecen

Es la misma forma que los premios del §60, y por la misma razón: los dos números salen de algo real
y ninguno se marca a mano. **Ganadas** = los logros de prueba, liga y rematch, que se detectan solos
contra el cartucho. **Gastadas** = cuántos eventos `RouletteSpun` hay en el historial. Se deben las
primeras menos las segundas, y el botón no se enciende si eso es cero.

`RouletteSpun` es un tipo de evento propio porque **es el contador**, igual que `RewardClaimed` es
el cerrojo del premio.

### Cada tirada se puede recomputar

Como el gacha: la rueda sale de la seed de la run y del número de tirada. Eso da dos cosas por el
precio de una — una tirada se puede comprobar después, y **una tirada fallida se puede repetir sin
riesgo**, porque el mismo número da exactamente la misma cara. Nadie puede volver a girar para
esquivar una muerte.

La rueda enseña **seis de las dieciséis, sorteadas entre todas juntas y sin repetir**. Nada las
equilibra: pueden salir seis buenas o seis malas, y hay un test que lo exige —si en cuatrocientas
tiradas no aparece ninguna rueda de un solo color, algo está amañando el sorteo—.

### Se decide y se escribe antes de animar

El orden importa y es deliberado: la aplicación **decide la cara, toca la partida y registra el
evento**, y solo entonces revela los seis `?` uno a uno y gira la rueda hasta la cuña ganadora. No
hay un botón que parezca estar resolviendo algo que sigue pendiente, ni una animación que decida la
suerte después de haber escrito. Es lo mismo que hace el gacha (§31): la rueda es presentación de un
resultado que ya existe.

### Qué toca cada clase de cara

- **Gacha**: no tira en el acto. Da **crédito**, que aparece en el banner que toque como «1 tirada
  gratis» y se gasta allí, con su rueda y su sprite. Cada tirada guarda en su propio evento a qué
  banners da crédito, para que lo que se cobra salga de lo que el evento dijo y no de la
  configuración de hoy (§63).
- **Habilidades, IV y muerte**: solo el equipo actual y siempre **por PID**. Una muerte deja la
  marca explícita —Shedinja `MUERTO` sin movimientos— y su evento `PokemonDied` con motivo
  «ruleta», pero sin delta de penalización.
- **Objetos**: los bolsillos los declara el propio cartucho. Una MT nueva no puede ser una que ya se
  lleve, y ninguna resta deja un contador por debajo de cero.
- **Puntos**: el `PointsDelta` de `RouletteSpun` es exactamente +200 o −200, sin pasar por el
  multiplicador del rol.

### Todo por el fichero de partida, y esa es la decisión de diseño

Las dieciséis caras entre todas tocan el **equipo** (habilidades, IV, muertes), la **mochila**
(objetos curativos, MT) y las **cajas** (las tiradas de gacha gratis). Por memoria serían tres
mecanismos distintos y, peor, una ruleta que necesitaría el juego **abierto** para unas caras y
**cerrado** para otras. Por el save es una sola puerta: se abre una vez, se copia, se escribe una
vez y se relee.

De ahí que la pantalla diga «guarda y cierra el juego» igual que el gacha y el wonder trade.

Y la relectura comprueba el **significado**, no que la escritura devolviera cierto: exige la
habilidad concreta, los **seis** IV, la marca de muerte o el número final exacto de cada objeto. Que
PKHeX haya aceptado `Write` no es la garantía; es el paso previo a comprobarlo.

### Detalles que costaron una medición

- **Las cien MT no son un rango.** En Ultra Luna son 328-419 (MT01-MT92), 618-620 y 690-694, con
  dos saltos, y 420-427 son las MO de generaciones viejas. La lista buena la da el propio bolsillo:
  `pouch.GetAllItems()` devuelve exactamente las cien de este cartucho, así que no se teclea.
- **Escribir en la mochila del save** es `pouch.SetPouch(game.Data)` y luego `game.Write()`.
  Comprobado sobre una copia de la partida real antes de fiarse: Poción 8 → 13 y un MT02 que no
  estaba.
- **Los nombres de habilidad se comprueban uno a uno.** La lista original traía diez mal escritos
  —«Absorbe Electricidad» es «Absorbe Elec», «Foco Interno» es «Fuerza Mental», «Fuerza Pura» es
  «Energía Pura», «Stall» es «Rezagado»— y cuatro que no son habilidades de este juego. Un nombre
  que no resuelve **se cae al cargar el fichero y hay un test que falla**; si se resolviera por
  número, la cara habría repartido en silencio otra habilidad. Y nada por encima de la 233, que es
  la última que conoce Ultra Luna: una de gen 8 tiene id pero el cartucho no sabe qué es.
- **Los puntos van en el delta del propio evento.** El saldo es la suma de los deltas del
  historial, así que la ruleta mueve puntos con un solo apunte que dice quién los movió. Y **no los
  multiplica el rol**: la ruleta dice doscientos y son doscientos.
- **Una muerte de ruleta no resta puntos**, que es lo que pide la competición, pero sigue siendo un
  `PokemonDied`: esconderla bajo otro nombre dejaría un recuento de muertes que miente.

### Lo que hace cuando no puede hacerlo entero

Quitar una MT a quien no lleva ninguna no hace nada; subir los IV a tres Pokémon cuando el equipo
tiene dos se los sube a esos dos. Ni se inventa ni se falla del todo, y la línea del resultado lo
dice. Un hueco que ya no tiene al Pokémon que la rueda eligió —comprobado por PID— **se salta**, en
vez de escribirle al que esté ahora.

### Un fallo que solo apareció al abrirla

La sección RULETA se mete y se saca de la barra lateral según el rol de la run cargada. La run se
carga **en segundo plano**, y tocar desde ahí la colección que pinta la barra lateral tira la
ventana entera:

```
System.NotSupportedException: Este tipo de CollectionView no admite cambios en el SourceCollection
de un subproceso distinto del subproceso Dispatcher.
```

No salta abriendo la aplicación sin run —que es como se prueba casi siempre—, solo arrancando con
una run ya guardada. Ahora pasa por `IUiDispatcher`.

### Verificado en el juego, y una advertencia

Las dos primeras tiradas se hicieron **contra la partida real**: la primera salió «1 tirada de
gacha» y entregó un Archeops en la caja 6, hueco 4; la segunda salió «1 MT» y escribió el MT56 en
la mochila. Las dos con copia previa y relectura.

Ocurrieron desde una **copia aislada** de la aplicación levantada para ver la pantalla, y de ahí una
lección que conviene no repetir: una copia aislada tiene su propia carpeta de run, pero
`PlayerSave` localiza el save de Azahar, que es **el de verdad**. Aislar la run no aísla la partida.
La copia se ha borrado por eso.

Esas dos tiradas dejaban sin ver justo lo que más importaba: las caras que **destruyen** algo, que
nunca habían tocado una partida. Se cerró después pasando **las dieciséis** contra copias de la
partida real, cada una sobre la suya, en el §65.

---

## 63. Tiradas gratis y wonder trades: crédito, no regalos sueltos (2026-08-27)

Dos peticiones que resultaron ser la misma cosa.

**Una:** la ruleta, cuando salía una cara de gacha, tiraba en el acto y ponía el nombre del Pokémon
en una línea de texto. El jugador quería que en vez de eso apareciera **«1 tirada gratis» en el
banner que toca**, y que se tirase allí, con su rueda y su sprite. Tiene toda la razón: una tirada
tiene que ocurrir donde ocurren las tiradas.

**Dos:** superar cada prueba pasa a dar tiradas de gacha y wonder trades, según la tabla de la
competición: dos pochas en las tres primeras pruebas, dos decentes en las cuatro siguientes, una
tocha y una decente en las cuatro siguientes, dos tochas en la duodécima, y tres tochas y cuatro
wonder trades por la liga y otros tantos por el rematch.

Las dos son **crédito**: algo ganado que se guarda y se gasta en su pantalla.

### Ni un contador guardado

Misma forma que los premios del §60 y las tiradas de ruleta del §62, y por la misma razón:

- **Ganado** = los logros desbloqueados, según `Data/grants.json`, más las caras de gacha que haya
  sacado la ruleta. Estas últimas se leen **de los eventos de tirada**, no se apuntan al vuelo: el
  evento guarda qué cara salió, y la cara sabe a qué banners paga, así que el crédito y la
  auditoría son el mismo hecho.
- **Gastado** = los eventos marcados `gratis`.
- **Disponible** = la resta, nunca por debajo de cero.

Nada se almacena, así que nada puede desincronizarse.

### La marca `gratis` es lo que hace que esto funcione

`GachaRoll` y `WonderTrade` guardan ahora si se pagaron con crédito. Sin esa marca **todas** las
tiradas del historial contarían como gastadas, y una run con ciento cincuenta tiradas debería
créditos que nunca tuvo.

Es también lo que hace que la regla no mire hacia atrás: esta run llevaba **treinta wonder trades**
hechos cuando los wonder trades eran gratis e ilimitados. Ninguno lleva la marca, así que ninguno se
cobra. Una regla que alcanza al pasado no es una regla, es un castigo.

### Los wonder trades pasan a estar limitados, y eso es una decisión

Hasta ahora no costaban nada y no había tope. Con eso, «te dan 1 wonder trade» no significaría nada,
así que **exigen crédito**: sin crédito, el botón no deja y dice de dónde salen. Es la lectura que
convierte la tabla de la competición en una tabla y no en decoración.

Va en `Data/grants.json` como `limitarWonderTrades`, porque es una regla de la competición y no un
hecho del intercambio. A false vuelven a ser libres y el número se queda como información. Y **si el
fichero falta, no se limita nada**: un fichero de configuración que desaparece no puede cerrar una
función que ya existía.

### En pantalla

La tarjeta del banner cambia «300 puntos» por «**2 tiradas gratis**» y se pone verde; el botón dice
**TIRAR GRATIS**. El panel del wonder trade lleva una chapa con los que quedan. Los dos números se
vuelven a pedir después de cada uso en lugar de descontarse en la vista: una copia local sería un
número más capaz de discrepar del historial.

---

## 64. Cambiar de rol, y cuadrar la run con la partida (2026-08-27)

Tres cosas pequeñas que se estorbaban entre sí.

### El rol no tenía puerta

`RunService.ChangeRoleAsync` existía desde el §46 —con su motivo obligatorio, su evento y su
retroceso si el evento falla— y **no había ningún sitio desde el que llamarlo**. Con el rol
LUDÓPATA recién hecho eso pasó de ser una carencia teórica a un problema: la ruleta solo aparece en
ese rol, así que para probarla había que empezar una run desde cero.

Ahora hay un botón en la tarjeta de HOME. La ventana marca de entrada el rol actual, exige el
motivo, y **avisa arriba y en grande de lo único que no se deshace**: cambiar de rol **no vuelve a
randomizar**. Los niveles de los enemigos y el Pokémon extra de los combates importantes están
escritos en el mod ya instalado y ahí se quedan; lo que cambia al momento son los puntos y las
reglas de la aplicación. Venderlo sin decirlo sería vender media dificultad.

### Un crédito que se habría pagado dos veces

Al reconciliar la base de datos apareció un caso que no se ve razonando en abstracto. Las dos
tiradas de ruleta del §62 salieron **antes** del §63, cuando una cara de gacha tiraba en el acto y
entregaba el Pokémon. Si el crédito se dedujera de la cara ganadora mirándola en el catálogo, esas
dos tiradas pagarían **otra vez** ahora, por un Archeops que ya está en la caja 6.

Así que el crédito no se deduce: **cada tirada guarda en su propio evento a qué banners da crédito**,
en el campo `credito`. Las tiradas viejas no lo llevan y por tanto no pagan nada. De rebote se gana
otra propiedad que interesa igual: editar `Data/roulette.json` ya no puede reescribir crédito
concedido.

Es el mismo principio que la marca `gratis` del §63, aplicado del otro lado: **lo que se cobra y lo
que se paga salen los dos de lo que el evento dijo que pasó**, nunca de la configuración de hoy.

### La run vuelve a cuadrar con la partida

Los diez eventos que se quedaron en la copia aislada del §62 —dos wonder trades, una tirada de
gacha con su Archeops y las dos tiradas de ruleta— se han incorporado. Se comprobó dos veces que la
copia era un **superconjunto estricto**: cero eventos en la real que no estuvieran en la copia, diez
en la copia que no estuvieran en la real.

Y con `Probe --intercambiados --arreglar` se han cerrado **26** registros que seguían `Alive` y cuyos
Pokémon se habían entregado en wonder trades. Los **5 en disputa** se quedan: cuando el historial
dice que se entregaron dos Giratina y solo hay uno sin PID, elegir cuál sería inventar historia.

```
antes:   Alive 189,  Traded  1
después: Alive 163,  Traded 29
```

---

## 65. Las dieciséis caras, contra una copia de la partida real (2026-08-27)

Las caras de la ruleta tenían pruebas contra un save construido en memoria, que cubre la edición
pero no el viaje por un fichero de verdad. Y las que más falta hacía comprobar son justo las que
**destruyen** algo: un Pokémon convertido en Shedinja, seis IV puestos a cero. Ninguna había tocado
nunca una partida, así que la primera vez que se ejecutasen de verdad habría sido también la primera
vez que ese código se ejecutaba.

`Probe --ruleta --probar` las pasa **todas**, cada una sobre su **propia copia** —para que no se
tapen entre ellas— y comprueba el resultado releyendo el fichero:

```
  Habilidad buena            OK   Yveltal: Ímpetu Tóxico | Kommo-o: Allanamiento | Ledyba: Cura Natural
  Objetos curativos          OK   Despertar: 0 → 1 | Revivir: 6 → 7 | Antihielo: 0 → 1
  1 MT                       OK   MT51: 0 → 1
  IV al máximo               OK   Ledyba, Yveltal y Grubbin: los seis IV a 31
  Muere 1 Pokémon            OK   Muere Ledyba (Nv.4, Ledyba)
  Mueren 3 Pokémon           OK   Muere Ledyba | Muere Grubbin | Muere MUERTO (Nv.3, Shedinja)
  IV a cero                  OK   Yveltal, MUERTO y Kommo-o: los seis IV a 0
  Menos 1 MT                 OK   MT56: 1 → 0
  Menos objetos curativos    OK   Revivir Máximo: 1 → 0 | Revivir: 6 → 5 | Poción: 8 → 7

TODAS LAS CARAS HACEN LO QUE DICEN. La partida no se ha tocado.
```

Comprobado además por fuera: la fecha del save no se movió.

### Ninguna ruta de escritura apunta a la partida

No basta con no escribir en ella: la sonda registra su **propio** `IRouletteWorldPort` con la carpeta
de respaldo dentro del temporal, así que aunque algo llamase por error al camino normal de la
ruleta, la copia de seguridad y la escritura irían ahí. Es la lección del §62 aplicada de antemano:
aislar la run no aisla la partida, así que lo que hay que aislar es **la ruta de escritura**.

### La comprobación estaba peor escrita que lo comprobado

Dos caras salieron **MAL** en la primera pasada, y las dos eran mentira. La condición decía «si
bajaba, que no haya quedado con más de uno», que no significa nada: Revivir estaba a 6, bajaba 1 y
quedó en 5, que es exactamente lo correcto.

El fallo de fondo era que la comprobación no leía el **antes**. Ahora se lee el recuento de la copia
antes de aplicar y se exige `después == recorte(antes + delta)`, con lo que de paso queda probado
que quitar **no baja de cero**: el Revivir Máximo estaba a 1, la cara pedía tres y quedó en 0.

Vale la pena dejarlo escrito porque es un error con forma propia y fácil de repetir: **una
verificación puede estar peor pensada que el código que verifica**, y entonces lo que falla es la
prueba. Se distingue mirando el dato concreto que da por malo, no volviendo a leer el código.

---

## 66. Diez Super Balls, los iniciales, y empezar de cero (2026-08-27)

### El premio se ancla en lo que el juego deja, no en lo que el juego cuenta

«La primera vez que el profesor Tilo te da Poké Balls» no mueve ningún récord de la ficha de
entrenador ni enciende ninguna bandera que se haya identificado. Buscar una a ojo entre las 4960 sin
etiquetar sería el error del §40 otra vez. Pero ese momento **deja Poké Balls en la mochila**, y eso
sí se ve.

Así que `Data/rewards.json` gana una segunda clase de condición junto a `achievements`:

```json
"objetosEnMochila": [4]
```

Es el mismo truco con el que se anclaron las doce pruebas a su cristal Z, y llega al mismo sitio:
`RewardService` cuenta contra `reward.Conditions`, que ahora es la suma de las dos clases, y
`GetStatusAsync` sigue diciendo «1/1» sin saber de qué clase era. Los ids se buscaron
(`Probe --objeto-find`) en vez de recordarse: **3 es Super Ball y 4 es Poké Ball**, y el nombre se
comprueba contra la tabla del cartucho antes de escribir, como en el §52.

Una diferencia que conviene tener escrita, porque la primera versión del comentario la tenía mal.
Un cristal Z el juego **no lo quita nunca**, así que su condición, una vez encendida, no se apaga.
Una Poké Ball **se gasta**: un jugador que se quede sin ninguna verá el botón apagarse. No es un
fallo —la condición dice la verdad en todo momento—, pero lo que hace que el premio sea de una sola
vez no es la condición sino el evento `RewardClaimed`, y ese no se borra al gastar nada. Un premio
ya recogido sigue recogido con la mochila vacía.

Y la lectura falla hacia el lado seguro: si la partida no se puede leer, `HeldAsync` devuelve un
conjunto vacío, o sea condición **no cumplida**. Un premio que se entregase solo porque faltaba un
fichero sería la peor forma posible de fallar, y hay test.

### Los tres iniciales: se leyeron, y luego se quitaron de la app

En un random los tres huevos son idénticos, así que la forma habitual de elegir es coger uno,
mirarlo y volver a un guardado si no gusta. Se hizo un panel en RANDOMIZADOR que los enseñaba con
nombre y sprite, y **el jugador pidió quitarlo el mismo día**: saber qué hay dentro de cada huevo
antes de abrirlo le quita a la elección justo lo que la hace una elección. Queda escrito porque la
decisión es de diseño y no de implementación, y porque lo que sí sobrevive es lo de debajo.

`StarterReader` sigue en `PermaLocke.Randomizer`, con su comando `RomTool iniciales <carpeta>`. Abre
el `a/1/5/9` **de la carpeta del mod** y lee las tres primeras entradas de la tabla de regalos. Es
deliberado que no mire el informe de la última generación: un informe es lo que PermaLocke **dijo**
que hizo, y la pregunta es qué hay en el fichero que el emulador carga.

Y se ancló algo que llevaba desde el §19 escrito como afirmación y nunca medido: **que las entradas
0-2 son los iniciales**. Se comprobó leyendo el `a/1/5/9` sin parchear del propio cartucho, donde
valen **722, 725 y 728** —Rowlet, Litten y Popplio, en el orden en que el juego los ofrece—. Sobre
los dos mods generados del repositorio salen especies distintas, válidas y todas de primera etapa.
Ese anclaje vale para cualquier cosa que en el futuro toque esa tabla, se enseñe o no.

Detalle de implementación con su motivo: `GarcPatcher.ReadOnly` existe porque el constructor normal
abre el contenedor en lectura/escritura, que es lo que hace falta para parchear. Pedir permiso de
escritura sobre una carpeta que es del emulador, solo para mirarla, es como un lector acaba
truncando un fichero que nunca tuvo que tocar.

### Empezar de cero: una run nueva, no un borrado

Con una run cargada no había **ninguna** forma de crear otra: el botón CREAR RUN solo existe en el
estado vacío de HOME, así que empezar una segunda partida obligaba a tocar la base de datos por
fuera. Ahora hay EMPEZAR DE CERO junto al rol, que abre la misma pantalla de creación.

Lo que hace es crear una run con su propia seed y dejarla activa —`RunService.CreateAsync` ya llama
a `SetCurrent`, y el arranque carga la más reciente por fecha, así que no hace falta nada más—. Lo
que **no** hace, dicho en el aviso antes de abrir nada, que es donde sirve:

- **No borra la run vieja.** Su historial está encadenado por hash; borrar un registro de auditoría
  para que las cifras queden limpias es exactamente lo que ese registro existe para impedir.
- **No toca la partida de Ultra Luna.** PermaLocke no empieza partidas, las empieza el jugador. Si
  quiere una partida nueva, la hace en el juego.
- **No vuelve a randomizar.** La run nueva tiene otra seed, así que el mundo instalado sigue siendo
  el de la anterior hasta que se genere e instale otra vez.

Los tres avisos son del mismo tipo que el de cambiar de rol (§64): un botón que suena a «reiniciar»
tiene que decir qué se lleva por delante y qué no, porque las dos suposiciones contrarias —«esto me
borra los Pokémon» y «esto me deja el mundo nuevo»— llevan a un desastre distinto.

### Abierto o cerrado, dicho en cada pantalla

PermaLocke entra al juego por **dos puertas distintas** y cada una pide lo contrario. La memoria
está viva y necesita Azahar en marcha (§22); el fichero de partida es un fichero y no se puede
escribir por debajo de un juego que lo va a pisar al guardar. Eso estaba escrito en el `ARCHITECTURE`
y suelto en algún texto de alguna pantalla, pero la aplicación **no lo decía donde hace falta**, que
es en la pantalla en la que estás a punto de pulsar algo.

Ahora cada sección lo declara —`SectionViewModel.Needs`, con su porqué en `NeedsDetail`— y la
cabecera del shell pinta la insignia arriba a la derecha, en las mismas tres bandas de color que ya
usa todo lo demás: **verde** juego abierto, **ámbar** juego cerrado, **apagado** da igual. Es una
propiedad virtual y no una tabla en el shell a propósito: la respuesta sale de por qué puerta escribe
cada pantalla, y eso solo lo sabe la pantalla.

| Sección | Necesita | Por qué |
|---|---|---|
| HOME | abierto | equipo en vivo, detección y cap, todo por memoria |
| RANDOMIZADOR | cerrado | instalar y quitar tocan la carpeta de mods; generar da igual |
| GACHA | cerrado | el Pokémon se escribe en el fichero de partida |
| TIENDA | abierto | escribe en la mochila del juego en marcha |
| LOGROS | da igual | solo lee el save; enseña lo último guardado |
| VISOR POKÉMON | cerrado | mirar se puede siempre, pero editar EV y el wonder trade escriben el save |
| POKE PASTE | da igual | solo lee el save |
| MISCELÁNEA | abierto | premios y herramientas escriben en la mochila |
| RULETA | cerrado | equipo, mochila y cajas, todo por el save |

Dos decisiones que no son de estilo. El VISOR va marcado **cerrado** aunque leer funcione siempre,
porque la insignia tiene que fallar hacia el lado que no rompe nada: quien entra a hacer un wonder
trade con el juego abierto necesita saberlo antes, y quien solo entra a mirar no pierde nada por
verlo. Y «da igual» va **apagado** en vez de en un tercer color llamativo, porque es la respuesta
«no te preocupes»: gritarla le quitaría fuerza a las dos que sí ahorran un fallo.

---

## 67. Empezar de cero borra de verdad (2026-08-27)

El botón del §66 creaba una run nueva y dejaba la vieja guardada. El jugador pidió lo otro: que
**borre la run entera y la partida**. Se hace, y lo que cambia respecto de la primera versión no es
solo el alcance sino qué hay que garantizar.

### Dos cosas, en dos sitios, con dos riesgos distintos

La **run** son tres almacenes: la carpeta `Saves/<id>/` con su `run.json`, las filas de `events` y
las de `pokemon`. Hay un test por cada uno, separados a propósito: un borrado que se olvidase de una
tabla dejaría la aplicación con aspecto limpio y filas huérfanas en la base para siempre. Y otro test
que exige que una **segunda run no sea daño colateral**, porque las tres consultas van por `run_id` y
la carpeta va por el id, no por «lo que haya en Saves».

La **partida** es un fichero de otro programa. `SaveEraser` la copia antes y **sin copia no borra**:
es la misma norma que cumple cada escritura del proyecto, aplicada al caso en que lo que se escribe
es la nada. La copia va a `Saves/backup/borrada-<fecha>/` y el mensaje dice dónde, porque «empezar de
cero» y «perder una partida que querías» se parecen mucho hasta el segundo siguiente.

Deliberadamente estrecho: vacía la carpeta del título y **se niega** si esa carpeta no es la de Ultra
Luna, en cuyo caso borra solo el fichero `main` que localizó. Un «empezar de cero» que llegue más
lejos que el juego al que le apuntaron es como alguien pierde un save del que nadie estaba hablando.
Y relee: que `File.Delete` no lanzara no es que el fichero se haya ido.

### El orden lo decide qué puede fallar

Primero la partida, después la run. La partida es la que **puede negarse** —Azahar puede tenerla
abierta—, así que fallar ahí lo deja todo como estaba. Al revés, el jugador se quedaría sin run y con
la partida vieja intacta, que es el peor de los dos estados a medias.

### El único DELETE del registro encadenado, y por qué no es una grieta

`IEventStore` decía, y sigue diciendo, que no hay Update ni Delete: un error se corrige añadiendo un
evento compensatorio. `DeleteRunAsync` no es una excepción a eso, y la diferencia es justo la que
importa: **no existe forma de borrar un evento**. La cláusula es `WHERE run_id` y no hay sobrecarga
que acepte un id de evento. Borrar uno suelto dejaría una cadena cuyos hashes siguen cuadrando
alrededor del hueco, que es lo único que esa tabla existe para impedir; borrar la run entera no deja
nada que falsificar. **Una run descartada no es una run corregida.**

Por lo mismo no se escribe ningún evento de despedida: iría a la cadena que se está borrando.
Lo que queda es la línea del log y la copia de la partida con su fecha.

### Dos confirmaciones, y la segunda corta

La primera enumera lo que se pierde y lo que no —la randomización instalada sigue puesta—. La segunda
es una frase. Un aviso largo se lee en diagonal; una pregunta seca detrás, no. Y el botón va en rojo,
que es parte del aviso: llegar aquí por costumbre no debería ser fácil.

### El borrado dejó la partida «dañada» (2026-08-28)

El §67 se estrenó fallando, y el fallo tiene una forma que conviene recordar: **borró de menos y eso
fue peor que borrar de más**.

Azahar no guarda la partida como un fichero suelto: guarda un **archivo de datos**, que es la carpeta
`…/001b5100/data/00000001/` con `main` dentro **y un hermano `00000001.metadata`** al lado. Esos 16
bytes -`00 00 04 00`, y después tres unos- son el `ArchiveFormatInfo`: tamaño reservado, y cuántos
directorios y ficheros declara. Es como el emulador anota que ese archivo **está formateado**.

`SaveEraser` borró los ficheros de dentro de la carpeta y dejó la metadata, porque vive un nivel más
arriba. Resultado: un archivo que dice «aquí hay una partida» sobre una carpeta vacía. El juego lo
abre, lo cree, busca `main`, no está — y eso es exactamente **«los datos de guardado están dañados»**.
Y es el peor de los tres estados posibles, porque desde ahí el jugador **tampoco puede empezar una
partida nueva**: no es «no se borró del todo», es un estado del que no se sale jugando.

La lección general, que ya había aparecido en el §53 y en el §55 con otra ropa: **una estructura se
borra entera o no se toca**. Media estructura no es una versión suave de la operación, es un estado
que nadie diseñó y que nadie sabe leer. Y la concreta: cuando algo se guarda como «archivo» -carpeta
más metadatos de formato- el objeto real no es el fichero, es el archivo.

Ahora `PlayerSave.FindArchive()` localiza la carpeta **aunque no haya `main` dentro**, que es
justamente el estado roto, y el borrador se lleva las tres cosas: ficheros, metadata y carpeta. Con
eso el juego ve un archivo sin formatear, que es lo mismo que ve una consola nueva, y formatea al
guardar. La metadata **también se copia** antes de borrarla: sin ella la copia no se podría devolver
a su sitio, porque el juego no reconocería un archivo que nadie declara formateado.

Cuatro pruebas, y la que importa es la de la regresión: después de borrar, `00000001.metadata` no
existe. Hay otra que parte del estado roto -archivo sin `main`- y exige que el borrador sepa
terminar el trabajo, porque quien deja a alguien en un estado inconsistente tiene que saber sacarlo
de él.

---

## 68. Nada de esto contaba porque nadie había pulsado un botón (2026-08-28)

El jugador reportó tres cosas: las Super Balls no llegaban, los muertos no se volvían Shedinja y no
se restaban puntos. Dos de las tres eran el mismo fallo, y no estaba en el código sino en la forma.

### La medida

`Probe --run` sobre la run nueva:

```
POKÉMON REGISTRADOS: 0
  con PID: 0     sin PID: 0
EVENTOS: 2   (RunCreated, RomRandomized)
MUERTES REGISTRADAS: 0
```

`GameWatcher.InspectAsync` decide que alguien ha muerto emparejando el equipo vivo contra **lo
registrado en la run, por PID**. Con cero registrados la lista de caídos es vacía siempre, y de ahí
cuelga todo lo demás: sin muerte no hay `PokemonDied`, sin `PokemonDied` no hay −25, y sin muerte no
se escribe el Shedinja. La cadena estaba entera. Lo que faltaba era el primer eslabón.

Y el primer eslabón era **un botón**. El vigilante anunciaba los Pokémon sin registrar en HOME y
esperaba a que alguien pulsara REGISTRAR. El §56 ya avisaba de que «detectable no es detectado»;
esta es la primera vez que se ve en una partida de verdad, y lo que se ve es peor que un fallo:
el sistema de penalizaciones queda **inerte y en silencio**. Una regla que solo se aplica cuando
alguien se acuerda de pulsar algo no es una regla.

### Registrar no es arbitrar

Ahora se registra solo, pero **no se inventa lo que no se sabe**. De memoria se leen especie, nivel,
mote, variocolor, PID y el lugar de encuentro que el propio Pokémon lleva escrito. Lo que no se lee
es la **ball**, y sin ella no se puede decir el tipo de encuentro — que no es decorativo: decide si
la captura **gasta el encuentro de la zona**, y dispara las reglas de regalo y de estático.

Poner «salvaje» por defecto habría gastado zonas que el jugador no usó, y sin fallar nunca. Así que
hay un `EncounterType.Unknown` que **no gasta zona y no dispara reglas especiales**. El Pokémon
existe, que es lo que hacía falta para que pueda morirse; lo que no hace el registro automático es
fingir que ha arbitrado la captura. Dos pruebas lo fijan: no gasta la zona, y la deja libre para la
captura que el jugador sí declare.

Lo que **no** hace: forzar. Una captura que una regla bloquea se queda sin registrar y vuelve al
aviso de HOME, porque saltarse una regla es decisión del jugador y de nadie más. Y el evento
`PokemonCaught` guarda ahora **quién lo decidió** (`EventSource.AutoDetect` contra `Player`), porque
«el vigilante vio aparecer un Pokémon» y «una persona miró el encuentro y dijo lo que era» no son la
misma afirmación y el historial tiene que poder distinguirlas después.

### Y el premio que era deberes

Las Super Balls estaban bien: eran un premio con su botón en MISCELÁNEA. Pero un premio que tiene
que llegar **junto** a las Poké Balls que te da Tilo y que en realidad hay que ir a buscar a otra
pantalla no es un premio, son deberes. `Data/rewards.json` gana `"automatico": true` y el enlace con
el juego lo entrega en cuanto está ganado.

No relaja ninguna garantía —mismas condiciones, mismo cotejo del nombre contra la tabla del
cartucho, misma escritura con relectura, mismo `RewardClaimed` que hace que «una vez» sea una vez—:
lo único que cambia es quién pulsa. Va **limitado a una consulta cada 30 s**, y no por pulcritud:
saber si está ganado lee el fichero de partida entero con PKHeX, y hacerlo cada segundo al lado del
sondeo del equipo sería un coste real por una respuesta que cambia dos veces por run.

Con una condición: **se dice**. HOME enseña en verde lo último que PermaLocke ha hecho por su
cuenta, porque diez Super Balls que aparecen en la mochila sin que nadie las pida tienen que venir
con una frase que explique de dónde salen. Y la tarjeta del premio dice `LLEGA SOLO` en vez de
`RECOGER`, que sigue funcionando si se pulsa pero ya no parece obligatorio.

Hay prueba de que el fichero que se reparte lleva de verdad la marca: una bandera que el lector
ignorase en silencio apagaría la función sin que fallara nada, que es exactamente la forma del bug
del que sale toda esta sección.

### Y en vivo, como el cap (2026-08-28)

El Shedinja y las muertes ya funcionaban en vivo, porque van por memoria. Lo que seguía pidiendo
guardar a mano eran otras dos cosas, y las dos por el mismo motivo de fondo: **preguntarle al
fichero de partida algo que la memoria sabe antes**.

**La condición del premio.** «¿Lleva Poké Balls?» se leía de `IGameRecords`, que lee el save, o sea
que las Super Balls no llegaban hasta que el jugador guardaba. Pero la mochila viva se lee desde el
§22, así que ahora se pregunta **primero al juego en marcha y solo después al fichero**. El orden es
todo el arreglo: el save dice lo que había la última vez que se guardó, y la memoria dice lo que hay.

El respaldo no es adorno: con Azahar cerrado la mochila viva no puede contestar, y la pantalla tiene
que poder seguir diciendo si un premio está ganado. Lo que no puede hacer es contestar **mal**, así
que los dos extremos fallan hacia «no ganado». Hay una trampa de contrato que quedó escrita en el
propio código y en el doble del test: `CarriedAllAsync` devuelve **una entrada por id preguntado,
aunque valga cero**, cuando el juego responde, y **el diccionario vacío** cuando no. Por eso vacío
significa «no se sabe» y no «no lleva ninguno», y por eso solo se pregunta con una lista no vacía.

De paso, la comprobación automática dejó de pasar por `GetStatusAsync`: se llama en bucle, así que
mira primero el historial y, si los premios automáticos ya están recogidos, **no toca ni la partida
ni la mochila**. Un premio de una vez no se des-recoge nunca. Con eso el intervalo baja de 30 s a 5.

**Y lo que la app hacía sin que se viera.** Una muerte se registraba y se cobraba bien, y HOME
seguía enseñando el saldo viejo hasta que el jugador salía de la sección y volvía a entrar. El
evento `TeamWiped` llevaba desde el §36 lanzándose **sin que nadie lo escuchara**. Ahora el monitor
lanza `RunDataChanged` una vez por ciclo y **solo si de verdad cambió algo** —registro, muerte,
equipo caído, premio—, y HOME se refresca con eso. Trabajo hecho y sin verse se lee igual que
trabajo no hecho.

### Un premio puede dar tiradas, no solo objetos (2026-08-28)

`Data/rewards.json` gana `tiradasGratis`, una lista de banners con **una entrada por tirada**. El de
las primeras Poké Balls pasa a dar diez Super Balls **y una tirada en DECENTE**.

No se guarda en ningún sitio: se escribe en el `credito` del evento `RewardClaimed` y `CreditService`
lo cuenta de vuelta desde el historial, exactamente como las tiradas de la ruleta. Y de paso se
generalizó lo que ya existía: `AddGrantedRollsAsync` mira **el campo `credito` y no el tipo de
evento**, así que una tirada de ruleta, un premio y un ajuste a mano pagan igual y el servicio no
tiene que aprenderse cada fuente nueva.

Consecuencia deliberada, que es el §64 otra vez: **añadir una tirada a un premio no le paga nada a
quien ya lo recogió**, porque su evento no la lleva escrita. Correcto, y por eso hace falta una
forma de decir «dame una» en voz alta en vez de volver la regla retroactiva por lo bajo:
`Probe --credito <banner> [motivo]` escribe un `AdminAdjustment` con su `credito` y su porqué. El
historial dice que se concedió a mano, cuándo y por qué; no hay ningún número editado en ninguna
parte.

### La trampa del `with` en un record

El test de esto falló, y lo que fallaba era el código. `Reward.Credits` estaba escrito como
`public IReadOnlyList<string> Credits { get; } = Credit ?? [];`, y `reward with { Credit = ["decente"] }`
**no vuelve a ejecutar los inicializadores del cuerpo**: el constructor de copia copia los campos y
luego aplica los valores nuevos, así que `Credit` quedaba puesto y `Credits` seguía siendo la lista
vacía de la copia. La propiedad derivada mentía en silencio.

`HeldItems` tenía exactamente la misma forma y nadie lo había notado porque ningún test usaba
`with { Held = ... }`. Las dos son ahora propiedades **calculadas** (`=> Held ?? []`). La regla que
queda: en un record, una propiedad derivada de un parámetro posicional se calcula, no se inicializa,
o `with` la deja desincronizada del parámetro del que dice depender.

## 69. Los iniciales, primera etapa de una línea de tres (2026-08-28)

La competición pide que un inicial sea algo que **crece**: dos evoluciones por delante, tres formas
en total. No una lista de especies mantenida a mano, sino una propiedad que se lee del cartucho.

### La tabla de evoluciones, leída en vez de escrita

`a/0/1/4` tiene un subfichero por especie con entradas de ocho bytes: método en un `u16` en 0 y
especie destino en un `u16` en 4; método cero es un hueco. Es exactamente el formato que el módulo
de datos parchea, leído aquí. `EvolutionTable` lo convierte en la única pregunta que interesa:
**¿tiene esta especie dos evoluciones por delante?** No modela métodos, ni niveles, ni objetos.

Medido contra la ROM real con `RomTool evoluciones`:

```
Primeras etapas (nadie evoluciona en ellas): 589
  lineas de 1 etapa(s): 298
  lineas de 2 etapa(s): 197
  lineas de 3 etapa(s):  94
```

Trece anclas comprobadas a mano, y las trece cuadran: Bulbasaur, Charmander, Squirtle, los tres del
cartucho, Caterpie y Treecko dentro; Pikachu fuera **porque evoluciona de Pichu** y por tanto no es
primera etapa; Eevee fuera porque sus muchas ramas son de un solo paso; Magikarp fuera por tener dos
etapas; Ditto y Articuno fuera por no evolucionar.

De 94 a las 92 que el randomizador usa hay una diferencia que conviene tener explicada y no
redondeada: **una** está por encima de `maxSpecies` (la 924, una forma) y **Cosmog** está en
`bannedSpecies`. 94 − 1 − 1 = 92, y el informe de la generación dice ese número.

### Filtrar el saco, no repetir la tirada

`SpeciesPool.Where` devuelve un saco más estrecho con las mismas reglas de reparto. Es
deliberadamente eso y no «tira hasta que salga una que valga»: con un filtro no hay un límite de
intentos del que caerse, que es como una restricción deja de serlo justo cuando más cuesta
cumplirla. Y el respaldo de la banda de fuerza —que se ensancha y acaba tirando de todo el saco—
sigue dentro del saco estrecho, con test propio.

### Dos fallos que cazaron los tests, no la ROM

`EvolutionTable.FromTargets`, la puerta que existe para poder probar la lógica sin cartucho, **no
limpiaba las autoevoluciones** como sí hacía `Read`: una especie que evoluciona en sí misma contaba
como una etapa más. Y la profundidad memorizaba resultados obtenidos **cortando un ciclo**, que
dependen del camino de llegada, así que la tabla podía contestar distinto según el orden en que se
le preguntara. Ahora una profundidad que vino de romper un ciclo no se guarda, y hay un test que
pregunta hacia delante y hacia atrás y exige las mismas respuestas.

El cartucho no tiene ciclos, pero esta tabla se lee de un fichero que un randomizador escribe, y
apuntar una evolución de vuelta a su propia línea es precisamente lo que ese randomizador hace.

### Verificado generando

Con seed 20260828 salen **Duskull, Fletchling y Nidoran♂** — 355, 661 y 32 leídos del `a/1/5/9`
generado—, las tres primeras etapas de líneas de tres.

Aviso que va en el informe y no solo aquí: los iniciales se eligen **antes** de que el módulo de
datos toque las líneas evolutivas, así que con `randomizeEvolutions` en true la garantía es sobre
las familias del cartucho. Con la configuración actual está apagado y no hay diferencia.

## 70. Las evoluciones que a solas no existen (2026-08-28)

Un Nuzlocke se juega solo, así que una evolución por intercambio es una evolución que **no existe**:
Kadabra, Machoke, Haunter y veintisiete más se quedan a medias para siempre. Y con
`randomizeLearnsets` en true, las nueve que esperan un movimiento concreto tampoco llegan, porque ese
movimiento puede no aprenderse nunca.

Qué cambiar y por qué sale de la lista de Universal Pokémon Randomizer ZX para Sol/Luna/USUM que
pasó el jugador. Los números de método, **no**: esos se midieron.

### Los métodos, sacados del cartucho

`RomTool evo-dump` vuelca `a/0/1/4` agrupado por método, y con eso cada número queda anclado a un
caso reconocible:

| Método | Qué es | Cómo se sabe |
|---|---|---|
| 5 | intercambio a secas | Kadabra → Alakazam, sin argumento |
| 6 | intercambio con objeto | Poliwhirl → Politoed, arg **221** = Roca del Rey |
| 7 | intercambio con la otra | las **dos** que se cambian entre sí |
| 8 | usar piedra | Pikachu → Raichu, arg 83 |
| 19 | subir de nivel con objeto, **de día** | Happiny → Chansey con la Piedra Oval |
| 20 | lo mismo de noche | Gligar → Gliscor con el Colmillo Agudo |
| 22 | subir de nivel con otra en el equipo | Mantyke → Mantine con Remoraid |

De ahí las conversiones: 5 → 4 (nivel 37), 6 → 19 conservando el objeto, 7 → 22 con la otra como
argumento, y las de movimiento a 4 con su nivel. **Solo existen las variantes de día y de noche**
para «subir de nivel con objeto», así que esas evoluciones piden ahora que sea de día. Es una
restricción real y va dicha.

### Por método, no por lista de especies

La diferencia no es de estilo. La lista de referencia enumera 27 casos; el cartucho tiene **30**
entradas de intercambio, porque las **tres tallas de Calabruja** y el **Geodude de Alola** son
entradas propias que esa lista no nombra. Buscar por método las coge todas.

Igual con el emparejamiento de Karrablast y Shelmet: no está escrito en el código sino **deducido**
—el cartucho tiene exactamente dos entradas de método 7 y cada una es la pareja de la otra—. Con
cualquier otro número el emparejamiento deja de ser obvio, así que no se adivina: esas entradas se
quedan como estaban y el paso de verificación las cuenta como imposibles en vez de dejarlas pasar.

### La única excepción hecha a mano

Slowking pasa a **Piedra Agua** (objeto 84, comprobado contra la tabla del cartucho) en vez de a
«subir de nivel con la Roca del Rey». Es la excepción de la lista de referencia y tiene motivo: en el
cartucho la entrada de Slowking lleva **nivel 37**, que es exactamente el nivel al que Slowpoke ya se
convierte en Slowbro. Dejarla en el método 19 pondría dos evoluciones sobre disparadores solapados y
haría que cuál te toca dependa del orden de los huecos. Una piedra es un disparador aparte.

### Verificado sobre el mod generado

Generando con seed 20260829 y **releyendo el fichero con el volcador**, no el informe:

```
metodo  4:  266 -> 287   (+21: 12 intercambios + 9 movimientos)
metodo  8:   43 ->  44   (+1: Slowking)
metodo 19:    1 ->  16   (+15: los 16 con objeto menos Slowking)
metodo 22:    1 ->   3   (+2: Karrablast y Shelmet)
metodos 5, 6, 7 y 21: desaparecen
```

Cada cuenta cuadra con la anterior. Y cuatro comprobaciones puntuales: Slowpoke → 199 por método 8
con arg 84; Karrablast → 589 por método 22 con arg **616** y Shelmet → 617 con arg **588**, cruzados;
Kadabra → 65 a nivel 37; y Geodude-Alola (925) → Golem-Alola (76, forma 1) a nivel 37, que es una de
las cuatro que la lista no enumera.

### Orden

Va **después** del módulo de datos de Pokémon, a propósito: ese decide **a quién** evoluciona cada
uno y esto decide **cómo**. Al revés, una evolución recién redirigida se quedaría con su método de
intercambio intacto. Por lo mismo, la tabla de movimiento está indexada por especie **y** destino: si
las líneas se randomizan y Lickitung ya no apunta a Lickilicky, darle el nivel 33 de Lickilicky sería
inventar un número para una pareja que nadie ha medido, así que se deja como está.

### La insignia era una etiqueta, no un indicador (2026-08-28)

El jugador wipeó y ningún Pokémon se convirtió en Shedinja. La medida, antes de tocar nada:

```
grep "Muerte detectada"  -> una sola en todo el dia, y esa SI se transformo
grep "Equipo caido"      -> ninguna
grep "Conectado al juego" -> la ultima a las 02:17:12
```

O sea que durante esa partida **PermaLocke no estaba mirando**. El vigilante lee el equipo de la
memoria del emulador; sin enlace no hay equipo que leer, no hay muertes que detectar y no hay nada
que escribir. La cadena entera estaba correcta y desconectada.

Lo que falla ahí no es la detección: es que **se pudo jugar una sesión entera sin enterarse**. La
insignia del §66 decía «JUEGO ABIERTO» en todas las pantallas que lo necesitan, estuviera abierto o
no, porque salía solo de lo que la sección declara. Un requisito que nunca se pone en rojo es
decoración.

Ahora el shell escucha `SnapshotChanged` y la insignia dice si el requisito **se cumple**:

| Sección necesita | El emulador contesta | Insignia |
|---|---|---|
| abierto | sí | verde, JUEGO CONECTADO |
| abierto | no | **rojo, SIN CONEXIÓN** |
| cerrado | sí | **rojo, CIERRA EL JUEGO** |
| cerrado | no | ámbar, JUEGO CERRADO |
| da igual | — | apagado |

El único caso que no se pinta de verde es «necesita cerrado y nadie contesta», y es a propósito: que
el RPC no conteste **no demuestra** que el juego esté cerrado —Azahar puede estar abierto con el
servidor apagado— y un verde ahí sería una suposición. Verde y rojo son para lo medido; el ámbar
sigue diciendo qué hace falta.

### Lo que sigue sin poder afirmarse del wipe

Que la cadena esté bien no es haberla visto funcionar. La única muerte observada nunca fue un wipe, y
además llegó **con el Pokémon ya a 0 PS** cuando el vigilante lo vio por primera vez, así que
tampoco prueba que la lectura del equipo funcione **durante** un combate.

Y un wipe se cura solo: al despertar en el Centro Pokémon el juego restaura el equipo, de modo que
la ventana en la que los seis se leen a 0 PS es la del final del combate y el fundido, no un estado
que se quede ahí como el de una muerte suelta. Con el sondeo a 1 Hz debería bastar, pero **debería
no es se comprobó**. Queda por medir con el enlace levantado y un combate perdido delante.

## 71. Poké Balls en el mostrador, y el equipo entra al wonder trade (2026-08-28)

### Los seis curativos de estado pasan a ser Poké Balls

Las ocho tiendas normales de los Centros —las que van creciendo según superas pruebas— seguían
intactas a propósito: son donde un Nuzlocke compra balls. Ahora cambian una cosa: los **seis
curativos de estado** se venden como Poké Balls.

Los ids salieron de la tabla del cartucho, no de la memoria: **17 Poción, 18 Antídoto, 19
Antiquemar, 20 Antihielo, 21 Despertar, 22 Antiparalizador**. Están seguidos, que es por qué «de la
poción hasta el antihielo» y la lista de seis nombran exactamente lo mismo. Lo que **no** entra:
Superpoción, Hiperpoción, Poción Máxima, Restaurar Todo, Revivir y Cura Total, que no estaban en la
lista y siguen a la venta.

El emparejamiento es **por id y no por posición**, porque los seis están en un sitio distinto en cada
una de las ocho tiendas y las últimas venden más cosas. Y cada id viaja con su nombre en
`Data/randomizer.json`, comprobado contra la tabla del cartucho **antes** de escribir: un id que
caiga en otro objeto surte la tienda con otra cosa, el juego funciona perfectamente y nadie se entera
nunca. Un desajuste lanza y no se toca ninguna tienda (§52).

Verificado releyendo el `Shop.cro` generado, con `RomTool shops --gen <ruta>`, que ahora acepta la
ruta del mod: **48 huecos** cambiados, que es 8 tiendas × 6 curativos, y en las ocho quedan las
Poké Balls con el resto del surtido intacto.

### El wonder trade acepta Pokémon del equipo

El §51 dejó el equipo fuera con una razón buena —el equipo es **otro almacén** del save, y un índice
de equipo tratado como caja cae en la **caja 0** y destruye a un Pokémon que nadie eligió—, y el
corte se puso en la puerta del escritor. Ahora en vez de cortarlo se atiende: `SaveBoxSwap` bifurca
igual que hace `SaveEvTrainer` desde el §51, con `GetPartySlotAtIndex` / `SetPartySlotAtIndex`.

Lo que se guarda con test es justo el error que el corte evitaba: entregar al del equipo escribe en
el equipo y **la caja 0 se queda como estaba**. Un fallo ahí no se notaría, porque algo se habría
escrito y el intercambio diría que salió bien.

Aviso que va en el mensaje del resultado, no escondido: un Pokémon que entra al **equipo** llega con
las estadísticas que calcula PKHeX, y la ROM baraja las bases (§51), así que el número puede no
cuadrar hasta que el juego las recalcule. Curarse en un Centro basta. En una caja no pasa porque un
Pokémon en caja no lleva estadísticas de combate.

## 72. Megaevolución temprana: no era el objeto, era un campo con nombre (2026-08-28)

En Ultra Luna la megaevolución no llega hasta después del Alto Mando. La competición la quiere
antes, y la primera hipótesis —darle al jugador la Piedra Activadora— resultó **falsa**, medida:

```
EQUIPO:  1. Swampert Nv.16  lleva: Swampertita (752)
MOCHILA clave: Piedra Activadora (773)
```

Todo puesto, releído, y el botón no salía. **El juego no mira el objeto.** Lo que mira es
`MyStatus.MegaUnlocked`, un booleano del bloque de entrenador, y lo que lo identificó fue su vecino:
`MyStatus.ZMoveUnlocked = True`, ya encendido, porque los movimientos Z sí funcionaban. Un par de
banderas hermanas, una a true y otra a false, explicando exactamente lo que se veía y lo que no.

Encenderlo y ver el botón lo confirmó.

Vale la pena decir cómo se encontró, porque el primer barrido **no lo vio**: se listaron las
propiedades de `SAV7USUM` y ahí no está. Está en un sub-bloque. Buscar solo en la raíz de un objeto
grande es como no buscar.

### Segunda clase de premio

`Data/rewards.json` gana `desbloquea`, una lista de llaves. Hoy hay una. La maquinaria del §60 se
reutiliza entera —ganado por los logros, no recogido por el evento `RewardClaimed`, entregable— y lo
único nuevo es por dónde se escribe.

Y ahí está la restricción que obliga a una regla: los objetos se escriben en la **mochila del juego
en marcha** y esto en el **fichero de partida**, que exige el juego **cerrado**. Un premio con las
dos cosas no se podría recoger en ningún estado del emulador, así que el catálogo lo **rechaza** en
vez de mandar a la pantalla un botón que siempre falla. Con test.

Por lo mismo este premio **no puede ser automático**: la entrega automática corre desde el enlace con
el juego, que por definición solo existe con el juego abierto.

Tres guardas, las de siempre: se niega con el juego cargado, copia la partida entera antes, y
**relee el flag del fichero** antes de dar el premio por recogido. Y una llave que el escritor no
conozca se rechaza en vez de ignorarse: una errata en el JSON que no hiciera nada dejaría un premio
que se cobra y no desbloquea.

Un test guarda el caso que importa: **con el juego abierto no se escribe nada y no se recoge nada**,
así que el premio sigue ahí para después. Un premio de una vez quemado porque el emulador estaba
abierto sería uno que el jugador no recibe nunca.

### Y de paso, a qué nivel llega cada mega

`RomTool megas` cruza las 48 megaevoluciones con la tabla de evoluciones y dice a qué nivel se llega
a la especie que puede megaevolucionar. Con eso la elección de la 6ª prueba deja de ser una
corazonada:

| Cap | Megas alcanzables subiendo de nivel |
|---|---|
| 24 | 3 |
| 34 | 10 |
| **40** (7ª prueba) | **22** |
| 54 | 27 |

Más veinte que no dependen del nivel. Con cap 40 está la mayoría del catálogo, y solo seis quedan
fuera: Aggron y Glalie a 42, Metagross a 45, Garchomp a 48, Salamence a 50 y Tyranitar a 55.

## 73. Los jefes con mega: es una forma, no una especie (2026-08-28)

Con la megaevolución del jugador abierta desde la 6ª prueba (§72), los combates importantes tenían
que responder o la curva de dificultad se venía abajo: una mega son unos +100 de estadísticas base.

La pregunta del jugador —que venía de haber visto en otro randomizador un jefe que salía **ya
megaevolucionado**, como un Pokémon normal— resultó ser la buena.

### La medida que lo decide

La tabla `a/0/1/5` es `especie → (forma, piedra)`, y el primer campo **es el número de forma**:

```
especie   6 hueco 0  campo0=1  -> Charizardita X    <- forma 1 = Mega X
especie   6 hueco 1  campo0=2  -> Charizardita Y    <- forma 2 = Mega Y
especie 150 hueco 0  campo0=1  -> Mewtwoita X
especie 150 hueco 1  campo0=2  -> Mewtwoita Y
```

Las dos especies con dos megas tienen dos entradas, forma 1 y forma 2; las otras 44, una con forma 1.
Y la tabla de entrenadores ya tenía **campo de forma** en el offset `0x12`.

Así que un jefe con mega es **especie + forma**. Un byte, y ninguna dependencia de la IA.

Lo que se pierde va dicho: **no megaevoluciona, llega mega**. No hay animación ni «el rival está
megaevolucionando», porque nada evoluciona. La alternativa —darle la piedra y confiar en la IA—
depende de comportamiento sin medir; ésta no depende de nada.

### El corte: no se puede gatear por prueba, se gatea por fuerza

La ROM se randomiza **una vez**, antes de empezar, así que no puede saber que el jugador lleva seis
pruebas. Lo que sí puede es mirar **cómo de fuerte es el combate**, que es lo mismo visto del otro
lado. El suelo es **nivel 33 del cartucho**: la 7ª prueba tiene cap 40 y el §48 midió que un cap es
su jefe subido un quinto.

El umbral se sube con la **misma cuenta** que subió los equipos —`TrainerRandomizer.Raise`— así que
se mueve exactamente igual que ellos. Comparar un umbral vanilla contra niveles ya subidos dejaría
entrar combates de antes de la séptima.

Medido sobre el cartucho: **62 de los 96** combates importantes pasan el corte.

### Sustituye, no añade

Lo que pidió la competición: un jefe con cinco sigue teniendo cinco y **uno de ellos** es la mega.
Añadir habría sido una segunda ración del Pokémon extra del rol (§47), que es otra cosa. Va después
de ese módulo a propósito, para que «uno de los cinco» sea de verdad uno de los cinco.

Como máximo **uno por combate**, y las candidatas son las 46 del cartucho menos Rayquaza —cuya mega
va por movimiento y no tiene piedra— y menos los legendarios de `bannedSpecies`: quedan **41**. Un
Mega Mewtwo de regalo a mitad de partida es justo lo que esa lista existe para evitar.

### Verificado releyendo, y dos fallos por el camino

Generando con la seed 20260831 y el rol real, y **releyendo el `trpoke` generado con otro programa**:
62 combates con mega, **cero con más de una**, cero formas que la especie no tenga, y el más temprano
a nivel 41. Los números cierran con el informe y entre sí: 62 + 34 fuera = 96.

Dos fallos que costaron una vuelta cada uno, los dos en la comprobación y no en el módulo:

- El bucle de verificación admitía un hueco que **no cabía entero** (`< Length` en vez de
  `(slot+1)*Entry <= Length`), y el campo de forma, que va en 0x12, se salía del array.
- La comprobación independiente cruzaba la tabla de entrenadores **vanilla** con el `trpoke` del mod.
  El módulo del Pokémon extra cambia las cuentas, así que ninguna cuadraba y **se saltaban en
  silencio**: daba 9 megas en vez de 62. La tabla y el equipo tienen que salir del mismo sitio.

---

## 74. La interfaz vuelve a hacerse, esta vez con identidad (2026-09-01)

El §50 ya había ordenado la presentación. Lo de hoy es distinto: el jugador pidió que la aplicación
**pareciera un producto de videojuego y no un tema aplicado por encima**, y para eso hubo que medir
primero qué la hacía parecer una plantilla.

### El diagnóstico, contado en números

| | Antes |
|---|---|
| Tamaños de letra | **25 distintos** en 132 usos, y los cuatro tokens `FontSize*` sin usar por nadie |
| Padding | **50 valores** distintos |
| Radios | **10** a mano, ignorando los tres tokens |
| Colores fuera del tema | **37**, con una subpaleta entera dentro de `ShopView.xaml` |
| Contenedores con borde | **29** usos de `Inset`: todo era una tarjeta |
| Navegación | Una lista de palabras, sin un solo icono |

No era «falta gusto»: era que **no había sistema**, había veinticinco.

### La regla del color

El gris hace el trabajo y **un único acento** significa valor y acción. Si algo es del color de
acento, o es dinero o se pulsa. Los semánticos solo aparecen como distintivos pequeños con fondo
teñido, nunca como relleno grande, y la rampa de rareza se queda en el gacha y la colección.

El acento empezó siendo **latón** —el color de la recompensa en cualquier RPG, y aquí además la
moneda— y el jugador lo cambió a **violeta**, que es el del ultraespacio de Ultra Luna. Con el
cambio se tiñeron también los neutros: un gris exacto debajo de un violeta se lee sucio y verdoso.

Y salió una colisión que había que resolver: **el tier 4 de la rareza ERA violeta**. Con el acento
violeta habrían sido el mismo color diciendo dos cosas distintas, así que el tier 4 pasó a magenta.
El tier 5 se queda en oro, que todo el mundo lee como legendario.

### Cuatro materiales en vez de una card para todo

```
Section   NADA. Un rótulo y aire. Es el material POR DEFECTO.
Inset     Un plano un escalón por encima del fondo, SIN BORDE.
Well      Hundido. Listas, historiales, inventarios.
Card      Con borde. SOLO para objetos que se cogen.
```

La palanca que lo cambió todo: `Inset` se usaba en **29 sitios**, así que quitarle el marco en el
tema desencajonó la aplicación entera **sin tocar una sola vista**.

La elevación la da el escalón de gris. **No hay ni una sombra** en todo el tema.

### La tipografía, y el fallo que costó una iteración

Dos familias con papeles distintos: **Bahnschrift** para cifras y rótulos, **Segoe** para frases,
**Consolas** para lo que se alinea en columna. Bahnschrift la trae Windows 10 en adelante, así que
viaja en la carpeta de reparto sin licencia ni fichero.

El fallo: se eligió **Bahnschrift SemiBold Condensed**. Una condensada a 10 px se estrecha y se
cierra, y no se vio mirando el contador de puntos —donde queda bien— sino en una etiqueta de once
caracteres. Pasó a SemiBold normal y la escala subió un escalón: 11 / 12,5 / 13,5 / 16 / 19 / 25 / 36.

**Cifras tabulares en todos los números**, que es lo que impide que algo baile al pasar de 9 a 10.

### Iconografía propia

Catorce geometrías dibujadas aquí, lienzo de 24×24, **silueta rellena y no contorno** —a 18 px un
trazo de 1,5 px se emborrona en pantallas sin escalado entero—, y peso óptico parejo para que
ninguno pese más que los demás en una lista. Ni un emoji.

### Las tres trampas de WPF de esta tanda

1. **`Style` puesto dos veces** en el mismo `TextBlock` —como atributo y como `<TextBlock.Style>`—
   no es un aviso, es un error de compilación. Cayó **cuatro veces** en la misma sesión.
2. **Los comentarios XML no admiten `--`**, así que un separador de guiones dentro de un comentario
   rompe el XAML y también el `.csproj`.
3. Un estilo con `x:Key` **no hereda del implícito** salvo `BasedOn="{StaticResource {x:Type X}}"`.

### Lo que se verificó, y cómo

Recorriendo las nueve pantallas por **automatización de interfaz** y mirando cada captura. De ahí
salió un fallo de método que conviene recordar: **`PrintWindow` devuelve el dibujo anterior si la
ventana no está delante y WPF aún no ha repintado**, así que dos capturas del recorrido iban
desfasadas una pantalla. Se corrige con un disparo de calentamiento.

Se conservan las **57 claves** que las vistas ya consumían, así que ninguna pantalla se queda sin
recurso.

---

## 75. La carpeta que se le pasa a otro jugador (2026-09-01)

`tools\publicar.ps1` construye el reparto: un solo exe autocontenido más `Data\` y `Emulator\`.
Quien la recibe no instala .NET ni Azahar; `ROM\`, `Saves\`, `Randomized\`, `Logs\` y `Config\` se
crean solas al arrancar.

### Dos cosas que estaban rotas y no se veían jugando

**`Data\` no se publicaba.** Solo el emulador estaba en el `.csproj`, así que la copia publicada
arrancaba con un `Data\` vacío y media aplicación no encontraba su fichero. Ahora se copian los
catorce `.json` al publicar, y **nunca `Data\sprites\`**, que son del cartucho de cada uno.

**El empaquetado de fichero único se tragaba el emulador.** Con
`IncludeNativeLibrariesForSelfExtract`, `azahar.exe` y sus DLL entraban en el bundle: el exe pasaba
de 162 MB a **267 MB** y `Emulator\` se quedaba con **tres ficheros**, así que la aplicación buscaba
`Emulator\azahar.exe` a su lado y no había nada.

Eso **solo habría fallado en el ordenador de quien la recibiera**. `ExcludeFromSingleFile` lo deja
fuera.

### La norma del script

Comprueba **lo que acaba de escribir**, no lo que pretendía escribir: el exe, `azahar.exe`, los
catorce json, la guía, y que no se hayan colado ni sprites, ni `Saves`, ni `ROM`. Si algo falta,
lanza en vez de dejar una carpeta a medias.

---

## 76. La run no se copiaba nunca (2026-09-01)

**319 copias de la partida. Cero de la run.**

PermaLocke respaldaba religiosamente el fichero de **otro programa** —la partida de Ultra Luna—
antes de cada escritura, y no respaldaba **su propia base de datos** ni una vez. Dentro viven la
cadena de eventos firmada por hash, los puntos y los 157 Pokémon.

`RunBackup` copia al arrancar y conserva las diez últimas en `Saves/backup/run/`.

### Las tres decisiones

**Va en `App.OnStartup`, antes de que nada abra la base de datos**, y no dentro de un servicio. Es
el único momento en el que el fichero está garantizadamente en reposo: copiar un SQLite que otro
está escribiendo puede capturar una página a medias, y **una copia que quizá esté corrupta es peor
que ninguna, porque en ella se confía**.

**No lanza nunca.** Un arranque no se detiene porque una copia no salga; quien abre la aplicación
viene a jugar. Lo que pasó queda en el log.

**La rotación ordena por NOMBRE, no por fecha de fichero.** Es lo único que sobrevive a copiar,
restaurar o sincronizar la carpeta —cualquiera de esas cosas reescribe las fechas y el recortador
borraría las equivocadas—. Y solo mira los ficheros que escribió ella: una copia hecha a mano es de
quien la hizo.

Siete tests, incluido que **dos arranques en el mismo segundo no se pisen**, que costaría una de las
diez en silencio.

---

## 77. El mantenimiento sale de la terminal (2026-09-01)

`Probe` tiene **62 comandos** y la carpeta que se le pasa a otro jugador lleva `PermaLocke.exe` y
nada más.

Cuando esta run se desincronizó —28 registros sin PID (§56), 26 sin cerrar tras un wonder trade
(§59)— se arregló desde una línea de comandos. **La run de otro se habría roto igual y se habría
quedado así**, porque no tiene ni la herramienta ni a quién preguntar.

### Qué se expone y qué no

Solo lo que un jugador puede necesitar y no puede hacer de otra forma: auditoría de la run, reparar
PID, cerrar entregados y corregir etapas. El resto de `Probe` es investigación —barridos de memoria,
diffs de banderas, cazas de patrones— y se queda donde está.

La fila que importa de la auditoría es **SIN PID**: el vigilante empareja por PID y por nada más,
así que un registro sin él es invisible y puede caer delante de la aplicación sin que se registre
nada. HOME no distingue eso de «no ha muerto ninguno», y por eso tiene fila y aviso propios.

### Las tres herramientas, en dos pasos

Mirar cuántos, y **solo entonces** se enciende el botón que escribe. Un botón único escribiría en la
partida sin que nadie hubiera visto antes cuánto iba a tocar.

### Lo que costó traerlas, y que salió distinto de lo esperado

**Etapas: no había nada que extraer.** Toda la lógica estaba ya en `ProgressService`
—`DetectedAsync`, `ClearedAsync`, `CurrentCapAsync`, `AdvanceAsync`— y `StageProbe` era cableado y
`Console.WriteLine`.

Lo que sí aporta la pantalla es **separar los tres números**. El tope en vigor es el mayor entre lo
detectado y lo marcado a mano; leídos como uno solo no se distinguen, y una run cuyo contador a mano
se adelantó tiene un tope que nada respalda —ésta llegó a **40 con dos pruebas detectadas** (§58)—.

**Intercambiados: aquí sí había lógica, y solo en el probe.** Sale a `TradedAwayReconciler` en
`Core/Services`, y **el probe pasa a usarlo**: una sola implementación de «cuál se fue», porque dos
acabarían discrepando y solo una sería la que ve el jugador.

El test que importa es el que **se niega a adivinar**: tres Giratina sin PID y dos entregas en el
historial son cuentas que no cuadran, así que no toca ninguno y lo dice. Elegir cuál de los tres se
fue sería inventar historia dentro de un registro encadenado por hash.

---

## 78. ESTADÍSTICAS: la cadena de eventos, leída del revés (2026-09-01)

La aplicación guardaba con cuidado una cadena firmada por hash con todo lo que ha pasado, enseñaba
las últimas veinte líneas en HOME y **tiraba el resto**.

Esta pantalla no calcula nada nuevo: es esa cadena, contada.

### El libro de puntos

Contesta la pregunta que la aplicación no sabía contestar: **por qué tienes los puntos que tienes**.
De dónde salen a la izquierda, en qué se van a la derecha, agrupado por lo que los movió.

Sale del `PointsDelta` que cada evento guardó **en su momento**, no de los precios de hoy. Eso es lo
que lo mantiene honesto cuando se cambia la configuración con una run ya empezada: el libro dice lo
que pasó, no lo que costaría ahora.

### Tres decisiones que no son obvias

**La curva se escala entre su propio mínimo y su máximo, no desde cero.** El saldo **puede quedarse
en negativo** —una penalización no es una compra y no pregunta si puedes pagarla (§36)—, y anclada a
cero una run que se hundió se dibujaría plana.

**Se muestrea a 120 puntos.** Mil segmentos en 700 píxeles son un borrón, y la curva solo está para
la forma.

**La racha sin bajas se cuenta en EVENTOS, y la etiqueta lo dice.** PermaLocke solo ve lo que pasa
con la aplicación abierta, así que contarla en días convertiría una semana sin jugar en una semana
sobreviviendo.

Verificado contra la run real: saldo 75 = ganado 100 menos perdido 25, y 6 capturados + 3 de gacha +
1 de wonder trade = los 10 registrados = 8 en pie + 1 caído + 1 entregado.

---

## 79. COMPETICIÓN: la clasificación, por una carpeta compartida (2026-09-01)

Lo único de la tabla que estaba **sin empezar**, y lo que convierte cinco aplicaciones aisladas en
una competición.

### La decisión de arquitectura

**No hay servidor y no lo va a haber.** Montar una API son hospedaje, cuentas y despliegue para
cinco amigos que nadie va a mantener. El transporte **es una carpeta compartida** —Drive, Dropbox,
OneDrive, red, un pendrive—: cada aplicación escribe **un** fichero con el resumen de su run y lee
los de las demás. De ahí sale gratis exportar y mandarlo por donde sea: es el mismo fichero.

**Se publica un resumen, no la run.** La cadena son miles de filas; lo que una competición necesita
ver es el marcador. Mandarla entera sería mandar tu diario para que lean la última página.

### Lo que no es

Son ficheros escritos por la aplicación de otro, en una carpeta que puede abrir. **No está
verificado y no puede estarlo** sin ese servidor. La regla 3 prohíbe un anti-trampas de mentira, así
que no hay ninguno fingiendo estar: la pantalla lo dice con su distintivo **SIN VERIFICAR**.

Lo que sí se guarda es el **número de eventos y el hash del último**. No demuestran que un número sea
cierto —nada puede— pero hacen **comparables** dos instantáneas de la misma run: un contador que baja
significa que se restauró una copia, y un hash que cambia sin que el contador se mueva significa que
se reescribió el historial.

### Detalles que se pagan si no se piensan

- **Se escribe a un temporal y se mueve.** La carpeta la sincroniza otro programa, y escribir en el
  sitio deja que Drive suba media versión.
- **El fichero va por id de run, no por nombre de jugador.** Dos Ash se pisarían, y una segunda run
  borraría la primera.
- **Los empatados comparten puesto.** Dar el 3 y el 4 a dos runs idénticas sería inventar una
  diferencia.
- **La antigüedad va al lado del número.** Una clasificación donde alguien publicó hace una semana y
  nadie lo dice se lee como alguien que ha dejado de jugar.
- Todo lo que se lee es un fichero ajeno: se analiza a la defensiva, y **uno roto se dice por nombre**
  sin impedir que carguen los demás.

---

## 80. El combate por link entre los jugadores (2026-09-01)

**Verificado: dos Azahar en el mismo PC, combate completo de séptima generación por red local
emulada.** Era la única incógnita, y estaba en el lado que no controlamos: la red de Nintendo cerró
en abril de 2024, así que el modo local es la única vía.

### La randomización es lo que puede romperlo, no el emulador

Un combate por link es una **simulación en paso fijo**: las dos consolas calculan lo mismo y solo se
intercambian las órdenes. Si los mundos no coinciden en lo que el juego lee **durante** el combate,
cada máquina calcula un daño distinto y se separan en el primer turno.

Medido: de los **ocho ficheros** que el mod reemplaza, **siete no se leen en combate**.

| Fichero | Qué es | ¿En combate? |
|---|---|---|
| `a/0/1/3` | aprendizajes por nivel | No — los movimientos van dentro del Pokémon |
| `a/0/1/4` | evoluciones | No |
| **`a/0/1/7`** | **datos de especie** | **SÍ** |
| `a/0/8/3` | salvajes y objetos del suelo | No |
| `a/1/0/6` · `a/1/0/7` | entrenadores | No |
| `a/1/5/9` | estáticos | No |
| `Shop.cro` | tiendas | No |

Y de `a/0/1/7` solo se cambian dos cosas: `shuffleBaseStats` y `randomizeAbilities`. Los tipos ya
están en `false` y **los datos de movimiento (`a/0/1/1`) no se tocan nunca**.

**Conclusión: se puede combatir con cada uno su propio mundo**, siempre que esas dos opciones sean
iguales en todos —lo simple, las dos en `false`—. Salvajes, entrenadores, iniciales, objetos del
suelo, tiendas, el Pokémon extra y las megas de jefe pueden ser distintos sin ningún problema.

### La prueba controlada que lo cierra (2026-09-01)

Tres combates, cambiando **una sola cosa cada vez**:

| | Configuración | Resultado |
|---|---|---|
| 1 | Vanilla en las dos instancias | **funciona** |
| 2 | Dos mundos distintos, `shuffleBaseStats` y `randomizeAbilities` en `true` | **SE DESINCRONIZA** |
| 3 | Los mismos dos mundos, esas dos en `false` | **funciona** |

Entre 2 y 3 **solo cambian esos dos interruptores** y el resultado se da la vuelta. No es
correlación: es la causa.

Y de paso entierra una duda que estuvo sobre la mesa. El §51 midió que un Pokémon de equipo lleva
**sus estadísticas de combate ya calculadas dentro**, y eso hacía pensar que quizá viajaban por el
link y que las estadísticas base locales daban igual. **No dan igual.** Lo que viaje no basta: las
dos consolas acaban leyendo `a/0/1/7` y separándose.

El aviso de COMPETICIÓN (§79) no es una precaución teórica, entonces: está evitando un fallo real
que se ha visto en pantalla.

### El aviso en COMPETICIÓN

Existe para que nadie descubra al mes que no puede pelear con nadie.

**El dato sale del evento `RomRandomized`, no de `randomizer.json`.** El fichero dice cómo estaría
configurada la *próxima* generación; el evento dice con qué se hizo el mundo que se está jugando.
Quien edite el JSON y no regenere sigue jugando el viejo, y es ése el que decide. Mismo principio
que la marca `gratis` y el crédito de la ruleta (§63, §64).

**Tres estados y no dos.** `null` es **«no se sabe»** —una run randomizada antes de que esto se
guardara— y jamás se lee como «no puede»: eso sería un veredicto que nadie ha medido.

**El formato de la instantánea se queda en 1**, y es lo delicado del cambio. Un lector rechaza
cualquier instantánea de formato **más nuevo**, así que subirlo habría hecho que todo amigo con la
versión anterior rechazara todas las instantáneas nuevas y viera **una clasificación vacía**: un
fallo mucho peor que perder un campo. Añadir campos opcionales es seguro en los dos sentidos, y hay
un test que lo fija.

### Montar la prueba: lo que costó averiguarlo

Está en `Escritorio\PermaLocke prueba link`. Dos cosas que no documenta nadie:

**`CITRA_USER_DIR` NO funciona.** La variable existe en el binario, pero el emulador la ignora y se
va a `%APPDATA%\Azahar`, o sea a la instalación real y a la partida buena. Se descubrió mirando la
fecha de la configuración real: escrita 25 segundos antes. Lo que sí funciona es el **modo portátil**:
una carpeta `user\` **junto al ejecutable**, lo que obliga a duplicar el emulador.

**Los dos campos de apodo de multijugador están bloqueados.** Medido por automatización de interfaz:

```
QApplication.HostRoom.settings.username    ReadOnly=True
QApplication.DirectConnect.nickname        ReadOnly=True
```

No es el validador ni la selección: **no se puede escribir en ellos**. Azahar los espeja del
**nombre de la consola emulada**, cuyo valor por defecto es `AZAHAR`, así que dos carpetas `user`
nuevas dan dos consolas con el mismo nombre y la sala rechaza el duplicado. Se cambia en
**Emulación → Configurar → Sistema**, y basta con cambiarlo en una de las dos.

La otra colisión, la de la MAC, se resuelve sola con carpetas separadas: cada una genera la suya en
`user\sysdata\mac.txt`.

---

## 81. El mapa de Alola, y la regla que nunca se había ejecutado (2026-09-02)

El jugador preguntó por la regla de las Poké Balls con la primera captura. Al medirla salió algo
peor de lo que se buscaba: **`FirstEncounterRule` no había actuado ni una sola vez en toda la run.**

### Lo medido

La base de datos de la partida real:

```
origin=captura  encounter_type=9 (Unknown)  consumed_zone=0  n=7
origin=gacha    encounter_type=8            consumed_zone=0  n=5
origin=wt       encounter_type=5            consumed_zone=0  n=2
                                            TOTAL 14, marcadas 0

ruta-1   n=2   gastan=0     ← dos capturas en la misma zona, y ni una palabra
```

### Por qué

Dos decisiones correctas por separado que juntas se anulan:

1. `encounterTypesThatConsumeZone` vale `[Wild, Fishing, Sos]`.
2. El §68 hizo que el vigilante registre toda captura automática como `Unknown`, porque no sabe
   deducir el tipo de encuentro — «registrar no es arbitrar».

`Unknown` no está en esa lista, así que **ninguna captura automática gasta zona jamás**,
`RuleContext.UsedZones` queda vacío para siempre y la regla no tiene con qué comparar. Tenía
código, configuración y **tests propios**, y todos registraban una captura `Wild` a mano: ninguno
pasaba por el camino que la aplicación toma de verdad.

Es la tercera vez con la misma forma —§55 y §68 son las otras—: **un guardia hecho tan prudente que
no puede equivocarse nunca, y por eso tampoco puede acertar.**

### Por qué no se puede deducir

La séptima generación **no guarda ningún campo que diga que un encuentro fue salvaje**. Medido
sobre la partida real:

```
Bidoof      Super Ball   met=46   nv.enc=9     ← captura salvaje
Ivysaur     Poké Ball    met=8    nv.enc=5     ← captura salvaje
Kingler     Poké Ball    met=0    nv.enc=1     ← entrega del gacha
```

Las entregas se distinguen (`met=0`, nivel 1), pero un **regalo del juego es idéntico a una captura
salvaje**: misma ball, lugar real, nivel real, `FatefulEncounter` en false. De paso quedó
desmentido el §68 en un detalle: la ball **sí se puede leer** —`Pk7Reader` recibe un `PK7` entero
con checksum válido— solo que nadie le había pedido el campo. No ayuda, porque no es la ball lo que
distingue.

BxnnyLocke tampoco lo deduce: **te lo pregunta**.

### La solución: un mapa

En vez de una pregunta seca, la sección **MAPA** dibuja Alola por islas y el jugador pincha la zona
donde salió cada captura. Lo que se registra es lo mismo, y se registra como lo que es —una
afirmación del jugador, `ZoneConfirmed`, `EventSource.Player`—, nunca como algo deducido.
`ZoneCleared` deshace un clic equivocado, porque deshacerlo editando la base de datos dejaría una
cadena cuyos hashes cuadran alrededor de un hueco.

Confirmar la zona **es** decir que fue salvaje, así que el tipo pasa de `Unknown` a `Wild`. Un tipo
que alguien afirmó mirando el encuentro **no se toca**: esto no sabe más que él.

Y lo que **no** hace: bloquear al vigilante. Una captura automática en una zona gastada **se sigue
registrando**, porque un Pokémon del que la run no se entera es un Pokémon cuya muerte no se cuenta,
que es justo el agujero que cerró el §68. Lo que se bloquea es la captura **declarada**, la del
diálogo, donde alguien miró el encuentro y dijo que era salvaje.

### El reparto por islas: medido, no recordado

Repartir las zonas entre las cuatro islas de memoria habría sido inventar datos. El cartucho lo
publica, aunque no donde parece: `ZoneData7` da a cada zona un `WorldIndex`, y aunque un «mundo» es
un mapa y no una isla —hay **303**—, los cuatro **exteriores** son exactamente las cuatro islas,
cada uno con su tramo de rutas:

| Mundo | Isla | Rutas |
|---|---|---|
| 0 | Melemele | 1-3 |
| 58 | Akala | 4-9 |
| 117 | Ula-Ula | 10-17 |
| 197 | Poni | — |

Los interiores se numeran **entre** ellos, así que el índice de mundo coloca cualquier zona.
Comprobado contra **21 nombres que solo pueden ser de una isla** —Hauoli, Konikoni, Lanakila…— con
**cero contradicciones**. Valía la pena: de memoria se habían colocado mal **Playa Big Wave** (es de
Melemele, no de Akala) y **Colina Saltagua** (Akala). Lo genera `RomTool mundos` en `Data/islas.json`.

### La trampa que casi se cuela: son dos listas de nombres

El cartucho dice «Ciudad Hauoli»; PKHeX dice «Ciudad Hauoli (Zona Comercial)». **La que la run
guarda en cada captura es la segunda.** Un mapa dibujado con los nombres cortos habría producido
`ciudad-hauoli` al pinchar, contra una captura registrada como `ciudad-hauoli-zona-comercial`: un
clic que no casa con nada y no falla nunca. Lo cazó un test que contaba las zonas —101 en vez de
116—, no la vista. Así que `islas.json` guarda **los nombres largos**, heredando de los cortos la
isla que se midió, y hay una teoría que fija los cuatro identificadores contra los que la run real
tiene guardados.

Quedan 113 y no 116 porque tres de los nombres de PKHeX no son sitios: «Lugar misterioso»,
«Lugar lejano (-)» y un guion suelto.

### Lo que sigue sin resolverse

`BallControlService` —quitarle al jugador las Poké Balls mientras la zona está gastada— **sigue
apagado**, y va a seguir: necesita la zona en vivo, cuyo anclaje murió en el §55, y **BxnnyLocke
tampoco lo hace** (medido: `MetLocation` 31 apariciones, `ZonaActual`/`CurrentZone`/`ZonaId` cero).
Reactivarlo es una investigación del tamaño del §22 para algo que la referencia no hace.

---

## 82. El mapa del cartucho: lo que hay y lo que no (2026-09-02)

El jugador enseñó un mapa de Kanto de otro tracker —fondo pintado, marcadores numerados, colores
por estado— y pidió algo así. Ese dibujo es de Game Freak, así que no puede viajar en el
repositorio; la pregunta era si se puede sacar de **su propia ROM**, como los sprites del §28.

### El decodificador que faltaba

Barrido del RomFS entero: 747 ficheros, 235 GARC. Salen **2271 láminas de 64×64 para arriba y
todas son `Etc1A4`** — justo el formato del que el §28 se libró porque los iconos son RGBA5551.
Así que había que escribir el decodificador de ETC1, y está escrito en `Etc1Texture`.

ETC1 es un formato publicado de Khronos: bloques de 4×4 con dos colores base, dos tablas de brillo
y dos bits por píxel, más un plano de alfa de cuatro bits. Lo que **no** publica nadie es cómo
ordena la consola los bloques, y eso se fijó mirando.

**La trampa que costó una pasada:** invertir los bytes es la lectura obvia de una especificación
que numera sus bits al revés, y produce una imagen con **la silueta correcta, el alfa correcto y el
color en ruido puro**. Ni un checksum ni una comprobación de tamaño habrían dicho una palabra. Se
lee en little-endian directo. Para una imagen, **el ojo es el único oráculo** que caza esto.

### Lo que hay

| Fichero | Contenido |
|---|---|
| `a/1/6/3` | **866 piezas de 128×64: los mapas de área**, a 4 columnas |
| `a/1/6/0` | **una** vista de isla, 256×256, recortada (76% transparente) |
| `a/1/6/6` | 4 MB **sin una sola imagen** — casi seguro la geometría 3D |
| `a/2/7/3` | 1157 ilustraciones de Pokémon a 256×256 y 512×256 |
| `a/1/2/5` | **las placas de tipo**, que el §34 daba por inexistentes |
| `a/1/3/8` | arte de efectos de combate |

La anchura de 4 columnas **está medida, no tanteada**: el salto medio entre el borde de abajo de
una pieza y el de arriba de la que va `w` más allá es **14,6 para w=4** y de 64 a 79 para todas las
demás. Cosidas así, las piezas 0-15 dan un 512×256 impecable de la zona de Ciudad Hauoli.

### Lo que NO hay, y por qué importa

**No hay cuatro mapas de isla.** Buscando por todo el cartucho cualquier lámina grande y
*recortada* —transparencia alrededor, que es lo que distingue una vista de isla de un mapa de área,
pintado hasta el borde— aparecen exactamente **dos**: las placas de tipo y una sola isla. Y ninguna
de las 866 piezas de `a/1/6/3` tiene un solo píxel transparente.

Que `a/1/6/6` sean 4 MB sin imágenes lo explica: **el juego dibuja el mapa en 3D**, así que no
existe la lámina plana por isla que haría falta.

Consecuencia para PermaLocke: se pueden mostrar los mapas de área, pero **no se sabe qué sitio es
cada uno**, y sin eso no se le pueden poner marcadores encima.

### El candidato a la tabla, descartado

`a/1/6/9` —573 KB y ninguna imagen— parecía el sitio donde estaría la tabla de «qué área es cada
mapa». **No lo es.** Son doce subficheros comprimidos con la forma «cuenta + tabla de
desplazamientos relativos al byte 4 + secciones», y lo que hay dentro son `Head`, `Hips`, `LArm`,
`LFeeler1`, `LFingerA1`, `LEar1`, `LEye`, `LFoot`: **esqueletos 3D de Pokémon**.

Antes de mirar los nombres, la medida numérica ya lo apuntaba: ninguno de los doce es una lista de
índices —un 10-19% de valores por debajo de 866 y un 18% de coma flotante es lo que tiene
cualquier binario de geometría, no una tabla—. Que un fichero esté al lado de otro en el RomFS no
lo hace de su familia, y ese es el único motivo por el que era candidato.

Así que **la correspondencia mapa → sitio sigue sin localizarse**, y con ella la única vía barata
para usar el arte real del cartucho.

### Lo que se montó

Marcadores numerados por isla, que es lo que hace legible la imagen de referencia: el número cabe,
el nombre está a un ratón de distancia, y **cuánto llevas de Alola se ve desde el otro lado de la
habitación**. Cada isla lleva su propia cuenta, y los tres estados —libre, gastada, su Pokémon
murió— tienen leyenda.

Nada de esto inventa geografía: la isla de cada zona sale del cartucho (§81) y el número es su
puesto dentro de ella. Poner un marcador en unas coordenadas elegidas a ojo sobre una isla dibujada
a ojo habría sido decir dónde están las cosas sin saberlo.

### Herramientas

`RomTool mapa-buscar`, `mapa-tallar`, `mapa-volcar`, `mapa-tallar-png`, `mapa-coser`, `mapa-medir`,
`mapa-segmentar`, `mapa-transparencia` y `mapa-islas`. Ninguna escribe en el juego y todo lo que
sacan va al temporal, **nunca al repositorio**: son píxeles de Nintendo.
