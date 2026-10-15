namespace PermaLocke.App.Views;

/// <summary>
/// Two things far off in the sky (2026-10-15, the approved prototype): the Sky Pillar on the horizon of the deep's window, and an Ultra Wormhole
/// in the sky of the party's. Both are mixed with the sky that is already there, so they take its hour and its weather; at night the pillar
/// goes dim and the wormhole keeps its light. Painted every frame (the pillar's gold breathes and its clouds drift; the wormhole's core pulses
/// and its trail drifts), on a 3 s loop of the mood's clock. Only in the tall band, and only where the sky has the room.
/// </summary>
public static class CapFarSky
{
    /// <param name="wormholeSize">How big the wormhole is drawn (1 its own size): a death can make it swell (2026-10-15).</param>
    /// <param name="wormholeAlpha">How much of it shows: it flickers while it charges.</param>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase, double wormholeSize = 1, double wormholeAlpha = 1)
    {
        if (l.Mode is not (CapMode.Column or CapMode.Slim)) return;

        var t = phase / 3;
        if (l.Panel == CapPanelKind.Deep) Pillar(p, l, pal, ctx, t);
        else Wormhole(p, l, t, wormholeSize, wormholeAlpha);
    }

    /// <summary>Where the wormhole's core is in the party's window, or null when it is not drawn there.</summary>
    public static (int X, int Y)? WormholeAt(CapWorldLayout l)
    {
        if (l.Mode is not (CapMode.Column or CapMode.Slim) || l.Panel != CapPanelKind.Party) return null;
        var cx = l.Main.Left + R(l.Main.Width * 0.49);
        var cy = R(l.SurfaceY * 0.43);
        var trail = Math.Min(72, l.Main.Right - 4 - (cx + 3));
        return cy < 24 || cx - 28 < l.Main.Left || trail < 30 ? null : (cx, cy);
    }

    /// <summary>The top of the Sky Pillar in the deep's window (in the middle of its crown), or null when it is not drawn there.</summary>
    public static (int X, int Y)? PillarTopAt(CapWorldLayout l)
    {
        if (l.Mode is not (CapMode.Column or CapMode.Slim) || l.Panel != CapPanelKind.Deep) return null;
        var cx = l.Main.Left + 42;
        return 49 + l.SurfaceY - 70 < 34 || l.Main.Width < 70 || l.InNotch(cx + 16, 2) ? null : (cx, 9);
    }

    /// <summary>Rounds as the prototype did (halves up), so the shapes are the ones approved.</summary>
    private static int R(double v) => (int)Math.Floor(v + 0.5);

    /// <summary>The prototype's hash, double arithmetic and all, so the cracks, the moss and the chips of the stone fall where they were approved.</summary>
    private static uint Hash(double a, double b)
    {
        static int ToInt32(double v) => unchecked((int)(long)v);
        var h = ToInt32((a * 374761393) + (b * 668265263));
        h = ToInt32((h ^ (int)((uint)h >> 13)) * 1274126177.0);
        return (uint)(h ^ (int)((uint)h >> 16));
    }

    /// <summary>A cell far away: its colour mixed with the sky already under it by <paramref name="haze"/>, then laid over it. Only in the sky.</summary>
    private static void Far(CapSheet p, CapWorldLayout l, int x, int y, uint c, double haze, int alpha = 255)
    {
        if (y >= l.SurfaceY || alpha <= 0) return;
        var under = p.Solid(x, y) ? p.At(x, y) : c;
        p.Set(x, y, CapSheet.Mix(c, under, haze), Math.Min(255, alpha));
    }

    // ---------------------------------------------------------------- the Sky Pillar

    private static readonly (uint Hi, uint Lit, uint Mid, uint Shade, uint Dark, uint Edge, uint Joint, uint Crack) Stone =
        (0xD8D6BA, 0xBEC0A4, 0xA2A68C, 0x82886E, 0x666C58, 0x3E4438, 0x8A8E76, 0x545A48);

    private static readonly (uint Hi, uint Mid, uint Dark) Gold = (0xFFE68A, 0xE8BE48, 0x9C7618);
    private static readonly uint[] Moss = [0x6CAE5E, 0x46844A];

    private static readonly (int X, int Y, double R)[] Puffs =
        [(-15, 11, 3), (-10, 9, 3.5), (-4, 8, 3.5), (3, 8, 3.5), (9, 9, 3.5), (15, 11, 3), (-12, 13, 3), (-5, 14, 3.5), (3, 14, 3.5), (11, 13, 3)];

    /// <summary>
    /// A tall, narrow tower (about five to one) of grey-beige stone with a green cast: marked blocks, two-tone side shading, a gold band at every
    /// change of section, gold circles of Rayquaza's mark set in the shaft, a base of four steps with green on its corners, a crown with
    /// battlements and a platform of white cloud round the top. Drawn for a sky of 70 rows; a shorter one shortens the shaft.
    /// </summary>
    private static void Pillar(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double t)
    {
        const int half = 6, crownTop = 8, shaftTop = 12;
        var shift = l.SurfaceY - 70;
        var length = 49 + shift;
        var cx = l.Main.Left + 42;
        if (length < 34 || l.Main.Width < 70 || l.InNotch(cx + 16, 2)) return;

        // Far away: it takes the colour of the sky (and of the weather, and of the night) by this much more than its own haze.
        var veil = (0.5 * CapSeaPalette.Night(ctx.SkyHour)) + (0.4 * pal.Murk);
        void Put(int x, int row, uint c, int alpha = 255, double haze = 0.12) => Far(p, l, cx + x, row, c, haze + ((1 - haze) * veil), alpha);

        var wave = 2 * Math.PI * t;
        var pulse = 0.5 + (0.5 * Math.Sin(wave));
        var shaftBottom = 61 + shift;
        int Row(int r) => shaftTop + R((r - shaftTop) * length / 49.0);
        int[] bands = [shaftTop, Row(27), Row(44), shaftBottom];
        int[] circles = [Row(32), Row(49)];
        (int Half, int Top)[] steps = [(13, 68 + shift), (11, 66 + shift), (9, 64 + shift), (8, 62 + shift)];

        // the cloud platform round the top, half of it behind the tower, drifting slowly
        var drift = Math.Sin(wave) * 1.2;
        void Clouds(bool front)
        {
            for (var n = 0; n < Puffs.Length; n++)
            {
                var (ox, oy, r) = Puffs[n];
                if (oy >= 12 != front) continue;
                for (var dy = -5; dy <= 5; dy++)
                {
                    for (var dx = -5; dx <= 5; dx++)
                    {
                        var d = Math.Sqrt((dx * dx) + (dy * 1.25 * dy * 1.25));
                        if (d > r) continue;
                        var c = dy > r * 0.4 ? 0xA4C4ECu : d > r - 1 ? 0xCCE6FCu : dx + dy < -r * 0.6 ? 0xFFFFFFu : 0xEEF6FFu;
                        Put(R(ox + (drift * (n % 2 == 1 ? 1 : -0.7))) + dx, oy + dy, c, 255, 0.06);
                    }
                }
            }
        }

        // the gold glow behind the top, breathing
        for (var dy = -8; dy <= 8; dy++)
        {
            for (var dx = -14; dx <= 14; dx++)
            {
                var d = Math.Sqrt(Math.Pow(dx / 14.0, 2) + Math.Pow(dy / 8.0, 2));
                if (d <= 1) Far(p, l, cx + dx, crownTop + 1 + dy, 0xFFE070u, 0, R(60 * (1 - d) * (0.35 + (0.65 * pulse))));
            }
        }

        Clouds(false);

        // the steps of the base, green on the corners
        for (var lvl = 0; lvl < steps.Length; lvl++)
        {
            var (h, top) = steps[lvl];
            for (var row = top; row < top + 2; row++)
            {
                for (var x = -h; x < h; x++)
                {
                    var c = row == top ? Stone.Hi : x < -h + 2 ? Stone.Lit : x >= h - 3 ? Stone.Shade : Stone.Mid;
                    if (row != top && (x + (lvl * 3) + 40) % 5 == 0) c = Stone.Joint;
                    if (Math.Abs(x + 0.5) > h - 3 && Hash(x + 70, row) % 2 == 0) c = Moss[Hash(row, x) % 2];
                    Put(x, row, c);
                }

                Put(-h - 1, row, Stone.Edge);
                Put(h, row, Stone.Edge);
            }

            Put(-h, top - 1, Moss[0]);                                                 // a tuft on each corner
            Put(h - 1, top - 1, Moss[0]);
        }

        // the shaft: courses of blocks, staggered joints, the left lit and the right shaded in two tones, a crack here and there
        for (var row = shaftTop; row <= shaftBottom; row++)
        {
            var course = row / 3;
            var joint = row % 3 == 2;
            for (var x = -half; x < half; x++)
            {
                var c = x < -half + 2 ? Stone.Hi : x < 0 ? Stone.Lit : x < half - 3 ? Stone.Mid : Stone.Shade;
                if (joint || (x + 12 + ((course % 2) * 2)) % 4 == 0) c = x >= half - 3 ? Stone.Dark : Stone.Joint;
                if (Hash(x + 30, row) % 41 == 0) c = Stone.Crack;
                Put(x, row, c);
            }

            Put(-half - 1, row, Stone.Edge);
            Put(half, row, Stone.Edge);
        }

        // a gold band at each change of section: a ledge a cell wider each side, its shadow under it
        foreach (var row in bands)
        {
            for (var x = -half - 1; x <= half; x++)
            {
                Put(x, row, x < 0 ? Gold.Hi : Gold.Mid);
                Put(x, row + 1, x < half - 2 ? Gold.Mid : Gold.Dark);
            }

            Put(-half - 2, row, Stone.Edge);
            Put(half + 1, row, Stone.Edge);
            for (var x = -half; x < half; x++) Put(x, row + 2, Stone.Dark, 120);
        }

        // the circles of the mark: a gold ring, a dark round inside with a gold dot, a stroke above and below; they glow with the breath
        foreach (var oy in circles)
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var d = Math.Sqrt((dx * dx) + (dy * dy));
                    if (d > 2.2 && d <= 3.3) Put(dx, oy + dy, dx + dy < 0 ? Gold.Hi : dx + dy > 2 ? Gold.Dark : Gold.Mid);
                    else if (d <= 2.2) Put(dx, oy + dy, d < 0.8 ? Gold.Hi : 0x4E5444u);
                }
            }

            Put(0, oy - 4, Gold.Mid);
            Put(0, oy + 4, Gold.Mid);
            for (var dy = -5; dy <= 5; dy++)
            {
                for (var dx = -5; dx <= 5; dx++)
                {
                    var d = Math.Sqrt((dx * dx) + (dy * dy));
                    if (d <= 5) Far(p, l, cx + dx, oy + dy, 0xFFF0A0u, 0, R(70 * (1 - (d / 5)) * pulse));
                }
            }
        }

        // the crown: a block a cell wider, a gold rim on top and battlements
        for (var row = crownTop; row < shaftTop; row++)
        {
            for (var x = -half - 1; x <= half; x++)
            {
                Put(x, row, row == crownTop ? Gold.Hi : x < -half + 1 ? Stone.Hi : x < 1 ? Stone.Lit : x < half - 2 ? Stone.Mid : Stone.Shade);
            }
        }

        foreach (var bx in new[] { -half - 1, -3, 2, half }) Put(bx, crownTop - 1, Stone.Lit);

        Clouds(true);
    }

    // ---------------------------------------------------------------- the Ultra Wormhole

    private const double CoreRx = 4.5, CoreRy = 8;

    /// <summary>The cracks, made once: from the rim of the core outward, straight runs each with a small turn, a branch now and then, and a few pieces across.</summary>
    private static readonly (double X, double Y)[][] Cracks = MakeCracks();

    private static (double X, double Y)[][] MakeCracks()
    {
        double seed = 977;
        double Rnd()
        {
            seed = Hash(seed, 31);
            return seed % 10000 / 10000;
        }

        var cracks = new List<(double, double)[]>();
        for (var i = 0; i < 18; i++)
        {
            var a = (i / 18.0 * 2 * Math.PI) + ((Rnd() - 0.5) * 0.25);
            var x = Math.Cos(a) * (CoreRx + 0.5);
            var y = Math.Sin(a) * (CoreRy + 0.5);
            var len = 9 + (Rnd() * 10);
            var points = new List<(double, double)> { (x, y) };
            var run = 0.0;
            while (run < len)
            {
                var step = 2.5 + (Rnd() * 3);
                a += (Rnd() - 0.5) * 0.45;
                x += Math.Cos(a) * step;
                y += Math.Sin(a) * step * 0.95;
                run += step;
                points.Add((x, y));
                if (Rnd() < 0.3 && run > 4)
                {
                    var branch = a + (Rnd() < 0.5 ? 0.75 : -0.75);
                    var branchLength = 2.5 + (Rnd() * 3);
                    cracks.Add([(x, y), (x + (Math.Cos(branch) * branchLength), y + (Math.Sin(branch) * branchLength))]);
                }
            }

            cracks.Add([.. points]);
        }

        for (var i = 0; i < 7; i++)
        {
            var r = 9 + (Rnd() * 8);
            var a0 = Rnd() * 2 * Math.PI;
            var a1 = a0 + 0.25 + (Rnd() * 0.2);
            cracks.Add([(Math.Cos(a0) * r * 1.05, Math.Sin(a0) * r), (Math.Cos(a1) * r * 1.05, Math.Sin(a1) * r)]);
        }

        return [.. cracks];
    }

    /// <summary>
    /// After the official art (day, night and the side view): a white core, round and very bright; white cracks running out of it like a pane
    /// broken from the centre, still; a soft envelope of violet, pink and cyan round it; the net of its side view, a funnel of thin lines with no
    /// fill narrowing away from the opening and bright inside; and a long cyan and violet trail along it, fading into the sky.
    /// </summary>
    private static void Wormhole(CapSheet p, CapWorldLayout l, double t, double size = 1, double shown = 1)
    {
        if (WormholeAt(l) is not { } at) return;
        var (cx, cy) = at;
        var trail = Math.Min(72, l.Main.Right - 4 - (cx + 3));

        // bigger than its own size, each cell of it is a block of cells, as far from the core as it is made bigger
        var block = Math.Max(1, (int)Math.Ceiling(size));
        void Put(double x, double y, uint c, double alpha)
        {
            var a = Math.Clamp(R(alpha * shown), 0, 255);
            if (size == 1) { Far(p, l, cx + (int)x, cy + (int)y, c, 0.04, a); return; }
            int bx = cx + R((int)x * size), by = cy + R((int)y * size);
            for (var v = 0; v < block; v++) for (var u = 0; u < block; u++) Far(p, l, bx + u, by + v, c, 0.04, a);
        }
        void Line(double x0, double y0, double x1, double y1, uint c, double alpha)
        {
            int ax = R(x0), ay = R(y0), bx = R(x1), by = R(y1);
            int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay), sx = ax < bx ? 1 : -1, sy = ay < by ? 1 : -1, err = dx + dy;
            while (true)
            {
                Put(ax, ay, c, alpha);
                if (ax == bx && ay == by) return;
                var e2 = 2 * err;
                if (e2 >= dy) { err += dy; ax += sx; }
                if (e2 <= dx) { err += dx; ay += sy; }
            }
        }

        var pulse = 0.5 + (0.5 * Math.Sin(2 * Math.PI * t));

        // the envelope: violet all round, pink and a touch of orange on the open side, cyan close to the core
        for (var dy = -24; dy <= 24; dy++)
        {
            for (var dx = -27; dx <= 27; dx++)
            {
                var d = Math.Sqrt(Math.Pow(dx / 25.0, 2) + Math.Pow(dy / 23.0, 2));
                if (d > 1) continue;
                var left = Math.Max(0, -dx / 25.0);
                var c = CapSheet.Mix(0x7A48D8u, 0xF08CC8u, Math.Min(1, left * 1.5));
                if (left > 0.45 && Math.Abs(dy) < 8) c = CapSheet.Mix(c, 0xF8B888u, (left - 0.45) * 0.8);
                if (d < 0.4) c = CapSheet.Mix(c, 0x8CF4E8u, (0.4 - d) / 0.4);
                Put(dx, dy, c, 235 * (1 - (d * d)));
            }
        }

        // the trail: a long band of cyan into violet, going right and a little up along the tunnel, fading; it drifts a little
        var drift = Math.Sin(2 * Math.PI * t) * 1.2;
        for (var dy = -22; dy <= 8; dy++)
        {
            for (var dx = 0; dx <= trail + 4; dx++)
            {
                var along = dx / (double)trail;
                var axis = -2 - (along * 12) + (drift * along);
                var width = 7 * (1 - (along * 0.5));
                var off = Math.Abs(dy - axis) / width;
                if (along > 1 || off > 1) continue;
                var c = CapSheet.Mix(0x5CFFE8u, 0xB06CFFu, Math.Min(1, along * 1.1));
                var shimmer = 0.82 + (0.18 * Math.Sin((along * 14) - (2 * Math.PI * t)));
                Put(dx + 3, dy, c, 250 * Math.Pow(1 - along, 0.75) * (1 - (off * off)) * shimmer);
            }
        }

        // the net seen from the side: rings shrinking away from the opening and lines along it, thin and unfilled; the inside a little bright
        var rings = Enumerable.Range(0, 7).Select(i => (X: 4 + (i * 8) - (i * i * 0.25), Ry: 11 * (1 - (i * 0.1)), Rx: 11 * (1 - (i * 0.1)) * 0.3)).ToArray();
        for (var i = 0; i + 1 < rings.Length; i++)
        {
            var (a, b) = (rings[i], rings[i + 1]);
            for (var x = (int)Math.Ceiling(a.X); x < b.X; x++)
            {
                var ry = a.Ry + ((b.Ry - a.Ry) * ((x - a.X) / (b.X - a.X)));
                for (var y = -(int)Math.Floor(ry); y <= ry; y++) Put(x, y, 0xC8FFF8u, 60 * (1 - (i / 7.0)));
            }
        }

        for (var i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            (double X, double Y)? previous = null;
            for (var s = 0.0; s <= 1.0001; s += 1 / 40.0)
            {
                var angle = s * 2 * Math.PI;
                var point = (X: r.X + (Math.Cos(angle) * r.Rx), Y: Math.Sin(angle) * r.Ry);
                if (previous is { } from) Line(from.X, from.Y, point.X, point.Y, 0xE6FAFFu, (Math.Cos(angle) > 0 ? 120 : 70) * (1 - (i * 0.11)));
                previous = point;
            }
        }

        for (var j = 0; j < 10; j++)
        {
            var angle = (j / 10.0 * 2 * Math.PI) + 0.3;
            for (var i = 0; i + 1 < rings.Length; i++)
            {
                var (a, b) = (rings[i], rings[i + 1]);
                Line(a.X + (Math.Cos(angle) * a.Rx), Math.Sin(angle) * a.Ry, b.X + (Math.Cos(angle) * b.Rx), Math.Sin(angle) * b.Ry, 0xE6FAFFu, 85 * (1 - (i * 0.13)));
            }
        }

        // the cracks, thin and white, still; never over the core
        foreach (var crack in Cracks)
        {
            for (var i = 0; i + 1 < crack.Length; i++)
            {
                int ax = R(crack[i].X), ay = R(crack[i].Y), bx = R(crack[i + 1].X), by = R(crack[i + 1].Y);
                int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay), sx = ax < bx ? 1 : -1, sy = ay < by ? 1 : -1, err = dx + dy;
                while (true)
                {
                    if (Math.Pow(ax / (CoreRx + 0.5), 2) + Math.Pow(ay / (CoreRy + 0.5), 2) >= 1) Put(ax, ay, 0xFFFFFFu, 235);
                    if (ax == bx && ay == by) break;
                    var e2 = 2 * err;
                    if (e2 >= dy) { err += dy; ax += sx; }
                    if (e2 <= dx) { err += dx; ay += sy; }
                }
            }
        }

        // the core: a bloom round it, a cyan rim on the far side, the white body; it pulses softly
        var grow = 0.5 * pulse;
        for (var dy = -13; dy <= 13; dy++)
        {
            for (var dx = -9; dx <= 9; dx++)
            {
                var d = Math.Sqrt(Math.Pow(dx / (CoreRx + grow), 2) + Math.Pow(dy / (CoreRy + grow), 2));
                if (d <= 1) Put(dx, dy, d > 0.82 && dx > 1 ? 0xA8FFF0u : 0xFFFFFFu, 255);
                else if (d < 1.6) Put(dx, dy, 0xF4FFFFu, (150 + (60 * pulse)) * (1.6 - d) / 0.6);
            }
        }
    }
}
