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

/// <param name="Id">The cartridge's own item id, which is what gets written into the shop.</param>
/// <param name="Name">
/// What that id is called, checked against the cartridge's item table before anything is written.
/// An id that lands on the wrong item stocks the wrong thing and never fails, so the name is not
/// documentation here: it is the guard (§52).
/// </param>
public sealed record MartItem(int Id, string Name);

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
    /// Cartridge level a trainer has to reach before one of its Pokemon may be a mega.
    /// </summary>
    /// <remarks>
    /// The ROM is randomized once, before the run starts, so it cannot know the player has cleared
    /// six trials. What it can do is look at how strong the battle is, which is the same thing seen
    /// from the other side. 33 is the seventh trial: its cap is 40 and §48 measured that a cap is
    /// its boss raised a fifth. Measured against the cartridge, that lets 62 of the 96 important
    /// battles through.
    /// </remarks>
    public int MegaTrainerMinimumLevel { get; init; } = 33;

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
    public int EnemyLevelPercent { get; init; }

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
    /// Species left exactly as the cartridge has them, wherever they appear. Cosmog is here by
    /// default: the story hands it over and later requires it to become Solgaleo or Lunala, and
    /// whether the game survives having it replaced has not been tested.
    /// </summary>
    public IReadOnlyList<int> ProtectedSpecies { get; init; } = [789];

    /// <summary>Species that may never be handed out. Legendaries and mythicals, typically.</summary>
    public IReadOnlyList<int> BannedSpecies { get; init; } = [];

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
    /// This exists because of a measured failure, not a hypothetical one. The ability field of the
    /// personal table is <b>one byte</b>, so a game can address at most 255 abilities; the cartridge
    /// uses 233 and the gen 8-9 expansion fills the rest, hitting the ceiling exactly. The
    /// twenty-two it added do not work: the mod's own issue tracker reports that a Pokémon given
    /// one shows the name in the summary and nothing happens in battle.
    /// <para>
    /// So on that mod this is set to 233, and the point is that the failure it avoids is the kind
    /// PermaLocke exists to refuse — the ability is not missing, it is <em>displayed and inert</em>.
    /// See <c>docs/MOD-EXPANSION.md</c> §5.
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
    /// The companion to <see cref="MaxAbility"/>, and a softer case. Of the expansion's 192 new
    /// moves, 160 reuse a battle routine the engine already has and 32 ask for one the cartridge
    /// never uses — those 32 rely on the mod's patched <c>code.bin</c>, which nobody here has
    /// disassembled. Setting this to the cartridge's move count keeps learnsets to what is known to
    /// work; leaving it at zero trusts the mod. Which of the two is right is the player's call, so
    /// it is a number in the configuration and not a decision baked into the code.
    /// </remarks>
    public int MaxMove { get; init; }

    /// <inheritdoc cref="EffectiveMaxSpecies"/>
    public int EffectiveMaxMove(int gameMaxMove) =>
        MaxMove > 0 ? Math.Min(MaxMove, gameMaxMove) : gameMaxMove;
}
