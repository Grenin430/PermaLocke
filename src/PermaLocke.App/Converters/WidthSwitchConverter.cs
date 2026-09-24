using System.Globalization;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// One number or another depending on a width: <c>ConverterParameter="umbral;ancho;estrecho"</c>.
/// </summary>
/// <remarks>
/// For a piece that sits beside something when there is room and drops below it when there is not, by switching its
/// grid row and column: the stats of the MOVIMIENTOS ficha (§144), which at the smallest window squeezed the name to
/// four letters.
/// </remarks>
public sealed class WidthSwitchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter?.ToString() ?? string.Empty).Split(';');

        if (parts.Length != 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var wide)
            || !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var narrow))
        {
            return 0;
        }

        return value is double width && width > 0 && width < threshold ? narrow : wide;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
