using System.Globalization;
using System.Windows.Data;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Converters;

/// <summary>
/// The line under a section's name in the start menu (2026-10-01): what it is for, or, for a group, the pages it holds.
/// Presentation only: the names are the sections' own, the sentences are interface text.
/// </summary>
public sealed class SectionCaptionConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Captions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HOME"] = "Tu run, tu equipo y tus pruebas",
        ["GACHA"] = "Tira y consigue Pokémon",
        ["TIENDA"] = "Gasta tus puntos en objetos",
        ["MAPA"] = "Las islas de Alola",
        ["RULETA"] = "Un giro de feria",
        ["CONFIGURACIÓN"] = "Ventana, avisos y torneo"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            GroupSectionViewModel group => string.Join(" · ", group.Pages.Select(page => page.Title).Take(3))
                + (group.Pages.Count > 3 ? $" · +{group.Pages.Count - 3}" : string.Empty),
            SectionViewModel section when Captions.TryGetValue(section.Title, out var caption) => caption,
            SectionViewModel section => section.Subtitle,
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
