using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>Whether the player may have their Poké Balls right now.</summary>
public enum BallAction
{
    GiveBack,
    Withhold
}

/// <summary>Everything the decision needs, already read from the game and the run.</summary>
/// <param name="Zone">Where the player is, or where the battle is happening; null when it cannot be known.</param>
/// <param name="IsRoute">The zone is one of the routes of the map screen, which the player placed by hand.</param>
/// <param name="Spent">The zone's single encounter was already used, before this battle.</param>
/// <param name="InWildBattle">A wild battle is in progress.</param>
/// <param name="WildSpecies">The wild Pokémon, once the battle tables have been read.</param>
/// <param name="Shiny">The game counted a shiny encounter when this battle started.</param>
/// <param name="Duplicate">The wild Pokémon's evolutionary line is already caught.</param>
/// <param name="SpeciesOverdue">The battle has gone on long enough without the species being read.</param>
/// <param name="AllowedStatic">
/// What the wild Pokémon is when it is one of the static captures the competition allows in this zone — the Necrozma
/// of Monte Lanakila, the four Tapus — or null. Always capturable, and never spends the zone (§119).
/// </param>
/// <param name="HasAllowedStatic">The zone holds one of those captures, whatever this battle turns out to be.</param>
public sealed record EncounterSituation(
    FieldZone? Zone,
    bool IsRoute,
    bool Spent,
    bool InWildBattle,
    int? WildSpecies = null,
    bool Shiny = false,
    bool Duplicate = false,
    bool SpeciesOverdue = false,
    string? AllowedStatic = null,
    bool HasAllowedStatic = false,
    string? PendingTrial = null);

/// <param name="SpendZone">This battle uses up the zone's encounter.</param>
/// <param name="Reason">In Spanish, for the event and the log.</param>
public sealed record EncounterDecision(BallAction Action, bool SpendZone, string Reason);

/// <summary>
/// The competition's first-encounter rule, as a decision with no side effects.
/// </summary>
/// <remarks>
/// <para>
/// What the player decided on 2026-09-14 (§117): the first wild battle in a route spends it, whatever happens in
/// it; a duplicate — a Pokémon whose evolutionary line is already caught — cannot be caught and does not spend
/// the route; a shiny can always be caught. The routes are the ones the player placed on the map screen.
/// </para>
/// <para>
/// And what the player added on 2026-09-14 (§119): <b>outside the routes of the map there is nothing to catch, ever</b>.
/// The places left off the map were left off on purpose — the volcano tunnel, the towns — so in them the balls are
/// taken away, walking and in battle. The exceptions are the static captures the competition allows, each in its
/// own place: the Necrozma of Monte Lanakila and the four Tapus in their ruins. Those can always be caught and spend
/// nothing. A shiny, as before, can always be caught.
/// </para>
/// <para>
/// Pure so every combination can be tested without an emulator. The one doubt still resolved in the player's
/// favour is not knowing where they are at all: taking balls away on a reading PermaLocke does not have would take
/// them everywhere the zone reader fails. Everything else that is uncertain — a battle whose species is not read yet
/// — keeps them away until it is known.
/// </para>
/// </remarks>
public static class EncounterPolicy
{
    public static EncounterDecision Decide(EncounterSituation situation, bool shinyConsumesEncounter)
    {
        ArgumentNullException.ThrowIfNull(situation);

        if (situation.Zone is not { } zone)
        {
            return new(BallAction.GiveBack, false, "No se sabe en qué zona estás.");
        }

        if (situation.InWildBattle && situation.AllowedStatic is { } allowed)
        {
            return new(BallAction.GiveBack, false, $"{allowed} en {zone.LocationName}: captura permitida.");
        }

        if (!situation.IsRoute)
        {
            return OutsideTheMap(situation, zone);
        }

        // Una zona de prueba sin su cristal Z: aquí aún no se captura (§179). Un variocolor, sí.
        var trialClosed = situation.PendingTrial is { } trial
            ? new EncounterDecision(BallAction.Withhold, false, $"{zone.LocationName}: primero supera la prueba ({trial}). Con el cristal Z ya se podrá capturar aquí.")
            : null;

        if (!situation.InWildBattle && trialClosed is not null)
        {
            return trialClosed;
        }

        if (!situation.InWildBattle)
        {
            return situation.Spent
                ? new(BallAction.Withhold, false, $"{zone.LocationName} ya gastó su encuentro.")
                : new(BallAction.GiveBack, false, $"{zone.LocationName} conserva su encuentro.");
        }

        // Un variocolor se puede atrapar siempre, gastada o no.
        if (situation.Shiny)
        {
            return new(BallAction.GiveBack, !situation.Spent && shinyConsumesEncounter,
                $"Variocolor en {zone.LocationName}: se puede capturar.");
        }

        if (trialClosed is not null)
        {
            return trialClosed;
        }

        if (situation.Spent)
        {
            return new(BallAction.Withhold, false, $"{zone.LocationName} ya gastó su encuentro.");
        }

        if (situation.WildSpecies is null)
        {
            // Mientras no se sabe qué es, sin Poké Balls: un duplicado no se puede lanzar en el segundo que
            // tarda en leerse. Si no llega a leerse, la duda es del jugador.
            return situation.SpeciesOverdue
                ? new(BallAction.GiveBack, true, $"Primer encuentro en {zone.LocationName}.")
                : new(BallAction.Withhold, false, $"Primer encuentro en {zone.LocationName}.");
        }

        if (situation.Duplicate)
        {
            return new(BallAction.Withhold, false, $"Duplicado en {zone.LocationName}: la zona sigue libre.");
        }

        return new(BallAction.GiveBack, true, $"Primer encuentro en {zone.LocationName}.");
    }

    /// <summary>A place the player left off the map: no balls, except a shiny or an allowed static capture.</summary>
    private static EncounterDecision OutsideTheMap(EncounterSituation situation, FieldZone zone)
    {
        var closed = $"{zone.LocationName} no está en el mapa: aquí no se captura.";

        if (!situation.InWildBattle)
        {
            return new(BallAction.Withhold, false, closed);
        }

        if (situation.Shiny)
        {
            return new(BallAction.GiveBack, false, $"Variocolor en {zone.LocationName}: se puede capturar.");
        }

        // Donde vive una captura permitida, un combate cuya especie no llega a leerse puede ser ella: la duda, del
        // jugador. En el resto de sitios fuera del mapa no hay nada que dudar.
        return situation is { WildSpecies: null, SpeciesOverdue: true, HasAllowedStatic: true }
            ? new(BallAction.GiveBack, false, $"Combate en {zone.LocationName}.")
            : new(BallAction.Withhold, false, closed);
    }

    /// <summary>
    /// How a wild battle ended, from the game's counters at its start and after it, for the mark on the map.
    /// </summary>
    /// <param name="wildFainted">The battle tables saw the wild Pokémon reach zero.</param>
    /// <returns>The outcome and, in Spanish, how it was read.</returns>
    /// <remarks>
    /// <para>
    /// A capture and a flee are the game's own counts, so they go first. A KO is what PermaLocke saw in the battle
    /// tables.
    /// </para>
    /// <para>
    /// Anything else is marked as <see cref="ZoneOutcome.Fled"/>: the wild Pokémon teleported or was blown away, or
    /// the player lost, or the tables missed the KO. None of those leaves a Pokémon behind, and leaving the route
    /// unmarked would have the map say «sin marcar» about a route the rule already treats as spent — with nobody
    /// able to mark it by hand any more (§118). All four outcomes spend the route equally, so a wrong one costs a
    /// label, and the reason says exactly what was read.
    /// </para>
    /// </remarks>
    public static (ZoneOutcome Outcome, string How) Ending(BattleCounters start, BattleCounters end, bool wildFainted)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        if (end.Caught > start.Caught)
        {
            return (ZoneOutcome.Caught, "Capturado.");
        }

        if (end.Fled > start.Fled)
        {
            return (ZoneOutcome.Fled, "Huida.");
        }

        return wildFainted
            ? (ZoneOutcome.Died, "El salvaje se debilitó.")
            : (ZoneOutcome.Fled, "Se escapó o perdiste el combate.");
    }
}
