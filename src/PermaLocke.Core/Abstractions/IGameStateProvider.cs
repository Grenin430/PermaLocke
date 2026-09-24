namespace PermaLocke.Core.Abstractions;

/// <summary>What a game link is actually able to tell us. The UI must consult this and
/// disable what is unavailable with a clear message, rather than showing empty panels.</summary>
[Flags]
public enum GameLinkCapabilities
{
    None = 0,
    Party = 1,
    Boxes = 2,
    Bag = 4,
    Badges = 8,
    WildEncounter = 16,
    BattleState = 32,
    LiveUpdates = 64,
    Write = 128
}

/// <param name="Slot">Zero based position in the party.</param>
public sealed record LivePartyMember(
    int Slot,
    int Species,
    string SpeciesName,
    string Nickname,
    int Level,
    int CurrentHp,
    int MaxHp,
    bool IsShiny,
    uint Pid,
    int MetLocationId,
    string MetLocationName,
    string TrainerName,
    int Form = 0,
    IReadOnlyList<int>? Moves = null)
{
    public bool IsFainted => CurrentHp == 0;
}

/// <param name="Problem">Why the read failed, in words the player can act on. Null when fine.</param>
/// <param name="Notice">Something worth telling the player that is not a failure.</param>
public sealed record GameSnapshot(
    bool Connected,
    string? Problem,
    IReadOnlyList<LivePartyMember> Party,
    DateTimeOffset ReadAt,
    string? Notice = null)
{
    public static GameSnapshot Disconnected(string problem, DateTimeOffset at) =>
        new(false, problem, [], at);
}

/// <summary>
/// Reads the state of the running game. Implementations range from nothing at all to a full
/// live link; callers branch on <see cref="Capabilities"/>, never on the concrete type.
/// </summary>
public interface IGameStateProvider
{
    GameLinkCapabilities Capabilities { get; }

    Task<GameSnapshot> ReadAsync(CancellationToken ct = default);
}
