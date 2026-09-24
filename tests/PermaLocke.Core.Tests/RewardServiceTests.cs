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

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

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

    /// <summary>Stands in for the save-file unlocks. Records what it was asked to turn on.</summary>
    private sealed class NoUnlocks : IGameUnlocks
    {
        public List<string> Applied { get; } = [];

        public bool Reachable { get; init; } = true;

        public IReadOnlySet<string> Known { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "megaevolucion" };

        public bool CanApplyNow(out string reason)
        {
            reason = Reachable ? string.Empty : "El juego está cargado en Azahar.";
            return Reachable;
        }

        public Task<UnlockResult> ApplyAsync(IReadOnlyList<string> keys, CancellationToken ct = default)
        {
            if (!Reachable)
            {
                return Task.FromResult(new UnlockResult(false, "El juego está cargado en Azahar."));
            }

            Applied.AddRange(keys);
            return Task.FromResult(new UnlockResult(true, "Desbloqueado."));
        }
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

        /// <summary>Item ids the live bag holds. Nothing to do with what the save remembers.</summary>
        public IReadOnlyList<int> Carrying { get; init; } = [];

        public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
            Task.FromResult(Carrying.Contains(itemId) ? 1 : 0);

        /// <summary>
        /// Copies the real contract, because the caller depends on it: a reachable game answers
        /// with one entry per id asked for, even when the count is zero, and an unreachable one
        /// answers with nothing at all. That is what lets "empty" mean "cannot tell" instead of
        /// "carries none", and a fake that blurred the two would hide the bug it exists to catch.
        /// </summary>
        public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(Reachable
                ? itemIds.Distinct().ToDictionary(id => id, id => Carrying.Contains(id) ? 1 : 0)
                : new Dictionary<int, int>());
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
            records ?? new Records(), basket, new NoUnlocks(), new Items(), log, clock),
            log, basket);
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
        Assert.Contains("no se puede entregar", result.Message);
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

    /// <summary>
    /// Carrying it in the <b>running game</b> is enough, with a save that knows nothing about it.
    /// </summary>
    /// <remarks>
    /// The point of reading the live bag: the save only says what the bag held last time the player
    /// saved, so a prize that waited for it would sit dark until somebody remembered to save.
    /// </remarks>
    [Fact]
    public async Task Carrying_the_item_in_the_running_game_is_enough()
    {
        var (service, log, bag) = Build(new Records(), new Bag { Carrying = [4] }, FirstBalls);

        var result = await service.ClaimAsync(TheRun(), "primeras-balls");

        Assert.Equal(RewardOutcome.Delivered, result.Outcome);
        Assert.Equal((3, 10), Assert.Single(bag.Given));
        Assert.Contains(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>With the game closed the save answers, so the screen is not left blank.</summary>
    [Fact]
    public async Task With_the_game_closed_the_save_still_answers()
    {
        var (service, _, _) = Build(new Records(4),
            new Bag { Reachable = false }, FirstBalls);

        var status = Assert.Single(await service.GetStatusAsync(TheRun()));

        Assert.True(status.CanClaim);
    }

    /// <summary>
    /// The live bag wins over the save when both can speak. A Poké Ball is spent, unlike a
    /// Z-Crystal, so the truthful answer is the current one even when it turns the prize off.
    /// </summary>
    [Fact]
    public async Task The_running_game_wins_over_what_the_save_remembers()
    {
        var (service, _, bag) = Build(new Records(4), new Bag(), FirstBalls);

        Assert.Equal(RewardOutcome.NotEarned,
            (await service.ClaimAsync(TheRun(), "primeras-balls")).Outcome);
        Assert.Empty(bag.Given);
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
    /// Neither source can answer, so the condition is unmet. It is the direction that matters: a
    /// prize that hands itself over because nothing could be read is the worst way to fail.
    /// </summary>
    [Fact]
    public async Task With_nothing_readable_it_is_not_earned()
    {
        var (service, _, bag) = Build(new Blind(), new Bag { Reachable = false }, FirstBalls);

        Assert.Equal(RewardOutcome.NotEarned,
            (await service.ClaimAsync(TheRun(), "primeras-balls")).Outcome);
        Assert.Empty(bag.Given);
    }

    /// <summary>
    /// The automatic prize arrives on its own, once, and leaves the same event behind. Being
    /// automatic changes who pressed it, not what is allowed.
    /// </summary>
    [Fact]
    public async Task An_automatic_prize_is_handed_over_without_being_asked()
    {
        var automatic = FirstBalls with { Automatic = true };
        var (service, log, bag) = Build(new Records(), new Bag { Carrying = [4] }, automatic);
        var run = TheRun();

        var given = Assert.Single(await service.ClaimAutomaticAsync(run));

        Assert.Equal(RewardOutcome.Delivered, given.Outcome);
        Assert.Equal((3, 10), Assert.Single(bag.Given));
        Assert.Contains(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>Automatic does not mean repeatedly: the event is still what makes "once" true.</summary>
    [Fact]
    public async Task An_automatic_prize_is_not_handed_over_twice()
    {
        var (service, _, bag) = Build(new Records(), new Bag { Carrying = [4] },
            FirstBalls with { Automatic = true });
        var run = TheRun();

        await service.ClaimAutomaticAsync(run);
        Assert.Empty(await service.ClaimAutomaticAsync(run));

        Assert.Single(bag.Given);
    }

    [Fact]
    public async Task An_automatic_prize_that_is_not_earned_yet_is_not_handed_over()
    {
        var (service, _, bag) = Build(new Records(807), reward: FirstBalls with { Automatic = true });

        Assert.Empty(await service.ClaimAutomaticAsync(TheRun()));
        Assert.Empty(bag.Given);
    }

    /// <summary>A prize nobody marked automatic keeps waiting for its button.</summary>
    [Fact]
    public async Task A_manual_prize_is_left_alone()
    {
        var (service, _, bag) = Build(new Records(), new Bag { Carrying = [4] }, FirstBalls);

        Assert.Empty(await service.ClaimAutomaticAsync(TheRun()));
        Assert.Empty(bag.Given);
    }

    /// <summary>
    /// A prize can hand over a free roll as well as items, and the roll lives in the event.
    /// </summary>
    /// <remarks>
    /// Written into <c>credito</c> and counted back out by <c>CreditService</c>, the same field the
    /// wheel writes. Which is what makes adding a roll to a prize safe: a claim made before the
    /// roll existed carries no such field and pays nothing, so editing the catalogue cannot hand
    /// credit backwards to everyone who already claimed.
    /// </remarks>
    [Fact]
    public async Task A_prize_can_grant_a_free_roll_and_the_event_records_it()
    {
        var withRoll = FirstBalls with { Credit = ["decente"] };
        var (service, log, bag) = Build(new Records(), new Bag { Carrying = [4] }, withRoll);

        var result = await service.ClaimAsync(TheRun(), "primeras-balls");

        Assert.Equal(RewardOutcome.Delivered, result.Outcome);
        Assert.Equal((3, 10), Assert.Single(bag.Given));
        Assert.Contains("decente", result.Message);

        var claimed = Assert.Single(log.Appended, e => e.Type == GameEventType.RewardClaimed);
        Assert.Equal("decente", claimed.Data["credito"]);
    }

    /// <summary>A prize with no roll writes an empty field, which grants nothing.</summary>
    [Fact]
    public async Task A_prize_with_no_roll_grants_no_credit()
    {
        var (service, log, _) = Build(new Records(807, 813));

        await service.ClaimAsync(TheRun(), "doce-pruebas");

        var claimed = Assert.Single(log.Appended, e => e.Type == GameEventType.RewardClaimed);
        Assert.Equal(string.Empty, claimed.Data["credito"]);
    }

    /// <summary>A prize whose whole content is a flag in the saved game.</summary>
    private static readonly Reward Megas = new(
        "megaevolucion", "Megaevolución", "Tras las seis primeras pruebas.",
        ["prueba-01", "prueba-02"], [], Unlocks: ["megaevolucion"]);

    private static (RewardService Service, Events Log, NoUnlocks Unlocks) BuildWithUnlocks(
        NoUnlocks unlocks, Reward reward)
    {
        var log = new Events();
        var clock = new FixedClock();
        var records = new Records(807, 813);

        var achievements = new AchievementService(
            new Achievements(Trials()), new PointsService(log, clock), log, clock,
            records, new FixedRole(FixedRole.Normal));

        return (new RewardService(new Catalog(reward), achievements, records, new Bag(),
            unlocks, new Items(), log, clock), log, unlocks);
    }

    /// <summary>
    /// The unlock is applied and the prize is recorded, with no items involved at all.
    /// </summary>
    /// <remarks>
    /// Measured, not assumed: Ultra Moon does not gate Mega Evolution on carrying the Key Stone.
    /// The item went into the bag and was read back, the Pokémon held its stone, and no button
    /// appeared. What gates it is a field in the trainer block.
    /// </remarks>
    [Fact]
    public async Task A_prize_can_unlock_something_in_the_saved_game()
    {
        var (service, log, unlocks) = BuildWithUnlocks(new NoUnlocks(), Megas);

        var result = await service.ClaimAsync(TheRun(), "megaevolucion");

        Assert.Equal(RewardOutcome.Delivered, result.Outcome);
        Assert.Equal(["megaevolucion"], unlocks.Applied);
        Assert.Contains(log.Appended, e => e.Type == GameEventType.RewardClaimed);
    }

    /// <summary>
    /// With the game open nothing is written and nothing is recorded, so it can be claimed later.
    /// </summary>
    /// <remarks>
    /// The one that matters: this writes the save file, and a prize burned because the emulator
    /// happened to be running would be a one-off the player never got.
    /// </remarks>
    [Fact]
    public async Task With_the_game_open_the_unlock_is_refused_and_nothing_is_claimed()
    {
        var (service, log, unlocks) = BuildWithUnlocks(new NoUnlocks { Reachable = false }, Megas);
        var run = TheRun();

        var refused = await service.ClaimAsync(run, "megaevolucion");

        Assert.Equal(RewardOutcome.GameUnreachable, refused.Outcome);
        Assert.Empty(unlocks.Applied);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RewardClaimed);

        // Y sigue ahí para cuando cierre el juego.
        Assert.True(Assert.Single(await service.GetStatusAsync(run)).CanClaim);
    }

    /// <summary>Not earned means the save is never even opened.</summary>
    [Fact]
    public async Task An_unearned_unlock_never_touches_the_save()
    {
        var log = new Events();
        var clock = new FixedClock();
        var records = new Records(807);
        var unlocks = new NoUnlocks();

        var achievements = new AchievementService(
            new Achievements(Trials()), new PointsService(log, clock), log, clock,
            records, new FixedRole(FixedRole.Normal));

        var service = new RewardService(new Catalog(Megas), achievements, records, new Bag(),
            unlocks, new Items(), log, clock);

        Assert.Equal(RewardOutcome.NotEarned,
            (await service.ClaimAsync(TheRun(), "megaevolucion")).Outcome);

        Assert.Empty(unlocks.Applied);
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
