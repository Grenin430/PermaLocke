using System.Windows.Media;

namespace PermaLocke.App.Services;

/// <summary>
/// The colours the series has used for the types since forever, by the game's type number.
/// </summary>
/// <remarks>
/// One table for every screen that paints a type: the wonder trade had its own copy, and the move reminder needs the
/// same eighteen. Two copies of a palette drift the first time somebody adjusts one of them.
/// </remarks>
public static class TypePalette
{
    // La tabla vive en TypeColours, sin pinceles, para que las cartas del álbum se prueben sin WPF (§187).
    private static readonly SolidColorBrush[] Brushes = [.. TypeColours.Hex.Select(Frozen)];

    private static readonly SolidColorBrush Unknown = Frozen("#FF" + TypeColours.UnknownHex[1..]);

    /// <summary>The type's colour, or a neutral grey for a type nobody could read.</summary>
    public static Color ColourOf(int type)
    {
        var colour = TypeColours.Of(type);
        return Color.FromRgb(colour.R, colour.G, colour.B);
    }

    public static SolidColorBrush BrushOf(int type) =>
        type >= 0 && type < Brushes.Length ? Brushes[type] : Unknown;

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
