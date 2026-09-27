using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer;

/// <summary>How a replacement species is chosen.</summary>
public enum SpeciesPickMode
{
    /// <summary>Every slot is rolled on its own. Maximum variety.</summary>
    PerSlot,

    /// <summary>
    /// One species always becomes the same other species, everywhere. The classic randomizer
    /// mapping: knowing what a Pidgey turned into tells you what every Pidgey turned into.
    /// </summary>
    OneToOne,
}

/// <summary>How the items lying on the ground are randomized (§163).</summary>
public enum FieldItemsMode
{
    /// <summary>
    /// What the cartridge placed, shuffled among the same spots: TMs among TMs, the rest among the rest. Universal Pokémon
    /// Randomizer's «shuffle». Nothing appears that the game did not already put on the ground.
    /// </summary>
    Shuffle,

    /// <summary>
    /// Every spot drawn at random, no item more than a few times: any general item, medicine or berry in the ordinary
    /// balls and berry piles, any TM in the gold ones. What the reference competition's world does.
    /// </summary>
    Random,
}

/// <param name="Id">The cartridge's own item id, which is what gets written into the shop.</param>
/// <param name="Name">
/// What that id is called, checked against the cartridge's item table before anything is written.
/// An id that lands on the wrong item stocks the wrong thing and never fails, so the name is not
/// documentation here: it is the guard (§52).
/// </param>
/// <param name="Price">
/// What this one item costs, when it must differ from the rest of its list; zero takes the list's
/// price. Exists for the Gimmighoul Coin: Gholdengo needs 999 of them, so at the list's 50000 the
/// evolution could never be bought (§145).
/// </param>
public sealed record MartItem(int Id, string Name, int Price = 0);

/// <summary>A special counter given a fixed list of its own, at its own price.</summary>
/// <param name="Shop">
/// The inventory's index inside <c>Shop.cro</c>. The cartridge does not say which counter belongs to
/// which town, so this comes from measuring in the game or from pk3DS's labels (§145).
/// </param>
/// <param name="Place">Where the player finds it, in words. Only for people: nothing reads it.</param>
/// <param name="Price">What each item costs, everywhere: the price lives in the item table. Zero leaves it.</param>
/// <param name="Items">What it sells, in shelf order. Slots left over get the filler item.</param>
public sealed record MartShelf(int Shop, string Place, int Price, IReadOnlyList<MartItem> Items);

/// <summary>
/// What to randomize and how. Lives in <c>Data/randomizer.json</c>, never in the code, so the
/// admin can change the competition's rules without a rebuild.
/// </summary>
public sealed record RandomizerOptions
{
    /// <summary>Wild encounters. The only module whose output is large: ~460 MB.</summary>
    public bool WildEncounters { get; init; } = true;

    /// <summary>
    /// The Poké Balls lying on the ground: ordinary ones hold an item, gold ones a TM. They live
    /// in the same GARC as the wild encounters.
    /// </summary>
    public bool FieldItems { get; init; } = true;

    /// <summary>How the field items are randomized: shuffled among themselves, or drawn at random (§163).</summary>
    /// <remarks>Shuffle by default, so a world generated from a file that does not say keeps coming out the same.</remarks>
    public FieldItemsMode FieldItemsMode { get; init; } = FieldItemsMode.Shuffle;

    /// <summary>Items the random draw never places on the ground. Only for <see cref="FieldItemsMode.Random"/>.</summary>
    public IReadOnlyList<int> FieldItemsBanned { get; init; } = [];

    /// <summary>How many times the random draw may place the same item. Only for <see cref="FieldItemsMode.Random"/>.</summary>
    public int FieldItemsMaxRepeats { get; init; } = 2;

    /// <summary>Starters, the eleven fossils, gifts, statics and totems.</summary>
    public bool StaticEncounters { get; init; } = true;

    public bool Trainers { get; init; } = true;

    /// <summary>Types, base stats, abilities, learnsets and evolutions.</summary>
    public bool PokemonData { get; init; }

    /// <summary>Special mart stock. Lives in <c>Shop.cro</c>, not in a GARC.</summary>
    public bool SpecialMarts { get; init; }

    public SpeciesPickMode PickMode { get; init; } = SpeciesPickMode.PerSlot;

    /// <summary>
    /// Keep the replacement near the original's base stat total, so an early route does not
    /// hand out a pseudo-legendary. Zero disables the band.
    /// </summary>
    public bool SimilarStrength { get; init; } = true;

    /// <summary>How far the base stat total may stray, as a fraction, when matching strength.</summary>
    public double StrengthTolerance { get; init; } = 0.15;

    /// <summary>
    /// Copy the base slots over the SOS and weather slots, so an SOS call brings the same
    /// species that appeared. Off means those slots are rolled too.
    /// </summary>
    public bool MirrorSosSlots { get; init; }

    /// <summary>The three starters must differ from each other.</summary>
    public bool DistinctStarters { get; init; } = true;

    /// <summary>
    /// The three starters must be the first stage of a family with three stages.
    /// </summary>
    /// <remarks>
    /// What the competition asks: a starter is what you carry all game, so it should have two
    /// evolutions ahead of it rather than being something that never grows or is already finished.
    /// Worked out from the cartridge's own evolution table, not from a list of species.
    /// <para>
    /// Read before the evolution lines are touched, because the starters are chosen first. With
    /// <see cref="RandomizeEvolutions"/> on, the guarantee is therefore about the families the
    /// cartridge has, which is worth knowing and is why the report says so.
    /// </para>
    /// </remarks>
    public bool StartersWithTwoEvolutions { get; init; } = true;

    /// <summary>
    /// Make the starter scene's text name the Pokémon the gift entries now hold, instead of Rowlet,
    /// Litten and Popplio. Off unless the file asks for it. See <see cref="Modules.StarterTextRandomizer"/>.
    /// </summary>
    public bool StarterText { get; init; }

    /// <summary>
    /// Rewrite the evolutions a solo player can never reach: trades, and moves once the learnsets
    /// are randomized.
    /// </summary>
    public bool FixImpossibleEvolutions { get; init; } = true;

    /// <summary>What a plain trade evolution becomes: level up at this level.</summary>
    public int TradeEvolutionLevel { get; init; } = 37;

    /// <summary>
    /// Give the late important battles one Pokemon that is already mega evolved.
    /// </summary>
    public bool MegaTrainers { get; init; } = true;

    /// <summary>
    /// Baraja qué movimiento enseña cada MT. Solo funciona sobre un mod que traiga su code.bin.
    /// </summary>
    /// <remarks>
    /// La lista de las cien MT no está en el RomFS: son cien ids de movimiento dentro del
    /// ejecutable. Por eso, hasta ahora, PermaLocke movía las MT de sitio pero la MT54 seguía
    /// siendo Falso Tortazo. Se barajan entre ellas, no se sortean de la tabla entera, así que el
    /// conjunto sigue siendo el mismo y ninguna MT acaba enseñando algo que no valga una MT.
    /// </remarks>
    public bool RandomizeMachines { get; init; } = true;

    /// <summary>
    /// Cartridge level a trainer has to reach before one of its Pokemon may be a mega.
    /// </summary>
    /// <remarks>
    /// The ROM is randomized once, before the run starts, so it cannot know the player has cleared
    /// six trials. What it can do is look at how strong the battle is, which is the same thing seen
    /// from the other side. 29 is the first level past the sixth trial, as the player asked on
    /// 2026-09-21: that trial is Olivia's grand trial, a level 28 battle whose cap is 34, and §48
    /// measured that a cap is its boss raised a fifth. Cartridge levels, not screen levels, so every
    /// role gets its megas in the same battles.
    /// </remarks>
    public int MegaTrainerMinimumLevel { get; init; } = 29;

    /// <summary>
    /// Trainer classes whose Pokémon are drawn from a floor instead of from the whole pool.
    /// </summary>
    /// <remarks>
    /// It exists for the league. Similar-strength drawing keeps a replacement near what it
    /// replaced, which is right almost everywhere and wrong at the end: the Elite Four's cartridge
    /// teams are not uniformly strong, so half of a final battle could come out as things nobody
    /// would bring to one.
    /// </remarks>
    public IReadOnlyList<TrainerMinimum> TrainerMinimums { get; init; } = [];

    /// <summary>
    /// Static encounters that get their own rule instead of the ordinary draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are addressed by <b>the species and form the cartridge has there</b>, not by the row's
    /// index. An index is a number that means whatever is at it: if the table ever shifts, entry
    /// 160 quietly becomes some other encounter and the rule lands on the wrong one without
    /// failing. Species plus form says what it is looking for, so it can also say when it is not
    /// there — and the randomizer stops rather than guessing.
    /// </para>
    /// <para>
    /// The rules exist because two of these battles are not ordinary encounters: Ultra Necrozma is
    /// the roof of the story and gets a mega, while the Necrozma and Lunala the player can actually
    /// keep should be worth keeping, which is what the minimum total is for.
    /// </para>
    /// </remarks>
    public IReadOnlyList<StaticOverride> StaticOverrides { get; init; } = [];

    /// <summary>
    /// Items the ordinary Pokémon Center counters stop selling, and what they sell instead.
    /// </summary>
    /// <remarks>
    /// The eight ordinary inventories are otherwise left alone, because they are where a Nuzlocke
    /// buys its balls. This turns the six status medicines into more of them. The name travels with
    /// the id and is checked against the cartridge before anything is written: an id typed from
    /// memory that lands on another item stocks the wrong thing and never fails (§52).
    /// </remarks>
    public IReadOnlyList<MartItem> RegularMartReplaced { get; init; } =
    [
        new(17, "Poción"), new(18, "Antídoto"), new(19, "Antiquemar"),
        new(20, "Antihielo"), new(21, "Despertar"), new(22, "Antiparalizador"),
    ];

    public MartItem RegularMartReplacement { get; init; } = new(4, "Poké Ball");

    /// <summary>
    /// What the BP counter of the Mantine Surf beaches sells, in order, to turn into <see cref="RegularMartReplacement"/>
    /// (2026-09-27). Empty leaves it alone. See <see cref="Modules.ShopRandomizer.ReplaceBattlePointItems"/>.
    /// </summary>
    public IReadOnlyList<MartItem> BattlePointShopReplaced { get; init; } = [];

    /// <summary>
    /// Nobody can learn anything from the BP tutors (2026-09-27): every tutor bit of every species goes to zero, in the
    /// table and in each row's own copy. The tutors still stand there; they have nobody to teach.
    /// </summary>
    public bool BanTutors { get; init; }

    /// <summary>
    /// Blank a trainer Pokémon's moves when its species changes, so the game builds a moveset
    /// from the new species' learnset instead of keeping one picked for the old one.
    /// </summary>
    public bool TrainerMovesFromLearnset { get; init; } = true;

    /// <summary>
    /// Percentage added to every trainer Pokémon's level. Zero leaves the cartridge alone.
    /// </summary>
    /// <remarks>
    /// This comes from the <b>role</b>, not from the randomizer's own configuration, and it is the
    /// reason the role has to be chosen before anything is generated: two players on different
    /// roles do not get the same world, and changing role later means randomizing again.
    /// </remarks>
    /// <summary>
    /// From this cartridge level up, every trainer Pokémon comes fully evolved. Zero turns it off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule the competition asked for is «after the sixth trial», and that is a point in the
    /// story. The mod is generated once, before a single trial is done, so the story is not
    /// something it can consult: the only thing the trainer table carries that tracks progress is
    /// the level the cartridge gave each team. Hence a level, and hence <b>34</b>, which is the
    /// sixth trial's own cap.
    /// </para>
    /// <para>
    /// It is compared against the level the <b>cartridge</b> shipped, never the one the role raised.
    /// A role puts every enemy 20-27% higher, so a trainer the cartridge placed at 30 arrives at 36
    /// and would cross the line while still being, in the story, well before the sixth trial. The
    /// threshold asks «when does this trainer appear», and only the original level answers that.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Special counters to stock first, by index, before falling back to index order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The evolution items spill from one counter to the next as each fills up, and which counter
    /// comes first decides how far the player has to walk. Left in index order they start at 8,
    /// which is Konikoni -- most of the way through the island chain -- and reach three counters.
    /// </para>
    /// <para>
    /// The two indices anchored so far were <b>measured by the player in the running game</b>, which
    /// is the only way there is: the cartridge does not say which counter belongs to which town.
    /// Konikoni is 8, identified from the five species its stock could evolve. Hau'oli City's
    /// Pokémon Center is 10 and Route 2's is 11, identified from what each was selling. Eight
    /// slots plus twelve is twenty, and there are eighteen items: the whole list fits in the two
    /// earliest counters in the game.
    /// </para>
    /// <para>
    /// Since §145 Route 2 sells a list of its own (<see cref="SpecialMartShelves"/>), and the rest of
    /// the indices come from pk3DS's labels for Ultra Sun and Ultra Moon: 15 Paniola Town, 14 Route 8,
    /// 21 to 23 the Thrifty Megamart. <b>Those labels are not all right</b>: 24 says Route 3, and the
    /// player found no Pokémon Center there, so where 24 is remains unknown.
    /// </para>
    /// </remarks>
    /// <summary>What every TM sold in a Pokémon Center counter costs. Zero leaves prices alone.</summary>
    /// <remarks>
    /// It is the price of the ITEM, not of the shelf, so it applies wherever that TM is sold and
    /// wherever the game quotes it. Only the machines actually placed in a counter are touched:
    /// repricing all hundred would change the ones the player finds on the ground too, and those
    /// are not bought.
    /// </remarks>
    /// <summary>
    /// Shuffles what the move tutors teach. Needs a world that brings its own <c>code.bin</c>.
    /// </summary>
    /// <remarks>
    /// The tutors are the stalls that charge BP -- the Mantine Surf ones on the beaches among them
    /// -- and their list is not in the RomFS: it is sixty-seven move ids inside the executable.
    /// Same condition as the TMs, so a plain cartridge leaves it alone rather than pretending.
    /// </remarks>
    /// <summary>How which TMs each species can learn is dealt again.</summary>
    /// <remarks>
    /// <c>PreferType</c> is how Universal Pokémon Randomizer does it and is the default: a roll per
    /// TM, nine in ten when the move shares a type with the species, one in two for a Normal move,
    /// one in four otherwise. <c>Shuffle</c> keeps every species' total and only moves which ones,
    /// which is safer and duller — a Magikarp stays useless and a Mew stays universal.
    /// </remarks>
    public MachineCompatibility MachineCompatibility { get; init; } = MachineCompatibility.PreferType;

    public bool RandomizeTutors { get; init; } = true;

    public int MachineMartPrice { get; init; }

    public int[] SpecialMartOrder { get; init; } = [];

    public int FullyEvolvedFromLevel { get; init; }

    public int EnemyLevelPercent { get; init; }

    /// <summary>Smarter trainers with better Pokémon. See <see cref="TrainerDifficultyOptions"/>.</summary>
    public TrainerDifficultyOptions TrainerDifficulty { get; init; } = new();

    /// <summary>Pokémon added to each important battle. Zero leaves the parties alone.</summary>
    /// <remarks>Also from the role. See <see cref="ImportantTrainerClasses"/> for what counts.</remarks>
    public int ExtraTrainerPokemon { get; init; }

    /// <summary>
    /// Trainer classes whose battles count as important, by id.
    /// </summary>
    /// <remarks>
    /// By id and not by name: Giovanni and the Rainbow Rocket grunts share a class name, and only
    /// the id separates the boss from the mooks. The list is measured off the cartridge and lives
    /// in <c>Data/roles.json</c>.
    /// </remarks>
    public IReadOnlySet<int> ImportantTrainerClasses { get; init; } = new HashSet<int>();

    /// <summary>Randomize the type or types of every species and form.</summary>
    public bool RandomizeTypes { get; init; } = true;

    /// <summary>
    /// Rearrange the six base stats without changing their total. Shuffling rather than rolling
    /// keeps the base stat total meaningful, which is what the encounter and trainer modules use
    /// to match a replacement to the species it replaces.
    /// </summary>
    public bool ShuffleBaseStats { get; init; } = true;

    /// <summary>Randomize abilities. A slot that was empty stays empty.</summary>
    public bool RandomizeAbilities { get; init; } = true;

    /// <summary>
    /// Redirect what each species evolves into. The trigger is left alone, so an evolution still
    /// happens at the same level or with the same stone.
    /// </summary>
    public bool RandomizeEvolutions { get; init; } = true;

    /// <summary>Replace the moves of every level-up learnset, keeping the levels.</summary>
    public bool RandomizeLearnsets { get; init; } = true;

    /// <summary>
    /// How much of a learnset is forced to be a move that really hurts, as a percentage.
    /// </summary>
    /// <remarks>
    /// Universal Pokémon Randomizer's own figure, and it is not decoration: without it a
    /// randomized Pokémon spends its whole level curve learning stat drops and screens. The move
    /// learnt last at level one is forced on top of this, so a freshly caught Pokémon can always
    /// attack.
    /// </remarks>
    public int LearnsetGoodDamagingPercent { get; init; } = 30;

    /// <summary>Bias the moves towards the Pokémon's own types. The old name of <see cref="LearnsetSameTypePercent"/>.</summary>
    /// <remarks>True is Universal Pokémon Randomizer's own figure, forty per cent, split between a dual type's two.</remarks>
    public bool LearnsetPreferSameType { get; init; }

    /// <summary>
    /// How often a move is drawn from the Pokémon's own types, as a percentage of its slots; the rest is drawn from
    /// everything. Split between the two of a dual type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What comes out is more than what is asked for, because the open draws land on its types too: measured on the
    /// whole game, 40 gives 46 % of the learnset in its own types and 20 gives 28 %. The cartridge itself is at 48,7 %
    /// and the reference's world at 28 %.
    /// </para>
    /// <para>
    /// Zero leaves it to chance, which is 9,6 %: every Pokémon learns a bit of all eighteen types and they all end up
    /// playing the same, which is what the player reported on 2026-09-22 (§166).
    /// </para>
    /// </remarks>
    public int LearnsetSameTypePercent { get; init; }

    /// <summary>The percentage in force, with the old boolean still meaning Universal Pokémon Randomizer's forty.</summary>
    public int EffectiveSameTypePercent() =>
        LearnsetSameTypePercent > 0 ? Math.Clamp(LearnsetSameTypePercent, 0, 100) : LearnsetPreferSameType ? 40 : 0;

    /// <summary>Base power a move needs before it counts as a real attack.</summary>
    /// <remarks>Fifty, which is the reference's floor. Reached with accuracy, or doubled without.</remarks>
    public int LearnsetDamagingFloor { get; init; } = 50;

    /// <summary>
    /// Above zero, each learnt move is replaced by one like the cartridge's in that slot: an attack within this share of
    /// its strength, a status move for a status move. Zero draws from the whole catalogue with the quota, as before.
    /// </summary>
    /// <remarks>See <see cref="Modules.LearnsetPlanner"/>: at level 5, 59% of the attacks were 80 or more (2026-09-21).</remarks>
    public double LearnsetPowerTolerance { get; init; }

    /// <summary>
    /// Moves nobody may have: taken out of every level-up learnset, egg move list, trainer moveset and static moveset,
    /// whether the rest is randomized or not (§162). The one-hit knockouts, at the player's request.
    /// </summary>
    public IReadOnlyList<int> BannedMoves { get; init; } = [];

    /// <summary>
    /// With the whole-catalogue draw, sorts the attacks of each learnset by strength, weakest first, into the slots that
    /// already held attacks: Universal Pokémon Randomizer's «reorder damaging moves», and what pk3DS does by default.
    /// </summary>
    public bool LearnsetReorderByPower { get; init; }

    /// <summary>
    /// What a special mart that does not sell TMs is stocked with, in every slot. Poké Ball (4)
    /// by default: a Nuzlocke needs balls far more than it needs X Items.
    /// </summary>
    public int NonMachineMartItem { get; init; } = 4;

    /// <summary>
    /// Items put on sale in the special marts that do not sell TMs, in shop order and spilling
    /// into the next shop when one fills up. Empty leaves those shops stocked with
    /// <see cref="NonMachineMartItem"/>.
    /// </summary>
    /// <remarks>
    /// It exists for the gen 8-9 expansion, and for a reason that is not cosmetic: the mod's
    /// evolution items — the Malicious Armor that turns Charcadet into Ceruledge, the scrolls
    /// Urshifu needs, the Gimmighoul Coin — are in the item table and <b>are not placed anywhere
    /// on the ground</b>, so without a shop selling them those evolutions cannot happen at all.
    /// </remarks>
    public IReadOnlyList<MartItem> SpecialMartItems { get; init; } = [];

    /// <summary>
    /// What each of <see cref="SpecialMartItems"/> costs. Zero leaves prices alone.
    /// </summary>
    /// <remarks>
    /// The price lives in the <b>item table</b> and not in the shop, so this makes them cost that
    /// everywhere, selling included. The field is a <c>ushort</c> holding the price divided by ten,
    /// which is why the ceiling is 655350 and why it has to be a multiple of ten: 50000 is stored
    /// as 5000, and a number that does not divide cleanly would be silently truncated.
    /// </remarks>
    public int SpecialMartItemPrice { get; init; }

    /// <summary>
    /// Special counters that sell a list of their own instead of taking their turn in
    /// <see cref="SpecialMartItems"/>. Empty leaves every counter to the spilling list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked for on 2026-09-19: the classic evolution items, each list in the town the player chose
    /// — the stones on Route 8, the trade items on Route 2, the rest in a Konikoni shop. A counter
    /// named here is skipped by the spilling list, so the gen 8-9 items move on to the next one
    /// instead of being overwritten.
    /// </para>
    /// <para>
    /// Stricter than <see cref="SpecialMartOrder"/> on purpose. An order is a preference and an
    /// index that does not exist is ignored; a shelf that cannot be placed means items nobody can
    /// buy, so a missing counter, a TM counter, a list longer than the counter or an item listed
    /// twice stops the shops before a byte is written.
    /// </para>
    /// </remarks>
    public IReadOnlyList<MartShelf> SpecialMartShelves { get; init; } = [];

    /// <summary>
    /// Species left exactly as the cartridge has them, wherever they appear. Cosmog is here by
    /// default: the story hands it over and later requires it to become Solgaleo or Lunala, and
    /// whether the game survives having it replaced has not been tested.
    /// </summary>
    public IReadOnlyList<int> ProtectedSpecies { get; init; } = [789];

    /// <summary>Species that may never be handed out. Legendaries and mythicals, typically.</summary>
    public IReadOnlyList<int> BannedSpecies { get; init; } = [];

    /// <summary>
    /// Species kept out of the <b>wild</b> only; trainers, statics and gifts may still use them.
    /// </summary>
    /// <remarks>
    /// Added on 2026-09-21 for the legendaries and mythicals of gen 8 and 9. <see cref="BannedSpecies"/>
    /// was written before the expansion and stops at 807, so the installed world had Meltan in 105
    /// grass slots, Kubfu in 60 and Calyrex in 38. The player asked for every legendary out of the
    /// wild and for the rest of the game to stay as it was, which is why this is a second list and not
    /// more ids in the first: that one narrows every module at once.
    /// </remarks>
    public IReadOnlyList<int> WildBannedSpecies { get; init; } = [];

    /// <summary>
    /// Regional forms a randomized Pokémon may come out in: Alola, Galar, Hisui and Paldea. Empty
    /// means every Pokémon comes out in its ordinary form, which is how it was until §138.
    /// </summary>
    public IReadOnlyList<Modules.RegionalFormEntry> RegionalForms { get; init; } = [];

    /// <summary>Abilities never dealt to a species, whatever <see cref="MaxAbility"/> says.</summary>
    /// <remarks>
    /// For the expansion mod's abilities that belong to one Pokémon's forms — Cambio Heroico,
    /// Comandar, Cara de Hielo, Mutapetito, Tragamisil and the Tera ones. They are code in the mod's
    /// executable written for that Pokémon, and on another it could try to change it into a form it
    /// does not have. The cartridge's own form abilities are left out of this on purpose: the
    /// original game handles them on any species, by doing nothing. §136.
    /// </remarks>
    public IReadOnlyList<int> BannedAbilities { get; init; } = [];

    /// <summary>
    /// A deliberate ceiling on which species may be handed out. <b>Zero means no ceiling</b> and the
    /// game's own tables decide.
    /// </summary>
    /// <remarks>
    /// Zero is the default because the alternative has a trap in it. A fixed 807 is right for the
    /// cartridge and quietly wrong on a mod that adds Pokémon: install the expansion, forget to
    /// raise this number, and the randomization succeeds while not one of the three hundred and
    /// fifty new species ever appears. Nothing fails, so nothing tells you. Defaulting to "whatever
    /// the game has" makes the safe case the one you get by not thinking about it, and leaves the
    /// ceiling for when somebody actually wants one — capping a vanilla run to the first
    /// generation, say.
    /// </remarks>
    public int MaxSpecies { get; init; }

    /// <summary>The ceiling actually in force, given what the loaded game turned out to hold.</summary>
    public int EffectiveMaxSpecies(int gameMaxSpecies) =>
        MaxSpecies > 0 ? Math.Min(MaxSpecies, gameMaxSpecies) : gameMaxSpecies;

    /// <summary>
    /// Highest ability id the randomizer may hand out. Zero means whatever the game declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On the gen 8-9 expansion this is set to 233, the cartridge's own abilities, and what it keeps out are the
    /// 79 the mod adds (234 to 316). The first reading of why was <b>wrong</b>: it took the ability field for one
    /// byte, full at 255, and the mod's issue #1 —abilities «displayed and inert» after randomizing— for proof
    /// that the new ones do nothing. The mod keeps a <b>ninth bit</b> per slot in the entry's last byte
    /// (<see cref="Modules.PersonalEntry7.AbilityHighBitsOffset"/>), and issue #1 is what a randomizer that writes
    /// only the byte produces, this one included until §132: a bit left behind turns ability 50 into 306, or into
    /// one that does not exist.
    /// </para>
    /// <para>
    /// So the cap came to rest on «nobody has seen them work», and on 2026-09-18 the player saw four of them work
    /// in battle (§134) and asked for all of them: it is back at zero. «What the game declares» now really is the
    /// game's — the length of its ability names — and not pk3DS's constant of 233, which had been capping the mod
    /// whatever this said (§136). The ones tied to a single Pokémon's forms stay out through
    /// <see cref="BannedAbilities"/>.
    /// </para>
    /// </remarks>
    public int MaxAbility { get; init; }

    /// <inheritdoc cref="EffectiveMaxSpecies"/>
    public int EffectiveMaxAbility(int gameMaxAbility) =>
        MaxAbility > 0 ? Math.Min(MaxAbility, gameMaxAbility) : gameMaxAbility;

    /// <summary>
    /// Highest move id a randomized learnset may hand out. Zero means whatever the game declares.
    /// </summary>
    /// <remarks>
    /// The companion to <see cref="MaxAbility"/>. Of the expansion's 192 new ids, 32 are empty —
    /// the Let's Go and Max moves, with zero PP, which <see cref="Modules.MoveTable.Teachable"/>
    /// already leaves out —, 128 reuse a battle routine the cartridge has and 32 ask for one only
    /// the mod's <c>code.bin</c> has. Setting this to the cartridge's count keeps learnsets to what
    /// is known to work; zero trusts the mod, which is where the player put it on 2026-09-18 after
    /// seeing three of those 32 work in battle (§134). As with abilities, «what the game declares»
    /// is the length of its own move table, not pk3DS's constant (§136).
    /// </remarks>
    public int MaxMove { get; init; }

    /// <inheritdoc cref="EffectiveMaxSpecies"/>
    public int EffectiveMaxMove(int gameMaxMove) =>
        MaxMove > 0 ? Math.Min(MaxMove, gameMaxMove) : gameMaxMove;
}
