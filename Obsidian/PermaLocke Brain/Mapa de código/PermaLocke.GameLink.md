---
tipo: mapa-codigo
proyecto: PermaLocke.GameLink
generado: 2026-09-26
---
# PermaLocke.GameLink — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.GameLink/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `AzaharExecutable.cs` — **EmulatorChoice, AzaharExecutable** — Which azahar.exe the launcher starts (§125). 
- `AzaharGameStateProvider.cs` — **AzaharGameStateProvider** — Live link to Ultra Moon running in Azahar, over the emulator's RPC server. 
- `AzaharGameWriter.cs` — **MemoryWriteResult, AzaharGameWriter** — What happened to a write into the game's memory. 
- `AzaharInstallation.cs` — **AzaharLocation, AzaharInstallation** — Finds the Azahar the player is going to use and makes sure it is configured for PermaLocke. Azahar picks its user directory itself, and the rule is in its own source: if a folder named user sits ne...
- `BagItemDelivery.cs` — **BagItemDelivery** — Hands an item over by writing it into the bag of the running game. 
- `BagService.cs` — **BagWriteOutcome, BagWriteResult, BagService** — Why a bag write did or did not happen. The UI reports this, it never guesses.
- `Battle/BattleFaintTracker.cs` — **BattleFaint, BattleFaintTracker** — A Pokémon that has just fainted in the battle in progress.
- `Battle/BattleLayout.cs` — **BattleBlock, BattleLayout, BattleTable** — One Pokémon as the battle holds it, read from its block.
- `Battle/BattlePokemon.cs` — **BattlePokemon** — The whole Pokémon behind a battle block: PID, OT, and so whether it is shiny. 
- `Battle/BattleTableReader.cs` — **BattleTableReader** — Reads the battle tables from the running game without ever searching memory in a loop. 
- `Battle/HpBar.cs` — **HpBarState, HpBarReading** — What the player's HP bar shows on the battle screen.
- `Clock/AlolaClock.cs` — **AlolaPeriod, AzaharClockSettings, AlolaClock** — The four parts of the day the seventh generation tells apart.
- `Data/BagLayout.cs` — **BagEntry, BagPocket, BagLayout** — One bag entry as generation 7 packs it: a single 32 bit word. 
- `Data/BagLocator.cs` — **BagBlock, BagSlot, BagLocator** — The bag block found in the running game.
- `Data/DeathMark.cs` — **DeathMark** — Marks a Pokémon in the save as one of the run's dead: no HP left, and nothing else changed. 
- `Data/GameLevels.cs` — **GameLevels** — The level of a Pokémon as the game being played computes it. 
- `Data/PartyLayout.cs` — **PartyLayout, IPartyLayoutLocator** — Every copy of the party, found from the encryption constants of the saved party. Empty when none is in memory. 
- `Data/PartyLocator.cs` — **PartyLocator** — Finds the party block in the running game's memory. 
- `Data/PartyStats.cs` — **PartyStats** — Whether a party entry really keeps its battle stats where a party `K7` puts them. 
- `Data/Pk7Reader.cs` — **LivePokemon, Pk7Reader** — Reads party structures out of the running game and parses them with PKHeX.Core. 
- `Data/PkhexAbilityLookup.cs` — **PkhexAbilityLookup** — Ability names out of PKHeX, in the language the player reads.
- `Data/PkhexItemLookup.cs` — **PkhexItemLookup** — Item names from PKHeX, which ships the cartridge item list for generation 7.
- `Data/PkhexLocationLookup.cs` — **PkhexLocationLookup** — Zone names from PKHeX's Ultra Sun / Ultra Moon location tables.
- `Data/PkhexSpeciesLookup.cs` — **PkhexSpeciesLookup** — Species names from PKHeX.Core, so PermaLocke neither hardcodes a Pokédex nor invents one. Limited to generation 7, which is the last one Ultra Sun and Ultra Moon know about. 
- `Data/PkhexTypeLookup.cs` — **PkhexTypeLookup** — Types of a species: from the installed world's own table when it has published one, from PKHeX's Ultra Sun / Ultra Moon table otherwise. 
- `Data/PokemonAbility.cs` — **PokemonAbility** — The ability of a Pokémon as the game being played stores it: one byte, plus a ninth bit. 
- `Data/PokemonBuilder.cs` — **NewPokemon, PokemonBuilder** — What a Pokémon PermaLocke is about to put in the game looks like.
- `Data/StatCalculator.cs` — **StatCalculator** — Works out the six battle stats a party Pokémon should be showing. 
- `Data/WorldLimits.cs` — **WorldLimits** — How big the world being played is, for the sanity filters that separate a real Pokémon from heap rubbish. 
- `Data/WorldMoveCatalog.cs` — **WorldMoveCatalog** — Learnsets and moves from the installed world when it has published them, from PKHeX's Ultra Sun / Ultra Moon tables otherwise. 
- `Data/WorldMoves.cs` — **WorldMove, WorldMoves** — What one move is in the installed world, before any name is attached.
- `Field/BattleCounterReader.cs` — **BattleCounterReader** — The trainer card counters, read live: how many wild battles, captures, escapes and shinies. 
- `Field/FieldRecord.cs` — **FieldRecord** — One of the game's records of where the player is: world and map, then the position and the rotation. 
- `Field/FieldZoneReader.cs` — **FieldZoneReader** — Says which map, and so which zone of the run, the player is on, read live from the game. 
- `Field/SaveDex.cs` — **SaveDex** — The species the Pokédex of the last save says were caught. Only reads. 
- `Field/SavedGameCache.cs` — **SavedGameCache** — The player's last save, parsed once and parsed again only when the file changes. Only reads. 
- `PlayerSave.cs` — **PlayerSave** — Where the player's Ultra Moon save lives, and whether the emulator is holding it right now. 
- `Rpc/AzaharRpcClient.cs` — **RpcRequestType, MemoryWrite, EmulatedProcess, AzaharRpcException, AzaharRpcClient** — Only in PermaLocke's Azahar fork. The official build rejects it.
- `Rpc/MemorySearch.cs` — **MemoryRegion, MemorySearch** — Finds where the game keeps its data, by scanning the emulated address space over the RPC. 
- `Rpc/RpcTrace.cs` — **RpcTraceEntry** — The last requests sent to the emulator, kept so that a crash report can say what PermaLocke was doing at the moment Azahar went down. 
- `SaveBoxDelivery.cs` — **SaveBoxDelivery** — Delivers a Pokémon by writing it into the player's save file: into the party when it has room, into a box otherwise. 
- `SaveBoxReader.cs` — **SaveBoxReader** — Reads the player's PC boxes out of the save file. 
- `SaveBoxSwap.cs` — **SaveBoxSwap** — Performs a wonder trade on the player's save: the Pokémon handed over is replaced, in its own slot, by the one that came back. 
- `SaveDeathEnforcer.cs` — **DeathEnforcementReport, SaveDeathEnforcer** — Leaves the run's dead at zero HP inside the save file, as themselves. 
- `SaveEraser.cs` — **SaveErasure, SaveEraser** — Deletes the player's Ultra Moon save, after copying it somewhere they can get it back from. 
- `SaveEvTrainer.cs` — **SaveEvTrainer** — Writes effort values into the player's save, in the slot the Pokémon already occupies. 
- `SaveGameUnlocks.cs` — **SaveGameUnlocks** — Turns on the saved game's own unlock flags. 
- `SaveMoveTeacher.cs` — **SaveMoveTeacher** — Writes one remembered move into the player's save, in the slot the Pokémon already occupies. 
- `SaveNameRepair.cs` — **NameRepairReport, SaveNameRepair** — Puts the species name back on the Pokémon PermaLocke delivered without one. 
- `SavePidRepair.cs` — **PidAssignment, PidRepairReport, SavePidRepair** — Gives a personality value to the Pokémon PermaLocke delivered without one. 
- `SaveRecordReader.cs` — **SaveRecordReader** — Reads the game's own record counters out of the save file. 
- `SaveRenamer.cs` — **SaveRenamer** — Writes a nickname into the player's save, in the slot the Pokémon already occupies (2026-09-26). 
- `SaveRouletteWorld.cs` — **SaveRouletteWorld** — Everything the LUDÓPATA wheel does to the player's game, against the save file. 
- `ServiceCollectionExtensions.cs` — **ServiceCollectionExtensions** — The encryption constants of the party in the last save, or null when there is no save: what tells the provider whether a full sweep has anything to find. 
- `WithheldLedger.cs` — **WithheldLedger** — What the first-encounter rule has taken out of the bag and owes back, and which run it owes it to. 
- `WorldStatForecast.cs` — **WorldStatForecast** — Works out a Pokémon's stats with other EVs, from the installed world's own base stats. 
