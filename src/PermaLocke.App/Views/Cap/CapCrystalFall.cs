namespace PermaLocke.App.Views;

/// <summary>
/// The first Z-Crystal's way into the Gimmighoul ruins (2026-10-18, prototype; places marked by the player): after the trial film's splash it
/// comes down onto the top of the upper storey of the tall wall (red), stands there on its point, wobbles and topples onto the wall's right
/// shoulder (green), slides down the slope of broken bricks and drops into the window of the low wall (yellow), where it stays. Only the way
/// down to the red spot changes between the prototypes (<see cref="Variant"/>): 0 a leaf swaying down, 1 a dive, 2 a spiral round the tower,
/// 3 Gimmighoul fishing it out with a golden line, 4 Arceus flying over and dropping it (CapArceus; Tapu Koko until 2026-10-19).
/// Places are in the ruins' own cells: across from their first column, up from the floor their walls stand on. A function of the moment.
/// </summary>
public static class CapCrystalFall
{
    /// <summary>For the lab: which way down it takes.</summary>
    public static int Variant { get; set; } = 4;

    /// <summary>The first trial's crystal, the one this way is for.</summary>
    public const int Normastal = 807;

    // The timeline, on the trial film's clock: the splash, the landing (the film's impact), then the rest.
    private const double Splash = CapTrial.Splash, Land = CapTrial.Hit, Wobble = 4.8, Topple = 6.3, Fall = 6.45, OnShoulder = 7.05, Slide = 7.2,
        Off = 8.0, InWindow = 8.45, Still = 8.9, Glint = 9.05;

    private const double Scale = 0.4;

    // The marks, in the ruins' cells: the column it stands on; the slide's foot; the window's sill (its opening starts there).
    private const int RedX = 29, GreenX = 41, RampEndX = 52;
    private const double SillX = 60, SillY = 11;

    /// <summary>The tilt it rests at in the window: lying on one of its faces.</summary>
    private const double Rest = 0.48;

    private const uint White = 0xFFFFFFu, Silt = 0xC8C2A0u, Foam = 0xCFF4FFu;
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>Where it touches down on the tower, in the window's cells, or null without the ruins: the trial film splashes over it.</summary>
    public static (int X, int Y)? LandingSpot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) ? (x0 + RedX, floor - CapGimmighoulRuins.Surface(RedX)) : null;

    /// <summary>
    /// The Normastal where it came to rest, for the rest of the run once its trial is passed: lying on a face on the sill of the low wall's
    /// window, its shadow in it, its symbol faintly lit, a glint running over it every eight seconds or so.
    /// </summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _)) return;
        var glint = phase % 8;
        var centre = (X: x0 + SillX, Y: floor - SillY - Lowest(Rest, Scale));
        Draw(p, crystal, x0, floor, centre, Rest, Scale, 0, 0.25, glint < 0.6 ? glint / 0.6 : -1, false, true);
        StarDust(p, centre, phase, 1);
    }

    /// <summary>
    /// Star dust round the Normastal, its type's mark on the ruins (2026-10-18, asked by the player): little white sparkles, as an item on the
    /// ground sparkles in the games, coming and going round it one after another, each a four-pointed star that swells and fades as it drifts up a
    /// little, a few pale motes between them.
    /// </summary>
    private static void StarDust(CapSheet p, (double X, double Y) at, double phase, double k)
    {
        if (k <= 0) return;
        for (var n = 0; n < 7; n++)
        {
            var cycle = 1.6 + (n % 3 * 0.4);
            var age = ((phase / cycle) + (n * 0.29)) % 1;
            var turn = Math.Floor((phase / cycle) + (n * 0.29));
            var a = CapArt.Hash(n, (int)turn) % 628 / 100.0;
            var d = 6 + (CapArt.Hash(n + 7, (int)turn) % 7);
            double x = at.X + (Math.Cos(a) * d * 1.3), y = at.Y + (Math.Sin(a) * d * 0.6) - (age * 5);
            var size = Math.Sin(age * Math.PI);
            if (size < 0.15) continue;
            CapTrial.Star(p, x, y, 0xFFF6D8u, 255 * size * k, size > 0.7 ? 2 : size > 0.4 ? 1 : 0);
        }

        for (var n = 0; n < 6; n++)
        {
            var age = ((phase * 0.3) + (n / 6.0)) % 1;
            CapTrial.Light(p, at.X + ((n - 2.5) * 3) + (Math.Sin((age * 6) + n) * 1.5), at.Y + 3 - (age * 12), 0xFFFFFFu, 160 * Math.Sin(age * Math.PI) * k);
        }
    }

    /// <summary>The crystal from the splash on, in the abyss's window; false when the ruins are not there and the film keeps its own way down.</summary>
    public static bool Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (t < Splash || CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _)) return false;

        (double X, double Y) Cell(double x, double ly) => (x0 + x, floor - ly);
        var red = Cell(RedX, CapGimmighoulRuins.Surface(RedX));
        var green = Cell(GreenX, CapGimmighoulRuins.Surface(GreenX));
        var rampEnd = Cell(RampEndX, CapGimmighoulRuins.Surface(RampEndX));
        var sill = Cell(SillX, SillY);
        var slope = Math.Atan2(rampEnd.Y - green.Y, rampEnd.X - green.X);
        // lying on one of its faces along the slope
        var lie = slope - Rest;

        // Arceus brings it (it holds it until the film's impact): the rest of the way starts that much later
        if (Variant == 4)
        {
            if (t < Land + Carried) return FromTapu(p, l, ctx, crystal, t, x0, floor, red);
            t -= Carried;
        }

        // Gimmighoul fishing it takes longer: the rest of the way starts that much later
        if (Variant == 3)
        {
            Coins(p, x0, floor, t);
            if (t < Land + Hooked) return Fish(p, l, crystal, t, x0, floor, red);
            t -= Hooked;
        }

        // where its bottom point touches, how it is turned, how big, its yaw, and any light on it
        (double X, double Y) touch;
        double roll = 0, scale = Scale, spin = 0, burn = 0, glint = -1;

        if (t < Land)
        {
            var u = (t - Splash) / (Land - Splash);
            scale = 0.8 + ((Scale - 0.8) * Ease(u));
            var top = (X: red.X, Y: (double)l.SurfaceY);
            switch (Variant)
            {
                case 1:
                    // a dive: it hangs under the surface spinning, gathering itself, then shoots straight down
                    const double gather = 0.4;
                    if (u < gather)
                    {
                        touch = (top.X, top.Y + 4 + (u / gather * 6));
                        spin = (t - Splash) * 28;
                    }
                    else
                    {
                        var k = Math.Pow((u - gather) / (1 - gather), 2.2);
                        touch = (top.X, top.Y + 10 + ((red.Y - top.Y - 10) * k));
                        Streaks(p, crystal, touch, k);
                    }

                    break;
                case 2:
                    // a spiral round the tower, narrowing, nearer and farther as it goes round
                    var a = u * Math.PI * 4.5;
                    var r = 30 * (1 - u);
                    touch = (red.X + (Math.Cos(a) * r), top.Y + 8 + ((red.Y - top.Y - 8) * u) + (Math.Sin(a) * r * 0.3));
                    scale *= 1 + (0.25 * Math.Sin(a) * (1 - u));
                    roll = Math.Sin(a) * 0.3 * (1 - u);
                    Trail(p, crystal, t, x0, floor, red, top);
                    break;
                default:
                    // a leaf: it sways from side to side as it sinks, tipping with each swing, the swings dying down
                    var sway = u * Math.PI * 2.5;
                    touch = (red.X + (Math.Sin(sway) * 8 * (1 - u)), top.Y + ((red.Y - top.Y) * EaseInOut(u)));
                    roll = Math.Cos(sway) * 0.55 * (1 - u);
                    break;
            }

            Bubbles(p, touch, t, l.SurfaceY);
        }
        else if (t < Topple)
        {
            // on its point at the top of the tower: it rocks on that point, more and more (a bounce first if it dived)
            var since = t - Land;
            if (t > Wobble)
            {
                var w = (t - Wobble) / (Topple - Wobble);
                roll = (0.1 + (0.3 * w)) * Math.Sin((t - Wobble) * Math.PI * 2 * 1.3);
                // it ends its last swing leaning out over the edge
                if (w > 0.8) roll = Math.Max(roll, 0.4 * ((w - 0.8) / 0.2));
            }

            touch = (red.X, red.Y - (Variant == 1 && since < 0.28 ? Math.Sin(since / 0.28 * Math.PI) * 3 : 0));
            if (since < 0.5) Clink(p, red, since);
        }
        else if (t < Fall)
        {
            // it goes over, turning on its point
            var k = (t - Topple) / (Fall - Topple);
            touch = red;
            roll = 0.4 + (0.7 * k * k);
        }
        else if (t < OnShoulder)
        {
            // it falls onto the wall's shoulder, half a turn at most, landing on a face along the slope
            var k = (t - Fall) / (OnShoulder - Fall);
            var from = Over(red, 1.1, Scale);
            var to = Rested(x0, floor, green.X, lie, Scale);
            var centreNow = (X: from.X + ((to.X - from.X) * k), Y: from.Y + ((to.Y - from.Y) * k * k) - (Math.Sin(k * Math.PI) * 3));
            return Draw(p, crystal, x0, floor, centreNow, 1.1 + ((lie + Math.PI - 1.1) * k), Scale, 0, 0, -1, true, false);
        }
        else if (t < Off)
        {
            // a little hop where it lands, then it slides down the broken bricks lying on a face, faster and faster, dust behind it
            if (t < Slide)
            {
                var k = (t - OnShoulder) / (Slide - OnShoulder);
                var at = Rested(x0, floor, green.X, lie, Scale);
                Clink(p, green, t - OnShoulder);
                return Draw(p, crystal, x0, floor, (at.X, at.Y - (Math.Sin(k * Math.PI) * 1.5)), lie, Scale, 0, 0, -1, true, false);
            }

            var u = Math.Pow((t - Slide) / (Off - Slide), 1.8);
            Dust(p, green, rampEnd, u, t);
            return Draw(p, crystal, x0, floor, Rested(x0, floor, green.X + ((rampEnd.X - green.X) * u), lie, Scale), lie, Scale, 0, 0, -1, true, false);
        }
        else if (t < InWindow)
        {
            // off the end of the slope and down in front of the wall into the window, turning back onto a face; its shadow on the bricks
            var k = (t - Off) / (InWindow - Off);
            var from = Rested(x0, floor, rampEnd.X, lie, Scale);
            var to = (X: sill.X, Y: sill.Y - Lowest(Rest, Scale));
            var centreNow = (X: from.X + ((to.X - from.X) * k), Y: from.Y + ((to.Y - from.Y) * k * k) - (Math.Sin(k * Math.PI) * 2));
            return Draw(p, crystal, x0, floor, centreNow, lie + ((Rest - lie) * k), Scale, 0, 0, -1, false, true);
        }
        else
        {
            // it settles on the sill, rocking to a stop on one of its faces, and glints once
            var k = Math.Min(1, (t - InWindow) / (Still - InWindow));
            var r = Rest + (0.2 * (1 - k) * Math.Cos(k * Math.PI * 3));
            if (t - InWindow < 0.5) Clink(p, sill, t - InWindow);
            var drawn = Draw(p, crystal, x0, floor, (sill.X, sill.Y - Lowest(r, Scale)), r, Scale, 0, 0.25, t > Glint && t < Glint + 0.6 ? (t - Glint) / 0.6 : -1, false, true);
            StarDust(p, (sill.X, sill.Y - Lowest(r, Scale)), t, Math.Clamp((t - Still) / 1.5, 0, 1));
            return drawn;
        }

        // standing on its point, or on its way down: its middle above the point, turned with it, and never inside a wall
        return Draw(p, crystal, x0, floor, Over(touch, roll, scale), roll, scale, spin, burn, glint, true, false);
    }

    /// <summary>Its middle when it stands on its bottom point at <paramref name="touch"/>, turned by <paramref name="roll"/> round that point.</summary>
    private static (double X, double Y) Over((double X, double Y) touch, double roll, double scale)
    {
        var hh = CapTrialCrystal.H * scale;
        return (touch.X + (Math.Sin(roll) * hh), touch.Y - (Math.Cos(roll) * hh));
    }

    /// <summary>How far below its middle its lowest corner is, turned by <paramref name="roll"/>: a wide rhombus.</summary>
    private static double Lowest(double roll, double scale) =>
        Math.Max(Math.Abs(CapTrialCrystal.W * scale * Math.Sin(roll)), Math.Abs(CapTrialCrystal.H * scale * Math.Cos(roll)));

    /// <summary>Its middle resting on the tops of the walls at a column, turned by <paramref name="roll"/>: as low as it goes without entering them.</summary>
    private static (double X, double Y) Rested(int x0, int floor, double x, double roll, double scale) => (x, Support(x0, floor, x, roll, scale));

    /// <summary>
    /// The lowest its middle can be at column <paramref name="cx"/> without any part of it inside a wall: the edge of the rhombus, turned, walked
    /// round, each point kept above the top of the wall under it.
    /// </summary>
    private static double Support(int x0, int floor, double cx, double roll, double scale)
    {
        double a = CapTrialCrystal.W * scale, b = CapTrialCrystal.H * scale, co = Math.Cos(roll), si = Math.Sin(roll);
        var best = double.MaxValue;
        for (var n = 0; n < 32; n++)
        {
            // round the four edges, eight points each
            var side = n / 8;
            var f = (n % 8) / 8.0;
            var ux = side switch { 0 => a * (1 - f), 1 => -a * f, 2 => -a * (1 - f), _ => a * f };
            var uy = side switch { 0 => b * f, 1 => b * (1 - f), 2 => -b * f, _ => -b * (1 - f) };
            double dx = (co * ux) - (si * uy), dy = (si * ux) + (co * uy);
            var top = floor - CapGimmighoulRuins.Surface((int)Math.Round(cx + dx) - x0);
            best = Math.Min(best, top - dy - 0.5);
        }

        return best;
    }

    /// <summary>
    /// Paints it with its middle at <paramref name="centre"/>, kept out of the walls when <paramref name="solid"/> (it moves among them), with a
    /// shadow on the bricks when it is <paramref name="inFront"/> of a wall.
    /// </summary>
    private static bool Draw(CapSheet p, CapTrialCrystal crystal, int x0, int floor, (double X, double Y) centre, double roll, double scale, double spin,
        double burn, double glint, bool solid, bool inFront)
    {
        var y = solid ? Math.Min(centre.Y, Support(x0, floor, centre.X, roll, scale)) : centre.Y;
        int cx = (int)Math.Round(centre.X), cy = (int)Math.Round(y);
        if (inFront) Shadow(p, cx + 1, cy + 2, roll, scale);
        crystal.Paint(p, cx, cy, scale, spin, burn, glint, roll: roll);
        return true;
    }

    /// <summary>Its shadow: the rhombus, turned, darkening what is behind it.</summary>
    private static void Shadow(CapSheet p, int cx, int cy, double roll, double scale)
    {
        double a = CapTrialCrystal.W * scale, b = CapTrialCrystal.H * scale, co = Math.Cos(roll), si = Math.Sin(roll);
        var reach = (int)Math.Ceiling(a) + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                double ux = (co * dx) + (si * dy), uy = (-si * dx) + (co * dy);
                if ((Math.Abs(ux) / a) + (Math.Abs(uy) / b) <= 1) p.Set(cx + dx, cy + dy, 0x000000u, 80);
            }
        }
    }

    /// <summary>How much later it reaches the tower when Gimmighoul fishes it (<see cref="Variant"/> 3): the rest of the way waits for it.</summary>
    private const double Hooked = 1.6;

    // Gimmighoul fishing it, on the film's clock: it notices, casts, hooks it at the film's impact, reels it in swinging, lets go over the tower.
    private const double Notice = 3.35, Cast = 3.65, Catch = CapTrial.Hit, Release = 5.9, Dropped = Land + Hooked;

    private const uint Gold = 0xFFD448u, GoldDeep = 0xC48E1Eu, GoldHi = 0xFFF6C0u, Thread = 0xF2C230u;

    /// <summary>
    /// Gimmighoul fishes it out of the water (variant 3). It sinks slowly from the splash, turning; Gimmighoul's mask flashes as it sees it;
    /// a golden line flies from its mouth in a high arc, its hook glinting, and catches the crystal at the film's impact; Gimmighoul reels it
    /// in, a little gold reel spinning at its mouth, the crystal swinging under the taut line as it comes; over the tower the hook lets go with a
    /// spark, the line whips back into the chest and the crystal drops onto the tower's top. Then coins hop out of the chest for joy.
    /// </summary>
    private static bool Fish(CapSheet p, CapWorldLayout l, CapTrialCrystal crystal, double t, int x0, int floor, (double X, double Y) red)
    {
        var chestTop = CapGimmighoulRuins.ChestTop(floor);
        var mouth = (X: x0 + CapGimmighoulRuins.ChestLeft + 9.0, Y: chestTop + 17.0);
        var caught = (X: red.X + 18.0, Y: red.Y - 30.0);
        var above = Over((red.X, red.Y - 9), 0, Scale);
        var splash = (X: red.X, Y: (double)l.SurfaceY);

        // where it hangs and how it is turned
        (double X, double Y) at;
        double roll, scale = Scale;
        if (t < Catch)
        {
            // it sinks slowly from the splash, drifting towards where the hook will meet it, turning over and back
            var u = (t - Splash) / (Catch - Splash);
            at = (splash.X + ((caught.X - splash.X) * EaseInOut(u)), splash.Y + 6 + ((caught.Y - splash.Y - 6) * Ease(u)));
            scale = 0.8 + ((Scale - 0.8) * Ease(u * 1.4));
            roll = Math.Sin(u * Math.PI * 2.2) * 0.5 * (1 - (u * 0.6));
            Bubbles(p, (at.X, at.Y + 3), t, l.SurfaceY);
        }
        else if (t < Release)
        {
            // reeled in along an arc towards the top of the tower, swinging under the line, the swings dying down
            var u = EaseInOut((t - Catch) / (Release - Catch));
            var path = (X: caught.X + ((above.X - caught.X) * u), Y: caught.Y + ((above.Y - caught.Y) * u) - (Math.Sin(u * Math.PI) * 10));
            var swing = Math.Sin((t - Catch) * Math.PI * 2 * 1.1) * 0.6 * (1 - (u * 0.8));
            var jolt = t - Catch < 0.2 ? Math.Sin((t - Catch) * 70) * 1.5 * (1 - ((t - Catch) / 0.2)) : 0;
            at = (path.X + (Math.Sin(swing) * 5) + jolt, path.Y + ((1 - Math.Cos(swing)) * 4));
            roll = swing * 0.8;
        }
        else
        {
            // let go: it drops onto the top of the tower
            var k = Math.Min(1, (t - Release) / (Dropped - Release));
            var rest = Over(red, 0, Scale);
            at = (above.X + ((rest.X - above.X) * k), above.Y + ((rest.Y - above.Y) * k * k));
            roll = 0.15 * (1 - k);
        }

        // Gimmighoul sees it: its mask flashes, a spark of gold over its head
        var seen = t - Notice;
        if (seen > 0 && seen < 0.35)
        {
            var k = Math.Sin(seen / 0.35 * Math.PI);
            Star(p, mouth.X - 1, chestTop + 15, GoldHi, k, 2);
            Star(p, mouth.X + 2, chestTop + 6, Gold, k, 3);
        }

        // the line: cast in a high arc, taut while it reels, whipping back after it lets go
        var hook = at with { Y = at.Y - (CapTrialCrystal.H * scale) };
        if (t >= Cast && t < Catch)
        {
            var u = (t - Cast) / (Catch - Cast);
            var flying = (X: mouth.X + ((hook.X - mouth.X) * u), Y: mouth.Y + ((hook.Y - mouth.Y) * u) - (Math.Sin(u * Math.PI) * 26));
            Line(p, mouth, flying, 10 * (1 - u), t);
            Hook(p, flying);
        }
        else if (t >= Catch && t < Release)
        {
            Line(p, mouth, hook, 3, t);
            Hook(p, hook);
            Reel(p, mouth, t);
            if (t - Catch < 0.4) Ring(p, hook, (t - Catch) / 0.4);
        }
        else if (t >= Release && t < Release + 0.3)
        {
            var k = (t - Release) / 0.3;
            var tip = (X: hook.X + ((mouth.X - hook.X) * k * k), Y: hook.Y + ((mouth.Y - hook.Y) * k * k));
            Line(p, mouth, tip, 6 * Math.Sin(k * Math.PI), t);
            Hook(p, tip);
            if (k < 0.5) Star(p, hook.X, hook.Y, GoldHi, 1 - (k * 2), 3);
        }

        crystal.Paint(p, (int)Math.Round(at.X), (int)Math.Round(Math.Min(at.Y, Support(x0, floor, at.X, roll, scale))), scale, 0, t >= Catch ? 0.2 : 0, -1,
            roll: roll);
        return true;
    }

    /// <summary>Coins hopping out of the chest and back in, for joy, once the crystal is on the tower.</summary>
    private static void Coins(CapSheet p, int x0, int floor, double t)
    {
        var since = t - Dropped - 0.1;
        if (since < 0 || since > 0.9) return;
        var top = CapGimmighoulRuins.ChestTop(floor) + 14;
        for (var n = 0; n < 4; n++)
        {
            var age = since - (n * 0.1);
            if (age < 0 || age > 0.6) continue;
            var x = (int)Math.Round(x0 + CapGimmighoulRuins.ChestLeft + 4 + (n * 4) + ((n - 1.5) * age * 8));
            var y = (int)Math.Round(top - (age * 34) + (age * age * 60));
            p.Set(x, y, Gold);
            p.Set(x + 1, y, GoldDeep);
            p.Set(x, y - 1, GoldHi);
        }
    }

    /// <summary>The golden line from its mouth to the tip, sagging by <paramref name="sag"/> in the middle, a glint running along it.</summary>
    private static void Line(CapSheet p, (double X, double Y) from, (double X, double Y) to, double sag, double t)
    {
        var length = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        var steps = (int)Math.Ceiling(length * 1.5) + 1;
        var glint = (t * 1.6) % 1;
        for (var i = 0; i <= steps; i++)
        {
            var u = i / (double)steps;
            int x = (int)Math.Round(from.X + ((to.X - from.X) * u)), y = (int)Math.Round(from.Y + ((to.Y - from.Y) * u) + (Math.Sin(u * Math.PI) * sag));
            p.Set(x, y, Math.Abs(u - glint) < 0.04 ? GoldHi : Thread, 230);
        }
    }

    /// <summary>The hook: a little gold J, its point lit.</summary>
    private static void Hook(CapSheet p, (double X, double Y) at)
    {
        int x = (int)Math.Round(at.X), y = (int)Math.Round(at.Y);
        p.Set(x, y - 2, GoldDeep);
        p.Set(x, y - 1, Gold);
        p.Set(x, y, Gold);
        p.Set(x - 1, y + 1, Gold);
        p.Set(x - 2, y, GoldHi);
    }

    /// <summary>The reel turning at its mouth: a ring of four gold specks going round.</summary>
    private static void Reel(CapSheet p, (double X, double Y) at, double t)
    {
        for (var n = 0; n < 4; n++)
        {
            var a = (t * 14) + (n * Math.PI / 2);
            p.Set((int)Math.Round(at.X + 3 + (Math.Cos(a) * 2)), (int)Math.Round(at.Y - 1 + (Math.Sin(a) * 2)), n == 0 ? GoldHi : Gold);
        }
    }

    /// <summary>A ring of light opening where the hook bites.</summary>
    private static void Ring(CapSheet p, (double X, double Y) at, double k)
    {
        var r = 2 + (k * 9);
        for (var n = 0; n < 20; n++)
        {
            var a = n / 20.0 * Math.PI * 2;
            int x = (int)Math.Round(at.X + (Math.Cos(a) * r)), y = (int)Math.Round(at.Y + (Math.Sin(a) * r * 0.7));
            if (Bayer[y & 3, x & 3] < (1 - k) * 16) p.Set(x, y, n % 2 == 0 ? GoldHi : White);
        }
    }

    /// <summary>A four-pointed star of light, <paramref name="k"/> strong, its arms <paramref name="size"/> long.</summary>
    private static void Star(CapSheet p, double x, double y, uint colour, double k, int size)
    {
        if (k <= 0) return;
        int cx = (int)Math.Round(x), cy = (int)Math.Round(y), alpha = (int)(255 * Math.Min(1, k));
        p.Set(cx, cy, White, alpha);
        for (var d = 1; d <= size; d++)
        {
            p.Set(cx + d, cy, colour, alpha);
            p.Set(cx - d, cy, colour, alpha);
            p.Set(cx, cy + d, colour, alpha);
            p.Set(cx, cy - d, colour, alpha);
        }
    }

    /// <summary>How much later it reaches the tower when Arceus brings it (<see cref="Variant"/> 4): the rest of the way waits for it.</summary>
    private const double Carried = 1.38;

    // Arceus lets it go at the film's impact; it falls through the air, splashes, sinks onto the tower.
    private const double LetGo = CapTrial.Hit, InWater = LetGo + 0.4, OnTower = Land + Carried;

    /// <summary>
    /// The crystal let go by Arceus (variant 4): it tumbles down through the air, less than half a turn, growing to its size, and splashes
    /// into the sea over the ruins (a crown of drops, rings, a gulp of bubbles); it sinks, swaying, slowing, turning upright, and comes down on
    /// its point on the top of the tower.
    /// </summary>
    private static bool FromTapu(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t, int x0, int floor, (double X, double Y) red)
    {
        if (t < LetGo || CapArceus.Hands(l, ctx, LetGo) is not { } hands) return true;
        var surface = (double)l.SurfaceY;
        var top = Over(red, 0, Scale);
        if (t < InWater)
        {
            var k = (t - LetGo) / (InWater - LetGo);
            var at = (X: hands.X + ((red.X - hands.X) * k), Y: hands.Y + ((surface - hands.Y) * k * k));
            return Draw(p, crystal, x0, floor, at, k * Math.PI * 0.8, 0.3 + (0.1 * k), Math.Sin(t * 3) * 0.6, 0.3, -1, true, false);
        }

        var since = t - InWater;
        if (since < 0.9) SplashAt(p, red.X, l.SurfaceY, since);
        var u = (t - InWater) / (OnTower - InWater);
        var sink = (X: red.X + (Math.Sin(u * Math.PI * 2) * 4 * (1 - u)), Y: surface + 3 + ((top.Y - surface - 3) * (1 - Math.Pow(1 - u, 2))));
        Bubbles(p, sink, t, l.SurfaceY);
        return Draw(p, crystal, x0, floor, sink, (Math.PI * 0.8) + (Math.PI * 0.2 * u) + (Math.Sin(u * Math.PI * 3) * 0.25 * (1 - u)), Scale, 0, 0.3 * (1 - u), -1, true, false);
    }

    /// <summary>Where it hits the water: a crown of drops thrown up, rings opening on the surface, a gulp of bubbles under it.</summary>
    private static void SplashAt(CapSheet p, double x, int surface, double age)
    {
        for (var n = 0; n < 26; n++)
        {
            var life = 0.4 + ((CapArt.Hash(n, 51) % 100) / 250.0);
            if (age > life) continue;
            var a = Math.PI * (0.15 + (((CapArt.Hash(n, 52) % 100) / 100.0) * 0.7));
            var v = 20 + (CapArt.Hash(n, 53) % 30);
            var px = (int)Math.Round(x - (Math.Cos(a) * v * age));
            var py = (int)Math.Round(surface - (Math.Sin(a) * v * age) + (90 * age * age));
            if (py <= surface) p.Set(px, py, n % 3 == 0 ? White : Foam, (int)(240 * (1 - (age / life))));
        }

        for (var r = 0; r < 2; r++)
        {
            var k = age - (r * 0.15);
            if (k <= 0 || k > 0.8) continue;
            var rad = 2 + (k * 26);
            for (var n = 0; n < 40; n++)
            {
                var a = n / 40.0 * Math.PI;
                int px = (int)Math.Round(x + (Math.Cos(a) * rad)), py = surface + (int)Math.Round(Math.Sin(a) * rad * 0.08);
                if (Bayer[py & 3, px & 3] < (1 - (k / 0.8)) * 16) p.Set(px, py, White, 200);
            }
        }
    }

    /// <summary>Speed lines and a column of bubbles over it as it dives.</summary>
    private static void Streaks(CapSheet p, CapTrialCrystal crystal, (double X, double Y) at, double k)
    {
        for (var n = 0; n < 7; n++)
        {
            var x = (int)Math.Round(at.X - 6 + (n * 2));
            var length = (int)(6 + (14 * k) + (CapArt.Hash(n, 3) % 6));
            for (var d = 4; d < length; d++)
            {
                var y = (int)Math.Round(at.Y) - d - 4;
                if (Bayer[y & 3, x & 3] < (1 - ((double)d / length)) * 16) p.Set(x, y, n % 2 == 0 ? crystal.Shine : Foam, 170);
            }
        }
    }

    /// <summary>A ribbon of its light along where it has been, round the tower.</summary>
    private static void Trail(CapSheet p, CapTrialCrystal crystal, double t, int x0, int floor, (double X, double Y) red, (double X, double Y) top)
    {
        for (var k = 1; k < 24; k++)
        {
            var u = (t - (k * 0.02) - Splash) / (Land - Splash);
            if (u < 0) break;
            var a = u * Math.PI * 4.5;
            var r = 30 * (1 - u);
            int x = (int)Math.Round(red.X + (Math.Cos(a) * r)), y = (int)Math.Round(top.Y + 8 + ((red.Y - top.Y - 8) * u) + (Math.Sin(a) * r * 0.3));
            if (Bayer[y & 3, x & 3] >= (1 - (k / 24.0)) * 16) continue;
            p.Set(x, y, k % 3 == 0 ? White : crystal.Shine, 220);
        }
    }

    /// <summary>Bubbles rising off it on its way down.</summary>
    private static void Bubbles(CapSheet p, (double X, double Y) at, double t, int surface)
    {
        for (var n = 0; n < 6; n++)
        {
            var age = ((t * 1.7) + (n / 6.0)) % 1;
            var x = (int)Math.Round(at.X + ((CapArt.Hash(n, 11) % 7) - 3) + (Math.Sin((age * 7) + n) * 1.2));
            var y = (int)Math.Round(at.Y - 4 - (age * 16));
            if (y > surface + 1) p.Set(x, y, Foam, (int)(190 * (1 - age)));
        }
    }

    /// <summary>Where it strikes stone: a small star of its light and silt puffing out either side.</summary>
    private static void Clink(CapSheet p, (double X, double Y) at, double age)
    {
        if (age < 0 || age > 0.5) return;
        var k = 1 - (age / 0.5);
        int cx = (int)Math.Round(at.X), cy = (int)Math.Round(at.Y);
        if (age < 0.18)
        {
            for (var d = 1; d <= 3; d++)
            {
                p.Set(cx + d, cy - 1, White, (int)(220 * k));
                p.Set(cx - d, cy - 1, White, (int)(220 * k));
            }
        }

        for (var n = 0; n < 10; n++)
        {
            var side = n % 2 == 0 ? 1 : -1;
            var v = 4 + (CapArt.Hash(n, 21) % 7);
            var x = (int)Math.Round(cx + (side * v * age * 3));
            var y = (int)Math.Round(cy - 1 - (age * (2 + (CapArt.Hash(n, 22) % 4))) + (age * age * 6));
            if (Bayer[y & 3, x & 3] < k * 16) p.Set(x, y, Silt, 200);
        }
    }

    /// <summary>Dust kicked up behind it along the slope.</summary>
    private static void Dust(CapSheet p, (double X, double Y) from, (double X, double Y) to, double k, double t)
    {
        for (var n = 0; n < 12; n++)
        {
            var at = k - (n * 0.06);
            if (at < 0) break;
            var x = from.X + ((to.X - from.X) * at);
            var y = from.Y + ((to.Y - from.Y) * at) - 1 - (n * 0.4) - (Math.Sin((t * 6) + n) * 0.6);
            int px = (int)Math.Round(x), py = (int)Math.Round(y);
            if (Bayer[py & 3, px & 3] < (1 - (n / 12.0)) * 14) p.Set(px, py, Silt, 180);
        }
    }

    private static double Ease(double k) => 1 - Math.Pow(1 - Math.Clamp(k, 0, 1), 3);

    private static double EaseInOut(double k)
    {
        k = Math.Clamp(k, 0, 1);
        return k < 0.5 ? 4 * k * k * k : 1 - (Math.Pow((-2 * k) + 2, 3) / 2);
    }
}
