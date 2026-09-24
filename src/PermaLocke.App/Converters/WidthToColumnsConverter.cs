using System.Globalization;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// How many columns fit in a width: four when there is room for them, two when there is not.
/// </summary>
/// <remarks>
/// For the four known moves of MOVIMIENTOS (§144): each card carries a name, the category icon and three numbers, and
/// at the smallest window four in a row leave each card a hundred pixels. The parameter is the width from which four
/// fit.
/// </remarks>
public sealed class WidthToColumnsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var threshold = double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture,
            out var parsed) ? parsed : 760;

        return value is double width && width > 0 && width < threshold ? 2 : 4;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
