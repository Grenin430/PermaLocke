namespace PermaLocke.Core.Domain;

/// <param name="Id">The cartridge's own item id, which is what gets written into the bag.</param>
/// <param name="Name">
/// What the item is called, checked against the cartridge table before anything is written. An id
/// typed from memory that lands on the wrong item hands over the wrong thing and never fails, so
/// the name is not decoration here: it is the guard (§52).
/// </param>
public sealed record RewardItem(int Id, string Name, int Amount);

/// <summary>
/// Something the competition hands over once, for reaching a milestone.
/// </summary>
/// <param name="Achievements">
/// Every achievement that has to be unlocked. All of them, not any of them: a reward for clearing
/// the twelve trials is not a reward for clearing one.
/// </param>
/// <param name="Held">
/// Items the player has to be carrying.
/// </param>
/// <param name="Automatic">
/// Hand it over as soon as it is earned, instead of waiting for the player to press RECOGER.
/// </param>
/// <remarks>
/// For prizes that are meant to arrive <em>with</em> something the game gives you, where a button
/// somewhere else is not a reward, it is homework. The guards do not change: it is still earned
/// against the same conditions, still written into the bag and read back, and still recorded once
/// with <c>RewardClaimed</c>, which is what keeps "once" true whoever pressed it.
/// </remarks>
/// <remarks>
/// The second kind of condition, and it exists for the things the game marks by <em>giving</em>
/// rather than by counting -- the same trick §40 used to anchor the trials on their Z-Crystal.
/// "The first time somebody hands you Poké Balls" leaves no record anywhere, but it leaves Poké
/// Balls in the bag, and that can be seen. Only for what the game does not take back.
/// </remarks>
public sealed record Reward(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Achievements,
    IReadOnlyList<RewardItem> Items,
    IReadOnlyList<int>? Held = null,
    bool Automatic = false)
{
    public IReadOnlyList<int> HeldItems { get; } = Held ?? [];

    /// <summary>How many conditions there are in total, of both kinds.</summary>
    public int Conditions => Achievements.Count + HeldItems.Count;
}

/// <summary>The rewards, read from configuration rather than compiled in.</summary>
/// <remarks>
/// Same reason as the shop and the penalties: what the competition gives, for what, and how much
/// is something an admin edits between runs. Adding a reward is a line in <c>Data/rewards.json</c>.
/// </remarks>
public interface IRewardCatalog
{
    IReadOnlyList<Reward> All { get; }
}

/// <summary>How a claim ended.</summary>
public enum RewardOutcome
{
    /// <summary>Written into the bag and seen there afterwards.</summary>
    Delivered,

    /// <summary>Some of the items arrived and some did not. It still counts as claimed.</summary>
    PartlyDelivered,

    /// <summary>Not every achievement is unlocked yet. Nothing was written.</summary>
    NotEarned,

    /// <summary>Already taken. It is a one-off, so there is nothing left to give.</summary>
    AlreadyClaimed,

    /// <summary>The game is not there to write into. Nothing was written, and nothing was claimed.</summary>
    GameUnreachable,

    /// <summary>
    /// The game answered but nothing reached the bag, so the reward is still there to take.
    /// </summary>
    NotDelivered,

    /// <summary>No such reward in the catalogue.</summary>
    UnknownReward
}

/// <param name="Missing">Achievements still to unlock, so the screen can say what is left.</param>
/// <param name="Delivered">What actually reached the bag, as read back from it.</param>
public sealed record RewardResult(
    RewardOutcome Outcome,
    Reward? Reward,
    IReadOnlyList<string> Missing,
    IReadOnlyList<RewardItem> Delivered,
    string Message)
{
    public bool Succeeded => Outcome is RewardOutcome.Delivered or RewardOutcome.PartlyDelivered;
}

/// <param name="Unlocked">How many of the required achievements are already done.</param>
/// <param name="Required">How many there are in total.</param>
public sealed record RewardStatus(Reward Reward, int Unlocked, int Required, bool Claimed)
{
    public bool CanClaim => Unlocked >= Required && !Claimed;

    public string Progress => $"{Unlocked}/{Required}";
}
