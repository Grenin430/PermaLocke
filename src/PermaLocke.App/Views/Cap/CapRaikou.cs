using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The seventh trial's Z-Crystal, the Electrostal, brought by Raikou (2026-10-18, first simple prototype: the player's choice and sprite). Its own
/// film, starting <see cref="Lead"/> seconds before the film's clock: a storm, lightning striking the sea; Raikou gallops in from the right on the
/// surface of the sea, like the legendary beasts running over water, across the party's window and then the abyss's, each paw leaving a
/// footprint of sparks and a ripple on the water; it skids to a stop over the ruins' arch (the spot the player marked), roars, and a huge
/// bolt comes down through the sea with the crystal at its tip into the arch. Raikou turns and runs off to the right, across the abyss's window and then all across the party's. The crystal stays in the
/// arch with bolts burnt into the stone round it (fulgurites, a spark running along them now and then) and a small storm cloud inside the
/// arch's top that drops a spark on it now and then, for the rest of the run (<see cref="PaintRest"/>).
/// </summary>
public static class CapRaikou
{
    /// <summary>The seventh trial's crystal.</summary>
    public const int Electrostal = 810;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 3.6;

    private const int Art = 243, Standing = 20243;

    /// <summary>How many times a second the arcs and bolts change their path.</summary>
    private const double Flicker = 15;
    private const double Size = 0.5, Lying = 0.4, Foot = 31.5;

    private const double Storm = -3.4, Strike1 = -3.2, Strike2 = -1.6, InParty = -2.8, OutParty = -0.6, InDeep = -0.3, Stop = 2.5, Roar = 2.8,
        Call = 3.7, Drop = 4.1, Hit = CapTrial.Hit, Turn = Hit + 0.7, Gone = Hit + 2.4, Away = Gone + 2.2, Step = 0.16;

    private const uint White = 0xFFFFFFu, Spark = 0xFFE45Au, Gold = 0xF8B800u, Volt = 0x9FE8FFu, Foam = 0xCFF4FFu, Burnt = 0x1A1612u,
        Scorch = 0x3A2E22u, Ember = 0xC88A1Cu, BoltRim = 0x3A3AB8u;

    /// <summary>Where the crystal stands: on the floor inside the arch of the tall wall; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) ? (x0 + 21, floor - 3 - R(CapTrialCrystal.H * Lying)) : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (!CritterArt.All.TryGetValue(Art, out var raikou) || !CritterArt.All.TryGetValue(Standing, out var standing)) return;
        var deep = l.Panel == CapPanelKind.Deep;
        if (!deep && l.Panel != CapPanelKind.Party) return;
        var surface = l.SurfaceY;
        var flick = (int)Math.Floor((t + Lead) * Flicker);

        // the storm: lightning striking the sea far off, the window flashing with it
        foreach (var (at, share, seed) in Strikes)
        {
            var s = t - at;
            if (s < 0 || s > 0.8 || deep != (seed % 2 == 1)) continue;
            var x = share * l.Cols;
            // it strikes, goes, and strikes again down the same path
            if (s < 0.12 || (s > 0.2 && s < 0.28)) Bolt(p, x + 10, 0, x, surface, seed, 1, surface, s > 0.2);
            if (s < 0.06) CapTrial.Flash(p, 110);
            Splash(p, x, surface, s, seed);
        }

        if (!deep)
        {
            Run(p, raikou, standing, t, surface, InParty, OutParty, l.Cols + 40, -40, false, flick);
            // and back the other way when it leaves: all across the party's window and off its right edge (asked by the player)
            Run(p, raikou, standing, t, surface, Gone - 0.1, Away, -40, l.Cols + 40, false, flick);
            return;
        }

        if (Spot(l, ctx) is not { } spot) return;
        double startX = l.Cols + 40, stopX = spot.X + 4;
        if (t < Turn) Run(p, raikou, standing, t, surface, InDeep, Stop, startX, stopX, true, flick);
        else Run(p, raikou, standing, t, surface, Turn, Gone, stopX, l.Cols + 50, false, flick);

        // the roar: rings of sound off its head, arcs off its crest, the window shaking
        if (t >= Roar && t < Roar + 0.9)
        {
            var r = t - Roar;
            for (var c = 0; c < 3; c++)
            {
                var age = r - (c * 0.18);
                if (age > 0 && age < 0.5) CapTrial.Ring(p, stopX - 14, surface - 26, 4 + (age * 50), 0.8, Spark, 230, false);
            }

            if (r < 0.6) Crackle(p, stopX - 12, surface - 30, 8, flick);
            CapTrial.Shake(p, r, 2, 0.6);
        }

        // the sky charging over the arch: arcs crawling along the top of the window, gathering where the bolt will fall
        if (t >= Call && t < Drop)
        {
            var charge = Clamp01((t - Call) / (Drop - Call));
            for (var n = 0; n < 3; n++)
            {
                var from = spot.X + ((n - 1) * 40 * (1.2 - charge));
                Arc(p, from, 2 + n, spot.X, 4, 50 + n, flick, false);
            }
        }

        var top = spot.Y - 10;
        if (t >= Drop && t < Hit + 0.45)
        {
            var reach = Clamp01((t - Drop) / (Hit - Drop));
            var tip = reach * top;
            // after the strike it blinks out
            var after = t - Hit;
            if (after < 0.1 || (after > 0.16 && after < 0.24) || (after > 0.3 && after < 0.38))
            {
                Bolt(p, spot.X + 6, 0, spot.X, top, 77, 2, tip, after > 0.3);
            }

            if (t < Hit) crystal.Paint(p, spot.X, R(tip), Lying, 0, 0.9, -1);
        }

        if (t >= Hit)
        {
            var f = t - Hit;
            if (f < 0.1) CapTrial.Flash(p, 230 * (1 - (f / 0.1)));
            if (f < 0.5)
            {
                // a burst of rays out of the arch, rings running over the floor, sparks flying
                Rays(p, spot.X, spot.Y + 2, 6 + (f * 60), f / 0.5);
                for (var c = 0; c < 2; c++) CapTrial.Ring(p, spot.X, spot.Y + 6, 6 + (f * 70) + (c * 6), 0.35, Spark, 230, false);
                for (var n = 0; n < 24; n++)
                {
                    var life = 0.3 + (Rnd(n, 45) * 0.2);
                    if (f > life) continue;
                    var a = Math.PI * (1.05 + (Rnd(n, 41) * 0.9));
                    var v = 20 + (Rnd(n, 43) * 40);
                    Put(p, spot.X + (Math.Cos(a) * v * f), spot.Y + 4 + (Math.Sin(a) * v * f) + (40 * f * f), n % 3 == 0 ? White : Spark);
                }
            }

            CapTrial.Shake(p, f, 3, 0.5);
            Rest(p, spot, crystal, t, f);
        }
    }

    /// <summary>The far strikes of the storm: when, where across the window (a share of its width), and which window (odd: the abyss's).</summary>
    private static readonly (double At, double Share, int Seed)[] Strikes =
        [(Storm, 0.2, 2), (Strike1, 0.75, 4), (Strike1 + 0.5, 0.45, 6), (Strike2, 0.15, 8), (-0.1, 0.8, 3), (1.2, 0.3, 5)];

    /// <summary>
    /// Raikou galloping over the sea's surface from one place to another between two moments (with a skid at the end if it stops there), each
    /// paw's landing leaving a footprint of sparks on the water, arcs crackling behind it; standing as the sprite is once it has stopped.
    /// </summary>
    private static void Run(CapSheet p, CritterArt art, CritterArt standing, double t, int surface, double from, double to, double x0, double x1, bool stops,
        int flick)
    {
        if (t < from) return;
        var right = x1 > x0;
        var back = right ? -1 : 1;
        // the footprints of the run, each fading where its paw came down
        for (var k = 0; k < 40; k++)
        {
            var at = from + (k * Step);
            if (at > Math.Min(t, to)) break;
            var age = t - at;
            if (age > 0.9) continue;
            var fx = Along(x0, x1, from, to, at, stops) + ((k % 2 == 0 ? -1 : 1) * 7);
            Footprint(p, fx, surface, age, k, flick);
        }

        if (!stops && t >= to) return;
        var x = Along(x0, x1, from, to, Math.Min(t, to), stops);
        var still = stops && t >= to;
        var y = surface - R(Foot * Size) + 1;

        // arcs crackling off its back and legs, trailing behind it
        if (!still)
        {
            Arc(p, x + (back * 10), y - 6, x + (back * 34), y - 4 + (flick % 3), 3, flick, false);
            Arc(p, x + (back * 8), y + 6, x + (back * 28), surface - 2, 7, flick + 1, false);
            if (flick % 2 == 0) Arc(p, x - (back * 4), y + 10, x + (back * 6), surface, 11, flick, false);
        }

        // the skid: spray off its paws as it brakes
        if (stops)
        {
            var sk = t - (to - 0.45);
            if (sk > 0 && sk < 0.9)
            {
                for (var n = 0; n < 24; n++)
                {
                    var life = 0.3 + (Rnd(n, 61) * 0.4);
                    var age = sk - (Rnd(n, 63) * 0.4);
                    if (age < 0 || age > life) continue;
                    var px = Along(x0, x1, from, to, Math.Min(to, t - age), stops) - 10;
                    Put(p, px - (age * 40), surface - (age * 30) + (age * age * 90), n % 3 == 0 ? White : Foam);
                }
            }
        }

        if (still)
        {
            // standing as the sprite is: the head thrown up while it roars
            CapVisitors.Centred(p, standing, 0, R(x), y, Size, right, White, 0, t >= Roar && t < Roar + 0.6 ? 10 : 0);
            return;
        }

        // its own clock for the gallop, always forward (the film's is negative before it starts)
        CapVisitors.Centred(p, art, t + Lead, R(x), y, Size, right);
    }

    /// <summary>Where along its way it is: steady, then (if it stops) braking over the last part.</summary>
    private static double Along(double x0, double x1, double from, double to, double t, bool stops)
    {
        var u = Clamp01((t - from) / (to - from));
        if (!stops) return x0 + ((x1 - x0) * u);
        const double A = 0.72, Total = A + ((1 - A) / 2);
        var f = u < A ? u : A + (u - A) - ((u - A) * (u - A) / (2 * (1 - A)));
        return x0 + ((x1 - x0) * f / Total);
    }

    /// <summary>The drops of a splash: where each goes (across, up) at its highest.</summary>
    private static readonly (int X, int Y)[] Drops = [(-4, -3), (-2, -5), (0, -6), (2, -5), (4, -3), (-3, -2), (3, -2)];

    /// <summary>
    /// A paw's footprint on the water, drawn pixel by pixel: a crown of drops thrown up and falling back, a flat ring of light spreading on the
    /// water (yellow, then gold, then pale blue, breaking up as it fades), and two little arcs jumping off it at first.
    /// </summary>
    private static void Footprint(CapSheet p, double x, int surface, double age, int k, int flick)
    {
        if (age < 0.3)
        {
            var up = Math.Sin(age / 0.3 * Math.PI);
            foreach (var (dx, dy) in Drops) Put(p, x + (dx * (0.6 + (age * 2))), surface + (dy * up), dy < -4 ? White : Foam);
        }

        var rx = 2 + (age * 11);
        var ry = Math.Max(1, rx / 4);
        var n = Math.Max(12, (int)(rx * 6));
        var keep = age < 0.35 ? 1 : age < 0.6 ? 2 : 3;
        for (var i = 0; i < n; i++)
        {
            if ((i + k) % keep != 0) continue;
            var a = i / (double)n * 2 * Math.PI;
            Put(p, x + (Math.Cos(a) * rx), surface + (Math.Sin(a) * ry), age < 0.3 ? Spark : age < 0.55 ? Gold : Volt);
        }

        if (age < 0.3)
        {
            Arc(p, x - 1, surface, x - 4, surface - 5, k, flick, false);
            Arc(p, x + 1, surface, x + 4, surface - 6, k + 5, flick, false);
        }
    }

    /// <summary>Where lightning struck the sea: spray thrown up and a ring running out on the water.</summary>
    private static void Splash(CapSheet p, double x, int surface, double age, int seed)
    {
        if (age > 0.8) return;
        foreach (var (dx, dy) in Drops) Put(p, x + (dx * (1 + (age * 4))), surface + (dy * 2 * Math.Sin(Math.Min(1, age / 0.5) * Math.PI)), Foam);
        var rx = 4 + (age * 30);
        var n = (int)(rx * 6);
        for (var i = 0; i < n; i++)
        {
            if (age > 0.4 && (i + seed) % 2 != 0) continue;
            var a = i / (double)n * 2 * Math.PI;
            Put(p, x + (Math.Cos(a) * rx), surface + (Math.Sin(a) * rx * 0.2), age < 0.3 ? White : Foam);
        }
    }

    /// <summary>The colours of a bolt: its white-hot core, the pale blue round it, the blue of its forks and the violet of its dithered halo.</summary>
    private const uint Core = 0xFFFFFFu, Pale = 0xD8E4FFu, Blue = 0x9AAAFFu, Fork = 0x7C86F0u, Halo = 0x5A48D0u;

    // the points of a bolt's paths, filled anew for each one drawn (one set per thread: the windows may paint at once)
    [ThreadStatic] private static double[]? mainX, mainY, forkX, forkY, twigX, twigY;

    /// <summary>
    /// A bolt of lightning drawn like a real one, as pixel art: its path is fractal (split again and again at its middle, each half pushed aside
    /// by less), so it wanders with big bends and small kinks at once; thicker at the top, thinning towards its end; a white-hot core, pale blue
    /// round it and a dithered violet halo outside (no blur); forks leaving it downwards at an angle, each wandering the same way and forking
    /// again, dimmer and thinner the further from the trunk. Only what is above <paramref name="cut"/> is drawn (the bolt coming down);
    /// <paramref name="dim"/> draws it as it fades between flashes. Size 2 is the big one, 1 a far strike, 0 a small spark.
    /// </summary>
    private static void Bolt(CapSheet p, double x0, double y0, double x1, double y1, int seed, int size, double cut, bool dim)
    {
        mainX ??= new double[129]; mainY ??= new double[129];
        forkX ??= new double[65]; forkY ??= new double[65];
        twigX ??= new double[33]; twigY ??= new double[33];
        var n = Path(mainX, mainY, size == 2 ? 7 : size == 1 ? 6 : 4, x0, y0, x1, y1, size == 0 ? 0.16 : 0.14, seed);
        double dx = x1 - x0, dy = y1 - y0, len = Math.Sqrt((dx * dx) + (dy * dy));
        var down = Math.Atan2(dy, dx);

        // the forks first, under the trunk
        var forks = size == 2 ? 8 : size == 1 ? 5 : 1;
        for (var f = 0; f < forks; f++)
        {
            var at = (int)(n * (0.08 + (0.8 * Rnd(seed + f, 101))));
            var side = Rnd(seed + f, 103) < 0.5 ? -1 : 1;
            var angle = down + (side * (0.4 + (Rnd(seed + f, 105) * 0.6)));
            var reach = len * (1 - (at / (double)n)) * (0.25 + (Rnd(seed + f, 107) * 0.3));
            double fx0 = mainX[at], fy0 = mainY[at], fx1 = fx0 + (Math.Cos(angle) * reach), fy1 = fy0 + (Math.Sin(angle) * reach);
            var m = Path(forkX, forkY, 5, fx0, fy0, fx1, fy1, 0.14, seed + (f * 31));
            if (!dim && size > 0) Trace(p, forkX, forkY, m, cut, 1, Halo, true, 0.5);
            Trace(p, forkX, forkY, m, cut, 0, dim ? Fork : Blue, false, 1);
            if (!dim) Trace(p, forkX, forkY, m, cut, 0, size == 2 ? Pale : Blue, false, 0.4);

            // a twig off the fork, and the bright root of it
            var tAt = m / 2 + (int)(Rnd(seed + f, 109) * (m / 3));
            var tAngle = angle + (-side * (0.3 + (Rnd(seed + f, 111) * 0.5)));
            var tReach = reach * 0.4;
            double tx0 = forkX[tAt], ty0 = forkY[tAt];
            var k = Path(twigX, twigY, 4, tx0, ty0, tx0 + (Math.Cos(tAngle) * tReach), ty0 + (Math.Sin(tAngle) * tReach), 0.16, seed + (f * 53));
            Trace(p, twigX, twigY, k, cut, 0, Fork, false, 1);
        }

        // the trunk: halo, glow, core; thicker towards the top
        var thick = 0;
        if (!dim) Trace(p, mainX, mainY, n, cut, thick + 2, Halo, true, 1);
        Trace(p, mainX, mainY, n, cut, thick + 1, dim ? Blue : Pale, false, size == 0 ? 1 : 0.85);
        Trace(p, mainX, mainY, n, cut, size == 2 ? 1 : 0, dim ? Pale : Core, false, size == 2 ? 0.3 : 1);
        if (size == 2) Trace(p, mainX, mainY, n, cut, 0, dim ? Pale : Core, false, 1);
    }

    /// <summary>
    /// A fractal path from one point to another into the buffers (2^levels pieces): each piece's middle pushed aside across the way by a share
    /// of its length that halves, roughly, at each level. Returns how many pieces.
    /// </summary>
    private static int Path(double[] xs, double[] ys, int levels, double x0, double y0, double x1, double y1, double rough, int seed)
    {
        var n = 1 << levels;
        (xs[0], ys[0], xs[n], ys[n]) = (x0, y0, x1, y1);
        double dx = x1 - x0, dy = y1 - y0, len = Math.Max(1, Math.Sqrt((dx * dx) + (dy * dy)));
        double nx = -dy / len, ny = dx / len;
        var amp = len * rough;
        for (var step = n; step > 1; step /= 2)
        {
            for (var i = 0; i < n; i += step)
            {
                var mid = i + (step / 2);
                var d = (Rnd(seed + mid, step + 7) - 0.5) * 2 * amp;
                xs[mid] = ((xs[i] + xs[i + step]) / 2) + (nx * d);
                ys[mid] = ((ys[i] + ys[i + step]) / 2) + (ny * d);
            }

            amp *= 0.56;
        }

        return n;
    }

    /// <summary>
    /// Draws a path pixel by pixel as round dabs of a radius (only the share of it from its start given by <paramref name="along"/> gets the
    /// full radius; the rest one less), solid or dithered in a checkerboard, as far down as the cut.
    /// </summary>
    private static void Trace(CapSheet p, double[] xs, double[] ys, int n, double cut, int radius, uint c, bool dither, double along)
    {
        for (var i = 0; i < n; i++)
        {
            if (ys[i] > cut) return;
            var r = i < n * along ? radius : Math.Max(0, radius - 1);
            double ax = xs[i], ay = ys[i], bx = xs[i + 1], by = Math.Min(ys[i + 1], cut);
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay))));
            for (var s = 0; s <= steps; s++)
            {
                int x = R(ax + ((bx - ax) * s / steps)), y = R(ay + ((by - ay) * s / steps));
                for (var oy = -r; oy <= r; oy++)
                {
                    for (var ox = -r; ox <= r; ox++)
                    {
                        if ((ox * ox) + (oy * oy) > (r * r) + (r > 1 ? 1 : 0)) continue;
                        if (dither && ((x + ox + y + oy) & 1) == 1) continue;
                        p.Set(x + ox, y + oy, c);
                    }
                }
            }
        }
    }

    /// <summary>A small arc of electricity between two points: a few sharp zigzags, yellow with white, its path changing every flicker.</summary>
    private static void Arc(CapSheet p, double x0, double y0, double x1, double y1, int seed, int flicker, bool big)
    {
        var len = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        var steps = Math.Max(2, (int)(len / 4));
        double px = x0, py = y0;
        var side = 1;
        for (var i = 1; i <= steps; i++)
        {
            var u = i / (double)steps;
            side = -side;
            var j = i == steps ? 0 : side * (1 + (Rnd(seed + i, flicker) * 2.5));
            // the zigzag goes across the arc's way
            double nx = x0 + ((x1 - x0) * u) - (j * (y1 - y0) / Math.Max(1, len)), ny = y0 + ((y1 - y0) * u) + (j * (x1 - x0) / Math.Max(1, len));
            Stroke(p, px, py, nx, ny, big ? 1 : 0, false);
            (px, py) = (nx, ny);
        }
    }

    /// <summary>Little arcs crackling out of a point, every way.</summary>
    private static void Crackle(CapSheet p, double x, double y, double r, int flicker)
    {
        for (var n = 0; n < 4; n++)
        {
            var a = (n * Math.PI / 2) + (Rnd(n, flicker) * 1.2);
            Arc(p, x + (Math.Cos(a) * 3), y + (Math.Sin(a) * 3), x + (Math.Cos(a) * r), y + (Math.Sin(a) * r), n * 3, flicker, false);
        }
    }

    /// <summary>A burst of light from a point: eight rays, white at the root and yellow out, broken up as they fade.</summary>
    private static void Rays(CapSheet p, double x, double y, double r, double fade)
    {
        for (var n = 0; n < 8; n++)
        {
            var a = n * Math.PI / 4;
            var len = r * (n % 2 == 0 ? 1 : 0.6);
            for (var d = 3.0; d < len; d += 1)
            {
                if (fade > 0.5 && ((int)d + n) % 2 == 0) continue;
                Put(p, x + (Math.Cos(a) * d), y + (Math.Sin(a) * d * 0.7), d < len * 0.4 ? White : Spark);
            }
        }
    }

    /// <summary>
    /// One straight stretch of a bolt, solid pixels only: width 0 is a yellow line with a white pixel now and then, 1 a white core with yellow
    /// down both sides, 2 a white core two cells wide; a big one gets a deep blue rim outside the yellow.
    /// </summary>
    private static void Stroke(CapSheet p, double x0, double y0, double x1, double y1, int width, bool rim)
    {
        var n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)));
        if (n == 0) return;
        // mostly up and down: the yellow goes on its left and right; mostly across: above and below
        var steep = Math.Abs(y1 - y0) >= Math.Abs(x1 - x0);
        int ox = steep ? 1 : 0, oy = steep ? 0 : 1;
        for (var i = 0; i <= n; i++)
        {
            var u = i / (double)n;
            int x = R(x0 + ((x1 - x0) * u)), y = R(y0 + ((y1 - y0) * u));
            if (width == 0)
            {
                Put(p, x, y, i % 4 == 0 ? White : Spark);
                continue;
            }

            var core = width - 1;
            if (rim)
            {
                Put(p, x - (ox * (core + 2)), y - (oy * (core + 2)), BoltRim);
                Put(p, x + (ox * (core + 2)), y + (oy * (core + 2)), BoltRim);
            }

            Put(p, x - (ox * (core + 1)), y - (oy * (core + 1)), Spark);
            Put(p, x + (ox * (core + 1)), y + (oy * (core + 1)), Spark);
            for (var c = -core; c <= core; c++) Put(p, x + (ox * c), y + (oy * c), White);
        }
    }

    // ---------------------------------------------------------------- what stays

    /// <summary>The fulgurites burnt into the wall round the arch: branching paths from the arch's top (offsets from the crystal's spot).</summary>
    private static readonly (int X, int Y)[][] Burns =
    [
        [(0, -14), (-2, -18), (-1, -22), (-4, -26), (-3, -30), (-6, -33)],
        [(-4, -26), (-8, -27), (-11, -30)],
        [(0, -14), (3, -17), (5, -21), (8, -23), (9, -27), (12, -30)],
        [(5, -21), (9, -20), (12, -22)],
        [(-7, -12), (-10, -9), (-12, -5), (-15, -3), (-16, 1)],
        [(7, -12), (10, -8), (11, -4), (14, -1), (15, 3)],
    ];

    /// <summary>
    /// The small storm cloud, drawn by hand: three puffs (a small one left, a big one in the middle, one right) on a flat underside, lit from
    /// the top left (highlight, light, middle, shadow), a seam of shadow where the puffs meet, a dark outline round it.
    /// </summary>
    private static readonly string[] CloudArt =
    [
        "......oooo.......",
        ".....ohhhlo......",
        "..ooohhhlllo.oo..",
        ".ohhlshhlllmohllo",
        "ohhlllslllmmshlmo",
        "ohllllmlllmmmlmmo",
        "olllmmmmmmmmmmmdo",
        "ommmmmmmmmmdmmddo",
        "ommddmmddmdddddo.",
        ".oddddddddddddo..",
        "..oooooooooooo...",
    ];

    /// <summary>
    /// The cloud, its middle at a point, at a size (1 is as drawn; smaller or larger while it forms, each cell taking the drawing's nearest);
    /// lit up from inside (yellower) while it sparks.
    /// </summary>
    private static void Cloud(CapSheet p, int cx, int cy, bool lit, double size = 1)
    {
        var shades = lit ? LitShades : Shades;
        int cols = CloudArt[0].Length, rows = CloudArt.Length;
        int w = (int)Math.Round(cols * size), h = (int)Math.Round(rows * size);
        if (w < 2 || h < 2) return;
        for (var ty = 0; ty < h; ty++)
        {
            for (var tx = 0; tx < w; tx++)
            {
                var c = CloudArt[Math.Min(rows - 1, ty * rows / h)][Math.Min(cols - 1, tx * cols / w)] switch
                {
                    'h' => shades[0], 'l' => shades[1], 'm' => shades[2], 'd' or 's' => shades[3], 'o' => CloudLine, _ => 0u,
                };
                if (c != 0) p.Set(cx - (w / 2) + tx, cy - (h / 2) + ty, c);
            }
        }
    }

    /// <summary>When the cloud starts gathering after the strike, and how long it takes to form.</summary>
    private const double Gather = 0.3, Form = 1.4;

    /// <summary>
    /// The cloud forming after the strike (the player found it popping up all at once): wisps of the strike's smoke curling in from round the
    /// arch towards its top, then the cloud swelling out of them, a little too big, and settling to its size.
    /// </summary>
    private static void Forming(CapSheet p, int cx, int cy, double since)
    {
        var k = Clamp01((since - Gather) / (Form * 0.75));
        if (k < 1)
        {
            for (var n = 0; n < 14; n++)
            {
                var a = (n / 14.0 * 2 * Math.PI) + (k * 2.4);
                var r = (10 + (n % 3 * 4)) * (1 - k);
                double x = cx + (Math.Cos(a) * r * 1.3), y = cy + 6 - (k * 6) + (Math.Sin(a) * r * 0.6);
                // a puff of smoke two cells across, lit on top
                Put(p, x, y, Shades[n % 2]);
                Put(p, x + 1, y, Shades[1]);
                Put(p, x, y + 1, Shades[2]);
                Put(p, x + 1, y + 1, Shades[3]);
            }
        }

        var u = Clamp01((since - Gather - (Form * 0.35)) / (Form * 0.65));
        if (u <= 0) return;
        // swelling out a little past its size and settling back (an ease out with overshoot)
        const double C1 = 1.4, C3 = C1 + 1;
        var size = 1 + (C3 * Math.Pow(u - 1, 3)) + (C1 * Math.Pow(u - 1, 2));
        Cloud(p, cx, cy, false, Math.Max(0.15, size));
    }

    private const uint CloudLine = 0x1C1E30u;
    private static readonly uint[] Shades = [0xC4C8E0u, 0x8A90B0u, 0x5C6284u, 0x3C4060u];
    private static readonly uint[] LitShades = [0xFFF8D8u, 0xF0E4A0u, 0xB8AC80u, 0x6C6658u];

    /// <summary>The crystal in the arch, the burns round it and its little cloud.</summary>
    private static void Rest(CapSheet p, (int X, int Y) spot, CapTrialCrystal crystal, double phase, double since)
    {
        var show = Clamp01(since / 0.3);
        foreach (var path in Burns)
        {
            for (var i = 1; i < path.Length; i++)
            {
                var (ax, ay) = path[i - 1];
                var (bx, by) = path[i];
                var n = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay));
                for (var s = 0; s <= n; s++)
                {
                    double x = spot.X + ax + ((bx - ax) * s / (double)Math.Max(1, n)), y = spot.Y + ay + ((by - ay) * s / (double)Math.Max(1, n));
                    Put(p, x + 1, y, Scorch, 200 * show);
                    Put(p, x, y, Burnt, 255 * show);
                    // still glowing just after the strike
                    if (since < 2) CapTrial.Light(p, x, y, Ember, 200 * (1 - (since / 2)));
                }
            }
        }

        // now and then a spark runs along one of them
        var run = (phase * 0.35) % 1;
        var which = (int)Math.Floor(phase * 0.35) % Burns.Length;
        if (run < 0.4 && since > 1)
        {
            var path = Burns[which];
            var at = run / 0.4 * (path.Length - 1);
            var i = Math.Min(path.Length - 2, (int)at);
            var u = at - i;
            double x = spot.X + path[i].X + ((path[i + 1].X - path[i].X) * u), y = spot.Y + path[i].Y + ((path[i + 1].Y - path[i].Y) * u);
            Put(p, x, y, White);
            CapTrial.Light(p, x, y - 1, Spark, 200);
            CapTrial.Light(p, x, y + 1, Spark, 200);
        }

        var glint = phase % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);

        // the little cloud under the arch's top, drifting a cell; now and then it lights up inside and drops a spark onto the crystal
        if (since < Gather) return;
        var drift = R(Math.Sin(phase * 0.8));
        int cx = spot.X + drift, cy = spot.Y - 12;
        if (since < Gather + Form)
        {
            Forming(p, cx, cy, since);
            return;
        }

        var zap = (phase * 0.45) % 1;
        var flash = zap < 0.1;
        Cloud(p, cx, cy, flash);
        if (zap < 0.06 || (zap > 0.08 && zap < 0.1))
        {
            Bolt(p, cx, cy + 6, spot.X, spot.Y - 1, 300 + ((int)Math.Floor(phase * 0.45) % 7), 0, spot.Y, zap > 0.08);
        }

        // a spark crawling in it now and then
        var glow = (phase * 1.3) % 1;
        if (glow < 0.12)
        {
            var g = (int)Math.Floor(phase * 1.3);
            p.Set(cx - 3 + (g % 7), cy - 1 + (g % 2), Spark);
            p.Set(cx - 2 + (g % 7), cy + (g % 2), White);
        }
    }

    /// <summary>The Electrostal in its arch for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Spot(l, ctx) is { } spot) Rest(p, spot, crystal, phase, 30);
    }
}
