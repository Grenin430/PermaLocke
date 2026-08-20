using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PermaLocke.App.Converters;

/// <summary>
/// Turns a resource key into the resource itself, so a view model can name a palette entry
/// without referencing WPF brushes.
/// </summary>
/// <remarks>
/// Used by the gacha portals: the tiers come from <c>Data/gacha.json</c> and each one names its
/// colour key, which keeps the palette in <c>Themes/Palette.xaml</c> where the rest of the app
/// looks for colours. Presentation only — no business logic travels through here.
/// </remarks>
public sealed class ResourceKeyConverter : IValueConverter
{
    /// <param name="parameter">
    /// Pass <c>color</c> to get the <see cref="System.Windows.Media.Color"/> instead of the
    /// brush: gradient stops need a colour, and the palette pairs every <c>…Brush</c> with a
    /// <c>…Color</c> under the same name.
    /// </param>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || key.Length == 0)
        {
            return DependencyProperty.UnsetValue;
        }

        if (parameter is string wanted && wanted.Equals("color", StringComparison.OrdinalIgnoreCase))
        {
            key = key.EndsWith("Brush", StringComparison.Ordinal)
                ? string.Concat(key.AsSpan(0, key.Length - "Brush".Length), "Color")
                : key;
        }

        return Application.Current?.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
