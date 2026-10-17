namespace PermaLocke.App.Views;

/// <summary>
/// The Mega Evolution unlocked, at the sixth trial (2026-10-16, second version, after the Mega Evolution of Pokémon Champions the player chose:
/// light streaks closing in, a flash, an iridescent sphere that turns to obsidian, cracks of colour that turn to gold, a burst, and the Megas
/// revealed in an aurora with star dust spiralling and golden ribbons). Played from the cap rising.
/// <para>
/// The arrival: one after another, every Pokémon of the sea and the sky that has a Mega form comes in, in its own form, on a long curve of its
/// own: the fliers dive out of the sky from either end of the one sea, skim the surface, plunge in (a splash) and swing past the abyss's middle
/// before taking their place; the swimmers wind in at depth; Kyogre rises out of the abyss. Each leaves a trail of dust in its colours and banks
/// as it turns. They circle the middle as a carousel seen from a little above: the near ones larger, the far ones smaller and fainter.
/// </para>
/// <para>
/// The Mega Evolution: light streaks close in from all round, the carousel speeds up and tightens, the Pokémon turn white and stream into the
/// middle, a flash; an iridescent sphere is there, bands of pink, lime, turquoise and lilac swirling over it as it turns, rays round it; it turns
/// to obsidian from below, a web of cracks spreads over it in the symbol's colours, they turn to gold and widen, it shakes, light leaks out; it
/// bursts (a white flash, shockwaves, obsidian shards edged in gold, rainbow sparkles) and the Megas are there round the Mega symbol, white first
/// and then in their colours, in an aurora: the sea darkens, bands of green and violet wave above, star dust spirals round, two golden ribbons
/// turn round them and «MEGAEVOLUCIÓN / DESBLOQUEADA» lights up. Then they break away one by one, each on its own curve out of the sea, trailing
/// light, and the sea clears. From then on the visitors are Megas (<see cref="CapSeaContext.Megas"/>).
/// </para>
/// Pixel art: flat tones and an ordered dither, no blur and no glow. A function of the moment: no clock and no random. Everything is laid out on
/// the one sea, the middle being the abyss's; each window draws what falls in it, and the abyss's window has the aurora and the flashes.
/// </summary>
public static class CapMega
{
    /// <summary>How long the parade lasts from the cap rising.</summary>
    public const double Length = 20.0;

    // The timeline, in seconds from the cap rising.
    private const double Travel = 2.8, Gather = 8.9, Streaks = 7.9, Flash1 = 10.2, DarkFrom = 11.8, DarkTo = 12.6, CracksFrom = 12.25, GoldFrom = 13.0,
        Burst = 13.6, RevealAt = 13.75, SignAt = 14.6, ExitFrom = 16.8, ExitEach = 0.08, ExitSpan = 2.3, ClearFrom = 17.6;

    /// <summary>When they are seen in their Mega forms: from here on the visitors are Megas.</summary>
    public const double Reveal = RevealAt;

    /// <param name="Side">Where it comes from: -1 the left end of the sea, 1 the right one, 0 up out of the abyss.</param>
    /// <param name="Trail">The two colours of the dust it leaves.</param>
    private sealed record Mega(int Species, bool Flies, double Arrive, int Side, uint Trail, uint Trail2);

    private static readonly Mega[] Megas =
    [
        new(382, false, 0.6, 0, 0x4A8CFF, 0xFF6060), new(6, true, 1.0, 1, 0x46B4FF, 0x9EE6FF), new(130, false, 1.6, -1, 0x3C78D8, 0xFF7878),
        new(149, true, 2.1, 1, 0xFFB040, 0x7CE0D0), new(380, true, 2.6, -1, 0xC88CF0, 0xFFFFFF), new(381, true, 2.75, 1, 0x6ED88A, 0xFFFFFF),
        new(319, false, 3.4, 1, 0x4C7CE0, 0xF0D040), new(142, true, 3.9, -1, 0xC8B8E8, 0x9070C0), new(18, true, 4.5, 1, 0xF0C860, 0x6E9EE8),
        new(334, true, 5.0, -1, 0x9CE0FF, 0xFFFFFF), new(227, true, 5.5, 1, 0xF2C630, 0x7A7AE0), new(398, true, 6.0, -1, 0xFF6A50, 0xF2B41E)
    ];

    /// <summary>The symbol's colours, top to bottom.</summary>
    private static readonly uint[] Hue = [0xFFE45A, 0xC8E650, 0x6EDC84, 0x48D2D0, 0x4AA8F0, 0x6A7CF0, 0xA06AE8];

    /// <summary>The iridescent sphere's bands.</summary>
    private static readonly uint[] Iris = [0xFFFFFF, 0xFFC4F2, 0xF488E4, 0xE2FF9A, 0x9CF4DC, 0xC4B4FF];

    /// <summary>Obsidian, shade to light.</summary>
    private static readonly uint[] Obsidian = [0x120A1A, 0x1E1428, 0x2E2240, 0x4A3A6A];

    private const uint Ink = 0x14101C, WhiteC = 0xFFFFFF, Gold = 0xFFD65C, GoldHi = 0xFFF6BE, GoldDeep = 0xE8A030, Night = 0x120826, Deep = 0x0A2848,
        Foam = 0xE8FAFF, Aurora1 = 0x3CE8A8, Aurora2 = 0xB868F8, Dust = 0xBFF4FF, Rim = 0x2A1840;

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>The Mega symbol, cell by cell (traced from the official one at a quarter of its size): the helix and its hollow rungs.</summary>
    private static readonly string[] Helix =
    [
        "................##..........", "............######..........", "..........########..........", "........#########...........",
        ".......##########...........", ".....#######.####...........", "....######...####...........", "...######....####...........",
        "...######....####...........", "..#########...###...........", ".#################..........", ".####..############.........",
        ".###......##########........", "#####.......#########.......", "#######........########.....", "#########.........######....",
        "############.......######...", ".#############......######..", ".####..##########.....####..", "..####....###########..####.",
        "..#####......##############.", "...######.......############", "....######.........#########", ".....#######...........#####",
        "......#########..........###", "........##########.......###", ".........#############..####", "..........##################",
        "............###############.", "............####....#######.", ".............####....#####..", ".............####..######...",
        "..............##########....", "..............#########.....", "..............#######.......", "..............######........",
        "..............###..........."
    ];

    private const int HelixW = 28, HelixH = 37;

    /// <summary>The cracks' seeds on the sphere: points spread over it (a Fibonacci sphere, nudged).</summary>
    private static readonly (double X, double Y, double Z)[] Seeds = SeedsOf(30);

    /// <summary>Where things are in this window: the abyss's middle, the carousel's radius, the surface, the floor, the ends of the sea, the size.</summary>
    private readonly record struct Stage(double Cx, double Cy, double Ring, double Sphere, int Surface, int Floor, double Left, double Right, int W, int H, double Z);

    /// <summary>One of them at a moment: where, how big, how near (1 in front), facing which way, turned how much, how white, how much drawn.</summary>
    private readonly record struct Pose(double X, double Y, double Scale, double Depth, bool Right, double Tilt, double White, double Keep, bool Moving);

    /// <summary>How long ago the parade started, while it plays: the cap rose at the sixth trial.</summary>
    public static double? Since(CapPlaqueData data, CapAnim? anim, double t) =>
        data.Cleared == 6 && anim?.CapRise(t) is { } rise && rise.Since < Length ? rise.Since : null;

    /// <summary>Paints its part in one window, <paramref name="s"/> seconds in.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double s, bool deep)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip || s < 0 || s > Length) return;
        var host = deep || ctx.WorldWidth <= 0;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var water = Math.Max(20, l.FloorTop - l.SurfaceY);
        // the abyss's middle in this window: the abyss is the first window of the sea, about as wide as this one
        var cx = (l.Cols / 2.0) - ctx.WorldX;
        var cy = l.SurfaceY + (water * 0.42);
        var ring = Math.Max(18, Math.Min(l.Cols * 0.36, water * 0.5));
        var st = new Stage(cx, cy, ring, Math.Max(10, Math.Min(l.Cols * 0.17, water * 0.24)), l.SurfaceY, l.FloorTop, -ctx.WorldX - 70,
            world - ctx.WorldX + 70, p.W, p.H, Math.Clamp(l.Cols / 85.0, 1, 2.6));

        // the aurora behind it all, in the abyss's window
        if (host) Aurora(p, st, s);

        var poses = new List<(int I, Pose P)>();
        for (var i = 0; i < Megas.Length; i++)
        {
            if (PoseOf(i, s, st) is { } pose) poses.Add((i, pose));
        }

        poses.Sort((a, b) => a.P.Depth.CompareTo(b.P.Depth));
        foreach (var (i, pose) in poses) Splashes(p, st, i, s);
        foreach (var (i, pose) in poses.Where(q => q.P.Depth < 0)) Draw(p, st, i, pose, s);
        Ribbons(p, st, s, back: true);

        LightStreaks(p, st, s);
        Sphere(p, st, s);
        Burst1(p, st, s);
        Symbol(p, (int)Math.Round(st.Cx), (int)Math.Round(st.Cy), st.Z * 0.55 * (1 + (0.03 * Math.Sin(s * 3))), s, SymbolKeep(s), s is >= RevealAt and < RevealAt + 0.12);

        foreach (var (i, pose) in poses.Where(q => q.P.Depth >= 0)) Draw(p, st, i, pose, s);
        Ribbons(p, st, s, back: false);
        StarDust(p, st, s);
        var signScale = st.Z > 1.5 ? 2 : 1;
        Sign(p, (int)Math.Round(st.Cx), st.Floor - (22 * signScale), s, signScale);

        // the flashes: full in the abyss's window, half in the other
        var flash = Math.Max(Fade(s, Flash1, 0.35), Fade(s, Burst, 0.5));
        Flash(p, host ? flash : flash * 0.5);
    }

    // ---------------------------------------------------------------- where they are

    private static double Fade(double s, double at, double span) => s >= at && s < at + span ? 1 - ((s - at) / span) : 0;

    /// <summary>The carousel's turn for one of them at a moment: slow, faster as it tightens.</summary>
    private static double AngleOf(int i, double s)
    {
        var g = Math.Clamp((s - Gather) / (Flash1 - Gather), 0, 1);
        return (i * Math.PI * 2 / Megas.Length) + (s * 0.42) + (g * g * g * 7);
    }

    /// <summary>The carousel's radius: whole, tightening into the middle before the flash, opening out of it after the burst.</summary>
    private static double RingOf(double s, double ring) =>
        s < Gather ? ring : s < Flash1 ? ring * (1 - Ease((s - Gather) / (Flash1 - Gather))) : ring * Ease((s - RevealAt) / 0.7);

    private static (double X, double Y, double Depth) Slot(int i, double s, Stage st)
    {
        var a = AngleOf(i, s);
        var r = RingOf(s, st.Ring);
        return (st.Cx + (Math.Cos(a) * r), st.Cy + (Math.Sin(a) * r * 0.36), Math.Sin(a));
    }

    /// <summary>A point on a Catmull-Rom curve through the given points at 0 to 1 along it.</summary>
    private static (double X, double Y) Curve((double X, double Y)[] k, double u)
    {
        var n = k.Length - 1;
        var f = Math.Clamp(u, 0, 1) * n;
        var seg = Math.Min(n - 1, (int)Math.Floor(f));
        var t = f - seg;
        var p0 = seg == 0 ? (X: (2 * k[0].X) - k[1].X, Y: (2 * k[0].Y) - k[1].Y) : k[seg - 1];
        var p1 = k[seg];
        var p2 = k[seg + 1];
        var p3 = seg + 2 > n ? (X: (2 * k[n].X) - k[n - 1].X, Y: (2 * k[n].Y) - k[n - 1].Y) : k[seg + 2];
        double C(double a, double b, double c, double d) => 0.5 * ((2 * b) + ((-a + c) * t) + (((2 * a) - (5 * b) + (4 * c) - d) * t * t) + ((-a + (3 * b) - (3 * c) + d) * t * t * t));
        return (C(p0.X, p1.X, p2.X, p3.X), C(p0.Y, p1.Y, p2.Y, p3.Y));
    }

    /// <summary>The way in: from its end of the sea (or the abyss), the fliers diving, skimming, plunging; the swimmers winding; past the middle to its place.</summary>
    private static (double X, double Y)[] WayIn(int i, Stage st)
    {
        var m = Megas[i];
        var end = Slot(i, m.Arrive + Travel, st);
        var side = m.Side == 0 ? -1 : m.Side;
        var x0 = m.Side < 0 ? st.Left : st.Right;
        if (m.Side == 0)
        {
            return [(st.Cx + (st.Ring * 0.2), st.Floor + 70), (st.Cx - (st.Ring * 0.7), st.Floor - 12), (st.Cx + (st.Ring * 0.95), st.Cy + (st.Ring * 0.25)), (end.X, end.Y)];
        }

        if (m.Flies)
        {
            var y0 = 6 + ((i * 5) % Math.Max(1, st.Surface - 26));
            return [(x0, y0), (x0 + ((st.Cx - x0) * 0.42), st.Surface - 5), (st.Cx - (side * st.Ring * 0.95), st.Cy + (st.Ring * 0.5)), (end.X, end.Y)];
        }

        var y1 = st.Surface + 18 + ((i * 11) % Math.Max(1, st.Floor - st.Surface - 40));
        return [(x0, y1), (x0 + ((st.Cx - x0) * 0.4), y1 + ((i % 2 == 0 ? 1 : -1) * 22)), (st.Cx - (side * st.Ring * 0.9), st.Cy - (st.Ring * 0.3)), (end.X, end.Y)];
    }

    /// <summary>The way out: from its place, out a little, then off to its side of the sea, the fliers up into the sky, the swimmers low.</summary>
    private static (double X, double Y)[] WayOut(int i, Stage st)
    {
        var m = Megas[i];
        var from = Slot(i, ExitFrom + (i * ExitEach), st);
        var dx = from.X - st.Cx;
        var side = dx >= 0 ? 1 : -1;
        var x1 = side > 0 ? st.Right : st.Left;
        var near = (from.X + (dx * 0.6), from.Y - (m.Flies ? st.Ring * 0.25 : -st.Ring * 0.1));
        return m.Flies
            ? [(from.X, from.Y), near, (from.X + (side * st.Ring * 1.6), st.Surface - 8), (x1, 6 + ((i * 7) % Math.Max(1, st.Surface - 20)))]
            : [(from.X, from.Y), near, (from.X + (side * st.Ring * 1.5), st.Floor - 30), (x1, st.Surface + 26 + ((i * 9) % Math.Max(1, st.Floor - st.Surface - 50)))];
    }

    private static double Smooth(double u) => u * u * (3 - (2 * u));

    /// <summary>Where it is on its way at a moment, or null when it is not on one.</summary>
    private static (double X, double Y)? OnWay(int i, double s, Stage st)
    {
        var m = Megas[i];
        if (s >= m.Arrive && s < m.Arrive + Travel) return Curve(WayIn(i, st), Smooth((s - m.Arrive) / Travel));
        var from = ExitFrom + (i * ExitEach);
        if (s >= from && s < from + ExitSpan)
        {
            var u = (s - from) / ExitSpan;
            return Curve(WayOut(i, st), u * u * (2 - u));
        }

        return null;
    }

    /// <summary>Where it is at a moment: on its way, in the carousel, or nowhere (before it comes, inside the sphere, gone).</summary>
    private static (double X, double Y, double Depth)? Where(int i, double s, Stage st)
    {
        var m = Megas[i];
        if (s < m.Arrive || (s >= Flash1 && s < RevealAt) || s >= ExitFrom + (i * ExitEach) + ExitSpan) return null;
        if (OnWay(i, s, st) is { } way) return (way.X, way.Y, 1);
        return Slot(i, s, st);
    }

    private static Pose? PoseOf(int i, double s, Stage st)
    {
        if (Where(i, s, st) is not { } here) return null;
        var before = Where(i, s - 0.04, st) ?? here;
        double vx = here.X - before.X, vy = here.Y - before.Y;
        var moving = OnWay(i, s, st) is not null;
        var heading = Math.Atan2(vy, Math.Max(0.2, Math.Abs(vx))) * 180 / Math.PI;

        var g = Math.Clamp((s - Gather) / (Flash1 - Gather), 0, 1);
        var m = Megas[i];
        double scale, white = 0;
        if (s < Flash1)
        {
            var ring = 0.4 * (0.8 + (0.24 * here.Depth));
            scale = moving ? 0.36 + (0.2 * (1 - Smooth(Math.Clamp((s - m.Arrive) / Travel, 0, 1)))) : ring * (1 - (0.8 * g));
            white = Ease(g * 1.4);
        }
        else
        {
            var ring = 0.4 * (0.8 + (0.24 * here.Depth));
            var from = ExitFrom + (i * ExitEach);
            scale = s < from ? ring : ring + (0.24 * Math.Clamp((s - from) / ExitSpan, 0, 1));
            white = 1 - Ease((s - RevealAt - (i * 0.05)) / 0.9);
        }

        // the far side of the carousel is fainter, sunk in the water
        return new Pose(here.X, here.Y, scale, here.Depth, vx > 0, Math.Clamp(-heading, -8, 8), white, 1, moving);
    }

    private static void Draw(CapSheet p, Stage st, int i, Pose pose, double s)
    {
        var m = Megas[i];
        var mega = s >= RevealAt;
        if (!CritterArt.All.TryGetValue(mega ? 10000 + m.Species : m.Species, out var art)) return;
        Trail(p, st, i, s);
        var far = Math.Max(0, -pose.Depth) * 0.35;
        var mixTo = pose.White > 0 ? WhiteC : Deep;
        var mix = pose.White > 0 ? pose.White : far;
        CapVisitors.Centred(p, art, s + (i * 0.3), (int)Math.Round(pose.X), (int)Math.Round(pose.Y), pose.Scale, pose.Right, mixTo, mix, pose.Tilt, pose.Keep);
    }

    /// <summary>The dust it leaves behind it: dots of its two colours along where it has been, thinning; a twinkle now and then.</summary>
    private static void Trail(CapSheet p, Stage st, int i, double s)
    {
        var m = Megas[i];
        var gathering = s is >= Gather and < Flash1;
        var count = OnWay(i, s, st) is not null || gathering ? 16 : 5;
        for (var k = 1; k <= count; k++)
        {
            if (Where(i, s - (k * 0.035), st) is not { } at || (at.Depth < 0 && !gathering && OnWay(i, s, st) is null && k > 2)) continue;
            var keep = 1 - (k / (double)(count + 1));
            var spread = k * 0.25;
            int x = (int)Math.Round(at.X + ((Rnd(i * 31, k + (int)(s * 20)) - 0.5) * spread)), y = (int)Math.Round(at.Y + ((Rnd(i * 37, k + (int)(s * 20)) - 0.5) * spread));
            if (Bayer[y & 3, x & 3] >= keep * 16) continue;
            var colour = gathering ? WhiteC : k % 2 == 0 ? m.Trail : m.Trail2;
            p.Set(x, y, colour);
            if (k % 5 == 0 && keep > 0.4) Twinkle(p, x, y, 1, colour);
        }
    }

    /// <summary>A flier going into the water or out of it: a crown of drops and a ring opening on the surface.</summary>
    private static void Splashes(CapSheet p, Stage st, int i, double s)
    {
        var m = Megas[i];
        if (!m.Flies) return;
        foreach (var (from, span) in new[] { (m.Arrive, Travel), (ExitFrom + (i * ExitEach), ExitSpan) })
        {
            if (s < from) continue;
            (double X, double Y)? last = null;
            for (var u = from; u <= Math.Min(s, from + span); u += 0.04)
            {
                if (OnWay(i, u, st) is not { } q) continue;
                if (last is { } a && (a.Y < st.Surface) != (q.Y < st.Surface))
                {
                    var age = s - u;
                    if (age < 0.8) Splash(p, (int)Math.Round(q.X), st.Surface, age, (i * 7) + (int)(from * 10), st.Z);
                }

                last = q;
            }
        }
    }

    private static void Splash(CapSheet p, int x, int surface, double age, int seed, double z)
    {
        var keep = 1 - (age / 0.8);
        var ring = (int)Math.Round(age * 26 * z);
        for (var dx = -ring; dx <= ring; dx++)
        {
            if (Math.Abs(dx) < ring - 2 || Bayer[surface & 3, (x + dx) & 3] >= keep * 16) continue;
            p.Set(x + dx, surface, Foam);
        }

        for (var n = 0; n < 12; n++)
        {
            var vx = (Rnd(seed, n) - 0.5) * 34 * z;
            var vy = -(22 + (Rnd(seed, n + 20) * 26)) * z;
            int px = x + (int)Math.Round(vx * age), py = surface + (int)Math.Round((vy * age) + (70 * z * age * age));
            if (py > surface || Bayer[py & 3, px & 3] >= keep * 18) continue;
            p.Set(px, py, n % 3 == 0 ? WhiteC : Foam);
        }
    }

    // ---------------------------------------------------------------- the Mega Evolution

    /// <summary>Light closing in from all round before the flash: thin streaks of the symbol's colours with white heads, more and faster.</summary>
    private static void LightStreaks(CapSheet p, Stage st, double s)
    {
        if (s < Streaks || s >= Flash1) return;
        var count = (int)Math.Min(36, (s - Streaks) * 22);
        var reach = Math.Max(st.W, st.H) * 0.8;
        for (var n = 0; n < count; n++)
        {
            var a = Rnd(n, 51) * Math.PI * 2;
            var life = ((s * (1.4 + Rnd(n, 52))) + Rnd(n, 53)) % 1;
            var r = reach * (1 - life);
            var length = (8 + (Rnd(n, 54) * 18)) * st.Z * 0.6;
            for (var k = 0; k < length; k++)
            {
                var d = r + k;
                if (d < st.Sphere * 0.3) continue;
                int x = (int)Math.Round(st.Cx + (Math.Cos(a) * d)), y = (int)Math.Round(st.Cy + (Math.Sin(a) * d * 0.7));
                if (k > 2 && Bayer[y & 3, x & 3] >= (1 - (k / length)) * 16) continue;
                p.Set(x, y, k < 2 ? WhiteC : Hue[n % 7]);
            }
        }
    }

    /// <summary>
    /// The sphere: born in the flash, iridescent (bands swirling as it turns, lit from the upper left, rays round it), then obsidian from below,
    /// a web of cracks spreading over it in the symbol's colours, turning gold and widening while it shakes and leaks light; gone at the burst.
    /// </summary>
    private static void Sphere(CapSheet p, Stage st, double s)
    {
        if (s < Flash1 || s >= Burst) return;
        var born = Math.Clamp((s - Flash1) / 0.3, 0, 1);
        var rs = st.Sphere * (0.6 + (0.4 * born) + (born >= 1 ? 0.03 * Math.Sin(s * 9) : 0));
        var cx = st.Cx;
        if (s >= GoldFrom) cx += (Hash((int)(s * 16), 5) % 3) - 1;
        var dark = Math.Clamp((s - DarkFrom) / (DarkTo - DarkFrom), 0, 1);
        var gold = Math.Clamp((s - GoldFrom) / (Burst - GoldFrom), 0, 1);
        var width = s < CracksFrom ? 0 : (0.02 * Math.Clamp((s - CracksFrom) / 0.3, 0, 1)) + (0.045 * gold);
        var spin = s * 0.9;
        double co = Math.Cos(spin), si = Math.Sin(spin);

        // the rays round the iridescent sphere
        if (dark < 1)
        {
            for (var n = 0; n < 14; n++)
            {
                var a = (Rnd(n, 61) * Math.PI * 2) + (s * 0.3);
                var flick = ((s * 3) + Rnd(n, 62)) % 1;
                if (flick > 0.6 - (dark * 0.5)) continue;
                var length = (6 + (Rnd(n, 63) * 14)) * st.Z * 0.5;
                for (var k = 0; k < length; k++)
                {
                    var d = rs + 2 + k;
                    int x = (int)Math.Round(cx + (Math.Cos(a) * d)), y = (int)Math.Round(st.Cy + (Math.Sin(a) * d));
                    if (Bayer[y & 3, x & 3] >= (1 - (k / length)) * 14) continue;
                    p.Set(x, y, k < length * 0.3 ? WhiteC : Iris[1 + (n % 5)]);
                }
            }
        }

        var r = (int)Math.Ceiling(rs) + 1;
        for (var y = -r; y <= r; y++)
        {
            for (var x = -r; x <= r; x++)
            {
                double nx = (x + 0.5) / rs, ny = (y + 0.5) / rs, d2 = (nx * nx) + (ny * ny);
                int px = (int)Math.Round(cx) + x, py = (int)Math.Round(st.Cy) + y;
                if (d2 > 1)
                {
                    if (d2 < Math.Pow(1 + (1.3 / rs), 2)) p.Set(px, py, Ink);
                    continue;
                }

                var nz = Math.Sqrt(1 - d2);
                double qx = (nx * co) + (nz * si), qy = ny, qz = (-nx * si) + (nz * co);
                var light = (-0.5 * nx) - (0.6 * ny) + (0.62 * nz);
                var spec = ((nx + 0.42) * (nx + 0.42)) + ((ny + 0.46) * (ny + 0.46)) < 0.018;
                var b = Bayer[py & 3, px & 3] / 16.0;

                // obsidian creeping up from below
                var isDark = ((ny * 0.85) - (nx * 0.25) + 1) / 2 > 1 - (dark * 1.25) + ((b - 0.5) * 0.12);
                uint c;
                if (isDark)
                {
                    var tone = light > 0.45 ? 2 : light > -0.05 ? 1 : 0;
                    c = Obsidian[tone];
                    if (d2 > Math.Pow(1 - (1.6 / rs), 2) && nx < 0.3) c = Obsidian[3];
                    if (spec) c = Obsidian[3];
                }
                else
                {
                    var v = Math.Sin((qy * 5) + (2.2 * Math.Sin((qx * 3) + (s * 1.7))) + (s * 2.3));
                    var w = Math.Sin((qx * 4) - (qz * 3) + (s * 1.1));
                    var band = (int)Math.Floor((((v * 0.5) + 0.5) * 5) + (w * 0.6) + b);
                    c = Iris[((band % Iris.Length) + Iris.Length) % Iris.Length];
                    if (light < -0.15) c = CapSheet.Darker(c, 0.22);
                    if (d2 > Math.Pow(1 - (1.4 / rs), 2)) c = Rim;
                    if (spec) c = WhiteC;
                }

                // the cracks, on the obsidian
                if (isDark && width > 0 && Crack(qx, qy, qz, s, width) is { } crack)
                {
                    c = gold <= 0 ? Hue[crack.Pair % 7] : crack.Core ? (gold > 0.5 ? WhiteC : GoldHi) : gold > 0.3 ? Gold : Hue[crack.Pair % 7];
                }

                p.Set(px, py, c);
            }
        }

        // light leaking out of the cracks
        if (gold > 0)
        {
            for (var n = 0; n < 22; n++)
            {
                var life = ((s * 2.2) + Rnd(n, 71)) % 1;
                var a = Rnd(n, 72 + (int)((s * 2.2) + Rnd(n, 71))) * Math.PI * 2;
                var d = rs * (0.8 + (life * 0.9));
                int x = (int)Math.Round(cx + (Math.Cos(a) * d)), y = (int)Math.Round(st.Cy + (Math.Sin(a) * d));
                if (Bayer[y & 3, x & 3] < (1 - life) * 16) p.Set(x, y, life < 0.3 ? WhiteC : Gold);
            }
        }
    }

    /// <summary>The crack at a point of the sphere (turned with it), if there is one: the pair of cells it parts, and whether it is its core.</summary>
    private static (int Pair, bool Core)? Crack(double x, double y, double z, double s, double width)
    {
        int a = -1, b = -1;
        double da = double.MaxValue, db = double.MaxValue;
        for (var n = 0; n < Seeds.Length; n++)
        {
            var d = 1 - ((Seeds[n].X * x) + (Seeds[n].Y * y) + (Seeds[n].Z * z));
            if (d < da)
            {
                db = da;
                b = a;
                da = d;
                a = n;
            }
            else if (d < db)
            {
                db = d;
                b = n;
            }
        }

        var gap = db - da;
        if (gap >= width) return null;
        var pair = (Math.Min(a, b) * 31) + Math.Max(a, b);
        // the web spreads from one point of the sphere (low on its near left), each crack a little earlier or later
        var from = 1 - ((-0.35 * x) + (0.45 * y) + (0.82 * z));
        if ((s - CracksFrom) * 3.2 < from + (Rnd(pair, 77) * 0.35)) return null;
        return (pair, gap < width * 0.45);
    }

    /// <summary>The burst: shockwaves running out, obsidian shards edged in gold, rainbow sparkles.</summary>
    private static void Burst1(CapSheet p, Stage st, double s)
    {
        var age = s - Burst;
        if (age < 0 || age > 1.6) return;
        var cx = (int)Math.Round(st.Cx);
        var cy = (int)Math.Round(st.Cy);
        for (var w = 0; w < 2; w++)
        {
            var at = age - (w * 0.12);
            if (at is < 0 or > 0.8) continue;
            var r = st.Sphere + (at * Math.Max(st.W, st.H) * 1.4);
            var keep = 1 - (at / 0.8);
            var steps = (int)(r * 7);
            for (var k = 0; k < steps; k++)
            {
                var a = k / (double)steps * Math.PI * 2;
                for (var th = 0; th < 2; th++)
                {
                    int x = cx + (int)Math.Round(Math.Cos(a) * (r - th)), y = cy + (int)Math.Round(Math.Sin(a) * (r - th) * 0.8);
                    if (Bayer[y & 3, x & 3] >= keep * 16) continue;
                    p.Set(x, y, w == 0 ? WhiteC : Hue[(int)(a * 7 / (Math.PI * 2)) % 7]);
                }
            }
        }

        for (var n = 0; n < 30 && age < 1.4; n++) Shard(p, cx, cy, age, n + 40, st.Z * 0.8, st.Sphere);
        for (var n = 0; n < 32; n++)
        {
            var a = (n / 32.0 * Math.PI * 2) + (Rnd(n, 81) * 0.3);
            var d = st.Sphere + (age * st.Ring * 2.2 * (0.6 + (Rnd(n, 82) * 0.7)));
            var beat = ((age * 3) + Rnd(n, 83)) % 1;
            var size = age > 1.2 ? (beat < 0.3 ? 1 : 0) : beat < 0.15 ? 3 : beat < 0.45 ? 2 : 1;
            Twinkle(p, cx + (int)Math.Round(Math.Cos(a) * d), cy + (int)Math.Round(Math.Sin(a) * d * 0.85), size, Hue[n % 7]);
        }
    }

    /// <summary>A shard of obsidian: a triangle edged in gold or one of the symbol's colours, turning as it flies out and falls, thinning at the end.</summary>
    private static void Shard(CapSheet p, int cx, int cy, double age, int n, double z, double from)
    {
        var ang = Rnd(n, 7) * Math.PI * 2;
        var v = (34 + (Rnd(n, 8) * 40)) * z;
        var x0 = cx + (Math.Cos(ang) * ((from * 0.6) + (v * age)));
        var y0 = cy + (Math.Sin(ang) * ((from * 0.6) + (v * age)) * 0.8) + (22 * z * age * age);
        var keep = 1 - (age / 1.4);
        var size = (2.5 + (Rnd(n, 9) * 3)) * Math.Min(1.8, z * 0.8);
        var spin = (Rnd(n, 10) * 6) + (age * (4 + (Rnd(n, 11) * 6)));
        var corners = new (double X, double Y)[3];
        for (var c = 0; c < 3; c++)
        {
            var a = spin + (c * 2.1) + (Rnd(n, 12 + c) * 0.6);
            corners[c] = (x0 + (Math.Cos(a) * size), y0 + (Math.Sin(a) * size));
        }

        var edgeColour = n % 3 == 0 ? Hue[n % 7] : Gold;
        var facet = Math.Sin(spin) > 0;
        for (var y = (int)Math.Floor(corners.Min(c => c.Y)) - 1; y <= (int)Math.Ceiling(corners.Max(c => c.Y)) + 1; y++)
        {
            for (var x = (int)Math.Floor(corners.Min(c => c.X)) - 1; x <= (int)Math.Ceiling(corners.Max(c => c.X)) + 1; x++)
            {
                if (!InTriangle((x, y), corners) || (keep < 1 && Bayer[y & 3, x & 3] >= keep * 18)) continue;
                var edge = !InTriangle((x + 1, y), corners) || !InTriangle((x - 1, y), corners) || !InTriangle((x, y + 1), corners) || !InTriangle((x, y - 1), corners);
                p.Set(x, y, edge ? edgeColour : facet ? CapSheet.Lighter(Obsidian[3], 0.25) : Obsidian[2]);
            }
        }
    }

    // ---------------------------------------------------------------- the reveal

    /// <summary>How strong the aurora is: in with the burst, out as they leave.</summary>
    private static double AuroraOf(double s) => Math.Min(Math.Clamp((s - Burst) / 0.3, 0, 1), 1 - Math.Clamp((s - ClearFrom) / 1.0, 0, 1));

    /// <summary>The sea darkens to night violet and two curtains of aurora hang across the top, green fading to violet, rays drifting along them.</summary>
    private static void Aurora(CapSheet p, Stage st, double s)
    {
        var k = AuroraOf(s);
        if (k <= 0) return;
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (p.Solid(x, y)) p.Set(x, y, Night, (int)(165 * k));
            }
        }

        // the curtains: from a bright lower edge just over the water, each column rises in a ray of its own height, green and then violet at its
        // top, brighter rays drifting along
        for (var curtain = 0; curtain < 2; curtain++)
        {
            var top = st.Surface - (2 * st.Z) - (curtain * 3 * st.Z);
            for (var x = 0; x < p.W; x++)
            {
                var wx = x - st.Cx;
                var edge = top + (1.5 * Math.Sin((wx * 0.07) + (s * 0.9) + (curtain * 2.1)));
                var ray = 0.55 + (0.45 * Math.Sin((wx * 0.21) + (s * 2.2) + (curtain * 1.7))) * Math.Sin((wx * 0.057) - (s * 0.9));
                var length = (14 + (14 * ray) + (8 * Math.Sin((wx * 0.03) + (s * 0.5) + (curtain * 2.4)))) * st.Z * (curtain == 0 ? 1 : 0.7);
                for (var d = 0; d < length; d++)
                {
                    var y = (int)Math.Round(edge) - d;
                    var down = d / length;
                    var strength = k * (1 - down) * (0.45 + (0.55 * ray)) * (curtain == 0 ? 1 : 0.75);
                    if (y < 0 || y >= p.H || Bayer[y & 3, x & 3] >= strength * 14) continue;
                    var colour = d < 2 ? CapSheet.Lighter(Aurora1, 0.5) : down < 0.6 ? Aurora1 : Aurora2;
                    p.Set(x, y, curtain == 0 ? colour : CapSheet.Mix(colour, Aurora2, 0.5), 190);
                }
            }
        }
    }

    /// <summary>Two golden ribbons turning round them, each a running arc, white at its head; the half behind them drawn first.</summary>
    private static void Ribbons(CapSheet p, Stage st, double s, bool back)
    {
        var k = Math.Min(Math.Clamp((s - RevealAt - 0.2) / 0.4, 0, 1), 1 - Math.Clamp((s - ExitFrom) / 0.8, 0, 1));
        if (k <= 0) return;
        for (var rib = 0; rib < 2; rib++)
        {
            var head = (s * 1.6) + (rib * Math.PI);
            var lean = rib == 0 ? 1 : -1;
            for (var j = 0; j < 60; j++)
            {
                var th = head - (j * 0.045);
                var behind = Math.Sin(th) < 0;
                if (behind != back) continue;
                int x = (int)Math.Round(st.Cx + (Math.Cos(th) * st.Ring * 1.12)),
                    y = (int)Math.Round(st.Cy + (Math.Sin(th) * st.Ring * 0.42) + (lean * Math.Cos(th) * st.Ring * 0.14));
                var keep = (1 - (j / 60.0)) * k;
                if (Bayer[y & 3, x & 3] >= keep * 16) continue;
                var colour = j < 3 ? WhiteC : j < 14 ? GoldHi : j % 7 == 0 ? GoldDeep : Gold;
                p.Set(x, y, colour);
                if (j < 20) p.Set(x, y + 1, j < 3 ? GoldHi : GoldDeep);
            }
        }
    }

    /// <summary>Star dust spiralling out round them: white, gold and pale blue specks with a speck behind each.</summary>
    private static void StarDust(CapSheet p, Stage st, double s)
    {
        var k = Math.Min(Math.Clamp((s - Burst) / 0.3, 0, 1), 1 - Math.Clamp((s - ClearFrom) / 1.2, 0, 1));
        if (k <= 0) return;
        for (var n = 0; n < 96; n++)
        {
            for (var tail = 0; tail < 2; tail++)
            {
                var life = (((s - (tail * 0.05) - Burst) * 0.32) + Rnd(n, 1)) % 1;
                var r = (0.15 + (1.15 * life)) * st.Ring;
                var a = (Rnd(n, 2) * Math.PI * 2) + (life * 3.4) + (s * 0.25);
                int x = (int)Math.Round(st.Cx + (Math.Cos(a) * r)), y = (int)Math.Round(st.Cy + (Math.Sin(a) * r * 0.55) - (life * 10));
                if (Bayer[y & 3, x & 3] >= (1 - life) * k * (tail == 0 ? 16 : 8)) continue;
                p.Set(x, y, n % 3 == 0 ? Gold : n % 3 == 1 ? WhiteC : Dust);
            }
        }
    }

    private static double SymbolKeep(double s) =>
        s < RevealAt ? 0 : Math.Min(Ease((s - RevealAt) / 0.5), 1 - Math.Clamp((s - (ExitFrom + 0.6)) / 1.0, 0, 1));

    /// <summary>A flash over the whole window: an ordered dither of white, as much as <paramref name="a"/>.</summary>
    private static void Flash(CapSheet p, double a)
    {
        if (a <= 0) return;
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (Bayer[y & 3, x & 3] < a * 16) p.Set(x, y, WhiteC);
            }
        }
    }

    // ---------------------------------------------------------------- the symbol and the sign

    /// <summary>The helix cell at a point of its own grid (its middle at 0, 0), and its row.</summary>
    private static (bool Inside, int Row) InHelix(double x, double y)
    {
        var col = (int)Math.Floor(x + (HelixW / 2.0));
        var row = (int)Math.Floor(y + (HelixH / 2.0));
        return (col is >= 0 and < HelixW && row is >= 0 and < HelixH && Helix[row][col] == '#', row);
    }

    /// <summary>
    /// The Mega symbol at <paramref name="scale"/> cells per cell of its trace: each row its colour from yellow to violet, lit on its upper left
    /// edges and shaded on its lower right ones, an ink outline; a light sweeping across it now and then; sparkles of its colours round it.
    /// <paramref name="white"/> draws it all white, for the flash it is born in.
    /// </summary>
    private static void Symbol(CapSheet p, int cx, int cy, double scale, double s, double keep, bool white)
    {
        if (scale <= 0 || keep <= 0) return;
        var hw = (int)Math.Ceiling(HelixW * scale / 2) + 2;
        var hh = (int)Math.Ceiling(HelixH * scale / 2) + 2;
        bool In(int x, int y) => InHelix(x / scale, y / scale).Inside;
        var sweep = ((s * 0.7) % 1 * 3) - 1;
        for (var y = -hh; y <= hh; y++)
        {
            for (var x = -hw; x <= hw; x++)
            {
                int px = cx + x, py = cy + y;
                if (Bayer[py & 3, px & 3] >= keep * 16) continue;
                if (!In(x, y))
                {
                    if (In(x + 1, y) || In(x - 1, y) || In(x, y + 1) || In(x, y - 1)) p.Set(px, py, Ink);
                    continue;
                }

                if (white)
                {
                    p.Set(px, py, WhiteC);
                    continue;
                }

                var hue = Hue[Math.Clamp(InHelix(x / scale, y / scale).Row * 7 / HelixH, 0, 6)];
                var colour = !In(x - 1, y) || !In(x, y - 1) ? CapSheet.Lighter(hue, 0.45) : !In(x + 1, y) || !In(x, y + 1) ? CapSheet.Darker(hue, 0.3) : hue;
                if (Math.Abs((((x / (double)hw) + (y / (double)hh)) / 2) - sweep) < 0.06) colour = WhiteC;
                p.Set(px, py, colour);
            }
        }

        for (var n = 0; n < 6; n++)
        {
            var a = (s * 1.3) + (n / 6.0 * Math.PI * 2);
            var beat = ((s * 2.1) + (n * 0.27)) % 1;
            var size = beat < 0.12 ? 3 : beat < 0.3 ? 2 : beat < 0.55 ? 1 : 0;
            int tx = cx + (int)Math.Round(Math.Cos(a) * hw * 1.25), ty = cy + (int)Math.Round(Math.Sin(a) * hh * 1.05);
            if (Bayer[ty & 3, tx & 3] < keep * 16) Twinkle(p, tx, ty, size, Hue[n]);
        }
    }

    /// <summary>«MEGAEVOLUCIÓN / DESBLOQUEADA»: each letter lit in turn with a white flash, then gold, a light of the colours sweeping along.</summary>
    private static void Sign(CapSheet p, int cx, int y, double s, int scale)
    {
        const double Each = 0.06;
        var alpha = (int)(255 * Math.Clamp((ClearFrom + 0.8 - s) / 0.8, 0, 1));
        if (s < SignAt || alpha <= 0) return;
        string[] lines = ["MEGAEVOLUCIÓN", "DESBLOQUEADA"];
        var index = 0;
        for (var line = 0; line < lines.Length; line++)
        {
            var text = lines[line];
            var x0 = cx - (CapGlyphs.Width(text, true, scale) / 2);
            for (var i = 0; i < text.Length; i++, index++)
            {
                var lit = s - SignAt - (index * Each);
                if (lit < 0) return;
                var x = x0 + CapGlyphs.Width(text[..i], true, scale);
                var sweep = (((s - SignAt) * 40) % 160) - 20;
                var colour = lit < 0.12 ? WhiteC : Math.Abs(x - (x0 + sweep)) < 4 ? Hue[(index + (int)(s * 8)) % 7] : line == 0 ? Gold : GoldHi;
                CapGlyphs.Outlined(p, text[i].ToString(), x, y + (line * 10 * scale) + (lit < 0.12 ? -1 : 0), colour, Ink, big: true, scale: scale, alpha: alpha);
            }
        }
    }

    private static void Twinkle(CapSheet p, int x, int y, int size, uint colour)
    {
        p.Set(x, y, WhiteC);
        for (var k = 1; k <= size; k++)
        {
            var c = k == size ? colour : CapSheet.Lighter(colour, 0.5);
            p.Set(x + k, y, c);
            p.Set(x - k, y, c);
            p.Set(x, y + k, c);
            p.Set(x, y - k, c);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static (double X, double Y, double Z)[] SeedsOf(int n)
    {
        var seeds = new (double X, double Y, double Z)[n];
        var golden = Math.PI * (3 - Math.Sqrt(5));
        for (var i = 0; i < n; i++)
        {
            var y = Math.Clamp(1 - (2 * (i + 0.5) / n) + ((Rnd(i, 3) - 0.5) * 0.2), -1, 1);
            var r = Math.Sqrt(Math.Max(0, 1 - (y * y)));
            var a = (i * golden) + ((Rnd(i, 4) - 0.5) * 1.1);
            seeds[i] = (Math.Cos(a) * r, y, Math.Sin(a) * r);
        }

        return seeds;
    }

    private static bool InTriangle((double X, double Y) q, (double X, double Y)[] t)
    {
        static double Side((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        var d1 = Side(t[0], t[1], q);
        var d2 = Side(t[1], t[2], q);
        var d3 = Side(t[2], t[0], q);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    private static double Ease(double k) => 1 - Math.Pow(1 - Math.Clamp(k, 0, 1), 3);

    private static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    private static double Rnd(int a, int b) => (Hash(a, b) % 10000) / 10000.0;
}
