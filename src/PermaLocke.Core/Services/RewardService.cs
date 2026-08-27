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
    IGameRecords records,
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

        var held = await HeldAsync(WantedItems(catalog.All), ct).ConfigureAwait(false);

        return
        [
            .. catalog.All.Select(reward => new RewardStatus(
                reward,
                reward.Achievements.Count(unlocked.Contains) + reward.HeldItems.Count(held.Contains),
                reward.Conditions,
                claimed.Contains(reward.Id)))
        ];
    }

    /// <summary>
    /// Hands over every prize marked automatic that is earned and not yet taken.
    /// </summary>
    /// <remarks>
    /// Called from the game link, so it only ever runs with the emulator answering. It goes
    /// through <see cref="ClaimAsync"/> exactly as the button does -- same conditions, same
    /// name check, same write-then-read, same one-off event -- because the difference between
    /// automatic and manual is who pressed it, not what is allowed.
    /// </remarks>
    public async Task<IReadOnlyList<RewardResult>> ClaimAutomaticAsync(Run run,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var automatic = catalog.All.Where(reward => reward.Automatic).ToList();

        if (automatic.Count == 0)
        {
            return [];
        }

        // Deliberadamente NO pasa por GetStatusAsync, que pregunta por todos los premios. Esto se
        // llama en bucle desde el enlace con el juego, asi que cada lectura que no haga falta se
        // paga muchas veces: una vez recogidos, la comprobacion no toca ni la partida ni la
        // mochila, solo el historial, y un premio de una vez no se des-recoge nunca.
        var claimed = await ClaimedAsync(run.Id, ct).ConfigureAwait(false);
        var pending = automatic.Where(reward => !claimed.Contains(reward.Id)).ToList();

        if (pending.Count == 0)
        {
            return [];
        }

        var held = await HeldAsync(WantedItems(pending), ct).ConfigureAwait(false);

        var unlocked = pending.Any(reward => reward.Achievements.Count > 0)
            ? await UnlockedAsync(run, ct).ConfigureAwait(false)
            : [];

        var given = new List<RewardResult>();

        foreach (var reward in pending)
        {
            var earned = reward.Achievements.All(unlocked.Contains)
                         && reward.HeldItems.All(held.Contains);

            if (earned)
            {
                given.Add(await ClaimAsync(run, reward.Id, ct).ConfigureAwait(false));
            }
        }

        return given;
    }

    /// <summary>Every item id some reward is waiting on, asked for in one go.</summary>
    private static IReadOnlyList<int> WantedItems(IEnumerable<Reward> rewards) =>
        [.. rewards.SelectMany(reward => reward.HeldItems).Distinct()];

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
        var held = await HeldAsync(reward.HeldItems, ct).ConfigureAwait(false);

        var missing = reward.Achievements.Where(id => !unlocked.Contains(id))
            .Concat(reward.HeldItems.Where(id => !held.Contains(id)).Select(id => items.GetName(id)))
            .ToList();

        if (missing.Count > 0)
        {
            return new RewardResult(RewardOutcome.NotEarned, reward, missing, [],
                $"Todavía no: te faltan {missing.Count} de {reward.Conditions}.");
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

    /// <summary>
    /// What the player is carrying: the live bag first, the saved game as the fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the whole point. The save only tells you what the bag held the last time the
    /// player saved, so a prize conditioned on carrying something would sit there dark until they
    /// remembered to save — which is exactly the complaint that produced this. The running game's
    /// bag is readable directly (§22) and changes the moment the item does, so the prize arrives
    /// on picking the item up, the same way the level cap acts without anybody saving.
    /// </para>
    /// <para>
    /// The fallback is not decoration: with Azahar closed the live bag cannot answer at all, and
    /// falling back means the screen can still say whether a prize is earned. What it must never
    /// do is answer <em>wrongly</em>, so an unreachable game returns no items rather than none
    /// carried, and both ends fail towards "not earned".
    /// </para>
    /// </remarks>
    private async Task<IReadOnlySet<int>> HeldAsync(IReadOnlyList<int> wanted, CancellationToken ct)
    {
        if (wanted.Count == 0)
        {
            return new HashSet<int>();
        }

        try
        {
            // Con el juego respondiendo devuelve una entrada por id pedido, aunque sea cero; con el
            // juego cerrado devuelve el diccionario vacio. Por eso vacio significa «no se sabe» y
            // no «no lleva ninguno», y por eso se pregunta solo por una lista no vacia.
            var live = await delivery.CarriedAllAsync(wanted, ct).ConfigureAwait(false);

            if (live.Count > 0)
            {
                return live.Where(pair => pair.Value > 0).Select(pair => pair.Key).ToHashSet();
            }
        }
        catch (Exception)
        {
            // Se cae al fichero de partida, que es peor pero es algo.
        }

        try
        {
            var snapshot = await records.ReadAsync(ct).ConfigureAwait(false);
            return snapshot.Available && snapshot.Items is { } held ? held : new HashSet<int>();
        }
        catch (Exception)
        {
            return new HashSet<int>();
        }
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
