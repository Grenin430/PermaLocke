using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <param name="Collected">True when the gift is now the player's and will never be offered again.</param>
/// <param name="Message">One line for the inbox.</param>
public sealed record GiftResult(bool Collected, string Message);

/// <summary>
/// Collecting the gifts the admin left in the shared folder (§129).
/// </summary>
/// <remarks>
/// <para>
/// The admin writes a request; this is what turns it into points, items and credits, and it runs on the player's own
/// machine because that is where their run lives. Every gift lands in the history as an
/// <see cref="GameEventType.AdminGiftClaimed"/> carrying its id, and «already collected» means «that event exists» —
/// the same thing that makes a one-off prize one-off (§60).
/// </para>
/// <para>
/// Order as in the shop and the prizes (§45): <b>the bag first, the record second</b>. Points and credits cannot fail
/// once started, items can, and a gift marked collected before an item that never arrived would be one nobody can
/// appeal. A gift with items refuses to start at all when the game is shut, so nothing is half done.
/// </para>
/// <para>
/// Partial delivery still counts as collected, for the reason the prizes give: owing somebody a Hyper Potion is a
/// smaller failure than a button that can be pressed again to duplicate what already arrived.
/// </para>
/// </remarks>
public sealed class GiftService(IEventStore events, IPointsService points, IItemDelivery delivery, IClock clock)
{
    /// <summary>The gifts this run has already collected, by id.</summary>
    public async Task<IReadOnlySet<Guid>> CollectedAsync(Guid runId, CancellationToken ct = default)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        return history
            .Where(e => e.Type == GameEventType.AdminGiftClaimed)
            .Select(e => e.Data.TryGetValue("regalo", out var id) && Guid.TryParse(id, out var gift) ? gift : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
    }

    /// <summary>Applies one gift to this run, once.</summary>
    public async Task<GiftResult> ClaimAsync(Run run, AdminGift gift, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(gift);

        if ((await CollectedAsync(run.Id, ct).ConfigureAwait(false)).Contains(gift.Id))
        {
            return new GiftResult(false, "Ya lo habías recogido.");
        }

        if (gift.IsEmpty)
        {
            return new GiftResult(false, "Este regalo no trae nada.");
        }

        var wanted = gift.Items.Where(item => item.Amount > 0).ToList();

        // Con objetos de por medio se pregunta ANTES de tocar nada: un diccionario vacío es «no se sabe» y no
        // «no lleva ninguno» (§68), así que con el juego cerrado el regalo se queda esperando entero.
        if (wanted.Count > 0)
        {
            var carried = await delivery.CarriedAllAsync([.. wanted.Select(item => item.Id)], ct).ConfigureAwait(false);

            if (carried.Count == 0)
            {
                return new GiftResult(false, "Abre Azahar con la partida cargada para recogerlo.");
            }
        }

        var delivered = new List<GiftItem>();
        var failed = new List<string>();

        foreach (var item in wanted)
        {
            var result = await delivery.GiveAsync(item.Id, item.Amount, ct).ConfigureAwait(false);

            if (result.Delivered)
            {
                delivered.Add(item);
            }
            else
            {
                failed.Add(item.Name);
            }
        }

        if (wanted.Count > 0 && delivered.Count == 0)
        {
            return new GiftResult(false, "No se ha podido entregar. Vuelve a intentarlo.");
        }

        if (gift.Points != 0)
        {
            await points.AdjustAsync(run.Id, gift.Points, $"{(gift.Adjustment ? "Ajuste" : "Regalo")} de {gift.From}: {gift.Reason}", gift.From, ct)
                .ConfigureAwait(false);
        }

        await RecordAsync(run, gift, delivered, failed, ct).ConfigureAwait(false);

        return new GiftResult(true, failed.Count == 0
            ? $"Recogido: {gift.Say()}."
            : $"Recogido a medias: no ha llegado {string.Join(", ", failed)}.");
    }

    /// <summary>
    /// Writes the event that collects the gift, and with it the credits it carries.
    /// </summary>
    /// <remarks>
    /// Free rolls go in <c>credito</c> and wonder trades in <c>creditoIntercambio</c>, which are the fields
    /// <see cref="CreditService"/> already counts whoever wrote them (§63): one entry per roll, so two rolls in a
    /// banner name it twice. Nothing new had to be taught about gifts for them to pay.
    /// </remarks>
    private async Task RecordAsync(Run run, AdminGift gift, IReadOnlyList<GiftItem> delivered,
        IReadOnlyList<string> failed, CancellationToken ct)
    {
        var data = new Dictionary<string, string>
        {
            ["regalo"] = gift.Id.ToString(),
            ["de"] = gift.From,
            ["motivo"] = gift.Reason
        };

        if (delivered.Count > 0)
        {
            data["objetos"] = string.Join("; ", delivered.Select(item => $"{item.Amount}x{item.Name}"));
        }

        if (failed.Count > 0)
        {
            data["noEntregado"] = string.Join("; ", failed);
        }

        var rolls = gift.Rolls
            .Where(roll => roll.Value > 0)
            .SelectMany(roll => Enumerable.Repeat(roll.Key, roll.Value))
            .ToList();

        if (rolls.Count > 0)
        {
            data["credito"] = string.Join(",", rolls);
        }

        if (gift.WonderTrades > 0)
        {
            data["creditoIntercambio"] = gift.WonderTrades.ToString();
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AdminGiftClaimed,
            Source = EventSource.Admin,
            Actor = gift.From,
            Description = $"{(gift.Adjustment ? "Ajuste" : "Regalo")} de {gift.From}: {gift.Say()}.",
            Reason = gift.Reason,
            Data = data
        }, ct).ConfigureAwait(false);
    }
}
