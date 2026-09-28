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
    IClock clock,
    INatureChanger? natures = null)
{
    public IReadOnlyList<ShopItem> Items => catalog.Items;

    /// <summary>
    /// Why the shop cannot sell now, or null when it can (2026-09-28): closed by the organiser, or not open until a trial
    /// this run has not cleared yet.
    /// </summary>
    public string? ClosedReason(int clearedTrials) =>
        !catalog.Open ? "La tienda está cerrada por el organizador."
        : clearedTrials < catalog.OpensAtTrial ? $"La tienda abre al superar la prueba {catalog.OpensAtTrial}."
        : null;

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
        if (!catalog.Open)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, null, await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false), 0,
                "La tienda está cerrada por el organizador.");
        }

        return await BuyOpenAsync(run, itemId, ct).ConfigureAwait(false);
    }

    private async Task<PurchaseResult> BuyOpenAsync(Run run, int itemId, CancellationToken ct)
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

        if (item.IsUnlock)
        {
            return await UnlockAsync(run, item, ct).ConfigureAwait(false);
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


    /// <summary>Whether a herb can be used right now: the game has to be closed. False with the reason otherwise.</summary>
    public bool CanChangeNatureNow(out string reason)
    {
        if (natures is null)
        {
            reason = "Esta versión no puede cambiar naturalezas.";
            return false;
        }

        return natures.CanChangeNow(out reason);
    }

    /// <summary>
    /// Uses a nature herb on a Pokémon of the save (2026-09-27): writes the nature, then records and charges, the order of
    /// every purchase here, so nobody pays for a nature the save refused.
    /// </summary>
    public async Task<PurchaseResult> ChangeNatureAsync(Run run, int itemId, BoxedPokemon target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (!catalog.Open)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, null, balance, 0, "La tienda está cerrada por el organizador.");
        }

        if (catalog.Items.FirstOrDefault(i => i.Id == itemId) is not { IsHerb: true } herb)
        {
            return new PurchaseResult(PurchaseOutcome.UnknownItem, null, balance, 0, $"La tienda no vende la hierba {itemId}.");
        }

        if (balance < herb.Price)
        {
            return new PurchaseResult(PurchaseOutcome.NotEnoughPoints, herb, balance, 0,
                $"{herb.Name} cuesta {herb.Price} y tienes {balance}. Te faltan {herb.Price - balance}.");
        }

        if (!target.IsIntact || target.IsEgg)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, herb, balance, 0, $"{target.DisplayName} no se puede tocar.");
        }

        if (target.Nature == herb.Nature)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, herb, balance, 0,
                $"{target.DisplayName} ya tiene esa naturaleza. No se ha cobrado nada.");
        }

        if (natures is null)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, herb, balance, 0, "Esta versión no puede cambiar naturalezas.");
        }

        var written = await natures.ApplyAsync(
            new NatureChange(target.Box, target.Slot, target.Pid, target.DisplayName, herb.Nature), ct).ConfigureAwait(false);

        if (!written.Delivered)
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, herb, balance, 0, written.Message);
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.ShopPurchase,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Tienda: {herb.Name} por {herb.Price} puntos para {target.DisplayName}.",
            Data = new Dictionary<string, string>
            {
                ["objeto"] = herb.Id.ToString(),
                ["nombre"] = herb.Name,
                ["precio"] = herb.Price.ToString(),
                ["pid"] = target.Pid.ToString("X8"),
                ["naturalezaAntes"] = target.Nature.ToString(),
                ["naturaleza"] = herb.Nature.ToString()
            }
        }, ct).ConfigureAwait(false);

        var spent = await points
            .SpendAsync(run.Id, herb.Price, $"Tienda: {herb.Name}.", EventSource.Player, run.PlayerName, ct)
            .ConfigureAwait(false);

        return new PurchaseResult(PurchaseOutcome.Delivered, herb, spent.NewBalance, 1,
            $"{target.DisplayName} ya tiene la naturaleza nueva. {written.Message}".TrimEnd());
    }
    /// <summary>The key of a purchase's event that says which move it unlocked.</summary>
    public const string UnlockKey = "desbloqueo";

    /// <summary>The species that unlock is for.</summary>
    public const string UnlockSpeciesKey = "especie";

    /// <summary>
    /// Every (species, move) this run has bought, from its own history: an unlock is paid once and lasts the run.
    /// </summary>
    public static async Task<IReadOnlySet<(int Species, int Move)>> UnlockedAsync(IEventStore events, Guid runId,
        CancellationToken ct = default)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        return history
            .Where(e => e.Type == GameEventType.ShopPurchase
                        && e.Data.TryGetValue(UnlockKey, out var move) && int.TryParse(move, out _)
                        && e.Data.TryGetValue(UnlockSpeciesKey, out var species) && int.TryParse(species, out _))
            .Select(e => (int.Parse(e.Data[UnlockSpeciesKey]), int.Parse(e.Data[UnlockKey])))
            .ToHashSet();
    }

    /// <summary>The unlocks this run has bought.</summary>
    public Task<IReadOnlySet<(int Species, int Move)>> UnlockedAsync(Guid runId, CancellationToken ct = default) =>
        UnlockedAsync(events, runId, ct);

    /// <summary>Buys an unlock: nothing goes into the bag, so it needs no game, and it is bought once.</summary>
    private async Task<PurchaseResult> UnlockAsync(Run run, ShopItem item, CancellationToken ct)
    {
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if ((await UnlockedAsync(events, run.Id, ct).ConfigureAwait(false)).Contains((item.UnlockSpecies, item.UnlockMove)))
        {
            return new PurchaseResult(PurchaseOutcome.NotDelivered, item, balance, 1,
                $"{item.Name} ya está comprado: enséñalo gratis en EQUIPO › MOVIMIENTOS.");
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
                [UnlockKey] = item.UnlockMove.ToString(),
                [UnlockSpeciesKey] = item.UnlockSpecies.ToString()
            }
        }, ct).ConfigureAwait(false);

        var spent = await points
            .SpendAsync(run.Id, item.Price, $"Tienda: {item.Name}.", EventSource.Player, run.PlayerName, ct)
            .ConfigureAwait(false);

        return new PurchaseResult(PurchaseOutcome.Delivered, item, spent.NewBalance, 1,
            $"{item.Name} comprado: enséñalo en EQUIPO › MOVIMIENTOS, gratis siempre que quieras.");
    }
}
