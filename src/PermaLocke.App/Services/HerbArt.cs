using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Services;

/// <summary>
/// The icon of a nature herb (2026-09-27): a pixel leaf tinted by the stat it raises, since the cartridge has no Mints and
/// so no icon for them. Grey for the five natures that change nothing.
/// </summary>
public static class HerbArt
{
    // Una hoja de 16×16: '#' borde, 'o' hoja, 'l' brillo, 'v' nervio.
    private static readonly string[] Leaf =
    [
        "..........###...",
        "........##ooo#..",
        "......##oooloo#.",
        ".....#oooollloo#",
        "....#ooooolllo#.",
        "...#ooooovoooo#.",
        "...#oooovooooo#.",
        "..#oooovoooooo#.",
        "..#ooovooooo##..",
        "..#oovoooo##....",
        ".#oovooo##......",
        ".#ovoo##........",
        ".#v###..........",
        "#v#.............",
        "#...............",
        "................"
    ];

    /// <summary>The colour of each stat a nature raises, in the game's nature order: Ataque, Defensa, Velocidad, At. Esp., Def. Esp.</summary>
    private static readonly Color[] Tints =
    [
        Color.FromRgb(0xE0, 0x50, 0x48), Color.FromRgb(0xE8, 0xA8, 0x30), Color.FromRgb(0xE8, 0x70, 0xB8),
        Color.FromRgb(0x50, 0x90, 0xE8), Color.FromRgb(0x48, 0xC0, 0x68)
    ];

    private static readonly Color Neutral = Color.FromRgb(0xB0, 0xB0, 0xB8);

    public static BitmapSource Make(int nature)
    {
        var up = nature / 5;
        var tint = up == nature % 5 ? Neutral : Tints[up];
        var pixels = new byte[16 * 16 * 4];

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                Color? c = Leaf[y][x] switch
                {
                    '#' => Shade(tint, 0.35),
                    'o' => tint,
                    'l' => Lift(tint, 0.45),
                    'v' => Shade(tint, 0.6),
                    _ => null
                };

                if (c is not { } colour) continue;
                var at = ((y * 16) + x) * 4;
                pixels[at] = colour.B;
                pixels[at + 1] = colour.G;
                pixels[at + 2] = colour.R;
                pixels[at + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Color Shade(Color c, double k) => Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));

    private static Color Lift(Color c, double k) =>
        Color.FromRgb((byte)(c.R + ((255 - c.R) * k)), (byte)(c.G + ((255 - c.G) * k)), (byte)(c.B + ((255 - c.B) * k)));
}
