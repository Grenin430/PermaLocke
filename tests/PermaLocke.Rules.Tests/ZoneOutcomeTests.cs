using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The map's zone marks: how each zone's one encounter ended.
/// </summary>
/// <remarks>
/// Since §118 PermaLocke is the only thing that marks the map. Runs from before carry marks the player clicked,
/// and what these tests guard hardest is that one of those can never undo or overwrite a detected one.
/// </remarks>
public sealed class ZoneOutcomeTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static (ZoneOutcomeService Service, FakeEvents Events) Build()
    {
        var events = new FakeEvents();
        return (new ZoneOutcomeService(events, new StepClock()), events);
    }

    private static Task Detect(ZoneOutcomeService service, string zone, ZoneOutcome outcome, string? species = null) =>
        service.SetAsync(RunId, zone, zone, outcome, "javi", EventSource.AutoDetect, species);

    private static Task Click(ZoneOutcomeService service, string zone, ZoneOutcome outcome) =>
        service.SetAsync(RunId, zone, zone, outcome, "javi", EventSource.Player);

    [Fact]
    public async Task A_mark_is_what_the_zone_reads_as()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Caught);

        Assert.Equal(ZoneOutcome.Caught, (await service.GetAsync(RunId))["ruta-1"]);
    }

    [Fact]
    public async Task A_detected_mark_says_so_and_against_what()
    {
        var (service, events) = Build();

        await Detect(service, "ruta-2", ZoneOutcome.Fled, "Lillipup");

        var mark = (await service.GetMarksAsync(RunId))["ruta-2"];
        Assert.False(mark.ByPlayer);
        Assert.Equal("Lillipup", mark.Species);

        var stored = Assert.Single(events.All);
        Assert.Equal(EventSource.AutoDetect, stored.Source);
        Assert.Equal("huida", stored.Data["resultado"]);
        Assert.Contains("Lillipup", stored.Description);
    }

    /// <summary>Marks clicked before §118 stay in the run, and say they were clicked.</summary>
    [Fact]
    public async Task An_old_manual_mark_still_counts_and_is_labelled_as_manual()
    {
        var (service, _) = Build();

        await Click(service, "ruta-3", ZoneOutcome.Died);

        var mark = (await service.GetMarksAsync(RunId))["ruta-3"];
        Assert.Equal(ZoneOutcome.Died, mark.Outcome);
        Assert.True(mark.ByPlayer);
    }

    [Fact]
    public async Task A_manual_mark_cannot_overwrite_a_detected_one()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Died);
        await Click(service, "ruta-1", ZoneOutcome.Caught);

        var mark = (await service.GetMarksAsync(RunId))["ruta-1"];
        Assert.Equal(ZoneOutcome.Died, mark.Outcome);
        Assert.False(mark.ByPlayer);
    }

    [Fact]
    public async Task A_manual_clear_cannot_free_a_detected_zone()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Fled);
        await Click(service, "ruta-1", ZoneOutcome.Free);

        Assert.Equal(ZoneOutcome.Fled, (await service.GetAsync(RunId))["ruta-1"]);
    }

    /// <summary>What PermaLocke detected replaces a guess clicked before it.</summary>
    [Fact]
    public async Task A_detected_mark_replaces_an_earlier_manual_one()
    {
        var (service, _) = Build();

        await Click(service, "ruta-1", ZoneOutcome.Caught);
        await Detect(service, "ruta-1", ZoneOutcome.Fled);

        Assert.Equal(ZoneOutcome.Fled, (await service.GetAsync(RunId))["ruta-1"]);
    }

    /// <summary>Among old manual marks, the state of a zone is still the last thing said about it.</summary>
    [Fact]
    public async Task Among_manual_marks_the_latest_wins_and_clearing_unmarks()
    {
        var (service, _) = Build();

        await Click(service, "ruta-1", ZoneOutcome.Caught);
        await Click(service, "ruta-1", ZoneOutcome.Died);
        await Click(service, "ruta-2", ZoneOutcome.Fled);
        await Click(service, "ruta-2", ZoneOutcome.Free);

        var outcomes = await service.GetAsync(RunId);

        Assert.Equal(ZoneOutcome.Died, outcomes["ruta-1"]);
        Assert.DoesNotContain("ruta-2", outcomes.Keys);
    }

    [Fact]
    public async Task Marks_are_per_zone()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Caught);
        await Click(service, "ruta-2", ZoneOutcome.Fled);

        var outcomes = await service.GetAsync(RunId);

        Assert.Equal(ZoneOutcome.Caught, outcomes["ruta-1"]);
        Assert.Equal(ZoneOutcome.Fled, outcomes["ruta-2"]);
    }

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
        await Click(service, "ruta-1", ZoneOutcome.Caught);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = RunId,
            Timestamp = DateTimeOffset.UtcNow.AddDays(1),
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

    /// <summary>
    /// Lo que no se podía hacer y costó la Ruta 1 el 2026-09-21: PermaLocke marcó la zona de al lado y no había
    /// forma de quitarlo. Liberar es la única corrección que sí puede con una marca detectada.
    /// </summary>
    [Fact]
    public async Task Clearing_takes_down_a_mark_that_permalocke_itself_detected()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1-afueras-de-hauoli", ZoneOutcome.Fled, "Snubbull");
        Assert.Contains("ruta-1-afueras-de-hauoli", (await service.GetAsync(RunId)).Keys);

        await service.ClearAsync(RunId, "ruta-1-afueras-de-hauoli", "Ruta 1 (Afueras de Hauoli)", "javi",
            "se leyó la ruta de al lado");

        Assert.DoesNotContain("ruta-1-afueras-de-hauoli", (await service.GetAsync(RunId)).Keys);
    }

    [Fact]
    public async Task Clearing_is_an_addition_to_the_chain_with_who_and_why()
    {
        var (service, events) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Fled);
        await service.ClearAsync(RunId, "ruta-1", "Ruta 1", "javi", "la leyó mal");

        var all = await events.GetAllAsync(RunId);

        Assert.Equal(2, all.Count);   // nada se borra
        var cleared = all.Single(e => e.Type == GameEventType.ZoneCleared);
        Assert.Equal(EventSource.Player, cleared.Source);
        Assert.Equal("la leyó mal", cleared.Reason);
        Assert.Equal("ruta-1", cleared.LocationId);
    }

    [Fact]
    public async Task A_zone_freed_and_then_met_again_is_marked_again()
    {
        var (service, _) = Build();

        await Detect(service, "ruta-1", ZoneOutcome.Fled);
        await service.ClearAsync(RunId, "ruta-1", "Ruta 1", "javi", "la leyó mal");
        await Detect(service, "ruta-1", ZoneOutcome.Caught, "Tepig");

        Assert.Equal(ZoneOutcome.Caught, (await service.GetAsync(RunId))["ruta-1"]);
    }

    [Fact]
    public async Task Clearing_needs_a_reason()
    {
        var (service, _) = Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.ClearAsync(RunId, "ruta-1", "Ruta 1", "javi", "  "));
    }
}
