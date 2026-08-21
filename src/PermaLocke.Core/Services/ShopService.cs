using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Sells the competition's items for run points and puts them in the player's bag.
/// </summary>
/// <remarks>
/// <para>
/// The order matters and is deliberate: <b>deliver first, charge second</b>. The delivery is the
/// step that can fail for reasons outside PermaLocke — the emulator closed, the pocket full, the
/// write rejected — and a player charged for an item that never arrived has no way to get their
/// points back. Charging afterwards means the worst case is an item given away free, which is
/// visible in the bag and in the log rather than silently missing.
/// </para>
/// <para>
/// The purchase is a <see cref="GameEventType.ShopPurchase"/> event and the points move through
/// <see cref="IPointsService"/>, so the balance stays a projection over the log like everything
/// else: no counter anywhere to drift.
/// </para>
/// </remarks>
public sealed class ShopService(
    IShopCatalog catalog,
    IPointsService points,
    IItemDelivery delivery,
    IEventStore events,
    IClock clock)
{
    public IReadOnlyList<ShopItem> Items => catalog.Items;

    public Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default) =>
        points.GetBalanceAsync(runId, ct);

    /// <summary>How many of an item the bag holds, or -1 when the game cannot be reached.</summary>
    public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
        delivery.CarriedAsync(itemId, ct);

    /// <summary>How many of each item the bag holds, in one read. Empty when it cannot be read.</summary>
    public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
        IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
        delivery.CarriedAllAsync(itemIds, ct);

    public async Task<PurchaseResult> BuyAsync(Run run, int itemId, CancellationToken ct = default)
    {
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (catalog.Items.FirstOrDefault(i => i.Id == itemId) is not { } item)
        {
            return new PurchaseResult(PurchaseOutcome.UnknownItem, null, balance, 0,
                $"La tienda no vende el objeto {itemId}.");
        }

        if (balance < item.Price)
        {
            return new PurchaseResult(PurchaseOutcome.NotEnoughPoints, item, balance, 0,
                $"{item.Name} cuesta {item.Price} y tienes {balance}. Te faltan {item.Price - balance}.");
        }

        var given = await delivery.GiveAsync(item.Id, 1, ct).ConfigureAwait(false);

        if (!given.Delivered)
        {
            var outcome = given.GameReachable
                ? PurchaseOutcome.NotDelivered
                : PurchaseOutcome.GameUnreachable;

            return new PurchaseResult(outcome, item, balance, 0, given.Problem);
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.ShopPurchase,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Tienda: {item.Name} por {item.Price} puntos.",
            Data = new Dictionary<string, string>
            {
                ["objeto"] = item.Id.ToString(),
                ["nombre"] = item.Name,
                ["precio"] = item.Price.ToString(),
                ["llevaAhora"] = given.Carried.ToString()
            }
        }, ct).ConfigureAwait(false);

        var spent = await points
            .SpendAsync(run.Id, item.Price, $"Tienda: {item.Name}.", EventSource.Player, run.PlayerName, ct)
            .ConfigureAwait(false);

        return new PurchaseResult(PurchaseOutcome.Delivered, item, spent.NewBalance, given.Carried,
            $"{item.Name} está en tu mochila. Llevas {given.Carried}.");
    }
}
