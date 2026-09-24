namespace PermaLocke.Core.Domain;

/// <summary>A Pokémon that belongs to a run, however it got there.</summary>
public sealed record PokemonEntry
{
    public required Guid Id { get; init; }

    public required Guid RunId { get; init; }

    /// <summary>National Pokédex species number.</summary>
    public required int Species { get; init; }

    /// <summary>Species name resolved for display. Cached so the UI does not need the dex.</summary>
    public required string SpeciesName { get; init; }

    public string? Nickname { get; init; }

    public int Level { get; init; }

    public bool IsShiny { get; init; }

    public PokemonStatus Status { get; init; } = PokemonStatus.Alive;

    public required PokemonOrigin Origin { get; init; }

    public required EncounterType EncounterType { get; init; }

    /// <summary>Zone where it was obtained. Null for gacha and wonder trade.</summary>
    public string? LocationId { get; init; }

    public DateTimeOffset ObtainedAt { get; init; }

    public DateTimeOffset? DiedAt { get; init; }

    /// <summary>Event that brought this Pokémon into the run. Lets the viewer link back to the history.</summary>
    public Guid? OriginEventId { get; init; }

    /// <summary>
    /// True when a rule exception (shiny clause, admin override…) allowed this Pokémon in.
    /// The viewer must show it.
    /// </summary>
    public bool ObtainedByRuleException { get; init; }

    /// <summary>
    /// True when this capture used up the zone's single encounter. A shiny caught under the
    /// shiny clause, or a gift, may be configured not to.
    /// </summary>
    public bool ConsumedZoneEncounter { get; init; }

    /// <summary>Personality value from the game, when the Pokémon came from a real save or memory read.</summary>
    public uint? Pid { get; init; }

    /// <summary>The form it arrived in: zero for the ordinary one, 1 for an Alolan Vulpix. §140.</summary>
    public int Form { get; init; }
}
