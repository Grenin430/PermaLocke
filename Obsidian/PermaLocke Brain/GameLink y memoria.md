---
tipo: gamelink
revisado: 2026-09-24
---
# GameLink y memoria

Código en `src/PermaLocke.GameLink/` ([[Mapa de código/PermaLocke.GameLink]]). Cómo se usa en [[Flujos del vigilante]]. Errores con memoria que no hay que repetir: [[Trampas y lecciones]].

## Emulador y fork
- Azahar, fork propio en `github.com/Grenin430/azahar`. Los binarios van en `Emulator/` (no versionados, solo `Emulator/LEEME.md`). `Nuevo_azahar/` y las carpetas `*-anterior` / `*-antes-parche3` del repo son versiones viejas de trabajo.
- `AzaharInstallation` busca el Azahar que va a usarse:
  - modo portátil: una carpeta `user/` junto al exe;
  - activa el RPC;
  - escribe `qt-config.ini` nuevo en la distribución;
  - pone `confirmClose` a false.
- `CITRA_USER_DIR` **no funciona** (§80).
- Parches del fork (`docs/fork/`):
  1. `SearchMemory` nativo. `SupportsSearch()` detecta si el emulador es el fork; el oficial devuelve las búsquedas vacías.
  2. `WatchBlock`, que reponía un bloque de muerte. Verificado, pero **la app ya no lo usa** desde §98 ter; solo queda una mención.
  3. Puntos de observación de escritura (`WatchWrites`, `ReadWriteLog`). Con ellos se halló que los PS están en `0x1E4+0x158` (§99).
  4. `ReadBlockNoFlush` (`0dfe782`): lectura y búsqueda sin volcar la caché de la GPU. **Es el arreglo de los cierres del amigo** (§173). `azahar.exe` `06143878…`.
- El emulador lleva al lado las 6 DLL de Visual C++ 14.51, porque con un runtime anterior se cierra (§168; las copia `publicar.ps1`).

## RPC (`Rpc/AzaharRpcClient.cs`)
- UDP `127.0.0.1:45987`.
- Cabecera de 16 bytes: versión=1, id, tipo, tamaño. Carga máxima de 1024 bytes, así que las lecturas se trocean.
- Timeout de 1500 ms y `Attempts = 3` **con el mismo id**.
- `lock _gate`: una sola petición a la vez, porque el cliente es un singleton compartido.
- `Receive` tira las respuestas con otro id (`Discarded`) y respeta un plazo total aunque lleguen respuestas viejas sin parar (§54, 20/09).
- Contadores `Retries` y `Discarded`. `Trace` guarda las últimas peticiones para el informe de cierre.
- API:
  - `TryPing`, `ListProcesses`, `AttachTo(titleId)` (hace **SetGetProcess**, sin el que las lecturas devuelven basura; trampa documentada);
  - `ReadMemory`, `TryReadMemory`, `WriteMemory`;
  - `SearchMemory(addr, size, pattern, mask)`, con un máximo de **255** resultados por llamada, así que se pagina.

## Equipo (`AzaharGameStateProvider.cs`, `Data/PartyLayout.cs`, `PartyLocator`, `Pk7Reader`, `PartyStats`)
- Hay **varias copias** del equipo en memoria:
  - el **espejo**, salto `0x104`, visto en `0x330128E4`: el juego escribe en él pero **no lo lee**;
  - la **autoritativa**, salto `0x1E4`, `PartyLayoutLocator.AuthoritativeStride`. La cola de estadísticas está en **`+0x158`**, con los PS y el nivel (§99, §154).
- `PartyLayoutLocator.Distinct` evita vistas solapadas.
- `PartyStats.AreHere` **mide** si la cola de una entrada son estadísticas; el ancla es que el nivel aparece dos veces.
- Orden de `Read()`:
  1. `AttachTo`. Con 5 fallos seguidos olvida el layout.
  2. Layout en caché.
  3. Direcciones recordadas en `Saves/backup/equipo.txt`, revalidadas. Si la recordada es el espejo, se marca `_sweepNext`.
  4. Espera 20 lecturas (`PollsBeforeSweeping`) antes de barrer.
  5. **`LocateFromSave`**, cada 20 s como mucho: busca los Pokémon del save por sus **constantes de encriptación** (`savedPartyKeys`, a partir de `SavedGameCache`), con pocas búsquedas y **sin barrer**. Si no hay partida guardada, **no barre** y pide guardar (§152).
  6. Barrido completo con `_locator.LocateAll`. Exige **dos Pokémon contiguos**, así que con solo el inicial no encuentra nada. El enfriamiento empieza en 30 s y se duplica hasta 4 min (`SweepCameUpEmpty`).
- `SweepAgain()` fuerza un barrido nuevo; lo usa `KeepFallenDownAsync`.
- `WorldLimits`, estado **global**: techo de especies 807 o 1025. Lo fija `InstalledWorld.Apply` al arrancar y al instalar. **Las sondas deben llamar a `InstalledWorld.ApplyQuietly`** (§91).
- Filtro anti-basura: `PK7.ChecksumValid` más los límites de PS.

## Mochila (`BagService.cs`, `Data/BagLocator.cs`, `BagLayout.cs`, `BagItemDelivery.cs`)
- Bloque de siete bolsillos seguidos en `0xE28` bytes, seguido de una tabla con **7 punteros** que se valida. Visto en `0x33011934` (§22).
- Toda escritura se relee.
- La entrega **suma**, no reemplaza. `CapacityFor` sirve para los objetos clave (§52).
- Retirar y devolver balls (regla de encuentro) se apunta en `objetos-retirados.txt` con `WithheldLedger`, que lleva el id de la run (§147).
- Escaneo caro (96 MB): solo se hace si el ping contesta.

## Zona (`Field/FieldZoneReader.cs`, `FieldRecord.cs`, `SavedGameCache.cs`)
- Registros de posición del juego (mundo y mapa delante de las coordenadas), validados con `Data/mapas.json` por `FieldRecord.Parse`.
- **Mayoría estricta de ≥2** copias (`FieldRecord.Resolve`). El ancla antigua (§23) murió (§55).
- Heap lineal `0x30000000`, 64 MB. `SearchBudget = 6` búsquedas, compartido entre el patrón del guardado, el del aterrizaje (3 páginas) y los hermanos del mapa.
- **Paginación con cursor** (`_siblingCursors`): r3.
- **`Nearby`**: 2 páginas de 4 KB alrededor de los registros validados (r3).
- Tiempos:

| Constante | Valor | Cuándo |
|---|---|---|
| `SearchEvery` | 2 min | intervalo normal entre búsquedas |
| `SearchEveryWhileDisagreeing` | 20 s | cuando no hay mayoría |
| `UnknownBeforeSearch` | 20 s | zona desconocida |
| `SettleBeforeSearch` | 5 s | espera antes de buscar al cambiar de mapa (§159) |

- `SavedSinceLastSearch`: un guardado nuevo permite volver a buscar sin esperar los 2 min (arreglo del 20/09).
- No busca durante un combate (`CurrentZone(allowSearch:false)`).
- `registros-de-posicion.txt`: direcciones de la última sesión, probadas antes de buscar.
- Lecciones:
  - basura en (1,0,0) se rechaza (§152);
  - la bolsa apaga los registros que siguen al jugador, y la zona no se deja cuando se callan (§156);
  - el registro que anda gana a la mayoría quieta al cambiar de mapa sin puerta (`WalkingWindow`, 5 s; §164).

## Contadores del juego (`Field/BattleCounterReader.cs`)
- Récords de la ficha de entrenador en memoria: `WildBattlesRecord = 4` (sube **al empezar** un combate salvaje y cuenta al huir), `CaughtRecord = 6`, `FledRecord = 46`, `ShinyRecord = 127`.
- Referencia a partir de la mochila validada, comprobada cada 5 s. La búsqueda completa, como mucho una vez por minuto.
- Una dirección ilegible se descarta tras 3 fallos (20/09).
- Los récords del **save** se leen aparte, con `SaveRecordReader` (`SAV7USUM.Records`, §38).

## Combate (`Battle/BattleTableReader.cs`, `BattleLayout.cs`, `BattleFaintTracker.cs`, `BattlePokemon.cs`, `HpBar.cs`)
- Dos tablas de bloques de 800 bytes: la de **cálculo** es instantánea; la de la **barra** va con retraso y se usa para la animación. Vistas en `0x30002748` y `0x30009730` (§114).
- Búsqueda de **1 MB cada 3 s** como mucho y solo fuera de combate. Nada más ancho: **se congeló el emulador dos veces** buscando más.
- PK7 del combatiente en **puntero `+0x40`** (`BattlePokemon.Parse`, checksum más especie). Medido en un combate salvaje; los entrenadores, los dobles y el SOS **no están medidos**.
- `HpBar` y `HpBarWatcher` leen **píxeles** de la ventana. Suponen la disposición por defecto de Azahar. El relleno son los 3 colores de la barra: el suelo naranja no cuenta (§165).

## Escritura en memoria (`AzaharGameWriter.cs`)
- `Modify` relee **solo los bytes que tocó** (§53).
- `SetLiveHp(address, hp, pid)` y `ReadAuthoritative`.
- **Siempre el PID por delante**, nunca una posición sola (§96).
- No escribe más allá del bloque cifrado en estructuras cuya cola no está identificada.
- Solo la **experiencia** decide el nivel en las estructuras sin identificar.
- Copia en `Saves/backup` antes de escribir.

## Fichero de partida (`PlayerSave.cs`, `Save*.cs`)
- `PlayerSave` localiza `<user>/sdmc/Nintendo 3DS/…/00040000001B5100/data/…`.
- **Escribir exige Azahar cerrado.** Leer se puede con él abierto, pero se ve lo último guardado.
- Escritores:

| Clase | Qué escribe |
|---|---|
| `SaveBoxDelivery` | gacha; va al equipo si cabe (§155) |
| `SaveBoxSwap` | wonder trade |
| `SaveEvTrainer` | EV |
| `SaveMoveTeacher` | recordar movimientos |
| `SaveRouletteWorld` | ruleta |
| `SaveDeathEnforcer` / `DeathMark` | caídos a 0 PS |
| `SavePidRepair`, `SaveNameRepair` | reparaciones |
| `SaveEraser` | empezar de cero; copia antes y se niega si la carpeta no es la de Ultra Luna |

- Todos hacen copia previa y relectura, y comprueban la identidad por PID.
- Importar un Pokémon con PKHeX sube los récords si no se desactiva: `EntityImportSettings` con los récords en `Disable` (§42).
- `PokemonBuilder` pone PID y EC al azar y corrige el brillo después (§56).
- El nivel de las especies del mod usa las curvas del mundo, no las de PKHeX (§134).
- Habilidad de **9 bits** (bit 4 de `0x15`; §132).
