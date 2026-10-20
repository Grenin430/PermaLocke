namespace PermaLocke.App.Views;

/// <summary>What the sea depends on that is not the party: the seed of the run, the hour of Alola, and where this window sits in the one sea.</summary>
/// <param name="Seed">A number out of the run: its relief, its coral and its rare events are the same every time and different from another run's.</param>
/// <param name="Hour">The game's hour, 0 to 24, or -1 when it cannot be known (then it is mid-morning).</param>
/// <param name="WorldX">The column of the sea this window's first column is: the party's window is further along than the deep's, so the floor runs on from one to the other and whatever swims behind the game comes out of the other side.</param>
/// <param name="WorldWidth">How many columns the whole sea is: the deep, the screens of the game, and the party.</param>
/// <param name="Megas">The Mega Evolution is unlocked (the sixth trial cleared, 2026-10-16): the visitors that have a Mega form pass in it.</param>
/// <param name="Parade">The Megas' parade is on (CapMega): the visitors keep away.</param>
/// <param name="Shiny">Seconds since a shiny's rainbow began (2026-10-17), -1 without one: the passing Pokémon then come out in their shiny colours.</param>
/// <param name="Rise">How far the Gimmighoul ruins' buried storey has risen out of the sand, 0 to 1 (2026-10-19, after the Electrostal): the ruins stand on it.</param>
/// <param name="Rising">Seconds since it started rising while that is being shown, -1 otherwise.</param>
public sealed record CapSeaContext(int Seed = 0, double Hour = -1, int WorldX = 0, int WorldWidth = 0, bool Megas = false, bool Parade = false,
    double Shiny = -1, double Rise = 0, double Rising = -1)
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
        // The fallen that drift in the water (CapFossils.InWater) leave the sand as it is.
        // The fallen are not in the overlay any more (2026-10-15): after its death a Pokémon is kept by the cemetery, not by the sea floor.
        int[] fossilColumns = [];

        Sky(p, l, pal, ctx);
        if (l.Panel == CapPanelKind.Deep && l.Sun.Width > 0) Sun(p, l, pal, ctx);
        Water(p, l, pal);
        // The glitter of the sun or the moon on the water under it is gone (2026-10-20, asked by the player).
        Floor(p, l, pal, data, ctx, fossilColumns);
        // The ruins, the crystals and the old fossils on the deep's sand (2026-10-15); the fossils of the fallen lie in front of them.
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
    /// <summary>Where the sun (or the moon at night) is in the sky of the deep: the light of the focus comes down from there.</summary>
    public static (int X, int Y) SunAt(CapWorldLayout l, CapSeaContext ctx)
    {
        var hour = ctx.SkyHour;
        var day = hour is >= 6 and < 19;
        var k = Math.Clamp(day ? (hour - 6) / 13.0 : (((hour + 5) % 24) / 11.0), 0, 1);
        var radius = day ? 8 : 6;
        return (l.Sun.Left + 14 + (int)(k * (l.Sun.Width - 28)), l.SurfaceY - 3 - (int)(Math.Sin(Math.PI * k) * (l.SurfaceY - 3 - radius - 2)));
    }

    private static void Sun(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx)
    {
        var hour = ctx.SkyHour;
        var day = hour is >= 6 and < 19;
        var radius = day ? 8 : 6;
        var (cx, cy) = SunAt(l, ctx);

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
        // The fallen that wander in the water move: they are painted every frame (CapFossils.PaintDrifting), not into the scene.
        if (CapFossils.InWater(l)) return;

        foreach (var fossil in CapFossils.Build(l, graves, ctx, columns))
        {
            // A dark rim round the stone, so that it stands out from the sand and from the water whatever is behind it.
            if (l.Panel == CapPanelKind.Deep) foreach (var (rx, ry) in fossil.Rim) p.Set(rx, ry, 0x0A1224u, 170);
            foreach (var cell in fossil.Cells) p.Set(cell.X, cell.Y, cell.Stone);
            Overgrowth(p, l, pal, fossil.Spec, ctx, columns);
        }
    }

    /// <summary>
    /// What the days have done to a fossil: a strand of kelp for every three days it has lain there, up to four, and from the ninth day a little coral
    /// on its shoulder. Still, painted into the scene with the fossil.
    /// </summary>
    private static void Overgrowth(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapFossilSpec spec, CapSeaContext ctx, int[] columns)
    {
        // The terrace of a fossil of the back row: a mound of dark sand from where it lies down to the floor in front.
        if (spec.Back)
        {
            for (var x = spec.Left - 3; x < spec.Left + spec.Width + 3; x++)
            {
                var edge = Math.Min(x - (spec.Left - 3), (spec.Left + spec.Width + 2) - x);
                for (var y = spec.Ground + 1 + Math.Max(0, 3 - edge); y < Relief(l, ctx, x, columns); y++) p.Set(x, y, ((x + y) & 1) == 0 ? 0x5A6A6Eu : 0x4A5A62u);
            }
        }

        for (var k = 0; k < spec.Growth; k++)
        {
            var bx = spec.Left + (((k * 5) + 1) % Math.Max(3, spec.Width));
            var by = Relief(l, ctx, bx, columns);
            var height = 4 + (CapArt.Hash(spec.Index, k) % 4);
            for (var h = 0; h < height; h++)
            {
                var lean = h / 3 * (k % 2 == 0 ? 1 : -1);
                p.Set(bx + lean, by - h, h % 2 == 0 ? 0x2F8A74u : 0x246E62u);
            }
        }

        if (spec.Growth >= 3)
        {
            var cx = spec.Left + spec.Width - 1;
            p.Set(cx, spec.Top + 1, 0xC8607Au);
            p.Set(cx - 1, spec.Top + 2, 0xE890A8u);
            p.Set(cx + 1, spec.Top + 2, 0xC8607Au);
        }

        _ = pal;
    }
}
