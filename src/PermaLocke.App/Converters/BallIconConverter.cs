using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Converters;

/// <summary>
/// Turns a tier's palette key (<c>Tier3Brush</c>) into the picture of its ball — Poké, Super, Ultra, Gloria or Master —
/// drawn with the same cells as the capsule machine.
/// </summary>
/// <remarks>
/// It is what puts the ladder in writing: the portal over each tier shows the ball that tier comes out in, so the
/// Master Ball on the mat means Tier 5 without a legend. Presentation only.
/// </remarks>
public sealed class BallIconConverter : IValueConverter
{
    private static readonly Dictionary<int, BitmapSource> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || !key.StartsWith("Tier", StringComparison.Ordinal)
            || !int.TryParse(key.AsSpan(4, key.Length - 4 - (key.EndsWith("Brush", StringComparison.Ordinal) ? 5 : 0)),
                out var position))
        {
            return DependencyProperty.UnsetValue;
        }

        var tier = Math.Clamp(position - 1, 0, 4);

        if (!Cache.TryGetValue(tier, out var icon))
        {
            icon = CapsuleMachineScene.BallIcon(tier, 14);
            Cache[tier] = icon;
        }

        return icon;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
