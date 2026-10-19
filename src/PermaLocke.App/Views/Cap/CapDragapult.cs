using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The tenth trial's Z-Crystal, the Dracostal, brought by Dragapult and its Dragon Darts as a railgun (2026-10-19, fourth prototype: the player's
/// choice among new ideas, their sprites of Dragapult and of a chibi Dreepy). Its own film, starting <see cref="Lead"/> seconds before the film's
/// clock:
/// <list type="number">
/// <item>Dragapult swoops in from the left out of nothing, as ghosts do, a train of fading afterimages behind it, and hangs over the ruins;</item>
/// <item>the charge: the Dreepy in its horn spins faster and faster, rings of dragon energy light up one after another along a barrel from the
/// horn towards the arch, sparks arc between them and the water round is drawn into the horn;</item>
/// <item>a moment of silence: the water dims, everything holds still, the rings burn white;</item>
/// <item>it fires: in a single frame the Dreepy is in the arch with the crystal, leaving a dead straight line of light across the abyss (white,
/// teal, violet) that fades slowly from the horn's end; the shock wave comes after along it, rings bursting out of the line one after another
/// and bubbles streaming, the window shaking hard, Dragapult thrown back;</item>
/// <item>Dragapult glides off to the right, fading.</item>
/// </list>
/// For the rest of the run the back of the arch has a clean round hole shot through it, its rim turned to glass (glowing hot at first), and the
/// Dreepy lives in the arch with the crystal: dizzy at first, stars going round its head, then peeking out now and then (<see cref="PaintRest"/>).
/// </summary>
public static class CapDragapult
{
    /// <summary>The tenth trial's crystal.</summary>
    public const int Dracostal = 821;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 4.5;

    private const int Art = 887, Dart = 885;

    /// <summary>The arch of the risen storey it lands in (the player's choice).</summary>
    private const int ArchIndex = 4;

    private const double Lying = 0.4, Size = 0.85, DartSize = 0.6, NestSize = 0.55;

    // the shot is the film's own moment of impact
    private const double In = -4.0, Stop = 0.0, Charge = 0.8, Hush = 4.17, Hit = CapTrial.Hit, Fade = 1.8, Off = 6.8, Gone = 9.2;

    private const int Coils = 5;

    private const uint Trail = 0x7AD4CBu, TrailHi = 0xC5E5E3u, TrailLo = 0x4BA9AEu, Ghost = 0x8A5AA6u, GhostHi = 0xC8A0E8u, White = 0xFFFFFFu,
        Ink = 0x140A20u, Hush0 = 0x06101Cu, Deep = 0x081830u, DeepHi = 0x123058u, Glass = 0x9FE8E0u, GlassLo = 0x5AB8B0u, GlassHi = 0xE8FFFFu,
        Molten = 0xFFE8A0u, MoltenLo = 0xF0A040u, Star = 0xFFE45Au;

    /// <summary>The cracks round the hole, as where each ends from its middle (they start at its rim).</summary>
    private static readonly (int X, int Y)[] Cracks = [(-4, -5), (5, -3), (-5, 2), (4, 4), (0, -6)];

    /// <summary>Where the crystal lies: on the floor of one of the risen storey's arches; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _)
            ? (x0 + CapGimmighoulRuins.Arches[ArchIndex] + (CapGimmighoulRuins.ArchW / 2),
               floor + CapGimmighoulRuins.Storey - CapGimmighoulRuins.Plinth - R(CapTrialCrystal.H * Lying))
            : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel != CapPanelKind.Deep || CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, width) || Spot(l, ctx) is not { } spot) return;
        if (!CritterArt.All.TryGetValue(Art, out var art) || !CritterArt.All.TryGetValue(Dart, out var dart)) return;
        var clock = t + Lead;
        var hover = (X: spot.X - 64.0, Y: floor - 92.0);
        // the horn the Dreepy rides in, from the drawing's middle (turned: it faces right), and the end of the shot
        var horn = (X: hover.X + (22 * Size), Y: hover.Y - (25 * Size));
        var aim = (X: (double)spot.X, Y: spot.Y - 4.0);

        // the silence before the shot: the water dims
        if (t >= Hush && t < Hit + 0.3) Dim(p, l, t < Hit ? Span(Hush, Hit, t) : 1 - ((t - Hit) / 0.3));

        if (t >= In && t < Gone) Glide(p, art, l, hover, t, clock);

        if (t >= Charge && t < Hit) Charging(p, dart, horn, aim, t, clock);

        if (t >= Hit)
        {
            var since = t - Hit;
            Shot(p, horn, aim, since, clock);
            Shock(p, horn, aim, since);
            Rest(p, dart, spot, floor, crystal, since, clock);
            StarAt(p, horn.X, horn.Y, since, 0.22, 10);
            StarAt(p, aim.X, aim.Y, since, 0.35, 18);
            Debris(p, aim, since);
            Grit(p, x0, floor, width, since);
            if (since < 1.0) CapTrial.Shake(p, since, 4, 1.0);
        }

        ImpactFrames(p, t - Hit);
    }

    /// <summary>
    /// Dragapult: swooping in from the left out of nothing with a train of fading afterimages, hanging there (thrown back hard by the shot), and
    /// gliding off to the right, fading.
    /// </summary>
    private static void Glide(CapSheet p, CritterArt art, CapWorldLayout l, (double X, double Y) hover, double t, double clock)
    {
        static (double X, double Y, double Keep) At(double t, (double X, double Y) hover, int cols)
        {
            if (t < Stop)
            {
                var k = Ease(Span(In, Stop, t));
                return (-50 + ((hover.X + 50) * k), hover.Y - 26 + (26 * k) + (Math.Sin(k * Math.PI * 2) * 10), Span(In, In + 1.8, t));
            }

            if (t < Off) return (hover.X, hover.Y, 1);
            var o = Ease(Span(Off, Gone, t));
            return (hover.X + (o * (cols - hover.X + 60)), hover.Y - (o * 30), 1 - Span(Gone - 1.2, Gone, t));
        }

        // the afterimages while it moves, and while it is thrown back
        var thrown = t >= Hit && t < Hit + 0.5;
        if (t < Stop || t >= Off || thrown)
        {
            for (var k = 3; k >= 1; k--)
            {
                var back = t - (k * 0.1);
                var (ax, ay, ak) = At(back, hover, l.Cols);
                ax -= Kick(back - Hit);
                CapVisitors.Centred(p, art, clock - (k * 0.1), R(ax), R(ay), Size, true, Ghost, 0.55, 0, ak * (0.5 - (k * 0.12)));
            }
        }

        var (x, y, keep) = At(t, hover, l.Cols);
        // still while it charges, as if holding its breath in the silence
        if (t >= Stop && t < Hush) y += R(Math.Sin(clock * 1.6));
        var kick = Kick(t - Hit);
        CapVisitors.Centred(p, art, clock, R(x - kick), R(y + (kick * 0.3)), Size, true, 0xFFFFFF, 0, -kick * 1.5, keep);
    }

    /// <summary>How far the shot throws it back, a moment after: a hard jolt easing back.</summary>
    private static double Kick(double since) => since < 0 || since > 0.8 ? 0 : 9 * Math.Pow(1 - (since / 0.8), 2);

    /// <summary>
    /// The charge: the Dreepy spinning in the horn, faster and faster; the rings of the barrel lighting up one after another towards the arch,
    /// sparks arcing between them, the water drawn into the horn; in the silence everything holds and the rings burn white.
    /// </summary>
    private static void Charging(CapSheet p, CritterArt dart, (double X, double Y) horn, (double X, double Y) aim, double t, double clock)
    {
        var k = Span(Charge, Hush, t);
        var hush = t >= Hush;
        double dx = aim.X - horn.X, dy = aim.Y - horn.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;

        // the water drawn in, until the silence: streaks of light curling into the horn, each a head and a fading tail
        if (!hush)
        {
            for (var n = 0; n < 20; n++)
            {
                var run = ((clock * (0.8 + k)) + (n * 0.131)) % 1;
                var a = n * 0.314;
                for (var s = 0; s < 4; s++)
                {
                    var rr = 34 * (1 - run) + (s * 1.5);
                    var aa = a + (run * 2) - (s * 0.05);
                    Put(p, horn.X + (Math.Cos(aa) * rr), horn.Y + (Math.Sin(aa) * rr * 0.8), s == 0 ? (n % 3 == 0 ? White : TrailHi) : s == 1 ? Trail : TrailLo);
                }
            }
        }

        // the sight: a dashed line of light from the horn to the arch, its dashes running down it, steady in the silence
        if (k > 0.15)
        {
            var march = hush ? 0 : (clock * 30) % 6;
            for (var d = 0.0; d < len; d += 0.5)
            {
                if (((d + march) % 6) > 3) continue;
                Put(p, horn.X + (ux * d), horn.Y + (uy * d), hush ? GhostHi : k > 0.7 ? Trail : TrailLo);
            }
        }

        // the orb of dragon energy swelling at the horn
        Orb(p, horn.X, horn.Y, 1 + (k * 4), clock, hush);

        // the barrel: rings across the line of the shot, each lighting up in its turn, the light running down them faster as it charges
        var barrel = Math.Min(44, len * 0.4);
        for (var i = 0; i < Coils; i++)
        {
            if (k < (i + 0.5) / (Coils + 1)) continue;
            var along = 12 + (i * (barrel - 12) / (Coils - 1));
            double cx = horn.X + (ux * along), cy = horn.Y + (uy * along);
            var lit = ((clock * (3 + (k * 12))) - (i * 0.6)) % 1;
            var c = hush ? White : lit < 0.25 ? White : lit < 0.6 ? TrailHi : i % 2 == 0 ? Trail : GhostHi;
            Coil(p, cx, cy, ux, uy, nx, ny, 5 + (i * 0.6), c);

            // a spark arcing to the next ring now and then
            if (!hush && i < Coils - 1 && k >= (i + 1.5) / (Coils + 1) && ((int)(clock * 15) + i) % 3 == 0)
            {
                var next = 12 + ((i + 1) * (barrel - 12) / (Coils - 1));
                var side = (((int)(clock * 15) + i) % 2 == 0 ? 1 : -1) * (4 + (i * 0.6));
                Arc(p, cx + (nx * side), cy + (ny * side), horn.X + (ux * next) + (nx * side), horn.Y + (uy * next) + (ny * side), (int)(clock * 15) + i);
            }
        }

        // the Dreepy spinning in the horn: turned and flipped faster and faster, curved streaks round it, still in the silence
        var spin = hush ? 0 : (int)(clock * (4 + (k * 26)));
        var tilt = hush ? 0 : ((spin % 4) - 1.5) * 40;
        if (!hush && k > 0.3)
        {
            var turn = clock * (6 + (k * 20));
            for (var arc = 0; arc < 3; arc++)
            {
                for (var a = 0.0; a < 1.1; a += 0.12)
                {
                    var aa = turn + (arc * 2.094) + a;
                    Put(p, horn.X + (Math.Cos(aa) * 9), horn.Y + (Math.Sin(aa) * 7), a < 0.4 ? White : a < 0.8 ? TrailHi : Trail);
                }
            }
        }

        CapVisitors.Centred(p, dart, clock, R(horn.X), R(horn.Y), DartSize, spin % 2 == 0, 0xFFFFFF, 0, tilt);
    }

    /// <summary>
    /// A ring of the barrel, seen side on across the line of the shot: a band two cells thick outlined in ink, its near half lit and its far half
    /// in shade, four dark notches round it like runes, and a spark of its own light running round it.
    /// </summary>
    private static void Coil(CapSheet p, double cx, double cy, double ux, double uy, double nx, double ny, double r, uint c)
    {
        var shade = CapSheet.Mix(c, Ink, 0.45);
        var lit = CapSheet.Mix(c, White, 0.45);
        // the ink first, all round a little outside and inside the band; then the band over it
        for (var pass = 0; pass < 2; pass++)
        {
            for (var a = 0.0; a < Math.PI * 2; a += 0.04)
            {
                var near = Math.Sin(a) > 0;
                var notch = Math.Abs(Math.IEEERemainder(a - (Math.PI / 4), Math.PI / 2)) < 0.1;
                if (pass == 0)
                {
                    foreach (var w in Rims) Put(p, cx + (nx * Math.Cos(a) * (r + w)) + (ux * Math.Sin(a) * (2 + (w * 0.6))), cy + (ny * Math.Cos(a) * (r + w)) + (uy * Math.Sin(a) * (2 + (w * 0.6))), Ink);
                    continue;
                }

                for (var w = 0.0; w <= 1.0; w += 0.5)
                {
                    var col = notch ? Ink : near ? (w < 0.5 ? lit : c) : shade;
                    Put(p, cx + (nx * Math.Cos(a) * (r + w)) + (ux * Math.Sin(a) * (2 + (w * 0.6))), cy + (ny * Math.Cos(a) * (r + w)) + (uy * Math.Sin(a) * (2 + (w * 0.6))), col);
                }
            }
        }
    }

    /// <summary>How far outside and inside the band of a ring its ink outline is drawn.</summary>
    private static readonly double[] Rims = [-1.0, 2.0];

    /// <summary>The orb at the horn: an ink edge, teal, a pale throbbing heart; white all through in the silence.</summary>
    private static void Orb(CapSheet p, double x, double y, double r, double clock, bool hush)
    {
        var beat = r + (!hush && Math.Sin(clock * 22) > 0 ? 0.6 : 0);
        for (var dy = -(int)beat - 2; dy <= beat + 2; dy++)
        {
            for (var dx = -(int)beat - 2; dx <= beat + 2; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > beat + 1) continue;
                Put(p, x + dx, y + dy, d > beat ? Ink : hush ? White : d > beat * 0.65 ? Trail : d > beat * 0.3 ? TrailHi : White);
            }
        }
    }

    /// <summary>A star of solid rays flashing out of a point and gone: white, teal at the tips, every other ray shorter.</summary>
    private static void StarAt(CapSheet p, double x, double y, double age, double span, double size)
    {
        if (age < 0 || age > span) return;
        var k = age / span;
        var len = size * (k < 0.3 ? k / 0.3 : 1 - ((k - 0.3) / 0.7));
        for (var n = 0; n < 8; n++)
        {
            var a = (n * Math.PI / 4) + 0.39;
            var l = n % 2 == 0 ? len : len * 0.5;
            for (var d = 0; d < l; d++) Put(p, x + (Math.Cos(a) * d), y + (Math.Sin(a) * d), d < l * 0.5 ? White : n % 2 == 0 ? TrailHi : GhostHi);
        }
    }

    /// <summary>Chips of stone and puffs of dust blown out of the arch by the hit, falling back.</summary>
    private static void Debris(CapSheet p, (double X, double Y) at, double age)
    {
        if (age > 1.4) return;
        for (var n = 0; n < 18; n++)
        {
            var a = -Math.PI * (0.05 + (Rnd(n, 111) * 0.9));
            var v = 18 + (Rnd(n, 113) * 34);
            double x = at.X + (Math.Cos(a) * v * age), y = at.Y + (Math.Sin(a) * v * age) + (55 * age * age);
            if (n % 3 == 0)
            {
                // a puff of dust, shrinking
                var r = Math.Max(0, 2.5 - (age * 2));
                for (var dy = -(int)r; dy <= r; dy++) for (var dx = -(int)r; dx <= r; dx++) if ((dx * dx) + (dy * dy) <= r * r) Put(p, x + dx, y + dy, 0x7C7C70u);
                continue;
            }

            Put(p, x, y, 0x9C9C8Cu);
            Put(p, x + 1, y, 0x5C5C52u);
            Put(p, x, y + 1, 0x3A3A34u);
        }
    }

    /// <summary>Grit shaken off the tops of the ruins' walls by the shot, falling in short streaks.</summary>
    private static void Grit(CapSheet p, int x0, int floor, int width, double age)
    {
        if (age > 1.6) return;
        for (var n = 0; n < 24; n++)
        {
            var rx = (int)(Rnd(n, 121) * width);
            var top = floor - CapGimmighoulRuins.Surface(rx);
            var start = Rnd(n, 123) * 0.5;
            var fall = age - start;
            if (fall < 0 || CapGimmighoulRuins.Surface(rx) == 0) continue;
            var y = top + (fall * fall * 90);
            Put(p, x0 + rx, y, 0x8C8C7Cu);
            Put(p, x0 + rx, y - 1, 0x5C5C52u);
        }
    }

    /// <summary>A short crackling spark between two points: a path broken at random every frame, a cell or two off the straight.</summary>
    private static void Arc(CapSheet p, double x0, double y0, double x1, double y1, int seed)
    {
        var steps = (int)Math.Max(4, Math.Abs(x1 - x0) + Math.Abs(y1 - y0));
        for (var s = 0; s <= steps; s++)
        {
            var u = s / (double)steps;
            var off = s == 0 || s == steps ? 0 : ((int)(CapArt.Hash(s + (seed * 7), seed) % 3)) - 1;
            Put(p, x0 + ((x1 - x0) * u) + off, y0 + ((y1 - y0) * u) - off, s % 3 == 0 ? White : TrailHi);
        }
    }

    /// <summary>The water dimmed for the silence before the shot, flat.</summary>
    private static void Dim(CapSheet p, CapWorldLayout l, double k)
    {
        var a = (int)(130 * Clamp01(k));
        for (var y = l.SurfaceY + 2; y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, Hush0, a);
        }
    }

    /// <summary>
    /// The line the shot left: dead straight from the horn to the arch, white at its heart, teal, a violet edge; it narrows as it fades and goes
    /// first from the horn's end, slowly.
    /// </summary>
    private static void Shot(CapSheet p, (double X, double Y) horn, (double X, double Y) aim, double since, double clock)
    {
        if (since >= Fade) return;
        var k = since / Fade;
        double dx = aim.X - horn.X, dy = aim.Y - horn.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        var width = 3.0 * (1 - k);
        var start = len * Math.Pow(k, 1.5);
        for (var s = start; s <= len; s += 0.5)
        {
            double cx = horn.X + (ux * s), cy = horn.Y + (uy * s);
            for (var o = -(int)Math.Ceiling(width) - 1; o <= width + 1; o++)
            {
                var d = Math.Abs(o);
                var c = d > width ? Ghost : d > width - 1 ? Trail : d > width - 2 ? TrailHi : White;
                Put(p, cx + (nx * o), cy + (ny * o), c);
            }

            // two thin strands of violet and teal coiling round it, fading with it
            if (k < 0.7)
            {
                for (var strand = 0; strand < 2; strand++)
                {
                    var off = Math.Sin((s * 0.35) - (clock * 12) + (strand * Math.PI)) * (width + 2.5);
                    Put(p, cx + (nx * off), cy + (ny * off), strand == 0 ? GhostHi : TrailHi);
                }
            }
        }
    }

    /// <summary>The shock wave coming after along the line: rings bursting out of it one after another from the horn to the arch, bubbles streaming.</summary>
    private static void Shock(CapSheet p, (double X, double Y) horn, (double X, double Y) aim, double since)
    {
        double dx = aim.X - horn.X, dy = aim.Y - horn.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        for (var s = 6.0; s < len; s += 9)
        {
            var age = since - 0.06 - (s / len * 0.45);
            if (age < 0 || age > 0.5) continue;
            var r = 2 + (age * 22);
            double cx = horn.X + (ux * s), cy = horn.Y + (uy * s);
            var c = age < 0.2 ? TrailHi : Trail;
            for (var a = 0.0; a < Math.PI * 2; a += 0.12)
            {
                double across = Math.Cos(a) * r, along = Math.Sin(a) * r * 0.35;
                Put(p, cx + (nx * across) + (ux * along), cy + (ny * across) + (uy * along), c);
                double ia = Math.Cos(a) * (r - 1.2), il = Math.Sin(a) * (r - 1.2) * 0.35;
                Put(p, cx + (nx * ia) + (ux * il), cy + (ny * ia) + (uy * il), TrailLo);
            }

            // streaks of water pushed out sideways
            for (var w = 0; w < 2; w++)
            {
                var side = w == 0 ? 1 : -1;
                for (var d = 0; d < 3; d++) Put(p, cx + (nx * side * (r + 2 + d)), cy + (ny * side * (r + 2 + d)), d == 0 ? White : 0xCFF4FFu);
            }

            // bubbles shaken out of the water by it, rising
            for (var b = 0; b < 2; b++)
            {
                var side = (b == 0 ? 1 : -1) * (r + 1);
                Put(p, cx + (nx * side), cy + (ny * side) - (age * 16), 0xCFF4FFu);
            }
        }
    }

    /// <summary>Two impact frames (a twentieth of a second each): the scene in white and ink, then in teal and ink.</summary>
    private static void ImpactFrames(CapSheet p, double age)
    {
        if (age < 0 || age >= 0.1) return;
        var frame = (int)(age / 0.05);
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (!p.Solid(x, y)) continue;
                var c = p.At(x, y);
                var lum = ((((c >> 16) & 255) * 3) + (((c >> 8) & 255) * 6) + (c & 255)) / 10;
                p.Set(x, y, frame == 0 ? (lum > 96 ? Ink : White) : lum > 96 ? Trail : Ink);
            }
        }
    }

    private static double Ease(double k) => k * k * (3 - (2 * k));

    /// <summary>
    /// The arch after the shot: a clean round hole through the wall at its back, the water behind showing, its rim turned to glass (glowing hot at
    /// first and cooling); the crystal; and the Dreepy, dizzy at first with stars going round its head, then peeking out now and then.
    /// </summary>
    private static void Rest(CapSheet p, CritterArt dart, (int X, int Y) spot, int floor, CapTrialCrystal crystal, double since, double clock)
    {
        var foot = floor + CapGimmighoulRuins.Storey;
        int hx = spot.X, hy = foot - 11;
        var hot = Clamp01(1 - (since / 3));
        for (var dy = -6; dy <= 6; dy++)
        {
            for (var dx = -6; dx <= 6; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 5.4) continue;
                uint c;
                if (d <= 3.9) c = (dx + dy) % 5 == 0 && d < 2.5 ? DeepHi : Deep;
                else if (hot > 0.3) c = d > 4.8 ? MoltenLo : Molten;
                else c = dx + dy < -3 ? GlassHi : dx + dy > 3 ? GlassLo : Glass;
                p.Set(hx + dx, hy + dy, c);
            }
        }

        // cracks running out from the hole across the back of the arch
        foreach (var (cx, cy) in Cracks)
        {
            var steps = Math.Max(Math.Abs(cx), Math.Abs(cy));
            for (var s = 0; s <= steps; s++)
            {
                var u = s / (double)steps;
                var kink = s == steps / 2 ? 1 : 0;
                p.Set(hx + (int)Math.Round(cx * u * (1 + (5.6 / steps))) + kink, hy + (int)Math.Round(cy * u * (1 + (5.6 / steps))), 0x1A1612u);
            }
        }

        // drops of molten glass running down from the rim, cooling as they go
        for (var d = 0; d < 3; d++)
        {
            var run = Math.Min(4, since * (2 + d));
            var dx = (d - 1) * 3;
            for (var s = 0; s <= run; s++) p.Set(hx + dx, hy + 5 + (int)s, hot > 0.3 ? (s >= run - 1 ? Molten : MoltenLo) : s >= run - 1 ? GlassHi : Glass);
        }

        // steam rising off it while it is hot
        if (hot > 0)
        {
            for (var n = 0; n < 6; n++)
            {
                var age = ((clock * 0.9) + (n / 6.0)) % 1;
                Put(p, hx - 4 + (n * 1.6) + (Math.Sin((age * 6) + n) * 1.2), hy - 6 - (age * 14), age < 0.5 ? 0xE8F4FFu : 0xA8C4D8u, 255 * hot);
            }
        }

        var glint = clock % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);

        // dizzy just after: over the crystal, stars going round its head
        if (since < 3)
        {
            var wobble = Math.Sin(clock * 7) * 8;
            CapVisitors.Centred(p, dart, clock, spot.X, spot.Y - 8, NestSize, (int)(clock * 3) % 2 == 0, 0xFFFFFF, 0, wobble, Clamp01((3 - since) / 0.4));
            for (var s = 0; s < 3; s++)
            {
                var a = (clock * 5) + (s * 2.094);
                double sx = spot.X + (Math.Cos(a) * 6), sy = spot.Y - 15 + (Math.Sin(a) * 2);
                Put(p, sx, sy, White);
                Put(p, sx - 1, sy, Star);
                Put(p, sx + 1, sy, Star);
                Put(p, sx, sy - 1, Star);
                Put(p, sx, sy + 1, Star);
            }

            return;
        }

        // then it peeks out every seven seconds
        var cycle = (since - 3) % 7;
        if (cycle > 3) return;
        var keep = Clamp01(cycle / 0.4) * Clamp01((3 - cycle) / 0.4);
        var right = cycle > 1.5;
        CapVisitors.Centred(p, dart, clock, spot.X + (right ? 1 : -1), spot.Y - 8 + R(Math.Sin(clock * 3)), NestSize, right, 0xFFFFFF, 0, 0, keep);
    }

    /// <summary>The Dracostal in its arch for the rest of the run, the hole through the arch's back, and the Dreepy that lives there.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (_, floor, _) || Spot(l, ctx) is not { } spot || !CritterArt.All.TryGetValue(Dart, out var dart)) return;
        Rest(p, dart, spot, floor, crystal, 30, phase);
    }
}
