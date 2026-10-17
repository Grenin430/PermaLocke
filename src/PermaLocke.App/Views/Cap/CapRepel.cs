namespace PermaLocke.App.Views;

/// <summary>
/// The Repelente Infinito in the party's window (2026-10-16, prototype): the can of the bag icon with volume, rendered once from a small 3D model
/// (a rounded can, a ridged grey cap, a nozzle with its spray head pointing right) into cells of the overlay, tilted like the Pokévial and leaning
/// to the viewer so the cap's top shows. The can is the icon's night sky, blue on the left to magenta on the right, its stars drifting slowly round
/// it and twinkling; the golden ∞ of the icon on its front, a light sweeping along it now and then; the icon's blue aura round it all. It stands in
/// the sand right of the Pokévial, its foot sunk in it.
/// </summary>
/// <remarks>
/// On: the cap is pressed, the can shakes, a ring of light, and thin violet wisps with stardust come out of the nozzle and run along the sand to
/// the right, curling, see-through. The ∞ shines while it is on. Off: the cap springs
/// back, a last puff, and what is out drifts on and fades. A function of the data and the moment: no clock and no random of its own.
/// </remarks>
public sealed class CapRepel
{
    private const double Roll = -0.12, Pitch = 0.2;
    private const double BodyR = 7.0, BodyY = -1.0, BodyH = 8.0, CapR = 5.4, CapY = 8.8, CapH = 1.8;

    private static readonly uint[] Sky = [0x29107B, 0x4A188B, 0x7329A4, 0x9C31B4, 0xC539C5];
    private static readonly uint[] Grey = [0x5A6280, 0xA4ACCD, 0xC5CDE6, 0xD5DEF6, 0xE6EEFF];
    private static readonly uint[] Nozzle = [0x202028, 0x3A3A48, 0x55556A, 0x7A7A90, 0xA0A0B8];
    private static readonly uint[] Smoke = [0x5E46A0, 0x8466C8, 0xAE92E6, 0xD2BEF6, 0xF0E4FF];
    private const uint Ink = 0x202020, Aura = 0x7BA4FF, WhiteC = 0xFFFFFF, StarBlue = 0xCDDEFF;
    private const uint Gold = 0xFFF68B, GoldHi = 0xFFFFE0, GoldDim = 0xC8B860;
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
    private static readonly string[] Infinity = [".##..##.", "#..##..#", "#..##..#", ".##..##."];

    /// <summary>How long the turning on and off play, from the moment.</summary>
    private const double PressLength = 0.6, ReleaseLength = 0.45;

    private enum Part { Body, Cap, Nozzle }

    private readonly record struct Cell(int X, int Y, Part Part, int Tone, bool Shine, double Theta, double H, bool Top);

    private static readonly Lazy<(Cell[] Cells, (int X, int Y)[] Edge, (int X, int Y)[] Halo, int Bottom, (int X, int Y) Tip)> Model = new(Render);

    private uint[] _sand = [];
    private readonly int[] _tops = new int[25];

    /// <summary>Paints the can (and its smoke) into the party's window, its middle at column <paramref name="cx"/>, its foot on <paramref name="line"/>.</summary>
    /// <param name="ground">The sand's top row at a column: the smoke rolls along it.</param>
    public void Paint(CapSheet p, int cx, int line, CapToggle repel, Func<int, int> ground, double t)
    {
        var (cells, edge, halo, bottom, tip) = Model.Value;
        var on = t >= repel.OnAt && t < repel.OffAt;
        var sinceOn = t - repel.OnAt;
        var sinceOff = t - repel.OffAt;
        var pressing = sinceOn is >= 0 and < PressLength;
        var releasing = sinceOff is >= 0 and < ReleaseLength;

        var cy = line + 2 - bottom;
        var shake = pressing && sinceOn > 0.08 && sinceOn < 0.4 ? ((int)(sinceOn * 30) % 2 == 0 ? -1 : 1) : 0;
        // The cap: pressed one cell down while it sprays, springing back with a hop when let go.
        var press = pressing ? (sinceOn < 0.08 ? 0 : 1) : on ? 1 : releasing ? (sinceOff < 0.15 ? -1 : 0) : 0;

        // The sand in front of its foot, kept to be drawn back over it.
        const int Left = -12, Right = 12, Depth = 8;
        if (_sand.Length < (Right - Left + 1) * Depth) _sand = new uint[(Right - Left + 1) * Depth];
        for (var dx = Left; dx <= Right; dx++)
        {
            var top = line - 1 + (int)Math.Round(Math.Sin((cx + dx) * 0.45));
            _tops[dx - Left] = top;
            for (var k = 0; k < Depth; k++) _sand[((dx - Left) * Depth) + k] = top >= 0 && p.Solid(cx + dx, top + 3 + k) ? p.At(cx + dx, top + 3 + k) | 0xFF000000u : 0;
        }

        int Lift(Part part) => part == Part.Body ? 0 : press;
        foreach (var (x, y) in halo) p.Set(cx + x + shake, cy + y, Aura);
        foreach (var (x, y) in edge) p.Set(cx + x + shake, cy + y, Ink);
        foreach (var c in cells.Where(c => c.Part == Part.Body)) p.Set(cx + c.X + shake, cy + c.Y, Colour(c, on, sinceOn, t));
        foreach (var c in cells.Where(c => c.Part != Part.Body))
        {
            // Pressed, the cap's outline goes down with it.
            if (Lift(c.Part) != 0 && c.Top) p.Set(cx + c.X + shake, cy + c.Y, Ink);
            p.Set(cx + c.X + shake, cy + c.Y + Lift(c.Part), Colour(c, on, sinceOn, t));
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

        Wisps(p, repel, cx + tip.X + shake, cy + tip.Y + press, ground, p.W - 2, t);

        // The icon's sparkles round it: long four-pointed stars of pale blue, each on its own beat; brighter and quicker while it is on.
        for (var n = 0; n < Glints.Length; n++)
        {
            var life = ((t / (on ? 1.7 : 2.9)) + (n * 0.37)) % 1;
            var size = life < 0.1 ? 1 : life < 0.28 ? 2 : life < 0.38 ? 1 : life < 0.46 ? 0 : -1;
            if (size >= 0) Twinkle(p, cx + Glints[n].X + shake, cy + Glints[n].Y, size, StarBlue);
        }

        // Turned on: a ring of light out of the nozzle and the stars of the can flash once.
        if (pressing && sinceOn > 0.08)
        {
            var k = (sinceOn - 0.08) / (PressLength - 0.08);
            for (var a = 0; a < 10; a++)
            {
                var ang = a / 10.0 * Math.PI * 2;
                var r = 2 + (k * 9);
                int x = (int)Math.Round(cx + tip.X + (Math.Cos(ang) * r)), y = (int)Math.Round(cy + tip.Y + press + (Math.Sin(ang) * r * 0.7));
                if (Bayer[y & 3, x & 3] < (1 - k) * 16) p.Set(x, y, a % 2 == 0 ? WhiteC : StarBlue);
            }
        }
    }

    private static readonly (int X, int Y)[] Glints = [(-11, -12), (11, -4), (-12, 5)];

    // ---------------------------------------------------------------- the smoke

    /// <summary>
    /// The smoke (2026-10-16; 2026-10-17, third try: the thin see-through wisps were hard to see, and rising it ran into the party's text): along the
    /// sand to the right again, but a body of mist nearly opaque, thicker and higher, its wisps cut deeper, lit on top and shaded underneath so it
    /// has volume. Out of the nozzle a thread drops to the sand. While it is on, its front runs out to the right; turned off, its tail leaves the
    /// nozzle and runs after it.
    /// </summary>
    private static void Wisps(CapSheet p, CapToggle repel, int nx, int ny, Func<int, int> ground, int right, double t)
    {
        if (double.IsInfinity(repel.OnAt) || t < repel.OnAt) return;
        var front = (Speed * (t - repel.OnAt)) + 6;
        var tail = t < repel.OffAt ? double.NegativeInfinity : Speed * (t - repel.OffAt) * 1.15;
        if (tail > right - nx + 20) return;
        var burst = Math.Exp(-(t - repel.OnAt) * 1.4);
        var tipAbove = ground(nx) - ny;

        for (var x = nx + 1; x < right; x++)
        {
            var u = x - nx;
            var reach = Math.Clamp((front - u) / 12, 0, 1) * Math.Clamp((u - tail) / 14, 0, 1) * Math.Clamp((right - x) / 10.0, 0, 1);
            if (reach <= 0) continue;
            var g = ground(x);
            var open = Smooth(Math.Min(1, u / 60.0));
            var centre = (tipAbove * (1 - Smooth(Math.Min(1, u / 14.0)))) + (4 + (open * 4));
            var half = 1.8 + (open * 7.5);
            var thin = 1 - (0.35 * Smooth(Math.Min(1, u / (double)Math.Max(1, right - nx))));
            var lift = 1 + (burst * 0.5 * (1 - open)) + (1.2 * (1 - Smooth(Math.Min(1, u / 24.0))));
            for (var y = g - (int)Math.Ceiling(centre + half + 3); y <= g + 1; y++)
            {
                var d = Density(x, y, g, centre, half, t) * reach * thin * lift;
                if (d < 0.26) continue;
                if (d < 0.36 && Bayer[y & 3, x & 3] >= 8) continue;
                var top = Density(x, y - 1, g, centre, half, t) * reach * thin * lift < 0.26;
                var under = Density(x, y + 1, g, centre, half, t) * reach * thin * lift < 0.26;
                var (tone, alpha) = top ? (4, 235) : under ? (0, 215) : d > 0.6 ? (3, 225) : d > 0.4 ? (2, 205) : (1, 180);
                p.Set(x, y, Smoke[tone], alpha);
            }
        }

        // stardust carried along, twinkling
        for (var i = 0; i < 12; i++)
        {
            var u = ((t * Speed) + (i * 23) + (Rnd(i, 1) * 20)) % Math.Max(1, right - nx);
            if (u > front - 4 || u < tail + 4) continue;
            var x = nx + (int)u;
            var y = ground(x) - 4 - (int)(Rnd(i, 2) * 9 * Smooth(Math.Min(1, u / 60.0)));
            var beat = ((t * 1.9) + (Rnd(i, 3) * 7)) % 1;
            var size = beat < 0.1 ? 1 : beat < 0.2 ? 2 : beat < 0.3 ? 1 : beat < 0.45 ? 0 : -1;
            if (size >= 0) Twinkle(p, x, y, size, Rnd(i, 4) < 0.5 ? WhiteC : StarBlue);
        }
    }

    /// <summary>How fast the smoke runs along the sand, cells a second.</summary>
    private const double Speed = 22;

    /// <summary>
    /// The smoke's thickness at a cell (2026-10-17, fourth try: cutting the wisps through the whole body left gaps that ran along with it): a body
    /// that is always there, whose outline the turbulence swells and thins, top and bottom on their own; inside it, the wisps only lighten.
    /// </summary>
    private static double Density(int x, int y, int g, double centre, double half, double t)
    {
        var ax = x - (Speed * 0.85 * t);
        var above = g - y - centre;
        var swell = above > 0 ? Fbm(ax * 0.05, 3 + (t * 0.3)) : Fbm(ax * 0.05, 11 + (t * 0.3));
        var off = above / (half * (0.7 + (0.6 * swell)));
        var body = Math.Exp(-off * off * 1.6);
        if (body < 0.05) return 0;
        var strand = Math.Sin((ax * 0.11) + (Fbm(ax * 0.06, (y * 0.14) + (t * 0.25)) * 5) + (y * 0.45));
        return body * (0.85 + (0.25 * Math.Clamp(strand, 0, 1)));
    }

    /// <summary>Smooth value noise, two octaves, 0 to about 1.</summary>
    private static double Fbm(double x, double y) => (0.65 * Noise(x, y)) + (0.35 * Noise((x * 2.1) + 17, (y * 2.1) + 5));

    private static double Noise(double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = Smooth(x - ix), fy = Smooth(y - iy);
        double a = Rnd(ix, iy), b = Rnd(ix + 1, iy), c = Rnd(ix, iy + 1), d = Rnd(ix + 1, iy + 1);
        return (a + ((b - a) * fx)) + (((c + ((d - c) * fx)) - (a + ((b - a) * fx))) * fy);
    }

    // ---------------------------------------------------------------- the colours

    private static uint Colour(Cell c, bool on, double sinceOn, double t)
    {
        if (c.Part == Part.Nozzle) return c.Shine ? Nozzle[4] : Nozzle[c.Tone];
        if (c.Part == Part.Cap)
        {
            // ridges round the cap; its top lighter
            var ridge = !c.Top && (int)Math.Floor((c.Theta + Math.PI) * 14 / (2 * Math.PI)) % 2 == 1;
            return c.Shine ? WhiteC : Grey[Math.Clamp(c.Tone + (c.Top ? 1 : 0) - (ridge ? 1 : 0), 0, 4)];
        }

        if (c.Shine) return WhiteC;
        var arc = c.Theta * BodyR;

        // the ∞ on its front, and a light running along it
        if (Math.Abs(arc) < 4.5 && c.H is > -3.6 and <= 0.4)
        {
            var col = (int)Math.Floor(arc + 4);
            var row = (int)Math.Floor(0.4 - c.H);
            if (col is >= 0 and < 8 && row is >= 0 and < 4 && Infinity[row][col] == '#')
            {
                var sweep = ((t / (on ? 1.6 : 4.2)) % 1) * 14 - 3;
                return Math.Abs(col - sweep) < 0.8 ? GoldHi : on ? Gold : GoldDim;
            }
        }

        // the night sky, blue on the left to magenta on the right and below, its bands dithered
        var u = 0.5 + (0.38 * Math.Sin(c.Theta)) - (0.2 * (c.H / BodyH));
        var band = Math.Clamp((int)Math.Floor((u * 5) + ((Bayer[c.Y & 3, c.X & 3] - 7.5) / 16 * 0.9)), 0, 4);
        var sky = Sky[band];

        // the stars, drifting round the can and twinkling; flashing all at once when it is turned on
        var drift = (c.Theta + (t * 0.12)) * BodyR;
        var ai = ((int)Math.Round(drift) % 44 + 44) % 44;
        var hi = (int)Math.Round(c.H);
        if (Hash(ai, hi + 50) % 17 == 0)
        {
            var flash = sinceOn is >= 0.1 and < 0.5;
            var beat = ((t * 1.1) + (Hash(ai, hi) % 100 / 100.0)) % 1;
            if (flash || beat < 0.55) return beat < 0.12 || flash ? WhiteC : CapSheet.Mix(StarBlue, sky, 0.2);
        }

        return c.Tone switch
        {
            0 => CapSheet.Darker(sky, 0.45),
            1 => CapSheet.Darker(sky, 0.22),
            3 => CapSheet.Lighter(sky, 0.15),
            4 => CapSheet.Lighter(sky, 0.32),
            _ => sky
        };
    }

    private static void Twinkle(CapSheet p, int x, int y, int size, uint colour)
    {
        p.Set(x, y, WhiteC);
        for (var k = 1; k <= size; k++)
        {
            var c = k == size && size == 2 ? Aura : colour;
            p.Set(x + k, y, c);
            p.Set(x - k, y, c);
            p.Set(x, y + k, c);
            p.Set(x, y - k, c);
        }
    }

    // ---------------------------------------------------------------- the model

    private static (Cell[] Cells, (int X, int Y)[] Edge, (int X, int Y)[] Halo, int Bottom, (int X, int Y) Tip) Render()
    {
        var light = Norm((-0.55, 0.75, 0.6));
        var cells = new List<Cell>();
        var lit = new HashSet<(int, int)>();
        for (var sy = -18; sy <= 18; sy++)
        {
            for (var sx = -14; sx <= 14; sx++)
            {
                double z = 30;
                var what = -1;
                for (var i = 0; i < 90 && z > -30; i++)
                {
                    var (d, w) = Scene((sx, sy, z));
                    if (d < 0.02)
                    {
                        what = w;
                        break;
                    }

                    z -= Math.Max(0.05, d);
                }

                if (what < 0) continue;
                const double E = 0.05;
                var n = Norm((Scene((sx + E, sy, z)).D - Scene((sx - E, sy, z)).D, Scene((sx, sy + E, z)).D - Scene((sx, sy - E, z)).D,
                    Scene((sx, sy, z + E)).D - Scene((sx, sy, z - E)).D));
                var lamb = Dot(n, light);
                var reflect = Norm(((2 * lamb * n.X) - light.X, (2 * lamb * n.Y) - light.Y, (2 * lamb * n.Z) - light.Z));
                var q = Unturn((sx, sy, z));
                var nm = Unturn(n);
                var tone = lamb > 0.72 ? 4 : lamb > 0.4 ? 3 : lamb > 0.05 ? 2 : lamb > -0.3 ? 1 : 0;
                cells.Add(new Cell(sx, -sy, (Part)what, tone, reflect.Z > 0.94 && what != 2, Math.Atan2(q.X, q.Z), q.Y, nm.Y > 0.7));
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
            foreach (var (dx, dy) in Four)
            {
                if (taken.Add((x + dx, y + dy))) halo.Add((x + dx, y + dy));
            }
        }

        var tip = Turn((4.7, 12.8, 0));
        return ([.. cells], [.. edge], [.. halo], cells.Max(c => c.Y), ((int)Math.Round(tip.X) + 1, -(int)Math.Round(tip.Y)));
    }

    private static readonly (int, int)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private static (double D, int W) Scene((double X, double Y, double Z) p)
    {
        var q = Unturn(p);
        var body = Capsule(q, BodyR, BodyY, BodyH, 2.0);
        var cap = Capsule(q, CapR, CapY, CapH, 0.8);
        var stem = Capsule(q, 1.3, 11.4, 1.0, 0.3);
        var head = Box((q.X - 2.2, q.Y - 12.8, q.Z), (2.5, 1.3, 1.3), 0.3);
        var nozzle = Math.Min(stem, head);
        var d = Math.Min(body, Math.Min(cap, nozzle));
        return (d, d == body ? 0 : d == cap ? 1 : 2);
    }

    /// <summary>A rounded cylinder upright on the model's own axis.</summary>
    private static double Capsule((double X, double Y, double Z) q, double r, double y, double half, double round)
    {
        var dx = Math.Sqrt((q.X * q.X) + (q.Z * q.Z)) - (r - round);
        var dy = Math.Abs(q.Y - y) - (half - round);
        return Math.Min(Math.Max(dx, dy), 0) + Math.Sqrt((Math.Max(dx, 0) * Math.Max(dx, 0)) + (Math.Max(dy, 0) * Math.Max(dy, 0))) - round;
    }

    private static double Box((double X, double Y, double Z) q, (double X, double Y, double Z) b, double round)
    {
        double dx = Math.Abs(q.X) - b.X + round, dy = Math.Abs(q.Y) - b.Y + round, dz = Math.Abs(q.Z) - b.Z + round;
        return Len((Math.Max(dx, 0), Math.Max(dy, 0), Math.Max(dz, 0))) + Math.Min(Math.Max(dx, Math.Max(dy, dz)), 0) - round;
    }

    /// <summary>The model to the screen: pitched towards the viewer, then rolled a little to the right.</summary>
    private static (double X, double Y, double Z) Turn((double X, double Y, double Z) v)
    {
        var p = (X: v.X, Y: (v.Y * Math.Cos(Pitch)) - (v.Z * Math.Sin(Pitch)), Z: (v.Y * Math.Sin(Pitch)) + (v.Z * Math.Cos(Pitch)));
        return ((p.X * Math.Cos(Roll)) + (p.Y * Math.Sin(Roll)), (-p.X * Math.Sin(Roll)) + (p.Y * Math.Cos(Roll)), p.Z);
    }

    private static (double X, double Y, double Z) Unturn((double X, double Y, double Z) v)
    {
        var p = (X: (v.X * Math.Cos(Roll)) - (v.Y * Math.Sin(Roll)), Y: (v.X * Math.Sin(Roll)) + (v.Y * Math.Cos(Roll)), v.Z);
        return (p.X, (p.Y * Math.Cos(Pitch)) + (p.Z * Math.Sin(Pitch)), (-p.Y * Math.Sin(Pitch)) + (p.Z * Math.Cos(Pitch)));
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static double Len((double X, double Y, double Z) a) => Math.Sqrt(Dot(a, a));

    private static (double X, double Y, double Z) Norm((double X, double Y, double Z) a)
    {
        var l = Len(a);
        return (a.X / l, a.Y / l, a.Z / l);
    }

    private static double Smooth(double k) => k * k * (3 - (2 * k));

    private static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    private static double Rnd(int a, int b) => (Hash(a, b) % 10000) / 10000.0;
}
