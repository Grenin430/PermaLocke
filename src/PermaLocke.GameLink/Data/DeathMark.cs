using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Marks a Pokémon in the save as one of the run's dead: no HP left, and nothing else changed.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, used by everything that kills — the roulette's own face and the enforcement
/// that writes the run's dead into the save. Two copies of "what happens to a corpse" would drift,
/// and the one that drifted would be whichever nobody looked at.
/// </para>
/// <para>
/// It used to turn the Pokémon into a Shedinja called MUERTO. The player asked for that to go: a
/// Shedinja destroys the Pokémon and cannot be undone, and a run is more legible when the dead are
/// still recognisably themselves. So the mark is now the absence of HP, which is already what the
/// game means by fainted — species, nickname, moves, level, ribbons and stats all stay.
/// </para>
/// <para>
/// <b>It only works in the party</b>, and that is not a detail to leave implied. A boxed Pokémon
/// does not carry battle stats at all (§32): writing zero into one changes nothing the game reads,
/// and it comes out of the box at full health. So <see cref="WorksIn"/> exists and callers have to
/// ask, because a marker that silently does nothing is worse than no marker.
/// </para>
/// <para>
/// The <b>PID and the encryption constant are left alone</b>, which is what makes this usable at
/// all: they are what the run matches by (§56), so a marked Pokémon is still the same entry.
/// </para>
/// </remarks>
public static class DeathMark
{
    /// <summary>Whether the mark means anything where this Pokémon is stored.</summary>
    /// <param name="box">
    /// <see cref="BoxedPokemon.PartyBox"/> for the party, or a box number. Only the party keeps the
    /// battle stats the mark is written into.
    /// </param>
    public static bool WorksIn(int box) => box == BoxedPokemon.PartyBox;

    /// <summary>True when this Pokémon is already on the floor.</summary>
    public static bool IsMarked(PK7 pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        return pokemon.Stat_HPCurrent == 0;
    }

    /// <summary>Applies the mark. Does nothing to one already down.</summary>
    public static void Apply(PK7 pokemon)
    {
        if (IsMarked(pokemon))
        {
            return;
        }

        pokemon.Stat_HPCurrent = 0;

        // El estado se va con los PS porque eso es desmayarse: uno en el suelo no está además
        // envenenado, y dejarlo sería dejar la partida en un estado que el juego no escribe nunca.
        pokemon.Status_Condition = 0;

        // Las estadísticas de combate NO se recalculan aquí a propósito: PKHeX las calcularía con
        // SU tabla de estadísticas base y la ROM lleva las barajadas, así que escribiría números
        // falsos — medido en el §51, donde un Kommo-o pasó de 168 PS a 151.
        pokemon.RefreshChecksum();
    }
}
