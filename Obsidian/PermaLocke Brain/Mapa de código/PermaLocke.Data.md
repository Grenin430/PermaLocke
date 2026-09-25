---
tipo: mapa-codigo
proyecto: PermaLocke.Data
generado: 2026-09-26
---
# PermaLocke.Data — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Data/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `JsonAchievementCatalog.cs` — **JsonAchievementCatalog** — Reads Data/achievements.json: what the run rewards and with how much.
- `JsonCreditCatalog.cs` — **JsonCreditCatalog** — Reads Data/grants.json: what each milestone hands over in free rolls and trades.
- `JsonGachaCatalog.cs` — **JsonGachaCatalog, JsonSpeciesStatsCatalog** — Reads Data/gacha.json: the banners, their cost and their odds.
- `JsonIslandMap.cs` — **MapZone, JsonIslandMap** — Reads Data/islas.json: which island each of Alola's zones belongs to. 
- `JsonMapTable.cs` — **JsonMapTable** — Reads Data/mapas.json, the game's maps with their world and zone, into a `apTable`. 
- `JsonPenaltyCatalog.cs` — **JsonPenaltyCatalog** — Reads Data/penalties.json: what losing costs.
- `JsonPlayerProfileStore.cs` — **JsonPlayerProfileStore** — This machine's player, as Config/jugador.json. 
- `JsonPlaytimeStore.cs` — **JsonPlaytimeStore** — Play sessions as Saves/&lt;run&gt;/sesiones.json, beside the run they belong to.
- `JsonRewardCatalog.cs` — **JsonRewardCatalog** — Reads Data/rewards.json: what the competition hands over once, and for what.
- `JsonRoleCatalog.cs` — **JsonRoleCatalog** — Reads Data/roles.json: the ways the competition can be played.
- `JsonRouletteCatalog.cs` — **JsonRouletteCatalog** — Reads Data/roulette.json: the faces of the LUDÓPATA wheel and what they do.
- `JsonRunRepository.cs` — **JsonRunRepository** — Stores each run as Saves/&lt;runId&gt;/run.json. Kept as readable JSON on purpose: the seed, the ROM hash and the randomizer options must be inspectable without tooling. 
- `JsonShopCatalog.cs` — **JsonShopCatalog** — Reads Data/shop.json: what the shop sells, in what order and for how much.
- `JsonWonderTradeCatalog.cs` — **JsonWonderTradeCatalog** — Reads Data/wondertrade.json: how wide the band of an acceptable trade is.
- `JsonZoneMarkers.cs` — **ZoneMarker, JsonZoneMarkers** — Where each zone's marker sits on its island, in Data/marcadores.json. 
- `JsonZonePhotos.cs` — **JsonZonePhotos** — The picture of each zone, from Data/fotos.json. 
- `RunBackup.cs` — **RunBackupResult** — Copies the run database before anything opens it, and keeps the last few copies. 
- `ServiceCollectionExtensions.cs` — **ServiceCollectionExtensions** — 
- `SqliteEventStore.cs` — **SqliteEventStore** — Append-only event log on SQLite. One database for every run, so PermaLocke.Admin can query across them. Inserts are serialized: the hash chain requires reading the tail and writing the new row as o...
- `SqlitePokemonRepository.cs` — **SqlitePokemonRepository** — Pokémon of every run, in the same database as the event log. Unlike the log this table is mutable — a Pokémon dies, is released, changes level — but every mutation is paired with an event by ...
