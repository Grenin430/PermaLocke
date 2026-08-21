using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Counts what the run has done and hands out the points for it.
/// </summary>
/// <remarks>
/// <para>
/// Progress is a projection over the event log, exactly like the balance: nothing stores a
/// counter that could drift from the history. Recomputing it from scratch always gives the same
/// answer, and that is what makes a claim checkable.
/// </para>
/// <para>
/// Unlocking and claiming are separate on purpose. The run shows <c>3 / 5</c> as it goes, and the
/// player collects with a button, so the log says where every point came from and when.
/// </para>
/// </remarks>
public sealed class AchievementService(IAchievementCatalog catalog, IPointsService points,
    IEventStore events, IClock clock)
{
    public IReadOnlyList<Achievement> All => catalog.All;

    /// <summary>Where every achievement of the run stands right now.</summary>
    public async Task<IReadOnlyList<AchievementProgress>> GetProgressAsync(Guid runId,
        CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        var counts = all
            .GroupBy(e => e.Type)
            .ToDictionary(group => group.Key, group => group.Count());

        var claimed = all
            .Where(e => e.Type == GameEventType.AchievementUnlocked && e.Data.TryGetValue("logro", out _))
            .Select(e => e.Data["logro"])
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. catalog.All.Select(achievement => new AchievementProgress(
                achievement,
                achievement.Trigger is { } trigger ? counts.GetValueOrDefault(trigger) : 0,
                claimed.Contains(achievement.Id)))
        ];
    }

    /// <summary>
    /// Collects one achievement: the points are earned and the claim is written down.
    /// </summary>
    /// <remarks>
    /// Refused when it is not unlocked or was already collected, and the refusal writes nothing:
    /// a claim that did not happen must leave no trace.
    /// </remarks>
    public async Task<PointsResult> ClaimAsync(Run run, string achievementId,
        CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(run.Id, ct).ConfigureAwait(false);
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (progress.FirstOrDefault(p => p.Achievement.Id == achievementId) is not { } found)
        {
            return new PointsResult(false, balance, $"No existe el logro «{achievementId}».");
        }

        if (found.Claimed)
        {
            return new PointsResult(false, balance, $"«{found.Achievement.Name}» ya estaba cobrado.");
        }

        if (!found.Unlocked)
        {
            return new PointsResult(false, balance, found.Achievement.IsDetectable
                ? $"«{found.Achievement.Name}» va por {found.Count} de {found.Achievement.Target}."
                : $"«{found.Achievement.Name}» cuenta algo que PermaLocke todavía no detecta "
                  + $"({found.Achievement.TriggerName}).");
        }

        // El evento de cobro primero: si algo fallara después, el logro queda cobrado y sin
        // puntos, que se ve y se arregla. Al revés quedarían puntos sin explicación.
        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AchievementUnlocked,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Logro «{found.Achievement.Name}»: +{found.Achievement.Points} puntos.",
            Data = new Dictionary<string, string>
            {
                ["logro"] = found.Achievement.Id,
                ["nombre"] = found.Achievement.Name,
                ["puntos"] = found.Achievement.Points.ToString(),
                ["contador"] = found.Count.ToString()
            }
        }, ct).ConfigureAwait(false);

        if (found.Achievement.Points <= 0)
        {
            return new PointsResult(true, balance);
        }

        return await points.EarnAsync(run.Id, found.Achievement.Points,
            $"Logro «{found.Achievement.Name}».", EventSource.Player, run.PlayerName, ct)
            .ConfigureAwait(false);
    }
}
