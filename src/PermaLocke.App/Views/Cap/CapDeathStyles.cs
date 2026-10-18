using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The way of one death (2026-10-15): the colours it gives the fallen one, how its pieces move, what they dissolve into, and what it does
/// round them. The shared breaking is <see cref="CapDeathScene.Engine"/>; a style gives it its own and adds what is only its own.
/// </summary>
internal abstract class DeathStyle(CapDeathScene s)
{
    protected readonly CapDeathScene S = s;
    protected DeathModel M = new();

    public virtual uint Glow => 0xA060FFu;
    public virtual double EndAt => 2.6;
    public virtual uint CrackRim => 0x0C0E16u;
    public virtual double InfoFrom => 1.0;
    public virtual double DissolveFrom => 0.5;
    public virtual double DissolveSpread => 0.4;
    public virtual DeathPiece? HoldOn => null;

    public virtual uint Body(DeathCell q, double t) => q.C;
    public virtual uint Crack(double age, DeathCell q, double t) => 0xFFFFFFu;
    public virtual uint Hole(double t, DeathCell q) => 0xFFFFFFu;
    public abstract (double X, double Y, bool Flip) Motion(DeathPiece p, double s);
    public virtual uint Piece(DeathCell q, double s, double t, DeathPiece p) => q.C;
    public abstract void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece);
    public virtual uint? Info(DeathCell q, double t) => null;
    public virtual void FromCrack(CapSheet p, int x, int y, double age, DeathCell q, double t) { }
    public virtual void AfterPiece(CapSheet p, DeathPiece piece, double s, double t) { }
    public virtual double WormholeSize(double t) => 1;

    /// <summary>A frame of the death: its own things under the fallen one, the shared breaking, its own things over it, and the shake of the hit.</summary>
    public virtual void Frame(CapSheet p, double t, double phase)
    {
        Under(p, t);
        S.Engine(p, M, this, t);
        Extra(p, t);
        Shake(p, t, 3, 0.3);
    }

    protected virtual void Under(CapSheet p, double t) { }

    protected virtual void Extra(CapSheet p, double t) { }

    /// <summary>The fallen one as it was, picture and writing, for the deaths that keep it a moment before they start.</summary>
    protected void Whole(CapSheet p)
    {
        foreach (var q in S.Body) p.Set(q.X, q.Y, q.C);
        foreach (var q in S.Info) p.Set(q.X, q.Y, q.C);
    }

    /// <summary>Light that goes on into the wormhole's core.</summary>
    protected void Sucked(CapSheet p, DeathCell q, double x, double y, double age)
    {
        if (age > 0.45 || q.H > 0.6) return;
        var k = age / 0.45;
        Put(p, x + ((S.Core.X - x) * k), y + ((S.Core.Y - y) * k), q.H < 0.3 ? 0x9CF2FFu : 0xE0C8FFu, 240);
    }

    public static DeathStyle For(CapDeathKind kind, CapDeathScene s) => kind switch
    {
        CapDeathKind.Wormhole => new WormholeDeath(s),
        CapDeathKind.Tera => new TeraDeath(s),
        CapDeathKind.Stone => new StoneDeath(s),
        CapDeathKind.Sky => new SkyDeath(s),
        CapDeathKind.Corruption => new CorruptionDeath(s),
        CapDeathKind.Storm => new StormDeath(s),
        CapDeathKind.Bolts => new BoltsDeath(s),
        _ => new NecrozmaDeath(s)
    };
}

// ---------------------------------------------------------------- the Ultra Wormhole takes it

/// <summary>
/// It cracks like the wormhole's glass and its shards are sucked up into the core one by one, turning to light on the way; the piece with its
/// face holds on, shaking and stretching, before it snaps up last. The wormhole flashes violet, cyan and white, cracks a little more, and spits
/// a few sparks back that drift down and go out.
/// </summary>
internal sealed class WormholeDeath : DeathStyle
{
    private readonly DeathPiece? _face;

    public WormholeDeath(CapDeathScene s) : base(s)
    {
        M = S.Model(S.Shards(7), p => -Math.Sqrt((p.Cx * p.Cx) + (p.Cy * p.Cy)) + (p.Face ? 100 : 0) + Rnd(p.Id, 2), 0.95, 1.75, (0.1, -0.2));
        _face = M.Pieces.FirstOrDefault(p => p.Face);
        if (_face is not null) _face.Leave = 2.05;
    }

    public override double EndAt => 2.75;
    public override uint CrackRim => 0x5A2CA8u;
    public override double DissolveFrom => 0.45;
    public override double DissolveSpread => 0.4;
    public override DeathPiece? HoldOn => _face;

    private (double X, double Y) ToCore(DeathPiece p, double s)
    {
        var u = Math.Pow(Clamp01(s / 0.85), 1.7);
        double cx = S.Core.X - S.X, cy = S.Core.Y - S.Y, bx = p.Cx - 26 + (Rnd(p.Id, 4) * 12), by = ((p.Cy + cy) / 2) + 18;
        return (((1 - u) * (1 - u) * p.Cx) + (2 * (1 - u) * u * bx) + (u * u * cx), ((1 - u) * (1 - u) * p.Cy) + (2 * (1 - u) * u * by) + (u * u * cy));
    }

    public override uint Body(DeathCell q, double t) => CapSheet.Mix(q.C, S.Greyed(q.C), Ease(t / 0.5) * 0.6);

    public override uint Crack(double age, DeathCell q, double t) => CapSheet.Mix(0xFFFFFFu, 0x9CF2FFu, Clamp01((age - 0.3) / 0.5));

    public override uint Hole(double t, DeathCell q) => CapSheet.Mix(0xE8F8FFu, 0x9C70FFu, 0.5 + (0.5 * Math.Sin(t * 6)));

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s)
    {
        if (p == _face && s < 0.25) return (p.Cx, p.Cy - (s / 0.25 * 3) + (Math.Sin(s * 70) * 0.8), false);   // it holds on, then snaps
        var (x, y) = ToCore(p, p == _face ? s - 0.25 : s);
        return (x, y, false);
    }

    public override void AfterPiece(CapSheet p, DeathPiece piece, double s, double t)
    {
        if (piece == _face && s < 0.25)
        {
            for (var i = 1; i <= 6; i++) Put(p, S.X + piece.Cx + ((Rnd(i, 3) - 0.5) * 6), S.Y + piece.Cy + 4 + i, 0xB890F0u, 160 - (i * 20));   // the stretch
        }

        var ss = piece == _face ? s - 0.25 : s;
        if (ss <= 0) return;
        for (var q = 1; q <= 8; q++)
        {
            var (x, y) = ToCore(piece, Math.Max(0, ss - (q * 0.03)));
            Put(p, S.X + x + (Math.Sin((q * 1.7) + piece.Id) * 1.5), S.Y + y, q % 2 == 1 ? 0x7CF0E8u : 0xB06CFFu, 200 - (q * 22));
        }
    }

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p) =>
        CapSheet.Mix(CapSheet.Mix(q.C, S.Greyed(q.C), 0.6), 0xD8F4FFu, Clamp01((s - 0.3) / 0.5) * 0.7);

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (age > 0.5 || q.H > 0.6) return;
        var k = age / 0.5;
        Put(p, x + ((S.Core.X - x) * k), y + ((S.Core.Y - y) * k), q.H < 0.3 ? 0x9CF2FFu : 0xE0C8FFu, 240 * (1 - (k * 0.5)));
    }

    protected override void Extra(CapSheet p, double t)
    {
        // the core draws breath while it eats, then flashes violet, cyan and white, cracks more and spits sparks back
        var (cx, cy) = S.Core;
        var eating = Clamp01((t - 1.0) / 1.8);
        // sized by the core of the wormhole as drawn since the official art (2026-10-18), so the breath shows round it and not inside it
        const double rx = CapFarSky.CoreRx, ry = CapFarSky.CoreRy;
        Glow(p, cx, cy, rx + 2 + (eating * 4), ry + 2 + (eating * 5), 0xFFFFFFu, 120 * eating);
        var k = (t - 2.75) / 0.6;
        if (k > 0 && k < 1)
        {
            var c = k < 0.33 ? 0xB070FFu : k < 0.66 ? 0x7CF0FFu : 0xFFFFFFu;
            Glow(p, cx, cy, rx + 3 + (k * 22), ry + 1 + (k * 20), c, 255 * (1 - k));
            for (var a = 0; a < 70; a++)
            {
                var ang = a / 70.0 * 2 * Math.PI;
                var r = rx + 2 + (k * 34);
                Put(p, cx + (Math.Cos(ang) * r), cy + (Math.Sin(ang) * r * 0.85), CapSheet.Mix(0xE0C8FFu, 0xFFFFFFu, k), 220 * (1 - k));
            }
        }

        // The rays grow and then go (2026-10-15): they stayed whole until the death ended and were cut in one frame.
        var grow = Ease((t - 2.75) / 0.4);
        var rays = 235 * (1 - Ease((t - 3.15) / 0.4));
        if (grow > 0 && rays > 0)
        {
            for (var i = 0; i < 4; i++)
            {
                var a = (i * 1.57) + 0.6;
                var len = (24 + (i * 5)) * grow;
                Line(cx + (Math.Cos(a) * rx), cy + (Math.Sin(a) * ry), cx + (Math.Cos(a) * (rx + len)), cy + (Math.Sin(a) * (ry + (len * 0.8))), (x, y) =>
                {
                    if (y < S.Layout.SurfaceY) p.Set(x, y, 0xFFFFFFu, (int)rays);
                });
            }
        }

        if (t > 2.85)
        {
            for (var i = 0; i < 6; i++)
            {
                var age = t - 2.85 - (i * 0.04);
                if (age < 0 || age > 0.9) continue;
                var a = -0.3 + (i * 0.4) + (Math.PI * 0.5);
                Put(p, cx + (Math.Cos(a) * (rx + 2 + (age * 30))), cy + (Math.Sin(a) * (ry + (age * 14))) + (age * age * 30), i % 2 == 1 ? 0x9CF2FFu : 0xFFE0FFu, 255 * (1 - (age / 0.9)));
            }
        }
    }
}

// ---------------------------------------------------------------- Terastallized

/// <summary>
/// It turns to glass from where it was hit, its facets split along gold seams, and the shards tumble down glinting. As the last piece goes a
/// prism ray fans out in rainbow colours; the shards turn into motes of every hue that rise, and a last glow fades.
/// </summary>
internal sealed class TeraDeath : DeathStyle
{
    private static readonly uint[] Lit = [0x8CF0FF, 0x9AB8FF, 0xB08CFF];
    private static readonly uint[] Dark = [0x38A8E0, 0x4C68D8, 0x6A40C8];
    private static readonly uint[] Hues = [0xFFC8F4, 0xC8FFFF, 0xFFF0C0, 0xE0C8FF];

    public TeraDeath(CapDeathScene s) : base(s) => M = S.Model(S.Grid(6, 13), p => (p.Cy * 0.5) + (Rnd(p.Id, 6) * 6), 1.0, 1.85, (-0.25, 0.25));

    public override uint Glow => 0x78A8FFu;
    public override uint CrackRim => 0xA87818u;
    public override double DissolveFrom => 0.4;
    public override double DissolveSpread => 0.45;

    private static uint Ramp(uint[] stops, double k)
    {
        var s = Math.Min(1.999, Math.Max(0, k) * 2);
        var i = (int)Math.Floor(s);
        return CapSheet.Mix(stops[i], stops[i + 1], s - i);
    }

    private uint Glass(DeathCell q)
    {
        var down = (q.Dy - S.Top) / (double)Math.Max(1, S.Bottom - S.Top);
        return CapSheet.Mix(q.Dx < 0 ? Ramp(Lit, down) : Ramp(Dark, down), q.C, 0.35);
    }

    public override uint Body(DeathCell q, double t)
    {
        var d = Math.Sqrt(((q.Dx - M.Hx) * (q.Dx - M.Hx)) + ((q.Dy - M.Hy) * (q.Dy - M.Hy)));
        return d < Clamp01((t - 0.35) / 0.6) * 46 ? Glass(q) : CapSheet.Mix(q.C, 0x9AB8FFu, Ease(t / 0.5) * 0.3);   // the glass spreads from the hit
    }

    public override uint Crack(double age, DeathCell q, double t) => CapSheet.Mix(0xF0C030u, 0xFFFFFFu, Clamp01(age / 0.8));   // the gold seams, heating

    public override uint Hole(double t, DeathCell q) => Hues[(int)Math.Floor((t * 6) + (q.Dx * 0.2)) & 3];

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s) =>
        (p.Cx + ((p.Cx > 0 ? 1 : -1) * 7 * s), p.Cy + (4 * s) + (26 * s * s), (int)Math.Floor((s * 7) + p.Id) % 2 == 1);   // tumbling down

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p)
    {
        var g = Glass(q);
        return (int)Math.Floor((s * 7) + p.Id) % 2 == 1 && q.Dx < p.Cx ? CapSheet.Mix(g, 0xFFFFFFu, 0.45) : g;   // the glint as it turns
    }

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (age > 1.1 || q.H > 0.65) return;
        Put(p, x + (Math.Sin((age * 7) + (q.H * 30)) * (1 + (age * 2))), Math.Max(S.Layout.SurfaceY + 1, y - (age * (16 + (q.H * 18)))), Hues[(int)Math.Floor(q.H * 40) & 3], 240 * (1 - (age / 1.1)));
    }

    protected override void Extra(CapSheet p, double t)
    {
        // the prism: as the last piece goes, a ray fans out in rainbow bands and sweeps
        var k = (t - 1.85) / 0.6;
        if (k > 0 && k < 1)
        {
            uint[] bands = [0xFF5050, 0xFFA040, 0xFFF060, 0x60F080, 0x60C0FF, 0xA070FF];
            var sweep = -0.9 + (k * 0.6);
            for (var i = 0; i < bands.Length; i++)
            {
                var a = sweep + (i * 0.05);
                for (var r = 3; r < 40; r++)
                {
                    double x = S.X + (Math.Cos(a) * r), y = S.Y + (Math.Sin(a) * r * 0.6);
                    if (x < S.TextLeft) Put(p, x, y, bands[i], 200 * Math.Sin(Math.PI * k) * (1 - (r / 40.0)));
                }
            }

            Glow(p, S.X, S.Y, 6, 6, 0xFFFFFFu, 230 * Math.Sin(Math.PI * k));
        }

        var e = (t - 2.6) / 0.6;
        if (e > 0 && e < 1) Glow(p, S.X, S.Y, 10 + (e * 10), 9 + (e * 8), CapSheet.Mix(0x8CF0FFu, 0xB08CFFu, e), 200 * (1 - e), S.TextLeft);
    }
}

// ---------------------------------------------------------------- Turned to stone

/// <summary>
/// Stone climbs it from the feet, its cracks puff dust as they open, and the rocks drop, bounce on the line of its row and crumble into sand
/// and bubbles. The last rock, its head, splits open on a glowing ember that flickers out.
/// </summary>
internal sealed class StoneDeath : DeathStyle
{
    private readonly DeathPiece? _head;

    public StoneDeath(CapDeathScene s) : base(s)
    {
        M = S.Model(S.Grid(5, 23), p => -p.Cy + (Rnd(p.Id, 7) * 4) + (p.Face ? 100 : 0), 1.05, 1.8, (0, 0.3), 0.6, 1.25);
        _head = M.Pieces.FirstOrDefault(p => p.Face);
        if (_head is not null) _head.Leave = 1.95;
    }

    public override uint Glow => 0xD8B070u;
    public override double DissolveFrom => 0.55;
    public override double DissolveSpread => 0.45;

    private uint Stone(uint c) => CapSheet.Mix(0x3A342Cu, 0xD8CCB0u, Math.Pow(Clamp01(S.Tone(c)), 0.85));

    private double Line2(double t) => S.Bottom - ((S.Bottom - S.Top + 2) * Ease((t - 0.3) / 0.55));   // the stone climbs from the feet

    /// <summary>A rock falls, bounces once on the line under its row, and falls on.</summary>
    private (double X, double Y) Fall(DeathPiece p, double s)
    {
        double y0 = p.Cy, v0 = -2, g = 70, floor = S.RowLine - S.Y - 1;
        var hit = (-v0 + Math.Sqrt((v0 * v0) + (2 * g * Math.Max(0, floor - y0)))) / g;
        if (s < hit) return (p.Cx + (p.Cx * 0.3 * s), y0 + (v0 * s) + (0.5 * g * s * s));
        var sb = s - hit;
        var vb = -(v0 + (g * hit)) * 0.38;
        return (p.Cx + (p.Cx * 0.3 * s) + ((Rnd(p.Id, 8) - 0.5) * 10 * sb), floor + (vb * sb) + (0.5 * g * sb * sb));
    }

    public override uint Body(DeathCell q, double t)
    {
        var line = Line2(t);
        return q.Dy > line ? Stone(q.C) : Math.Abs(q.Dy - line) < 1 ? 0xF0E8D0u : CapSheet.Mix(q.C, Stone(q.C), Ease(t / 0.5) * 0.3);
    }

    public override uint Crack(double age, DeathCell q, double t) => CapSheet.Mix(0x221C14u, 0x0E0A06u, Clamp01(age / 0.5));

    public override void FromCrack(CapSheet p, int x, int y, double age, DeathCell q, double t)
    {
        if (age < 0.45 && q.H < 0.12) Puff(p, x + (Math.Sin(age * 9) * 1.5), y - (age * 10), 1 + (age * 3), 0xC8B890u, 140 * (1 - (age / 0.45)));   // dust from the cracks
    }

    public override uint Hole(double t, DeathCell q) => CapSheet.Mix(0xFF9A40u, 0xFFE0A0u, 0.5 + (0.5 * Math.Sin(t * 9)));

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s)
    {
        if (p == _head && s < 0.45) return (p.Cx, p.Cy + (Math.Sin(s * 40) * 0.6), false);
        var (x, y) = Fall(p, p == _head ? s - 0.45 : s);
        return (x, y, false);
    }

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p)
    {
        // the head splits open on an ember before it falls
        if (p == _head && s < 0.45 && Math.Abs(q.Dx - p.Cx) < 1 + (s / 0.45 * 2)) return CapSheet.Mix(0xFF8020u, 0xFFE070u, 0.5 + (0.5 * Math.Sin(s * 30)));
        return Stone(q.C);
    }

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (age > 1.1 || q.H > 0.7) return;
        if (q.H < 0.18) Bubble(p, x + (Math.Sin((age * 6) + (q.H * 20)) * 2), Math.Max(S.Layout.SurfaceY + 1, y - (age * 26)), 0xD8F0FFu, 210 * (1 - (age / 1.1)));
        else Puff(p, x + (Math.Sin((age * 5) + (q.H * 30)) * 1.5), y + (age * 4) - (age * age * 6), 0.8 + (age * 1.6), 0xC8B890u, 170 * (1 - (age / 1.1)));
    }

    protected override void Extra(CapSheet p, double t)
    {
        // the ember of the head flickers out as the head falls
        if (_head is not null)
        {
            var s = t - _head.Leave - 0.45;
            if (s > 0 && s < 0.6)
            {
                var (mx, my) = Fall(_head, s);
                for (var i = 0; i < 3; i++) Put(p, S.X + mx + ((Rnd(i, 9) - 0.5) * 3), S.Y + my - 1 - (s * 8 * i), 0xFF9A40u, (Math.Floor(s * 20) % 2 == 1 ? 230 : 90) * (1 - (s / 0.6)));
            }
        }

        var e = (t - 2.6) / 0.6;
        if (e > 0 && e < 1) Glow(p, S.X, S.RowLine - 4, 14 + (e * 8), 6 + (e * 4), 0xFFD8A0u, 170 * (1 - e), S.TextLeft);
    }
}

// ---------------------------------------------------------------- Up to the sky

/// <summary>
/// It cracks with gold light leaking out in short rays, its pieces float up and turn gold and come apart into motes that gather into its soul.
/// The soul turns back to look at the team once, then darts up out of the party's window; the deep's window takes it on to the top of the Sky
/// Pillar, which flashes gold, then white (<see cref="CapDeathSky"/>).
/// </summary>
internal sealed class SkyDeath : DeathStyle
{
    public const double SoulFrom = 2.15;
    private readonly (double X, double Y) _gather;

    public SkyDeath(CapDeathScene s) : base(s)
    {
        M = S.Model(S.Grid(6, 31), p => -p.Cy + (Rnd(p.Id, 10) * 5), 0.9, 1.6, (0, -0.1), 0.5, 1.05);
        _gather = (S.X, S.Y - 22);
    }

    public override uint Glow => 0xFFD060u;
    public override double EndAt => 2.2;
    public override uint CrackRim => 0xC08020u;
    public override double DissolveFrom => 0.35;
    public override double DissolveSpread => 0.35;

    private uint Gold(uint c, double k) => CapSheet.Mix(c, CapSheet.Mix(0xF0A830u, 0xFFF8D0u, Clamp01(S.Tone(c))), k);

    /// <summary>The soul: gathers over the row, turns back to look at the team once, and darts up out of the window by its side towards the deep.</summary>
    private (double X, double Y)? SoulAt(double t)
    {
        if (t < SoulFrom + 0.25) return _gather;
        if (t < SoulFrom + 0.55)
        {
            var a = (t - SoulFrom - 0.25) / 0.3 * 2 * Math.PI;
            return (_gather.X + (Math.Sin(a) * 6), _gather.Y + ((1 - Math.Cos(a)) * 3));   // the look back
        }

        if (t < SoulFrom + 0.85)
        {
            var u = Ease((t - SoulFrom - 0.55) / 0.3);
            var edge = S.Layout.Main.Left - 2;
            return (_gather.X + ((edge - _gather.X) * u), _gather.Y + ((18 - _gather.Y) * u) - (Math.Sin(Math.PI * u) * 10));
        }

        return null;
    }

    public override uint Body(DeathCell q, double t) => Gold(CapSheet.Mix(q.C, S.Greyed(q.C), Ease(t / 0.5) * 0.4), 0.1);

    public override uint Crack(double age, DeathCell q, double t) => CapSheet.Mix(0xFFE070u, 0xFFFFFFu, Clamp01(age / 0.6));

    public override void FromCrack(CapSheet p, int x, int y, double age, DeathCell q, double t)
    {
        if (q.H >= 0.1 || age <= 0.15) return;
        var a = q.H * 60;
        var r = 2 + (Clamp01(age - 0.15) * 4);
        for (var j = 1; j <= r; j++) Put(p, x + (Math.Cos(a) * j), y + (Math.Sin(a) * j), 0xFFF0A0u, 150 * (1 - (j / (r + 1))));   // short rays of light
    }

    public override uint Hole(double t, DeathCell q) => 0xFFF4C0u;

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s) =>
        (p.Cx + (Math.Sin((s * 3) + p.Id) * 2) + (p.Cx * 0.25 * s), p.Cy - (8 * s) - (6 * s * s), false);   // floating up

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p) => Gold(q.C, 0.45 + (Clamp01(s / 0.4) * 0.5));

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (q.H > 0.55) return;
        var k = Clamp01(age / 0.55);
        var tx = _gather.X + (Math.Sin(q.H * 40) * 2);
        Put(p, x + ((tx - x) * Ease(k)) + (Math.Sin((age * 8) + (q.H * 30)) * (1 - k) * 2), y + ((_gather.Y - y) * Ease(k)), q.H < 0.25 ? 0xFFF0A0u : 0xFFC040u, 230);
    }

    private static void Orb(CapSheet p, (double X, double Y) at, double a)
    {
        Glow(p, at.X, at.Y, 4.5, 4.5, 0xFFE890u, a * 0.5);
        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) Put(p, at.X + dx, at.Y + dy, 0xFFFFFFu, a);
    }

    protected override void Extra(CapSheet p, double t)
    {
        if (t > SoulFrom - 0.4 && t < SoulFrom + 0.25) Orb(p, _gather, 255 * Clamp01((t - SoulFrom + 0.4) / 0.4));
        if (SoulAt(t) is { } now && t >= SoulFrom + 0.25)
        {
            for (var q = 1; q <= 8; q++)
            {
                if (SoulAt(t - (q * 0.025)) is { } back) Put(p, back.X, back.Y, 0xFFE890u, 180 - (q * 20));
            }

            Orb(p, now, 255);
            if (Math.Abs(now.Y - S.Layout.SurfaceY) < 2)
            {
                for (var d = -5; d <= 5; d++) Put(p, now.X + d, S.Layout.SurfaceY, 0xE8FFFFu, 200 * (1 - (Math.Abs(d) / 6.0)));
            }
        }
    }
}

/// <summary>The deep's share of the death that goes up to the sky: the soul comes in by its side and the top of the Sky Pillar takes it.</summary>
internal static class CapDeathSky
{
    public static void Deep(CapSheet p, CapWorldLayout l, double t)
    {
        if (CapFarSky.PillarTopAt(l) is not { } top) return;
        var s0 = SkyDeath.SoulFrom;
        if (t > s0 + 0.95 && t < s0 + 1.25)
        {
            (double X, double Y) At(double tt)
            {
                var u = Ease((tt - s0 - 0.95) / 0.3);
                double fromX = l.Main.Right + 2, fromY = 18;
                return (fromX + ((top.X - fromX) * u), fromY + ((top.Y - fromY) * u) - (Math.Sin(Math.PI * u) * 6));
            }

            for (var q = 1; q <= 8; q++) { var b = At(t - (q * 0.025)); Put(p, b.X, b.Y, 0xFFE890u, 180 - (q * 20)); }
            var now = At(t);
            Glow(p, now.X, now.Y, 4.5, 4.5, 0xFFE890u, 128);
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) Put(p, now.X + dx, now.Y + dy, 0xFFFFFFu, 255);
        }

        // the tower takes it: gold, then white, a ring going out and rays
        var k = (t - s0 - 1.25) / 0.7;
        if (k <= 0 || k >= 1) return;
        var c = CapSheet.Mix(0xFFD040u, 0xFFFFFFu, k);
        Glow(p, top.X, top.Y, 18, 14, c, 220 * (1 - k));
        var r = 4 + (k * 26);
        for (var a = 0; a < 64; a++)
        {
            var ang = a / 64.0 * 2 * Math.PI;
            Put(p, top.X + (Math.Cos(ang) * r), top.Y + (Math.Sin(ang) * r * 0.6), 0xFFF4C0u, 220 * (1 - k));
        }

        for (var i = 0; i < 8; i++)
        {
            var ang = (i / 8.0 * 2 * Math.PI) + 0.2;
            for (var j = 4; j < 4 + (14 * (1 - k)); j++) Put(p, top.X + (Math.Cos(ang) * j), top.Y + (Math.Sin(ang) * j * 0.6), c, 170 * (1 - k));
        }
    }
}

// ---------------------------------------------------------------- Corruption (MissingNo.)

/// <summary>
/// It corrupts into glitch blocks that copy themselves over the whole window; the rows tear sideways in strips, the writing turns to garbage,
/// the colours swap. All at once everything snaps back, but its row is empty, with a few corrupt cells that blink before they go.
/// </summary>
internal sealed class CorruptionDeath : DeathStyle
{
    private static readonly uint[] Glitch = [0xFF30C0, 0x30FF90, 0xFFFFFF, 0x101018, 0xF0E040, 0x40A0FF];
    private const double Snap = 2.55;
    private readonly Dictionary<int, uint> _picture = [];

    public CorruptionDeath(CapDeathScene s) : base(s)
    {
        M = S.Model(S.Grid(5, 61), p => Rnd(p.Id, 18), 0.8, 1.9, (0.2, -0.1), 0.45, 0.9);
        foreach (var q in S.Body) _picture[(q.X * 1000) + q.Y] = q.C;
    }

    public override uint Glow => 0xFF30C0u;
    public override double EndAt => 2.5;
    public override uint CrackRim => 0x101018u;
    public override double InfoFrom => 0.9;
    public override double DissolveFrom => 0.35;
    public override double DissolveSpread => 0.4;

    private static double Chaos(double t) => t < 0.45 ? 0 : t < Snap ? Ease((t - 0.45) / 1.0) : 0;

    public override uint Body(DeathCell q, double t) => Rnd(q.Dx + (int)Math.Floor(t * 12), q.Dy) < Chaos(t) * 0.3 ? Glitch[(int)(Rnd(q.Dy, (int)Math.Floor(t * 12)) * 6)] : q.C;

    public override uint Crack(double age, DeathCell q, double t) => Glitch[((int)Math.Floor(t * 12) + q.Dx) % 2 != 0 ? 0 : 1];   // scan cracks, magenta and green

    public override uint Hole(double t, DeathCell q) => Glitch[(int)(Rnd(q.Dx, (int)Math.Floor(t * 12)) * 6)];

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s)
    {
        var f = (int)Math.Floor(s * 12);
        var k = Math.Min(1, s * 1.5);
        return (p.Cx + ((Rnd(p.Id, f) - 0.5) * 50 * k), p.Cy + ((Rnd(f, p.Id) - 0.5) * 40 * k), false);   // teleports
    }

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p) => (int)Math.Floor(s * 12) % 3 == 0 ? Swap(q.C) : q.C;

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece)
    {
        if (age > 0.5 || q.H > 0.5) return;
        var f = (int)Math.Floor(age * 12);
        var h = (int)(q.H * 10000);
        Put(p, x + ((Rnd(h, f) - 0.5) * 16), y + ((Rnd(f, h) - 0.5) * 10), Glitch[f % 6], 255);
    }

    public override uint? Info(DeathCell q, double t) => Rnd(q.X + (int)Math.Floor(t * 12), q.Y) < Chaos(t) * 0.5 ? Glitch[(int)(Rnd(q.Y, q.X) * 6)] : q.C;

    public override void Frame(CapSheet p, double t, double phase)
    {
        var f = (int)Math.Floor(t * 12);
        if (t < Snap) S.Engine(p, M, this, t);
        var k = Chaos(t);
        if (k > 0)
        {
            // copies of its tiles over the whole window
            for (var n = 0; n < R(k * 26); n++)
            {
                var sx = S.X + S.Left + (int)(Rnd(n, f) * Math.Max(1, S.Right - S.Left - 4));
                var sy = S.Y + S.Top + (int)(Rnd(f, n) * Math.Max(1, S.Bottom - S.Top - 4));
                var tx = (int)(Rnd(n * 3, f + 7) * Math.Max(1, p.W - 4));
                var ty = (int)(Rnd(n * 5, f + 3) * Math.Max(1, p.H - 4));
                var swap = Rnd(n, 9) < 0.5;
                for (var dy = 0; dy < 4; dy++)
                {
                    for (var dx = 0; dx < 4; dx++)
                    {
                        if (_picture.TryGetValue(((sx + dx) * 1000) + sy + dy, out var c)) p.Set(tx + dx, ty + dy, swap ? Swap(c) : c);
                    }
                }
            }

            // the writing of everyone turns to garbage
            for (var y = 0; y < p.H; y++)
            {
                for (var x = Math.Max(0, S.TextLeft - 6); x < p.W; x++)
                {
                    if (p.Solid(x, y) && Luma(p.At(x, y)) > 150 && Rnd((x * 7) + f, y) < k * 0.45) p.Set(x, y, Glitch[(int)(Rnd(y, x + f) * 6)]);
                }
            }

            // the rows tear sideways in strips
            var copy = (byte[])p.B.Clone();
            var row = 0;
            while (row < p.H)
            {
                var h = 2 + (int)(Rnd(row, f) * 5);
                var shift = Rnd(f, row) < k * 0.55 ? R((Rnd(row + 1, f) - 0.5) * 24 * k) : 0;
                for (var yy = row; yy < Math.Min(p.H, row + h) && shift != 0; yy++)
                {
                    for (var x = 0; x < p.W; x++)
                    {
                        var from = x - shift;
                        if (from < 0 || from >= p.W) continue;
                        if (yy < p.NotchY1 && ((x >= p.NotchX0 && x < p.NotchX1) || (from >= p.NotchX0 && from < p.NotchX1))) continue;
                        Buffer.BlockCopy(copy, ((yy * p.W) + from) * 4, p.B, ((yy * p.W) + x) * 4, 4);
                    }
                }

                row += h;
            }

            // the colours swap for a frame now and then
            if (Rnd(f, 77) < k * 0.35) Each(p, (x, y) => { if (p.Solid(x, y)) p.Set(x, y, Swap(p.At(x, y))); });
        }

        // after the snap: a few corrupt cells blink where it was, and go
        if (t >= Snap && t < Snap + 0.9)
        {
            for (var n = 0; n < 14; n++)
            {
                if (Rnd(n, f) < (t - Snap) / 0.9) continue;
                p.Set(S.X - 20 + (int)(Rnd(n, 31) * 70), S.Y - 12 + (int)(Rnd(31, n) * 26), Glitch[(n + f) % 6]);
            }
        }

        Shake(p, t, 3, 0.3);
    }

    /// <summary>
    /// The deep's share (2026-10-15): the corruption takes the whole overlay, not only the party's window. Blocks of wrong colour, rows torn
    /// sideways and the colours swapped now and then, as strong as the chaos in the party's window at the same moment.
    /// </summary>
    public static void Deep(CapSheet p, double t)
    {
        var k = Chaos(t);
        if (k <= 0) return;
        var f = (int)Math.Floor(t * 12);

        for (var n = 0; n < R(k * 40); n++)
        {
            var tx = (int)(Rnd(n * 3, f + 11) * Math.Max(1, p.W - 6));
            var ty = (int)(Rnd(n * 5, f + 13) * Math.Max(1, p.H - 6));
            var c = Glitch[(int)(Rnd(n, f + 17) * 6)];
            var w = 2 + (int)(Rnd(n, f + 19) * 6);
            var h = 1 + (int)(Rnd(f + 19, n) * 4);
            for (var dy = 0; dy < h; dy++)
            {
                for (var dx = 0; dx < w; dx++)
                {
                    if (p.Solid(tx + dx, ty + dy)) p.Set(tx + dx, ty + dy, c);
                }
            }
        }

        var copy = (byte[])p.B.Clone();
        var row = 0;
        while (row < p.H)
        {
            var h = 2 + (int)(Rnd(row, f + 23) * 5);
            var shift = Rnd(f + 23, row) < k * 0.55 ? R((Rnd(row + 1, f + 23) - 0.5) * 24 * k) : 0;
            for (var yy = row; yy < Math.Min(p.H, row + h) && shift != 0; yy++)
            {
                for (var x = 0; x < p.W; x++)
                {
                    var from = x - shift;
                    if (from < 0 || from >= p.W) continue;
                    if (yy < p.NotchY1 && ((x >= p.NotchX0 && x < p.NotchX1) || (from >= p.NotchX0 && from < p.NotchX1))) continue;
                    Buffer.BlockCopy(copy, ((yy * p.W) + from) * 4, p.B, ((yy * p.W) + x) * 4, 4);
                }
            }

            row += h;
        }

        if (Rnd(f, 79) < k * 0.35) Each(p, (x, y) => { if (p.Solid(x, y)) p.Set(x, y, Swap(p.At(x, y))); });
    }
}

// ---------------------------------------------------------------- Ultra Space bolts

/// <summary>
/// The wormhole charges and lets go violet bolts that bounce round the window: through the rest of the party (each flashes in negative as it
/// is struck) and onto the fallen one, which fills with violet energy, swells, crackles, and bursts in a shower of sparks and pieces that the
/// wormhole swallows back.
/// </summary>
internal sealed class BoltsDeath : DeathStyle
{
    private const double Hit = 1.45;
    private readonly List<List<(double X, double Y)>> _legs = [];
    private readonly List<(int Leg, int Other)> _hits = [];

    public BoltsDeath(CapDeathScene s) : base(s)
    {
        M = S.Model(S.Grid(5, 71), p => Rnd(p.Id, 33), 0.75, 0.85, (0, 0), 0.25, 0.7);

        // the path: from the core, bouncing off the edges of the window and through each of the others, to the fallen one
        var l = S.Layout;
        var stops = new List<(double X, double Y)> { (S.Core.X, S.Core.Y) };
        for (var i = 0; i < S.Others.Count; i++)
        {
            var o = S.Others[i];
            stops.Add((i % 2 == 0 ? l.Main.Right - 6 : l.Main.Left + 4, o.Centre.Y - 14));
            stops.Add(o.Centre);
            _hits.Add((stops.Count - 2, i));
        }

        stops.Add((S.X, S.Y));
        for (var i = 1; i < stops.Count; i++)
        {
            var (ax, ay) = stops[i - 1];
            var (bx, by) = stops[i];
            var n = Math.Max(3, R(Math.Sqrt(((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay))) / 7));
            var pts = new List<(double, double)> { (ax, ay) };
            for (var j = 1; j < n; j++)
            {
                var k = j / (double)n;
                pts.Add((ax + ((bx - ax) * k) + ((Rnd(i, j) - 0.5) * 8), ay + ((by - ay) * k) + ((Rnd(j, i) - 0.5) * 8)));
            }

            pts.Add((bx, by));
            _legs.Add(pts);
        }
    }

    public override uint Glow => 0xB050FFu;
    public override double EndAt => 1.4;
    public override uint CrackRim => 0x3A1060u;
    public override double InfoFrom => 0.75;
    public override double DissolveFrom => 0.5;
    public override double DissolveSpread => 0.35;

    private double LegTime(int i) => 0.45 + (i / (double)Math.Max(1, _legs.Count) * (Hit - 0.45));

    private static (double X, double Y) Out(DeathPiece p, double s)
    {
        var a = Math.Atan2(p.Cy == 0 ? 0.01 : p.Cy, p.Cx == 0 ? 0.01 : p.Cx);
        var v = 34 + (Rnd(p.Id, 34) * 20);
        return (p.Cx + (Math.Cos(a) * v * s), p.Cy + (Math.Sin(a) * v * s * 0.8));
    }

    public override uint Body(DeathCell q, double t) => CapSheet.Mix(q.C, 0xC070FFu, Clamp01((Clamp01(t / 0.7) * 0.55) + (0.15 * Math.Sin(t * 40))));

    public override uint Crack(double age, DeathCell q, double t) => CapSheet.Mix(0xFFE0FFu, 0xFFFFFFu, Clamp01(age / 0.3));

    public override uint Hole(double t, DeathCell q) => 0xF0C0FFu;

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s)
    {
        if (s < 0.35) { var (x, y) = Out(p, s); return (x, y, false); }
        var (x0, y0) = Out(p, 0.35);
        var u = Ease((s - 0.35) / 0.5);
        return (x0 + ((S.Core.X - S.X - x0) * u), y0 + ((S.Core.Y - S.Y - y0) * u), false);
    }

    public override uint Piece(DeathCell q, double s, double t, DeathPiece p) => CapSheet.Mix(q.C, 0xE0A0FFu, 0.6);

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece) => Sucked(p, q, x, y, age);

    /// <summary>
    /// The wormhole as it charges, lets go, swallows the pieces and settles (2026-10-15): every change of size is eased. It used to drop 0.15 in one
    /// frame after the charge and to be cut from 1.6 back to 1 when the death ended; now it charges, eases back, swells as it swallows and goes
    /// slowly back to its size before the end.
    /// </summary>
    public override double WormholeSize(double t)
    {
        // smaller swells since the wormhole is drawn bigger after the official art (2026-10-18), so it stays in the sky
        var charge = 0.22 * (Ease(Clamp01(t / 0.45)) - (0.6 * Ease(Clamp01((t - 0.45) / 0.5))));
        var swallow = Hit + 1.1;
        var swell = 0.28 * (Ease(Clamp01((t - swallow) / 0.45)) - Ease(Clamp01((t - swallow - 0.55) / (CapDeath.Seconds(CapDeathKind.Bolts) - swallow - 0.75))));
        return 1 + charge + swell;
    }

    public override void Frame(CapSheet p, double t, double phase)
    {
        var te = t - Hit;
        if (te < 0) Whole(p);
        else
        {
            // it swells with the charge between the hit and the burst
            if (te < 0.75)
            {
                var sw = 1 + (Clamp01(te / 0.7) * 0.25) + (Math.Sin(te * 60) * 0.03);
                foreach (var q in S.Body) Put(p, S.X + (q.Dx * sw), S.Y + (q.Dy * sw), q.C);
            }

            S.Engine(p, M, this, te);
        }

        // the bolt runs leg by leg, the legs behind it flickering out; each of the others flashes in negative as it is struck
        for (var i = 0; i < _legs.Count; i++)
        {
            double t0 = LegTime(i), t1 = LegTime(i + 1);
            var k = t < t0 ? 0 : t < t1 ? (t - t0) / (t1 - t0) : 1;
            var fade = t > t1 ? 1 - Clamp01((t - t1) / 0.35) : 1;
            if (k <= 0 || fade <= 0) continue;
            var pts = _legs[i];
            var upto = (int)Math.Ceiling(k * (pts.Count - 1));
            var a = fade * ((int)Math.Floor((t * 20) + i) % 3 != 0 ? 1 : 0.5);
            for (var j = 0; j + 1 < pts.Count && j < upto; j++)
            {
                Line(pts[j].X, pts[j].Y, pts[j + 1].X, pts[j + 1].Y, (x, y) =>
                {
                    p.Set(x, y, 0xFFFFFFu, (int)(255 * a));
                    foreach (var (ox, oy) in Round4) p.Set(x + ox, y + oy, 0xC060FFu, (int)(180 * a));
                    p.Set(x + 2, y, 0x7A30C8u, (int)(90 * a));
                    p.Set(x - 2, y, 0x7A30C8u, (int)(90 * a));
                });
            }
        }

        foreach (var (leg, other) in _hits)
        {
            var when = LegTime(leg);
            if (t > when && t < when + 0.15) S.Others[other].PaintNegative(p);
        }

        // crackling arcs round it while it charges, then the burst
        if (te > 0 && te < 0.75)
        {
            var f = (int)Math.Floor(te * 20);
            for (var i = 0; i < 6; i++)
            {
                var a = Rnd(i, f) * 2 * Math.PI;
                var r = 16 + (Rnd(f, i) * 6);
                double px = S.X, py = S.Y;
                for (var j = 1; j <= 5; j++)
                {
                    double nx = S.X + (Math.Cos(a) * r * j / 5) + ((Rnd(i, j + f) - 0.5) * 4), ny = S.Y + (Math.Sin(a) * r * 0.8 * j / 5);
                    Line(px, py, nx, ny, (x, y) => p.Set(x, y, 0xF0C8FFu, 230));
                    px = nx;
                    py = ny;
                }
            }
        }

        if (te > 0.72 && te < 1.1)
        {
            var k = (te - 0.72) / 0.38;
            Glow(p, S.X, S.Y, 14 + (k * 30), 12 + (k * 22), 0xE0A0FFu, 240 * (1 - k));
            for (var i = 0; i < 50; i++)
            {
                var a = Rnd(i, 51) * 2 * Math.PI;
                var v = 30 + (Rnd(i, 52) * 50);
                Put(p, S.X + (Math.Cos(a) * v * k), S.Y + (Math.Sin(a) * v * k * 0.8), i % 3 != 0 ? 0xF0C8FFu : 0xFFFFFFu, 255 * (1 - k));
            }
        }

        if (te > 0.7) Shake(p, te - 0.72, 5, 0.3);
        else Shake(p, te, 2, 0.3);
    }
}
