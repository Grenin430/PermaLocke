using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// The free gacha rolls and wonder trades a run has, and how many are left.
/// </summary>
/// <remarks>
/// <para>
/// Same shape as the one-off prizes and the roulette spins, and for the same reason: <b>earned</b>
/// comes from the achievements, which work themselves out from the cartridge, and <b>spent</b>
/// comes from the history, which cannot be edited without breaking its own hash chain. Nothing is
/// stored, so nothing can drift.
/// </para>
/// <para>
/// Two things pay in. The milestones of <c>Data/grants.json</c>, and the LUDÓPATA wheel, whose
/// gacha faces no longer roll on the spot: they hand over credits, which the player then spends in
/// the gacha screen like any other free roll. That was the point of the change — a roll should
/// happen where rolls happen, with its reel and its sprite, not as a line of text on the wheel.
/// </para>
/// </remarks>
public sealed class CreditService(
    ICreditCatalog catalog,
    AchievementService achievements,
    IEventStore events)
{
    public bool LimitsWonderTrades => catalog.LimitWonderTrades;

    public IReadOnlyList<MilestoneGrant> Milestones => catalog.Milestones;

    /// <summary>Everything the run has ever been given, whether or not it has been used.</summary>
    public async Task<RunCredits> EarnedAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var progress = await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false);

        var unlocked = progress
            .Where(p => p.Unlocked)
            .Select(p => p.Achievement.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rolls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var trades = 0;

        foreach (var milestone in catalog.Milestones.Where(m => unlocked.Contains(m.Achievement)))
        {
            foreach (var (banner, count) in milestone.Rolls)
            {
                rolls[banner] = rolls.GetValueOrDefault(banner) + count;
            }

            trades += milestone.WonderTrades;
        }

        await AddWheelRollsAsync(run.Id, rolls, ct).ConfigureAwait(false);

        return new RunCredits(rolls, trades);
    }

    /// <summary>What has already been used, straight from the history.</summary>
    /// <remarks>
    /// Only events <b>marked as free</b> count. That is what keeps the thirty wonder trades this
    /// run made before any of this existed from being charged retroactively: they happened under
    /// the old rules, and a rule that reaches backwards is not a rule, it is a punishment.
    /// </remarks>
    public async Task<RunCredits> SpentAsync(Guid runId, CancellationToken ct = default)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        var rolls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var trades = 0;

        foreach (var e in history.Where(WasFree))
        {
            if (e.Type == GameEventType.GachaRoll && e.Data.TryGetValue("banner", out var banner))
            {
                rolls[banner] = rolls.GetValueOrDefault(banner) + 1;
            }
            else if (e.Type == GameEventType.WonderTrade)
            {
                trades++;
            }
        }

        return new RunCredits(rolls, trades);
    }

    /// <summary>What is still there to use.</summary>
    public async Task<RunCredits> AvailableAsync(Run run, CancellationToken ct = default)
    {
        var earned = await EarnedAsync(run, ct).ConfigureAwait(false);
        var spent = await SpentAsync(run.Id, ct).ConfigureAwait(false);

        return earned.Minus(spent);
    }

    private static bool WasFree(GameEvent e) =>
        e.Data.TryGetValue("gratis", out var free)
        && string.Equals(free, bool.TrueString, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Adds the rolls the wheel has handed out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Taken from what each spin <b>recorded that it granted</b>, in its own <c>credito</c> field,
    /// and not by looking the winning face up in the catalogue. Two things follow, and both matter.
    /// Editing <c>Data/roulette.json</c> cannot rewrite credit already given. And the spins made
    /// before gacha faces started granting credit -- back when they rolled on the spot and handed
    /// over the Pokémon there and then -- carry no such field, so they pay nothing now: otherwise
    /// they would pay twice for the one Pokémon they already delivered.
    /// </para>
    /// </remarks>
    private async Task AddWheelRollsAsync(Guid runId, Dictionary<string, int> rolls, CancellationToken ct)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        foreach (var spin in history.Where(e => e.Type == GameEventType.RouletteSpun))
        {
            if (!spin.Data.TryGetValue("credito", out var granted) || granted.Length == 0)
            {
                continue;
            }

            foreach (var banner in granted.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                rolls[banner] = rolls.GetValueOrDefault(banner) + 1;
            }
        }
    }
}
