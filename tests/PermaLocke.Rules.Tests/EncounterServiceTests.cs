using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;
using static PermaLocke.Rules.Tests.RuleTestContext;

namespace PermaLocke.Rules.Tests;

public sealed class EncounterServiceTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static (EncounterService Service, FakeEvents Events, FakePokemon Pokemon) Build(
        RulesConfiguration? configuration = null)
    {
        var events = new FakeEvents();
        var pokemon = new FakePokemon();
        var service = new EncounterService(
            Engine(), pokemon, events,
            configuration ?? RulesConfiguration.Default,
            new NullEvolutionLineProvider(),
            new LevelCapTable([]),
            new PermaLocke.Core.Services.RunContext(),
            new StepClock());

        return (service, events, pokemon);
    }

    private static RegisterCaptureRequest Request(
        int species = 731, string name = "Pikipek", string location = "Ruta 1",
        EncounterType type = EncounterType.Wild, bool shiny = false, bool force = false) =>
        new(species, name, location, type, shiny, Level: 5, Nickname: null, Force: force);

    [Fact]
    public async Task A_first_capture_is_stored_and_recorded()
    {
        var (service, events, pokemon) = Build();

        var result = await service.RegisterAsync(RunId, Request(), "javi");

        Assert.True(result.Registered);
        Assert.Equal(RuleOutcome.Allowed, result.Evaluation.Outcome);

        var stored = Assert.Single(await pokemon.GetAllAsync(RunId));
        Assert.Equal("Pikipek", stored.SpeciesName);
        Assert.Equal("ruta-1", stored.LocationId);
        Assert.True(stored.ConsumedZoneEncounter);
        Assert.False(stored.ObtainedByRuleException);

        var caught = Assert.Single(events.All, e => e.Type == GameEventType.PokemonCaught);
        Assert.Equal(stored.OriginEventId, caught.Id);
        Assert.Equal("ruta-1", caught.LocationId);
    }

    [Fact]
    public async Task A_second_capture_in_the_same_zone_is_refused_and_stores_nothing()
    {
        var (service, events, pokemon) = Build();
        await service.RegisterAsync(RunId, Request(), "javi");

        var result = await service.RegisterAsync(RunId, Request(19, "Rattata"), "javi");

        Assert.False(result.Registered);
        Assert.True(result.Evaluation.IsBlocked);
        Assert.Single(await pokemon.GetAllAsync(RunId));
        Assert.DoesNotContain(events.All, e => e.Description.Contains("Rattata")
                                               && e.Type == GameEventType.PokemonCaught);
    }

    [Fact]
    public async Task Zone_names_are_normalised_so_a_typo_cannot_bypass_the_rule()
    {
        var (service, _, _) = Build();
        await service.RegisterAsync(RunId, Request(location: "Ruta 1"), "javi");

        var result = await service.RegisterAsync(RunId, Request(19, "Rattata", "  RUTA   1 "), "javi");

        Assert.False(result.Registered);
    }

    [Fact]
    public async Task Forcing_a_blocked_capture_stores_it_and_leaves_a_violation()
    {
        var (service, events, pokemon) = Build();
        await service.RegisterAsync(RunId, Request(), "javi");

        var result = await service.RegisterAsync(RunId, Request(19, "Rattata", force: true), "javi");

        Assert.True(result.Registered);
        Assert.Equal(2, (await pokemon.GetAllAsync(RunId)).Count);

        var violation = Assert.Single(events.All, e => e.Type == GameEventType.RuleViolation);
        Assert.Contains("Registro forzado", violation.Description);
        Assert.Equal("sí", violation.Data["forzado"]);
    }

    [Fact]
    public async Task A_refused_capture_is_audited_when_the_rule_is_set_to_BlockAndLog()
    {
        var (service, events, _) = Build(
            With((RuleIds.FirstEncounter, new RuleSettings { Mode = RuleMode.BlockAndLog })));

        await service.RegisterAsync(RunId, Request(), "javi");
        await service.RegisterAsync(RunId, Request(19, "Rattata"), "javi");

        var violation = Assert.Single(events.All, e => e.Type == GameEventType.RuleViolation);
        Assert.Contains("Intento bloqueado", violation.Description);
        Assert.Equal("no", violation.Data["forzado"]);
    }

    [Fact]
    public async Task A_shiny_in_a_spent_zone_is_registered_as_an_exception()
    {
        var (service, events, pokemon) = Build();
        await service.RegisterAsync(RunId, Request(), "javi");

        var result = await service.RegisterAsync(RunId, Request(19, "Rattata", shiny: true), "javi");

        Assert.True(result.Registered);
        Assert.True(result.Evaluation.IsException);

        var shiny = (await pokemon.GetAllAsync(RunId)).Single(p => p.SpeciesName == "Rattata");
        Assert.True(shiny.ObtainedByRuleException);

        // The shiny clause is configured not to consume the encounter, so the zone keeps
        // pointing at the original capture.
        Assert.False(shiny.ConsumedZoneEncounter);

        var exception = Assert.Single(events.All, e => e.Type == GameEventType.RuleException);
        Assert.Equal(RuleIds.ShinyClause, exception.Data["regla"]);
        Assert.Contains(RuleIds.FirstEncounter, exception.Data["reglasLevantadas"]);
    }

    [Fact]
    public async Task A_gift_Pokemon_does_not_consume_the_zone()
    {
        var (service, _, pokemon) = Build();

        await service.RegisterAsync(RunId, Request(722, "Rowlet", "Pueblo Iki", EncounterType.Starter), "javi");

        var stored = Assert.Single(await pokemon.GetAllAsync(RunId));
        Assert.False(stored.ConsumedZoneEncounter);
        Assert.Equal(PokemonOrigin.Starter, stored.Origin);
    }

    [Fact]
    public async Task A_gift_does_not_block_a_later_wild_encounter_in_the_same_zone()
    {
        var (service, _, _) = Build();
        await service.RegisterAsync(RunId, Request(722, "Rowlet", "Ruta 1", EncounterType.Gift), "javi");

        var result = await service.RegisterAsync(RunId, Request(location: "Ruta 1"), "javi");

        Assert.True(result.Registered);
    }

    [Fact]
    public async Task Preview_evaluates_without_writing_anything()
    {
        var (service, events, pokemon) = Build();

        var evaluation = await service.PreviewAsync(RunId, Request());

        Assert.Equal(RuleOutcome.Allowed, evaluation.Outcome);
        Assert.Empty(events.All);
        Assert.Empty(await pokemon.GetAllAsync(RunId));
    }

    [Fact]
    public async Task Known_locations_come_back_for_the_zone_suggestions()
    {
        var (service, _, _) = Build();
        await service.RegisterAsync(RunId, Request(location: "Ruta 1"), "javi");
        await service.RegisterAsync(RunId, Request(19, "Rattata", "Ciudad Hauoli"), "javi");

        var locations = await service.GetKnownLocationsAsync(RunId);

        Assert.Equal(["ruta-1", "ciudad-hauoli"], locations);
    }

    [Theory]
    [InlineData("Ruta 1", "ruta-1")]
    [InlineData("  RUTA   1 ", "ruta-1")]
    [InlineData("Ciudad Hau'oli", "ciudad-hau-oli")]
    [InlineData("Montaña Lanakila", "montana-lanakila")]
    public void Location_ids_are_stable_across_spelling(string input, string expected) =>
        Assert.Equal(expected, EncounterService.NormaliseLocationId(input));

    private sealed class StepClock : IClock
    {
        private DateTimeOffset _now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Now => _now = _now.AddSeconds(1);
    }

    private sealed class FakeEvents : IEventStore
    {
        public List<GameEvent> All { get; } = [];

        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
        {
            var previous = All.LastOrDefault(e => e.RunId == gameEvent.RunId)?.Hash ?? string.Empty;
            var sealedEvent = EventHasher.Seal(gameEvent, previous);
            All.Add(sealedEvent);
            return Task.FromResult(sealedEvent);
        }

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>([.. All.Where(e => e.RunId == runId)]);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>([.. All.Where(e => e.RunId == runId).TakeLast(count)]);

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult(new IntegrityReport(true, All.Count, null, null));
    }

    private sealed class FakePokemon : IPokemonRepository
    {
        private readonly List<PokemonEntry> _entries = [];

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([.. _entries.Where(p => p.RunId == runId)]);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult(_entries.FirstOrDefault(p => p.Id == pokemonId));

        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
        {
            _entries.RemoveAll(p => p.Id == pokemon.Id);
            _entries.Add(pokemon);
            return Task.CompletedTask;
        }
    }
}
