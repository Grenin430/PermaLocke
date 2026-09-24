using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Takes points away when the competition says losing costs.
/// </summary>
/// <remarks>
/// <para>
/// A penalty is <b>not a purchase</b>, so it never asks whether the player can afford it: it
/// applies, and the balance goes negative if it has to. <see cref="IPointsService.SpendAsync"/>
/// refuses what cannot be paid for, which is right for a shop and wrong for a rule.
/// </para>
/// <para>
/// The balance is a projection over the event log, so writing an event with a negative delta is
/// the whole of it. Nothing stores a number that could disagree with the history.
/// </para>
/// </remarks>
public sealed class PenaltyService(IPenaltyCatalog catalog, IEventStore events, IClock clock,
    IRunRoles roles)
{
    public PenaltyRules Rules => catalog.Rules;

    /// <summary>How many wipes this run has already been charged for, not counting the revoked ones.</summary>
    public async Task<int> CountWipesAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Count(e => e.Type == GameEventType.TeamWiped) - Revoked(all).Count;
    }

    /// <summary>The ids of the wipes a <see cref="GameEventType.WipeRevoked"/> has taken back.</summary>
    private static HashSet<string> Revoked(IReadOnlyList<GameEvent> all) =>
        [.. all.Where(e => e.Type == GameEventType.WipeRevoked && e.Data.ContainsKey("equipoCaido"))
            .Select(e => e.Data["equipoCaido"])];

    /// <summary>
    /// Takes back a wipe that should not have been charged: the points it cost come back and it stops counting
    /// towards the four (§161).
    /// </summary>
    /// <remarks>
    /// An addition to the chain, like <see cref="GameEventType.DeathRevoked"/>: the <see cref="GameEventType.TeamWiped"/>
    /// stays. The refund is what that wipe's own event took, not today's price. It refuses rather than guesses: an id
    /// that is not a wipe of this run, or one already taken back.
    /// </remarks>
    /// <returns>Null when done; otherwise why nothing was done.</returns>
    public async Task<string?> RevokeWipeAsync(Guid runId, Guid wipeId, string actor, string reason,
        CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        if (all.FirstOrDefault(e => e.Id == wipeId && e.Type == GameEventType.TeamWiped) is not { } wipe)
        {
            return "Ese evento no es un equipo caído de esta run.";
        }

        if (Revoked(all).Contains(wipeId.ToString()))
        {
            return "Ese equipo caído ya está revocado.";
        }

        var refund = Math.Max(0, -wipe.PointsDelta);
        var why = string.IsNullOrWhiteSpace(reason) ? "sin motivo anotado" : reason.Trim();

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.WipeRevoked,
            Source = EventSource.Player,
            Actor = actor,
            Description = refund > 0
                ? $"Equipo caído revocado: no cuenta y se devuelven {refund} puntos."
                : "Equipo caído revocado: no cuenta.",
            PointsDelta = refund,
            Reason = why,
            Data = new Dictionary<string, string>
            {
                ["equipoCaido"] = wipeId.ToString(),
                ["devuelto"] = refund.ToString()
            }
        }, ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    /// Charges for a Pokémon that fell. Called from wherever the death itself is recorded, so a
    /// death cannot exist without its cost.
    /// </summary>
    public async Task<PenaltyResult> ChargeDeathAsync(Guid runId, string actor, PokemonEntry entry,
        CancellationToken ct = default)
    {
        var scaled = RoleAdjusted.Penalty(roles.Of(runId), Math.Max(0, catalog.Rules.PerDeath));
        var cost = scaled.Final;

        if (cost == 0)
        {
            return new PenaltyResult(false, 0, await BalanceAsync(runId, ct).ConfigureAwait(false));
        }

        var name = entry.Nickname ?? entry.SpeciesName;

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.PointsPenalty,
            Source = EventSource.AutoDetect,
            Actor = actor,
            Description = $"−{cost} puntos por la muerte de {name}.{scaled.Explain()}",
            PointsDelta = -cost,
            PokemonId = entry.Id,
            Reason = "muerte",
            Data = new Dictionary<string, string>
            {
                ["motivo"] = "muerte",
                ["especie"] = entry.Species.ToString(),
                ["pokemon"] = name,
                ["base"] = scaled.Base.ToString(),
                ["rol"] = scaled.RoleId,
                ["multiplicador"] = scaled.Multiplier.ToString("0.##")
            }
        }, ct).ConfigureAwait(false);

        return new PenaltyResult(true, cost, await BalanceAsync(runId, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Charges for the whole party going down at once.
    /// </summary>
    /// <remarks>
    /// Past <see cref="PenaltyRules.MaxWipes"/> the event is still written, with a delta of zero.
    /// It happened, so it belongs in the history; it simply stops costing. Recording nothing would
    /// make the log claim the party never fell.
    /// </remarks>
    public async Task<PenaltyResult> ChargeWipeAsync(Guid runId, string actor,
        IReadOnlyList<string> fallen, CancellationToken ct = default)
    {
        var already = await CountWipesAsync(runId, ct).ConfigureAwait(false);
        var capped = already >= catalog.Rules.MaxWipes;
        var scaled = RoleAdjusted.Penalty(roles.Of(runId), capped ? 0 : Math.Max(0, catalog.Rules.PerWipe));
        var cost = scaled.Final;

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.TeamWiped,
            Source = EventSource.AutoDetect,
            Actor = actor,
            Description = capped
                ? "Equipo caído. Sin penalización."
                : $"−{cost} puntos: equipo caído ({already + 1} de {catalog.Rules.MaxWipes}).",
            PointsDelta = -cost,
            Reason = "equipo caído",
            Data = new Dictionary<string, string>
            {
                ["motivo"] = "equipo",
                ["numero"] = (already + 1).ToString(),
                ["maximo"] = catalog.Rules.MaxWipes.ToString(),
                ["contados"] = fallen.Count.ToString(),
                ["equipo"] = string.Join(", ", fallen),
                ["base"] = scaled.Base.ToString(),
                ["rol"] = scaled.RoleId,
                ["multiplicador"] = scaled.Multiplier.ToString("0.##")
            }
        }, ct).ConfigureAwait(false);

        return new PenaltyResult(true, cost, await BalanceAsync(runId, ct).ConfigureAwait(false), capped);
    }

    private async Task<int> BalanceAsync(Guid runId, CancellationToken ct)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Sum(e => e.PointsDelta);
    }
}
