namespace PermaLocke.Core.Abstractions;

/// <summary>
/// What a Pokémon's six stats would be with a given set of EVs, worked out the way the game does.
/// </summary>
/// <remarks>
/// <para>
/// A port because the answer needs the base stats of <b>the installed world</b>, not PKHeX's: this
/// run is played with <c>shuffleBaseStats</c> on, so a stat worked out from the vanilla table is a
/// wrong number with a straight face (168 PS read back as 151, §51). Whoever implements this knows
/// where that table lives; the screen does not.
/// </para>
/// <para>
/// It answers <c>null</c> rather than guess. No table, an egg, or an entry broken in the save: in all
/// three the honest thing on screen is no number at all. A form is not one of them: it is worked out
/// from its own base stats when it has them, which is what the game does.
/// </para>
/// </remarks>
public interface IStatForecast
{
    /// <summary>
    /// The six stats in HP/Atk/Def/SpA/SpD/Spe order with <paramref name="evs"/> in place of the
    /// Pokémon's own, or null when they cannot be known.
    /// </summary>
    IReadOnlyList<int>? With(BoxedPokemon pokemon, IReadOnlyList<int> evs);

    /// <summary>
    /// What the Pokémon's nature does to one stat of the summary screen: +1 raises it, -1 lowers
    /// it, 0 leaves it alone. PS are never touched, and neither is anything when the nature is
    /// unknown.
    /// </summary>
    int NatureEffect(BoxedPokemon pokemon, int stat);
}
