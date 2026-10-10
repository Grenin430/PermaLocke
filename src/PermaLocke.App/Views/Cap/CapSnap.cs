using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>Writes a sheet of the cap panel as a PNG, magnified by whole numbers, over a plain background: what the tests and the lab look at.</summary>
public static class CapSnap
{
    /// <summary>The panel over a colour (<paramref name="background"/>, 0xRRGGBB; black is the emulator's band), each cell <paramref name="scale"/> pixels.</summary>
    public static byte[] Bgra(CapSheet sheet, int scale, uint background, double opacity = 1)
    {
        var w = sheet.W * scale;
        var h = sheet.H * scale;
        var pixels = new byte[w * h * 4];
        var bgR = (int)((background >> 16) & 255);
        var bgG = (int)((background >> 8) & 255);
        var bgB = (int)(background & 255);

        for (var y = 0; y < sheet.H; y++)
        {
            for (var x = 0; x < sheet.W; x++)
            {
                var i = ((y * sheet.W) + x) * 4;
                var a = sheet.B[i + 3] / 255.0 * opacity;
                var b = (byte)((sheet.B[i] * a) + (bgB * (1 - a)));
                var g = (byte)((sheet.B[i + 1] * a) + (bgG * (1 - a)));
                var r = (byte)((sheet.B[i + 2] * a) + (bgR * (1 - a)));
                for (var sy = 0; sy < scale; sy++)
                {
                    for (var sx = 0; sx < scale; sx++)
                    {
                        var o = ((((y * scale) + sy) * w) + (x * scale) + sx) * 4;
                        (pixels[o], pixels[o + 1], pixels[o + 2], pixels[o + 3]) = (b, g, r, 255);
                    }
                }
            }
        }

        return pixels;
    }

    public static void Save(string path, byte[] bgra, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        png.Save(file);
    }

    public static void Save(string path, CapSheet sheet, int scale = 3, uint background = 0x000000, double opacity = 1) =>
        Save(path, Bgra(sheet, scale, background, opacity), sheet.W * scale, sheet.H * scale);
}
