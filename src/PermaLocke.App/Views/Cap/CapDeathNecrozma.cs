using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The death by Necrozma, as a cinematic (2026-10-15). Black bars slide in and the camera pushes in on the party's window; a white slash across
/// it onto the fallen one splits into the seven colours; above it reality cracks into a rift (a void of deep violet and stars, an iridescent
/// rim, shards of glass breaking off, light leaking out in rays); in its dark a blue eye opens, its coloured specks lighting one by one;
/// Necrozma (the sprite of the player's reference) comes out face first, materialising from its eye outwards, a rim of cold light on it, its
/// crystal blade glinting, unfolding its arms, floating and breathing. A wave of grey spreads over both windows and the colours are torn out
/// of everything and stream into its eye; its seams light up with the colour it drinks, the fallen one the last thing in colour. A prism beam
/// from its eye: the fallen one comes apart in the seven bands of the rainbow that flow up the beam with its writing, while it reaches for it
/// with its claws and bends its head to it. It fills with light, arms up, prismatic rays sweep the layout, a white flash; the colour comes
/// back in rainbow rings, the rift closes on a point and a last glint, the bars go, prismatic motes drift down.
/// </summary>
internal sealed class NecrozmaDeath : DeathStyle
{
    private static readonly (double A, double B) Bars = (0, 0.45), Slash = (0.05, 0.4), Tear = (0.45, 1.15), Eye = (1.05, 1.5), Come = (1.35, 2.45),
        Drain = (2.45, 3.4), Take = (3.3, 4.25), BurstT = (4.25, 4.75), Back = (4.75, 5.45), End = (5.45, 6.2);

    private static double K((double A, double B) span, double t) => Span(span.A, span.B, t);

    private readonly (int X, int Y) _rift;
    private readonly HashSet<int> _keep = [];
    private readonly (int X, int Y)[] _speckFrom;
    private readonly int[] _bandTop = [];

    public NecrozmaDeath(CapDeathScene s) : base(s)
    {
        var l = S.Layout;
        _rift = (Math.Clamp(S.X + 14, l.Main.Left + 30, Math.Max(l.Main.Left + 30, l.Main.Right - 30)), Math.Max(34, S.Y - 58));
        int Band(DeathCell q) => Math.Min(6, (int)Math.Floor((q.Dy - S.Top) / (double)(S.Bottom - S.Top + 1) * 7));
        M = S.Model(q => (Band(q) * 100) + (int)Math.Floor((q.Dx + 40) / 7.0), p => p.Cy, 0.35, 0.75, (0, 0), 0.05, 0.3);
        foreach (var q in S.Body) _keep.Add((q.X * 1000) + q.Y);
        foreach (var q in S.Info) if (Luma(q.C) > 100) _keep.Add((q.X * 1000) + q.Y);
        _speckFrom = [.. Enumerable.Range(0, 180).Select(i => ((int)(Rnd(i, 31) * l.Cols), (int)(Rnd(31, i) * l.Rows)))];
    }

    public override uint Glow => 0xFFFFFFu;
    public override double EndAt => 9;
    public override uint CrackRim => 0x202028u;
    public override double InfoFrom => 0.25;
    public override double DissolveFrom => 0.3;
    public override double DissolveSpread => 0.2;

    private uint BandColour(DeathCell q) => q.Writing
        ? Rainbow[((q.X * 3) + q.Y) % 7]
        : Rainbow[Math.Clamp((int)Math.Floor((q.Dy - S.Top) / (double)(S.Bottom - S.Top + 1) * 7), 0, 6)];

    private (double X, double Y) EyeAt => (_rift.X, _rift.Y);

    public override uint Body(DeathCell q, double te) => CapSheet.Mix(q.C, BandColour(q), Clamp01(te / 0.3) * 0.75);

    public override uint Hole(double t, DeathCell q) => BandColour(q);

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s)
    {
        var u = Ease(Clamp01(s / 0.5));
        double tx = EyeAt.X - S.X, ty = EyeAt.Y - S.Y;
        return (p.Cx + ((tx - p.Cx) * u) + (Math.Sin((u * 9) + p.Cy) * 4 * (1 - u)), p.Cy + ((ty - p.Cy) * u), false);
    }

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p) => CapSheet.Mix(BandColour(q), 0xFFFFFFu, 0.2);

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (age > 0.3) return;
        var u = age / 0.3;
        Put(p, x + ((EyeAt.X - x) * u), y + ((EyeAt.Y - y) * u), BandColour(q), 240);
    }

    public override uint? Info(DeathCell q, double te) => CapSheet.Mix(q.C, Rainbow[((q.X * 3) + q.Y) % 7], Clamp01(te / 0.25) * 0.8);

    // ---------------------------------------------------------------- the rift

    private (double Rx, double Ry) RiftSize(double t)
    {
        var o = K(Tear, t);
        var c = K(Back, t);
        var open = Ease(Clamp01((o - 0.35) / 0.65));
        return (17 * open * (1 - Ease(c)), 23 * Math.Max(open, 0.001) * (1 - Ease(c)));
    }

    private static uint Hue(double a, double t) => Rainbow[((int)Math.Floor((a / (2 * Math.PI) * 14) + (t * 9)) % 7 + 7) % 7];

    private void Rift(CapSheet p, double t)
    {
        var (rx, ry) = RiftSize(t);
        var o = K(Tear, t);
        var (cx, cy) = _rift;

        // the crack of white light before it opens
        if (o > 0 && o < 0.6)
        {
            var grow = Clamp01(o / 0.35);
            var pts = Enumerable.Range(0, 13).Select(i => (X: cx + ((Rnd(i, 3) - 0.5) * 5 * Math.Sin(Math.PI * i / 12.0)), Y: cy - 24 + (i / 12.0 * 48))).ToArray();
            for (var i = 0; i + 1 < pts.Length && i < grow * 12; i++)
            {
                Line(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, (x, y) =>
                {
                    p.Set(x, y, 0xFFFFFFu);
                    p.Set(x + 1, y, 0xC8E8FFu, 160);
                    p.Set(x - 1, y, 0xC8E8FFu, 160);
                });
            }

            for (var b = 0; b < 6; b++)
            {
                var (bx, by) = pts[2 + (b * 2)];
                var a = b % 2 == 1 ? 0.6 : Math.PI - 0.6;
                var len = 4 + (Rnd(b, 9) * 6 * grow);
                Line(bx, by, bx + (Math.Cos(a) * len), by + (Math.Sin(a) * len * 0.6), (x, y) => p.Set(x, y, 0xE8F4FFu, 220));
            }
        }

        if (rx < 0.6) return;

        // light leaking from it in rays that turn slowly
        for (var r = 0; r < 12; r++)
        {
            var a = (r / 12.0 * 2 * Math.PI) + (t * 0.4);
            for (var d = rx; d < rx + 70; d++)
            {
                for (var s = -1; s <= 1; s++)
                {
                    var aa = a + (s * 0.07 * (1 - ((d - rx) / 70)));
                    Put(p, cx + (Math.Cos(aa) * d), cy + (Math.Sin(aa) * d * 1.1), s == 0 ? 0xF4F8FFu : Hue(aa, t), 70 * (1 - ((d - rx) / 70)));
                }
            }
        }

        // the void inside and its iridescent rim
        for (var dy = -(int)Math.Ceiling(ry) - 3; dy <= ry + 3; dy++)
        {
            for (var dx = -(int)Math.Ceiling(rx) - 3; dx <= rx + 3; dx++)
            {
                var lens = (Math.Abs(dx) / rx) + ((dy / ry) * (dy / ry));
                if (lens <= 1)
                {
                    var n = 0.5 + (0.5 * Math.Sin((dx * 0.4) + (t * 2)) * Math.Sin((dy * 0.3) - t));
                    var c = CapSheet.Mix(0x05030Cu, 0x2A1060u, n * 0.8 * (1 - lens));
                    if (CapArt.Hash(dx + 50, dy + 50) % 29 == 0) c = CapSheet.Mix(c, 0xFFFFFFu, 0.5 + (0.5 * Math.Sin((t * 8) + dx)));
                    p.Set(cx + dx, cy + dy, c);
                }
                else if (lens <= 1.35)
                {
                    p.Set(cx + dx, cy + dy, CapSheet.Mix(Hue(Math.Atan2(dy, dx), t), 0xFFFFFFu, lens < 1.12 ? 0.55 : 0), (int)((255 * (1.35 - lens) / 0.35) + 60));
                }
            }
        }

        // shards of glass breaking off its rim and floating out
        for (var i = 0; i < 16; i++)
        {
            var age = (((t - Tear.A) * 0.6) + Rnd(i, 21)) % 1;
            var a = Rnd(i, 22) * 2 * Math.PI;
            var d = rx + 2 + (age * 30);
            double x = cx + (Math.Cos(a) * d), y = cy + (Math.Sin(a) * d * 1.1) - (age * 6);
            var size = 1 + (int)(Rnd(i, 23) * 3);
            for (var j = 0; j < size; j++)
            {
                for (var k = 0; k <= j; k++) Put(p, x + k, y + j, k == 0 ? 0xFFFFFFu : Hue(a, t), 230 * (1 - age));
            }
        }
    }

    // ---------------------------------------------------------------- Necrozma

    private sealed record NecCell(int X, int Y, uint C, bool Edge, bool Eye, bool Light, double Ang, double H, string Part);

    private static readonly NecCell[] Cells;
    private static readonly Dictionary<string, Dictionary<int, NecCell>> Parts;
    private static readonly Dictionary<string, (double X, double Y)> Joints;
    private static readonly (double X, double Y) Hand;
    private static readonly double EyeX, EyeY;

    static NecrozmaDeath()
    {
        var rows = CapNecrozmaArt.Rows;
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var raw = new List<(int X, int Y, uint C)>();
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] != '.') raw.Add((x, y, CapNecrozmaArt.Palette[chars.IndexOf(rows[y][x])]));
            }
        }

        var set = raw.Select(c => (c.X * 1000) + c.Y).ToHashSet();
        double cx = raw.Average(c => c.X), cy = raw.Average(c => c.Y);
        var sc = CapNecrozmaArt.Scale;
        Cells = [.. raw.Select(c =>
        {
            double sx = c.X / sc, sy = c.Y / sc;
            var part = sx < 26 || (sx < 30 && sy > 28) ? "claws" : sx >= 57 && sy >= 44 ? "blade" : sy >= 46 && sx >= 26 && sx < 57 ? "legs" : "head";
            return new NecCell(c.X, c.Y, c.C, Round4.Any(o => !set.Contains(((c.X + o.X) * 1000) + c.Y + o.Y)), Sat(c.C) > 70, Luma(c.C) > 175,
                Math.Atan2(c.Y - cy, c.X - cx), Rnd((c.X * 17) + 3, (c.Y * 29) + 11), part);
        })];
        var eyes = Cells.Where(c => c.Eye).ToArray();
        EyeX = eyes.Length > 0 ? eyes.Average(c => c.X) : cx;
        EyeY = eyes.Length > 0 ? eyes.Average(c => c.Y) : cy;
        Parts = Cells.GroupBy(c => c.Part).ToDictionary(g => g.Key, g => g.ToDictionary(c => (c.X * 1000) + c.Y));
        (double, double) J(double x, double y) => (x * sc, y * sc);
        Joints = new() { ["claws"] = J(25, 36), ["blade"] = J(58, 46), ["legs"] = J(42, 50), ["head"] = J(44, 46) };
        Hand = J(8, 88);
    }

    /// <summary>Necrozma in a pose: each part turned about its joint and moved, back to front, as where each of its cells is now.</summary>
    private static List<(NecCell C, int X, int Y)> Posed(Dictionary<string, double> angles, Dictionary<string, (double X, double Y)> shifts)
    {
        var placed = new Dictionary<int, (NecCell, int, int)>();
        foreach (var name in new[] { "claws", "legs", "head", "blade" })
        {
            if (!Parts.TryGetValue(name, out var part)) continue;
            var a = angles.GetValueOrDefault(name);
            var (jx, jy) = Joints[name];
            var (mx, my) = shifts.GetValueOrDefault(name);
            double c = Math.Cos(a), s = Math.Sin(a);
            var rad = part.Values.Max(q => Math.Max(Math.Abs(q.X - jx), Math.Abs(q.Y - jy))) + 3;
            for (var ty = (int)Math.Floor(jy - rad + my); ty <= jy + rad + my; ty++)
            {
                for (var tx = (int)Math.Floor(jx - rad + mx); tx <= jx + rad + mx; tx++)
                {
                    double dx = tx - mx - jx, dy = ty - my - jy;
                    int sx = R(jx + (dx * c) + (dy * s)), sy = R(jy - (dx * s) + (dy * c));
                    if (part.TryGetValue((sx * 1000) + sy, out var q)) placed[((tx + 500) * 1000) + ty + 500] = (q, tx, ty);
                }
            }
        }

        return [.. placed.Values];
    }

    /// <summary>
    /// How it moves: it floats and breathes all the time; it comes out unfolding its arms; it spreads them wide while it drinks; it reaches for
    /// the fallen one with its claws and bends its head to it; it throws its arms up at the burst.
    /// </summary>
    private (List<(NecCell C, int X, int Y)> Cells, (int X, int Y) Float) PoseAt(double t)
    {
        var unfold = Ease(K(Come, t));
        var spread = Ease(K(Drain, t)) * (1 - Ease(Span(Take.A, Take.A + 0.3, t)));
        var take = Math.Sin(Math.PI * K(Take, t));
        var up = Ease(K(BurstT, t));
        double sway = Math.Sin(t * 2.6), sway2 = Math.Sin((t * 2.6) + 1.3);
        var (sjx, sjy) = Joints["claws"];
        double hx = Hand.X - sjx, hy = Hand.Y - sjy;
        double sx = _rift.X - EyeX + sjx, sy = _rift.Y - EyeY + sjy;
        var aim = Math.Atan2(S.Y - sy, S.X - sx) - Math.Atan2(hy, hx);
        var angles = new Dictionary<string, double>
        {
            ["claws"] = (-0.45 * (1 - unfold)) + (0.06 * sway) + (0.22 * spread) + (aim * 0.8 * take) + (0.85 * up),
            ["blade"] = (0.45 * (1 - unfold)) + (0.06 * sway2) - (0.22 * spread) - (0.85 * up),
            ["legs"] = (0.05 * Math.Sin(t * 1.9)) - (0.12 * spread),
            ["head"] = (0.035 * Math.Sin(t * 1.7)) - (0.06 * spread) + (0.12 * take) - (0.05 * up),
        };
        var reach = take * 4;
        var shifts = new Dictionary<string, (double, double)> { ["claws"] = ((Math.Cos(aim) * reach * 0.3) - (reach * 0.5), reach), ["head"] = (0, R(take * 1.5)) };
        return (Posed(angles, shifts), (R(0.9 * Math.Sin(t * 1.3)), R((1.6 * Math.Sin(t * 2.2)) - (9 * (1 - unfold)))));
    }

    private void Necrozma(CapSheet p, double t, int f)
    {
        var c = K(Come, t);
        var gone = K(Back, t);
        if (c <= 0 && K(Eye, t) <= 0) return;
        var reveal = Ease(c) * 95;
        var drink = K(Drain, t);
        var fill = K(BurstT, t);
        var run = t * 2.6;
        var (pose, flt) = PoseAt(t);
        (int X, int Y) Place(int x, int y) => (R(_rift.X - EyeX + x + flt.X), R(_rift.Y - EyeY + y + flt.Y));

        // a soft violet halo behind it, so it stands out from the dark
        var halo = Ease(c) * (1 - gone);
        if (halo > 0)
        {
            foreach (var (q, px, py) in pose)
            {
                if (!q.Edge || q.H > 0.5) continue;
                var (x, y) = Place(px, py);
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        if (((x + dx + y + dy) & 1) == 0) p.Set(x + dx, y + dy, 0x7A60D0u, (int)(40 * halo));
                    }
                }
            }
        }

        var (rx, ry) = RiftSize(t);
        foreach (var (q, px, py) in pose)
        {
            var (x, y) = Place(px, py);
            var d = Math.Sqrt(((q.X - EyeX) * (q.X - EyeX)) + ((q.Y - EyeY) * (q.Y - EyeY)));
            if (gone > 0.15)
            {
                var lens = (Math.Abs(x - _rift.X) / Math.Max(0.6, rx)) + (((y - _rift.Y) / Math.Max(0.6, ry)) * ((y - _rift.Y) / Math.Max(0.6, ry)));
                if (lens > 1) continue;
            }

            if (d > reveal + 0.5 && !(q.Eye && K(Eye, t) > 0)) continue;
            var col = q.C;
            if (q.Eye)
            {
                var on = (int)Math.Floor((t * 10) + (q.X * 0.7)) % 7;
                col = CapSheet.Mix(q.C, Rainbow[(on + q.Y) % 7], 0.7);
                col = CapSheet.Mix(col, 0xFFFFFFu, 0.3 * Math.Max(0, Math.Sin((t * 12) + q.X)));
            }
            else if (q.Edge)
            {
                col = CapSheet.Mix(col, 0xC8D4FFu, 0.45);   // a rim of cold light, so it reads on the dark
            }

            if (d > reveal - 1.5 && c < 1) col = CapArt.Hash(q.X, q.Y + f) % 3 == 0 ? 0xFFFFFFu : CapSheet.Mix(col, 0xC8E0FFu, 0.5);   // forming, sparkling
            if (q.Edge)
            {
                var da = Math.Abs((((q.Ang - run) % (2 * Math.PI)) + (3 * Math.PI)) % (2 * Math.PI) - Math.PI);
                if (da < 0.35) col = CapSheet.Mix(col, 0xFFFFFFu, 0.7 * (1 - (da / 0.35)));   // light running along its outline
            }

            if (drink > 0 && (q.Edge || q.Light) && !q.Eye) col = CapSheet.Mix(col, Rainbow[((int)Math.Floor((q.Ang * 3) + (t * 8) + 70) % 7 + 7) % 7], 0.55 * drink);   // its seams drink the colour
            if (q.Light && t > 2.0 && t < 2.5)
            {
                var sweep = ((t - 2.0) / 0.5 * 40) - ((q.X + q.Y) * 0.5);
                if (Math.Abs(sweep) < 2) col = 0xFFFFFFu;   // the blade glints
            }

            if (fill > 0) col = CapSheet.Mix(col, CapSheet.Mix(0xFFFFFFu, 0xFFE890u, q.H), Clamp01((fill * 1.6) - (d / 60)));
            p.Set(x, y, col);
        }

        if (K(Eye, t) > 0) Glow(p, _rift.X, _rift.Y, 6, 4, 0x6A8CFFu, 120 * Math.Min(1, K(Eye, t) * 2) * (1 - gone));
    }

    // ---------------------------------------------------------------- the frame

    public override void Frame(CapSheet p, double t, double phase)
    {
        var f = (int)Math.Floor(t * 15);
        var l = S.Layout;
        var te = t - Take.A;
        if (te < 0) Whole(p);
        else S.Engine(p, M, this, te);

        // the colours of what the specks are torn out of, before anything goes grey
        var speckColours = _speckFrom.Select(sp => p.Solid(sp.X, sp.Y) ? p.At(sp.X, sp.Y) : 0x40506Au).ToArray();

        var back = K(Back, t);
        var dim = Math.Min(K(Tear, t) * 1.5, 1) * (1 - back);
        if (dim > 0) Each(p, (x, y) => p.Set(x, y, 0x06040Eu, (int)(90 * dim)));

        // the grey: everything the wave has passed loses its colour; the fallen one keeps its own a moment longer, the last colour there is
        var gr = K(Drain, t) * 700;
        if (gr > 0 && back <= 0)
        {
            Each(p, (x, y) =>
            {
                var d = Math.Sqrt(((x - _rift.X) * (x - _rift.X)) + ((y - _rift.Y) * (y - _rift.Y)));
                if (d > gr || !p.Solid(x, y)) return;
                if (t < Take.A + 0.3 && _keep.Contains((x * 1000) + y)) return;
                p.Set(x, y, Grey(p.At(x, y)), (int)(255 * Clamp01((gr - d) / 30)));
            });
        }

        Rift(p, t);
        Necrozma(p, t, f);

        // the specks torn out of everything, each its own colour, spiralling into the eye
        if (K(Drain, t) > 0 && back <= 0)
        {
            for (var i = 0; i < _speckFrom.Length; i++)
            {
                var (sx0, sy0) = _speckFrom[i];
                var dist = Math.Sqrt(((sx0 - _rift.X) * (sx0 - _rift.X)) + ((sy0 - _rift.Y) * (sy0 - _rift.Y)));
                var start = Drain.A + (Rnd(i, 32) * 0.7) + (dist / 700 * 0.3);
                var age = (t - start) / 0.75;
                if (age < 0 || age > 1) continue;
                var spin = Rnd(i, 33) > 0.5 ? 1 : -1;
                for (var j = 0; j < 3; j++)
                {
                    var u = Ease(Math.Max(0, age - (j * 0.04)));
                    var ang = Math.Atan2(sy0 - _rift.Y, sx0 - _rift.X) + (spin * u * 2.2);
                    var r = dist * (1 - u);
                    Put(p, _rift.X + (Math.Cos(ang) * r), _rift.Y + (Math.Sin(ang) * r), j == 0 ? CapSheet.Mix(speckColours[i], 0xFFFFFFu, 0.2) : speckColours[i], j == 0 ? 255 : 160 / j);
                }
            }
        }

        // the slash: a white beam across the window onto it, splitting into the seven colours
        var sl = K(Slash, t);
        if (sl > 0 && sl < 1)
        {
            double fx = l.Main.Right - 2, fy = 4;
            var head = Math.Min(1, sl * 2.5);
            Line(fx, fy, fx + ((S.X - fx) * head), fy + ((S.Y - fy) * head), (x, y) =>
            {
                for (var d = -1; d <= 1; d++) p.Set(x, y + d, 0xFFFFFFu, (int)(255 * (1 - (sl * 0.6))));
            });
            if (sl > 0.4)
            {
                for (var i = 0; i < 7; i++)
                {
                    var off = (i - 3) * (sl - 0.4) * 6;
                    var colour = Rainbow[i];
                    Line(fx + off, fy, S.X + off, S.Y, (x, y) => p.Set(x, y, colour, (int)(200 * (1 - sl))));
                }
            }
        }

        // the prism beam from the eye onto it while it is taken
        var tk = K(Take, t);
        if (tk > 0 && tk < 1)
        {
            var fade = tk < 0.85 ? 1 : (1 - tk) / 0.15;
            Line(EyeAt.X, EyeAt.Y, S.X, S.Y, (x, y) =>
            {
                for (var d = -3; d <= 3; d++) p.Set(x + d, y, d == 0 ? 0xFFFFFFu : Rainbow[(d + 3) % 7], (int)((d == 0 ? 255 : 170) * fade));
            });
            Glow(p, S.X, S.Y, 20, 16, 0xFFFFFFu, 90 * fade, l.Cols);
        }

        Shared(p, l, t, _rift, false);

        // the end: prismatic motes drift down and fade
        var en = K(End, t);
        if (en > 0 && en < 1)
        {
            for (var i = 0; i < 40; i++) Put(p, _rift.X + ((Rnd(i, 41) - 0.5) * 120) + (Math.Sin((en * 6) + i) * 3), _rift.Y + ((Rnd(41, i) - 0.5) * 60) + (en * 40), Rainbow[i % 7], 230 * (1 - en));
        }

        // the camera: a slow push in on the party's window while it is there, and back
        var zoom = 1 + (0.07 * Ease(Span(Tear.A, Come.B, t)) * (1 - Ease(Span(Back.A, End.B, t))));
        if (zoom > 1.001)
        {
            var copy = (byte[])p.B.Clone();
            for (var y = 0; y < p.H; y++)
            {
                for (var x = 0; x < p.W; x++)
                {
                    if (y < p.NotchY1 && x >= p.NotchX0 && x < p.NotchX1) continue;
                    var sx = Math.Clamp(R(_rift.X + ((x - _rift.X) / zoom)), 0, p.W - 1);
                    var sy = Math.Clamp(R(_rift.Y + ((y - _rift.Y) / zoom)), 0, p.H - 1);
                    if (sy < p.NotchY1 && sx >= p.NotchX0 && sx < p.NotchX1) continue;
                    Buffer.BlockCopy(copy, ((sy * p.W) + sx) * 4, p.B, ((y * p.W) + x) * 4, 4);
                }
            }
        }

        Bars_(p, t);
        foreach (var (at, strength) in new[] { (Slash.A + 0.15, 2.0), (Come.B - 0.1, 4.0), (BurstT.B, 6.0) })
        {
            if (t - at >= 0 && t - at < 0.35) { Shake(p, t - at, strength, 0.35); break; }
        }
    }

    /// <summary>
    /// What both windows share: the burst's rays and white, the colour coming back in rainbow rings, the rift's last glint. In the deep the rift
    /// is beyond its right edge, past the game.
    /// </summary>
    private static void Shared(CapSheet p, CapWorldLayout l, double t, (int X, int Y) rift, bool deep)
    {
        var bu = K(BurstT, t);
        if (bu > 0 && bu < 1)
        {
            for (var r = 0; r < 10; r++)
            {
                var a = (r / 10.0 * 2 * Math.PI) + (bu * 1.4);
                for (var d = 6; d < 640; d++)
                {
                    double x = rift.X + (Math.Cos(a) * d), y = rift.Y + (Math.Sin(a) * d * 0.9);
                    if (x < -2 || y < -2 || x > p.W + 2 || y > p.H + 2) continue;
                    for (var w = -2; w <= 2; w++) Put(p, x, y + w, w == 0 ? 0xFFFFFFu : Rainbow[(r + w + 7) % 7], 200 * bu * (1 - (d / 640.0)));
                }
            }

            if (bu > 0.7) Each(p, (x, y) => p.Set(x, y, 0xFFFFFFu, (int)(255 * (bu - 0.7) / 0.3)));
        }

        var back = K(Back, t);
        if (back > 0)
        {
            if (back < 0.35) Each(p, (x, y) => p.Set(x, y, 0xFFFFFFu, (int)(255 * (1 - (back / 0.35)))));
            for (var i = 0; i < 7; i++)
            {
                var r = (back * 760) - (i * 6);
                if (r < 0) continue;
                for (var a = 0; a < 900; a++)
                {
                    var ang = a / 900.0 * 2 * Math.PI;
                    double x = rift.X + (Math.Cos(ang) * r), y = rift.Y + (Math.Sin(ang) * r * 0.9);
                    if (x >= 0 && y >= 0 && x < p.W && y < p.H) Put(p, x, y, Rainbow[i], 210 * (1 - back));
                }
            }

            var g = (back - 0.6) / 0.4;
            if (!deep && g > 0 && g < 1)
            {
                var s = Math.Sin(Math.PI * g) * 9;
                for (var d = -R(s); d <= s; d++)
                {
                    Put(p, rift.X + d, rift.Y, 0xFFFFFFu, 255 * (1 - (Math.Abs(d) / (s + 1))));
                    Put(p, rift.X, rift.Y + d, 0xFFFFFFu, 255 * (1 - (Math.Abs(d) / (s + 1))));
                }
            }
        }
    }

    /// <summary>The letterbox: black bars at the top and the bottom of the window.</summary>
    private static void Bars_(CapSheet p, double t)
    {
        var bars = R(14 * Ease(K(Bars, t)) * (1 - Ease(K(End, t))));
        if (bars <= 0) return;
        for (var y = 0; y < p.H; y++)
        {
            if (y >= bars && y < p.H - bars) continue;
            for (var x = 0; x < p.W; x++) p.Set(x, y, 0x020204u);
        }
    }

    /// <summary>The deep's share: the dim, the grey wave arriving from the side of the rift, specks flying out towards it, the burst, the rings, the bars.</summary>
    public static void Deep(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double t)
    {
        var rift = (X: ctx.WorldWidth > 0 ? ctx.WorldWidth - l.Cols + 70 : l.Cols + 220, Y: 100);
        var back = K(Back, t);
        var dim = Math.Min(K(Tear, t) * 1.5, 1) * (1 - back);
        if (dim > 0) Each(p, (x, y) => p.Set(x, y, 0x06040Eu, (int)(90 * dim)));
        var gr = K(Drain, t) * 700;
        if (gr > 0 && back <= 0)
        {
            Each(p, (x, y) =>
            {
                var d = Math.Sqrt(((x - rift.X) * (x - rift.X)) + ((y - rift.Y) * (y - rift.Y)));
                if (d <= gr && p.Solid(x, y)) p.Set(x, y, Grey(p.At(x, y)), (int)(255 * Clamp01((gr - d) / 30)));
            });

            // specks of colour torn out and flying off towards the rift, out by the window's side
            for (var i = 0; i < 90; i++)
            {
                var start = Drain.A + 0.2 + (Rnd(i, 61) * 0.6);
                var age = (t - start) / 0.6;
                if (age < 0 || age > 1) continue;
                double x0 = Rnd(i, 62) * l.Cols, y0 = Rnd(62, i) * l.Rows;
                var u = Ease(age);
                double x = x0 + ((rift.X - x0) * u), y = y0 + ((rift.Y - y0) * u);
                Put(p, x, y, Rainbow[i % 7], 230 * (1 - (age * 0.3)));
            }
        }

        Shared(p, l, t, rift, true);
        Bars_(p, t);
        if (t - BurstT.B >= 0 && t - BurstT.B < 0.35) Shake(p, t - BurstT.B, 6, 0.35);
    }
}
