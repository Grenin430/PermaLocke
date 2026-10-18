using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <param name="Closed">True when the order is finished, done or refused for good, and must not run again.</param>
/// <param name="Message">What happened, for the player's notice and the organiser's list.</param>
public sealed record OrderResult(bool Closed, string Message);

/// <summary>
/// Applies the organiser's orders to this player's run (2026-09-26), through the same services the player's own buttons
/// use, and closes each one with an <see cref="GameEventType.AdminGiftClaimed"/> so it never runs twice.
/// </summary>
/// <remarks>
/// <para>
/// An order that cannot be done yet (the item needs the game open, the Pokémon needs it closed) is left open and tried
/// again on the next look. One that can never be done (the Pokémon is not in the run, it is already alive) is closed with
/// the reason, so the organiser reads why instead of it waiting forever.
/// </para>
/// <para>
/// Every change is an event with the organiser as its actor: deaths and revivals through <see cref="GameWatcher"/>, wipes
/// through <see cref="PenaltyService"/>, routes through <see cref="ZoneOutcomeService"/>, stages through
/// <see cref="ProgressService"/>. Nothing here writes a number straight into the run (rule 4).
/// </para>
/// </remarks>
public sealed class OrderService(IEventStore events, IPokemonRepository pokemon, GameWatcher watcher,
    PenaltyService penalties, ZoneOutcomeService zones, ProgressService progress, IItemDelivery items,
    IPokemonDelivery deliveries, ISpeciesStatsCatalog species, IRunRepository runs, IRunContext runContext, IClock clock,
    ILogger<OrderService> logger)
{
    public async Task<OrderResult> ApplyAsync(Run run, AdminGift gift, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (gift.Order is not { } order)
        {
            return new OrderResult(true, "No es una orden.");
        }

        OrderResult result;

        try
        {
            result = order.Kind switch
            {
                AdminOrderKinds.Revive => await ReviveAsync(run, gift, order, ct),
                AdminOrderKinds.Kill => await KillAsync(run, gift, order, ct),
                AdminOrderKinds.RevokeWipe => await RevokeWipeAsync(run, gift, order, ct),
                AdminOrderKinds.FreeZone => await FreeZoneAsync(run, gift, order, ct),
                AdminOrderKinds.SetStages => await SetStagesAsync(run, gift, order, ct),
                AdminOrderKinds.GiveItem => await GiveItemAsync(order, ct),
                AdminOrderKinds.GivePokemon => await GivePokemonAsync(run, gift, order, ct),
                AdminOrderKinds.Message => new OrderResult(true, order.Arg("texto")),
                AdminOrderKinds.PlayLock => await PlayLockAsync(run, gift, order, ct),
                _ => new OrderResult(true, "Esta versión de PermaLocke no sabe hacer esa orden.")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falló la orden {Kind} {Id}", order.Kind, gift.Id);
            return new OrderResult(false, "No se ha podido aplicar; se volverá a intentar.");
        }

        if (result.Closed)
        {
            await CloseAsync(run, gift, order, result.Message, ct);
        }

        return result;
    }

    /// <summary>True when the order has been closed in this run.</summary>
    public static bool IsClosed(IEnumerable<GameEvent> history, Guid orderId) =>
        history.Any(e => e.Type == GameEventType.AdminGiftClaimed
                         && e.Data.TryGetValue("regalo", out var id) && id == orderId.ToString());

    /// <summary>Whether JUGAR is closed for this run by the organiser, and why.</summary>
    public async Task<string?> PlayLockReasonAsync(Guid runId, CancellationToken ct = default)
    {
        var last = (await events.GetAllAsync(runId, ct).ConfigureAwait(false))
            .LastOrDefault(e => e.Type == GameEventType.PlayLock);

        return last is not null && last.Data.TryGetValue("cerrado", out var closed) && closed == "true"
            ? last.Reason ?? "El organizador ha cerrado el juego."
            : null;
    }

    private async Task<PokemonEntry?> EntryAsync(Run run, AdminOrder order, CancellationToken ct) =>
        Guid.TryParse(order.Arg("pokemon"), out var id)
            ? (await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false)).FirstOrDefault(p => p.Id == id)
            : null;

    private async Task<OrderResult> ReviveAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        if (await EntryAsync(run, order, ct) is not { } entry)
        {
            return new OrderResult(true, "Ese Pokémon no está en la run.");
        }

        var refused = await watcher.RevokeDeathAsync(entry, gift.From, gift.Reason, EventSource.Admin, ct);
        return new OrderResult(true, refused ?? $"{entry.Nickname ?? entry.SpeciesName} vuelve a estar vivo.");
    }

    private async Task<OrderResult> KillAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        if (await EntryAsync(run, order, ct) is not { } entry)
        {
            return new OrderResult(true, "Ese Pokémon no está en la run.");
        }

        if (entry.Status != PokemonStatus.Alive)
        {
            return new OrderResult(true, $"{entry.Nickname ?? entry.SpeciesName} ya no está vivo.");
        }

        await watcher.RecordDeathAsync(entry, gift.From, EventSource.Admin, $"orden del organizador: {gift.Reason}", ct);
        return new OrderResult(true, $"{entry.Nickname ?? entry.SpeciesName} marcado como caído.");
    }

    private async Task<OrderResult> RevokeWipeAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        if (!Guid.TryParse(order.Arg("wipe"), out var wipe))
        {
            return new OrderResult(true, "No dice qué equipo caído.");
        }

        var refused = await penalties.RevokeWipeAsync(run.Id, wipe, gift.From, gift.Reason, ct);
        return new OrderResult(true, refused ?? "Equipo caído revocado y devuelto.");
    }

    private async Task<OrderResult> FreeZoneAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        var zone = order.Arg("zona");

        if (string.IsNullOrWhiteSpace(zone))
        {
            return new OrderResult(true, "No dice qué ruta.");
        }

        var name = string.IsNullOrWhiteSpace(order.Arg("nombre")) ? zone : order.Arg("nombre");
        await zones.ClearAsync(run.Id, zone, name, gift.From, gift.Reason, EventSource.Admin, ct);
        return new OrderResult(true, $"{name} vuelve a estar libre.");
    }

    private async Task<OrderResult> SetStagesAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        if (!int.TryParse(order.Arg("etapas"), out var wanted) || wanted < 0)
        {
            return new OrderResult(true, "Número de etapas no válido.");
        }

        // La run se relee: la del contexto puede venir de antes de otra orden.
        var current = (await runs.GetAllAsync(ct).ConfigureAwait(false)).FirstOrDefault(r => r.Id == run.Id) ?? run;

        if (wanted == current.ClearedStages)
        {
            return new OrderResult(true, $"Ya tenía {wanted} etapas.");
        }

        var updated = await progress.AdvanceAsync(current, wanted - current.ClearedStages, gift.From, ct);

        if (runContext.Current?.Id == updated.Id)
        {
            runContext.SetCurrent(updated);
        }

        return new OrderResult(true, $"Etapas: de {current.ClearedStages} a {wanted}.");
    }

    private async Task<OrderResult> GiveItemAsync(AdminOrder order, CancellationToken ct)
    {
        if (!int.TryParse(order.Arg("objeto"), out var item) || item <= 0
            || !int.TryParse(order.Arg("cantidad"), out var amount) || amount <= 0)
        {
            return new OrderResult(true, "Objeto o cantidad no válidos.");
        }

        // Sin lectura de la mochila no se sabe si está el juego: se espera, no se falla (§68).
        if ((await items.CarriedAllAsync([item], ct).ConfigureAwait(false)).Count == 0)
        {
            return new OrderResult(false, "Espera a que abras el juego con la partida cargada.");
        }

        var result = await items.GiveAsync(item, amount, ct).ConfigureAwait(false);
        var name = string.IsNullOrWhiteSpace(order.Arg("nombre")) ? $"objeto {item}" : order.Arg("nombre");

        return result.Delivered
            ? new OrderResult(true, $"{amount} x {name} en la mochila.")
            : new OrderResult(false, "No ha entrado en la mochila; se volverá a intentar.");
    }

    private async Task<OrderResult> GivePokemonAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        if (!int.TryParse(order.Arg("especie"), out var id) || species.All.FirstOrDefault(s => s.Id == id) is not { } chosen)
        {
            return new OrderResult(true, "Esa especie no existe en este mundo.");
        }

        if (!deliveries.CanDeliverNow(out var wait))
        {
            return new OrderResult(false, wait);
        }

        var level = int.TryParse(order.Arg("nivel"), out var asked) ? Math.Clamp(asked, 1, 100) : 5;
        var form = int.TryParse(order.Arg("forma"), out var f) ? f : 0;
        var shiny = order.Arg("variocolor") == "true";

        // Habilidad y naturaleza al azar como en el gacha, con una semilla que sale de la orden: la misma orden da siempre
        // el mismo Pokémon.
        var source = new SeededRandomSource((ulong)gift.Id.GetHashCode() & 0xFFFFFFFF);
        var ability = AbilityDraw.Roll(source, species.Abilities, species.BannedAbilities);
        var nature = source.Next(25);
        int[] ivs = [.. Enumerable.Range(0, 6).Select(_ => source.Next(32))];

        var pull = new GachaPull("admin", "admin", chosen.Id, chosen.Name, chosen.Legendary, chosen.BaseStatTotal, level,
            shiny, ivs, nature, nature < species.Natures.Count ? species.Natures[nature] : string.Empty, ability,
            ability > 0 && ability < species.Abilities.Count ? species.Abilities[ability] : string.Empty,
            source.Seed, 0, form);

        var delivered = await deliveries.DeliverAsync(pull, run, ct).ConfigureAwait(false);

        if (!delivered.Delivered)
        {
            return delivered.Outcome is DeliveryOutcome.GameRunning or DeliveryOutcome.SaveNotFound
                ? new OrderResult(false, delivered.Message)
                : new OrderResult(true, $"No se ha podido dar: {delivered.Message}");
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = chosen.Id,
            SpeciesName = pull.DisplayName,
            Level = level,
            IsShiny = shiny,
            Origin = PokemonOrigin.AdminGrant,
            EncounterType = EncounterType.Gift,
            ObtainedAt = clock.Now,
            ConsumedZoneEncounter = false,
            Pid = delivered.Pid == 0 ? null : delivered.Pid,
            Form = form
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);
        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonDelivered,
            Source = EventSource.Admin,
            Actor = gift.From,
            Description = $"{pull.DisplayName} Nv. {level}{(shiny ? " variocolor" : "")}, dado por {gift.From}. Caja {delivered.Box}, hueco {delivered.Slot}.",
            PokemonId = entry.Id,
            Reason = gift.Reason,
            Data = new Dictionary<string, string>
            {
                ["pid"] = delivered.Pid.ToString("X8"),
                ["caja"] = delivered.Box.ToString(),
                ["hueco"] = delivered.Slot.ToString(),
                ["origen"] = entry.Origin.ToString(),
                ["especie"] = chosen.Id.ToString(),
                ["forma"] = form.ToString(),
                ["nivel"] = level.ToString(),
                ["orden"] = gift.Id.ToString()
            }
        }, ct).ConfigureAwait(false);

        return new OrderResult(true, $"{pull.DisplayName} Nv. {level} en la caja {delivered.Box}.");
    }

    private async Task<OrderResult> PlayLockAsync(Run run, AdminGift gift, AdminOrder order, CancellationToken ct)
    {
        var closed = order.Arg("cerrado") == "true";

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PlayLock,
            Source = EventSource.Admin,
            Actor = gift.From,
            Description = closed ? $"{gift.From} ha cerrado el juego: {gift.Reason}" : $"{gift.From} ha vuelto a abrir el juego.",
            Reason = gift.Reason,
            Data = new Dictionary<string, string> { ["cerrado"] = closed ? "true" : "false" }
        }, ct).ConfigureAwait(false);

        return new OrderResult(true, closed ? "Juego cerrado por el organizador." : "Juego abierto otra vez.");
    }

    private Task CloseAsync(Run run, AdminGift gift, AdminOrder order, string message, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AdminGiftClaimed,
            Source = EventSource.Admin,
            Actor = gift.From,
            Description = $"Orden de {gift.From}: {order.Summary}. {message}",
            Reason = gift.Reason,
            Data = new Dictionary<string, string>
            {
                ["regalo"] = gift.Id.ToString(),
                ["de"] = gift.From,
                ["motivo"] = gift.Reason,
                ["orden"] = order.Kind,
                ["resultado"] = message
            }
        }, ct);
}
