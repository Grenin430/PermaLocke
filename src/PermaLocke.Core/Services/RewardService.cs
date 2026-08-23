using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Hands over the one-off prizes of the competition, once each.
/// </summary>
/// <remarks>
/// <para>
/// Three things have to be true before anything is written, and each is checked against something
/// real rather than against a flag somebody set: <b>earned</b> — every achievement the reward
/// names is unlocked, which the achievements work out from the cartridge itself; <b>not taken</b>
/// — no <see cref="GameEventType.RewardClaimed"/> for it in the history, which is what makes "once"
/// mean once; and <b>deliverable</b> — the bag is there to write into.
/// </para>
/// <para>
/// The order is the shop's, and for the shop's reason (§45): <b>deliver first, record second</b>.
/// A claim recorded before a delivery that then failed would burn a one-off prize and leave the
/// player nothing to appeal to.
/// </para>
/// <para>
/// Partial delivery still counts as claimed. Two items go into the bag one after the other, and if
/// the emulator disappears between them, the choice is between a player who is owed a few Full
/// Heals and a button that can be pressed again to duplicate the Hyper Potions it already gave.
/// In a competition the second is the worse failure, so the event records exactly what arrived and
/// the screen says it out loud.
/// </para>
/// </remarks>
public sealed class RewardService(
    IRewardCatalog catalog,
    AchievementService achievements,
    IItemDelivery delivery,
    IItemLookup items,
    IEventStore events,
    IClock clock)
{
    public IReadOnlyList<Reward> All => catalog.All;

    /// <summary>Where every reward of the run stands: earned, taken, or how far off.</summary>
    public async Task<IReadOnlyList<RewardStatus>> GetStatusAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (catalog.All.Count == 0)
        {
            return [];
        }

        var unlocked = await UnlockedAsync(run, ct).ConfigureAwait(false);
        var claimed = await ClaimedAsync(run.Id, ct).ConfigureAwait(false);

        return
        [
            .. catalog.All.Select(reward => new RewardStatus(
                reward,
                reward.Achievements.Count(unlocked.Contains),
                reward.Achievements.Count,
                claimed.Contains(reward.Id)))
        ];
    }

    public async Task<RewardResult> ClaimAsync(Run run, string rewardId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (catalog.All.FirstOrDefault(r => string.Equals(r.Id, rewardId, StringComparison.OrdinalIgnoreCase))
            is not { } reward)
        {
            return new RewardResult(RewardOutcome.UnknownReward, null, [], [],
                $"No existe el premio «{rewardId}».");
        }

        if ((await ClaimedAsync(run.Id, ct).ConfigureAwait(false)).Contains(reward.Id))
        {
            return new RewardResult(RewardOutcome.AlreadyClaimed, reward, [], [],
                $"«{reward.Name}» ya está recogido. Es de una sola vez.");
        }

        var unlocked = await UnlockedAsync(run, ct).ConfigureAwait(false);
        var missing = reward.Achievements.Where(id => !unlocked.Contains(id)).ToList();

        if (missing.Count > 0)
        {
            return new RewardResult(RewardOutcome.NotEarned, reward, missing, [],
                $"Todavía no: te faltan {missing.Count} de {reward.Achievements.Count}.");
        }

        // El nombre se comprueba contra la tabla del cartucho ANTES de escribir. Un id copiado de
        // memoria que caiga en otro objeto entrega otra cosa y no falla nunca (§52).
        if (reward.Items.FirstOrDefault(item => !Matches(item)) is { } wrong)
        {
            return new RewardResult(RewardOutcome.NotDelivered, reward, [], [],
                $"No se entrega nada: PermaLocke esperaba que el objeto {wrong.Id} fuese "
                + $"«{wrong.Name}» y la tabla del juego dice «{items.GetName(wrong.Id)}».");
        }

        var delivered = new List<RewardItem>();
        var failed = new List<string>();
        var reachable = true;

        foreach (var item in reward.Items)
        {
            var result = await delivery.GiveAsync(item.Id, item.Amount, ct).ConfigureAwait(false);

            if (result.Delivered)
            {
                delivered.Add(item);
                continue;
            }

            failed.Add($"{item.Name}: {result.Problem}");
            reachable &= result.GameReachable;
        }

        if (delivered.Count == 0)
        {
            // Nada ha llegado, así que nada se recoge: el premio sigue disponible.
            return new RewardResult(
                reachable ? RewardOutcome.NotDelivered : RewardOutcome.GameUnreachable,
                reward, [], [],
                failed.Count > 0
                    ? string.Join(" ", failed)
                    : "No se ha podido escribir en la mochila.");
        }

        await RecordAsync(run, reward, delivered, failed, ct).ConfigureAwait(false);

        var what = string.Join(" y ", delivered.Select(item => $"{item.Amount} {item.Name}"));

        return delivered.Count == reward.Items.Count
            ? new RewardResult(RewardOutcome.Delivered, reward, [], delivered,
                $"{what} en la mochila. Escrito y releído. «{reward.Name}» queda recogido.")
            : new RewardResult(RewardOutcome.PartlyDelivered, reward, [], delivered,
                $"Solo ha llegado parte: {what}. No llegó {string.Join("; ", failed)}. "
                + "El premio queda recogido igualmente para que el botón no lo dé dos veces.");
    }

    /// <summary>Ids of the achievements the run has already unlocked.</summary>
    private async Task<HashSet<string>> UnlockedAsync(Run run, CancellationToken ct)
    {
        var progress = await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false);

        return progress
            .Where(p => p.Unlocked)
            .Select(p => p.Achievement.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Ids of the rewards already taken, straight from the history.</summary>
    private async Task<HashSet<string>> ClaimedAsync(Guid runId, CancellationToken ct)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        return history
            .Where(e => e.Type == GameEventType.RewardClaimed)
            .Select(e => e.Data.TryGetValue("premio", out var id) ? id : null)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private bool Matches(RewardItem item) =>
        string.Equals(items.GetName(item.Id), item.Name, StringComparison.OrdinalIgnoreCase);

    private Task RecordAsync(Run run, Reward reward, IReadOnlyList<RewardItem> delivered,
        IReadOnlyList<string> failed, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.RewardClaimed,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Premio «{reward.Name}» recogido: "
                          + string.Join(", ", delivered.Select(item => $"{item.Amount} {item.Name}"))
                          + ".",
            Reason = reward.Description,
            Data = new Dictionary<string, string>
            {
                ["premio"] = reward.Id,
                ["entregado"] = string.Join(", ",
                    delivered.Select(item => $"{item.Id}x{item.Amount}")),
                ["completo"] = (failed.Count == 0).ToString(),
                ["noEntregado"] = string.Join("; ", failed)
            }
        }, ct);
}
