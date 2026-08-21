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
/// <param name="Record">
/// A counter the cartridge keeps itself — Z-moves used, battles fled — read straight from the
/// save. Preferred over anything PermaLocke could count on its own, because the game has been
/// counting it all along and a second number could only disagree.
/// </param>
/// <param name="Item">
/// An item whose presence in the bag <em>is</em> the milestone: done when the player holds it.
/// <para>
/// Some things the game marks leave no counter at all. Clearing a trial is one: no record moves,
/// and which of the 4960 event flags means it cannot be told apart from the dozens the same
/// session lights up. What it does leave is the reward, and the reward has a name the cartridge
/// prints — the first trial hands over the Normalium Z, item 807, and that was watched entering
/// an empty pouch. Only for what the game never takes back; a consumable would flicker.
/// </para>
/// </param>
public sealed record Achievement(
    string Id,
    string Name,
    string Description,
    GameEventType? Trigger,
    string TriggerName,
    int Target,
    int Points,
    int? Record = null,
    int? Item = null)
{
    /// <summary>PermaLocke counts this one on its own, from the events it already records.</summary>
    public bool IsAutomatic => Trigger is not null || Record is not null || Item is not null;

    /// <summary>The number comes from the player's own save, not from anything PermaLocke counted.</summary>
    public bool IsFromGame => Record is not null || Item is not null;

    /// <summary>
    /// The player moves this counter by hand.
    /// </summary>
    /// <remarks>
    /// A competition list names things PermaLocke cannot see: trials cleared, stickers found,
    /// trainers beaten. Rather than hiding those or, far worse, pretending to detect them, the
    /// player marks them, and every mark is its own event — signed by the player, not by the
    /// automatic detection, so the history always says which of the two it was.
    /// </remarks>
    public bool IsManual => Trigger is null && Record is null && Item is null;
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
    public bool Unlocked => Count >= Achievement.Target;

    /// <summary>Unlocked and not yet collected: the only state where the button does anything.</summary>
    public bool CanClaim => Unlocked && !Claimed;

    /// <summary>The counter can still be moved by hand: manual, and not there yet.</summary>
    public bool CanMark => Achievement.IsManual && !Unlocked;

    /// <summary>Progress capped at the target, which is what a "3/5" label wants.</summary>
    public int Shown => Math.Min(Count, Achievement.Target);

    /// <summary>"3/5", as the card shows it.</summary>
    public string Label => $"{Shown}/{Achievement.Target}";
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
