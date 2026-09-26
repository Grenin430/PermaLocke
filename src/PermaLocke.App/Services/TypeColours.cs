using System.Globalization;
using System.Windows.Media;

namespace PermaLocke.App.Services;

/// <summary>
/// The colours the series has used for the types since forever, by the game's type number: the table itself, without
/// the brushes of <see cref="TypePalette"/>.
/// </summary>
/// <remarks>
/// Apart so the album's cards can be drawn and tested without the rest of WPF (<c>tools/PermaLocke.PixelCheck</c>, §187).
/// <see cref="TypePalette"/> builds its brushes from this same table: one palette, not two that could drift.
/// </remarks>
public static class TypeColours
{
    public static readonly IReadOnlyList<string> Hex =
    [
        "#9FA19F", "#FF8000", "#81B9EF", "#9141CB", "#915121", "#AFA981", "#91A119", "#704170",
        "#60A1B8", "#E62829", "#2980EF", "#3FA129", "#FAC000", "#EF4179", "#3DCEF3", "#5060E1",
        "#624D4E", "#EF70EF", "#2E9AA0"
    ];

    /// <summary>The grey for a type nobody could read.</summary>
    public const string UnknownHex = "#5A5470";

    /// <summary>The type's colour, or <see cref="UnknownHex"/> for a type nobody could read.</summary>
    public static Color Of(int type) => Parse(type >= 0 && type < Hex.Count ? Hex[type] : UnknownHex);

    public static Color Parse(string hex) => Color.FromRgb(
        byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
