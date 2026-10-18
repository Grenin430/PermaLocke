using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The third trial's Z-Crystal, the Hidrostal, thrown into the Gimmighoul ruins by Mega Greninja (2026-10-18, prototype: the player's choice
/// and sprites). Its own film, slower than the others (it starts <see cref="Lead"/> seconds before the film's clock): the Ultra Wormhole
/// beats and the crystal is born in it and flies down over the abyss's sea along the film's own curve, slowed; Greninja's shadow streaks in
/// under the water, a geyser bursts up and Mega Greninja leaps out of it flipping, afterimages behind it, and catches it; lands standing on the water facing the ruins, the water shuriken on its back turning all the time; it forms a
/// second shuriken of water in its hand round the crystal and throws it: turning, it cuts into the sea and sticks in the face of the tall
/// wall, still turning, then bursts into drops and leaves the crystal set in the stone in a star of cracks. Greninja is gone in a cloud of
/// smoke, its shape away into the sky, leaving a log in its place (its Substitute, as in Super Smash Bros. Ultimate): it drops into the sea
/// and floats away to the right, out of the window. The crystal stays for the rest of the run (<see cref="PaintRest"/>). The spot is a stand-in until the player marks one.
/// </summary>
public static class CapGreninja
{
    /// <summary>The third trial's crystal.</summary>
    public const int Hidrostal = 809;

    /// <summary>How long it all starts before the film's own clock: the film's moments here are negative till then.</summary>
    public const double Lead = 2.6;

    private const int Standing = 10658, Flipping = 20658;
    private const double Size = 0.5, Held = 0.3, Stuck = 0.4;

    // On the film's clock, slowed (2026-10-18, asked by the player); the crystal sticks at the film's impact.
    private const double Beat1 = -2.3, Beat2 = -2.0, Born = -1.6, Shadow = -1.3, Geyser = -0.12, Leap = 0.0, Catch = 0.8, Lands = 1.7, Forms = 2.0, Throw = CapTrial.Hit - 0.9,
        Hit = CapTrial.Hit, Burst = Hit + 0.9, Vanish = Hit + 1.4;

    private const uint White = 0xFFFFFFu, Foam = 0xCFF4FFu, Water = 0x48CDE9u, WaterDeep = 0x23A1BAu, WaterEdge = 0x184F7Au, Streak = 0xDBE7E8u, Crack = 0x161C1Eu,
        Smoke = 0xD6D3DEu, SmokeLight = 0xF2F0F6u, SmokeShade = 0xA09CB0u, SmokeEdge = 0x8E8AA2u;

    /// <summary>Where the film's flight comes down to (it is caught just above): over the right of the ruins.</summary>
    public static (int X, int Y)? Catching(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, _, width) ? (x0 + Math.Min(105, width - 60), l.SurfaceY) : null;

    /// <summary>Where the shuriken sticks: the face of the tall wall over its arch.</summary>
    public static (int X, int Y)? Target(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) ? (x0 + 12, floor - 34) : null;

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    /// <summary>The moment of the film's own flight that this one shows: the same curve, slower, reaching the catch at <see cref="Catch"/>.</summary>
    private static double Flown(double t) => CapTrial.Born + ((t - Born) * (CapTrial.Splash - 0.15 - CapTrial.Born) / (Catch - Born));

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel == CapPanelKind.Party) Party(p, l, ctx, crystal, t);
        else if (l.Panel == CapPanelKind.Deep) Deep(p, l, ctx, crystal, t);
    }

    private static void Party(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (CapFarSky.WormholeAt(l) is not { } core) return;

        // the wormhole beats twice and bursts as the crystal is born in it
        foreach (var beat in (ReadOnlySpan<double>)[Beat1, Beat2])
        {
            var k = Math.Exp(-Math.Pow((t - beat) / 0.1, 2));
            CapTrial.Shine(p, core.X, core.Y, 18 + (10 * k), 15 + (8 * k), crystal.Glow, 170 * k);
        }

        var b = t - Born;
        if (b >= 0 && b < 0.6)
        {
            var k = 1 - (b / 0.6);
            CapTrial.Shine(p, core.X, core.Y, 22 * k, 18 * k, White, 255 * k);
            CapTrial.Ring(p, core.X, core.Y, 5 + (b * 100), 0.6, crystal.Shine, 220 * k);
        }

        Flight(p, l, ctx, crystal, t);
    }

    /// <summary>The crystal on the film's curve, slowed, with its light and a few sparks behind it.</summary>
    private static void Flight(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (t < Born || t >= Catch || CapTrial.Where(l, ctx, Hidrostal, Flown(t)) is not { } f) return;
        for (var n = 1; n <= 10; n++)
        {
            if (CapTrial.Where(l, ctx, Hidrostal, Flown(t - (n * 0.04))) is { } back) CapTrial.Light(p, back.P.X, back.P.Y, n < 4 ? White : crystal.Shine, 220 - (n * 18));
        }

        CapTrial.Shine(p, f.P.X, f.P.Y, (12 * f.Scale) + 6, (10 * f.Scale) + 5, crystal.Glow, 130);
        crystal.Paint(p, R(f.P.X), R(f.P.Y), f.Scale, f.Spin, 0, -1);
    }

    private static void Deep(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        Flight(p, l, ctx, crystal, t);
        if (Catching(l, ctx) is not { } over || Target(l, ctx) is not { } target || t < Shadow) return;
        var surface = l.SurfaceY;
        (double X, double Y) from = (over.X - 10, surface + 12), stand = (over.X + 26, surface - 23);
        var grab = CapTrial.Where(l, ctx, Hidrostal, Flown(Catch))?.P ?? (over.X, surface - 14);
        (double X, double Y) hand = (stand.X - 20, stand.Y + 12), back = (stand.X + 6, stand.Y - 10);
        CritterArt.All.TryGetValue(Standing, out var standing);
        CritterArt.All.TryGetValue(Flipping, out var flipping);

        // its shadow under the water first, fast from the right, a wake of bubbles behind it
        if (t < Leap && standing is not null)
        {
            var u = Clamp01((t - Shadow) / (Geyser - Shadow));
            var e = 1 - Math.Pow(1 - u, 2);
            double x = l.Cols + 30 + ((from.X - l.Cols - 30) * e), y = surface + 34 - (14 * e);
            CapVisitors.Centred(p, standing, 0.5, R(x), R(y), Size, true, 0x0A1E3Au, 1, -12, 0.55 * (1 - Clamp01((t - Geyser) / 0.15)));
            for (var n = 0; n < 16; n++)
            {
                var age = ((t * 2) + (n / 16.0)) % 1;
                var at = Math.Max(0, u - (age * 0.35));
                var bx = l.Cols + 30 + ((from.X - l.Cols - 30) * (1 - Math.Pow(1 - at, 2))) + 14;
                Bubble(p, bx + (Math.Sin(n * 2.1) * 3), surface + 34 - (14 * at) - (age * 12), n % 4 == 0 ? 2 : 1, 200 * (1 - age));
            }

            // the wake it draws on the surface above it
            for (var d = 0; d < 26; d++)
            {
                var spread = d * 0.45;
                Put(p, x + 4 + d, surface - (d % 3 == 0 ? 1 : 0), d % 2 == 0 ? White : Foam, 200 * (1 - (d / 26.0)));
                Put(p, x + 4 + d, surface + 1 + (spread * 0.2), Foam, 120 * (1 - (d / 26.0)));
            }
        }

        Geyser_(p, from.X, surface, t - Geyser);

        // Greninja: out of the geyser flipping, afterimages behind it and water dripping off it, through the catch, down onto the water;
        // standing with its shuriken turning
        if (t >= Leap && t < Lands && flipping is not null)
        {
            for (var n = 3; n >= 0; n--)
            {
                var when = t - (n * 0.05);
                if (when < Leap) continue;
                var (x, y) = Flip(from, grab, stand, when);
                var turn = 540 * (when - Leap) / (Lands - Leap);
                if (n > 0) CapVisitors.Centred(p, flipping, 0.5, R(x), R(y), Size, true, Water, 0.7, turn, 0.5 - (n * 0.12));
                else CapVisitors.Centred(p, flipping, 0.5, R(x), R(y), Size, true, White, 0, turn);
                if (n == 0 && t >= Catch) crystal.Paint(p, R(x), R(y), Held, (t - Catch) * 6, 0.5);
                if (n == 0 && t >= Catch && t < Catch + 0.3) CapTrial.Star(p, x, y, White, 255 * (1 - ((t - Catch) / 0.3)), 4);
            }

            for (var n = 0; n < 14; n++)
            {
                var born = Leap + (n * 0.1);
                var age = t - born;
                if (age < 0 || age > 0.6) continue;
                var (dx, dy) = Flip(from, grab, stand, born);
                Drop(p, dx + ((Rnd(n, 71) - 0.5) * 10), dy + 6 + (age * age * 120), 0, age * 240, 220 * (1 - (age / 0.6)));
            }
        }
        else if (t >= Lands && t < Vanish + 0.08 && standing is not null)
        {
            var lean = t >= Throw - 0.4 && t < Throw ? 2 : t >= Throw && t < Throw + 0.3 ? -2 : 0;
            var bob = R(Math.Sin(t * 3));
            Shuriken(p, back.X + lean, back.Y + bob, 15, t * 4, false);
            Ripples(p, stand.X, surface, (t - Lands) % 1.2, 0.5);
            CapVisitors.Centred(p, standing, 0.5, R(stand.X) + lean, R(stand.Y) + bob, Size, true, White, t < Lands + 0.1 ? 0.6 : 0);
        }

        Splash(p, stand.X, surface, t - Lands, 0.6);

        // the thrown shuriken: water spun into its hand round the crystal, growing; thrown, turning down into the sea and into the wall,
        // still turning there until it bursts
        if (t >= Forms && t < Burst)
        {
            var size = Clamp01((t - Forms) / (Throw - 0.4 - Forms));
            double x = hand.X, y = hand.Y;
            if (t < Throw)
            {
                for (var n = 0; n < 36; n++)
                {
                    var age = ((t * 1.1) + (n / 36.0)) % 1;
                    var a = (n * 2.4) + (age * 6);
                    var r = 26 * (1 - age);
                    Put(p, x + (Math.Cos(a) * r), y + (Math.Sin(a) * r * 0.8), n % 4 == 0 ? White : n % 2 == 0 ? Foam : Water, 230 * age * (1 - (size * 0.6)));
                }

                CapTrial.Shine(p, x, y, 14 * size, 12 * size, crystal.Glow, 120 * size);
                if (t > Throw - 0.4) x += 4 * (t - Throw + 0.4) / 0.4;
            }
            else if (t < Hit)
            {
                var u = (t - Throw) / (Hit - Throw);
                (x, y) = Thrown(hand, target, u);
                Helix(p, hand, target, u, t);
            }
            else
            {
                x = target.X;
                y = target.Y;
            }

            Shuriken(p, x, y, 18 * (0.2 + (0.8 * size)), t * (t < Throw ? 7 : 11), true);
            crystal.Paint(p, R(x), R(y), Held, 0, 0.7, -1);
        }

        // where it cut through the surface on its way down: the water opened in a V and thrown up
        var cross = (surface - hand.Y) / (target.Y - hand.Y);
        var cut = t - Throw - ((Hit - Throw) * cross);
        var cutX = hand.X + ((target.X - hand.X) * cross);
        Splash(p, cutX, surface, cut, 1.1);
        if (cut >= 0 && cut < 0.5)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                for (var d = 0; d < 14; d++) Put(p, cutX + (side * d * (1 + (cut * 2))), surface - (d * 0.7 * (1 - (cut * 2))), d % 2 == 0 ? White : Foam, 230 * (1 - (cut / 0.5)));
            }
        }

        if (t >= Hit)
        {
            var bursting = t - Burst;
            if (bursting >= 0 && bursting < 0.7)
            {
                for (var n = 0; n < 40; n++)
                {
                    var a = n / 40.0 * 2 * Math.PI;
                    var r = 4 + (bursting * (30 + (Rnd(n, 5) * 24)));
                    Put(p, target.X + (Math.Cos(a) * r), target.Y + (Math.Sin(a) * r * 0.8) + (bursting * bursting * 18), n % 3 == 0 ? White : Water, 230 * (1 - (bursting / 0.7)));
                }

                CapTrial.Ring(p, target.X, target.Y, 6 + (bursting * 60), 0.8, Foam, 200 * (1 - (bursting / 0.7)), false);
            }

            Set(p, target, crystal, t, t >= Burst);
        }

        // gone: a cloud of smoke where it stood, its shape away into the sky, and the log it left in its place
        Escape(p, flipping, stand, t - Vanish);
        Substitute(p, l, stand, t - Vanish);
        Smoke_(p, stand, t - Vanish);
        Leaves(p, stand, surface, t - Vanish);
    }

    /// <summary>Where it is on its leap: out of the geyser up to the catch, then over in an arc down onto the water.</summary>
    private static (double X, double Y) Flip((double X, double Y) from, (double X, double Y) grab, (double X, double Y) stand, double t)
    {
        if (t < Catch)
        {
            var u = (t - Leap) / (Catch - Leap);
            return (from.X + ((grab.X - from.X) * u), from.Y + ((grab.Y + 4 - from.Y) * (1 - Math.Pow(1 - u, 2))));
        }

        var v = Clamp01((t - Catch) / (Lands - Catch));
        return (grab.X + ((stand.X - grab.X) * v), grab.Y + 4 + ((stand.Y - grab.Y - 4) * v) - (16 * Math.Sin(Math.PI * v)));
    }

    /// <summary>Where the thrown shuriken is: a slight arc from its hand to the wall.</summary>
    private static (double X, double Y) Thrown((double X, double Y) from, (int X, int Y) to, double u) =>
        (from.X + ((to.X - from.X) * u), from.Y + ((to.Y - from.Y) * u) - (10 * Math.Sin(Math.PI * u)));

    /// <summary>Two strands of water twisting round the shuriken's way behind it.</summary>
    private static void Helix(CapSheet p, (double X, double Y) from, (int X, int Y) to, double u, double t)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, length = Math.Sqrt((dx * dx) + (dy * dy));
        for (var d = 1; d <= 40; d++)
        {
            var was = u - (d * 0.012);
            if (was < 0) break;
            var (x, y) = Thrown(from, to, was);
            var fade = 1 - (d / 40.0);
            for (var strand = 0; strand < 2; strand++)
            {
                var off = Math.Sin((d * 0.7) - (t * 18) + (strand * Math.PI)) * 4 * fade;
                Put(p, x - (dy / length * off), y + (dx / length * off), strand == 0 ? White : Water, 230 * fade);
            }

            if (d % 5 == 0) Put(p, x + ((Rnd(d, 81) - 0.5) * 6), y + ((Rnd(d, 82) - 0.5) * 6), Foam, 160 * fade);
        }
    }

    /// <summary>
    /// The geyser it comes out of: the surface swells into a dome, bursts; a column of water shoots up, streaks running up it, its sides
    /// trembling, a crown of drops thrown out like an umbrella from its head, foam and bubbles boiling at its foot, rings running out; it falls
    /// back as a curtain of drops.
    /// </summary>
    private static void Geyser_(CapSheet p, double x, int surface, double age)
    {
        if (age < -0.25 || age > 1.4) return;
        if (age < 0)
        {
            // the swell before it bursts
            var k = (age + 0.25) / 0.25;
            for (var dx = -11; dx <= 11; dx++)
            {
                var h = 4 * k * (1 - ((dx / 11.0) * (dx / 11.0)));
                for (var dy = 0; dy <= h; dy++) Put(p, x + dx, surface - dy, dy > h - 1 ? White : dy > h - 2 ? Foam : Water, 220);
            }

            return;
        }

        const double Top = 52;
        var height = age < 0.15 ? Top * EaseOut(age / 0.15) : age < 0.5 ? Top + (Math.Sin(age * 30) * 1.5) : Top * Math.Max(0, 1 - ((age - 0.5) / 0.55));
        for (var h = 0; h < height; h++)
        {
            var taper = h / Top;
            var w = 5.5 - (taper * 2.5) + (Math.Sin((h * 0.55) + (age * 34)) * 0.8) + (h < 4 ? (4 - h) * 0.8 : 0);
            for (var dx = -(int)Math.Ceiling(w); dx <= w; dx++)
            {
                var side = dx / Math.Max(1, w);
                var streak = ((h + (age * 90) + (Math.Abs(dx) * 5)) % 9) < 1.2;
                uint c = Math.Abs(side) > 0.85 ? WaterEdge : side < -0.45 ? White : streak ? White : side > 0.45 ? Water : Foam;
                Put(p, x + dx, surface - h, c, Math.Abs(side) > 0.85 ? 160 : 235);
            }
        }

        // the crown: drops thrown out from its head like an umbrella, falling, each a little streak
        for (var n = 0; n < 40; n++)
        {
            var since = age - 0.1 - ((n % 6) * 0.05);
            var life = 0.55 + (Rnd(n, 92) * 0.45);
            if (since < 0 || since > life) continue;
            var a = (Rnd(n, 91) - 0.5) * 2.8;
            var v = 26 + (Rnd(n, 93) * 32);
            double vx = Math.Sin(a) * v, vy = -Math.Cos(a) * v * 0.6;
            double dx = vx * since, dy = (vy * since) + (95 * since * since);
            Drop(p, x + dx, surface - Math.Min(height, Top) + dy, vx, vy + (190 * since), 230 * (1 - (since / life)));
        }

        // the curtain it falls back as
        if (age > 0.5)
        {
            for (var n = 0; n < 30; n++)
            {
                var since = age - 0.5 - (Rnd(n, 95) * 0.3);
                if (since < 0 || since > 0.5) continue;
                Drop(p, x + ((Rnd(n, 96) - 0.5) * 16), surface - Top + (Rnd(n, 97) * 30) + (160 * since * since), 0, 160 * since, 220 * (1 - (since / 0.5)));
            }
        }

        // the foot: foam and bubbles boiling, rings running out
        for (var n = 0; n < 22; n++)
        {
            var a = Rnd(n, 98) * Math.PI;
            var r = 6 + (Rnd(n, 99) * 6) + (Math.Sin((age * 12) + n) * 1.5);
            Put(p, x + (Math.Cos(a) * r), surface - (Math.Sin(a) * 2), n % 3 == 0 ? White : Foam, 220 * (1 - (age / 1.4)));
        }

        Ripples(p, x, surface, age, 1.2);
    }

    /// <summary>A drop flying: a white head and a pale tail behind it along its way.</summary>
    private static void Drop(CapSheet p, double x, double y, double vx, double vy, double a)
    {
        var speed = Math.Sqrt((vx * vx) + (vy * vy));
        Put(p, x, y, White, a);
        if (speed > 25) Put(p, x - (vx / speed * 1.5), y - (vy / speed * 1.5), Foam, a * 0.8);
        if (speed > 70) Put(p, x - (vx / speed * 3), y - (vy / speed * 3), Water, a * 0.5);
    }

    /// <summary>Rings running out on the surface from a point, three of them, fading.</summary>
    private static void Ripples(CapSheet p, double x, int surface, double age, double size)
    {
        for (var n = 0; n < 3; n++)
        {
            var k = age - (n * 0.18);
            if (k <= 0 || k > 1.2) continue;
            var r = (4 + (k * 30)) * size;
            for (var i = 0; i < 64; i++)
            {
                var a = i / 64.0 * 2 * Math.PI;
                if (((i + n) & 1) == 0 && k > 0.6) continue;
                Put(p, x + (Math.Cos(a) * r), surface + (Math.Sin(a) * r * 0.1), i < 32 ? Foam : White, 190 * (1 - (k / 1.2)));
            }
        }
    }

    /// <summary>A bubble: one pale cell, or a little round one with a light on it.</summary>
    private static void Bubble(CapSheet p, double x, double y, int size, double a)
    {
        if (size <= 1)
        {
            Put(p, x, y, Foam, a);
            return;
        }

        Put(p, x, y, Water, a);
        Put(p, x + 1, y, Water, a);
        Put(p, x, y + 1, Water, a);
        Put(p, x + 1, y + 1, Foam, a);
        Put(p, x, y, White, a);
        if (size > 2)
        {
            Put(p, x - 1, y + 1, WaterEdge, a * 0.7);
            Put(p, x + 2, y + 1, WaterEdge, a * 0.7);
            Put(p, x, y + 2, WaterEdge, a * 0.7);
            Put(p, x + 1, y + 2, WaterEdge, a * 0.7);
        }
    }

    /// <summary>Its shape away out of the smoke, up and off to the right, flipping, small and dark, a glint as it goes.</summary>
    private static void Escape(CapSheet p, CritterArt? flipping, (double X, double Y) stand, double age)
    {
        if (flipping is null || age < 0.1 || age > 0.95) return;
        var u = (age - 0.1) / 0.75;
        if (u < 1)
        {
            for (var n = 2; n >= 0; n--)
            {
                var w = Math.Max(0, u - (n * 0.04));
                double x = stand.X + (w * 90), y = stand.Y - 8 - (w * 60) + (w * w * 20);
                CapVisitors.Centred(p, flipping, 0.5, R(x), R(y), 0.3 * (1 - (w * 0.5)), true, 0x0A1E3Au, n == 0 ? 1 : 0.6, w * 900, n == 0 ? 1 : 0.35);
            }
        }
        else
        {
            CapTrial.Star(p, stand.X + 90, stand.Y - 48, White, 255 * (1 - ((age - 0.85) / 0.1)), 3);
        }
    }

    /// <summary>
    /// The cloud of smoke it goes in, soft as in a cartoon: a few sparkles as it pops, round puffs running together into one cloud that swells
    /// past its size and settles; lit from the top left (white, pale, mid, shade, dithered between them), a soft dark edge only underneath and on
    /// the right; then the puffs drift out and up, spreading as they go, and come apart from their edges inwards into wisps.
    /// </summary>
    private static void Smoke_(CapSheet p, (double X, double Y) at, double age)
    {
        if (age < 0 || age > 1.8) return;
        if (age < 0.2)
        {
            for (var n = 0; n < 5; n++)
            {
                var a = (n / 5.0 * 2 * Math.PI) + 0.6;
                var d = 14 + (age * 70);
                CapTrial.Star(p, at.X + (Math.Cos(a) * d), at.Y + (Math.Sin(a) * d * 0.8), White, 255 * (1 - (age / 0.2)), 1);
            }
        }

        // how deep a cell is in the cloud (0 outside), and the shape of the puff it is most in, for its light
        (double Depth, double Dx, double Dy) Inside(int x, int y)
        {
            var best = (0.0, 0.0, 0.0);
            for (var n = 0; n < Puffs; n++)
            {
                var (cx, cy, r) = PuffAt(at, n, age);
                if (r < 0.6) continue;
                double dx = (x - cx) / r, dy = (y - cy) / r * 1.1, depth = 1 - ((dx * dx) + (dy * dy));
                if (depth > best.Item1) best = (depth, dx, dy);
            }

            return best;
        }

        var dissolve = Clamp01((age - 0.55) / 1.1);
        const int Span = 40;
        for (var y = (int)at.Y - Span; y <= at.Y + Span; y++)
        {
            for (var x = (int)at.X - Span; x <= at.X + Span; x++)
            {
                var (depth, dx, dy) = Inside(x, y);
                if (depth <= 0) continue;

                // it comes apart from its edges in: the shallow cells go first
                var dither = Bayer[y & 3, x & 3] / 16.0;
                if (dissolve > 0 && dither < dissolve * (1.6 - depth)) continue;

                var light = -(dx * 0.55) - (dy * 0.9) + ((dither - 0.5) * 0.35);
                var underside = Inside(x + 1, y + 1).Depth <= 0;
                uint c = underside && light < 0 ? SmokeEdge : light > 0.65 ? White : light > 0.15 ? SmokeLight : light > -0.4 ? Smoke : SmokeShade;
                p.Set(x, y, c);
            }
        }
    }

    private const int Puffs = 11;
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>One puff of the smoke at a moment: swelling past its size and settling, then drifting out and up, growing as it thins.</summary>
    private static (double X, double Y, double R) PuffAt((double X, double Y) at, int n, double age)
    {
        var a = (n / (double)(Puffs - 1) * 2 * Math.PI) + 0.5;
        var d = n == 0 ? 0 : 10 + (Rnd(n, 101) * 3);
        var size = n == 0 ? 9 : 5.5 + (Rnd(n, 102) * 2.5);
        var grow = age < 0.12 ? EaseOut(age / 0.12) * 1.2 : age < 0.3 ? 1.2 - (0.2 * ((age - 0.12) / 0.18)) : 1;
        var leave = Clamp01((age - 0.3) / 1.4);
        var r = size * grow * (1 + (0.35 * leave));
        return (at.X + (Math.Cos(a) * d * (1 + (leave * 1.2))), at.Y + (Math.Sin(a) * d * 0.7) - (leave * 16), r);
    }

    /// <summary>The leaves that flutter out of the smoke and down onto the water, where they float and fade.</summary>
    private static void Leaves(CapSheet p, (double X, double Y) at, int surface, double age)
    {
        if (age < 0.05 || age > 3.5) return;
        for (var n = 0; n < 3; n++)
        {
            var since = age - 0.05 - (n * 0.04);
            if (since < 0) continue;
            var side = n == 1 ? 1 : -1 + n;
            var x = at.X + (side * (10 + (since * 14))) + (Math.Sin((since * 5) + n) * 5);
            var y = Math.Min(surface - 1, at.Y - 10 + (since * since * 4) + (since * 8) - (since < 0.3 ? since * 30 : 9));
            var tilt = Math.Sin((since * 6) + n);
            var fade = since > 2.5 ? 1 - ((since - 2.5) / 1) : 1;
            Put(p, x, y, 0x4FA640u, 255 * fade);
            Put(p, x + (tilt > 0 ? 1 : -1), y, 0x8AD45Au, 255 * fade);
            Put(p, x, y + 1, 0x1E5A22u, 255 * fade);
            Put(p, x + (tilt > 0 ? -1 : 1), y - (Math.Abs(tilt) > 0.5 ? 1 : 0), 0x2E7A30u, 255 * fade);
        }
    }

    /// <summary>
    /// The substitute log it leaves (as in Super Smash Bros. Ultimate): one big log with a twig and a leaf, there in the smoke where it stood,
    /// hanging a moment, dropping into the sea with a crown of water and rings, going under and coming back up; then it floats, half under,
    /// rocking, rings running from it, and drifts slowly away to the right with a little wake, out of the window, not to come back.
    /// </summary>
    private static void Substitute(CapSheet p, CapWorldLayout l, (double X, double Y) stand, double age)
    {
        if (age < 0.05) return;
        var surface = l.SurfaceY;
        double x = stand.X, y = stand.Y + 6, angle = 0.1;
        const double Hang = 0.45, Settle = 1.2, Drift = 16;
        var fall = Math.Sqrt(2 * (surface - y) / 160.0);
        var t = age - Hang;
        var waterline = int.MaxValue;
        if (t <= 0)
        {
            y += Math.Sin(age * 30) * 0.6 * (1 - (age / Hang));
        }
        else if (t < fall)
        {
            y += 80 * t * t;
            angle = 0.1 + (t * 1.6);
            for (var d = 1; d < 6; d++) Put(p, x + ((d % 2 == 0 ? -1 : 1) * 6), y - 6 - (d * 2), Foam, 120 - (d * 18));
        }
        else
        {
            var s = t - fall;
            var tumble = 0.1 + (fall * 1.6);
            Splash(p, stand.X, surface, s, 1.5);
            Ripples(p, stand.X, surface, s, 1);

            // floating: lying along the water, half under, rocking; it drifts off right, slowly at first
            var lie = Ease(Clamp01(s / Settle));
            var dip = s < 0.3 ? Math.Sin(s / 0.3 * Math.PI) * 6 : 0;
            var going = Math.Max(0, s - Settle);
            x = stand.X + (Drift * going) + (going < 1 ? -Drift * going * (1 - going) * 0.5 : 0);
            y = surface + 1 + dip + (Math.Sin(s * 2.6) * 0.8) + ((1 - lie) * Math.Sin(s * 8) * 1.5);
            angle = (tumble * (1 - lie)) + (Math.Sin(s * 2.1) * 0.06) + (lie * 0.02);
            waterline = surface + 1;
            if (x > l.Cols + 24) return;

            // rings now and then round it, and its wake
            Ripples(p, x, surface, (s % 1.6) + 0.2, 0.7);
            if (going > 0)
            {
                for (var d = 2; d < 22; d++) Put(p, x - 13 - d, surface + ((d / 3) % 2), d % 3 == 0 ? White : Foam, 170 * (1 - (d / 22.0)) * Math.Min(1, going));
            }

            if (s < 1.2)
            {
                for (var n = 0; n < 6; n++) Bubble(p, stand.X + ((Rnd(n, 121) - 0.5) * 18), surface + 3 + (Rnd(n, 122) * 8) - (s * 10), n % 3 == 0 ? 2 : 1, 200 * (1 - (s / 1.2)));
            }
        }

        Log(p, x, y, angle, waterline);
    }

    /// <summary>
    /// The log: a thick round log, its bark in ridges running along it (lit on top, a dark groove under each), a knot, a dark edge; its cut end
    /// seen a little from the side, a rim of bark round rings of pale wood, a crack and the pith; a twig with a fork and a leaf with its vein.
    /// </summary>
    /// <param name="waterline">Below this row it is under the water: tinted with it.</param>
    private static void Log(CapSheet p, double cx, double cy, double angle, int waterline = int.MaxValue)
    {
        const double Half = 12, Radius = 5;
        double co = Math.Cos(angle), si = Math.Sin(angle);
        (double U, double V) Local(int x, int y) => ((co * (x - cx)) + (si * (y - cy)), (-si * (x - cx)) + (co * (y - cy)));
        bool End(double u, double v) => (((u - Half) / 2.2) * ((u - Half) / 2.2)) + ((v / Radius) * (v / Radius)) <= 1;
        bool Body(double u, double v) => u >= -Half - 1 && u <= Half && Math.Abs(v) <= Radius - (u < -Half ? 1 : 0);
        bool In(int x, int y)
        {
            var (u, v) = Local(x, y);
            return Body(u, v) || End(u, v);
        }

        // the twig: from the top of the log up and to the right, a short fork off it to the left
        bool Twig(double u, double v) =>
            (v < -Radius + 0.6 && v > -Radius - 6 && Math.Abs(u + 4 - ((-Radius - v) * 0.55)) < 0.75)
            || (v < -Radius - 2 && v > -Radius - 4.6 && Math.Abs(u + 2.6 + ((-Radius - 2 - v) * 0.9)) < 0.6);

        (bool In, bool Edge, bool Vein, bool Light) Leaf(double u, double v)
        {
            double lu = u + 4 - (6 * 0.55) - 2.2, lv = v + Radius + 6.6;
            var k = ((lu / 3.2) * (lu / 3.2)) + ((lv / 1.7) * (lv / 1.7));
            return (k <= 1, k > 0.6, Math.Abs(lv + (lu * 0.15)) < 0.35 && Math.Abs(lu) < 2.6, lv < 0);
        }

        for (var y = (int)(cy - 20); y <= cy + 16; y++)
        {
            for (var x = (int)(cx - 18); x <= cx + 18; x++)
            {
                var (u, v) = Local(x, y);
                if (In(x, y))
                {
                    var edge = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1);
                    uint c;
                    if (End(u, v) && u > Half - 0.5)
                    {
                        var ring = Math.Sqrt((((u - Half) / 2.2) * ((u - Half) / 2.2)) + ((v / Radius) * (v / Radius)));
                        c = ring > 0.82 ? 0x5A3A1Cu : ring < 0.18 ? 0x7A4E26u : ((int)(ring * 5) % 2 == 0) ? 0xEED49Au : 0xCBA466u;
                        if (Math.Abs(v + ((u - Half) * 0.8)) < 0.4 && ring < 0.8 && v < 0) c = 0x8A5E30u;
                    }
                    else
                    {
                        var ridge = ((v * 1.3) + (Math.Sin(u * 0.45) * 0.7) + 20) % 2.4;
                        c = v < -Radius + 1.2 ? 0xC08A4Eu : v > Radius - 1.5 ? 0x4A2C14u : ridge < 0.55 ? 0x4E3018u : ridge < 1.1 ? 0x9E6C3Au : v < 0 ? 0x8A5C30u : 0x704626u;
                        var knot = (((u + 5) / 2.2) * ((u + 5) / 2.2)) + (((v - 1) / 1.5) * ((v - 1) / 1.5));
                        if (knot <= 1) c = knot < 0.3 ? 0x2E1C0Cu : knot < 0.7 ? 0x6A4422u : 0x3A2410u;
                    }

                    p.Set(x, y, Under(edge ? 0x24160Au : c, y, waterline));
                }
                else if (Twig(u, v))
                {
                    p.Set(x, y, Under(v < -Radius - 3 ? 0x6E4A26u : 0x5A3A1Cu, y, waterline));
                }
                else if (Leaf(u, v) is { In: true } leaf)
                {
                    var leafEdge = !Leaf(Local(x - 1, y).U, Local(x - 1, y).V).In || !Leaf(Local(x + 1, y).U, Local(x + 1, y).V).In
                        || !Leaf(Local(x, y - 1).U, Local(x, y - 1).V).In || !Leaf(Local(x, y + 1).U, Local(x, y + 1).V).In;
                    p.Set(x, y, Under(leafEdge ? 0x1E5A22u : leaf.Vein ? 0x2E7A30u : leaf.Light ? 0x8AD45Au : 0x4FA640u, y, waterline));
                }
            }
        }
    }

    /// <summary>A colour seen through the water when its row is below the waterline.</summary>
    private static uint Under(uint c, int y, int waterline) => y > waterline ? CapSheet.Mix(c, 0x2A8CA8u, y == waterline + 1 ? 0.3 : 0.5) : c;

    /// <summary>The crystal set in the face of the wall in a star of cracks, a glint over it now and then.</summary>
    private static void Set(CapSheet p, (int X, int Y) target, CapTrialCrystal crystal, double phase, bool drawn)
    {
        // the star of cracks; once the crystal is set it is Greninja's mark (the player's pick, 2026-10-18): it glows blue, a wave of light
        // running out along it again and again
        for (var n = 0; n < 6; n++)
        {
            var a = (n / 6.0 * 2 * Math.PI) + 0.3;
            for (var r = 5; r < 9 + (n % 3); r++)
            {
                var wave = 0.5 + (0.5 * Math.Sin((phase * 2.4) - (r * 0.9)));
                p.Set(R(target.X + (Math.Cos(a) * r)), R(target.Y + (Math.Sin(a) * r * 0.8)), drawn ? CapSheet.Mix(0x0E3A5Au, 0x7FE8FFu, wave) : Crack);
            }
        }

        if (drawn) Mark(p, target, phase);
        if (!drawn) return;
        var glint = phase % 8;
        crystal.Paint(p, target.X, target.Y, Stuck, 0, 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>
    /// Greninja's mark (the water type's mark, its maker's): the star of cracks glowing blue with a wave of light running out along it (drawn
    /// in <see cref="Set"/>), and a scrap of its red scarf caught on one of the points, its end stirring in the current.
    /// </summary>
    private static void Mark(CapSheet p, (int X, int Y) target, double phase)
    {
        CapTrial.Shine(p, target.X, target.Y, 14, 11, 0x3AB8F0u, 70 + (40 * Math.Sin(phase * 2.4)));
        var a = (4 / 6.0 * 2 * Math.PI) + 0.3;
        double x = target.X + (Math.Cos(a) * 9), y = target.Y + (Math.Sin(a) * 9 * 0.8);
        for (var d = 0; d < 9; d++)
        {
            var flutter = Math.Sin((phase * 3) - (d * 0.6)) * (d / 9.0) * 1.6;
            var sx = x + d;
            var sy = y + (d * 0.35) + flutter;
            p.Set(R(sx), R(sy), d % 3 == 0 ? 0xAD2A3Au : 0xE24A4Au);
            if (d < 7) p.Set(R(sx), R(sy) + 1, d % 2 == 0 ? 0x8A1A2Au : 0xC8383Au);
        }
    }

    /// <summary>
    /// A shuriken of water: six points sweeping back, each lit on its leading edge and darker towards its trailing one, a dark edge, pale streaks
    /// running round it. The one it throws (<paramref name="thrown"/>) also blurs the air behind each point, flings drops off its tips and has a
    /// bright ring round the hole in its middle.
    /// </summary>
    private static void Shuriken(CapSheet p, double cx, double cy, double r, double spin, bool thrown)
    {
        if (r < 1) return;
        double Fraction(double a)
        {
            var u = ((((a - spin) / (2 * Math.PI)) % 1) + 1) % 1 * 6;
            return u - Math.Floor(u);
        }

        double Reach(double a) => r * (0.45 + (0.55 * Math.Pow(1 - Fraction(a), 2.2)));

        bool In(int x, int y)
        {
            double dx = x - cx, dy = y - cy, d = Math.Sqrt((dx * dx) + (dy * dy));
            return d <= Reach(Math.Atan2(dy, dx)) && d > r * 0.18;
        }

        if (thrown)
        {
            // the blur of each point behind it as it turns
            for (var i = 0; i < 6; i++)
            {
                var tip = spin + (i * Math.PI / 3);
                for (var k = 1; k <= 8; k++)
                {
                    var a = tip - (k * 0.08);
                    Put(p, cx + (Math.Cos(a) * r * 0.95), cy + (Math.Sin(a) * r * 0.95), Foam, 170 - (k * 18));
                }

                // drops flung off the tip
                for (var k = 1; k <= 3; k++)
                {
                    var a = tip - (k * 0.3);
                    var d = r + (k * 2.5) + ((spin * 3) % 2);
                    Put(p, cx + (Math.Cos(a) * d), cy + (Math.Sin(a) * d), k == 1 ? White : Water, 220 - (k * 50));
                }
            }
        }

        var span = (int)Math.Ceiling(r) + 1;
        for (var y = (int)cy - span; y <= cy + span; y++)
        {
            for (var x = (int)cx - span; x <= cx + span; x++)
            {
                if (!In(x, y)) continue;
                var edge = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1);
                double dx = x - cx, dy = y - cy, d = Math.Sqrt((dx * dx) + (dy * dy)), a = Math.Atan2(dy, dx);
                var f = Fraction(a);
                var streak = Math.Abs(Math.Sin((a * 3) - spin + (d * 0.4))) < 0.12;
                uint c = edge ? WaterEdge : streak ? Streak : f > 0.85 && d > r * 0.35 ? White : f < 0.3 ? WaterDeep : d < r * 0.4 ? WaterDeep : Water;
                if (thrown && !edge && d < r * 0.3) c = Foam;
                p.Set(x, y, c);
            }
        }
    }

    /// <summary>A splash on the surface: drops thrown up and falling back.</summary>
    private static void Splash(CapSheet p, double x, int surface, double sp, double size)
    {
        if (sp < 0 || sp > 0.8) return;
        for (var n = 0; n < 24; n++)
        {
            var life = 0.4 + (Rnd(n, 9) * 0.4);
            if (sp > life) continue;
            var a = Math.PI * (0.15 + (Rnd(n, 3) * 0.7));
            var v = (20 + (Rnd(n, 5) * 34)) * size;
            var y = surface - (Math.Sin(a) * v * sp) + (110 * sp * sp);
            if (y <= surface + 1) Put(p, x - (Math.Cos(a) * v * sp), y, n % 3 != 0 ? Foam : White, 255 * (1 - (sp / life)));
        }
    }

    /// <summary>The Hidrostal set in the wall for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Target(l, ctx) is { } target) Set(p, target, crystal, phase, true);
    }
}
