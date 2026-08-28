namespace PermaLocke.Core.Abstractions;

/// <param name="Applied">True when the saved game really came back with it turned on.</param>
public sealed record UnlockResult(bool Applied, string Message);

/// <summary>
/// Switches on something the saved game keeps as a flag of its own, rather than as an item.
/// </summary>
/// <remarks>
/// <para>
/// A second kind of prize, and it exists because of a measurement: Ultra Moon gates Mega Evolution
/// on a field in the trainer block and <b>not</b> on carrying the Key Stone. Writing the item into
/// the bag changed nothing, and the flag next to it -- the one for Z-moves, already on -- is what
/// said the pair had been found.
/// </para>
/// <para>
/// A different door from <see cref="IItemDelivery"/>, with the opposite requirement: the bag is
/// written in the running game's memory, and this is written in the save file, so it needs the
/// game <b>closed</b>. That is why a prize may not hand over items and unlocks at once — there is
/// no state of the emulator in which both could be done.
/// </para>
/// </remarks>
public interface IGameUnlocks
{
    /// <summary>Every key this implementation understands. A prize naming another one is refused.</summary>
    IReadOnlySet<string> Known { get; }

    /// <summary>True when the save could be written right now.</summary>
    bool CanApplyNow(out string reason);

    Task<UnlockResult> ApplyAsync(IReadOnlyList<string> keys, CancellationToken ct = default);
}
