using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Counts what the run has done and hands out the points for it.
/// </summary>
/// <remarks>
/// <para>
/// Progress is a projection over the event log, exactly like the balance: nothing stores a
/// counter that could drift from the history. Recomputing it from scratch always gives the same
/// answer, and that is what makes a claim checkable.
/// </para>
/// <para>
/// Unlocking and claiming are separate on purpose. The run shows <c>3 / 5</c> as it goes, and the
/// player collects with a button, so the log says where every point came from and when.
/// </para>
/// </remarks>
public sealed class AchievementService(IAchievementCatalog catalog, IPointsService points,
    IEventStore events, IClock clock, IGameRecords records, IRunRoles roles, IItemDelivery? bag = null)
{
    public IReadOnlyList<Achievement> All => catalog.All;

    /// <summary>The last reading of the cartridge's own counters, for the screen to explain itself.</summary>
    public GameRecordSnapshot? LastRecords { get; private set; }

    /// <summary>Where every achievement of the run stands right now.</summary>
    public async Task<IReadOnlyList<AchievementProgress>> GetProgressAsync(Guid runId,
        CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        // Los contadores del propio juego. Si no se pueden leer, esos logros se quedan a cero y
        // la pantalla dice por qué, en vez de fingir un progreso.
        var game = await records.ReadAsync(ct).ConfigureAwait(false);
        LastRecords = game;
        var held = await HeldAsync(game, ct).ConfigureAwait(false);

        var counts = all
            .GroupBy(e => e.Type)
            .ToDictionary(group => group.Key, group => group.Count());

        var claimed = all
            .Where(e => e.Type == GameEventType.AchievementUnlocked && e.Data.ContainsKey("logro"))
            .Select(e => e.Data["logro"])
            .ToHashSet(StringComparer.Ordinal);

        // Las marcas a mano se suman por logro. Cada una es su propio evento con su delta, así
        // que el progreso se reconstruye igual que el saldo: sumando el historial.
        var marked = all
            .Where(e => e.Type == GameEventType.AchievementProgressed && e.Data.ContainsKey("logro"))
            .GroupBy(e => e.Data["logro"], StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(e => int.TryParse(e.Data.GetValueOrDefault("delta"), out var d) ? d : 0),
                StringComparer.Ordinal);

        return
        [
            .. catalog.All.Select(achievement => new AchievementProgress(
                achievement,
                CountFor(achievement, counts, marked, game, held),
                claimed.Contains(achievement.Id)))
        ];
    }

    /// <summary>
    /// The prize items the player has: the save file's, plus what the live bag says right now.
    /// </summary>
    /// <remarks>
    /// Only the save counted before, so a trial passed and not yet saved stayed locked here while the zone rule, which
    /// reads the live bag (§179), already treated it as passed. These items are given and never taken, so a union
    /// cannot overcount. An empty answer from the bag means «not known» (§68) and adds nothing.
    /// </remarks>
    private async Task<IReadOnlySet<int>> HeldAsync(GameRecordSnapshot game, CancellationToken ct)
    {
        var held = new HashSet<int>(game.Available && game.Items is { } saved ? saved : []);
        var wanted = catalog.All.Where(a => a.Item is not null).Select(a => a.Item!.Value).Distinct().ToList();

        if (bag is null || wanted.Count == 0)
        {
            return held;
        }

        try
        {
            var live = await bag.CarriedAllAsync(wanted, ct).ConfigureAwait(false);
            held.UnionWith(live.Where(pair => pair.Value > 0).Select(pair => pair.Key));
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // La mochila viva es un extra: si falla, queda lo del fichero de partida.
        }

        return held;
    }

    /// <summary>
    /// Where one achievement's number comes from: the cartridge first, then the run's own events,
    /// and only failing both, the player's own marks.
    /// </summary>
    /// <remarks>
    /// The cartridge wins because it has been counting since before PermaLocke existed. Anything
    /// PermaLocke worked out on its own could only be a second opinion about the same fact.
    /// </remarks>
    private static int CountFor(Achievement achievement, IReadOnlyDictionary<GameEventType, int> counts,
        IReadOnlyDictionary<string, int> marked, GameRecordSnapshot game, IReadOnlySet<int> held)
    {
        if (achievement.Record is { } record)
        {
            return game.Available ? game.Get(record) : 0;
        }

        // Un objeto que el juego entrega y nunca retira: está o no está, así que cuenta uno o cero.
        if (achievement.Item is { } item)
        {
            return held.Contains(item) ? 1 : 0;
        }

        // Un contador del propio juego que no sale en la ficha de entrenador, como las Dominsignias.
        if (achievement.Work is { } work)
        {
            return game.Available ? game.Work(work) : 0;
        }

        return achievement.Trigger is { } trigger
            ? counts.GetValueOrDefault(trigger)
            : marked.GetValueOrDefault(achievement.Id);
    }

    /// <summary>
    /// Moves a manual counter, by one step or all the way to the target.
    /// </summary>
    /// <remarks>
    /// Refused on an automatic achievement: letting a hand-typed number sit on top of a counter
    /// PermaLocke works out itself would make the two disagree with no way to tell which is right.
    /// </remarks>
    public async Task<PointsResult> MarkAsync(Run run, string achievementId, bool complete,
        CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(run.Id, ct).ConfigureAwait(false);
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (progress.FirstOrDefault(p => p.Achievement.Id == achievementId) is not { } found)
        {
            return new PointsResult(false, balance, $"No existe el logro «{achievementId}».");
        }

        if (found.Achievement.IsAutomatic)
        {
            return new PointsResult(false, balance,
                $"«{found.Achievement.Name}» se cuenta solo.");
        }

        if (found.Unlocked)
        {
            return new PointsResult(false, balance, $"«{found.Achievement.Name}» ya está al completo.");
        }

        var delta = complete ? found.Achievement.Target - found.Count : 1;

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AchievementProgressed,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = complete
                ? $"«{found.Achievement.Name}» marcado como completo por el jugador."
                : $"«{found.Achievement.Name}»: {found.Count + 1} de {found.Achievement.Target}.",
            Data = new Dictionary<string, string>
            {
                ["logro"] = found.Achievement.Id,
                ["nombre"] = found.Achievement.Name,
                ["delta"] = delta.ToString(),
                ["anterior"] = found.Count.ToString(),
                ["origen"] = "marca manual"
            }
        }, ct).ConfigureAwait(false);

        return new PointsResult(true, balance);
    }

    /// <summary>
    /// Collects one achievement: the points are earned and the claim is written down.
    /// </summary>
    /// <remarks>
    /// Refused when it is not unlocked or was already collected, and the refusal writes nothing:
    /// a claim that did not happen must leave no trace.
    /// </remarks>
    public async Task<PointsResult> ClaimAsync(Run run, string achievementId,
        CancellationToken ct = default)
    {
        var progress = await GetProgressAsync(run.Id, ct).ConfigureAwait(false);
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (progress.FirstOrDefault(p => p.Achievement.Id == achievementId) is not { } found)
        {
            return new PointsResult(false, balance, $"No existe el logro «{achievementId}».");
        }

        if (found.Claimed)
        {
            return new PointsResult(false, balance, $"«{found.Achievement.Name}» ya estaba cobrado.");
        }

        if (!found.Unlocked)
        {
            return new PointsResult(false, balance,
                $"«{found.Achievement.Name}» va por {found.Count} de {found.Achievement.Target}.");
        }

        // Lo que paga el logro depende del rol: la mitad para el cagoneta, vez y media para el
        // experto. Se guardan los tres números —base, multiplicador y resultado— para que el
        // historial enseñe la cuenta en vez de un total que hay que creerse.
        var reward = RoleAdjusted.Reward(roles.Of(run.Id), found.Achievement.Points);

        // El evento de cobro primero: si algo fallara después, el logro queda cobrado y sin
        // puntos, que se ve y se arregla. Al revés quedarían puntos sin explicación.
        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AchievementUnlocked,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Logro «{found.Achievement.Name}»: +{reward.Final} puntos.{reward.Explain()}",
            Data = new Dictionary<string, string>
            {
                ["logro"] = found.Achievement.Id,
                ["nombre"] = found.Achievement.Name,
                ["puntos"] = reward.Final.ToString(),
                ["base"] = reward.Base.ToString(),
                ["rol"] = reward.RoleId,
                ["multiplicador"] = reward.Multiplier.ToString("0.##"),
                ["contador"] = found.Count.ToString()
            }
        }, ct).ConfigureAwait(false);

        if (reward.Final <= 0)
        {
            return new PointsResult(true, balance);
        }

        return await points.EarnAsync(run.Id, reward.Final,
            $"Logro «{found.Achievement.Name}».", EventSource.Player, run.PlayerName, ct)
            .ConfigureAwait(false);
    }
}
