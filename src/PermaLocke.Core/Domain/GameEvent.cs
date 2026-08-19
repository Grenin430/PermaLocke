namespace PermaLocke.Core.Domain;

/// <summary>
/// An immutable, append-only record of something that happened in a run.
/// The points balance is a projection over these; no code may change state without one.
/// </summary>
/// <remarks>
/// <see cref="PreviousHash"/> and <see cref="Hash"/> chain the log so that editing or
/// deleting an entry after the fact is detectable. See docs/ARCHITECTURE.md §9 for what
/// this does and does not protect against.
/// </remarks>
public sealed record GameEvent
{
    public required Guid Id { get; init; }

    public required Guid RunId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required GameEventType Type { get; init; }

    public required EventSource Source { get; init; }

    /// <summary>Player name, admin name, or the subsystem that produced the event.</summary>
    public required string Actor { get; init; }

    /// <summary>Human readable summary, shown in the history views.</summary>
    public required string Description { get; init; }

    /// <summary>Signed points delta. Zero for events that do not move the balance.</summary>
    public int PointsDelta { get; init; }

    public Guid? PokemonId { get; init; }

    public string? LocationId { get; init; }

    /// <summary>Seed used by the random operation behind this event, when there was one.</summary>
    public ulong? Seed { get; init; }

    /// <summary>Reason supplied by an administrator. Mandatory for admin adjustments.</summary>
    public string? Reason { get; init; }

    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Hash of the preceding event in the chain, or the empty string for the first one.</summary>
    public string PreviousHash { get; init; } = string.Empty;

    /// <summary>Hash of this event's content plus <see cref="PreviousHash"/>.</summary>
    public string Hash { get; init; } = string.Empty;
}
