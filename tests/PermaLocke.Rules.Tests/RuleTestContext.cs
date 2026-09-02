using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;
using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using PermaLocke.Rules.Rules;

namespace PermaLocke.Rules.Tests;

/// <summary>Builds engines and contexts for the tests without repeating the wiring everywhere.</summary>
internal static class RuleTestContext
{
    public static RuleEngine Engine() => new(
    [
        new FirstEncounterRule(),
        new ShinyClauseRule(),
        new DupesClauseRule(),
        new SpeciesClauseRule(),
        new GiftPokemonRule(),
        new StaticEncounterRule(),
        new LevelCapRule()
    ]);

    public static RuleContext Context(
        RulesConfiguration? configuration = null,
        IEnumerable<PokemonEntry>? pokemon = null,
        IEnumerable<ZoneEncounter>? usedZones = null,
        int? levelCap = null,
        IEvolutionLineProvider? evolutionLines = null) => new()
    {
        Configuration = configuration ?? RulesConfiguration.Default,
        Pokemon = [.. pokemon ?? []],
        UsedZones = (usedZones ?? []).ToDictionary(z => z.LocationId),
        LevelCap = levelCap,
        EvolutionLines = evolutionLines ?? new NullEvolutionLineProvider()
    };

    public static PokemonEntry Caught(int species, string name, string? location = null) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.Empty,
        Species = species,
        SpeciesName = name,
        Origin = PokemonOrigin.Capture,
        EncounterType = EncounterType.Wild,
        LocationId = location,
        ObtainedAt = DateTimeOffset.UtcNow
    };

    public static ZoneEncounter Zone(string id, string name, int species, string speciesName) =>
        new(id, name, species, speciesName, DateTimeOffset.UtcNow);

    public static RulesConfiguration With(params (string RuleId, RuleSettings Settings)[] overrides)
    {
        var rules = RulesConfiguration.Default.Rules.ToDictionary(p => p.Key, p => p.Value);

        foreach (var (ruleId, settings) in overrides)
        {
            rules[ruleId] = settings;
        }

        return RulesConfiguration.Default with { Rules = rules };
    }
}

// Los dobles compartidos por las pruebas de servicios. Estaban dentro de EncounterServiceTests
// como privados, y la primera prueba que los necesitó desde otro fichero no podía verlos.
internal sealed class StepClock : IClock
    {
    private DateTimeOffset _now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now => _now = _now.AddSeconds(1);
    }

internal sealed class FakeEvents : IEventStore
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

    public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
        Task.FromResult(new IntegrityReport(true, All.Count, null, null));
    }

internal sealed class FakePokemon : IPokemonRepository
    {
    private readonly List<PokemonEntry> _entries = [];

    public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PokemonEntry>>([.. _entries.Where(p => p.RunId == runId)]);

    public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
        Task.FromResult(_entries.FirstOrDefault(p => p.Id == pokemonId));

    public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>

        throw new NotSupportedException();


    public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
    {
        _entries.RemoveAll(p => p.Id == pokemon.Id);
        _entries.Add(pokemon);
        return Task.CompletedTask;
    }
    }
