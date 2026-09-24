using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Abstractions;

/// <summary>Time source. Injected so tests are deterministic.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

/// <summary>
/// Append-only event log. There is deliberately no Update and no Delete: a mistake is
/// corrected by appending a compensating event, never by rewriting history.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Seals the event against the tail of the run's chain and persists it.
    /// Returns the stored event, with <see cref="GameEvent.PreviousHash"/> and
    /// <see cref="GameEvent.Hash"/> filled in.
    /// </summary>
    Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default);

    Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default);

    Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default);

    /// <summary>Walks the hash chain and reports the first inconsistency found, if any.</summary>
    Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Throws away every event of one run. Returns how many went.
    /// </summary>
    /// <remarks>
    /// Not a hole in the rule above, and the difference is the whole point: there is no way to
    /// delete <em>an</em> event, which is what would let somebody drop the death they did not like
    /// and leave a chain that still verifies. This takes the run whole -- events, Pokémon, the lot
    /// -- so nothing survives to be falsified. A discarded run is not a corrected one.
    /// </remarks>
    Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default);
}

/// <param name="IsValid">False when the chain was broken, i.e. the log was tampered with.</param>
/// <param name="CheckedEvents">How many events were walked.</param>
/// <param name="FirstBrokenEventId">The first event whose hash does not match, when there is one.</param>
public sealed record IntegrityReport(bool IsValid, int CheckedEvents, Guid? FirstBrokenEventId, string? Message);

public interface IPokemonRepository
{
    Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default);

    Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default);

    /// <summary>Inserts or replaces. Callers must also append the matching event.</summary>
    Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default);

    /// <summary>Throws away every Pokémon of one run. Returns how many went.</summary>
    Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default);
}

public interface IRunRepository
{
    Task<Run?> GetAsync(Guid runId, CancellationToken ct = default);

    Task<IReadOnlyList<Run>> GetAllAsync(CancellationToken ct = default);

    Task SaveAsync(Run run, CancellationToken ct = default);

    /// <summary>Removes the run and its folder. False when there was nothing to remove.</summary>
    Task<bool> DeleteAsync(Guid runId, CancellationToken ct = default);
}

/// <summary>This machine's player. One file per installation, because each player has their own PC.</summary>
public interface IPlayerProfileStore
{
    /// <summary>Null when nobody has been set up on this machine yet.</summary>
    Task<PlayerProfile?> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(PlayerProfile profile, CancellationToken ct = default);
}

/// <summary>
/// The only way points may change. Every operation writes an event; there is no setter
/// for the balance anywhere in the codebase.
/// </summary>
public interface IPointsService
{
    Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default);

    Task<PointsResult> EarnAsync(Guid runId, int amount, string description, EventSource source,
        string actor, CancellationToken ct = default);

    /// <summary>Fails without side effects when the balance is insufficient.</summary>
    Task<PointsResult> SpendAsync(Guid runId, int amount, string description, EventSource source,
        string actor, CancellationToken ct = default);

    /// <summary>Administrative correction. <paramref name="reason"/> is mandatory and stored.</summary>
    Task<PointsResult> AdjustAsync(Guid runId, int delta, string reason, string adminName,
        CancellationToken ct = default);
}

public sealed record PointsResult(bool Success, int NewBalance, string? FailureReason = null);

/// <summary>The run the UI is currently looking at. Null when none has been created yet.</summary>
public interface IRunContext
{
    Run? Current { get; }

    event EventHandler? CurrentChanged;

    void SetCurrent(Run? run);
}
