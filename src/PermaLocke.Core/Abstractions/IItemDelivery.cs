namespace PermaLocke.Core.Abstractions;

/// <param name="Carried">How many the bag holds after the write, re-read from the game.</param>
/// <param name="GameReachable">False when the emulator never answered, which is a different
/// problem from a write that failed: one is "abre el juego", the other is "algo va mal".</param>
public sealed record ItemDeliveryResult(
    bool Delivered, int Carried, string Problem, bool GameReachable = true)
{
    public static ItemDeliveryResult Failed(string problem, bool reachable = true) =>
        new(false, 0, problem, reachable);

    public static ItemDeliveryResult Unreachable(string problem) => new(false, 0, problem, false);
}

/// <summary>
/// Puts an item into the player's bag.
/// </summary>
/// <remarks>
/// <para>
/// A port, so the shop never learns whether that means the emulator's memory, the save file or
/// something else. Today it is memory, through the bag block located by its own structure, and it
/// therefore needs the game open with the run loaded.
/// </para>
/// <para>
/// The contract is the strict one: <see cref="GiveAsync"/> reports success only after reading the
/// bag back and finding the item there. A shop that charged for something it did not deliver is
/// worse than a shop that refuses.
/// </para>
/// </remarks>
public interface IItemDelivery
{
    /// <summary>Adds <paramref name="amount"/> of an item, on top of whatever is already carried.</summary>
    Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default);

    /// <summary>How many of an item the bag holds, or -1 when the bag cannot be read.</summary>
    Task<int> CarriedAsync(int itemId, CancellationToken ct = default);

    /// <summary>
    /// How many of each of several items the bag holds, in <b>one</b> read.
    /// </summary>
    /// <remarks>
    /// Not a convenience: locating the bag can mean sweeping ninety-six megabytes of the game's
    /// memory over a UDP channel, so asking eighteen times froze the shop for as long as it took
    /// to fail eighteen times. Empty when the bag cannot be read at all.
    /// </remarks>
    Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
        IReadOnlyList<int> itemIds, CancellationToken ct = default);
}
