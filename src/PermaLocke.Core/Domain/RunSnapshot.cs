namespace PermaLocke.Core.Domain;

/// <summary>
/// What one player's application publishes about their run so the others can see it.
/// </summary>
/// <remarks>
/// <para>
/// A <b>summary, not the run</b>. The event chain is thousands of rows and holds everything that
/// ever happened; what a competition needs to see is the scoreboard. Publishing the whole chain
/// would be sending somebody your diary so they can read the last page.
/// </para>
/// <para>
/// <b>What this is NOT: proof.</b> It is a file written by another player's copy of PermaLocke,
/// living in a folder they can open. Anybody can edit it. There is no anti-cheat here and there is
/// not going to be one pretending to be here — the honest description is "this is what their
/// application said", and every screen that shows it says so.
/// </para>
/// <para>
/// <see cref="ChainHead"/> and <see cref="EventCount"/> are a <b>fingerprint</b>, not a signature.
/// They cannot tell you a number is true. What they can do is make two snapshots of the same run
/// comparable: a count that goes backwards means the run was restored from a copy, and a chain head
/// that changes without the count moving means the history was rewritten. That is information a
/// group of friends can act on themselves, which is the most an honest system can offer without a
/// server nobody is going to run.
/// </para>
/// </remarks>
/// <param name="Schema">Format version, so an older application can refuse a file it cannot read.</param>
/// <param name="ChainHead">Hash of the last event, as the fingerprint above.</param>
public sealed record RunSnapshot
{
    /// <summary>Current format. Bump only when a reader would get it wrong otherwise.</summary>
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    public required Guid RunId { get; init; }

    public required string PlayerName { get; init; }

    public required string RunName { get; init; }

    /// <summary>The role as the catalogue names it, not its id: this is read by people.</summary>
    public string RoleName { get; init; } = string.Empty;

    public string SeedLabel { get; init; } = string.Empty;

    public int Points { get; init; }

    public int Registered { get; init; }

    public int Alive { get; init; }

    public int Dead { get; init; }

    public int Traded { get; init; }

    public int StagesCleared { get; init; }

    public int? LevelCap { get; init; }

    public int AchievementsUnlocked { get; init; }

    public int AchievementsTotal { get; init; }

    public int EventCount { get; init; }

    public string ChainHead { get; init; } = string.Empty;

    public DateTimeOffset RunCreatedAt { get; init; }

    /// <summary>When this snapshot was published, by the clock of whoever published it.</summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// File name this snapshot belongs in, derived from the run so one player publishing twice
    /// replaces their own file instead of piling up.
    /// </summary>
    /// <remarks>
    /// Keyed on the run id and not the player name: two people called "Ash" would otherwise
    /// overwrite each other, and the same person starting a second run would lose the first.
    /// </remarks>
    public string FileName => $"permalocke-{RunId:N}.json";
}
