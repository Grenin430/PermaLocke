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

    /// <summary>One counted automatically, one marked by hand: the two kinds the screen shows.</summary>
    private static Achievement[] Catalogue() =>
    [
        new("gacha-3", "Tirador", "Haz 3 tiradas.", GameEventType.GachaRoll, "GachaRoll", 3, 40),
        new("pegatinas-25", "Pegatinas", "Encuentra 25 pegatinas.", null, "sin disparador", 25, 75),
    ];

    private static (AchievementService Service, Events Log, Run Run) Build()
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

        return (new AchievementService(new Catalog(Catalogue()), points, log, clock), log, run);
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
