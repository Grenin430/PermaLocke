using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Tests;

public sealed class EventChainTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static GameEvent Sample(int delta, string description) => new()
    {
        Id = Guid.NewGuid(),
        RunId = RunId,
        Timestamp = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
        Type = GameEventType.PointsEarned,
        Source = EventSource.Player,
        Actor = "javi",
        Description = description,
        PointsDelta = delta
    };

    [Fact]
    public async Task Each_event_links_to_the_previous_one()
    {
        var store = new InMemoryEventStore();

        var first = await store.AppendAsync(Sample(10, "uno"));
        var second = await store.AppendAsync(Sample(20, "dos"));

        Assert.Equal(string.Empty, first.PreviousHash);
        Assert.NotEmpty(first.Hash);
        Assert.Equal(first.Hash, second.PreviousHash);
    }

    [Fact]
    public async Task An_intact_chain_verifies()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync(Sample(10, "uno"));
        await store.AppendAsync(Sample(20, "dos"));

        var report = await store.VerifyChainAsync(RunId);

        Assert.True(report.IsValid);
        Assert.Equal(2, report.CheckedEvents);
    }

    [Fact]
    public async Task Editing_the_points_of_a_stored_event_is_detected()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync(Sample(10, "uno"));
        await store.AppendAsync(Sample(20, "dos"));

        // Someone opens the database and inflates their points.
        store.TamperWith(0, e => e with { PointsDelta = 9999 });

        var report = await store.VerifyChainAsync(RunId);

        Assert.False(report.IsValid);
        Assert.Equal(1, report.CheckedEvents);
    }

    [Fact]
    public async Task Events_are_returned_in_the_order_they_happened()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync(Sample(1, "primero"));
        await store.AppendAsync(Sample(2, "segundo"));
        await store.AppendAsync(Sample(3, "tercero"));

        var all = await store.GetAllAsync(RunId);

        Assert.Equal(["primero", "segundo", "tercero"], all.Select(e => e.Description));
    }
}
