namespace PermaLocke.Core.Domain;

/// <param name="Trigger">
/// Event this achievement counts, or <c>null</c> when PermaLocke cannot detect it yet.
/// </param>
/// <param name="TriggerName">
/// What the catalogue asked for, kept verbatim. It is what lets the screen say <em>which</em>
/// thing is not detectable instead of quietly dropping the achievement.
/// </param>
/// <param name="Target">How many of them unlock it. One means "the first time it happens".</param>
/// <param name="Points">Awarded when the player claims it, not when it unlocks.</param>
public sealed record Achievement(
    string Id,
    string Name,
    string Description,
    GameEventType? Trigger,
    string TriggerName,
    int Target,
    int Points)
{
    /// <summary>
    /// False when nothing in the run can ever count towards this one.
    /// </summary>
    /// <remarks>
    /// A competition list will name things PermaLocke does not watch yet — beating a trainer,
    /// clearing a trial. Those stay on the list and are shown as pending rather than deleted or,
    /// worse, silently awarded.
    /// </remarks>
    public bool IsDetectable => Trigger is not null;
}

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
    public bool Unlocked => Achievement.IsDetectable && Count >= Achievement.Target;

    /// <summary>Unlocked and not yet collected: the only state where the button does anything.</summary>
    public bool CanClaim => Unlocked && !Claimed;

    /// <summary>Progress capped at the target, which is what a "3/5" label wants.</summary>
    public int Shown => Math.Min(Count, Achievement.Target);

    /// <summary>"3 / 5", or why it cannot be counted at all.</summary>
    public string Label => Achievement.IsDetectable
        ? $"{Shown} / {Achievement.Target}"
        : "sin detectar";
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
