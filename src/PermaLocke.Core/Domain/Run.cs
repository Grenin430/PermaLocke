namespace PermaLocke.Core.Domain;

/// <summary>An island of the Alola tour and how far the player has got in it.</summary>
public sealed record Island(string Id, string Name, IslandState State);

/// <summary>
/// Everything that identifies a run. Reproducibility depends on this being written once
/// at creation and never silently changed: seed + options + ROM hash define the randomization.
/// </summary>
public sealed record Run
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required GameVersion Game { get; init; }

    /// <summary>Display seed, e.g. PERMA-839421.</summary>
    public required string SeedLabel { get; init; }

    /// <summary>Numeric seed actually fed to the randomizers.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Role / difficulty identifier. Roles are configuration, not an enum.</summary>
    public required string RoleId { get; init; }

    public required string PlayerName { get; init; }

    /// <summary>
    /// The <see cref="PlayerProfile"/> this run belongs to. Null for runs created before profiles
    /// existed; those are linked once, with an event, the first time the app sees them (§123).
    /// </summary>
    public Guid? PlayerId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>SHA-256 of the vanilla ROM used, so a swapped ROM is detectable.</summary>
    public string? RomHash { get; init; }

    /// <summary>3DS title id of the ROM, e.g. 00040000001B5100 for Ultra Moon (EUR).</summary>
    public string? TitleId { get; init; }

    public string? RandomizerVersion { get; init; }

    public string? AppVersion { get; init; }

    public IReadOnlyList<Island> Islands { get; init; } = [];

    /// <summary>
    /// Milestones already cleared: trials, the league, the rematch. Drives the level cap, so it
    /// only ever moves through an auditable event.
    /// </summary>
    public int ClearedStages { get; init; }
}
