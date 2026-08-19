using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

public sealed class PointsServiceTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static (PointsService Service, InMemoryEventStore Store) Build()
    {
        var store = new InMemoryEventStore();
        return (new PointsService(store, new FixedClock()), store);
    }

    [Fact]
    public async Task Earning_points_increases_the_balance_and_records_an_event()
    {
        var (service, store) = Build();

        var result = await service.EarnAsync(RunId, 50, "Logro de prueba", EventSource.Player, "javi");

        Assert.True(result.Success);
        Assert.Equal(50, result.NewBalance);

        var events = await store.GetAllAsync(RunId);
        var single = Assert.Single(events);
        Assert.Equal(GameEventType.PointsEarned, single.Type);
        Assert.Equal(50, single.PointsDelta);
    }

    [Fact]
    public async Task Spending_more_than_the_balance_fails_without_recording_anything()
    {
        var (service, store) = Build();
        await service.EarnAsync(RunId, 30, "Logro", EventSource.Player, "javi");

        var result = await service.SpendAsync(RunId, 100, "Gacha", EventSource.Player, "javi");

        Assert.False(result.Success);
        Assert.Equal(30, result.NewBalance);
        Assert.Contains("insuficientes", result.FailureReason);

        // A rejected purchase must leave no trace in the ledger.
        Assert.Single(await store.GetAllAsync(RunId));
    }

    [Fact]
    public async Task Spending_within_the_balance_deducts_the_points()
    {
        var (service, _) = Build();
        await service.EarnAsync(RunId, 250, "Logros", EventSource.Player, "javi");

        var result = await service.SpendAsync(RunId, 100, "Gacha común", EventSource.Player, "javi");

        Assert.True(result.Success);
        Assert.Equal(150, result.NewBalance);
    }

    [Fact]
    public async Task Admin_adjustment_requires_a_reason()
    {
        var (service, store) = Build();

        var result = await service.AdjustAsync(RunId, 100, "   ", "admin");

        Assert.False(result.Success);
        Assert.Empty(await store.GetAllAsync(RunId));
    }

    [Fact]
    public async Task Admin_adjustment_records_the_reason_and_may_go_negative()
    {
        var (service, store) = Build();

        var result = await service.AdjustAsync(RunId, -40, "Penalización por saltarse una regla", "admin");

        Assert.True(result.Success);
        Assert.Equal(-40, result.NewBalance);

        var single = Assert.Single(await store.GetAllAsync(RunId));
        Assert.Equal(GameEventType.AdminAdjustment, single.Type);
        Assert.Equal(EventSource.Admin, single.Source);
        Assert.Equal("Penalización por saltarse una regla", single.Reason);
    }

    [Fact]
    public async Task Balances_of_different_runs_do_not_mix()
    {
        var (service, _) = Build();
        var otherRun = Guid.NewGuid();

        await service.EarnAsync(RunId, 100, "Logro", EventSource.Player, "javi");
        await service.EarnAsync(otherRun, 25, "Logro", EventSource.Player, "otro");

        Assert.Equal(100, await service.GetBalanceAsync(RunId));
        Assert.Equal(25, await service.GetBalanceAsync(otherRun));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now { get; } = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    }
}
