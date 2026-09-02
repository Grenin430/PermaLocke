using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The map's zone marks: what the player says happened at each zone's one encounter.
/// </summary>
/// <remarks>
/// A tracker and nothing more. It replaced pinning captures to zones, which was the only thing
/// feeding the first-encounter rule — so that rule is switched off in <c>Data/rules.json</c> rather
/// than left enabled with nothing to compare against, which is precisely the §81 fault.
/// </remarks>
public sealed class ZoneOutcomeTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static (ZoneOutcomeService Service, FakeEvents Events) Build()
    {
        var events = new FakeEvents();
        return (new ZoneOutcomeService(events, new StepClock()), events);
    }

    [Fact]
    public async Task A_mark_is_what_the_zone_reads_as()
    {
        var (service, _) = Build();

        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Caught, "javi");

        Assert.Equal(ZoneOutcome.Caught, (await service.GetAsync(RunId))["ruta-1"]);
    }

    /// <summary>The state of a zone is the last thing said about it, not the first.</summary>
    [Fact]
    public async Task The_latest_mark_wins()
    {
        var (service, _) = Build();

        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Caught, "javi");
        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Died, "javi");

        Assert.Equal(ZoneOutcome.Died, (await service.GetAsync(RunId))["ruta-1"]);
    }

    /// <summary>Clearing takes the zone off the board rather than marking it as something.</summary>
    [Fact]
    public async Task Clearing_leaves_the_zone_unmarked()
    {
        var (service, _) = Build();

        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Fled, "javi");
        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Free, "javi");

        Assert.DoesNotContain("ruta-1", (await service.GetAsync(RunId)).Keys);
    }

    /// <summary>
    /// Every click leaves its own event: undoing a mis-click adds history, it never rewrites it.
    /// </summary>
    [Fact]
    public async Task Every_click_is_recorded()
    {
        var (service, events) = Build();

        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Caught, "javi");
        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Free, "javi");

        var marks = events.All.Where(e => e.Type == GameEventType.ZoneOutcomeSet).ToArray();

        Assert.Equal(2, marks.Length);
        Assert.All(marks, mark => Assert.Equal(EventSource.Player, mark.Source));
        Assert.Equal("ruta-1", marks[0].LocationId);
        Assert.Equal("atrapado", marks[0].Data["resultado"]);
    }

    /// <summary>Zones do not interfere with each other.</summary>
    [Fact]
    public async Task Marks_are_per_zone()
    {
        var (service, _) = Build();

        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Caught, "javi");
        await service.SetAsync(RunId, "ruta-2", "Ruta 2", ZoneOutcome.Fled, "javi");

        var outcomes = await service.GetAsync(RunId);

        Assert.Equal(ZoneOutcome.Caught, outcomes["ruta-1"]);
        Assert.Equal(ZoneOutcome.Fled, outcomes["ruta-2"]);
    }

    /// <summary>Four clicks bring a zone back to where it started.</summary>
    [Theory]
    [InlineData(ZoneOutcome.Free, ZoneOutcome.Caught)]
    [InlineData(ZoneOutcome.Caught, ZoneOutcome.Died)]
    [InlineData(ZoneOutcome.Died, ZoneOutcome.Fled)]
    [InlineData(ZoneOutcome.Fled, ZoneOutcome.Free)]
    public void The_cycle_closes(ZoneOutcome from, ZoneOutcome to) =>
        Assert.Equal(to, ZoneOutcomeService.Next(from));

    /// <summary>
    /// A stored word nobody recognises reads as unmarked.
    /// </summary>
    /// <remarks>
    /// The harmless direction: an unmarked zone invites a look, while guessing at one of the other
    /// three would put a colour on the map that nobody put there.
    /// </remarks>
    [Fact]
    public async Task An_unknown_outcome_reads_as_unmarked()
    {
        var (service, events) = Build();
        await service.SetAsync(RunId, "ruta-1", "Ruta 1", ZoneOutcome.Caught, "javi");

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = RunId,
            Timestamp = DateTimeOffset.UtcNow,
            Type = GameEventType.ZoneOutcomeSet,
            Source = EventSource.Player,
            Actor = "javi",
            Description = "algo",
            LocationId = "ruta-1",
            Data = new Dictionary<string, string> { ["resultado"] = "cualquier-cosa" }
        });

        Assert.DoesNotContain("ruta-1", (await service.GetAsync(RunId)).Keys);
    }

    [Theory]
    [InlineData(ZoneOutcome.Caught, "Atrapado en esta zona")]
    [InlineData(ZoneOutcome.Died, "Primer pokémon matado")]
    [InlineData(ZoneOutcome.Fled, "Primer encuentro: huida")]
    public void Each_state_says_what_it_is(ZoneOutcome outcome, string label) =>
        Assert.Equal(label, ZoneOutcomeService.Label(outcome));
}
