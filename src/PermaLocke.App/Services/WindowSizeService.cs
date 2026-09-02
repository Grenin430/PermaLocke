using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <param name="Name">What the option is called on screen.</param>
/// <param name="Why">One line saying who it is for.</param>
public sealed record WindowSize(string Key, string Name, double Width, double Height, string Why);

/// <summary>
/// How big the window opens, chosen by the player and remembered.
/// </summary>
/// <remarks>
/// <para>
/// The window is a fixed size — it cannot be dragged or maximised — which is the right call for a
/// layout built around a map that has to be seen whole, and the wrong one for anybody whose eyes or
/// screen do not match the number somebody else picked. So the number is a setting.
/// </para>
/// <para>
/// The sizes are presets rather than two boxes to type into, because a free width is a way to end
/// up with a window taller than the screen or narrower than the sidebar, and neither of those is
/// worth the flexibility. Whatever is chosen is still clamped to the working area on startup.
/// </para>
/// </remarks>
public sealed class WindowSizeService(AppPaths paths, ILogger<WindowSizeService> logger)
{
    /// <summary>
    /// The offered sizes. The widths are what the map does with them: the sidebar takes 216 and the
    /// legend 290, so «Grande» is the first one where the widest island fits at its own size.
    /// </summary>
    public static readonly IReadOnlyList<WindowSize> Sizes =
    [
        new("normal", "NORMAL", 1180, 760, "1180 × 760. El mapa se reduce para caber."),
        new("grande", "GRANDE", 1360, 860, "1360 × 860. La isla más ancha cabe a tamaño natural."),
        new("enorme", "MUY GRANDE", 1560, 980, "1560 × 980. Todo más grande, para pantallas amplias.")
    ];

    public static WindowSize Default => Sizes[0];

    private string SettingsPath => Path.Combine(paths.Config, "ventana.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The chosen size, or the default when nothing has been chosen or the file is bad.</summary>
    public WindowSize Current
    {
        get
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return Default;
                }

                var stored = JsonSerializer
                    .Deserialize<WindowSettings>(File.ReadAllText(SettingsPath), Json)?.Size;

                return Sizes.FirstOrDefault(size =>
                    string.Equals(size.Key, stored, StringComparison.Ordinal)) ?? Default;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se ha podido leer el tamaño de ventana");
                return Default;
            }
        }
    }

    public void Set(WindowSize size)
    {
        Directory.CreateDirectory(paths.Config);
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(new WindowSettings { Size = size.Key }, Json));

        logger.LogInformation("Tamaño de ventana: {Size} ({Width}x{Height})",
            size.Name, size.Width, size.Height);
    }

    private sealed class WindowSettings
    {
        public string Size { get; set; } = Default.Key;
    }
}
