using System.IO;
using System.Text.Json;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Services;

/// <summary>
/// Which look the player chose, remembered in <c>Config/tema.json</c> (2026-10-01). Read once at start, before any
/// window exists, and written when the gallery changes it.
/// </summary>
public static class ThemeStore
{
    private static string? _folder;

    /// <summary>The key of the look to start with: the one on the command line, else the one saved, else the original.</summary>
    public static string Start(string configFolder, string? fromCommandLine)
    {
        _folder = configFolder;
        if (fromCommandLine is not null) return PixelTheme.Find(fromCommandLine).Key;

        try
        {
            var file = Path.Combine(configFolder, "tema.json");
            if (File.Exists(file) && JsonDocument.Parse(File.ReadAllText(file)).RootElement.TryGetProperty("tema", out var key))
            {
                return PixelTheme.Find(key.GetString()).Key;
            }
        }
        catch (Exception)
        {
            // Un fichero roto es el diseño de siempre, no un fallo al arrancar.
        }

        return PixelTheme.All[0].Key;
    }

    /// <summary>Puts a look in place now and remembers it.</summary>
    public static void Choose(string key)
    {
        PixelTheme.Apply(key, System.Windows.Application.Current.Resources);

        try
        {
            if (_folder is not null)
            {
                Directory.CreateDirectory(_folder);
                File.WriteAllText(Path.Combine(_folder, "tema.json"),
                    JsonSerializer.Serialize(new { tema = PixelTheme.Find(key).Key }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception)
        {
            // No poder recordarlo no impide verlo: se vuelve al de siempre al reabrir.
        }
    }
}
