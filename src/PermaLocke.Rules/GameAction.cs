using PermaLocke.Core.Domain;

namespace PermaLocke.Rules;

/// <summary>
/// Something the player is trying to do. Rules never care whether it was typed into
/// PermaLocke or detected from the running game, which is what lets the same rule set
/// validate manual input today and automatic detection later.
/// </summary>
public abstract record GameAction;

/// <param name="LocationId">Stable zone identifier; <paramref name="LocationName"/> is for display.</param>
public sealed record AttemptCapture(
    int Species,
    string SpeciesName,
    string LocationId,
    string LocationName,
    EncounterType EncounterType,
    bool IsShiny = false,
    int Level = 0) : GameAction;

/// <summary>A Pokémon reaching a level, checked against the cap of the current stage.</summary>
public sealed record LevelCheck(Guid PokemonId, string SpeciesName, int Level) : GameAction;
