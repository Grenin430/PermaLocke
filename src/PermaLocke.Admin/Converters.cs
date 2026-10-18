using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PermaLocke.Admin;

/// <summary>Visible while the value is null: the «pick one» card that sits where a page will go.</summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
