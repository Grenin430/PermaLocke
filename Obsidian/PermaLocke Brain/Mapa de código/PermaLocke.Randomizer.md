---
tipo: mapa-codigo
proyecto: PermaLocke.Randomizer
generado: 2026-09-25
---
# PermaLocke.Randomizer — mapa de ficheros

Generado de la primera frase del `<summary>` de cada fichero (`src/PermaLocke.Randomizer/`). Tipos en negrita. Vuelve a [[00 - Inicio]] · [[Arquitectura]].

- `Modules/AbilityTable.cs` — **AbilityTable** — Which abilities a randomized species may be given. 
- `Modules/BannedMoveScrubber.cs` — **BannedMoveResult, BannedMoveScrubber** — Takes the banned moves out of everywhere a Pokémon can get a move from (§162). 
- `Modules/EncounterTable7.cs` — **EncounterTable7** — Byte-level view of one Ultra Sun/Moon wild encounter table, living inside the decompressed payload of an encdata subfile. PermaLocke reads the layout with pk3DS but does not write with it: pk3DS's ...
- `Modules/EvolutionTable.cs` — **EvolutionTable** — Who evolves into whom, read out of a/0/1/4. 
- `Modules/ExtraPokemonRandomizer.cs` — **ExtraPokemonResult, ExtraPokemonRandomizer** — Gives the important battles the extra Pokémon the role asks for. 
- `Modules/FieldItemRandomizer.cs` — **FieldItemResult, FieldItemRandomizer** — Rewrites the items lying on the ground: ordinary Poké Balls for items, gold ones for TMs. In both modes a TM becomes another TM and an ordinary item another ordinary item, and only the spots the c...
- `Modules/FieldItemTable.cs` — **FieldItemTable** — Finds every field item of a zone: the Poké Balls lying on the ground, gold ones for TMs, and the berry piles. They live in subfile zone * 11 of encdata, which is an ED mini pack; entry 10 of it is...
- `Modules/ImpossibleEvolutionFixer.cs` — **EvolutionFixResult, ImpossibleEvolutionFixer** — Rewrites the evolutions a solo player can never reach. 
- `Modules/LearnsetPlanner.cs` — **LearnsetRules, LearnsetPlanner** — Chooses the moves of one Pokémon's level-up learnset. 
- `Modules/MachineCompatibilityRandomizer.cs` — **MachineCompatibility, MachineCompatibilityResult, MachineCompatibilityRandomizer** — How the TM compatibility bits are dealt again.
- `Modules/MachineFlags.cs` — **MachineFlags** — Which TMs a species can be taught: one bit each, inside its personal entry. 
- `Modules/MachineRandomizer.cs` — **MachineResult, MachineRandomizer** — Shuffles which move each TM teaches, inside the game's own executable. 
- `Modules/MachineTable.cs` — **MachineTable** — The list of which move each TM teaches, which lives in code.bin and nowhere else. 
- `Modules/MegaTrainerRandomizer.cs` — **MegaTrainerResult, MegaTrainerRandomizer** — Gives the late important battles one Pokémon that is already mega evolved. 
- `Modules/MoveFacts.cs` — **MoveFacts, MoveCatalog** — What a level-up randomizer needs to know about one move. 
- `Modules/MoveTable.cs` — **MoveTable** — The cartridge's move table, read for the one thing the randomizer needs from it: which moves may be handed out and which must never be. 
- `Modules/NameLocalizer.cs` — **LocalizedNames, NameLocalizer** — What extending one list of names did.
- `Modules/PersonalEntry7.cs` — **PersonalEntry7** — Byte-level view of one entry of the personal table (a/0/1/7): base stats, types and abilities of a species or of one of its forms. The GARC holds 977 subfiles: 976 individual entries and, as the la...
- `Modules/PokemonDataRandomizer.cs` — **PokemonDataResult, PokemonDataRandomizer** — Rewrites what a species is: types, base stats, abilities, what it evolves into and what it learns by level. All three GARCs are uncompressed and every edit keeps its entry the same length, so all o...
- `Modules/RegionalForms.cs` — **RegionalFormEntry, RegionalForms** — A species and the regional forms it may appear in, as configured.
- `Modules/ShopRandomizer.cs` — **ShopResult, SpecialStock, ShopRandomizer** — What stocking the special counters did, before anything is priced or saved.
- `Modules/ShopTable.cs` — **ShopInventory, ShopTable** — Byte-level view of the mart inventories inside Shop.cro. A CRO is a relocatable module, not a GARC. Editing one does not work on real hardware unless the RO module's signature check is patched, whi...
- `Modules/SpeciesPool.cs` — **SpeciesPool** — The set of species a module may hand out, and how it picks one. Built from base stat totals rather than from pk3DS directly, so the selection rules can be tested without a cartridge. 
- `Modules/StarterReader.cs` — **StarterChoice, StarterReader** — Reads back the three starters from a mod folder, so they can be shown at the moment the game asks the player to choose. 
- `Modules/StarterTextRandomizer.cs` — **StarterLabel, StarterTextResult, StarterTextRandomizer** — One starter as the story text names it: its name, and its types in words.
- `Modules/StaticEncounterRandomizer.cs` — **StaticEncounterResult, StaticEncounterRandomizer** — Rewrites the starters, the eleven fossils, gifts, static encounters, totems and the species received in in-game trades. All of it lives in one 20 KB GARC. 
- `Modules/StaticEncounterTable.cs` — **EncounterEntryLayout, StaticEncounterTable** — Byte-level view of the fixed-size tables in a/1/5/9: starters, the eleven fossils, gifts, static encounters, totems and in-game trades. The six subfiles are uncompressed, so these tables are patche...
- `Modules/TrainerDifficultyRandomizer.cs` — **TrainerDifficultyResult, TrainerDifficultyRandomizer** — Makes the trainers harder without touching who they are: their AI byte, their IVs, their EV spreads and how many of their Pokémon hold an item. 
- `Modules/TrainerPokemonTable.cs` — **TrainerPokemonTable** — Byte-level view of one trainer's party inside trpoke (a/1/0/7): a flat run of fixed-size entries, one per Pokémon, one subfile per trainer. Both trainer GARCs are uncompressed, so parties are patc...
- `Modules/TrainerRandomizer.cs` — **TrainerResult, TrainerRandomizer** — Replaces the species of every trainer Pokémon in trpoke (a/1/0/7), and raises their levels by whatever the role asks for. The randomization of species still never touches levels: the competition's...
- `Modules/TutorRandomizer.cs` — **TutorResult, TutorRandomizer** — Shuffles what the move tutors teach. 
- `Modules/TutorTable.cs` — **TutorTable** — The move tutors' list, which lives in code.bin and nowhere else. 
- `Modules/WildEncounterRandomizer.cs` — **WildEncounterResult, WildEncounterRandomizer** — Rewrites the wild encounter tables of Ultra Moon. The encdata GARC is ~460 MB because each area ships eleven subfiles and only one of them is encounters; the rest is map data. Its subfiles are LZ11...
- `Modules/WorldMoveTables.cs` — **WorldMoveEntry, WorldMoveTables** — One move of a world's move table, with its category already translated.
- `Output/LayeredFsMod.cs` — **LayeredFsMod** — The mod folder Azahar reads: &lt;user&gt;/load/mods/&lt;ProgramId&gt;/romfs/. Azahar turns LayeredFS on merely because that folder exists — there is no setting to enable (ncch_container.cpp, use_...
- `Output/ModInstaller.cs` — **ModInstaller** — Puts a generated mod into the emulator's load folder, base layer included. 
- `RandomizerOptions.cs` — **SpeciesPickMode, FieldItemsMode, MartItem, MartShelf, RandomizerOptions** — How a replacement species is chosen.
- `RandomizerOptionsLoader.cs` — **RandomizerOptionsLoader** — Reads Data/randomizer.json into a `andomizerOptions`.
- `RandomizerService.cs` — **RandomizerStep, RandomizationReport, RandomizerService** — Files taken from a base layer instead of from the cartridge. Empty is the norm.
- `Rom/AlwaysShinyPatch.cs` — **AlwaysShinyPatch** — A test switch that makes every Pokémon the game generates shiny, as an IPS patch Azahar applies at boot. 
- `Rom/GameFiles.cs` — **GameFiles** — The RomFS paths PermaLocke touches, for Pokémon Ultra Moon. Taken from pk3DS's GARCReference_UM table and confirmed against the real cartridge: the ROM has exactly 333 files under a/, which is how...
- `Rom/GameTextPatch.cs` — **GameTextPatch** — Replaces a few lines of one of the game's text files and leaves every other byte where it was. 
- `Rom/GarcPatcher.cs` — **GarcPatcher** — Overwrites subfiles inside a GARC without repacking the container. Only valid when the replacement is exactly as long as the original, which is the case for every fixed-size game table. Repacking w...
- `Rom/RomFsReader.cs` — **RomFsEntry, RomFsReader** — One file inside the cartridge's RomFS, addressed absolutely within the .3ds.
- `Rom/RomInspector.cs` — **RomInfo, RomInspector** — Reads the NCSD/NCCH headers of a 3DS cartridge dump. Opens the file read-only and never writes to it: the vanilla ROM is untouchable. 
- `Rom/RomWorkspace.cs` — **RomWorkspace** — A scratch copy of the handful of RomFS files the randomizer needs, plus the pk3DS `ameConfig` that reads them. The vanilla cartridge is opened read-only and never written to. Extracting only these ...
- `Sprites/BflimTexture.cs` — **BflimFormat, BflimTexture** — How the pixels of a BFLIM are encoded. Only what Ultra Moon's icons actually use.
- `Sprites/Etc1Texture.cs` — **Etc1Texture** — Decodes the 3DS's ETC1 and ETC1A4 textures, which is where the cartridge keeps its big artwork. 
- `Sprites/IslandMapReader.cs` — **IslandMap, IslandMapReader** — The four island maps of Alola, cut out of the player's own cartridge. 
- `Sprites/ItemIconIndex.cs` — **ItemIconIndex** — Which icon of a/0/6/1 belongs to which item. 
- `Sprites/ItemIconReader.cs` — **ItemIconReader** — Reads the item icons out of the player's own cartridge. 
- `Sprites/MoveCategoryIconReader.cs` — **MoveCategoryIcon, MoveCategoryIconReader, AlytCarver** — Which of the three a move is: the icons the game draws next to a move.
- `Sprites/PngImage.cs` — **PngImage** — A minimal PNG encoder for RGBA8888 buffers. Written by hand on purpose: System.Drawing is Windows-only and WPF's imaging lives behind PresentationCore, and PermaLocke.Randomizer is not allowed to r...
- `Sprites/PokemonIconIndex.cs` — **PokemonIconIndex** — Which icon of a/0/6/2 belongs to which species. 
- `Sprites/PokemonIconReader.cs` — **PokemonIcon, PokemonIconReader** — One decoded box icon: straight RGBA8888, already cropped to what is drawn.
- `Sprites/ShinyPalette.cs` — **ShinyPalette** — Works out how a box icon changes colour when its Pokémon is shiny, from a pair of reference renders. 
- `Sprites/ZCrystalIconReader.cs` — **ZCrystalIconReader** — Reads the eighteen type Z-Crystals out of the player's own cartridge. 
- `Sprites/ZCrystalIndex.cs` — **ZCrystalIndex** — Which of the eighteen carved Z-Crystals belongs to which item. 
- `StaticOverride.cs` — **StaticOverrideRule** — What a static encounter is replaced by, when the ordinary draw is not what is wanted.
- `TrainerDifficultyOptions.cs` — **TrainerHeldItem, TrainerDifficultyOptions** — How much harder the trainers are than the cartridge made them, beyond the role's levels. 
- `TrainerMinimum.cs` — **TrainerMinimum** — 
