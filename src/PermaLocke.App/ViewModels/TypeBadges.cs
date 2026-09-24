using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The one or two type plates of a Pokémon, for every screen that shows whom you picked: the viewer, ENTRENAR EV and
/// MOVIMIENTOS (§176).
/// </summary>
/// <remarks>
/// From <see cref="ITypeLookup"/>, which reads the installed world, so a form with its own types — an Alolan Vulpix —
/// shows its own. An egg shows none: the game does not say what is inside either.
/// </remarks>
public static class TypeBadges
{
    public static IReadOnlyList<ViewerTypeBadge> For(ITypeLookup types, BoxedPokemon? pokemon)
    {
        if (pokemon is null || pokemon.IsEgg)
        {
            return [];
        }

        var pair = types.GetTypes(pokemon.Species, pokemon.Form);
        var badges = new List<ViewerTypeBadge> { new(pair.FirstName.ToUpperInvariant(), Colour(pair.First)) };
        if (pair.IsDual)
        {
            badges.Add(new ViewerTypeBadge(pair.SecondName.ToUpperInvariant(), Colour(pair.Second)));
        }

        return badges;
    }

    /// <summary>The type's colour a step darker, so white letters read on the light ones (Normal, Eléctrico, Hielo).</summary>
    public static System.Windows.Media.Color Colour(int type)
    {
        var colour = TypePalette.ColourOf(type);
        return System.Windows.Media.Color.FromRgb((byte)(colour.R * 0.82), (byte)(colour.G * 0.82), (byte)(colour.B * 0.82));
    }
}
