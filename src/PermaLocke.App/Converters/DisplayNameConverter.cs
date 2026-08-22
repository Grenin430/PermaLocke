using System.Globalization;
using System.Windows.Data;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Converters;

/// <summary>
/// Shows a domain enum by its Spanish label instead of its name.
/// </summary>
/// <remarks>
/// Used where the bound value has to stay the enum — a <c>ComboBox.SelectedItem</c>, for one —
/// so the list cannot simply be projected into strings in the view model. Presentation only, and
/// one-way: nothing is ever parsed back out of a label.
/// </remarks>
public sealed class DisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            EncounterType encounter => DisplayNames.Of(encounter),
            GameEventType type => DisplayNames.Of(type),
            IslandState state => DisplayNames.Of(state),
            _ => value?.ToString()
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
