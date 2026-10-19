using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The twelfth and last trial's Z-Crystal, the Geostal, brought by Groudon, Primal Reverted, with Precipice Blades (2026-10-19, second prototype:
/// the player's choice, their sprites of Groudon and Primal Groudon, and the Primal Reversion as Omega Ruby shows it). Its own film, starting
/// <see cref="Lead"/> seconds before the film's clock:
/// <list type="number">
/// <item>the seabed rumbles: glowing cracks run across the sand, bubbles rise; the sand bursts open and Groudon rises out of it, sand pouring off
/// it, and roars;</item>
/// <item>Primal Reversion, as in the game: it glows and bursts into pink rays; a sphere of magenta energy closes round it and turns to faceted
/// red crystal; the sphere goes black, a rim of violet and red light turning round it, and the Omega sign
/// appears on it, dark red and brightening to orange (a thick ring open at the bottom, its two feet arrowheads pointing out); the sphere is gone
/// and Groudon's shape stands dark with the sign burning red on its chest, white rays bursting behind; it bursts into fire: Primal Groudon, magma
/// flowing in its lines;</item>
/// <item>Precipice Blades: it roars and stamps; a glowing crack runs along the seabed ahead of a wave of blades bursting out of it one after
/// another, bigger and bigger, to the last arch of the ruins: clusters of faceted spires of volcanic rock with magma veins, as in the game, each
/// out of a fountain of molten drops and smoke; the last, smaller, rises in front of the last arch with the crystal on its tip;</item>
/// <item>the other blades crumble back into the sand, and Groudon sinks back into it.</item>
/// </list>
/// For the rest of the run the last blade stands in front of the last arch with the Geostal on its tip, the magma in it breathing (<see cref="PaintRest"/>).
/// </summary>
public static class CapGroudon
{
    /// <summary>The last trial's crystal.</summary>
    public const int Geostal = 815;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 8.6;

    private const int Art = 383, Primal = 10383;

    /// <summary>The arch the last blade rises in front of: the last one that shows (asked; the seventh is past the window's edge).</summary>
    private const int ArchIndex = 5;

    private const double Lying = 0.4, Size = 0.95, PrimalSize = 0.88, Ball = 38;

    /// <summary>How far below the middle of their sheets' cells the feet of Groudon and Primal Groudon are (measured on the straight frames).</summary>
    private const double GroudonFeet = 36, PrimalFeet = 37;

    /// <summary>How much lower Primal Groudon's middle is than Groudon's, so its feet stand on the same sand.</summary>
    private const double PrimalDrop = (GroudonFeet * Size) - (PrimalFeet * PrimalSize);

    private const double Rumble = -8.4, Emerge = -6.2, Up = -4.4, Roar = -4.2, Glow = -3.4, Energy = -2.8, Crystal = -2.3, Dark = -1.7,
        Sign = -1.3, Shape = -0.5, Fire = 0.0, Roar2 = 1.0, Stamp = 1.5, Wave = 1.7, Hit = CapTrial.Hit, Crumble = 5.2, Sink = 5.8, Down = 8.0;

    private const int Count = 7;

    private const uint White = 0xFFFFFFu, Ink = 0x14080Au, Glowing = 0xFF6A2Au, GlowHi = 0xFFD070u, Sand = 0xB8A47Cu, SandLo = 0x8A7A5Au,
        Magenta = 0xF040C0u, Omega = 0xFF8A20u, OmegaHi = 0xFFD860u, OmegaLo = 0xC84808u, OmegaRed = 0x9A1010u, Rim1 = 0xA040E0u, Rim2 = 0xE02040u,
        Vein = 0xFFB040u, Tip = 0xFFF0B0u, Smoke = 0x3A3030u,
        SmokeLo = 0x241C1Cu;

    private static readonly uint[] Bands = [0xFFFFFFu, 0xFFB0F0u, 0xF060D0u, 0xC030A0u, 0xFF80E0u];

    private static readonly uint[] Reds = [0x5A0A14u, 0x8E1420u, 0xC82434u, 0xF0505Au, 0xFF98A0u];

    private static readonly uint[] Flame = [0xFFF4C0u, 0xFFD060u, 0xFF8A20u, 0xE04010u, 0x902008u];

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    private static readonly (double X, double Y, double Z)[] Seeds = Spread(24);

    /// <summary>The scratch sheet Groudon is drawn into, to cut it at the sand or darken it to its shape (one per painting thread).</summary>
    [ThreadStatic]
    private static CapSheet? _scratch;

    private const int ScratchW = 150, ScratchH = 150;

    private static (double X, double Y, double Z)[] Spread(int n)
    {
        var points = new (double X, double Y, double Z)[n];
        var golden = Math.PI * (3 - Math.Sqrt(5));
        for (var i = 0; i < n; i++)
        {
            var y = 1 - ((i + 0.5) / n * 2);
            var r = Math.Sqrt(1 - (y * y));
            points[i] = (Math.Cos(golden * i) * r, y, Math.Sin(golden * i) * r);
        }

        return points;
    }

    /// <summary>The row of the sand in front of the ruins.</summary>
    private static int SandOf(int floor) => floor + CapGimmighoulRuins.Storey + 1;

    /// <summary>Where the last blade stands: in front of the last arch.</summary>
    private static double LastX(int x0) => x0 + CapGimmighoulRuins.Arches[ArchIndex] + (CapGimmighoulRuins.ArchW / 2) - 3;

    /// <summary>Where the crystal lies: on the tip of the last blade; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _)
            ? (R(LastX(x0) + (Height(Count - 1) * LastLean)), SandOf(floor) - Height(Count - 1) - R(CapTrialCrystal.H * Lying) + 1)
            : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel != CapPanelKind.Deep || CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _) || Spot(l, ctx) is not { } spot) return;
        if (!CritterArt.All.TryGetValue(Art, out var art) || !CritterArt.All.TryGetValue(Primal, out var primal)) return;
        var clock = t + Lead;
        var sand = SandOf(floor);
        // the sand's top in front of the ruins, and Groudon's middle where its feet stand on it (one cell into it)
        var ground = sand - 3;
        var stand = (X: x0 + 96.0, Y: ground + 1 - (GroudonFeet * Size));

        // the rumble: cracks glowing across the sand, bubbles, the window shaking
        if (t >= Rumble && t < Up) Cracks(p, sand, stand.X, Span(Rumble, Emerge, t) * (1 - Span(Emerge + 1, Up, t)), clock);
        if (t >= Rumble && t < Up) CapTrial.Shake(p, (t - Rumble) % 0.5, 1.5, 0.5);

        // the magma spreading over the sand round the last blade's foot, from the strike (under the blades)
        if (t >= Hit) Pool(p, LastX(x0) - 9, sand, Ease(Span(Hit, Hit + 1.8, t)), clock);

        // the blades (behind Groudon)
        if (t >= Wave) BladesAt(p, x0, stand.X, sand, t, clock);

        // Groudon: up out of the sand, then into the Reversion
        if (t >= Emerge && t < Energy + 0.2) Rising(p, art, stand, ground, t, clock);
        if (t >= Emerge && t < Up) SandBurst(p, stand.X, sand, t - Emerge);
        if (t >= Roar && t < Roar + 0.8) Roared(p, stand.X + 18, stand.Y - 16, t - Roar);

        // the Reversion
        if (t >= Glow && t < Energy + 0.3) Rays(p, stand, Span(Glow, Energy, t), clock);
        if (t >= Energy && t < Shape) Sphere(p, stand, t, clock);
        if (t >= Sign && t < Fire + 0.2) OmegaAt(p, stand.X, stand.Y - 2, t, clock);
        if (t >= Shape && t < Fire + 0.1) Silhouette(p, primal, stand, t, clock);
        if (t >= Fire && t < Fire + 1.6) Burst(p, stand, t - Fire, clock);

        // Primal Groudon (facing the ruins to its right)
        if (t >= Fire && t < Down)
        {
            if (t >= Sink) Cut(p, primal, stand.X, stand.Y + PrimalDrop + (Ease(Span(Sink, Down, t)) * 70), ground, PrimalSize, clock, 0, 0);
            else CapVisitors.Centred(p, primal, clock, R(stand.X), R(stand.Y + PrimalDrop), PrimalSize, true, White, 1 - Span(Fire, Fire + 0.7, t), 0);
            if (t >= Sink) SandBurst(p, stand.X, sand, (t - Sink) * 0.8);
        }

        if (t >= Roar2 && t < Roar2 + 0.8) Roared(p, stand.X + 22, stand.Y - 18, t - Roar2);
        if (t >= Stamp && t < Stamp + 0.6) CapTrial.Shake(p, t - Stamp, 3.5, 0.6);

        // the crystal riding up on the last blade
        var lastAt = BladeTime(Count - 1);
        if (t >= lastAt && t < Hit)
        {
            var rise = Rise(t - lastAt);
            crystal.Paint(p, R(LastX(x0) + (Height(Count - 1) * rise * LastLean)), R(sand - (Height(Count - 1) * rise) - (CapTrialCrystal.H * Lying) + 1), Lying, 0, 0.8, -1);
        }

        if (t >= Hit)
        {
            var since = t - Hit;
            Rest(p, crystal, spot, since, clock);
        }
    }

    // ---------------------------------------------------------------- Groudon

    /// <summary>Draws a sprite into the scratch sheet and copies it onto the window above the sand only, darkened to <paramref name="dark"/>.</summary>
    private static void Cut(CapSheet p, CritterArt art, double x, double y, int sand, double size, double clock, uint tint, double dark)
    {
        _scratch ??= new CapSheet(ScratchW, ScratchH);
        _scratch.Clear();
        CapVisitors.Centred(_scratch, art, clock, ScratchW / 2, ScratchH / 2, size, true, tint, dark, 0);
        int ox = R(x) - (ScratchW / 2), oy = R(y) - (ScratchH / 2);
        for (var sy = 0; sy < ScratchH; sy++)
        {
            var wy = oy + sy;
            if (wy > sand) break;
            for (var sx = 0; sx < ScratchW; sx++) if (_scratch.Solid(sx, sy)) p.Set(ox + sx, wy, _scratch.At(sx, sy));
        }
    }

    /// <summary>Groudon rising out of the sand, cut where the sand is, sand pouring off it; turning pink-white as the Reversion starts.</summary>
    private static void Rising(CapSheet p, CritterArt art, (double X, double Y) stand, int sand, double t, double clock)
    {
        var k = Ease(Span(Emerge, Up, t));
        var depth = (1 - k) * 64;
        Cut(p, art, stand.X, stand.Y + depth, sand, Size, clock, 0xFF60D0u, Span(Glow, Glow + 0.4, t) * 0.8);
        if (t < Up + 0.6)
        {
            for (var n = 0; n < 16; n++)
            {
                var fall = ((clock * 1.5) + (n / 16.0)) % 1;
                Put(p, stand.X - 24 + (n * 3), stand.Y + depth - 20 + (fall * 30), n % 2 == 0 ? Sand : SandLo);
            }
        }
    }

    /// <summary>Cracks of red light running across the sand from where it will come up, wider as the rumble grows; bubbles out of them.</summary>
    private static void Cracks(CapSheet p, int sand, double from, double k, double clock)
    {
        if (k <= 0) return;
        for (var c = 0; c < 6; c++)
        {
            var dir = c % 2 == 0 ? -1 : 1;
            var reach = (20 + (c * 10)) * k;
            var y = sand + 1 + (c % 3);
            for (var s = 0; s < reach; s++)
            {
                var x = from + (dir * s);
                var jog = (int)(CapArt.Hash((int)x, c) % 3) - 1;
                Put(p, x, y + jog, s > reach - 3 ? GlowHi : ((clock * 3) + (s * 0.1)) % 1 < 0.5 ? Glowing : 0xC03018u);
            }
        }

        for (var n = 0; n < 8; n++)
        {
            var age = ((clock * 0.8) + (n / 8.0)) % 1;
            Put(p, from + ((Rnd(n, 221) - 0.5) * 80 * k), sand - (age * 40), 0xCFF4FFu);
        }
    }

    /// <summary>The sand bursting up: a fountain of sand and stones, falling back.</summary>
    private static void SandBurst(CapSheet p, double x, int sand, double age)
    {
        if (age < 0 || age > 1.6) return;
        for (var n = 0; n < 34; n++)
        {
            var a = -Math.PI * (0.15 + (Rnd(n, 231) * 0.7));
            var v = 26 + (Rnd(n, 233) * 34);
            double sx = x + ((Rnd(n, 235) - 0.5) * 40) + (Math.Cos(a) * v * age), sy = sand + (Math.Sin(a) * v * age) + (45 * age * age);
            if (sy > sand + 2) continue;
            Put(p, sx, sy, n % 4 == 0 ? 0x6E5C50u : n % 2 == 0 ? Sand : SandLo);
            if (n % 3 == 0) Put(p, sx + 1, sy, SandLo);
        }
    }

    /// <summary>A roar: rings of sound out of its mouth, the window shaking.</summary>
    private static void Roared(CapSheet p, double x, double y, double age)
    {
        for (var c = 0; c < 3; c++)
        {
            var a = age - (c * 0.16);
            if (a > 0 && a < 0.5) CapTrial.Ring(p, x, y, 4 + (a * 50), 0.8, c == 1 ? GlowHi : White, 220, false);
        }

        CapTrial.Shake(p, age, 2.5, 0.8);
    }

    // ---------------------------------------------------------------- the Reversion

    /// <summary>The glow before the sphere: pink rays bursting out of it, turning, more and longer.</summary>
    private static void Rays(CapSheet p, (double X, double Y) at, double k, double clock)
    {
        for (var n = 0; n < 18; n++)
        {
            var a = (n * Math.PI / 9) + (clock * 0.8);
            var len = (20 + ((n % 3) * 12)) * k;
            for (var d = 8; d < 8 + len; d++) Put(p, at.X + (Math.Cos(a) * d), at.Y + (Math.Sin(a) * d * 0.85), d < 8 + (len / 3) ? White : n % 2 == 0 ? Magenta : 0xFFB0F0u);
        }

        if (k > 0.9) CapTrial.Flash(p, 100);
    }

    /// <summary>
    /// The sphere: magenta energy, then faceted red crystal (each facet its tone by its turn, a line of light along its edges), then black, a rim
    /// of violet and red light turning round it.
    /// </summary>
    private static void Sphere(CapSheet p, (double X, double Y) at, double t, double clock)
    {
        var born = Clamp01((t - Energy) / 0.3);
        var rs = Ball * (0.5 + (0.5 * born));
        var crystal = Span(Crystal, Crystal + 0.5, t);
        var dark = Span(Dark, Dark + 0.4, t);
        var spin = clock * 0.8;
        double co = Math.Cos(spin), si = Math.Sin(spin);
        var r = (int)Math.Ceiling(rs) + 3;
        for (var y = -r; y <= r; y++)
        {
            for (var x = -r; x <= r; x++)
            {
                double nx = (x + 0.5) / rs, ny = (y + 0.5) / rs, d2 = (nx * nx) + (ny * ny);
                int px = R(at.X) + x, py = R(at.Y) + y;
                var d = Math.Sqrt(d2);
                if (d > 1)
                {
                    // the rim of light round the black sphere, in turning arcs
                    if (dark > 0 && d < 1 + (2.5 / rs))
                    {
                        var arc = Math.Sin((Math.Atan2(ny, nx) * 3) + (clock * 4));
                        if (arc > -0.2) Put(p, px, py, arc > 0.5 ? Rim2 : Rim1, 255 * dark);
                    }
                    else if (d < 1 + (1.3 / rs)) p.Set(px, py, Ink);
                    continue;
                }

                var nz = Math.Sqrt(1 - d2);
                double qx = (nx * co) + (nz * si), qy = ny, qz = (-nx * si) + (nz * co);
                var light = (-0.5 * nx) - (0.6 * ny) + (0.62 * nz);
                var b = Bayer[((py & 3) * 4) + (px & 3)] / 16.0;
                uint c;
                if (b >= crystal)
                {
                    var v = Math.Sin((qy * 5) + (2.2 * Math.Sin((qx * 3) + (clock * 1.7))) + (clock * 2.3));
                    c = Bands[Math.Clamp((int)Math.Floor(((v * 0.5) + 0.5) * 4.99), 0, Bands.Length - 1)];
                    if (light < -0.15) c = CapSheet.Darker(c, 0.25);
                }
                else
                {
                    var near = 0;
                    double da = double.MaxValue, db = double.MaxValue;
                    for (var n = 0; n < Seeds.Length; n++)
                    {
                        var dd = 1 - ((Seeds[n].X * qx) + (Seeds[n].Y * qy) + (Seeds[n].Z * qz));
                        if (dd < da) { db = da; da = dd; near = n; }
                        else if (dd < db) db = dd;
                    }

                    var s = Seeds[near];
                    var turned = (-0.5 * ((s.X * co) - (s.Z * si))) - (0.6 * s.Y) + (0.62 * ((s.X * si) + (s.Z * co)));
                    var tone = Math.Clamp((int)Math.Floor((turned + 0.8) * 2.6), 0, Reds.Length - 1);
                    c = db - da < 0.03 ? Reds[Math.Min(Reds.Length - 1, tone + 2)] : Reds[tone];
                }

                // black, sweeping in by an ordered dither; a faint red sheen left on its lit side
                if (b < dark) c = dark >= 1 && light > 0.8 ? 0x2A0A10u : Ink;
                if (dark < 0.5 && ((nx + 0.42) * (nx + 0.42)) + ((ny + 0.46) * (ny + 0.46)) < 0.016) c = White;
                p.Set(px, py, c);
            }
        }
    }

    /// <summary>
    /// The Omega sign: a thick ring open at the bottom, its two ends turned out into arrowheads pointing down and out; dark red when it appears,
    /// brightening to orange with a yellow top and a dark orange underside, an ink edge; red on the dark shape, then gone in the fire.
    /// </summary>
    private static void OmegaAt(CapSheet p, double cx, double cy, double t, double clock)
    {
        var bright = Span(Sign, Sign + 0.5, t);
        var onShape = t >= Shape;
        const double ro = 22, ri = 14;
        for (var dy = -26; dy <= 30; dy++)
        {
            for (var dx = -36; dx <= 36; dx++)
            {
                var inside = InOmega(dx, dy, ro, ri);
                if (!inside)
                {
                    if (InOmega(dx + 1, dy, ro, ri) || InOmega(dx - 1, dy, ro, ri) || InOmega(dx, dy + 1, ro, ri) || InOmega(dx, dy - 1, ro, ri))
                    {
                        Put(p, cx + dx, cy + dy, Ink);
                    }

                    continue;
                }

                uint c;
                if (onShape) c = (int)(clock * 12) % 3 == 0 ? Omega : OmegaRed;
                else if (bright < 0.4) c = OmegaRed;
                else
                {
                    var top = !InOmega(dx, dy - 2, ro, ri);
                    var under = !InOmega(dx, dy + 2, ro, ri);
                    c = top ? OmegaHi : under ? OmegaLo : Omega;
                    if (bright < 0.7) c = CapSheet.Mix(OmegaRed, c, (bright - 0.4) / 0.3);
                }

                Put(p, cx + dx, cy + dy, c);
            }
        }
    }

    /// <summary>
    /// Whether a point (from the sign's middle) is in the Omega: the ring's band, open wide at the bottom, or one of its feet, an arrowhead at each
    /// end of the ring pointing out and a little down.
    /// </summary>
    private static bool InOmega(int dx, int dy, double ro, double ri)
    {
        var d = Math.Sqrt((dx * dx) + (dy * dy));
        var angle = Math.Atan2(dy, dx);
        if (d <= ro && d >= ri && !(angle > Math.PI * 0.22 && angle < Math.PI * 0.78)) return true;
        for (var side = -1; side <= 1; side += 2)
        {
            if (Inside(dx, dy, side * 9, 9, side * 17, 26, side * 31, 21)) return true;
        }

        return false;
    }

    private static bool Inside(double x, double y, double ax, double ay, double bx, double by, double cx, double cy)
    {
        var d1 = ((x - bx) * (ay - by)) - ((ax - bx) * (y - by));
        var d2 = ((x - cx) * (by - cy)) - ((bx - cx) * (y - cy));
        var d3 = ((x - ax) * (cy - ay)) - ((cx - ax) * (y - ay));
        return !(((d1 < 0) || (d2 < 0) || (d3 < 0)) && ((d1 > 0) || (d2 > 0) || (d3 > 0)));
    }

    /// <summary>Groudon's shape standing dark where the sphere was, the Omega burning red on its chest, white rays bursting from behind it.</summary>
    private static void Silhouette(CapSheet p, CritterArt primal, (double X, double Y) at, double t, double clock)
    {
        var k = Span(Shape, Fire, t);
        for (var n = 0; n < 20; n++)
        {
            var a = (n * Math.PI / 10) + 0.15;
            var len = (30 + ((n % 3) * 16)) * Math.Min(1, k * 3);
            for (var d = 20; d < 20 + len; d++) Put(p, at.X + (Math.Cos(a) * d), at.Y + (Math.Sin(a) * d * 0.85), d < 20 + (len * 0.4) ? White : 0xFFE8D0u);
        }

        CapVisitors.Centred(p, primal, clock, R(at.X), R(at.Y + PrimalDrop), PrimalSize, true, 0x1A0A0Cu, 0.88, 0);
    }

    /// <summary>The burst of fire it comes out of: tongues of flame flung out round it, hot at their roots; drops of magma; a flash.</summary>
    private static void Burst(CapSheet p, (double X, double Y) at, double age, double clock)
    {
        if (age < 0.08) CapTrial.Flash(p, 170);
        var k = age / 1.6;
        for (var n = 0; n < 26; n++)
        {
            var a = (n * Math.PI * 2 / 26) + (Rnd(n, 241) * 0.2);
            var len = (28 + (Rnd(n, 243) * 26)) * Math.Min(1, age * 4) * (1 - (k * 0.6));
            var wob = Math.Sin((clock * 18) + n) * 2.5;
            for (var d = 4; d < 4 + len; d++)
            {
                var u = (d - 4) / len;
                if (k > 0.35 && u < (k - 0.35) * 1.6) continue;
                var c = Flame[Math.Min(Flame.Length - 1, (int)(u * Flame.Length))];
                for (var w = 0; w < (u < 0.4 ? 3 : u < 0.7 ? 2 : 1); w++) Put(p, at.X + (Math.Cos(a) * d) + (wob * u) + w, at.Y + (Math.Sin(a) * d * 0.8), c);
            }
        }

        for (var n = 0; n < 20; n++)
        {
            var a = -Math.PI * (0.1 + (Rnd(n, 245) * 0.8));
            var v = 24 + (Rnd(n, 247) * 34);
            Put(p, at.X + (Math.Cos(a) * v * age), at.Y + (Math.Sin(a) * v * age) + (40 * age * age), n % 2 == 0 ? GlowHi : Glowing);
        }
    }

    // ---------------------------------------------------------------- Precipice Blades

    /// <summary>How far each blade leans towards the ruins' last arch, per row up.</summary>
    private const double Lean = 0.16;

    /// <summary>The last blade leans back the other way, so its tip and the crystal stand inside the window.</summary>
    private const double LastLean = -0.1;

    private static double BladeX(int i, int x0, double from) => from + 30 + (((LastX(x0) - from - 30) * i) / (Count - 1));

    /// <summary>How tall each blade is: bigger and bigger along the wave; the last, which stays, small enough not to cover the wall over its arch (asked).</summary>
    private static int Height(int i) => i == Count - 1 ? 24 : 20 + (i * 5);

    private static double BladeTime(int i) => Wave + 0.3 + (i * 0.28);

    /// <summary>How far up a blade is, a moment after it bursts out: fast, overshooting a little, settling.</summary>
    private static double Rise(double since) => since <= 0 ? 0 : since >= 0.4 ? 1 : since < 0.18 ? (since / 0.18) * 1.08 : 1.08 - (0.08 * ((since - 0.18) / 0.22));

    /// <summary>
    /// The wave: a glowing crack running along the seabed ahead of it, then each blade bursting out in its turn (a fountain of molten drops and
    /// smoke, the window jolting); after the crystal, all but the last crumble back into the sand.
    /// </summary>
    private static void BladesAt(CapSheet p, int x0, double from, int sand, double t, double clock)
    {
        // the crack running ahead
        var head = Span(Wave, BladeTime(Count - 1), t);
        if (t < Crumble)
        {
            for (var x = from + 20; x < from + 20 + ((LastX(x0) - from - 20) * head); x++)
            {
                var jog = (int)(CapArt.Hash((int)x, 9) % 3) - 1;
                Put(p, x, sand + 1 + jog, ((clock * 4) + (x * 0.05)) % 1 < 0.5 ? Glowing : GlowHi);
            }
        }

        for (var i = 0; i < Count; i++)
        {
            var at = BladeTime(i);
            if (t < at) continue;
            var last = i == Count - 1;
            var crumble = last ? 0 : Span(Crumble + (i * 0.08), Crumble + 0.6 + (i * 0.08), t);
            if (crumble >= 1) continue;
            var x = BladeX(i, x0, from);
            var tall = Height(i) * Rise(t - at) * (1 - crumble);
            Blade(p, x, sand, tall, Height(i), i, clock);
            if (t - at < 0.9) Eruption(p, x, sand, t - at, i);
            if (t - at < 0.3) CapTrial.Shake(p, t - at, 2, 0.3);
            if (crumble > 0) Rubble(p, x, sand, crumble, i);
        }
    }

    /// <summary>
    /// One eruption of Precipice Blades, as in Omega Ruby: a cluster of spires of volcanic rock bursting out of the seabed at an angle, the big
    /// one in front and two smaller ones behind it leaning out either side (<see cref="Spire"/>); lava glowing in the sand round their foot.
    /// </summary>
    private static void Blade(CapSheet p, double x, int sand, double tall, int full, int seed, double clock)
    {
        if (tall < 1) return;
        var lean = seed == Count - 1 ? LastLean : Lean;
        Spire(p, x - 2 - (full * 0.12), sand, tall * 0.58, full * 0.58, lean - 0.34, (seed * 3) + 1, clock, true);
        Spire(p, x + 2 + (full * 0.14), sand, tall * 0.72, full * 0.72, lean + 0.3, (seed * 3) + 2, clock, true);
        Spire(p, x, sand, tall, full, lean, seed * 3, clock, false);

        // the lava round their foot, in the sand
        var pulse = ((clock * 1.4) + (seed * 0.3)) % 1 < 0.5;
        var reach = 6 + (full * 0.22);
        for (var dx = -(int)reach; dx <= reach; dx++)
        {
            var edge = Math.Abs(dx) > reach - 2;
            Put(p, x + dx, sand + 1, edge ? 0xC03018u : pulse ? Glowing : GlowHi);
            if (!edge && Math.Abs(dx) < reach * 0.5) Put(p, x + dx, sand + 2, 0xC03018u);
        }
    }

    /// <summary>The rock of the blades, from its highlight to its deepest shadow.</summary>
    private static readonly uint[] Rock = [0xA89684u, 0x7E6C60u, 0x5A4A42u, 0x382C2Au, 0x221A1Au];

    /// <summary>
    /// One spire of the blades, drawn row by row as faceted rock: leaning, its sides ragged and chipped (each side its own wander), three faces
    /// (lit, middle and shadow) parted by edges that wander up it, a lit edge on its ridge, ledges of rock across it every few rows (lit on top,
    /// shadowed under), an ink outline; magma in it like Primal Groudon's lines: a vein winding up from its foot with a pulse of light running
    /// up it and a branch off to its side, the rock round the veins warm; a sharp tip. The ones behind are a shade darker.
    /// </summary>
    private static void Spire(CapSheet p, double x, int sand, double tall, double full, double lean, int seed, double clock, bool back)
    {
        if (tall < 2) return;
        var baseHalf = 3 + (full * 0.15);
        var shade = back ? 1 : 0;
        var veinTop = tall * 0.7;
        var branchAt = tall * (0.25 + ((seed % 3) * 0.08));
        var branchDir = seed % 2 == 0 ? -1 : 1;
        for (var h = 0; h < tall; h++)
        {
            var u = h / tall;
            var half = baseHalf * Math.Pow(1 - u, 0.8);
            // each side wanders and is chipped here and there
            var left = half * (1 + (0.18 * Math.Sin((h * 0.23) + seed))) - (CapArt.Hash(h / 2, seed + 50) % 6 == 0 ? 1 : 0);
            var right = half * (1 + (0.18 * Math.Cos((h * 0.19) + (seed * 1.7)))) - (CapArt.Hash(h / 2, seed + 60) % 6 == 0 ? 1 : 0);
            var cx = x + (h * lean);
            int l0 = (int)Math.Round(cx - left), r0 = (int)Math.Round(cx + right);
            if (r0 < l0) r0 = l0;
            var w = r0 - l0;
            // the edges between its faces
            var e1 = l0 + (w * (0.38 + (0.1 * Math.Sin((h * 0.11) + seed))));
            var e2 = l0 + (w * (0.7 + (0.06 * Math.Sin((h * 0.15) + (seed * 2)))));
            var ledge = (h + (seed * 3)) % 8 == 0 && h > 2 && u < 0.85;
            var underLedge = (h + (seed * 3)) % 8 == 7 && h > 2 && u < 0.85;
            // the vein winding up it, and its branch
            var vein = cx + (Math.Sin((h * 0.21) + seed) * half * 0.4);
            var branch = h >= branchAt && h < branchAt + (half * 1.6) ? vein + (branchDir * (h - branchAt) * 0.9) : double.NaN;
            var hot = ((h * 0.5) - (clock * 14) + seed) % 7;
            if (hot < 0) hot += 7;
            for (var px = l0 - 1; px <= r0 + 1; px++)
            {
                uint c;
                if (px < l0 || px > r0 || h >= tall - 1) c = Ink;
                else
                {
                    var face = px < e1 ? 1 : px < e2 ? 2 : 3;
                    c = Rock[Math.Min(Rock.Length - 1, face + shade)];
                    if (Math.Abs(px - e1) < 0.5) c = Rock[shade];                       // the lit ridge
                    else if (Math.Abs(px - e2) < 0.5) c = Rock[Math.Min(Rock.Length - 1, 3 + shade)];
                    if (ledge && px < e2) c = Rock[shade];
                    if (underLedge) c = Rock[Math.Min(Rock.Length - 1, face + 1 + shade)];
                    if (px == l0 && u < 0.6 && !back) c = Rock[0];                      // the light down its lit side
                    // magma: the vein (hot where the pulse is), warm rock either side of it, the branch
                    if (h < veinTop)
                    {
                        if (Math.Abs(px - vein) < 0.6) c = hot < 1.5 ? Tip : hot < 3 ? GlowHi : Vein;
                        else if (Math.Abs(px - vein) < 1.6) c = back ? 0x6A2A1Cu : 0x8A3A20u;
                        if (!double.IsNaN(branch) && Math.Abs(px - branch) < 0.6 && px > l0 && px < r0) c = hot < 3 ? GlowHi : Vein;
                    }

                    // the seam of lava where it comes out of the sand
                    if (h < 2) c = h == 0 ? Glowing : (px + seed) % 3 == 0 ? GlowHi : 0xC03018u;
                }

                p.Set(px, sand - h, c);
            }
        }
    }

    /// <summary>Where a blade bursts out: a fountain of molten drops and black smoke puffs, a ring over the sand.</summary>
    private static void Eruption(CapSheet p, double x, int sand, double age, int seed)
    {
        for (var n = 0; n < 16; n++)
        {
            var a = -Math.PI * (0.15 + (Rnd(n, 251 + seed) * 0.7));
            var v = 20 + (Rnd(n, 253 + seed) * 28);
            double sx = x + (Math.Cos(a) * v * age), sy = sand + (Math.Sin(a) * v * age) + (55 * age * age);
            if (sy > sand + 1) continue;
            Put(p, sx, sy, n % 3 == 0 ? Tip : n % 2 == 0 ? GlowHi : Glowing);
        }

        for (var n = 0; n < 5; n++)
        {
            var r = Math.Max(0, 3 - (age * 2.5)) + (n % 2);
            double sx = x + ((n - 2) * 4), sy = sand - 4 - (age * 18) - (n % 3);
            for (var dy = -(int)r; dy <= r; dy++) for (var dx = -(int)r; dx <= r; dx++) if ((dx * dx) + (dy * dy) <= r * r) Put(p, sx + dx, sy + dy, (dx + dy) < 0 ? Smoke : SmokeLo);
        }

        CapTrial.Ring(p, x, sand + 1, 3 + (age * 24), 0.25, GlowHi, 230 * Math.Max(0, 1 - (age / 0.6)), false);
    }

    /// <summary>A blade crumbling: chunks of obsidian breaking off and falling, sinking into the sand.</summary>
    private static void Rubble(CapSheet p, double x, int sand, double k, int seed)
    {
        for (var n = 0; n < 8; n++)
        {
            var sx = x + ((Rnd(n, 261 + seed) - 0.5) * 10) + (k * (n % 2 == 0 ? 6 : -6));
            var sy = sand - (Rnd(n, 263 + seed) * 20 * (1 - k)) + (k * k * 10);
            if (sy > sand) continue;
            Put(p, sx, sy, Rock[n % 2 == 0 ? 1 : 3]);
            Put(p, sx + 1, sy, Ink);
        }
    }

    private static double Ease(double k) => k * k * (3 - (2 * k));

    /// <summary>
    /// The magma round the last blade's foot (asked: a little of it spreading over the sand): a flat pool lying on the sand, its edge ragged,
    /// spreading out from the foot as <paramref name="k"/> goes to 1; a crust of cooled rock round its rim, red inside it, orange and yellow where
    /// it is hottest, plates of dark crust drifting on it, hot spots breathing and now and then a bubble popping.
    /// </summary>
    private static void Pool(CapSheet p, double x, int sand, double k, double clock)
    {
        if (k <= 0) return;
        var rx = 5 + (26 * k);
        const double Ry = 3.4;
        var mid = sand - 1;
        for (var y = (int)Math.Floor(mid - Ry); y <= (int)Math.Ceiling(mid + Ry); y++)
        {
            var v = (y - mid) / Ry;
            if (Math.Abs(v) > 1) continue;
            var half = rx * Math.Sqrt(1 - (v * v));
            for (var dx = -(int)Math.Ceiling(half) - 1; dx <= half + 1; dx++)
            {
                // a ragged edge: each column reaches a little more or less
                var reach = half - (CapArt.Hash(dx + 40, 81) % 3);
                var d = Math.Abs(dx);
                if (d > reach + 1) continue;
                uint c;
                if (d > reach) c = 0x3A1A12u;                                         // the cooled crust round it
                else if (d > reach - 1.5) c = 0x7A2410u;
                else
                {
                    var heat = 1 - (d / Math.Max(1, reach));
                    var breath = Math.Sin((clock * 2.2) + (dx * 0.4) + (y * 1.3));
                    c = heat > 0.6 && breath > 0.3 ? GlowHi : heat > 0.35 ? Glowing : 0xC03018u;
                    // plates of crust drifting on it
                    var plate = (int)Math.Floor((dx + (clock * 1.5)) / 5);
                    if (CapArt.Hash(plate, y + 90) % 5 == 0 && ((dx + (int)(clock * 1.5)) % 5 + 5) % 5 < 2) c = 0x5A2014u;
                }

                Put(p, x + dx, y, c);
            }
        }

        // a bubble rising out of it and popping, now and then
        var bub = (clock * 0.9) % 1;
        if (k >= 1 && bub < 0.25)
        {
            var bx = x + (((int)Math.Floor(clock * 0.9) * 7 % 17) - 8);
            var by = mid - 1 - (bub * 8);
            Put(p, bx, by, bub < 0.18 ? GlowHi : Tip);
            if (bub > 0.18) { Put(p, bx - 1, by, Glowing); Put(p, bx + 1, by, Glowing); }
        }
    }

    /// <summary>The last blade in front of the last arch with the crystal on its tip.</summary>
    private static void Rest(CapSheet p, CapTrialCrystal crystal, (int X, int Y) spot, double since, double clock)
    {
        var glint = clock % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>The Geostal on the last blade, in front of the last arch, for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _) || Spot(l, ctx) is not { } spot) return;
        Pool(p, LastX(x0) - 9, SandOf(floor), 1, phase);
        Blade(p, LastX(x0), SandOf(floor), Height(Count - 1), Height(Count - 1), Count - 1, phase);
        Rest(p, crystal, spot, 30, phase);
    }
}
