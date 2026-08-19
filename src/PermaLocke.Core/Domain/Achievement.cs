namespace PermaLocke.Core.Domain;

/// <param name="Trigger">Event this achievement counts.</param>
/// <param name="Target">How many of them unlock it. One means "the first time it happens".</param>
/// <param name="Points">Awarded when the player claims it, not when it unlocks.</param>
public sealed record Achievement(
    string Id,
    string Name,
    string Description,
    GameEventType Trigger,
    int Target,
    int Points);

/// <summary>
/// State of one achievement, derived from the event log rather than stored anywhere.
/// </summary>
/// <remarks>
/// Unlocking and claiming are separate on purpose: the run shows progress as <c>x/y</c> and the
/// player collects with an explicit button. Keeping both as events means the history explains
/// where every point came from, which is the whole point of the audit log.
/// </remarks>
/// <param name="Count">Qualifying events so far. Can exceed the target.</param>
public sealed record AchievementProgress(Achievement Achievement, int Count, bool Claimed)
{
    public bool Unlocked => Count >= Achievement.Target;

    /// <summary>Unlocked and not yet collected: the only state where the button does anything.</summary>
    public bool CanClaim => Unlocked && !Claimed;

    /// <summary>Progress capped at the target, which is what a "3/5" label wants.</summary>
    public int Shown => Math.Min(Count, Achievement.Target);
}

/// <summary>
/// The achievements a run plays with.
/// </summary>
/// <remarks>
/// A port so the domain never learns where they come from. Today it is a JSON file an admin can
/// edit; no achievement, target or reward is written in code.
/// </remarks>
public interface IAchievementCatalog
{
    IReadOnlyList<Achievement> All { get; }
}
