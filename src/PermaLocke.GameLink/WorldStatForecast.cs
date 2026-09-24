using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Works out a Pokémon's stats with other EVs, from the installed world's own base stats.
/// </summary>
/// <remarks>
/// <para>
/// The same formula and the same table <see cref="SaveEvTrainer"/> uses when it rewrites a party
/// member's stats, so what the training screen promises is what the writer then puts on disk, and
/// what the game shows when a boxed one comes out of the PC.
/// </para>
/// <para>
/// By species <b>and form</b>: an Alolan Raichu is not built like a Raichu. Until §131 the world table
/// only knew species, and this answered null for any other form rather than project from the wrong row.
/// </para>
/// </remarks>
public sealed class WorldStatForecast : IStatForecast
{
    /// <summary>Shedinja, whose PS the game forces to one whatever the formula says.</summary>
    private const int Shedinja = 292;

    public IReadOnlyList<int>? With(BoxedPokemon pokemon, IReadOnlyList<int> evs)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(evs);

        if (pokemon.IsEgg || !pokemon.IsIntact || pokemon.Nature < 0
            || evs.Count != StatCalculator.Count || pokemon.Ivs.Count != StatCalculator.Count)
        {
            return null;
        }

        if (WorldLimits.BaseStatsOf(pokemon.Species, pokemon.Form) is not { } bases)
        {
            return null;
        }

        // El nivel con el que el escritor recalcula (el guardado junto a las estadísticas, en el equipo),
        // no el de la experiencia: si no, la pantalla promete un número y en la partida se escribe otro.
        return StatCalculator.Compute(bases, pokemon.Ivs, evs, pokemon.LevelForStats, pokemon.Nature,
            pokemon.Species == Shedinja);
    }

    public int NatureEffect(BoxedPokemon pokemon, int stat)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        var factor = StatCalculator.NatureFactor(pokemon.Nature, stat);

        return factor > 1 ? 1 : factor < 1 ? -1 : 0;
    }
}
