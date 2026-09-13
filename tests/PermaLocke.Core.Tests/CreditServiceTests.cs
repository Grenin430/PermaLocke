using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The free rolls and wonder trades the trials hand over.
/// </summary>
/// <remarks>
/// Nothing here is stored, so what these guard is the arithmetic that stands in for storage:
/// earned from the achievements, spent from the events marked free, and the difference never
/// below zero. The one that matters most is that an <b>unmarked</b> event is not counted — that is
/// what keeps the thirty wonder trades made before any of this existed from being charged.
/// </remarks>
public sealed class CreditServiceTests
{
    private sealed class Catalog(bool limit = true) : ICreditCatalog
    {
        public IReadOnlyList<MilestoneGrant> Milestones { get; init; } =
        [
            new("prueba-01", "1ª", new Dictionary<string, int> { ["pocho"] = 2 }, 1),
            new("prueba-02", "2ª", new Dictionary<string, int> { ["pocho"] = 2 }, 1),
            new("prueba-08", "8ª", new Dictionary<string, int> { ["bueno"] = 1, ["decente"] = 1 }, 1),
            new("alto-mando-campeon", "Liga", new Dictionary<string, int> { ["bueno"] = 3 }, 4)
        ];

        public bool LimitWonderTrades => limit;
    }

    private sealed class Events : IEventStore
    {
        public List<GameEvent> Appended { get; } = [];

        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
        {
            Appended.Add(gameEvent);
            return Task.FromResult(gameEvent);
        }

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Achievements(IReadOnlyList<Achievement> all) : IAchievementCatalog
    {
        public IReadOnlyList<Achievement> All => all;
    }

    private sealed class Records(params int[] itemsHeld) : IGameRecords
    {
        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(new GameRecordSnapshot(true, null, null,
                new Dictionary<int, int>(), DateTimeOffset.UnixEpoch,
                itemsHeld.ToHashSet(), new Dictionary<int, int>()));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private static Achievement[] Milestones() =>
    [
        new("prueba-01", "1ª", "", null, "sin disparador", 1, 100, Item: 807),
        new("prueba-02", "2ª", "", null, "sin disparador", 1, 100, Item: 813),
        new("prueba-08", "8ª", "", null, "sin disparador", 1, 100, Item: 820),
        new("alto-mando-campeon", "Liga", "", null, "sin disparador", 1, 300, Item: 900)
    ];

    private static Run TheRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260827",
        Seed = 20260827,
        RoleId = "ludopata",
        PlayerName = "Grenin"
    };

    private static (CreditService Service, Events Log) Build(
        Records? records = null, Catalog? catalog = null)
    {
        var log = new Events();
        var clock = new FixedClock();

        var achievements = new AchievementService(
            new Achievements(Milestones()), new PointsService(log, clock), log, clock,
            records ?? new Records(), new FixedRole(FixedRole.Normal));

        return (new CreditService(catalog ?? new Catalog(), achievements, log), log);
    }

    private static GameEvent Roll(Guid runId, string banner, bool free) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = GameEventType.GachaRoll,
        Source = EventSource.Player,
        Actor = "Grenin",
        Description = "tirada",
        Data = new Dictionary<string, string> { ["banner"] = banner, ["gratis"] = free.ToString() }
    };

    private static GameEvent Trade(Guid runId, bool free) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = GameEventType.WonderTrade,
        Source = EventSource.Player,
        Actor = "Grenin",
        Description = "intercambio",
        Data = free
            ? new Dictionary<string, string> { ["gratis"] = "True" }
            : new Dictionary<string, string>()
    };

    private static GameEvent Spin(Guid runId, string face, string credit = "") => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = GameEventType.RouletteSpun,
        Source = EventSource.System,
        Actor = "Grenin",
        Description = "ruleta",
        Data = new Dictionary<string, string> { ["cara"] = face, ["credito"] = credit }
    };

    [Fact]
    public async Task Nothing_cleared_gives_nothing()
    {
        var (service, _) = Build(new Records());

        var earned = await service.EarnedAsync(TheRun());

        Assert.Equal(0, earned.TotalRolls);
        Assert.Equal(0, earned.WonderTrades);
        Assert.False(earned.Any);
    }

    [Fact]
    public async Task Each_trial_pays_what_the_table_says()
    {
        var (service, _) = Build(new Records(807, 813));

        var earned = await service.EarnedAsync(TheRun());

        Assert.Equal(4, earned.RollsOn("pocho"));
        Assert.Equal(0, earned.RollsOn("decente"));
        Assert.Equal(2, earned.WonderTrades);
    }

    /// <summary>
    /// The table of what each milestone pays needs to know <b>which</b> have been reached.
    /// </summary>
    /// <remarks>
    /// <see cref="CreditService.EarnedAsync"/> adds them up and loses that on the way, which is
    /// right for a total and useless for a list. Same source, so the screen listing the trials and
    /// the counter paying for them cannot end up disagreeing about what counts as reached.
    /// </remarks>
    [Fact]
    public async Task It_says_which_milestones_have_been_reached()
    {
        var (service, _) = Build(new Records(807, 813));

        var reached = await service.ReachedAsync(TheRun().Id);

        Assert.Contains("prueba-01", reached);
        Assert.Contains("prueba-02", reached);
        Assert.DoesNotContain("prueba-03", reached);

        // Y cuadra con lo que se cobró: dos pruebas a dos tiradas de pocho son las cuatro de
        // arriba. Si una de las dos cuentas se moviera sola, esto lo diría.
        var earned = await service.EarnedAsync(TheRun());
        Assert.Equal(2, reached.Count(id => id.StartsWith("prueba-", StringComparison.Ordinal)));
        Assert.Equal(4, earned.RollsOn("pocho"));
    }

    /// <summary>The eighth pays on two banners at once, and the league pays four trades.</summary>
    [Fact]
    public async Task Milestones_add_up_across_banners()
    {
        var (service, _) = Build(new Records(807, 813, 820, 900));

        var earned = await service.EarnedAsync(TheRun());

        Assert.Equal(4, earned.RollsOn("pocho"));
        Assert.Equal(1, earned.RollsOn("decente"));
        Assert.Equal(4, earned.RollsOn("bueno"));
        Assert.Equal(7, earned.WonderTrades);
    }

    /// <summary>The wheel's gacha faces pay too, read back from the spin they were won on.</summary>
    [Fact]
    public async Task The_wheel_adds_its_own_rolls()
    {
        var (service, log) = Build(new Records(807));
        var run = TheRun();

        await log.AppendAsync(Spin(run.Id, "gacha-1", "decente"));
        await log.AppendAsync(Spin(run.Id, "gacha-3", "pocho,decente,bueno"));
        await log.AppendAsync(Spin(run.Id, "puntos-mas"));

        var earned = await service.EarnedAsync(run);

        Assert.Equal(3, earned.RollsOn("pocho"));    // 2 de la prueba + 1 de la cara de tres
        Assert.Equal(2, earned.RollsOn("decente"));  // una de cada cara de gacha
        Assert.Equal(1, earned.RollsOn("bueno"));
    }

    /// <summary>
    /// A roll that was paid for with points is not a spent credit.
    /// </summary>
    /// <remarks>
    /// This is the one that keeps the run's history honest: without it every roll ever made would
    /// eat a credit, and a run with a hundred and fifty rolls would owe credits it never had.
    /// </remarks>
    [Fact]
    public async Task Only_the_rolls_marked_free_are_counted_as_spent()
    {
        var (service, log) = Build(new Records(807, 813));
        var run = TheRun();

        await log.AppendAsync(Roll(run.Id, "pocho", free: true));
        await log.AppendAsync(Roll(run.Id, "pocho", free: false));
        await log.AppendAsync(Roll(run.Id, "pocho", free: false));

        Assert.Equal(1, (await service.SpentAsync(run.Id)).RollsOn("pocho"));
        Assert.Equal(3, (await service.AvailableAsync(run)).RollsOn("pocho"));
    }

    /// <summary>The same, for the thirty wonder trades this run made before the rule existed.</summary>
    [Fact]
    public async Task Trades_made_before_the_rule_do_not_count()
    {
        var (service, log) = Build(new Records(807, 813));
        var run = TheRun();

        for (var i = 0; i < 30; i++)
        {
            await log.AppendAsync(Trade(run.Id, free: false));
        }

        Assert.Equal(0, (await service.SpentAsync(run.Id)).WonderTrades);
        Assert.Equal(2, (await service.AvailableAsync(run)).WonderTrades);
    }

    [Fact]
    public async Task Spending_takes_them_away()
    {
        var (service, log) = Build(new Records(807, 813));
        var run = TheRun();

        await log.AppendAsync(Trade(run.Id, free: true));
        await log.AppendAsync(Roll(run.Id, "pocho", free: true));
        await log.AppendAsync(Roll(run.Id, "pocho", free: true));

        var left = await service.AvailableAsync(run);

        Assert.Equal(2, left.RollsOn("pocho"));
        Assert.Equal(1, left.WonderTrades);
    }

    /// <summary>Spending more than was earned floors at zero: a credit cannot go negative.</summary>
    [Fact]
    public async Task It_never_goes_below_zero()
    {
        var (service, log) = Build(new Records(807));
        var run = TheRun();

        for (var i = 0; i < 9; i++)
        {
            await log.AppendAsync(Roll(run.Id, "pocho", free: true));
            await log.AppendAsync(Trade(run.Id, free: true));
        }

        var left = await service.AvailableAsync(run);

        Assert.Equal(0, left.RollsOn("pocho"));
        Assert.Equal(0, left.WonderTrades);
    }

    [Fact]
    public async Task A_banner_nobody_earned_anything_on_has_nothing()
    {
        var (service, _) = Build(new Records(807));

        Assert.Equal(0, (await service.AvailableAsync(TheRun())).RollsOn("no-existe"));
    }

    /// <summary>
    /// Without the file nothing is limited, so a missing config cannot lock a feature that worked.
    /// </summary>
    /// <summary>
    /// Credit is keyed on the <c>credito</c> field, not on the kind of event that carries it.
    /// </summary>
    /// <remarks>
    /// So a prize pays in exactly like a wheel spin, and so does anything granted by hand, without
    /// this service having to be taught about each new source. The test uses a prize and an admin
    /// adjustment precisely because neither is a spin.
    /// </remarks>
    [Fact]
    public async Task Any_event_that_recorded_a_credit_pays_in()
    {
        var (service, log) = Build(new Records());
        var run = TheRun();

        await log.AppendAsync(Granted(run.Id, GameEventType.RewardClaimed, "decente"));
        await log.AppendAsync(Granted(run.Id, GameEventType.AdminAdjustment, "decente"));
        await log.AppendAsync(Granted(run.Id, GameEventType.RewardClaimed, string.Empty));

        var earned = await service.EarnedAsync(run);

        Assert.Equal(2, earned.Rolls["decente"]);
    }

    private static GameEvent Granted(Guid runId, GameEventType type, string credit) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = type,
        Source = EventSource.System,
        Actor = "Grenin",
        Description = "concedido",
        Data = new Dictionary<string, string> { ["credito"] = credit }
    };

    [Fact]
    public void An_empty_catalogue_limits_nothing()
    {
        var (service, _) = Build(catalog: new Catalog(limit: false) { Milestones = [] });

        Assert.False(service.LimitsWonderTrades);
        Assert.Empty(service.Milestones);
    }
}
