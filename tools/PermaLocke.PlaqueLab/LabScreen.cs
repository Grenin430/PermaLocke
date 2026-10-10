using System.IO;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;
using PermaLocke.GameLink;

namespace PermaLocke.PlaqueLab;

/// <summary>A window of the emulator the lab imitates: the size of the picture and how the screens are laid out in it.</summary>
public sealed record Preset(string Name, int Width, int Height, AzaharScreenLayout Layout)
{
    public override string ToString() => Name;
}

/// <summary>
/// The emulator, imitated (2026-10-10): the picture of a window, the 3DS screens where Azahar would draw them, and over the black
/// band what the app would put there — through the same <see cref="CapGeometry"/> and the same pipeline as the app. All in one BGRA
/// buffer, a pixel of it a pixel of the screen.
/// </summary>
public static class LabScreen
{
    public static readonly Preset[] Presets =
    [
        new("1920×976 · apilado (el del jugador)", 1920, 976, AzaharScreenLayout.Default),
        new("1600×850 · apilado", 1600, 850, AzaharScreenLayout.Default),
        new("1366×700 · apilado", 1366, 700, AzaharScreenLayout.Default),
        new("1280×690 · apilado (ventana de 720p)", 1280, 690, AzaharScreenLayout.Default),
        new("1100×600 · apilado", 1100, 600, AzaharScreenLayout.Default),
        new("960×520 · apilado", 960, 520, AzaharScreenLayout.Default),
        new("640×360 · apilado (diminuta)", 640, 360, AzaharScreenLayout.Default),
        new("2560×1300 · apilado", 2560, 1300, AzaharScreenLayout.Default),
        new("3840×2064 · apilado (4K)", 3840, 2064, AzaharScreenLayout.Default),
        new("900×900 · apilado (casi cuadrada)", 900, 900, AzaharScreenLayout.Default),
        new("1920×976 · lado a lado", 1920, 976, new AzaharScreenLayout(3, false, false)),
        new("1280×690 · lado a lado", 1280, 690, new AzaharScreenLayout(3, false, false)),
        new("1920×976 · una pantalla", 1920, 976, new AzaharScreenLayout(1, false, false)),
        new("1920×976 · apilado y girado", 1920, 976, new AzaharScreenLayout(0, false, true)),
        new("1920×976 · pantalla grande (no se calcula)", 1920, 976, new AzaharScreenLayout(2, false, false))
    ];

    /// <summary>The picture the lab draws for the game: a screenshot given in <c>PERMALOCKE_PROTO_ASSETS</c> (bg1.png top, bg3.png bottom), or flat colours.</summary>
    private static (byte[] Pixels, int W, int H)? _top, _bottom;
    private static bool _looked;

    public static (byte[] Bgra, CapPlan Plan) Compose(CapScenario scenario, double t, Preset preset, CapOptions options, bool game, double dpi = 1,
        CapMode? previous = null)
    {
        var w = preset.Width;
        var h = preset.Height;
        var bgra = new byte[w * h * 4];
        for (var i = 3; i < bgra.Length; i += 4) bgra[i] = 255;

        var plan = CapGeometry.Plan(new CapRect(0, 0, w, h), preset.Layout, dpi, options, previous);
        DrawGame(bgra, w, h, preset.Layout, plan.Content, game);

        foreach (var slot in new[] { plan.Main, plan.Memorial })
        {
            if (slot is null) continue;
            var sheet = CapPlaque.Replay(scenario, slot.Mode, slot.Cols, slot.Rows, t, slot.Panel, plan.Memorial is null);
            Blit(sheet, bgra, w, slot.Box, slot.Cell, 0.97);
        }

        return (bgra, plan);
    }

    /// <summary>The sheet put on the buffer, each cell a block of <paramref name="cell"/> pixels, over what is there.</summary>
    public static void Blit(CapSheet sheet, byte[] bgra, int width, CapRect box, int cell, double opacity)
    {
        for (var y = 0; y < sheet.H; y++)
        {
            for (var x = 0; x < sheet.W; x++)
            {
                var i = ((y * sheet.W) + x) * 4;
                var a = sheet.B[i + 3] / 255.0 * opacity;
                if (a <= 0) continue;
                for (var sy = 0; sy < cell; sy++)
                {
                    var py = box.Top + (y * cell) + sy;
                    for (var sx = 0; sx < cell; sx++)
                    {
                        var px = box.Left + (x * cell) + sx;
                        var o = ((py * width) + px) * 4;
                        if (o < 0 || o + 3 >= bgra.Length || px < 0 || px >= width) continue;
                        bgra[o] = (byte)((sheet.B[i] * a) + (bgra[o] * (1 - a)));
                        bgra[o + 1] = (byte)((sheet.B[i + 1] * a) + (bgra[o + 1] * (1 - a)));
                        bgra[o + 2] = (byte)((sheet.B[i + 2] * a) + (bgra[o + 2] * (1 - a)));
                    }
                }
            }
        }
    }

    private static void DrawGame(byte[] bgra, int width, int height, AzaharScreenLayout layout, CapRect content, bool game)
    {
        if (CapGeometry.PictureOf(layout) is not { } picture) return;
        if (game && !_looked) Look();

        // The picture is 3DS pixels; each screen is a rectangle of it (unrotated: the lab shows where the game is, not what it says).
        var scale = content.Width / (double)(picture.Width + 2);
        var left = content.Left + 1;
        var top = content.Top + 1;

        void Screen(double x, double y, double w, double h, uint tint, (byte[] Pixels, int W, int H)? image)
        {
            var x0 = (int)(left + (x * scale));
            var y0 = (int)(top + (y * scale));
            var x1 = (int)(left + ((x + w) * scale));
            var y1 = (int)(top + ((y + h) * scale));
            for (var py = Math.Max(0, y0); py < Math.Min(height, y1); py++)
            {
                for (var px = Math.Max(0, x0); px < Math.Min(width, x1); px++)
                {
                    var o = ((py * width) + px) * 4;
                    if (game && image is { } img)
                    {
                        var sx = Math.Min(img.W - 1, (int)((px - x0) / (double)(x1 - x0) * img.W));
                        var sy = Math.Min(img.H - 1, (int)((py - y0) / (double)(y1 - y0) * img.H));
                        Buffer.BlockCopy(img.Pixels, ((sy * img.W) + sx) * 4, bgra, o, 3);
                        continue;
                    }

                    var shade = 0.75 + (0.25 * ((py - y0) / (double)Math.Max(1, y1 - y0)));
                    bgra[o] = (byte)((tint & 255) * shade);
                    bgra[o + 1] = (byte)(((tint >> 8) & 255) * shade);
                    bgra[o + 2] = (byte)(((tint >> 16) & 255) * shade);
                }
            }
        }

        // 3DS pixels in the picture, whichever its layout: stacked (top over bottom), single, or side by side. Turned ones swap the axes.
        var upright = layout.Upright;
        (double x, double y, double w, double h) Turn(double x, double y, double w, double h) => upright ? (y, x, h, w) : (x, y, w, h);
        var top1 = (0.0, 0.0, 400.0, 240.0);
        var bottom1 = layout.Option switch { 3 => (400.0, 0.0, 320.0, 240.0), _ => (40.0, 240.0, 320.0, 240.0) };
        var single = layout.Option == 1;
        var first = single && layout.Swapped ? (0.0, 0.0, 320.0, 240.0) : top1;
        var t1 = Turn(first.Item1, first.Item2, first.Item3, first.Item4);
        Screen(t1.x, t1.y, t1.w, t1.h, single && layout.Swapped ? 0x4A2A2Au : 0x2A4A3Au, single && layout.Swapped ? _bottom : _top);
        if (single) return;
        var b1 = Turn(bottom1.Item1, bottom1.Item2, bottom1.Item3, bottom1.Item4);
        Screen(b1.x, b1.y, b1.w, b1.h, 0x4A2A2A, _bottom);
    }

    private static void Look()
    {
        _looked = true;
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PROTO_ASSETS");
        if (folder is not { Length: > 0 }) return;
        _top = Load(Path.Combine(folder, "bg1.png"));
        _bottom = Load(Path.Combine(folder, "bg3.png"));
    }

    private static (byte[] Pixels, int W, int H)? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var decoder = new PngBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = new FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
            return (pixels, frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }
}
