using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The shop. What matters here is not the arithmetic but the order: nothing gets charged for
/// something that did not arrive.
/// </summary>
public sealed class ShopServiceTests
{
    private sealed class Catalog(params ShopItem[] items) : IShopCatalog
    {
        public IReadOnlyList<ShopItem> Items => items;
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
        public DateTimeOffset Now => new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Stands in for the bag. Records what it was asked for, so the order can be checked.</summary>
    private sealed class Bag(bool works, string problem = "") : IItemDelivery
    {
        public List<int> Given { get; } = [];

        public int Carried { get; private set; }

        public Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default)
        {
            if (!works)
            {
                return Task.FromResult(ItemDeliveryResult.Unreachable(problem));
            }

            Given.Add(itemId);
            Carried += amount;
            return Task.FromResult(new ItemDeliveryResult(true, Carried, string.Empty));
        }

        public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
            Task.FromResult(works ? Carried : -1);

        public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(works
                ? itemIds.ToDictionary(id => id, _ => Carried)
                : new Dictionary<int, int>());
    }

    private static readonly ShopItem Candy = new(50, "Caramelo Raro", 150);

    private static (ShopService Shop, Events Log, Run Run, IPointsService Points) Build(IItemDelivery bag)
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

        return (new ShopService(new Catalog(Candy), points, bag, log, clock), log, run, points);
    }

    private static async Task GiveAsync(IPointsService points, Run run, int amount) =>
        await points.EarnAsync(run.Id, amount, "prueba", EventSource.Player, run.PlayerName);

    /// <summary>Ascenso Draco: paid once, nothing in the bag, and the run remembers it for Rayquaza.</summary>
    [Fact]
    public async Task An_unlock_is_paid_once_and_needs_no_bag()
    {
        var bag = new Bag(works: false);
        var log = new Events();
        var clock = new FixedClock();
        var points = new PointsService(log, clock);
        var run = new Run
        {
            Id = Guid.NewGuid(), Name = "Prueba", Game = GameVersion.UltraMoon, SeedLabel = "1", Seed = 1,
            RoleId = "player", PlayerName = "Grenin"
        };
        var draco = new ShopItem(90620, "Ascenso Draco (Rayquaza)", 600, ShopItem.MegaStones, 620, 384);
        var shop = new ShopService(new Catalog(draco), points, bag, log, clock);
        await GiveAsync(points, run, 1300);

        Assert.True((await shop.BuyAsync(run, draco.Id)).Succeeded);
        var again = await shop.BuyAsync(run, draco.Id);

        Assert.False(again.Succeeded);
        Assert.Equal(700, again.Balance);
        Assert.Empty(bag.Given);
        Assert.Contains((384, 620), await shop.UnlockedAsync(run.Id));
    }

    [Fact]
    public async Task Buying_delivers_the_item_and_charges_the_price()
    {
        var bag = new Bag(works: true);
        var (shop, log, run, points) = Build(bag);
        await GiveAsync(points, run, 200);

        var result = await shop.BuyAsync(run, Candy.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.Balance);
        Assert.Equal([50], bag.Given);
        Assert.Single(log.Appended, e => e.Type == GameEventType.ShopPurchase);
    }

    /// <summary>
    /// The rule the whole thing hangs on: if the bag write fails, nothing is charged and nothing
    /// is written down. A player with fewer points and no item has no way back.
    /// </summary>
    [Fact]
    public async Task A_failed_delivery_charges_nothing_and_records_nothing()
    {
        var bag = new Bag(works: false, problem: "No se encuentra la mochila del juego.");
        var (shop, log, run, points) = Build(bag);
        await GiveAsync(points, run, 200);

        var result = await shop.BuyAsync(run, Candy.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(PurchaseOutcome.GameUnreachable, result.Outcome);
        Assert.Equal(200, await shop.GetBalanceAsync(run.Id));
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.ShopPurchase);
    }

    /// <summary>Too poor: refused before anything is written into the game at all.</summary>
    [Fact]
    public async Task Without_the_points_nothing_is_even_delivered()
    {
        var bag = new Bag(works: true);
        var (shop, log, run, points) = Build(bag);
        await GiveAsync(points, run, 100);

        var result = await shop.BuyAsync(run, Candy.Id);

        Assert.Equal(PurchaseOutcome.NotEnoughPoints, result.Outcome);
        Assert.Empty(bag.Given);
        Assert.Contains("Te faltan 50", result.Message);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.ShopPurchase);
    }

    [Fact]
    public async Task Something_the_shop_does_not_sell_is_refused()
    {
        var bag = new Bag(works: true);
        var (shop, _, run, points) = Build(bag);
        await GiveAsync(points, run, 1000);

        var result = await shop.BuyAsync(run, itemId: 999);

        Assert.Equal(PurchaseOutcome.UnknownItem, result.Outcome);
        Assert.Empty(bag.Given);
    }

    /// <summary>The balance is a projection, so two purchases have to leave the right number.</summary>
    [Fact]
    public async Task Two_purchases_leave_the_balance_right()
    {
        var bag = new Bag(works: true);
        var (shop, _, run, points) = Build(bag);
        await GiveAsync(points, run, 400);

        await shop.BuyAsync(run, Candy.Id);
        var second = await shop.BuyAsync(run, Candy.Id);

        Assert.True(second.Succeeded);
        Assert.Equal(100, second.Balance);
        Assert.Equal(2, second.Carried);
    }

    private sealed class Natures(bool works) : INatureChanger
    {
        public List<NatureChange> Written { get; } = [];

        public bool CanChangeNow(out string reason)
        {
            reason = works ? string.Empty : "El juego está abierto.";
            return works;
        }

        public Task<DeliveryResult> ApplyAsync(NatureChange change, CancellationToken ct = default)
        {
            if (!works) return Task.FromResult(new DeliveryResult(DeliveryOutcome.GameRunning, "El juego está abierto."));
            Written.Add(change);
            return Task.FromResult(new DeliveryResult(DeliveryOutcome.Delivered, string.Empty));
        }
    }

    /// <summary>The nature herbs (2026-09-27): written first, charged after, never charged for a refused one.</summary>
    [Fact]
    public async Task A_herb_writes_the_nature_then_charges_and_a_refusal_costs_nothing()
    {
        var log = new Events();
        var clock = new FixedClock();
        var points = new PointsService(log, clock);
        var run = new Run
        {
            Id = Guid.NewGuid(), Name = "Prueba", Game = GameVersion.UltraMoon, SeedLabel = "1", Seed = 1,
            RoleId = "player", PlayerName = "Grenin"
        };
        var timid = new ShopItem(91010, "Hierba Miedosa", 500, ShopItem.Herbs, Nature: 10);
        var target = new BoxedPokemon(BoxedPokemon.PartyBox, 2, 25, 0, "Pikachu", "", 30, false, false, "", "Fuerte", "", "",
            "", "", "", 5, [], [], [], [], 70, 0xABCD, Nature: 0);
        await GiveAsync(points, run, 700);

        var closedGame = new Natures(works: false);
        var refused = await new ShopService(new Catalog(timid), points, new Bag(true), log, clock, closedGame)
            .ChangeNatureAsync(run, timid.Id, target);
        Assert.False(refused.Succeeded);
        Assert.Equal(700, refused.Balance);

        var natures = new Natures(works: true);
        var shop = new ShopService(new Catalog(timid), points, new Bag(true), log, clock, natures);
        var done = await shop.ChangeNatureAsync(run, timid.Id, target);

        Assert.True(done.Succeeded);
        Assert.Equal(200, done.Balance);
        Assert.Equal(new NatureChange(BoxedPokemon.PartyBox, 2, 0xABCD, "Pikachu", 10), natures.Written.Single());
        Assert.False((await shop.ChangeNatureAsync(run, timid.Id, target with { Nature = 10 })).Succeeded);
    }
}
