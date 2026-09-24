using System.ComponentModel;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Makes a screen's messages go away on their own: five seconds on screen, then empty (2026-09-24, at the player's
/// request: «el mensaje que sale al cambiar un ataque se queda ahí»).
/// </summary>
/// <remarks>
/// Only the text goes; nothing it reported is undone, and the event that recorded it is still in the run. A message
/// replaced before its time starts its own five seconds, and an old timer never wipes a newer message.
/// </remarks>
public static class Fleeting
{
    /// <summary>How long a message stays.</summary>
    public static readonly TimeSpan Life = TimeSpan.FromSeconds(5);

    /// <summary>Clears each of these string properties <see cref="Life"/> after it is set to something.</summary>
    public static void Fade(INotifyPropertyChanged model, params string[] properties)
    {
        var watched = properties
            .Select(name => model.GetType().GetProperty(name)
                            ?? throw new ArgumentException($"{model.GetType().Name} no tiene {name}", nameof(properties)))
            .ToDictionary(property => property.Name);

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && watched.TryGetValue(name, out var property)
                && property.GetValue(model) is string { Length: > 0 } text)
            {
                _ = ClearLaterAsync(model, property, text);
            }
        };
    }

    private static async Task ClearLaterAsync(object model, System.Reflection.PropertyInfo property, string text)
    {
        // Vuelve al hilo de la interfaz, que es el que cambió la propiedad.
        await Task.Delay(Life);

        if (Equals(property.GetValue(model), text))
        {
            property.SetValue(model, string.Empty);
        }
    }
}
