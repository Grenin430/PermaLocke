---
tipo: arquitectura
revisado: 2026-09-24
---
# Arquitectura

Ver también [[Flujos del vigilante]] · [[GameLink y memoria]] · [[UI y kit pixel]] · los mapas de ficheros en [[00 - Inicio]].

## Solución (`PermaLocke.slnx`)
| Proyecto | TFM | Responsabilidad | Depende de |
|---|---|---|---|
| `PermaLocke.Core` | net10.0 | dominio, eventos, puntos, servicios de run; sin IO concreto | DI.Abstractions |
| `PermaLocke.Rules` | net10.0 | motor de reglas, cap, `EncounterService`, balls y zonas | Core |
| `PermaLocke.GameLink` | net10.0 | RPC de Azahar, memoria viva, fichero de partida (PKHeX.Core **26.7.7**) | Core |
| `PermaLocke.Data` | net10.0 | SQLite (`Microsoft.Data.Sqlite` 10.0.11), catálogos JSON, copia de la run, carpeta compartida | Core |
| `PermaLocke.Infrastructure` | net10.0 | `AppPaths`, log a fichero (`Logging/FileLoggerProvider.cs`) | Core |
| `PermaLocke.Randomizer` | net10.0 | randomización por LayeredFS | Core, `third_party/pk3DS.Core` |
| `PermaLocke.App` | net10.0-windows (WPF) | app del jugador | todos |
| `PermaLocke.Admin` | net10.0-windows (WPF) | ventana de admin sobre la carpeta compartida (§129) | todos |
| `tools/PermaLocke.Probe` | net10.0 exe | ~60 sondas de diagnóstico (`--run`, `--pids`, `--ev`…) | GameLink, Data, Rules, Infra, Randomizer |
| `tools/PermaLocke.RomTool` | net10.0 exe | herramienta contra la ROM (un solo `Program.cs`) | Randomizer, Core, Rules, Data, pk3DS, PKHeX |
| `tests/*` (5) | xUnit 2.9.3 | ver [[Pruebas]] | — |

- MVVM con CommunityToolkit.Mvvm 8.4.2.
- Microsoft.Extensions DI/Logging 10.0.11.
- SDK .NET 10.0.400.
- `Core`, `Rules`, `GameLink` y `Randomizer` nunca referencian WPF.

## Arranque (`src/PermaLocke.App/App.xaml.cs`, `OnStartup`)
1. `AnotherIsRunning`: mutex `Local\PermaLocke.App`, una sola instancia. `--sin-juego` está exento (§159).
2. `new AppPaths()` y `EnsureCreated()`.
3. `RunBackup(paths.Saves).Run()`: copia `permalocke.db` antes de abrirla y guarda 10. No se hace con `--sin-juego` (§76).
4. DI:
   - `AddPermaLockeInfrastructure`, `…Data`, `…Core`, `…Rules(Data/rules.json)`, `…GameLink(Saves/backup)`;
   - catálogos JSON como singletons;
   - todos los ViewModels como singletons, menos los diálogos (`CreateRun`, `RegisterCapture`, `ChangeRole`), que son transient.
5. Manejadores globales de excepciones: dispatcher, AppDomain y tareas sin observar. El de UI enseña «Ha ocurrido un error inesperado.».
6. `InstalledWorld.Apply` fija el techo de especies (807 o 1025) antes de leer el juego, desde el `WorldLimits` global.
7. `AlolaSky.Start`, `MainWindow`, `RunService.LoadMostRecentAsync`, `PlayerProfileService.LinkCurrentRunAsync`, `EmulatorLauncher.Start` (1 Hz) y `MainViewModel.InitialiseAsync`.
8. Se instancia `PlayNotifications`, que se suscribe a los eventos antes de que arranque el vigilante.
9. Si no es `LocalOnly`: `CommunityService` y `GiftInbox`.
10. Si no hay `--sin-juego`:
    - `EdgeTab.Attach`;
    - **`GameLinkMonitor.Start()`**;
    - calentar `WorldEvolutionLines` y `WorldAllowedStatics`.
11. Flags de ensayo: `--seccion X`, `--hora-alola HH:mm`, `--tamano normal|grande|enorme`, `--ensayar-muerte`, `--ensayar-killcam`.

## Rutas (`src/PermaLocke.Infrastructure/AppPaths.cs`)
- La raíz es la carpeta del exe si existe `PermaLocke.local`. Si no, se sube hasta encontrar `PermaLocke.slnx` (en desarrollo la raíz es el repo).
- `LocalOnly` equivale a que exista `PermaLocke.local`: desactiva la sincronización y oculta COMPETICIÓN (se quita de `Sections`).
- `Expansion`: la propia, o la de la carpeta padre si tiene `romfs` (por la carpeta compartida del §148). Con `LocalOnly`, solo la propia.
- Carpetas: `ROM/`, `Expansion/`, `Randomized/`, `Data/`, `Config/`, `Logs/`, `Saves/`, `Saves/backup/`.

## Persistencia
| Qué | Dónde | Código |
|---|---|---|
| Eventos (cadena por hash) | `Saves/permalocke.db`, tabla `events` (seq, id, run_id, timestamp, type, source, actor, description, points_delta, pokemon_id, location_id, seed…) | `Data/SqliteEventStore.cs` |
| Pokémon de la run | misma BD, tabla `pokemon` (pid, form, status…) | `Data/SqlitePokemonRepository.cs` |
| Run | `Saves/<runId:N>/run.json` | `Data/JsonRunRepository.cs` |
| Tiempo jugado | `Saves/<runId:N>/sesiones.json` | `Data/JsonPlaytimeStore.cs` |
| Killcams | `Saves/killcam/<run>/<pokemon>.killcam` | `App/Services/KillcamClip.cs` |
| Copias de la run | `Saves/backup/*.db` (10 rotativas) | `Data/RunBackup.cs` |
| Direcciones recordadas | `Saves/backup/equipo.txt`, `registros-de-posicion.txt`, `mochila.txt`, `objetos-retirados.txt` (`WithheldLedger`, §147) | `GameLink/ServiceCollectionExtensions.cs` |
| Copias de la partida antes de escribir | `Saves/backup/` | cada `Save*` de GameLink |
| Perfil y preferencias | `Config/jugador.json`, `sync.json`, `ventana.json`, `pestana.json`, `competicion-vista.json`, **`ajustes.json`** (`AppSettings`, §177) | `JsonPlayerProfileStore`, `WindowSizeService`, `EdgeTab` |
| Log | `Logs/permalocke-AAAA-MM-DD.log` | `FileLoggerProvider` |
| Sprites extraídos | `Data/sprites/v2/<sourceKey>/…` (`categorias/`, `items/`, `balls/`), fuera de git | `App/Services/PokemonSpriteService.cs` |
| Partida del juego | `<Azahar user>/sdmc/Nintendo 3DS/…/00040000001B5100/data/…/main` | `GameLink/PlayerSave.cs` |
| Informe de cierre de Azahar | `Diagnosticos/cierre-azahar-<fecha>.zip` | `App/Services/EmulatorCrashReport.cs` |

- Tipos de evento, en `Core/Domain/Enums.cs` `GameEventType`: RunCreated … RoleChanged, BattleModeChanged, ZoneConfirmed, ZoneOutcomeSet, ZoneCleared, RouletteGranted, DeathRevoked, ZoneEncounterSpent, PlayerLinked, RulesAdopted, AdminGiftClaimed, MoveRemembered, FirstPokeBallSeen, WipeRevoked.
- Regla 4 de `CLAUDE.md`: todo cambio de estado pasa por un evento.

## Datos de juego (`Data/*.json`, configuración fuera del código)
| Fichero | Lo carga | Para qué |
|---|---|---|
| `rules.json` | Rules DI, `OfficialRules` | reglas, modos, `allowedStatics` |
| `levelcaps.json` | Rules | cap por etapa (`logro` por etapa, §49) |
| `achievements.json` | `JsonAchievementCatalog` | 21 logros (récord, objeto o trigger) |
| `penalties.json` | `JsonPenaltyCatalog` | −25 por muerte, −100 por equipo caído, máximo 4 |
| `roles.json` | `JsonRoleCatalog` | NORMAL, CAGONETA, EXPERTO, LUDÓPATA |
| `gacha.json` / `species.json` | `JsonGachaCatalog` / `JsonSpeciesStatsCatalog` | banners por rango de BST; tabla de especies de la ROM (1025) |
| `wondertrade.json` | `JsonWonderTradeCatalog` | banda −8 % / +20 % |
| `shop.json` | `JsonShopCatalog` | tienda |
| `rewards.json` | `JsonRewardCatalog` | premios de una vez; `automatico` |
| `grants.json` | `JsonCreditCatalog` | tiradas y wonder trades por prueba |
| `roulette.json` | `JsonRouletteCatalog` | 16 caras |
| `randomizer.json` | `RandomizerOptionsLoader`, `InstalledWorld` | todos los interruptores del randomizador |
| `mapas.json` | `JsonMapTable` | mapa → mundo → zona (regla de encuentro) |
| `marcadores.json` | `EncounterGuard`, `MapViewModel` | rutas del MAPA |
| `islas.json`, `fotos.json`, `mapa-areas/`, `mapa-islas/` | `JsonIslandMap`, `ZonePhotoService` | dibujo del MAPA |
| `zones.json` | nadie lo referencia por nombre en `src` | ¿histórico? ver [[Preguntas abiertas]] |

## Secciones (`App/ViewModels/MainViewModel.cs`, `Sections`)
JUGAR, HOME, RANDOMIZADOR, GACHA, TIENDA, LOGROS, MAPA, VISOR POKÉMON, ENTRENAR EV, MOVIMIENTOS, POKE PASTE, CEMENTERIO, COMPETICIÓN (`sync`, se quita si es `LocalOnly`), COMBATES, MISCELÁNEA y CONFIGURACIÓN (antes MANTENIMIENTO, §177). RULETA se inserta antes de MISCELÁNEA solo con el rol LUDÓPATA. Cada sección declara `SectionViewModel.Needs` (juego abierto, cerrado o le da igual; §66). Cada ViewModel tiene su `DataTemplate` en `MainWindow.xaml`.

## Dos puertas para tocar el juego
1. **Memoria viva** (RPC, juego abierto): equipo, mochila, zona, combate, cap, PS de los caídos. Ver [[GameLink y memoria]].
2. **Fichero de partida** (PKHeX, **juego cerrado**): entregas del gacha y del wonder trade, EV, recordar movimientos, ruleta, marca de muerte al cerrar, reparaciones. Siempre con copia previa y relectura.
