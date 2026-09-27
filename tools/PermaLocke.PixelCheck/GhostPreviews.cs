using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>A friend's ghost crossing the emulator (1.0.5): every moment draws, it ends clear, and a sheet as PNG when asked.</summary>
public sealed class GhostPreviews
{
    /// <summary>A pale round ghost of 32×32 with dark eyes, standing in for an icon passed through GhostArt.</summary>
    private static byte[] Sprite()
    {
        var pixels = new byte[32 * 32 * 4];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var dx = x - 15.5;
                var dy = y - 14.0;
                if ((dx * dx / 144) + (dy * dy / 196) > 1) continue;
                var at = ((y * 32) + x) * 4;
                var eye = y is 11 or 12 && x is 11 or 12 or 19 or 20;
                var rim = (dx * dx / 144) + (dy * dy / 196) > 0.8;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = eye ? ((byte)0xA8, (byte)0x7C, (byte)0x5C, (byte)225)
                    : rim ? ((byte)0xFF, (byte)0xF2, (byte)0xE4, (byte)235) : ((byte)0xE8, (byte)0xD0, (byte)0xB0, (byte)165);
            }
        }

        return pixels;
    }

    [Fact]
    public void Every_moment_draws_and_the_screen_ends_clear()
    {
        var scene = new GhostScene(200, 140, Sprite(), 32, 32, 3);
        for (var t = 0.0; t < GhostTimeline.Length + 0.3; t += 1 / 30.0)
        {
            scene.Render(t);
        }

        Assert.All(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(0, alpha));
        scene.Render(GhostTimeline.Pause + 0.3);
        Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha > 150);
    }

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PIXEL_DIR");
        if (string.IsNullOrWhiteSpace(folder)) return;

        Directory.CreateDirectory(folder);
        const int cols = 200, rows = 140, cell = 4, w = cols * cell, h = rows * cell;
        var scene = new GhostScene(cols, rows, Sprite(), 32, 32, 3);
        double[] times = [0.4, 1.2, 2.2, 3.0, 3.5, 4.0, 5.0, 6.0, 6.5, 6.9];
        const int columns = 2;
        var sheetRows = (times.Length + columns - 1) / columns;
        var sheet = new byte[w * columns * h * sheetRows * 4];

        for (var i = 0; i < times.Length; i++)
        {
            scene.Render(times[i]);
            var left = (i % columns) * w;
            var top = (i / columns) * h;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var at = (((y / cell) * cols) + (x / cell)) * 4;
                    var a = scene.Pixels[at + 3];
                    // Sobre un «juego» de mentira: cuadros verdes.
                    byte gb = (byte)(((x / 24) + (y / 24)) % 2 == 0 ? 0x40 : 0x52), gg = (byte)(gb + 0x38), gr = (byte)(gb - 0x08);
                    var to = ((((top + y) * w * columns) + left + x) * 4);
                    sheet[to] = (byte)(scene.Pixels[at] + (gb * (255 - a) / 255));
                    sheet[to + 1] = (byte)(scene.Pixels[at + 1] + (gg * (255 - a) / 255));
                    sheet[to + 2] = (byte)(scene.Pixels[at + 2] + (gr * (255 - a) / 255));
                    sheet[to + 3] = 255;
                }
            }
        }

        PngFile.Write(sheet, w * columns, h * sheetRows, 1, Path.Combine(folder, "fantasma.png"));
    }
}
