using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Turns a Pokémon in the save into the run's marker for a dead one.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, used by everything that kills: the roulette's own face and the enforcement
/// that catches deaths the watcher could not write. Two copies of "what happens to a corpse" would
/// drift, and the one that drifted would be whichever nobody looked at.
/// </para>
/// <para>
/// The <b>PID and the encryption constant are left alone</b>, and that is the whole reason this
/// works: they are what the run matches by (§56), so a transformed Pokémon is still recognisably
/// the same entry. Change them and the marker becomes an orphan nobody can tie to anything.
/// </para>
/// </remarks>
public static class DeathMark
{
    /// <summary>Shedinja, which is what the run uses as a headstone.</summary>
    public const int Species = 292;

    /// <summary>Written as the nickname so the player sees it in the box without opening it.</summary>
    public const string Nickname = "MUERTO";

    /// <summary>True when this Pokémon has already been marked.</summary>
    /// <remarks>
    /// Both halves are required. A real Shedinja is a legitimate Pokémon somebody may have caught,
    /// and a nickname alone proves nothing; together they are the shape only this code writes.
    /// </remarks>
    public static bool IsMarked(PK7 pokemon) =>
        pokemon.Species == Species && pokemon.Nickname == Nickname;

    /// <summary>Applies the mark. Does nothing to an already marked Pokémon.</summary>
    public static void Apply(PK7 pokemon)
    {
        if (IsMarked(pokemon))
        {
            return;
        }

        pokemon.Species = Species;
        pokemon.Form = 0;
        pokemon.Ability = 0;
        pokemon.CurrentLevel = 1;

        pokemon.Move1 = pokemon.Move2 = pokemon.Move3 = pokemon.Move4 = 0;
        pokemon.Move1_PP = pokemon.Move2_PP = pokemon.Move3_PP = pokemon.Move4_PP = 0;
        pokemon.Move1_PPUps = pokemon.Move2_PPUps = pokemon.Move3_PPUps = pokemon.Move4_PPUps = 0;

        pokemon.Nickname = Nickname;
        pokemon.IsNicknamed = true;

        // Las estadísticas de combate NO se recalculan aquí a propósito: PKHeX las calcularía con
        // SU tabla de estadísticas base y la ROM lleva las barajadas, así que escribiría números
        // falsos — medido en el §51, donde un Kommo-o pasó de 168 PS a 151. El juego las pone al
        // día solo en cuanto el Pokémon entra en combate.
        pokemon.RefreshChecksum();
    }
}
