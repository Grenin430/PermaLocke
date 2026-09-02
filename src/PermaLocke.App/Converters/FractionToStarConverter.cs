using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// Turns 0..1 into a star <see cref="GridLength"/>, for a bar that fills part of a row.
/// </summary>
/// <remarks>
/// A two-column grid of stars is the only way to size something by proportion in WPF without
/// knowing the width first. <paramref name="parameter"/> being «resto» gives the other column, so
/// one converter fills both and the two halves cannot drift apart.
/// </remarks>
public sealed class FractionToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double number && double.IsFinite(number)
            ? Math.Clamp(number, 0, 1)
            : 0;

        var rest = string.Equals(parameter as string, "resto", StringComparison.Ordinal);
        return new GridLength(rest ? 1 - fraction : fraction, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
