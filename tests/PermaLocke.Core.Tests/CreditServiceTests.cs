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

    private sealed class Wheel : IRouletteCatalog
    {
        public IReadOnlyList<RouletteFace> Faces { get; init; } =
        [
            new("gacha-1", "1 tirada", "", true, RouletteEffect.Gacha, Banners: ["decente"]),
            new("gacha-3", "3 tiradas", "", true, RouletteEffect.Gacha,
                Banners: ["pocho", "decente", "bueno"]),
            new("puntos-mas", "+200", "", true, RouletteEffect.Puntos, 200)
        ];

        public IReadOnlyList<int> GoodAbilities => [];

        public IReadOnlyList<int> BadAbilities => [];

        public IReadOnlyList<int> HealingItems => [];

        public int SpinsPerTrial => 1;

        public int SpinsForLeague => 3;

        public int SpinsForRematch => 2;

        public IReadOnlyList<string> TrialAchievements => ["prueba-01", "prueba-02", "prueba-08"];

        public string LeagueAchievement => "alto-mando-campeon";

        public string RematchAchievement => "alto-mando-otra-vez";
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
        Records? records = null, Catalog? catalog = null, Wheel? wheel = null)
    {
        var log = new Events();
        var clock = new FixedClock();

        var achievements = new AchievementService(
            new Achievements(Milestones()), new PointsService(log, clock), log, clock,
            records ?? new Records(), new FixedRole(FixedRole.Normal));

        return (new CreditService(catalog ?? new Catalog(), wheel ?? new Wheel(), achievements, log), log);
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

    private static GameEvent Spin(Guid runId, string face) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = GameEventType.RouletteSpun,
        Source = EventSource.System,
        Actor = "Grenin",
        Description = "ruleta",
        Data = new Dictionary<string, string> { ["cara"] = face }
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

        await log.AppendAsync(Spin(run.Id, "gacha-1"));
        await log.AppendAsync(Spin(run.Id, "gacha-3"));
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
    [Fact]
    public void An_empty_catalogue_limits_nothing()
    {
        var (service, _) = Build(catalog: new Catalog(limit: false) { Milestones = [] });

        Assert.False(service.LimitsWonderTrades);
        Assert.Empty(service.Milestones);
    }
}
