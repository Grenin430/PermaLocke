using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// Says out loud that the run's history has just grown.
/// </summary>
/// <remarks>
/// <para>
/// It exists because of a bug that had been there since the points counter was put in the header:
/// buying something, pulling the gacha or claiming an achievement changed the balance in the
/// database and <b>the number on screen stayed as it was until you changed section</b>. Every
/// screen refreshed itself and nobody told the shell.
/// </para>
/// <para>
/// Hung off the event chain and not off the points service on purpose. Everything that moves a
/// point writes an event — that is the rule the whole application is built on — so this catches
/// purchases, penalties, deaths, achievements and whatever gets added later, without each screen
/// having to remember to announce itself. A screen that forgets to announce is exactly the bug
/// this replaces.
/// </para>
/// </remarks>
public sealed class RunActivity
{
    /// <summary>Raised after an event is safely stored, on whatever thread stored it.</summary>
    public event EventHandler<GameEvent>? Appended;

    internal void Announce(GameEvent stored) => Appended?.Invoke(this, stored);
}

/// <summary>
/// The real event store with <see cref="RunActivity"/> bolted on.
/// </summary>
/// <remarks>
/// A wrapper and not a change to <see cref="IEventStore"/>: the interface has ten implementations,
/// nine of them fakes in the tests, and none of them has anything to announce. Announcing is a
/// concern of the application that draws the numbers, so it lives here.
/// </remarks>
public sealed class WatchedEventStore(IEventStore inner, RunActivity activity) : IEventStore, IDisposable
{
    public async Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        // Se anuncia DESPUES de guardar, y solo si guardar salio bien: un numero en pantalla que
        // se adelanta a la base de datos es peor que uno que llega tarde.
        var stored = await inner.AppendAsync(gameEvent, ct).ConfigureAwait(false);

        activity.Announce(stored);

        return stored;
    }

    public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
        inner.GetAllAsync(runId, ct);

    public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
        inner.GetLatestAsync(runId, count, ct);

    public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
        inner.VerifyChainAsync(runId, ct);

    public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
        inner.DeleteRunAsync(runId, ct);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
