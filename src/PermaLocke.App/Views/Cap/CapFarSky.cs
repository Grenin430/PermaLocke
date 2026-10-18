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
        else Wormhole(p, l, t, wormholeSize, wormholeAlpha, CapSeaPalette.Night(ctx.SkyHour), phase);
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

    internal const double CoreRx = 8, CoreRy = 13;
    private const double CoreTilt = -0.17;
    private const int Tunnel = 72;

    /// <summary>One bolt of the radial fractures, from the rim of the core outward; a branch is drawn a little dimmer.</summary>
    private sealed record Bolt((double X, double Y)[] Points, bool Branch);

    /// <summary>The fractures, made once: jagged bolts all round the core, fewer and shorter on the right where it opens into the tunnel.</summary>
    private static readonly Bolt[] Bolts = MakeBolts();

    private static Bolt[] MakeBolts()
    {
        double seed = 4111;
        double Rnd()
        {
            seed = Hash(seed, 57);
            return seed % 10000 / 10000;
        }

        // Ordered as in the art: rays evenly spread round the core, each a jagged line straight out (starting at the rim or at the first ring
        // of the net), small ticks off its sides, and a forked tip; the tips all end near one circle, so together they draw a round burst.
        var bolts = new List<Bolt>();
        void Twig(double x, double y, double a, double length) =>
            bolts.Add(new Bolt([(x, y), (x + (Math.Cos(a) * length), y + (Math.Sin(a) * length))], true));

        const int rays = 16;
        for (var i = 0; i < rays; i++)
        {
            var a = (i / (double)rays * 2 * Math.PI) + ((Rnd() - 0.5) * 0.12);
            var right = Math.Cos(a);
            if (right > 0.8 && i % 2 == 1) continue;
            var (x, y) = Rim(a, i % 3 == 0 ? 2.5 : 0.4);
            var end = (right > 0.5 ? 22.5 : 28) + ((Rnd() - 0.5) * 3);
            var points = new List<(double X, double Y)> { (x, y) };
            var dist = Math.Sqrt((x * x) + (y * y));
            while (dist < end - 3)
            {
                dist += 2 + (Rnd() * 1.5);
                var side = (Rnd() - 0.5) * 2.2;
                points.Add(((Math.Cos(a) * dist) - (Math.Sin(a) * side), (Math.Sin(a) * dist) + (Math.Cos(a) * side)));
                if (Rnd() < 0.15) Twig(points[^1].X, points[^1].Y, a + ((Rnd() < 0.5 ? 1 : -1) * (0.8 + (Rnd() * 0.4))), 1.5 + (Rnd() * 1.5));
            }

            var tipAngle = a + ((Rnd() - 0.5) * 0.3);
            points.Add((Math.Cos(tipAngle) * end, Math.Sin(tipAngle) * end));
            bolts.Add(new Bolt([.. points], false));
            var (fx, fy) = points[^2];
            Twig(fx, fy, a + 0.35 + (Rnd() * 0.25), 2.5 + (Rnd() * 1.5));
            if (Rnd() < 0.5) Twig(fx, fy, a - 0.35 - (Rnd() * 0.25), 2.5 + (Rnd() * 1.5));
        }

        return [.. bolts];
    }

    /// <summary>A point on the rim of the core (a tilted oval) at angle <paramref name="a"/>, pushed out by <paramref name="extra"/> cells.</summary>
    private static (double X, double Y) Rim(double a, double extra)
    {
        double x = Math.Cos(a) * (CoreRx + extra), y = Math.Sin(a) * (CoreRy + extra);
        return ((x * Math.Cos(CoreTilt)) - (y * Math.Sin(CoreTilt)), (x * Math.Sin(CoreTilt)) + (y * Math.Cos(CoreTilt)));
    }

    /// <summary>How far out of the core a cell is: under 1 inside it.</summary>
    private static double CoreDistance(double x, double y, double grow)
    {
        double u = (x * Math.Cos(CoreTilt)) + (y * Math.Sin(CoreTilt)), v = (-x * Math.Sin(CoreTilt)) + (y * Math.Cos(CoreTilt));
        return Math.Sqrt(Math.Pow(u / (CoreRx + grow), 2) + Math.Pow(v / (CoreRy + grow), 2));
    }

    private static double Frac(uint h) => h % 10000 / 10000.0;

    /// <summary>The broken rings of the net round the mouth: size against the core, how far left, up to which side, how bright.</summary>
    private static readonly (double Scale, double Shift, double Upto, double Alpha)[] Webs = [(1.22, 0.5, 2, 200), (1.6, 3, 0.35, 170), (2.0, 6, 0.1, 140)];

    /// <summary>The night's two hexagons of lens flare: where and how big.</summary>
    private static readonly (double X, double Y, double R)[] Flares = [(-35, -1, 4.5), (-28, 12, 3)];

    /// <summary>A point of a ring of the tunnel: its offset from the axis turned as the core is, so every ring leans like the mouth.</summary>
    private static (double X, double Y) Tilted(double x, double y, double ox, double oy) =>
        (x + (ox * Math.Cos(CoreTilt)) - (oy * Math.Sin(CoreTilt)), y + (ox * Math.Sin(CoreTilt)) + (oy * Math.Cos(CoreTilt)));

    /// <summary>
    /// After the official concept art (day, night, the side view and its three images): a tall white oval, blinding; jagged white fractures
    /// out of it all round like broken glass struck by lightning, which crackle; a glow of violet, magenta and pink round it with a warm touch on
    /// the left; behind it a tunnel of thin white lines (rings and runs along it) narrowing to the right, bright inside, an aurora of cyan along
    /// its top and violet along its bottom flowing away; at night two pale hexagons of lens flare on the left.
    /// </summary>
    private static void Wormhole(CapSheet p, CapWorldLayout l, double t, double size = 1, double shown = 1, double night = 0, double seconds = 0)
    {
        if (WormholeAt(l) is not { } at) return;
        var (cx, cy) = at;
        var length = Math.Min(Tunnel, l.Main.Right - 4 - cx);

        // bigger than its own size, each cell of it is a block of cells, as far from the core as it is made bigger
        var block = Math.Max(1, (int)Math.Ceiling(size));
        void Put(double x, double y, uint c, double alpha)
        {
            var a = Math.Clamp(R(alpha * shown), 0, 255);
            if (size == 1) { Far(p, l, cx + R(x), cy + R(y), c, 0.04, a); return; }
            int bx = cx + R(R(x) * size), by = cy + R(R(y) * size);
            for (var v = 0; v < block; v++) for (var u = 0; u < block; u++) Far(p, l, bx + u, by + v, c, 0.04, a);
        }
        // A line of cells; `outside` keeps it off the core; `reach` paints a halo that far round each cell instead of the cell itself (no
        // delegates, so a frame allocates nothing).
        void Line(double x0, double y0, double x1, double y1, uint c, double alpha, bool outside = false, int reach = 0)
        {
            int ax = R(x0), ay = R(y0), bx = R(x1), by = R(y1);
            int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay), sx = ax < bx ? 1 : -1, sy = ay < by ? 1 : -1, err = dx + dy;
            while (true)
            {
                if (reach == 0)
                {
                    if (!outside || CoreDistance(ax, ay, 0) >= 1) Put(ax, ay, c, alpha);
                }
                else
                {
                    for (var v = -reach; v <= reach; v++)
                        for (var u = -reach; u <= reach; u++)
                            if ((u != 0 || v != 0) && CoreDistance(ax + u, ay + v, 0) >= 1) Put(ax + u, ay + v, c, alpha / Math.Max(Math.Abs(u), Math.Abs(v)));
                }

                if (ax == bx && ay == by) return;
                var e2 = 2 * err;
                if (e2 >= dy) { err += dy; ax += sx; }
                if (e2 <= dx) { err += dx; ay += sy; }
            }
        }

        var wave = 2 * Math.PI * t;
        var pulse = 0.5 + (0.5 * Math.Sin(wave));
        double Axis(double x) => -x * 0.14;
        double Half(double x) => (CoreRy + 5) * (1 - (0.65 * Math.Pow(Math.Clamp(x / length, 0, 1), 0.9)));

        // the glow: violet outside, magenta and pink nearer, pale pink by the core, a warm orange touch on the left and bluer on the right
        for (var dy = -38; dy <= 38; dy++)
        {
            for (var dx = -48; dx <= 38; dx++)
            {
                var d = Math.Sqrt(Math.Pow((dx + 4) / 44.0, 2) + Math.Pow(dy / 36.0, 2));
                if (d > 1) continue;
                var c = CapSheet.Mix(0x7048E0u, 0xE050D0u, Math.Clamp((0.9 - d) / 0.45, 0, 1));
                c = CapSheet.Mix(c, 0xFFC8F0u, Math.Clamp((0.35 - d) / 0.25, 0, 1));
                if (dx > 0) c = CapSheet.Mix(c, 0x5A7CF0u, dx / 40.0);
                var warm = Math.Sqrt(Math.Pow((dx + 12) / 9.0, 2) + Math.Pow((dy + 4) / 8.0, 2));
                if (warm < 1) c = CapSheet.Mix(c, 0xFFA868u, (1 - warm) * 0.8);
                Put(dx, dy, c, Math.Min(255, (300 + (40 * pulse)) * Math.Pow(1 - d, 1.1)));
            }
        }

        // the tunnel's inside: bright by the mouth, an aurora of cyan along the top and violet along the bottom flowing away, a haze past its end
        for (var dx = 0; dx <= length + 10; dx++)
        {
            var along = dx / (double)length;
            var axis = Axis(dx);
            var half = Half(dx);
            for (var dy = (int)Math.Floor(axis - half); dy <= axis + half; dy++)
            {
                var off = (dy - axis) / half;
                if (Math.Abs(off) > 1) continue;
                var fade = along > 1 ? Math.Max(0, 1 - ((along - 1) * length / 10)) * 0.35 : Math.Pow(1 - (along * 0.85), 0.8);
                var flow = 0.6 + (0.4 * Math.Sin((along * 15) - (wave * 2)));
                var ripple = 0.08 * Math.Sin((along * 9) + wave);
                var c = CapSheet.Mix(0x6A50E0u, 0x6C9CF8u, along);
                var alpha = 185.0;
                var top = Math.Max(0, 1 - (Math.Abs(off + 0.5 + ripple) / 0.45));
                var bottom = Math.Max(0, 1 - (Math.Abs(off - 0.45 - ripple) / 0.35));
                if (top > 0) { c = CapSheet.Mix(c, 0x58FFD8u, Math.Min(1, top * flow * 1.3)); alpha += 200 * top * flow; }
                if (bottom > 0) { c = CapSheet.Mix(c, 0xC27CFFu, bottom * flow * 0.8); alpha += 70 * bottom * flow; }
                if (along < 0.25) { var mouth = 1 - (along / 0.25); c = CapSheet.Mix(c, 0xC8FAFFu, mouth * 0.7); alpha += 90 * mouth; }
                Put(dx, dy, c, alpha * fade * (1 - (Math.Pow(off, 6) * 0.6)));
            }
        }

        // the net: rings that run away from the mouth (closer and closer as they go) and lines along the tunnel, thin, white and unfilled
        for (var i = 0; i < 7; i++)
        {
            var u = ((i + (t * 2)) / 7) % 1;
            var x = length * (1 - Math.Pow(1 - u, 1.6));
            var half = Half(x);
            var alpha = 150 * Math.Min(1, u / 0.12) * Math.Pow(1 - (x / length), 0.7);
            (double X, double Y)? previous = null;
            for (var s = 0.0; s <= 1.0001; s += 1 / 36.0)
            {
                var angle = s * 2 * Math.PI;
                var point = Tilted(x, Axis(x), Math.Cos(angle) * half * 0.3, Math.Sin(angle) * half);
                var a = alpha * (Math.Cos(angle) > 0 ? 1 : 0.6);
                if (previous is { } from) Line(from.X, from.Y, point.X, point.Y, 0xEAF6FFu, a);
                previous = point;
            }
        }

        for (var j = 0; j < 9; j++)
        {
            var angle = (j / 9.0 * 2 * Math.PI) + 0.35;
            (double X, double Y) P(double at) => Tilted(at, Axis(at), Math.Cos(angle) * Half(at) * 0.3, Math.Sin(angle) * Half(at));
            for (var x = 0.0; x + 3 <= length; x += 3)
            {
                var a = 130 * Math.Pow(1 - (x / length), 0.8) * (Math.Cos(angle) > 0 ? 1 : 0.65);
                var (x0, y0) = P(x);
                var (x1, y1) = P(x + 3);
                Line(x0, y0, x1, y1, 0xEAF6FFu, a);
            }
        }

        // the net round the mouth (marked by the user on the art): broken rings round the core, each bigger one further left, as the funnel
        // flares open towards us, with short ticks across between them; the first hugs the core all round, the others only on its open side
        for (var k = 0; k < Webs.Length; k++)
        {
            var (scale, shift, upto, alpha) = Webs[k];
            (double X, double Y) On(double angle, double grow)
            {
                var (rx, ry) = Rim(angle, 0);
                return ((rx * (scale + grow)) - shift, ry * (scale + grow));
            }

            const int steps = 96;
            for (var s = 0; s < steps; s++)
            {
                var a0 = s / (double)steps * 2 * Math.PI;
                if (Math.Cos(a0) > upto || Hash(k + 11, s / 3) % 10 >= 7) continue;
                var (x0, y0) = On(a0, 0);
                var (x1, y1) = On(a0 + (2 * Math.PI / steps), 0);
                Line(x0, y0, x1, y1, 0xF0F8FFu, alpha, outside: true);
            }

            if (k + 1 == Webs.Length) continue;
            for (var n = 0; n < 9; n++)
            {
                var a = Frac(Hash(k + 21, n)) * 2 * Math.PI;
                if (Math.Cos(a) > Webs[k + 1].Upto) continue;
                var (x0, y0) = On(a, 0.05);
                var (x1, y1) = On(a, 0.05 + ((Webs[k + 1].Scale - scale) * 0.6));
                Line(x0, y0, x1, y1, 0xF0F8FFu, 90);
            }
        }

        // at night, two pale hexagons of lens flare on the left
        if (night > 0.05)
        {
            foreach (var (hx, hy, hr) in Flares)
            {
                for (var dy = -4; dy <= 4; dy++)
                {
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        var e = Math.Max(Math.Abs(dy) / 0.866, (Math.Abs(dy) * 0.577) + Math.Abs(dx)) / hr;
                        if (e <= 1) Put(hx + dx, hy + dy, 0xC8A8FFu, night * (e > 0.75 ? 80 : 45));
                    }
                }
            }
        }

        // the fractures: a lavender glow under each, then the white bolt; they crackle ten times a second (the inner points shake, the tips
        // come and go, one now and then flashes)
        var tick = Math.Floor(seconds * 10);
        (double X, double Y) Shaken(int b, int i)
        {
            var (x, y) = Bolts[b].Points[i];
            if (i == 0) return (x, y);
            return (x + ((Frac(Hash(tick, (b * 131) + (i * 7))) - 0.5) * 1.1), y + ((Frac(Hash(tick + 7, (b * 131) + (i * 7))) - 0.5) * 1.1));
        }

        for (var pass = 0; pass < 2; pass++)
        {
            for (var b = 0; b < Bolts.Length; b++)
            {
                var count = Bolts[b].Points.Length;
                if (count > 2 && Frac(Hash(tick, (b * 17) + 3)) < 0.3) count--;
                var light = (Bolts[b].Branch ? 0.8 : 1) * (0.72 + (0.28 * Frac(Hash(tick, b + 900))));
                var flash = Hash(tick, b + 400) % 14 == 0;
                for (var i = 0; i + 1 < count; i++)
                {
                    var (x0, y0) = Shaken(b, i);
                    var (x1, y1) = Shaken(b, i + 1);
                    if (pass == 0) Line(x0, y0, x1, y1, 0xD6B4FFu, (flash ? 90 : 30) * light, reach: flash ? 2 : 1);
                    else Line(x0, y0, x1, y1, 0xFFFFFFu, flash ? 255 : 255 * light, outside: true);
                }
            }
        }

        // the core: the white oval, a pale cyan rim on its right where the tunnel opens, a bloom round it; it pulses softly
        var grow = 0.5 * pulse;
        for (var dy = -24; dy <= 24; dy++)
        {
            for (var dx = -16; dx <= 16; dx++)
            {
                var d = CoreDistance(dx, dy, grow);
                if (d <= 1) Put(dx, dy, d > 0.85 && dx > 2 ? 0xC8FFF8u : 0xFFFFFFu, 255);
                else if (d < 1.7) Put(dx, dy, 0xF8F0FFu, (170 + (60 * pulse)) * (1.7 - d) / 0.7);
            }
        }
    }
}
