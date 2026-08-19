using PermaLocke.Core.Domain;

namespace PermaLocke.Rules;

/// <summary>The encounter that consumed a zone.</summary>
public sealed record ZoneEncounter(
    string LocationId,
    string LocationName,
    int Species,
    string SpeciesName,
    DateTimeOffset RegisteredAt);

/// <summary>
/// Groups species into evolutionary families for the species clause.
/// </summary>
/// <remarks>
/// Deliberately a port with no built-in table. A hand-written vanilla family list would be
/// wrong the moment evolutions are randomized; the real data comes from the randomized ROM
/// in phase 8. Until then <see cref="NullEvolutionLineProvider"/> is used and the species
/// clause stays disabled.
/// </remarks>
public interface IEvolutionLineProvider
{
    /// <summary>False when no family data is available, which rules must check before relying on it.</summary>
    bool HasData { get; }

    /// <summary>Identifier of the family a species belongs to.</summary>
    int GetLineId(int species);
}

/// <summary>Every species is its own family. Honest stand-in until the ROM can be read.</summary>
public sealed class NullEvolutionLineProvider : IEvolutionLineProvider
{
    public bool HasData => false;

    public int GetLineId(int species) => species;
}

/// <summary>
/// Read-only snapshot of the run that rules evaluate against. Exposes state, never services,
/// so a rule cannot change anything as a side effect of being asked a question.
/// </summary>
public sealed class RuleContext
{
    public required IReadOnlyList<PokemonEntry> Pokemon { get; init; }

    /// <summary>Zones whose encounter has already been used, keyed by location id.</summary>
    public required IReadOnlyDictionary<string, ZoneEncounter> UsedZones { get; init; }

    public required RulesConfiguration Configuration { get; init; }

    public IEvolutionLineProvider EvolutionLines { get; init; } = new NullEvolutionLineProvider();

    /// <summary>Level cap of the current stage, or null when no stage has been reached yet.</summary>
    public int? LevelCap { get; init; }

    /// <summary>Species already obtained, whatever their current status.</summary>
    public bool HasSpecies(int species) => Pokemon.Any(p => p.Species == species);

    public PokemonEntry? FindBySpecies(int species) => Pokemon.FirstOrDefault(p => p.Species == species);

    /// <summary>Whether this kind of encounter uses up the zone's single chance.</summary>
    public bool ConsumesZone(EncounterType type) =>
        Configuration.EncounterTypesThatConsumeZone.Contains(type);
}
