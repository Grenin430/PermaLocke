namespace PermaLocke.Randomizer;

/// <param name="Id">The cartridge's item id, which is what gets written into the trainer entry.</param>
/// <param name="Name">
/// What that id is called, checked against the cartridge's item table before anything is written.
/// Same guard as <see cref="MartItem"/>: an id typed from memory that lands on another item hands
/// every trainer the wrong thing and never fails (§52).
/// </param>
public sealed record TrainerHeldItem(int Id, string Name);

/// <summary>
/// How much harder the trainers are than the cartridge made them, beyond the role's levels.
/// </summary>
/// <remarks>
/// <para>
/// Four levers, each measured against the cartridge before it was chosen (§122): the AI byte, the
/// IVs, the EV spread and how many trainer Pokémon hold an item. Moves and abilities are left to the
/// other modules on purpose.
/// </para>
/// <para>
/// Off by default, so a missing block in <c>Data/randomizer.json</c> leaves the trainers as the
/// cartridge has them rather than as some number compiled in here.
/// </para>
/// </remarks>
public sealed record TrainerDifficultyOptions
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Bits ORed into every trainer's AI byte. 7 is Basic, Strong and Expert.
    /// </summary>
    /// <remarks>
    /// OR and never assignment: the same byte carries Doubles, No Whiteout, Battle Royal and Use Item,
    /// which describe the battle rather than how clever the trainer is, and a trainer that already
    /// has more keeps all of it.
    /// </remarks>
    public int AiFlagsAdded { get; init; }

    /// <summary>Lowest percentage an IV is raised by. Each Pokémon draws its own between the two.</summary>
    public int IvRaiseMinPercent { get; init; }

    public int IvRaiseMaxPercent { get; init; }

    /// <summary>
    /// Deal each trainer Pokémon's EVs again for the species it is now, keeping how many it has.
    /// </summary>
    /// <remarks>
    /// The cartridge chose its spreads for the species it placed. After the trainer module swaps the
    /// species, an Attack and Speed spread can land on a special attacker and do nothing.
    /// </remarks>
    public bool RecalculateEvs { get; init; }

    /// <summary>Base Speed from which a Pokémon invests in Speed rather than in HP.</summary>
    public int EvFastSpeed { get; init; } = 80;

    /// <summary>Share of all trainer Pokémon that should hold an item, counting the ones that already do.</summary>
    public int HeldItemPercent { get; init; }

    /// <summary>Items any Pokémon may be given.</summary>
    public IReadOnlyList<TrainerHeldItem> HeldItemsAny { get; init; } = [];

    /// <summary>Added to the choice when the Pokémon's Attack is at least its Special Attack.</summary>
    public IReadOnlyList<TrainerHeldItem> HeldItemsPhysical { get; init; } = [];

    /// <summary>Added to the choice when its Special Attack is higher.</summary>
    public IReadOnlyList<TrainerHeldItem> HeldItemsSpecial { get; init; } = [];
}
