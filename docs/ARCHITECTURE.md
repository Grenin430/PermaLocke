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
entrada de `trpoke`), que es lo único que la norma del §19 permite. El nivel se redondea **al entero
más cercano**, las mitades hacia arriba, y no se trunca: truncando, un nivel 4 subido un 20% (4,8) se
quedaría en 4, y en la primera isla el +20% apenas se notaría. **No es un redondeo hacia arriba**,
como decía este párrafo hasta el 2026-09-21: 12 × 1,2 = 14,4 da 14, y 14 es el cap de la 1ª prueba en
`Data/levelcaps.json`; hacia arriba saldría 15 y la tabla dejaría de cuadrar.

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

---

## 83. El mapa de verdad: arte del cartucho y marcadores puestos a mano (2026-09-02)

### Las cuatro islas

Están en `a/1/6/3`, y llegar a ellas costó cuatro segmentaciones fallidas. Lo que las tumbó todas
fue una suposición: **la anchura no es la misma para todos los mapas.** Los de área son de cuatro
columnas y los de isla de **ocho**. Un mapa de cinco cosido a cuatro sale en diagonal, y contra eso
no hay umbral que valga.

Y aun con la anchura buena quedaba **por dónde empieza cada mapa**, que es un fallo que las
costuras no pueden ver: un desfase igual en todas las filas deja la pieza *i* y la *i+8* vecinas,
así que las costuras verticales salen perfectas mientras cada fila sale girada. La isla se parte y
sus dos mitades tocan los bordes. Medir la fase por las costuras laterales tampoco valió: un mapa
de isla es casi todo océano, y el océano encaja consigo mismo en cualquier desplazamiento —salía
5,5 contra 8,9, que es ruido—.

**La medida que sí funciona es específica de lo que se busca: una isla bien encuadrada tiene mar en
los cuatro bordes.** Eso no pasa por casualidad. Con ella se fijan el arranque y la altura, y en
caso de empate gana el marco **más grande**, porque cualquier recorte que caiga en agua puntúa
igual de bien que el correcto —mirando solo los bordes laterales, Akala salía con el Rancho Ohana
cortado—. Después se recorta a la tierra: el cartucho encuadra cada isla en una esquina de un mar
enorme, que es donde el juego dibuja las demás al alejarte, y Poni ocupaba menos de media lámina.

Vive en `IslandMapReader`, no en la herramienta, para que la aplicación y `RomTool mapa-cuatro`
usen el mismo cosido. Las imágenes se extraen de la ROM del propio jugador la primera vez que se
abre la pantalla, como los sprites del §28, y `Data/mapa-islas/` está en `.gitignore`.

Trampa al leerlas: si una lámina no decodifica **se para**, no se salta. Saltarla correría todas
las siguientes un puesto y los mapas saldrían en diagonal sin que nada avisara.

### Los marcadores

Dónde va cada zona sobre el dibujo **no está en el cartucho**. Se buscó —`a/1/6/9` era el candidato
y resultaron ser esqueletos 3D— y no aparece. Así que lo pone una persona: MODO COLOCAR, eliges la
zona, pinchas el mapa, y se guarda en `Data/marcadores.json`.

Tres decisiones que valen la pena:

- **Se guarda en fracción de la imagen, no en píxeles.** Si el mapa se vuelve a extraer con otro
  recorte, un marcador en píxeles apuntaría a otro sitio sin que nada fallara.
- **La clave es el identificador normalizado de zona**, el mismo que la run guarda en cada captura,
  así que un marcador y un Pokémon hablan del mismo sitio por construcción.
- **Una posición fuera del cuadro se descarta, no se recorta.** Recortarla la pegaría al borde y la
  conservaría, y eso se lee como una respuesta. Una zona sin marcador es honesta; una clavada en el
  borde porque alguien editó el fichero miente sobre dónde está.

El fichero **sí viaja con PermaLocke**, al revés que las imágenes: es trabajo nuestro, así que uno
coloca y los cinco lo tienen.

El modo es un interruptor explícito y no una deducción de lo que esté elegido: el mismo clic
significaría dos cosas y una de ellas escribe en el historial de la run.

Y si faltan las imágenes —alguien sin la ROM en su sitio— la pantalla cae al tablero numerado del
§82, que funciona igual de bien. Que falte un dibujo no puede costarle a nadie su seguimiento.

### 83 bis. «Aquí no se atrapa nada» es un estado, no una ausencia

Terminada Melemele, el jugador dijo que las que había dejado sin colocar eran **zonas donde no se
puede atrapar de ninguna forma**. Eso hacía falta decirlo en voz alta: sin ello, una zona sin
marcador significa dos cosas —«no la he colocado todavía» y «aquí no hay nada»— y el contador de
la pantalla incluye sitios que **nunca se pueden gastar**, así que no puede llegar a su propio tope.

Ahora `marcadores.json` lleva `sinEncuentros`, y el total cuenta solo donde se puede atrapar. Un
marcador y esa marca se excluyen: colocar un pin **es** decir que el sitio vale uno.

**Y el cartucho tiene voz.** `encdata` sabe qué áreas llevan tabla de encuentros, así que al
marcar la isla se contrasta y se avisa por su nombre de cualquier zona que el jugador esté
descartando y que el cartucho diga que **sí** los tiene. No se le lleva la contraria —él ha jugado,
y el cruce de nombres es imperfecto— pero una discrepancia merece un alto.

La primera vez que se pasó ya encontró una: de las nueve que dejó sin colocar, el cartucho
corrobora Pueblo Lilii, Senda Mahalo, Ruinas de la Guerra y Playa Big Wave, y **discrepa en Huerto
de Bayas**, que tiene tabla.

Por qué la respuesta puede ser «no lo sé»: el cartucho nombra corto —«Ciudad Hauoli»— y la run
largo —«Ciudad Hauoli (Puerto)»—, así que un sublugar hereda del padre, y un padre con áreas de
los dos tipos no puede contestar por su hijo. `JsonZoneEncounters.HasEncounters` devuelve null en
vez de adivinar.

### 83 ter. El mapa se cierra: 61 marcadores y ninguna puerta abierta

Terminadas las cuatro islas, el mapa deja de editarse. **61 marcadores colocados y 52 zonas sin
encuentros**, que son las 113 exactas.

Las estériles **ya no se dibujan apagadas: no se construyen**. Pintarlas era peor que inútil —no se
pueden gastar nunca, así que no son parte del mapa— y dejarlas dentro hacía que el contador no
pudiera llegar a su propio tope. El número de cada marcador se reparte ahora entre las que quedan,
así que significa algo.

Y **no hay modo de colocar**. El fichero viaja con la aplicación, así que una pantalla que dejara a
cinco personas arrastrar Alola acabaría con cinco Alolas distintas. `Data/marcadores.json` se queda
como fuente única, y volver a abrir la edición es un cambio pequeño aquí, no algo que pueda pasar
por accidente. Con ella se va `JsonZoneEncounters`, que existía solo para avisar al marcar una isla.

Un guardia que se queda: una zona **gastada que no esté en el mapa** se dice en rojo. Pasaría si
alguien recorta `marcadores.json` con una run empezada, y callarlo dejaría un encuentro gastado que
la pantalla jura que sigue libre.

### 83 quater. La foto de cada zona

El jugador reunió **57 imágenes** de las zonas y las dejó en `islas/`, una carpeta por isla. Al
pasar el ratón por un marcador sale una tarjeta con la foto, el nombre y lo que se cazó allí.

**El emparejamiento tiene que cerrar.** Los ficheros vienen con nombre de wiki —
`285px-Ruta_4_(Alola).png`, `1200px-Ciudad_Konikoni_USUL.png`— así que casi todo casa quitando la
decoración, y **lo que no casa se escribe a mano** en vez de adivinarlo. `Probe --fotos` se niega a
escribir `Data/fotos.json` mientras quede una zona sin foto o una foto sin zona: un emparejamiento
malo pondría Ruta 4 sobre Ruta 5 y **se vería perfectamente bien**, que es la misma forma de fallo
que el §30.

Un fichero `(COMPLETO)` cubre una zona y sus sublugares en una sola imagen. Detalle que costó una
pasada: cuando el padre **también** es una zona con marcador —Colina Dequilate y su Caldera
Remota—, quedarse en la coincidencia exacta dejaba al sublugar sin foto. Un `(COMPLETO)` responde
por los dos.

Siete venían en **WebP**, que WPF solo lee si está instalado `Microsoft.WebpImageExtension`, un
añadido opcional del Store. Aquí estaba y se veían; en la máquina de otro habría salido un hueco en
blanco sin decir por qué. Convertidas a PNG con el propio códec.

Las imágenes son del juego, así que `islas/` va al `.gitignore` como `Data/sprites/` y
`Data/mapa-islas/`. **Lo que sí se versiona es `Data/fotos.json`**, que es solo el emparejamiento.

## 84. La ruleta rehecha, y un color que no podía significar nada (2026-09-04)

Repaso visual de la pantalla del rol LUDÓPATA. Lo que la sostenía era un fallo de fondo: el color
de cada cuña salía de `WedgeBrush(index)`, o sea de **la posición**, y los seis
`RouletteSlotViewModel` se construían una vez en el constructor con su color ya puesto mientras la
cara llegaba después, escribiendo `Text`. Así que la misma cara salía roja o azul según dónde
cayera y **el color no podía significar nada ni queriendo**. Mirando la rueda no se sabía si te iba
bien o mal: «mueren 3 Pokémon» y «3 tiradas de gacha» se veían igual de festivos.

El comentario que defendía aquello decía que un verde y un rojo «chafarían la tensión media vuelta
antes». La premisa no se sostiene y por eso se revoca por escrito: las seis caras **se desvelan una
a una antes** de que la rueda arranque, y la lista de al lado ya las pintaba de verde o rojo. Lo
que el color añade ya estaba en pantalla; lo que sigue sin saberse —y es toda la tensión— es en
cuál para.

Ahora son dos tonos por bando alternados por posición. La alternancia **no es una escala de
gravedad**: decidir que «IV a cero» es peor que «menos 1 MT» sería un juicio inventado, y lo único
que hace es que dos cuñas seguidas del mismo bando no se lean como una sola mancha.

### Los iconos, y tres medidas nuevas

`PokemonSpriteService` ya estaba inyectado en el ViewModel desde el §62 y solo dibujaba **una** Poké
Ball. Cada cara declara ahora su dibujo en `Data/roulette.json`, con dos campos porque son dos
cosas distintas: `icono` es un id de objeto y `iconoEspecie` un id de especie. Las dos caras de
muerte llevan **Shedinja (292)**, que no es decoración: es literalmente en lo que `DeathTransform`
convierte a un caído.

Tres iconos hubo que medirlos, con la disciplina del §45 —renderizar el vecindario y reconocer algo
inconfundible—:

| objeto | icono | lo que lo ancla |
|---|---|---|
| 328 MT01 | 309 | el icono **308 es un colmillo blanco curvado**, o sea el Colmillo Agudo (327), y el 309 ya es un disco; el 310 es otro disco de distinto color, que es la tirada de veinte tipos empezando |
| 795 Chapa Plateada | 649 | **649 plateada, 650 dorada, 651 una pulsera azul oscuro**, en ese orden, que son exactamente los objetos 795, 796 y 797 |
| 796 Chapa Dorada | 650 | ídem |

Una sola cosa redonda podría ser cualquier cosa; tres seguidas en ese orden no. Y **solo se reclama
la primera MT**: las cien comparten veinte discos, así que no hay correspondencia id→icono para las
demás e inventarla dibujaría el disco de otro tipo. La ruleta nunca dice *cuál* MT.

El guardia de esto es un test, no el código: `ItemIconIndex.TryGet` contesta `false` para un objeto
que nadie ha mirado y la pantalla lo convierte en «sin dibujo», que es lo correcto **y es
invisible**. `RouletteWheelIconTests` cruza el fichero que se reparte contra la tabla que se
reparte, así que un id equivocado falla ahí y no en una cuña en blanco a mitad de partida.

### Lo demás

- **La cuña dice tres cosas**: dibujo, cifra grande y nombre corto. `cifra` se escribe a mano y no
  se deduce de `cantidad`, porque tres objetos distintos a tres de cada son **nueve** objetos.
- **La lista de dieciséis baja a una tira** de 2×8 debajo. Ocupaba 330 px a la derecha para decir
  algo que se lee de un vistazo: cuáles de todas están en juego.
- **El final ocurre en la rueda**: la ganadora se queda encendida, las otras cinco se apagan y la
  tarjeta sale sobre el disco. Va abajo a propósito, porque la cuña ganadora acaba **siempre**
  arriba, debajo de la marca, así que no puede taparla nunca.
- **El panel se tiñe** del bando de la cuña que pasa por la marca, y solo a partir del primer golpe:
  durante el barrido pasa una cuña cada cincuenta milisegundos y aquello sería un parpadeo. Son dos
  capas con su color puesto a las que se anima la **opacidad**; animar el color de un `GradientStop`
  pide un Freezable vivo, que es una trampa que esta pantalla ya pagó una vez.
- **`WheelEnding`**, cinco perfiles de frenada con la misma forma y la misma norma que `ReelEnding`
  del §31: ninguno puede correlacionar con lo que salió. Antes eran doce segundos de un solo
  `PowerEase` idénticos en cada tirada. Un desplazamiento de cuña es «el ángulo de reposo menos k
  por sesenta», así que uno **negativo** manda la rueda una cuña más allá del ganador y la obliga a
  volver, que es la que de verdad engaña. Los tramos se encadenan en vez de ser un storyboard con
  fotogramas clave porque un tramo puede ir **hacia atrás** y cada uno quiere su propia curva.
- **El eje**: ya no gira —media vuelta por minuto no se lee como movimiento y sí se nota como un
  parpadeo— y ya no se ve borroso. El icono del cartucho mide **dieciocho** píxeles de lado, así que
  estirarlo a setenta y seis con interpolación suave era pedirle cuatro veces más de lo que tiene.
  Cinco aumentos exactos y vecino más próximo.

### Lo que solo se vio abriendo la ventana

La rueda pasó de 460 a 700 contando el ancho, que es lo que la limitaba antes con el panel lateral.
Con el panel fuera **la limita el alto**, y a 700 **no cabía**: el aro salía cortado por arriba y por
abajo y la marca no se veía en absoluto. Nada de eso se nota leyendo el XAML.

Va dentro de un `Viewbox` con `StretchDirection="DownOnly"`: se dibuja siempre a 700 —las cuñas son
geometría en píxeles y estirarlas las descuadraría con las etiquetas, que se sitúan por
coordenadas— y se encoge hasta caber. En una ventana de 1560×980 se ve a unos **545**, no a 700; en
una más alta crece hasta el tamaño de diseño. Los dibujos no se emborronan al encogerse porque
`NearestNeighbor` se hereda por el árbol visual y no interpola nunca. La tarjeta de resultado va
**fuera** del Viewbox: un párrafo no se encoge igual de bien que un dibujo.

### Lo que NO está comprobado

Todo lo estático está visto en la aplicación real: el tamaño, el aro, la marca, el eje nítido y la
tira de dieciséis con sus dieciséis dibujos —que es la misma llamada `Sprite(face)` que usan las
cuñas, así que prueba que los iconos resuelven—. **La rueda desvelada y la tirada entera no**:
verlas exige girar de verdad, y girar escribe en la partida y puede matar tres Pokémon. El jugador
debe cero tiradas, así que GIRAR está apagado. Que la cuña desvelada quepa está comprobado por
aritmética y no por ojo: el contenido mide unos 113 px de los 132 de su caja, y su radio va de 148 a
280 con el eje en 75 y el borde en 325.

### El marco clásico, y qué cuesta (mismo día)

El jugador pasó una foto de una ruleta de premios de feria —aro dorado con bombillas, cuñas
pastel, buje ornamentado, flecha roja y pie— y una instrucción que zanjaba la única duda que
tenía: **«el relleno no lo cambies, cambia los contornos y el color de ellos»**. O sea que el verde
y el rojo de las cuñas se quedan diciendo lo que dicen, y lo que se rehace es el marco.

Lo que entró: **aro dorado** con degradado de cuatro paradas —reflejo, metal, sombra y un rebote
al filo, que es como se ve un aro de latón y no un anillo pintado—, **veinte bombillas**, **juntas
doradas** de 3 px entre cuñas en vez de las casi negras de 1,5, **flecha roja** con filo dorado, y
un **buje dorado** alrededor de la Poké Ball. El ganador y la onda de aterrizaje pasan a oro claro,
que es lo único que se lee encima de un verde y de un rojo.

Las bombillas **se generan en el code-behind**, por el mismo motivo que las cuñas se calculan en
vez de dibujarse a mano: veinte círculos sobre una circunferencia son veinte ocasiones de teclear
mal una coordenada, y una lámpara cuatro píxeles fuera de sitio es de las cosas que cantan en
pantalla y no se ven en el XAML. Son veinte y no dieciocho **porque seis no divide a veinte**, así
que ninguna cae justo sobre una junta. Cada una es un degradado radial y no un círculo plano con
efecto de brillo: veinte `DropShadowEffect` serían veinte pasadas de render por fotograma durante
doce segundos de giro, con la rueda ya animándose detrás.

**El escenario pasa de 700 a 740 y la rueda sigue midiendo 700.** Esos veinte píxeles por lado son
la banda dorada, así que el aro se gana **por fuera** y no quitándole sitio a la geometría de las
cuñas, que no se toca: el disco sigue siendo de 650 dentro de una caja de 700 y las etiquetas
siguen cayendo donde caían.

**Lo que sí cuesta, medido y no estimado.** El `Viewbox` escala para que quepa el alto, y el
escenario ha crecido, así que la escala baja. Sobre la ventana real del jugador —1560×980—, contado
sobre la captura: el objeto entero pasa de **545 a 553 px** de ancho, pero el disco de color, que es
lo que se lee, pasa de **506 a 486**, un 4% menos. Es inherente: un marco de feria es sobre todo
marco.

Si algún día molesta, la altura está ahí para recuperarla — la tira de dieciséis ocupa 110 px
debajo y a los lados de la rueda sobran casi 400 a cada lado, así que en dos columnas de ocho el
disco pasaría de 570. No se ha hecho porque no se pidió.

**El pie no se ha puesto.** Sale en la foto, pero un pie no es un contorno: es un objeto nuevo, y
además comería otros 45 px de alto del mismo presupuesto. Queda dicho aquí para que la próxima vez
la decisión no haya que volver a tomarla a ciegas.

### Y el aplastado fuera (mismo día)

El jugador: «la ruleta está un poco más horizontada, no es simétrica como tal, está levemente
aplastada». Tenía razón, y era mío: el `ScaleY` de 0,94 de la idea 5, puesto para que la rueda se
leyera **apoyada** y no como un gráfico de tarta.

Funcionaba mientras el aro era un anillo gris plano. Con el aro dorado, su degradado de cuatro
paradas y su sombra, **el bulto ya lo da el metal**, así que lo único que seguía aportando el
aplastado era que la rueda no fuese redonda. Y una ruleta de feria es un círculo. Fuera.

Medido sobre las dos capturas, comparando el radio horizontal con el vertical del aro:

| | V/H | diámetro exterior | disco de color |
|---|---|---|---|
| con aplastado | 0,976 | 553 px | 486 px |
| sin aplastado | **1,008** | 522 px | **458 px** |

Redonda dentro del error de medida. Y **cuesta un 6%**, porque el escenario pasa a necesitar 740 px
de alto en vez de 696 y el `Viewbox` reparte lo que hay.

Sumando las dos vueltas del día, el disco de color ha ido de **506 → 486 → 458**: el aro dorado se
llevó 20 px y la redondez otros 28. Sigue por encima de los 428 que medía antes de todo esto, pero
la dirección es la contraria de la que pidió la idea 4, así que queda dicho con números en vez de
callado. La palanca para recuperarlo sigue siendo la misma y sigue sin usarse porque nadie la ha
pedido: la tira de dieciséis ocupa 110 px de alto y a los lados de la rueda sobran casi 400 px a
cada lado.

Y una trampa de las de siempre, que cayó otra vez: **un comentario XML no admite `--`**. Está
escrito en `CLAUDE.md` desde el §74 y aun así costó una compilación.

## 85. La sexta prueba comparaba dos unidades distintas (2026-09-04)

El jugador: «te dije que después de completar la sexta prueba TODOS los entrenadores saquen Pokémon
en su última etapa; estoy peleando contra un random del Team Skull y me ha sacado **un Larvitar**».
Tenía razón, y el fallo eran cinco niveles de nada.

### El corte estaba en la unidad equivocada

La regla no puede consultar por dónde vas —el mod se genera una vez, antes de empezar—, así que usa
lo único que la tabla de entrenadores lleva y que sigue el avance de la historia: **el nivel que el
cartucho le dio a cada equipo**. Eso está bien, y el comentario del fichero lo decía: *«se compara
contra el nivel del CARTUCHO y nunca contra el que sube el rol»*.

Y acto seguido el número era **34**, que salió de `Data/levelcaps.json`. Pero un cap **ya va subido
un 20%** (§48): es el nivel del jefe más el porcentaje de la competición. Así que el corte estaba
comparando un nivel subido contra niveles sin subir, cinco por encima de donde tocaba.

Medido, no deducido. El recluta es el **entrenador 473**, clase 28, con un Larvitar a **nivel 33 de
cartucho**; el corte estaba en 34. Y el número bueno sale de la tabla de estáticos, donde los ocho
Dominantes miden **12, 20, 22, 24, 29, 33, 35 y 49**: el de la sexta prueba es **Vikavolt, 29**.
Multiplicados por 1,2 esos ocho caen sobre los caps de la tabla, que es la comprobación cruzada que
nadie había hecho — y que ahora es un test.

Un Larvitar a 33 está **entre la sexta prueba y la séptima**, o sea exactamente donde el jugador
dijo que estaba.

### Y un segundo agujero en la misma regla

`ExtraPokemonRandomizer` **no aplicaba la regla en absoluto**. Se escribió como «copiar el último
del equipo y cambiarle la especie» y ahí no había ninguna regla que aplicar, así que el Pokémon
extra que el rol añade a los 106 combates importantes podía ser una primera etapa después de la
sexta prueba. El jugador pidió «TODOS los entrenadores, TODOS» y ese también es uno.

La trampa al arreglarlo: este módulo corre **después** del randomizador de entrenadores, que ya ha
subido los niveles por el rol. Comparar el nivel que hay en el fichero contra un corte de cartucho
habría metido aquí entrenadores cinco niveles por debajo del corte — el mismo error, del otro lado.
Se lee el nivel del **cartucho** del último Pokémon que el entrenador ya tenía, que es de quien se
copia la entrada.

### La comprobación tenía el agujero en el mismo sitio

`RomTool trainers` relee lo generado y cuenta cuántos Pokémon por encima del corte se han quedado
sin evolucionar. Pero empezaba con `if (before.Length != after.Length) continue;`, así que **los 106
equipos que crecieron quedaban fuera de todas sus comprobaciones**, incluida esa. O sea que la única
herramienta que podía haber cazado el segundo fallo estaba ciega justo donde estaba.

Ahora un equipo que crece se comprueba igual; los huecos que ya existían se comparan uno a uno con
el cartucho y el añadido hereda el nivel de historia del último que había. La cuenta pasa de **474 a
809** Pokémon vigilados.

De paso, el renglón `NIVELES movidos: N (debe ser 0)` dejó de ser verdad el día que los roles
empezaron a subir niveles: generado con rol se mueven todos. Un contador que se lee como una alarma
cuando lo normal es que salte enseña a ignorarlo, así que dice lo que significa.

### Verificado releyendo, no por el informe

Generado con la seed real de la run y el rol LUDÓPATA:

- **809 Pokémon** de nivel de cartucho 29 en adelante, **0 sin evolucionar del todo**.
- **0 entrenadores** llevan un Larvitar en todo el juego.
- El entrenador 473 pasa de **Larvitar** a **Tyranitar**, que es literalmente lo que el jugador dijo
  que debería salir.

Y dos herramientas nuevas, porque «me ha salido un X, de dónde sale» se ha preguntado ya tres veces
—la clase 222 de Tilo, el Nihilego del Paraíso y ahora esto— y las tres se contestaron a mano:
`RomTool quien-lleva <especie> [ruta]` y `RomTool entrenador <id> [ruta]`, que leen el trpoke de un
mod **generado o instalado** y enseñan el equipo con su nivel de cartucho al lado.

### Lo que cuesta aplicarlo

Hay que **volver a randomizar y reinstalar**. Cambiar el corte no desplaza el sorteo —`FinalOf` es
determinista y se aplica **después** de elegir la especie, así que no consume azar—, de modo que por
sí solo daría el mismo mundo con más entrenadores evolucionados. Pero el mod instalado es del 3 de
septiembre a las 06:26 y **le faltan las ocho clases importantes** que se añadieron a las 06:36
(§84 bis, medido en esta misma sesión), así que reinstalar arrastra también ese cambio, que **sí**
reordena qué mega y qué Pokémon extra lleva cada combate importante.

## 86. La rueda paraba y luego cambiaba de opinión (2026-09-04)

Primera tirada de verdad con la pantalla nueva, y el jugador: «se ha parado en el medio de IV AL
MÁXIMO y ha pasado por la cara a la siguiente; quiero que en el que pare, paró».

Es la idea 8 del §84 haciendo exactamente lo que se le pidió. `WheelEnding` era una lista de
**paradas**: la rueda se plantaba dos o tres cuñas antes del ganador y entraba de una en una, y uno
de los cinco perfiles llegaba a pasarse una cuña entera y volver. Se hizo para que el final no se
aprendiera, y para eso funciona. Lo que costaba no se vio hasta verlo girar: la rueda **se para**
sobre una cara, se queda ahí el tiempo justo para leerla, y luego se mueve.

El jugador tiene razón y el motivo va más allá del gusto. **El resultado ya está decidido, escrito
en la partida y registrado antes de que la rueda empiece a girar** — es la norma de esta pantalla
desde el §62. Una rueda que se detiene sobre una respuesta y después la cambia no está creando
tensión: está enseñando algo que no ha pasado. Es de la misma familia que el §65, donde la
comprobación estaba peor pensada que el código.

### La variedad se muda de sitio

No se pierde: pasa de **dónde para** a **cómo frena**. Cada perfil es ahora un solo barrido
monótono hasta el ganador, con tres cosas que cambian —la potencia del frenado, las vueltas de más
y cuánto se asienta al final— y ninguna de ellas puede enseñar otra cara:

| perfil | potencia | vueltas de más | rebote |
|---|---|---|---|
| larga | 5 | 0 | 8° |
| seca | 3 | +1 | 0° |
| agónica | 7 | 0 | 5° |
| vuelta de más | 4 | +2 | 14° |
| limpia | 6 | +1 | 0° |

Sigue en pie la norma del §31: **ninguno correlaciona con lo que salió**, porque se sortea de su
propia corriente.

### El número que lo sostiene

Media cuña son **30°**: ahí es exactamente donde la vecina llega bajo la marca. El rebote está
acotado a **20°** por `MaxBounce`, y el perfil más movido usa 14°, o sea que quedan 16° de margen.
Un rebote dentro de ese límite se lee como el peso de la rueda cayendo en su muesca; uno por encima
aparcaría **otra cara** bajo la marca mientras la tarjeta anuncia la correcta, y nada lo diría. Por
eso el constructor estático **lanza** en vez de dejar jugar un perfil mal escrito: es el mismo
guardia que ya tenía, apuntando ahora a lo que de verdad puede mentir.

### De rebote, el ambiente mejora

El tinte del panel se encendía con una bandera que se ponía al acabar el primer tramo. Sin tramos
no hay bandera, así que ahora se le pregunta a la rueda: se mide **cuánto se ha movido desde el
fotograma anterior** y el color entra por debajo de 4° por fotograma, que es una cuña cada cuarto de
segundo. Sale mejor que antes — el ambiente aparece cuando la rueda va despacio de verdad, y no
cuando a un perfil le tocaba decir que iba despacio.

### Lo que NO está comprobado

El giro entero sigue sin verse desde este lado: girar consume una tirada y escribe en la partida, y
el jugador debe cero. Lo que sí se puede afirmar es la geometría, que es donde estaba el problema —
20° de rebote máximo contra 30° de media cuña—, y que un perfil que se pase de ahí no llega a
animarse.

## 87. El gacha reventaba en cada tirada, y el Pokémon llegaba igual (2026-09-04)

El jugador tiró dos veces al gacha y las dos le salió «ha habido un error inesperado». Lo primero
que dice el log es que **no se perdió nada**: Roserade a la caja 1 y Salamence a la caja 2, los dos
escritos en la partida y registrados. Lo que reventó fue el espectáculo.

```
System.ArgumentOutOfRangeException: '1,006' no es un valor Percent válido para KeyTime.
   at PermaLocke.App.Views.GachaView.StartClickFeedback(...)
```

### Un fotograma clave fuera del final

`ReelEnding` describe el cierre como una lista de tiempos, y su constructor estático **exige** que
el último sea `1.00`: el carrete tiene que acabar en el ganador (§31). `Clicks` son esos tiempos
menos el primero, así que **el último clic cae siempre en el 1,00 de la tirada**. Y cada clic pide
tres fotogramas: uno justo antes, el golpe en `+0,006` y la vuelta en `+0,03`.

Para el último eso es el 1,006 y el 1,03, y `KeyTime.FromPercent` lanza por encima de 1. O sea que
**fallaba en todas las tiradas desde que se añadieron los cinco cierres**, no en dos.

La cola de la línea de tiempo lo arregla: dura un 6% más que la tirada y las fracciones se
reescalan a ella, así que 1,03 cae en 0,972. No es un parche de conveniencia — ese último rebote es
el golpe del **aterrizaje**, y el aterrizaje ocurre justo cuando el carrete ya ha parado. Además
`OnTail` recorta a [0, 1], de modo que la excepción es ahora estructuralmente imposible aunque
alguien escriba un cierre raro: la cola hace que se vea bien, el recorte hace que no pueda reventar.

### Lo que de verdad costó: la excepción se escapaba

La tirada se decide, se escribe y se registra **antes** de animar un solo fotograma. Una animación
rota tendría que costar la animación y nada más — es exactamente la lección que la ruleta aprendió
en el §84, envolviendo su `PlayAsync` en un try propio porque una excepción en el primer desvelado
dejó al jugador sin saber qué le había tocado.

El gacha no la tenía, y por una razón que conviene entender: la vista arranca el giro desde un
`Dispatcher.BeginInvoke`, o sea **fuera del await**, así que ningún try del ViewModel puede verla.
Subía hasta el manejador de la aplicación, le enseñaba al jugador un error por algo que había
funcionado, y de paso dejaba al ViewModel esperando a que el carrete parase hasta que saltaba su
red de seguridad **ocho segundos después** — se ve en el log, entre la excepción y la entrega.

Ahora el `BeginInvoke` lleva su propio try: registra por `GachaViewModel.AnimationFailed` y llama a
`request.Stopped()`, así que el resultado sale en el acto.

### Lo que no está comprobado

El giro arreglado no se ha visto: tirar consume una tirada y escribe un Pokémon en la partida del
jugador. Lo que sí es demostrable es la aritmética, que es donde estaba el fallo — el clic más
tardío que `ReelEnding` puede producir es 1,00 por su propio constructor estático, el desplazamiento
mayor es +0,03, y 1,03 entre 1,06 da 0,972.

## 88. Volcanion con el dibujo de Hoopa (2026-09-04)

El jugador hizo un wonder trade, le salió Volcanion, y la tarjeta lo enseñó con el sprite de **Hoopa
Desatado**.

La tabla de especie → icono se construyó a mano mirando el contenedor tramo a tramo (§30 y §30 bis),
porque el cartucho no la publica. El segundo bloque —las 158 especies de la 650 a la 807— no sigue
el orden nacional, así que cada una hubo que identificarla. Y en un sitio se identificaron cruzadas.

El tramo real es:

| icono | quién es |
|---|---|
| 1001 | Diancie |
| 1002 | Mega Diancie |
| 1003 | **Volcanion** |
| 1004 | Hoopa |
| 1005 | Hoopa Desatado |

O sea que **Volcanion va delante de Hoopa**, al revés que en la Pokédex, y la tabla tenía
`[720] = 1003` y `[721] = 1005`. Cada uno enseñaba el dibujo del otro; lo que salió en pantalla fue
el 1005, que es Hoopa Desatado.

### Por qué no lo cazó el guardia que ya existía

`The_second_block_covers_the_rest_and_uses_up_its_icons_exactly` comprueba que las 158 especies
estén, que cada una apunte a un icono propio y que todos caigan dentro del bloque. **Un cruce pasa
las tres**: la cuenta sigue siendo 158, siguen siendo distintos y siguen estando en rango. Ese
guardia detecta que falte o sobre alguien, no que dos estén intercambiados, y por construcción no
puede.

Lo único que lo encuentra es mirar. Así que se miró **el bloque entero**: se generaron diez hojas de
contacto con el icono que la tabla asigna a cada especie y su nombre debajo, y se leyeron las
**158 de 158**. Esta pareja es el único error que hay. La tabla queda auditada de punta a punta, que
es más de lo que se podía decir hasta hoy.

### Nota sobre el script de auditoría

Dos trampas de PowerShell costaron tres intentos, y las dos son de las que no dan error:

- **`$s` y `$S` son la misma variable.** El bucle `for($s=0; ...)` machacaba la ruta de los sprites,
  así que `Test-Path` daba falso para los 158 y las hojas salían con los nombres y sin dibujos.
- **`[int]` redondea, no trunca.** `[int](5/8)` es 1, así que la fila de cada celda salía mal y las
  imágenes se pisaban unas a otras.

### Lo que no es un fallo

Que del wonder trade salga un Volcanion es correcto: `Data/wondertrade.json` lleva
`permitirLegendarios: true`, y la banda ya limita sola —hay que entregar algo de 600 para sacar uno,
y el jugador entregó una Primarina de 530 con un +13%—. Si la competición lo prefiere, ese
interruptor los quita del todo sin tocar código.

## 89. El Shedinja que se deshizo, y por qué no es un fallo de escritura (2026-09-04)

Al jugador se le murió un Latias, PermaLocke lo convirtió en Shedinja, y al volver a entrar tenía
otra vez un Latias debilitado.

Lo primero, lo que **sí** funcionó, porque conviene separarlo: la muerte está en el historial —
«04/09 20:41 Latias ha caído a 0 PS» y su penalización de −25— y ahí seguirá pase lo que pase. La
run es el registro autoritativo y no perdió nada.

Y la escritura tampoco falló. El log:

```
20:41:52 Muerte detectada: Latias
20:41:52 0x3254EE60: muerte, 26 bytes escritos y releídos
20:41:52 0x330128E4: muerte, 26 bytes escritos y releídos
20:41:52 0x3002E558: muerte, 25 bytes escritos y releídos
20:41:52 0x3002F0FC: muerte, 25 bytes escritos y releídos
20:41:52 0x33F80744: muerte, 25 bytes escritos y releídos
20:41:52 Latias transformado en el juego (hueco 0, 5 copias releídas)
```

Cinco copias escritas y **releídas una a una**. El Shedinja estuvo ahí.

### Lo que pasó de verdad

Quince segundos después:

```
20:42:07 Sin conexión con el juego: Azahar responde pero la partida todavía no está cargada.
```

El jugador cerró el juego. **Esa marca vive en la memoria del emulador**, no en el fichero de
partida, así que sobrevive solo si se guarda dentro del juego. Al cerrar sin guardar, la RAM se va
y la partida sigue teniendo el Latias que tenía.

No es un fallo de código: es la frontera de siempre entre las dos puertas —memoria viva contra
fichero de partida— que la aplicación marca en cada pantalla desde el §66. Lo que faltaba es que
alguien la dijera **en el momento en que importa**, que es justo cuando la marca se escribe.

### Lo que se ha hecho

`GameLinkMonitor` lanza ahora `DeathMarked` al conseguir la transformación, y HOME lo enseña por el
mismo canal verde que los premios automáticos del §68:

> «Latias marcado como caído en el juego. Está solo en la memoria: guarda dentro del juego para que
> quede, o escríbelo en la partida desde MANTENIMIENTO.»

Nada más. La herramienta que lo arregla ya existía —MANTENIMIENTO → **CONVERTIR A LOS CAÍDOS EN
SHEDINJA**, que escribe la partida y es permanente— y su propio texto ya explicaba esto mismo; lo
que no había era ningún camino desde la muerte hasta ese botón.

### Medido en la partida del jugador

Pulsando «MIRAR CUÁNTOS HAY», que no escribe nada: **sin marcar 2, ya son Shedinja 5, no están en la
partida 0**. O sea que de las siete muertes de la run, cinco ya están selladas y dos siguen enteras,
una de ellas el Latias.

### Lo que NO se ha hecho, y por qué

Convertirlos solo, al cerrar el juego. Sería cómodo y encajaría con el precedente del §68, donde un
premio pasó a entregarse sin pulsar nada. Pero aquel **añade** Super Balls y esto **destruye un
Pokémon** en el fichero de partida, de forma permanente. Un botón que el jugador pulsa cuando quiere
es la respuesta correcta para eso; lo que estaba mal era que nadie le dijera que existe.

## 90. Los PS no se pueden clavar a cero: medido contra el juego (2026-09-04)

El jugador preguntó si se puede dejar al Pokémon **tal como está** —sin convertirlo en Shedinja— y
que se quede muerto para siempre: que aunque lo curen en el Centro siga en rojo. La respuesta es
**no por la vía obvia**, y ahora está medido en vez de supuesto.

### La prueba

Con la partida cargada y el equipo en `0x330128E4`:

| paso | resultado |
|---|---|
| leer el hueco 0 | Gyarados **131/131** |
| escribir 7 PS | **un solo byte**, offset `0xF0` |
| releer | Gyarados **7/131** |
| esperar quince segundos con el juego corriendo | sigue en **7/131**: el juego no lo pisa |
| abrir el menú del equipo | **131/131**, barra verde llena |

O sea: la escritura entra, se queda, y **el juego no la mira**. Los PS que pinta la pantalla no
salen de esa copia.

Encaja exactamente con el §53. La copia autoritativa es la de salto `0x1E4` y guarda las
estadísticas de combate en otro sitio; lo que sí obedece la copia de `0x104` es el **bloque
cifrado** —especie, mote, nivel—, que es justo lo que escribe el marcador de muerte, y por eso ese
sí se ve en pantalla y el Shedinja aparece.

El comentario de `--write-hp` decía «ahora abre el menú del juego y comprueba si lo refleja». Se
programó, nadie fue a mirar, y el que fue a mirar lo escribió: ahora la respuesta está en el código.

### Lo que haría falta

Localizar dónde guarda los PS actuales la estructura de `0x1E4`. Es una investigación de las del
§22 —barrer, anclar, verificar en pantalla—, no un ajuste. Y una vez encontrada, lo que se
construiría encima es un **vigilante**, no un candado: la app volvería a poner el 0 cada vez que
viera vivo a un muerto, con las limitaciones de siempre —solo con la aplicación abierta y Azahar
respondiendo, y con una ventana de un segundo tras cada curación—.

### Un hallazgo que no se buscaba, y que resultó ser mío

En la misma lectura, el equipo en memoria decía **Tinkaton nivel 40** y la pantalla decía **42**.
Escribí aquí que la copia de `0x104` «puede ir por detrás del juego» y que de ella salen la
detección de muertes y los niveles del cap. **Eso era falso, y lo falso era la herramienta.**

`--peek` imprimía `pokemon.CurrentLevel`, que es el nivel **según PKHeX**. Un nivel no es un campo:
se deriva de la experiencia con la curva de la especie, y la tabla de PKHeX se acaba en la 807, así
que para Tinkaton (959) caía a Medium Fast y daba 40 con toda confianza. La aplicación no usa eso:
usa `GameLevels.Of`, que lee la curva del **mundo instalado**. Medido con las tres cifras juntas:

```
#959 Tinkaton  exp=68225  Nv(exp)=42  Nv(pkhex)=40  Nv(0xEC)=42
```

Cuarenta y dos por la curva del mod, cuarenta y dos en `Stat_Level`, cuarenta y dos en pantalla.
No hay copia retrasada y no había nada que arreglar en la aplicación.

### Lo que sigue siendo verdad

La marca de Shedinja **ya es muerte permanente**: `shuffleBaseStats` está en `false`, así que
Shedinja conserva su **1 PS máximo** del cartucho, y con nivel 1 y sin movimientos lo único que
puede hacer es Forcejeo, cuyo retroceso lo mata. Y la especie está sobrescrita, así que no vuelve a
ser lo que era. Lo que falló en la partida del jugador no fue eso, fue el §89: la marca vivía en la
memoria y el juego se cerró sin guardar.

## 91. Dos diagnósticos falsos, y los dos eran la sonda (2026-09-04)

Buscando dónde guarda el juego los PS aparecieron dos cosas raras que apunté como problemas de la
aplicación. **Las dos eran de la sonda**, y las dos por la misma causa.

`WorldLimits` es estado **global** y arranca con el techo del cartucho, 807. La aplicación lo sube
al arrancar leyendo la tabla del mod instalado —`InstalledWorld.Apply`—, y con eso los lectores en
vivo aceptan las 1025 especies y `GameLevels` usa las curvas de experiencia del mod. **La sonda no
llamaba a nada de eso**, así que corría con 807 y con las curvas de PKHeX.

De ahí salieron:

**«El barrido no encuentra el equipo».** El localizador necesita un segundo Pokémon válido a la
distancia del salto para confirmar una estructura, y el segundo del equipo era un **Ursaluna (901)**.
Rechazado por el techo, no hay salto, no hay copia. Mientras tanto la aplicación leía el equipo sin
inmutarse, y eso mismo debería haberme hecho sospechar antes de la herramienta y no del código.

**«Tinkaton va dos niveles por detrás».** Un nivel no es un campo: se deriva de la experiencia con
la curva de la especie, y la tabla de PKHeX se acaba en la 807. Para Tinkaton (959) caía a Medium
Fast y daba 40. Con las tres cifras juntas:

```
#959 Tinkaton  exp=68225  Nv(exp)=42  Nv(pkhex)=40  Nv(0xEC)=42
```

Es exactamente el fallo que `GameLevels` existe para evitar (§53), reaparecido en la única puerta
que no lo usaba.

### El arreglo

La sonda llama ahora a `InstalledWorld.ApplyQuietly` **antes de leer un solo byte**, y el fichero se
**enlaza** desde la aplicación en vez de copiarse: dos implementaciones de «cómo de grande es el
mundo» acabarían discrepando, que es justo lo que esto repara. Se enlaza y no se muda porque solo un
punto de entrada puede juntar GameLink y Randomizer —la regla de dependencias no deja que dos
hermanos se referencien— y este cargador necesita `WorldLimits` de uno y `PersonalEntry7` del otro.

Y `--peek` enseña ahora **los tres** números: el de la experiencia con la curva del mundo, el que
daría PKHeX, y `Stat_Level`. Cuando no coinciden, esa es la noticia.

Con eso, el mismo barrido que no encontraba nada encuentra **las dos estructuras**, incluida la
autoritativa de salto `0x1E4` en `0x33F80744`, que es lo que hacía falta para seguir con los PS.

### La lección, otra vez

Es el §65 con otro traje: **una herramienta de diagnóstico puede estar peor calibrada que el código
que diagnostica**, y cuando lo está no falla — contesta con seguridad. Lo que tendría que haberme
puesto en guardia estaba delante: la aplicación, al lado, leía bien lo que la sonda no encontraba.

## 92. Dónde NO están los PS del equipo (2026-09-04)

Con la estructura autoritativa por fin localizada —`0x33F80744`, salto `0x1E4`— se pudo buscar en
serio dónde guarda el juego los puntos de vida. **No se ha encontrado**, y lo que sí hay son tres
negativas medidas que acotan bastante el terreno.

El equipo del momento, con sus PS a tope: Gyarados **131**, Ursaluna **161**, Tinkaton **123**,
Houndoom **115**. Cuatro valores distintos, que es lo que hace falsificable cualquier candidato.

**1. Escribir `Stat_HPCurrent` en la copia de `0x104` no se ve.** Escritos 7 PS en el Gyarados —un
solo byte, offset `0xF0`—, releídos, y aguantan quince segundos sin que el juego los pise. La
pantalla del equipo seguía marcando 131/131 con la barra llena (§90).

**2. La estructura autoritativa no contiene ninguno de los cuatro valores.** Volcados los 484 bytes
de cada hueco y buscados los cuatro números como enteros de 16 bits: **cero apariciones**, ni
siquiera en la cola, que es la parte que no va cifrada. Comprobado además que el volcado se está
leyendo bien, porque el bloque sale cifrado como debe: el campo de especie da 64001 en vez de 130.

**3. No hay ninguna tabla con los cuatro a paso constante.** Barrida la memoria legible entera —heap
`0x08000000-0x10000000` y linear `0x30000000-0x40000000`— buscando cada valor por separado: 1269,
840, 2567 y 2264 candidatos. Cruzándolos, **ni un solo cuarteto** con los cuatro a la misma
distancia, con pasos de hasta 64 KB. Si los PS del equipo vivieran en un array de estructuras, ahí
tendría que haber salido.

### Qué queda

Dos posibilidades, y no se puede elegir entre ellas sin más medidas:

- **Están, pero no así**: en 32 bits, en un byte, cifrados, o en una región que este barrido no
  cubre.
- **No están**: el §53 ya observó que el nivel y las estadísticas de combate son campos
  **derivados** que el juego recalcula al entrar en combate. Si el menú también los recalcula al
  dibujarse, no hay ningún PS que clavar — habría que tocar lo que los deriva, o sea nivel, IV, EV o
  estadísticas base, y eso ya no es «dejar al Pokémon tal como está».

La vía que falta es la clásica y necesita al jugador: `Probe --scan 131`, **cambiar los PS dentro
del juego** —un golpe, una poción—, y `Probe --refine <nuevo>`. Los 1269 candidatos colapsan a un
puñado en dos pasadas. Es una sesión corta pero no es automática, porque el valor tiene que moverlo
el juego.

Hasta entonces, la respuesta a «dejar al Pokémon muerto sin convertirlo en Shedinja» sigue siendo
**no**, y ahora se sabe bastante mejor por qué.

## 93. Los PS se pueden LEER pero no clavar (2026-09-04)

El jugador dio lo que faltaba: un salvaje dejó a su Houndoom en **82** y una poción lo subió a
**102**, con máximo 115. Un valor que se mueve es lo único que permite refinar un barrido.

### Lo que se encontró

**La copia de `0x330128E4` sí lleva los PS de verdad, y el juego la mantiene al día.** Leída
después del cambio: **Houndoom 102/115**, exacto. Ninguna de las otras cuatro estructuras da un
número con sentido ahí —62905/24981, 4433/20887, 1333/20887—, así que de las cinco copias del
equipo **solo esa tiene una cola de estadísticas de combate poblada**.

Eso resuelve la mitad útil del problema: **PermaLocke puede leer los PS con fiabilidad**, y eso
abre la puerta a detectar una muerte por PS a cero en vez de por desaparición.

### Lo que sigue sin encontrarse

**Escribir ahí no manda.** Ya medido en el §90, y ahora se entiende mejor: esa copia es un espejo
que el juego refresca. El 7 que se escribió aguantó quince segundos porque no pasó nada; en cuanto
el juego tocó el equipo, lo pisó con el valor bueno.

**Y la fuente no está donde debería.** Tres negativas:

- Los cuatro máximos no aparecen en los 484 bytes de la estructura autoritativa (§92).
- No hay ningún cuarteto de los cuatro a paso constante en heap ni en linear.
- Buscando la pareja adyacente **(102, 115)** —actual y máximo, que es una firma mucho más rara que
  un número suelto— salen **cinco** sitios, y en ninguno de ellos aparecen los otros tres miembros
  a una distancia coherente.

**Y el refinado clásico da cero.** De las 2264 direcciones que valían 115 cuando Houndoom estaba
lleno, **ninguna** vale 102 ahora. O sea que la dirección donde vive ese dato **no es estable**.

Lo corrobora algo que se vio de paso: entre una lectura y otra el equipo **se reordenó** —Houndoom
pasó a ir primero— y el localizador pasó de encontrar **2 estructuras a encontrar 5**, con
`0x33F80744` conservando el orden viejo. Esas estructuras se crean, se mueven y se quedan rancias.

### Dónde queda

Un vigilante que reponga el cero necesita **escribir**, y para escribir hace falta una dirección
que siga siendo la buena un segundo después. Hoy no la hay. Que se pueda leer no es poco, pero no
es lo que el jugador pedía.

### Un error propio, dicho

Buscando en qué hueco estaba Houndoom escribí PS en tres estructuras **cuya cola no está
identificada**, que es exactamente lo que el §53 prohíbe después del Ledyba. También escribí 102 en
un Tinkaton por confundir el hueco: el equipo se había reordenado entre dos lecturas y el índice 3
ya no era quien yo creía. Se restauró a 123 en el acto, las cinco estructuras siguen leyéndose con
sus especies y sus PID correctos, y el juego se ve normal. Pero la regla existía y me la salté con
un comando de diagnóstico, que es justo donde es más fácil saltársela.

## 94. Cómo lo hace BxnnyLocke: desde DENTRO del emulador (2026-09-04)

El jugador insistió: en la referencia la muerte permanente «va perfectamente». Se volvió a mirar, y
la conclusión anterior —«BxnnyLocke no toca la memoria del emulador»— **era falsa**. No la toca
desde su aplicación: la toca desde **su propio Azahar parcheado**.

Todo lo de abajo sale de leer las cadenas de sus binarios, que es análisis de arquitectura y de
formatos, lo que la regla del repositorio permite. **No se copia su código.**

### La prueba

Su `azahar.exe` está compilado desde `X:\BxnnyLocke\AzaharEdit\` y lleva **dos módulos que el
Azahar original no tiene**:

**`poke_export.cpp`**, que es donde vive la muerte permanente:

```
poke_export: OnBattleStarted()
poke_export: OnBattleEnded()
poke_export: OnBattleEnded convirtiendo slots muertos en Shedinja: {}, {}, {}, {}, {}, {}
poke_export: PID {} anadido a la watchlist de Shedinja
poke_export: PID {} ya es Shedinja en slot {}, check {}/{}
poke_export: FALLBACK - PID {} convertido, check {}/{}
poke_export: FALLBACK - PID {} sigue pendiente
poke_export: el juego reescribio
poke_export: Error desencriptando Battle Stats
poke_export: Escribiendo en memoria levelCapAdjustment
```

**`poke_capture.cpp`**, que es la otra mitad:

```
poke_capture: MAPA ACTUAL: {}
poke_capture: loadVisitedRoutes / saveVisitedRoutes
poke_capture: RUTA YA VISITADA
poke_capture: POKEMON YA CAPTURADO (LINEA EVOLUTIVA)
poke_capture: RemovePokeBalls() / RestorePokeBalls()
```

Y en `Emulador/user/rtp/p/shd.pk7` hay un **Shedinja ya preparado** que el emulador inyecta tal
cual, con conversión manual como respaldo.

### Qué hacen, dicho en una línea

Enganchan **el final del combate** dentro del emulador, convierten ahí mismo los huecos caídos, y
apuntan el PID en una **lista de vigilancia**: siguen comprobando cada uno, detectan cuándo **el
juego reescribió** el hueco, y lo vuelven a convertir. Además saben descifrar y volver a cifrar el
bloque de **Battle Stats**, que es justo la estructura que desde fuera no se ha podido tocar (§92).

### Por qué lo nuestro no puede

PermaLocke es una aplicación **externa** que pregunta por el RPC una vez por segundo. Con eso:

- no hay ningún momento «fin del combate»: se mira cuando toca, no cuando pasa;
- no se ve la estructura de combate, solo copias del equipo, y la que manda no se deja escribir
  (§90, §92, §93);
- y cuando el juego reescribe un hueco, no hay nada que lo detecte ni que reintente.

Que sea la misma idea —convertir en Shedinja— con el mismo resultado en pantalla escondía que el
sitio desde donde se hace lo cambia todo.

### Lo que abre

PermaLocke **ya tiene su propio fork** de Azahar (`Emulator/`, de `github.com/Grenin430/azahar`, con
el parche de búsqueda nativa del §22). O sea que este camino está abierto, y es el mismo. Lo que
haría falta es un módulo propio en ese fork: enganchar el fin de combate, mantener una lista de PID
y reaplicar cuando el juego pise el hueco.

Y de paso resolvería, por la misma puerta, tres cosas que desde fuera están muertas o apagadas:

| problema | estado hoy | desde dentro |
|---|---|---|
| zona actual del jugador | **muerta** desde el §55 | `MAPA ACTUAL` lo sabe |
| regla de las Poké Balls | implementada y **apagada** (§24) | `RemovePokeBalls` funciona |
| muerte permanente | se pierde si no guardas (§89) | enganchada al fin del combate |

No es una tarde de trabajo: es compilar y mantener un fork del emulador. Pero es la respuesta
honesta a «cómo lo hacen ellos», y es que **no lo hacen desde fuera**.

## 95. El emulador mantiene la marca de muerte (2026-09-05)

El §94 estableció que la referencia no hace nada de esto desde su aplicación: lo hace desde su
Azahar parcheado, con una lista de vigilancia y reintentos. PermaLocke ya tenía su propio fork
—`github.com/Grenin430/azahar`, con los parches de `NEW_LINEAR_HEAP` y `SearchMemory`—, así que el
camino estaba abierto. Este es el parche 2, y **está en `master` y verificado**.

### Lo que hace

Un tipo de paquete nuevo, `WatchBlock` (6). PermaLocke le entrega al emulador una dirección, cuatro
bytes de etiqueta y el bloque que debe haber ahí; el emulador compara **cinco veces por segundo**
desde un hilo propio y lo repone cuando alguien lo ha deshecho.

El emulador se queda **tonto a propósito**: no cifra, no calcula checksums y no sabe qué es un
Shedinja. PermaLocke le da los bytes ya hechos, que son los mismos que hoy escribe de todas formas.
La etiqueta es la **constante de encriptación**, el único campo de una entrada de equipo que va en
claro, y si deja de coincidir el emulador **no toca nada**: es la disciplina del §53 —la identidad
por delante— y aquí importa porque el equipo se reordena, cosa que se vio pasar en mitad de una
sesión mientras se medía el §92.

Detalle del lado de PermaLocke que no es menor: los bytes se leen **en crudo** y se mandan tal como
están en memoria, **cifrados**. Pasarlos por `PK7` los descifraría, y lo registrado sería un bloque
que no aparece nunca en el juego — o sea que el emulador reescribiría el hueco con basura cinco
veces por segundo, que es bastante peor que no vigilar nada.

### Verificado, con números

| paso | prueba |
|---|---|
| el emulador es el del parche | `Azahar Version: 87ed55b` |
| el hilo arranca | `Block watcher started.` |
| PermaLocke manda la lista | `El emulador vigila 5 huecos de 1 caído(s)` |
| el emulador la acepta | `WatchBlock: 0x… tag 14EFCBBA, 260 bytes`, cinco veces |
| repone lo deshecho | byte `FA` → escrito `66` → releído **`FA`** |

Y lo dijo: `EnforceOnce:320: The game rewrote 0x330128E4; marker restored`.

**Lo que esa prueba no demuestra:** quien deshizo el bloque fue una escritura a mano, no el juego.
Para el vigilante es la misma operación, pero el caso concreto de «el juego lo reescribe al salir de
un combate» sigue sin observarse.

### Las dos cosas que hicieron fracasar la primera prueba

Ninguna era el parche, y las dos merecen quedar escritas porque son de las que se repiten:

**El binario que se instala no es el que se abre.** Se puso el build nuevo en `Emulator/` y el
jugador abre el emulador desde `Nuevo_azahar/`. Lo delató la versión del propio log —`cf46ecc`, el
build de agosto—, y la fecha de último acceso del fichero señalaba a la otra carpeta. Comprobar la
versión que el emulador imprime es más barato que suponer cuál se ha arrancado.

**Un log filtrado no es un log vacío.** Azahar trae `log_filter=*:Info RPC_Server:Error`, de modo
que el servidor RPC solo escribe cuando falla. Toda la verificación se apoyaba en una línea
informativa que ese filtro tira. `EnsureRpcEnabled` sube ahora esa palabra a `Info` —solo esa, el
resto del filtro del jugador se respeta—. Un plan de comprobación que depende de un mensaje hay que
comprobarlo también a él.

### Y el fallo propio

`RefreshWatchList` volvía **en silencio** cuando el emulador no aceptaba la lista. El jugador mató
un Pokémon, guardó, y no había una sola línea en ningún registro diciendo que la vigilancia ni
siquiera se había pedido. Ahora lo dice una vez, y dice qué falta. Un emulador sin el parche es el
caso normal — pero normal no es lo mismo que invisible, y este proyecto lleva ochenta secciones
tropezando con esa diferencia.

### Alcance

La lista cubre las muertes marcadas **durante esta sesión de la aplicación**, que es el caso que
estaba fallando. Y sigue siendo memoria: cerrar el juego sin guardar la pierde igual, como la de la
referencia. Lo permanente de verdad sigue siendo MANTENIMIENTO, que escribe la partida.

## 96. La lista de vigilancia, y dos veces el mismo error mío (2026-09-05)

El §95 dejó el parche funcionando y verificado. Ponerlo a trabajar en una partida de verdad destapó
tres cosas, **ninguna del parche y las tres del lado de PermaLocke**.

### Una comodidad no puede tumbar la detección de muertes

El jugador salió del juego al menú del emulador. Ahí el RPC deja de contestar un instante,
`ClearWatchList` lanzó `AzaharRpcException`, y como `EnsureWatchList` estaba **al principio** de
`InspectAsync` se llevó por delante el ciclo entero: la muerte que ocurrió después **no se
registró**.

Tres arreglos, la misma lección: va **después** de las muertes, va **envuelto**, y **la firma se
guarda solo si el refresco salió bien** — se guardaba antes de intentarlo, así que un intento
fallido quedaba anotado como hecho y la lista se quedaba vieja hasta que el equipo se moviera otra
vez.

### La lista se perdía al reiniciar

Vive en la memoria del emulador y PermaLocke solo la mandaba **en el momento** de una muerte, así
que bastaba cerrar y abrir cualquiera de los dos para quedarse con el Shedinja puesto y nadie
mirándolo. Medido: emulador reiniciado a las 03:15, aplicación a las 03:20, y el registro del
emulador sin una sola entrada mientras el Shedinja seguía en el hueco 0.

Ahora, al conectar, se adoptan los caídos que ya estén en el equipo. **Quién está muerto lo dice el
historial, por PID**, y no «ese parece un Shedinja MUERTO»: esta run ha tenido uno real que nunca
fue una muerte (§59), y leer el juego para decidirlo lo habría adoptado y apuntalado para siempre.

### Y el error que hice dos veces en la misma noche

`RefreshWatchList` daba por hecho que el muerto ocupa **el mismo número de hueco en todas las
estructuras**. No lo ocupa —guardan el equipo en órdenes distintos y una llega a quedarse con un
orden viejo—, y eso estaba medido en el §93, unas horas antes.

Se vio en las etiquetas del propio log del emulador:

```
WatchBlock: 0x3254EE60 tag DE5EBE08     ← otro Pokémon
WatchBlock: 0x330128E4 tag 14EFCBBA     ← el Shedinja
WatchBlock: 0x3002E558 tag DE5EBE08     ← otro
WatchBlock: 0x3002F0FC tag DE5EBE08     ← otro
WatchBlock: 0x33F80744 tag DE5EBE08     ← otro
```

Cuatro de cinco apuntaban a otro sitio. Es **la regla que yo mismo puse en el emulador** —comprobar
la identidad antes de escribir— olvidada al elegir qué direcciones mandarle: el guardia comprobaba
bien, y yo le daba mal la lista.

Ahora se **busca** el PID hueco por hueco en cada estructura, y además `Watch` lee el bloque y no
manda nada si el PID no coincide. Dos guardias en vez de uno. Después del arreglo, la lista queda en
**una sola entrada**, la del Shedinja, que es lo correcto: las demás estructuras ya eran escombros.

### No hubo daño, y el motivo importa

El emulador **no repuso nada en toda esa sesión**: cero `marker restored`. Aquellas tres direcciones
ya no tenían el equipo sino memoria reutilizada —especie 464, PS 2895/18024, nivel 227—, así que la
etiqueta no cuadraba y las dejó en paz.

**La comprobación de identidad del lado del emulador tapó un fallo del lado del cliente.** Se
escribió pensando en un equipo que se reordena, y acabó protegiendo de algo bastante peor. Cuando
una guarda sirve para más de lo que se le pidió, conviene anotarlo: es la razón por la que se ponen.

### La otra vez, la misma noche

Midiendo los PS del §92 escribí 102 en un **Tinkaton** creyendo que era el Houndoom, porque el
equipo se había reordenado entre dos lecturas y el índice 3 ya no era quien yo creía. Y después
escribí 999 PS en un Gyarados por usar la herramienta que escribe para leer.

Tres veces el mismo error en una noche —dar por buena una **posición** en vez de comprobar la
**identidad**—, y las tres con el dato correcto ya medido y escrito. La regla no es «acuérdate»: es
que ninguna función que escriba en la partida debería aceptar una dirección sin un PID al lado.
`ApplyDeath` y `Watch` ya lo exigen; `--write-hp` no, y por eso costó un Gyarados a 999.

## 97. El Huevo Malo: PKHeX descifra el array que se le da (2026-09-05)

El jugador guardó la partida y su Shedinja **apareció como un huevo**. No era un huevo: era un
**Huevo Malo**, que es lo que el juego dibuja cuando una entrada de Pokémon no cuadra con su propia
firma de control. Ningún campo lo anuncia — no hay una bandera de «esto está roto», solo un
checksum que no da.

Distinguir las dos cosas era el primer trabajo, porque los arreglos son opuestos: un huevo de
verdad es la **bandera `IsEgg`**, que vive en el **bit 30** del entero de 32 bits que guarda los
seis IV, así que una escritura torcida en los IV puede incubar un Pokémon sin que nada más cambie.
Para eso está `Probe --huevo`, que pone los dos lados juntos.

### La medida

En **memoria** el Shedinja estaba perfecto: `#292 «MUERTO»`, firma correcta, PID `8EC2769F`. En la
**partida guardada**, el mismo hueco:

```
hueco 1: #13740 «MUERTO»
    EC=DE5EBE08  orden de bloques=16
    huevo=no  mote=sí
    checksum guardado=CEA8 → NO CUADRA (Huevo Malo)
    exp=2458524448  Nv(exp)=100  Stat_Level=226
    IV=27/5/19/28/8/21
    PS=2895/18021  PID=00AAA024
```

`Probe --huevo-diff` compara esa entrada con la captura que PermaLocke guarda antes de cada
escritura, descifrando los dos lados. De los cuatro bloques de 56 bytes que forman un PK7:

| Bloque | Estado |
|---|---|
| A — especie, PID, experiencia | **basura** |
| B — mote, movimientos, IV | exactamente lo que se escribió |
| C — entrenador original | intacto |
| D — encuentro, entrenador | **basura** |

Y la basura de A y de D **compartía tramos largos** — la misma secuencia de quince bytes aparecía
en los dos, a 168 bytes de distancia, que son tres bloques justos y el mismo desplazamiento dentro
de cada uno. Eso no es una escritura equivocada: es texto en claro donde debería haber texto
cifrado, descifrado encima por quien lo lee.

### Los dos fallos, los dos en `AzaharGameWriter.Watch`

**Uno: PKHeX descifra el array que se le da, en el sitio.** La comprobación de identidad añadida en
el §96 hacía `new PK7(bytes)` sobre el mismo array que después se le entregaba al emulador. A
partir de esa línea, `bytes` ya no era lo que el juego tiene: era su versión **descifrada**. El
emulador estuvo estampando texto en claro sobre un hueco cifrado **cinco veces por segundo**, el
juego lo reponía, y vuelta a empezar — que es lo que sale en su log:

```
1474.16  The game rewrote 0x33F80744; marker restored
1475.57  The game rewrote 0x330128E4; marker restored
1477.57  The game rewrote 0x33F80744; marker restored
1478.57  The game rewrote 0x33F80744; marker restored
```

Al guardar, el juego leyó ese hueco en mitad de la reescritura y serializó una entrada con mitades
de dos momentos distintos. Lo humillante es que **el comentario de esa misma función avisaba de
este peligro exacto** — «pasarlos por PKHeX registraría un bloque que no existe en el juego, y el
emulador reescribiría el hueco con basura cinco veces por segundo»— y el §96 lo provocó al añadir
el `PK7`. Cuesta una copia de una línea. Un comentario que describe un peligro no lo impide; lo
impide el código.

**Dos: se registraban 260 bytes.** Solo los 232 primeros son el Pokémon. Lo que sigue es la cola de
equipo, que en las estructuras autoritativas pertenece a algo que el juego actualiza sin parar, así
que el guardia veía diferencia **siempre**. El §53 ya había prohibido escribir más allá del bloque
cifrado en las estructuras cuya cola no está identificada; el guardia también tiene que obedecerlo.
Todo lo que la marca de muerte toca —especie, mote, movimientos, nivel— vive dentro de esos 232, y
el checksum responde por ellos.

### La prueba

`WatchBlockTests` levanta un emulador falso que **apunta lo que se le registra**, y comprueba las
dos mitades. Verificado que falla con el código viejo, que es lo único que hace que una prueba
signifique algo: `Expected 232, Actual 260`, y el contenido distinto a partir del byte 8 — el
primero que va cifrado.

### El arreglo de una partida ya rota

Un Huevo Malo **no se repara en su sitio**: todo lo que hay dentro se lee como ruido, así que no
hay nada que corregir. Lo que lo hace posible es que PermaLocke captura los bytes exactos del hueco
antes de cada escritura, así que el Pokémon que había está en disco, íntegro. `Probe --huevo
--arreglar <captura>` le vuelve a aplicar el `DeathMark` de siempre y lo mete en el hueco.

La identidad va por la **constante de encriptación** y no por el PID, y es la única excepción del
proyecto a la regla del §96. No es un descuido: el PID vive dentro del bloque roto y se lee como
ruido; los cuatro bytes del principio van en claro y sobreviven a lo que le pase al resto. Son lo
único de un Huevo Malo que sigue significando algo. Y se niega a tocar un hueco cuya firma esté
bien, porque un arreglo que también puede pisar a un Pokémon sano no es un arreglo.

Con `--probar` va sobre una copia, y se comprobaron las dos cosas antes de tocar la partida: que
reconstruye (`#292 «MUERTO» Nv 1 PID 8EC2769F firma OK`) y que **una captura de otro Pokémon la
frena** (`las constantes de encriptación no coinciden. No se escribe`).

---

## §98 · Los PS del equipo: dónde NO están, medido siete veces (2026-09-06)

El jugador pidió lo que el §92 había dejado a medias: dejar a un Pokémon muerto **siendo él**, a
cero PS, en vez de convertirlo en Shedinja. La respuesta, después de una noche de medidas, es que
**no se puede desde fuera**, y esta sección existe para que nadie vuelva a empezar de cero.

### Lo que decide, y es una sola medida

El espejo de `0x330128E4` **sigue los PS con precisión perfecta**: se leyó 118, luego 128, luego
120, siempre lo que marcaba la pantalla. Eso invita a pensar que es el almacén. No lo es.

Se le escribió **120** por el camino correcto —descifrar, cambiar el campo, volver a cifrar,
releer— con el Gyarados a 128 de 131. Y entonces las tres medidas juntas:

| Medida | Valor |
|---|---|
| memoria justo después de escribir | **120** |
| pantalla, con el menú del equipo abierto | **128** |
| memoria **después** de abrir el menú | **120** |

La tercera es la que contesta, y es la que el §93 no tomó. El juego **ni lee de ahí ni lo
corrige**: `0x330128E4` es un espejo de **una sola dirección**, un buffer de guardado. El §93
concluyó bien por el motivo equivocado —escribió un byte en claro sobre un campo cifrado— y yo
corregí el motivo y me quedé con la conclusión contraria sin comprobarla. Lo que había que
comprobar era la pantalla.

### Todo lo demás, excluido con su medida

Con los valores reales del equipo, no inferidos:

| Hipótesis | Cómo se midió | Resultado |
|---|---|---|
| u16 en claro que siga a los PS | barrido de 400 MB, dos avistamientos cruzados | solo la barra de vida |
| daño recibido (`max - actual`) | misma pasada, valor derivado | nada |
| valor desalineado, o de un byte | barrido a paso 1 | nada |
| tabla del equipo a paso constante | los cinco en orden de hueco, paso hasta 8192 | nada |
| los seis valores de uno, juntos | ventana de 64 bytes, con los valores reales | nada |
| lo mismo **con el menú abierto** | idéntico, dibujándose en pantalla | nada |
| cualquier offset de las entradas | en crudo y **descifradas**, los seis a la vez | nada |
| otra copia del Pokémon | por su constante de encriptación, en toda la memoria | **solo 3** |

Las tres copias son: el espejo, la estructura de salto `0x1E4` —cuya cola son cabeceras del
asignador, marcas `DU` y `RF`— y un **objeto de gráficos** en `0x3048xxxx`, que empieza por la
constante y sigue con once punteros a texturas y *shaders*. Ninguna es un almacén vivo.

### Por qué ninguna búsqueda podía encontrarlo

Dos cosas, y las dos son estructurales:

Las estadísticas de un PK7 de equipo van **cifradas** igual que el resto — PKHeX cifra la cola en
una segunda pasada con la misma semilla—, así que en pantalla 118 y en memoria `EF A6`. Un barrido
por el número no puede verlas por muy ancho que sea.

Y lo que el juego usa **no vive lo suficiente**. Si descifra al Pokémon en una pila para dibujar la
barra y lo tira, una pasada que tarda 30 s en recorrer 400 MB no lo va a pillar nunca, y si lo
pillase la dirección no valdría para el fotograma siguiente. Encaja con todos los negativos, con
que las estructuras aparezcan y desaparezcan entre pasadas —cinco un minuto y dos al siguiente— y
con que lo único en claro sea la barra de vida, que es pintura: `0x30020634` guarda actual y máximo
como u32, el valor **anterior**, y los porcentajes `118/131*100 = 90,08` y `108/131*100 = 82,44`
con un `100.0` al lado. Eso es la interpolación de la barra al animarse, no un almacén.

### La vía que queda

No es buscar más: es **preguntárselo al emulador**. El fork ya intercepta memoria, así que un
punto de observación de **lectura** sobre el espejo diría en una frase si el juego lo lee alguna
vez, y otro sobre el origen de la barra daría la cadena entera. Es trabajo del tamaño del §95, no
un ajuste.

Mientras tanto **el Shedinja se queda**, porque es lo que se sabe que se ve. Se llegó a cambiar la
muerte automática por «dejarlo a cero PS» y se revirtió el mismo día, en cuanto la pantalla dijo
que no. Un marcador que no se ve es peor que uno feo.

### Lo que sí queda hecho

`PartyStats.AreHere` decide **midiendo** si la cola de una entrada son de verdad las estadísticas.
Antes se preguntaba el salto —`0x104` significaba «aquí están»— y eso es un proxy, no el hecho: las
**dos** estructuras de salto `0x104` leyeron, en el mismo segundo, `118/131` y `42649/10902`. O sea
que el cap de nivel y la marca de muerte llevaban tiempo escribiendo más allá del bloque cifrado en
bytes que nadie ha identificado, que es justo lo que el §53 prohíbe desde que un «por si acaso»
evolucionó un Ledyba. El ancla es que un Pokémon de equipo **lleva el nivel dos veces** —como
experiencia dentro del bloque cifrado, avalada por el checksum, y como `Stat_Level` en la cola— y
donde la cola es buena coinciden; con los seis valores en rango, la casualidad tendría que darse
siete veces a la vez. Seis pruebas, ancladas a esos números reales.

Y `AzaharGameWriter.SetHp` / `Faint`, que escriben los PS bien aunque el juego no los mire, más
`Probe --ps`, `--ps-tabla`, `--ps-bloque`, `--ps-copias`, `--ps-cola` y `--ps-escribir`.

### Tres errores míos de esa noche, porque los tres se repiten solos

**Construí la aguja con basura.** El primer barrido buscó un Gyarados de «42649 de 10902», leídos de
una copia que no guarda estadísticas. Cero aciertos, y ese cero no significaba nada. Es el §53 otra
vez: un campo solo vale donde la estructura está identificada.

**Usé una herramienta justo para lo que su propio comentario prohíbe.** `SearchMemory` devuelve como
mucho **255 aciertos por llamada** y lo dice en su resumen: «nunca para demostrar que algo no está».
Las cifras lo gritaban —510 y luego 765, dos y tres veces 255 clavados— y las leí como medidas.
Nada de lo de aquí la usa: el barrido lee la memoria y compara de este lado.

**Y comparé el hueco cero contra todo.** La primera criba solo miraba el primer hueco de cada
estructura, así que habría dicho «no está» de un Pokémon sentado en cualquier otro sitio. La
ausencia de evidencia no vale nada cuando la búsqueda no podía encontrarlo.

Y uno de bulto: pasé horas viendo «huecos 0, 1, 3, 4, 5» en un equipo de cinco sin preguntarme por
qué faltaba el 2. Era un **Huevo Malo** del §97 que la partida guardada arrastraba desde el día
anterior, y todas mis herramientas lo daban por «hueco vacío» porque PKHeX no lo puede leer. De ahí
que `--huevo --arreglar` gane `--hueco`: iba fijo en el primero, que es donde pasó la primera vez, y
una herramienta de reparación que solo sabe arreglar el sitio del estreno no sirve la segunda.

### §98 bis · La otra puerta sí está abierta: el fichero de partida

Cerrada la memoria, quedaba una vía sin probar, y funciona. **Medido**: con el juego cerrado se
escribieron **55** PS al Tinkaton en el fichero de partida, se releyó el fichero (55 de 123), el
jugador cargó y el menú del equipo decía **55**. El juego lee los PS del **fichero** al cargar,
aunque no lea nunca el espejo de memoria.

Ojo con lo que **no** era prueba: se propuso esta vía apoyándose en que el Mudsdale muerto del
jugador está a `0/13` en la partida. No demuestra nada — ese Mudsdale se murió de verdad, así que
ese cero lo puso el juego. Plausible no es medido, y esa distinción es la moraleja de todo el §98.

`SaveFainter` deja a los caídos de la run a **0 PS siendo ellos**: no toca especie, mote,
movimientos ni nivel. Por PID y por nada más — nunca «este parece un marcador», que esta run tuvo un
Shedinja llamado MUERTO que jamás fue una muerte (§59). El estado se va con los PS, porque un
Pokémon en el suelo no está además envenenado, y dejarlo sería dejar la partida en un estado que el
juego no produce nunca. Copia previa, `InPlace` por lo que costó el §42, y **relectura del fichero**
comprobando que ninguno sigue en pie antes de dar nada por bueno.

En MANTENIMIENTO, en dos pasos como el resto: mirar cuántos, y solo entonces se enciende el botón
que escribe. Convive con CONVERTIR A LOS CAÍDOS EN SHEDINJA y la pantalla dice en qué se
diferencian, que es lo único que hay que elegir: **el Shedinja no se puede deshacer y esto sí**. Un
Centro Pokémon revive a un caído sin PS, y no hay forma de impedirlo desde fuera —para eso haría
falta reponerlo en memoria, que es justo lo que el §98 midió que no llega—. Así que esto es «muerto
entre sesiones», y así está dicho en la tarjeta.

Lo que **sigue sin hacerse**, a propósito: la muerte automática **no** ha pasado a usar esto. La
marca del vigilante sigue siendo el Shedinja en memoria, porque es lo que se ve en el momento; esto
se pasa al terminar la sesión, con el juego cerrado. Cambiar lo automático es otra decisión y no se
toma de madrugada, el mismo día en que ya se cambió una vez sin verificar y hubo que revertirlo.

Prueba y herramienta: cinco pruebas unitarias sobre un save construido en memoria, y
`Probe --tumbar [--probar] [--pid <hex>] [--ps <n>]`. El ensayo va sobre una **copia** en el
temporal y no abre la partida para escribir; `--pid` solo vale en ensayo, para demostrar el viaje
completo por el fichero cuando los caídos de la run no están en el equipo —que era el caso ese día,
12 caídos y ninguno en pie—. Y `--ps` **rechaza el cero** a propósito: escribir un 0 para comprobar
haría que el vigilante lo leyera como una muerte y cobrase 25 puntos por algo que no ha pasado. Una
comprobación que le cuesta puntos al jugador no es una comprobación.

### §98 ter · Fuera el Shedinja

El jugador lo zanjó en una frase: «quita todo lo del shedinja, quiero olvidarme de esa mecánica».
Así que la marca de muerte deja de ser un Pokémon distinto y pasa a ser **quedarse sin PS**.

No fue borrar código, fue **cambiar una definición**. `DeathMark` era ya la única implementación de
«qué le pasa a un cadáver» —lo decía su propio comentario, y por eso lo usaban tanto la ruleta como
el escritor de partida—, así que cambiarla ahí las cambió todas a la vez. `Apply` pone los PS a cero
y limpia el estado; `IsMarked` pregunta por los PS. Un solo sitio, y ninguna de las dos rutas pudo
quedarse atrás.

**Lo que se perdió al cambiar, dicho porque es real:** el Shedinja se podía marcar en cualquier
sitio y esto **solo vale en el equipo**. Un Pokémon en caja no lleva estadísticas de combate (§32),
así que escribirle un cero no cambia nada que el juego lea y saldría entero. De ahí `WorksIn`, que
el escritor tiene que preguntar: los de caja se **cuentan y se dicen** en vez de saltarse en
silencio, porque una marca que aparenta funcionar es peor que no tener ninguna. Sale en la tarjeta,
en el diálogo de confirmación y en una prueba.

**Y se hace sola al cerrar el emulador**, que es el único momento en que se puede escribir la
partida. La marca vivía en memoria y se ponía en el instante de morir; ahora vive en el fichero, y
el fichero solo se deja escribir con el juego cerrado, así que el monitor la pone en la transición
de conectado a desconectado. El botón de MANTENIMIENTO se queda para cuando eso no ocurrió —la
aplicación estaba cerrada, o la escritura falló—, y como es idempotente los dos caminos pueden
pisarse sin hacer nada dos veces. La comprobación de «¿está el juego abierto?» se movió **dentro**
del escritor: desde que quien llama es un temporizador y no una persona leyendo un aviso, dejarla en
el que llama era dejarla sin poner.

**Y se fue con él toda su maquinaria.** El vigilante del emulador —`WatchBlock`, la lista, el
`Watch` del escritor y sus pruebas— existía para sostener el Shedinja en memoria cinco veces por
segundo. Sin marca en memoria no tiene usuario, y encima es el código que corrompió una partida en
el §97, así que quedarse con él apagado era guardar un arma cargada. El parche sigue en el fork y
documentado en §95-97; el número de paquete se queda en el enum con una nota de que nadie lo manda.
De paso se van `DeathTransform` y `ApplyDeath`, y `AzaharGameWriter.SetHp` se queda **solo como
sonda**, con su resumen diciendo que el juego no lo lee — que es lo que costó averiguar.

Con esto el ciclo del monitor pierde tres pasos —marcar, adoptar caídos, refrescar la lista— y unas
340 líneas. Ninguno hacía falta: los tres estaban al servicio de una marca que el juego nunca miró.

### §98 quater · Y el espejo no se lee ni montando un combate

El jugador pidió lo que tiene sentido: que un muerto esté muerto **todo el rato**, dé igual cuántas
veces lo reviva un PNJ, comprobado al cerrar un menú o al entrar y salir de combate. Su instinto
apuntaba a una hipótesis que quedaba sin comprobar: que el juego se cargue el equipo al arrancar y
solo vuelva a leer el espejo en momentos concretos —y los momentos que él nombró son exactamente los
candidatos—.

Se midió. Se escribieron **40** PS a un Ursaluna de 161, se entró en un combate salvaje y se huyó:
**161 antes y 161 después**. El espejo no se lee ni siquiera cuando el juego reconstruye el equipo.

De paso, una lección sobre cómo se elige un sujeto de prueba: la primera pasada se hizo sobre el
Mudsdale, que **ya estaba a 0 PS**. El jugador lo cazó en una frase —«da igual que pasara que al
salir seguiria con 0»—, y tenía razón: escribir un cero sobre algo que ya vale cero no puede
distinguir ninguna hipótesis de ninguna otra. La prueba necesitaba un número raro en un Pokémon
sano, y además uno que **no combatiera**, porque si pelea recibe daño de verdad y el número deja de
significar nada.

Con eso se agota lo preguntable desde fuera, y el diagnóstico pasa al emulador: el **parche 3**,
escrito en `docs/fork/03-donde-vive-el-ps.md`. La idea es que el juego **escribe** los PS buenos en
ese espejo, así que hay una instrucción suya que los copia desde donde de verdad viven; si el
emulador entrega el PC y los dieciséis registros de esa escritura, PermaLocke prueba cada registro
como dirección y se queda con el que lea como un Pokémon con los PS correctos. Mecánico, sin
desensamblar a mano y sin elegir a ojo.

El lado del cliente ya está hecho y probado contra un servidor falso: `WatchWrites`, `ReadWriteLog`
y `Probe --escrituras`, que **distingue** «el juego no ha tocado esos bytes» de «el enganche no ve
las escrituras porque el JIT las hace por su cuenta» — dos causas con el mismo síntoma, y confundir
un tope de herramienta con una medida es justo lo que costó una noche en el §98. Las pruebas del
registro incluyen una respuesta que dice llevar más entradas de las que manda, y exigen que las
enteras se devuelvan y el resto se cuente como pendiente en vez de inventarse.

**Nada del parche está aplicado ni compilado**, y tiene una suposición grande dicha en su documento:
el JIT de Azahar escribe memoria sin pasar por `MemorySystem`, así que el enganche depende de marcar
el rango con `RasterizerMarkRegionCached` —el mecanismo que Citra ya usa para la GPU— y, si no
basta, de desactivar el JIT mientras se diagnostica. Y puede terminar diciendo que **no se puede**:
si el juego descifra al Pokémon en una pila, lo usa y la tira, no hay dirección que clavar. Eso
también sería una respuesta, y hoy no la tenemos.

---

## §99 · Dónde vive el PS de verdad: `0x1E4 + 0x158` (2026-09-06)

**Encontrado y verificado contra la pantalla.** Se escribió **77** en esa posición y el menú del
equipo dijo 77. La estructura de salto `0x1E4` guarda las estadísticas de combate en el offset
**`0x158`** de cada entrada, cifradas, y **esa es la que el juego lee**.

### Cómo se llegó

Toda la noche del §98 fue negativa porque se buscaba desde fuera. Lo que la cerró fue el parche 3
del fork: un punto de observación de escritura sobre los dos bytes de los PS en el espejo. Al
**guardar la partida**, el emulador anotó:

```
pc 0x00322FAC  escribio 4 bytes en 0x330129D4  valor 0x2A15A6FE
r0=330129CC  r1=33F808AC  r4=CEC1641C  r6=330128E4  r7=330129CC  r12=2A15A6FE
```

Y el código en esa dirección, desensamblado, es una rutina de dos copias:

```
cmp r6, #0 · ldr r1, [r4, #8]  · mov r2, #232 · mov r0, r6 · bl memcpy
cmp r7, #0 · ldr r1, [r4, #4]  · mov r2, #28  · mov r0, r7 · bl memcpy
```

**232 y 28**: el bloque del Pokémon y la cola de estadísticas de un PK7 de equipo, exactamente. La
segunda copia es la que disparó el aviso, así que su origen —`r1`, ya avanzado por el `memcpy`— era
`0x33F808AC`, y los bytes escritos aparecían en `0x33F808A4`. Restando el desplazamiento dentro de
la copia, el bloque empieza en `0x33F8089C`, que es `0x33F80744 + 0x158`.

### Lo que esto corrige

El §53 dijo que la copia autoritativa «guarda las estadísticas de combate en otro sitio» y ahí se
quedó, sin decir dónde, durante meses. Y `PartyStats.AreHere` la rechazaba **por eso mismo**:
buscaba en `0xF0`, que es donde las pone un PK7, y ahí no están. La comprobación no estaba mal,
estaba incompleta — y su consecuencia fue que todo lo que escribía PermaLocke se limitaba al espejo,
que es justamente el sitio que no se lee.

También corrige lo que yo repetí toda la noche: que el espejo «sigue los PS clavados». No los sigue.
Es una **foto que se rellena al guardar**, y entre guardado y guardado se queda vieja — medido: la
pantalla decía 103 y el espejo seguía en 128. Coincidía las tres primeras veces por casualidad de
cuándo miré.

### Cómo se escribe ahí

Las estadísticas están cifradas con la misma tirada que la cola de un PK7, y se comprobó por
partida doble: los bytes de `0x1E4 + 0x158` y los del espejo en `+0xF0` son **idénticos**. Así que
no hace falta reimplementar el cifrado: se lee el bloque de 232 bytes de `+0` y los 28 de `+0x158`,
se juntan en un PK7 de equipo de 260, PKHeX lo descifra, se cambia el campo, se vuelve a cifrar y
se escriben los últimos 28 bytes en `+0x158`.

Para la comprobación de esta noche se hizo aún más simple, derivando la clave de un solo hueco:
texto en claro 103 contra cifrado `A6FE` da `0xA699`, y con eso 77 se escribe como `A6D4`. Sirve
para una prueba; para el código va la vía de PKHeX, que no depende de conocer el valor de antes.

### Lo que queda por decidir

Clavarlo con el vigilante del emulador (§95) tiene un problema de identidad: `WatchBlock` toma
como etiqueta los **cuatro primeros bytes del bloque**, y en `+0x158` eso es el estado alterado
cifrado, no la constante de encriptación. Vigilar desde `+0` para que la etiqueta sea la constante
obligaría a guardar también los bytes entre `0xE8` y `0x158`, que nadie ha identificado y que el
juego toca — y guardar bytes que cambian solos es exactamente lo que corrompió una partida en el
§97. Así que o el parche 4 separa la dirección de la etiqueta, o lo repone PermaLocke desde su
sondeo de 1 Hz, que no necesita tocar el emulador y no puede corromper nada.

### §99 bis · Y con eso, muerto es muerto mientras juegas

Encontrada la dirección, lo que faltaba era barato. `AzaharGameWriter.SetLiveHp` escribe los PS
donde el juego los lee: junta los 232 bytes de `+0` con los 28 de `+0x158`, deja que PKHeX lo
descifre, cambia el campo, vuelve a cifrar y escribe **solo la cola y solo los bytes que difieren**
—nada entre `0xE8` y `0x158`, que es territorio sin identificar y lo que se toca ahí cuesta un Huevo
Malo (§97)—. Copia previa, PID por delante y relectura descifrada antes de dar nada por bueno.

`GameLinkMonitor.KeepFallenDownAsync` corre **en cada vuelta**, una vez por segundo, y devuelve al
suelo a cualquier caído de la run que tenga PS. Cada vuelta y no solo cuando cambia el equipo,
porque curar en un Centro Pokémon no mueve la plantilla; y no cuesta nada cuando no hay trabajo,
porque el HP ya viene en la instantánea.

**No se le da al vigilante del emulador**, y es una decisión. `WatchBlock` toma los cuatro primeros
bytes del bloque como identidad, y en `+0x158` eso es el estado alterado, no la constante de
encriptación. Vigilar desde el principio de la entrada obligaría a guardar también los bytes que el
juego mueve por su cuenta, que es literalmente lo que corrompió una partida. Una vez por segundo
desde la aplicación no puede corromper nada, y para lo que se pide —que a un muerto no lo revivan—
sobra.

Lo que sigue siendo verdad y conviene no olvidar: esto vive **mientras la aplicación esté abierta**.
Con ella cerrada, el juego cura y nadie lo deshace hasta la próxima vez. Y la marca en el fichero de
partida (§98 bis) sigue haciendo falta para que aguante entre sesiones.

**Verificado en la partida real (2026-09-06):** el jugador curó al Mudsdale en un Centro Pokémon y
volvió al suelo solo, con su aviso en HOME. Muerto es muerto mientras juegas.

Y costó una corrección por el camino que vale más que el propio cableado: **hay tres estructuras de
salto `0x1E4` y no dicen lo mismo**. Leídas en el mismo segundo, una daba el Gyarados a 128, otra a
103 y la tercera a 131 — dos fotos viejas y la viva. La aplicación tenía guardada la más rancia de
una sesión anterior y la revalidó «sin barrer», así que la primera versión de esta pasada miró a un
Pokémon recién curado, leyó cero y decidió que no había nada que hacer.

Nada las distingue por su forma, así que **ninguna se elige**: se pregunta a todas, por PID y hueco
a hueco, y a la que tenga PS se los quita. Escribir un cero donde ya hay un cero no cuesta nada;
dejarse la buena sí. Es el §96 otra vez —la identidad por delante, nunca la posición— aplicado donde
faltaba, que era en de dónde se leía y no en dónde se escribía.

---

## §100 · La clase que faltaba, por tercera vez (2026-09-06)

El jugador peleó contra Francine —Plumeria— a nivel 46 con un Emboar y un Stunfisk, y no le salió
mega ni Pokémon extra. Es el entrenador 238, **clase 78 «Comandante Skull»**, y esa clase no estaba
en `clasesImportantes`. La **79**, que también es Francine, sí.

Y ahí está lo que importa, porque es la tercera vez con la misma forma. Antes fue la clase 222, y
después una revisión que añadió ocho más. Las dos revisiones se hicieron **leyendo nombres a mano**,
y por eso ninguna cazó esto: quien busca «Francine», la encuentra en la 79 y sigue. **Un personaje
con nombre repartido en varias clases** es invisible a una revisión por nombres.

Así que la revisión deja de depender de que alguien se acuerde. `RomTool clases <a/1/0/6>` lista las
clases que no están, ordenadas por lo que delata a un jefe —pocos entrenadores y equipo grande—, y
sobre todo cruza los **nombres de los entrenadores** contra los de las clases que ya cuentan. Esa
segunda comprobación saca la familia entera de un tirón:

```
clase  30  Entrenador          Tilo       (la 101 y la 102 estan)
clase  72  Director Æther      Fabio      (la 71 esta)
clase  78  Comandante Skull    Francine   (la 79 esta)
clase 221  Entrenador          Tilo
```

Añadidas 72, 78 y 221. La **30** se deja fuera: nueve entrenadores de los que ocho tienen el nombre
sin traducir y uno es Tilo, así que puede ser el rival de las primeras escenas o puede ser relleno,
y sin medirlo no se mete. Y el filtro de nombres ignora los que son solo puntos o `[~ n]`, porque
emparejaban clases de relleno con clases de relleno: sin eso salían cuatro falsos positivos.

### Arreglar la lista no arregla el mundo ya generado

`roles.json` decide cómo se randomiza **la próxima vez**. El mundo del jugador ya estaba hecho, y
volver a randomizar a mitad de run le cambiaría el mapa entero. Así que `RomTool parchear-megas
<romfs> <clases...> [--escribir]` parchea esos combates **donde están**, en el mod instalado.

Se puede porque una mega es una **forma** de su especie y no una especie aparte: dos bytes en su
sitio, el subfichero no cambia de tamaño. El Pokémon extra de esa misma tanda **no** entra, y no es
pereza: añade un miembro al equipo, lo que obliga a reempaquetar el GARC, y reempaquetar es la
operación que más caro ha salido aquí (§19, §47). Eso se queda para la siguiente randomización.

Es idempotente —un equipo que ya lleva una forma se deja— y se niega a tocar un entrenador cuyo
equipo no mida lo que su propia tabla declara, que es la comprobación que el §47 se ganó. La tirada
va sembrada con el id del entrenador, así que repetir el comando no reparte megas distintas.

Aplicado a los ocho combates de las tres clases, con copia previa y **relectura confirmando especie
y forma**. La copia se guarda **fuera** del mod, en `Randomized/copias-mod/`: dejarla dentro
significaría meter un fichero que no es del juego en una carpeta que LayeredFS mapea entera.

---

## §101 · Los aprendizajes, con las reglas de Universal Pokémon Randomizer (2026-09-06)

El jugador pidió comparar cómo randomiza la referencia los movimientos que se aprenden por nivel y
hacer lo que ella hace y aquí no. Lo que había era un sorteo **uniforme con reemplazo**: para cada
hueco, un movimiento al azar del catálogo enseñable, conservando los niveles. Nada más.

Lo que eso produce, **medido sobre el mundo que el jugador tiene instalado**:

| | antes | con las reglas |
|---|---|---|
| movimientos repetidos dentro de un aprendizaje | **263** | **0** |
| especies sin nada con que atacar al nivel 1 | **698 de 1329** | **0** |
| huecos que de verdad hacen daño | 48 % | 66 % |

Ese 698 es el número que justifica el trabajo entero: **más de la mitad de lo que capturas no puede
hacer daño el día que lo capturas**. En un Nuzlocke eso no es una molestia, es el encuentro tirado.

### Qué se ha traído, y qué no

**Sin repetir** dentro de un aprendizaje, y **un ataque garantizado en el último hueco de nivel 1**.
Ninguna de las dos es configurable, porque no son un gusto: son la diferencia entre un Pokémon con
cuatro movimientos y uno con uno.

**Una cuota de ataques de verdad**, por defecto el 30 % de la referencia, con «de verdad» definido
como ella lo define: potencia × golpes ≥ 100, o ≥ 50 con precisión de 90 o mejor. Un movimiento
flojo pero fiable cuenta y uno fuerte pero errático no.

**Físico o especial según el Ataque contra el Ataque Especial** del propio Pokémon, así que a un
bruto le tocan movimientos físicos. Y **sesgo de tipo opcional**, apagado por defecto porque en la
referencia es un modo aparte y no parte del sorteo normal.

Lo que **no** se ha traído: sus listas de movimientos prohibidos. Los Z ya estaban fuera por el §41
—PP = 1, medido, que sobrevive a un mod que añada movimientos—, y de las suyas la mayor parte cae
sola: los de KO fulminante y los de daño fijo tienen potencia 0, así que la regla de «buen ataque»
los deja fuera del saco de daño por sí misma. Aquí conviene una **corrección a lo que se dijo al
comparar**: la referencia no los prohíbe del todo, solo los saca del saco de ataques; siguen
pudiendo salir como movimiento normal, igual que aquí.

### Dos anclajes, porque suponer no vale

La categoría de un movimiento es **un byte** y nada dice qué valor es físico. Suponerlo pondría a
todos los atacantes físicos con movimientos especiales y **no fallaría nunca** — el §45 con otro
traje. Se ancla en dos movimientos que nadie discute, buscados **por su nombre** en el texto del
propio cartucho, y si los dos declaran la misma categoría, lanza.

Y la **precisión perfecta** no es 100: el juego escribe un valor propio. Se lee de Rapidez, que es
lo que hace la referencia, en vez de escribir 101 a mano.

Los dos anclajes se ganaron el sueldo el primer día: el ancla especial estaba escrita como «Ascua» y
el movimiento se llama **«Ascuas»**. La generación se paró en seco en vez de repartir un mundo con
la categoría al revés. Un error de tecleo que, sin el ancla, habría salido como «los aprendizajes
quedan un poco raros» y nadie lo habría atado a esto nunca.

### Cómo se comprueba

Siete pruebas fijan las reglas contra un catálogo inventado —incluidos los casos incómodos: un
Pokémon con más huecos que movimientos hay, y uno que no aprende nada al nivel 1—, y
`RomTool aprendizajes <a/0/1/3>` relee un mundo ya generado y cuenta las tres cifras de la tabla de
arriba. Las pruebas dicen que las reglas son correctas; el comando dice que están enchufadas, que
es otra cosa y hace falta igual.

---

## §102 · Cinco arreglos de pantalla, y una medida que no hizo falta arreglar (2026-09-07)

Cinco cosas que el jugador fue apuntando mirando la aplicación. Cuatro son de presentación; la
quinta resultó ser una pregunta, y la respuesta se midió en vez de prometerse.

### Los legendarios ya no dependían de la banda

La petición era «mueve todos los legendarios al banner BUENO y que el tier 5 tenga un 20% de ser
legendario entre todos los que hay». La primera mitad ya se cumplía —los tiers 1 a 4 tienen
`legendaryChance` a cero y solo BUENO tira el tier 5—, así que lo que la frase pedía de verdad era
la segunda: **el saco de legendarios se recortaba por banda como cualquier otro**, o sea a los de
más de 590. Eso dejaba fuera a las aves, los perros, los Tapu, los regis y Type: Null, que **no
podían salir nunca** sin que nada lo dijera. `GachaService.PoolOf(tier, legendary: true)` devuelve
ahora todos, y la probabilidad baja de 0,40 a **0,20**.

Lo que hay que cuidar al ampliar así un saco es el respaldo: la salida de emergencia para un saco
vacío ahora **solo va de legendario a normal**, nunca al revés. Al revés, un tier sin especies
propias podría dar un legendario en un banner que no los reparte.

Y la prueba que se cayó tenía razón en lo que protegía y no en cómo lo medía: comprobaba que el
saco de legendarios estuviera **vacío** en los tiers baratos, que es justo lo que se ha cambiado.
Lo que importa es que un banner barato no pueda dar un legendario, y eso lo sostienen dos hechos
distintos —su `legendaryChance` es cero y su saco ordinario excluye la bandera—, así que eso es lo
que fija ahora, más un segundo test con un legendario de 580 en la tabla que **falla si alguien
vuelve a recortar el saco por banda**.

### La cinta del gacha giraba en los dos sentidos

Era `AutoReverse = true`, y el comentario que lo defendía decía que un cambio de sentido cada dos
minutos no lo ve nadie. Lo vio el jugador, y además se lee como una avería: una máquina de estas
gira para un lado. Un bucle sin costura necesita que la tira **repita**, así que la tira de reposo
repite sus 48 primeras casillas al final y la vuelta al origen cae sobre celdas idénticas.

Repetir la tira **entera** habría valido igual y cuesta el doble de celdas que construir, que es
tiempo al abrir la pantalla. Y el largo del bucle lo **declara** el ViewModel
(`GachaViewModel.IdleLoopCells`) en vez de medirlo la vista sobre el ancho de la tira: una tira
construida para una tirada no es un bucle, y contesta 0.

### Clic derecho en el mapa

Un marcador ciclaba sin marcar → atrapado → muerto → huida, y deshacer una marca puesta por error
costaba dar la vuelta entera. El clic derecho lo devuelve a **sin marcar** de una. Va por
`ClearZoneCommand`, el mismo camino de escritura que el clic izquierdo, así que deja su evento
igual que todo lo demás; y no se escribe nada si ya estaba sin marcar.

Va por manejador y no por `MouseBinding` **por una trampa de WPF**: un `InputBinding` no está en el
árbol visual y **no hereda el `DataContext`**, así que el `CommandParameter` habría llegado en
`null` y cada clic derecho no habría limpiado nada, en silencio. Y está **dicho en la leyenda**,
porque un gesto que no se anuncia no existe.

### Cambiar de pestaña cierra lo que dejaste abierto

`SectionViewModel.ResetState()`, llamada sobre la sección que se **abandona**. Lo que cierra es lo
que está **abierto** o **armado**; lo que no toca es el trabajo de alguien:

| Sección | Qué cierra | Qué respeta |
|---|---|---|
| GACHA | la lista de especies de un tier | la tarjeta del resultado: es lo que salió |
| TIENDA | vuelve a la pestaña COMBATE | — |
| MANTENIMIENTO | los dos contadores de «mirar», que desarman los botones que escriben | no toca nada mientras está trabajando |
| VISOR | la ficha abierta | **no cierra nada si hay EV editados sin guardar** |

Ese último es el que importa: tirar una edición a medias porque se pulsó otra pestaña sería un
cambio de estado silencioso, que es la regla 4. El resto de secciones heredan un `ResetState` vacío
a propósito — cada una sabe cuál de su estado es un panel y cuál es de alguien.

### Los EV de los enemigos: ya estaban como el cartucho, y ahora está medido

La petición era dejarlos por defecto. **Nada del randomizador los escribía**, pero eso es una
afirmación sobre el código y lo que interesa es el mundo que se está jugando, así que se midió: en
el trpoke7 los EV son los bytes **0x02-0x07** —anclado en `TrainerPoke7` de pk3DS, donde 0x00 es
género y habilidad, 0x01 la naturaleza y 0x08 los IV empaquetados—, y `RomTool trainers` los
compara ahora contra la vanilla uno a uno. **0 de 653 entrenadores** en el mod generado y **0 de
653 en el instalado**. Hay además un test que pasa cada escritor de la tabla por una entrada y
exige que los seis bytes no se muevan, porque «no los escribe nadie» es cierto hasta que deja de
serlo y un enemigo entrenado a 252 en Velocidad no se nota jugando.

De paso, `trainers` acepta ahora **una ruta** además de una seed, como `quien-lleva` y `entrenador`
del §85, o sea que puede repasar el mod **instalado** y no solo lo recién generado. Y con eso salió
algo que no se buscaba: el mod instalado lleva **tres Pokémon sin evolucionar** por encima del
corte de la sexta prueba —Tangela nivel 60, Aipom 67 y Quaxwell 63—, y los tres son **AÑADIDOS**,
o sea el Pokémon extra del rol. Es exactamente el segundo agujero del §85, y el mundo instalado se
generó antes de aquel arreglo: lo generado hoy da 0. Queda dicho y no tocado, porque cambiarlo es
reinstalar el mod y eso es del jugador.


---

## §103 · El gacha: el fallo que metí ayer, un buscador y la tabla de la competición (2026-09-07)

### Los legendarios salían en los cinco tiers, y era mío

El §102 amplió el saco de legendarios para que dejara de recortarse por banda. Lo que no miró es
que **la lista de «quién puede salir» pregunta por tier**, así que `PoolOf(tier, legendary: true)`
le devolvía **todos los legendarios a los cinco**. El jugador abría el Tier 1 y veía a Mewtwo.

Ninguna tirada repartió uno de más —la probabilidad de los tiers 1 a 4 es cero y eso no se tocó—,
o sea que el fallo era de **lo que la pantalla decía**, que en un gacha es casi peor: la lista
existe justamente para que el jugador sepa qué compra. La condición que faltaba no es «si es el
tier 5» sino **«si este tier reparte legendarios»**, `LegendaryChance > 0`, que es el mismo hecho
dicho donde no hay que acordarse de mantenerlo. Y la probabilidad vuelve a **0,40**.

La forma del error merece quedar escrita porque es de las que no fallan: se amplió un saco mirando
**quién lo consume para tirar** y no **quién lo consume para enseñarlo**. Los dos llamaban a la
misma función, y solo uno de los dos pasaba por la probabilidad.

Dos pruebas nuevas, y la segunda es la que importa: una fija que el saco de legendarios de un tier
que no los reparte está **vacío**, y otra mira el **fichero que se reparte** y exige que haya
exactamente **un tier** con probabilidad por encima de cero y **un solo banner** cuyas
probabilidades lo alcancen. «Los legendarios solo salen del BUENO» se apoyaba en esos dos hechos y
ninguno estaba escrito en ningún sitio: bastaba editar un porcentaje de `Data/gacha.json` para
repartirlos por el banner barato sin que nada se quejara.

### La lista, en condiciones

Ocupaba el hueco de abajo y el Tier 5 salía en dos filas con barra de desplazamiento. Ahora **cubre
también el carrete y la tarjeta del resultado** —con la lista abierta no se está tirando, así que
ese sitio no lo usa nadie— y lleva **buscador**, con la misma forma que el de la tienda: una
segunda colección rellenada a mano en vez de un `ICollectionView`, que tocado fuera del hilo de UI
tumba la ventana (§62). Se ignoran mayúsculas **y acentos**, por la misma razón que allí: con el
mod de expansión la lista mezcla el español del cartucho con el inglés del mod.

La cuenta se dice al lado (`163 de 240` mientras se filtra, `240 Pokémon` cuando no), y abrir otro
tier **borra el filtro**: encontrarse un tier ya recortado por lo que se buscó en el anterior se
leería como que ese tier tiene cuatro Pokémon.

Y por sexta vez, la trampa del §74: `Style` puesto **como atributo y como `<TextBlock.Style>`** es
error de compilación. Van seis.

### Lo que da cada prueba

Catorce filas en dos columnas bajo los banners, sacadas de `Data/grants.json` y no escritas en el
XAML, con las tiradas en el color del tier que ese banner reparte más —el mismo que ya usan la
barra de la tarjeta y los portales, para que POCHO sea del mismo color en los tres sitios—.

Lo conseguido va marcado con un punto y lo que falta, apagado. Se marca y **no se quita**, porque
la pregunta que la tabla contesta es «¿gasto puntos ahora o espero a la prueba que viene?», y para
eso hacen falta las que faltan tanto como las que ya pagaron. Qué está conseguido sale de
`CreditService.ReachedAsync`, **no de que la pantalla pregunte a los logros por su cuenta**:
`EarnedAsync` los suma y pierde de cuál vino cada tirada, que está bien para un total y no sirve
para una lista, y dos sitios decidiendo qué cuenta como conseguido acabarían discrepando.

Detalle que solo se ve pensando en la ventana pequeña: la fila es `Auto`, así que sin tope la tabla
**empuja el botón de TIRAR fuera de la pantalla**. Va con `MaxHeight` y barra: que la tabla se
desplace es un incordio, que no se vea el botón de tirar es una pantalla rota.

**No está visto en la aplicación**: se comprobó que compila, que las 831 pruebas pasan y que las
veinte claves `StaticResource` que usa existen en el tema, que es lo que se puede comprobar sin
abrirla.


---

## §104 · Cómo reparte el gacha de la referencia, medido (2026-09-07)

El jugador preguntó si en BxnnyLocke salen **primeras evoluciones** —si un tier 5 puede dar Gible,
Dratini o Axew tomando como criterio el total base de la **forma final**—. Se ha medido, y la
respuesta es **sí, y es la regla central de su gacha**.

Nada de esto se copia. Se leen sus **recursos de datos** y se traza **qué llama** su código, que es
lo que la norma de `Locke/` permite: interoperar y aprender. Ni una línea suya entra aquí.

### Los pools son líneas evolutivas, no Pokémon

Seis recursos embebidos, `gachaPools.pool_1.txt` … `pool_6.txt`. Cada renglón es una **línea
completa**, por etapas, y una etapa puede tener varias ramas:

```
[[147], [148], [149]]                  Dratini  -> Dragonair -> Dragonite
[[265], [266, 268], [267, 269]]        Wurmple  -> Silcoon/Cascoon -> Beautifly/Dustox
[[83]]                                 Farfetch'd, que no evoluciona
```

Cruzados con nuestro `Data/species.json`, el criterio de reparto salta a la vista, y es el de la
**forma final**:

| pool | líneas | total base de la FORMA FINAL | total base de la PRIMERA |
|---|---|---|---|
| 1 | 17 | 175-400 | 175-380 |
| 2 | 178 | 236-490 | 180-490 |
| 3 | 97 | 494-525 | 198-517 |
| 4 | 45 | 528-567 | 200-535 |
| 5 | 8 | **600 clavado** | **300 clavado** |
| 6 | 51 | 540-600, los 51 legendarios | 420-600 |

El pool 5 son **exactamente los ocho pseudolegendarios**: Dratini, Larvitar, Bagon, Beldum, Gible,
Deino, Goomy y Jangmo-o. Axew **no** está ahí sino en el 4, porque su línea acaba en Haxorus (540)
y no en 600 — o sea que la intuición del jugador era correcta en la forma y se pasaba por una
especie.

### Qué etapa te entrega: depende de por dónde vas

Trazando `selectPokemonID`, el orden es: coger la pool del tier filtrada por rol, elegir **una
línea** al azar, y después elegir **la etapa** con un dado de 100 contra dos porcentajes que salen
de un `switch` sobre **cuántas medallas Z llevas**. La etapa se recorta con `Math.Min(etapa,
etapas - 1)`, así que una línea de dos formas nunca se sale de su rango y Farfetch'd siempre es
Farfetch'd. Dentro de la etapa, si hay ramas, se sortea entre ellas.

| Medallas Z | 1ª forma | 2ª forma | forma final |
|---|---|---|---|
| 0 | **100 %** | 0 % | 0 % |
| 1 | 97 % | 3 % | 0 % |
| 2 | 94 % | 6 % | 0 % |
| 3 | 90 % | 9 % | 1 % |
| 4 | 84 % | 12 % | 4 % |
| 5 | 71 % | 20 % | 9 % |
| 6 | 67 % | 23 % | 10 % |
| 7 | 63 % | 26 % | 11 % |
| 8 | 59 % | 29 % | 12 % |
| 9 | 55 % | 32 % | 13 % |
| 10 | 51 % | 35 % | 14 % |
| 11 | 47 % | 38 % | 15 % |
| 12 | 35 % | 45 % | 20 % |

O sea: al principio de la partida el gacha da **siempre** la primera forma, y lo que el tier te
promete es **en qué se va a convertir**. Un tier 5 recién empezado es un Gible, no un Garchomp. Con
las doce pruebas hechas, un tier 5 es Garchomp una de cada cinco veces.

### En qué se diferencia del nuestro

El nuestro gradúa por el total base de **la especie que entrega**, no de su línea, y entrega esa
especie y ya está. Consecuencias, dichas sin juicio porque son dos diseños distintos y el segundo
es el que el jugador eligió:

- Nuestro tier 5 da un Pokémon de 600 **hecho**; el suyo da la semilla de uno.
- Nuestro tier 1 da cosas que **nunca** van a servir —lo que sale de 400 se queda en 400—; el suyo
  reparte líneas que acaban en 400, así que su tier bajo también da algo que crece.
- El suyo escala con el avance de la partida; el nuestro no mira por dónde vas.
- El suyo necesita una tabla de líneas evolutivas mantenida a mano; el nuestro sale de un rango y
  por eso sigue valiendo con la ROM randomizada y con el mod de expansión de 1025 especies.

Ese último punto es el que habría que resolver si se quisiera copiar la idea: nosotros **ya
tenemos** la tabla de evoluciones del cartucho leída (`EvolutionTable`, §69), así que se podría
graduar por `FinalOf(especie)` sin mantener ninguna lista. **No se ha hecho nada de esto**: la
pregunta era qué hacen ellos.


---

## §105 · El gacha reparte líneas evolutivas (2026-09-07)

A petición del jugador —«hazlo igual que BxnnyLocke, me parece que está más balanceado»—, el motor
del gacha pasa a hacer lo que el §104 midió: **el tier es la banda del total base de la FORMA FINAL
de una línea, y lo que te entregan es una de sus etapas, según por dónde vayas**.

### Lo que cambia

Antes: el tier era la banda de la especie entregada. El tier 1 daba cosas de 400 que se quedaban en
400 para siempre, y el tier 5 daba un 600 hecho. Ahora el **tier 5 son doce familias**:

```
Dratini 300 -> Dragonair 420 -> Dragonite 600      Gible 300 -> Gabite 410 -> Garchomp 600
Larvitar 300 -> Pupitar 410 -> Tyranitar 600       Deino 300 -> Zweilous 420 -> Hydreigon 600
Slakoth 280 -> Vigoroth 440 -> Slaking 670         Goomy 300 -> Sliggoo 452 -> Goodra 600
Bagon 300 -> Shelgon 420 -> Salamence 600          Jangmo-o 300 -> Hakamo-o 420 -> Kommo-o 600
Beldum 300 -> Metang 420 -> Metagross 600          Duraludon 535 -> Archaludon 600
Dreepy 270 -> Drakloak 410 -> Dragapult 600        Frigibax 320 -> Arctibax 423 -> Baxcalibur 600
```

Y la etapa la decide `etapaPorProgreso` en `Data/gacha.json`, contra las **etapas superadas** de la
run — la misma cifra de la que sale el cap de nivel (§49), y no una segunda idea de progreso que
pudiera discrepar—. Con 0 etapas es **siempre** la primera forma; con las doce, 35 / 45 / 20.

### Las tres decisiones que no eran obvias

**El progreso viaja en el evento.** Es el único dato de una tirada que no sale de la seed, así que
sin él la tirada dejaría de poder recomputarse en cuanto la run superara otra prueba: misma seed,
mismo número, otro Pokémon, y una auditoría llamaría mentira a una tirada honrada. `RollAsync`
**exige** el número en vez de tener un valor por defecto de 0, porque un llamante que se lo dejara
repartiría primeras formas para siempre sin que nada pareciera roto.

**Se clava a la última etapa que la familia tenga**, no se salta. Sin eso, las familias cortas
rechazarían su parte de las tiradas tardías y lo raro de cada tier caería solo en las líneas
largas. Farfetch'd siempre es Farfetch'd.

**Los dos dados se tiran siempre**, aunque la familia tenga una sola etapa: si el segundo dado
dependiera de con qué línea tocó, cambiar la tabla de familias movería el resultado de todas las
tiradas siguientes.

### El fallo que la medición encontró, y el que salió de arreglarlo

Las líneas salen de `EvolutionTable.Lines()`, leído del cartucho por `RomTool species` — o del mod
de expansión, si está—. Al comprobar contra los ficheros reales aparecieron **24 especies en dos
familias**: las formas de Alola tienen línea propia —Rattata de Alola evoluciona a Raticate de
Alola— y su base es una entrada de **forma**, con índice por encima de las especies. Quedarse solo
con los ids conocidos dejaba una familia cuya única etapa era **Raticate**, así que el gacha
entregaba Arcanines y Golems hechos como si fueran primeras etapas, desde un tier barato, sin que
nada fallara.

Descartar esas familias destapó lo contrario: **diez especies sin familia ninguna** —Obstagoon,
Sirfetch'd, Basculegion, Clodsire y compañía, que solo evolucionan de una forma regional— y una
especie sin familia **no puede salir del gacha**. Se les da familia propia de una etapa, que es lo
que son para el gacha: algo a lo que no se llega subiendo desde ninguna base, igual que un Paradoja
o un Ditto. Las dos mitades están fijadas con un test **sobre el fichero que se reparte**, no sobre
un fixture: ninguna especie en dos familias, ninguna en cero.

### Verificado contra los datos reales

553 familias, 114 de tres etapas. Con 0 etapas superadas, **971 de 971** tiradas de tier 5 son
primera etapa. Con 12, salen 58,9 / 29,9 / 11,2 en vez de 35 / 45 / 20, y **eso es correcto**: el
40 % de las tiradas de tier 5 son legendarias, casi todas de una sola etapa, así que se clavan en
la primera. `0,4·100 + 0,6·35 = 61`, y se midió 58,9. Las tres cifras cuadran con esa cuenta.

Diez tiradas seguidas de BUENO con la misma seed, cambiando solo el progreso:

```
 0 etapas: Growlithe(350), Ferromole(590), ..., Applin(260), Ferropúas(570)
12 etapas: Arcanine(555),  Ferromole(590), ..., Flapple(485), Ferropúas(570)
```

Los Paradoja no se mueven porque no evolucionan; Growlithe y Applin sí.

### En la pantalla

La lista de «quién puede salir» enseña **familias enteras**, con flecha entre etapas, ordenadas por
dónde acaban. Buscar «Garchomp» encuentra la familia aunque lo que te vayan a dar sea el Gible. La
banda del portal lleva ahora una flecha delante —`→ 600+`— porque es en lo que ACABA la línea y no
lo que te dan, y al pie va la otra mitad de la mecánica, que si no es invisible: «Con 3 etapas
superadas: 90% primera forma · 9% segunda · 1% forma final».

**Nada de esto se ha visto girando**: compila, pasan las 847 pruebas y el motor está verificado
contra los ficheros reales, pero la pantalla no se ha abierto.


---

## §106 · Una muerte que no se contó: la lectura se iba al espejo (2026-09-07)

El jugador siguió jugando y avisó de que a un Pokémon **se lo mataron, PermaLocke no lo detectó y
pudo curarlo**. El log del día lo confirma por omisión: **cero muertes registradas**, y sí una línea
de `KeepFallenDownAsync` devolviendo al suelo a un Ursaluna que ya constaba caído.

### El defecto

`AzaharGameStateProvider.Choose` decidía de qué estructura leer el equipo así:

```
.OrderByDescending(candidate.Party.Count)                    // primero: cuántos se leen
.ThenBy(esAutoritativa ? 0 : 1)                              // después: cuál es la buena
```

La cuenta era la clave **principal** y la autoridad un desempate, y eso está al revés. El espejo de
salto `0x104` es preparación del bloque de partida: el juego lo escribe y **nunca lo lee**, así que
sus PS van con retraso (§98, §99). Con esa ordenación, cualquier vuelta en la que la estructura
autoritativa tuviera **un solo hueco** que el lector no aceptara —una estadística de combate fuera
de rango un instante basta— entregaba la lectura entera al espejo, donde un Pokémon recién caído
seguía enseñando sus PS de antes. El vigilante decide la muerte con `CurrentHp == 0`, así que no
veía nada.

Que las lecturas son inestables está **medido en el log del propio jugador**, en las líneas del
escritor del cap: «corregido y releído en 1 copias», «2 copias», «3», «4», minuto a minuto.

La regla pasa a ser **autoridad primero, cuenta después**, y sale de `Choose` a
`PartyLayoutLocator.Preferred`, que es una función pura y por tanto se puede fijar con pruebas sin
juego delante. El coste de preferir la verdad es leer **menos** huecos en esas vueltas, que es la
forma correcta de equivocarse: un hueco no leído es un hueco no juzgado, y un hueco leído de una
copia con retraso es una respuesta equivocada dicha con seguridad.

### Y lo que ese arreglo ponía en riesgo

Leer el equipo incompleto más a menudo tiene una víctima: `CheckWipeAsync` cobra **−100** cuando
«nadie está en pie», y su única guarda era que el equipo no estuviera vacío. Una lectura corta que
pillara un hueco caído habría cobrado un equipo caído que no ocurrió.

Ahora hace falta ver «nadie en pie» **tres vueltas seguidas** —tres segundos— antes de cobrar. Un
equipo caído de verdad no es un instante: te manda al Centro Pokémon y sigue caído hasta que curas,
así que esperar no puede perderse ninguno.

Ese camino **no tenía ni una prueba**, o sea que las 93 de `Rules` en verde no decían nada de él y
podría haberlo roto entero en silencio. Ahora tiene seis, incluida la que exige que una lectura
corta de caídos no cueste cien puntos, y está comprobado que **tres de ellas fallan** con la guarda
puesta a 1.

### Lo que NO está demostrado

Que esto sea lo que le pasó a ese Pokémon. Es un defecto real que produce exactamente ese síntoma,
pero el enlace estuvo caído casi toda la sesión —conectado de 13:10 a 13:26 y de 13:28 a 13:48, y
antes «Azahar no responde» desde las 11:06—, así que la muerte pudo caer sencillamente fuera de esa
ventana. Las dos causas dan el mismo resultado y no hay forma de distinguirlas a posteriori: los PS
ya están curados. **Nada de esto se ha visto en el juego todavía**, porque Azahar estaba cerrado.


---

## §107 · La Unidad Ultra pasa a combate importante, y lo que cuesta regenerar (2026-09-10)

El jugador se topó con un combate de **un solo Pokémon** —un Terapagos a nivel 56— y preguntó si
estaba bien. Lo estaba: entrenador **498, clase 192 «Unidad Ultra», Miria**, que en la capa base
lleva **un Poipole a nivel 47** y nada más. Las dos cuentas cuadran —47 × 1,2 = 56, y Poipole 420 →
Terapagos 450, los dos Ultraente/legendario—, así que el randomizador no le había quitado nada.

Aun así pidió meterlo en los importantes, y así queda: **192 y 193** —Miria y Darius— entran en
`clasesImportantes`. Es el primer caso en que esa lista crece por **decisión de diseño** y no por
tapar un fallo, y conviene que esté dicho, porque su firma medida es la de relleno: cuatro
entrenadores por clase y **un Pokémon como mucho**, justo lo contrario del criterio —pocas
apariciones, equipos grandes— con el que se cazaron las demás. Van las dos y no solo la de Miria
porque son el mismo grupo de la historia.

### Lo que cambia de verdad, medido contra el mundo instalado

Regenerado con **la misma seed** del mundo que se juega (`17037260821123107863`) y el mismo rol
—que hay que pasárselo: sin `--rol`, `RomTool randomize` genera un mundo **sin subir niveles y sin
Pokémon extra**, y ahí Miria sigue con uno a nivel 47—, de los nueve ficheros del mod solo cambian
**dos**:

| fichero | |
|---|---|
| `a/0/8/3` encuentros salvajes | **idéntico** |
| `a/0/1/3` aprendizajes, `a/0/1/4` evoluciones, `a/0/1/7` datos, `a/0/1/9`, `Shop.cro`, `a/1/5/9` | **idénticos** |
| `a/1/0/6` y `a/1/0/7` entrenadores | distintos |

Que los salvajes no se muevan es lo que decide si esto se puede instalar a media partida: las zonas
del Nuzlocke siguen dando lo mismo.

Dentro de los entrenadores, de 653: **620 idénticos**, **8 que solo crecen** —los cuatro de Miria y
los cuatro de Darius, exactamente— y **25 con una especie distinta en un hueco que ya existía**. Eso
último es el §27 otra vez: añadir dos clases consume tiradas de más y **desplaza la corriente** de
todo lo que viene después, empezando justo en el 499. Repartidos por nivel, 24 de los 25 están entre
**61 y 84** —contenido por delante de un jugador que va por 56— y solo el 630, de nivel 35, queda
por detrás.

De rebote, regenerar **arregla los tres sin evolucionar del §102**: el mundo instalado tenía Tangela
a 60, Aipom a 67 y Quaxwell a 63, los tres como Pokémon añadido, y el regenerado da **817 de nivel
29 en adelante, 0 sin evolucionar**.

**Verificado releyendo lo generado**, no el informe: `entrenador 498` pasa de un Terapagos a
Terapagos Nv56 **más un Malamar Nv56**. Y `RomTool clases` ya no lista la 192 ni la 193 entre las
que faltan, que es la comprobación que el §100 dejó montada precisamente para esto.

**Instalado el mismo dia**, con el jugador viendo antes las 25 tiradas desplazadas. Verificado sobre
lo INSTALADO y no sobre lo generado: los nueve ficheros coinciden con lo que salio del randomizador,
Miria lleva Terapagos Nv56 y Malamar Nv56, y el repaso da 0 objetos alterados, 0 EV alterados, 0
especies prohibidas y 817 de nivel 29 en adelante con 0 sin evolucionar. El mundo anterior **NO** queda guardado: esto se escribió
creyendo que sí, y es falso -ver la corrección al final del §109-.

Un cabo suelto que conviene tener presente: se regenero desde `RomTool` y no desde la aplicacion, asi
que **no hay evento `RomRandomized`** de este mundo. El historial sigue diciendo que el ultimo mundo
es el anterior. No cambia nada jugable -- la configuracion de `shuffleBaseStats` y
`randomizeAbilities` no se ha tocado, que es lo unico que el §80 lee de ese evento --, pero si se
quiere que el historial lo refleje, el camino es regenerar desde la seccion RANDOMIZADOR con la
misma seed y el mismo rol: da los mismos bytes y ademas deja el evento.


---

## §108 · Poner EV y que no cambie nada: faltaba la otra mitad (2026-09-10)

El jugador entrenó EV desde la aplicación, entró al juego y **las estadísticas seguían iguales**.
Tenía razón, y lo que fallaba no era la escritura —los EV llegaban— sino lo que el §51 decidió no
hacer con ellos.

### El razonamiento viejo era bueno; la frase que lo cerraba, no

`SaveEvTrainer` escribía los seis bytes de EV y **no tocaba las estadísticas guardadas**, con este
motivo, que sigue siendo correcto: un Pokémon de equipo **almacena** sus estadísticas —uno de caja
no, se calculan al sacarlo—, y recalcularlas con PKHeX da números falsos, porque PKHeX usa **su**
tabla de estadísticas base y esta run se juega con `shuffleBaseStats`. Medido en su día: un Kommo-o
de 168 PS volvía con 151.

Lo que no se sostenía era la frase siguiente: «dejarlas quietas no cuesta nada, la estadística se
pone al día cuando el juego recalcule». Eso es verdad de un Pokémon que gana EV **combatiendo**.
Aquí no: escribir el fichero de partida no hace que el juego recalcule nada, así que la estadística
espera a una subida de nivel — y **con el cap de nivel puesto no hay subida de nivel**. El equipo
estaba clavado en 59 con el cap en 59, o sea que no se habrían puesto al día nunca. El comentario
predecía el fallo si se leía con cuidado.

### El arreglo: las bases del mundo instalado, no las de PKHeX

`InstalledWorld` ya leía la tabla `personal` del mod instalado para las curvas de experiencia (§91),
así que las bases estaban a un paso. Van a `WorldLimits.BaseStats`, y `StatCalculator` calcula las
seis. Si el mundo **no** publica su tabla, no se toca nada y **el mensaje lo dice**: eso es lo que
faltaba antes, porque el silencio se leía como éxito.

Dos cuidados que no son adorno. Los PS actuales siguen al máximo hacia arriba, como hace el juego al
subir de nivel, **pero un Pokémon a cero se queda a cero**: en este proyecto cero PS *es* la muerte
(§98), y curarlo desde la pantalla de EV sería deshacer una muerte por la puerta de atrás. Y
Shedinja lleva siempre 1 PS porque el juego lo fuerza por especie; sin esa excepción, entrenarlo
habría escrito un Shedinja de setenta PS.

### El orden, dos veces, y quién cazó la segunda

La tabla `personal` guarda **PS, Ataque, Defensa, VELOCIDAD, At. Esp., Def. Esp.**, no el orden de
la ficha. Comparar sin reordenar hace que PS y Ataque cuadren y las otras tres no, que se lee
exactamente como un fallo que no existe — pasó al diagnosticar, y por poco da el diagnóstico al
revés.

Y la **naturaleza usa ese mismo orden interno**: `sube = naturaleza / 5`, `baja = naturaleza % 5`
sobre Ataque, Defensa, Velocidad, At. Esp., Def. Esp. Eso entró mal y **los tests pasaron**, porque
el que había usaba Huraña —sube Ataque, baja Defensa—, y esos dos caen en el mismo sitio en los dos
órdenes. Lo cazó comprobar la calculadora contra **los seis Pokémon de la partida real**, cuyas
estadísticas las calculó el juego y por tanto son correctas por definición: **25 de 36**, y los once
fallos eran todos At. Esp., Def. Esp. o Velocidad, en los cinco Pokémon de naturaleza no neutra.
Corregido el mapeo: **36 de 36**, seis especies y cinco naturalezas, con las bases barajadas. Hay
ahora un test con la naturaleza 20, donde los dos órdenes discrepan, y está comprobado que **falla**
con el mapeo viejo.

Es el §65 otra vez y en su mejor versión: la verificación de un cálculo no puede ser el mismo
cálculo. El oráculo bueno estaba delante —una partida llena de números que el juego ya había
resuelto— y es lo único que separó «los tests pasan» de «está bien».

### Lo que no hace falta reparar

Los seis del equipo **ya están cuadrados**: sus estadísticas actuales coinciden con sus EV, porque
en algún momento subieron de nivel y el juego recalculó. O sea que el fallo costó tiempo y confusión,
no números malos en la partida.

### Verificado de punta a punta, sobre una copia

No solo la calculadora: el camino real de escritura contra una COPIA de la partida real, con la de
verdad comprobada por hash antes y después y sin moverse. A un Salamence se le dio la vuelta al
reparto -de 252 en Ataque y Velocidad a 252 en At. Esp. y Def. Esp.- y las seis estadísticas se
movieron exactamente donde tenían que moverse:

```
Atk     197 -> 163    -34      AtEsp   153 -> 190    +37
Def     118 -> 118      0      DefEsp  128 -> 169    +41
PS      199 -> 199      0      Vel     178 -> 141    -37
```

La Defensa pasó de 5 EV a 6 y no se movió, que es correcto: los EV cuentan de cuatro en cuatro. Y
los PS actuales se quedaron en 199/199.

**Lo que sigue sin verse es la pantalla**: el arreglo está probado por debajo, pero nadie ha pulsado
el botón de la aplicación con él dentro.


---

## §109 · La fusión con Lunala, y una regla que no mueve el mundo (2026-09-13)

El jugador llegó al combate de Necrozma **Alas del Alba** —la fusión con Lunala, la escena en torno a
la que gira Ultra Luna— y le salió un **Swampert a nivel 60**. No era un fallo: la fila 159 de la
tabla de estáticos (Necrozma forma 2, nivel 50 de cartucho) estaba **a propósito** en sorteo normal.
El comentario de `staticOverrides` lo decía —«las formas 1 y 2 son los combates de la historia y se
quedan con el sorteo normal»— pero no apuntaba por qué. Solo Ultra Necrozma, la fila 160, tenía regla
de mega, y en el mundo instalado es un **Mega Salamence a nivel 72**, como debe. Se añaden reglas de
mega para las formas 1 y 2.

### Añadirla como las demás habría sorteado otra vez el mundo entero

Regenerado con la seed y el rol de la run, y comparado fila a fila con el mundo instalado:
**249 de 252 estáticos y los 7 intercambios cambiaban**, Dominantes y legendarios todavía por delante
del jugador incluidos. Por arreglar dos filas.

La causa estaba en cómo se aplican: las reglas corren **antes** del sorteo normal y con **su misma
corriente aleatoria**. Cada mega gasta dos tiradas —especie y forma— y además saca su fila del
sorteo, que por tanto también gasta menos. Todo lo que viene detrás se corre unos puestos, y se ve en
la comparación: la misma lista de especies desplazada filas abajo. Es el §27 otra vez.

### `independentDraw`

Una regla con `"independentDraw": true` se **busca** en la tabla del cartucho antes de que nada
escriba —después del sorteo, la fila 159 ya es otra especie y no casaría—, deja que el sorteo normal
pase por su fila **gastando exactamente lo que gastaba**, y **se aplica después**, con una corriente
derivada de la fila (`static-override-<fila>`). Derivar no avanza la corriente madre —sale de la
semilla, no del estado—, así que el resto del mundo sale **idéntico byte a byte**. Medido con las dos
fusiones marcadas así: **2 de 252 estáticos, 0 regalos, 0 intercambios**, y los otros ocho ficheros
del mod iguales. La fila 159 pasa a **Mega Ampharos a nivel 60**; la 158, la escena de Ultra Sol, a
Mega Charizard X, que en Ultra Luna no se ve nunca.

No es lo que hacen por defecto las reglas, y a propósito: las cuatro que ya había generaron mundos que
se están jugando, y cambiarles la forma de sortear los volvería a sortear igual. Las reglas nuevas
sobre un mundo instalado lo quieren encendido.

### Dos tropiezos del día, dichos

Una **comilla dentro de un comentario** dejó el fichero entero ilegible, y la comparación que siguió
salió «igual de mal que antes» porque comparaba la salida **de la generación anterior**: el
randomizador había reventado y la carpeta no se había tocado. Se borra la salida antes de regenerar,
y hay test que carga el fichero que se reparte. Es el §65 con otro disfraz: una comparación que no
comprueba que lo comparado sea nuevo puede contestar con toda seguridad sobre algo viejo.

**No está instalado**: Azahar estaba abierto. Y ojo con lo que vale ya: es un combate de la historia
que se juega una vez, así que solo sirve si el jugador **aún no lo ha guardado superado**.


### Instalado, y una corrección que afecta a la red de seguridad

Instalado con Azahar cerrado **y el jugador sin haber guardado** el combate. Verificado sobre lo
instalado: los nueve ficheros coinciden byte a byte con la generación medida antes, que difería del
mundo que se jugaba **solo en las filas 158 y 159**. La fila 159 es Mega Ampharos.

Y al comprobarlo salió algo que el §107 afirmó mal: **reinstalar NO guarda el mundo anterior**.
`ModInstaller.Install` copia encima fichero a fichero y no aparta nada. Lo que sí se aparta es la
**salida del randomizador** (`Randomized/…` → `permalocke-mod-anterior` en la raíz del repositorio,
vía `LayeredFsMod.KeepAside`), que es otra cosa. La carpeta `Azahar\load\permalocke-mod-anterior` es
del **21 de agosto**, de un instalador anterior al de la capa base: la promesa del §42 dejó de
cumplirse cuando se reescribió la instalación para el mod de expansión, y nadie lo notó porque nunca
hubo que volver atrás. El §107 la repitió sin mirar la fecha. Esta vez no costó nada —la generación
previa sí estaba apartada y es idéntica a lo instalado—, pero la próxima reinstalación sin comparar
antes no tendría forma de deshacerse.


---

## §110 · Instalar ya no destruye el mundo anterior, y el `code.bin` que se quedaba atrás (2026-09-13)

### Un solo instalador, con copia

`ModInstaller.Install` y el nuevo `ModInstaller.SwitchToBase` —el cambio al modo combate— **copian
aparte, antes de escribir nada, todo lo que van a pisar y nadie podría reconstruir**, y releen la
copia byte a byte. Si la copia no se puede hacer o no se lee igual, **no se instala nada**: la regla
de `SaveEraser` (§67), sin copia no se destruye.

Qué cuenta como «se perdería»: el fichero existe, lo que acaba ahí es distinto —por hash en lo
randomizado, que es pequeño y cuyas fechas siempre se mueven; por longitud y fecha en la capa base,
donde hashear 2,5 GB no enseña nada—, y **no es igual que la copia del propio mod base**, que sigue en
`Expansion/`. Esto último no es por ahorrar sitio: sin ello, volver del modo combate gastaría una de
las tres copias en guardar el mod sin randomizar y echaría fuera un mundo de verdad. Reinstalar el
mismo mundo tampoco hace copia, por lo mismo.

Van a `Azahar\load\permalocke-copias\<fecha>`, **al lado** de `mods` y no dentro —ahí lo que se llame
como un title id es un mod que el emulador cargaría—, con un `LEEME.txt` que dice cómo volver y qué
ficheros hay. Se guardan **tres**, y lo viejo se borra solo cuando la copia nueva ya está comprobada.

La carpeta `load\permalocke-mod-anterior` del 21 de agosto **no se ha tocado**: es de un mundo viejo,
es del jugador y no hay motivo para borrarla sin preguntar.

Nueve pruebas, y está comprobado que **cinco fallan** con la copia desactivada.

### Tres instaladores, y el tercero se dejaba el ejecutable

`ModInstaller` existía precisamente porque hubo dos copias de «qué ficheros ganan» y su comentario
avisaba de que acabarían discrepando. Había una **tercera**, a mano, en `RomTool randomize --install`,
y discrepaba: ponía la capa base y el `romfs` generado pero **no el `exefs` generado**. Ese `code.bin`
es el del mod con la tabla de MT y la de tutores barajadas, así que las dos instalaciones hechas desde
ahí (§107 y §109) dejaron el juego del jugador con **las MT y los tutores del mod de expansión** en vez
de los de su mundo. Medido: instalado `cd9c45…`, igual que `Expansion/exefs/code.bin`; generado
`17e4d4…`. Nada falló y el informe de la generación seguía diciendo «100 de las 100 MT enseñan otro
movimiento».

Por qué no se vio al verificar: las comparaciones de esos dos días recorrían **la carpeta `romfs`** de
lo generado contra lo instalado, fichero a fichero, y el `code.bin` vive en `exefs`. Una comprobación
exhaustiva de la mitad de la instalación. `RomTool` pasa ahora por `ModInstaller.Install`, y hay una
prueba que exige que el `code.bin` generado gane al del mod base.


**Reparado el mismo día**, con Azahar y la aplicación cerrados y a condición del jugador de que no
cambiara qué MT puede aprender cada Pokémon. Esa compatibilidad son los bits de `a/0/1/7`, no el
`code.bin`, y se comprobó antes y después: hash de los 37 ficheros instalados, y **cambia exactamente
uno**, `exefs/code.bin`, de `cd9c45…` (el del mod) a `17e4d4…` (el generado). `a/0/1/7` sigue en
`206eb106…`. No hizo copia, y es correcto: lo que había en `exefs` era idéntico al mod base, que sigue
en `Expansion/`, y el resto era igual que lo que se iba a escribir.


---

## §111 · Ferropaladín contra la fusión, y un diagnóstico equivocado sobre Okidogi (2026-09-13)

El jugador perdió a Ferropaladín contra Necrozma Alas del Alba y la app no lo vio. En el mismo
mensaje contó que Okidogi, en su equipo, figuraba como «muerto de antes».

### Lo de Okidogi era una muerte real, y lo diagnostiqué al revés

Las copias de partida daban Okidogi a **191/191** a las 21:31:39 del día 10, antes de abrir el juego,
y la muerte se registró a las 21:39:46. Un segundo después la app tuvo que barrer la memoria entera,
encontró 34 copias del equipo y en una de ellas Okidogi estaba a 191. De ahí saqué que la muerte había
salido de **una sola lectura con la partida a medio cargar**, se lo dije al jugador como hecho medido,
añadí una regla de **tres lecturas seguidas** y un botón para revocar la muerte.

Era falso. **La app llevaba conectada desde las 21:31:51**, ocho minutos antes, y el jugador confirmó
que a Okidogi lo mataron en un combate y lo dejó en el equipo. Esa conexión no la vi porque miré el
log con `head -30`, que cortó justo antes. El 191 posterior era una **copia atrasada** de la memoria,
no la verdad —la misma clase de copia que el §106 ya había cazado enseñando PS viejos—.

Y la regla nueva era dañina por eso mismo: con tres lecturas seguidas, esa copia atrasada a 191 habría
**reiniciado la cuenta** y la muerte real podría no haberse registrado nunca. Una lectura atrasada que
enseña vivo a un muerto está medida; una que enseñe muerto a un vivo, no. **Se ha quitado**, con sus
pruebas, y el vigilante vuelve a registrar con una lectura a cero.

Es el §55 dos veces en un mismo día: una medida parcial —aquí, una ventana de log recortada— sosteniendo
una regla que gobierna todas las muertes. Y la verificación que la habría cazado estaba a una línea: buscar
un «Conectado» *antes* de la muerte en vez de dar por hecho que no lo había.

### Lo que sí queda

**MARCAR COMO CAÍDO no funcionaba**: su desplegable solo se rellenaba después de marcar uno, cosa
imposible con la lista vacía. Las listas se cargan ahora al abrir la pantalla. Es la herramienta para
Ferropaladín: el combate acabó a las 00:29:34, la hora a la que el juego curó al equipo —y le devolvió
los PS a Okidogi, que la app volvió a tirar al suelo, como debe—, y ningún sondeo alcanzó a ver el cero.
Ese caso sigue sin solución automática.

**DESHACER UNA MUERTE** (`DeathRevoked`, al final del enum porque el tipo se guarda como número) se
queda, pero con su motivo real: deshacer una marca a mano sobre el Pokémon equivocado, que cobra puntos
y lo deja a 0 PS y no tenía vuelta atrás. La muerte se queda en el historial, la revocación se añade al
lado con su id, y se devuelve exactamente lo que se cobró. Cuatro pruebas.


---

## §112 · Fuera el recuadro verde de HOME (2026-09-13)

El §68 puso en HOME, en verde, «lo último que la app ha hecho sola»: premio entregado, caídos marcados
al cerrar el emulador y equipo caído. Desde que existen los avisos flotantes (`PlayNotifications`)
esas tres cosas se decían dos veces, y la de HOME además se quedaba puesta hasta el siguiente aviso,
contando algo de hace una hora como si acabara de pasar. A petición del jugador se quita el recuadro
y su propiedad; no se pierde nada, porque los cuatro eventos —muerte, caídos marcados, equipo caído y
premio— siguen saliendo como aviso. El recuadro rojo de problemas se queda: eso no es algo que haya
pasado sino algo que está mal ahora.

---

## §113 · «X HA MUERTO», encima del juego (2026-09-13)

A petición del jugador, una muerte ya no es solo un aviso de esquina. **El juego se congela en el
fotograma y se queda en gris**, entran dos franjas negras y del borde de la de arriba **bajan hilos de
sangre**. Lo único con color es el Pokémon: da un golpe —tres destellos en blanco con su temblor y la
pantalla teñida de rojo un instante— y **le sale sangre de su propio cuerpo**, que cae y se queda
donde toca el suelo; se sostiene un momento y **se hunde por debajo de su suelo** en ese charco, que
es como se debilita un Pokémon en los juegos. Debajo entra **«MOTE HA MUERTO»** —o la especie si no
tiene mote— **a lo Souls**: rojo, con serifa, espaciado, sobre una banda oscura, apareciendo despacio
y creciendo un poco mientras se lee. Debajo, pequeño y en gris, lo que ha costado. **Sin nivel.**

**La sangre es pixel art, en la rejilla del Pokémon** (`PixelBlood`). La primera versión de la sangre
eran elipses suaves, gotas repartidas y chorreones con la punta redonda, y el jugador la descartó por
genérica: es la sangre de plantilla, y además chocaba con lo único auténtico de la pantalla, un sprite
de píxeles del cartucho. Ahora el sprite se dibuja siempre a **7 unidades por píxel** —el icono más
grande del cartucho mide 40×30, medido sobre los 1508— en posiciones múltiplo de 7, y la sangre es un
mapa de bits de **una celda por píxel del Pokémon** puesto encima de esa misma rejilla y ampliado sin
suavizar: tres rojos y un brillo, sin degradados. Y con comportamiento en vez de adorno: las gotas
**salen de los píxeles opacos del sprite**, vuelan con gravedad y una celda de estela, se estiran
cayendo rápido, **se quedan donde tocan el suelo**; el charco crece en tres filas con el borde
irregular y algún bulto, y al hundirse el Pokémon salta sangre por los lados. El temblor mueve una
celda y el hundimiento baja celda a celda, para que sprite y sangre no se desalineen nunca. Los hilos
de arriba bajan a trompicones —se paran, arrancan, les cuesta más cuanto más largos— y **sueltan gotas**
que caen. Al irse la tarjeta, su sangre **se deshace celda a celda**, cada una en su momento fijo; la de
los hilos es de la escena y se queda. Se simula en `CompositionTarget.Rendering` con el reloj de la
ventana, el mismo que marca los tiempos del golpe y del hundimiento. Nada es un recurso ajeno y su azar
no decide nada.

La letra es Palatino Linotype, que trae Windows, con Book Antiqua y Georgia detrás; el espaciado se hace
con espacios finos, porque el texto de WPF no tiene propiedad para eso.

**Una tarjeta por muerte, en orden.** Las muertes se leen casi siempre al acabar el combate, todas
juntas, y un «han muerto tres» se comería justo lo que la pantalla quiere dar. La escena se abre
**una vez**, las muertes pasan por ella una detrás de otra con su nombre y su sprite, y si el equipo
entero cae va «EQUIPO CAÍDO» al final (el monitor anuncia las muertes antes que el equipo caído).

**Tres versiones el mismo día.** La primera se descartó por parecer genérica: lápida con «R.I.P.»,
viñeta roja, título grande con brillo rojo y todo centrado. La segunda sacó todo del propio juego —el
fotograma, el sprite, el desmayo— y escribía el nombre letra a letra en la franja de abajo, con los
puntos en violeta. Gustó, y el jugador pidió dos retoques que son la tercera: sangre, y el nombre a lo
Souls en rojo debajo del sprite. Los puntos pasan a gris porque el violeta al lado del rojo desentona.

Piezas:

- `DeathCeremony` recibe `DeathNotice(Name, Species, Penalty)` y hace la cola: abre la escena, pasa
  las tarjetas y la cierra; una muerte que llega mientras se cierra la vuelve a abrir. No decide ni
  registra nada: se entera de muertes **ya escritas**, y si falla cuesta la animación y nada más.
- `DeathWindow` es una ventana transparente, siempre encima y **intocable**
  (`WS_EX_TRANSPARENT | NOACTIVATE | TOOLWINDOW`, puesto antes del primer `Show`): no se lleva el foco
  ni los clics. Se coloca sobre el **área cliente de Azahar** y la sigue cada 100 ms; sin Azahar,
  sobre PermaLocke; sin ninguna de las dos, sobre el área de trabajo.
- **Con la app escondida en la pestaña del borde también sale**, y está medido: con Azahar abierto
  PermaLocke se minimiza sola, y vigilando el estado de sus ventanas cada 100 ms durante un ensayo
  entero la principal sigue minimizada antes, durante y después. La escena no depende de la ventana
  principal.
- El fotograma es `OverlayWindows.Capture`: una copia GDI de lo que hay en pantalla en ese sitio,
  tomada **antes** de enseñarse y solo en memoria. Como es lo que hay debajo, aparecer no se nota. Si
  no se puede leer, un velo oscuro hace de fondo. *(Aquí decía que sin `CAPTUREBLT` las ventanas por capas
  quedaban fuera de la copia. **Es falso**, y lo desmintió la primera killcam real: ver §115.)*
- El nombre sale del equipo **vivo** (el mote puede haber cambiado desde el registro), y los puntos de
  lo que **cobró de verdad** `RecordDeathAsync`, que ahora devuelve el `PenaltyResult`. De paso el
  aviso de esquina deja de decir «−25 puntos» escrito a mano, que era falso para el CAGONETA.
- Las muertes marcadas a mano en MANTENIMIENTO también tienen tarjeta (`MaintenanceService.MarkedDead`).

**Alcance:** al escribirse esto salía al acabar el combate, que es cuando PermaLocke veía la muerte. Desde
el §114 bis sale **en el momento en que la barra de vida se vacía**, porque la muerte se lee de las tablas
del combate; al acabar solo sale si el combate no se pudo leer.

**Ensayo:** `PermaLocke.App.exe --ensayar-muerte` reproduce las tarjetas de las **tres últimas
muertes ya registradas**, con lo que costó cada una leído de su propio evento de penalización, sin
escribir nada. Existe porque la animación solo se ve cuando algo muere.

Tres trampas de WPF, las tres vistas **solo en capturas del ensayo** y ninguna con error ni log:

1. **Animar un `Transform` con `Storyboard.SetTarget` directo no se aplicaba**: el título se quedaba
   a escala 1,35 —mayúsculas de 86 px donde tocaban 64— y se salía de la pantalla. Se apunta siempre
   al elemento con la ruta `(UIElement.RenderTransform).(ScaleTransform.ScaleX)`.
2. **`Storyboard.Remove` no actúa en el momento**: se aplica en el siguiente fotograma. Retirando la
   tarjeta anterior antes de empezar la siguiente, **solo se veía la primera muerte**: la retirada
   llegaba cuando ya había arrancado la segunda y le quitaba también sus animaciones. Su reloj corría
   y la escena se cerraba a su hora, pero no se movía nada. Ahora **nada se retira**: cada propiedad
   es una pista que empieza en el instante cero con su valor de salida (`Track`), y sustituye a la
   anterior en ese mismo instante. Eso cierra también la trampa de siempre de las animaciones con
   retraso, que enseñan el valor base hasta su turno.
3. `TaskCompletionSource` corre sus continuaciones **dentro** del `Completed` del storyboard; va con
   `RunContinuationsAsynchronously` para que la siguiente tarjeta no arranque ahí dentro.
4. **La duración de un storyboard corta a sus hijos.** Los chorreones de la primera sangre iban en la
   animación de apertura, de 700 ms, y empezaban a bajar después: el primer ensayo con sangre no tenía
   ni uno. La sangre de ahora no usa storyboards: es una simulación por fotograma.

Un `Viewbox` `DownOnly` encoge un mote muy ancho a una línea en vez de partirlo en dos frases.

---

## §114 · Los PS durante el combate: dónde viven, medido en un combate real (2026-09-13)

Una muerte solo se veía al acabar el combate porque la estructura del equipo que el juego lee fuera de
combate (§99, `0x1E4 + 0x158`) **no se toca durante el combate**: medido, Ferrocuello siguió en 176
mientras la pantalla decía 174, y pasó a 177/179 en el segundo en que terminó el anterior. El combate
lleva su propia copia, y esa es la que necesita una muerte en tiempo real.

### Primero, lo que costó: un emulador congelado y progreso sin guardar

La primera grabadora buscaba cada cuatro segundos en `0x08000000-0x0A000000` y `0x30000000-0x34000000`.
**Del montón solo existen los primeros 4 MB**: de `0x08425000` a `0x0A000000` no hay nada, el emulador
apunta un error por cada página que no existe, y el log llegó a su tope de 100 MB en minutos. Además
el emulador **no emula mientras busca**. A la segunda tanda Azahar se quedó congelado —medido: dos
lecturas de un búfer que cambiaba varias veces por segundo daban lo mismo— y el jugador perdió unos
diez minutos sin guardar. Su último guardado, de la 01:06, quedó intacto.

Se intentó rescatar ese progreso de la RAM (`Probe --rescate`, que solo lee y escribe aparte): la
partida no vive en memoria como un bloque contiguo sino en trozos, y recomponer un fichero de ahí no
se hizo, porque un trozo mal puesto estropearía la partida entera por diez minutos de juego.

**La regla que sale:** una búsqueda contra el emulador del jugador es una pausa del juego. Solo en
memoria que existe —el mapa se leyó del propio log del congelamiento, sin volver a sondear—, y solo
cuando el juego está parado esperando: **el menú de ataques espera todo lo que haga falta**.

### Cómo se encontró

`Probe --combate buscar | filtrar | vigilar`, en el menú de ataques:

1. **buscar**: los PS y el máximo de cada Pokémon del equipo juntos, en ocho disposiciones. 48
   búsquedas en **0,7 s**, 567 candidatos, **cero** errores nuevos en el log del emulador.
2. Un golpe: 176 → 174. **filtrar** leyó cada candidato una vez: **2** bajaron con su máximo intacto.
3. Los bytes de alrededor daban la forma, y buscando esa forma salieron todos los bloques.

### Lo que hay

Un **bloque de 800 bytes por Pokémon** (`0x320`), con la cabecera de reserva de Nintendo delante —`44 55`,
«DU», bloque en uso, y el tamaño— y los datos empezando por `E7 FF FF FF 20 00 00 00`:

| Offset | Qué | Ejemplo (Ferrocuello) |
|---|---|---|
| `+0x20` | puntero a su estructura de equipo de salto `0x1E4` | `0x3002E518` |
| `+0x28` | experiencia, 32 bits | `0x00040412` |
| `+0x2C` | especie | 993 |
| `+0x2E` | PS máximo | 179 |
| `+0x30` | **PS actual** | 174 |
| `+0x39` | **identificador en el combate**: 0-5 tu equipo en su orden, 12 el salvaje | `00` |

Los bloques forman una **tabla indexada por ese identificador**, a `0x330` uno de otro: el del salvaje
está exactamente 12 posiciones después del primero. Van **los seis del equipo, muertos incluidos** —
Flamariete, Ferropaladín y Okidogi a 0—, y **no se reordenan al cambiar de Pokémon**: Salamence recibió
su golpe en su bloque de siempre.

**Hay dos tablas**, y no son iguales:

| | Tabla B (`0x30009730` en este combate) | Tabla A (`0x30002748`) |
|---|---|---|
| Al recibir un golpe | cambia **en el acto** | cambia hasta **3 s después**, con valores intermedios (199 → 197 → 173) |
| Al acabar el combate | **se libera**: su memoria se llena de otra cosa | **se queda**, con los PS finales |

B es la del cálculo y A la de la pantalla: A baja con la barra de vida. Para la ceremonia importa A —
llega a 0 cuando la barra llega a 0—, y para saber que se está en combate importa B, que solo existe
durante él. Fuera de combate la búsqueda de la cabecera devuelve **solo la tabla A**, sin rival.

### Lo que falta por medir antes de dar esto por bueno

- Si las direcciones **se repiten** de un combate a otro. El diseño no debería depender de ello: la
  búsqueda de la cabecera es una sola llamada y cuesta milisegundos.
- Combates contra **entrenadores**, **dobles** y **SOS** (más identificadores en la tabla).
- Una muerte de verdad. No se va a provocar: el mismo mecanismo se puede comprobar con el **rival**,
  cuyo bloque llegó a 0 al vencerlo.

### Lo que no se debe hacer con esto

- **Fiarse de la tabla A fuera de combate.** Se queda con los PS de la última pelea; un Pokémon curado
  después en un Centro seguiría a 0 ahí y sería una muerte falsa. Una muerte en combate exige que la
  tabla B exista.
- **Buscar en bucle.** Es exactamente lo que congeló el emulador.

### §114 bis · La muerte en el momento, implementada y vista contra el juego

`PermaLocke.GameLink/Battle`:

- **`BattleLayout.Parse`** lee un bloque desde su cabecera y **rechaza** todo lo que no sea un bloque de
  combate vivo con un Pokémon: cabecera «DU» con su tamaño, los ocho bytes de salida, puntero, especie y
  máximo distintos de cero, y PS que no pasen del máximo. Los bloques se liberan al acabar el combate y su
  memoria se reutiliza enseguida, así que esto no es celo: medido, la tabla del cálculo acababa llena de
  números como 35027 y 12042.
- **`BattleFaintTracker`** decide la caída sin tocar la memoria: hay combate si **dos tablas tienen rival**,
  y un Pokémon cae cuando **las dos lo dan a cero después de haberlo visto en pie**. La tabla que se queda
  tras el combate no tiene rival, y la ausencia de rival es lo que impide que un Pokémon curado después
  en un Centro se lea como muerto.
- **`BattleTableReader`** es el único que habla con el emulador, con las reglas del congelamiento en el
  código: fuera de combate, **una búsqueda de un megabyte cada 3 s**; toda la memoria lineal, solo si ese
  megabyte no tiene ni un bloque y como mucho **una vez por minuto**; dentro de un combate, **ninguna
  búsqueda**, solo relecturas de los bloques conocidos.

`GameLinkMonitor` tiene un **segundo bucle**, a 4 lecturas por segundo en combate y a una fuera. Si la caída
es de una posición 0-5 **y la especie cuadra con ese hueco del equipo**, registra la muerte y lanza la
ceremonia en el acto; si no cuadra, no registra nada y lo deja para la comprobación del equipo, porque
equivocarse de muerto es peor que verlo tarde. Las dos vías pasan por **la misma puerta**: cada una vuelve
a mirar si el Pokémon sigue vivo en la run antes de cobrar, así que al terminar el combate la comprobación
del equipo encuentra el cero y no hace nada. Mientras dura un combate tampoco se devuelve al suelo a nadie
en la estructura del equipo: los bloques del combate apuntan a esas estructuras y el juego ya copia el cero
al terminar. Los rivales solo se apuntan en el log, «Rival debilitado en combate».

**Verificado sin que muriera nadie**, con `Probe --combate detector`, que ejecuta el mismo lector y la misma
decisión sin registrar nada. Dos combates salvajes ganados:

| | Shuppet | Pumpkaboo |
|---|---|---|
| Combate localizado, sin ayuda | 14:38:15.050 | 14:38:29.662 |
| Tabla del cálculo a 0 | 14:38:18.196 | 14:38:30.443 |
| **Caída detectada** (las dos a 0) | **14:38:18.461** | **14:38:30.965** |
| Tablas soltadas | 14:38:20.565 | 14:38:33.827 |

Las dos veces en `0x30002748` y `0x30009730`. Los tres caídos de la run estaban a cero desde la primera
lectura y no se tomaron por muertes. Cero errores nuevos en el log del emulador.

**Lo que sigue sin verse**, dicho claro: la muerte de un Pokémon del jugador —la decisión es la misma que
con el rival y la especie se comprueba, pero no se ha provocado ninguna—, y los combates contra
entrenadores, dobles y SOS. Si alguno pone las tablas en otro sitio, el lector las encuentra en la búsqueda
amplia, a costa de hasta un minuto de retraso; y si no las encuentra, la muerte se sigue viendo al acabar
el combate como antes.

### §114 ter · La animación saltaba con la barra bajando, y el segundo congelamiento

**Lo que se vio.** La primera muerte real con la detección puesta fue un Leavanny de 12 PS. El vídeo del
jugador, fotograma a fotograma: la barra bajó 12 → 11 → 9 → 6 → 4 → 2 en unas tres décimas, y la escena
de muerte congeló la imagen con **5/12**. La detección llegó tarde para el cálculo del juego y **pronto para
la barra**.

**Por qué.** Se midió con pantalla y memoria en el mismo reloj (`Probe --combate barra`, que lee las dos
tablas y captura la ventana de Azahar con marca de tiempo): la tabla del cálculo cambia **al elegir el
ataque**, y la otra **cuando aparece «¡X ha usado Y!»**, antes de la animación del ataque. La barra baja
después de esa animación, que dura distinto con cada movimiento. El §114 llamó a esa tabla «la de la
pantalla» por su retraso; su retraso es el del mensaje, no el de la barra, y la escena salta con ese
mensaje. Ninguna espera fija lo arregla.

**El segundo congelamiento.** Para encontrar el valor animado de la barra se lanzó una búsqueda de 137
formas sobre los 64 MB de la memoria lineal, con el combate en el menú. El juego **se paró en el segundo
876,5 del emulador, en mitad de la ráfaga** —su última llamada al sistema, un `nwm::UDS` que hacía cada
0,4 s, es de ese instante— y no volvió; el jugador perdió lo que no había guardado. La memoria ya no era
inexistente: con 48 búsquedas seguidas aguantó dos veces, con 137 no. **Lo que lo tumba es buscar mucho
seguido mientras el juego corre**, así que:

- No se lanzan más ráfagas de búsquedas grandes contra la partida del jugador.
- `BattleTableReader` pierde la búsqueda de 64 MB «una vez por minuto si no hay nada»: se queda con la de
  un megabyte cada 3 s, que es 64 veces menor y lleva toda una tarde de juego sin un problema. Un combate
  que ponga las tablas fuera de ese megabyte ve sus muertes al terminar.

### §114 quater · La escena salta cuando la barra llega a cero, mirando la barra

Elegido por el jugador entre tres caminos (mirar la barra, una espera fija, buscar la barra en memoria
despacio), porque es el único exacto que **no pide nada al emulador**.

- **`HpBar`** (GameLink, sin WPF) lee una fila de la barra a partir de sus píxeles: celda **con color**
  —verde, amarillo o rojo saturados— o **hueco** —el gris neutro (67,67,64)—. Si entre los dos no llenan
  la fila, la barra **no se ve**; si se ve y no queda color, está **vacía**. Colores y posición medidos en
  fotogramas del vídeo del jugador: la barra propia va de x 5 a 89 en las filas 218-220 de la pantalla de
  400×240, y la del rival de x 287 a 370 en las 21-23.
- **`HpBar.ZeroWatch`** decide el momento: vacía **justo después de haberla visto con color**, dos lecturas
  seguidas. Sale de una medida: quieto en el menú y sin que nadie recibiera un golpe, la caja se ocultó y
  volvió como siete segundos de gris; una barra que llega a cero lo hace a la vista, nunca apareciendo ya
  vacía.
- **`HpBarWatcher`** (App) captura con GDI esa franja de la pantalla cada 15 ms y espera como mucho 6 s; si
  no ve la barra —ventana tapada, otra disposición de pantallas— la escena sale igual, tarde en vez de no
  salir. La imagen se toma de la **superficie donde Qt dibuja el juego**, una ventana hija de clase
  `Qt…QWindowOwnDC…`, y no del área cliente: Windows cuenta la barra de menú de Azahar dentro del cliente, y
  medido en la ventana del jugador la pantalla de arriba empezaba en y 56 con el cliente en 23. Con el área
  cliente, la primera prueba leyó «oculta» con la barra delante.
- `GameLinkMonitor` espera a la barra entre la caída que dicen las tablas y el registro de la muerte.

**Verificado contra el juego con la barra del rival** (`Probe --combate ver-barra`, que solo mira la
pantalla): un Goomy bajó 19 → 15 → 10 → 4 en rojo, la barra apareció vacía a los 40 780 ms y la regla dio el
cero a los **40 815 ms**, en el fotograma siguiente. Durante el combate las cajas se ocultaron y
reaparecieron decenas de veces con las animaciones, y ninguna de esas veces disparó nada.

**Sin ver todavía:** una muerte del jugador con esto puesto, y combates dobles, donde hay dos cajas propias
y solo se mira la primera; si cae el Pokémon de la segunda, la escena sale a los 6 s.

**Corrección tras la primera muerte con esto puesto (Maushold).** La escena salió unos 3 s tarde, y el log
dijo por qué: «barra vista pero sin llegar a cero en 6 s», o sea el plan de respaldo. La regla pedía dos
lecturas vacías pegadas a una con color, y cada lectura localizaba otra vez el proceso de Azahar y su
superficie de dibujo, de 8 a 11 ms medidos, más la captura y la pausa. Dos cambios:

- La ventana se localiza al empezar y cada medio segundo, no en cada lectura.
- `HpBarReading` lleva **cuánto color** tiene la barra, y `ZeroWatch` acepta un vacío **en una sola lectura**
  si antes vio la barra **en rojo, a lo sumo un 30 %**, aunque haya un instante oculto por medio (hasta
  600 ms). El caso falso del menú sigue fuera, porque allí la barra estaba **llena** antes de ocultarse.

Y si vuelve a salir por tiempo, el log guarda **la secuencia de lecturas** con sus milisegundos, para que la
próxima vez se sepa qué vio en lugar de suponerlo.

**Verificado con una muerte del jugador (2026-09-13, 15:53:41).** Empoleon cayó en combate; el log dice
«barra a cero vista a los 478 ms» —el cero en pantalla, no el plan de respaldo— y la muerte se registró dos
milisegundos después. El jugador lo confirmó mirando: la escena salió justo a tiempo.

## §115 · CEMENTERIO y killcam (2026-09-13)

Una sección con **una tumba por cada caído** y, al elegir una, su historia: ante quién cayó, cuándo, de
dónde venía, lo que costó, cómo se supo y, si la hay, **la repetición de su muerte**. Nada de lo que enseña es
dato nuevo: la caída es el `PokemonDied`, el coste la suma de sus `PointsPenalty` tal como se cobraron, los
rivales lo que las tablas de combate tenían en ese momento (`rivales` en el evento, §114 bis) y la
repetición el fichero que escribió la killcam. Una muerte de antes de que existiera todo eso **lo dice** en
vez de rellenar el hueco.

**La escena** (`CemeteryScene`) es pixel art por lo mismo que la sangre del §113, y cada celda cae en un
número **entero** de píxeles de pantalla: el tamaño de la rejilla sigue al panel, descontando el escalado de
Windows. La primera versión estiraba 480 celdas a lo que midiera el panel —1,85 veces en la ventana del
jugador— y eso dibuja unas celdas de dos píxeles y otras de uno; la trama se vuelve moaré. **El tamaño de la
tumba dice cuánto aguantó**: cruz de madera menos de un día, piedra menos de cuatro, lápida menos de diez,
obelisco a partir de ahí, dicho en la propia escena. Tierra removida si cayó en el último día y un destello
si era variocolor, también de la run. La niebla, los fuegos fatuos, las estrellas y el vaivén de los
fantasmas no significan nada y no pretenden hacerlo. El elegido sube y recupera su color.

**La killcam** (`KillcamRecorder`) guarda la pantalla de arriba a 20 fps, a su tamaño nativo de 400×240,
**solo mientras hay combate** y solo los últimos 7 s. Al ver la barra a cero se marca el instante y se guarda
de −4,5 s a +1,3 s en `Saves/killcam/<run>/<pokémon>.killcam` (fotogramas JPEG con su tiempo respecto a la
caída, así que un hueco es una pausa y no un desplazamiento). No pide nada al emulador: después de dos
congelamientos buscando en su memoria (§114 ter), ese es el requisito. El reproductor va a velocidad real o
a cámara lenta, salta a cualquier punto de la barra, avanza fotograma a fotograma, marca en rojo el cero, y
**se amplía** a toda la sección.

Tres cosas que salieron probándola y que conviene no repetir:

1. **Copiar la pantalla copia lo que haya delante.** El primer ensayo guardó 93 fotogramas perfectos de
   *Grounded 2*, que estaba a pantalla completa encima de Azahar. `GameWindow.Shows` pregunta a nueve puntos
   de la zona qué ventana hay (`WindowFromPoint`), y si alguno no es Azahar el fotograma no existe; la espera
   de la barra del §114 quater usa lo mismo y lo cuenta como oculta. Verificado en los dos sentidos: con
   Azahar a la vista, 93 fotogramas del Centro Pokémon; con una ventana casi invisible delante, cero, y el log
   la nombra.
2. **Sin `CAPTUREBLT` las ventanas por capas NO quedan fuera.** El §113 y `OverlayWindows` lo daban por
   hecho. La primera killcam real (Exploud ante Duskull) lo desmintió: hasta **+597 ms** es el juego, y
   desde **+646 ms** baja la franja negra de la escena de PermaLocke, luego el gris y el sprite. Con el
   escritorio compuesto, la copia es lo que se ve. La escena avisa ahora a la killcam al taparlo
   (`CoverBegins`/`CoverEnds`) y no se guarda nada mientras está; el clip acaba en el último fotograma del
   juego. Ocultar la escena de toda captura era el otro camino, y la habría ocultado también a quien retransmita
   la partida. La killcam de Exploud se recortó a mano a sus 84 fotogramas buenos.
3. **Las ventanas transparentes al ratón no contestan a `WindowFromPoint`**, medido con una ventana así
   encima de Azahar: el punto seguía diciendo `azahar`. Por eso los avisos y la escena no hacen perder
   fotogramas, y por eso mismo **no** sirven para saber si salen en la copia.

**Sin ver todavía:** una muerte con los tres arreglos puestos a la vez.

## §116 · El cielo de Alola (2026-09-13)

Detrás de la cabecera de cada sección hay una **franja en pixel art con el mar y el cielo de Alola a la hora que
es en el juego**, y al pie de la barra lateral una ventanita con lo mismo y «ALOLA · Día · 10:01». Solo
presentación: no decide ni registra nada.

**De dónde sale la hora, sin tocar la memoria del juego** (`GameLink/Clock/AlolaClock`):

- Con `init_clock=0`, Azahar arranca el reloj de la consola en la **hora local** del ordenador. Leído en su
  código (`core/hle/kernel/shared_page.cpp`): toma el reloj UTC, suma una hora si hay horario de verano y resta
  una época construida con `mktime`, que es local. Luego suma `init_time_offset`, **en segundos**, con una
  aritmética rara para los negativos que se copia tal cual (hay test).
- Ultra Luna va **12 horas cambiada** respecto al reloj de la consola. Todo lo que PermaLocke lee es Ultra Luna
  (title id `00040000001B5100`), y la partida del jugador lo confirma: `SAV7USUM.Version = UM`. La ventana de
  Azahar pone «Pokémon Ultra Sun», pero es solo su etiqueta.
- La configuración es la del Azahar **que está abierto**, localizado con la regla del propio emulador (carpeta
  `user` junto al ejecutable, o `%APPDATA%\Azahar`): el jugador no abre el que trae PermaLocke (§95). Se
  mira cada 20 s.
- Con la hora **fija** (`init_clock=1`) no se puede saber cuánto lleva la partida en marcha, así que no se
  inventa: la franja no se dibuja y la barra lo dice.

Los tramos del día son los de la séptima generación —mañana 6:00-9:59, día 10:00-16:59, atardecer
17:00-17:59, noche 18:00-5:59— y **no están medidos contra el juego**; los colores del tinte y de la ventanita
son dibujo, no el cielo real del juego. Verificado: con las 22:01 locales la barra dijo **10:01 · Día**.

**Tres versiones, y la que queda es la tercera.** La primera teñía el suelo de la ventana, tan poco que no se
notaba. La segunda pintó la ventana entera con degradados suaves, un resplandor que cruzaba la pantalla, una
franja de horizonte difuminada y estrellas repartidas por la interfaz, con las barras translúcidas para verlo
detrás. **El jugador dijo que parecía hecha por una IA, y lo parecía**: es exactamente el aspecto de un fondo
generado. Se tiró entera. Lo que sí había funcionado aquí era lo contrario —la sangre y el cementerio,
dibujados celda a celda (§113, §115)—, y la franja es eso:

- **Contenida**: detrás de la cabecera, acabando justo en su raya (`MainWindow` iguala su alto al de la
  cabecera). El resto de la aplicación tiene el suelo de siempre y las barras vuelven a ser opacas.
- **Píxeles enteros**, dos por celda descontando el escalado de Windows, pocos colores planos con trama
  ordenada, y el cielo oscurecido un tercio porque encima van el título, los puntos y los distintivos.
- **Formas escritas a mano**: las cuatro islas con su silueta columna a columna —Melemele, Akala con el cráter
  de Wela, que de noche tiene lava; Ula'ula subiendo al monte Lanakila, con nieve; Poni con sus
  acantilados—, y las nubes y los pájaros como pequeños sprites escritos en texto dentro del código.
- **Se mueve a saltos**, cuatro veces por segundo: nubes que avanzan de celda en celda, pájaros que baten las
  alas un paso sí y otro no, rayos del sol que alternan, estrellas fijas que se apagan un instante. Nada se
  desvanece suavemente.
- El sol y la luna recorren el arco **solo hasta el 79 % del ancho**: al principio el sol se ponía detrás de
  «PUNTOS» y se comía la palabra. Las islas van en el tramo central por lo mismo.

**La ventanita** (`AlolaWindow`) es de la misma familia, 86×48 celdas a dos píxeles, animada a seis fotogramas
por segundo porque está en pantalla cientos de horas. Las dos toman los colores de `AlolaPalette`, así que no
pueden discrepar sobre qué hora es. **Solo aparece si cabe**: en NORMAL (760 de alto) la lista de
secciones ya ocupa casi toda la barra, y `MainWindow` mide la lista antes de enseñarla para no ponerle una barra
de desplazamiento a la navegación.

Una primera versión escondía la puesta de sol detrás de la palmera: el sol sale por el borde izquierdo y se pone
por el derecho, así que las islas van en el tramo central. `--hora-alola HH:mm` fuerza una hora para mirar el
cielo sin esperar, y no toca nada más.

**Límites dichos:** pausar el emulador retrasa el reloj de la consola respecto al del ordenador (avanza con el
tiempo emulado), y con el escalado de Windows al 125 % las celdas de la ventanita no caen en píxeles enteros.

## §117 · La regla de primer encuentro, por fin con zona (2026-09-14)

El jugador la retomó porque en la competición va a haber gente que apenas conoce. Lo que decidió:

- **En las rutas del MAPA** —las 61 que colocó a mano— el **primer combate salvaje gasta la ruta**, pase lo
  que pase en él: huir, debilitarlo o capturarlo.
- En una ruta gastada **no hay Poké Balls**.
- **Duplicados**: si ya tienes su **línea evolutiva** (por la Pokédex capturada de la partida o por la run),
  no salen Poké Balls **y la ruta sigue libre**.
- **Un variocolor se puede atrapar siempre.**

La maquinaria de quitar y devolver Poké Balls existía desde el §24; lo que faltaba desde el §55 era **saber
dónde está el jugador**. Todo lo que sigue se midió contra la partida del jugador con él moviéndose.

### Dónde está el jugador: los registros de posición

**La partida guardada como verdad.** El bloque `Situation` de la partida trae el **mundo** en +0 (lo que PKHeX
llama `M`), el **mapa** en +2 y la posición en +8, +0xC y +0x10. Con la partida recién guardada y el jugador
quieto, una búsqueda de esos 12 bytes exactos dio **dos copias que solo cambian al guardar**: volando a la Ruta 2
no se movieron. Una búsqueda de la X sola por los 64 MB de la zona lineal dio la pista buena: justo delante de
las coordenadas vivas estaban `00 00 07 00`, **mundo 0, mapa 7 = Ruta 2**.

**El registro**, 0x24 bytes: `u16 mundo, u16 mapa, f32 X, Y, Z, f32 qx, qy, qz, qw, FFFFFFFF`. Hay varios: uno
sigue cada paso y otros guardan por dónde se entró al mapa, y **todos cambian de mapa en el mismo instante**.
Comprobado contra el cartucho (`RomTool mapas`) en doce sitios, con el mundo que el cartucho da a cada mapa:

| Recorrido del jugador | Leído | Cartucho |
|---|---|---|
| Ruta 2 → cementerio → Ruta 2 → Hauoli | 0/7, 15/28, 0/7, 0/13 | Ruta 2, Cementerio de Hauoli, Ruta 2, Ciudad Hauoli (Zona Comercial) |
| Vuelos | 227/304, 0/6, 123/187, 208/283, 117/171 | Paraíso Æther, Ruta 3, Pico Hokulani, Altar de la Luna, Ruta 16 |
| Centro Pokémon de la Ruta 16 | 194/261 | lugar: Ruta 16 |

**Por qué se valida así** (`FieldRecord.Parse`): durante un vuelo los registros leen «mundo 600, mapa 12288» y
ceros; durante un combate, «mundo 35, mapa 89», que es un mapa real **de otro mundo** (Pueblo Ohana es del 58).
Exigir que el mapa pertenezca a ese mundo según el cartucho descarta todo eso. Además: posición distinta de cero
(un registro muerto lee ceros, y mapa 0 mundo 0 es la Ruta 1), X, Y y Z no iguales (la búsqueda por forma trae
decenas de vectores 1, 1, 1), rotación de longitud 1 y `FFFFFFFF` detrás. **Hacen falta dos copias de acuerdo**:
dos de las cuatro del primer día murieron en el primer vuelo, y la lección del §55 es no creerse una sola.

**Cómo se encuentran sin quemar direcciones** (`FieldZoneReader`): con la **posición guardada** —justo después
de cargar, los registros tienen exactamente mundo, mapa y posición de la partida: tras reiniciar el emulador dio
cinco en 14 ms— y con la **firma del aterrizaje** —la rotación identidad y `FFFFFFFF`, que es lo que queda al
llegar a un mapa: 68 coincidencias en 67 ms—. Las direcciones sobrevivieron al reinicio, pero no se usan como
fijas. Una búsqueda de cada al conectar, y otra solo si nada valida durante 20 s y han pasado 2 minutos.

`Data/mapas.json` lo genera `RomTool mapas --escribir`: 380 mapas con su mundo y el id de zona normalizado del
**nombre de PKHeX para su `ParentMap`**, que casa con el del cartucho en los 380 y es el mismo id que usan la run
y el MAPA. Las 61 rutas del MAPA tienen mapa; ninguna se queda fuera.

### Si un combate es salvaje: los contadores del juego

El identificador 12 de las tablas de combate **no distingue salvaje de entrenador**: un Gastrodon de entrenador
salió en el 12. Los **récords de la ficha de entrenador** sí, y están en memoria con el formato de la partida (100
de 32 bits y 100 de 16): búsqueda de los diez primeros de la partida, **una coincidencia**, a 0x68114 de la
mochila. El récord **4, combates salvajes, sube al empezar el combate**, antes de que aparezcan las tablas, **y
cuenta aunque huyas** (Boldore huido, Sewaddle debilitado, Aipom capturado). El 6 son capturas y el 46 huidas,
y con ellos se sabe al acabar cómo terminó, para marcarlo en el MAPA. El 127, variocolor encontrados, es la
cláusula shiny: **su posición está calculada, no medida**, y se valida aparte para que un error no se lleve los
demás (`BattleCounterReader`).

**Quitar Poké Balls dentro del combate funciona**: con 14 Super Ball puestas a 0 en mitad de un combate, la
mochila del combate enseñaba 0. Al devolverlas vuelven **al final del bolsillo**, no a su hueco.

### Cómo queda

- `EncounterPolicy`: la decisión, pura y con tests. Toda duda (zona desconocida, sitio que no es ruta, especie
  que no se llega a leer en 8 s) acaba con el jugador conservando sus Poké Balls.
- `BallControlService`: retira y devuelve con evento `BallsWithheld`/`BallsReturned` con el motivo; gasta la ruta
  con `ZoneEncounterSpent` (**nuevo, al final del enum**); y marca el resultado en el MAPA con `ZoneOutcomeSet`
  firmado como `AutoDetect`. **Marcar una ruta como libre en el MAPA no deshace un combate detectado.**
- La mochila **suma**: `Withhold` añade a lo debido y `GiveBack` devuelve encima de lo que lleves. Antes
  sobrescribía, y unas Poké Balls compradas mientras las otras estaban retiradas se habrían perdido.
- `EncounterGuard` (App): en el bucle de combate, dos veces por segundo fuera y cuatro dentro. Al subir el
  contador pide las tablas al momento (`BattleTableReader.SearchSoon`) y, mientras no sabe qué salvaje es, retira.
  Con zona desconocida fuera de combate deja la mochila como estaba 60 s antes de devolver.
- Las líneas evolutivas salen del `a/0/1/4` del mundo instalado (`WorldEvolutionLines`). Avisos en pantalla con
  el motivo.
- Retirado lo muerto del §23: `ZoneLocator`, `ZoneService`, `ZoneTable` y `Probe --zona`.

**Sin ver todavía:** la regla entera en una partida —esto está compilado y con tests, y cada pieza medida por
separado, pero no se ha jugado con ella encendida—; un variocolor real; combates dobles y SOS; y cuánto tarda
de verdad en retirar al empezar un combate con un duplicado.

## §118 · El MAPA se marca solo (2026-09-14)

Petición del jugador, antes de probar los variocolor: que el MAPA **no se pueda marcar a mano**, que lo marque la
aplicación leyendo la zona, **para que no se hagan trampas**. Tiene sentido por algo más que la desconfianza: desde
el §117 la regla de las Poké Balls **lee** el mapa —una ruta marcada cuenta como gastada—, así que un mapa que
cualquiera pincha era un árbitro que cualquiera mueve.

**Lo que marca**, y es lo mismo que ya decidía la regla: el **primer combate salvaje de una ruta libre** del MAPA.
Al empezar se gasta la ruta (`ZoneEncounterSpent`) y al acabar se marca cómo terminó (`ZoneOutcomeSet`, firmado
`AutoDetect`, con la especie). Un duplicado no gasta la ruta y no la marca; un variocolor, según
`shinyClause.consumesEncounter`. Cómo terminó sale de `EncounterPolicy.Ending`, pura y con tests:

| Leído | Marca |
|---|---|
| El récord 6 (capturas) subió | atrapado |
| El récord 46 (huidas) subió | huida |
| Las tablas vieron al salvaje a cero | muerto |
| Nada de lo anterior | huida, diciendo en el evento que no se contó nada |

La última fila es una decisión: el salvaje se teletransportó o fue expulsado, o el jugador perdió, o las tablas no
vieron el K.O. Dejarla sin marcar tendría el mapa diciendo «sin marcar» de una ruta que la regla ya trata como
gastada, **y ya nadie puede marcarla a mano**. Las cuatro gastan la ruta por igual, así que equivocarse cuesta la
etiqueta, y el motivo queda escrito tal cual.

**Independiente de la regla de las Poké Balls.** `EncounterGuard` salía en la primera línea si `ballControl` estaba
apagada, y con ella se iba el mapa. Ahora sigue combates y marca siempre, y solo retirar y devolver Poké Balls mira
el interruptor: un mapa que solo se rellena mientras otra opción está encendida es un mapa que deja de ser un registro
sin avisar.

**Dos huecos que se cierran al pasar a ser la única vía:**

- **Zona desconocida al empezar** (al conectar, antes de la primera búsqueda; o si el lector falla). Antes el combate
  se perdía para el mapa. Ahora se lee la zona **al acabar**, porque un combate no te mueve de sitio… salvo perderlo,
  que te lleva al último Centro Pokémon, y **hay Centros en las rutas**. Por eso, si hubo una baja propia, la zona de
  después solo vale si es la última que se leyó antes de empezar. Y si resulta ser una ruta ya gastada con una captura
  dentro, se avisa y se deja en el log: no se pudieron retirar las Poké Balls a tiempo.
- **Algo sin leer al acabar** (la zona o los contadores, mientras se recarga el campo). Se reintenta en cada vuelta
  hasta 15 s; pasado eso, o si empieza otro combate —cuyos contadores ya se mezclan con los de este—, **se deja sin
  marcar y se dice** con un aviso, en vez de adivinar. Un combate cuyas tablas nunca aparecieron se da por acabado a
  los 3 minutos como mucho, para que un lector de zona roto no ciegue todos los siguientes.

**Las marcas a mano que ya había.** La run del jugador tiene **418 clics**, que dejan **18 zonas marcadas**, y 0
detectadas (medido sobre una copia de la base de datos). Se quedan: eran suyas y las marcó comprobándolo. Pero
`ZoneOutcomeService` las distingue (`ZoneMark.ByPlayer`), la carta de la zona dice «marcada a mano, de cuando el
mapa se pinchaba», y **una marca a mano nunca pisa ni borra una detectada**, venga de la historia vieja o de una
versión anterior de la aplicación; al revés sí: lo detectado sustituye a lo pinchado. `SetAsync` ya no tiene
`source` por defecto, para que nadie vuelva a escribir como jugador sin decirlo.

**La pantalla**: los marcadores siguen siendo `Button` por la plantilla y la carta del ratón, pero sin comando, sin
foco y sin cursor de mano, y sin la onda al pulsar, que prometían un clic que no hace nada. La carta enseña contra qué
Pokémon fue y cuándo se marcó. El mapa se refresca solo con `RunDataChanged`, porque ahora cambia mientras juegas.

### La primera prueba: «no se sabe dónde fue»

El jugador huyó de un Haunter en los **Jardines de Ula-Ula** y salió el aviso de zona desconocida. El log lo cuenta
entero: conectado a las 02:08:29, la búsqueda encontró **un registro** (`0x33F6E448`), el combate empezó 20 s
después, y hasta las 02:09:15 no hubo zona. **Con una copia no se decide nada** —hacen falta dos— y **con una copia ya
encontrada no se volvía a buscar en 2 minutos**. Leyendo solo 36 bytes de las tres direcciones medidas el día anterior,
las tres estaban vivas y decían Jardines de Ula-Ula: las copias estaban, lo que fallaba era encontrarlas.

El motivo es de las dos firmas de búsqueda, que **solo casan en dos momentos**: la posición guardada, justo al cargar
y antes de moverse, y la rotación identidad, justo al aterrizar. Un jugador que conecta PermaLocke después de dar
unos pasos no está en ninguno: sus registros miraban de lado, `(0, −0,707, 0, 0,707)`.

Dos arreglos, los dos en `FieldZoneReader`:

- **Las direcciones de la última sesión se prueban antes de buscar**, como ya hacen el equipo y la mochila, en
  `Saves/backup/registros-de-posicion.txt`. No se confía en ellas por eso: se leen y validan como cualquier otra, y
  solo valen mientras dos coinciden. Se guardan solo cuando una búsqueda acaba en zona.
- **Si la búsqueda no da dos de acuerdo, una búsqueda más de «hermanas»** (`FieldRecord.SiblingPattern`): el mundo y
  el mapa delante, `FFFFFFFF` al final y **todo lo de en medio comodín**. El mapa lo dice la copia que sí se encontró,
  o la partida guardada si no hay ninguna. Medida contra el juego en el mismo sitio: **6 candidatos en 16 ms, los 6
  válidos**. Ocho bytes fijos es poco a propósito y todo pasa por `Parse`; para un mapa con todos los bytes a cero
  (Ruta 1) los candidatos pueden llenar la respuesta y dejar registros fuera.

Con eso la búsqueda al conectar pasa de cuatro a cinco como mucho, siempre sueltas y con los 2 minutos de separación.

### La segunda prueba: «siempre va un mapa por detrás», y el shiny que no se veía

Con el parche de todo variocolor puesto, el jugador contó dos cosas: al pasar a una ruta PermaLocke le detectaba la
anterior, y al salirle un shiny no le devolvía las Poké Balls.

**El mapa anterior.** Al conectar, la búsqueda de hermanas del mapa 83 (Ruta 7) dio **dos** registros:
`0x33F6E448` y `0x33F6E490`. Leyendo siete a la vez con el jugador en la Ruta 2, **seis decían Ruta 2 con la misma
posición al decimal y `0x33F6E490` decía Ciudad Hauoli** en otra posición: **ese registro guarda el mapa anterior**.
Justo después de cargar coincidía con los vivos, y por eso la búsqueda lo cogió. Con la regla de «todos de acuerdo»,
tras cada cambio de mapa esos dos discrepaban, la zona salía desconocida y el combate se asignaba a la **última zona
confirmada**, que aún tenía menos de 60 s: la del mapa que se acababa de dejar. Un Dewpider quedó apuntado en Ciudad Hauoli
(Zona Comercial), con la ruta gastada y marcada como huida; **está sin confirmar dónde fue de verdad**.

Tres arreglos:
- `FieldRecord.Resolve` pasa a **mayoría estricta con al menos dos**: el registro atrasado pierde la votación en vez
  de vetarla, y un empate sigue siendo «no se sabe».
- `FieldZoneReader`: si hay lecturas válidas sin mayoría, **busca otra vez a los 20 s** en vez de a los 2 minutos,
  y busca las hermanas de **cada mapa que se ve**, hasta dos. Y una búsqueda que no encuentra nada **ya no vacía la
  lista**: tras el combate del Dewpider se buscó con el campo recargándose, salieron 0 y se quedó sin zona.
- `EncounterGuard`: un combate solo se asigna a una zona confirmada **hace 5 s o menos**, en vez de 60. La zona se
  confirma dos veces por segundo mientras se lee; si lleva más sin leerse, la del combate se lee al acabar.

**El shiny, en dos intentos.** La posición calculada del récord 127 **es la buena**: en memoria valía 4, en la
partida guardada 0, y desde que se guardó hubo exactamente cuatro combates salvajes, todos variocolor. El primer
arreglo supuso que subía en la misma lectura que el récord 4 y comparó con la lectura de antes del combate. **Era
falso**, y lo dijo la línea de log que se añadió para comprobarlo: el siguiente combate, contra un Wooloo variocolor en
una ruta gastada, empezó con «variocolor 5 -> 5», y leído a mano en pleno combate el récord seguía en 5. **El juego
cuenta el variocolor más tarde**, demasiado tarde para devolver las Poké Balls. Supuse una causa a partir de un
síntoma y la escribí como medida; la medida la trajo el log.

Lo que sí sirve es **el propio salvaje** (`BattlePokemon`): leyendo alrededor del puntero del bloque del rival, en las
**dos** tablas del combate hay un PK7 cifrado con firma válida y la especie del bloque en **puntero + 0x40**, con PID
`7D64F0DE`, nivel 8 y el TID y SID del jugador, que el salvaje ya lleva; `IsShiny` dice que sí y cuadra con el PID
contra la ficha del jugador. `EncounterGuard` lo lee en cuanto aparece el bloque y usa su brillo; el récord 127 se queda
de segunda señal. El test usa los 296 bytes leídos de ese combate. **Medido con un solo combate salvaje**: sin ver en
combates dobles, SOS ni contra entrenadores.

**Y la huida no contada.** El Ditto de la Ruta 7 se marcó como huida por la regla de «nada contado»: se leyeron los
contadores **11 ms** después de irse las tablas y el juego aún no había apuntado la huida, que sí apuntó. Ahora, si no
hay nada contado, se esperan hasta **4 s** antes de decidir. La etiqueta salió bien por casualidad; con una captura
habría salido mal.
El combate del Haunter **no se ha marcado** y no se marca a posteriori: ese evento diría que PermaLocke lo vio, y no
lo vio.

**Sin ver todavía:** una marca automática en una partida real, el caso de la zona leída al acabar, y cualquier
combate doble o SOS.

## §119 · Fuera del MAPA no se captura (2026-09-14)

El jugador entró en el **Túnel del Volcán**, que dejó fuera del MAPA a propósito, y el aviso le dijo que como no era
ruta le **devolvía** las Poké Balls. Era lo que decía el §117 —toda duda a favor del jugador, y «no es ruta» contaba
como duda—, y no es lo que quiere: **si una zona no aparece en el MAPA, no hay opción de capturar en ningún momento.**
Las zonas que no se pusieron no son zonas olvidadas, son zonas descartadas.

**La regla** (`EncounterPolicy.Decide`, con tests): fuera del mapa se retiran las Poké Balls andando y en combate, y
un combate ahí no gasta ni marca nada. Dos excepciones:

- **Un variocolor**, que se puede atrapar siempre, como se decidió en el §117. Queda dicho aquí por si no era eso.
- **Las capturas estáticas permitidas**: el Necrozma del Monte Lanakila y los cuatro Tapus en sus ruinas. Se pueden
  atrapar siempre —fuera del mapa, y también con la ruta gastada, porque **el Monte Lanakila sí es ruta del MAPA**— y
  no gastan nada.

La única duda que sigue a favor del jugador es **no saber dónde está**: retirar las Poké Balls con una lectura que
no se tiene sería retirarlas en todos los sitios donde falle el lector de zona.

**Cómo se reconoce una captura permitida.** No por la zona sola: el Monte Lanakila tiene salvajes normales, y abrir el
monte entero dejaría capturar en una ruta gastada. Es **zona + la especie del rival**, y esa especie es la de **tu
mundo**, no la del cartucho: el randomizador la cambia. Así que `rules.json` las describe por lo que el cartucho tiene
—especie, forma y nivel— y `WorldAllowedStatics` busca esa fila en la tabla de estáticos del cartucho
(`StaticEncounterTable.RowsOf`) y lee la misma fila del mod instalado, que se parchea en su sitio. Especie, forma,
nivel y zona salen de **la tabla de encuentros legales de PKHeX para Ultra Luna**, no de memoria:

| Captura | Cartucho | Fila | En el mundo del jugador |
|---|---|---|---|
| Necrozma del Monte Lanakila | Necrozma f0, Nv 65 | 161 | **Wo-Chien**, forma normal |
| Tapu Koko, Ruinas de la Guerra | Tapu Koko, Nv 60 | 129 y 135 | Bellibolt y Roserade |
| Tapu Lele, Ruinas de la Vida | Tapu Lele, Nv 60 | 130 | Flamariete |
| Tapu Bulu, Ruinas de la Cosecha | Tapu Bulu, Nv 60 | 131 | Wo-Chien |
| Tapu Fini, Ruinas del Tránsito | Tapu Fini, Nv 60 | 132 | Ursaluna |

**El jugador pidió comprobar que el Necrozma no fuera una mega al azar**, y no lo es: lleva la regla `Strong` de
`randomizer.json` —un Pokémon en forma normal con total base de 550 o más— y es un Wo-Chien. Las megas al azar son
las otras tres entradas de Necrozma, que son **combates de jefe**: la fusión con Solgaleo (Charizard mega), la fusión
con Lunala (Ampharos mega) y Ultra Necrozma (Salamence mega). Esas no están en la lista y no se pueden atrapar. La
tabla tiene además un **Necrozma de forma 0 a nivel 75** (fila 99) que PKHeX no reconoce como encuentro de Ultra Luna;
por eso la entrada lleva el nivel.

**Dos Tapu Koko idénticos.** Las filas 129 y 135 solo se diferencian en el byte 0x07 (1 y 2), y nada medido dice cuál
es el de las ruinas. `RowsOf` devuelve las dos y se permiten las dos especies. No cuesta nada: las Ruinas de la Guerra
están en `sinEncuentros`, ahí no hay salvajes. Y dentro de una zona con captura permitida, un combate cuya especie no
llega a leerse se resuelve a favor del jugador, porque puede ser ella.

Verificado por la misma cadena que la aplicación, **sacando la tabla de la ROM** (la capa `Expansion/` no la trae) y
leyendo el mod instalado: 252 filas en los dos lados y las cinco capturas resueltas como en la tabla. Si algo no se
resuelve —ROM ausente, tablas de distinto tamaño, una fila que no aparece— **no se permite nada** y queda en el log.

De rebote se ve que `bannedSpecies` solo cubre las especies 1-807, así que **Wo-Chien, un legendario de gen 9, sale
en los estáticos**. No se toca aquí; es una decisión del randomizador.

**No se sabe si un combate estático sube el récord 4** de combates salvajes, que es con lo que PermaLocke ve empezar
un combate. Para no depender de eso, `EncounterGuard` pregunta también a las **tablas del combate**: si enseñan como
rival una especie permitida en la última zona leída, devuelve las Poké Balls aunque el contador no se haya movido. No
abre nada: en un combate contra entrenador no se puede lanzar una Poké Ball.

**Sin ver todavía:** nada de esto en una partida, ni cuál de las dos vías salta con el primer Tapu o con el Necrozma.

### Para probar la cláusula shiny: todo variocolor

El jugador confirmó que un variocolor se captura siempre y pidió **forzar la probabilidad al 100 %** para probarlo. No
se escribe un truco de memoria: el sitio sale del editor de probabilidad de **pk3DS** (`ShinyRate.cs`, GPLv3), que
para séptima generación cambia un byte, de `0A` (BEQ) a `EA` (B), al final de la comprobación de shiny. En el
`code.bin` del mod instalado el patrón aparece **una sola vez**, el byte está en `0x2205CF` con `0A`, y la rutina de
tiradas de PID que pk3DS también localiza está justo detrás, en `0x220668`, sin tocar.

Va como **`exefs/code.ips` del mod**, que Azahar aplica al arrancar el juego **después** de cargar el `code.bin` del
mod (leído en su código: `ApplyCodePatch` desde `AppLoader_NCCH::LoadExec`). Ni el `code.bin` ni la ROM se tocan, y
quitarlo es borrar un fichero. `Probe --shiny-siempre` lo pone y `--shiny-siempre quitar` lo quita; los dos se niegan
con Azahar abierto, y quitar solo borra un parche cuyos bytes son exactamente los suyos. Reinstalar el mod también lo
retira, porque la carpeta vieja se mueve entera. **Es una prueba, no una opción de la competición**: todo lo que el
juego genere mientras esté puesto se queda variocolor para siempre.

La prueba sirve además para lo que sigue sin medir desde el §117: **la posición del récord 127**, variocolor
encontrados, que está calculada. Si no es esa, PermaLocke no ve el shiny y lo trata como un salvaje normal. (Salió
buena, pero no sirve para esto: el juego lo cuenta tarde. El brillo se lee del propio salvaje; ver §118.)

## §120 · Los avisos, en pixel art (2026-09-14)

El jugador pidió un rediseño de las notificaciones «realmente guapo, con sprites, y que no parezca hecho por IA». Lo
que había era justo lo que él llama IA: una tarjeta redondeada con franja de color, **sombra difuminada** y un
deslizamiento suave. Lo que sí aprobó antes fue lo dibujado celda a celda —el cementerio (§115) y la franja de Alola
(§116)—, así que los avisos pasan a ese idioma.

**Las piezas** (`Views/ToastPixels.cs`), todas pintadas en celdas enteras de pantalla y escaladas sin interpolar:
- `ToastFrame`: la caja, con contorno de una celda, relieve de una celda —luz arriba y a la izquierda, surco abajo y a
  la derecha—, esquinas recortadas y **sombra dura**: un bloque negro semitransparente desplazado dos celdas. Con
  `IsTab`, la pestaña de color del tipo de aviso, abierta por abajo y metida en la caja para que salga de ella.
- `ToastPlate`: la placa del sprite. Un pozo oscuro con suelo de **trama ordenada** en el color del aviso, el sprite
  del cartucho a un píxel por celda, y la marca de su tipo **escrita a mano como texto**: estrella de variocolor en dos
  fotogramas, señal de prohibido, flecha, cruz, exclamación. Sin sprite, una lápida o una señal de peligro dibujadas
  igual. El sprite **salta una celda cada medio segundo en dos fotogramas**, que es como mueve los iconos el menú de
  equipo de los juegos: la única animación que no se ha inventado. Un caído no salta y sale en gris.
- `ToastCountdown`: doce segmentos que se apagan de uno en uno durante los seis segundos del aviso.
- La entrada es a saltos, tres pasos en 150 ms, no un deslizamiento.

**El tamaño de la celda se decidió mirando**, no de antemano: se renderizó la ventana de verdad con los sprites del
cartucho sobre fondo oscuro y sobre verde de hierba. A 2 píxeles por celda el contorno se leía como un borde fino y los
sprites salían pequeños encima del juego; a **3** se lee como pixel art. A otras escalas de pantalla la celda sigue
siendo un número entero de píxeles.

**Qué dice cada aviso** (`ToastKind`), con pestaña y color de lo que ya significaba algo en la aplicación: VARIOCOLOR
en el oro de la ruleta, POKÉ BALLS en rojo si se retiran y verde si vuelven, PRIMER ENCUENTRO en el violeta de
PermaLocke, CAPTURA PERMITIDA en azul, BAJA y EQUIPO CAÍDO en rojo, PREMIO en verde, ATENCIÓN en naranja.
`EncounterGuard` ya no avisa con dos textos sueltos: manda el tipo y la especie, y `PlayNotifications` pone el sprite
—el Pokémon del combate, el caído, la Poké Ball, el objeto del premio—. De paso, **un combate nuevo daba dos avisos**:
«Poké Balls devueltas» tras la retirada silenciosa mientras se lee el Pokémon, y «Primer encuentro». Ahora devolver lo
que se quitó en silencio es silencioso también, salvo que lo devuelto sea la noticia: un variocolor o una captura
permitida, que tienen su propio aviso.

**Visto** en renders de la ventana real con `ToastWindow`, `Toast` y los sprites extraídos, sin abrir la aplicación ni
tocar el juego. **Sin ver** encima de Azahar durante una partida; el botón PROBAR AVISO de MISCELÁNEA saca ahora cuatro
de tipos distintos, marcados como prueba.

## §121 · El wonder trade, por cable (2026-09-14)

El jugador pidió rehacer la animación del intercambio para que fuera bonita y no pareciera de IA, **manteniendo la
revelación de generación, tipos y total base**. La anterior era exactamente lo que él llama IA: fondo de degradado
radial, rayos de luz girando, destellos blancos difuminados, una onda expansiva, el color del tipo entrando como
degradado y todo con curvas de aceleración elásticas.

**La idea es un homenaje al intercambio por cable de las primeras generaciones**: dos pedestales unidos por un cable, y
las bolas viajando por él. `TradeStage` la dibuja celda a celda, como el cementerio, la franja de Alola y los avisos:

1. Tu Pokémon en tu pedestal, dando saltos como en el menú de equipo; en el otro, un «?» que flota.
2. Parpadea tres veces en silueta blanca, suelta un anillo de polvo y queda la Poké Ball del cartucho.
3. Las dos bolas recorren el cable en arco y **se cruzan en lo alto** con un chispazo. El cable se enciende detrás de
   cada una en tres escalones. El suelo es una rejilla en perspectiva cuyas líneas avanzan mientras viajan.
4. La que llega aterriza en tu pedestal con dos botes y **se sacude tres veces**, una celda a cada lado. Solo entonces
   se avisa al ViewModel.
5. Generación, tipos y total base, en el orden de siempre y decididos por el ViewModel, salen en cajas de pixel art
   (`PixelPanel`) con sombra dura en el texto; los tipos en su color. Aparecen como un sello: dos saltos de tamaño.
6. Al abrirse: un «pop» blanco, ocho rayos sólidos y **el color del tipo inunda la escena con trama ordenada** —las
   celdas se encienden umbral a umbral de una matriz de Bayer—, que es la forma de fundir sin fundido. Sale el Pokémon
   parpadeando en silueta, con estrella si es variocolor, y su ficha en una caja con un botón de píxeles.

**Todo se mueve a saltos por construcción**: las bolas siguen una parábola redondeada a celdas, así que saltan de celda
en celda solas, y el repintado va a treinta pasos por segundo.

**Lo que salió de mirar los renders**, no de antemano: el cielo tramado de arriba abajo se leía como un semitono, así
que pasó a **bandas lisas con la trama solo en la costura**; la rejilla junto al horizonte era un borrón de puntos y esa
franja va lisa; los rayos a trazos parecían puntos sueltos y ahora son sólidos; y a un píxel por celda un icono pequeño
como el de Wooloo parecía una cuenta sobre el pedestal, así que **los Pokémon van a doble tamaño** —cada píxel del icono
son dos por dos celdas, sin suavizar— y las bolas a uno. La nube al entrar en la bola quedaba tapada por la bola.

`TradeStage.FrozenAt` fija el reloj a un instante: es lo que permite renderizar cualquier momento sin hacer un
intercambio. **La lógica no se ha tocado**: el intercambio se decide, se escribe en la partida y se registra antes del
primer fotograma, y el ViewModel sigue diciendo cuándo sale cada cosa.

### Lo que dijo el jugador tras varios intercambios de verdad

**Los de generación 8 y 9 salían como generación 7 y con tipos «?».** Dos fallos con la misma forma, la del §-mod de
expansión entero: una tabla que acaba en 807. `Generations` terminaba en la 807, así que todo lo de después era de la
séptima; ahora acaba en **809, 905 y 1025** (Meltan y Melmetal son de la séptima en la dex nacional, que es el orden del
mod). Y `PkhexTypeLookup` leía la tabla de PKHeX de Ultra Sol/Ultra Luna, que también acaba en la 807. Los tipos salen
ahora de **la tabla de especies del mundo instalado** (`PersonalEntry7.Types` → `WorldLimits.Types`), igual que ya salían
de ahí las curvas de experiencia y las estadísticas base, con PKHeX de respaldo sin mod. Comprobado contra el mod
instalado, que declara 1025 especies: Charizard Fuego/Volador, Grookey Planta, Corviknight Volador/Acero, Dragapult
Dragón/Fantasma, Sprigatito Planta, Pecharunt Veneno/Fantasma. Los intercambios ya hechos tienen en su evento la
generación con la que se hicieron; eso no se reescribe.

De paso, `InstalledWorld` abría esa tabla del mod **con permiso de escritura** para leerla, porque usaba el constructor
de `GarcPatcher`, que existe para parchear. Existía `GarcPatcher.ReadOnly` justo para mirar un mod instalado —la carpeta
es del emulador— y ahora se usa.

**Las bolas no giraban, se reflejaban de lado a lado.** Ahora giran **a cuartos de vuelta**, que es la única forma de
girar un sprite de píxeles sin redibujarlo: cada píxel cae en una celda. Un cuarto cada cuatro celdas de recorrido, en
sentidos opuestos para la que va y la que vuelve.

**Y más cosas en el fondo**, todas en el mismo idioma: una **luna** con el lado en sombra tramado y cráteres puestos a
mano; **veinte estrellas en sitios fijos**, no repartidas al azar, cada una con su fase y parpadeando a saltos; **nubes**
escritas como texto que avanzan una celda cada medio segundo; e **islas** en el horizonte con el perfil en alturas por
columna, como las de la franja de Alola. Islas y nubes van a dos celdas por dato: a una, en el render eran granos. La
inundación del color del tipo también las tiñe.

**Sin ver todavía** con un intercambio de verdad después de estos cambios.

### Solo con Pokémon vivos

Regla que el jugador añadió al final: **un Pokémon muerto no se puede entregar en un wonder trade.** Sin ella una muerte
dejaba de ser una pérdida: se entrega el cadáver y vuelve algo vivo de la misma fuerza.

Quién está muerto lo dice **la run, por PID**, que es como se registra cada muerte (§56); la partida no puede decirlo,
porque un Pokémon en caja no guarda PS. `WonderTradeGift` lleva ahora el PID y `WonderTradeService.TradeAsync` se niega
**antes de sortear, guardar o registrar nada**, así que ninguna otra pantalla ni herramienta puede saltárselo. La
pantalla lo avisa además en cuanto se elige al caído y apaga CONFIRMAR, pero eso es cortesía: la regla está en el
servicio. Un PID a cero no identifica a nadie y no bloquea; desde el §56 todos los de la partida tienen uno real. Tres
tests: el caído se rechaza sin escribir nada, el vivo se intercambia como siempre, y el PID a cero no bloquea.

---

## §122 · Entrenadores más difíciles, sin cambiar quiénes son (2026-09-14)

El jugador pidió «más dificultad a la IA del juego» y antes de tocar nada se midió qué trae el cartucho, porque
«subirla» no significa nada sin saber desde dónde. Resumen para cualquiera en `subida_de_dificultad_explicada.txt`.

### Lo que se midió

**La IA es un byte**, `trdata[0x0C]`, con los bits que expone pk3DS (PR #412): Básica 0x01, Fuerte 0x02, Experta
0x04, Dobles 0x08, Sin derrota 0x10, Battle Royal 0x20, Cambiar de Pokémon 0x40, Usar objetos 0x80. Qué hacen por dentro
Fuerte y Experta **no está documentado para gen 7**. Lo medido es el reparto: de 653 entrenadores, **166 (25 %)** llevan
los tres niveles, 245 solo la Básica, **0x40 no lo lleva nadie** —el código tiene `AiPokeChangeJudge` y
`p_PokeChangeEnable`, y en foros cuentan que activarlo en entrenadores normales no hace nada; no se ha probado— y 0x80
lo llevan 60, que son justo los que tienen objetos de entrenador (Restaurar Todo, Defensa X...). Las parejas de
combate doble llevan 0x08, los nueve del primer combate con Hau 0x10 y los de la Battle Royal 0x20, así que el
significado de los bits cuadra con dónde aparecen.

**PermaLocke no tocaba nada de esto**: el mod instalado y el cartucho coinciden en IA, IV y EV salvo por las copias del
Pokémon extra del rol.

Por tramos de historia —nivel más alto del equipo en el cartucho contra el nivel del jefe de cada prueba, que es su cap
entre 1,2 (§48)—: hasta la 1ª prueba el 87 % solo tiene la IA Básica y el **85 % lleva los IV a 0**; de la 2ª a la 10ª
los IV rondan 15-22, con 15 como valor más repetido; la IA completa no pasa del 50 % hasta la 11ª, y **nadie lleva 31 en
todo**, el máximo que usa el juego es 30. Naturalezas: 79 % Seria. Habilidad oculta: 27 de 1139. Objeto equipado: 10 %,
sobre todo cristales Z.

Y un fallo de fondo que nadie había visto: **los EV, la naturaleza y el objeto sobreviven al cambio de especie**. Un
reparto de Ataque y Velocidad pensado para el Pokémon del cartucho le caía a un atacante especial y no le servía de
nada.

### Radical Red, para comparar

Radical Red y Run & Bun están construidos sobre las **descompilaciones** de Rojo Fuego y Esmeralda, así que su IA es
código editable: puntúa cada movimiento con un cálculo de daño, conoce el equipo del jugador y cambia de Pokémon con
reglas. Para los juegos de 3DS no existe nada parecido —la IA de Ultra Luna es la clase `btl::BattleAi` del `code.bin`
más scripts sin herramientas—, así que igualar eso no es un ajuste sino ingeniería inversa de meses. Lo que sí se puede
hacer es lo que el cartucho ya expone como datos.

### Lo que se decidió y cómo está hecho

Módulo propio, `TrainerDifficultyRandomizer`, configurado en el bloque `trainerDifficulty` de `Data/randomizer.json`:

- **IA**: `ai | 0x07` en los 653. Es un OR y no una asignación, así que Dobles, Sin derrota, Battle Royal y Usar objetos
  se conservan. 487 entrenadores ganan algún bit.
- **IV**: cada Pokémon sortea un porcentaje entre 10 y 20 y cada IV sube eso de sí mismo, redondeando hacia arriba como
  los niveles del rol y con tope 31. **Un IV a 0 sigue a 0**; es lo que significa «un 10-20 % de lo que tienen» y está
  escrito en el código para que nadie lo tome por un fallo.
- **EV**: la misma cantidad que llevaba, repartida para la especie y forma que acabaron en el hueco: 252 a su mejor
  ataque, 252 a Velocidad si su base llega a 80 o a PS si no, y lo que sobre a la siguiente. Una mega lee su propia fila
  de la tabla, como `PersonalInfo.FormeIndex` de pk3DS. Quien no llevaba EV sigue sin llevar.
- **Objetos**: se reparten hasta que el 25 % de todos los Pokémon de entrenador lleve uno; lo que ya llevaban se
  respeta. Salen de una lista común más Cinta Fuerte o Gafas Especiales según ataque, **sin objetos Elegidos ni Chaleco
  Asalto**, que bloquean movimientos con una IA que nadie ha medido así. El nombre de cada objeto se comprueba contra la
  tabla del cartucho antes de escribir (§52).
- **Movimientos, habilidades, naturalezas y rol**: sin tocar, a petición del jugador.

**El orden de los EV se midió antes de escribir el reparto**, en vez de fiarse del comentario de la estructura: entre los
252 del cartucho, el byte 1 cae en especies de 103 de Ataque base medio, el 3 en 101 de Ataque Especial y el 5 en 90 de
Velocidad. Es PS, Ataque, Defensa, At. Esp., Def. Esp., Velocidad. Equivocarse ahí no falla, reparte la Velocidad a quien
debía llevar Ataque Especial.

**Va después** de entrenadores, extra del rol, datos de Pokémon y megas, porque un reparto solo significa algo para la
especie final con las estadísticas que el juego va a usar; y con **sal propia** (`trainer-difficulty`) y una corriente
derivada por palanca, para que encenderlo no mueva las especies de nadie ni cambiar el porcentaje de objetos mueva los IV
(§27). Todo son campos de tamaño fijo parcheados en su sitio, sin reempaquetar. La comprobación relee los dos ficheros y
exige que especie, forma, nivel, movimientos, sexo, habilidad y naturaleza salgan **byte a byte** iguales, los EV con el
mismo total y nada por encima de 252, ningún IV más bajo, las banderas de encima de los IV intactas, ningún objeto previo
perdido y exactamente los Pokémon con objeto que tocaba.

### Verificado contra la ROM

Generado con la seed y el rol de la run real (LUDÓPATA) sobre la capa de expansión, en una carpeta temporal:

- **Sin la dificultad, `a/1/0/6` y `a/1/0/7` salen idénticos byte a byte al mod instalado**, o sea que la generación
  reproduce el mundo que se está jugando.
- **Con ella**, en `a/1/0/6` solo cambia el offset 0x0C (487 entrenadores) y en `a/1/0/7` solo los offsets 0x02-0x0B
  (EV e IV) y 0x14-0x15 (objeto).
- IA completa 25 % → **100 %**; IV medio 19,2 → **21,0**; Pokémon con EV 649 → 649; con objeto 151 (12,0 %) → **316
  (25,1 %)**. Ejemplos del reparto: Luxray, base Ataque 120 y Velocidad 70, pasa de At. Esp.+Velocidad a PS+Ataque; un
  atacante especial con Velocidad 85 pasa de Ataque+At. Esp. a At. Esp.+Velocidad.

La primera generación de verdad **falló**, y por el módulo: la comprobación releía `a/1/0/7` con el escritor todavía
abierto y Windows no la dejaba abrirlo. Ahora el fichero se cierra antes de verificar.

**Instalado el mismo día** con `RomTool randomize <seed> --rol ludopata --install` y Azahar cerrado. Antes de instalar se
generó aparte y se comparó fichero a fichero con lo instalado: **8 de 10 idénticos**, y los dos distintos eran `a/1/0/6` y
`a/1/0/7`, o sea que el resto del mundo no se movió. El instalador guardó esos dos en
`load/permalocke-copias/20260914-153303` antes de pisarlos, y después los diez instalados salen idénticos a los
generados. El `code.ips` de la prueba del variocolor (§119) sigue en `exefs`: instalar copia encima y no borra lo que no
trae. El primer intento se cortó **antes de instalar nada**, en `LayeredFsMod.Clear`, que no pudo borrar la carpeta
generada un momento antes —algo la tenía abierta—; el segundo pasó entero.

**Sin jugar todavía.** Lo que no se sabe es cuánto más difícil se nota la IA Fuerte+Experta en un entrenador de ruta; eso
solo lo dice jugarlo.

---

## §123 · Un jugador, una carpeta, y un resumen que se puede comprobar (2026-09-14)

Paso 3 de la lista del jugador: «un sistema de usuarios o que cada uno tenga su carpeta». Se preguntó lo único que
decidía el diseño —si varios juegan en el mismo PC— y la respuesta fue que **cada uno juega en su PC**. Entonces el
usuario ya es la instalación: lo que faltaba era una **identidad que sobreviva** y **orden en la carpeta compartida**.

### Lo que había

La aplicación no sabía quién era nadie. El «jugador» era el texto escrito en cada run, así que dos Ash eran la misma
fila, y quien empezaba de cero dejaba su run vieja en la clasificación para siempre junto a la nueva. La carpeta
compartida era un fichero suelto por **run**, en la raíz, y el §79 publicaba solo el resumen, «sin verificar y sin
forma de verificarlo».

### Lo que hay ahora

- **Perfil**: `Config/jugador.json` con un id fijo y un nombre. En `Config/` y no en `Saves/`, porque EMPEZAR DE CERO
  borra la run y la partida y el que vuelve a empezar sigue siendo la misma persona. Se crea con el nombre que el
  jugador ya puso en su run, así que nadie tiene que presentarse otra vez. El nombre se cambia en COMPETICIÓN sin
  romper nada: todo va por el id. Cambiar el nombre no escribe evento, porque no mueve ni un punto ni un Pokémon.
- **La run tiene dueño**: `Run.PlayerId`. Las nuevas nacen con él; las de antes se **vinculan una vez al arrancar**,
  con su evento `PlayerLinked` (regla 4). Una run que ya tiene otro dueño **no se toma nunca** —es lo que pasaría
  copiando la carpeta `Saves` de un amigo—, y COMPETICIÓN la enseña pero se niega a publicarla con tu nombre.
- **La carpeta compartida**, con cada aplicación escribiendo **solo dentro de la suya**, que es lo que evita que Drive
  fabrique copias en conflicto:
  ```
  Competición/
    reglas/                      las oficiales; solo las toca el admin
    jugadores/
      Grenin-6f91040e/           nombre legible + los 8 primeros del id
        perfil.json
        run-activa.json          el resumen de la run que se está jugando
        historial.json           la cadena de eventos entera de esa run
  ```
  Un jugador es **una fila**: empezar de cero sustituye `run-activa.json`. Renombrarse renombra la carpeta, encontrada
  por el id. Los ficheros sueltos del formato anterior **se siguen leyendo** —marcados SIN HISTORIAL— y al publicar se
  borra el suelto de la propia run, y solo ese.

### El resumen se comprueba contra el historial

El §79 decía que publicar la cadena entera era «mandar el diario para que lean la última página». Con una carpeta por
jugador el diario sirve para algo: **comprobar la última página**. `SnapshotAudit` exige que la cadena publicada
verifique con `EventHasher` —el mismo cálculo que la base de datos, sacado a `EventChain` para no tener dos—, que el
número de eventos y el hash del último sean los del resumen, y que **los puntos sean la suma de los deltas**, que es
exactamente como los calcula `PointsService`. Los recuentos de Pokémon no están en la cadena y no se reclaman.

Y un caso que una cadena sola **no puede** cazar: una copia vieja es perfectamente coherente. Lo que la delata es
haber visto antes la run más avanzada, así que cada aplicación guarda en `Config/competicion-vista.json` lo más lejos
que ha visto cada run y avisa si una publicación **retrocede** o si lo ya visto **ha cambiado**. En la máquina de cada
uno y nunca en la carpeta compartida: una marca que el vigilado pudiera editar sería una marca que podría mover. La marca
solo avanza con resúmenes que cuadran, para que una copia restaurada siga marcada en vez de convertirse en lo normal.

Cuatro marcas: **CUADRA**, **NO CUADRA**, **HA RETROCEDIDO** y **SIN HISTORIAL**. Lo que no cuadra **va detrás** de lo
que se puede fiar, con sus propios números: la primera prueba en la aplicación puso primeros 900 puntos inventados,
porque se ordenaba por lo que cada uno dice de sí mismo.

**Qué no es**, dicho también en la pantalla: un antitrampas. Una cadena de hashes no es una firma (§9); quien rehaga un
historial entero con herramientas publica uno que cuadra. Lo que sí caza es editar un número, volver a una copia y
borrar una muerte.

### Las reglas oficiales

El admin deja ficheros en `reglas/` y cada aplicación los compara con su `Data/`. **Solo una lista fija**
—logros, gacha, créditos, caps, penalizaciones, premios, ruleta, reglas, tienda, wonder trade, y `randomizer.json` y
`roles.json` marcados como «hay que regenerar el mod»—; lo que sale del cartucho de cada uno (`species.json`,
`mapas.json`) no se reparte nunca. Cada fichero oficial se **carga con el mismo cargador que usa la aplicación al
arrancar** antes de ofrecerlo, porque un fichero con una errata copiado a cinco máquinas impediría arrancar a las
cinco. Si uno no carga no se adopta **ninguno**: medio juego de reglas es peor que cualquiera de los dos enteros. Los
ficheros propios se copian a `Saves/backup/reglas-<fecha>/` antes de pisarlos, y se deja `RulesAdopted` en la run con
el hash de cada fichero antes y después. Se aplican **al reiniciar**: los catálogos son singletons construidos al
arranque, y cambiarlos debajo del vigilante de combates no merece lo que arriesga.

### Verificado

- 37 tests nuevos: perfil y vinculación —incluida la run ajena que no se toca—, la comprobación en cada forma de
  tocar los ficheros, la estructura de carpetas, el formato viejo y la adopción de reglas. El más importante escribe
  eventos **reales en SQLite** con desfase horario, semilla, Pokémon y diccionario, los publica como JSON, los relee y
  exige que cuadren: si el fichero perdiera un tic de una fecha, todo jugador honrado saldría como tramposo.
- En la aplicación, **en una copia aislada fuera del repositorio** y con una carpeta compartida falsa: la run sin dueño
  se vincula al arrancar, PUBLICAR escribe en `jugadores/Grenin-…` y sale CUADRA, un amigo con los puntos editados sale
  NO CUADRA y detrás, uno que volvió a una copia sale HA RETROCEDIDO, el formato viejo sale SIN HISTORIAL, y adoptar
  reglas copió `shop.json` guardando el anterior y dejó `PlayerLinked` y `RulesAdopted` en una cadena que sigue
  verificando.
- **Sin probar con dos PCs sincronizando de verdad** por Drive o Dropbox. Tu run real no se ha tocado: se vinculará la
  próxima vez que abras PermaLocke.

---

## §124 · COMPETICIÓN decía «no puede combatir» a quien sí podía (2026-09-14)

Al publicar la run real por primera vez, el aviso de combates dijo «Grenin no puede: su mundo baraja estadísticas base o
randomiza habilidades». Era la regla del §80, y **COMBATES la dejó obsoleta**: esa sección aparta el mundo randomizado y
pone el juego base mientras dura el combate, así que las opciones del randomizador de cada uno ya no importan. El aviso
no se enteró de que existía.

Lo que sí tiene que coincidir es el **juego base**: el mod de las generaciones 8 y 9 en un lado y el cartucho en el otro
son juegos distintos y se desincronizan igual que dos randomizaciones. La instantánea gana `WorldSpecies`, sacado del
`maxSpecies` del último `RomRandomized` —1025 con el mod, 807 sin él—, opcional como `BattleReady` para que las
versiones viejas sigan leyendo. `LinkBattleAdvice` (en Core, con 5 tests) sustituye al resumen que vivía en
`SyncService`: explica cómo combatir desde COMBATES, avisa por nombre si hay juegos base distintos, dice aparte a quién no
se le conoce el dato, y con un solo jugador no enseña «COMPATIBLES», porque no hay con quién. Visto en la aplicación real
y republicado: la ficha de Drive lleva `worldSpecies: 1025`.

---

## §125 · JUGAR: la aplicación como lanzador (2026-09-14)

El jugador pidió que PermaLocke fuese un lanzador «rollo juego de Steam»: abrir y cerrar el emulador desde la app, que
todo girase alrededor de ella, y que no pareciera hecho por una IA. JUGAR es ahora **la primera sección y la de
arranque**, y cada pantalla lleva en la cabecera un botón pequeño que dice JUGAR o `EN JUEGO · 00:12:03` y lleva a ella.

### Qué emulador, y por qué es la parte delicada

Azahar se vuelve **portátil** cuando hay una carpeta `user` junto a su ejecutable, y entonces guarda allí la partida.
PermaLocke decide dónde leer con `AzaharInstallation.Locate`: el emulador que viaja con la app es portátil; cualquier
otro se lee de `AppData\Roaming\Azahar`. Arrancar un Azahar portátil encontrado por ahí **abriría otra partida** mientras
todas las pantallas leen la de siempre. `AzaharExecutable.Choose` lo impide: el del reparto si lo hay; si no, solo
ejecutables **no** portátiles, primero el que el jugador eligiera a mano y luego el más nuevo. En este repositorio hay tres
—el Azahar oficial de agosto y dos copias idénticas del fork del 6 de septiembre— y gana el fork, que es el único que
escribe en el juego. Medido: arranca `Nuevo_azahar\azahar.exe "ROM\Pokemon Ultra Moon (Europe)….3ds"`.

### Abrir, vigilar y cerrar

- **Abrir** es pasarle la ROM a Azahar como argumento, que es como su propio código arranca un juego desde la línea de
  órdenes. El mod se carga solo por LayeredFS. Antes, con el emulador cerrado, se asegura el servidor RPC y se apaga
  la pregunta «Would you like to exit now?» (`confirmClose`), porque la hace el lanzador.
- **Vigilar** es mirar una vez por segundo si existe un proceso `azahar`, **lo haya abierto quien lo haya abierto**: un
  lanzador que solo conoce a su hijo diría «listo para jugar» encima de un juego abierto a mano.
- **Cerrar** es pedirle a su ventana que se cierre, que es el apagado limpio: Azahar detiene la emulación y guarda sus
  ajustes. Verificado en su log: `Received end packet`, audio detenido, proceso limpiado, **en 1,3 s**. Si a los 12 s sigue
  abierto aparece **FORZAR CIERRE**, que mata el proceso y es otro botón con su propia pregunta, nunca algo que se haga
  solo. Ninguno de los dos guarda la partida, y la pregunta lo dice.

La pregunta va **dentro de la barra de jugar** y no en una ventana de Windows: el primer intento usaba el cuadro gris del
sistema, y en medio del lanzador en píxeles se leía como otro programa. Probado entero: CERRAR JUEGO pregunta y el juego
sigue abierto, SEGUIR JUGANDO no lo cierra, SÍ, CERRAR lo cierra.

### El tiempo jugado

`Saves/<run>/sesiones.json`, una sesión por arranque del emulador **identificada por la hora de inicio del propio proceso**:
cerrar y abrir PermaLocke con el juego en marcha sigue la misma sesión en vez de contar dos. Se apunta cada 30 s y al
cerrar. **No es un evento**: no mueve puntos, Pokémon ni reglas (regla 4), y un latido cada medio minuto enterraría la
cadena. Va en la carpeta de la run, así que EMPEZAR DE CERO se lo lleva con ella. Las cuatro sesiones de las pruebas de
hoy se borraron: eran mías, no del jugador.

### La portada

No hay arte del juego que sea nuestro para enseñar, así que la portada se hace con lo que sí es del jugador: **su
equipo, leído de su partida y dibujado con los iconos de su cartucho** (§28), en una playa de Alola a la hora del juego
(`LauncherStage`, con la paleta del §116). Cada Pokémon salta a su ritmo, dos celdas, a cuatro pasos por segundo; con el
juego abierto se quedan quietos, que es el único cambio que hace la portada. El primer cielo tramaba cada fila y a tres
píxeles por celda se leía como un trame de periódico —lo mismo que el jugador señaló en el wonder trade (§121)—: ahora son
bandas lisas con la costura tramada, el mar son franjas con rayas de espuma sueltas, la arena lisa con granos contados, el
sol va por la mitad derecha para no salir detrás del título y la palmera a dos celdas por punto.

Debajo, la barra de una biblioteca de juegos: el botón en caja de píxeles (violeta JUGAR, rojo CERRAR JUEGO), el estado con
el reloj de la sesión, TIEMPO JUGADO, ÚLTIMA VEZ y LOGROS. Y dos paneles: ANTES DE JUGAR —emulador, ROM, mundo instalado
o modo combate, run cargada; lo que falta bloquea el botón y lo que conviene mirar avisa— y LO ÚLTIMO QUE HA PASADO.

### Un fallo de la tanda anterior que salió aquí

LO ÚLTIMO enseñó **dos** «Run vinculada al jugador» a las 18:04. El arranque y la pantalla de COMPETICIÓN vincularon la
misma run en el mismo segundo (§123). `PlayerProfileService` serializa ahora vincular y crear el perfil con un cerrojo y
**relee la run dentro de él**; hay un test que lanza cinco vinculaciones a la vez y exige un solo evento. El evento de
sobra se queda en la cadena, porque no existe forma de quitar uno.

Tests: elección de emulador (5), el ajuste de cierre sin tocar nada más del fichero, tiempo jugado (6) y la carrera. 1077.

## §126 · JUGAR como una biblioteca de Steam: amigos y actividad (2026-09-14)

El jugador pasó una captura de la página de un juego en Steam y pidió que JUGAR fuese igual: fuera ANTES DE JUGAR y LO
ÚLTIMO QUE HA PASADO, y en su sitio **a la derecha la lista de amigos** con todos los jugadores de la competición —verde
jugando, azul en la app, gris desconectado— y **a la izquierda los logros que va reclamando cada uno**.

### No hay servidor, así que la presencia es un fichero con fecha

Lo mismo que la clasificación (§79, §123): cada aplicación escribe `jugadores/<nombre>-<id>/presencia.json` en la carpeta
compartida y lee los de los demás. `CommunityService` lo escribe **cada 45 s**, **en el momento** en que el juego se abre o
se cierra, y «desconectado» al cerrar la aplicación. Una aplicación que se cuelga o se queda sin luz no escribe nada, así
que lo que decide es la **edad** del fichero: pasados **3 minutos** cuenta como desconectado aunque diga «jugando»
(`Presence.StateOf`). Son cuatro latidos de margen porque Drive tarda en llevar un fichero, y un minuto arriba o abajo es
lo normal. **No es tiempo real y no lo finge**: un amigo que abre el juego tarda lo que tarde Drive en salir en verde.

Verde, azul y gris son exactamente los de Steam (`#90BA3C`, `#57CBDE`, `#898989`), en el nombre, la línea de debajo y el
marco del avatar; desconectado además apaga el dibujo. El avatar es **el primero del equipo** que el jugador tenía al
publicar (`RunSnapshot.AvatarSpecies`, campo nuevo que un lector viejo ignora, así que el formato sigue en 1), dibujado
con el icono de caja del cartucho al doble y recortado por el marco: a tamaño natural se quedaba en una mancha.

### La actividad sale de los historiales, así que se publica sola

Un logro reclamado es un evento `AchievementUnlocked` con su `logro`, y eso ya viaja en `historial.json` (§123). Pero un
historial que solo se publica cuando alguien se acuerda de pulsar un botón no es una actividad: la aplicación **vuelve a
publicar sola** cuando la cadena ha crecido, como mucho cada 90 s, con la misma publicación, relectura y comprobación que el
botón. Los historiales se releen solo si su fichero cambió, porque son lo único grande de la carpeta y la lista se
refresca cada 15 s. Lo propio sale de la base de datos local, que va por delante de lo publicado y está **aunque no se haya
publicado nunca** —el primer intento lo sacaba de la carpeta y en una carpeta sin tu perfil tus logros no salían—.

La actividad va agrupada por día como la de Steam («14 DE SEPTIEMBRE»), con «<nombre> ha conseguido un logro» y la tarjeta
del logro con su dibujo (§61), su descripción y sus puntos, y los 30 más recientes de todos. A la derecha, debajo de los
amigos, el panel LOGROS: «Has desbloqueado 10/21 (48 %)», la barra y los últimos reclamados. La barra de jugar pasa a los
rótulos de Steam —ÚLTIMA SESIÓN, TIEMPO DE JUEGO, LOGROS con su barra— y debajo van los enlaces a LOGROS, ESTADÍSTICAS,
COMPETICIÓN, CEMENTERIO y VISOR POKÉMON, más ELEGIR EMULADOR. Lo que hacía ANTES DE JUGAR queda en **una línea**: lo que
bloquea en rojo y lo que conviene mirar en ámbar.

### Qué escribe quién

Una copia abierta con `--sin-juego` **lee** amigos y actividad pero no escribe presencia ni publica: la aplicación de
verdad ya lo está haciendo con la misma run. La presencia se escribe en la carpeta del jugador; el perfil a su lado solo
si cambió, porque cada fichero escrito es un fichero que Drive vuelve a subir.

### Verificado, y lo que no

Visto en una copia aislada con una carpeta compartida de prueba de cuatro amigos, cada uno en un estado: Misty en verde
«Jugando a Ultra Luna · 25 min», Brock en azul «En la app», Lillie gris «Desconectado · hace 2 h» y Kiawe sin presencia.
Y la caducidad se vio sola: la segunda captura se hizo más de tres minutos después de escribir los ficheros y los cuatro
salieron grises. Por el camino apareció una carrera: la primera lectura de la carpeta llega antes de que la pantalla haya
cargado los sprites, y los avatares salían con la inicial; ahora espera a los sprites.

**Sin probar con dos ordenadores de verdad**, que es donde se verá cuánto tarda Drive en llevar un cambio.

Tests: presencia —desconocido, fresco, caducado, el margen de latidos, lo que dice cada estado— y la carpeta: escribir y
releer, reescribir en la misma carpeta, carpeta sin perfil y presencia ilegible (10). 1087.

## §127 · Fuera las explicaciones: textos para el jugador (2026-09-14)

El jugador pidió quitar de la aplicación todas las «descripciones de IA»: la app está casi terminada y lo que se ve
tiene que hablarle a quien juega, no a quien la programó. Repaso **solo de textos**; ni una regla ni un servicio cambian
de comportamiento.

Criterio aplicado en las 24 pantallas, sus ViewModels y los servicios cuyos mensajes llegan a la pantalla (GameLink,
Core, Rules):

- **Fuera** los párrafos que explicaban cómo funciona algo por dentro o por qué se decidió así: PID, hash, eventos,
  cadena, memoria, fork, RPC, rutas de carpetas (`Data/…`, `Saves/backup`, `load/…`), «escrito y releído», «la copia
  está en…», «está medido, no supuesto», y todos los «porque…».
- **Se queda** como mucho una frase corta cuando evita un error: «Cierra Azahar antes», «Necesita Azahar abierto»,
  «Se recogen una sola vez».
- «El detalle está en la carpeta Logs» desaparece de los ~35 mensajes de error; el detalle sigue en el log.
- Las confirmaciones pasan de cinco párrafos a una línea y la pregunta.
- COMBATES pierde el bloque «POR QUÉ HACE FALTA» y COMPETICIÓN el de «QUÉ FIABILIDAD TIENE ESTO»; los pasos para pelear
  se quedan, más cortos.
- MANTENIMIENTO renombra lo técnico: REPARAR LOS PID → POKÉMON SIN RECONOCER, CERRAR LOS ENTREGADOS → POKÉMON
  INTERCAMBIADOS, ETAPAS Y TOPE DE NIVEL → PRUEBAS Y NIVEL MÁXIMO; la auditoría deja de enseñar «Detectables por el
  vigilante» y «Eventos en el historial».
- MISCELÁNEA ya no enseña la dirección de memoria de la mochila ni el hueco de cada objeto.
- «cap» pasa a «nivel máximo» en lo que se ve.

Lo que **no** se ha tocado: las descripciones de los eventos ya guardados (son historial encadenado y no se reescribe),
los comentarios del código y los mensajes de log. Siete tests comprobaban frases antiguas y se han ajustado a las nuevas
sin perder lo que comprobaban. 1087 tests.

**Sin ver en pantalla**: las vistas compilan y los tests pasan, pero no se capturaron porque el jugador estaba usando el
ordenador.

## §128 · El visor Pokémon, con aspecto de PC del juego (2026-09-15)

El jugador pidió una mejora visual del visor «con decoraciones y cosas, pero que no parezca IA». Solo presentación: los
datos, la escritura de EV y el wonder trade no cambian.

- **Fondos de caja** (`BoxWallpaper`): como las cajas del juego, cada una tiene su fondo. Doce, dibujados aquí celda a
  celda y no sacados del cartucho —bosque, ciudad, desierto, sabana, rocas, volcán, nieve, cueva, playa, fondo marino,
  cielo y noche—, más uno de Poké Balls para el equipo. Cada uno es un suelo liso y un motivo de pocas celdas escrito como
  texto, repetido en filas al tresbolillo, en cuatro colores apagados para que los iconos sigan siendo lo más vivo. La
  caja 13 vuelve a empezar por el bosque. Sin degradados; la playa junta arena y mar con dos filas de trama y nada más.
  Nieve y cielo se apagaron tras verlos: se comían los huecos.
- **Huecos hundidos**: `PixelPanel` gana `IsSunken` (el relieve se da la vuelta) y respeta el alfa del color, así que el
  fondo asoma por los huecos vacíos. Caído en rojo con una cruz de celdas; variocolor con un destello dorado de celdas.
- **Cursor** (`PixelCursor`): cuatro esquinas doradas que respiran una celda hacia dentro y hacia fuera, a saltos, como el
  cursor del PC. Solo corre el temporizador mientras se ve.
- **Iconos a escala entera**: los iconos del cartucho miden entre 19 y 30 píxeles; se dibujan ×2 en los huecos y ×3 en la
  ficha. A un tamaño que no es múltiplo, los píxeles salían desiguales.
- **Placa de caja** con flechas de celdas; **fila de Poké Balls** del cartucho encima del equipo (llena, vacía o tachada).
- **Ficha**: el retrato de pie sobre el fondo de su caja con una sombra escalonada, la ball en la que se capturó (icono del
  cartucho, balls 1-16: `BoxedPokemon.Ball`, nuevo), el nivel en su placa, el sexo en azul o rosa, placas de sección, barras
  de IV y EV en celdas (`PixelBar`) y los movimientos en cuatro placas de dos en dos. `Subtitle` pasa a ser solo la especie
  cuando hay mote, porque el nivel ya tiene placa. Fuera el PID del pie y el emoji del variocolor.

**Visto en renders de la vista real** con la partida del jugador, leída y nada más, dibujados fuera de pantalla para no
robarle el foco. En esos renders se marcaron como caídos dos Pokémon a propósito, para ver la cruz. Sin ver todavía en la
aplicación abierta.

## §129 · El admin manda regalos, y la app los recoge (2026-09-17)

El jugador quería «una app para tener el control de todo» como admin: dar cosas por ganar un combate, arreglar líos.
Lo primero de eso es esto: **regalos**.

### Por qué un regalo es una petición y no un cambio

La run de cada jugador vive en **su** PC, encadenada evento a evento. Nada de fuera puede escribir en ella, así que el
admin no da puntos: **deja un fichero** en la carpeta compartida y la aplicación del jugador lo aplica y lo registra.
Eso es lo único honesto que se puede hacer sin servidor, y de paso es lo que hace que el regalo quede en el historial
diciendo **de quién** viene y **por qué**.

No es un sistema de permisos: cualquiera con acceso a la carpeta puede escribir un regalo. Entre cinco amigos ese es el
trato, y fingir lo contrario sería el antitrampas de mentira que la regla 3 prohíbe.

### Cómo viaja

- `admin/regalos/<id>.json` en la carpeta compartida, **un fichero por regalo** (`GiftStore`). Una lista única sería un
  fichero que dos programas se pisan mientras Drive sincroniza; ficheros sueltos no chocan, y uno a medio sincronizar es
  **un** regalo que no carga en vez de todos.
- Lleva a quién (un id de jugador o `todos`), de quién, el motivo —obligatorio—, puntos, objetos, tiradas por banner y
  wonder trades.
- **Ahí no se apunta si se recogió.** Eso vive en el historial del jugador, que es el único sitio que puede decirlo: el
  admin lo lee de lo que cada uno publica, y quien recogió algo y aún no ha publicado sale como pendiente.

### Recogerlo

`GiftService.ClaimAsync` en el lado del jugador, con el orden de la tienda y los premios (§45, §60): **primero la
mochila, después el registro**. Un regalo con objetos y el juego cerrado **no empieza**, así que no queda a medias; una
entrega parcial sí cuenta como recogido, porque deberle una Hiperpoción a alguien es mejor que un botón que se puede
repulsar para duplicar lo ya dado. Los puntos van por `PointsService.AdjustAsync`, y las tiradas y los wonder trades se
escriben en los campos `credito` y `creditoIntercambio` que `CreditService` ya cuenta desde el §63: **no hubo que
enseñarle nada de regalos para que paguen**. «Una vez» es que exista su `AdminGiftClaimed`, por id.

### La bandeja

Un **regalo en la cabecera** con el número encima, en todas las pantallas, tal como lo pidió el jugador; al pulsarlo se
abre la lista con lo que trae, el motivo y RECOGER. Nada se aplica solo. Empezó como `Popup` y **se cambió a una capa de
la ventana** tras verlo: el popup se colocaba en la esquina contraria pese a su `PlacementTarget`, y además un menú del
sistema no se dibuja con el pixel art del resto.

### La app del admin

`PermaLocke.Admin` deja de ser el andamio de Visual Studio: lee la carpeta compartida, lista a los jugadores con sus
puntos, sus caídos, cuándo publicaron y si sus números **cuadran con su propio historial**, y escribe regalos. Comparte
`Config/` y `Data/` con PermaLocke en esta máquina, así que sabe dónde está la carpeta compartida sin que se le diga dos
veces — para eso `SharedFolderSettings` pasa a `PermaLocke.Data` y `SyncService` lo usa, en vez de que cada programa
lea el mismo ajuste a su manera. **No abre la base de datos de ninguna run.**

### Lo que costó

`GiftService` pedía `PointsService` **por su clase** y en el contenedor solo está `IPointsService`: la aplicación
arrancaba y moría en el primer arranque de la copia de pruebas. Lo cazó abrirla, no el compilador.

Y dos trampas ya conocidas volvieron a morder al verificar: `PrintWindow` **devuelve el dibujo anterior** con la ventana
fuera de pantalla (§74), así que tres capturas seguidas enseñaron una cabecera sin regalo que sí estaba; y una captura
de pantalla copia **lo que haya delante**, así que una salió con el navegador del jugador y se borró.

**Verificado** en una copia aislada con una carpeta compartida de prueba: dos regalos escritos a mano en la carpeta
aparecen con su contador, su motivo y el aviso de «Necesita Azahar abierto» en el que lleva objetos. **Sin ver todavía**:
la ventana del admin dibujada (el jugador estaba usando el ordenador) y una recogida de verdad contra una partida.

Tests: el regalo —para quién es, qué dice que trae, vacío—, recoger —puntos, objetos, dos veces, con el juego cerrado,
créditos— y la carpeta —ida y vuelta, orden, fichero roto, retirar— (15). 1102.

## §130 · ENTRENAR EV, su propia sección (2026-09-18)

El editor de EV vivía en la ficha del visor: seis casillas debajo de la tabla de estadísticas, en una tarjeta que va del
Pokémon y no de entrenarlo. A petición del jugador pasa a **una sección propia, justo debajo del VISOR**, y el visor se
queda con lo que es: valor e IV, y un botón **ENTRENAR EV** que abre la sección con ese Pokémon ya en el banco.

**Las reglas no se movieron ni cambiaron.** 252 por estadística se recorta, 510 en total solo se comprueba —se puede
pasar mientras repartes y GUARDAR se apaga—, y la escritura sigue siendo `EvTrainingService` → `SaveEvTrainer`: juego
cerrado, PID por delante, copia previa, relectura, y el evento `EvsTrained` **después** de escribir.

### La pantalla

A la izquierda, a quién: el equipo al doble exacto sobre su fondo, y debajo una caja del PC con sus treinta huecos al
tamaño del cartucho, como en el PC del juego (el icono sobre el fondo, el agujero solo al pasar por encima). A la
derecha, el banco: retrato al triple, la **naturaleza con lo que sube y lo que baja**, el hexágono de EV, seis filas
—`−`/`+` de cuatro en cuatro, que es un punto a nivel 100, MÁX y 0— y el presupuesto de 510 en celdas.

El hexágono es el de la ficha del juego —PS arriba y en el sentido de las agujas Ataque, Defensa, Velocidad, Def. Esp.
y At. Esp.— y **se rasteriza celda a celda** (`EvHexagon`): cada celda se decide por si su centro cae dentro, el borde
son las celdas con un vecino fuera y los radios son de Bresenham. Un polígono suavizado en medio de una pantalla hecha de
celdas es lo que delata algo generado. Con una edición en curso, lo guardado se dibuja **como contorno encima** de lo
que hay en pantalla, así que la diferencia se ve sin leer un número.

### Lo que aporta: qué te da

Cada fila dice la estadística de hoy y **la que quedaría**. La cuenta es `IStatForecast` → `WorldStatForecast`, con la
**misma fórmula y la misma tabla** del mundo instalado que `SaveEvTrainer` usa al recalcular: lo que la pantalla promete
es lo que se escribe. Contesta `null` —y la fila no enseña nada— sin tabla, para un huevo, y para una **forma que no es
la primera**, porque `WorldLimits.BaseStatsOf` va solo por especie y un Marowak de Alola no tiene las bases de Marowak.
A nivel bajo el número a menudo no se mueve (252 EV a nivel 1 no dan nada): es el juego, y la pantalla lo enseña tal cual.

`BoxedPokemon` gana `Nature` (el número, no solo el nombre) para decir qué mueve. Ojo con los nombres: **«Tímida» es la
neutra** (Bashful, 18); la que sube Velocidad y baja Ataque es **«Miedosa»** (10).

### Dos cosas que salieron al mirarla, no al escribirla

**Un nivel que no es el de las estadísticas.** Con +8 EV en PS la primera versión prometía `179 ▸ 195` a un Ferrocuello.
Ocho EV no dan dieciséis puntos: los 179 guardados son los de **nivel 59** y la previsión usaba el que sale de la
experiencia, **64**. Medido en la partida real: es el único de los seis del equipo con los dos niveles distintos. El
escritor recalcula con el nivel guardado junto a las estadísticas (`Stat_Level`), así que la pantalla prometía un número
y en la partida se habría escrito otro. `BoxedPokemon.StatLevel` lo trae del equipo, `LevelForStats` elige, y la placa
de nivel de esta sección enseña **el nivel de los números que hay debajo**. Por qué la experiencia de ese Ferrocuello va
por delante de su nivel guardado **no está averiguado**.

**Un Huevo Malo que se podía «entrenar».** La caja 2 tiene una entrada que no cuadra con su firma —«MUERTO», nivel 100,
sin dibujo, 829 EV—, de la familia del §97. Guardar EV ahí la haría pasar por PKHeX, que la devolvería **con una firma
válida**: eso no la repara, deja basura con una especie fuera de rango como Pokémon de verdad, que es lo que puede colgar
el juego. El editor del visor tenía el mismo agujero. `BoxedPokemon.IsIntact` sale de `ChecksumValid` **antes** de
recalcular nada, y `EvTrainingService` **se niega** a escribir una entrada dañada, sea cual sea la pantalla. Aquí se
enseña con su «?» y la frase «Está dañado en la partida: no se toca».

**Pendiente, apuntado aparte:** `SaveEvTrainer.Restat` recalcula las estadísticas del equipo con las bases de la especie
**sin mirar la forma**, así que a una forma regional le escribiría las de la normal. La previsión ya no promete nada en
ese caso; el escritor sigue haciéndolo. **Arreglado el mismo día en el §131.**

**Verificado** con la vista y el ViewModel reales sobre la partida del jugador leída en solo lectura, con un escritor de
mentira que se niega, a los tamaños NORMAL y GRANDE: vacío, equipo, editando, pasado de 510, guardar rechazado, dañado y
de caja. Y la aplicación de verdad arranca con `--sin-juego`, la sección sale en la barra lateral y se abre sin un error
en el log. **Sin ver todavía**: un guardado de verdad desde esta pantalla.

Tests: la previsión —sin tabla, fórmula, tabla del mundo, huevo, forma, naturaleza desconocida, qué marca la naturaleza,
nivel guardado— (9) y el servicio que no escribe una entrada dañada (1). 1112.

## §131 · Las formas regionales, con sus propias estadísticas (2026-09-18)

Al guardar EV, `SaveEvTrainer.Restat` recalcula las estadísticas del equipo con las bases del mundo instalado, y las
buscaba **solo por especie**: `WorldLimits.BaseStats` se llenaba con las filas de las especies y nada más. A un Raichu
de Alola le habría escrito las estadísticas de un Raichu —cinco de seis bases distintas—, y el juego no las recalcula
hasta que sube de nivel, así que con un cap de nivel en vigor se habrían quedado mal. **En la partida del jugador no hay
hoy ningún Pokémon con forma alternativa**, así que no llegó a escribir nada.

### Una sola regla

La tabla de especies del juego pone primero todas las especies y detrás las formas, y cada especie dice en qué fila
empiezan las suyas (`FormStatsIndex`, 0x1C) y cuántas tiene (`FormCount`, 0x20). La regla del juego —la de pk3DS—: una
forma lee **su propia fila** solo si la especie la declara; cualquier otra forma lee **la de la especie**, que es lo que
hacen las que solo cambian de dibujo.

Esa regla ya estaba escrita **dentro** del módulo de dificultad de entrenadores. Pasa a `PersonalEntry7.RowOf` y el
módulo la usa desde ahí, porque dos copias de «qué fila» acabarían discrepando la primera vez que alguien arreglase una.
`GameLink` no puede verla —no depende del randomizador—, así que la regla se aplica **al cargar el mundo** y lo que
llega es el dato: `PersonalEntry7.FormBaseStatsInScreenOrder` saca las formas con fila propia y `InstalledWorld` las
deja en `WorldLimits.FormBaseStats`. `BaseStatsOf(especie, forma)` mira ahí primero y si no, la fila de la especie, que
por la misma regla **no es un hueco sino la respuesta**. El escritor y la previsión de ENTRENAR EV preguntan por especie
y forma, y la previsión deja de callarse con las formas.

### Medido contra el mundo instalado

`1025` especies y **304 formas con fila propia**. Doce anclas con sus bases de la serie: Raichu, Meowth, Ninetales,
Exeggutor y Lycanroc en sus formas normales, de Alola, nocturna y crepuscular, y **el Meowth de Galar, que trae el mod de
gen 8-9**. Las doce cuadran, y no solo como conjunto —que es lo que sobreviviría a `shuffleBaseStats`— sino **valor a
valor**, porque este mundo no las baraja.

La prueba del escritor **falla con el código de antes** (quitado el arreglo, `The_ev_writer_recomputes_an_alolan_raichu…`
en rojo) y pasa con el nuevo.

Tests: la regla de filas y la tabla de formas (4), el mundo por especie y forma, el escritor con un Raichu de Alola y con
uno normal, y la previsión (4). 1120.

## §132 · Habilidades de nueve bits: el randomizador repartía habilidades que no existen (2026-09-18)

Salió mirando la actualización 1.4 del mod de gen 8-9. La incidencia #1 del mod decía que las habilidades nuevas
«salen con nombre y no hacen nada» después de randomizar, y el §5 de `MOD-EXPANSION.md` lo había tomado como
prueba de que no funcionan, con una explicación estructural: el campo de habilidad es un byte, el mod llenó los 22
huecos que quedaban hasta el 255 y ahí se acabó el sitio. **Las dos cosas estaban mal.**

### Lo medido

Comparando byte a byte entradas que deberían llevar habilidades de la 256 en adelante contra una del cartucho: el
**último byte de la entrada (0x53)**, que en el cartucho vale cero en **sus 976 filas**, lleva **un bit por hueco**
que suma 256. Great Tusk es `25/25/25` con `0b111` → 281 Protosynthesis; Kingambit `128/37/46` con `0b010` → el
segundo hueco es 293 Supreme Overlord; Koraidon 288 Orichalcum Pulse. El propio `code_map.csv` del mod lo confirma
con sus palabras: «NINTH ABILITY BIT — restore the ninth bit for the first Ability slot in a personal record», y sube
el tope de nombres a 320. Con los nueve bits el mod usa **79 habilidades nuevas en 104 entradas**, no 22 en 43.

Y **nuestro randomizador era la incidencia #1**. `PersonalEntry7.SetAbility` escribía solo el byte y dejaba el bit,
así que «la habilidad 50» salía como la 306 o como una que no existe. Medido en el mundo instalado (generado el 14 de
septiembre con `randomizeAbilities` en `true`): **240 huecos rotos en 104 Pokémon, 184 apuntando más allá de la 319**
—Koraidon con la 434, la 393 y la 355—, cinco a nombres vacíos y el resto a habilidades de la 9.ª generación al azar.
Todas las paradoja, los tesoros funestos, Koraidon, Miraidon y varias megas. Qué hace el juego con una habilidad que
no existe no está medido; en el mejor caso nada.

### El arreglo

- `GetAbility` y `SetAbility` leen y escriben los **nueve bits**; una habilidad de hasta 255 **apaga** el bit, así
  que nada de lo que el mod había puesto sobrevive en un valor nuevo. Fuera de 0–511 lanza.
- **La regla del hueco vacío miraba el byte**: la 256 (Neutralizing Gas, byte 0 más el bit) pasaba por hueco vacío y
  ni se randomizaba ni gastaba tirada. Ahora se randomiza, pero de **su propia fuente** (`abilities-256`): si gastara
  de la de siempre desplazaría las habilidades de las 250 filas de formas que vienen detrás, en un mundo que ya se
  está jugando. Es la regla del §27 —una fuente por aspecto— aplicada al caso que el lector viejo no veía.
- `RomTool species` lee también los nueve bits.
- El texto de `maxAbility` deja de decir que las nuevas «no funcionan»: siguen fuera, en 233, porque **nadie las ha
  visto funcionar en un combate**, que es otra cosa. Y el `code_map` enseña un bloque de combate que «ejecuta el
  conjunto completo de habilidades añadidas por su número real de 16 bits», con código propio para una treintena.

### Cómo se comprobó

- Ocho pruebas nuevas (leer y escribir el bit, un bit por hueco, el tope, que randomizar no deje ninguno, que los
  bits **no cambien qué habilidades salen**, y que el hueco de la 256 no mueva a los demás). **Cinco fallan con el
  escritor viejo**.
- **Ensayo con la semilla real** (`PERMA-107863`, rol LUDÓPATA) en una carpeta temporal: de los diez ficheros del
  mundo, **nueve salen idénticos** al instalado y el único distinto es `a/0/1/7`. Dentro, cambian **104 filas**,
  solo en el byte **0x53**, y **una** además en un hueco (la 1077: 256 → 195). Ninguna habilidad queda por encima de
  255. El instalado es byte a byte el generado el día 14.

### Lo que falta, dicho

**No está aplicado al mundo instalado.** El paso que sustituye solo la tabla —con copia de la vieja, de la generada y
de la base de datos, relectura, y un evento `RomRandomized` de origen `System` que copia los datos del anterior para
que COMPETICIÓN siga leyendo lo mismo— lo **bloqueó el sistema de permisos** de la sesión como escritura difícil de
deshacer. Se eligió ese camino y no GENERAR + INSTALAR porque reinstalar entero **aparta la carpeta del mod**, y en
ella está el `code.ips` de prueba de «todo sale variocolor» que el jugador quiere quitar él más adelante.

**Y un fallo aparte que salió al comparar**: el módulo de compatibilidad de MT escribe **solo la tabla empaquetada**,
no las filas sueltas, contra lo que dice este mismo documento en el §20. En el mundo instalado discrepan 1329 de 1330
filas, solo en 0x28–0x34. Que el mod 1.4 haya cambiado seis habilidades **solo en las filas sueltas** es un indicio de
que el juego lee esas, y entonces **las MT randomizadas nunca llegaron al juego**. Sin medir; apuntado como tarea.

Tests: 1128.

## §133 · La 1.4 del mod de gen 8-9, instalada (2026-09-18)

A petición del jugador. `Expansion/` pasa de la copia del 1 de septiembre a la **1.4** (13 de septiembre), y el mundo de
la run se regenera encima con la misma semilla y se instala por el camino de la app, GENERAR e INSTALAR.

### Qué se hizo

- **`Expansion/`**: copia previa de todo lo que se iba a sustituir en `Saves/backup/expansion-antes-1.4-<fecha>/`
  (44 MB), los ficheros de la 1.4 encima —comprobados uno a uno— y **el fichero de modelos de 2,5 GB conservado**,
  porque el zip no lo trae. Se quitan `banner.bin` e `icon.bin`, que el propio mod pide borrar (hacían que Ultra Luna se
  anunciase como Ultra Sol).
- **El texto en español**: el `a/0/3/6` que había en `Expansion/` **lo había escrito `RomTool traducir`** el 2 de
  septiembre sobre el inglés del mod. La 1.4 trae el suyo con los nombres oficiales (218 de 218 especies, los 159
  movimientos reales), así que se sustituye y `traducir` deja de hacer falta con esta versión.
- **Nombres de objeto de la configuración**: `randomizer.json` (18 objetos de evolución de las tiendas especiales) y
  `shop.json` (30 megapiedras) los tenían en inglés, porque el mod no traía otro. El guardia de `ShopRandomizer` —el
  nombre se comprueba contra el juego antes de tocar una tienda— **paró la primera generación**: «Galarica Cuff» ahora se
  llama «Brazal Galanuez». Se pasaron al español comprobando que cada id seguía siendo el mismo objeto por su nombre
  inglés en la 1.4; cuatro que la 1.4 retocó en inglés (Scroll of Darkness, Leader's Crest y las dos tazas) se revisaron
  a mano.

### Qué se comprobó

- **Ensayo antes de instalar**, con la semilla real y el rol LUDÓPATA: de los diez ficheros del mundo, **nueve salen
  idénticos** a los de la run y el décimo es el `code.bin`, que cambia porque cambia su base. **Nuestros parches caen en
  los mismos 70 tramos con los mismos 263 bytes** sobre el `code.bin` viejo y sobre el de la 1.4. O sea: encuentros,
  entrenadores, tiendas, aprendizajes y habilidades, iguales; lo nuevo es lo que trae el mod.
- **Después de instalar, fichero a fichero**: los diez nuestros son los generados, todos los de la 1.4 están, la tabla de
  especies es la arreglada del §132 y el fichero de modelos está entero.
- El parche de prueba de «todo sale variocolor» (`code.ips`, que el jugador quitará él) **cambia un byte en 0x2205CF**, y
  el código de alrededor es idéntico en los dos `code.bin`: sigue haciendo lo mismo.

### Lo que salió por el camino

- **El instalador copia encima y no quita lo que ya no viene.** `ModInstaller.Install` guarda una copia de lo que va a
  sustituir, pero un fichero que la nueva base ya no trae **se queda**: el `banner.bin` y el `icon.bin` viejos seguían en
  `exefs` después de instalar, y se apartaron a mano a la copia de seguridad. Con un cambio de versión del mod eso puede
  dejar ficheros de la versión vieja mezclados con la nueva sin que nadie lo vea.
- El arreglo del §132 lo aplicó el jugador con el paso preparado, y **ese paso no llegó a apuntar su evento**: el
  programa no inicializaba SQLite y falló justo ahí, después de sustituir y verificar las tablas. No quedó nada a medias
  en la base de datos, y el evento `RomRandomized` de esta regeneración (15:09) ya recoge el mundo arreglado.

**Sin jugar todavía** con la 1.4: que arranque, que el texto no cuelgue y que las habilidades y movimientos nuevos hagan
lo que dicen en un combate.

## §134 · Pokémon de prueba del mod, y dos lecturas que PKHeX hacía con la tabla de la 807 (2026-09-18)

El jugador pidió Pokémon con habilidades y movimientos nuevos para probarlos en combate, porque con todo randomizado
podía tardar en salirle uno. En realidad **no le saldría nunca**: `maxAbility` 233 y `maxMove` 729 dejan fuera todo lo
nuevo. Hacerlo destapó dos cosas que PKHeX no sabe del mod y que PermaLocke había dado por buenas.

### La habilidad de un Pokémon también lleva noveno bit

El §132 encontró el noveno bit en la tabla de especies. **El Pokémon guardado tiene el suyo**, y está en el bloque que el
mod añade al `code.bin` (`0x4B9C10`, 108 bytes, filas «NINTH ABILITY BIT» de su `code_map.csv`), desensamblado:

- al guardar: `strb r4,[r0,#0xC]` (el byte bajo) y luego `bic`/`orrne r1,#0x10` sobre `[r0,#0xD]`;
- al cargar: `ldrb r4,[r0,#0xC]`, y si `[r0,#0xD] & 0x10`, `orr r4,#0x100`.

`r0` apunta al bloque A del PK7, que empieza en 0x08: son **0x14 y 0x15**. En 0x15 los tres bits bajos son el hueco de
habilidad (1, 2 o 4), así que el bit 4 estaba libre. PKHeX lee y escribe solo 0x14. `PokemonAbility` lee y escribe los
dos, y lo usan el constructor de Pokémon, la ruleta (escribir y comprobar) y el visor. **Sin esto, la ruleta habría
dado a un Pokémon con habilidad nueva la habilidad elegida más 256**, y el visor le habría puesto otro nombre: Cambio
Heroico (278) se lee como Imán (22).

### El nivel de los Pokémon de gen 8-9 se escribía con la curva equivocada

`GameLevels` existe porque PKHeX da crecimiento Medio a todo lo que pasa de la 807, pero **solo lo usaban
el cap y el lector en vivo**. El constructor ponía `CurrentLevel`, o sea la experiencia de ese nivel en la curva de
PKHeX: un Dragapult (lento) pedido a nivel 40 llegaba al **37**, y la comprobación de la entrega lo releía con la misma
curva equivocada, así que pasaba. Afectaba al **wonder trade** con especies del mod, que promete «al mismo nivel», y al
gacha si alguna vez reparte especies del mod. El visor tenía el mismo fallo al leer: es lo que explica el **Ferrocuello
«de nivel 64»** del §130, que es de nivel 59 en su curva lenta (el `Stat_Level` guardado ya decía 59). Ahora el
constructor escribe con `GameLevels.Set`, y el visor, las dos comprobaciones de entrega y los textos de la ruleta leen
con `GameLevels.Of`.

### `Probe --dar-mod`

`--dar-mod <especie> <nivel> <habilidad> <m1,m2,m3,m4> [--forma n] [--objeto id] [--naturaleza n] [--probar]`. Lo que
`--dar-pokemon` no hace con una especie del mod: nivel en la curva del mundo instalado, habilidad con su noveno bit y
**los PP de cada movimiento sacados del `a/0/1/1` instalado**, porque PKHeX no conoce nada por encima de la 742 y daría
cero; un movimiento sin PP (los 32 huecos vacíos del mod) se rechaza. Nombres del texto español instalado. Azahar
cerrado **por proceso** y no solo por RPC (un emulador sin servidor RPC contestaría «cerrado» con el juego cargado),
copia de la partida, relectura campo a campo, y solo entonces se registra: entrada de la run con **origen AdminGrant y
PID**, para que el vigilante lo reconozca en el equipo, y un `PokemonDelivered` del admin con qué es y para qué. Sin
registrar, la primera vez que entrase en el equipo se registraría solo como una captura, y no lo es.

Consecuencias que van con el registro: **cuentan como de la run**, así que si caen restan puntos como cualquiera, y la
cláusula de duplicados bloquea después su línea evolutiva, igual que con lo que da el gacha.

### Lo que se metió en la partida real

Cuatro, a nivel 60 (cap en vigor 66), en la caja 2, huecos 7 a 10, cada uno con copia previa en `Saves/backup/`:

| Pokémon | Habilidad | Movimientos | Qué mirar |
|---|---|---|---|
| Palafin | Cambio Heroico (278) | Puño Jet, Envite Acuático, Viraje, **Plancha Corporal** | salir con Viraje y volver: se transforma |
| Gholdengo | Cuerpo Áureo (283) | Fiebre Dorada, Metaláser, Poltergeist, Tajo Taquión | los movimientos de estado del rival fallan |
| Colmilargo | Paleosíntesis (281), con Energía Potenciadora | Arremetida, Pirueta Helada, Cólera Ardiente, **Giro Mortífero** | se activa al salir, con su mensaje |
| Cinderace | Líbero (236) | Balón Ígneo, **Cambio de Cancha**, Patada Hacha, Brinco | cambia de tipo antes de cada ataque |

En negrita, tres de los **32 con rutina de combate nueva** del mod: son los que más pueden fallar. El resto reutiliza
una rutina del cartucho. Releídos por el lector del visor: habilidad, nivel y movimientos salen bien.

**Queda por arreglar:** el visor nombra los objetos con la lista de PKHeX, y a partir del 960 el mod los numera a su
manera. La Energía Potenciadora (960) sale como «Caramelo Vigor», y las megapiedras del mod también saldrían con otro
nombre. Es solo el nombre en pantalla; en el juego se llama bien.

## §135 · Los muertos se curaban: la lista de direcciones del equipo tenía ocho días (2026-09-18)

El jugador: «los Pokémon que mueren se pueden curar, no vuelven a estar muertos». Y el registro lo confirma: a las
18:00:31 muere Bouffalant (detectado en el combate, en el momento), a las 18:03:19 vuelve a caer alguien en la primera
posición sin muerte nueva —el mismo Bouffalant, ya curado— y **`KeepFallenDownAsync` no escribió ni una vez en toda la
sesión**, sin una línea en el registro.

La causa está en cómo se conecta. Para no barrer 96 MB en cada arranque, `AzaharGameStateProvider` guarda las
direcciones del equipo en `Saves/backup/equipo.txt` después de cada barrido y la sesión siguiente las **revalida sin
barrer**. Ese fichero era del **10 de septiembre**, y desde entonces se aceptaba siempre, porque la revalidación solo
pedía que **alguna** de las recordadas leyera el equipo, y el espejo (`0x330128E4`, salto `0x104`) está en el mismo
sitio en todas las sesiones. La estructura que el juego lee de verdad (salto `0x1E4`, §99) **no**: se reserva donde
toca cada vez. Así que la app:

- leía el equipo **del espejo**, cuyos PS van con retraso: el caso exacto que el §99 quería evitar;
- y mandaba a los caídos a 0 PS en direcciones de hace ocho días, donde ya no estaban. Como allí no había su PID,
  no escribía nada y no lo decía.

El síntoma estaba en la propia línea del registro: «Equipo en 0x330128E4, el de la última vez, revalidado sin
barrer». `PartyLayoutLocator.Preferred` elige la estructura del juego siempre que lea a alguien, así que **acabar en el
espejo** significa que ninguna de las recordadas de salto `0x1E4` tiene ya el equipo.

**Arreglo:**

- `PartyLayoutLocator.NeedsLocatingAgain`: si lo elegido de lo recordado es el espejo, se conecta ya con él —para no
  dejar de vigilar— y **en la lectura siguiente se barre** (unos 5 s) y se guarda la lista nueva.
- `KeepFallenDownAsync` distingue «está y ya tiene 0 PS» de «no está en ninguna copia que lea el juego». Lo segundo se
  dice en el registro, una vez por Pokémon, y pide otro barrido. Callarse es lo que escondió esto ocho días.

**Sin comprobar en el juego todavía**: la app estaba abierta y bloqueaba sus ficheros. Hay que cerrarla, compilar, y
meter en el equipo a un caído con la app y el juego abiertos: tiene que bajar a 0 PS en un segundo y dejar en el
registro «al suelo otra vez». Bouffalant sirve: está muerto en la run y guardado en la caja 8.

## §136 · Los ataques y habilidades del mod entran en el randomizado (2026-09-18)

Después de probar en combate los cuatro Pokémon del §134 —cuatro habilidades nuevas y doce ataques nuevos, tres de
ellos de los 32 con rutina de combate propia del mod—, el jugador pidió meterlos todos en el randomizado de su mundo, a
mitad de run, sabiendo que cambian las habilidades y los aprendizajes de cada especie.

### El tope que no se veía

Poner `maxAbility` y `maxMove` a cero **no habría hecho nada**. `PokemonDataRandomizer` calculaba el techo como
`Math.Min(configuración, workspace.Config.Info.MaxAbilityID)`, y `GameInfo` de pk3DS trae **233 habilidades y 728
movimientos clavados**, los del cartucho, sin mirar los ficheros. Es la misma trampa del 807 de las especies: la
configuración decía una cosa, el mundo otra, y nada fallaba. Ahora el techo sale del juego que se randomiza: la longitud
de su lista de nombres de habilidad (320 en el mod) y de su tabla de movimientos (921).

La primera prueba en seco salió **idéntica** al mundo instalado, y no por el código: la herramienta de ensayo usaba las
DLL compiladas de RomTool, que estaban viejas. Recompilada, cambian exactamente los dos ficheros que tienen que cambiar.

### Qué se reparte y qué no

- **Habilidades**: `AbilityTable.Assignable` saca de 1 al techo las que tienen nombre (los huecos «-», 301-304 y
  317-318, no existen) y quita `bannedAbilities`: **las del mod atadas a las formas de un Pokémon** —Tragamisil, Cara de
  Hielo, Mutapetito, Cambio Heroico, Comandar y las tres Tera—, cuyo código está escrito para ese Pokémon y en otro
  podría intentar cambiarlo a una forma que no tiene; y la 319, que tiene nombre pero **ninguna especie del mod usa**. Las
  del cartucho con formas no se tocan: el juego original las ignora en otro Pokémon.
- **Ataques**: todo lo que tenga PP de 2 para arriba. Los 32 huecos del mod (Let's Go y Dinamax) tienen 0 y los Z tienen
  1, así que `MoveTable.Teachable` ya los dejaba fuera.
- **El sorteo no se ha movido**: se tira un índice dentro de la lista. Con la lista entera 1..máx es la misma tirada que
  el `Next(1, máx + 1)` de antes —hay test que lo comprueba mil veces—, así que un mundo sin exclusiones, el cartucho por
  ejemplo, sale igual que antes.

### Medido en seco, con la semilla y el rol reales

- De los diez ficheros del mundo cambian **dos**: `a/0/1/7` (habilidades) y `a/0/1/3` (aprendizajes). Salvajes,
  entrenadores, tiendas, estáticos, evoluciones, objetos y `code.bin`, **idénticos** al instalado.
- `a/0/1/7`: 1329 filas, **ni un byte cambiado fuera de las habilidades**; 946 huecos con habilidad nueva, 71 habilidades
  nuevas distintas, cero huecos sin nombre y cero prohibidas. La mayor que sale es la 316, Crin de Fuego.
- `a/0/1/3`: mismas longitudes, **ni un nivel cambiado**; de 21286 movimientos, 4746 son nuevos (22 %), 159 distintos,
  y ninguno con 0 o 1 PP.
- Las filas sueltas de `a/0/1/7` siguen sin cuadrar con la tabla empaquetada **en los bytes de las MT** (0x28-0x34), igual
  que en el mundo instalado: es el fallo ya apuntado, no de este cambio.

**Queda por ver jugando** lo que no se puede medir en un fichero: que las 71 habilidades y los 159 ataques hagan lo que
dicen. Solo se han visto cuatro y doce.

## §137 · El gacha y el wonder trade también reparten las habilidades del mod (2026-09-18)

Los dos sortean la habilidad entre **todas** las del juego, no las de la especie, y los dos descartaban todo lo que
pasara de la 233 (`IAbilityLookup.LastUsableAbility`), con la misma premisa falsa que el randomizador: que la habilidad
de un Pokémon cabe en un byte y que las del mod «tienen nombre y no hacen nada». El Ursaluna que se anunció con General
Supremo (293) y llegó con Potencia (37) era el constructor escribiendo solo el byte; desde el §134 escribe el noveno bit.

- **La lista de nombres estaba vieja.** `Data/species.json` era de antes de la 1.4: la mezcla de PKHeX e inglés de
  `RomTool traducir`. La 301-304 decían «Evocarrecuerdos (…)», que en el mod son **huecos**, y la 311-316 venían en
  inglés. Quitar el tope sin rehacerla habría repartido habilidades que no existen. Regenerada con `RomTool species`
  sobre la 1.4: **solo cambian los nombres de habilidad (15) y las habilidades propias de 74 especies**; totales, nombres,
  legendarios y las 553 familias salen idénticos, así que los niveles del gacha no se mueven.
- **Un sorteo, no dos.** El bucle vivía copiado en `GachaService` y `WonderTradeService`; ahora es `AbilityDraw`, con
  la misma forma (hasta doce intentos, se tira y se descarta en vez de estrechar), así que una tirada que ya caía en
  una habilidad válida sale igual —hay test— y solo se mueven las que caían por encima de la 233.
- **Las excluidas son las del randomizador.** `JsonSpeciesStatsCatalog` recibe `bannedAbilities` de
  `Data/randomizer.json` al arrancar la app: una sola lista de «qué no se puede dar».
- La ruleta **no cambia**: sus caras nombran habilidades concretas del cartucho y las resuelve contra PKHeX hasta la 233.

Medido con los ficheros reales, 5000 sorteos: el 23 % sale con habilidad del mod, salen las **71** posibles, y ninguna
es un hueco ni una excluida. Afecta solo a lo que se tire a partir de ahora; lo ya entregado no cambia.

## §138 · Las formas regionales, por fin en el randomizado (2026-09-18)

El jugador, con razón: «es un random, tiene que poder salir cualquier Pokémon». **Nunca había salido una forma regional.**
Todos los módulos escribían forma 0: `EncounterTable7.SetSpecies` la pone a cero por defecto, `TrainerPokemonTable` y
`StaticEncounterTable` la limpian a propósito —«un índice de forma válido para la especie vieja no lo es para la
nueva»—, y el constructor del gacha y del wonder trade también. Comprobado en la partida: de 123 Pokémon, **ninguno**
tenía forma distinta de 0 salvo el Huevo Malo.

### Qué formas

Medido contra el mod: **59 formas con nombre de región** (PKHeX, contexto de gen 9) y **las 59 con los tipos oficiales**
en su fila de la tabla del mod, o sea que el mod las numera como los juegos. Fuera la gorra «Alola» de Pikachu, que es un
disfraz, y el modo Daruma de Darmanitan de Galar, que solo existe en combate. Quedan **57 formas en 54 especies**: 18 de
Alola, 19 de Galar, 16 de Hisui y 4 de Paldea (las tres razas de Tauros y Wooper). La lista va en `regionalForms` de
`Data/randomizer.json`, porque el randomizador no usa PKHeX; al generar se queda solo con las que **el mundo cargado
declara** (`RegionalForms.From` contra el número de formas de la especie), así que el cartucho sin mod coge solo las de
Alola.

### Cómo se decide

**Después de la especie y con su propio sorteo** (`random.Derive("forms")` en cada módulo): la especie de cada hueco
sale **exactamente** la misma, tirada a tirada, y lo único que cambia es que algunos salen en forma regional. Cada forma
es tan probable como la normal —un Meowth es de Kanto, de Alola o de Galar un tercio de las veces—, y una especie sin
formas regionales no gasta tirada. Las llamadas SOS copian especie **y forma** de su hueco base. Afecta a salvajes,
entrenadores, el Pokémon extra del rol, estáticos, regalos, intercambios e iniciales; no a las reglas especiales de
estáticos ni a las megas de jefe, que ya ponen su forma a propósito.

### Medido en el mundo de la run, e instalado

De los diez ficheros cambian **tres**: salvajes, entrenadores y estáticos. Dentro:

- **Salvajes**: 45.848 huecos, **ninguna especie distinta**, 1.214 en forma regional, cero formas no válidas y ningún otro
  bit del hueco tocado.
- **Entrenadores**: 1.261 Pokémon, ninguna especie distinta, 35 en forma regional.
- **Regalos**: un Arcanine de Hisui. **Estáticos**: 8 (Sandslash y Ninetales de Alola, Slowbro y Corsola de Galar,
  Samurott de Hisui...). **Intercambios**: ninguno.

Instalado desde la app (GENERAR e INSTALAR, evento `RomRandomized`), fichero a fichero igual que el ensayo.

### Lo que falta

- **El gacha y el wonder trade** siguen dando la forma normal: les falta sortear la forma, llevarla hasta el constructor
  y enseñar tipos por forma.
- **Los dibujos**: toda la app pinta el icono **de la especie**, así que un Vulpix de Alola sale con el dibujo del normal
  en HOME, el visor o el cementerio. Los iconos de las formas de Alola están en el cartucho y la tabla del §30 sabe dónde
  (van antes que el normal); los de Galar, Hisui y Paldea están entre los que añade el mod y **no se han medido**.
- Sin jugar: que el juego saque bien en combate las formas que añade el mod.

## §139 · Formas regionales en el gacha y el wonder trade, y un dibujo por forma (2026-09-18)

Lo que faltaba del §138.

### Los dibujos, sacados del propio mod

Toda la app pintaba el icono **de la especie**. Para los de Alola la respuesta ya estaba en el §30: el icono de Alola va
**justo delante** del normal (`NormalFormOffsets` lo salta), así que la forma 1 es un icono atrás; Dugtrio y Muk tienen
dos de Alola idénticos y uno atrás sigue siendo uno de ellos. Para los de Galar, Hisui y Paldea **no se ha mirado ni un
dibujo para decidir**: el mod sustituye la búsqueda de iconos del juego (`0x20C88C` de su `code.bin`, fila «POKEMON
ICONS» de su `code_map.csv`) y, desensamblada, construye la clave `especie | forma << 11`
(`orr r3, r0, r4, lsl #11`), recorre una tabla de medias palabras en `0x5BDBE2` hasta un cero y devuelve
`1153 + 0xDB` más la posición de la clave. Esa tabla son **136 claves** en orden de especie, justo los 136 iconos que
el mod añade tras sus especies (1372-1507). Están copiadas en `PokemonIconIndex.ExpansionFormKeys`, y los 39 de Galar,
Hisui y Paldea que el randomizador reparte **se comprobaron después a ojo** en una hoja de contactos: todos son su forma.
Solo se usan si el contenedor tiene exactamente 1372 + 136 iconos, que es la 1.4; con otro, cada forma pinta su especie
en vez de arriesgarse a pintar a otro Pokémon (el riesgo del §45).

`PokemonSpriteService.Get(especie, forma)`, y lo usan el visor, ENTRENAR EV, HOME, JUGAR, el gacha y el wonder trade.
El equipo en vivo (`LivePartyMember`, `LivePokemon`) lleva ya la forma.

### El gacha y el wonder trade

- `Data/species.json` lleva ahora, por especie, sus formas regionales con el nombre de PKHeX en español («Alola»,
  «Galar», «Paldea Combatiente»...): las de `regionalForms` que el mundo declara. `RomTool species` lo regenera; solo se
  añaden las formas, lo demás sale idéntico.
- `FormDraw`: la normal o cualquiera de sus formas, **todas igual de probables**, desde una fuente **derivada** de la de
  la tirada. Derivar no la avanza, así que especie, nivel, IV, naturaleza, habilidad y brillo salen **exactamente como
  antes** —hay test que lo compara en 200 tiradas— y solo la forma es nueva.
- La tirada y el intercambio llevan `Form` y `FormName`, y se enseñan como «Vulpix de Alola». La forma llega al
  constructor, se escribe en la partida y **se comprueba al releer**. Los eventos guardan `forma`.
- El wonder trade anuncia los **tipos de la forma**: `PersonalEntry7.FormTypes` los lee de las filas de forma del mundo
  instalado y `WorldLimits.TypesOf(especie, forma)` los da; un Meowth de Galar sale de Acero, no Normal.

### Lo que no lleva forma todavía

- El **cementerio**: el registro de la run (`PokemonEntry`) no guarda la forma, así que una tumba de un Vulpix de Alola
  pinta el normal. Hace falta una columna nueva en la base de datos.
- **POKE PASTE** exporta el nombre de la especie sin el sufijo de forma («Vulpix-Alola»), así que la web la dibuja normal.
- La lista de «quién puede salir» del gacha enseña especies, no formas.

## §140 · La forma también en el cementerio, POKE PASTE y la lista del gacha (2026-09-19)

Lo que el §139 dejó dicho como pendiente.

- **La run guarda la forma.** `PokemonEntry.Form` y una columna `form` en la tabla `pokemon`. Es **la primera migración
  de esa tabla**: se pregunta a la tabla qué columnas tiene (`PRAGMA table_info`) en vez de llevar un número de versión,
  así que repetirla o abrir una base ya creada con la columna no hace nada. Las filas viejas quedan en 0, que es lo que
  eran: hasta el §138 no salió ninguna forma regional. Probada sobre **una copia de la base de datos real** antes de que
  la app la abriera: 145 Pokémon y 1147 eventos antes y después, con la columna añadida. La guardan el gacha, el wonder
  trade, el registro automático de capturas (que la lee del equipo en vivo), la ventana de captura a mano (solo si la
  especie sigue siendo la detectada) y `Probe --dar-mod`.
- **Cementerio y escena de muerte**: la tumba, el aviso y la escena «HA MUERTO» pintan el dibujo de la forma.
- **POKE PASTE**: `BoxedPokemon.FormName` lleva el nombre que Showdown pone tras la especie, sacado de
  `ShowdownParsing.GetStringFromForm` en contexto de **gen 9** —el de gen 7 no conoce las de Galar ni las de Hisui—, y el
  formateador escribe «Vulpix-Alola», «Meowth-Galar», «Tauros-Paldea-Combat». Sin eso la web dibuja la forma normal.
- **La lista de «quién puede salir» del gacha**: cada forma va **al lado de su especie y sin flecha**, porque no es en
  lo que evoluciona sino otra manera de salir esa misma etapa (la tirada elige especie y luego forma). Se busca por
  forma: escribir «Alola» deja las familias que la tienen. Vista en la app con el Tier 3: Exeggutor, Vulpix, Ninetales,
  Grimer, Muk, Geodude, Graveler y Golem de Alola, cada uno con su dibujo.

### §135, verificado en el juego (2026-09-19)

Del registro de la sesión del jugador: a las 01:39:18 la conexión por las direcciones recordadas acaba en el espejo
(`0x330128E4`), la app lo dice y **barre**; a las 01:39:24 —5,5 s después— tiene la estructura del juego en
`0x33F807C4` (salto `0x1E4`, 10 copias). A las 01:40:30 y otra vez a las 01:40:50, «Bouffalant está caído y le habían
devuelto los PS: al suelo otra vez». El jugador lo confirma mirando la pantalla.

## §141 · Las MT solo se escribían en una de las dos copias de la tabla de especies (2026-09-19)

`a/0/1/7` guarda la tabla de especies **dos veces**: una copia suelta por especie y forma (subficheros 0 a 1328) y la
tabla entera al final. El módulo de datos escribe su fila en las dos (§27); **el de compatibilidad de MT solo en la
entera**. En el mundo instalado del jugador, con su equipo guardado, las dos copias discrepan en **94 de las 100 MT**
para alguien del equipo. Cuál lee el juego no está medido; la pista es que la 1.4 del mod cambió seis habilidades
(Empoleon, Gallade, cuatro megas) **solo en las sueltas**, cosa que su autor habría notado si el juego leyera la
entera.

**Arreglo**: el módulo escribe las MT también en cada copia suelta, y al releer exige que cada una coincida con su fila de
la tabla entera. Ensayado con la semilla y el rol reales: de los diez ficheros solo cambia `a/0/1/7`; la tabla entera
**no cambia ni un byte**, y las 1329 copias sueltas cambian **solo** en los bytes de las MT (0x28-0x34). Las dos copias
quedan idénticas.

**La prueba para saber cuál lee el juego**, con el mundo instalado todavía sin arreglar: la MT43 (Velo Sagrado). Según la
tabla entera la aprenden Bouffalant, Cinderace, Gholdengo, Slaking y Salamence; según las sueltas, ninguno.

**Medido (2026-09-19): el juego lee la tabla ENTERA.** El jugador abrió la MT43 con ese equipo y la pueden aprender
**todos menos Colmilargo**, exactamente lo que dice la tabla entera; según las sueltas no podía ninguno. O sea que las MT
del mundo del jugador **siempre estuvieron randomizadas** y la pista de arriba era falsa: los seis cambios de habilidad
que la 1.4 hizo solo en las sueltas **tampoco funcionan en el propio mod**, que es un fallo suyo y no nuestro. El arreglo
se queda —las dos copias iguales no cuestan nada y cualquier herramienta que lea las sueltas verá lo mismo que el
juego—, pero **no hacía falta reinstalar** el mundo: entra en la próxima generación. Anclaje que vale para todo lo que lea
esta tabla en adelante: **la autoridad es el último subfichero de `a/0/1/7`**, que es la que ya leían `InstalledWorld`, el
visor y la dificultad de los entrenadores.

## §142 · El recuerda-movimientos, dentro de la aplicación, como el de Añil (2026-09-19)

**La petición.** La recuerda-movimientos de Ultra Luna es la señora del Centro Pokémon del Monte Lanakila, a las puertas
de la liga: un juego entero de distancia. El jugador preguntó si se podía «mover» al principio; si no, diseñarla en la
app, y en ese caso **como la de Pokémon Añil en formato randomlocke**, porque en un randomlocke lo que se aprende depende
de la evolución y del nivel.

**Mover a la señora: no, de forma fiable.** Una persona del mundo y lo que hace al hablarle viven en los ficheros de
zona y en sus scripts (AMX comprimido), que en este proyecto nadie ha identificado: pk3DS no trae editor de mundo para la
séptima generación y aquí no hay ni una medida de ese formato. Sería una investigación del tamaño del §22, con la pega
añadida de que un script mal escrito **cuelga el juego**. Y aunque se pudiera, la del juego cobra una Escama Corazón por
movimiento. Así que se hace en la app, que además no depende de si el juego sabría manejar los movimientos del mod.

**Cómo funciona el de Añil.** Mirado en el seguimiento de la competición de Añil (`SukenFuyumi/anil-super-randomlocke`,
`companion/extract.js` y `jugador.html`): enseña los movimientos **iniciales** —los que el Pokémon traía al obtenerlo,
un campo que el juego guarda por Pokémon— «siempre disponibles», y los **de su aprendizaje randomizado por nivel**, de su
especie y forma **actuales**, con los de evolución aparte. Es la regla de Pokémon Essentials: nivel ≤ el suyo, más los
de «primeros movimientos». La idea de fondo, que es la que pedía el jugador: al evolucionar, la lista pasa a ser **la de
la nueva especie** —en un randomlocke cada especie tiene su aprendizaje, así que una familia no comparte movimientos—, y
lo que sabía de antes solo se recupera si estaba entre los iniciales.

**La regla aquí** (`MoveReminder`, en Core, sin nada más que la regla):
1. **Los que sabía al llegar.** Dos fuentes: lo que PermaLocke apuntó al registrarlo —desde hoy el evento de captura
   guarda `movimientos`— y los cuatro huecos «para volver a aprender» que el propio juego guarda en cada Pokémon (huevo,
   regalo). Es la misma idea que los `first_moves` de Añil, puesta por el juego.
2. **Al evolucionar**: las entradas de **nivel 0** de su aprendizaje. Medido en el mundo instalado: 342 filas las
   tienen, y son las de especies evolucionadas (Venusaur, Charizard, Metapod, Butterfree…).
3. **Por nivel**: su aprendizaje hasta **su nivel incluido**.
Fuera lo que ya sabe; un movimiento que llega por dos caminos sale una vez, por el que dice a qué nivel; y nada con 0 PP
(los huecos vacíos del mod), que el juego no deja elegir.

**De dónde salen los aprendizajes.** Del **mundo instalado** (`a/0/1/3`, una entrada por fila de la tabla de especies,
pares movimiento-nivel cerrados por `0xFFFF`), con `WorldMoveTables` leyéndolo al arrancar y `WorldMoves` publicándolo,
igual que `WorldLimits`. Medido: 1330 aprendizajes para 1330 filas. Las formas con fila propia leen la suya por la regla
de `PersonalEntry7.RowOf`. Los datos del movimiento (tipo, clase, potencia, precisión, PP) salen de `a/0/1/1` y los
nombres del texto español del mundo (fichero 118); la clase y el «no falla nunca» se anclan en Placaje, Ascuas y Rapidez
como en el randomizador. Sin mundo instalado se pregunta a PKHeX, que es el cartucho, y la pantalla dice de cuál sale.

**La escritura** (`SaveMoveTeacher`), con las guardas de los EV: juego cerrado, el hueco comprobado **por PID**, el
movimiento del hueco comprobado **tal como la pantalla lo vio** —si el jugador cambió movimientos en el juego entre medias,
no se le olvida uno que no eligió—, copia de la partida entera y relectura. El movimiento entra con sus PP del mundo y sin
los Más PP del que sustituye, como en el juego. **El servicio vuelve a calcular la lista él mismo**, así que la pantalla no
puede escribir nada que la regla no permita. Primero se escribe y después se registra el evento `MoveRemembered`, con el
movimiento, el olvidado y por qué se podía. Un **caído no aprende nada**, igual que el wonder trade no acepta uno; un
Huevo Malo ni se toca (§97). **No cuesta nada**, como los EV: es editar un Pokémon propio, y un precio sería una regla que
la competición no ha acordado.

**Lo que salió de paso: lo que entrega la app aprendía los movimientos del cartucho.** `PokemonBuilder` pedía a PKHeX los
movimientos sugeridos, que son los del **cartucho**: todo lo del gacha, el wonder trade y la ruleta llegaba sabiendo cosas
que su especie no aprende en el mundo randomizado, y uno de gen 8-9 no llegaba sabiendo nada (la tabla de PKHeX acaba en la
807). Ahora aprende lo que haría el juego con uno salvaje —los cuatro últimos de su aprendizaje del mundo hasta su nivel—,
con los PP del mundo, y esos cuatro quedan también como «para volver a aprender», que es lo que el recuerda-movimientos
ofrece siempre.

**Verificado.** Contra el mundo instalado y la partida real con `Probe --recordar`: a Salamence (Nv 59) le ofrece su
movimiento de evolución y los veinte de su especie hasta el 59; a Cinderace, Colmilargo y Gholdengo sus aprendizajes del
mod con nombres españoles. `Probe --recordar --probar` escribe **sobre una copia** (Bouffalant, Viento Carámbano en el
hueco 4), la relee con la firma bien, y la partida real tiene la misma huella antes y después. La pantalla está vista
con la partida real: con un Pokémon vivo, y con uno caído, que sale atenuado y con RECORDAR apagado. **Sin probar
todavía:** recordar de verdad desde la pantalla y ver el movimiento en el juego.

## §143 · ENTRENAR EV y MOVIMIENTOS, con el aspecto de la bolsa de Ultra Luna (2026-09-19)

**La petición.** Las dos pantallas «cansan la vista»: mucho morado oscuro, texto pequeño y apagado, iconos de caja
diminutos. El jugador pidió innovar, a poder ser con el estilo de **la bolsa del juego**, y que los Pokémon de la caja
se vieran más grandes. Es **solo presentación**: ni un servicio, ni una regla, ni un enlace cambian.

**La referencia, mirada y no copiada.** La bolsa de Sol/Luna y Ultra Sol/Ultra Luna (capturas de Bulbapedia): fondo
naranja a cuadros finos, el equipo en tarjetas verdes a la izquierda, los objetos en **cápsulas** claras con borde
oscuro y los extremos redondeados, una **flecha roja** que señala la elegida, pestañas de bolsillo, una franja de
**cuero con costura** para los botones y, arriba, la pantalla de la bolsa por dentro: marco turquesa con **cremallera**
y un amanecer de amarillo a naranja donde va el nombre del objeto y su descripción. No se usa ni un dibujo del juego:
todo se pinta en celdas (`Views/BagPixels.cs`) con colores planos.

**Cómo encaja con la regla visual del proyecto** (sin aspecto de IA, §116): donde el juego usa degradado, aquí hay
**franjas planas unidas por una trama ordenada** (Bayer 2×2); los extremos redondos de las cápsulas son **un círculo
rasterizado fila a fila**, en escalones; la sombra es un bloque desplazado una celda, y el texto blanco lleva sombra
dura sin difuminar. La flecha roja se mueve **a saltos**, dos fotogramas.

**Las piezas.**
- `BagChecker`, `BagCapsule` (píldora o tarjeta con `Radius`, luz arriba, labio abajo, sombra y estado pulsado),
  `BagScreen` (marco con cremallera y amanecer), `BagArrow` y `BagLeather`, en `Views/BagPixels.cs`.
- `Themes/Bag.xaml`: colores, textos, botones en cápsula, tarjetas del equipo, filas de la bolsa y una barra de
  desplazamiento de cuero y turquesa. **Lo mezclan solo estas dos pantallas**: el resto de la app sigue con su tema.
- `Views/BagPocketPanel`: la mitad izquierda, compartida. El equipo en tarjetas verdes (rojizas si están caídas,
  amarillas si es la elegida) y **la caja como un bolsillo de la bolsa**: una fila por Pokémon, **con su icono al
  doble** asomando por encima de la cápsula, nombre y nivel. Los huecos vacíos no se dibujan, como en la bolsa.

**ENTRENAR EV**: la ficha con cremallera lleva el retrato al cuádruple sobre un disco, nombre, sitio, nivel,
naturaleza con lo que sube y baja y el hexágono (que gana `Plate` y `Guide` para ir en turquesa oscuro con los EV en
amarillo). Debajo, las seis estadísticas en cápsulas —amarilla la que tiene cambios sin guardar—, el total de 510 y los
botones en la franja de cuero.

**MOVIMIENTOS**: los cuatro que sabe son **los botones de combate del juego, del color de su tipo**, con el cursor de
esquinas en el que va a olvidar; lo que puede recordar es una lista de la bolsa con la flecha roja, su placa de tipo en
color y la razón («EVO», «INICIO», «Nv 12») en una cápsula turquesa. La explicación de la lista va dentro de la ficha,
como la descripción del objeto en la bolsa.

**Visto** con la partida real (solo lectura, `--sin-juego`) en el tamaño MUY GRANDE y en el NORMAL (1180×760, el más
pequeño que ofrece la app), con un Pokémon vivo, con uno caído y con un reparto de EV a medias sin guardar. Para ver el
NORMAL sin tocar el tamaño que eligió el jugador hay un argumento de ensayo, `--tamano normal|grande|enorme`, que abre
a ese tamaño **sin guardarlo**. Las dos columnas son elásticas (la izquierda entre 290 y 370) para que a 1180 no se
corten las filas.

## §144 · MOVIMIENTOS: los iconos oficiales de categoría, las cifras a la vista y la lista seguida (2026-09-19)

**La petición.** En MOVIMIENTOS, que se vean mejor el tipo, la potencia, la precisión y la categoría, con **los iconos
oficiales** —el rojo y el azul de siempre— y **«-----» para los de estado**; y la lista **toda seguida**, sin
apartados por evolución, nivel ni nada.

**Los iconos están en el cartucho, y se sacan de él.** Listando el nombre de cada imagen de cada pantalla (ALYT) del
RomFS salió `a/0/6/6`, que nombra `waza_icon_all.bflim`. Volcadas sus imágenes, una de 64×64 en RGBA8888 lleva **los
tres iconos apilados, 41×18 cada uno**: estado (gris), físico (naranja con la estrella) y especial (azul con los anillos).
`MoveCategoryIconReader` la talla con el mismo método que los cristales Z (§61, ahora compartido en `AlytCarver`) y
**nombra cada banda por su color y no por su orden**, que es lo que enseñó el §61: gris es estado, rojo físico y azul
especial, y una lámina que no traiga exactamente uno de cada se rechaza. Pruebas con una lámina fabricada, sin ROM. Como
todo sprite, **PermaLocke no reparte ninguno**: se sacan de la ROM del jugador la primera vez y se guardan en
`Data/sprites/categorias`, que no se versiona.

De paso, la lista de nombres enseña que en esas pantallas hay un `type_icon_00_normal.bflim`: **uno** solo, el del tipo
Normal. No cambia lo que dijo el §34: el juego no trae una placa por tipo, la compone.

**La pantalla.**
- La lista de lo que puede recordar va **seguida**, en el orden de la regla (lo que sabía al llegar, lo de evolución y
  lo de nivel), y cada fila lleva a la izquierda por qué se puede («ORIGEN», «EVO», «INICIO», «Nv 12»). Se fueron los
  apartados y la propiedad `Group` que los sostenía.
- Cada fila, en dos líneas: la razón y el nombre, grande; debajo **la placa de tipo** (más grande que antes) y **la
  categoría con el icono oficial al doble**, o una cajita con «-----» si es de estado. A la derecha, **tres cajitas
  blancas con POTENCIA, PRECISIÓN y PP**, la cifra grande. Donde el juego no pone número —potencia de un movimiento de
  estado, precisión de uno que no falla, los de potencia variable— sale «---», como en su propia ficha.
- Los cuatro que sabe llevan lo mismo sobre el color de su tipo: nombre, icono de categoría con el tipo, y las tres
  cifras con su rótulo. Van **cuatro en fila en la ventana grande y 2×2 en las pequeñas** (`WidthToColumnsConverter`),
  porque a 1180 cuatro en fila dejaban cien píxeles por tarjeta.
- La columna derecha se desplaza **entera** —ficha, los cuatro y la lista— y la franja con RECORDAR queda siempre
  abajo. Las listas no tienen barra propia, para que la rueda del ratón no se atasque encima de ellas.

**Visto** con la partida real (solo lectura) en MUY GRANDE y en NORMAL, con Salamence y con Cinderace, y los iconos
comprobados a zoom: nítidos, a píxel entero. Sin cambios de comportamiento: la regla y la escritura son las del §142.

**Y las estadísticas del Pokémon en la ficha (mismo día).** El jugador pidió verlas también aquí, que es donde se decide
entre un físico y un especial. Son **las mismas que en ENTRENAR EV**: las del equipo, las que guarda la partida; las de
caja, calculadas con las bases del mundo instalado (`IStatForecast`), y marcadas con «≈» solo si esas bases no se pueden
leer. El nombre de cada una va del color de la naturaleza —rojo la que sube, azul la que baja—. Van **al lado del
nombre** en la ventana grande y **debajo** en las estrechas (`WidthSwitchConverter` cambia su fila y su columna), porque a
1180 dejaban el nombre en «Sala…». La nota de dónde salen los aprendizajes baja junto a PUEDE RECORDAR para dejarles sitio.
Visto con Salamence, Cinderace y un Natu de la caja, en los dos tamaños.

**Y un fallo al RECORDAR (mismo día): se cerraba la ficha.** El jugador enseñó un movimiento a Gholdengo y la pantalla lo
soltó. Después de escribir, la pantalla relee la partida para enseñar los movimientos nuevos, y para volver a elegir al
Pokémon pasaba por el mismo camino que un clic; ese camino **se niega mientras se está escribiendo** —para que nadie cambie
de Pokémon a mitad de una escritura— y la relectura ocurre justo antes de dar la escritura por acabada, así que se negaba
y dejaba la ficha vacía. Ahora la relectura vuelve a poner el marco a mano, sin pasar por el clic, y elige al Pokémon con
el registro recién leído. Y al ser el mismo Pokémon, **sus listas no se vacían antes de llenarse**: se sustituyen de una
vez, así que la columna no se encoge y el desplazamiento se queda donde estaba. Comprobado con RELEER LA PARTIDA, que
sigue el mismo camino: Salamence sigue abierto y la lista en el mismo punto (60 % antes y después).

**Y qué hace cada movimiento (mismo día).** El jugador pidió ver la descripción del ataque que está eligiendo. Sale del
**texto del propio juego**: el fichero 117 del texto en español (`a/0/3/6`), junto al 118 de los nombres, leído del mundo
instalado como el resto (`WorldMoveTables`, `WorldMoves.MoveDescriptions`), así que los movimientos del mod de gen 8-9
traen la suya. El juego parte las líneas para que quepan en su pantalla, y aquí se leen **como un solo párrafo**
(`WorldMoveTables.Flatten`, con prueba). Se enseña en dos sitios: **debajo de la fila elegida** de lo que puede recordar,
en una cápsula blanca como la línea de abajo de la bolsa, y **debajo de los cuatro que sabe** la del que se va a olvidar,
con su nombre delante, para no olvidar a ciegas. Los demás no la enseñan: con nueve filas abiertas la lista sería un
muro de texto. Sin mundo instalado no hay descripción —PKHeX no lleva ninguna— y la cápsula no sale. Visto con Gholdengo
(Metaláser y Dracoflechas) en MUY GRANDE y en NORMAL.

Trampa de WPF de paso: **un `ContentPresenter` toma su contenido como contexto de datos**, así que un enlace puesto en el
propio `ContentPresenter` —aquí su visibilidad, `SelectedKnown.HasDescription`— se resuelve contra el contenido y no
contra la pantalla, falla en silencio y cae en su valor de reserva. La cápsula de lo que se olvida no salía por eso; va
dentro de un `Grid`, que sí hereda el contexto.

## §145 · Los objetos de evolución clásicos, cada lista en su tienda (2026-09-19)

**La petición.** Tres listas de objetos de evolución clásicos, a **30.000** cada uno, en tres sitios que eligió el
jugador: las once piedras en el Centro Pokémon de la **Ruta 8**, los doce objetos de intercambio en el de la **Ruta 2** y
seis sueltos (Mejora, Escama Bella, Dulce de Nata, Piedra Eterna, Baya Tamate, Saquito Fragante) en **Ciudad
Konikoni**, donde el Centro Pokémon no tiene mostrador especial y va en la tienda donde se pusieron primero los de gen
8-9. Y los de gen 8-9 que coincidieran con uno de esos sitios, a la tienda especial siguiente.

**Qué mostrador es cada sitio.** El cartucho no lo dice, y hasta hoy había tres medidos jugando: 8 Konikoni, 10 Hauoli,
11 Ruta 2. El editor de tiendas de pk3DS para Ultra Sol y Ultra Luna (`MartEditor7UU.cs`) **etiqueta los veinte**, y
acierta en los tres medidos —«Konikoni City [Incenses]», «Hau'oli City [X Items]», «Route 2 [Misc]»—, así que se toman
sus etiquetas para el resto: **14 «Route 8 [Misc]», 24 «Route 3 [X Items]», 15 «Paniola Town [Poké Balls]»**. Una
comprobación que no depende de pk3DS: el 14 tiene **11 huecos**, justo las 11 piedras, y el 11 tiene **12**, justo los
12 de la Ruta 2. Esos tres índices nuevos **no se han visto jugando** y así va dicho.

**Cómo.** `specialMartShelves` en `randomizer.json`: una lista propia por mostrador (`MartShelf`), con su sitio y su
precio. Un mostrador con lista **no entra** en el reparto de `specialMartItems`, así que la lista que se derrama pasa al
siguiente en vez de pisarse. Es más estricta que `specialMartOrder` a propósito: un orden es una preferencia, pero una
lista que no se puede colocar son objetos que nadie puede comprar, así que un mostrador que no existe, uno de MT, una
lista más larga que el mostrador o un objeto en dos listas **paran las tiendas antes de escribir un byte**
(`ShopRandomizer.ValidateShelves`). Lo del objeto repetido no es manía: el precio es un campo **del objeto** y no de la
tienda, y un objeto en dos listas con dos precios acabaría costando el último que se escribiera. Cada id se comprueba
contra la tabla del cartucho antes de escribir (§52): «Escama de Dragón» en el juego es **«Escama Dragón»**, y se puso
con el nombre del juego. Las tiendas de MT sortean igual que antes: una lista propia no gasta números aleatorios.

**Dónde acaban los de gen 8-9.** Estaban en Hauoli (8) y Ruta 2 (10 más dos Poké Balls). Los de Hauoli se quedan; los
diez de la Ruta 2 pasan a la **Ruta 3** (8 huecos) y los dos que no caben a **Pueblo Paniola** (3), con
`specialMartOrder` a `[10, 24, 15]`. Siguen a 50.000.

**Verificado contra los ficheros e instalado.** Generado con la seed y el rol de la run y comparado fichero a fichero con
el mundo instalado: las tiendas normales y las de MT, **idénticas**; en la tabla de objetos cambian **exactamente 29
entradas, dos bytes cada una** (el precio); y la tabla de especies cambia solo en las copias sueltas, por el §141 —la
entera, que es la que lee el juego, sale igual—. Instalado desde la propia aplicación (GENERAR e INSTALAR, que deja su
`RomRandomized`), y lo instalado coincide byte a byte con lo comprobado. **Sin ver todavía dentro del juego.** Para el
jugador hay un resumen en `Tiendas especiales.txt` en su escritorio. 15 pruebas nuevas (`SpecialMartShelfTests`).

Ojo con una consecuencia del precio: al ser del objeto, **venderlos da 15.000**, y eso incluye lo que se encuentra por el
suelo y las Bayas Tamate, que se pueden recoger. Es la misma decisión que ya se tomó con los de gen 8-9.

**Corrección del mismo día: el 24 no es la Ruta 3.** El jugador lo miró: en la Ruta 3 **no hay Centro Pokémon**, así
que la etiqueta «Route 3 [X Items]» de pk3DS está mal y el 24 **no se sabe dónde está**. Es la primera etiqueta de pk3DS
que falla; las demás que se han mirado (8, 10, 11, 14, 15) cuadran. El 24 vuelve a las Poké Balls y los ocho que tenía
van al siguiente mostrador especial que sí existe: **Pueblo Paniola (15)**, que el jugador dio por bueno, y el
**Supermercado Ultraganga** de la Avenida Royal, el mostrador de la izquierda (21, 5 huecos) y el del centro (22, 7).
Esos dos son etiquetas de pk3DS con un respaldo propio: el 23, a su lado, vende en el cartucho **Estatuillas Raras**, y
eso solo lo vende ese supermercado. `specialMartOrder` pasa a `[10, 15, 21, 22]`. Sin ver jugando.

**Y la Moneda de Gimmighoul, a 30.** Gholdengo pide **999**, así que a 50.000 cada una la evolución no se podía comprar
nunca. `MartItem` gana un `price` propio que manda sobre el de su lista (cero es «el de la lista», nunca «gratis»), y
pasa por la misma comprobación antes de escribir: múltiplo de 10 y como mucho 655.350. Releído del fichero generado:
la moneda a 30, el resto de gen 8-9 a 50.000 y los clásicos a 30.000. Instalado otra vez desde la app y comparado byte a
byte.

## §146 · La escena de los iniciales nombra a los de verdad (2026-09-19)

**La petición.** Que al elegir inicial el cuadro de texto diga el Pokémon randomizado y no Rowlet, Litten o Popplio. No
contradice el §66, donde el jugador pidió que la **aplicación** no enseñara los iniciales antes de tiempo: esto es el
propio juego diciendo qué hay en la Poké Ball en el momento de elegirla, como en un juego sin randomizar.

**Dónde está.** En el **texto de historia** en español, `a/0/4/6` (pk3DS lo llama `storytext`, 040), que el mod de gen
8-9 no trae, así que sale del cartucho. Buscando los tres nombres salen **21 líneas en tres ficheros**: el 38 (Kukui los
presenta), el 39 (descripción, confirmación y nombre sueltos) y el 51 (el menú, la confirmación y «te está mirando
fijamente»). Los nombres van escritos tal cual. La confirmación usa una variable, `[VAR 0101(0001)]`, y se sustituye por
el nombre escrito, que es correcto valga lo que valga la variable. Las líneas compartidas («¡Has elegido a [VAR
0101(0000)]!») no se tocan: no dicen qué inicial es. Tampoco los gritos («¡Rooow!»), que son del Pokémon en pantalla, y
no está comprobado si el juego enseña el modelo del cartucho o el randomizado. Los textos de la personalidad
(«te pareces a Rowlet») hablan de la especie en general y se dejan.

**Qué dice ahora.** El nombre y **todos** los tipos de la especie que hay en la tabla de regalos del mundo final,
con la forma regional si la tiene: «Ese es Quaxly, un Pokémon de tipo Agua», «¿Te decantas por Machop, el Pokémon de tipo
Lucha?». Las tres descripciones hablaban de Rowlet volando y Popplio haciendo globos, así que se sustituyen enteras por
«¡Quaxly es un Pokémon de tipo Agua!». Una línea que pasaría de 44 letras se parte antes de «un Pokémon»; las del juego
llegan a 48. **El menú no está medido**: nadie sabe lo ancho que es, así que con dos tipos se abrevia a «Bulbasaur,
Planta/Veneno».

**Cómo se escribe, y por qué no con pk3DS.** pk3DS reconstruye un fichero de texto, pero no igual: medido sobre este
texto, cuenta el relleno dentro de la longitud de cada línea, pone a cero el campo que sigue —el cartucho lo tiene a 4
en algunas líneas, sin que se sepa para qué— y recorta espacios. **1005 de los 1124 ficheros** salen distintos y **cuatro
pierden caracteres**. Así que `GameTextPatch` codifica con pk3DS solo las líneas nuevas, les deja su campo, calcula la
longitud como el cartucho (hasta el terminador, saltando las variables, porque una variable puede llevar un cero), y copia
el resto cifrado tal cual. Sin cambios, la salida es la entrada byte a byte.

**Guardias.** Cada fichero tiene que dar exactamente las líneas medidas (3, 9 y 9); si no, **no se escribe nada**. Y
antes de escribir se relee: los tres ficheros tienen que decir lo que se quería y los otros **1121 tienen que ser byte a
byte los del cartucho**. Las dos guardias saltaron en el desarrollo, y las dos con razón. La primera porque la
configuración de la ROM escribe la variable por su nombre, `[VAR PKNAME(0001)]`, y no por su número. La segunda porque
pk3DS **lee** el salto de línea del juego como dos letras, `\n`, y las reglas buscaban un salto de verdad. Al escribir da
igual, los dos son la palabra 0x000A, pero al leer no: las tres descripciones no se reconocían y habrían dicho «¡Jangmo-o
es capaz de volar…». La relectura compara texto y lo paró. De paso salió un fallo de orden: el texto se guardaba en la
carpeta del mod **antes** de releerlo. Ahora se relee primero.

**Interruptor:** `starterText` en `randomizer.json`, encendido. Es el **último** paso del randomizador, para nombrar los
iniciales que quedan y con los tipos del mundo final, y no gasta números aleatorios, así que el resto del mundo sale
igual. 17 pruebas (`StarterTextTests`).

**Verificado contra los ficheros e instalado, sin ver en el juego.** Con la seed de la run anterior se comprobó fichero a
fichero contra lo instalado: todo igual salvo `a/0/4/6`, que es nuevo, con 3 ficheros de texto distintos de 1124. Al
instalar por la app se vio que el jugador **había empezado de cero a las 18:10** (run nueva, rol EXPERTO) y que Azahar
seguía con el mundo de la run borrada. No había partida guardada, así que se instaló el de la run nueva, que es lo que
tocaba antes de jugar. Releído: Quaxly, Froakie y Machop (912, 656 y 66 en la tabla de regalos), y el texto lo dice en
las 21 líneas.

## §147 · Las Poké Balls de la run anterior aparecieron en la partida nueva (2026-09-19)

**Lo que vio el jugador.** Empezó de cero (18:10: run y partida borradas), cogió al inicial y en la Ruta 1 tenía en la
mochila **doce tipos de ball de la partida anterior**, entre ellas una **Master Ball**. Y cuatro segundos después le llegó
el premio «Refuerzo de Poké Balls» (10 Super Balls y una tirada gratis), que espera a que lleves alguna Poké Ball: la que
llevaba era una de las heredadas.

**La causa.** La regla de primer encuentro quita las balls en una ruta gastada y las devuelve al salir, y lo que debe lo
apunta en `Saves/backup/objetos-retirados.txt` **antes** de tocar la mochila, para que un cierre a medias no se las coma.
Ese fichero **no decía de qué run era**. «Empezar de cero» borra la run y la partida, pero no ese fichero, así que la
partida nueva conectó en la Ruta 1, la regla dijo «la ruta conserva su encuentro, devuelve», y devolvió lo que se le
debía a la run anterior. Consta en el historial de la run nueva como `BallsReturned` con los doce tipos (1 Master Ball,
13 Super, 3 Poké, 11 Buceo, 10 Nido, 1 Turno, 6 Lujo, 11 Ocaso, 1 Sana, 9 Veloz, 1 Peso y 3 Ente) y el `RewardClaimed`
justo detrás.

**El arreglo.** Lo que se debe **pertenece a la run que lo quitó**. `WithheldLedger` escribe `run=<id>` en la primera
línea y solo contesta a esa run. `IItemWithholder` pide la run en `Owed`, `Withhold` y `GiveBack`, y `BallControlService`
se la da. Un fichero de otra run —o uno anterior a esta línea, que no sabe de quién es— **no le debe nada a nadie**. Y no
se tira: la primera vez que la run actual escribe, se aparta con fecha a `objetos-retirados-de-otra-run-<fecha>.txt`, por
si resultara que sí se debía. Pruebas: cinco del registro sobre ficheros temporales y una de la regla con dos runs. La
prueba de «devolver encima de lo cogido mientras tanto» usaba **una run distinta en cada llamada** y pasaba porque el
registro no las distinguía; ahora usa una sola, que era lo que quería comprobar.

**Lo que no arregla.** Una partida nueva empezada **dentro del juego** sin pasar por «empezar de cero» sigue siendo la
misma run para PermaLocke. Distinguirla exigiría atar el registro también al entrenador de la partida (su identificador),
y eso no está hecho.

## §148 · Cinco copias para los amigos, dentro de la carpeta de la competición (2026-09-19)

A petición del jugador, cinco carpetas `2` a `6` en `G:\Mi unidad\PermaLocke Competición`, cada una con la salida de
`tools\publicar.ps1` (exe autocontenido, `Data\`, `Emulator\`, las fotos de zona y `LICENSE`) **sin los tres LEEME**
—el jugador lo explica él— y con `Config\sync.json` a `".."`. Se repasó antes de copiar que el emulador no llevara una
carpeta `user\` ni nada del jugador.

**Por qué `..`.** La carpeta compartida se guardaba como ruta absoluta, y en el PC de cada amigo la unidad y la ruta de
su Drive son otras. `SharedFolderSettings.Read` resuelve ahora una ruta **relativa contra la carpeta de la aplicación**,
no contra el directorio de arranque, que depende de cómo se abra un acceso directo. Así cada copia encuentra la
competición subiendo una carpeta. Tres pruebas. **Consecuencia:** cada amigo necesita acceso a la carpeta de la
competición entera, no solo a la suya.

**Lo que no va, como en cualquier reparto:** la ROM (cada uno la suya en `ROM\`) y el mod de gen 8-9 (`Expansion\`).
Sin el mod, su mundo se randomiza sobre el cartucho, con 807 especies.

**Visto:** una copia idéntica metida en una carpeta de competición de prueba, fuera del Drive, arranca con otro
directorio de trabajo, toma su propia carpeta como raíz y su propio Azahar portátil, sin errores ni avisos. Las cinco
del Drive no se han abierto, para no crearles un perfil de jugador que no es suyo.

**Y el mod de gen 8-9, una sola copia para los cinco (mismo día).** El jugador pidió que las copias llevaran el mod
«como lo suyo», dejando a mano solo la ROM. La ROM no trae gen 8-9, eso es del mod. Y **no cabe**: el Drive del jugador
tiene 15 GB con **893 MB libres**, y el mod ocupa 2,4 GB, casi todo el fichero de modelos `a/0/9/4`. Cinco copias serían
12 GB. Así que `AppPaths` usa ahora la `Expansion` de la carpeta de encima cuando la suya no tiene `romfs`: una copia del
mod junto a las carpetas `2` a `6` sirve a todas. Se mira el `romfs` y no la carpeta, porque cada copia crea al arrancar
una `Expansion` vacía, y esa no puede tapar la compartida. Cuatro pruebas. **Sin aplicar todavía en el Drive:** hace falta
espacio para el mod y para cambiar el `.exe` de las cinco copias, que siguen siendo las de antes, sin este cambio.

**Aplicado el mismo día, con una copia por carpeta.** El jugador amplió su Drive a 100 GB y pidió el mod en cada
carpeta, como el suyo. Copiada su `Expansion` entera (2.463 MB) a `2` a `6`, y comprobado con la suma de todos los
ficheros: las cinco idénticas a la suya. Va con el `README.txt` del propio mod, que no es una explicación de PermaLocke:
es su licencia (**CC BY-NC-ND 4.0**), que deja compartirlo sin ánimo de lucro **conservando los créditos y el enlace
oficial**, y quitarlo lo incumpliría. Solo la ROM queda a mano. La vuelta a la `Expansion` de la carpeta de encima sigue
en el código, pero con una copia por carpeta no hace falta, y las cinco llevan todavía el `.exe` de antes de ese cambio.

## §149 · Las rutas cuentan desde la primera Poké Ball (2026-09-19)

El jugador, en una partida de prueba, gastó la Ruta 1 cruzando la hierba que la historia obliga a cruzar antes de que
nadie le dé una Poké Ball. Ahora un combate salvaje **no gasta la ruta ni marca el MAPA** mientras la run no haya
tenido ninguna ball. `BallControlService.HasHadBallsAsync` mira la mochila, y la primera vez que ve una ball (llevada, o
retenida por esta run) escribe **`FirstPokeBallSeen`**. Desde ahí cuenta siempre, aunque luego se tiren todas: la
mochila se vacía, pero haberlas tenido no se deshace. Si la mochila no se puede leer, contesta que sí, o sea, la regla
de siempre, en vez de dejar rutas libres por una lectura fallida. `EncounterGuard` lo pregunta antes de gastar y antes
de marcar. Una prueba. **La Ruta 1 de la partida de prueba se queda gastada**, como pidió el jugador. Aplicado a su app
y a las cuatro carpetas que quedan en el Drive, ya con nombre (Guille, Juan, Juanega y Tosi). Sin jugar todavía.

## §150 · Capturas y muertes, también desde la primera Poké Ball (2026-09-19)

Mismo interruptor que el §149: hasta que la run tiene su primera Poké Ball (`FirstPokeBallSeen`), `GameLinkMonitor` no
registra capturas ni muertes del equipo, y `RecordDeathOnceAsync` tampoco cuenta las del combate. Aplicado a la app del
jugador y a las cinco carpetas del Drive. Sin prueba propia ni visto en el juego, por falta de tiempo esa noche.

## §151 · Revisión entera del mundo instalado, y lo que salió (2026-09-21)

El jugador pidió dejar de descubrir fallos de uno en uno: antes de hacer la carpeta de los amigos, comparar **todo** lo
que el randomizador cambia contra el juego sin tocar. Se hizo sobre el mundo instalado (rol EXPERTO), módulo a módulo,
con una herramienta de un solo uso que lee la capa base (cartucho + mod de gen 8-9) y el mod instalado.

**Lo que está como debe:** datos de Pokémon (estadísticas y tipos sin mover, habilidades asignables, filas sueltas
iguales a la entera, §141); estáticos al +27% del rol y regalos intactos; entrenadores (137 combates importantes con el
extra del rol recortado a seis, megas desde la 6ª prueba, y la dificultad del §122: **0** objetos del cartucho
sustituidos, 143 añadidos, 25,1% con objeto, **0** EV con otro total, **0** por encima de 252, **0** IV que bajen);
iniciales y las 21 líneas de su escena, sin un Rowlet/Litten/Popplio suelto; salvajes (45.848 huecos, ninguno fuera
de rango ni prohibido, formas regionales válidas, **todos** dentro del ±15% de fuerza); objetos del suelo (los mismos
538 del cartucho barajados, las 45 MT siguen siendo MT, ningún cristal Z en el saco); tiendas (las tres listas del
§145 en su mostrador, el derrame en el orden de `specialMartOrder`, precios leídos de la tabla de objetos iguales a
los pedidos, curativos fuera de las ocho normales); y en `code.bin`, las mismas 100 MT y 67 tutores reordenados y
**ni un byte** cambiado fuera de esas dos tablas.

**Legendarios de gen 8-9 en la hierba.** `bannedSpecies` se escribió antes del mod y acaba en el 807: salían Meltan en
105 huecos, Kubfu en 60, Terapagos en 41, Calyrex en 38, y así hasta 22 especies. El gacha ya los trataba como
legendarios —`Data/species.json` marca exactamente esos 26 por encima del 807—, así que era un olvido. El jugador pidió
fuera de salvajes **todos** los legendarios y el resto como estaba, así que es una lista aparte, `wildBannedSpecies`,
que estrecha solo el saco de salvajes (`WildEncounterRandomizer.PoolFor`); entrenadores y estáticos pueden seguir
llevándolos. Con la lista vacía el saco es el mismo objeto, así que un mundo generado sin ella sale idéntico. Un test
falla si algún legendario de `species.json` no está en una de las dos listas, comprobado quitando la lista. Ojo con lo
que eso deja: un estático que en el cartucho era un Pokémon normal (Leavanny, Comfey) puede salir legendario.

**Seis evoluciones imposibles.** Las del mod que piden saber un movimiento no estaban en `MoveLevels`, y con
`randomizeLearnsets` quedaban sin forma de conseguirse. Cinco siguen la regla de las nueve del cartucho —el nivel al
que la preevolución aprende el movimiento en la capa base, leído de `a/0/1/3`—: Annihilape 35, Farigiraf 32, Wyrdeer
21, Grapploct 35 y Overqwil 28 (fila de forma 1100, Qwilfish de Hisui). Dipplin aprende Bramido Dragón solo a nivel 1,
como Piloswine y Poipole, y el jugador lo puso al 45 como ellos. La lista completa para jugadores está en
`EVOLUCIONES CAMBIADAS.txt`, generada pasando la tabla sin randomizar por el propio `ImpossibleEvolutionFixer`.

Verificado generando un mundo con otra semilla: **ningún** legendario de gen 8-9 en salvajes, siguen en entrenadores
y estáticos, y «no queda ninguna que exija otro jugador» con **15** por movimiento pasadas a nivel. **El mundo
instalado no cambia hasta que se vuelva a generar**, y regenerar cambia todas las rutas.

**Cosas menores:**
- `RomTool trainers` decía «objetos alterados … (debe ser 0)» y «EV alterados … (debe ser 0)», y daba cientos con un
  mundo correcto, porque el §122 los cambia a propósito. Ahora mide sus promesas: objetos del cartucho cambiados o
  quitados, EV con otro total y EV por encima de 252.
- `GarcPatcher.ReadOnly` y `ReadAllReadOnly` devuelven los bytes **tal como están guardados**; `LazyGARC`
  descomprime LZ11 sin avisar y ellos no. Sobre `a/0/8/3` la revisión encontró cero huecos salvajes y nada falló.
  Ningún llamador de hoy lee un contenedor comprimido; queda escrito en el código.
- El §46 decía que el nivel del rol se redondea **hacia arriba**, y el código redondea al más cercano. Manda el
  código: 12 × 1,2 = 14,4 da 14, que es el cap de la 1ª prueba; hacia arriba saldría 15.

## §152 · Afueras de Hauoli en cada combate de la Ruta 1, y el cierre al poner el nombre (2026-09-21)

Dos cosas que el jugador vio empezando partida nueva, y las dos salieron del registro de la app y del de Azahar sin
tocar su partida.

**Todos los combates de la Ruta 1 caían en «Ruta 1 (Afueras de Hauoli)».** El juego apunta en el propio Pokémon
dónde se encontró, y el Vulpix decía «Ruta 1»; la app decía Afueras. El diagnóstico que dejó el §151 en el lector
—cada registro con su mapa y su posición cuando no se ponen de acuerdo— lo contó: memoria llena de transformaciones en
**(1, 0, 0)** con la rotación identidad y `FFFFFFFF` detrás, que pasaban todos los filtros del §117 y, como sus cuatro
primeros bytes son cero, se leían como **mundo 0, mapa 0**: Afueras de Hauoli. Salen a montones de la búsqueda de
hermanas del mapa 0, cuyo patrón son cuatro ceros. Fuera de combate se perdían entre los buenos, pero **al empezar un
combate los registros reales pasan al mapa del combate** y dejan de validar (§117), y durante esos segundos solo
votaban ellos: el combate se colocaba en las afueras. **45** veces en los registros, siempre exactamente (1, 0, 0).
`FieldRecord.Parse` rechaza ya una posición dentro del cubo unidad del origen; la posición real más pequeña registrada
es (2000, 3.94, 6299), en Senda Mahalo. Es la misma familia que los (1, 1, 1) que el §117 ya descartaba.

Y quitada la basura quedaba un empate de verdad. El §117 midió que todos los registros cambian de mapa a la vez, y eso
vale **al cruzar una puerta o volar**; andando por un borde sin pantalla de carga no: de las afueras a la Ruta 1,
`0x33F6E4C8` cambiaba de posición en cada lectura y pasó al mapa 3, y `0x33F6E510` se quedó en el mapa 0 y en un punto
fijo, por donde se entró. Uno contra uno, `Resolve` no decide nada, y una ruta así no se marcaría nunca. Ahora
desempata **el registro que se mueve**: el que ha cambiado de posición al menos dos veces en los últimos 5 s, y solo si
todos los que se mueven están en el mismo mapa. Dos veces porque el de la entrada también cambia, una vez, al entrar.
Un combate salvaje empieza andando por la hierba, que es justo cuando esto se cumple; parado no decide nada, y una
lectura sola nunca basta, que es la lección del §55. El registro lo dice: «Zona: … — por el registro que se mueve».

**El cierre de Azahar mientras escribía su nombre.** El registro del emulador se corta a media línea a los 21,8 s,
dentro de la ráfaga de lecturas del barrido completo del equipo, que la app había lanzado a los 19 s: la cuarta vez con
esa firma. Y no había nada que encontrar, porque en la pantalla del nombre no existe ningún equipo. La espera doblada
del §151 no ayuda al **primer** barrido. Ahora, antes de barrer, `AzaharGameStateProvider` pregunta dos cosas:

- **¿Hay partida guardada?** Sin ella el juego nunca se ha guardado: es la intro, o un equipo que la regla de
  encuentros tampoco puede usar todavía (§117). No se barre, y HOME dice «Guarda la partida dentro del juego».
- **¿Están en memoria los Pokémon de esa partida?** Los cuatro primeros bytes de un PK7 guardado son su constante de
  encriptación, en claro, seguidos de un cero: la búsqueda del fork los encuentra en **una** petición por región,
  donde un barrido son unas cien mil. En la pantalla de título o cargando no están, y no se barre. Se pregunta como
  mucho cada 20 s.

Con un Azahar que no es el fork la búsqueda contesta vacía siempre, lo que se leería como «no está» para siempre; ahí
la comprobación se aparta y se barre como antes. Un barrido pedido porque la estructura se movió (§135) no pregunta:
el equipo se acaba de leer.

**De paso**, MANTENIMIENTO lanzaba «Falló la auditoría de la run» cada vez que se borraba o creaba una run: el aviso de
run cambiada llega desde el hilo que la cambió y la auditoría rellenaba una lista de la pantalla desde allí. Ahora pasa
por el hilo de la ventana, como ya hacían HOME, CEMENTERIO y COMPETICIÓN. ESTADÍSTICAS tenía el mismo fallo sin
estrenar y queda igual.

**Ojo con el registro del emulador del jugador**: tiene `RPC_Server:Info` y `Service.FS:Trace`, así que apunta cada
petición de la app —doscientas mil líneas por barrido— y llegó a rotar a los 100 MB. Se dejó así en alguna prueba. No
se ha tocado porque es la configuración de su emulador; la carpeta de los amigos lleva una configuración nueva sin
ese filtro.

**Sin ver todavía en el juego**: ni el desempate andando por la Ruta 1 ni el primer arranque sin barrido. Todo con
pruebas que fallan sin el arreglo, comprobado quitándolo.

### Y dos cosas que salieron preparando la carpeta de los amigos (mismo día)

**Con solo el inicial no se encontraba el equipo, y sin equipo no funcionaba nada.** El barrido confirma un equipo
encontrando un **segundo** Pokémon a un salto de distancia (`DetectStride`), así que quien lleva solo su inicial no
existe para él. En el PC del desarrollador nunca se vio porque la dirección de la última vez se revalida sin barrer;
en una instalación nueva no hay dirección de la última vez, y todo lo demás cuelga de tener equipo: la regla del
primer encuentro, los avisos, la ruta. Es la explicación más probable de «no salían las notis» en la carpeta anterior.

Ahora la comprobación de arriba **ya no pregunta, localiza**: `PartyLayoutLocator.LocateByKeys` busca las constantes
de encriptación de la partida guardada, vuelve atrás desde cada coincidencia mientras la entrada anterior siga siendo
un Pokémon —así da igual qué miembro se encontró— y **mide** qué salto tiene cada inicio, porque con un solo Pokémon no
hay segunda entrada que lo diga: busca las estadísticas donde las guarda cada estructura (detrás del bloque en las
copias, en 0x158 en la que lee el juego) y acepta solo donde `PartyStats.AreHere` lo confirma. Una vista con el salto
equivocado lee su cola de otra cosa y no pasa. Con partida guardada **no hay barrido**: el equipo sale en unas pocas
búsquedas. El barrido queda para un Azahar que no es el fork y para cuando la estructura que lee el juego se ha movido
y la partida guardada no la alcanza (§135). Probado con Pokémon de verdad en la memoria de un emulador falso: un inicial
solo en la estructura del juego, una coincidencia en el tercer hueco que vuelve al primero, y una instalación nueva que
se conecta sin un solo barrido.

**El mundo instalado se leía una vez, al arrancar.** Un amigo crea la run, genera e instala con la aplicación abierta, y
se quedaba en las 807 especies del cartucho hasta reiniciar: un inicial de gen 8-9 no era una especie que los lectores
conocieran. Reinstalar tenía la versión suave: los aprendizajes del mundo anterior en MOVIMIENTOS y sus capturas
estáticas permitidas en la regla de las balls. Ahora RANDOMIZADOR relee el mundo al instalar y al quitar
(`InstalledWorld.Apply`, y `Forget` en `WorldAllowedStatics` y `WorldEvolutionLines`).

## §153 · La primera partida en la carpeta de prueba: balls perdidas, la escuela sin ruta y un combate de otra run (2026-09-21)

El jugador jugó en `PermaLocke prueba` con la versión del §152 y contó tres cosas: en su casa le «devolvieron» las
Poké Balls y en realidad se las quitaron, en la hierba de Afueras de Hauoli no contaba nada, y en la Escuela de
Entrenadores dejaron de salir avisos y no le daban las balls. Salió todo del registro de esa carpeta. Lo bueno primero:
el equipo se encontró **por la partida guardada, sin barrer**, y la Ruta 1 se marcó bien la primera vez.

**Las balls se perdían, y era mío (el arreglo de la deuda doble del mismo día).** `DebtWasUndoneByAReload` decía «la
mochila tiene lo mismo que la partida guardada, luego ha recargado» — y con **cero y cero** eso no demuestra nada: es
justo la mochila recién retirada cuando las balls se recogieron después del último guardado. Así, en la casa (una zona
libre) devolvió las Super Balls pero **perdonó** la deuda de las diez Poké Balls, y en la hierba de Afueras retiró las
Super Balls para el primer encuentro y al ver que era un Bonsly las **perdonó** en vez de devolverlas: sin balls no se
pudo capturar y el mapa marcó huida. El mismo aviso («la retirada anterior nunca llegó a guardarse») está en el
registro de la partida principal esa mañana. Una recarga solo se ve cuando **trae balls de vuelta**, así que ahora hace
falta que la mochila tenga alguna. El único caso que eso acierta peor —recargar una partida de antes de recibir las
balls y que la historia las vuelva a dar— regala diez; perderlas para siempre para la partida.

**En la Escuela no había ruta con todos los registros de acuerdo.** Seis registros del mapa de la escuela (mundo 27,
mapa 46) y seis del de fuera (mundo 0, mapa 0), los doce de «Ruta 1 (Afueras de Hauoli)». `FieldRecord.Resolve` pedía
mayoría **de mapa**, seis contra seis no decide, y la ruta quedaba sin identificar. Las reglas trabajan **por zona**, así
que sin mayoría de mapa se busca mayoría de zona, con la misma exigencia: dos como poco y más de la mitad. Lo mismo pasa
dentro de la casa del jugador, que son dos mapas de la misma zona.

**Un combate de la run anterior terminó en la nueva.** Azahar se cerró a mitad de un combate, se empezó de cero, y cuatro
minutos después `EncounterGuard` acabó aquel combate en la run nueva y avisó de «Mapa sin marcar». No marcó nada porque
justo no se leían los contadores; si se hubieran leído, habría marcado una ruta de la run nueva con un combate de la
vieja. Además la cuenta de combates de la partida vieja seguía de referencia, y una partida nueva empieza de cero: hasta
superarla no se veía empezar ningún combate. Ahora se tira todo al cambiar de run, y también cuando el contador de
combates salvajes **baja**, que jugando no pasa nunca: es que se ha recargado o se ha abierto otra partida. **Sin prueba
automática**: `EncounterGuard` depende de cuatro piezas concretas que hablan con el emulador y montarle un banco de
pruebas es un trabajo aparte.

Las otras dos, con pruebas que fallan sin el arreglo, comprobado quitándolo. La partida de prueba del jugador **no se
toca**: las balls perdonadas no vuelven solas porque la deuda ya se borró, así que lo limpio es empezar de cero en esa
carpeta.

**Y los puntos del MAPA contra el lector (mismo día).** Cruzados `Data/marcadores.json`, `Data/mapas.json` y los
encuentros del cartucho (`encdata` con `zonedata` y `worlddata`): **los 61 marcadores tienen al menos un mapa** que el
lector traduce a su id, y **ninguna zona con encuentros se queda sin clasificar** —o es un marcador o está en
`sinEncuentros`—. El jugador confirmó que el MAPA es la lista de la competición tal cual, así que lo que el cartucho
tiene fuera de él (Colina del Recuerdo, Prado de Poni, Túnel del Volcán, Pueblo Ohana, los árboles de bayas del Huerto y
de la Playa de Ula-Ula, Ruinas de la Cosecha) se queda **sin capturas a propósito**. Tres marcadores caen donde el
cartucho no tiene ningún salvaje —Ciudad Konikoni, Cueva Sotobosque (Sala del Dominante) y Playa de Poni— y nunca se
rellenarán; dicho, sin cambiar nada.

## §154 · El cap de nivel bajaba la experiencia y dejaba el nivel de la pantalla (2026-09-21)

El jugador: «antes, si subía uno a 15 con cap 14, volvía atrás, abría el menú y estaba a 14; eso dejó de ir». El registro
de la carpeta de prueba lo tenía dicho: «Froakie estaba a nivel 15, cap 14. Corregido y releído en 1 copias», con
**4 bytes** escritos. La escritura entraba, se releía, y en el menú seguía a 15.

La causa es de orden: `EnforceLevelCap` se escribió antes de que el §99 encontrase dónde guarda el juego las
estadísticas de la estructura que lee, **28 bytes en `0x1E4 + 0x158`**, y esa cola es donde vive el **nivel que enseña
el menú** (`Stat_Level`). El cap leía la entrada contigua, veía que la cola no eran estadísticas —en esa estructura no lo
son— y con buen criterio (§53) solo tocaba el bloque cifrado: bajaba la **experiencia**. Releía la experiencia, veía 14 y
lo daba por hecho. El aviso de HOME ya lo delataba: «entra y sal de un combate para verlo».

Ahora, si esa entrada es la estructura que lee el juego —medido **antes** de escribir con `PartyStats.AreHere` sobre
`ReadAuthoritative`, porque después el nivel de la cola ya no coincide con el de la experiencia—, además de la
experiencia se escribe la cola de `0x158`: `Stat_Level` al cap y las seis estadísticas **recalculadas** con
`StatCalculator.Restat`, el mismo código que usa ENTRENAR EV, sacado de `SaveEvTrainer` sin cambiar una línea de su
lógica. Bajar solo el número habría dejado un nivel 14 con las estadísticas de nivel 34 a quien se pase del cap con
caramelos. Los PS actuales bajan lo mismo que el máximo y **un caído se queda a cero** (§98). Sin tabla de estadísticas
del mundo instalado no hay número honrado: baja el nivel, las estadísticas se quedan y el registro lo dice. Se escriben
solo los bytes que cambian, con copia previa, y se relee como lo lee el juego antes de darlo por bueno. Las copias con la
cola contigua (el espejo) reciben lo mismo por coherencia, aunque el juego no las lea.

Cinco pruebas con un Froakie de verdad, cifrado, en la memoria de un emulador falso que acepta escrituras: el nivel y
las estadísticas de nivel 14, caramelos hasta 34 sin quedarse sus estadísticas, un caído a cero, otro Pokémon en el
hueco intacto y sin tabla del mundo solo el nivel. **Cuatro fallan con el código de antes**, comprobado. Y como
`WorldLimits` es global, las ocho clases de pruebas que lo tocan pasan a una colección sin paralelismo.

**Sin ver en el juego todavía.** Lo que se sabe del juego es que esa cola es la que lee el menú (77 PS escritos ahí, 77
en pantalla, §99); que el nivel también se lea de ahí es lo que hay que ver.

## §155 · Lo que sale del gacha va al equipo si cabe (2026-09-21)

Pedido por el jugador: «si tiras de gacha y el equipo no está completo, que se añada al equipo». `SaveBoxDelivery`
—la única entrega que usa `IPokemonDelivery`, y solo la usa el gacha, así que el wonder trade y la ruleta no cambian—
mira primero el equipo de la partida guardada: con menos de seis, el Pokémon entra en el primer hueco libre; con seis,
al PC como siempre. Mismas garantías que el PC: juego cerrado, copia de la partida antes de tocarla y relectura del
fichero, ahora comprobando especie, forma, nivel, PID y que llegue a plena salud.

Lo único que un Pokémon de equipo lleva y uno de caja no son las **estadísticas de combate**, y ahí está la trampa: un
Pokémon en caja no las guarda, así que `PokemonBuilder` podía dejar las que calcula PKHeX con **su** tabla; en el equipo
se ven, y esa tabla está mal para todo lo que añadió el mod (§51). Se recalculan con `StatCalculator.Restat`, el mismo
código de ENTRENAR EV y del cap (§154), con las estadísticas base del mundo instalado, y los PS al máximo. **Sin tabla
del mundo no hay estadísticas honradas y va al PC**, como antes. El resultado dice «caja 0» para el equipo —las cajas se
cuentan desde el 1— y el evento `PokemonDelivered` lo escribe como «en el equipo, hueco N».

Cuatro pruebas con partidas de verdad hechas con PKHeX y una tabla del mundo **distinta de la de PKHeX a propósito**,
para cazar que PKHeX recalculase las suyas por detrás al meterlo en el equipo (no lo hace): con hueco entra en el equipo
con las estadísticas del mundo y a plena salud, con seis va al PC, sin tabla va al PC, y se copia la partida antes. La
primera falla con el código de antes, comprobado. **Sin ver en el juego todavía.**

## §156 · Abrir la bolsa te devolvía a la ruta de antes (2026-09-21)

Visto jugando en la carpeta de prueba, dos veces en media hora. En la Escuela de Entrenadores, recién recibidas las
Poké Balls, abrir la bolsa las quitaba «porque Ruta 1 (Afueras de Hauoli) ya gastó su encuentro» y cerrarla las
devolvía. Lo mismo en Ciudad Hauoli: capturado en la Zona Comercial y de vuelta en el Paseo Marítimo, que aún conservaba
su encuentro, abrir la bolsa decía Zona Comercial. No costaba nada —con la bolsa abierta no hay combate—, pero es la
misma avería que puede situar mal un combate, así que se ha mirado entera.

El log de esos momentos lo cuenta. El juego guarda, junto a los registros que siguen al jugador, otros de **dónde
estuvo**: el mapa del que viene (`0x33F6E510`, ya conocido del §152), el sitio donde empezó el último combate y copias
viejas de otras veces —un trío de la escuela seguía diciendo la escuela minutos después de salir—. En minoría no hacen
daño, porque manda la mayoría (§118). Pero **abrir la bolsa apaga los que siguen al jugador** mientras está abierta, y los
que quedan votan solos: en el Paseo Marítimo, cuatro de la Zona Comercial contra ninguno. Es la forma del §152 —allí
votaban unas transformaciones basura al empezar un combate— con registros que sí son de verdad, solo que viejos.

La regla nueva está en `FieldZoneReader.Believable`: **una zona se deja cuando los registros que la decían cambian de
opinión, no cuando se callan.** Para pasar a otro sitio, entre los que ahora lo dicen tiene que haber o uno que dijo el
sitio actual mientras era el actual —los que siguen al jugador, que cambian de mapa con él en una puerta, en un vuelo o
al cruzar un borde— o uno que esté andando (dos movimientos en cinco segundos, el criterio del §152). Uno que ya decía
el otro sitio antes y no se ha movido desde entonces es lo que el jugador dejó atrás. Solo para un cambio de **sitio**:
dos mapas del mismo sitio cambian libremente, porque ninguna regla los distingue. Y solo **mantiene** una zona, nunca
se la inventa: sin mayoría la respuesta sigue siendo «no lo sé».

Mientras se mantiene una zona se busca con la cadencia de cuando no hay acuerdo (cada 20 s), por si el registro que
sigue al jugador no estuviera entre los que se leen, y el log lo dice una vez con todos los registros. De paso protege
también el arranque de un combate: al empezar, los registros vivos pasan al mapa del combate y dejan de valer, y los
viejos podían mover la zona confirmada a otro sitio justo en el segundo en que se sitúa el combate.

El coste, dicho: si la zona adoptada fuese la equivocada —por ejemplo, al conectar PermaLocke con la bolsa ya abierta—
salir de ella exige que el jugador dé un par de pasos, cuando antes bastaba la mayoría. Un combate salvaje empieza
siempre andando, así que no puede situarse mal por eso.

Tres pruebas con los registros en la memoria de un emulador falso: la bolsa abierta en el Paseo (con 30 s de bolsa y
búsqueda incluida) no mueve la zona; una puerta se sigue en el acto y la bolsa detrás de ella no; y registros que nunca
dijeron el sitio actual se llevan al jugador en cuanto uno anda. **Las tres fallan con el código de antes**, comprobado.
**Sin ver en el juego todavía.**

## §157 · Megas solo después de la sexta prueba (2026-09-21)

El jugador llegó a Liam, el primer combate importante, en Ciudad Hauoli, y le salió una mega. No era un fallo:
`megaTrainerMinimumLevel` estaba en **1**, «todos los combates importantes», porque así se había pedido. Lo que pide
ahora es lo contrario: **ninguna mega antes de la sexta prueba**, que es donde el que juega también las desbloquea, y
de ahí en adelante sí.

En este proyecto la sexta prueba es la **Gran Prueba de la Kahuna Mayla** (`prueba-06` de `achievements.json`), un
combate de nivel **28** de cartucho, y el cap de esa etapa es **34**, que es ese 28 subido el 20% de la edición base. «Más
nivel que el cap de la sexta» es por tanto **29 de cartucho**: 29 subido un 20% da 35. El umbral se queda en niveles del
cartucho y no de pantalla a propósito, porque así las megas salen en los **mismos combates con cualquier rol**: con
EXPERTO (+27%) la propia Mayla se ve a 36, que ya pasaría de 34 comparando en pantalla. Es la trampa del §85 al revés, y
la prueba nueva `MegaFloorTests` ata el umbral al cap con la misma cuenta que usa el randomizador, además de fijar que el
combate de Mayla no la lleva. Las dos fallan con el 1 de antes.

Medido con `RomTool importantes`, que ahora marca con el umbral del JSON (antes llevaba un 33 escrito a mano y decía
MEGA donde el randomizador no la ponía) y acepta `--rol` para un mod generado: sobre la capa base quedan fuera Tilo a
25, Francine a 27 y Mayla a 28, y los primeros con mega son Olano a 29 y Tilo a 30. Generado con la semilla 20260921 y
el rol NORMAL: **87 combates con mega y 50 sin ella**, y releyendo el fichero la única forma especial por debajo del
nivel 35 es un Sandshrew de Alola a nivel 6, que es forma regional. Generado también con el umbral viejo y comparado:
cambia **solo** `a/1/0/7`, en 137 entrenadores, y **solo en el hueco de la mega**; ningún otro Pokémon cambia un byte,
porque las megas salen de su propia corriente aleatoria. Así que reinstalar el mundo de una run empezada no mueve nada
más.

**Los estáticos no se tocan**: el Nihilego del Paraíso Æther (Nv 27) sigue siendo mega por su regla de
`staticOverrides`, y es a propósito: el jugador lo quiere así, como única mega antes de la sexta prueba. **Vale al
regenerar e instalar el mundo**; el que está instalado no cambia solo.

## §158 · Los ataques, ordenados de flojo a fuerte (2026-09-21)

El jugador: «casi todos, si no prácticamente todos los ataques que tengo o que me atacan son de mínimo 80 de potencia,
me parece un descontrol». Medido, y tenía razón. Sobre el mundo instalado, contando los cuatro últimos movimientos que un
Pokémon aprende hasta cada nivel —que es lo que llevan un salvaje y un entrenador, porque `trainerMovesFromLearnset`
deja que el juego se los dé—: a nivel 5, **el 59 % de los ataques eran de 80 o más y 89 de cada 100 Pokémon llevaban
alguno**, contra un 5 % y un 8 en el cartucho, y **igual en todos los niveles** hasta el 30. La mediana de potencia era 80
a nivel 1 y a nivel 50.

No es un fallo de código sino de diseño. El sorteo del §101 copió de Universal Pokémon Randomizer la cuota de ataques
de verdad, sin repetidos y el ataque garantizado a nivel 1, y cada hueco sale **del catálogo entero sin mirar su nivel**.
En ese catálogo la mitad de los ataques con daño son de 80 o más. De UPR no se copió la otra mitad: su opción
«reordenar los ataques de daño». Lo mismo hace pk3DS por defecto (`OrderByPower` en su `LearnsetRandomizer`, con un
primer movimiento flojo), y midiendo el mundo de la referencia (BxnnyLocke, solo lectura, con su propia tabla de
movimientos porque también la cambia) sale exactamente esa forma: 12 % a nivel 5, 29 % a nivel 15, 65 % a nivel 30.

Se hicieron las dos maneras y se midieron con la semilla 20260921, y el jugador eligió:

- **Ordenar por potencia** (`learnsetReorderByPower`, **la elegida**): el sorteo de siempre, y después los ataques de cada
  aprendizaje se ordenan de más flojo a más fuerte en los huecos que ya eran de ataque; los de estado no se mueven, y el
  hueco del ataque garantizado de nivel 1 sigue siendo de ataque (el más flojo). **15 % a nivel 5, 32 % a nivel 15, 72 %
  a nivel 30**, casi la referencia.
- **La curva del cartucho** (`learnsetPowerTolerance` por encima de 0, apagada): cada ataque se cambia por otro de
  potencia parecida al que el cartucho tenía en ese hueco, y uno de estado por otro de estado. 5 %, 7 % y 26 %, calcado al
  juego. Se queda hecha y probada por si se quiere más suave.

Para comparar dos ataques se usa la potencia por los golpes, con los de dos a cinco golpes contados como tres
(`MoveFacts.Strength`): contar cinco pondría Recurrente al lado de Hiperrayo. Y `MoveFacts` distingue ya los de daño fijo
—Sísmico, Tinieblas, los de fulminar—, que tienen potencia 0 pero no son de estado.

Comprobado: generando la misma semilla con la opción y sin ella, **solo cambia `a/0/1/3`**, porque los aprendizajes
tienen su propia corriente aleatoria. Seis pruebas nuevas en `LearnsetPlannerTests`: ordenados de flojo a fuerte, los de
estado en su sitio y el mismo conjunto, el garantizado de nivel 1 sigue siendo ataque, y las dos de la curva. La de
ordenar falla sin la ordenación. **Vale al regenerar e instalar el mundo**, y cambia lo que aprenden todos, también los
Pokémon de los entrenadores.

## §159 · Dos cierres de Azahar en tres minutos, y dos PermaLocke a la vez (2026-09-21)

Tras reinstalar el mundo, el emulador se cerró **al entrar** y otra vez **a los tres minutos, moviéndose por el juego**.
Los dos logs del emulador (`Emulator/user/log`, el `.old` y el actual) y el de PermaLocke cuentan tres cosas distintas.

**El primero, con un solo PermaLocke, fue un fallo mío del §152.** Con el juego todavía en el vídeo de inicio (el log
está cargando el CRO `MovieLib`), el equipo leído en `0x330128E4` era el espejo, y `LocateByKeys` buscó las claves de
la partida guardada por `MemorySearch.LiveStateRegions`, que incluía el heap de aplicación `0x08000000-0x0A000000`. En
el arranque esa memoria **no está mapeada**: 28.871 líneas «unmapped ReadBlock» de `0x08420000` a `0x09AB4000` en un
tercio de segundo, con bloques de 65.541 bytes (64 KB más los cinco del patrón de seis), y Azahar murió en la última.
Leer memoria inexistente ya lo había congelado (§114 ter). Y esa zona **no aportaba nada**: en todos los logs de todas
las runs su único acierto fue el 2026-08-18, un «equipo» de un Pokémon con el nombre de entrenador hecho de símbolos,
más el falso positivo de caramelos en `0x081D55B0` del §22. `LiveStateRegions` queda en el heap lineal, que es donde
está todo lo localizado: equipos, mochila, contadores, tablas de combate. Afecta también a los dos barridos (el del
equipo y el de la mochila), que pasan de 96 a 64 MB.

**El segundo fue al cambiar de mapa.** Saliendo de un edificio de Ciudad Hauoli, el emulador descarga y vuelve a cargar
`FieldEffectCommon` (177,38 s y 177,62 s), y **70 ms después** PermaLocke lanzó tres búsquedas de 64 MB y una ráfaga de
lecturas para comprobar los candidatos; el log se corta en mitad de la ráfaga. Un cambio de mapa es justo cuando los
registros de posición no se ponen de acuerdo, así que era justo cuando el lector buscaba: mientras el juego reordenaba
la memoria. Ahora `FieldZoneReader` solo busca si la duda **dura 5 segundos** (`SettleBeforeSearch`): un cambio de mapa
se aclara solo en uno o dos, cuando los registros nuevos coinciden, y lo que dura más ya es un registro viejo o perdido,
que es para lo que está buscar. La primera búsqueda al conectar no espera, porque sin ella no hay nada que leer.

**Y había dos PermaLocke abiertos**, del mismo ejecutable, desde las 20:41:33: todo sale dos veces en el log —dos
«Conectado al juego», dos «Combate salvaje», dos escrituras en la mochila—, y a las 20:44:14 uno situó el mismo combate
en «Ruta 2, ya gastada» y el otro en «Playa Big Wave». Cada búsqueda iba doble contra el mismo emulador. Ahora la
aplicación toma un mutex `Local\PermaLocke.App` al arrancar, antes de tocar la base de datos, y una segunda avisa y se
cierra; `--sin-juego`, la copia de solo lectura para mirar pantallas, queda exenta. En la run quedaron dos pares de
eventos duplicados (el primer encuentro de la Cueva Costera y su resultado): mismo sitio, mismo resultado y 0 puntos,
así que ninguna proyección cambia, y la cadena **sigue entera** —comprobado sobre una copia de la base de datos con
`VerifyChainAsync`—, porque cada escritura lee el último hash dentro de su transacción y SQLite las puso en fila.

Tres pruebas nuevas, y las tres fallan con el código de antes: la búsqueda por la partida guardada no sale del heap
lineal (el emulador falso apunta cada rango buscado), un cambio de mapa no se busca en sus primeros segundos, y uno que
se aclara solo no se busca nunca.

## §160 · Los combates de una prueba no son el encuentro de la ruta (2026-09-21)

El jugador, en la Cueva Sotobosque: la prueba de Liam obliga a vencer a tres salvajes en las madrigueras y después al
Dominante, y **el juego no deja lanzar ni una Poké Ball** hasta tener el cristal Z. PermaLocke tomó el primer combate de
las madrigueras por el encuentro de la zona: gastó «Cueva Sotobosque (Sala de la Prueba)» y la marcó «debilitado» en el
MAPA (log de la carpeta de prueba, 23:18:51 y 23:19:07) sin que el jugador hubiera podido capturar nada.

La regla nueva, en `ballControl.trialZones` de `Data/rules.json` y `TrialZoneService`: en una zona de prueba, **mientras
el cristal Z de su prueba no esté en la mochila**, un combate salvaje **no gasta la ruta ni marca el MAPA, salvo que acabe
en captura**. La captura cuenta siempre, para que la prueba no sirva de captura de regalo. El gasto se deja para el final
del combate (`WildBattle.Trial`), que es cuando se sabe si hubo captura. El cristal no se escribe otra vez: sale del logro
de la prueba en `achievements.json`, el mismo ancla con el que se cuenta la prueba (§40, §43). Una mochila que no se lee
responde «no se sabe» (§68), y entonces el combate cuenta como siempre.

**Qué zonas, y por qué solo esas.** Las dos salas de la Cueva Sotobosque (madrigueras y Dominante) y la sala del
Dominante de la Colina Saltagua: salas que existen para la prueba, donde antes de superarla no hay nada que atrapar, así
que la regla no abre ninguna puerta. Las demás pruebas o no cuentan (la sala del Dominante de Wela y el observatorio de
Hokulani están en `sinEncuentros`) o pelean a su Dominante **dentro de una ruta normal**: Jungla Umbría, el Súper
Ultraganga abandonado y el Cañón de Poni. Ahí la misma regla dejaría descartar encuentros antes de la prueba huyendo, y
además **no está medido** si un combate contra un Dominante sube el contador de combates salvajes (§119). El del jugador
en la Cueva Sotobosque lo va a medir: si el log dice «Combate de prueba en Cueva Sotobosque (Sala del Dominante)», sí lo
sube, y esas tres zonas necesitan una regla por especie (reconocer al Dominante por la fila que el mundo instalado puso
en su tabla de estáticos, como `allowedStatics`), no por zona.

La zona ya gastada de la run del jugador **no se toca desde aquí**: se libera en MANTENIMIENTO → LIBERAR LA ZONA, que
añade su `ZoneCleared` y no borra nada (§67). Cinco pruebas en `TrialZoneTests`, incluida una que carga los ficheros
reales y exige que cada zona exista en `mapas.json` y que cada logro tenga un cristal Z de tipo. **El paso del vigilante
de encuentros no tiene banco de pruebas** —nunca lo ha tenido— y queda sin ver en el juego.

**Y el mismo día, la sala del Dominante sale del MAPA.** El jugador: «dejé marcado en el mapa la sala del dominante,
cuando ahí no hay para atrapar». `cueva-sotobosque-sala-del-dominante` pasa de los marcadores a `sinEncuentros` de
`Data/marcadores.json`, y con eso deja de ser ruta: ahí no se gasta nada nunca, y su entrada en `trialZones` sobraba y
se quita. La Sala de la Prueba que el combate de las madrigueras había gastado en la run de la carpeta de prueba se
liberó **a petición del jugador**, por el mismo camino que MANTENIMIENTO → LIBERAR LA ZONA
(`ZoneOutcomeService.ClearAsync`): un `ZoneCleared` añadido, nada borrado, con copia previa de la base de datos.

## §161 · Te curaban y te cobraban otro equipo caído (2026-09-21)

El jugador: «cuando se mueren todos los pokémon cuenta el wipeo, pero cuando te lleva a curarlos, la app te los mata
otra vez, que es lo que tiene que hacer, pero te vuelve a contar otro wipeo más». En la run de la carpeta de prueba:
seis muertes y **dos `TeamWiped` a 23:50:25 y 23:50:31**, seis segundos entre uno y otro, −200 cada uno con el rol
EXPERTO.

`GameWatcher.CheckWipeAsync` cobra en el **flanco**: cuando el equipo pasa de tener a alguien en pie a no tener a nadie
(§36). «En pie» eran los PS y nada más. Tras un equipo caído el juego lleva al Centro Pokémon y **cura a todos**;
PermaLocke devuelve al suelo a los caídos una vez por segundo (§99 bis), como tiene que hacer, y ese ir y volver era un
flanco nuevo. Ahora **en pie es con PS y vivo en la run**: un Pokémon que el historial da por muerto (`FallenPidsAsync`,
por PID) no se levanta porque el juego le dé PS. El historial dice quién ha muerto; los PS solo dicen dónde está ahora.

Y la misma avería por el otro lado: el estado del flanco vivía solo en memoria y **arrancaba en «había alguien en
pie»**, así que abrir PermaLocke después de perder, con todos a cero, lo cobraba otra vez a las tres lecturas. El
comentario del campo decía lo contrario de lo que hacía. Ahora arranca, por run, del historial: si el último equipo caído
es más nuevo que la última muerte, nadie ha caído desde entonces y sigue siendo aquel desastre. Las muertes de un equipo
caído se apuntan antes que él (`GameLinkMonitor` las mira primero), así que siempre va detrás de las suyas.

Para lo ya cobrado hay una corrección nueva, `WipeRevoked`, con la forma de `DeathRevoked`: el `TeamWiped` se queda, el
evento nuevo dice que era erróneo, quién y por qué, **devuelve exactamente lo que ese equipo caído quitó** y lo descuenta
de los cuatro que cuentan (`PenaltyService.RevokeWipeAsync` y `CountWipesAsync`). Se niega con un id que no es un
equipo caído de la run o con uno ya revocado. Va al final del enum porque el tipo se guarda como número. No tiene botón
todavía: el único caso es el de la carpeta de prueba.

Cinco pruebas nuevas en `TeamWipeTests`: curado en el Centro y devuelto al suelo no es otro equipo caído, abrir la app
después de uno tampoco, alguien vivo que cae después sí lo es, y revocar devuelve los puntos, deja de contar y no se
repite. Las dos primeras fallan con el código de antes; la tercera pasa con los dos, que es lo que tiene que hacer.

## §162 · Los ataques fulminantes, fuera para todos (2026-09-22)

A petición del jugador: «que banees tanto para aprender como los ataques enemigos los ataques fulminantes, como fisura,
que absolutamente nadie lo pueda usar». Son cuatro, y los ids se comprobaron contra los nombres del propio cartucho:
**Guillotina 12, Perforador 32, Fisura 90 y Frío Polar 329**. Van en `bannedMoves` de `Data/randomizer.json`.

Antes de tocar nada se midió por dónde puede llegar un ataque, en la capa base y en el mundo instalado de la carpeta de
prueba: **65 en aprendizajes por nivel** (en 64 Pokémon), **16 en movimientos huevo**, ninguno en MT, tutores,
entrenadores ni estáticos. La capa base tenía 2 en ataques fijos de entrenadores, que el randomizador ya limpiaba al
cambiar la especie. Los salvajes y los entrenadores sin ataques propios sacan los suyos del aprendizaje, así que las
listas de aprendizaje son casi todo. Ojo con una medida que salió mal a la primera: los ataques de un entrenador están en
`0x18` de su entrada, no en `0x20`, que es donde empieza la siguiente; con el offset malo el recuento daba 0 por el
motivo equivocado.

**En el mundo que se genera**, `BannedMoveScrubber`, el penúltimo paso del randomizador, con sal propia y **cambiando solo
los huecos que tenían uno**: meterlos fuera del sorteo habría vuelto a sortear todos los aprendizajes del juego para
cambiar 65 huecos. Un hueco de aprendizaje recibe un movimiento de estado que no tuviera ya, porque un fulminante tiene
potencia 0 y ocupa un hueco que la ordenación por potencia (§158) no mueve; un movimiento huevo, cualquier otro; a un
entrenador o estático que nombre uno se le vacían los cuatro, para que el juego le dé los de su aprendizaje, ya limpio.
Todo se relee al final y el paso lanza si sobrevive uno. Verificado con la semilla 20260921 y el rol NORMAL: 57 en
aprendizajes y 16 en huevos, **cero** después en todas partes. Comparado contra la misma semilla sin la lista, solo
cambian `a/0/1/3`, en esos 57 huecos y **ningún nivel**, y entra `a/0/1/2`, que antes no se tocaba.

**En lo que la app enseña o construye por su cuenta** —el recuerda-movimientos, el gacha y el wonder trade—, la misma lista
se publica al arrancar en `WorldMoves.Banned` (desde el mismo `randomizer.json`, para que las dos no discrepen):
`WorldMoves.LevelUpOf` la deja fuera, lo que sugiere PKHeX sin mundo instalado también, y el recuerda-movimientos no la
ofrece ni como «lo que sabía al llegar» (`IMoveCatalog.IsBanned`). Así tampoco sale de un mundo generado antes de esto.

Revisada la partida de la carpeta de prueba: **ninguno de sus Pokémon sabía ya uno**. Pruebas: seis del paso del
randomizador (solo cambian los huecos prohibidos y sus niveles quedan, un aprendizaje limpio queda byte a byte, huevos,
entrenadores, estáticos y la lista que se reparte), una de `WorldMoves` y una del recuerda-movimientos, que falla sin el
filtro. **Vale al regenerar e instalar el mundo**; lo de la app, en cuanto se abre la versión nueva.

## §163 · Los objetos del suelo, al azar como en la referencia (2026-09-22)

A petición del jugador, tras comparar cómo se randomizan las Poké Balls del suelo en PermaLocke, en el Universal Pokémon
Randomizer y en la carpeta Locke: «¿podrías dejarlo como la carpeta de Locke?». Hasta hoy PermaLocke **barajaba** lo
que el cartucho ya ponía: las mismas 493 cosas en cada partida, en otros sitios. La referencia **sortea cada sitio**.

Lo que hace la referencia se midió en su mundo instalado antes de escribir nada (solo lectura): 493 sitios normales y
45 dorados, los mismos que el cartucho; en los normales objetos generales, bayas, medicinas, megapiedras, Poké Balls,
cartas y abonos, y **nunca** un objeto clave, un cristal Z ni una MT; ninguna Master Ball; **ningún objeto más de dos
veces** (177 una vez y 158 dos); y en los dorados MT sacadas de las cien, 39 una vez y 3 dos.

Esa clase de objetos es **exactamente** los bolsillos 0, 1 y 3 del cartucho —general, medicinas y bayas—, que la tabla de
objetos `a/0/1/9` guarda en los bits 7-10 del u16 de 0x08. La correspondencia se comprobó contra las listas de bolsillo
de PKHeX, las 716, sin un desacuerdo. Y se comprobó que el saco sale igual que el de la referencia: con el cartucho son
534 objetos, **los 335 distintos que la referencia puso están todos dentro**, y deja fuera 199, que es lo que da un
sorteo uniforme con tope (la prueba lo exige: de 320 a 350 distintos en 50 semillas). Quedan fuera las 38 entradas sin
usar que el cartucho llama «(?)».

`fieldItemsMode` en `Data/randomizer.json`: **`Random`** sortea así, y **`Shuffle`** es lo de antes. Si el fichero no lo
dice, `Shuffle`, para que una configuración vieja no cambie de mundo sin avisar. `fieldItemsMaxRepeats` es el tope (2) y
`fieldItemsBanned` la lista de fuera (la Master Ball, 1). En los dos modos una MT se cambia solo por otra MT y un objeto
normal por otro objeto normal, y los montones de bayas cuentan como normales, como ya pasaba y como hace la referencia.
El modo aleatorio saca cada saco de su propia fuente derivada y `Shuffle` no cambia ni una llamada: **medido**, con
`Shuffle` la misma semilla da los once ficheros byte a byte iguales que antes de este cambio.

**Con el mod de gen 8-9 el saco pasa de 534 a 597**: entran sus 63 objetos, casi todos objetos de evolución (confites,
teteras, manzanas, manuscritos, armaduras...) y las 29 megapiedras de Leyendas Z-A. Por eso sale alguna megapiedra más
que en la referencia: 77 de 493 con la semilla de prueba, contra 46. Entran también cuatro cuyo efecto en este motor nadie
ha medido —Energía Potenciadora, Cristal Teracristal, Espada y Escudo Oxidados—; en el peor caso son un objeto que no hace
nada, como las cartas y los abonos que también pone la referencia. Si molestan, van a `fieldItemsBanned`.

Verificado con la semilla 20260921 y el rol NORMAL contra la misma generación en modo `Shuffle`: de los once ficheros del
mod **solo cambia `a/0/8/3`**, y dentro de él **cero bytes fuera de los huecos de objeto** —los encuentros salvajes quedan
idénticos—. Resultado: 493 normales con 356 distintos, 219 una vez y 137 dos; 45 dorados, todos MT, 37 distintas; ninguna
Master Ball, ningún objeto clave, cristal Z ni «(?)». Nueve pruebas en `FieldItemRandomTests`. **Vale al regenerar e
instalar el mundo.**

## §164 · El registro que anda gana a la mayoría quieta (2026-09-22)

El jugador: «en un par de rutas empieza a contar la ruta y me devuelven las Poké Balls al entrar en un Pokémon salvaje y
huir». El log de la carpeta de prueba tiene los dos casos, con la misma forma:

- 15:05, un combate contado en «Ruta 1 (Afueras de Hauoli)», ya gastada. Al acabar, la zona pasa a «Ruta 1 (Escuela
  Entrenadores)» y se devuelven las balls; el siguiente combate ya cuenta como primer encuentro de la Escuela.
- 15:42, un Zigzagoon contado en «Ciudad Hauoli (Zona Comercial)», ya gastada. Medio segundo después de acabar, la zona
  pasa a «Ruta 2» y se devuelven las balls; el siguiente combate, el Cascoon, se lleva el primer encuentro de la Ruta 2.

O sea que el primer combate de la ruta nueva se contó en la de detrás, sin balls, y la zona no se enteraba del cambio
hasta que un combate terminaba. En los dos, un borde **sin puerta**, y entre el cambio y el combate ni un aviso de
desacuerdo ni de zona mantenida: la mayoría de los registros seguía diciendo el sitio anterior.

Por qué: el juego guarda **copias de la posición que solo refresca a ratos** —en una puerta, al empezar o acabar un
combate—. En el mismo log se ven: a las 14:47:07 `0x303B7534` y `0x33F68F40` son basura, y a las 14:47:27 tienen la
posición exacta de `0x33F6E4C8`, el registro que sigue al jugador. Y la misma tarde hay un trío congelado desde mucho
antes, `0x32DE3898`, `0x32DE3948` y `0x32DE3978`, que dice «Senda Mahalo (Puente Colgante)» en (2000, 3.94, 6304) sesión
tras sesión. Al cruzar un borde sin puerta, el que sigue al jugador cambia de mapa y anda; las copias se quedan detrás,
quietas, y le ganan la votación. Hasta que el combate las pone al día, que es justo cuando se devolvían las balls.

El lector decidía **primero por mayoría** y solo miraba al que anda para deshacer un empate (§118). Ahora, si hay un
registro andando —el mismo listón de antes: dos movimientos en cinco segundos, y todos los que andan en un mismo mapa— y
está en **otro mapa que la mayoría**, manda el que anda. Parado no cambia nada: decide la mayoría, y la histéresis del
§156 mantiene la zona nueva cuando se deja de andar, porque las copias quietas ni la decían ni se mueven. La bolsa del
§156 sigue igual: al abrirla, los que siguen al jugador se apagan y no anda nadie.

Lo que **no está medido** es que en esos dos bordes hubiera un registro andando en el mapa nuevo: el log solo apunta los
registros cuando no se ponen de acuerdo, y aquí había mayoría. Es la explicación que encaja con todo lo que sí consta,
pero es una explicación. Por eso, cuando el que anda le lleva la contraria a una mayoría, el log **apunta ahora todos los
registros**, para confirmarlo o desmentirlo la próxima vez. Si no hubiera ninguno andando, este arreglo no bastaría y
habría que buscar al que sigue al jugador.

Dos pruebas nuevas en `EncounterRecoveryTests`: tres copias quietas en el mapa de detrás y uno que anda en el nuevo
—falla con el código de antes, se queda en el de detrás—, que parado no se lo lleven de vuelta, búsqueda incluida, y que
uno que anda en el mismo mapa que la mayoría no cambia nada.

## §165 · El suelo naranja no es una barra de PS (2026-09-22)

El jugador: «un par de Pokémon que me han matado con veneno o con la trampa de rocas, la detección de muerte ha tardado
en salir». En el log de esa tarde hay seis caídas y **dos tardaron**: Tranquill a las 18:50 y Raboot a las 18:51, las dos
con «barra vista pero sin llegar a cero en 6 s», o sea el tope entero de `HpBarWatcher`. Las otras cuatro vieron la barra
a cero entre 97 y 400 ms. Contadas todas las caídas registradas hasta hoy, cuarenta y nueve, la peor tardó **1889 ms**.

Las killcams de esas dos lo enseñan: en los 4,5 s grabados **no sale ni el Pokémon ni su caja de PS**, solo el rival y el
entrenador, porque ya había caído y el juego esperaba a que el jugador eligiera el siguiente. Lo que el vigilante leía
como «barra al 97 %» era el **suelo naranja** del ring de Pueblo Iki, medido en esos fotogramas a **(170,110,65)** justo
en las filas de la barra. El filtro de relleno pedía «cálido y saturado» —máximo de rojo y verde por encima de 150, azul
por debajo de 110—, y el suelo lo cumple.

Dos arreglos, los dos medidos contra los fotogramas reales:

**El relleno son los tres colores de la barra**, verde (148,254,48), amarillo (253,201,43) y rojo (251,0,20), con 60 de
margen por canal para lo que difumine el escalado del emulador. Pasado el lector nuevo por las killcams: en la muerte
normal ve 28 fotogramas con la barra al 6 % y luego **vacía**, y en las dos que tardaron **Hidden en los 96**, o sea que
el suelo ya no cuela.

**Y no se espera a una caja que no está.** Dos reglas nuevas en `ZeroWatch`: una caja **vacía** sin haber visto color
antes cuenta como la caída tras tres lecturas seguidas —la barra ya había bajado cuando llegó el aviso, que es lo que
pasa con el veneno y con las trampas de entrada, y este vigilante solo corre sobre una caída que las dos tablas ya dan
por buena—; y si la caja **no aparece** en 2,5 s se deja de esperar, que es el 1889 ms peor medido con margen. El tope de
6 s se queda para lo de siempre: una animación larga tapando una barra que sí está.

Por qué llega tarde el aviso en estos casos no está medido: la caída se da cuando las dos tablas llegan a cero y la
segunda sigue a la barra (§114), así que con el veneno y la trampa de rocas esa segunda tabla tiene que actualizarse
después de la escena, no durante. Lo que se arregla aquí es la espera, no la detección. Siete pruebas nuevas en
`HpBarTests`, con los colores del suelo medidos.

## §166 · Que cada Pokémon aprenda lo suyo (2026-09-22)

El jugador: «no sé si ha sido casualidad, pero mis Pokémon aprenden casi lo mismo todos por nivel». No era casualidad, y
lo que notaba no era repetición —dos especies al azar comparten el 2,5 % de su lista, menos que el 6,3 % del cartucho—,
sino **falta de identidad**. Medido sobre los tres mundos:

| | del tipo del propio Pokémon | tipos distintos por Pokémon |
|---|---|---|
| Cartucho | 48,7 % | 4,8 |
| Referencia (Locke) | 28,0 % | 9,2 |
| Mundo del jugador | **9,6 %** | **9,8** |

Un 9,6 % es lo que sale del puro azar: cada Pokémon aprendía un poco de los dieciocho tipos, así que ninguno tenía tipo
propio y todos se jugaban igual. El sesgo ya estaba implementado —`learnsetPreferSameType`, la regla del Universal
Pokémon Randomizer— y estaba **apagado**; encendido da el 46 %, casi el cartucho. El jugador eligió el punto de la
referencia, así que el interruptor pasa a ser un porcentaje, `learnsetSameTypePercent`, con el nombre viejo leyéndose
todavía como 40. En 20 sale **27,9 % y 9,2 tipos por Pokémon**, que es la referencia clavada.

De la potencia se preguntó también, y ahí la respuesta fue que **ya está como la referencia**: 52/76/102 de media por
tramo de nivel contra 53/71/95, y la diferencia la explica el mod de gen 8-9, que añade 192 ataques y son fuertes. Se
midió además la otra vía, `learnsetPowerTolerance`, que ata cada hueco a la potencia que el cartucho tenía ahí: **no
reproduce esa curva, la aplana** —con 1,0 sale 55/60/67 y con 2,5 sale 67/68/70, porque una banda ancha sobre un ataque
flojo solo puede subir y sobre uno fuerte solo puede bajar—. Queda en 0.

Verificado generando con la semilla 20260921: de los once ficheros del mod **solo cambia `a/0/1/3`**. Dos pruebas nuevas,
una que mide el reparto por tipos con tres porcentajes y otra que fija lo que pide la competición. **Vale al regenerar e
instalar el mundo**, y solo para lo que se aprenda a partir de ahí.

## §167 · Un log que se ahogaba en sí mismo (2026-09-22)

Un amigo del jugador contó cierres y que no le funcionaban los avisos «ni nada», con la carpeta `PermaLocke para
amigos` de la noche del 21. **Corrección**: la primera versión de este apartado decía que había recibido la carpeta
`PermaLocke prueba`, con la run, el perfil y la partida del jugador. **Era falso**: lo supuse sin preguntarlo, y el
jugador lo desmintió. La causa probable apareció después y está en el §168.

**Para medir un caso concreto ya existe el recogedor**: `RECOGER DIAGNOSTICO.cmd`, en la raíz del reparto, junta los tres
últimos logs de la app y de Azahar, los cierres de Windows de tres días con el módulo que falló, el hardware, la
configuración y las huellas de los ejecutables, **sin ROM ni partidas**, en un zip que no manda a ningún sitio.

**Y el log de la app estaba ahogado.** De las 14.120 líneas del log del día, **8.672 (el 61 %) eran la misma**:
«Azahar propio encontrado en…», escrita hasta 3,6 veces por segundo porque `AzaharInstallation.Locate` se pregunta en
cada lectura de la partida y en cada vuelta del modo combate. Detrás, 715 «Récords leídos» cada veinte segundos con los
mismos números. Un informe que llegue así tiene lo que importa enterrado. Ahora las dos líneas se escriben **cuando
cambian**: la búsqueda del emulador se sigue haciendo en cada llamada —es un `File.Exists`, y un emulador que aparece a
media sesión tiene que verse—, solo que no se repite en el log. Una prueba nueva, que cuenta las líneas y exige una sola
para cinco llamadas iguales y otra cuando aparece el emulador propio.

## §168 · Que funcione en cualquier PC sin probar amigo por amigo (2026-09-22)

El jugador: un amigo, con la carpeta `PermaLocke para amigos` de la noche del 21, tenía cierres y «no le funcionaba
nada de las notis ni nada». Y la condición: «no acabamos nunca si tengo que estar amigo por amigo probando; hay que
hacer que el producto funcione en todo tipo de ordenadores». O sea que la respuesta no puede ser pedirle pruebas a
cada uno, sino buscar lo que cambia de un PC a otro y, lo que no se pueda prever, recogerlo solo.

**Lo que cambia de un PC a otro, y estaba roto: el Visual C++ del emulador.** `azahar.exe` importa `MSVCP140.dll`,
`MSVCP140_ATOMIC_WAIT.dll`, `VCRUNTIME140.dll` y `VCRUNTIME140_1.dll`, y Qt y FFmpeg añaden `MSVCP140_1` y `_2`; la
carpeta no llevaba ninguna, así que cada PC ponía la suya. `azahar.exe` está enlazado con las herramientas **14.51**
(se compila en GitHub Actions con el Visual Studio más nuevo), y lo enlazado con la 14.40 o posterior se cierra con un
`msvcp140.dll` anterior la primera vez que bloquea un cerrojo: fallo documentado por Microsoft, que no avisa al
arrancar sino a media partida. Sin `msvcp140_atomic_wait.dll` directamente no arranca. El PC del jugador tiene el
14.51 y **nunca lo vio**. En el del amigo **no está comprobado** —no hay informe suyo—, pero con un runtime de antes
de 2024, lo normal en un PC al que solo se lo instalaron juegos viejos, es exactamente lo que pasa. Y con Azahar
caído no funciona nada de PermaLocke, avisos incluidos.

Arreglo: `publicar.ps1` pone las seis DLL **al lado de `azahar.exe`**, sacadas del `System32` del PC que publica,
exigiendo firma de Microsoft y una versión no menor que la del compilador de **cada** binario de la carpeta (hoy sale
14.51), y el reparto no se da por bueno si falta alguna. Windows busca primero en la carpeta del programa y ninguna es
KnownDLL, y está **medido**: arrancado desde una copia con las DLL al lado, las seis se cargan desde esa carpeta y no
desde `System32`. Microsoft permite distribuirlas así. Con eso el runtime de cada PC deja de importar.

**Lo que no se puede prever, se recoge solo.** Cuando Azahar se cierra y **no** lo ha cerrado PermaLocke, el lanzador
lee su código de salida y, si es de fallo, escribe al momento `Diagnosticos\cierre-azahar-<fecha>.zip` y avisa al
jugador de dónde está y de que se lo pase a quien le dio PermaLocke. Dentro: el código con lo que significa
(`0xC0000005` acceso a memoria, `0xC0000135` falta una DLL, `0xC0000139` DLL demasiado antigua…), la duración de la
sesión, el Visual C++ que usó el emulador y si llegaba, Windows, procesador, memoria, tarjeta gráfica, si la carpeta
está en OneDrive y el espacio libre; las últimas 400 líneas de los dos logs; y **las últimas 128 peticiones de
PermaLocke al emulador**, con dirección, tamaño, si respondió y cuánto tardó, porque los dos cierres de Azahar que se
explicaron alguna vez (§159) se explicaron por lo que PermaLocke estaba leyendo en ese momento, y aquello salió en el
log por suerte. Sin ROM, sin partidas y sin run, y no se manda a ningún sitio.

Tres piezas: `ProcessExitWatch` sujeta el proceso de Azahar con el permiso mínimo mientras vive —el lanzador lo busca
por nombre, porque puede abrirse a mano, y un proceso encontrado así no deja código de salida si nadie lo tenía
abierto—; `RpcTrace` anota cada petición dentro del cerrojo que ya tenía el cliente; y `EmulatorCrashReport` escribe.
Cerrarlo el jugador (0), cerrarlo PermaLocke y matarlo desde fuera (1, `0xC000013A`) no cuentan como caída.
**Verificado de punta a punta** con la app publicada en una carpeta aparte y un `azahar.exe` falso que termina con
`0xC0000005`: la app vio la sesión, leyó el código, escribió el zip y lanzó el aviso.

**Y JUGAR avisa antes de empezar** de lo que el PC puede estropear: el Visual C++ que va a cargar el emulador si no
llega (para repartos anteriores o un Azahar elegido a mano), una carpeta dentro de OneDrive —que bloquea los ficheros
mientras los sube—, una carpeta donde no se puede escribir y menos de 2 GB libres. Van delante de «instala tu mundo»,
porque JUGAR enseña solo el primer aviso y un emulador que se va a caer importa más.

Diecisiete pruebas nuevas en `EmulatorCrashTests`, una de ellas con un proceso de verdad que termina con
`0xC0000005`. Cazaron un fallo mío: leer el final del log contaba el último salto de línea como una línea y se comía
una de verdad.

Lo que queda **sin cubrir**, dicho: si PermaLocke carga el emulador con sus lecturas en un PC lento, esto no lo
arregla, pero el primer informe que llegue lo dirá, con las peticiones de ese momento y lo que tardaron.

## §169 · El cuarto del entrenador, en la portada de JUGAR (2026-09-22)

El jugador pidió algo en pixel art que impresionara, «como el cementerio y los avisos», y eligió de cuatro ideas el
cuarto del entrenador, pero no como sección nueva sino de fondo de una que ya hubiera. Se probó primero en HOME, en el
sitio de la tira de contadores, con una imagen de prueba hecha con los datos reales de su run; al verla decidió que
iba mejor **en JUGAR, en lugar de la playa**, y que HOME tendría otro rediseño más adelante. La playa
(`LauncherStage`) se borró a petición del jugador; está en el historial de git.

Cada objeto dice algo verdadero de la run:

- La **vitrina** tiene doce huecos, uno por prueba y en su orden. Las pruebas son los logros que se desbloquean con un
  cristal Z (`ZCrystalIndex`), sin sus nombres escritos en el código. Una prueba superada pone su cristal a color,
  sacado del cartucho del jugador; una pendiente, **la silueta de ese mismo cristal**, así que el hueco ya dice cuál
  espera.
- La **estantería-memorial** tiene una figurita de piedra por cada caído de la run, hecha con su propio sprite a media
  escala y pasada a tres tonos de piedra con contorno, y una placa con la cuenta.
- La **tele** pone un fotograma de la killcam más reciente, un instante antes de la caída, reducido a 44×21 celdas con
  seis niveles por canal y líneas de tubo. Sin killcam, nieve.
- En la **alfombra** saltan los vivos del equipo (los de la partida que no están caídos en la run, por PID); mientras
  el juego está abierto se quedan quietos, como en la playa.
- El **póster** es el tope de nivel en vigor, el **corcho** tiene los huecos de la colección de Dominsignias con una
  pegatina en cada una conseguida, el **monitor** del escritorio un punto por Pokémon en el PC, y la **ventana** es
  Alola a la hora del juego, con los colores de la cabecera. Al ganar la liga aparece un trofeo encima de la tele.

Técnica, la misma del resto (§116, §120): cada celda es un píxel de un bitmap que se escala en múltiplos exactos (tres
píxeles de pantalla por celda, sea cual sea el DPI), colores planos de la paleta violeta de la aplicación, trama
ordenada en vez de degradados, y formas escritas a mano. `TrainerRoomScene` pinta y `TrainerRoom` lo pone en pantalla a
cuatro pasos por segundo. Está diseñado a 430×72 celdas y centrado: un panel más ancho da más pared a los lados y uno
más alto, más pared arriba, con el suelo siempre abajo.

Lo que costó verlo en la aplicación y no en la prueba: la **barra de JUGAR se monta 22 píxeles** sobre la portada. La
playa lo aguantaba porque debajo solo había arena; en el cuarto se comía la alfombra y los pies del equipo. La escena
recibe cuántas filas quedan tapadas y se sube esas filas, con la tarima siguiendo por debajo, y la portada pasa de 300
a 320 píxeles para que el cartel de «POKÉMON ULTRA LUNA» no tape la ventana.

Y de la primera imagen de prueba salieron cinco correcciones antes de enseñarla: los cristales miden 29×23 y metidos en
9×9 eran un punto, así que la vitrina se ensanchó para dibujarlos a media escala; las siluetas se perdían entre los
reflejos del cristal; la tele tramada era un borrón; el número del póster se salía, y en el corcho «5/25» se leía
«5725», así que se quitó el texto y se dibujan los veinticinco huecos. Seis pruebas en `TrainerRoomTests`.

## §170 · El gacha vuelve a su sitio, y su historial es de una run (2026-09-22)

Dos cosas que pidió el jugador, las dos de la pantalla del gacha.

**El resultado se va solo.** La ficha del Pokémon y la tira parada sobre el ganador se quedaban puestas hasta cerrar la
aplicación: el view model es un singleton y nadie las quitaba. `ResetState` lo decía a propósito («la ficha se queda,
es lo que fue la última tirada»), y el jugador pidió lo contrario, con razón: volver al gacha tiene que encontrar la
máquina esperando. Ahora el escenario vuelve al reposo **a los 8 segundos** (`ResultShownFor`, contados desde que la
entrega termina) o **al cambiar de pestaña**: sin ficha, portales apagados, marcador en el color de reposo y la tira de
espera otra vez a la deriva. No se pierde nada: el Pokémon está en el juego y en la tira de LO QUE HA SALIDO. Dos
matices. La **línea de estado** de abajo se queda con el temporizador, porque dice adónde ha ido el Pokémon y no está
en el escenario; y al cambiar de pestaña también se queda **si es un aviso** («en la run sí está, en el juego todavía
no»), que es lo único que no se puede borrar sin que alguien lo haya leído. Irse con la rueda girando no corta la
tirada, que ya está escrita: vuelve al reposo en cuanto aterriza, salvo que el jugador haya vuelto antes.

La vista tenía la otra mitad: la deriva de la tira se arrancaba **una sola vez**, al primer tamaño, y tras una tirada
nunca más. El view model avisa con `ReturnedToIdle` cuando rehace la tira de espera y la vista suelta la de la tirada,
arranca la deriva y la funde en medio segundo, porque las celdas bajo el marcador cambian todas a la vez.

**El historial es de una run.** LO QUE HA SALIDO sobrevivía a «empezar de cero»: se siembra del historial de la run al
abrir la pantalla, pero solo si está vacía, y como el view model vive lo que la aplicación, la run nueva heredaba las
tiradas de la borrada. Ahora recuerda de qué run es la tira y, si la cargada es otra, la vacía, limpia el escenario y
vuelve a leerla.

Verificado con tiradas de verdad en una copia aislada fuera del repositorio (su propio emulador portátil y su propia
partida, precios a cero y un hito de prueba con tiradas gratis solo en sus JSON): la ficha sale a los 8,0 s de pulsar
y se va 7,9 s después; al ir a TIENDA con la ficha puesta y volver, ya no está; y tras EMPEZAR DE CERO la tira queda
vacía. La misma prueba con el ejecutable anterior deja la tira puesta, así que es el arreglo lo que la vacía.

## §171 · El gacha es una máquina de cápsulas (2026-09-22)

El jugador pidió rehacer el gacha, visual y en animación, con las probabilidades intactas, y eligió la máquina de
cápsulas entre las propuestas, «en pixel art, que es lo que mejor se te da». Sustituye a la ruleta de iconos del §31 y
a su fondo de ultraespacio, que eran degradados, desenfoques y resplandores, justo lo que se tiró del cielo (§116).

**La escena** (`CapsuleMachineScene`) se dibuja celda a celda como el cuarto del §169: tres píxeles de pantalla por
celda, colores planos, trama en vez de degradados y nada de desenfoque. Diseñada a 430×166 celdas con el suelo abajo;
un panel más alto da más pared encima y la tira de neón va pegada al techo de verdad, porque a media pared partía la
escena en dos.

- **La máquina** va a la izquierda, pintada del color del tier que más da su banner (el mismo que la barra de su
  tarjeta): POCHO verde, DECENTE azul y BUENO rosa con remates dorados y bombillas en la tapa. El cartel dice el
  nombre del banner y la placa de la moneda, su precio.
- **La cúpula enseña las probabilidades**: lleva las balls del banner en su proporción real, por resto mayor
  (`DomeMix`), y ninguna de un tier que el banner no da. POCHO son Poké, Super y Ultra Balls 15/60/25; BUENO, Ultra,
  Gloria y Master Balls. Al cambiar de banner la cúpula se vuelve a llenar delante del jugador.
- **Una ball por tier**: Poké, Super, Ultra, Gloria (la de los regalos de evento) y Master. No coinciden con el color
  de cada tier y no se ha forzado: una Master Ball se entiende sin leyenda. Por eso los portales de arriba llevan el
  dibujo de su ball, hecho con las mismas celdas (`BallIcon`), y se mudan arriba a la derecha, sobre la alfombrilla.
- **La estantería** de la izquierda sustituye a la tira de LO QUE HA SALIDO, con el rótulo ÚLTIMAS TIRADAS que eligió el
  jugador: las ocho últimas tiradas en figuritas, cada una sobre una peana del color de su tier. El letrero de neón de la derecha tapa la pared que la ficha usa luego.

**La tirada**, igual de larga para cualquier tier (`CapsuleTimeline`, 9,95 s hasta el Pokémon a color). La ruleta
vieja duraba más cuanto más raro el resultado, y eso lo contaba antes de tiempo:

1. Cae una moneda y la manivela da cuatro cuartos de vuelta, con un golpe de la máquina en cada clac.
2. Las balls se revuelven y una baja por el agujero.
3. Sale por la trampilla, bota hacia el jugador creciendo y rueda hasta la alfombrilla.
4. Se sacude tres veces como en una captura. Al final de un meneo puede **subir de ball**, con fogonazo y aro de
   chispas, y a veces brilla sin subir.
5. Parpadea el botón, se abre la tapa, sale un haz de luz y rayos del color del tier (dorados de más si es legendario),
   y el Pokémon aparece en silueta blanca, crece al doble y pasa a color. Si es variocolor, estrellas.
6. A los 8 s (§170) la ball vuela a la estantería y la figurita nueva cae en su hueco.

La subida es el engaño de siempre contado con Poké Balls: la ball cae como **la más barata que tiene el banner**, no
como una Poké Ball, porque en la cúpula de BUENO no hay ninguna, y sube como mucho dos veces (`CapsulePlay.StepsFor`).
En qué meneos sube sale de la semilla de la tirada y nunca del tier.

**Quién lleva el reloj.** El view model publica `CurrentPlay` con el instante en que cayó la moneda, y la escena se
dibuja en cada fotograma a partir de cuánto hace de eso. No hay animación a la que esperar ni red de seguridad: el view
model sigue los mismos tiempos para encender los portales y sacar la ficha, y volver al gacha a mitad de tirada
encuentra la ball donde debe estar. Un fallo al dibujar se apunta una vez en el log y deja la escena quieta; no toca la
tirada, que ya está escrita (§87).

Verificado con tiradas reales en la copia aislada del §170, con capturas de la app de cada fase: reposo, rodando,
Pokémon a la vista con la ficha y el portal encendido, la figurita en la estantería y el cambio a DECENTE. La ruleta vieja se ha borrado entera, `ReelEnding` y `GachaReelViewModel` incluidos, a petición del jugador; queda en
el historial de git. Diez
pruebas en `CapsuleMachineTests`, entre ellas dibujar cada fotograma de una tirada de cada tier. La tirada de un
legendario, con la Master Ball, solo se ha visto en la escena suelta, no en la app.

## §172 · Un solo zip para compartir, y el Escritorio fuera de las instrucciones (2026-09-22)

A un amigo le salió en JUGAR el aviso del §168 «Está dentro de OneDrive». Había bajado de Google Drive la carpeta
compartida y la había dejado en el Escritorio, que Windows 11 sincroniza con OneDrive en muchos PC. El aviso era
verdad, y parte de la culpa era nuestra: el `EMPIEZA AQUI.txt` recomendaba extraer la carpeta «por ejemplo en
Escritorio o Juegos». Ahora dice `C:\Juegos`, explica cómo («Extraer todo» y escribir la ruta) y por qué el Escritorio
y Documentos no: OneDrive bloquea los ficheros mientras los sube y los 2,7 GB llenan la mitad de su versión gratis.

Y se reparte **un fichero y no una carpeta**, porque Drive parte en varios zips cualquier carpeta de más de 2 GB, y
quien extrae solo uno se queda sin la mitad de los ficheros. `tools/empaquetar.ps1` hace el zip con una carpeta
`PermaLocke` dentro, de modo que «Extraer todo» en `C:\Juegos` deja `C:\Juegos\PermaLocke`. `publicar.ps1` lo llama
al terminar y deja `<destino>.zip` al lado de la carpeta; con `-SinZip` no lo hace.

La protección que importa es que **no empaqueta una carpeta que alguien haya abierto**: exige que cada fichero tenga
el tamaño y la huella que `publicar.ps1` apuntó en `Soporte\contenido.json`, ni uno de más ni uno de menos. Abrir la
app crea `Saves`, `Logs` y `Config\jugador.json`, y un zip hecho de ahí le daría a un amigo la run y la partida de
otro. Probado con una carpeta en miniatura a la que se le coló una base de datos: se niega diciendo qué sobra y no deja
zip. Además escribe a un `.parcial` y lo renombra solo si al releerlo cuadran los 164 ficheros con sus tamaños.
Nunca sobrescribe un zip. El de hoy pesa 2,2 GB y tarda unos dos minutos.

## §173 · Los cierres del amigo eran del emulador, y ya están arreglados (2026-09-23)

A un amigo se le cerraba Azahar muy pronto, varias veces. Llegaron dos informes automáticos del §168 y el
diagnóstico del `RECOGER DIAGNOSTICO.cmd`, y señalaban lo mismo desde tres sitios:

- **La petición en curso.** En los dos cierres, la última petición de PermaLocke era un `SearchMemory` de
  **64 MB** sobre el heap lineal, sin respuesta.
- **El registro de Windows.** Ponía el acceso inválido **siempre en el mismo punto**, `azahar.exe + 0x781693`, en
  tres días y tres carpetas distintas.
- **El log de Azahar.** El otro cierre era una aserción del propio emulador en la caché de la GPU
  (`DownloadFillSurface`).

No era su PC: el Visual C++ era el bueno (§168), dibujaba con su RTX 4060, y OneDrive no tenía que ver, aunque el
aviso de §172 también era verdad.

La causa estaba en **nuestro fork**. El servidor RPC atiende en su propio hilo y leía con `ReadBlock`, que vuelca
cualquier página que tenga la GPU en caché, y volcar llama al renderizador. Con OpenGL el contexto solo vive en el
hilo de emulación, así que hacerlo desde el RPC choca con el dibujado. Una lectura pequeña casi nunca cae en una
página de la GPU; un barrido de 64 MB de los lectores de zona, equipo y combate, casi siempre.

El arreglo es el parche 4 del fork (`docs/fork/04-leer-sin-volcar.md`, commit `0dfe782`, 31 líneas): las lecturas
del RPC usan la variante de `ReadBlockImpl` sin volcado, que ya existía y nadie usaba.

**Verificado antes y después, con condiciones iguales.** La copia aislada va en una ruta corta, porque en el
scratchpad la SD emulada pasaba de 260 caracteres y el juego no llegaba a cargar. Lleva el mod, la partida de
prueba y una prueba que entra en la partida sola y busca 64 MB sin parar.

- **Emulador viejo:** se cayó en **6 de 6** intentos, con las **dos firmas exactas del amigo** (el
  `0xC0000005` a cuatro bytes de su dirección y la misma aserción en la misma línea) y tres congelamientos a
  0 FPS.
- **Control:** sin ninguna petición RPC, el emulador viejo carga y corre a 160 FPS.
- **Emulador parcheado:** **0 de 4**, 3600 búsquedas por intento, 160 FPS todo el rato.

**Confirmado el 2026-09-24 por el jugador: a su amigo ya no se le cierra Azahar** con el emulador parcheado.

De paso, dos lecciones que valen para cualquier prueba así:

- Un detector de congelamiento que lee memoria puede fallar justo por el cambio que se está probando. El de trozos
  del heap dio falsos positivos con el parche, y la barra de estado de Azahar, leída por UI Automation, no.
- Una prueba a medio corregir hay que recompilarla. Dos intentos se perdieron porque el detector desactivado
  seguía dentro del binario.

La carpeta de amigos y su zip llevan ya el emulador nuevo.

## §174 · El wonder trade es una cabina en la sala del gacha (2026-09-23)

Después de la máquina de cápsulas (§171), el jugador pidió lo mismo para el wonder trade: pixel art, «lo más detallado
posible». Sustituye al cable de enlace del §121, cuyas tres pistas eran cajas de WPF encima del dibujo. Ahora las dice
la pantalla de la propia cabina, con sus píxeles, y se quedan ahí al lado de la ficha.

**Una sala para las dos máquinas.** La pared, el neón del techo, el foco y el suelo pasan a `PixelScene.PaintRoom`,
junto con los materiales comunes: cromo, cristal, placas y bombillas. Medido que el gacha sale **idéntico píxel a
píxel** en sus 27 fotogramas de referencia antes y después de mudarlo.

**La escena** (`TradeMachineScene`, 430×166 celdas, anclada al suelo como el gacha):

- **La cabina** va en el centro, en azul con remates de cromo. Encima lleva un cartel con bombillas que dice
  INTERCAMBIO / PRODIGIOSO en dos tonos, y en lo alto una baliza.
- **La pantalla** es de fósforo verde agua con líneas de barrido y un reflejo en la esquina.
- **Debajo de la pantalla**, dos rejillas de altavoz, dos pilotos con flecha (sube y baja) y el emblema.
- **Dos tubos neumáticos de cristal** suben hasta el techo de verdad. Por el cristal se ve la pared teñida, llevan
  aros y abrazaderas de cromo, una tira de LED por fuera y un brillo delante que pasa también por encima de la ball.
- **Una estación con trampilla** al pie de cada tubo. La puerta corredera lleva la flecha de su sentido.
- **Dos plataformas redondas** con anillo de luz, unidas a su estación por un cable de enlace en el suelo, el guiño al
  §121.
- **Un neón** en la pared izquierda: dos flechas persiguiéndose alrededor de un globo.

**El intercambio**, igual de largo para cualquier resultado (`TradeTimeline`, 11,35 s hasta el Pokémon a color):

1. El Pokémon entregado espera en la plataforma de la izquierda. Sale su ball, abierta, y un rayo rojo lo pasa a
   silueta roja, que encoge hasta entrar. La ball se cierra con fogonazo, aro de chispas y un meneo de captura.
2. Da dos botes hasta la trampilla, que se abre. La trampilla se la traga con una bocanada de aire, y la ball sube por
   el tubo acelerando, con estela y el cristal encendido a su paso. Los LED persiguen hacia arriba.
3. La pantalla pone ENVIANDO con su barra. Después, BUSCANDO con un globo de alambre que gira y señales que se
   encienden, mientras la baliza gira en ámbar. Con ¡CONECTADO! destella, la baliza y los LED se ponen verdes y hay
   un ping grande.
4. RECIBIENDO: la otra ball baja por el tubo derecho frenando y sale por la trampilla. Cae al suelo y rueda hacia el
   jugador creciendo, con dos botes, se pasa un poco y vuelve a la plataforma.
5. La pantalla dice **GENERACIÓN N**, luego **los tipos** en placas de su color, y luego **el total**, que cuenta
   desde el del Pokémon entregado hasta el nuevo. La diferencia sale en verde si es mejor, en rojo si es peor y sin
   color si es igual. Es el orden del §33. Cada línea entra con un bloque blanco, y la ball da un respingo con cada
   una.
6. Parpadea el botón y se abre: la sala se oscurece (lo que da luz sigue encendido), sale el haz y rayos del color de
   su primer tipo, que llegan más lejos cuanto mayor es el total y llevan oro si es legendario. El Pokémon sale en
   silueta, crece al doble y pasa a color. El anillo de la plataforma toma el color de su tipo, y si es variocolor
   salen estrellas.

Entonces aparece la ficha a la izquierda, sobre la plataforma de envío, que ya está vacía. Lleva el nombre, variocolor
o legendario, los tipos, el nivel y la naturaleza, la habilidad, los IV y el total con su diferencia.

**La ball es la suya.** El Pokémon entregado entra en la ball en la que vive. Si es Poké, Super, Ultra, Master o
Gloria, la dibuja la escena (`BallFor`). Si es cualquier otra de las dieciséis que se extraen (§28), sale con el icono
del cartucho, sin hacerla pasar por una Poké Ball; una ball de más allá del 16 sale como Poké Ball. El que llega viene
en Poké Ball porque es la que escribe `PokemonBuilder`.

**Quién lleva el reloj**, igual que en el gacha: el view model publica `CurrentPlay` (`TradePlay`) con el instante de
la confirmación, y la escena se dibuja cada fotograma a partir de cuánto hace. Ya no hay eventos entre vista y view
model ni red de seguridad de ocho segundos. El intercambio sigue escrito, releído y apuntado antes del primer
fotograma, y un fallo al dibujar se apunta en el log sin tocarlo.

**Verificado:**

- Un wonder trade real en la copia aislada del §170, con capturas de la app en cada fase: Slugma sale y entra
  Rookidee, generación 8, VOLADOR, 245 y −2 % en rojo, y la ficha con LISTO.
- El Rookidee no apareció en la plataforma de esa copia porque su carpeta `Expansion` está vacía y solo tiene los 1154
  iconos del cartucho. La de prueba tiene los 1508 (1025 especies), y el mismo Rookidee se dibuja bien con los sprites
  del repositorio.
- Siete pruebas en `TradeMachineTests`, entre ellas dibujar cada fotograma de cuatro intercambios en tres tamaños, el
  color de la diferencia y que una ball que la escena no dibuja salga como el icono.

`TradeStage.cs`, la escena del §121, se ha borrado a petición del jugador. No estaba en git (nunca se había hecho commit de ella), así que no queda copia; lo que hacía está descrito en el §121.

La carpeta de amigos, su zip (`.dist/amigos-20260923-040605.zip`) y la carpeta de prueba llevan ya la cabina, con el mismo ejecutable.

## §175 · La ruleta del LUDÓPATA es una rueda de feria en la sala recreativa (2026-09-23)

Tercera escena en píxeles después de la máquina de cápsulas (§171) y la cabina del intercambio (§174), a petición del
jugador: «del mismo estilo, igual de bien detallado, y añade más cosas». La rueda WPF del §84 —cuñas vectoriales,
degradados, sombras difuminadas, un `Viewbox` para que cupiera— pasa a dibujarse celda a celda en `RouletteMachineScene`,
en la misma sala que las otras dos (`PixelScene.PaintRoom`).

**Lo que no cambia**, porque ya se decidió y se revocó por escrito:

- Verde paga y rojo cuesta, con dos tonos por bando alternados (§84).
- Las seis caras se destapan una a una antes de que la rueda se mueva, con tiempo de leerlas (1,5 s cada una).
- Un solo barrido hasta la ganadora con uno de los cinco perfiles de `WheelEnding`, sacados de su propia semilla y
  nunca del resultado. El rebote nunca pasa de 20° (§86).
- La ganadora encendida y las otras cinco apagadas.
- La ficha del resultado, que se va a los ocho segundos.

**Lo que hay ahora en la escena:**

- **La rueda**: aro de oro con veinte bombillas y seis clavijas de cromo, una en cada junta; cuñas con luz fija de
  arriba a la izquierda, juntas de oro y el buje con la Poké Ball, que ya gira con la rueda.
- **Las bombillas** persiguen despacio mientras espera, se despiertan de una en una al tirar de la palanca,
  parpadean mientras se destapan las caras, corren durante el giro, toman el color de la cuña que pasa bajo la
  flecha cuando la rueda ya va despacio y destellan al parar.
- **La flecha** es una lengüeta roja colgada de un soporte dorado, y **las clavijas la empujan** al pasar: se dobla
  hacia la derecha y vuelve de golpe un poco hacia la izquierda, calculado del ángulo de la rueda (`Deflection`) y no
  de un temporizador. Sustituye al golpe de marcador del §84.
- **Las caras**: la interrogación dorada encoge antes de destaparse, la cuña da un fogonazo blanco, el color entra de
  dentro afuera y la cara salta con chispas: dibujo del cartucho, cifra grande y nombre, en dos líneas si no cabe.
  Siguen derechas mientras la rueda gira, como en el §84.
- **El tablero de premios y castigos** de la pared izquierda: las dieciséis caras, las que pagan arriba y las que
  cuestan abajo, con una bombilla que se enciende cuando esa cara sale en la rueda y un marco dorado que parpadea en
  la ganadora. Sustituye a la tira de dieciséis de debajo de la rueda.
- **El marcador DEBES** de la pared derecha, en cifras de segmentos rojos con las apagadas debajo, y **las fichas**:
  una por tirada debida, en dos montones sobre una mesita. Más de dieciséis se dicen con un número; no se inventan
  pilas.
- **La tirada empieza pagando**: una ficha sale del montón girando en el aire y entra de canto en la ranura de la
  caja de la palanca, el marcador baja uno, y la palanca baja y vuelve.
- **El giro** va precedido de un tirón hacia atrás de 10° y, mientras la rueda va despacio, un halo alrededor
  tramado del color de la cuña bajo la flecha.
- **Al parar**, la rueda se mece 2,5° (menos de media cuña) y sale un aro de oro desde el buje. Si la cara paga, cae
  confeti y saltan monedas; si cuesta, el neón del techo parpadea en rojo y los bordes de la sala se tiñen de rojo.
- **Un neón** con dos dados y la palabra LUDÓPATA.
- **Letras pequeñas**: `PixelScene` gana una fuente de 3×5 con acentos y Ñ, porque en una cuña no cabe la grande.

**Quién lleva el reloj**, igual que en las otras dos escenas: el view model publica `CurrentPlay` (`RoulettePlay`) con
el instante de la tirada, y la escena se dibuja cada fotograma a partir de cuánto hace. El ángulo es una función pura
del tiempo (`RouletteTimeline.AngleAt`), así que salir de la pantalla a mitad de giro y volver la encuentra donde debe
estar. Se van los eventos `SpinRequested` y `RevealRequested`, las cuñas vectoriales (`RouletteSlotViewModel`) y la red
de seguridad de 15 s. La tirada sigue decidida, escrita en la partida y apuntada antes del primer fotograma (§87).

La celda es de tres píxeles como en todo lo demás, salvo que el panel no tenga alto para la rueda entera. Entonces
pasa a dos, siempre un número entero, porque un mapa de bits escalado se emborrona.

**Verificado:**

- Dos tiradas reales en la copia aislada del §170, pasada a LUDÓPATA con `RunService.ChangeRoleAsync` y con tres
  tiradas concedidas por `RouletteService.GrantAsync`, los dos con su evento y su motivo. Salieron «+1 MT» (confeti,
  MT95 entregada) y «Habilidad mala» (alarma roja, tres Pokémon con su habilidad nueva), con capturas de la app en cada
  fase.
- Trece pruebas en `RouletteMachineTests`, entre ellas:
  - la rueda para en la ganadora con los cinco perfiles, las seis ganadoras y nueve ángulos de partida, y se queda
    ahí;
  - nunca va hacia atrás durante el barrido ni se pasa más de 20°;
  - en el último medio segundo la cuña bajo la flecha es la ganadora;
  - ningún fotograma falla, en tres tamaños.

## §176 · Muestra del estilo en píxeles: el marco, la barra lateral y HOME (2026-09-23)

**Estado: MUESTRA, pendiente de que el jugador decida si se rehace toda la aplicación así.** Solo presentación: ni
una regla, ni un servicio, ni un punto cambian. Lo único nuevo que se lee es la lista de etapas del tope de nivel, y
sale de la misma cuenta que decide el tope.

Las otras escenas (§169, §171, §174, §175) están dibujadas en celdas y el resto de la aplicación seguía en vectores
con Bahnschrift. Se propusieron dos caminos, un marco en píxeles o todo en píxeles, y se eligió probar uno intermedio:
**piezas en píxeles para todo lo que se toca y escenas enteras solo para los momentos grandes**, con una fuente propia
legible. La muestra cubre lo que se ve siempre: el marco de la ventana, la barra de título, la barra lateral, la
cabecera de cada sección y HOME entero.

**El kit, en `Views/Pixel/`:**

- **`PixelFont`**: fuente propia de mayúsculas de siete celdas, minúsculas con descendentes, acentos, Ñ, ª, º, §,
  «», flechas y ♀♂, proporcional y con cifras tabulares. No es un TTF a propósito: WPF suaviza una fuente de píxeles a
  cualquier tamaño que no sea el suyo exacto, y al 125 % es a todos. Lo que no está en la tabla sale como una caja
  hueca, que se ve que falta, y no como otra letra.
- **`PixelText`**: el texto de esa fuente como elemento de verdad (mide, ajusta línea, recorta con «…», se enlaza),
  con la celda redondeada a píxeles enteros según el DPI.
- **`PixelIcons` / `PixelIcon`**: un icono de 12×12 en color por sección, con las mismas claves que los vectoriales
  (`IconHome`, `IconGacha`…); apagado si no es la sección en pantalla y dando saltitos si lo es.
- **`PixelSprite`**: un dibujo del cartucho a píxeles enteros. Una `Image` con escala 2 al 125 % son 2,5 píxeles por
  píxel y sale una columna de cada dos más ancha. Hace silueta, contorno de una celda, «piedra» para los caídos (como
  las figuritas del §169) y recorte de lo vacío, todo cocinado en un mapa de bits porque `OpacityMask` con
  `ImageBrush` no pinta (§31).
- **`PixelRule`** (surco), **`PixelBackdrop`** (fondo con una trama tenue) y `Themes/Pixel.xaml`: botones, botón
  sutil, botón de peligro, botones de la barra de título, elemento del menú y barra de desplazamiento.

**HOME:** la franja de estado ganó el **recorrido**: las doce pruebas, la liga y el rematch, cada una con el dibujo que
declara su logro en `Data/achievements.json` (el cristal Z de cada prueba, la Master Ball y la Gloria Ball), en color
las superadas, en silueta las que faltan y la que toca encendida y saltando, con el tope de cada etapa debajo. Sale de
`LevelCapTable` y de `ProgressService.ClearedAsync`, la misma cuenta que pone el tope, así que no pueden discrepar. El
equipo en vivo va a ×3 con contorno, en piedra si está debilitado y con la estrella dorada si es variocolor. Los caídos
van en piedra y con su forma.

**Verificado:** en la app real sobre la copia aislada del §170 (arranque, HOME, RULETA), y el equipo en vivo, que solo
sale con Azahar abierto, con un arnés que pinta `HomeView` con datos de ejemplo. Cinco pruebas en `PixelUiTests`: que
cada glifo es un rectángulo que cabe en la línea, que el texto que se escribe de verdad no tiene ninguna letra sin
dibujo, que ajustar línea nunca se pasa del ancho, que cada icono es de 12×12 con colores conocidos, y que **cada
sección tiene su icono** (sin él se cae al punto sin fallar). `HomeViewTests` se adapta a la vista nueva.

Si la muestra no convence, se vuelve atrás restaurando `MainWindow.xaml(.cs)`, `HomeView.xaml`, `App.xaml`,
`Services/Rolling.cs` y `HomeViewTests.cs`; los ficheros de `Views/Pixel/` y `Themes/Pixel.xaml` solo se quitan si el
jugador lo confirma.

### §176 bis · Más pulido, y el VISOR en píxeles (2026-09-23)

El jugador dio por buena la muestra y pidió dos cosas: **mucho más pulido**, y el **visor Pokémon**, que «se ve algo pobre».

**El kit gana tres piezas**, que notan todas las pantallas que lo usen:

- **`PixelWindow`**: un bloque de pantalla como una ventana del menú de un juego. La placa gana una **banda de título**
  (`PixelPanel.HeaderHeight`) con su icono y su nombre, una línea de tinta debajo, el acento que arranca y sitio a la
  derecha para una cifra o un botón. HOME pasa a usarla en EQUIPO EN VIVO, PARTIDA, CAÍDOS y ÚLTIMAS ACCIONES.
- **Pestañas** (`PxTabControl`), como las páginas de la pantalla de datos del juego.
- **Pulsar un botón es hundirlo**: la placa baja sobre su propia sombra y el texto con ella, en vez de moverse con la
  sombra detrás.

Y detalles: cada pantalla dice en la cabecera **qué es** (el subtítulo que la sección ya llevaba), con sombra dura
porque de día el cielo de Alola es claro; el historial lleva **el icono de lo que pasó** en cada línea
(`Services/EventIcons.cs`, con el mismo criterio que `DisplayNames`: lo que no tiene icono sale con el punto); la fuente
gana ◀ ▲ ▼; hay dos iconos nuevos (estrella de variocolor y cruz de caído); y «RoleChanged» ya sale como «Cambio de rol».

**El visor** es ahora el PC del juego, con los mismos datos y órdenes de antes:

- **El equipo** como el menú del equipo: una placa por Pokémon con su icono, nombre, nivel y sexo; roja y en piedra si
  la run lo cuenta como caído; y en la banda una ball del cartucho por hueco.
- **La caja** con su fondo, los treinta huecos más grandes y el cursor de esquinas. Debajo, **las 32 cajas en
  miniatura** con una barrita de lo llenas que están, para saltar a cualquiera de un clic.
- **La ficha**, con retrato al cuádruple sobre el fondo de su caja, **sus tipos en placas del color de cada tipo**, y
  tres páginas: **DATOS** (con lo que la naturaleza sube y baja), **ESTADÍSTICAS** (el nombre en rojo si la naturaleza
  la sube y en azul si la baja, como en el juego) y **ATAQUES**, cada uno en su placa del color de su tipo con categoría,
  potencia, precisión y PP.

Lo nuevo que se enseña sale de fuentes que ya existían: los tipos de `ITypeLookup`, los ataques del mismo catálogo que
MOVIMIENTOS (`IMoveCatalog`, del mundo instalado) y el efecto de la naturaleza de `IStatForecast`, el mismo que ENTRENAR
EV. Nada nuevo se escribe en la partida.

**Verificado** en la app real sobre la copia aislada del §170: visor sin nada elegido, con un Pokémon del equipo, con
uno de una caja y las tres páginas; HOME otra vez. Y **una prueba nueva carga el visor con los recursos reales del
tema**, para que un recurso mal escrito falle en los tests y no al abrir la pantalla (van en la misma prueba que HOME
porque WPF solo admite una `Application` por proceso). Los tests de HOME se adaptaron: el aviso de encuentro vive ahora
dentro de una ventana que sin enlace con el juego está plegada.

### §176 ter · Todas las pantallas en píxeles, y fuera ESTADÍSTICAS (2026-09-23)

El jugador aprobó la dirección y pidió **convertir el resto** y **quitar ESTADÍSTICAS**, que le parecía inútil.

**ESTADÍSTICAS sale del menú**: fuera de `MainViewModel.Sections`, de su plantilla en `MainWindow.xaml`, de su registro
en el contenedor y del enlace de JUGAR. Sus ficheros (`StatisticsView.xaml(.cs)`, `StatisticsViewModel.cs`,
`StatisticsService.cs`) siguen en el disco sin que nada los use, **pendientes de que el jugador confirme borrarlos**.
Nada de lo que contaba se pierde: la cadena de eventos de la que salía es la misma.

**Convertidas las catorce secciones que quedaban**: JUGAR, RANDOMIZADOR, GACHA, TIENDA, LOGROS, MAPA, ENTRENAR EV,
MOVIMIENTOS, POKE PASTE, CEMENTERIO, COMBATES, RULETA, MISCELÁNEA y MANTENIMIENTO. Solo presentación: mismos enlaces,
mismas órdenes, ni una regla tocada. Lo único añadido a un ViewModel es la fracción de la barra de un logro
(`AchievementRowViewModel.Share`), porque la barra en celdas toma una fracción y no un rango.

**El kit gana tres piezas más**: un campo de texto (`PxTextBox`, en monoespaciada dentro de un pozo, con su texto de
ayuda sacado de `Tag`), una casilla (`PxCheckBox`) y un desplegable (`PxComboBox`). Lo que se escribe no puede ir en la
fuente propia porque hay que poder poner el cursor entre dos letras. Y tres iconos: la marca blanca, la cruz roja y la
flecha de huida, que son las chinchetas del MAPA.

Decisiones que conviene saber:

- **El MAPA** conserva el dibujo de cada isla, que es el del cartucho, pero pierde el halo difuminado y el brillo en
  degradado que lo cruzaban: son justo lo que este estilo no hace. Las chinchetas pasan a placas de celdas con su icono.
- **ENTRENAR EV y MOVIMIENTOS** conservan el aspecto de la bolsa del §143 (naranja, cápsulas, cuero) y cambian solo la
  letra. Los botones de la bolsa pintan su texto con una plantilla para `string` en `Bag.xaml`, y no metiéndolo en cada
  botón, para que los que llevan un dibujo en vez de texto lo sigan pintando.
- **GACHA**: los portales de tier salen de la escena y van a la barra de abajo, junto al botón de TIRAR. Encima de la
  escena tapaban el letrero de neón, porque al compactar los banners la escena mide distinto y el neón sube.
- **La cabecera**: el título y el marcador de puntos van sobre una placa translúcida. Con el cielo de Alola de día el
  sol salía justo detrás del subtítulo y no se leía ni con sombra.

**Sin convertir todavía**, y dicho para que no se dé por hecho: las ventanas de diálogo (crear run, cambiar de rol,
registrar captura), la tarjeta de resultado del wonder trade, los controles del reproductor de la killcam y los nombres
bajo las tumbas del cementerio, que dibuja la escena con otra letra. La sección COMPETICIÓN no sale en la distribución
local y tampoco está convertida.

**Verificado** en la app real, sobre la copia aislada del §170, sección por sección y con un Pokémon elegido en
ENTRENAR EV y MOVIMIENTOS. Y la prueba de carga de `HomeViewTests` construye y mide ahora **las catorce** con los
recursos reales del tema, para que un recurso mal escrito falle en los tests y no al abrir la pantalla.

### §176 quater · ENTRENAR EV y MOVIMIENTOS sin bolsa, los diálogos, y los tipos del elegido (2026-09-23)

El jugador confirmó **borrar ESTADÍSTICAS** (ya no quedan sus cuatro ficheros) y pidió que **ENTRENAR EV y
MOVIMIENTOS dejen el naranja de la bolsa** del §143 y lleven el diseño general, que **enseñen los tipos** del Pokémon
elegido y que se conviertan también los diálogos.

**Las dos pantallas, con el mismo esqueleto que el VISOR**: a la izquierda `PokemonPickerPanel`, un panel propio que
comparten las dos -equipo en tarjetas de dos en dos con el cursor del juego, cajas con ◀ ▶ y la lista de la caja en un
pozo, RELEER LA PARTIDA abajo-; a la derecha ventanas `PixelWindow` (FICHA, EV; FICHA, SABE AHORA, PUEDE RECORDAR). En
ENTRENAR EV las filas son placas con barra en celdas, los `−`/`+`/MÁX/0 son botones del kit y el hexágono pasa a los
colores del tema. En MOVIMIENTOS los cuatro que sabe siguen siendo **del color de su tipo**, como en combate, y un hueco
libre sale hundido. Solo presentación: mismos enlaces y órdenes.

**Los tipos del elegido**: `TypeBadges.For` es ahora la única forma de convertir un Pokémon en sus placas de tipo, la del
visor incluida, con la forma regional contada (`ITypeLookup`). Los dos ViewModels la exponen como `SelectedTypes`.

**Los diálogos en píxeles**: crear run, cambiar de rol y registrar captura. El rol se elige con `PxRadioCard` (la tarjeta
entera es el botón, con el cursor alrededor de la elegida) y el selector de especie con **`PxEditableComboBox`**, que es
un estilo aparte porque un `ComboBox` editable sin `PART_EditableTextBox` deja de aceptar texto en silencio (§50). Y
además la tarjeta del wonder trade, los controles de la killcam (con `PxToggleButton` para CÁMARA LENTA; envuelven en vez
de cortarse en la columna estrecha del cementerio) y los nombres bajo las tumbas, que ahora son letra de píxeles a una
celda por punto y salen nítidos al ampliarse la escena. La letra de los campos de texto sube de 13 a 15.

**Borrada con confirmación del jugador**: la bolsa (`Themes/Bag.xaml`, `BagPocketPanel.xaml(.cs)` y
`BagPixels.cs`). `ZeroToVisibleConverter` salió de `BagPocketPanel.xaml.cs` a `Converters/` para poder borrarlo entero.
Sigue sin convertir COMPETICIÓN, que no sale en la distribución local.

**Verificado**: las dos pantallas con un Pokémon elegido y el cementerio con su killcam, en la app real sobre la copia
aislada del §170, que ya tiene la capa de gen 8-9 (con la carpeta vacía faltaban los dibujos de Raboot, Finizen,
Rookidee y demás; en la carpeta del jugador están los 1025). Los diálogos, en `HomeViewTests`: se construyen sin su
ViewModel con datos de muestra y se miden, y con `PERMALOCKE_SNAP_DIR` se guardan en PNG para mirarlos. 1521 pruebas.

## §177 · Limpieza pedida por el jugador, y dos fallos del kit pixel que rompían botones y casillas (2026-09-24)

Quitado a petición del jugador:
- **CEMENTERIO**: el bloque ANTE QUIÉN, CUÁNDO, DE DÓNDE VENÍA y CÓMO SE SUPO. En él salía «#822», un nombre de gen 8-9
  sin resolver, y ya no se enseña. `CemeteryViewModel` sigue calculando esos textos, pero nadie los pinta.
- **COMBATES**: el botón COMPROBAR.
- **MISCELÁNEA**: +10 ESCAMAS CORAZÓN, porque el recuerda-movimientos va dentro de la app (§142). Los ajustes de avisos
  pasan a CONFIGURACIÓN.
- **HOME**: sin CAÍDOS, sin ÚLTIMAS ACCIONES y sin CAMBIAR el rol, que ahora solo se ve como distintivo. En su lugar
  hay IR A: accesos a ocho secciones por su título, con `HomeViewModel.GoCommand`, `NavigateRequested` y
  `MainViewModel`. `ChangeRoleWindow` y `ChangeRoleViewModel` siguen en el código sin ninguna entrada.
- **MANTENIMIENTO**, borrada (`MaintenanceViewModel` y `MaintenanceView`). La sustituye **CONFIGURACIÓN**
  (`SettingsViewModel`, `SettingsView`), con:
  - tamaño de ventana;
  - avisos encima del juego;
  - escena de muerte (`DeathCeremony.Enabled`);
  - grabar killcams (`KillcamRecorder.Enabled`);
  - minimizar al abrir el juego;
  - VER UN AVISO;
  - abrir las carpetas.

  Se guarda en `Config/ajustes.json` con `AppSettings`, que se carga al arrancar. Antes los avisos y el minimizar se
  olvidaban al cerrar. Son preferencias y no generan eventos. `MaintenanceService` se queda porque lo usa el
  vigilante. Las reparaciones siguen en `tools/PermaLocke.Probe`.
- **GACHA**: la escalera de probabilidades pasa a una fila propia a todo el ancho, con cinco columnas iguales. Cada
  una lleva nombre, % grande (o NO SALE), la banda de total base escrita «HASTA 400 / 401-490 / 591 O MÁS» y el texto
  «pulsa para ver quién sale». Antes la última se cortaba y la banda salía como «→ 0400», porque la letra pixel no
  tiene «≤» ni «→».

**Dos fallos de fondo del kit, que explican «JUGAR no va» y «las pestañas del visor no van»**:
1. `PixelPanel` y `PixelText` son `IsHitTestVisible = false`. Una plantilla cuya raíz no tiene `Background` **no
   recibe ni un clic**. Estaban así:
   - el botón JUGAR (`LauncherView`, `PlayButton`);
   - las pestañas (`PxTabItem`);
   - los portales del gacha;
   - las chinchetas del MAPA;
   - el regalo y el JUGAR de la cabecera (`MainWindow`);
   - los huecos de `PxTextBox` y `PxEditableComboBox`.

   Todos llevan ahora `Background="Transparent"` y se pueden pulsar en toda su superficie. **Regla: toda plantilla
   interactiva hecha con piezas pixel necesita fondo transparente en la raíz.**
2. `PixelPanel` no pinta nada por debajo de 6×5 celdas (18×15 px a escala 1). La marca de `PxCheckBox` medía 15 px, así
   que **ninguna casilla enseñaba nunca que estaba marcada**. Ahora es un `Border` liso. El punto de LO QUE DA CADA
   PRUEBA pasa de 15 a 18 px.

Verificado en la copia aislada: capturas de HOME, GACHA, CEMENTERIO y CONFIGURACIÓN, y 1521 pruebas. Los clics no se
pueden probar con UI Automation, porque `Invoke` se salta el hit-test: la causa está confirmada leyendo el código y
falta verlo con el ratón.

## §178 · HOME sin IR A, el GACHA con la máquina a la vista, y textos sin relleno (2026-09-24)

- **HOME**: fuera IR A. Queda PARTIDA sola, a todo el ancho y ajustada a su contenido.
- **GACHA**:
  - La escena de la máquina se queda con el alto. Los banners pierden la línea de descripción y la barra de abajo es
    una fila: TIRAR, las cinco probabilidades a lo ancho y, a la derecha, los puntos y PREMIOS POR PRUEBA. Con eso la
    escena se ve entera, con el suelo y la alfombrilla, que antes quedaban cortados.
  - El bote («quién puede salir») es un panel grande sobre la mitad izquierda, de arriba abajo. Tapa la estantería
    y la máquina, que al mirarlo no hacen falta, y deja a la vista la alfombrilla con la ball y su ficha.
  - Las celdas de probabilidad llevan ball, % (o NO SALE) y banda «0-400 / 401-490 / 591+»; el nombre del tier va en
    la etiqueta emergente. Los botones de los portales tienen `AutomationProperties.Name` para poder probarlos.
- **VISOR**: las estadísticas de un Pokémon en caja salen de `IStatForecast` con la tabla del mundo instalado, las
  mismas que MOVIMIENTOS. Fuera el aviso amarillo de «pueden no coincidir».
- **Textos de premio**:
  - La ruleta ya no escribe «MT32 Tajo Aéreo: 0 → 1» sino el nombre del objeto, «×N» si son varios o «Pierdes X»
    (`SaveRouletteWorld`).
  - Las fichas del gacha y del wonder trade ya no enseñan «Nv. · naturaleza» ni «IV total»: queda la habilidad.
- **Probe `--credito`** acepta `PERMALOCKE_ROOT` para apuntar a otra instalación. Con eso se dieron al jugador 5
  tiradas por banner en `PermaLocke prueba`, cada una como su propio `AdminAdjustment` con motivo. Pocho recibió 7
  porque estaba en −2: tenía 7 tiradas gratis gastadas contra 5 ganadas.

## §179 · La Colina Saltagua y la Jungla Umbría, cerradas hasta tener su cristal Z (2026-09-24)

A petición del jugador, dos pruebas más se tratan como la primera (§160), y con una regla más dura: **hasta que el
cristal Z de la prueba esté en la mochila, en esa zona no hay Poké Balls** (salvo un variocolor) y un combate no
gasta la ruta. Con el cristal, la zona es una ruta normal.
- **Prueba 3, Colina Saltagua**: `trialZones` pasa de la sala del Dominante a `colina-saltagua` (logro `prueba-03`,
  Aquastal Z 809). La **Colina Saltagua (Sala del Dominante)** sale del MAPA: va a `sinEncuentros` de
  `marcadores.json` porque ahí no se atrapa nunca.
- **Prueba 5, Jungla Umbría**: `jungla-umbria` con `prueba-05` (Herbastal Z 811). El §160 la había dejado fuera a
  propósito, porque la regla antigua permitía descartar encuentros antes de la prueba; con las balls retiradas ese
  riesgo desaparece.
- En el código: `EncounterSituation.PendingTrial`. `EncounterPolicy` retira las balls y no gasta la ruta, tanto
  andando como en combate, con el mensaje «primero supera la prueba (…)». `EncounterGuard` lo pasa desde
  `TrialZoneService.PendingAsync`, que mira el cristal en la mochila viva, en las dos decisiones. La Cueva Sotobosque
  (prueba 1) recibe la misma regla: allí el juego ya no dejaba lanzar Poké Balls.
- Prueba: `EncounterPolicyTests.A_trial_zone_before_its_crystal_has_no_balls_and_spends_nothing`. Se copiaron
  `rules.json` y `marcadores.json` a la carpeta de prueba, comprobando antes que solo diferían en estos cambios.

**Sin jugar todavía.** Falta ver cómo lee el juego la zona dentro de la Jungla Umbría durante la prueba.

## §180 · Iconos variocolor: colores medidos en renders de referencia y aplicados al icono del cartucho (2026-09-25)

El cartucho **no tiene iconos variocolor**: `a/0/6/2` lleva un dibujo por forma, y el juego enseña los colores del
variocolor solo en el modelo 3D. Ahora la app pinta en variocolor el icono de todo Pokémon que lo sea: VISOR, ENTRENAR EV,
MOVIMIENTOS, HOME, JUGAR, GACHA, WONDER TRADE, CEMENTERIO, escena de muerte y aviso de caída.

**De dónde salen los colores.** Pokémon Showdown publica renders `dex` y `dex-shiny` del mismo encuadre, píxel a píxel. Juntos
dicen en qué color se convierte cada color del cuerpo. `ShinyPalette` (en `Randomizer/Sprites`, sin WPF) lleva ese cambio al
icono:
- empareja cada color del icono con los del render en LCh, dando más peso al tono que a la luz, porque el icono está iluminado
  de otra manera;
- aplica el cambio de forma relativa: gira el tono, escala el croma y desplaza la luz;
- toma la **mediana** de los 16 vecinos, no la media. Con la media, el cuerpo y la llama de Charizard (mismo tono) se mezclaban
  en un marrón que no es de ninguno de los dos.

El peso de la luz, **1,5**, se eligió barriendo valores sobre Charizard: es el que separa cuerpo y llama.

**Lo que se guarda es una tabla de colores, no un dibujo.** `RomTool variocolor` genera `Data/variocolor.json`: por índice de
icono, `RRGGBB>RRGGBB` para cada color. `PokemonSpriteService.Get(especie, forma, variocolor)` repinta con esa tabla el icono
de la ROM del propio jugador. No se reparte nada de Nintendo ni de Showdown. Vale para todos los jugadores porque los iconos
son los mismos: cartucho más el mod 1.4.

**Fuentes y parejas que no sirven**, todo medido:
- `dex` no tiene casi nada de gen 9: su `dex-shiny` es una copia del normal. Se cae a `home`/`home-shiny` (renders de HOME).
- 16 parejas de `dex` están **en otra pose** (Ogerpon, Naganadel, Poipole, Xerneas, Zeraora…). `ShinyPalette` exige un
  solape de siluetas de 0,9. Medido en las 1246 parejas descargadas: esas 16 quedan por debajo de 0,84 y el resto en 0,92 o
  más.
- Resultado: **1123 iconos con tabla y 6 sin referencia usable** (Basculin raya blanca, Eiscue sin hielo, Maushold familia de
  cuatro, Armarouge, Ceruledge y Terapagos astral). Esos seis se dibujan en sus colores normales. La lista va en
  `sinReferencia` del JSON.

**Es una aproximación y así se dice.** Un color del icono que el render no tiene toma el cambio del más cercano. Revisadas a
ojo las 12 hojas (`%TEMP%/permalocke-variocolor`, con `leyenda.txt`): ningún icono roto ni sin contorno. Algunos pálidos, como
Zygarde o Guzzlord, son correctos: sus variocolores son blancos de verdad.

**Pruebas:** `ShinyPaletteTests` cubre un cambio que se lleva, lo que no cambia se queda, renders iguales, poses distintas y
tamaños distintos. `PngImageTests` cubre `Decode`, el decodificador PNG nuevo que usa RomTool. Además se comprobó el
servicio real con la ROM: Charizard negro, Gyarados rojo, y Armarouge devuelve su icono normal.

**Sin ver dentro de la app con un variocolor de verdad.**

## §181 · El Pokémon que te sigue: el plugin de otro autor, instalado al pulsar JUGAR (2026-09-25)

El jugador creía que el mod de gen 8-9 traía un Pokémon que te acompaña andando. No lo trae. Es otro mod del mismo
autor: **Pokemon Follower Mod (Includes SM)**, de Aqua_ (gamebanana.com/mods/694400), con licencia **CC BY-NC-ND 4.0**.
El propio mod de gen 8-9 lo menciona en sus preguntas frecuentes, y dice que no garantiza compatibilidad total entre los dos.

**Qué es.** Un plugin **3GX** (`Gen7FieldFollower.3gx`, 693 KB) que corre dentro de la consola emulada. Hace que el
primero del equipo ande detrás del jugador y reaccione con A. Toca solo el modelo del mapa: ni el equipo, ni el combate, ni
la partida. Se instala en `sdmc/luma/plugins/00040000001B5100/` y necesita el cargador de plugins del emulador. Nuestro
fork lo tiene.

**Probado antes de integrarlo**, en una copia aislada con emulador y partida copiados:
- El Dragonite de cabeza sale detrás del jugador.
- `Probe --equipo` sigue leyendo las dos estructuras del equipo, con el espejo en `0x330128E4` como siempre.
- El `FieldZoneReader` real da Ciudad Malíe con 3 registros de posición, también andando.
- El jugador probó en esa copia Pokémon de gen 8-9 de cabeza, hierba y combates, y todo fue bien.

Por eso lo de «no garantiza compatibilidad» no se tradujo en nada visible.

**Dos trampas medidas:**
- **Azahar ignora `plugin_loader=true` si `plugin_loader\default` sigue a `true`.** El log dice
  `System_PluginLoader: false` y no carga nada. Hay que escribir las dos claves, como con el RPC.
- **Una ruta de más de 260 caracteres rompe la partida sin avisar.** La primera copia vivía en el scratchpad. La ruta de
  la partida pasaba del límite: la copia no se hizo y el juego arrancaba como partida nueva. Las copias de prueba van en
  rutas cortas.

**Integración.**
- `AzaharInstallation.SetFollower` copia el plugin si falta o es distinto y escribe las dos claves. Apagado solo apaga
  el cargador: el fichero se queda y no se borra nada. Sin plugin en el reparto, no enciende nada.
- `EmulatorLauncher.Launch` lo llama al pulsar JUGAR, con el emulador aún cerrado, junto al RPC y a la confirmación de
  cierre.
- CONFIGURACIÓN tiene «Pokémon que te sigue», encendido por defecto, en `Config/ajustes.json` (`Follower`).
- El plugin viaja en `Emulator/follower/` con sus créditos en `LEEME.txt`, que es lo que pide la licencia. El binario no
  se versiona, como el resto del emulador. `publicar.ps1` lo exige en el reparto.
- Pruebas en `AzaharInstallationTests`: se instala y enciende de verdad las dos claves; apagar no borra; sin plugin no
  toca la configuración.

**Límites.**
- Solo se aplica si el juego se abre desde JUGAR. Un Azahar abierto a mano se queda como lo dejó la última vez.
- El cambio de forma con L+A del propio plugin puede colgar el juego, según su autor. No se puede desactivar desde fuera.

## §182 · Las pruebas se ven en LOGROS sin guardar dentro del juego (2026-09-25)

El jugador superó la séptima prueba y LOGROS no la enseñaba. Justo antes se había instalado el Pokémon que te sigue
(§181), así que parecía cosa suya. **No lo era.** La partida guardada tenía el **Electrostal Z (810)**, leída con PKHeX
sobre una copia, y el plugin no toca la partida.

La causa venía de antes: los logros con `item` (las doce pruebas) **solo miraban el fichero de partida**
(`SaveRecordReader`). Una prueba superada no contaba hasta **guardar dentro del juego** y volver a abrir LOGROS, que se
relee al entrar y no mientras está abierta. Mientras tanto, la regla de la zona de prueba (§179) ya la daba por superada
porque mira la **mochila viva**. Dos partes de la app contestaban distinto a la misma pregunta.

Ahora `AchievementService` une lo del fichero con lo que dice la mochila viva (`IItemDelivery.CarriedAllAsync`, una sola
consulta con los ids del catálogo). Es la misma forma que los premios del §68.
- **La unión no puede contar de más**, porque estos objetos el juego los da y no los quita.
- **Una mochila que no responde no quita nada**: queda lo del fichero.

Pruebas en `AchievementServiceTests`:
- un cristal solo en la mochila viva desbloquea su prueba;
- una mochila viva sin el cristal no borra lo que tiene la partida.

Sigue haciendo falta **reentrar en LOGROS** para ver el cambio si la pantalla ya estaba abierta.

## §183 · Los fantasmas: cuando a alguien se le muere un Pokémon, lo ven los demás (2026-09-26)

Idea del jugador. Cuando un Pokémon muere:
- **en la pantalla de quien lo pierde**, la escena de muerte termina con su fantasma saliendo del charco, subiendo y
  yéndose **por la izquierda**, como si pasara a las pantallas de los demás;
- **en la de los demás**, y **solo si tienen Azahar abierto**, sale un aviso arriba a la izquierda («Juanega ha perdido
  a Dragonite», nivel y zona). Cuando se va, el fantasma cruza el emulador de derecha a izquierda.

Varios seguidos salen en cola, uno tras otro. Todo se apaga con una casilla, «Fantasmas», en CONFIGURACIÓN.

**Cómo viaja.** Por el servidor del torneo, en una tabla nueva, `fantasmas` (`tools/supabase/12-fantasmas.sql`,
**hay que ejecutarla en Supabase**):
- cada jugador solo escribe los suyos (`jugador = auth.uid()`) y los de la lista leen todos;
- la muerte se envía **al momento**, desde `PlayNotifications` sobre `GameLinkMonitor.PokemonDied`, no con la subida de
  la run, que va cada 2 minutos;
- los demás la piden cada **10 s**, y solo con el juego abierto.

Al cerrar el juego se olvida el puntero, así que al volver a abrirlo no se repiten las muertes de toda la tarde. La tabla
es solo la noticia: la muerte sigue siendo el `PokemonDied` de la run de quien la sufrió.

**Piezas:**
- `GhostService`: envía, lee, lleva la cola y reproduce.
- `GhostWindow`: el aviso con el dibujo de siempre de los avisos, `ToastKind.Ghost` con pestaña celeste y sprite en
  gris, y el cruce del fantasma.
- `GhostArt`: el sprite del cartucho en azul pálido, con el contorno claro macizo y las líneas oscuras del icono macizas
  para que se le reconozca.
- `DeathWindow.AddGhost`: el fantasma en la escena propia, en la misma rejilla de celdas que el sprite y la sangre, que
  se mueve de celda en celda.
- `DeathNotice.Level`: nuevo, para decir el nivel.

**Lo que se probó y cómo.** Con `--sin-juego --ensayar-fantasma`, nuevo, que enseña el fantasma de un amigo usando el
último caído de la run y sin servidor, y con `--ensayar-muerte`. Capturas de pantalla durante la animación:
- el aviso sale arriba a la izquierda;
- el fantasma cruza;
- en la escena propia sube del charco y sale por la izquierda antes de que se vaya el título.

**Trampa medida:** la primera versión del fantasma usaba una trama de ajedrez para la transparencia. A 8 píxeles de
pantalla por celda se leía como un tablero y no como un Pokémon, así que se pasó a una transparencia lisa.

**Sin probar:** el viaje por el servidor entre dos PCs, porque la tabla no existía todavía. Si falta, los fallos se
apuntan una vez y no cada 10 s (§167).

## §184 · La lluvia de sangre: cuando alguien pierde el equipo, llueve en todos los emuladores (2026-09-26)

Idea del jugador, sobre el camino de los fantasmas (§183). Cuando a alguien se le cae el equipo entero (`TeamWiped`):
- **en su pantalla** llueve sangre al momento, sin esperar al servidor y sin aviso nuevo, porque ya salen la escena de
  EQUIPO CAÍDO y el aviso de siempre;
- **en la de los demás**, y **solo si tienen Azahar abierto**, sale un aviso arriba a la izquierda («Juanega ha perdido el
  equipo entero · Llueve sangre.», pestaña EQUIPO CAÍDO y lápida) y llueve sangre encima del emulador.

La lluvia dura **12 s** (eran 30; el jugador la acortó tras verla): empieza floja, arrecia en 1,5 s, deja de caer a los
9 s y se apaga en cuartos en los dos últimos.
Las gotas son de la misma celda que el fantasma (`Math.Max(3, alto/140)`), tres rojos lisos, salpican donde caen y
encharcan el fondo (hasta 1/18 del alto; crece el doble de rápido desde que dura 12 s). Hay un velo rojo liso muy fino (alfa `0x34`) para que el juego se vea debajo.
Sin degradados ni suavizado, a pasos de 45 ms como el fantasma. Va en la **cola de los fantasmas**: el fantasma del último
caído cruza primero y luego llueve. La apaga la misma casilla «Fantasmas» de CONFIGURACIÓN.

**Cómo viaja.** Tabla nueva `lluvias` (`tools/supabase/13-lluvias.sql`, **hay que ejecutarla en Supabase**), con las
mismas reglas que `fantasmas`: cada uno escribe las suyas y los de la lista leen todas. Es aparte a propósito: una app de
antes que leyera una fila de wipe en `fantasmas` enseñaría un fantasma vacío. Se lee en la misma consulta de cada 10 s,
**en su propio try**: si la tabla aún no existe, los fantasmas siguen llegando y el fallo se apunta una vez (§167).

**Piezas:**
- `Views/BloodRain.cs`: la lluvia celda a celda, sin WPF (`Advance`/`Draw`, `Pixels` en Bgra32). Pruebas en
  `BloodRainTests` (roja, velo fino, charco, nada al acabar, misma semilla misma lluvia, tamaños mínimos).
- `GhostWindow.RainAsync`: un `Image` con `WriteableBitmap` debajo del fantasma y del aviso, escalado a vecino más
  próximo. `PlayAsync` se partió en `ShowOver` y `CardAsync` para compartirlos.
- `GhostService.Rain` (envía y encola la propia, pasando al hilo de la pantalla porque el vigilante avisa desde el suyo),
  `ReadRainAsync`, `RehearseRainAsync`. `PlayNotifications` la llama en `TeamWiped`.

**Ensayo:** `--sin-juego --ensayar-lluvia`, la de un amigo sin servidor ni juego.

**Sin probar:** escrito desde un contenedor sin .NET (la red no deja bajar el SDK): **ni compilado, ni ejecutadas las
pruebas, ni visto**. Los umbrales de `BloodRainTests` se comprobaron con la misma simulación pasada a Python. Tampoco el
viaje entre dos PCs.

## §185 · «Jugando a PermaLocke» en Discord (2026-09-26)

Petición del jugador: que en su perfil de Discord salga **solo** «Jugando a PermaLocke» con el icono de la app, siempre
que PermaLocke esté abierto y **también con el emulador abierto**, por delante de él. Sin ruta, puntos ni equipo.

- `App/Services/DiscordPresence.cs`: habla con el Discord del PC por su tubería local (`discord-ipc-0` a `9`), sin
  librería nueva y sin pasar por el servidor del torneo. Saludo con el id de la aplicación, `SET_ACTIVITY` con una
  actividad vacía (`instance: false`, y la imagen si se configura) y la tubería abierta mientras dure la app: al cerrar
  PermaLocke, Discord quita el estado. Sin Discord abierto, o si se reinicia, reintenta cada 20 s; los fallos se apuntan
  una vez (§167). Arranca en `App.OnStartup` (también con `--sin-juego`) y se para en `OnExit`.
- **El texto lo pone Discord**: «Jugando a» + el nombre de la aplicación de Discord cuyo id va en `discordApp` de
  `Data/torneo.json`. El nombre y el icono se ponen en el Discord Developer Portal, no en el código. `discordImagen`
  (opcional) es el nombre de una imagen subida en Rich Presence > Art Assets, por si el icono de la aplicación no sale
  solo. Sin `discordApp` no hace nada.
- **Por delante del emulador:** Azahar puede poner su propio «Jugando a Azahar». `AzaharInstallation.DisableDiscordPresence`
  escribe `enable_discord_presence=false` (y su `\default`) en `[UI]` de `qt-config.ini` al lanzar el juego, junto al
  RPC y la confirmación de cierre. Prueba en `LauncherEmulatorTests`. Si además Discord detecta `azahar.exe` como juego
  por su cuenta, eso se quita en Discord (Ajustes > Juegos registrados), no desde aquí.

**Visto (2026-09-26):** el jugador lo compiló y sale «Jugando a PermaLocke» con la actividad vacía, sin tiempo ni texto.
**Requisito del lado de Discord:** con «Compartir tu actividad detectada con otras personas» apagado (Ajustes >
Privacidad de la actividad) no sale nada y la app no se entera: no hay error que apuntar. Es lo primero que mirar si a
alguien no le sale. Sin probar todavía: con el emulador abierto a la vez.

## §186 · ÁLBUM: los Pokémon de la partida como cartas TCG en una carpeta (2026-09-26)

Idea del jugador, a partir de la idea de los «cromos». Una sección nueva, **ÁLBUM**, en el grupo EQUIPO después del
VISOR. **Solo para mirar**: no escribe nada, no tiene wonder trade ni entrenamiento. Lee la partida como el visor
(`IBoxReader`), así que funciona con el juego abierto o cerrado (`GameNeed.Either`) y enseña lo último guardado.

**La carpeta.** Dos páginas abiertas con fundas de plástico, anillas en el lomo. Cada caja con Pokémon (y el equipo
primero) empieza en una doble página nueva; el hueco de una funda es el del PC, con sus vacíos (`AlbumPaging`). Arriba:
◀ caja ▶, «PÁGINAS 1-2 DE 4», el resumen (cartas, variocolor, quemadas) y los botones **3×3** (carta completa) y
**4×4** (carta pequeña, sin movimientos), que el jugador quería probar. Se pasa página con las flechas, la rueda del
ratón o ◀ ▶: la hoja se estrecha hacia el lomo y se abre al otro lado, a columnas enteras. Al pasar el ratón la carta se
levanta de su funda; con un clic sale **en grande** (hasta 5 píxeles por celda), flotando, y al pulsarla **se da la
vuelta** y enseña su ficha. ◀ ▶ recorren las cartas de la caja; Esc o un clic fuera la devuelven.

**La carta** (`TcgCard` = datos, `TcgCardArt` = dibujo, puro y probado): pixel art a celdas enteras, sin degradados
(tramado ordenado). Borde amarillo de carta de siempre; panel y fondo del dibujo del color de su **primer tipo**, con un
motivo por tipo (llamas, olas, hierba, rayos, noche con luna…). Arriba la **fase** («BÁSICO», «FASE 1», «FASE 2», de su
línea evolutiva en `ISpeciesStatsCatalog.Lines`) y la **energía** de sus tipos (un pictograma por tipo); el nombre (mote
o especie); el icono del cartucho con contorno y sombra; la tira con **NV** y **PS**, y la **rareza**; y sus cuatro
**movimientos** como ataques, con la energía de su tipo. La pequeña lleva nombre, dibujo, NV, PS, energías y rareza. El
**reverso** es la ficha: habilidad, naturaleza (con lo que sube y baja), objeto, estadísticas con barra de IV (dorada si es
31) y EV, dónde y a qué nivel se capturó. **Nada inventado**: sin daños de ataque, debilidades ni retirada, que el juego no
tiene (regla 3).

**Rareza** (petición del jugador): si salió del **gacha**, el tier de su tirada (`rareza` del evento `GachaRoll`, por PID);
si no, el tier que le toca a su especie por la regla del gacha, **`GachaService.TierOf`**: la banda de la forma final de
su familia, o el tier de los legendarios si algo de la familia lo es (pruebas en `GachaServiceTests`). Símbolos: ● tier 1,
◆ 2, ★ 3, ★ plateada 4, ★ dorada 5, y una cápsula pequeña delante si vino del gacha.

**Especiales:**
- **Variocolor:** lámina holográfica en el dibujo, con rayas de colores fijas, una franja de arcoíris que la cruza cada
  2,6 s y destellos (`TcgCardArt.Animate`).
- **Caída** (por PID en la run, petición del jugador «arrugada o medio quemada»): sin color con algo de sepia, tres
  dobleces, textura de pliegues, y **quemada desde una esquina** (según su PID) con borde carbonizado y **brasas que
  parpadean**; lo quemado no existe y se ve la funda. Se quema entre un 35 y un 40 %. Por detrás, la misma quemadura en la
  esquina contraria.
- **Huevo:** la carta boca abajo, el dorso violeta con una Poké Ball y «HUEVO».

**Piezas:** `Views/TcgCard.cs` (datos y `CellCanvas`), `Views/TcgCardArt.cs`, `Views/AlbumScene.cs` (páginas, fundas,
lomo, vuelta de hoja; guarda el dibujo de páginas y cartas quietas), `Views/AlbumStage.cs` y `Views/CardStage.cs`
(controles WPF, celdas enteras centradas en píxeles enteros), `Views/AlbumView.xaml`, `ViewModels/AlbumViewModel.cs`.
`PixelScene.SmallGlyph` expone la letra pequeña (con `: / ' , ( ) ·` nuevas); en las cartas la N va a 4 celdas porque la
de 3 se leía D («LADZALLAMAS»). Pruebas: `AlbumTests` (paginado, cartas sin huecos, quemadas, holo que se mueve, huevo,
funda bajo el ratón, vuelta de hoja sin agujeros) y `HomeViewTests` carga la vista.

**Cómo se hizo desde la nube, sin Windows:** el SDK de .NET 10 se instaló con `apt-get update && apt-get install
dotnet-sdk-10.0` (Ubuntu 24.04 lo trae), la app compila en Linux con `-p:EnableWindowsTargeting=true`, y el dibujo de
cartas y álbum se ejecutó en un programa de consola aparte (con `Color` sustituido) que sacaba PNG para revisarlos: así se
corrigieron el «PS» encima de la placa de fase, la N, el reverso cortado y los pictogramas de Dragón y Volador.

**Sin probar:** la sección dentro de la app (WPF no corre en Linux), el ratón, el teclado y la animación en pantalla, y
las cartas con los iconos reales del cartucho (en las imágenes de prueba había criaturas dibujadas a mano). Las pruebas de
`AlbumTests` compilan pero se ejecutan en Windows.

## §187 · ÁLBUM premium: carpeta de piel, acabados por rareza y la carta en la mano en 3D (2026-09-26)

Petición del jugador: transformar el álbum (§186) en algo «muchísimo más impresionante», con acabado de producto y sin
perder el pixel art ni ninguna función. Dirección elegida: **«colección nocturna del ultraespacio»** — una carpeta de
coleccionista forrada de piel violeta bajo una lámpara de escritorio, cartas con los acabados de las tiradas de verdad, y
una inspección de carta como la de Balatro.

**Primero, un fallo de tamaño.** La sección pedía `GameNeed.Either`, y con él la app pinta la franja «ABIERTO O CERRADO»
(unos 74 px). Con la cabecera y la barra, la doble página de 314 celdas no cabía a 2 px por celda en GRANDE y habría
bajado a 1 px: diminuta. Ahora `Needs => None` (solo lee la partida) y el aviso de partida vieja va pequeño en la barra.
La barra es más baja, y `AlbumStage` elige: carpeta vestida si cabe a los mismos píxeles por celda que la desnuda; si no,
sin tapa ni pestañas. Prueba: ambas medidas caben en 540 × 320 celdas.

**Las cartas** (`TcgCardArt`):
- **Acabados por rareza** (`TcgFinish`), como el ★ de una carta real: ● y ◆ papel; ★ **holo** (el dibujo es una lámina con
  estrellitas que titilan); ★ plata **holo inversa** (brilla el panel, con trama plateada, y el dibujo no); ★ oro borde
  **dorado grabado en cruz** y dibujo holo; **variocolor** polícroma en toda la carta, borde incluido, con estrella junto
  al nombre. Las caídas pierden el acabado en el fuego.
- La lámina sabe dónde está: el lienzo apunta la **región** de cada celda (dibujo, panel, borde; `CellCanvas.Regions`) y
  lo que se pinta encima —texto, figura— la borra solo. La luz nunca lava un nombre (probado).
- La luz del acabado (`Animate`) recorre la carta con una pausa entre pasadas o **va a donde se le diga**: la carta en la
  mano le pasa la inclinación. Bordes de la franja en tramado; brillo plateado, dorado o arcoíris según el acabado;
  destellos en dorada y variocolor.
- Detalle: panel más claro arriba fundido a tramado, borde con grano diagonal, nombre con sombra de relieve, energías con
  sombra, tira NV/PS en su placa, filas de movimientos alternas; el dorso con banda del color del tipo y las estadísticas
  en un pozo. Humo que sube de las brasas de las caídas.

**La carpeta** (`AlbumScene`, reescrita):
- Tapa de **piel** con grano y **pespunte**, **cantoneras doradas** remachadas, **pestañas índice** de colores en el canto
  (EQ y el número de cada caja; la abierta sale más y se enciende; se pulsan para ir a esa caja, `OpenTabCommand`).
- Cada página lleva **cabecera impresa** («CAJA 3 · NOMBRE») y su **número**; las fundas vacías, el **contorno de una Poké
  Ball** como en las carpetas de verdad; anillas más gruesas; lomo hondo.
- **Luz de lámpara** horneada en un mapa (más clara arriba a la izquierda, más oscura en las esquinas lejanas, a
  tramado) sobre todo; cada siete segundos un **destello** cruza el plástico.
- La carta bajo el ratón **se levanta** con su sombra en la página y la funda latiendo en violeta, por encima de la luz.
- **La página gira en perspectiva**: la hoja rota sobre el lomo, su borde libre crece al acercarse, se oscurece al ponerse
  de canto y echa sombra en la que destapa (0,62 s, inversa de la proyección por celda).

**La carta en la mano** (`HandScene` puro + `CardStage`), a la resolución de la pantalla:
- Proyección de un plano girado en 3D (homografía invertida por píxel): cada píxel toma el color de **una** celda, así que
  los píxeles de la carta siguen cuadrados y nítidos y solo se inclinan con ella.
- **Sale volando de su funda** (el `AlbumStage` dice dónde estaba, `CardOpening` → `FlyFrom`), en arco, girando una vuelta
  entera y creciendo con rebote. **Se inclina hacia el ratón** con un muelle amortiguado (y se mece sola si no está
  encima); la lámina y un **reflejo** siguen la inclinación. **Clic: media vuelta en 3D** con rebote para ver el dorso.
- Alrededor: **rayos** lentos del color del acabado detrás de las raras, **motas de polvo** en la luz (ceniza y chispas
  junto a una caída), **estallido** de destellos al aterrizar una rara, y su **sombra** en la mesa con la forma real (lo
  quemado no hace sombra). Solo se copia a la pantalla el rectángulo que cambia.
- Debajo, el título de la carta («DRAGONITE · RARA DORADA · DEL GACHA», `AlbumViewModel.TitleOf`) y cómo usarla.

**Para comprobar sin Windows:** `tools/PermaLocke.PixelCheck` compila los ficheros puros del álbum tal cual (con un
`Color` de sustituto) y ejecuta las mismas `AlbumTests`; con `PERMALOCKE_PIXEL_DIR` deja PNG del álbum, la página
girando, los acabados y la carta en la mano. Para eso salieron de sus ficheros WPF, sin cambiar nada: la letra pequeña
(`SmallFont`), la tabla de colores de tipo (`TypeColours`, de la que `TypePalette` hace sus pinceles), las celdas de
`RoomSprite` y el paginado (`AlbumPaging`).

**Verificado:** solución entera compilada en Linux sin avisos; Core 380, Rules 151, Randomizer 485, GameLink 389 y
PixelCheck 45 correctas; imágenes de cada pieza revisadas (así se corrigieron la sombra de las caídas, el dorado que no se
distinguía del amarillo, la cápsula encima de los PS y el reflejo en mitad de la carta). **Sin ver dentro de la app**: el
ratón, los muelles, el vuelo y el tamaño real en GRANDE se comprueban en Windows.

---

## §188 · ÁLBUM: la carta en la mano iba a trompicones (2026-09-26)

**Problema (jugador):** «al inspeccionar la carta va muy muy muy bajo en fps». El álbum iba bien; la carta en la mano no.

**Causa:** `System.Windows.Media.Color` no son cuatro bytes. `Color.FromRgb` y `FromArgb` guardan también el color en
scRGB y lo convierten con `Math.Pow` (tres potencias al construirlo, tres al leerlo). Todo el dibujo pixel del álbum
(`CellCanvas`, `TcgCardArt`, `AlbumScene`, `HandScene`) usaba ese `Color`, y la carta en la mano construye varios por
**píxel de pantalla** (leer la celda, mezclar el reflejo, escalar la luz): un cuarto de millón de píxeles por fotograma.
Medido con un sustituto que imita ese coste: **~100 ms por fotograma** en la mano (≈10 fps en el mejor caso; con una
pantalla grande, menos), 19 ms la página girando.

**Arreglo:**
- **`PixelColour`** (`Views/PixelColour.cs`): el color del dibujo pixel, cuatro bytes y nada más, con los mismos
  `FromRgb`/`FromArgb`/`Transparent`. Los ficheros del álbum lo toman con `using Color = PermaLocke.App.Views.PixelColour;`,
  así que su código no cambió. `TypeColours` devuelve `PixelColour`; `TypePalette` lo pasa a `Color` de WPF para sus
  pinceles; `RoomSprite.Cell` es `At` sin WPF.
- **El bucle de la mano** (`HandScene.Card`): la luz y el reflejo se calculan **una vez por celda** de la carta (unos
  miles) en un búfer de enteros ya listos; cada píxel solo avanza la proyección —lineal a lo largo de la fila, se suma—,
  divide una vez y copia cuatro bytes. La cara animada reusa su lienzo en vez de clonarlo cada fotograma; las mezclas de
  rayos, polvo y sombra son en enteros.
- **El álbum se para debajo** mientras se inspecciona (`AlbumStage.IsPaused` ← `IsInspecting`): bajo el velo casi opaco
  no se ve, y cada fotograma suyo era uno menos para la carta. Un cambio sigue pintándose una vez; una página girando
  termina.
- **La carta va a 60 fps** si el ordenador la pinta rápido: `CardStage` mide lo que cuesta cada fotograma (media móvil);
  por debajo de 6 ms pasa a 60, por encima de 9 vuelve a 30.

**Medido** (Release, en Linux, 1084×690 y 1900×1350, carta dorada a 6 píxeles por celda): la mano **~100 ms → 3,8–4 ms**
por fotograma; la página girando 19 → 5 ms; un fotograma quieto del álbum 7 → 4,4 ms. Se ve igual: las imágenes de
PixelCheck salen píxel a píxel como antes (la luz de la mano pasó a enteros: ±1 en algún canal).

**Prueba nueva:** `A_frame_in_the_hand_is_cheap` (el mejor de 12 fotogramas en 1900×1350 por debajo de 30 ms; antes
eran 100). PixelCheck 46 correctas.

**Trampa para el futuro:** en bucles por píxel, **nunca** `System.Windows.Media.Color`; `PixelColour` o bytes. Otras
escenas pixel (cementerio, sala, cápsulas) siguen con el `Color` de WPF; no se han quejado, pero si alguna va lenta,
esta es la primera sospecha.

**Sin ver dentro de la app:** los 60 fps y la fluidez real se comprueban en Windows.

---

## §189 · GACHA: más golpe en cada tirada, y cincuenta seguidas sin cansar (2026-09-26)

**Petición (jugador):** «potenciar las animaciones del gacha para que impacten aún más y que, si tiras 50 veces
seguidas, no te canses».

**Cincuenta seguidas.** La tirada dura 9,95 s hasta el Pokémon a color (§171), y así se queda la primera. Tres cosas
para las siguientes, y ninguna depende de lo que ha salido, que es lo que el §171 protegía:
- **Racha y exprés.** Una tirada lanzada hasta 20 s después de que saliera el Pokémon anterior cuenta como seguida
  (`GachaViewModel._streak`, `StreakWindow`). Desde la segunda, la máquina va a **×2,5 hasta que la ball se abre**
  (`CapsuleTimeline.ExpressSpeed`): moneda, manivela, rodar y meneos. La apertura y el Pokémon, que son el premio, van
  a su ritmo. Una tirada seguida tarda ~4,6 s en vez de 10. Salir del gacha corta la racha. En la pared, bajo el neón
  GACHA, sale **«RACHA ×N»**.
- **SALTAR.** Mientras la ball no se ha abierto, el botón TIRAR dice **SALTAR · ir al Pokémon**. Pulsarlo (o la
  máquina, o ESPACIO/INTRO) lleva la tirada a la ball parpadeando a punto de abrirse (`CapsuleTimeline.SkipTo`); otra
  vez, al Pokémon fuera. Nunca hacia atrás. No actúa en los primeros 0,3 s de una tirada, para que el segundo clic de
  un doble clic en TIRAR no salte la que acaba de empezar.
- **ESPACIO o INTRO** es el botón grande (TIRAR o SALTAR), con el foco en él al entrar. No mientras se escribe en el
  buscador, no con la tecla mantenida (repetiría tiradas y gastaría puntos) y no sobre otros botones.

**El reloj.** `CapsulePlay.Elapsed` ya no es el tiempo real: `CapsuleTimeline.Warp(real + saltado, velocidad)`, rápido
hasta `Open` y a 1× desde ahí; `RealFor` es su inversa. `Skip()` suma el tiempo real que falta hasta el destino. La
escena, los portales y la ficha leen todos ese mismo reloj, así que saltar lo mueve todo a la vez; el view model ya no
duerme hasta cada momento, mira cada 5-40 ms (`WaitUntil`).

**Más golpe** (`CapsuleMachineScene`, efectos nuevos en `PixelScene`, todos sobre los bytes: `Spotlight`, `Wash`,
`Flash`, `Shake`, `FloorRing`, `ShoutText`):
- **Tensión en los meneos:** la sala se apaga alrededor de la ball y la luz se cierra sobre ella, igual para
  cualquier tier. En el último tercio de segundo la ball tiembla y se escapa luz blanca por la junta.
- **Cada subida:** además del fogonazo y las chispas de antes, la sala se tiñe un instante del color de la ball
  nueva, tiembla, corre un aro por el suelo y sale **«¡SUBE!»**, estampado a doble tamaño y subiendo.
- **La apertura:** fogonazo blanco en tramado (no un velo gris), aro doble en el suelo si es de los dos tiers altos, y
  sacudida **más fuerte cuanto más rara la ball**, que para entonces ya se ve.
- **El Pokémon sale de golpe:** silueta, un instante al triple y se asienta al doble.
- **Celebración, solo cuando ya no hay nada que ocultar:** confeti del techo para los dos tiers altos y los
  variocolor (del color del tier, o arcoíris); en un legendario, además, lluvia de monedas de oro que se quedan en el
  suelo, destello dorado, temblor y **«¡LEGENDARIO!»** estampado; en un variocolor, **«¡VARIOCOLOR!»** con cada letra de
  un color. Una tirada normal sigue siendo discreta: así lo raro se nota.

**Coste:** ~2 ms por fotograma de media en una escena de 520×210 celdas a lo largo de una tirada legendaria y
variocolor (medido imitando el `Color` de WPF, §188). Va a 30 fps como antes.

**Comprobar sin Windows:** `tools/PermaLocke.PixelCheck` compila ahora también `PixelScene`, la máquina de cápsulas y
`CapsulePlay`, con sustitutos de `WriteableBitmap` y compañía (`WpfImaging.cs`), y ejecuta las `CapsuleMachineTests`.
Con `PERMALOCKE_PIXEL_DIR` deja `gacha-*.png`: dieciséis momentos de una tirada normal, una que sube dos veces a un
legendario, una variocolor en racha y una BUENO.

**Pruebas nuevas:** `A_pull_in_a_row_hurries_only_up_to_the_open`, `Skipping_goes_to_the_open_then_to_the_pokemon`,
`Only_a_rare_pull_rains_gold` (y antes de la primera subida, un legendario y una normal son los mismos píxeles);
`Every_frame_of_every_pull_draws` ahora con rachas y un legendario variocolor. PixelCheck 66 correctas.

**Sin ver dentro de la app:** el botón SALTAR, ESPACIO y el ritmo real de la racha se comprueban en Windows.

**Corrección (visto por el jugador):** al salir el Pokémon la sala se alejaba y, unos segundos después, volvía a
acercarse. No era de este §: la línea de estado («Tirada gratis. … va a tu equipo») vivía bajo el botón TIRAR, en una
fila de alto automático. Al aparecer le quitaba alto a la máquina, que en su pantalla pasaba de caber a 3 píxeles por
celda a no caber, y bajaba a 2 (`CapsuleMachine.Reshape`); a los 5 s `Fleeting` la borraba y volvía a 3. Ahora la línea
va **encima del suelo de la sala**, abajo a la izquierda, sobre un panel (`HasStatus`), y no ocupa sitio: la máquina
tiene siempre el mismo alto (y 8 px más que antes, los del margen de la línea vacía).

---

## §190 · La carta de cada captura vuela al álbum, encima del juego (2026-09-26)

**Petición (jugador):** al atrapar un Pokémon salvaje, que por la derecha entre un álbum pequeño que se reconozca como
álbum, que de la Poké Ball salga la carta del Pokémon, que con una animación se meta en el álbum y que el álbum se vaya
por la derecha. De momento solo capturas salvajes.

**Cuándo sale.** `EncounterGuard.Caught` avisa cuando el récord de capturas del juego (récord 6) sube respecto al
empezar el combate salvaje. El juego lo cuenta **al terminar el combate** (medido en el §118: la huida y la captura se
apuntan justo después de que se vayan las tablas), así que la animación sale al volver al campo, no sobre la ball del
combate. Por eso la ball es **nuestra**: cae donde está el entrenador, en medio de la pantalla de arriba. La vigilancia
va aparte de `_battle` (`_watched`, 20 s tras el combate): un combate que no gasta la ruta se suelta antes de que el
juego cuente nada, y una captura variocolor o de estático permitido también tiene su carta.

**La carta es de verdad.** El Pokémon salvaje se lee entero en el combate (`BattleTableReader.ReadPokemon`, ya se leía
para saber si era variocolor; ahora siempre, una vez) y se guarda en el combate. `SaveBoxReader.Describe(PK7)` lo
describe como uno del equipo, sobre una copia, y `TcgCardFactory` (sacada de `AlbumViewModel`, que ahora la usa) hace
**la misma carta que luego tendrá el ÁLBUM**: nivel, movimientos, naturaleza, estadísticas y rareza reales. Si no se
pudo leer, no sale nada: no se inventa una carta.

**La animación** (`CatchScene`, pura, 3,95 s; `CatchWindow` encima de la pantalla de arriba de Azahar,
`GameWindow.TopScreen`, sin ratón ni foco):
1. El álbum entra por la derecha, cerrado: piel morada con grano y pespunte, anillas doradas en el lomo, cantoneras y
   una Poké Ball en oro. Va a dos píxeles del juego por celda (~120×76 abierto): pequeño pero se lee como álbum.
2. Una Poké Ball cae donde está el entrenador, bota, se menea dos veces y se abre con un fogonazo de rayos blancos.
3. La carta sale de la ball dando vuelta y media y creciendo con rebote (la carta en la mano del §187, `HandScene`,
   ahora con `surroundings: false`: sin sombra ni polvo), se deja ver meciéndose con los rayos de su rareza.
4. El álbum se abre (la tapa gira sobre el lomo y cae al otro lado como hoja de fundas, casi todas con cartitas), la
   carta vuela en arco encogiendo hasta la funda vacía del centro, entra, la funda brilla y el álbum da un saltito.
5. Se cierra y se va por la derecha.

Todo en píxeles del juego (`Pixel` = tamaño de un píxel del 3DS en el monitor), así que parece parte de él.

**Ajuste:** «Carta al capturar» en CONFIGURACIÓN → MIENTRAS JUEGAS (`AppSettingsData.CatchCard`, encendido por defecto).
**Ensayo:** `--ensayar-captura` enseña la carta del primero del equipo de la partida, sin juego (sin Azahar, en medio del
escritorio). Mientras sale se avisa a la killcam (`CoverBegins/Ends`), aunque fuera de combate no graba.

**Pruebas (PixelCheck):** cada momento dibuja dentro de la pantalla en tres tamaños y al final no queda nada encima del
juego; mostrándose, la carta ocupa su trozo; el último instante del vuelo cae sobre la funda. `captura.png` con dieciséis
momentos sobre un campo de mentira.

**Visto por el jugador (2026-09-26): «todo perfecto».**

---

## §191 · La app se entera de que el organizador ha reiniciado su run (2026-09-26)

**Visto por el jugador:** reinició las dos runs desde Admin «para empezar ya definitivamente», entró en su app y no
parecía reiniciada.

**Por qué:** `reiniciar_run` (`07-una-run.sql`) solo **archiva** la run en el servidor (`activa = false`) y deja que el
jugador cree otra. No toca el PC del jugador, ni debe: la run local y la partida de Azahar son suyas, y borrarlas es la
única cosa destructiva de PermaLocke (HOME → EMPEZAR DE CERO, con dos confirmaciones y copia de la partida). Pero la
app no se enteraba, así que nada decía que había que hacerlo.

**Ahora:** `CommunityService.ReadRunStateAsync`, en cada lectura de amigos (60 s), pregunta `runs?run_id=eq.<run>` por
`activa`. Si está archivada: `RunRestarted`, un aviso encima del juego una vez por run («Tu run se ha reiniciado… Ve a
HOME y pulsa EMPEZAR DE CERO») y en JUGAR un panel rojo «TU RUN SE HA REINICIADO» encima del anuncio. Una run que nunca
se subió no tiene fila y no cuenta como reiniciada. Nada se borra solo.

**Sin ver en Windows.**

## §192 · Antitrampas de recarga (2026-09-26)

Petición del organizador: que nadie recargue para deshacer una muerte o repetir el primer encuentro de una ruta.

Lo que ya estaba cubierto: una muerte vive en la run (evento), se vuelve a escribir a 0 PS en la partida al cerrar el
juego (`SaveDeathEnforcer`) y se mantiene a 0 en vivo (`KeepFallenDownAsync`); una ruta gastada vive en la run
(`ZoneEncounterSpent`) y vuelve a retirar las balls. Recargar no resucita ni devuelve rutas.

Lo nuevo (`IntegrityGuard` en App, `IntegrityService` en Core, evento `IntegrityFlag` con `tipo`):
- **Estados guardados:** `AzaharInstallation.DisableSaveStates` quita las teclas de guardar/cargar estado y de
  reiniciar la emulación (con su `\default=false`). El menú de Azahar no se puede quitar: cualquier fichero que
  aparezca en `user/states` se **mueve** (no se borra) a `Saves/estados-retirados` en el siguiente segundo y se anota.
  Los que ya estaban antes de abrir el juego se retiran sin anotar.
- **Cerrar PermaLocke con el juego abierto:** la ventana se niega. Y si PermaLocke muere por otra vía (fallo,
  Administrador de tareas), un *job object* de Windows con «kill on job close» (`EmulatorJob`) se lleva Azahar con él.
  Se ata en cuanto el lanzador ve la sesión, se abriera desde JUGAR o no.
- **Jugar o restaurar sin PermaLocke:** al acabar cada sesión vigilada se guarda el tiempo de juego del save en
  `Saves/<run>/partida-vista.json`. Al abrir PermaLocke se compara: más de 1 min de más = «fuera», de menos =
  «retrocedida». Si el juego ya estaba abierto al arrancar PermaLocke, se anota «fuera».
- **Cerrar en mitad de un combate:** el vigilante guarda la última lectura dentro de un combate
  (`GameLinkMonitor.LastReadingInBattle`). Si el emulador termina en los 6 s siguientes con código 0, 1 o 0xC000013A
  (o desde CERRAR), se anota «abandono». Un fallo (cualquier otro código) nunca se anota.

Los avisos **solo los ve el organizador**: columna «Avisos de recarga» en AUDITORÍA de Admin. HOME los filtra y no mueven
puntos ni Pokémon: son pruebas, la sanción la decide él. Ojo: viajan en el historial subido, que el servidor deja leer
a la whitelist; ninguna pantalla del jugador los enseña. Todo apagado con `--sin-juego`.

Sin hacer: reiniciar desde el menú de Azahar (Emulación › Reiniciar) sin cerrar la ventana no se detecta; la tecla sí
se quita. Probado con pruebas (`IntegrityServiceTests`, `Save_states_lose_their_keys…`); **sin probar jugando**.

## §193 · Admin ampliado: ficha del jugador, órdenes, pausa y reglas oficiales (2026-09-26)

Petición: «que se pueda hacer y gestionar prácticamente todo lo posible» desde Admin. Admin sigue sin tocar ninguna run:
todo viaja por el canal de regalos como una **orden** y la aplica la app del jugador, con su evento.

- **Canal:** `AdminOrder(Kind, Args, Summary)` dentro de un `AdminGift` de esquema 3 (`AdminGift.OrderSchema`, misma tabla
  `regalos`). Una app vieja salta los esquemas nuevos en vez de entenderlos a medias. `GiftInbox` las aplica solas cada
  20 s con `OrderService` (App) y avisa encima del juego. Cerrada (hecha o rechazada para siempre) = un
  `AdminGiftClaimed` con su id y `resultado`; así Admin ve «Recogido» y nunca se aplica dos veces. Las que esperan al
  juego (objeto: abierto; Pokémon: cerrado) se reintentan.
- **Órdenes (`AdminOrderKinds`):** revivir (`GameWatcher.RevokeDeathAsync`, fuente Admin), marcar caído
  (`RecordDeathAsync`), revocar wipe (`PenaltyService`), liberar ruta (`ZoneOutcomeService.ClearAsync`), pruebas
  superadas (`ProgressService.AdvanceAsync`, mueve el cap), dar objeto (`IItemDelivery`), dar Pokémon (`IPokemonDelivery`
  como el gacha, habilidad/naturaleza/IV con semilla de la orden, registrado `AdminGrant` + `PokemonDelivered`), mensaje
  privado, y cerrar/abrir el juego (evento nuevo `PlayLock`; JUGAR queda bloqueado en `EmulatorLauncher.Evaluate`).
  Sin orden de cambiar rol: el rol exige regenerar la ROM (EXPERTO).
- **Ficha (`PlayerSheetWindow`):** botón FICHA en cada jugador. Pestañas POKÉMON (con REVIVIR/MARCAR CAÍDO), HISTORIAL
  (filtro por tipo y búsqueda), RUTAS GASTADAS (LIBERAR), EQUIPOS CAÍDOS (REVOCAR), AVISOS (§192), LOGROS y ACCIONES
  (puntos, pruebas, objeto de la tienda o por id, Pokémon, mensaje, cerrar/abrir juego). MOTIVO obligatorio arriba. Los
  Pokémon vienen de `RunSnapshot.Pokemon` (nuevo, opcional) y las horas de `RunSnapshot.PlayedHours`.
- **Torneo:** PAUSAR TORNEO / REANUDAR TORNEO mandan el bloqueo a todos con el motivo del regalo.
- **Reglas oficiales (`RulesWindow`, tabla `tools/supabase/14-reglas.sql`):** los ficheros de `TournamentRules.Files`
  (tienda, gacha, logros, penalizaciones, caps, roles, ruleta, créditos, premios, wonder trade, reglas). Admin edita el
  JSON, lo valida (y lo carga con el cargador real en tienda, gacha y wonder trade) y lo sube. `RulesSync` lo descarga
  al abrir la app, guarda copia en `Data/reglas-anteriores` y avisa: se aplica al reiniciar PermaLocke. Apagado con
  `--sin-juego` (en el repo pisaría los ficheros versionados).

Pendiente del usuario: ejecutar `14-reglas.sql`. **Sin probar con el servidor ni jugando**; compila, arranca y pasan las
pruebas (`AdminOrderTests`).

---

## §194 · Subir solo lo nuevo y LIMPIEZA en Admin (2026-09-26, plan del próximo torneo, paso 1)

**Para qué:** el próximo torneo es de ~20 personas y el plan gratis de Supabase tiene 500 MB. Hasta ahora cada app
sube su historial **entero** cada dos minutos (un `upsert` de `runs`): la fila se reemplaza, pero cada reemplazo de un
JSON grande deja una copia muerta hasta el vacuum, y `subidas` gana una fila cada vez.

**Servidor — `tools/supabase/15-eventos-y-limpieza.sql` (lo ejecuta el usuario).** Solo añade; lo que usan las apps que
están jugando (runs, subidas, ultima_run, logros, amigos, clasificacion) queda igual.
- Tabla `eventos` (`run_id`, `n`, `evento` jsonb, `llegada`): una fila por evento, que no se reescribe nunca. Leen los
  de la lista y el organizador; nadie escribe directamente.
- `subir_eventos(p_run, p_snapshot, p_desde, p_eventos)`: comprueba lista, dueño y que la run siga activa (una nueva la
  crea como el insert de siempre, con el índice de una run activa por jugador). Añade los eventos **solo** si `p_desde`
  es lo que ya tiene y el primero sigue la cadena (`previousHash` = hash del último guardado). Si no —copia restaurada,
  cadena reescrita— no añade ni borra nada. Siempre actualiza el `snapshot`, así que el trigger de siempre sigue
  anotando cada envío en `subidas` con su número y su huella: **la AUDITORÍA sigue viendo los retrocesos**. `runs.history`
  de esas runs queda en un esqueleto con `"enEventos": true`. Una run que subía entera pasa sus eventos a la tabla la
  primera vez, tal cual.
- Vista nueva `logros_todos`: los logros de `runs.history` y de `eventos`, para la ACTIVIDAD de las apps nuevas.
- `limpieza(p_ejecutar, p_historiales)`: solo organizador; cuenta (o borra) lo que nada vuelve a leer: fantasmas y
  lluvias de más de un día (salvo la última de cada tabla), anuncios salvo el último, regalos a una persona de más de 7
  días ya recogidos en su run activa (los de «todos» nunca), y en `subidas` todo menos la primera, la última y cada
  retroceso con su anterior. Con `p_historiales`, además el historial de las runs **archivadas** (y sus `eventos`); el
  resumen se queda. Nunca runs activas, lista, organizadores, reglas, reinicios ni presencia.

**App.** `TournamentUpload` sube por `subir_eventos`: la primera vez pregunta con `p_desde = -1` cuántos tiene el
servidor y luego manda lo que falta en tandas de 400. Si el servidor tiene más que el PC o no acepta la tanda, deja de
añadir y solo manda el resumen (aviso en el log). Si el servidor no tiene la función (404, SQL sin ejecutar), sube
entero como antes. `CommunityService` lee `logros_todos` y, sin ella, `logros`.

**Admin.** `ServerHistory` completa el historial desde `eventos` cuando `runs.history` viene vacío pero el resumen dice
que hay eventos (AUDITORÍA, ficha del jugador) y trae las recogidas de regalos de esas runs (`GiftDesk`). Botón
**LIMPIEZA** en CONTROL (`CleanupWindow`/`CleanupViewModel`): cuenta con `limpieza(false)`, enseña qué y cuánto, y borra
solo tras un sí; vaciar historiales archivados es una casilla aparte con segunda pregunta.

**Probado de verdad** en un Postgres 16 local que imita a Supabase (`tools/supabase/pruebas/`: `supabase-falso.sql`,
las pruebas del 15 y `probar.sh`): los SQL 01-15 aplican en orden; la run antigua pasa a eventos; una tanda que sigue
la cadena entra, una que no sigue o una copia restaurada no, y quedan anotadas en `subidas`; `logros_todos` ve los
logros de las dos maneras; otro jugador no puede subir a tu run ni limpiar ni escribir en `eventos`; la limpieza quita
exactamente lo previsto (regalos, fantasmas, anuncios, subidas intermedias) y vacía solo las runs archivadas.
Compilación entera sin avisos; Core 383, GameLink 391, Randomizer 485, Rules 151, PixelCheck 69. **Sin ver** contra el
Supabase real ni en Windows: primero hay que ejecutar el 15.
