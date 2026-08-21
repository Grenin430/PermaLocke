namespace PermaLocke.Core.Abstractions;

/// <param name="Problem">Why nothing could be read, in words the player can act on. Null when fine.</param>
/// <param name="Notice">Something worth saying that is not a failure, such as a stale reading.</param>
/// <param name="Values">Record number to its count, as the cartridge keeps them.</param>
/// <param name="Items">
/// Every item id the bag holds, quantity aside. Some milestones leave no counter behind but do
/// leave an object: clearing a trial hands over a Z-Crystal, and the cartridge names that object
/// itself. Held with no quantity because a key item can sit in the bag with a count of zero.
/// </param>
public sealed record GameRecordSnapshot(
    bool Available,
    string? Problem,
    string? Notice,
    IReadOnlyDictionary<int, int> Values,
    DateTimeOffset ReadAt,
    IReadOnlySet<int>? Items = null,
    IReadOnlyDictionary<int, int>? Works = null)
{
    private static readonly HashSet<int> None = [];

    public static GameRecordSnapshot Unavailable(string problem, DateTimeOffset at) =>
        new(false, problem, null, new Dictionary<int, int>(), at);

    public int Get(int record) => Values.GetValueOrDefault(record);

    /// <summary>True when the bag holds this item. Only meaningful for what the game never takes back.</summary>
    public bool Has(int item) => (Items ?? None).Contains(item);

    /// <summary>
    /// One of the save's own event counters, or zero when it was never set.
    /// </summary>
    /// <remarks>
    /// A thousand of them, unlabelled, and the game keeps its own tallies there for things no
    /// record counts — Totem Stickers among them. Which counter is which is measured, never
    /// guessed: see <c>ARCHITECTURE.md</c> §39.
    /// </remarks>
    public int Work(int counter) => Works?.GetValueOrDefault(counter) ?? 0;
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
