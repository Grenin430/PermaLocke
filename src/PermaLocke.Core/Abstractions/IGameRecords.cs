namespace PermaLocke.Core.Abstractions;

/// <param name="Problem">Why nothing could be read, in words the player can act on. Null when fine.</param>
/// <param name="Notice">Something worth saying that is not a failure, such as a stale reading.</param>
/// <param name="Values">Record number to its count, as the cartridge keeps them.</param>
public sealed record GameRecordSnapshot(
    bool Available,
    string? Problem,
    string? Notice,
    IReadOnlyDictionary<int, int> Values,
    DateTimeOffset ReadAt)
{
    public static GameRecordSnapshot Unavailable(string problem, DateTimeOffset at) =>
        new(false, problem, null, new Dictionary<int, int>(), at);

    public int Get(int record) => Values.GetValueOrDefault(record);
}

/// <summary>
/// The game's own counters: battles, captures, Z-moves used, times fled, shinies met.
/// </summary>
/// <remarks>
/// <para>
/// The cartridge has kept these all along — they are what the trainer card shows — so counting
/// them again from outside would be inventing a second, worse truth. PermaLocke reads the game's.
/// </para>
/// <para>
/// A port, so nothing upstream learns whether they come from the save file or from memory. Today
/// it is the save, which means the numbers are <b>as of the last time the player saved</b>.
/// </para>
/// </remarks>
public interface IGameRecords
{
    Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default);
}
