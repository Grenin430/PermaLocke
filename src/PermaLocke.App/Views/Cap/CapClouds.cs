namespace PermaLocke.App.Views;

/// <summary>
/// Clouds in Hirohiko Araki's manner (2026-10-18): the clouds of a still of JoJo's Bizarre Adventure the player sent, pixelled cell by cell
/// with their inner lines (<see cref="CapCloudArt"/>, made by tools/clouds/araki_clouds.js; the still's white streaks and the flat wisp of
/// the horizon were taken out at the player's word). They drift slowly along the one sky of both windows (the world's columns), behind the Sky Pillar
/// and the Ultra Wormhole, and take the hour and the weather.
/// </summary>
public static class CapClouds
{
    private sealed record Shape(int W, int H, sbyte[] Cells);

    // white, pale, blue, shade, Araki's inner line, the ink outline
    private static readonly uint[] Tones = [0xFFFFFF, 0xEEF4FD, 0xC8DAF2, 0xA2BCE4, 0x7E9CD4, 0x3E5AA0];

    private static readonly Shape[] Shapes = [.. CapCloudArt.Clouds.Select(Parse)];

    // Which cloud goes in which band of the sky (height as a share of the sky, pace in cells a second): the big heaps high, the middling and
    // small ones lower; the lower the slower.
    private static readonly (int Shape, double Band, double Pace)[] Plan =
    [
        (0, 0.3, 0.3), (4, 0.62, 0.2), (1, 0.32, 0.28), (6, 0.74, 0.14), (2, 0.36, 0.26), (5, 0.66, 0.18), (3, 0.5, 0.22),
        (7, 0.8, 0.12),
    ];

    private static Shape Parse(string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        var cells = new sbyte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                cells[(y * w) + x] = (sbyte)"wpbslk".IndexOf(rows[y][x]);
        return new Shape(w, h, cells);
    }

    private static double Rnd(int a, int b) => (uint)CapArt.Hash(a, b) % 10000 / 10000.0;

    /// <summary>Paints the clouds and their streaks into the sky of this window. <paramref name="seconds"/> is the panel's clock.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double seconds)
    {
        if (l.Mode is not (CapMode.Column or CapMode.Slim) || l.SurfaceY < 30) return;

        var night = CapSeaPalette.Night(ctx.SkyHour);
        var haze = 0.03 + (0.55 * night) + (0.4 * pal.Murk);
        var world = (ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols) + 140;
        var sky = l.SurfaceY - 1;

        void Put(int x, int y, uint c)
        {
            if (y >= sky) return;
            var under = p.Solid(x, y) ? p.At(x, y) : c;
            p.Set(x, y, CapSheet.Mix(c, under, haze));
        }

        var count = Math.Clamp(world / 30, 5, 14);
        for (var k = 0; k < count; k++)
        {
            var (index, band, pace) = Plan[k % Plan.Length];
            var shape = Shapes[index];
            var spread = (k * (world / (double)count)) + (Rnd(ctx.Seed + 3, k) * 24);
            var wx = ((spread + (seconds * pace)) % world) - 70;
            var x0 = (int)Math.Round(wx - ctx.WorldX - (shape.W / 2.0));
            var y0 = (int)Math.Round((l.SurfaceY * band) - (shape.H * 0.6));
            if (x0 > l.Cols + 90 || x0 + shape.W < -90) continue;

            for (var y = 0; y < shape.H; y++)
            {
                for (var x = 0; x < shape.W; x++)
                {
                    var tone = shape.Cells[(y * shape.W) + x];
                    if (tone >= 0) Put(x0 + x, y0 + y, Tones[tone]);
                }
            }
        }
    }
}
