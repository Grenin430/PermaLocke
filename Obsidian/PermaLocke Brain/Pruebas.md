---
tipo: pruebas
revisado: 2026-09-24
---
# Pruebas

- xUnit 2.9.3, en 5 proyectos. Lista completa con recuentos: [[Mapa de código/Tests]].
- **Última ejecución completa conocida: 1521 correctas, 0 fallos**, el 2026-09-23 (`dotnet test --no-build`), con App 107, Core 404, GameLink 381, Randomizer 479 y Rules 150.
- Las pruebas no demuestran el comportamiento en una partida real: casi todo lo de memoria se prueba con **servidores RPC simulados** y saves sintéticos.

## Por área → ficheros clave
| Área | Pruebas |
|---|---|
| RPC y robustez del enlace | `GameLink.Tests/AzaharRpcClientTests` (datagramas perdidos, respuestas tardías), `GameLinkRecoveryTests` (barridos, enfriamiento, sin partida guardada), `PartyFromSaveTests`, `PartyLocatorTests`, `PartyLayoutTests`, `PartyStatsTests` |
| Zona y encuentros | `EncounterRecoveryTests` (26; revisiones r2 y r3: primer guardado, no buscar en combate, contadores, paginación de hermanos, `Nearby`, sin mayoría), `FieldRecordTests` (26), `BattleCounterReaderTests`, `Rules.Tests/EncounterPolicyTests` (24), `FirstEncounterRuleTests`, `BallControlServiceTests` (18), `TrialZoneTests`, `ZoneOutcomeTests`, `Core.Tests/ZoneMarkerTests` |
| Combate y muertes | `BattleTableTests`, `BattlePokemonTests`, **`BattleIdentityTests`** (7: `MatchPlayer` por PID, contradicciones, especie repetida, sin PK7), `HpBarTests` (18), `DeathMarkTests`, `Rules.Tests/TeamWipeTests`, `DeathRevocationTests` |
| Killcam y avisos | `App.Tests/KillcamBufferTests`, `KillcamNotificationTests`, `NotificationTests` (escalas y monitores de `Notifier`), `EmulatorCrashTests`, `EmulatorProcessTests` |
| Sprites | `App.Tests/SpritePreparationTests`, `Randomizer.Tests/PokemonIconIndexTests`, `FormIconsTests`, `ExpansionItemIconTests` |
| Cap de nivel | `LevelCapLiveTests`, `LevelCapWriteTests`, `Rules.Tests/LevelCap*` |
| Escrituras en el save | `SaveEvTrainerTests`, `SaveMoveTeacherTests`, `SaveBoxSwapTests`, `SaveEraserTests`, `SavePidRepairTests`, `SaveNameRepairTests`, `SaveRouletteWorldTests`, `GachaPartyDeliveryTests`, `WithheldLedgerTests` |
| Run, eventos y puntos | `Core.Tests/EventChainTests`, `SqliteEventStoreTests`, `PointsServiceTests`, `PenaltyServiceTests`, `RunBackupTests`, `RunServiceTests`, `RolePointsTests` |
| Gacha, wonder trade, ruleta | `GachaServiceTests` (seed reproducible), `WonderTradeServiceTests`, `RouletteServiceTests` (25), `RouletteConfigTests` |
| Distribución | `Core.Tests/LocalDistributionTests`, `ExpansionFolderTests`, `SharedFolderSettingsTests` |
| Randomizador | 61 ficheros; el central es `ModInstallerTests` |
| UI | `App.Tests/HomeViewTests` (ver abajo), `PixelUiTests`, `CapsuleMachineTests`, `TradeMachineTests`, `RouletteMachineTests`, `TrainerRoomTests` |

## `HomeViewTests` (una sola prueba STA)
- Solo cabe una `Application` por proceso, así que va todo en una prueba. Carga los temas Palette, Icons, Controls y Pixel.
- Comprueba que el aviso de HOME se enseña y se oculta.
- Comprueba los títulos del visor: EQUIPO, CAJAS DEL PC, FICHA.
- Mide las 14 pantallas.
- Construye los 3 diálogos sin ViewModel (`LoadDialog<T>`, datos de muestra con `Mutable()` y `ExpandoObject`).
- `PERMALOCKE_SNAP_DIR=<dir>` guarda un PNG de cada diálogo.
- Avisos (§238): `RenderNotices` pinta 5 avisos (2 `ToastScroll` por aviso, compacto y completo) a `Reveal` 0,48 y 1
  (`ToastScroll-abriendo.png`, `ToastScroll-abierto.png`). `LiveNotices`, solo con `PERMALOCKE_SNAP_LIVE=<dir>`, abre una
  `ToastWindow` de verdad fuera de pantalla (Left -3000) con el dispatcher en marcha y guarda una tira de fotogramas de 16 ms a
  5,9 s (`ToastScroll-vivo.png`): así se ve la animación real, no solo el dibujo.
- `CapPlaqueTests` (fuera de `HomeViewTests`, no necesita `Application`): filas, tamaño, píxeles, punta transparente; con
  `PERMALOCKE_SNAP_DIR` deja `CapPlaque.png` y `CapPlaque-liga.png`.
- **Si se añade una pantalla o un diálogo, se añade aquí**: un `StaticResource` mal escrito revienta en esta prueba y no al abrirlo.

## Verificación que no es xUnit
- Sondas `tools/PermaLocke.Probe` con `--probar`: escriben **sobre una copia** de la partida ([[Comandos]]).
- Capturas de la app real en la copia aislada ([[UI y kit pixel]]).
- Jugando, con el usuario: es lo que falta para casi todo lo marcado «sin jugar» en la tabla de `CLAUDE.md`.

## Animación del objeto por categorías (2026-10-10, §243)
- `tests/PermaLocke.App.Tests`: `ItemCatalogTests` (13), `ItemIconCoverageTests` (3, sobre `Expansion/`), `ItemSceneTests` (1, sin huecos a cuatro tamaños), `ItemStyleTests` (7: altura declarada, escalado a 1,0/1,5/2,3/3,7, determinismo, tinte, placa y **0 bytes por fotograma**) y un fichero por estilo: `MegaStoneStyleTests` (14), `ZCrystalStyleTests` (4), `KeyItemStyleTests` (4), `EvolutionStyleTests` (3), `MachineStyleTests` (4), `BerryStyleTests` (3), `HealingStyleTests` (3), `BoostStyleTests` (4), `PokeBallStyleTests` (3), `BattleStyleTests` (3), `MiscStyleTests` (3).
- De la Megapiedra, las que importan: destello visible a 30, 45 y 60 fps con diez desfases, marca de impacto a 30, placa que no se sale de la escena y fondo vacío en todo instante (`The_background_is_left_alone_...`).
- Visual: `ItemLab --solo <categoría> --rel --tira a.png --en ...` y `--fondo negro|juego` ([[Comandos]]). En el juego: `--ensayar-objeto`, **pendiente**.
- Totales de la 1.0.16 (locales): App 262, Core 442, Rules 149, GameLink 404, Randomizer 514, Admin 21, PixelCheck 82. La Action solo corre Core, Rules, GameLink y Randomizer.
