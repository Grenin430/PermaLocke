namespace PermaLocke.App.Views;

/// <summary>
/// The Incubadora Turbo in the party's window (2026-10-16, prototype): the egg of the bag icon with volume, rendered once from a small 3D model
/// (an egg wider below, cream lit from the upper left, green spots round it) with the icon's golden lightning bolt across its front in its three
/// golds, an ink outline and the icon's gold aura, leaning to the right like the icon. It stands in the sand right of the Repelente Infinito.
/// Always: golden sparkles, little sparks jumping off its rim, now and then the icon's speed lines streaking off to the left, a light down the bolt.
/// </summary>
/// <remarks>
/// On: a discharge, the bolt strobing white, branching lightning out of the egg, a ring of sparks; then it stays overcharged: the bolt throbbing
/// white-hot, arcs crackling over the shell and down to the sand, sparks spitting from the bolt's ends, the aura flickering, a jolt now and then.
/// Off: it sputters out. A function of the data and the moment: no clock and no random of its own.
/// </remarks>
public sealed class CapEggTurbo
{
    private const double Roll = -0.22, Pitch = 0.15, Rx = 8, Ry = 11;

    private static readonly uint[] Cream = [0xA89462, 0xD6C692, 0xF6ECC4, 0xFFFCE8, 0xFFFFFF];
    private static readonly uint[] Green = [0x3E7A48, 0x50965A, 0x78BE6E, 0x9CD88E, 0xC4F0B0];
    private const uint Ink = 0x1C1624, Aura = 0xFFE250, WhiteC = 0xFFFFFF, Spark = 0xFFE650, SparkDeep = 0xFFB000, Pale = 0xFFF8C8;
    private static readonly uint[] BoltGold = [0xBE6E00, 0xFFCD14, 0xFFF596];
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    // The icon's bolt (EggTurboIcon), its three layers, in the icon's pixels: the egg's middle at (18, 16).
    private static readonly (double X, double Y)[][] Bolt =
    [
        [(20.5, 3), (12, 17), (17, 17), (13.5, 29), (25, 12.5), (19.5, 12.5), (23.5, 3)],
        [(21.5, 4), (13.5, 17.5), (18.5, 17.5), (14.5, 28), (24, 13.5), (18.5, 13.5), (22.5, 4)],
        [(21, 5), (15, 16), (19.5, 16), (16.5, 24), (22.5, 14.5), (17, 14.5), (21.5, 5)]
    ];

    private static readonly (double X, double Y, double Z, double R)[] Spots =
    [
        (-0.55, 0.35, 0.76, 0.2), (0.55, -0.15, 0.82, 0.24), (-0.35, -0.45, 0.82, 0.17), (0.15, 0.72, 0.68, 0.15),
        (-0.8, -0.05, 0.6, 0.17), (0.72, 0.45, 0.52, 0.17), (0.32, -0.66, 0.68, 0.19)
    ];

    private const double BurstLength = 0.9, SputterLength = 0.7;

    /// <summary>A cell of the model: <c>Layer</c> -1 the shell, 0-2 the bolt's layers; <c>Height</c> its height up the egg, for the light down the bolt.</summary>
    private readonly record struct Cell(int X, int Y, int Layer, uint Shell, int Tone, double Height);

    private static readonly Lazy<(Cell[] Cells, (int X, int Y)[] Edge, (int X, int Y)[] Halo, int Bottom)> Model = new(Render);

    private uint[] _sand = [];
    private readonly int[] _tops = new int[25];

    /// <summary>Paints the egg (and its sparks) into the party's window, its middle at column <paramref name="cx"/>, its foot on <paramref name="line"/>.</summary>
    public void Paint(CapSheet p, int cx, int line, CapToggle turbo, double t)
    {
        var (cells, edge, halo, bottom) = Model.Value;
        var on = t >= turbo.OnAt && t < turbo.OffAt;
        var sinceOn = t - turbo.OnAt;
        var sinceOff = t - turbo.OffAt;
        var burst = sinceOn is >= 0 and < BurstLength;
        var sputter = sinceOff is >= 0 and < SputterLength;
        var frame = (int)Math.Floor(t * 16);

        var cy = line + 2 - bottom;
        // A jolt: in the discharge, and every so often while overcharged.
        var jolt = burst ? (frame % 2 == 0 ? 1 : -1) : on && (t * 0.75 % 1) < 0.12 ? ((int)(t * 30) % 2 == 0 ? 1 : -1) : 0;
        var ex = cx + jolt;

        // The sand in front of its foot, kept to be drawn back over it.
        const int Left = -12, Right = 12, Depth = 8;
        if (_sand.Length < (Right - Left + 1) * Depth) _sand = new uint[(Right - Left + 1) * Depth];
        for (var dx = Left; dx <= Right; dx++)
        {
            var top = line - 1 + (int)Math.Round(Math.Sin((cx + dx) * 0.45));
            _tops[dx - Left] = top;
            for (var k = 0; k < Depth; k++) _sand[((dx - Left) * Depth) + k] = top >= 0 && p.Solid(cx + dx, top + 3 + k) ? p.At(cx + dx, top + 3 + k) | 0xFF000000u : 0;
        }

        // The speed lines of the icon, streaking off to the left now and then (more often overcharged).
        var lane = (t / (on ? 1.3 : 3.1)) % 1;
        if (lane < 0.3)
        {
            for (var n = 0; n < 3; n++)
            {
                var k = (lane / 0.3) - (n * 0.12);
                if (k is < 0 or > 1) continue;
                var y = cy - 5 + (n * 5);
                var head = ex - 11 - (int)(k * 10);
                var length = n == 1 ? 7 : 5;
                for (var i = 0; i < length; i++)
                {
                    var x = head - i;
                    if (Bayer[y & 3, x & 3] < (1 - k) * 18) p.Set(x, y, i == 0 ? Spark : WhiteC);
                }
            }
        }

        // The aura: gold, flickering gold and white while overcharged.
        foreach (var (x, y) in halo)
        {
            var flick = on && Hash((x * 7) + frame, y) % 5 == 0;
            p.Set(ex + x, cy + y, flick ? (Hash(x, y + frame) % 2 == 0 ? WhiteC : Pale) : Aura);
        }

        foreach (var (x, y) in edge) p.Set(ex + x, cy + y, Ink);

        // The bolt: idle, a light runs down it; in the discharge, strobing; overcharged, throbbing white-hot; going out, sputtering.
        var charge = burst ? (frame % 2 == 0 ? 1.0 : 0.4)
            : on ? 0.55 + (0.45 * (0.5 + (0.5 * Math.Sin(t * 13))) * (Hash(frame, 3) % 4 == 0 ? 0.3 : 1))
            : sputter ? (Hash(frame, 9) % 3 == 0 ? 0.6 * (1 - (sinceOff / SputterLength)) : 0)
            : 0;
        var sweep = 12 - ((t / 3.4 % 1) * 30);
        foreach (var c in cells)
        {
            if (c.Layer < 0)
            {
                p.Set(ex + c.X, cy + c.Y, burst && frame % 2 == 0 ? CapSheet.Mix(c.Shell, Pale, 0.35) : c.Shell);
                continue;
            }

            var colour = BoltGold[c.Layer];
            if (charge > 0)
            {
                colour = c.Layer == 0 ? (charge > 0.8 ? BoltGold[1] : BoltGold[0])
                    : c.Layer == 1 ? (charge > 0.7 ? BoltGold[2] : BoltGold[1])
                    : charge > 0.5 ? WhiteC : BoltGold[2];
                if (charge > 0.85 && c.Layer > 0) colour = WhiteC;
            }
            else if (Math.Abs(c.Height - sweep) < 1.2 && c.Layer > 0) colour = WhiteC;

            p.Set(ex + c.X, cy + c.Y, colour);
        }

        for (var dx = Left; dx <= Right; dx++)
        {
            var top = _tops[dx - Left];
            if (top < 0) continue;
            for (var k = 0; k < Depth; k++)
            {
                var kept = _sand[((dx - Left) * Depth) + k];
                if (kept != 0) p.Set(cx + dx, top + 3 + k, kept & 0xFFFFFF);
            }
        }

        // The electricity (2026-10-16, second try: the first sparks were straight sticks). Each arc is a fractal crack, re-drawn every frame so it
        // crackles: a white-hot core, a yellow skin, a dithered amber haze, forks off it, a flash where it touches.
        var f20 = (int)Math.Floor(t * 20);

        // Always: now and then a short arc crawls along the shell's rim.
        var idleBeat = (int)Math.Floor(t * 1.7);
        if (!on && Hash(idleBeat, 5) % 2 == 0 && (t * 1.7 % 1) < 0.16)
            Crawl(p, ex, cy, Hash(idleBeat, 6) % RimSteps, 3 + (Hash(idleBeat, 7) % 3), f20, 0.7);

        if (on && !burst)
        {
            // Overcharged: two arcs always crawling round the shell, each moving on every quarter second.
            for (var n = 0; n < 2; n++)
            {
                var hop = (int)Math.Floor((t * 4) + (n * 0.5));
                Crawl(p, ex, cy, (Hash(hop, n + 20) % RimSteps) + (n * RimSteps / 2), 4 + (Hash(hop, n + 21) % 5), f20 + (n * 97), 1);
            }

            // Now and then one jumps across the egg's front.
            var cross = (int)Math.Floor(t * 2.6);
            if (Hash(cross, 30) % 3 != 0 && (t * 2.6 % 1) < 0.45)
            {
                var a = Rim[Hash(cross, 31) % RimSteps];
                var b = Rim[(Hash(cross, 31) + (RimSteps / 2) + (Hash(cross, 32) % 7) - 3) % RimSteps];
                Arc(p, ex + a.X, cy + a.Y, ex + b.X, cy + b.Y, f20 * 13, 1, 1);
            }

            // And down into the sand, forking.
            var ground = (int)Math.Floor(t * 1.9);
            if (Hash(ground, 40) % 2 == 0 && (t * 1.9 % 1) < 0.3)
            {
                var side = Hash(ground, 41) % 2 == 0 ? -1 : 1;
                var from = Rim[(RimSteps * (side < 0 ? 5 : 3) / 8) % RimSteps];
                Arc(p, ex + from.X, cy + from.Y, ex + (side * (12 + (Hash(ground, 42) % 6))), line + 1, f20 * 7 + 3, 1, 2);
            }

            // Sparks spitting from the bolt's two ends: little streaks that fall and bounce once on the sand.
            for (var i = 0; i < 10; i++)
            {
                const double Cycle = 0.7;
                var born = (Math.Floor((t - (i * 0.07)) / Cycle) * Cycle) + (i * 0.07);
                var age = t - born;
                if (age is < 0 or > 0.65) continue;
                var top = i % 2 == 0;
                var seed = Hash((int)(born * 100), i);
                var vx = ((seed % 100 / 100.0) - (top ? 0.2 : 0.5)) * 34;
                var vy = (top ? -22 : -10) - (seed / 100 % 12);
                var x0 = ex + (top ? 4 : -3);
                var y0 = cy + (top ? -13 : 12);
                (double X, double Y) At(double a)
                {
                    var y = y0 + (vy * a) + (70 * a * a);
                    return (x0 + (vx * a), Math.Min(y, line + 1 - (y > line + 1 ? (y - (line + 1)) * 0.4 : 0)));
                }

                var (hx, hy) = At(age);
                var (tx, ty) = At(Math.Max(0, age - 0.05));
                LineCells(p, tx, ty, hx, hy, age < 0.3 ? Spark : SparkDeep);
                p.Set((int)Math.Round(hx), (int)Math.Round(hy), age < 0.35 ? WhiteC : Spark);
            }
        }

        // The discharge: five bolts forking out of the egg, re-struck every frame, and a ring of sparks.
        if (burst)
        {
            var k = sinceOn / BurstLength;
            for (var n = 0; n < 5; n++)
            {
                if (Hash(f20, n + 90) % 5 == 0) continue;
                var ang = -Math.PI * (0.02 + (n * 0.24)) + ((Hash(f20, n) % 100 / 100.0 - 0.5) * 0.35);
                var len = 16 + (k * 16) + (Hash(n, f20) % 6);
                var from = Rim[(int)Math.Round(((ang + (Math.PI * 2)) % (Math.PI * 2)) / (Math.PI * 2) * RimSteps) % RimSteps];
                Arc(p, ex + from.X, cy + from.Y, ex + (int)Math.Round(Math.Cos(ang) * len), cy + (int)Math.Round(Math.Sin(ang) * len * 0.85), (f20 * 7) + n, 1.2 - (k * 0.5), 3);
            }

            for (var a = 0; a < 20; a++)
            {
                var ang = a / 20.0 * Math.PI * 2;
                var r = 8 + (k * 24);
                int x = (int)Math.Round(ex + (Math.Cos(ang) * r)), y = (int)Math.Round(cy + (Math.Sin(ang) * r * 0.7));
                if (Bayer[y & 3, x & 3] < (1 - k) * 16)
                {
                    p.Set(x, y, a % 2 == 0 ? WhiteC : Spark);
                    if (a % 2 == 0 && k < 0.5) p.Set(x - (int)Math.Round(Math.Cos(ang)), y - (int)Math.Round(Math.Sin(ang)), SparkDeep);
                }
            }
        }

        // Going out: a last few sparks falling.
        if (sputter)
        {
            var k = sinceOff / SputterLength;
            for (var i = 0; i < 6; i++)
            {
                int x = ex - 6 + (i * 2) + (int)Math.Round((Hash(i, 5) % 7) - 3 * k), y = cy - 8 + (int)Math.Round(k * k * 26) + (Hash(i, 6) % 6);
                if (Bayer[y & 3, x & 3] < (1 - k) * 16) p.Set(x, y, i % 2 == 0 ? Spark : WhiteC);
            }
        }

        // The icon's golden sparkles round it, each on its own beat; quicker overcharged.
        for (var n = 0; n < Glints.Length; n++)
        {
            var life = ((t / (on ? 1.4 : 2.7)) + (n * 0.41)) % 1;
            var size = life < 0.1 ? 1 : life < 0.28 ? 2 : life < 0.38 ? 1 : life < 0.46 ? 0 : -1;
            if (size >= 0) Twinkle(p, ex + Glints[n].X, cy + Glints[n].Y, size);
        }
    }

    private static readonly (int X, int Y)[] Glints = [(-13, -11), (12, 9), (13, -9)];

    // ---------------------------------------------------------------- the electricity

    private const int RimSteps = 40;

    /// <summary>The shell's outline at 40 angles round its middle, starting to the right and going clockwise on screen.</summary>
    private static (int X, int Y)[] Rim => _rim.Value;

    private static readonly Lazy<(int X, int Y)[]> _rim = new(() =>
    {
        var edge = Model.Value.Edge;
        return [.. Enumerable.Range(0, RimSteps).Select(i =>
        {
            var a = i / (double)RimSteps * Math.PI * 2;
            return edge.OrderByDescending(e => (e.X * Math.Cos(a)) + (e.Y * Math.Sin(a)) - (0.8 * Math.Abs((e.X * Math.Sin(a)) - (e.Y * Math.Cos(a))))).First();
        })];
    });

    /// <summary>An arc crawling along the shell's rim from step <paramref name="from"/> for <paramref name="steps"/> steps, a cell or two off it.</summary>
    private static void Crawl(CapSheet p, int ex, int cy, int from, int steps, int seed, double energy)
    {
        var points = new List<(double X, double Y)>();
        for (var s = 0; s <= steps; s++)
        {
            var (x, y) = Rim[(from + s) % RimSteps];
            var a = (from + s) % RimSteps / (double)RimSteps * Math.PI * 2;
            var out_ = 2.5 + (Hash(seed, s) % 3 * 0.9);
            points.Add((ex + x + (Math.Cos(a) * out_), cy + y + (Math.Sin(a) * out_)));
        }

        var path = new List<(double X, double Y)> { points[0] };
        for (var i = 1; i < points.Count; i++) Crack(path, points[i - 1], points[i], seed + (i * 31), 0.45);
        Strike(p, path, energy);
        Flash(p, points[0], energy * 0.7);
        Flash(p, points[^1], energy * 0.7);
    }

    /// <summary>A bolt from one cell to another with <paramref name="forks"/> forks off it, its kinks set by <paramref name="seed"/>.</summary>
    private static void Arc(CapSheet p, int x1, int y1, int x2, int y2, int seed, double energy, int forks)
    {
        var path = new List<(double X, double Y)> { (x1, y1) };
        Crack(path, (x1, y1), (x2, y2), seed, 0.42);
        for (var f = 0; f < forks; f++)
        {
            var at = path[(int)(path.Count * (0.25 + (Hash(seed, f + 70) % 50 / 100.0)))];
            var dx = x2 - at.X;
            var dy = y2 - at.Y;
            var turn = (Hash(seed, f + 80) % 2 == 0 ? 1 : -1) * (0.5 + (Hash(seed, f + 81) % 50 / 100.0));
            var length = 0.35 + (Hash(seed, f + 82) % 30 / 100.0);
            var end = (at.X + (((dx * Math.Cos(turn)) - (dy * Math.Sin(turn))) * length), at.Y + (((dx * Math.Sin(turn)) + (dy * Math.Cos(turn))) * length));
            var fork = new List<(double X, double Y)> { at };
            Crack(fork, at, end, seed + (f * 977), 0.4);
            Strike(p, fork, energy * 0.55);
        }

        Strike(p, path, energy);
        Flash(p, (x2, y2), energy);
    }

    /// <summary>Midpoint displacement: the segment broken in two at a point pushed sideways, again and again down to two cells.</summary>
    private static void Crack(List<(double X, double Y)> path, (double X, double Y) a, (double X, double Y) b, int seed, double rough)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 2.2)
        {
            path.Add(b);
            return;
        }

        var push = ((Hash(seed, (int)(a.X * 13) + (int)(b.Y * 7)) % 1000 / 1000.0) - 0.5) * length * rough;
        var mid = (((a.X + b.X) / 2) - (dy / length * push), ((a.Y + b.Y) / 2) + (dx / length * push));
        Crack(path, a, mid, (seed * 3) + 1, rough);
        Crack(path, mid, b, (seed * 3) + 2, rough);
    }

    /// <summary>Lays a path down: its core white (pale gold when weak), a yellow skin round it, an amber haze dithered beyond when strong.</summary>
    private static void Strike(CapSheet p, List<(double X, double Y)> path, double energy)
    {
        var core = new HashSet<(int, int)>();
        for (var i = 1; i < path.Count; i++)
        {
            var n = (int)Math.Max(1, Math.Ceiling(Math.Max(Math.Abs(path[i].X - path[i - 1].X), Math.Abs(path[i].Y - path[i - 1].Y))));
            for (var s = 0; s <= n; s++)
                core.Add(((int)Math.Round(path[i - 1].X + ((path[i].X - path[i - 1].X) * s / n)), (int)Math.Round(path[i - 1].Y + ((path[i].Y - path[i - 1].Y) * s / n))));
        }

        if (energy > 0.75)
        {
            foreach (var (x, y) in core)
            {
                foreach (var (dx, dy) in Ring2)
                {
                    if (!core.Contains((x + dx, y + dy)) && Bayer[(y + dy) & 3, (x + dx) & 3] < 6) p.Set(x + dx, y + dy, SparkDeep, 150);
                }
            }
        }

        if (energy > 0.4)
        {
            foreach (var (x, y) in core)
            {
                foreach (var (dx, dy) in Four)
                {
                    if (!core.Contains((x + dx, y + dy))) p.Set(x + dx, y + dy, Spark, energy > 0.8 ? 235 : 170);
                }
            }
        }

        foreach (var (x, y) in core) p.Set(x, y, energy > 0.6 ? WhiteC : Pale);
    }

    /// <summary>Where an arc touches: a little star of light.</summary>
    private static void Flash(CapSheet p, (double X, double Y) at, double energy)
    {
        int x = (int)Math.Round(at.X), y = (int)Math.Round(at.Y);
        var size = energy > 0.9 ? 2 : 1;
        p.Set(x, y, WhiteC);
        for (var k = 1; k <= size; k++)
        {
            var c = k == 1 ? Pale : Spark;
            p.Set(x + k, y, c);
            p.Set(x - k, y, c);
            p.Set(x, y + k, c);
            p.Set(x, y - k, c);
        }

        if (energy > 0.9)
        {
            foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) }) p.Set(x + dx, y + dy, SparkDeep, 180);
        }
    }

    private static readonly (int, int)[] Ring2 = [(2, 0), (-2, 0), (0, 2), (0, -2), (1, 1), (-1, 1), (1, -1), (-1, -1)];

    private static void LineCells(CapSheet p, double x1, double y1, double x2, double y2, uint colour)
    {
        var n = (int)Math.Max(1, Math.Ceiling(Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1))));
        for (var i = 0; i <= n; i++) p.Set((int)Math.Round(x1 + ((x2 - x1) * i / n)), (int)Math.Round(y1 + ((y2 - y1) * i / n)), colour);
    }

    private static void Twinkle(CapSheet p, int x, int y, int size)
    {
        p.Set(x, y, WhiteC);
        for (var k = 1; k <= size; k++)
        {
            var c = k == size && size == 2 ? Aura : Pale;
            p.Set(x + k, y, c);
            p.Set(x - k, y, c);
            p.Set(x, y + k, c);
            p.Set(x, y - k, c);
        }
    }

    // ---------------------------------------------------------------- the model

    private static (Cell[] Cells, (int X, int Y)[] Edge, (int X, int Y)[] Halo, int Bottom) Render()
    {
        var light = Norm((-0.55, 0.75, 0.6));
        var cells = new List<Cell>();
        var lit = new HashSet<(int, int)>();
        var spots = Spots.Select(s => (Dir: Norm((s.X, s.Y, s.Z)), Cos: Math.Cos(s.R), Rim: Math.Cos(s.R * 0.7))).ToArray();
        for (var sy = -16; sy <= 16; sy++)
        {
            for (var sx = -14; sx <= 14; sx++)
            {
                double z = 30;
                var hit = false;
                for (var i = 0; i < 160 && z > -30; i++)
                {
                    var d = Egg((sx, sy, z));
                    if (d < 0.02)
                    {
                        hit = true;
                        break;
                    }

                    z -= Math.Max(0.04, d * 0.8);
                }

                // The bolt reaches past the shell at its two ends, as in the icon: there it is flat, in front.
                var flat = Unroll((sx, sy));
                var layer = -1;
                if (hit || flat.Y is > 9 or < -10)
                {
                    var (bx, by) = hit ? (Unturn((sx, sy, z)).X, Unturn((sx, sy, z)).Y) : flat;
                    if (!hit || Unturn((sx, sy, z)).Z > 0)
                    {
                        for (var l = 2; l >= 0 && layer < 0; l--)
                        {
                            if (Inside(Bolt[l], bx + 18, 16 - by)) layer = l;
                        }
                    }
                }

                if (!hit && layer < 0) continue;

                uint shell = 0;
                var tone = 2;
                if (hit)
                {
                    const double E = 0.05;
                    var n = Norm((Egg((sx + E, sy, z)) - Egg((sx - E, sy, z)), Egg((sx, sy + E, z)) - Egg((sx, sy - E, z)), Egg((sx, sy, z + E)) - Egg((sx, sy, z - E))));
                    var lamb = Dot(n, light);
                    var reflect = Norm(((2 * lamb * n.X) - light.X, (2 * lamb * n.Y) - light.Y, (2 * lamb * n.Z) - light.Z));
                    tone = lamb > 0.75 ? 4 : lamb > 0.42 ? 3 : lamb > 0.08 ? 2 : lamb > -0.3 ? 1 : 0;
                    var dir = Norm(Unturn((sx, sy, z)));
                    var ramp = Cream;
                    foreach (var s in spots)
                    {
                        var c = Dot(dir, s.Dir);
                        if (c > s.Cos)
                        {
                            ramp = Green;
                            if (c < s.Rim && Dot(dir, (0, 1, 0)) < Dot(s.Dir, (0, 1, 0))) tone = Math.Max(0, tone - 1);
                        }
                    }

                    shell = reflect.Z > 0.95 && ramp == Cream ? WhiteC : ramp[Math.Min(tone, 3)];
                }

                var height = hit ? Unturn((sx, sy, z)).Y : flat.Y;
                cells.Add(new Cell(sx, -sy, layer, shell, tone, height));
                lit.Add((sx, -sy));
            }
        }

        var taken = new HashSet<(int, int)>(lit);
        var edge = new List<(int, int)>();
        foreach (var c in cells)
        {
            foreach (var (dx, dy) in Four)
            {
                if (taken.Add((c.X + dx, c.Y + dy))) edge.Add((c.X + dx, c.Y + dy));
            }
        }

        var halo = new List<(int, int)>();
        foreach (var (x, y) in edge)
        {
            foreach (var (dx, dy) in Eight)
            {
                if (taken.Add((x + dx, y + dy))) halo.Add((x + dx, y + dy));
            }
        }

        // the outline drawn first, the cells over it, so a bolt's end past the shell keeps its ink
        return ([.. cells], [.. edge], [.. halo], cells.Max(c => c.Y));
    }

    private static readonly (int, int)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int, int)[] Eight = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)];

    /// <summary>The egg: an ellipsoid wider below than above, as the icon's.</summary>
    private static double Egg((double X, double Y, double Z) p)
    {
        var q = Unturn(p);
        var f = 1 - (0.14 * Math.Clamp(q.Y / Ry, -1, 1));
        var k = Math.Sqrt(((q.X / (Rx * f)) * (q.X / (Rx * f))) + ((q.Y / Ry) * (q.Y / Ry)) + ((q.Z / (Rx * f)) * (q.Z / (Rx * f))));
        return (k - 1) * Rx * 0.85;
    }

    private static bool Inside((double X, double Y)[] poly, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > y) != (poly[j].Y > y) && x < ((poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y)) + poly[i].X) inside = !inside;
        }

        return inside;
    }

    private static (double X, double Y) Unroll((double X, double Y) v) => ((v.X * Math.Cos(Roll)) - (v.Y * Math.Sin(Roll)), (v.X * Math.Sin(Roll)) + (v.Y * Math.Cos(Roll)));

    private static (double X, double Y, double Z) Unturn((double X, double Y, double Z) v)
    {
        var (x, y) = Unroll((v.X, v.Y));
        return (x, (y * Math.Cos(Pitch)) + (v.Z * Math.Sin(Pitch)), (-y * Math.Sin(Pitch)) + (v.Z * Math.Cos(Pitch)));
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static (double X, double Y, double Z) Norm((double X, double Y, double Z) a)
    {
        var l = Math.Sqrt(Dot(a, a));
        return (a.X / l, a.Y / l, a.Z / l);
    }

    private static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }
}
