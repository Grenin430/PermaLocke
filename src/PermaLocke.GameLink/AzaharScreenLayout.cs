namespace PermaLocke.GameLink;

/// <summary>
/// How Azahar lays the two 3DS screens out in its window, as its <c>qt-config.ini</c> says (2026-10-10): what decides where
/// the black band is, which is where the cap panel goes.
/// </summary>
/// <param name="Option"><c>layout_option</c>: 0 default (top over bottom), 1 single screen, 2 large screen, 3 side by side;
/// anything higher is a layout this does not compute (hybrid, custom, separate windows).</param>
/// <param name="Swapped"><c>swap_screen</c>: the bottom screen first (or alone, in single-screen).</param>
/// <param name="Upright"><c>upright_screen</c>: the whole picture turned a quarter.</param>
public sealed record AzaharScreenLayout(int Option, bool Swapped, bool Upright)
{
    /// <summary>Azahar's own default, which is the player's: top over bottom.</summary>
    public static AzaharScreenLayout Default { get; } = new(0, false, false);

    /// <summary>Reads the three settings from the lines of <c>qt-config.ini</c>; what is missing keeps its default.</summary>
    public static AzaharScreenLayout Parse(IEnumerable<string> lines)
    {
        var layout = Default;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith("layout_option=", StringComparison.Ordinal)
                && int.TryParse(line["layout_option=".Length..].Trim(), out var option))
            {
                layout = layout with { Option = option };
            }
            else if (line.StartsWith("swap_screen=", StringComparison.Ordinal))
            {
                layout = layout with { Swapped = line["swap_screen=".Length..].Trim() == "true" };
            }
            else if (line.StartsWith("upright_screen=", StringComparison.Ordinal))
            {
                layout = layout with { Upright = line["upright_screen=".Length..].Trim() == "true" };
            }
        }

        return layout;
    }

    /// <summary>The file's layout, or the default when there is no file to read.</summary>
    public static AzaharScreenLayout Read(string configPath)
    {
        try
        {
            return File.Exists(configPath) ? Parse(File.ReadAllLines(configPath)) : Default;
        }
        catch (IOException)
        {
            // Azahar escribe su configuración al cerrar; si justo coincide, se vuelve a mirar en la siguiente vuelta.
            return Default;
        }
    }
}
