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
    private static readonly string[] Hex =
    [
        "#9FA19F", "#FF8000", "#81B9EF", "#9141CB", "#915121", "#AFA981", "#91A119", "#704170",
        "#60A1B8", "#E62829", "#2980EF", "#3FA129", "#FAC000", "#EF4179", "#3DCEF3", "#5060E1",
        "#624D4E", "#EF70EF", "#2E9AA0"
    ];

    private static readonly SolidColorBrush[] Brushes = [.. Hex.Select(Frozen)];

    private static readonly SolidColorBrush Unknown = Frozen("#FF5A5470");

    /// <summary>The type's colour, or a neutral grey for a type nobody could read.</summary>
    public static Color ColourOf(int type) => BrushOf(type).Color;

    public static SolidColorBrush BrushOf(int type) =>
        type >= 0 && type < Brushes.Length ? Brushes[type] : Unknown;

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
