using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The storm (2026-10-15): storm clouds (lumpy, lit on top, a blue-grey body and a dark underside) roll in from both sides over the skies of
/// both windows, in layers at different heights, until the whole sky is covered and dark, and the sea darkens with it. Rain falls over
/// everything, the clouds flicker from inside, and after three seconds a huge bolt comes down: both windows flash white and shake, and a thick
/// jagged bolt with a white core, a blue halo and branches strikes the fallen one. It burns from its edges inwards (its outline black first,
/// then a front of embers, brown, then black), sparks and smoke rise, its burnt edges crack off as ash, and its black silhouette goes up in
/// smoke and ash; its writing burns the same way, from left to right. Then the clouds draw back and the light returns.
/// </summary>
internal sealed class StormDeath : DeathStyle
{
    public const double BoltAt = 3.0;
    private const double Burn = 1.3, CrumbleFrom = 0.8, Crumble = 1.4;
    private readonly List<(double X, double Y)> _main = [];
    private readonly List<(List<(double X, double Y)> Pts, int W)> _branches = [];
    private readonly List<(DeathCell Q, double D, bool Info)> _burning = [];
    private readonly double _maxD = 1;

    public StormDeath(CapDeathScene s) : base(s)
    {
        // the bolt: a jagged zigzag from the clouds to it, with branches that branch again
        double x = S.X + 10, y = 0;
        _main.Add((x, y));
        for (var i = 0; y < S.Y - 6; i++)
        {
            y = Math.Min(S.Y - 6, y + 4 + (Rnd(i, 1) * 6));
            x += ((Rnd(i, 2) - 0.5) * 12 * (1 - (y / Math.Max(1.0, S.Y) * 0.5))) + ((S.X - x) * 0.18);
            _main.Add((x, y));
        }

        void Grow((double X, double Y) from, double a, int len, int w, int depth, int salt)
        {
            var pts = new List<(double, double)> { from };
            var (bx, by) = from;
            for (var j = 0; j < len; j++)
            {
                bx += (Math.Cos(a) * 5) + ((Rnd(salt, j) - 0.5) * 4);
                by += Math.Sin(a) * 5;
                pts.Add((bx, by));
                if (depth > 0 && j == 1) Grow((bx, by), a + (Rnd(salt, 9) < 0.5 ? 0.6 : -0.6), Math.Max(2, len - 2), Math.Max(1, w - 1), depth - 1, salt + 17);
            }

            _branches.Add((pts, w));
        }

        int[] at = [2, 4, 6, 8, 10];
        for (var b = 0; b < at.Length; b++)
        {
            if (at[b] < _main.Count) Grow(_main[at[b]], (Math.PI / 2) + (b % 2 == 1 ? 0.7 : -0.7), 4 + (b % 3), 2, 1, 50 + b);
        }

        // how far into the picture each cell is (0 on its outline), for the burn that eats inwards; the writing burns from left to right
        var byKey = S.Body.ToDictionary(q => DeathPiece.Key(q.Dx, q.Dy));
        var depthOf = new Dictionary<DeathCell, int>();
        var frontier = S.Body.Where(q => Round4.Any(o => !byKey.ContainsKey(DeathPiece.Key(q.Dx + o.X, q.Dy + o.Y)))).ToList();
        foreach (var q in frontier) depthOf[q] = 0;
        for (var n = 1; frontier.Count > 0; n++)
        {
            var next = new List<DeathCell>();
            foreach (var q in frontier)
            {
                foreach (var (ox, oy) in Round4)
                {
                    if (byKey.TryGetValue(DeathPiece.Key(q.Dx + ox, q.Dy + oy), out var o) && !depthOf.ContainsKey(o))
                    {
                        depthOf[o] = n;
                        next.Add(o);
                    }
                }
            }

            frontier = next;
        }

        _maxD = Math.Max(1, depthOf.Count > 0 ? depthOf.Values.Max() : 1);
        foreach (var q in S.Body) _burning.Add((q, depthOf.GetValueOrDefault(q), false));
        foreach (var q in S.Info) _burning.Add((q, (q.X - S.InfoX0) * 0.11, true));
    }

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s) => (p.Cx, p.Cy, false);

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece) { }

    /// <summary>A cell burning: its colour when the front of the burn is <paramref name="e"/> cells past it.</summary>
    private static uint Burnt(uint c, double e, int f, int key)
    {
        if (e < -1.5) return c;
        if (e < 0) return CapSheet.Mix(c, 0xFF7020u, (e + 1.5) / 1.5 * 0.55);
        if (e < 1.2) return CapArt.Hash(key, f) % 3 != 0 ? 0xFFA030u : 0xFF5010u;
        if (e < 3) return CapSheet.Mix(0x6A3A18u, 0x2A1A10u, (e - 1.2) / 1.8);
        return Rnd(key, f / 3) < 0.05 ? 0xFF6A20u : 0x161210u;
    }

    private double BurnAt((DeathCell Q, double D, bool Info) b) => Burn * (b.D / _maxD);

    private double CrumbleAt((DeathCell Q, double D, bool Info) b) => CrumbleFrom + (Crumble * (b.D / _maxD) * (b.Info ? 0.9 : 1)) + (b.Q.H * 0.15);

    private void PaintBurn(CapSheet p, double t, int f)
    {
        foreach (var b in _burning)
        {
            var q = b.Q;
            var key = (q.X * 1000) + q.Y;
            var tc = CrumbleAt(b);
            if (t < tc)
            {
                // the dark edge round the letters only chars; the letters themselves burn through embers to black
                var e = ((t - BurnAt(b)) / Burn * _maxD) + 1;
                p.Set(q.X, q.Y, b.Info && Luma(q.C) < 90 ? (e < 0 ? q.C : 0x161210u) : Burnt(q.C, e, f, key));
                continue;
            }

            // a flake of ash: it falls, swaying, dark with a hot edge, and scatters
            var age = t - tc;
            if (age > 1.1 || q.H > 0.55) continue;
            double x = q.X + (Math.Sin((age * 6) + (q.H * 40)) * (1 + (age * 3))) + ((q.H - 0.27) * age * 20), y = q.Y + (age * 10) + (age * age * 14);
            Put(p, x, y, q.H < 0.08 ? 0xFF8030u : 0x3A3430u, 240 * (1 - (age / 1.1)));
            if (q.H < 0.2) Put(p, x + 1, y, 0x2A2622u, 200 * (1 - (age / 1.1)));
        }

        // sparks and embers rising from where it burns; smoke drifting up
        for (var i = 0; i < 90 && _burning.Count > 0; i++)
        {
            var b = _burning[CapArt.Hash(i, 7) % _burning.Count];
            var age = t - (BurnAt(b) + (Rnd(i, 8) * 0.4));
            if (age < 0 || age > 0.9) continue;
            Put(p, b.Q.X + (Math.Sin((age * 9) + i) * 2), b.Q.Y - (age * (14 + (Rnd(i, 9) * 12))), age < 0.3 ? 0xFFE070u : 0xFF7020u, 255 * (1 - (age / 0.9)));
        }

        for (var i = 0; i < 26 && _burning.Count > 0; i++)
        {
            var b = _burning[CapArt.Hash(i, 11) % _burning.Count];
            var age = t - (BurnAt(b) + 0.2 + (Rnd(i, 12) * 0.5));
            if (age < 0 || age > 1.4) continue;
            Puff(p, b.Q.X + (Math.Sin((age * 3) + i) * 3), b.Q.Y - 3 - (age * 16), 1.5 + (age * 3), 0x6A6A70u, 130 * (1 - (age / 1.4)));
        }

        // what is left goes up in a last cloud of smoke
        var last = (t - 2.2) / 0.8;
        if (last > 0 && last < 1)
        {
            for (var i = 0; i < 10; i++) Puff(p, S.X + ((Rnd(i, 13) - 0.5) * 24), S.Y - (last * 18) - (Rnd(i, 14) * 8), 3 + (last * 4), 0x5A5A62u, 150 * (1 - last));
        }
    }

    public override void Frame(CapSheet p, double t, double phase)
    {
        var tb = t - BoltAt;
        var f = (int)Math.Floor(t * 12);
        if (tb < 0) Whole(p);
        CapDeathStormSky.Paint(p, S.Layout, t);
        if (tb >= 0) PaintBurn(p, tb, f);

        // the bolt: the window white, then the huge jagged bolt with its branches, its glare where it strikes, sparks flying
        if (tb >= 0.04 && tb < 0.6 && !(tb > 0.22 && tb < 0.28))
        {
            var k = tb < 0.3 ? 1 : 1 - ((tb - 0.3) / 0.3);
            void DrawPath(List<(double X, double Y)> pts, int w)
            {
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    Line(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, (x, y) =>
                    {
                        for (var d = -w - 5; d <= w + 5; d++)
                        {
                            var a = Math.Abs(d);
                            var col = a <= w - 1 ? 0xFFFFFFu : a <= w ? 0xE8F0FFu : a <= w + 2 ? 0x9CB8FFu : 0x6A88FFu;
                            p.Set(x + d, y, col, (int)((a <= w ? 255 : a <= w + 2 ? 200 : 110) * k));
                        }
                    });
                }
            }

            foreach (var (pts, w) in _branches) DrawPath(pts, w);
            DrawPath(_main, 4);
            Glow(p, S.X, S.Y, 34, 26, 0xFFF4D0u, 230 * k);
            Glow(p, S.X, S.Y, 12, 10, 0xFFFFFFu, 255 * k);
            var s = tb - 0.04;
            for (var i = 0; i < 40; i++)
            {
                var a = Rnd(i, 111) * 2 * Math.PI;
                var v = 20 + (Rnd(i, 112) * 40);
                Put(p, S.X + (Math.Cos(a) * v * s), S.Y + (Math.Sin(a) * v * s * 0.7) + (20 * s * s), i % 2 == 1 ? 0xFFE070u : 0xFFFFFFu, 255 * k);
            }
        }

        CapDeathStormSky.Flash(p, t);
    }
}

/// <summary>What the storm does to a whole window, either of them: the dark, the clouds, the rain, the flash, the shake, the light coming back.</summary>
internal static class CapDeathStormSky
{
    private static readonly Dictionary<string, List<Cloud>> Clouds = [];

    private sealed record Cloud(double X, double Y, int From, int X0, int X1, int Layer, double Dark, double Speed, (int X, int Y, int Tone)[] Cells);

    private static double Cover(double t) => t < 0.2 ? 0 : t < 1.8 ? Ease((t - 0.2) / 1.6) : t < 6.1 ? 1 : 1 - Ease((t - 6.1) / 1.0);

    private static double Rain(double t) => t < 1.3 ? 0 : t < 6.3 ? Clamp01((t - 1.3) / 0.8) : 1 - Clamp01((t - 6.3) / 0.6);

    // hi, light, mid, low, deep
    private static readonly uint[] Tones = [0xA4ACC0, 0x7E8698, 0x5A6074, 0x3E4254, 0x282A36];

    /// <summary>A lumpy cloud made once: a flat-bottomed base and bumps on top, each cell shaded by where its own edge is.</summary>
    private static (int X, int Y, int Tone)[] MakeCloud(int seed, double w, double h)
    {
        var n = 3 + (seed % 3);
        var bumps = new (double X, double Y, double R)[n];
        for (var i = 0; i < n; i++) bumps[i] = (((i + 0.5) / n * w) - (w / 2) + ((Rnd(seed, i) - 0.5) * 6), (-h * 0.35) - (Rnd(i, seed) * h * 0.4), h * (0.45 + (Rnd(seed, i + 9) * 0.35)));
        var cells = new Dictionary<int, (int X, int Y, int Bump)>();
        int Key(int x, int y) => ((x + 500) * 1000) + y + 500;
        for (var y = -(int)Math.Ceiling(h * 1.4); y <= Math.Ceiling(h * 0.5); y++)
        {
            for (var x = -(int)Math.Ceiling((w / 2) + 6); x <= (w / 2) + 6; x++)
            {
                var inBase = Math.Abs(x) <= (w / 2) - (y > 0 ? y * 1.5 : 0) && y <= h * 0.5 && y >= -h * 0.45
                    && Math.Sqrt(((x / ((w / 2) + 1)) * (x / ((w / 2) + 1))) + ((Math.Max(0, -y) / (h * 0.6)) * (Math.Max(0, -y) / (h * 0.6)))) <= 1.05;
                var bump = -1;
                for (var i = 0; i < n; i++)
                {
                    if (Math.Sqrt(((x - bumps[i].X) * (x - bumps[i].X)) + ((y - bumps[i].Y) * 1.15 * (y - bumps[i].Y) * 1.15)) <= bumps[i].R) bump = i;
                }

                if (inBase || bump >= 0) cells[Key(x, y)] = (x, y, bump);
            }
        }

        // a ragged edge: a few edge cells dropped
        foreach (var (key, c) in cells.ToArray())
        {
            var edge = Round4.Any(o => !cells.ContainsKey(Key(c.X + o.X, c.Y + o.Y)));
            if (edge && Rnd(c.X + (seed * 3), c.Y) < 0.28) cells.Remove(key);
        }

        bool Has(int x, int y) => cells.ContainsKey(Key(x, y));
        return [.. cells.Values.Select(c =>
        {
            int tone;
            if (!Has(c.X, c.Y - 1)) tone = 0;
            else if (!Has(c.X, c.Y - 2)) tone = 1;
            else if (!Has(c.X, c.Y + 1)) tone = 4;
            else if (!Has(c.X, c.Y + 2) || (!Has(c.X, c.Y + 3) && ((c.X + c.Y) & 1) == 1)) tone = 3;
            else tone = 2;

            // the lower arc of each bump shows as a darker line, so the lumps read
            if (c.Bump >= 0 && tone == 2)
            {
                var (bx, by, br) = bumps[c.Bump];
                var d = Math.Sqrt(((c.X - bx) * (c.X - bx)) + ((c.Y - by) * 1.15 * (c.Y - by) * 1.15));
                if (d > br - 1.2 && c.Y > by) tone = 3;
            }

            if (tone == 2 && Rnd(c.X, c.Y + seed) < 0.12) tone = 1;
            return (c.X, c.Y, tone);
        })];
    }

    private static List<Cloud> CloudsOf(CapWorldLayout l)
    {
        var key = $"{l.Panel}|{l.Cols}|{l.Rows}|{l.Main}";
        lock (Clouds)
        {
            if (Clouds.TryGetValue(key, out var made)) return made;
            var list = new List<Cloud>();
            var seed = l.Panel == CapPanelKind.Deep ? 1 : 101;
            int x0 = l.Main.Left, x1 = l.Main.Right;
            var mid = (x0 + x1) / 2.0;
            (int Y, int W, int H, double Dark, double Speed)[] layers = [(8, 46, 15, 0.35, 0.9), (22, 40, 13, 0.15, 1.3), (38, 34, 11, 0, 1.8)];
            for (var layer = 0; layer < layers.Length; layer++)
            {
                var (y, w, h, dark, speed) = layers[layer];
                for (double x = x0 - 10 + (layer * 9); x < x1 + 20; x += w * 0.72)
                {
                    var cy = y * Math.Min(1, l.SurfaceY / 70.0);
                    list.Add(new Cloud(x, cy + ((Rnd(seed, layer) - 0.5) * 6), x < mid ? -1 : 1, x0, x1, layer, dark, speed, MakeCloud(seed++, w + (Rnd(seed, 2) * 10), h)));
                }
            }

            Clouds[key] = list;
            return list;
        }
    }

    /// <summary>The dark under the clouds, the clouds rolling in and drifting, and the rain: in whichever window.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, double t)
    {
        var c = Cover(t);
        if (c > 0) Each(p, (x, y) => p.Set(x, y, y < l.SurfaceY ? 0x2A2E3Cu : 0x0A0C18u, (int)((y < l.SurfaceY ? 185 : 95) * c)));
        if (c > 0)
        {
            var flick = (Math.Abs(t - 2.0) < 0.07 || Math.Abs(t - 2.45) < 0.05 || Math.Abs(t - 2.6) < 0.05) ? 1 : 0;
            foreach (var cl in CloudsOf(l))
            {
                var off = cl.From * (1 - c) * (((cl.X1 - cl.X0) * 0.75) + 30);
                var drift = (Math.Sin((t * 0.25) + cl.Layer) * 2) + (t * cl.Speed * 0.5 * -cl.From);
                foreach (var (dx, dy, tone) in cl.Cells)
                {
                    int x = R(cl.X + off + drift + dx), y = R(cl.Y + dy);
                    if (y >= l.SurfaceY || y < 0 || x < cl.X0 || x >= cl.X1) continue;
                    var col = Tones[tone];
                    if (cl.Dark > 0) col = CapSheet.Mix(col, 0x1A1C26u, cl.Dark);
                    if (flick == 1 && cl.Layer < 2) col = CapSheet.Mix(col, 0xE0E8FFu, tone >= 3 ? 0.25 : 0.55);
                    p.Set(x, y, col, (int)(255 * Math.Min(1, c * 1.5)));
                }
            }
        }

        // the rain over everything, splashes on the surface
        var rr = Rain(t);
        if (rr <= 0) return;
        for (var i = 0; i < R(160 * rr * Math.Max(0.5, p.W / 200.0)); i++)
        {
            var age = ((t * 2.4) + Rnd(i, 101)) % 1;
            var x0 = Rnd(i, 102) * (p.W + 40);
            var y = age * (p.H + 10);
            var x = x0 - (age * 34);
            for (var j = 0; j < 3; j++) Put(p, x - (j * 0.5), y - (j * 2), 0x9CB4D8u, (y - (j * 2) < l.SurfaceY ? 190 : 80) - (j * 40));
            if (Math.Abs(y - l.SurfaceY) < 2)
            {
                Put(p, x - 1, l.SurfaceY - 1, 0xC8E0FFu, 200);
                Put(p, x + 1, l.SurfaceY - 1, 0xC8E0FFu, 200);
            }
        }
    }

    /// <summary>The flash of the bolt over the whole window, the light coming back at the end, and the shake.</summary>
    public static void Flash(CapSheet p, double t)
    {
        var tb = t - StormDeath.BoltAt;
        if (tb >= 0 && tb < 0.1) Each(p, (x, y) => p.Set(x, y, 0xFFFFFFu, 245));
        var back = Clamp01((t - 6.7) / 0.5);
        if (back > 0 && back < 1) Glow(p, p.W / 2.0, 30, p.W * 0.45, 30, 0xFFF4D0u, 60 * Math.Sin(Math.PI * back));
        Shake(p, tb, 5, 0.4);
    }

    /// <summary>The deep's share: everything but the bolt and the burn.</summary>
    public static void Deep(CapSheet p, CapWorldLayout l, double t)
    {
        Paint(p, l, t);
        Flash(p, t);
    }
}
