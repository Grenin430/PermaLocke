using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// A fallen Pokémon drawn as its ghost: the cartridge's own icon, pale and see-through, pixel by pixel (§183).
/// </summary>
/// <remarks>
/// No blur and no glow: flat see-through pixels, a solid pale rim so the shape reads against a busy screen, and the
/// icon's dark lines kept solid so the Pokémon is recognisable. A checkerboard dither was tried first and at eight
/// screen pixels per cell it read as a chessboard, not as a ghost (seen on a capture of the rehearsal).
/// </remarks>
internal static class GhostArt
{
    private static readonly Color Deep = Color.FromRgb(0x5C, 0x7C, 0xA8);
    private static readonly Color Pale = Color.FromRgb(0xE4, 0xF2, 0xFF);

    /// <summary>The ghost of <paramref name="sprite"/>, or null when there is no sprite.</summary>
    public static BitmapSource? Make(BitmapSource? sprite)
    {
        if (sprite is null)
        {
            return null;
        }

        var bgra = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        var source = new byte[width * height * 4];
        bgra.CopyPixels(source, width * 4, 0);

        var ghost = new byte[source.Length];

        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && source[((y * width) + x) * 4 + 3] >= 128;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!Solid(x, y))
                {
                    continue;
                }

                var i = ((y * width) + x) * 4;
                var rim = !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1);

                // La luz del cuerpo se conserva para que se le reconozca; el color se va. Las líneas oscuras del
                // icono -ojos, contornos de dentro- se quedan macizas: sin ellas era una mancha a cuadros.
                var light = ((0.30 * source[i + 2]) + (0.59 * source[i + 1]) + (0.11 * source[i])) / 255.0;
                var line = light < 0.22;
                var colour = rim ? Pale : line ? Deep : Mix(Deep, Pale, 0.45 + (0.55 * light));

                ghost[i] = colour.B;
                ghost[i + 1] = colour.G;
                ghost[i + 2] = colour.R;
                ghost[i + 3] = rim ? (byte)235 : line ? (byte)225 : (byte)165;
            }
        }

        var result = BitmapSource.Create(width, height, bgra.DpiX, bgra.DpiY, PixelFormats.Bgra32, null, ghost, width * 4);
        result.Freeze();
        return result;
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + ((b.R - a.R) * t)),
        (byte)(a.G + ((b.G - a.G) * t)),
        (byte)(a.B + ((b.B - a.B) * t)));
}
