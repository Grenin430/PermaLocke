using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The second trial's Z-Crystal, the Lucharastal, punched into the Gimmighoul ruins (2026-10-18, prototype: the player's idea and sprites).
/// <para>
/// In the party's window: the Ultra Wormhole beats and flings out a Machamp and a cluster of Max Mushrooms, tumbling; they splash down on the
/// sea, Machamp standing, the mushrooms bobbing beside it. Machamp lifts them to its mouth and eats them in four bites, crumbs flying; a red
/// glow rises round it and it flickers white between itself and something far bigger, faster and faster, until a burst shows Gigantamax
/// Machamp, as tall as the sky, red clouds gathering over it. The Wormhole spits the crystal out in front of its fist; it winds up and punches
/// it off to the right, out of the window.
/// </para>
/// <para>
/// In the abyss's window it comes back in from the left as a streak, dives through the surface and drives itself point first into the narrow
/// column of the ruins: stone chips and dust fly, cracks run out of it glowing and go dark. It stays there, stuck in the stone, for the rest of
/// the run (<see cref="PaintRest"/>). Machamp shrinks back and is pulled into the Wormhole.
/// </para>
/// Facing right all through. Pixel art: sprites, specks and dither, no blur. A function of the moment: no clock and no random.
/// </summary>
public static class CapMachamp
{
    /// <summary>The second trial's crystal, the one this way is for.</summary>
    public const int Lucharastal = 813;

    /// <summary>How long it all starts before the film's own clock: the film's moments here are negative till then.</summary>
    public const double Lead = 7.6;

    /// <summary>How long it goes on after the film's own crystal part, before the cap's film: Machamp shrinking back and leaving.</summary>
    public const double After = 1.6;

    // The timeline, on the film's clock (the impact in the column is the film's).
    // Slowed down (2026-10-18, asked by the player: too fast to follow).
    private const double Beat1 = -7.2, Beat2 = -6.8, Out = -6.3, Splash = -5.1, Pop = Splash - 0.12, Grab = -4.3, Chomp = -3.6, Glow = -1.9, Grow = -1.1,
        Reveal = 1.1, Spit = 2.1, Windup = 3.0, Punch = 3.62, Strike = Punch + 0.2, Enter = Strike + 0.32, Hit = CapTrial.Hit, Shrink = 6.2, Leave = 7.0, Gone = 8.0;

    /// <summary>The frames the film holds still on: none, the punch and the impact in the column have their impact frames instead (<see cref="ImpactFrames"/>).</summary>
    internal static readonly (double At, double Span)[] Holds = [];

    /// <summary>The four bites, from the first.</summary>
    private static readonly double[] Bites = [0, 0.35, 0.7, 1.0, 1.35];

    private const int Small = 68, Giant = 20068, Shrooms = 20069;
    private const double SmallSize = 0.5, ShroomSize = 0.65, CrystalSize = 0.5, Stuck = 0.4;

    private const uint White = 0xFFFFFFu, Red = 0xFF2E52u, Rose = 0xFF8AB4u, Wine = 0x6A0E2Au, Ember = 0xFF9A3Cu, Foam = 0xCFF4FFu, Crumb = 0xEE4058u,
        Crack = 0x161C1Eu, CrackLip = 0xB8C0AAu, Chip = 0x8A9282u, ChipDark = 0x4E564Au, Dust = 0x9AA492u;

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>The cracks it leaves in the column: walks of cells out from where its point went in (local to it), and the step of each.</summary>
    private static readonly (int X, int Y, int Step)[] Cracks = MakeCracks();

    private static (int, int, int)[] MakeCracks()
    {
        var cells = new List<(int, int, int)>();
        double[] ways = [-1.2, -0.3, 0.45, 1.25, 1.6];
        for (var n = 0; n < ways.Length; n++)
        {
            double x = 0, y = 0, a = ways[n];
            var length = 3 + (CapArt.Hash(n, 61) % 3);
            for (var s = 1; s <= length; s++)
            {
                a += (Rnd(n * 13, s) - 0.5) * 0.9;
                x += Math.Cos(a);
                y += Math.Sin(a);
                cells.Add((R(x), R(y), s));
                if (s == 3 && n == 2)
                {
                    // a twig off it
                    double bx = x, by = y, b = a + (n % 4 == 0 ? 0.9 : -0.9);
                    for (var k = 1; k <= 3; k++)
                    {
                        bx += Math.Cos(b);
                        by += Math.Sin(b);
                        cells.Add((R(bx), R(by), s + k));
                    }
                }
            }
        }

        return [.. cells];
    }

    /// <summary>Where its point goes into the column, in the abyss's window; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Target(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, width) && width > 178 ? (x0 + 166, floor - 22) : null;

    /// <summary>Where it comes in from on the abyss's left, the way it flies and where its middle stops (its point in the stone).</summary>
    private static ((double X, double Y) From, (double X, double Y) Way, (double X, double Y) Stop)? Path(CapWorldLayout l, CapSeaContext ctx)
    {
        if (Target(l, ctx) is not { } target) return null;
        (double X, double Y) from = (-14, l.SurfaceY - 26);
        double dx = target.X - from.X, dy = target.Y - from.Y, length = Math.Sqrt((dx * dx) + (dy * dy));
        (double X, double Y) way = (dx / length, dy / length);
        // its middle: half its length back from a point three cells into the stone
        var half = CapTrialCrystal.W * Stuck;
        return (from, way, (target.X + (way.X * (3 - half)), target.Y + (way.Y * (3 - half))));
    }

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel == CapPanelKind.Party) Party(p, l, crystal, t);
        else if (l.Panel == CapPanelKind.Deep) Deep(p, l, ctx, crystal, t);
    }

    // ---------------------------------------------------------------- the party's window

    /// <summary>Where it stands: its column, and the row of its feet on the near half of the ring's top (as wide as the ring is now), bobbing.</summary>
    private static (int X, int Feet) Stance(CapWorldLayout l, (int X, int Y) core, double t) =>
        (Math.Max(34, core.X - 48), l.SurfaceY + R(Math.Sin(t * 2.2) * 0.7) + R(0.35 * RingHeight(t)));

    /// <summary>
    /// The impact frames, over everything else of the film in a window (<see cref="CapTrial"/> paints them last): the punch's, on the giant's
    /// fist, and the crystal's into the column; each window takes them, the one where the blow is with its figures and star, the other with
    /// its lines running off towards it.
    /// </summary>
    public static void Overlay(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double t)
    {
        if (l.Panel == CapPanelKind.Party)
        {
            (double X, double Y) far = (-90, l.SurfaceY * 0.45);
            if (CapFarSky.WormholeAt(l) is { } core && CritterArt.All.TryGetValue(Giant, out var giant))
            {
                var (cx, feet) = Stance(l, core, t);
                (double X, double Y) hold = (cx + 42, feet - 53);
                ImpactFrames(p, l, t - Punch, hold, (hold.X + 6, hold.Y), 0, giant, (cx, feet - 31), CrystalSize, 0);
            }

            ImpactFrames(p, l, t - Hit, far, null, 1, null, default, 0, 0);
        }
        else if (l.Panel == CapPanelKind.Deep)
        {
            ImpactFrames(p, l, t - Punch, (l.Cols + 90, l.SurfaceY * 0.45), null, 0, null, default, 0, 0);
            if (Path(l, ctx) is var (_, way, rest) && Target(l, ctx) is { } target)
            {
                ImpactFrames(p, l, t - Hit, rest, (target.X + 2, target.Y), 1, null, default, Stuck, Math.Atan2(way.Y, way.X));
            }
        }
    }

    private static void Party(CapSheet p, CapWorldLayout l, CapTrialCrystal crystal, double t)
    {
        if (CapFarSky.WormholeAt(l) is not { } core || t > Gone + 0.5) return;
        if (!CritterArt.All.TryGetValue(Small, out var small) || !CritterArt.All.TryGetValue(Giant, out var giant) || !CritterArt.All.TryGetValue(Shrooms, out var shrooms)) return;
        // they stand on a rubber ring on the sea, as in the games, bobbing with it: the small one on a small ring, the giant on a big one
        var surface = l.SurfaceY;
        var bob = R(Math.Sin(t * 2.2) * 0.7);
        var (cx, feet) = Stance(l, core, t);
        var giantFeet = feet;
        (double X, double Y) stand = (cx, feet - 16), big = (cx, giantFeet - 31), mouth = (cx + 2, feet - 28), floats = (cx + 27, surface - 7);
        (double X, double Y) hold = (cx + 42, giantFeet - 53);
        Ring(p, cx, surface + bob, t);

        // the wormhole: two beats pulling red light in, the burst that flings them out, a smaller one as it spits the crystal
        foreach (var beat in (ReadOnlySpan<double>)[Beat1, Beat2])
        {
            var k = Math.Exp(-Math.Pow((t - beat) / 0.1, 2));
            CapTrial.Shine(p, core.X, core.Y, 18 + (10 * k), 15 + (8 * k), Red, 160 * k);
        }

        if (t > Beat1 - 0.3 && t < Out)
        {
            for (var n = 0; n < 30; n++)
            {
                var age = t - (Beat1 - 0.3) - (n / 30.0 * 1.0);
                if (age < 0 || age > 0.6) continue;
                var a = Rnd(n, 7) * 2 * Math.PI;
                var r = (1 - (age / 0.6)) * (24 + (Rnd(n, 8) * 20));
                CapTrial.Light(p, core.X + (Math.Cos(a) * r), core.Y + (Math.Sin(a) * r * 0.7), n % 2 == 0 ? Rose : White, 220 * (age / 0.6));
            }
        }

        Burst(p, core.X, core.Y, t - Out, 1);
        Burst(p, core.X, core.Y, t - Spit, 0.6);

        // the red clouds of a Gigantamax over it while it lasts
        Clouds(p, l, t);

        // Machamp
        if (t >= Out && t < Grow)
        {
            var pose = 0;
            double x = stand.X, y = stand.Y, scale = SmallSize, tilt = 0;
            if (t < Splash)
            {
                // flung out of the wormhole, tumbling, down onto the sea
                var u = (t - Out) / (Splash - Out);
                x = core.X + ((stand.X - core.X) * u);
                y = core.Y + ((stand.Y - core.Y) * u) - (30 * Math.Sin(Math.PI * u));
                scale = SmallSize * (0.2 + (0.8 * Clamp01((t - Out) / 0.25)));
                tilt = -40 * Math.Cos(u * 3 * Math.PI) * (1 - u);
            }
            else if (t < Splash + 0.25) pose = 1;

            foreach (var bite in Bites) if (t >= Chomp + bite && t < Chomp + bite + 0.12) pose = 1;

            // the red glow coming over it before it grows
            var glow = Clamp01((t - Glow) / (Grow - Glow));
            if (glow > 0) Aura(p, stand.X, stand.Y, 14, 18, t, glow);
            CapVisitors.Centred(p, small, pose + 0.5, R(x), R(y), scale, false, Red, glow * 0.45, tilt);
        }
        else if (t >= Grow && t < Reveal)
        {
            // the change: white, flickering between itself and the giant, faster and faster
            var T = t - Grow;
            var phase = (2.5 * T) + (11.5 * T * T / (2 * (Reveal - Grow)));
            var k = T / (Reveal - Grow);
            Aura(p, stand.X, stand.Y - (10 * k), 14 + (18 * k), 18 + (16 * k), t, 1);
            if ((int)Math.Floor(phase * 2) % 2 == 1) CapVisitors.Centred(p, giant, 0.5, R(big.X), R(big.Y), 1, false, White, 1);
            else CapVisitors.Centred(p, small, 0.5, R(stand.X), R(stand.Y), SmallSize, false, White, 1);
            for (var r = 0; r < 3; r++)
            {
                var ring = 50 - (((T * 1.6) + (r / 3.0)) % 1 * 46);
                CapTrial.Ring(p, stand.X, surface - 1, ring, 0.18, Rose, 180 * k);
            }

            Churn(p, stand.X, surface, t, k);
        }
        else if (t >= Reveal && t < Shrink)
        {
            // Gigantamax Machamp: standing, breathing, its arms smouldering; it winds up and punches
            var since = t - Reveal;
            var pose = t >= Punch && t < Punch + 0.55 ? 2 : t >= Windup && t < Punch ? 1 : 0;
            var breath = pose == 0 ? R(Math.Sin(t * 2.6) * 0.8) : 0;
            Embers(p, big.X, big.Y, t);
            var head = (X: big.X + 3, Y: big.Y - 35 + breath);
            var clouds = Clamp01(since / 0.6);
            HeadClouds(p, head.X, head.Y, t, clouds, false);
            CapVisitors.Centred(p, giant, pose + 0.5, R(big.X), R(big.Y) + breath, 1, false, White, since < 0.35 ? 1 - (since / 0.35) : 0);
            HeadClouds(p, head.X, head.Y, t, clouds, true);
            if (since < 0.5) Churn(p, stand.X, surface, t, 1 - (since / 0.5));
        }
        else if (t >= Shrink && t < Gone)
        {
            if (t < Leave)
            {
                // back down: red, flickering the other way, in a mist
                var T = t - Shrink;
                var fade = 1 - (T / (Leave - Shrink));
                HeadClouds(p, big.X + 3, big.Y - 35, t, fade, false);
                HeadClouds(p, big.X + 3, big.Y - 35, t, fade, true);
                if ((int)Math.Floor(T * 14) % 2 == 0) CapVisitors.Centred(p, giant, 0.5, R(big.X), R(big.Y), 1, false, Red, 0.7, 0, fade);
                else CapVisitors.Centred(p, small, 0.5, R(stand.X), R(stand.Y), SmallSize, false, Red, 0.5);
                Mist(p, big.X, big.Y, T / (Leave - Shrink));
            }
            else
            {
                // pulled back into the wormhole, turning, shrinking
                var u = Math.Pow((t - Leave) / (Gone - Leave), 1.6);
                var x = stand.X + ((core.X - stand.X) * u);
                var y = stand.Y + ((core.Y - stand.Y) * u) - (12 * Math.Sin(Math.PI * u));
                CapVisitors.Centred(p, small, 0.5, R(x), R(y), SmallSize * (1 - (0.85 * u)), false, Red, 0.3 * (1 - u), 40 * Math.Sin(u * 9));
                CapTrial.Shine(p, core.X, core.Y, 16, 13, Red, 120 * u);
            }
        }

        // the splash as it comes down, and the mushrooms'
        Splashdown(p, stand.X, surface, t - Splash, 1);
        Splashdown(p, floats.X, surface, t - Splash - 0.15, 0.6);

        // the Max Mushrooms: flung out, bobbing on the sea, lifted to its mouth and eaten
        var bitesTaken = 0;
        foreach (var bite in Bites) if (t >= Chomp + bite) bitesTaken++;
        if (t >= Out + 0.08 && bitesTaken < Bites.Length)
        {
            double x = floats.X, y = floats.Y + R(Math.Sin(t * 4) * 0.6), scale = ShroomSize, tilt = 0;
            if (t < Splash + 0.15)
            {
                var u = (t - Out - 0.08) / (Splash + 0.07 - Out);
                x = core.X + ((floats.X - core.X) * u);
                y = core.Y + ((floats.Y - core.Y) * u) - (40 * Math.Sin(Math.PI * u));
                scale = ShroomSize * (0.2 + (0.8 * Clamp01((t - Out) / 0.3)));
                tilt = 60 * Math.Sin(u * 4 * Math.PI) * (1 - u);
            }
            else if (t >= Grab)
            {
                var u = Ease(Clamp01((t - Grab) / (Chomp - Grab)));
                x = floats.X + ((mouth.X + 6 - floats.X) * u);
                y = floats.Y + ((mouth.Y + 3 - floats.Y) * u);
            }

            CapVisitors.Centred(p, shrooms, Math.Max(0, bitesTaken - 1) + 0.5, R(x), R(y), scale, false, White, 0, tilt);
        }

        foreach (var bite in Bites) Crumbs(p, mouth.X + 6, mouth.Y + 1, t - Chomp - bite, (int)(bite * 100));

        // the crystal: spat out of the wormhole into the air in front of the fist, then punched away to the right
        if (t >= Spit && t < Enter)
        {
            double x, y, spin;
            if (t < Punch)
            {
                var u = EaseOut((t - Spit) / 0.6);
                x = core.X + ((hold.X - core.X) * u);
                y = core.Y + ((hold.Y - core.Y) * u) + (t > Spit + 0.6 ? Math.Sin((t - Spit) * 5) * 1.2 : 0);
                spin = (t - Spit) * 9;
                CapTrial.Shine(p, x, y, 9, 7, crystal.Glow, 80);
            }
            else
            {
                var k = Math.Max(0, t - Strike);
                x = hold.X + (420 * k);
                y = hold.Y + (30 * k);
                spin = 0;
                // its streak
                for (var d = 0; d < 46; d++)
                {
                    var fade = 1 - (d / 46.0);
                    CapTrial.Light(p, x - 4 - d, y, d < 10 ? White : crystal.Shine, 230 * fade);
                    if (d % 3 == 0) CapTrial.Light(p, x - 6 - d, y + ((d / 3 % 2 == 0) ? -2 : 2), crystal.Glow, 150 * fade);
                }
            }

            crystal.Paint(p, R(x), R(y), CrystalSize, spin, t > Windup ? 0.6 : 0.3, -1);
        }

        // the fist gathering itself, and the punch
        if (t >= Windup && t < Punch)
        {
            var k = (t - Windup) / (Punch - Windup);
            for (var n = 0; n < 18; n++)
            {
                var a = (n / 18.0 * 2 * Math.PI) + (k * 3);
                var r = 22 * (1 - k) + 3;
                CapTrial.Light(p, hold.X - 10 + (Math.Cos(a) * r), hold.Y + (Math.Sin(a) * r * 0.8), n % 2 == 0 ? Ember : White, 230 * k);
            }
        }

        Impact(p, l, hold.X - 4, hold.Y, t - Strike, crystal);
        CapTrial.Shake(p, t - Reveal, 3, 0.5);
        CapTrial.Shake(p, t - Strike, 4, 0.4);
        CapTrial.Shake(p, t - Splash, 1, 0.2);
    }

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    /// <summary>
    /// The rubber ring they stand on: it pops up out of nothing just before Machamp comes down (bigger than it is, then settling), grows with
    /// it into the giant's, shrinks back with it and goes with a pop as Machamp is pulled away. Red and white quarters like a lifebuoy, lit on
    /// top, shaded under, its lower edge in the water.
    /// </summary>
    /// <summary>How tall the ring is drawn at a moment (half its height): small under Machamp, big under the giant, between them as it changes.</summary>
    private static double RingHeight(double t) => 4.5 + (2.5 * Grown(t));

    /// <summary>How far the ring has grown into the giant's: 0 small, 1 big.</summary>
    private static double Grown(double t) => t < Grow ? 0 : t < Reveal ? Ease((t - Grow) / (Reveal - Grow)) : t < Shrink ? 1 : 1 - Ease((t - Shrink) / (Leave - Shrink));

    private static void Ring(CapSheet p, double cx, double cy, double t)
    {
        if (t < Pop || t > Leave + 0.15) return;
        var pop = t - Pop;
        var swell = pop < 0.1 ? 1.25 * (pop / 0.1) : pop < 0.22 ? 1.25 - (0.25 * ((pop - 0.1) / 0.12)) : 1;
        var giant = Grown(t);
        if (t > Leave) swell = 1 - ((t - Leave) / 0.15);
        if (pop < 0.3) CapTrial.Ring(p, cx, cy, 6 + (pop * 70), 0.3, White, 220 * (1 - (pop / 0.3)));

        double rx = (15 + (13 * giant)) * swell, ry = (4.5 + (2.5 * giant)) * swell, hx = rx - ((6 + (4 * giant)) * swell), hy = ry - ((2.6 + giant) * swell);
        Float(p, cx, cy, rx, ry, hx, hy, (int)Math.Round((2 + giant) * swell));
    }

    /// <summary>
    /// A rubber ring on the water: red and white quarters like a lifebuoy, its top lit, its tube's side under it shaded, a dark edge. Shared with
    /// the other trials' visitors that stand on the sea (<see cref="CapShaymin"/>).
    /// </summary>
    internal static void Float(CapSheet p, double cx, double cy, double rx, double ry, double hx, double hy, int side)
    {
        if (rx < 1 || ry < 1) return;
        bool In(int x, int y)
        {
            double dx = (x - cx) / rx, dy = (y - cy) / ry, ix = (x - cx) / Math.Max(0.5, hx), iy = (y - cy) / Math.Max(0.5, hy);
            return (dx * dx) + (dy * dy) <= 1 && (hx <= 0.5 || (ix * ix) + (iy * iy) > 1);
        }

        // the tube's side under its top: drawn wherever the top shifted down a cell or more would be
        bool Side(int x, int y)
        {
            for (var k = 1; k <= side; k++) if (In(x, y - k)) return true;
            return false;
        }

        bool Any(int a, int b) => In(a, b) || Side(a, b);

        uint Quarter(int x, int y) =>
            (int)Math.Floor(((Math.Atan2((y - cy) / ry, (x - cx) / rx) / (2 * Math.PI)) + 1.0625) * 8) % 2 == 0 ? 0xE8383Cu : 0xF4F0EAu;

        for (var y = (int)Math.Floor(cy - ry) - 1; y <= cy + ry + side + 1; y++)
        {
            for (var x = (int)Math.Floor(cx - rx) - 1; x <= cx + rx + 1; x++)
            {
                var top = In(x, y);
                if (!top && !Side(x, y)) continue;
                var edge = !Any(x - 1, y) || !Any(x + 1, y) || !Any(x, y - 1) || !Any(x, y + 1);
                uint c;
                if (edge) c = 0x4A1418u;
                else if (top)
                {
                    c = Quarter(x, y);
                    var up = (y - cy) / ry;
                    if (!In(x, y - 1)) c = CapSheet.Mix(c, White, 0.5);
                    else if (up > 0.3 && !In(x, y + 1)) c = CapSheet.Mix(c, 0x401018u, 0.2);
                }
                else
                {
                    // the side faces down and away from the light; the hole's far wall is in shadow too
                    var k = 1;
                    while (k < side && !In(x, y - k)) k++;
                    c = CapSheet.Mix(Quarter(x, y - k), 0x401018u, 0.35 + (0.12 * k));
                }

                p.Set(x, y, c);
            }
        }
    }

    /// <summary>
    /// The three storm clouds of a Gigantamax over its head, going round it: dark red puffs, lit on top, the one behind smaller and darker,
    /// drawn in two passes (behind the giant, then in front of it).
    /// </summary>
    private static void HeadClouds(CapSheet p, double hx, double hy, double t, double k, bool front)
    {
        if (k <= 0) return;
        for (var n = 0; n < 3; n++)
        {
            var a = (t * 1.3) + (n * 2 * Math.PI / 3);
            var depth = Math.Sin(a);
            if ((depth > 0) != front) continue;
            var s = (1.15 + (0.25 * depth)) * (0.4 + (0.6 * k));
            double cx = hx + (Math.Cos(a) * 16), cy = hy + (depth * 2) + (Math.Sin((t * 2.1) + n) * 0.8);
            Cloud(p, cx, cy, s, depth);
        }
    }

    /// <summary>One cloud: four round puffs run together, a dark edge round them, lit from above.</summary>
    private static void Cloud(CapSheet p, double cx, double cy, double s, double depth)
    {
        bool In(int x, int y)
        {
            double dx = x - cx, dy = y - cy;
            return Puff(dx + (4 * s), dy - (1 * s), 3.2 * s) || Puff(dx, dy + (1.5 * s), 4.4 * s) || Puff(dx - (4.2 * s), dy - (0.5 * s), 3.4 * s)
                || Puff(dx + (0.5 * s), dy - (2.5 * s), 3 * s);
        }

        static bool Puff(double dx, double dy, double r) => (dx * dx) + (dy * dy * 1.3) <= r * r;

        var dark = 0.3 * (1 - depth) / 2;
        for (var y = (int)Math.Floor(cy - (8 * s)); y <= cy + (8 * s); y++)
        {
            for (var x = (int)Math.Floor(cx - (10 * s)); x <= cx + (10 * s); x++)
            {
                if (!In(x, y)) continue;
                uint c = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1) ? 0x3A0612u
                    : !In(x, y - 2) ? 0xF0485Eu : !In(x, y - 4) ? 0xC01E36u : !In(x, y + 2) ? 0x5E0A1Cu : 0x8E1428u;
                p.Set(x, y, CapSheet.Mix(c, 0x2A0410u, dark));
            }
        }
    }

    /// <summary>
    /// The impact frames of the punch (anime's sakuga trick, the player's ask), drawn like an animator would: for a fifth of a second the film
    /// stops being itself, four frames of 0.05 s, each a different stark treatment of the same moment, in both windows (the party's with the
    /// giant, the blow and its star; the abyss's with only its own scene, the lines running off towards the blow beyond its right edge):
    /// <list type="number">
    /// <item>the negative: the whole scene in three tones inverted (light to ink, middle to red, dark to paper), a ragged star of paper on the
    /// blow and focus lines of paper closing on it;</item>
    /// <item>the line art: paper with only the scene's edges inked, the giant a solid ink shape, red tapered focus lines, the star red with an
    /// ink core;</item>
    /// <item>the silhouette: ink, the giant in paper with a red rim, the crystal in paper, thick paper focus lines, a broken red shock ring
    /// and shards flying;</item>
    /// <item>the positive: three tones the right way round (dark to ink, middle to red, light to paper), the star smaller, the lines thinning
    /// out.</item>
    /// </list>
    /// Then the film carries on with the flash and the crystal flies.
    /// </summary>
    /// <param name="shape">Where the crystal is (its shape is drawn there when <paramref name="star"/> is given, at <paramref name="size"/> and
    /// turned by <paramref name="roll"/>).</param>
    /// <param name="star">Where the blow lands, in this window; null when it is in the other and only the lines reach this one, from
    /// <paramref name="shape"/>.</param>
    private static void ImpactFrames(CapSheet p, CapWorldLayout l, double age, (double X, double Y) shape, (double X, double Y)? star, int seed,
        CritterArt? giant, (double X, double Y) big, double size, double roll)
    {
        if (age < 0 || age >= Frames * 0.05) return;
        var frame = (int)(age / 0.05);
        var salt = frame + (seed * 17);

        // the scene itself, turned into the frame's tones
        switch (frame)
        {
            case 0:
                Each(p, (x, y) =>
                {
                    var v = Luma(p.At(x, y));
                    p.Set(x, y, v > 104 ? Ink : Paper);
                });
                break;
            case 1:
                var luma = new double[p.W * p.H];
                for (var y = 0; y < p.H; y++) for (var x = 0; x < p.W; x++) luma[(y * p.W) + x] = Luma(p.At(x, y));
                Each(p, (x, y) =>
                {
                    var v = luma[(y * p.W) + x];
                    var right = x + 1 < p.W ? luma[(y * p.W) + x + 1] : v;
                    var down = y + 1 < p.H ? luma[((y + 1) * p.W) + x] : v;
                    p.Set(x, y, Math.Abs(v - right) > 50 || Math.Abs(v - down) > 50 ? Ink : Paper);
                });
                break;
            case 2:
                Each(p, (x, y) => p.Set(x, y, Ink));
                break;
            default:
                Each(p, (x, y) =>
                {
                    var v = Luma(p.At(x, y));
                    p.Set(x, y, v < 72 ? Ink : v < 100 ? Blood : Paper);
                });
                break;
        }

        // the focus lines: wedges sharp at the blow and thick at the frame's edge, closing on the blow (from the abyss, off past its right)
        var focus = star ?? shape;
        // (the negative's lines take the tone the window has least of, so they show on either)
        var papers = 0;
        for (var gy = 0; gy < 10; gy++) for (var gx = 0; gx < 10; gx++) if (p.At(Math.Min(p.W - 1, (gx * p.W / 10) + 3), Math.Min(p.H - 1, (gy * p.H / 10) + 3)) == Paper) papers++;
        var lines = frame switch { 0 => papers > 50 ? Ink : Paper, 1 => Blood, 2 => Paper, _ => Ink };
        var count = frame == 3 ? 30 : 64;
        for (var n = 0; n < count; n++)
        {
            var a = (n + (Rnd(n, 111 + salt) * 0.8)) / count * 2 * Math.PI;
            var from = (star is null ? 100 : 14) + (Rnd(n, 112 + salt) * 22);
            var width = (frame == 2 ? 5 : 3) * (0.4 + Rnd(n, 113));
            if (frame == 3) width *= 0.5;
            Wedge(p, l, focus, a, from, 330, width, lines);
        }

        if (star is not { } hold) return;

        // the giant (when it is its blow) and the crystal, as the frame wants them
        var crystalTone = frame == 2 ? Paper : frame == 0 ? Paper : Ink;
        if (giant is not null && frame == 0)
        {
            CapVisitors.Centred(p, giant!, 2.5, R(big.X) + 1, R(big.Y) + 1, 1, false, Ink, 1);
            CapVisitors.Centred(p, giant!, 2.5, R(big.X) - 1, R(big.Y) - 1, 1, false, Blood, 1);
            CapVisitors.Centred(p, giant!, 2.5, R(big.X), R(big.Y), 1, false, Paper, 1);
        }

        if (giant is not null && frame == 1) CapVisitors.Centred(p, giant!, 2.5, R(big.X), R(big.Y), 1, false, Ink, 1);
        if (giant is not null && frame == 2)
        {
            CapVisitors.Centred(p, giant!, 2.5, R(big.X) - 1, R(big.Y) - 1, 1, false, Blood, 1);
            CapVisitors.Centred(p, giant!, 2.5, R(big.X), R(big.Y), 1, false, Paper, 1);
        }

        Diamond(p, shape, CapTrialCrystal.W * size, CapTrialCrystal.H * size, roll, crystalTone);

        // the star of the blow: ragged spikes, a core in another tone
        var (fill, core, edge) = frame switch { 0 => (Paper, Blood, Ink), 1 => (Blood, Ink, Ink), 2 => (Blood, Paper, Paper), _ => (Paper, Blood, Ink) };
        Star(p, hold.X, hold.Y, frame == 3 ? 0.6 : 1 + (frame * 0.15), salt, fill, core, edge);

        // a broken ring running out, and shards of the blow flying
        if (frame >= 2)
        {
            var r = frame == 2 ? 34 : 52;
            for (var i = 0; i < 160; i++)
            {
                var a = i / 160.0 * 2 * Math.PI;
                if ((int)(Rnd(i / 9, 121 + seed) * 3) == 0) continue;
                for (var w = 0; w < 2; w++) p.Set(R(hold.X + (Math.Cos(a) * (r + w))), R(hold.Y + (Math.Sin(a) * (r + w) * 0.7)), frame == 2 ? Blood : Ink);
            }
        }

        for (var n = 0; n < 16; n++)
        {
            var a = Rnd(n, 131 + seed) * 2 * Math.PI;
            var d = 18 + (frame * 14) + (Rnd(n, 132 + seed) * 20);
            double sx = hold.X + (Math.Cos(a) * d), sy = hold.Y + (Math.Sin(a) * d * 0.75);
            // a shard: a little triangle pointing out
            for (var k = 0; k < 4; k++)
            {
                for (var w = -((3 - k) / 2); w <= (3 - k) / 2; w++) p.Set(R(sx + (Math.Cos(a) * k) - (Math.Sin(a) * w)), R(sy + (Math.Sin(a) * k) + (Math.Cos(a) * w)), frame == 2 ? Paper : Ink);
            }
        }
    }

    private const int Frames = 4;
    private const uint Ink = 0x0A0608u, Paper = 0xFFF8F0u, Blood = 0xE0102Cu;

    /// <summary>A focus line: a wedge from a point, nothing at <paramref name="from"/>, <paramref name="width"/> wide at <paramref name="to"/>.</summary>
    private static void Wedge(CapSheet p, CapWorldLayout l, (double X, double Y) o, double a, double from, double to, double width, uint c)
    {
        double cos = Math.Cos(a), sin = Math.Sin(a) * 0.8;
        for (var r = from; r < to; r += 0.6)
        {
            double x = o.X + (cos * r), y = o.Y + (sin * r);
            if ((x < -8 && cos < 0) || (x > l.Cols + 8 && cos > 0) || (y < -8 && sin < 0) || (y > l.Rows + 8 && sin > 0)) break;
            var w = width * ((r - from) / (to - from));
            for (var s = -w / 2; s <= w / 2; s += 0.6) p.Set(R(x - (Math.Sin(a) * s)), R(y + (cos * s)), c);
        }
    }

    /// <summary>The crystal as a flat shape of one tone, turned by <paramref name="roll"/> (clockwise).</summary>
    private static void Diamond(CapSheet p, (double X, double Y) at, double w, double h, double roll, uint c)
    {
        double co = Math.Cos(roll), si = Math.Sin(roll);
        var reach = (int)Math.Ceiling(w) + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                double ux = (co * dx) + (si * dy), uy = (-si * dx) + (co * dy);
                if ((Math.Abs(ux) / w) + (Math.Abs(uy) / h) <= 1) p.Set(R(at.X) + dx, R(at.Y) + dy, c);
            }
        }
    }

    /// <summary>The star of a blow: fourteen spikes, long and short by turns and each its own length, an edge round it and a core inside.</summary>
    private static void Star(CapSheet p, double cx, double cy, double size, int seed, uint fill, uint core, uint edge)
    {
        const int Spikes = 14;
        double Reach(double a)
        {
            var u = ((a / (2 * Math.PI)) + 1) % 1 * Spikes * 2;
            var i = (int)u;
            double Tip(int k) => k % 2 == 0 ? (26 + (Rnd(k, 141 + seed) * 22)) * size : (8 + (Rnd(k, 142) * 5)) * size;
            return Tip(i % (Spikes * 2)) + ((Tip((i + 1) % (Spikes * 2)) - Tip(i % (Spikes * 2))) * (u - i));
        }

        bool In(int x, int y, double k)
        {
            double dx = x - cx, dy = (y - cy) / 0.8;
            return Math.Sqrt((dx * dx) + (dy * dy)) <= Reach(Math.Atan2(dy, dx)) * k;
        }

        var span = (int)(52 * size);
        for (var y = (int)cy - span; y <= cy + span; y++)
        {
            for (var x = (int)cx - span; x <= cx + span; x++)
            {
                if (!In(x, y, 1)) continue;
                var outline = !In(x - 1, y, 1) || !In(x + 1, y, 1) || !In(x, y - 1, 1) || !In(x, y + 1, 1);
                p.Set(x, y, outline ? edge : In(x, y, 0.45) ? core : fill);
            }
        }
    }

    /// <summary>A burst of the wormhole: a white core, a red ring running out, a streak across.</summary>
    private static void Burst(CapSheet p, double x, double y, double b, double size)
    {
        if (b < 0 || b > 0.6) return;
        var k = 1 - (b / 0.6);
        CapTrial.Shine(p, x, y, 24 * size * k, 20 * size * k, White, 255 * k);
        CapTrial.Ring(p, x, y, 5 + (b * 110 * size), 0.6, Red, 220 * k);
        CapTrial.Ring(p, x, y, 3 + (b * 70 * size), 0.6, Rose, 180 * k);
        for (var dx = -80; dx <= 80; dx++) CapTrial.Light(p, x + (dx * size), y, White, 220 * Math.Pow(1 - (Math.Abs(dx) / 80.0), 2) * k);
    }

    /// <summary>Red light rising round it: specks going up its sides, flickering, white at their heads.</summary>
    private static void Aura(CapSheet p, double cx, double cy, double rx, double ry, double t, double k)
    {
        for (var n = 0; n < 40; n++)
        {
            var age = ((t * 0.9) + Rnd(n, 21)) % 1;
            var side = Rnd(n, 22) < 0.5 ? -1 : 1;
            var x = cx + (side * rx * (0.6 + (0.5 * Rnd(n, 23)))) + (Math.Sin((age * 7) + n) * 1.5);
            var y = cy + ry - (age * ry * 2.6);
            if (Bayer[R(y) & 3, R(x) & 3] >= (1 - age) * 16 * k) continue;
            Put(p, x, y, age < 0.2 ? White : n % 3 == 0 ? Rose : Red, 230 * k);
        }

        CapTrial.Shine(p, cx, cy, rx * 1.4, ry * 1.2, Red, 70 * k);
    }

    /// <summary>The sea round its feet stirred up: foam rising in a ring.</summary>
    private static void Churn(CapSheet p, double cx, int surface, double t, double k)
    {
        for (var n = 0; n < 30; n++)
        {
            var a = (n / 30.0 * Math.PI) + (t * 2);
            var x = cx + (Math.Cos(a) * (20 + (6 * Math.Sin((t * 5) + n))));
            var h = (Math.Sin((t * 9) + (n * 1.7)) + 1) * 2.5 * k;
            for (var dy = 0; dy < h; dy++) Put(p, x, surface - dy, dy > h - 1.5 ? White : Foam, 200 * k);
        }
    }

    /// <summary>Its burning arms: embers going up off them now and then.</summary>
    private static void Embers(CapSheet p, double cx, double cy, double t)
    {
        for (var n = 0; n < 24; n++)
        {
            var age = ((t * 0.7) + Rnd(n, 31)) % 1;
            var fromX = cx + ((Rnd(n, 32) - 0.5) * 60);
            var fromY = cy - 10 + (Rnd(n, 33) * 24);
            var x = fromX + (Math.Sin((age * 6) + n) * 2);
            var y = fromY - (age * 22);
            if (age > 0.8 || Bayer[R(y) & 3, R(x) & 3] >= (1 - age) * 18) continue;
            Put(p, x, y, age < 0.3 ? 0xFFE070u : age < 0.55 ? Ember : Red, 220);
        }
    }

    /// <summary>The red mist it shrinks in.</summary>
    private static void Mist(CapSheet p, double cx, double cy, double k)
    {
        for (var n = 0; n < 10; n++)
        {
            var a = n / 10.0 * 2 * Math.PI;
            var r = 10 + (k * 30);
            Puff(p, cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r * 0.9), 5 + (k * 4), n % 2 == 0 ? Red : Wine, 150 * (1 - k));
        }
    }

    /// <summary>The red storm clouds of a Gigantamax across the top of the sky, rolling, with a flicker of red lightning in them now and then.</summary>
    private static void Clouds(CapSheet p, CapWorldLayout l, double t)
    {
        var k = Clamp01((t - Reveal + 0.4) / 0.8) * (1 - Clamp01((t - Shrink) / 0.8));
        if (k <= 0) return;
        for (var x = 0; x < l.Cols; x++)
        {
            var depth = (4 + (1.6 * Math.Sin((x * 0.13) + (t * 1.3))) + (1.2 * Math.Sin((x * 0.41) - (t * 0.8)))) * k;
            for (var y = 0; y < depth + 3; y++)
            {
                if (l.InNotch(x, y)) continue;
                var edge = y > depth - 1.5;
                if (y > depth && Bayer[y & 3, x & 3] >= (depth + 3 - y) * 5) continue;
                p.Set(x, y, edge ? Red : (x + y + (int)(t * 4)) % 9 == 0 ? 0x9A1838u : Wine, (int)(220 * k));
            }
        }

        var flick = (int)Math.Floor(t * 6);
        if (CapArt.Hash(flick, 41) % 3 == 0)
        {
            var x0 = (int)(CapArt.Hash(flick, 42) % (uint)Math.Max(1, l.Cols));
            for (var y = 2; y < 9; y++) Put(p, x0 + ((y * 3) % 5) - 2, y, White, 230 * k);
        }
    }

    /// <summary>Coming down on the sea: a crown of drops and rings.</summary>
    private static void Splashdown(CapSheet p, double x, int surface, double sp, double size)
    {
        if (sp < 0 || sp > 1.2) return;
        for (var n = 0; n < 30; n++)
        {
            var life = 0.4 + (Rnd(n, 9) * 0.4);
            if (sp > life) continue;
            var a = Math.PI * (0.15 + (Rnd(n, 3) * 0.7));
            var v = (20 + (Rnd(n, 5) * 40)) * size;
            var y = surface - (Math.Sin(a) * v * sp) + (110 * sp * sp);
            if (y > surface + 1) continue;
            Put(p, x - (Math.Cos(a) * v * sp), y, n % 3 != 0 ? Foam : White, 255 * (1 - (sp / life)));
        }

        for (var r = 0; r < 2; r++)
        {
            var k = sp - (r * 0.2);
            if (k <= 0 || k >= 1) continue;
            CapTrial.Ring(p, x, surface, 3 + (k * 34 * size), 0.08, White, 200 * (1 - k), false);
        }
    }

    /// <summary>Crumbs off a bite: red and pink bits flung from its mouth, falling.</summary>
    private static void Crumbs(CapSheet p, double x, double y, double age, int seed)
    {
        if (age < 0 || age > 0.6) return;
        for (var n = 0; n < 9; n++)
        {
            var a = -Math.PI * (0.1 + (Rnd(n, seed) * 0.8));
            var v = 14 + (Rnd(n, seed + 1) * 18);
            Put(p, x + (Math.Cos(a) * v * age), y + (Math.Sin(a) * v * age) + (60 * age * age), n % 3 == 0 ? 0xFFC6E2u : Crumb, 255 * (1 - (age / 0.6)));
        }
    }

    /// <summary>The punch landing: a white star on the crystal, rings out from it, lines of speed running off to the right.</summary>
    private static void Impact(CapSheet p, CapWorldLayout l, double x, double y, double age, CapTrialCrystal crystal)
    {
        if (age < 0 || age > 0.5) return;
        var k = 1 - (age / 0.5);
        if (age < 0.08) CapTrial.Flash(p, 120 * (1 - (age / 0.08)));
        CapTrial.Shine(p, x, y, 20 * k, 16 * k, White, 255 * k);
        for (var n = 0; n < 12; n++)
        {
            var a = n / 12.0 * 2 * Math.PI;
            var length = (n % 2 == 0 ? 16 : 9) * (0.6 + (age * 2));
            for (var d = 3; d < length; d++) CapTrial.Light(p, x + (Math.Cos(a) * d), y + (Math.Sin(a) * d * 0.8), d < length / 2 ? White : crystal.Shine, 240 * k);
        }

        CapTrial.Ring(p, x, y, 6 + (age * 90), 0.7, crystal.Shine, 220 * k);
        CapTrial.Ring(p, x, y, 3 + (age * 60), 0.7, White, 200 * k);
        for (var n = 0; n < 16; n++)
        {
            var ly = y + ((Rnd(n, 51) - 0.5) * 50);
            var x0 = x + 10 + (Rnd(n, 52) * 30) + (age * 300);
            for (var d = 0; d < 18 + (Rnd(n, 53) * 20); d++)
            {
                if (x0 + d < l.Cols) CapTrial.Light(p, x0 + d, ly, White, 200 * k * (1 - (d / 40.0)));
            }
        }
    }

    // ---------------------------------------------------------------- the abyss's window

    private static void Deep(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        // the giant's clouds reach over this sky too, and the punch's impact frames
        Clouds(p, l, t);
        if (Path(l, ctx) is not var (from, way, rest) || Target(l, ctx) is not { } target || t < Enter) return;
        var roll = Math.Atan2(way.Y, way.X);
        var surface = l.SurfaceY;

        if (t < Hit)
        {
            // in from the left as a streak, through the surface, at the column
            var u = (t - Enter) / (Hit - Enter);
            double x = from.X + ((rest.X - from.X) * u), y = from.Y + ((rest.Y - from.Y) * u);
            for (var d = 0; d < 60; d++)
            {
                double sx = x - (way.X * (6 + d)), sy = y - (way.Y * (6 + d));
                var fade = 1 - (d / 60.0);
                CapTrial.Light(p, sx, sy, d < 14 ? White : crystal.Shine, 230 * fade);
                CapTrial.Light(p, sx - way.Y, sy + way.X, crystal.Glow, 140 * fade);
                CapTrial.Light(p, sx + way.Y, sy - way.X, crystal.Glow, 140 * fade);
            }

            crystal.Paint(p, R(x), R(y), Stuck, 0, 0.8, -1, int.MaxValue, 255, 0, roll);
        }

        // the splash where it went through the surface, and its bubbles behind it in the water
        var cross = (surface - from.Y) / (rest.Y - from.Y);
        var at = Enter + ((Hit - Enter) * cross);
        var crossX = from.X + ((rest.X - from.X) * cross);
        Splashdown(p, crossX, surface, t - at, 0.8);
        for (var n = 0; n < 40; n++)
        {
            var u = cross + ((1 - cross) * (n / 40.0));
            var born = Enter + ((Hit - Enter) * u);
            var age = t - born;
            if (age < 0 || age > 1.6) continue;
            var bx = from.X + ((rest.X - from.X) * u) + ((Rnd(n, 71) - 0.5) * 5) + (Math.Sin((age * 6) + n) * 1.5);
            var by = from.Y + ((rest.Y - from.Y) * u) - (age * 24);
            if (by > surface + 1) Put(p, bx, by, n % 3 == 0 ? White : Foam, 190 * (1 - (age / 1.6)));
        }

        if (t >= Hit) InColumn(p, l, ctx, crystal, target, rest, roll, Math.Max(0, t - Hit - (Frames * 0.05)), t);
    }

    /// <summary>In the column: the crystal stuck in it, the cracks round it (glowing as they open, then dark), chips, dust and bubbles at first.</summary>
    private static void InColumn(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, (int X, int Y) target, (double X, double Y) rest, double roll,
        double hit, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _)) return;

        // the hole it made: the stone round its point broken in, dark
        for (var dy = -3; dy <= 3; dy++)
        {
            for (var dx = 0; dx <= 3; dx++)
            {
                if ((dx * dx) + (dy * dy * 0.8) <= 3.2 && OnColumn(target.X + dx - x0, floor - target.Y - dy)) p.Set(target.X + dx, target.Y + dy, dx + Math.Abs(dy) >= 2 ? CrackLip : Crack);
            }
        }

        // the cracks, only on the column's stone: dark, with a lit lip under each, glowing as they open
        var glow = Clamp01(1 - ((hit - 0.25) / 1.4));
        foreach (var (cx, cy, step) in Cracks)
        {
            int x = target.X + 2 + cx, y = target.Y + cy;
            if (!OnColumn(x - x0, floor - y) || hit * 30 < step) continue;
            if (OnColumn(x - x0, floor - y - 1)) p.Set(x, y + 1, CrackLip);
        }

        foreach (var (cx, cy, step) in Cracks)
        {
            int x = target.X + 2 + cx, y = target.Y + cy;
            var open = (hit * 30) - step;
            if (!OnColumn(x - x0, floor - y) || open < 0) continue;
            // once the glow of the blow has gone they stay angry (2026-10-18, asked by the player): red, throbbing out from the crystal
            var angry = CapSheet.Mix(0x5A0A0Eu, 0xFF3A2Au, 0.5 + (0.5 * Math.Sin((phase * 2.5) - (step * 0.6))));
            p.Set(x, y, glow > 0 ? CapSheet.Mix(angry, open < 2 ? White : crystal.Shine, glow) : angry);
        }

        // and smoke curling up out of them, dark
        var fuming = Clamp01((hit - 1) / 1.5);
        if (fuming > 0)
        {
            for (var n = 0; n < 6; n++)
            {
                var age = ((phase * 0.3) + (n / 6.0)) % 1;
                var (sx, sy) = n % 3 == 0 ? (6, -4) : n % 3 == 1 ? (8, 2) : (5, -7);
                var x = target.X + sx + (Math.Sin((age * 6) + n) * 2) + (age * 3);
                var y = target.Y + sy - (age * 26);
                Puff(p, x, y, 1.6 + (age * 3.6), n % 2 == 0 ? 0x7A6E76u : 0x9A8E96u, 230 * (1 - age) * fuming);
            }
        }

        if (hit < 1.5)
        {
            // the shock through the water: a ring that bends what is behind it, no light of its own; and the sand of the floor thrown up,
            // rolling away both ways along it and settling
            if (hit > 0 && hit < 0.9) CapTrial.Lens(p, target.X, target.Y, 4 + (hit * 170), 3 * (1 - (hit / 0.9)), 7, 0x101820u);
            for (var n = 0; n < 14; n++)
            {
                var side = n % 2 == 0 ? -1 : 1;
                var k = hit - (n / 2 * 0.06);
                if (k <= 0 || k > 1.5) continue;
                var reach = (1 - Math.Exp(-k * 2.2)) * (40 + (Rnd(n, 151) * 30));
                Puff(p, target.X + 4 + (side * reach), floor - 3 - (k * 7) - ((n / 2) % 3 * 2), 4 + (k * 6), 0xD2D0B0u, 190 * (1 - (k / 1.5)));
            }

            CapTrial.Shine(p, target.X, target.Y, 14, 12, crystal.Glow, 160 * (1 - (hit / 1.5)));

            // stone chips flung back out of the hole, slowed by the water, sinking
            for (var n = 0; n < 26; n++)
            {
                var life = 0.7 + (Rnd(n, 81) * 0.8);
                if (hit > life) continue;
                var a = Math.PI + ((Rnd(n, 82) - 0.5) * 2.2);
                var v = 20 + (Rnd(n, 83) * 40);
                var k = 1 - Math.Exp(-hit * 3);
                double x = target.X + (Math.Cos(a) * v * k / 3), y = target.Y + (Math.Sin(a) * v * k / 3) + (14 * hit * hit);
                Put(p, x, y, n % 3 == 0 ? ChipDark : Chip);
                if (n % 4 == 0) Put(p, x + 1, y, ChipDark);
            }

            for (var n = 0; n < 6; n++)
            {
                var k = hit - (n * 0.05);
                if (k > 0 && k < 1.3) Puff(p, target.X - 2 - (k * 8 * (1 + (n % 3))), target.Y + ((n - 2.5) * 2.5) - (k * 4), 3 + (k * 3), Dust, 150 * (1 - (k / 1.3)));
            }

            for (var n = 0; n < 16; n++)
            {
                var age = hit - (Rnd(n, 91) * 0.3);
                if (age < 0 || age > 1.4) continue;
                Put(p, target.X - 3 + ((Rnd(n, 92) - 0.5) * 10) + (Math.Sin((age * 6) + n) * 1.5), target.Y - (age * 30), n % 2 == 0 ? White : Foam, 200 * (1 - (age / 1.4)));
            }
        }

        // the crystal, jolting as it goes in, then still; its symbol glowing a little, a glint over it every eight seconds or so
        var jolt = hit < 0.25 ? R(Math.Sin(hit * 60) * 1.5 * (1 - (hit / 0.25))) : 0;
        var glint = (phase % 8) < 0.6 ? (phase % 8) / 0.6 : -1;
        crystal.Paint(p, R(rest.X) + jolt, R(rest.Y), Stuck, 0, hit < 0.4 ? 1 : 0.3, hit < 1 ? -1 : glint, int.MaxValue, 255, 0, roll, x0 + 167);
    }

    /// <summary>Whether a cell of the ruins (from their first column, up from their floor) is on the narrow column's face.</summary>
    private static bool OnColumn(int lx, int ly) => lx >= 167 && lx <= 176 && ly >= 1 && ly < CapGimmighoulRuins.Surface(lx) - 1;

    /// <summary>The Lucharastal stuck in the column for the rest of the run, once its trial is passed: its cracks dark, its glint now and then.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Path(l, ctx) is not var (_, way, rest) || Target(l, ctx) is not { } target) return;
        InColumn(p, l, ctx, crystal, target, rest, Math.Atan2(way.Y, way.X), 10, phase);
    }
}
