using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// The balance is a projection over the event log, never a stored number. There is no way
/// to change points without leaving an event behind, which is the whole point of the design.
/// </summary>
public sealed class PointsService(IEventStore events, IClock clock) : IPointsService
{
    public async Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Sum(e => e.PointsDelta);
    }

    public async Task<PointsResult> EarnAsync(Guid runId, int amount, string description,
        EventSource source, string actor, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return new PointsResult(false, await GetBalanceAsync(runId, ct).ConfigureAwait(false),
                "La cantidad a ganar debe ser mayor que cero.");
        }

        await AppendAsync(runId, GameEventType.PointsEarned, source, actor, description, amount, null, ct)
            .ConfigureAwait(false);

        return new PointsResult(true, await GetBalanceAsync(runId, ct).ConfigureAwait(false));
    }

    public async Task<PointsResult> SpendAsync(Guid runId, int amount, string description,
        EventSource source, string actor, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(runId, ct).ConfigureAwait(false);

        if (amount <= 0)
        {
            return new PointsResult(false, balance, "La cantidad a gastar debe ser mayor que cero.");
        }

        if (balance < amount)
        {
            // Deliberately no event: a rejected purchase did not happen.
            return new PointsResult(false, balance,
                $"Puntos insuficientes: tienes {balance} y hacen falta {amount}.");
        }

        await AppendAsync(runId, GameEventType.PointsSpent, source, actor, description, -amount, null, ct)
            .ConfigureAwait(false);

        return new PointsResult(true, balance - amount);
    }

    public async Task<PointsResult> AdjustAsync(Guid runId, int delta, string reason, string adminName,
        CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(runId, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new PointsResult(false, balance, "Un ajuste administrativo exige un motivo.");
        }

        if (delta == 0)
        {
            return new PointsResult(false, balance, "El ajuste no puede ser de cero puntos.");
        }

        // An admin may push the balance negative; it is their call and it stays on the record.
        await AppendAsync(runId, GameEventType.AdminAdjustment, EventSource.Admin, adminName,
            $"Ajuste administrativo de {delta:+#;-#;0} puntos.", delta, reason, ct).ConfigureAwait(false);

        return new PointsResult(true, balance + delta);
    }

    private Task AppendAsync(Guid runId, GameEventType type, EventSource source, string actor,
        string description, int delta, string? reason, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = type,
            Source = source,
            Actor = actor,
            Description = description,
            PointsDelta = delta,
            Reason = reason
        }, ct);
}
