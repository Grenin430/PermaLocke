using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// The nursery's eggs arriving (§225): every moment draws and covers the screen, for one egg and for a full order, and with
/// <c>PERMALOCKE_PIXEL_DIR</c> set, a sheet of frames as PNG.
/// </summary>
public sealed class NurseryPreviews
{
    private static readonly PixelColour Fire = PixelColour.FromRgb(0xE6, 0x28, 0x29);
    private static readonly PixelColour Water = PixelColour.FromRgb(0x29, 0x80, 0xEF);

    [Fact]
    public void Every_moment_draws_and_covers_the_screen()
    {
        foreach (var (width, height) in new[] { (900, 560), (1400, 860), (500, 320) })
        {
            foreach (var eggs in new[] { 1, 3, 8, 24 })
            {
                var scene = new NurseryScene(width, height);

                for (var t = 0.0; t < NurseryTimeline.Rest(eggs) + 1.5; t += 1 / 20.0)
                {
                    scene.Render(eggs, Fire, 77, t);
                    Assert.All(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(255, alpha));
                }
            }
        }
    }

    [Fact]
    public void The_timeline_is_the_same_for_one_egg_or_forty_and_stays_short()
    {
        // Se construyen todos a la vez, con la misma lluvia: pedir más no alarga la animación.
        Assert.Equal(NurseryTimeline.Rest(1), NurseryTimeline.Rest(40));
        Assert.True(NurseryTimeline.Rest(40) < 9, "una tanda llena no puede durar más de nueve segundos");
    }

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PIXEL_DIR");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        Sheet(folder, "guarderia-1.png", 1, Fire);
        Sheet(folder, "guarderia-5.png", 5, Water);
    }

    private static void Sheet(string folder, string name, int eggs, PixelColour type)
    {
        const int w = 900, h = 560;
        var scene = new NurseryScene(w, h);
        double[] times =
        [
            0.3, NurseryTimeline.Lead + 0.3, NurseryTimeline.Lead + 0.8, NurseryTimeline.Lead + 1.3,
            NurseryTimeline.Lead + 1.8, NurseryTimeline.Lead + 2.3, NurseryTimeline.Lead + 2.8, NurseryTimeline.Landed(eggs) + 0.05,
            NurseryTimeline.Flash(eggs) + 0.05, NurseryTimeline.Flash(eggs) + 0.25, NurseryTimeline.Flash(eggs) + 0.5, NurseryTimeline.Flash(eggs) + 0.9,
            NurseryTimeline.Flash(eggs) + 1.3, NurseryTimeline.Rest(eggs) + 0.3, NurseryTimeline.Rest(eggs) + 1.0, NurseryTimeline.Rest(eggs) + 2.0
        ];

        const int columns = 4;
        var rows = (times.Length + columns - 1) / columns;
        var sheet = new byte[w * columns * h * rows * 4];

        for (var i = 0; i < times.Length; i++)
        {
            scene.Render(eggs, type, 11, times[i]);
            var left = (i % columns) * w;
            var top = (i / columns) * h;

            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(scene.Pixels, y * w * 4, sheet, (((top + y) * w * columns) + left) * 4, w * 4);
            }
        }

        PngFile.Write(sheet, w * columns, h * rows, 1, Path.Combine(folder, name));
    }
}
