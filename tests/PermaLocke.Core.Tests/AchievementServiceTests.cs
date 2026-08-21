using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Guards the achievements: progress recomputed from the log, manual marks that are marks and not
/// magic, and a claim that pays exactly once.
/// </summary>
public sealed class AchievementServiceTests
{
    private sealed class Catalog(IReadOnlyList<Achievement> all) : IAchievementCatalog
    {
        public IReadOnlyList<Achievement> All => all;
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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Stands in for the cartridge's own counters.</summary>
    private sealed class Records(bool available, params (int Id, int Value)[] values) : IGameRecords
    {
        /// <summary>What the bag holds, for the achievements anchored to a reward instead of a counter.</summary>
        public HashSet<int> Items { get; init; } = [];

        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(available
                ? new GameRecordSnapshot(true, null, null,
                    values.ToDictionary(v => v.Id, v => v.Value), DateTimeOffset.UnixEpoch, Items)
                : GameRecordSnapshot.Unavailable("sin partida", DateTimeOffset.UnixEpoch));
    }

    /// <summary>The three kinds the screen shows: from the game, from the run, and by hand.</summary>
    private static Achievement[] Catalogue() =>
    [
        new("gacha-3", "Tirador", "Haz 3 tiradas.", GameEventType.GachaRoll, "GachaRoll", 3, 40),
        new("pegatinas-25", "Pegatinas", "Encuentra 25 pegatinas.", null, "sin disparador", 25, 75),
        new("huidas-200", "Pies ligeros", "Huye 200 veces.", null, "sin disparador", 200, 100, Record: 46),
        new("prueba-01", "Primera prueba", "Completa la primera prueba.", null, "sin disparador", 1, 100,
            Item: 807),
    ];

    private static (AchievementService Service, Events Log, Run Run) Build(IGameRecords? records = null)
    {
        var log = new Events();
        var clock = new FixedClock();
        var points = new PointsService(log, clock);
        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = "Prueba",
            Game = GameVersion.UltraMoon,
            SeedLabel = "20260821",
            Seed = 20260821,
            RoleId = "player",
            PlayerName = "Grenin"
        };

        return (new AchievementService(new Catalog(Catalogue()), points, log, clock,
            records ?? new Records(true, (46, 0))), log, run);
    }

    private static GameEvent Roll(Guid runId) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = new DateTimeOffset(2026, 8, 21, 13, 0, 0, TimeSpan.Zero),
        Type = GameEventType.GachaRoll,
        Source = EventSource.Player,
        Actor = "Grenin",
        Description = "tirada"
    };

    [Fact]
    public async Task Automatic_progress_is_counted_from_the_log()
    {
        var (service, log, run) = Build();

        await log.AppendAsync(Roll(run.Id));
        await log.AppendAsync(Roll(run.Id));

        var progress = await service.GetProgressAsync(run.Id);
        var gacha = progress.Single(p => p.Achievement.Id == "gacha-3");

        Assert.Equal(2, gacha.Count);
        Assert.False(gacha.Unlocked);
        Assert.Equal("2/3", gacha.Label);
    }

    /// <summary>Each mark is its own event, and the counter is their sum.</summary>
    [Fact]
    public async Task Marking_by_hand_moves_the_counter_one_step_at_a_time()
    {
        var (service, log, run) = Build();

        await service.MarkAsync(run, "pegatinas-25", complete: false);
        await service.MarkAsync(run, "pegatinas-25", complete: false);

        var progress = await service.GetProgressAsync(run.Id);
        var stickers = progress.Single(p => p.Achievement.Id == "pegatinas-25");

        Assert.Equal(2, stickers.Count);
        Assert.Equal(2, log.Appended.Count(e => e.Type == GameEventType.AchievementProgressed));

        // Firmado por el jugador, no por la detección: el historial dice cuál de las dos fue.
        Assert.All(log.Appended.Where(e => e.Type == GameEventType.AchievementProgressed),
            e => Assert.Equal(EventSource.Player, e.Source));
    }

    [Fact]
    public async Task Completing_by_hand_jumps_straight_to_the_target()
    {
        var (service, _, run) = Build();

        await service.MarkAsync(run, "pegatinas-25", complete: true);

        var stickers = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "pegatinas-25");

        Assert.Equal(25, stickers.Count);
        Assert.True(stickers.Unlocked);
        Assert.True(stickers.CanClaim);
        Assert.False(stickers.CanMark);
    }

    /// <summary>
    /// An automatic achievement cannot be nudged by hand: two numbers for the same thing would
    /// disagree with no way to tell which is right.
    /// </summary>
    [Fact]
    public async Task An_automatic_achievement_refuses_a_manual_mark()
    {
        var (service, log, run) = Build();

        var result = await service.MarkAsync(run, "gacha-3", complete: true);

        Assert.False(result.Success);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.AchievementProgressed);
    }

    [Fact]
    public async Task Claiming_pays_once_and_only_once()
    {
        var (service, log, run) = Build();

        await log.AppendAsync(Roll(run.Id));
        await log.AppendAsync(Roll(run.Id));
        await log.AppendAsync(Roll(run.Id));

        var first = await service.ClaimAsync(run, "gacha-3");
        var second = await service.ClaimAsync(run, "gacha-3");

        Assert.True(first.Success);
        Assert.Equal(40, first.NewBalance);
        Assert.False(second.Success);
        Assert.Equal(40, second.NewBalance);
        Assert.Single(log.Appended, e => e.Type == GameEventType.AchievementUnlocked);
    }

    /// <summary>
    /// The cartridge's own counter is the progress: PermaLocke does not count battles again.
    /// </summary>
    [Fact]
    public async Task A_game_counter_is_the_progress()
    {
        var (service, _, run) = Build(new Records(true, (46, 137)));

        var fled = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "huidas-200");

        Assert.Equal(137, fled.Count);
        Assert.False(fled.Unlocked);
        Assert.True(fled.Achievement.IsFromGame);

        // Y no se puede tocar a mano: el número es del juego.
        Assert.False(fled.CanMark);
        Assert.False((await service.MarkAsync(run, "huidas-200", complete: true)).Success);
    }

    /// <summary>
    /// Without a save there is no counter, and the achievement sits at zero rather than at a
    /// number somebody made up.
    /// </summary>
    [Fact]
    public async Task An_unreadable_save_leaves_the_counter_at_zero()
    {
        var (service, _, run) = Build(new Records(false));

        var fled = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "huidas-200");

        Assert.Equal(0, fled.Count);
        Assert.False(service.LastRecords!.Available);
        Assert.NotNull(service.LastRecords.Problem);
    }

    /// <summary>
    /// The reward in the bag is the milestone. Clearing the first trial moves no record and lights
    /// dozens of unlabelled flags, but it hands over the Normalium Z, and that has a number.
    /// </summary>
    [Fact]
    public async Task An_item_in_the_bag_unlocks_its_achievement()
    {
        var (service, _, run) = Build(new Records(true) { Items = [807] });

        var trial = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "prueba-01");

        Assert.Equal(1, trial.Count);
        Assert.True(trial.Unlocked);
        Assert.True(trial.CanClaim);
        Assert.True(trial.Achievement.IsFromGame);
        Assert.False(trial.Achievement.IsManual);
    }

    /// <summary>Without the reward there is no progress, and no way to type one in either.</summary>
    [Fact]
    public async Task Without_the_item_the_achievement_stays_at_zero_and_refuses_a_manual_mark()
    {
        var (service, log, run) = Build(new Records(true) { Items = [4, 17] });

        var trial = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "prueba-01");
        Assert.Equal(0, trial.Count);
        Assert.False(trial.Unlocked);
        Assert.False(trial.CanMark);

        var marked = await service.MarkAsync(run, "prueba-01", complete: true);

        Assert.False(marked.Success);
        Assert.Empty(log.Appended);
    }

    /// <summary>An unreadable save must not look like an empty bag, which would read as "not done".</summary>
    [Fact]
    public async Task An_unreadable_save_leaves_an_item_achievement_at_zero()
    {
        var (service, _, run) = Build(new Records(false));

        var trial = (await service.GetProgressAsync(run.Id)).Single(p => p.Achievement.Id == "prueba-01");

        Assert.Equal(0, trial.Count);
        Assert.False(service.LastRecords!.Available);
    }

    /// <summary>A claim that did not happen must leave no trace at all.</summary>
    [Fact]
    public async Task Claiming_what_is_not_unlocked_writes_nothing()
    {
        var (service, log, run) = Build();

        var result = await service.ClaimAsync(run, "gacha-3");

        Assert.False(result.Success);
        Assert.Contains("0 de 3", result.FailureReason);
        Assert.Empty(log.Appended);
    }
}
