using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// A trial passed, being shown (2026-10-15): how long since the cap rose, from which cap to which, and the Z-Crystal of the trial (null when
/// the stage has none, as the league, or the icon cannot be read: then only the cap's film plays).
/// </summary>
public sealed record CapTrialTurn(double Since, int From, int To, CapTrialCrystal? Crystal)
{
    /// <summary>The moment of the crystal's film, with its held frames.</summary>
    public double CrystalT => CapTrial.Held(Since, CapTrial.CrystalHolds);

    /// <summary>The moment of the cap's film (negative before it starts), with its held frames.</summary>
    public double CapT => CapTrial.Held(Since - CapTrial.CapStart(Crystal is not null), CapTrial.CapHolds);
}

/// <summary>
/// The two films of a trial passed, approved as prototypes (imagenes/revision/prototipo_prueba, prueba.js) and played one after the other
/// when the cap rises.
/// <para>
/// The crystal (both windows): the skies dim and the Ultra Wormhole beats; it bursts and the trial's Z-Crystal is born in its core; spinning,
/// trailing ribbons of its light, it crosses the party's sky and the abyss's in one curve, splashes into the abyss, sinks, hangs over the sand
/// while its symbol kindles, and drives itself into it: the frame holds, everything flashes, three waves of its light run through both
/// windows, lighting the Unown, the crystals of the abyss, the Pillar, the Wormhole and each of the party. It stays, a beam of light over it.
/// </para>
/// <para>
/// The cap (the party's window): a light falls on the plaque; cracks of light open in the old number and stay, rays turn out of them, the
/// sand rises, rings close in, the window rumbles; it bursts (the frame holds), a shockwave bends the water, the pieces tumble out hot,
/// bounce and turn to dust; a beam comes down from the surface, the dust climbs it into two orbs and each slams into its digit; the number is
/// revealed with a sun of rays, «+N» floats up, and a wave of light climbs the party turning each level from gold to white.
/// </para>
/// Until the cap's film breaks it, the plaque shows the old cap, and each level stays gold until that last wave passes it.
/// </summary>
public static class CapTrial
{
    // the crystal's film
    internal const double Born = 1.25, Cross = 2.2, Splash = 3.0, Hover = 3.95, Drop = 4.4, Hit = 4.52, CrystalEnd = 7.8;

    // the cap's film
    internal const double Crack = 0.5, Rays = 1.0, Rise = 1.3, Converge = 1.75, Burst = 2.4, Beam = 3.3, Reveal = 5.35, CapEnd = 8.2;
    internal static readonly double[] Slams = [4.55, 4.95];

    internal static readonly (double At, double Span)[] CrystalHolds = [(Hit, 0.14)];
    internal static readonly (double At, double Span)[] CapHolds = [(Burst, 0.16), (4.55, 0.06), (4.95, 0.06), (Reveal, 0.1)];

    private const uint White = 0xFFFFFFu, Gold = 0xFFD040u, Pale = 0xFFF2B0u, Dark = 0x040818u, Foam = 0xCFF4FFu, SandLight = 0xD8C89Au;
    private const double Scale = 1.1, Speed = 300;

    /// <summary>When the cap's film starts: after the crystal's has had its impact and its waves.</summary>
    public static double CapStart(bool crystal) => crystal ? 6.4 : 0;

    /// <summary>How long both films last, held frames and all.</summary>
    public static double Length(bool crystal) => CapStart(crystal) + CapEnd + 0.4;

    /// <summary>The trial being shown now, or null.</summary>
    public static CapTrialTurn? Now(CapPlaqueData data, CapAnim? anim, double t)
    {
        if (anim?.CapRise(t) is not { } rise) return null;
        var crystal = CapTrialCrystal.Of(data.CrystalItem, data.Crystal);
        return rise.Since < Length(crystal is not null) ? new CapTrialTurn(rise.Since, rise.From, rise.To, crystal) : null;
    }

    /// <summary>The moment a film shows: it stands still for a moment at each of its held frames.</summary>
    internal static double Held(double t, (double At, double Span)[] holds)
    {
        var shown = t;
        foreach (var (at, span) in holds)
        {
            if (t > at + span) shown -= span;
            else if (t > at) shown -= t - at;
        }

        return shown;
    }

    /// <summary>The cap a row is drawn with: the old one until the last wave of the cap's film has passed its level.</summary>
    public static int CapOf(CapTrialTurn? turn, int cap, int levelY, int ground)
    {
        if (turn is null) return cap;
        var d = turn.CapT - Reveal;
        return d > 0.35 && ground - ((d - 0.35) * 200) <= levelY ? turn.To : turn.From;
    }

    /// <summary>The number the plaque writes itself: the old cap before the cap's film, none while it breaks and remakes it, the new one after.</summary>
    public static int? SignNumber(CapTrialTurn? turn, int cap) => turn is null ? cap : turn.CapT < 0 ? turn.From : turn.CapT < Reveal + 0.35 ? null : cap;

    // ---------------------------------------------------------------- what the films share

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    private static double EaseIn(double k) => Math.Pow(Clamp01(k), 2.2);

    /// <summary>Light added over what is there: it brightens, it never darkens.</summary>
    internal static void Light(CapSheet p, double x, double y, uint c, double a)
    {
        if (a <= 0) return;
        int ix = R(x), iy = R(y);
        if (!p.Solid(ix, iy)) return;
        var b = p.At(ix, iy);
        var k = Math.Min(1, a / 255);
        p.Set(ix, iy, CapSheet.Rgb(Math.Min(255, Red(b) + (int)(Red(c) * k)), Math.Min(255, Green(b) + (int)(Green(c) * k)), Math.Min(255, Blue(b) + (int)(Blue(c) * k))));
    }

    internal static void Shine(CapSheet p, double x, double y, double rx, double ry, uint c, double a)
    {
        if (a <= 0 || rx <= 0 || ry <= 0) return;
        for (var dy = -(int)Math.Ceiling(ry); dy <= ry; dy++)
        {
            for (var dx = -(int)Math.Ceiling(rx); dx <= rx; dx++)
            {
                var d = Math.Sqrt(((dx / rx) * (dx / rx)) + ((dy / ry) * (dy / ry)));
                if (d < 1) Light(p, x + dx, y + dy, c, a * Math.Pow(1 - d, 1.5));
            }
        }
    }

    private static void Disc(CapSheet p, double x, double y, double rx, double ry, uint c, double a)
    {
        for (var dy = -(int)Math.Ceiling(ry); dy <= ry; dy++)
        {
            for (var dx = -(int)Math.Ceiling(rx); dx <= rx; dx++)
            {
                var d = Math.Sqrt(((dx / rx) * (dx / rx)) + ((dy / ry) * (dy / ry)));
                if (d < 1) Put(p, x + dx, y + dy, c, a * (1 - d));
            }
        }
    }

    private static void Star(CapSheet p, double x, double y, uint c, double a, int size = 0)
    {
        Light(p, x, y, White, a);
        foreach (var (ox, oy) in Round4)
        {
            for (var r = 1; r <= size + 1; r++) Light(p, x + (ox * r), y + (oy * r), c, a * (1 - (r / (size + 2.0))));
        }
    }

    private static void Ring(CapSheet p, double x, double y, double r, double squash, uint c, double a, bool add = true)
    {
        var n = Math.Max(24, (int)Math.Round(r * 7));
        for (var i = 0; i < n; i++)
        {
            var ang = i / (double)n * 2 * Math.PI;
            if (add) Light(p, x + (Math.Cos(ang) * r), y + (Math.Sin(ang) * r * squash), c, a);
            else Put(p, x + (Math.Cos(ang) * r), y + (Math.Sin(ang) * r * squash), c, a);
        }
    }

    private static void Dim(CapSheet p, double a, Func<int, int, double> keep)
    {
        if (a <= 0) return;
        Each(p, (x, y) => p.Set(x, y, Dark, (int)(a * keep(x, y))));
    }

    private static void Flash(CapSheet p, double a)
    {
        if (a <= 0) return;
        Each(p, (x, y) => p.Set(x, y, White, (int)a));
    }

    /// <summary>The window moved a few cells this frame: a jolt that dies away, or (growing) a rumble that builds.</summary>
    private static void Shake(CapSheet p, double since, double strength, double span, bool growing = false)
    {
        if (since < 0 || since > span) return;
        var f = growing ? since / span : Math.Pow(1 - (since / span), 1.5);
        int dx = R(Math.Sin(since * 95) * strength * f), dy = R(Math.Cos(since * 71) * strength * 0.7 * f);
        if (dx == 0 && dy == 0) return;
        var copy = (byte[])p.B.Clone();
        Each(p, (x, y) =>
        {
            int sx = Math.Clamp(x - dx, 0, p.W - 1), sy = Math.Clamp(y - dy, 0, p.H - 1), i = ((y * p.W) + x) * 4, j = ((sy * p.W) + sx) * 4;
            if (copy[j + 3] == 0) return;
            (p.B[i], p.B[i + 1], p.B[i + 2], p.B[i + 3]) = (copy[j], copy[j + 1], copy[j + 2], copy[j + 3]);
        });
    }

    /// <summary>A shockwave that bends the water it passes, like a lens, with light on its edge.</summary>
    private static void Lens(CapSheet p, double cx, double cy, double r, double amp, double band, uint edge)
    {
        var copy = (byte[])p.B.Clone();
        int y0 = Math.Max(0, (int)(cy - ((r + band) / 1.25)) - 1), y1 = Math.Min(p.H - 1, (int)(cy + ((r + band) / 1.25)) + 1);
        int x0 = Math.Max(0, (int)(cx - r - band) - 1), x1 = Math.Min(p.W - 1, (int)(cx + r + band) + 1);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (y < p.NotchY1 && x >= p.NotchX0 && x < p.NotchX1) continue;
                double dx = x - cx, dy = (y - cy) * 1.25, d = Math.Sqrt((dx * dx) + (dy * dy)), e = d - r;
                if (Math.Abs(e) > band || d < 1) continue;
                var k = Math.Cos(e / band * Math.PI / 2);
                int sx = Math.Clamp(R(x - (dx / d * amp * k)), 0, p.W - 1), sy = Math.Clamp(R(y - (dy / d * amp * k * 0.8)), 0, p.H - 1);
                int i = ((y * p.W) + x) * 4, j = ((sy * p.W) + sx) * 4;
                if (copy[j + 3] != 0) (p.B[i], p.B[i + 1], p.B[i + 2], p.B[i + 3]) = (copy[j], copy[j + 1], copy[j + 2], copy[j + 3]);
                if (e > 0 && e < 2) Light(p, x, y, edge, 200 * k);
            }
        }
    }

    // ---------------------------------------------------------------- the crystal's film

    /// <summary>Where the crystal lands, in a window: in the near sand of the abyss, in front of its crystals (in the party's, where that is in the sea's world).</summary>
    private static (int X, int Y) Land(CapWorldLayout l, CapSeaContext ctx) =>
        l.Panel == CapPanelKind.Deep ? (Math.Min(110, l.Cols - 30), l.Rows - 12)
        : (ctx.WorldWidth > 0 ? 110 - ctx.WorldX : -160, l.Rows - 12);

    /// <summary>The first or last column of a row that is not under the game.</summary>
    private static int Edge(CapWorldLayout l, int y, bool left)
    {
        if (left)
        {
            for (var x = 0; x < l.Cols; x++) if (!l.InNotch(x, y)) return x;
            return 0;
        }

        for (var x = l.Cols - 1; x >= 0; x--) if (!l.InNotch(x, y)) return x;
        return l.Cols - 1;
    }

    private static (double X, double Y) Cubic((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d, double u)
    {
        var v = 1 - u;
        double Of(double p0, double p1, double p2, double p3) => (v * v * v * p0) + (3 * v * v * u * p1) + (3 * v * u * u * p2) + (u * u * u * p3);
        return (Of(a.X, b.X, c.X, d.X), Of(a.Y, b.Y, c.Y, d.Y));
    }

    private readonly record struct Flight((double X, double Y) P, double Scale, double Spin, double? Hover = null, double? Landed = null);

    /// <summary>
    /// Where the crystal is in a window at a moment of its film, or null when it is not in that window. Its flight is one curve through both
    /// skies, laid out as if the two windows touched: each window has its half, and both halves meet at the edge between them at the same
    /// height and the same velocity, so it leaves one sky and comes into the other going the same way.
    /// </summary>
    private static Flight? Where(CapWorldLayout l, CapSeaContext ctx, double t)
    {
        if (t < Born) return null;
        var land = Land(l, ctx);
        var yE = R(l.SurfaceY * 0.55);
        (double X, double Y) v = (-95, 4);
        var scale = 0.18 + (0.62 * Ease(Math.Min(1, (t - Born) / (Splash - Born) * 1.3)));
        var spin = (t - Born) * 15;
        if (t < Cross)
        {
            if (l.Panel != CapPanelKind.Party || CapFarSky.WormholeAt(l) is not { } core) return null;
            var e = (X: Edge(l, yE, true) - 1.0, Y: (double)yE);
            const double span = Cross - Born;
            var u = (t - Born) / span;
            return new Flight(Cubic(core, (core.X - 8, core.Y + 3), (e.X - (v.X * span / 3), e.Y - (v.Y * span / 3)), e, u), scale, spin);
        }

        if (l.Panel != CapPanelKind.Deep) return null;
        if (t < Splash)
        {
            var e = (X: Edge(l, yE, false) + 1.0, Y: (double)yE);
            const double span = Splash - Cross;
            var u = (t - Cross) / span;
            return new Flight(Cubic(e, (e.X + (v.X * span / 3), e.Y + (v.Y * span / 3)), (land.X + 20, l.SurfaceY - 28), (land.X, l.SurfaceY), u), scale, spin);
        }

        double rest = land.Y - (CapTrialCrystal.H * Scale) - 14, stand = land.Y - (CapTrialCrystal.H * Scale) + 4;
        var spin0 = (Splash - Born) * 15;
        if (t < Hover)
        {
            var u = (t - Splash) / (Hover - Splash);
            var e = EaseOut(u);
            var turns = Math.Ceiling((spin0 / (2 * Math.PI)) + 2) * 2 * Math.PI;           // it ends face on
            return new Flight((land.X + (Math.Sin(u * 5) * 3 * (1 - u)), l.SurfaceY + ((rest - l.SurfaceY) * e)), Scale, spin0 + ((turns - spin0) * e));
        }

        if (t < Drop) return new Flight((land.X, rest + (Math.Sin((t - Hover) * 9) * 1.2)), Scale, 0, (t - Hover) / (Drop - Hover));
        if (t < Hit) return new Flight((land.X, rest + ((stand - rest) * EaseIn((t - Drop) / (Hit - Drop)))), Scale, 0, 1);
        return new Flight((land.X, stand), Scale, 0, null, t - Hit);
    }

    /// <summary>When the first wave of the crystal's light reaches a cell of a window.</summary>
    private static double WaveAt(CapWorldLayout l, CapSeaContext ctx, double x, double y)
    {
        var land = Land(l, ctx);
        return Math.Sqrt(((x - land.X) * (x - land.X)) + ((y - land.Y) * 1.1 * (y - land.Y) * 1.1)) / Speed;
    }

    /// <summary>How one of the party is lit at a moment by the films: it hops, its colours wash towards the light, its halo grows.</summary>
    public static (int Hop, uint Wash, int Alpha, double Halo)? Lift(CapTrialTurn turn, CapWorldLayout l, CapSeaContext ctx, (int X, int Y) centre, int ground)
    {
        if (turn.Crystal is { } crystal)
        {
            var a = turn.CrystalT - Hit - WaveAt(l, ctx, centre.X, centre.Y);
            if (a >= 0 && a < 0.7) return (a < 0.4 ? R(Math.Sin(Math.PI * a / 0.4) * 4) : 0, crystal.Shine, (int)(140 * (1 - (a / 0.7))), 2.4 * (1 - (a / 0.7)));
        }

        var d = turn.CapT - Reveal - 0.35;
        if (d > 0)
        {
            var a = (centre.Y - (ground - (d * 200))) / 50;
            if (a > 0 && a < 0.7) return (a < 0.4 ? R(Math.Sin(Math.PI * a / 0.4) * 3) : 0, Pale, (int)(90 * (1 - (a / 0.7))), 2 * (1 - (a / 0.7)));
        }

        return null;
    }

    /// <summary>The crystal's film, in either window: what of it happens there.</summary>
    public static void PaintCrystal(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialTurn turn)
    {
        if (turn.Crystal is not { } crystal || l.Mode is not (CapMode.Column or CapMode.Slim)) return;
        var t = turn.CrystalT;
        if (t >= CrystalEnd + 3) return;

        var deep = l.Panel == CapPanelKind.Deep;
        var land = Land(l, ctx);
        var surface = l.SurfaceY;
        var hit = t - Hit;
        var st = Where(l, ctx, t);

        // what the waves will light, read before anything is painted over it: the strokes of the Unown, the glass of the abyss's crystals
        List<(int X, int Y)>? unown = null;
        List<(int X, int Y, uint C)>? glass = null;
        if (deep && hit > 0 && hit < 2.6)
        {
            unown = [];
            glass = [];
            for (var y = Math.Max(0, l.FloorTop - 45); y < Math.Min(l.Rows, l.FloorTop + 3); y++)
            {
                for (var x = 2; x < Math.Min(l.Cols, 90); x++)
                {
                    var c = p.At(x, y);
                    if (Luma(c) < 95 && Red(c) > Blue(c)) unown.Add((x, y));
                }

                for (var x = 82; x < Math.Min(l.Cols, 142); x++)
                {
                    var c = p.At(x, y);
                    if (Blue(c) > Red(c) + 25 && Luma(c) > 100 && y < l.FloorTop) glass.Add((x, y, c));
                }
            }
        }

        // the skies dim for it, and stay dim until its light has gone through
        var darkness = t < Born ? Ease(t / 0.6) : t < Hit ? 1 : 1 - Ease((t - Hit - 1.2) / 1.6);
        Dim(p, 70 * darkness, (_, y) => y < surface + 2 ? 1 : 0.55);

        if (!deep && CapFarSky.WormholeAt(l) is { } core)
        {
            // anticipation: the wormhole beats twice like a heart, pulling light into itself, rays turning round it
            if (t < Born + 0.4)
            {
                var beat = Math.Max(Math.Exp(-Math.Pow((t - 0.35) / 0.09, 2)), Math.Exp(-Math.Pow((t - 0.85) / 0.09, 2)));
                var grow = Clamp01(t / Born);
                Shine(p, core.X, core.Y, 14 + (grow * 10) + (beat * 8), 12 + (grow * 8) + (beat * 7), crystal.Glow, (150 * grow) + (110 * beat));
                for (var r = 0; r < 12; r++)
                {
                    var ang = (r / 12.0 * 2 * Math.PI) + (t * 0.9);
                    var len = 18 + (14 * grow) + (10 * beat);
                    Line(core.X + (Math.Cos(ang) * 5), core.Y + (Math.Sin(ang) * 4), core.X + (Math.Cos(ang) * len), core.Y + (Math.Sin(ang) * len * 0.8), (x, y) =>
                    {
                        if (((x + y) & 1) == 0) Light(p, x, y, crystal.Shine, (70 * grow) + (60 * beat));
                    });
                }

                for (var n = 0; n < 40; n++)
                {
                    var age = t - (n / 40.0 * 1.2);
                    if (age < 0 || age > 0.7) continue;
                    var ang = Rnd(n, 7) * 2 * Math.PI;
                    var r = (1 - EaseIn(age / 0.7)) * (26 + (Rnd(n, 8) * 22));
                    Light(p, core.X + (Math.Cos(ang) * r), core.Y + (Math.Sin(ang) * r * 0.7), n % 2 == 1 ? crystal.Shine : White, 220 * (age / 0.7));
                }
            }

            // birth: the core bursts white, a ring runs over the sky, a streak of light across it
            var b = t - Born;
            if (b > 0 && b < 0.7)
            {
                Shine(p, core.X, core.Y, 26 * (1 - b), 22 * (1 - b), White, 255 * (1 - (b / 0.7)));
                Ring(p, core.X, core.Y, 6 + (b * 120), 0.55, CapSheet.Mix(crystal.Shine, White, 0.5), 220 * (1 - (b / 0.7)));
                Ring(p, core.X, core.Y, 3 + (b * 80), 0.55, crystal.Glow, 160 * (1 - (b / 0.7)));
                for (var dx = -110; dx <= 110; dx++)
                {
                    var k = Math.Pow(1 - (Math.Abs(dx) / 110.0), 2) * (1 - (b / 0.7));
                    Light(p, core.X + dx, core.Y, White, 240 * k);
                    Light(p, core.X + dx, core.Y - 1, crystal.Shine, 120 * k);
                    Light(p, core.X + dx, core.Y + 1, crystal.Shine, 120 * k);
                }

                for (var dy = -22; dy <= 22; dy++) Light(p, core.X, core.Y + dy, White, 200 * (1 - (Math.Abs(dy) / 22.0)) * (1 - (b / 0.4)));
            }
        }

        // the flight: two ribbons of its light twisting round its path, sparks shed from it
        if (t >= Born && t < Splash)
        {
            for (var k = 0; k < 26; k++)
            {
                var tt = t - (k * 0.014);
                if (tt < Born || Where(l, ctx, tt) is not { } s) continue;
                var prev = Where(l, ctx, Math.Max(Born, tt - 0.01))?.P ?? s.P;
                var ang = Math.Atan2(s.P.Y - prev.Y, s.P.X - prev.X) + (Math.PI / 2);
                var fade = 1 - (k / 26.0);
                foreach (var (side, c) in new[] { (1, crystal.Shine), (-1, White) })
                {
                    var off = Math.Sin((tt * 26) + (side * 1.6)) * 4 * s.Scale * 2.2;
                    Light(p, s.P.X + (Math.Cos(ang) * off), s.P.Y + (Math.Sin(ang) * off), c, 230 * fade);
                }

                Light(p, s.P.X, s.P.Y, crystal.Glow, 120 * fade);
            }

            for (var n = 0; n < 60; n++)
            {
                var born = Born + (n / 60.0 * (Splash - Born));
                var age = t - born;
                if (age < 0 || age > 0.7 || Where(l, ctx, born) is not { } s) continue;
                var c = n % 3 == 0 ? White : n % 3 == 1 ? crystal.Shine : crystal.Glow;
                Star(p, s.P.X + ((Rnd(n, 1) - 0.5) * 8) + ((Rnd(n, 2) - 0.5) * age * 18), s.P.Y + ((Rnd(n, 4) - 0.5) * 6) + (age * 10), c, 230 * (1 - (age / 0.7)), n % 7 == 0 ? 1 : 0);
            }
        }

        if (deep)
        {
            // it goes past the Sky Pillar, which glints
            var pass = t - Cross - 0.25;
            if (pass > 0 && pass < 0.6 && CapFarSky.PillarTopAt(l) is { } top)
            {
                var k = Math.Sin(Math.PI * pass / 0.6);
                Star(p, top.X, top.Y, crystal.Shine, 255 * k, 3);
                Shine(p, top.X, top.Y, 10, 8, crystal.Glow, 120 * k);
            }

            // the water: a crown of drops, rings on the surface, bubbles behind it as it sinks
            var sp = t - Splash;
            if (sp > 0)
            {
                if (sp < 0.12) Shine(p, land.X, surface, 30, 10, White, 230 * (1 - (sp / 0.12)));
                for (var n = 0; n < 54; n++)
                {
                    var life = 0.5 + (Rnd(n, 9) * 0.5);
                    if (sp > life) continue;
                    var ang = Math.PI * (0.12 + (Rnd(n, 3) * 0.76));
                    var v = 26 + (Rnd(n, 5) * 52);
                    var y = surface - (Math.Sin(ang) * v * sp) + (120 * sp * sp);
                    if (y > surface + 1) continue;
                    Put(p, land.X - (Math.Cos(ang) * v * sp), y, n % 3 != 0 ? Foam : White, 255 * (1 - (sp / life)));
                }

                for (var r = 0; r < 3; r++)
                {
                    var k = sp - (r * 0.18);
                    if (k <= 0 || k >= 1.4) continue;
                    var rad = 3 + (k * 46);
                    for (var i = 0; i < 90; i++)
                    {
                        var ang = i / 90.0 * Math.PI;
                        var x = land.X + (Math.Cos(ang) * rad);
                        Put(p, x, surface + R(Math.Sin(ang) * rad * 0.06), White, 200 * (1 - (k / 1.4)));
                        Put(p, x, surface - R(Math.Sin(ang) * rad * 0.04), Foam, 120 * (1 - (k / 1.4)));
                    }
                }

                if (sp < 0.4) for (var dy = 0; dy < 10; dy++) Put(p, land.X + ((Rnd(dy, 3) - 0.5) * 6), surface - dy - (sp * 30), White, 220 * (1 - (sp / 0.4)) * (1 - (dy / 10.0)));

                for (var n = 0; n < 50; n++)
                {
                    var born = Splash + (n / 50.0 * (Hit + 0.3 - Splash));
                    var age = t - born;
                    if (age < 0 || age > 1.3 || Where(l, ctx, born) is not { } s) continue;
                    var x = s.P.X + ((Rnd(n, 21) - 0.5) * 16) + (Math.Sin((age * 7) + n) * 2);
                    var y = s.P.Y - (age * 26);
                    if (y < surface + 1) continue;
                    Put(p, x, y, Foam, 170 * (1 - (age / 1.3)));
                    if (n % 4 == 0)
                    {
                        Put(p, x + 1, y, Foam, 120 * (1 - (age / 1.3)));
                        Put(p, x, y - 1, White, 200 * (1 - (age / 1.3)));
                    }
                }
            }

            // hanging over the sand: its symbol kindles, light is drawn into it
            if (st is { Hover: { } hover, Landed: null } h)
            {
                for (var n = 0; n < 34; n++)
                {
                    var age = t - (Hover + (n / 34.0 * (Drop - Hover)));
                    if (age < 0 || age > 0.5) continue;
                    var ang = Rnd(n, 31) * 2 * Math.PI;
                    var r = (1 - EaseIn(age / 0.5)) * (24 + (Rnd(n, 33) * 18));
                    Star(p, h.P.X + (Math.Cos(ang) * r), h.P.Y + (Math.Sin(ang) * r * 0.75), crystal.Shine, 230 * (age / 0.5));
                }

                Shine(p, h.P.X, h.P.Y, 22 + (hover * 10), 16 + (hover * 8), crystal.Glow, 90 + (120 * hover));
                if (t > Drop) for (var dy = 1; dy < 30; dy++) Light(p, h.P.X, h.P.Y - dy, crystal.Shine, 120 * (1 - (dy / 30.0)));
            }
            else if (st is { Landed: null } under && t > Splash)
            {
                Shine(p, under.P.X, under.P.Y, 26, 20, crystal.Glow, 110);
            }
        }

        // the crystal itself, while it flies and sinks
        if (st is { Landed: null } f)
        {
            if (t < Splash) Shine(p, f.P.X, f.P.Y, (12 * f.Scale) + 6, (10 * f.Scale) + 5, crystal.Glow, 130);
            var burn = f.Hover ?? (t > Splash ? Clamp01((t - Hover + 0.5) / 0.5) * 0.4 : 0);
            crystal.Paint(p, R(f.P.X), R(f.P.Y), f.Scale, f.Spin, burn, t > Hover - 0.4 && t < Hover + 0.2 ? (t - Hover + 0.4) / 0.6 : -1);
        }

        if (hit >= 0)
        {
            // the impact: everything flashes; in the abyss the sand bursts and a ring runs over it
            if (hit < 0.1) Flash(p, (deep ? 230 : 140) * (1 - (hit / 0.1)));
            if (deep)
            {
                for (var n = 0; n < 90; n++)
                {
                    var life = 0.6 + (Rnd(n, 19) * 0.7);
                    if (hit > life) continue;
                    var ang = Math.PI * (0.05 + (Rnd(n, 13) * 0.9));
                    var v = 18 + (Rnd(n, 15) * 62);
                    var sand = p.At(land.X - 20 + (n % 41), land.Y - 2 + (n % 6));
                    Put(p, land.X - (Math.Cos(ang) * v * hit), land.Y - 2 - (Math.Sin(ang) * v * 1.25 * hit) + (85 * hit * hit), n % 6 == 0 ? CapSheet.Mix(sand, White, 0.5) : sand, 255 * (1 - Math.Pow(hit / life, 2)));
                }

                for (var n = 0; n < 8; n++)
                {
                    var k = hit - (n * 0.03);
                    if (k > 0 && k < 1.2) Puff(p, land.X + ((n - 3.5) * 7 * (1 + k)), land.Y - 4 - (k * 9), 4 * (1 + (k * 0.8)), SandLight, 150 * (1 - (k / 1.2)));
                }

                if (hit < 0.9)
                {
                    Ring(p, land.X, land.Y - 1, 6 + (hit * 90), 0.16, CapSheet.Mix(crystal.Shine, 0xE8D8A8u, 0.4), 230 * (1 - (hit / 0.9)), false);
                    Ring(p, land.X, land.Y - 1, 3 + (hit * 60), 0.16, White, 180 * (1 - (hit / 0.9)), false);
                }

                for (var n = 0; n < 24; n++)
                {
                    var age = hit - (Rnd(n, 41) * 0.4);
                    if (age < 0 || age > 1.6) continue;
                    var y = land.Y - 6 - (age * 40);
                    if (y > surface) Put(p, land.X + ((Rnd(n, 43) - 0.5) * 40) + (Math.Sin((age * 6) + n) * 2), y, Foam, 190 * (1 - (age / 1.6)));
                }
            }

            // three waves of its light through the water
            foreach (var (from, strength) in new[] { (0.0, 1.0), (0.16, 0.6), (0.6, 0.45) })
            {
                var r = (hit - from) * Speed;
                if (r <= 0 || r > 820) continue;
                var fade = strength * (1 - (r / 1300));
                Each(p, (x, y) =>
                {
                    var d = Math.Sqrt(((x - land.X) * (x - land.X)) + ((y - land.Y) * 1.1 * (y - land.Y) * 1.1));
                    var e = r - d;
                    if (e >= 0 && e < 1.5) Light(p, x, y, White, 230 * fade);
                    else if (e >= 1.5 && e < 6) Light(p, x, y, crystal.Shine, 200 * fade * (1 - ((e - 1.5) / 4.5)));
                    else if (e >= 6 && e < 40 && ((x + y) & 1) == 0) Light(p, x, y, crystal.Glow, 70 * fade * (1 - ((e - 6) / 34)));
                    else if (e > -3 && e < 0) Light(p, x, y, crystal.Glow, 90 * fade * (1 + (e / 3)));
                });
            }

            // what the waves touch: the Unown and the glass of the abyss, the Pillar and the Wormhole (the party is lit through its pose)
            if (unown is not null)
            {
                foreach (var (x, y) in unown)
                {
                    var a = hit - WaveAt(l, ctx, x, y) - ((x % 7) * 0.02);
                    if (a > 0 && a < 2.2) Put(p, x, y, CapSheet.Mix(crystal.Shine, White, a < 0.2 ? 0.6 : 0.1), 255 * (a < 0.15 ? a / 0.15 : 1 - ((a - 0.15) / 2.05)));
                }
            }

            if (glass is not null)
            {
                foreach (var (x, y, c) in glass)
                {
                    var a = hit - WaveAt(l, ctx, x, y);
                    if (a > 0 && a < 1.8) Put(p, x, y, CapSheet.Mix(c, CapSheet.Mix(crystal.Shine, White, 0.4), 0.9), 255 * (a < 0.12 ? a / 0.12 : 1 - ((a - 0.12) / 1.68)));
                }
            }

            foreach (var (point, size) in new[] { (CapFarSky.PillarTopAt(l), 3), (CapFarSky.WormholeAt(l), 4) })
            {
                if (point is not { } at) continue;
                var a = hit - WaveAt(l, ctx, at.X, at.Y);
                if (a <= 0 || a >= 0.8) continue;
                Star(p, at.X, at.Y, crystal.Shine, 255 * Math.Sin(Math.PI * a / 0.8), size);
                Shine(p, at.X, at.Y, 14, 11, crystal.Glow, 130 * Math.Sin(Math.PI * a / 0.8));
            }

            if (deep)
            {
                // after: half buried, a beam of its light up to the surface, motes rising in it, its glow breathing, a glint now and then
                double stand = land.Y - (CapTrialCrystal.H * Scale) + 4, after = Clamp01((hit - 0.6) / 0.8) * (1 - Clamp01((t - CrystalEnd - 1.5) / 1.5)), breath = 0.75 + (0.25 * Math.Sin(hit * 3.2));
                if (after > 0)
                {
                    for (var y = surface + 1; y < land.Y - 14; y++)
                    {
                        for (var dx = -7; dx <= 7; dx++)
                        {
                            var k = Math.Pow(1 - (Math.Abs(dx) / 7.5), 1.5) * (0.55 + (0.45 * (y - surface) / (double)(land.Y - surface))) * after * breath * (0.85 + (0.15 * Math.Sin((y * 0.4) - (hit * 6))));
                            if (((land.X + dx + y) & 1) == 0 || Math.Abs(dx) < 3) Light(p, land.X + dx, y, crystal.Glow, 75 * k);
                        }
                    }

                    Shine(p, land.X, surface + 1, 14, 3, crystal.Shine, 140 * after * breath);
                }

                for (var n = 0; n < 24; n++)
                {
                    var born = (n / 24.0 * 3) + 0.8;
                    var age = (hit - born) % 3;
                    if (hit < born || age > 2.2 || after <= 0) continue;
                    Light(p, land.X + ((Rnd(n, 51) - 0.5) * 10) + (Math.Sin((age * 3) + n) * 2), land.Y - 14 - (age * 100), n % 2 == 1 ? crystal.Shine : White, 220 * (1 - (age / 2.2)) * after);
                }

                var gone = 1 - Clamp01((t - CrystalEnd - 1.5) / 1.5);
                Shine(p, land.X, stand, 34, 24, crystal.Glow, (hit < 0.3 ? 220 : (90 + (40 * breath)) * Math.Max(0.6, 1 - (after * 0.4))) * gone);
                var jolt = hit < 0.25 ? R(Math.Sin(hit * 60) * 2 * (1 - (hit / 0.25))) : 0;
                crystal.Paint(p, land.X + jolt, R(stand), Scale, 0, hit < 0.4 ? 1 : 0.35 + (0.25 * breath), hit > 0.9 && hit < 1.5 ? (hit - 0.9) / 0.6 : hit > 2.3 && hit < 2.9 ? (hit - 2.3) / 0.6 : -1,
                    land.Y, (int)(255 * gone), hit < 0.12 ? 0.8 * (1 - (hit / 0.12)) : 0);
                // the sand closes round its foot
                for (var dx = -12; dx <= 12; dx++)
                {
                    var y = land.Y - R(1.5 * (1 - (Math.Abs(dx) / 12.0)));
                    Put(p, land.X + dx, y, CapSheet.Mix(p.At(land.X + dx, land.Y + 1), White, 0.15), 220 * gone);
                }
            }
        }

        // the jolts: the birth in the party's window, the splash and the hanging in the abyss's, the impact in both
        if (deep)
        {
            Shake(p, t - Splash, 2, 0.25);
            if (t > Hover && t < Hit) Shake(p, t - Hover, 1, Hit - Hover, true);
        }
        else
        {
            Shake(p, t - Born, 2, 0.3);
        }

        Shake(p, hit, 4, 0.45);
    }

    // ---------------------------------------------------------------- the cap's film

    /// <summary>What the cap's film breaks and makes, worked out once: the old number in pieces with their cracks, the new one by digit.</summary>
    public sealed class Plaque
    {
        internal sealed record Cell(int X, int Y, uint C)
        {
            public int Piece { get; set; }

            public bool Edge { get; set; }

            public double Crack { get; set; }
        }

        internal sealed class Piece
        {
            public required int N { get; init; }

            public required List<Cell> Cells { get; init; }

            public required Dictionary<(int, int), Cell> Map { get; init; }

            public double Cx { get; init; }

            public double Cy { get; init; }

            public double Radius { get; init; }

            public double Vx { get; init; }

            public double Vy { get; init; }

            public double Spin { get; init; }

            public double Crumble { get; init; }
        }

        internal sealed record Mote(double Sx, double Sy, int Group, double From, double Arrive, int Side);

        internal CapRect Board { get; }

        internal int Ground { get; }

        internal List<Cell> Old { get; } = [];

        internal List<Cell> New { get; } = [];

        internal List<Piece> Pieces { get; } = [];

        internal HashSet<(int, int)> Edges { get; } = [];

        internal List<(List<Cell> Cells, double Cx, double Cy)> Groups { get; } = [];

        internal List<Mote> Motes { get; } = [];

        internal (double X, double Y) Middle { get; }

        internal (int X0, int X1) Span { get; }

        public Plaque(CapRect board, int ground, int numberX, int numberY, int scale, int from, int to, int cols, int rows)
        {
            Board = board;
            Ground = ground;
            Old.AddRange(Cells(from, numberX, numberY, scale, cols, rows));
            New.AddRange(Cells(to, numberX, numberY, scale, cols, rows));
            if (Old.Count == 0 || New.Count == 0) return;

            var ox = Old.Average(q => q.X);
            var oy = Old.Average(q => q.Y);
            Middle = (ox, oy);
            int x0 = New.Min(q => q.X), x1 = New.Max(q => q.X);
            Span = (x0, x1);

            // the old number in pieces: the cracks are the edges between them, running out from its middle
            var seeds = new List<(double X, double Y)>();
            int bx0 = Old.Min(q => q.X), bx1 = Old.Max(q => q.X), by0 = Old.Min(q => q.Y), by1 = Old.Max(q => q.Y);
            for (var y = by0 - 2; y <= by1 + 2; y += 5)
            {
                for (var x = bx0 - 2; x <= bx1 + 2; x += 5) seeds.Add((x + ((Rnd(x, y) - 0.5) * 3.5), y + ((Rnd(y, x) - 0.5) * 3.5)));
            }

            foreach (var q in Old) q.Piece = seeds.Select((s, i) => (D: ((q.X - s.X) * (q.X - s.X)) + ((q.Y - s.Y) * (q.Y - s.Y)), i)).MinBy(s => s.D).i;
            var at = Old.ToDictionary(q => (q.X, q.Y));
            var far = Old.Max(q => Math.Sqrt(((q.X - ox) * (q.X - ox)) + ((q.Y - oy) * (q.Y - oy))));
            foreach (var q in Old)
            {
                q.Edge = Round4.Any(o => at.TryGetValue((q.X + o.X, q.Y + o.Y), out var n) && n.Piece != q.Piece);
                q.Crack = Crack + (Math.Sqrt(((q.X - ox) * (q.X - ox)) + ((q.Y - oy) * (q.Y - oy))) / far * 0.8);
                if (q.Edge) Edges.Add((q.X, q.Y));
            }

            foreach (var (group, n) in Old.GroupBy(q => q.Piece).Select((g, n) => (g.ToList(), n)))
            {
                double cx = group.Average(q => q.X), cy = group.Average(q => q.Y), len = Math.Max(1, Math.Sqrt(((cx - ox) * (cx - ox)) + ((cy - oy) * (cy - oy)))), v = 22 + (Rnd(n, 2) * 26);
                Pieces.Add(new Piece
                {
                    N = n, Cells = group, Cx = cx, Cy = cy, Map = group.ToDictionary(q => (q.X - R(cx), q.Y - R(cy))),
                    Radius = group.Max(q => Math.Sqrt(((q.X - cx) * (q.X - cx)) + ((q.Y - cy) * (q.Y - cy)))) + 1,
                    Vx = (cx - ox) / len * v, Vy = ((cy - oy) / len * v * 0.7) - 30 - (Rnd(n, 4) * 24), Spin = (Rnd(n, 5) - 0.5) * 18, Crumble = 0.9 + (Rnd(n, 6) * 0.5),
                });
            }

            // the new number, one digit at a time: split where a column in its middle is empty
            var columns = New.Select(q => q.X).ToHashSet();
            var middle = (x0 + x1) / 2;
            var split = Enumerable.Range(middle - 3, 7).Cast<int?>().FirstOrDefault(x => !columns.Contains(x!.Value)) ?? middle;
            var digits = to >= 10 ? new List<List<Cell>> { New.Where(q => q.X < split).ToList(), New.Where(q => q.X >= split).ToList() } : [New];
            foreach (var cells in digits)
            {
                if (cells.Count > 0) Groups.Add((cells, cells.Average(q => q.X), cells.Average(q => q.Y)));
            }

            // the dust called back: motes leave the sand, climb the beam turning round it and fall into the orb of their digit
            for (var j = 0; j < 90; j++)
            {
                var src = Old[(j * 11) % Old.Count];
                var (sx, sy) = DustOf(src, j);
                var g = j % Groups.Count;
                var from2 = Beam + 0.15 + (Rnd(j, 91) * 0.7);
                Motes.Add(new Mote(sx, sy, g, from2, Math.Min(Slams[Math.Min(g, 1)] - 0.15, from2 + 0.6 + (Rnd(j, 93) * 0.35)), Rnd(j, 95) < 0.5 ? -1 : 1));
            }
        }

        private static IEnumerable<Cell> Cells(int number, int x, int y, int scale, int cols, int rows)
        {
            var sheet = new CapSheet(cols, rows);
            CapGlyphs.Outlined(sheet, number.ToString(System.Globalization.CultureInfo.InvariantCulture), x, y, 0xFFD860u, 0x1A0F08u, true, scale);
            for (var yy = 0; yy < rows; yy++)
            {
                for (var xx = 0; xx < cols; xx++)
                {
                    if (sheet.Solid(xx, yy)) yield return new Cell(xx, yy, sheet.At(xx, yy));
                }
            }
        }

        /// <summary>A piece in flight: thrown out, turning, falling, one bounce on the sand.</summary>
        internal (double X, double Y, double Ang, double? Landed) Fly(Piece pc, double s)
        {
            const double g = 120;
            double a = 0.5 * g, b = pc.Vy, c = pc.Cy - Ground, d2 = (b * b) - (4 * a * c);
            var floor = d2 < 0 ? 99 : (-b + Math.Sqrt(d2)) / (2 * a);
            if (s < floor) return (pc.Cx + (pc.Vx * s), pc.Cy + (pc.Vy * s) + (0.5 * g * s * s), pc.Spin * s, null);
            var s2 = s - floor;
            var vy2 = -(pc.Vy + (g * floor)) * 0.32;
            return (pc.Cx + (pc.Vx * floor) + (pc.Vx * 0.5 * s2), Math.Min(Ground, Ground + (vy2 * s2) + (0.5 * g * s2 * s2)), (pc.Spin * floor) + (pc.Spin * 0.4 * s2), s2);
        }

        internal (double X, double Y) DustOf(Cell q, int n)
        {
            var pc = Pieces.First(p => p.Cells.Contains(q));
            var f = Fly(pc, pc.Crumble);
            return (f.X + (q.X - pc.Cx) + ((Rnd(n, q.Y) - 0.5) * 8), Math.Min(Ground + 1, f.Y + ((q.Y - pc.Cy) * 0.3)) + ((Rnd(q.X, n) - 0.5) * 2));
        }
    }

    /// <summary>The cap's film, in the party's window.</summary>
    public static void PaintCap(CapSheet p, CapWorldLayout l, CapTrialTurn turn, Plaque plaque)
    {
        var t = turn.CapT;
        if (t < 0 || t > CapEnd || plaque.Old.Count == 0 || plaque.New.Count == 0) return;

        var board = plaque.Board;
        var c = (X: board.Left + (board.Width / 2.0), Y: board.Top + (board.Height / 2.0));
        var (ox, oy) = plaque.Middle;
        var (x0, x1) = plaque.Span;
        var ground = plaque.Ground;
        var surface = l.SurfaceY;

        // the sea darkens round the plaque, and a cone of light falls on it from the surface
        var gone = t > Reveal + 1.2 ? 1 - Ease((t - Reveal - 1.2) / 1.0) : 1;
        var spot = (t < 0.5 ? Ease(t / 0.5) : 1) * gone;
        Dim(p, 150 * spot, (x, y) => Clamp01((Math.Sqrt(((x - c.X) * (x - c.X)) + ((y - c.Y) * 1.3 * (y - c.Y) * 1.3)) - 24) / 80));
        for (var y = surface + 1; y < board.Top; y++)
        {
            var k = (y - surface) / (double)Math.Max(1, board.Top - surface);
            var w = 6 + (k * 24);
            for (var dx = -(int)Math.Ceiling(w); dx <= w; dx++) if (((dx + y) & 1) == 0) Light(p, c.X + dx, y, 0xFFE6A0u, 26 * spot * (1 - (Math.Abs(dx) / (w + 1))) * (0.4 + (0.6 * k)));
        }

        Shine(p, c.X, c.Y, 40, 30, 0xFFE6A0u, 45 * spot);
        if (t < 0.45) Frame(p, board, 0xFFE070u, 230 * (1 - (t / 0.45)));
        if (t < 0.35) Ring(p, c.X, c.Y, 22 + (t * 60), 0.7, 0xFFE070u, 160 * (1 - (t / 0.35)));
        var jolt = t < 0.3 ? R(Math.Sin(t * 55) * 1.6 * (1 - (t / 0.3))) : 0;

        if (t < Burst)
        {
            var charge = Clamp01((t - Crack) / (Burst - Crack));
            var shiver = Clamp01((t - Rise) / (Burst - Rise));
            var f = (int)Math.Floor(t * 30);

            // the sand under it lifts, grains rising to it; bubbles stream up from the cracks
            for (var n = 0; n < 60; n++)
            {
                var gx = c.X + ((Rnd(n, 101) - 0.5) * 90);
                var age = t - (Rise + (Rnd(n, 103) * (Burst - Rise - 0.2)));
                if (age < 0) continue;
                var u = EaseIn(age / 0.9);
                if (u >= 1) continue;
                var sand = p.At(R(gx), ground + 2 + (n % 4));
                Put(p, gx + ((c.X - gx) * u * 0.6) + (Math.Sin((age * 9) + n) * 1.5), ground + 2 - ((ground + 2 - c.Y - 6) * u), n % 5 != 0 ? sand : Pale, 255 * (1 - (u * 0.5)));
            }

            var edges = plaque.Old.Where(q => q.Edge).ToList();
            for (var n = 0; n < 20 && edges.Count > 0; n++)
            {
                var age = t - (Rays + (Rnd(n, 111) * 1.3));
                if (age < 0 || age > 1.1) continue;
                var q = edges[(n * 29) % edges.Count];
                Put(p, q.X + (Math.Sin((age * 8) + n) * 1.5), q.Y - (age * 40), Foam, 180 * (1 - (age / 1.1)));
            }

            // long rays of light out of the cracks, turning slowly, longer and brighter as it charges
            if (t > Rays)
            {
                var k = Clamp01((t - Rays) / (Burst - Rays));
                for (var n = 0; n < 14; n++)
                {
                    var ang = (n / 14.0 * 2 * Math.PI) + (t * 0.5) + (Rnd(n, 7) * 0.3);
                    var len = 10 + (k * 80 * (0.6 + (0.4 * Rnd(n, 9))));
                    var wid = n % 3 == 0 ? 2 : 1;
                    for (var s = 4; s < len; s++)
                    {
                        double x = ox + (Math.Cos(ang) * s), y = oy + (Math.Sin(ang) * s * 0.75), fade = (1 - (s / len)) * k;
                        for (var w = -wid + 1; w < wid; w++)
                        {
                            if (((R(x) + R(y)) & 1) == 0 || s < 14) Light(p, x - (Math.Sin(ang) * w), y + (Math.Cos(ang) * w), Pale, 140 * fade);
                        }
                    }
                }
            }

            // rings closing in on it: the light gathering for the blow
            if (t > Converge)
            {
                for (var n = 0; n < 3; n++)
                {
                    var u = (((t - Converge) / (Burst - Converge) * 1.6) + (n * 0.33)) % 1;
                    Ring(p, ox, oy, 70 * (1 - u), 0.7, n % 2 == 1 ? Gold : Pale, 200 * u);
                }
            }

            Shine(p, ox, oy, 16 + (10 * charge), 12 + (7 * charge), Gold, 40 + (180 * charge));

            // the old number: its cracks open from the middle and stay, widening and burning brighter until it bursts; the stone between them heats
            foreach (var pc in plaque.Pieces)
            {
                var sx = jolt + R((Rnd((pc.N * 3) + f, 1) - 0.5) * 2.8 * shiver);
                var sy = R((Rnd(f, pc.N * 5) - 0.5) * 2.2 * shiver * shiver);
                foreach (var q in pc.Cells)
                {
                    var open = t - q.Crack;
                    var colour = CapSheet.Mix(q.C, 0xFFE6A0u, 0.55 * Clamp01((t - 1.8) / 0.6));
                    if (q.Edge && open > 0) colour = CapSheet.Mix(Pale, White, 0.45 + (0.55 * ((Math.Sin((t * 38) + (q.X * 1.7)) * 0.5) + 0.5)));
                    else if (q.Edge && open > -0.2) colour = CapSheet.Mix(q.C, Gold, (open + 0.2) / 0.2);
                    p.Set(q.X + sx, q.Y + sy, colour);
                    if (q.Edge && open > 0.15)
                    {
                        foreach (var o in Round4)
                        {
                            if (!plaque.Edges.Contains((q.X + o.X, q.Y + o.Y))) Light(p, q.X + sx + o.X, q.Y + sy + o.Y, Gold, 70 * Math.Min(1, open) * (0.5 + charge));
                        }
                    }
                }
            }

            // chips falling off
            for (var n = 0; n < 18; n++)
            {
                var age = t - (1.0 + (Rnd(n, 61) * 1.3));
                if (age < 0) continue;
                var q = plaque.Old[(n * 53) % plaque.Old.Count];
                var y = Math.Min(ground, q.Y + (20 * age) + (70 * age * age));
                Put(p, q.X + ((Rnd(n, 63) - 0.5) * age * 12), y, y >= ground ? 0xC8A040u : CapSheet.Mix(q.C, Gold, 0.5));
            }
        }
        else
        {
            var s = t - Burst;

            // the burst: the window flashes, a shockwave bends the water, long rays, sparks
            if (s < 0.09) Flash(p, 210 * (1 - (s / 0.09)));
            if (s < 0.25)
            {
                Shine(p, ox, oy, 60, 44, White, 255 * (1 - (s / 0.25)));
                for (var n = 0; n < 24; n++)
                {
                    var ang = n / 24.0 * 2 * Math.PI;
                    var len = 20 + (s * 400);
                    Line(ox + (Math.Cos(ang) * 6), oy + (Math.Sin(ang) * 5), ox + (Math.Cos(ang) * len), oy + (Math.Sin(ang) * len * 0.75), (x, y) => Light(p, x, y, Pale, 230 * (1 - (s / 0.25))));
                }
            }

            if (s < 1.1) Lens(p, ox, oy, 6 + (s * 190), 3.5 * (1 - (s / 1.1)), 6, Pale);
            if (s > 0.15 && s < 1.2) Lens(p, ox, oy, 4 + ((s - 0.15) * 120), 2 * (1 - (s / 1.2)), 4, Gold);
            for (var n = 0; n < 60; n++)
            {
                var life = 0.5 + (Rnd(n, 71) * 0.7);
                if (s > life) continue;
                var ang = Rnd(n, 73) * 2 * Math.PI;
                var v = 50 + (Rnd(n, 75) * 90);
                Star(p, ox + (Math.Cos(ang) * v * s), oy + (Math.Sin(ang) * v * s * 0.7) + (45 * s * s), n % 3 != 0 ? Gold : White, 255 * (1 - (s / life)), n % 6 == 0 ? 1 : 0);
            }

            // embers rising from where it was
            for (var n = 0; n < 30; n++)
            {
                var age = s - (Rnd(n, 121) * 1.2);
                if (age < 0 || age > 1.8) continue;
                Light(p, ox + ((Rnd(n, 123) - 0.5) * 26) + (Math.Sin((age * 4) + n) * 3), oy - (age * 26), n % 3 != 0 ? Gold : Pale, 220 * (1 - (age / 1.8)));
            }

            // the pieces: tumbling out hot, one bounce, then dust
            foreach (var pc in plaque.Pieces)
            {
                if (s < pc.Crumble)
                {
                    var fl = plaque.Fly(pc, s);
                    DrawPiece(p, plaque, pc, R(fl.X), R(fl.Y), fl.Ang, Math.Max(0, 1 - (s / 0.7)));
                    if (fl.Landed is { } landed && landed < 0.3)
                    {
                        for (var n = 0; n < 6; n++) Disc(p, fl.X + ((n - 2.5) * 2.4 * (1 + (landed * 4))), ground - (landed * 12) - (Rnd(n, pc.N) * 2), 2, 1.5, SandLight, 170 * (1 - (landed / 0.3)));
                    }

                    continue;
                }

                var age = s - pc.Crumble;
                var fade = 1 - Clamp01((t - Beam - 0.2) / 0.9);
                if (fade > 0)
                {
                    for (var n = 0; n < pc.Cells.Count; n += 2)
                    {
                        var (x, y) = plaque.DustOf(pc.Cells[n], n);
                        Put(p, x, y - (age < 0.2 ? (0.2 - age) * 10 : 0), (((int)Math.Floor(t * 12) + n) % 9) == 0 ? White : CapSheet.Mix(Gold, 0xC08020u, Rnd(n, 3)), 230 * fade);
                    }
                }

                if (age < 0.25)
                {
                    var fl = plaque.Fly(pc, pc.Crumble);
                    for (var n = 0; n < 6; n++) Star(p, fl.X + ((n - 2.5) * 2 * (1 + (age * 4))), fl.Y - (age * 14), Gold, 220 * (1 - (age / 0.25)));
                }
            }

            // the beam: a column of gold light comes down from the surface onto the plaque and calls the dust back up into the orbs
            var beamK = t < Beam ? 0 : t < Beam + 0.35 ? Ease((t - Beam) / 0.35) : t < Reveal ? 1 : 1 - Ease((t - Reveal) / 0.8);
            if (beamK > 0)
            {
                var front = surface + ((ground - surface) * Clamp01((t - Beam) / 0.35));
                for (var y = surface + 1; y < front; y++)
                {
                    for (var dx = -12; dx <= 12; dx++)
                    {
                        var k = Math.Pow(1 - (Math.Abs(dx) / 12.5), 1.6) * beamK * (0.8 + (0.2 * Math.Sin((y * 0.5) - (t * 9))));
                        if (((R(c.X) + dx + y) & 1) == 0 || Math.Abs(dx) < 4) Light(p, c.X + dx, y, Gold, 130 * k);
                    }
                }

                if (t < Beam + 0.45) Shine(p, c.X, surface + 1, 22, 4, Pale, 220 * (1 - ((t - Beam) / 0.45)));
                for (var n = 0; n < 20; n++)
                {
                    var age = ((t * 0.9) + Rnd(n, 131)) % 1;
                    Light(p, c.X + ((Rnd(n, 133) - 0.5) * 18), surface + 4 + (age * (ground - surface - 8)), White, 160 * beamK * Math.Sin(Math.PI * age));
                }
            }

            foreach (var m in plaque.Motes)
            {
                if (t < m.From || t > m.Arrive) continue;
                var (x, y) = MoteAt(plaque, m, t);
                var (px, py) = MoteAt(plaque, m, Math.Max(m.From, t - 0.03));
                Line(px, py, x, y, (lx, ly) => Light(p, lx, ly, Gold, 130));
                Put(p, x, y, White);
            }

            // the orbs swell as their dust comes in, then each slams into its digit: a flash, a ring, sparks, the window jolts
            for (var g = 0; g < plaque.Groups.Count; g++)
            {
                var (cells, gx, gy) = plaque.Groups[g];
                var slam = Slams[Math.Min(g, 1)];
                var mine = plaque.Motes.Where(m => m.Group == g).ToList();
                var fill = mine.Count(m => t >= m.Arrive) / (double)Math.Max(1, mine.Count);
                if (t < slam && fill > 0)
                {
                    var k = t > slam - 0.12 ? 1 - ((t - slam + 0.12) / 0.12 * 0.7) : 1;
                    var rr = (2 + (fill * 5)) * k;
                    Shine(p, gx, gy, rr * 3, rr * 2.4, Gold, 200);
                    Disc(p, gx, gy, rr, rr, White, 255);
                    for (var n = 0; n < 6; n++)
                    {
                        var ang = (n / 6.0 * 2 * Math.PI) + (t * 7 * (g == 1 ? -1 : 1));
                        Star(p, gx + (Math.Cos(ang) * rr * 1.8), gy + (Math.Sin(ang) * rr * 1.4), Pale, 220);
                    }
                }

                var d = t - slam;
                if (d < 0) continue;
                // the digit, white as it lands and then its own colour; the plaque writes it itself once the number is revealed
                if (t < Reveal + 0.35)
                {
                    var k = Clamp01(d / 0.35);
                    foreach (var q in cells) p.Set(q.X, q.Y, CapSheet.Mix(White, q.C, k));
                }

                if (d < 0.35)
                {
                    Shine(p, gx, gy, 22, 16, White, 230 * (1 - (d / 0.35)));
                    Ring(p, gx, gy, 4 + (d * 90), 0.7, Pale, 230 * (1 - (d / 0.35)));
                }

                if (d < 0.5)
                {
                    for (var n = 0; n < 16; n++)
                    {
                        var ang = Rnd(n, 141 + g) * 2 * Math.PI;
                        var v = 30 + (Rnd(n, 143) * 50);
                        Star(p, gx + (Math.Cos(ang) * v * d), gy + (Math.Sin(ang) * v * d * 0.7) + (40 * d * d), n % 2 == 1 ? Gold : White, 255 * (1 - (d / 0.5)), n % 5 == 0 ? 1 : 0);
                    }
                }

                if (d < 0.6) Lens(p, gx, gy, 4 + (d * 120), 2 * (1 - (d / 0.6)), 4, Pale);
            }

            // the reveal: the whole number flashes, a sun of rays turns behind the plaque, a ring, a shine over it, «+N»
            var r = t - Reveal;
            var mid = (x0 + x1) / 2.0;
            if (r > 0 && r < 1.6)
            {
                var k = Math.Pow(Math.Sin(Math.PI * Math.Min(1, r / 1.6)), 0.7);
                for (var n = 0; n < 16; n++)
                {
                    var ang = (n / 16.0 * 2 * Math.PI) + (r * 0.6);
                    var len = 40 + (50 * k);
                    for (var s2 = 10; s2 < len; s2++)
                    {
                        for (var w = -2; w <= 2; w++)
                        {
                            double x = c.X + (Math.Cos(ang) * s2) - (Math.Sin(ang) * w * (s2 / 30.0)), y = c.Y + (((Math.Sin(ang) * s2) + (Math.Cos(ang) * w * (s2 / 30.0))) * 0.8);
                            if (((R(x) + R(y)) & 1) == 0 && (x < board.Left - 1 || x > board.Right || y < board.Top - 1 || y > board.Bottom)) Light(p, x, y, n % 2 == 1 ? Gold : Pale, 120 * k * (1 - (s2 / len)));
                        }
                    }
                }
            }

            if (r > 0 && r < 0.2)
            {
                foreach (var q in plaque.New) p.Set(q.X, q.Y, White, (int)(230 * (1 - (r / 0.2))));
                Shine(p, mid, oy, 26, 15, White, 180 * (1 - (r / 0.2)));
            }

            if (r > 0 && r < 0.8)
            {
                Ring(p, mid, oy, 10 + (r * 110), 0.6, Pale, 230 * (1 - (r / 0.8)));
                Lens(p, mid, oy, 8 + (r * 160), 2.5 * (1 - (r / 0.8)), 5, Gold);
            }

            if (r > 0.3 && r < 0.8)
            {
                var band = x0 - 8 + ((r - 0.3) / 0.5 * (x1 - x0 + 18));
                foreach (var q in plaque.New)
                {
                    var g = q.X + ((q.Y - oy) * 0.6) - band;
                    if (Math.Abs(g) < 2.2) Put(p, q.X, q.Y, White, 240 * (1 - (Math.Abs(g) / 2.2)));
                }
            }

            if (r > 0 && r < 1.1)
            {
                for (var n = 0; n < 22; n++)
                {
                    var ang = Rnd(n, 81) * 2 * Math.PI;
                    var rad = 6 + (r * (40 + (Rnd(n, 83) * 40)));
                    Star(p, mid + (Math.Cos(ang) * rad), oy + (Math.Sin(ang) * rad * 0.6), Gold, 255 * (1 - (r / 1.1)), n % 4 == 0 ? 2 : 0);
                }
            }

            if (r > 0.1 && r < 2.2)
            {
                var k = (r - 0.1) / 2.1;
                var text = "+" + (turn.To - turn.From).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var a = 255 * (k < 0.75 ? Math.Min(1, k * 6) : 1 - ((k - 0.75) / 0.25));
                var tw = CapGlyphs.Width(text, true);
                var ty = R(board.Top - 9 - (EaseOut(k) * 22));
                Shine(p, c.X, ty + 3, (tw / 2.0) + 8, 9, Gold, a * 0.6);
                CapGlyphs.Outlined(p, text, R(c.X - (tw / 2.0)), ty, Gold, 0x1A0F08u, true, 1, (int)a);
            }

            // a wave of light climbs the party from the plaque (each level it passes turns from gold to white: the rows are drawn so)
            if (r > 0.35)
            {
                var front = ground - ((r - 0.35) * 200);
                if (front > 50)
                {
                    for (var y = Math.Max(0, R(front) - 4); y <= R(front) + 4; y++)
                    {
                        for (var x = 0; x < l.Cols; x++)
                        {
                            if (((x + y) & 1) == 0 || Math.Abs(y - front) < 1) Light(p, x, y, Gold, 130 * (1 - (Math.Abs(y - front) / 5)));
                        }
                    }

                    for (var n = 0; n < 10; n++) Star(p, l.Main.Left + (Rnd(n, (int)front) * l.Main.Width), front + ((Rnd((int)front, n) - 0.5) * 6), Pale, 200);
                }

                foreach (var creature in l.Creatures)
                {
                    var levelY = creature.Info.Top + 3;
                    var a = (levelY - front) / 60;
                    if (a <= 0 || a >= 0.6) continue;
                    for (var n = 0; n < 4; n++) Star(p, l.Cols - 40 + (Rnd(n, levelY) * 36), levelY - 4 + (Rnd(levelY, n) * 10) - (a * 8), Gold, 255 * (1 - (a / 0.6)), n == 0 ? 2 : 0);
                }
            }
        }

        // the jolts: the first knock, the rumble that builds, the burst, each digit landing, the reveal
        Shake(p, t, 1.5, 0.3);
        if (t > Rise && t < Burst) Shake(p, t - Rise, 1.4, Burst - Rise, true);
        Shake(p, t - Burst, 4.5, 0.5);
        foreach (var slam in Slams) Shake(p, t - slam, 2.5, 0.25);
        Shake(p, t - Reveal, 3, 0.35);
    }

    private static (double X, double Y) MoteAt(Plaque plaque, Plaque.Mote m, double t)
    {
        var u = Clamp01((t - m.From) / (m.Arrive - m.From));
        var e = Ease(u);
        var (_, gx, gy) = plaque.Groups[m.Group];
        return (m.Sx + ((gx - m.Sx) * e) + (Math.Sin(u * Math.PI * 3) * 9 * (1 - u) * m.Side), m.Sy + ((gy - m.Sy) * e) - (Math.Sin(u * Math.PI) * 14));
    }

    private static void DrawPiece(CapSheet p, Plaque plaque, Plaque.Piece pc, int x, int y, double ang, double heat)
    {
        double cs = Math.Cos(ang), sn = Math.Sin(ang);
        var r = (int)Math.Ceiling(pc.Radius);
        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (!pc.Map.TryGetValue((R((dx * cs) + (dy * sn)), R((-dx * sn) + (dy * cs))), out var q)) continue;
                // hot from the burst: white, then gold, then its own colour; its cracks glow longest
                var k = plaque.Edges.Contains((q.X, q.Y)) ? Math.Min(1, heat * 1.6) : heat;
                p.Set(x + dx, y + dy, k > 0.5 ? CapSheet.Mix(Gold, White, (k - 0.5) * 2) : CapSheet.Mix(q.C, Gold, k * 2));
            }
        }
    }

    private static void Frame(CapSheet p, CapRect box, uint c, double a)
    {
        for (var x = box.Left; x < box.Right; x++)
        {
            Put(p, x, box.Top, c, a);
            Put(p, x, box.Bottom - 1, c, a);
        }

        for (var y = box.Top; y < box.Bottom; y++)
        {
            Put(p, box.Left, y, c, a);
            Put(p, box.Right - 1, y, c, a);
        }
    }
}
