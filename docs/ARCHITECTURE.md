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
