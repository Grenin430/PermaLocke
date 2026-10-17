using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;

namespace PermaLocke.App.Services;

/// <summary>
/// The «points» of the singleplayer app are the money of the game (2026-10-12): every service that charged points (gacha, shop,
/// roulette, nursery…) now charges Pokédollars, through the one port they all use. The balance is the game's money, read
/// each time; spending takes it from the save or from the game (<see cref="GameMoney"/>) and leaves an event so the run still
/// says what was bought. Nothing is earned here: money is earned playing.
/// </summary>
public sealed class GameMoneyPoints(GameMoney money, IEventStore events, IClock clock) : IPointsService, IMoneyPort
{
    /// <summary>
    /// The money changed while nobody asked (2026-10-16): bought or sold in the game. It is looked at every two seconds, and the sections that
    /// show it (home, shop, gacha) take the new figure without waiting to be opened again. Raised on a background thread.
    /// </summary>
    public static event Action<int>? BalanceSeen;

    private int? _seen;
    private Timer? _watch;

    /// <summary>Starts looking at the money; the first look only takes note.</summary>
    public void Watch() => _watch ??= new Timer(_ =>
    {
        try
        {
            if (money.Read() is not { } now) return;
            var before = _seen;
            _seen = now;
            if (before is not null && before != now) BalanceSeen?.Invoke(now);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A look that fails is a look that did not happen.
        }
    }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

    public Task<MoneyChange> ChangeAsync(int delta, CancellationToken ct = default) => Task.Run(() =>
    {
        var (result, applied) = money.Change(delta);
        return new MoneyChange(result.Success, applied, result.Balance, result.Problem);
    }, ct);

    public Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default) => Task.Run(() => money.Read() ?? 0, ct);

    /// <summary>Money is not handed out by the app: what used to award points awards nothing.</summary>
    public async Task<PointsResult> EarnAsync(Guid runId, int amount, string description, EventSource source, string actor,
        CancellationToken ct = default) =>
        new(false, await GetBalanceAsync(runId, ct).ConfigureAwait(false), "En la versión de un jugador el dinero se gana jugando.");

    public async Task<PointsResult> SpendAsync(Guid runId, int amount, string description, EventSource source, string actor,
        CancellationToken ct = default)
    {
        var paid = await Task.Run(() => money.Spend(amount), ct).ConfigureAwait(false);
        if (!paid.Success) return new PointsResult(false, paid.Balance, paid.Problem);

        // Un rastro de lo pagado, sin tocar el saldo de puntos de siempre (PointsDelta 0): el saldo es el dinero del juego.
        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.PointsSpent,
            Source = source,
            Actor = actor,
            Description = $"{description} Pagado con {amount} ₽ del juego.",
            PointsDelta = 0,
            Data = new Dictionary<string, string> { ["dinero"] = amount.ToString(), ["quedan"] = paid.Balance.ToString() }
        }, ct).ConfigureAwait(false);

        return new PointsResult(true, paid.Balance);
    }

    public async Task<PointsResult> AdjustAsync(Guid runId, int delta, string reason, string adminName, CancellationToken ct = default) =>
        new(false, await GetBalanceAsync(runId, ct).ConfigureAwait(false), "No hay organizador en la versión de un jugador.");
}
