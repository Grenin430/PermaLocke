namespace PermaLocke.Core.Domain;

/// <summary>
/// Free gacha rolls and wonder trades a milestone hands over.
/// </summary>
/// <param name="Achievement">The achievement that pays for it, so nothing is marked by hand.</param>
/// <param name="Rolls">How many free rolls per banner id.</param>
public sealed record MilestoneGrant(
    string Achievement,
    string Name,
    IReadOnlyDictionary<string, int> Rolls,
    int WonderTrades);

/// <summary>
/// A running total of what the player may still use for free.
/// </summary>
/// <remarks>
/// Never stored anywhere. It is always <em>earned minus spent</em>, both worked out from things
/// that cannot drift: the achievements the cartridge proves, and the events the history keeps.
/// A counter kept in a file would be one more number able to disagree with reality.
/// </remarks>
public sealed record RunCredits(IReadOnlyDictionary<string, int> Rolls, int WonderTrades)
{
    public static RunCredits None { get; } = new(new Dictionary<string, int>(), 0);

    /// <summary>Free rolls left on one banner. Zero for a banner nobody has earned any on.</summary>
    public int RollsOn(string bannerId) =>
        Rolls.TryGetValue(bannerId, out var count) ? Math.Max(0, count) : 0;

    public int TotalRolls => Rolls.Values.Where(count => count > 0).Sum();

    public bool Any => TotalRolls > 0 || WonderTrades > 0;

    /// <summary>What is left of this once <paramref name="spent"/> has been taken out.</summary>
    public RunCredits Minus(RunCredits spent)
    {
        ArgumentNullException.ThrowIfNull(spent);

        var left = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (banner, count) in Rolls)
        {
            left[banner] = Math.Max(0, count - spent.RollsOn(banner));
        }

        return new RunCredits(left, Math.Max(0, WonderTrades - spent.WonderTrades));
    }

    /// <summary>"POCHO 2 · BUENO 1", or empty when there is nothing to spend.</summary>
    public string Describe(Func<string, string> nameOf) =>
        string.Join(" · ", Rolls
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{nameOf(entry.Key)} {entry.Value}"));
}

/// <summary>What each milestone grants, read from configuration rather than compiled in.</summary>
public interface ICreditCatalog
{
    IReadOnlyList<MilestoneGrant> Milestones { get; }

    /// <summary>
    /// True when a wonder trade needs a credit to happen at all.
    /// </summary>
    /// <remarks>
    /// It is what turns "te dan 1 wonder trade" into something that means anything: they used to
    /// be free and unlimited. Configuration and not a constant, because it is a rule of the
    /// competition and not a fact about the game.
    /// </remarks>
    bool LimitWonderTrades { get; }
}
