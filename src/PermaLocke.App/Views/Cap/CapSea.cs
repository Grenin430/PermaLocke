namespace PermaLocke.App.Views;

/// <summary>What the sea depends on that is not the party: the seed of the run, the hour of Alola, and where this window sits in the one sea.</summary>
/// <param name="Seed">A number out of the run: its relief, its coral and its rare events are the same every time and different from another run's.</param>
/// <param name="Hour">The game's hour, 0 to 24, or -1 when it cannot be known (then it is mid-morning).</param>
/// <param name="WorldX">The column of the sea this window's first column is: the party's window is further along than the deep's, so the floor runs on from one to the other and whatever swims behind the game comes out of the other side.</param>
/// <param name="WorldWidth">How many columns the whole sea is: the deep, the screens of the game, and the party.</param>
public sealed record CapSeaContext(int Seed = 0, double Hour = -1, int WorldX = 0, int WorldWidth = 0)
{
    public double SkyHour => Hour < 0 ? 10 : Hour;
}

/// <summary>
/// The still part of the sea (2026-10-10), painted once per mood and per change of the run: the sky at the hour of Alola, the water as a
/// dithered ramp, the floor with its strata (one for each trial cleared), the fossils (one for each Pokémon fallen) half buried in it and the
/// mounds they raise. What moves over it is <see cref="CapSeaLive"/>'s.
/// </summary>
public static class CapSea
{
    /// <summary>The seabed's surface at a column of the sea: a hill that undulates, and a mound where a fossil lies.</summary>
    public static int Relief(CapWorldLayout l, CapSeaContext ctx, int x, IReadOnlyList<int>? fossilColumns = null)
    {
        double wx = x + ctx.WorldX;
        var s = ctx.Seed;
        var h = (4.0 * Math.Sin((wx * 0.045) + (s % 17))) + (2.6 * Math.Sin((wx * 0.131) + (s % 29))) + (1.2 * Math.Sin((wx * 0.29) + (s % 13)));
        if (fossilColumns is not null)
        {
            foreach (var fx in fossilColumns)
            {
                var d = Math.Abs(x - fx) / 11.0;
                if (d < 1) h -= 4.5 * (1 - d) * (1 - d);
            }
        }

        // A band that is not the full height keeps its relief inside it.
        return Math.Clamp(l.FloorTop + (int)Math.Round(h), 6, l.Rows - 4);
    }

    /// <summary>
    /// The column of the sea the fossil of a given number lies in: a golden-ratio walk over the floor, so each new fossil falls in the largest gap
    /// there is and no fossil ever moves when another is added.
    /// </summary>
    public static int FossilColumn(CapWorldLayout l, int index, CapSeaContext ctx)
    {
        var left = 14;
        var span = Math.Max(1, l.Cols - 28);
        var fraction = (0.5 + ((ctx.Seed % 1000) / 1000.0) + (index * 0.6180339887)) % 1.0;
        return left + (int)(fraction * span);
    }

    /// <summary>The columns the fossils lie in, oldest first.</summary>
    public static int[] FossilColumns(CapWorldLayout l, int count, CapSeaContext ctx)
    {
        if (count <= 0) return [];
        var columns = new int[count];
        for (var i = 0; i < count; i++) columns[i] = FossilColumn(l, i, ctx);
        return columns;
    }

    /// <summary>How big the fossils are for how many there are: half size up to eight, then smaller.</summary>
    public static int FossilScale(CapWorldLayout l, int count) => l.Mode == CapMode.Compact ? 4 : count <= 3 ? 1 : count <= 8 ? 2 : 4;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, CapSeaContext ctx)
    {
        p.Clear();
        var grave = data.Graves ?? [];
        var fossilColumns = FossilColumns(l, grave.Count, ctx);

        Sky(p, l, pal, ctx);
        if (l.Panel == CapPanelKind.Deep && l.Sun.Width > 0) Sun(p, l, pal, ctx);
        Water(p, l, pal);
        if (l.Panel == CapPanelKind.Deep && l.Sun.Width > 0) Glitter(p, l, pal, ctx);
        Floor(p, l, pal, data, ctx, fossilColumns);
        Fossils(p, l, pal, grave, ctx, fossilColumns);
        CapDeep.Writing(p, l, pal, data, ctx, fossilColumns);
    }

    // ---------------------------------------------------------------- sky and water

    private static void Sky(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx)
    {
        if (l.SkyRows <= 0) return;

        var (top, horizon) = CapSeaPalette.Sky(ctx.SkyHour);
        top = CapSheet.Mix(top, pal.Water[3], pal.Murk);
        horizon = CapSheet.Mix(horizon, pal.Water[1], pal.Murk);
        var night = CapSeaPalette.Night(ctx.SkyHour);
        uint[] ramp = [top, CapSheet.Mix(top, horizon, 0.35), CapSheet.Mix(top, horizon, 0.7), horizon];
        for (var y = 0; y < l.SurfaceY; y++)
        {
            for (var x = 0; x < l.Cols; x++)
            {
                p.Set(x, y, CapSeaPalette.Dithered(ramp, y / (double)Math.Max(1, l.SurfaceY - 1), x, y));
            }
        }

        // Fixed stars; they only show at night, and twinkle live.
        if (night > 0.2)
        {
            for (var k = 0; k < l.Cols / 6; k++)
            {
                var sx = CapArt.Hash(ctx.Seed + (ctx.WorldX / 3), k) % l.Cols;
                var sy = CapArt.Hash(ctx.Seed + 7, k) % Math.Max(1, l.SkyRows - 1);
                p.Set(sx, sy, 0xDCE4FFu, (int)(190 * night));
            }
        }
    }

    /// <summary>The sun going over the sky of the deep window by the hour of Alola, or the moon at night: low in the east, high at noon, low in the west.</summary>
    private static void Sun(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx)
    {
        var hour = ctx.SkyHour;
        var day = hour is >= 6 and < 19;
        var k = day ? (hour - 6) / 13.0 : (((hour + 5) % 24) / 11.0);
        k = Math.Clamp(k, 0, 1);
        var radius = day ? 8 : 6;
        var cx = l.Sun.Left + 14 + (int)(k * (l.Sun.Width - 28));
        var cy = l.SurfaceY - 3 - (int)(Math.Sin(Math.PI * k) * (l.SurfaceY - 3 - radius - 2));

        for (var y = cy - radius - 7; y <= cy + radius + 7; y++)
        {
            for (var x = cx - radius - 9; x <= cx + radius + 9; x++)
            {
                if (y >= l.SurfaceY) continue;
                var d = Math.Sqrt(((x - cx) * (x - cx) / 1.0) + ((y - cy) * (y - cy) / 1.0));
                if (d <= radius)
                {
                    var edge = d > radius - 1.6;
                    if (day) p.Set(x, y, CapSheet.Mix(edge ? 0xFFB84Au : (d < radius * 0.5 ? 0xFFFBE0u : 0xFFE890u), pal.Glow, pal.Murk * 0.8));
                    else
                    {
                        var crater = CapArt.Hash(ctx.Seed, (x - cx + 20) * 7 + (y - cy + 20)) % 41 == 0 && d < radius - 2;
                        p.Set(x, y, crater ? 0xA8B0CCu : edge ? 0xC4CCE4u : 0xE8EEFFu);
                    }
                }
                else if (d <= radius + 6 && ((x + y) & 1) == 0)
                {
                    p.Set(x, y, day ? 0xFFE8A0u : 0xBCC8F0u, (int)(70 * (1 - ((d - radius) / 6))));
                }
            }
        }
    }

    /// <summary>The glitter of the sun or the moon on the water under it: a short column of dither.</summary>
    private static void Glitter(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx)
    {
        var hour = ctx.SkyHour;
        var day = hour is >= 6 and < 19;
        var k = Math.Clamp(day ? (hour - 6) / 13.0 : (((hour + 5) % 24) / 11.0), 0, 1);
        var cx = l.Sun.Left + 14 + (int)(k * (l.Sun.Width - 28));
        for (var y = l.SurfaceY + 1; y < l.SurfaceY + 20; y++)
        {
            var spread = 2 + ((y - l.SurfaceY) / 3);
            for (var x = cx - spread; x <= cx + spread; x++)
            {
                if (CapArt.Hash(x, y + ctx.Seed) % 5 != 0) continue;
                p.Set(x, y, day ? 0xFFF0B0u : 0xD8E0FFu, (int)(120 * (1 - ((y - l.SurfaceY) / 20.0))));
            }
        }
    }

    private static void Water(CapSheet p, CapWorldLayout l, CapSeaPalette pal)
    {
        for (var y = Math.Max(0, l.SurfaceY); y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, WaterAt(l, pal, x, y));
        }
    }

    /// <summary>The water at a cell: the dithered ramp from the surface to the abyss, mottled slowly across the width so that its bands are not ruled straight.</summary>
    public static uint WaterAt(CapWorldLayout l, CapSeaPalette pal, int x, int y)
    {
        var top = Math.Max(0, l.SurfaceY);
        var span = Math.Max(1, l.FloorTop + 14 - top);
        var bias = (0.09 * Math.Sin((x * 0.057) + (y * 0.021))) + (0.05 * Math.Sin((x * 0.19) - (y * 0.06)));
        return CapSeaPalette.Dithered(pal.Water, (y - top) / (double)span, x, y, bias);
    }

    // ---------------------------------------------------------------- the floor

    private static void Floor(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, CapSeaContext ctx, int[] fossils)
    {
        // One stratum for each trial cleared, thick enough to read and no thicker than the room allows.
        var (strata, sandRows, layerRows) = CapDeep.StrataOf(l, data);

        for (var x = 0; x < l.Cols; x++)
        {
            var top = Relief(l, ctx, x, fossils);
            for (var y = top; y < l.Rows; y++)
            {
                var depth = y - top;
                p.Set(x, y, FloorColour(pal, ctx, x, y, depth, sandRows, layerRows, strata, l.Rows - top));
            }

            // The lit edge: one row of light, and a row of water-coloured haze over it, so the floor is lit from above.
            p.Set(x, top, CapSheet.Mix(pal.Floor[0], pal.Shaft, 0.35));
            if (top > 0) p.Set(x, top - 1, pal.Shaft, 40);
        }
    }

    private static uint FloorColour(CapSeaPalette pal, CapSeaContext ctx, int x, int y, int depth, int sandRows, int layerRows, int strata, int total)
    {
        var fade = Math.Clamp(depth / (double)Math.Max(1, total), 0, 1);
        uint colour;

        if (depth < sandRows || strata == 0)
        {
            colour = CapSeaPalette.Dithered(pal.Floor, Math.Min(1, depth / (double)Math.Max(1, strata == 0 ? total : sandRows * 3)), x, y);
        }
        else
        {
            // The strata wave a little from one column to the next, and each blends into the one beyond it with a row of dither.
            var wobble = (int)Math.Round(1.4 * Math.Sin((x * 0.09) + (depth * 0.05)));
            var into = depth - sandRows + wobble;
            var k = Math.Clamp(into / layerRows, 0, strata - 1);
            var edge = into - (k * layerRows);
            var own = StratumColour(pal, ctx, k);
            colour = own;
            if (edge == 0 && k > 0 && ((x + y) & 1) == 0) colour = StratumColour(pal, ctx, k - 1);
            if (edge == layerRows - 1 && k < strata - 1 && ((x + y) & 1) == 0) colour = StratumColour(pal, ctx, k + 1);
        }

        // Darker with depth, towards the blue of the abyss and not towards black.
        colour = CapSheet.Mix(colour, pal.Water[^1], fade * 0.55);

        // Grain: a speck here and there, lighter or darker.
        var grain = CapArt.Hash(x + ctx.WorldX, y * 3) % 31;
        if (grain == 0) colour = CapSheet.Lighter(colour, 0.14);
        else if (grain == 1) colour = CapSheet.Darker(colour, 0.2);
        return colour;
    }

    private static uint StratumColour(CapSeaPalette pal, CapSeaContext ctx, int k) => pal.Strata[(k + (ctx.Seed % pal.Strata.Length)) % pal.Strata.Length];

    // ---------------------------------------------------------------- the fossils

    private static void Fossils(CapSheet p, CapWorldLayout l, CapSeaPalette pal, IReadOnlyList<CapGrave> graves, CapSeaContext ctx, int[] columns)
    {
        var scale = FossilScale(l, graves.Count);
        for (var i = 0; i < graves.Count; i++)
        {
            var grave = graves[i];
            var x = columns[i];
            var ground = Relief(l, ctx, x, columns);
            if (ToastPixels.SpritePixels.From(grave.Sprite) is not { } pixels) continue;

            var w = Math.Max(1, pixels.Width / scale);
            var h = Math.Max(1, pixels.Height / scale);

            // Sunk to its middle in the sediment: only what is above the ground shows.
            var left = x - (w / 2);
            var topY = ground - ((h * 2) / 3);
            for (var sy = 0; sy < h; sy++)
            {
                for (var sx = 0; sx < w; sx++)
                {
                    var (found, c) = Sample(pixels, sx * scale, sy * scale, scale);
                    if (!found) continue;
                    var cx = left + sx;
                    var cy = topY + sy;
                    if (cy > Relief(l, ctx, cx, columns) + 1) continue;

                    // Stone: the colour's light turned into a cold grey-blue, with a lit edge on top.
                    var luma = ((0.30 * c.R) + (0.59 * c.G) + (0.11 * c.B)) / 255.0;
                    var stone = CapSheet.Mix(0x1E2A40u, 0xC4D0E4u, luma * 0.85);
                    var above = Sample(pixels, sx * scale, (sy - 1) * scale, scale).Found;
                    if (!above) stone = CapSheet.Lighter(stone, 0.3);
                    p.Set(cx, cy, stone);
                }
            }
        }
    }

    private static (bool Found, System.Windows.Media.Color Colour) Sample(ToastPixels.SpritePixels pixels, int x, int y, int scale)
    {
        if (y < 0) return (false, default);

        // The first solid pixel of the block it stands for, like the half-scale icons of the rest of the app.
        for (var dy = 0; dy < scale; dy++)
        {
            for (var dx = 0; dx < scale; dx++)
            {
                if (pixels.Solid(x + dx, y + dy)) return (true, pixels.At(x + dx, y + dy));
            }
        }

        return (false, default);
    }
}
