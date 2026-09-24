using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The admin's gifts (§129): who they are for, what collecting one writes, and that it happens exactly once.
/// </summary>
public sealed class GiftServiceTests
{
    private static readonly Guid Player = Guid.Parse("cf1cdc28-2c5a-4644-b65d-477b64fa853c");

    [Fact]
    public void A_gift_for_everybody_is_for_everybody()
    {
        Assert.True(Gift(to: AdminGift.Everybody).IsFor(Player));
        Assert.True(Gift(to: AdminGift.Everybody).IsFor(Guid.NewGuid()));
    }

    [Fact]
    public void A_gift_for_one_player_is_not_for_another()
    {
        var gift = Gift(to: Player.ToString());

        Assert.True(gift.IsFor(Player));
        Assert.False(gift.IsFor(Guid.NewGuid()));
    }

    [Fact]
    public void An_empty_gift_says_so()
    {
        Assert.True(Gift().IsEmpty);
        Assert.False(Gift(points: 50).IsEmpty);
        Assert.False(Gift(items: [new GiftItem(25, "Hiperpoción", 2)]).IsEmpty);
        Assert.False(Gift(trades: 1).IsEmpty);
    }

    [Fact]
    public void What_it_gives_reads_as_one_line()
    {
        var gift = Gift(points: 100, items: [new GiftItem(25, "Hiperpoción", 2)], trades: 1,
            rolls: new Dictionary<string, int> { ["decente"] = 2 });

        Assert.Equal("+100 puntos · 2 Hiperpoción · 2 tiradas en «decente» · 1 wonder trade", gift.Say());
    }

    [Fact]
    public async Task Collecting_pays_the_points_and_writes_who_sent_it()
    {
        var (gifts, log, run, bag) = Build();

        var result = await gifts.ClaimAsync(run, Gift(points: 120));

        Assert.True(result.Collected);
        var all = await log.GetAllAsync(run.Id);

        Assert.Equal(120, all.Sum(e => e.PointsDelta));
        Assert.Empty(bag.Given);

        var claim = Assert.Single(all, e => e.Type == GameEventType.AdminGiftClaimed);
        Assert.Equal(EventSource.Admin, claim.Source);
        Assert.Equal("Grenin", claim.Data["de"]);
        Assert.Equal("por ganar el combate", claim.Reason);
    }

    /// <summary>The whole point of «once»: the same gift twice is one gift.</summary>
    [Fact]
    public async Task The_same_gift_cannot_be_collected_twice()
    {
        var (gifts, log, run, _) = Build();
        var gift = Gift(points: 50);

        Assert.True((await gifts.ClaimAsync(run, gift)).Collected);
        var second = await gifts.ClaimAsync(run, gift);

        var all = await log.GetAllAsync(run.Id);

        Assert.False(second.Collected);
        Assert.Equal(50, all.Sum(e => e.PointsDelta));
        Assert.Single(all, e => e.Type == GameEventType.AdminGiftClaimed);
    }

    [Fact]
    public async Task Items_go_to_the_bag_and_are_named_in_the_event()
    {
        var (gifts, log, run, bag) = Build();

        var result = await gifts.ClaimAsync(run, Gift(items: [new GiftItem(25, "Hiperpoción", 3)]));

        Assert.True(result.Collected);
        Assert.Equal((25, 3), Assert.Single(bag.Given));
        var all = await log.GetAllAsync(run.Id);
        Assert.Equal("3xHiperpoción", Assert.Single(all, e => e.Type == GameEventType.AdminGiftClaimed).Data["objetos"]);
    }

    /// <summary>
    /// With the game shut nothing is touched at all, so the gift is still there when it opens.
    /// </summary>
    [Fact]
    public async Task A_gift_with_items_waits_for_the_game_instead_of_half_applying()
    {
        var (gifts, log, run, bag) = Build(reachable: false);

        var result = await gifts.ClaimAsync(run,
            Gift(points: 90, items: [new GiftItem(25, "Hiperpoción", 1)]));

        Assert.False(result.Collected);
        Assert.Contains("Azahar", result.Message);
        Assert.Empty(bag.Given);
        Assert.Empty(await log.GetAllAsync(run.Id));
    }

    /// <summary>
    /// Free rolls and wonder trades are counted from the fields the credits already read, so a gift pays without
    /// <see cref="CreditService"/> being taught anything about gifts.
    /// </summary>
    [Fact]
    public async Task Rolls_and_trades_land_where_the_credits_look_for_them()
    {
        var (gifts, log, run, _) = Build();

        await gifts.ClaimAsync(run, Gift(trades: 2,
            rolls: new Dictionary<string, int> { ["decente"] = 2, ["bueno"] = 1 }));

        var claim = Assert.Single(await log.GetAllAsync(run.Id), e => e.Type == GameEventType.AdminGiftClaimed);

        Assert.Equal("decente,decente,bueno", claim.Data["credito"]);
        Assert.Equal("2", claim.Data["creditoIntercambio"]);
    }

    [Fact]
    public async Task An_empty_gift_is_refused_rather_than_recorded()
    {
        var (gifts, log, run, _) = Build();

        Assert.False((await gifts.ClaimAsync(run, Gift())).Collected);
        Assert.Empty(await log.GetAllAsync(run.Id));
    }

    private static AdminGift Gift(string? to = null, int points = 0, IReadOnlyList<GiftItem>? items = null,
        int trades = 0, IReadOnlyDictionary<string, int>? rolls = null) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        From = "Grenin",
        To = to ?? Player.ToString(),
        Reason = "por ganar el combate",
        CreatedAt = DateTimeOffset.UnixEpoch,
        Points = points,
        Items = items ?? [],
        Rolls = rolls ?? new Dictionary<string, int>(),
        WonderTrades = trades
    };

    private static (GiftService Gifts, InMemoryEventStore Log, Run Run, Bag Bag) Build(bool reachable = true)
    {
        var log = new InMemoryEventStore();
        var clock = new FixedClock();
        var bag = new Bag { Reachable = reachable };
        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = "PRUEBA",
            PlayerName = "Grenin",
            Seed = 1,
            SeedLabel = "PRUEBA-1",
            Game = GameVersion.UltraMoon,
            RoleId = "normal",
            CreatedAt = DateTimeOffset.UnixEpoch
        };

        return (new GiftService(log, new PointsService(log, clock), bag, clock), log, run, bag);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }

    /// <summary>
    /// Stands in for the bag, copying the real contract: a reachable game answers one entry per id asked for, and an
    /// unreachable one answers with nothing at all (§68).
    /// </summary>
    private sealed class Bag : IItemDelivery
    {
        public List<(int Item, int Amount)> Given { get; } = [];

        public bool Reachable { get; init; } = true;

        public Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default)
        {
            if (!Reachable)
            {
                return Task.FromResult(ItemDeliveryResult.Unreachable("Azahar no responde."));
            }

            Given.Add((itemId, amount));
            return Task.FromResult(new ItemDeliveryResult(true, amount, string.Empty));
        }

        public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
            Task.FromResult(Reachable ? 0 : -1);

        public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, int>>(Reachable
                ? itemIds.ToDictionary(id => id, _ => 0)
                : new Dictionary<int, int>());
    }
}
