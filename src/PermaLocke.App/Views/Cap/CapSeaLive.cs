namespace PermaLocke.App.Views;

/// <summary>
/// What moves in the sea (2026-10-10), painted every frame over the still scene and by the clock the mood sets (<see cref="CapAnim.Phase"/>):
/// the surface and the light that dances under it, the shafts of light that come down through the water, the snow that drifts through it
/// in three planes, the stars. Each is a function of the clock and of the seed, nothing else.
/// </summary>
public static class CapSeaLive
{
    // ---------------------------------------------------------------- the surface

    /// <summary>The waves on the line between sky and water, the foam on their crests, and the caustics that dance in the first rows under it.</summary>
    public static void Surface(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase)
    {
        if (l.SurfaceY <= 0) return;

        var (_, horizon) = CapSeaPalette.Sky(ctx.SkyHour);
        var amplitude = 0.55 * pal.Agitation;
        var foam = CapSheet.Mix(pal.Water[0], 0xFFFFFFu, 0.6);

        for (var x = 0; x < l.Cols; x++)
        {
            double wx = x + ctx.WorldX;
            var wave = amplitude * (Math.Sin((wx * 0.21) + (phase * 1.7)) + (0.6 * Math.Sin((wx * 0.53) - (phase * 2.3))));
            var surface = l.SurfaceY + (int)Math.Round(wave);

            // A crest above the line lifts water into the sky; a trough below it lets the sky in.
            for (var y = surface; y < l.SurfaceY; y++) p.Set(x, y, pal.Water[0]);
            for (var y = l.SurfaceY; y < surface; y++) p.Set(x, y, horizon);

            p.Set(x, surface, foam);
            p.Set(x, surface + 1, CapSheet.Mix(pal.Water[0], foam, 0.45));
            if (((x + surface) & 1) == 0) p.Set(x, surface + 2, pal.Shaft, 38);
        }

        // Caustics: the sun's net, a few cells that light up where two slow waves agree, fading with depth.
        var rows = Math.Min(24, l.FloorTop - l.SurfaceY - 4);
        for (var y = l.SurfaceY + 3; y < l.SurfaceY + 3 + rows; y++)
        {
            var depth = 1 - ((y - l.SurfaceY - 3) / (double)Math.Max(1, rows));
            for (var x = 0; x < l.Cols; x++)
            {
                double wx = x + ctx.WorldX;
                var v = Math.Sin((wx * 0.33) + (phase * 1.3) + (y * 0.21)) * Math.Sin((wx * 0.17) - (phase * 0.9) + (y * 0.37));
                if (v > 0.8) p.Set(x, y, pal.Shaft, (int)(70 * depth * pal.ShaftStrength));
            }
        }
    }

    /// <summary>The twinkle of the stars the scene fixed: some of them blink at night.</summary>
    public static void Stars(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double phase)
    {
        var night = CapSeaPalette.Night(ctx.SkyHour);
        if (night < 0.2 || l.SkyRows <= 0) return;

        for (var k = 0; k < l.Cols / 6; k++)
        {
            if (((int)Math.Floor((phase * 1.4) + (k * 0.37)) + k) % 5 != 0) continue;
            var sx = CapArt.Hash(ctx.Seed + (ctx.WorldX / 3), k) % l.Cols;
            var sy = CapArt.Hash(ctx.Seed + 7, k) % Math.Max(1, l.SkyRows - 1);
            p.Set(sx, sy, 0xFFFFFFu, (int)(255 * night));
        }
    }

    // ---------------------------------------------------------------- the light and the snow

    /// <summary>Shafts of light from the surface, slanted, widening and fading with depth, made of dither so they stay pixel art.</summary>
    public static void Shafts(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase)
    {
        var count = l.Mode switch { CapMode.Compact => 1, CapMode.Slim => 2, CapMode.Strip => 4, _ => 3 };
        var top = l.SurfaceY + 1;
        var bottom = Math.Max(top + 1, l.FloorTop - 8);
        var height = bottom - top;
        var width = l.Main.Width;

        for (var k = 0; k < count; k++)
        {
            var baseX = l.Main.Left + (CapArt.Hash(ctx.Seed + ctx.WorldX, k + 40) % Math.Max(1, width));
            var drift = Math.Sin((phase * 0.12 * (k + 1)) + k) * 8;
            var strength = pal.ShaftStrength * (0.78 + (0.22 * Math.Sin((phase * 0.35) + (k * 2))));

            for (var y = top; y < bottom; y++)
            {
                var d = (y - top) / (double)height;
                var centre = baseX + drift + (0.28 * (y - top));
                var half = 3 + (0.07 * (y - top));
                var level = strength * Math.Pow(1 - d, 1.5);
                for (var x = (int)Math.Floor(centre - half); x <= (int)Math.Ceiling(centre + half); x++)
                {
                    var edge = 1 - (Math.Abs(x - centre) / half);
                    if (edge <= 0) continue;
                    if ((level * edge * 16) > CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)]) p.Set(x, y, pal.Shaft, 48);
                }
            }
        }
    }

    /// <summary>Marine snow in three planes: the far one slow and dim, the near one fast and bright, and a current that leans them all.</summary>
    public static void Snow(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase)
    {
        var area = l.Main.Width * Math.Max(1, l.FloorTop - l.SurfaceY);
        var count = Math.Clamp(area / 1100, 8, 96);
        var span = Math.Max(1, l.FloorTop - l.SurfaceY - 2);
        var width = Math.Max(1, l.Main.Width);

        for (var k = 0; k < count; k++)
        {
            var plane = k % 3;
            var speed = plane switch { 0 => 0.8, 1 => 1.7, _ => 3.0 };
            var y0 = CapArt.Hash(ctx.Seed + (ctx.WorldX * 3), k * 3 + 1) % span;
            var x0 = CapArt.Hash(ctx.Seed + (ctx.WorldX * 3), k * 3 + 2) % width;

            var y = l.SurfaceY + 2 + (int)((y0 + (phase * speed * 3)) % span);
            var drift = pal.Agitation * 0.9 * (plane + 1) * phase;
            var x = l.Main.Left + (int)(((x0 + (Math.Sin((phase * 0.6) + k) * (1 + plane)) + drift) % width + width) % width);

            var alpha = plane switch { 0 => 70, 1 => 125, _ => 200 };
            p.Set(x, y, pal.Snow, alpha);
            if (plane == 2) p.Set(x + 1, y, pal.Snow, alpha / 2);
        }
    }
}
