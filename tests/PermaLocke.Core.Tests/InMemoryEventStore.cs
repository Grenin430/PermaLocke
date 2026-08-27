using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Test double that keeps the same append-only, hash-chained contract as the SQLite store,
/// so the services under test behave exactly as they do in production.
/// </summary>
internal sealed class InMemoryEventStore : IEventStore
{
    private readonly List<GameEvent> _events = [];

    public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        var previousHash = _events.LastOrDefault(e => e.RunId == gameEvent.RunId)?.Hash ?? string.Empty;
        var sealedEvent = EventHasher.Seal(gameEvent, previousHash);
        _events.Add(sealedEvent);
        return Task.FromResult(sealedEvent);
    }

    public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameEvent>>([.. _events.Where(e => e.RunId == runId)]);

    public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameEvent>>(
            [.. _events.Where(e => e.RunId == runId).TakeLast(count).Reverse()]);

    public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default)
    {
        var gone = _events.RemoveAll(e => e.RunId == runId);
        return Task.FromResult(gone);
    }

    public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default)
    {
        var expectedPrevious = string.Empty;
        var count = 0;

        foreach (var e in _events.Where(x => x.RunId == runId))
        {
            count++;

            if (e.PreviousHash != expectedPrevious || EventHasher.Compute(e, expectedPrevious) != e.Hash)
            {
                return Task.FromResult(new IntegrityReport(false, count, e.Id, "Cadena rota."));
            }

            expectedPrevious = e.Hash;
        }

        return Task.FromResult(new IntegrityReport(true, count, null, null));
    }

    /// <summary>Simulates someone editing the database by hand, for the tampering tests.</summary>
    public void TamperWith(int index, Func<GameEvent, GameEvent> mutate) =>
        _events[index] = mutate(_events[index]);
}
