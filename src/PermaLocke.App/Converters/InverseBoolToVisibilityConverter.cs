using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// Shows an element while a flag is <b>false</b>, which is what WPF's own converter cannot do.
/// </summary>
/// <remarks>
/// For the pairs where one panel replaces another: the EV editor shows the budget left, or the
/// warning that the reparto is over the line, never both.
/// </remarks>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed or Visibility.Hidden;
}
