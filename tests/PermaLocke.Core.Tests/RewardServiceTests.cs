using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The one-off prizes. What is guarded here is "once": a reward that can be taken twice is a
/// competition where one player has twenty-four Hyper Potions and the rest have twelve.
/// </summary>
public sealed class RewardServiceTests
{
    private sealed class Catalog(params Reward[] all) : IRewardCatalog
    {
        public IReadOnlyList<Reward> All => all;
    }

    private sealed class Achievements(IReadOnlyList<Achievement> all) : IAchievementCatalog
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
        public DateTimeOffset Now => new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Stands in for the cartridge's counters; here it only ever reports held items.</summary>
    private sealed class Records(params int[] itemsHeld) : IGameRecords
    {
        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(new GameRecordSnapshot(true, null, null,
                new Dictionary<int, int>(), DateTimeOffset.UnixEpoch,
                itemsHeld.ToHashSet(), new Dictionary<int, int>()));
    }

    /// <summary>Counters that cannot be read, the way a missing or locked save behaves.</summary>
    private sealed class Blind : IGameRecords
    {
        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(GameRecordSnapshot.Unavailable("no hay partida", DateTimeOffset.UnixEpoch));
    }

    /// <summary>Stands in for the bag. Remembers what it was asked to write, and can refuse.</summary>
    private sealed class Bag(params int[] refuse) : IItemDelivery
    {
        public List<(int Item, int Amount)> Given { get; } = [];

        public bool Reachable { get; init; } = true;

        public Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default)
        {
            if (!Reachable)
            {
                return Task.FromResult(ItemDeliveryResult.Unreachable("Azahar no responde."));
            }

            if (refuse.Contains(itemId))
            {
                return Task.FromResult(ItemDeliveryResult.Failed("el bolsillo está lleno"));
            }

            Given.Add((itemId, amount));
            return Task.FromResult(new ItemDeliveryResult(true, amount, string.Empty));
        }

        public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
    }

    private sealed class Items : IItemLookup
    {
        public string GetName(int itemId) => itemId switch
        {
            3 => "Super Ball",
            4 => "Poké Ball",
            25 => "Hiperpoción",
            27 => "Cura Total",
            _ => $"Objeto {itemId}"
        };
    }

    private static readonly Reward TwelveTrials = new(
        "doce-pruebas", "Premio de las doce pruebas", "Por superar las doce pruebas.",
        ["prueba-01", "prueba-02"],
        [new RewardItem(25, "Hiperpoción", 12), new RewardItem(27, "Cura Total", 12)]);

    /// <summary>
    /// A reward earned by <em>carrying</em> something instead of by an achievement.
    /// </summary>
    /// <remarks>
    /// "The first time somebody hands you Poké Balls" moves no counter and lights no flag, but it
    /// leaves Poké Balls in the bag. Same anchor as the trials on their Z-Crystal (§40), and it only
    /// works because the game never takes these back.
    /// </remarks>
    private static readonly Reward FirstBalls = new(
        "primeras-balls", "Refuerzo de Poké Balls", "Diez Super Balls de propina.",
        [], [new RewardItem(3, "Super Ball", 10)], [4]);

    /// <summary>Two trials, each anchored to the Z-crystal it hands over, as the real ones are.</summary>
    private static Achievement[] Trials() =>
    [
        new("prueba-01", "Primera", "La primera.", null, "sin disparador", 1, 100, Item: 807),
        new("prueba-02", "Segunda", "La segunda.", null, "sin disparador", 1, 100, Item: 813)
    ];

    private static Run TheRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260823",
        Seed = 20260823,
        RoleId = "player",
        PlayerName = "Grenin"
    };

    private static (RewardService Service, Events Log, Bag Bag) Build(
        IGameRecords? records = null, Bag? bag = null, Reward? reward = null)
    {
        var log = new Events();
        var clock = new FixedClock();
        var basket = bag ?? new Bag();

        var achievements = new AchievementService(
            new Achievements(Trials()), new PointsService(log, clock), log, clock,
            records ?? new Records(), new FixedRole(FixedRole.Normal));

        return (new RewardService(new Catalog(reward ?? TwelveTrials), achievements,
            records ?? new Records(), basket, new Items(), log, clock), log, basket);
    }

    [Fact]
    public async Task With_every_trial_done_it_hands_over_everything_and_records_it()
    {
        var (service, log, bag) = Build(new Records(807, 813));

        var result = await service.ClaimAsync(TheRun(), "doce-pruebas");

        Assert.Equal(RewardOutcome.Delivered, result.Outcome);
        Assert.Equal([(25, 12), (27, 12)], bag.Given);

        var recorded = Assert.Single(log.Appended, e => e.Type == GameEventType.RewardClaimed);
        Assert.Equal("doce-pruebas", recorded.Data["premio"]);
        Assert.Equal("True", recorded.Data["completo"]);
    }

    /// <summary>The whole point: pressing it twice gives nothing the second time.</summary>
    [Fact]
    public async Task It_can_only_be_taken_once()
    {
        var (service, _, bag) = Build(new Records(807, 813));
        var run = TheRun();

        await service.ClaimAsync(run, "doce-pruebas");
        var again = await service.ClaimAsync(run, "doce-pruebas");

        Assert.Equal(RewardOutcome.AlreadyClaimed, again.Outcome);
        Assert.Equal(2, bag.Given.Count);
    }

    /// <summary>Eleven trials is not twelve, and the reward says how many are left.</summary>
    [Fact]
    public async Task Not_every_trial_means_nothing_is_written()
    {
        var (service, log, bag) = Build(new Records(807));

        var result = await service.ClaimAsync(TheRun(), "doce-pruebas");

        Assert.Equal(RewardOutcome.NotEarned, result.Outcome);
        Assert.Equal(["prueba-02"], result.Missing);
        Assert.Empty(bag.Given);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>
    /// Nothing arrived, so nothing is spent: the prize is still there when the emulator is.
    /// </summary>
    [Fact]
    public async Task With_the_game_closed_the_prize_survives()
    {
        var (service, log, _) = Build(new Records(807, 813), new Bag { Reachable = false });

        var result = await service.ClaimAsync(TheRun(), "doce-pruebas");

        Assert.Equal(RewardOutcome.GameUnreachable, result.Outcome);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>
    /// Half arrived. It still counts as taken, because a button that can be pressed again to
    /// duplicate what it already gave is the worse failure of the two.
    /// </summary>
    [Fact]
    public async Task A_half_delivery_still_counts_as_taken_and_says_so()
    {
        var (service, log, bag) = Build(new Records(807, 813), new Bag(27));

        var result = await service.ClaimAsync(TheRun(), "doce-pruebas");

        Assert.Equal(RewardOutcome.PartlyDelivered, result.Outcome);
        Assert.Equal([(25, 12)], bag.Given);
        Assert.Contains("Cura Total", result.Message);

        var recorded = Assert.Single(log.Appended, e => e.Type == GameEventType.RewardClaimed);
        Assert.Equal("False", recorded.Data["completo"]);
        Assert.Contains("Cura Total", recorded.Data["noEntregado"]);
    }

    /// <summary>
    /// An id that lands on the wrong item hands over the wrong thing and never fails, so the name
    /// is checked against the cartridge before anything is written (§52).
    /// </summary>
    [Fact]
    public async Task A_wrong_item_id_writes_nothing()
    {
        var wrong = TwelveTrials with { Items = [new RewardItem(25, "Cura Total", 12)] };
        var (service, log, bag) = Build(new Records(807, 813), reward: wrong);

        var result = await service.ClaimAsync(TheRun(), "doce-pruebas");

        Assert.Equal(RewardOutcome.NotDelivered, result.Outcome);
        Assert.Contains("Hiperpoción", result.Message);
        Assert.Empty(bag.Given);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    [Fact]
    public async Task The_status_says_how_far_off_it_is()
    {
        var (service, _, _) = Build(new Records(807));

        var status = Assert.Single(await service.GetStatusAsync(TheRun()));

        Assert.Equal("1/2", status.Progress);
        Assert.False(status.CanClaim);
        Assert.False(status.Claimed);
    }

    [Fact]
    public async Task The_status_remembers_it_was_taken()
    {
        var (service, _, _) = Build(new Records(807, 813));
        var run = TheRun();

        await service.ClaimAsync(run, "doce-pruebas");
        var status = Assert.Single(await service.GetStatusAsync(run));

        Assert.True(status.Claimed);
        Assert.False(status.CanClaim);
    }

    [Fact]
    public async Task Carrying_the_item_is_enough_to_earn_it()
    {
        var (service, log, bag) = Build(new Records(4), reward: FirstBalls);

        var result = await service.ClaimAsync(TheRun(), "primeras-balls");

        Assert.Equal(RewardOutcome.Delivered, result.Outcome);
        Assert.Equal((3, 10), Assert.Single(bag.Given));
        Assert.Contains(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    [Fact]
    public async Task Without_the_item_nothing_is_handed_over()
    {
        var (service, log, bag) = Build(new Records(807, 813), reward: FirstBalls);

        var result = await service.ClaimAsync(TheRun(), "primeras-balls");

        Assert.Equal(RewardOutcome.NotEarned, result.Outcome);
        Assert.Equal("Poké Ball", Assert.Single(result.Missing));
        Assert.Empty(bag.Given);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>
    /// A save that cannot be read leaves the condition unmet. It is the direction that matters: a
    /// prize that hands itself over because a file was missing is the worst way to fail.
    /// </summary>
    [Fact]
    public async Task An_unreadable_save_does_not_earn_it()
    {
        var (service, _, bag) = Build(new Blind(), reward: FirstBalls);

        Assert.Equal(RewardOutcome.NotEarned,
            (await service.ClaimAsync(TheRun(), "primeras-balls")).Outcome);
        Assert.Empty(bag.Given);
    }

    [Fact]
    public async Task An_unknown_reward_is_not_invented()
    {
        var (service, _, bag) = Build(new Records(807, 813));

        Assert.Equal(RewardOutcome.UnknownReward,
            (await service.ClaimAsync(TheRun(), "no-existe")).Outcome);
        Assert.Empty(bag.Given);
    }
}
