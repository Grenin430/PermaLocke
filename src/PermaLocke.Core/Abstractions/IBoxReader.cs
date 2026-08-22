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
/// <param name="Ivs">Same order as <paramref name="Stats"/>.</param>
/// <param name="Evs">Same order as <paramref name="Stats"/>.</param>
/// <param name="IsPlayers">
/// False when the original trainer is somebody else, which is what a traded Pokémon looks like.
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
    bool StatsAreComputed = false)
{
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
    /// The line under the name. It only repeats the species when the name is a nickname, so an
    /// unnamed Pokémon does not read as "Electivire" twice.
    /// </summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Nickname)
        ? $"Nv. {Level}"
        : $"{SpeciesName} · Nv. {Level}";

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
