namespace PermaLocke.Core.Domain;

/// <summary>
/// Who plays on this machine, as the competition knows them.
/// </summary>
/// <remarks>
/// <para>
/// Every player has their own PC and their own copy of PermaLocke, so the installation <em>is</em>
/// the player; what was missing was an identity that survives. Before this, «who» was the free text
/// typed into each run: two friends called Ash were one row, and starting again left the old run
/// sitting in the standings next to the new one with nothing tying them together (§123).
/// </para>
/// <para>
/// The <see cref="Id"/> is what everything keys on. The <see cref="Name"/> is what people read and
/// can change without breaking anything.
/// </para>
/// </remarks>
public sealed record PlayerProfile
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Longest name accepted, so it fits a podium block and a folder name.</summary>
    public const int MaxNameLength = 24;

    /// <summary>
    /// The first eight characters of the id: enough to tell five friends apart in a folder name,
    /// short enough to read.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ShortId => Id.ToString("N")[..8];
}

/// <summary>
/// Everything one run has recorded, published next to its snapshot so that anybody can check the
/// snapshot against it.
/// </summary>
/// <remarks>
/// <para>
/// §79 said the whole chain would not be published — «sending somebody your diary so they can read
/// the last page». That changed with the per-player folders: the diary is what lets a snapshot be
/// <b>checked</b> instead of taken at its word. Its points are the sum of the events' deltas, its
/// event count and last hash are the chain's, and the chain verifies or it does not.
/// </para>
/// <para>
/// What it still is not: proof. A hash chain is not a signature (§9). Somebody willing to rebuild a
/// whole history with tools can publish one that verifies. It catches editing a number, rewinding
/// a run and deleting a death; it does not catch forgery, and nothing that reads it says it does.
/// </para>
/// </remarks>
public sealed record RunHistory
{
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    public required Guid RunId { get; init; }

    public Guid? PlayerId { get; init; }

    public DateTimeOffset ExportedAt { get; init; }

    public IReadOnlyList<GameEvent> Events { get; init; } = [];
}
