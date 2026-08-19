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
