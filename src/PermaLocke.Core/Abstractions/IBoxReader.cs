namespace PermaLocke.Core.Abstractions;

/// <summary>
/// One Pokémon as it sits in a box of the player's PC, with everything already resolved to text.
/// </summary>
/// <remarks>
/// Names, not ids: the screen that shows this must not need a Pokédex, an item table or PKHeX.
/// Whoever reads the save has all three at hand and resolves them once.
/// </remarks>
/// <param name="Box">Zero-based box.</param>
/// <param name="Slot">Zero-based slot inside the box.</param>
/// <param name="Stats">The six stats as the game would show them, in HP/Atk/Def/SpA/SpD/Spe order.</param>
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
    uint Pid)
{
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

/// <param name="Number">One-based box number, as the game numbers them.</param>
/// <param name="Name">The name the player gave the box in game.</param>
public sealed record BoxContents(int Number, string Name, IReadOnlyList<BoxedPokemon> Pokemon)
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

    public int Total => Boxes.Sum(box => box.Count);
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
