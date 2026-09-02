using System.Globalization;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// Flips the sign of a number, which is how something inside a rotating thing stays upright.
/// </summary>
/// <remarks>
/// The roulette's labels are children of the wheel, so they turn with it and the ones at the bottom
/// arrive upside down — unreadable for half of every turn. Countering the wheel's own angle on each
/// label keeps every one of them level the whole way round, which is what a real wheel does with
/// its numbers.
/// </remarks>
public sealed class NegateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double number && double.IsFinite(number) ? -number : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Convert(value, targetType, parameter, culture);
}
