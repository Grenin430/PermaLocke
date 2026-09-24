namespace PermaLocke.Core.Abstractions;

/// <summary>
/// One Pokémon as it sits in a box of the player's PC, with everything already resolved to text.
/// </summary>
/// <remarks>
/// Names, not ids: the screen that shows this must not need a Pokédex, an item table or PKHeX.
/// Whoever reads the save has all three at hand and resolves them once.
/// </remarks>
/// <param name="Box">Zero-based box, or <see cref="BoxedPokemon.PartyBox"/> when it is in the party.</param>
/// <param name="Slot">Zero-based slot inside the box.</param>
/// <param name="Stats">The six stats in HP/Atk/Def/SpA/SpD/Spe order.</param>
/// <param name="StatsAreComputed">
/// True when <paramref name="Stats"/> was worked out here rather than read from the save. A box
/// does not store battle stats, so they have to be; the party does, and those are the real ones.
/// It matters because the calculation uses PKHeX vanilla base stats, and a randomized ROM does not
/// have vanilla base stats - so a computed number is an estimate, and the screen says so.
/// </param>
/// <param name="Ball">The ball it was caught in, as the game numbers them; balls 1 to 16 share that number with their item.</param>
/// <param name="Ivs">Same order as <paramref name="Stats"/>.</param>
/// <param name="Evs">Same order as <paramref name="Stats"/>.</param>
/// <param name="IsPlayers">
/// False when the original trainer is somebody else, which is what a traded Pokémon looks like.
/// </param>
/// <param name="Nature">
/// The nature as the game numbers it, 0 to 24, or -1 when nobody read it. Next to
/// <paramref name="NatureName"/> because a name says nothing about which stat it moves.
/// </param>
/// <param name="IsIntact">
/// False when the entry in the save does not match its own checksum: what the game draws as a Huevo
/// Malo (§97). Everything else in the record is then whatever the broken bytes happen to say, and
/// anything that writes must leave it alone -- writing it back through PKHeX would give garbage a valid
/// checksum, which is not a repair, it is a Pokémon with a species out of range that the game may hang on.
/// </param>
/// <param name="StatLevel">
/// For a party member, the level stored next to its battle stats, which is the one they were worked out at
/// and the one the EV writer recomputes them with. Zero when there is none, as in a box. It can differ from
/// <paramref name="Level"/>, which comes from the experience: measured on the real partida, a Ferrocuello
/// whose experience says 64 carries stats and level for 59.
/// </param>
/// <param name="FormName">
/// The form as Pokémon Showdown writes it after the species — «Alola», «Galar», «Paldea-Combat» —, in the
/// reader's language. Empty for the ordinary form. For POKE PASTE, whose site draws «Vulpix-Alola» as an
/// Alolan one and «Vulpix» as the ordinary one (§140).
/// </param>
/// <param name="MoveIds">
/// Its four move slots as ids, zero for an empty one, in the same order as <paramref name="Moves"/>. Null when
/// whoever built the record did not read them; the move reminder needs ids, not names (§142).
/// </param>
/// <param name="RelearnMoveIds">
/// The four moves the game keeps for it to relearn — egg moves, a gift's special moves —, zero for none.
/// </param>
public sealed record BoxedPokemon(
    int Box,
    int Slot,
    int Species,
    int Form,
    string SpeciesName,
    string Nickname,
    int Level,
    bool IsShiny,
    bool IsEgg,
    string GenderMark,
    string NatureName,
    string AbilityName,
    string HeldItemName,
    string BallName,
    string TrainerName,
    string MetLocationName,
    int MetLevel,
    IReadOnlyList<string> Moves,
    IReadOnlyList<int> Stats,
    IReadOnlyList<int> Ivs,
    IReadOnlyList<int> Evs,
    int Friendship,
    uint Pid,
    bool StatsAreComputed = false,
    int Ball = 0,
    int Nature = -1,
    bool IsIntact = true,
    int StatLevel = 0,
    string FormName = "",
    IReadOnlyList<int>? MoveIds = null,
    IReadOnlyList<int>? RelearnMoveIds = null)
{
    /// <summary>The level a stat of this Pokémon is worked out at: the stored one in the party, the experience's otherwise.</summary>
    public int LevelForStats => StatLevel > 0 ? StatLevel : Level;

    /// <summary>
    /// The value <see cref="Box"/> takes for a Pokémon travelling with the player instead of
    /// sleeping in the PC.
    /// </summary>
    /// <remarks>
    /// A sentinel and not a thirty-third box, because the party is a different store in the save
    /// with a different size, and anything that writes has to pick the right one. Negative so that
    /// code which forgets to check cannot silently land on box zero.
    /// </remarks>
    public const int PartyBox = -1;

    public bool IsInParty => Box == PartyBox;

    /// <summary>What the player calls it: the nickname when there is one, the species otherwise.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? SpeciesName : Nickname;

    /// <summary>
    /// The line under the name: the species, only when the name is a nickname, so an unnamed Pokémon does
    /// not read as "Electivire" twice. The level has its own plate on screen.
    /// </summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Nickname) ? string.Empty : SpeciesName;

    /// <summary>Sum of the six IVs, out of 186.</summary>
    public int IvTotal => Ivs.Sum();
}

/// <param name="Number">One-based box number as the game numbers them, or 0 for the party.</param>
/// <param name="Name">The name the player gave the box in game.</param>
/// <param name="Slots">
/// How many holes this one has. Per box and not per PC because the party holds six and a box
/// holds thirty, and the screen draws the empty ones.
/// </param>
public sealed record BoxContents(
    int Number, string Name, IReadOnlyList<BoxedPokemon> Pokemon, int Slots = 30, bool IsParty = false)
{
    public int Count => Pokemon.Count;
}

/// <param name="Problem">Why nothing could be read, in words the player can act on. Null when fine.</param>
/// <param name="Notice">Something worth saying that is not a failure, such as a stale reading.</param>
/// <param name="SlotsPerBox">Slots a box holds, so the screen can draw the empty ones too.</param>
public sealed record BoxSnapshot(
    bool Available,
    string? Problem,
    string? Notice,
    IReadOnlyList<BoxContents> Boxes,
    int SlotsPerBox,
    string TrainerName,
    DateTimeOffset ReadAt)
{
    public static BoxSnapshot Unavailable(string problem, DateTimeOffset at) =>
        new(false, problem, null, [], 30, string.Empty, at);

    /// <summary>Everything the player has, party included.</summary>
    public int Total => Boxes.Sum(box => box.Count);

    /// <summary>Just the PC, which is the number the player recognises as "en el PC".</summary>
    public int Stored => Boxes.Where(box => !box.IsParty).Sum(box => box.Count);

    public BoxContents? Party => Boxes.FirstOrDefault(box => box.IsParty);
}

/// <summary>
/// Reads the player's PC boxes.
/// </summary>
/// <remarks>
/// A port, so the screen never learns whether the boxes come from the save file, from memory or
/// from nowhere at all. Today it is the save file, which means what it shows is <b>the last
/// thing the player saved</b>; the snapshot says so when the game is open.
/// </remarks>
public interface IBoxReader
{
    Task<BoxSnapshot> ReadAsync(CancellationToken ct = default);
}
