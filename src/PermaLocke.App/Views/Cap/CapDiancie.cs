using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The eleventh trial's Z-Crystal, the Feeristal, brought by Diancie, Mega-Evolved, with Diamond Storm (2026-10-19, second prototype: the player's
/// choice, their sprites of Diancie and Mega Diancie, and the move as Sun and Moon show it). Its own film, starting <see cref="Lead"/> seconds
/// before the film's clock:
/// <list type="number">
/// <item>Diancie floats down from the surface, a trail of pink twinkles behind it, and stops over the ruins;</item>
/// <item>it Mega-Evolves inside a sphere of crystal, as the Megas do when they are unlocked (<see cref="CapMega"/>): streaks of the symbol's
/// colours rush in, a sphere is born iridescent round it in a flash, pink crystal grows over it from below in facets, a web of cracks spreads over
/// the crystal in the symbol's colours and turns gold as it shakes and leaks light, and it bursts: shock waves, shards of pink crystal edged in
/// gold, twinkles; the Mega symbol shines over Mega Diancie as its colour comes back;</item>
/// <item>Diamond Storm, as in the games: a white-blue light gathers at its chest; the water turns to pale prism light with hexagonal plates of
/// crystal turning round it; it flashes white inside a pink and violet whirl with shards going round it; the water darkens to violet and a
/// spiralling torrent of pink diamonds and glitter pours out of it down onto the ruins, sparkling where it strikes;</item>
/// <item>the last, the biggest diamond, comes down the torrent with the crystal in it into an arch, and shatters there; Mega Diancie rises back
/// to the surface and fades out in twinkles.</item>
/// </list>
/// For the rest of the run the ruins round the arch stay turned to pink crystal (facets after each brick's own light, edges of light, a glint now
/// and then), as the torrent left them (<see cref="PaintRest"/>).
/// </summary>
public static class CapDiancie
{
    /// <summary>The eleventh trial's crystal.</summary>
    public const int Feeristal = 824;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 7.4;

    private const int Art = 719, Mega = 10719;

    /// <summary>The arch of the risen storey it lands in (the player's choice).</summary>
    private const int ArchIndex = 1;

    private const double Lying = 0.4, Size = 0.75, MegaSize = 0.62, Ball = 26;

    // the Mega Evolution, the storm, the end
    private const double Down = -7.2, Arrive = -4.6, Rush = -4.4, Born = -3.6, Crystal = -3.2, Crystallized = -2.2, Cracks = -2.4, Gold = -1.5,
        Burst = -0.7, Glow = 0.6, Prism = 1.0, Hexes = 1.2, Whirl = 2.2, Violet = 2.8, Torrent = 2.9, Last = 3.5, Hit = CapTrial.Hit, Rise = 5.8,
        Gone = 8.2;

    private const uint White = 0xFFFFFFu, Pink = 0xF488C8u, PinkHi = 0xFFD0E8u, PinkLo = 0xE060A8u, PinkDeep = 0xB03878u, PinkInk = 0x5A1838u,
        Twinkle = 0xFFE8F4u, GoldC = 0xFFD65Cu, GoldHi = 0xFFF6BEu, Ink = 0x14101Cu, HexEdge = 0x8AB4F8u, HexFill = 0xD8ECFFu, HexHi = 0xF4FAFFu,
        Night = 0x2A0E3Au, Chest = 0xC8E8FFu;

    /// <summary>The Mega symbol's colours.</summary>
    private static readonly uint[] Hue = [0xFFE45Au, 0xC8E650u, 0x6EDC84u, 0x48D2D0u, 0x4AA8F0u, 0x6A7CF0u, 0xA06AE8u];

    /// <summary>The iridescent sphere's bands.</summary>
    private static readonly uint[] Iris = [0xFFFFFFu, 0xFFC4F2u, 0xF488E4u, 0xE2FF9Au, 0x9CF4DCu, 0xC4B4FFu];

    /// <summary>The crystal's facets, shade to light.</summary>
    private static readonly uint[] Facet = [0xB03878u, 0xE060A8u, 0xF488C8u, 0xFFB8DCu, 0xFFE0F0u];

    /// <summary>The prism light the water turns to, in bands.</summary>
    private static readonly uint[] Prismatic = [0xFFD0E8u, 0xE0D0FFu, 0xC8E8FFu, 0xD0FFE8u, 0xFFF2C8u];

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>The facets' and cracks' seeds on the sphere: points spread over it (a Fibonacci sphere).</summary>
    private static readonly (double X, double Y, double Z)[] Seeds = Spread(26);

    private static (double X, double Y, double Z)[] Spread(int n)
    {
        var points = new (double X, double Y, double Z)[n];
        var golden = Math.PI * (3 - Math.Sqrt(5));
        for (var i = 0; i < n; i++)
        {
            var y = 1 - ((i + 0.5) / n * 2);
            var r = Math.Sqrt(1 - (y * y));
            var a = golden * i;
            points[i] = (Math.Cos(a) * r, y, Math.Sin(a) * r);
        }

        return points;
    }

    /// <summary>Where the crystal lies: on the floor of one of the risen storey's arches; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _)
            ? (x0 + CapGimmighoulRuins.Arches[ArchIndex] + (CapGimmighoulRuins.ArchW / 2),
               floor + CapGimmighoulRuins.Storey - CapGimmighoulRuins.Plinth - R(CapTrialCrystal.H * Lying))
            : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel != CapPanelKind.Deep || CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _) || Spot(l, ctx) is not { } spot) return;
        if (!CritterArt.All.TryGetValue(Art, out var art) || !CritterArt.All.TryGetValue(Mega, out var mega)) return;
        var clock = t + Lead;
        var stage = (X: x0 + 104.0, Y: floor - 90.0);

        // the water during the storm: prism light, then violet
        var prism = Span(Prism, Prism + 0.5, t) * (1 - Span(Violet, Violet + 0.4, t));
        if (prism > 0) PrismWater(p, l, prism, clock);
        var violet = Span(Violet, Violet + 0.4, t) * (1 - Span(Hit + 0.6, Hit + 1.6, t));
        if (violet > 0) Tint(p, l, Night, 150 * violet);

        if (t >= Hexes && t < Violet + 0.3) Plates(p, stage, t, clock);
        if (t >= Whirl && t < Violet + 0.6) Whirled(p, stage, t, clock);

        // Diancie floating down; inside the sphere it cannot be seen; Mega Diancie out of the burst, then rising away
        if (t >= Down && t < Born + 0.3) Floating(p, art, l, stage, t, clock);
        if (t >= Burst && t < Gone) Megaform(p, mega, l, stage, t, clock);

        if (t >= Rush && t < Born + 0.4) Streaks(p, stage, t, clock);
        if (t >= Born && t < Burst) Sphere(p, stage, t, clock);
        if (t >= Burst && t < Burst + 1.6) Bursting(p, stage, t - Burst);
        if (t >= Burst && t < Glow)
        {
            var keep = Span(Burst, Burst + 0.25, t) * (1 - Span(Glow - 0.4, Glow, t));
            CapMega.Symbol(p, R(stage.X), R(stage.Y - 54), 0.75, clock, keep, t < Burst + 0.12);
        }

        if (t >= Glow && t < Whirl) ChestLight(p, stage, t, clock);
        if (t >= Torrent && t < Hit + 0.4) TorrentOf(p, stage, spot, t, clock);
        Spikes(p, spot, floor, t);

        // the last and biggest, with the crystal, down the torrent into the arch
        if (t >= Last && t < Hit)
        {
            var f = Ease(Span(Last, Hit, t));
            var (gx, gy) = Along(stage, spot, f, clock, 0);
            Gem(p, gx, gy, 7, false);
            crystal.Paint(p, R(gx), R(gy), Lying, 0, 0.6, -1);
        }

        if (t >= Hit)
        {
            var since = t - Hit;
            Rest(p, spot, floor, crystal, since, clock);
            Shards(p, spot.X, spot.Y - 2, since);
            Starburst(p, spot.X, spot.Y - 4, since);
            if (since < 0.6) CapTrial.Shake(p, since, 3, 0.6);
        }
    }

    // ---------------------------------------------------------------- Diancie and Mega Diancie

    /// <summary>Diancie floating down from the surface with pink twinkles trailing behind it, rocking gently; white as the light rushes in.</summary>
    private static void Floating(CapSheet p, CritterArt art, CapWorldLayout l, (double X, double Y) stage, double t, double clock)
    {
        // from above the top of the window, down through the sky and the surface, slowing as it sinks
        var top = -40.0;
        var k = Ease(Span(Down, Arrive, t));
        double x = stage.X + (Math.Sin(k * Math.PI * 1.5) * 10), y = top + ((stage.Y - top) * k);
        if (t < Arrive + 0.5)
        {
            for (var n = 0; n < 12; n++)
            {
                var age = ((clock * 1.2) + (n / 12.0)) % 1;
                var kb = Ease(Span(Down, Arrive, t - (age * 1.2)));
                double bx = stage.X + (Math.Sin(kb * Math.PI * 1.5) * 10) + ((Rnd(n, 141) - 0.5) * 14), by = top + ((stage.Y - top) * kb);
                Star4(p, bx, by - (age * 6), age < 0.5 ? 2 : 1, age < 0.3 ? White : Twinkle);
            }
        }

        // where it goes through the surface: a crown of drops, a ring opening, bubbles after it
        var cross = Down + ((Arrive - Down) * Inverse(Clamp01((l.SurfaceY - top) / (stage.Y - top))));
        Splash(p, stage.X + (Math.Sin(Clamp01((l.SurfaceY - top) / (stage.Y - top)) * Math.PI * 1.5) * 10), l.SurfaceY, t - cross);

        var white = Span(Rush + 0.4, Born, t);
        CapVisitors.Centred(p, art, clock, R(x), R(y), Size, false, White, white, Math.Sin(clock * 1.3) * 5);
    }

    /// <summary>The share of the way (eased) at which the descent reaches a height: the inverse of <see cref="Ease"/>.</summary>
    private static double Inverse(double y)
    {
        double lo = 0, hi = 1;
        for (var i = 0; i < 20; i++)
        {
            var mid = (lo + hi) / 2;
            if (Ease(mid) < y) lo = mid;
            else hi = mid;
        }

        return (lo + hi) / 2;
    }

    /// <summary>Going through the surface: a crown of drops thrown up and falling, a ring opening on the water, bubbles trailing down.</summary>
    private static void Splash(CapSheet p, double x, int surface, double age)
    {
        if (age < 0 || age > 1.6) return;
        if (age < 0.9)
        {
            for (var n = 0; n < 14; n++)
            {
                var a = Math.PI * (1.08 + (n / 13.0 * 0.84));
                var v = 18 + (Rnd(n, 201) * 16);
                Put(p, x + (Math.Cos(a) * v * age), surface + (Math.Sin(a) * v * age) + (60 * age * age), n % 3 == 0 ? White : 0xCFF4FFu);
            }

            CapTrial.Ring(p, x, surface, 4 + (age * 30), 0.25, 0xE8FAFFu, 230 * (1 - (age / 0.9)), false);
        }

        for (var n = 0; n < 8; n++)
        {
            var b = age - (n * 0.08);
            if (b < 0) continue;
            Put(p, x + (Math.Sin((b * 7) + n) * 3), surface + 4 + (b * 40) - (b * b * 18), 0xCFF4FFu);
        }
    }

    /// <summary>Mega Diancie: out of the burst white, its colour coming back; white again in the whirl; rising away, fading in twinkles.</summary>
    private static void Megaform(CapSheet p, CritterArt mega, CapWorldLayout l, (double X, double Y) stage, double t, double clock)
    {
        double x = stage.X, y = stage.Y, keep = 1;
        if (t >= Rise)
        {
            var k = Ease(Span(Rise, Gone, t));
            y = stage.Y - (k * (stage.Y - l.SurfaceY + 30));
            keep = 1 - Span(Gone - 1.2, Gone, t);
            for (var n = 0; n < 8; n++) Star4(p, x + ((Rnd(n + (int)(clock * 4), 151) - 0.5) * 40), y + 30 + (Rnd(n, 153) * 20), 1, Twinkle);
        }

        var white = Math.Max(1 - Span(Burst, Burst + 0.7, t), Span(Whirl, Whirl + 0.2, t) * (1 - Span(Violet + 0.2, Violet + 0.6, t)));
        CapVisitors.Centred(p, mega, clock, R(x), R(y), MegaSize, false, White, white, 0, keep);
    }

    // ---------------------------------------------------------------- the Mega Evolution in a sphere of crystal

    /// <summary>Light closing in before the sphere: streaks of the symbol's colours with white heads, more and faster.</summary>
    private static void Streaks(CapSheet p, (double X, double Y) stage, double t, double clock)
    {
        var k = Span(Rush, Born, t);
        var count = 10 + (int)(k * 18);
        for (var n = 0; n < count; n++)
        {
            var run = ((clock * (1 + k)) + (n * 0.071)) % 1;
            var a = n * 0.229;
            for (var s = 0; s < 6; s++)
            {
                var r = (60 * (1 - run)) + (s * 1.5);
                Put(p, stage.X + (Math.Cos(a) * r), stage.Y + (Math.Sin(a) * r * 0.85), s == 0 ? White : Hue[n % Hue.Length]);
            }
        }

        if (t >= Born - 0.08 && t < Born + 0.1) CapTrial.Flash(p, 150);
    }

    /// <summary>
    /// The sphere: born in a flash, iridescent (bands swirling as it turns, lit from the upper left, rays round it), then pink crystal growing over
    /// it from below in facets (each facet its own tone after its own light, an edge of light along it), a web of cracks spreading over the crystal
    /// in the symbol's colours, turning gold and widening while it shakes and leaks light.
    /// </summary>
    private static void Sphere(CapSheet p, (double X, double Y) stage, double t, double clock)
    {
        var born = Clamp01((t - Born) / 0.3);
        var rs = Ball * (0.6 + (0.4 * born) + (born >= 1 ? 0.03 * Math.Sin(clock * 9) : 0));
        var cx = stage.X;
        if (t >= Gold) cx += (CapArt.Hash((int)(clock * 16), 5) % 3) - 1.0;
        var cy = stage.Y + 4;
        var cover = Span(Crystal, Crystallized, t);
        var gold = Span(Gold, Burst, t);
        var width = t < Cracks ? 0 : (0.02 * Clamp01((t - Cracks) / 0.3)) + (0.05 * gold);
        var spin = clock * 0.9;
        double co = Math.Cos(spin), si = Math.Sin(spin);

        // rays round it while it is still iridescent
        if (cover < 1)
        {
            for (var n = 0; n < 14; n++)
            {
                var a = (Rnd(n, 61) * Math.PI * 2) + (clock * 0.3);
                if (((clock * 3) + Rnd(n, 62)) % 1 > 0.6 - (cover * 0.5)) continue;
                var length = 8 + (Rnd(n, 63) * 14);
                for (var k = 0; k < length; k++)
                {
                    var d = rs + 2 + k;
                    int x = R(cx + (Math.Cos(a) * d)), y = R(cy + (Math.Sin(a) * d));
                    if (Bayer[((y & 3) * 4) + (x & 3)] >= (1 - (k / length)) * 14) continue;
                    p.Set(x, y, k < length * 0.3 ? White : Iris[1 + (n % 5)]);
                }
            }
        }

        var r = (int)Math.Ceiling(rs) + 1;
        for (var y = -r; y <= r; y++)
        {
            for (var x = -r; x <= r; x++)
            {
                double nx = (x + 0.5) / rs, ny = (y + 0.5) / rs, d2 = (nx * nx) + (ny * ny);
                int px = R(cx) + x, py = R(cy) + y;
                if (d2 > 1)
                {
                    if (d2 < Math.Pow(1 + (1.3 / rs), 2)) p.Set(px, py, Ink);
                    continue;
                }

                var nz = Math.Sqrt(1 - d2);
                double qx = (nx * co) + (nz * si), qy = ny, qz = (-nx * si) + (nz * co);
                var light = (-0.5 * nx) - (0.6 * ny) + (0.62 * nz);
                var spec = ((nx + 0.42) * (nx + 0.42)) + ((ny + 0.46) * (ny + 0.46)) < 0.018;
                var b = Bayer[((py & 3) * 4) + (px & 3)] / 16.0;

                // the crystal growing up from below
                var isCrystal = ((ny * 0.85) - (nx * 0.25) + 1) / 2 > 1 - (cover * 1.25) + ((b - 0.5) * 0.12);
                uint c;
                if (isCrystal)
                {
                    // the facet it is on: its nearest seed, lit by that facet's own turn, a line of light along its edges
                    var (near, edge) = FacetAt(qx, qy, qz);
                    var s = Seeds[near];
                    var turned = (-0.5 * ((s.X * co) - (s.Z * si))) - (0.6 * s.Y) + (0.62 * ((s.X * si) + (s.Z * co)));
                    var tone = Math.Clamp((int)Math.Floor((turned + 0.8) * 2.6), 0, Facet.Length - 1);
                    c = edge < 0.025 ? Facet[Math.Min(Facet.Length - 1, tone + 2)] : Facet[tone];
                    if (light < -0.2) c = Facet[Math.Max(0, tone - 1)];
                    if (spec) c = White;
                }
                else
                {
                    var v = Math.Sin((qy * 5) + (2.2 * Math.Sin((qx * 3) + (clock * 1.7))) + (clock * 2.3));
                    var w = Math.Sin((qx * 4) - (qz * 3) + (clock * 1.1));
                    var band = (int)Math.Floor((((v * 0.5) + 0.5) * 5) + (w * 0.6) + b);
                    c = Iris[((band % Iris.Length) + Iris.Length) % Iris.Length];
                    if (light < -0.15) c = CapSheet.Darker(c, 0.22);
                    if (d2 > Math.Pow(1 - (1.4 / rs), 2)) c = 0x2A1840u;
                    if (spec) c = White;
                }

                // the cracks, on the crystal
                if (isCrystal && width > 0 && Crack(qx, qy, qz, t, width) is { } crack)
                {
                    c = gold <= 0 ? Hue[crack.Pair % 7] : crack.Core ? (gold > 0.5 ? White : GoldHi) : gold > 0.3 ? GoldC : Hue[crack.Pair % 7];
                }

                p.Set(px, py, c);
            }
        }

        // light leaking out of the cracks
        if (gold > 0)
        {
            for (var n = 0; n < 22; n++)
            {
                var life = ((clock * 2.2) + Rnd(n, 71)) % 1;
                var a = Rnd(n, 72 + (int)((clock * 2.2) + Rnd(n, 71))) * Math.PI * 2;
                var d = rs * (0.8 + (life * 0.9));
                int x = R(cx + (Math.Cos(a) * d)), y = R(cy + (Math.Sin(a) * d));
                if (Bayer[((y & 3) * 4) + (x & 3)] < (1 - life) * 16) p.Set(x, y, life < 0.3 ? White : GoldC);
            }
        }
    }

    /// <summary>The facet a point of the sphere is on (its nearest seed), and how near it is to the edge with the next.</summary>
    private static (int Near, double Edge) FacetAt(double x, double y, double z)
    {
        int a = 0;
        double da = double.MaxValue, db = double.MaxValue;
        for (var n = 0; n < Seeds.Length; n++)
        {
            var d = 1 - ((Seeds[n].X * x) + (Seeds[n].Y * y) + (Seeds[n].Z * z));
            if (d < da)
            {
                db = da;
                da = d;
                a = n;
            }
            else if (d < db)
            {
                db = d;
            }
        }

        return (a, db - da);
    }

    /// <summary>The crack at a point of the sphere (turned with it), if there is one: the pair of facets it parts, and whether it is its core.</summary>
    private static (int Pair, bool Core)? Crack(double x, double y, double z, double t, double width)
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
        var from = 1 - ((-0.35 * x) + (0.45 * y) + (0.82 * z));
        if ((t - Cracks) * 3.2 < from + (Rnd(pair, 77) * 0.35)) return null;
        return (pair, gap < width * 0.45);
    }

    /// <summary>The burst: shock waves running out, shards of pink crystal edged in gold, twinkles of the symbol's colours.</summary>
    private static void Bursting(CapSheet p, (double X, double Y) stage, double age)
    {
        var cx = stage.X;
        var cy = stage.Y + 4;
        for (var w = 0; w < 2; w++)
        {
            var at = age - (w * 0.12);
            if (at is < 0 or > 0.8) continue;
            var r = Ball + (at * 120);
            var keep = 1 - (at / 0.8);
            var steps = (int)(r * 7);
            for (var k = 0; k < steps; k++)
            {
                var a = k / (double)steps * Math.PI * 2;
                for (var th = 0; th < 2; th++)
                {
                    int x = R(cx + (Math.Cos(a) * (r - th))), y = R(cy + (Math.Sin(a) * (r - th) * 0.8));
                    if (Bayer[((y & 3) * 4) + (x & 3)] >= keep * 16) continue;
                    p.Set(x, y, w == 0 ? White : Hue[(int)(a * 7 / (Math.PI * 2)) % 7]);
                }
            }
        }

        if (age < 0.08) CapTrial.Flash(p, 160);
        for (var n = 0; n < 30 && age < 1.4; n++) Shard(p, cx, cy, age, n + 40);
        for (var n = 0; n < 28; n++)
        {
            var a = (n / 28.0 * Math.PI * 2) + (Rnd(n, 81) * 0.3);
            var d = Ball + (age * 60 * (0.6 + (Rnd(n, 82) * 0.7)));
            var beat = ((age * 3) + Rnd(n, 83)) % 1;
            var size = age > 1.2 ? (beat < 0.3 ? 1 : 0) : beat < 0.15 ? 3 : beat < 0.45 ? 2 : 1;
            if (size > 0) Star4(p, cx + (Math.Cos(a) * d), cy + (Math.Sin(a) * d * 0.85), size, Hue[n % 7]);
        }
    }

    /// <summary>A shard of the crystal: a triangle of pink facets edged in gold or one of the symbol's colours, turning as it flies out and falls.</summary>
    private static void Shard(CapSheet p, double cx, double cy, double age, int n)
    {
        var ang = Rnd(n, 7) * Math.PI * 2;
        var v = 34 + (Rnd(n, 8) * 40);
        var x0 = cx + (Math.Cos(ang) * ((Ball * 0.6) + (v * age)));
        var y0 = cy + (Math.Sin(ang) * ((Ball * 0.6) + (v * age)) * 0.8) + (22 * age * age);
        var keep = 1 - (age / 1.4);
        var size = 2.5 + (Rnd(n, 9) * 3);
        var spin = (Rnd(n, 10) * 6) + (age * (4 + (Rnd(n, 11) * 6)));
        double ax = x0 + (Math.Cos(spin) * size), ay = y0 + (Math.Sin(spin) * size);
        double bx = x0 + (Math.Cos(spin + 2.1) * size), by = y0 + (Math.Sin(spin + 2.1) * size);
        double qx = x0 + (Math.Cos(spin + 4.2) * size), qy = y0 + (Math.Sin(spin + 4.2) * size);
        var edgeColour = n % 3 == 0 ? Hue[n % 7] : GoldC;
        var facet = Math.Sin(spin) > 0;
        for (var y = (int)Math.Floor(Math.Min(ay, Math.Min(by, qy))) - 1; y <= (int)Math.Ceiling(Math.Max(ay, Math.Max(by, qy))) + 1; y++)
        {
            for (var x = (int)Math.Floor(Math.Min(ax, Math.Min(bx, qx))) - 1; x <= (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, qx))) + 1; x++)
            {
                if (!Inside(x, y, ax, ay, bx, by, qx, qy) || (keep < 1 && Bayer[((y & 3) * 4) + (x & 3)] >= keep * 18)) continue;
                var edge = !Inside(x + 1, y, ax, ay, bx, by, qx, qy) || !Inside(x - 1, y, ax, ay, bx, by, qx, qy)
                           || !Inside(x, y + 1, ax, ay, bx, by, qx, qy) || !Inside(x, y - 1, ax, ay, bx, by, qx, qy);
                p.Set(x, y, edge ? edgeColour : facet ? PinkHi : PinkLo);
            }
        }
    }

    private static bool Inside(double x, double y, double ax, double ay, double bx, double by, double cx, double cy)
    {
        var d1 = ((x - bx) * (ay - by)) - ((ax - bx) * (y - by));
        var d2 = ((x - cx) * (by - cy)) - ((bx - cx) * (y - cy));
        var d3 = ((x - ax) * (cy - ay)) - ((cx - ax) * (y - ay));
        return !(((d1 < 0) || (d2 < 0) || (d3 < 0)) && ((d1 > 0) || (d2 > 0) || (d3 > 0)));
    }

    // ---------------------------------------------------------------- Diamond Storm

    /// <summary>The white-blue light gathering at its chest: a small orb, rays turning round it, growing.</summary>
    private static void ChestLight(CapSheet p, (double X, double Y) stage, double t, double clock)
    {
        var k = Span(Glow, Prism + 0.4, t) * (1 - Span(Whirl - 0.3, Whirl, t));
        if (k <= 0) return;
        double x = stage.X, y = stage.Y - 10;
        var r = 1 + (k * 4);
        for (var dy = -(int)r - 1; dy <= r + 1; dy++)
        {
            for (var dx = -(int)r - 1; dx <= r + 1; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d <= r) Put(p, x + dx, y + dy, d < r * 0.5 ? White : Chest);
            }
        }

        for (var n = 0; n < 8; n++)
        {
            var a = (n * Math.PI / 4) + (clock * 1.5);
            var len = (n % 2 == 0 ? 14 : 8) * k;
            for (var d = r + 1; d < r + len; d++) Put(p, x + (Math.Cos(a) * d), y + (Math.Sin(a) * d), d < r + (len / 2) ? White : Chest);
        }
    }

    /// <summary>The water turned to pale prism light: broad diagonal bands of pastel colours drifting across it, flat.</summary>
    private static void PrismWater(CapSheet p, CapWorldLayout l, double k, double clock)
    {
        var a = (int)(100 * k);
        for (var y = l.SurfaceY + 2; y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++)
            {
                var band = (int)Math.Floor((x + (y * 0.6) + (clock * 14)) / 16);
                p.Set(x, y, Prismatic[((band % Prismatic.Length) + Prismatic.Length) % Prismatic.Length], a);
                // beams of white light slanting across it, drifting
                var beam = (x - (y * 0.45) + (clock * 22)) % 46;
                if (beam > 0 && beam < 3) p.Set(x, y, White, a);
            }
        }
    }

    private static void Tint(CapSheet p, CapWorldLayout l, uint c, double alpha)
    {
        var a = (int)alpha;
        for (var y = l.SurfaceY + 2; y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, c, a);
        }
    }

    /// <summary>Hexagonal plates of crystal turning round it in a ring: each a pale hexagon edged in blue, lit along its top.</summary>
    private static void Plates(CapSheet p, (double X, double Y) stage, double t, double clock)
    {
        var come = Span(Hexes, Hexes + 0.5, t) * (1 - Span(Violet, Violet + 0.3, t));
        for (var n = 0; n < 9; n++)
        {
            var a = (n * Math.PI * 2 / 9) + (clock * 0.9);
            var r = 34 + (10 * (1 - come));
            double hx = stage.X + (Math.Cos(a) * r), hy = stage.Y + (Math.Sin(a) * r * 0.55);
            Hexagon(p, hx, hy, (5 + (n % 3) * 1.5) * come, clock + n);
        }
    }

    private static void Hexagon(CapSheet p, double cx, double cy, double r, double turn)
    {
        if (r < 1) return;
        for (var dy = -(int)r - 1; dy <= r + 1; dy++)
        {
            for (var dx = -(int)r - 1; dx <= r + 1; dx++)
            {
                // a hexagon by its distance along three axes
                double ax = Math.Abs(dx), ay = Math.Abs(dy);
                var d = Math.Max(ax * 0.866 + (ay * 0.5), ay);
                if (d > r) continue;
                // six facets round a flat middle, a line between each pair, lit from above; a band of light sliding across it
                var inner = d < r * 0.5;
                var sector = (int)Math.Floor((Math.Atan2(dy, dx) + Math.PI) / (Math.PI / 3)) % 6;
                var shine = Math.Abs(((dx + dy) * 0.5) - (((turn * 3) % 4 - 2) * r)) < 1;
                uint c = d > r - 1 ? HexEdge : inner ? HexHi : sector is 1 or 2 ? HexHi : sector is 4 or 5 ? 0xB8D4F8u : HexFill;
                if (!inner && d < r - 1 && Math.Abs(dx * 0.577) < 0.6 && dy != 0) c = HexEdge;
                if (shine && d < r - 1) c = White;
                Put(p, cx + dx, cy + dy, c);
            }
        }
    }

    /// <summary>The whirl round it: four arms of pink and violet spiralling, shards of crystal going round in them.</summary>
    private static void Whirled(CapSheet p, (double X, double Y) stage, double t, double clock)
    {
        var k = Span(Whirl, Whirl + 0.3, t) * (1 - Span(Violet + 0.2, Violet + 0.6, t));
        if (k <= 0) return;
        for (var arm = 0; arm < 4; arm++)
        {
            for (var r = 10.0; r < 50 * k; r += 0.5)
            {
                var a = (arm * Math.PI / 2) + (r * 0.11) - (clock * 5);
                double x = stage.X + (Math.Cos(a) * r), y = stage.Y + (Math.Sin(a) * r * 0.7);
                Put(p, x, y, r < 22 ? PinkHi : arm % 2 == 0 ? Pink : 0xA06AE8u);
                Put(p, x, y + 1, arm % 2 == 0 ? PinkLo : 0x6A3A9Cu);
            }
        }

        for (var n = 0; n < 10; n++)
        {
            var a = (n * 0.628) - (clock * 4);
            var r = 18 + ((n % 3) * 9);
            Gem(p, stage.X + (Math.Cos(a) * r), stage.Y + (Math.Sin(a) * r * 0.7), 2, false);
        }
    }

    /// <summary>A point along the torrent from it to the arch, <paramref name="u"/> of the way, spiralling round the line, the spiral narrowing.</summary>
    private static (double X, double Y) Along((double X, double Y) from, (int X, int Y) to, double u, double clock, double phase)
    {
        double dx = to.X - from.X, dy = to.Y - 2 - from.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double nx = -dy / len, ny = dx / len;
        var swirl = Math.Sin((u * Math.PI * 4) - (clock * 8) + phase) * 22 * (1 - (u * 0.8));
        return (from.X + (dx * u) + (nx * swirl), from.Y + (dy * u) + (ny * swirl));
    }

    /// <summary>The torrent: pink diamonds and glitter pouring out of it in a spiral down onto the ruins, sparkling where it strikes.</summary>
    private static void TorrentOf(CapSheet p, (double X, double Y) stage, (int X, int Y) spot, double t, double clock)
    {
        var fade = 1 - Span(Hit, Hit + 0.4, t);
        var count = (int)(72 * Span(Torrent, Torrent + 0.4, t) * fade);
        for (var n = 0; n < count; n++)
        {
            var u = ((clock * 0.9) + (n / 72.0)) % 1;
            // three strands of the spiral, each its own phase, a cell or two either side
            var (x, y) = Along(stage, spot, u, clock, (n % 3) * 2.094);
            x += (Rnd(n, 195) - 0.5) * 4;
            y += (Rnd(n, 197) - 0.5) * 4;
            if (n % 4 == 0)
            {
                // a diamond turning over as it flies, a streak behind it
                var (tx, ty) = Along(stage, spot, Math.Max(0, u - 0.04), clock, (n % 3) * 2.094);
                for (var st = 1; st < 4; st++) Put(p, x + ((tx - x) * st / 4), y + ((ty - y) * st / 4), st == 1 ? PinkHi : st == 2 ? Pink : PinkLo);
                SpinGem(p, x, y, 3 + (n % 2), (clock * 7) + n);
            }
            else if (n % 4 == 1) Star4(p, x, y, (int)(clock * 8 + n) % 3 == 0 ? 2 : 1, Twinkle);
            else
            {
                Put(p, x, y, n % 2 == 0 ? White : PinkHi);
                Put(p, x + 1, y + 1, Pink);
            }
            if (u > 0.92) Star4(p, spot.X + ((Rnd(n + (int)(clock * 6), 191) - 0.5) * 20), spot.Y - 2 - (Rnd(n, 193) * 10), 2, White);
        }
    }

    /// <summary>A diamond turning over as it flies: as wide as its turn shows it, its facets swapping as it goes past edge-on.</summary>
    private static void SpinGem(CapSheet p, double cx, double cy, double size, double spin)
    {
        var face = Math.Cos(spin);
        var w = Math.Max(0.5, size * Math.Abs(face));
        var h = size * 1.5;
        for (var dy = -(int)Math.Ceiling(h) - 1; dy <= h + 1; dy++)
        {
            for (var dx = -(int)Math.Ceiling(w) - 1; dx <= w + 1; dx++)
            {
                var d = (Math.Abs(dx) / w) + (Math.Abs(dy) / h);
                if (d > 1.15) continue;
                var left = (dx < 0) == (face > 0);
                uint c = d > 0.92 ? PinkInk : dx == 0 ? (dy < 0 ? White : PinkHi) : dy < 0 ? (left ? PinkHi : Pink) : left ? PinkLo : PinkDeep;
                Put(p, cx + dx, cy + dy, c);
            }
        }
    }

    /// <summary>Spikes of pink crystal shooting up out of the floor round where the torrent strikes, then shattering with the big diamond.</summary>
    private static void Spikes(CapSheet p, (int X, int Y) spot, int floor, double t)
    {
        var grow = Span(Last - 0.2, Last + 0.6, t);
        if (grow <= 0 || t >= Hit) return;
        for (var n = 0; n < 6; n++)
        {
            var side = n % 2 == 0 ? -1 : 1;
            var x = spot.X + (side * (12 + (n / 2 * 7)));
            var tall = (5 + ((n * 3) % 5)) * Clamp01((grow * 1.6) - (n * 0.1));
            var lean = side * 0.25;
            for (var h = 0; h < tall; h++)
            {
                var half = Math.Max(0, 2 - (h * 2.0 / tall));
                for (var dx = -(int)Math.Ceiling(half); dx <= half; dx++)
                {
                    var c = dx < 0 ? PinkHi : dx > 0 ? PinkLo : h > tall - 2 ? White : Pink;
                    Put(p, x + dx + (h * lean), floor + CapGimmighoulRuins.Storey - CapGimmighoulRuins.Plinth - h, c);
                }
            }
        }
    }

    /// <summary>
    /// A pink diamond, taller than wide: its four facets lit from the upper left, a line of light down its middle, an ink edge. <paramref name="set"/>
    /// draws only its upper part, the rest sunk in the stone.
    /// </summary>
    private static void Gem(CapSheet p, double cx, double cy, double size, bool set)
    {
        var w = size;
        var h = size * 1.5;
        for (var dy = -(int)Math.Ceiling(h) - 1; dy <= h + 1; dy++)
        {
            if (set && dy > h * 0.3) break;
            for (var dx = -(int)Math.Ceiling(w) - 1; dx <= w + 1; dx++)
            {
                var d = (Math.Abs(dx) / Math.Max(0.5, w)) + (Math.Abs(dy) / Math.Max(0.5, h));
                if (d > 1.15) continue;
                uint c;
                if (d > 0.92) c = PinkInk;
                else if (dx == 0) c = dy < 0 ? White : PinkHi;
                else c = dy < 0 ? (dx < 0 ? PinkHi : Pink) : dx < 0 ? PinkLo : PinkDeep;
                Put(p, cx + dx, cy + dy, c);
            }
        }
    }

    /// <summary>A four-pointed twinkle.</summary>
    private static void Star4(CapSheet p, double x, double y, int size, uint c)
    {
        Put(p, x, y, White);
        for (var d = 1; d <= size; d++)
        {
            Put(p, x - d, y, c);
            Put(p, x + d, y, c);
            Put(p, x, y - d, c);
            Put(p, x, y + d, c);
        }
    }

    /// <summary>The big diamond shattering in the arch: pink shards flying out, turning, falling.</summary>
    private static void Shards(CapSheet p, double x, double y, double age)
    {
        if (age < 0 || age > 0.9) return;
        for (var n = 0; n < 22; n++)
        {
            var a = Rnd(n, 181) * Math.PI * 2;
            var v = 20 + (Rnd(n, 183) * 30);
            double sx = x + (Math.Cos(a) * v * age), sy = y + (Math.Sin(a) * v * age) + (50 * age * age);
            Put(p, sx, sy, n % 3 == 0 ? White : n % 3 == 1 ? PinkHi : Pink);
            Put(p, sx + ((n % 2) * 2) - 1, sy + 1, PinkLo);
        }

        if (age < 0.08) CapTrial.Flash(p, 70);
    }

    private static double Ease(double k) => k * k * (3 - (2 * k));

    /// <summary>A burst of solid rays out of the arch as the big diamond shatters: white heart, pink, then pale, every other ray shorter.</summary>
    private static void Starburst(CapSheet p, double x, double y, double age)
    {
        if (age < 0 || age > 0.5) return;
        var k = age / 0.5;
        for (var n = 0; n < 12; n++)
        {
            var a = (n * Math.PI / 6) + 0.26;
            var len = (n % 2 == 0 ? 24 : 14) * Math.Min(1, age * 7) * (1 - (k * 0.5));
            for (var d = 2; d < len; d++)
            {
                if (k > 0.5 && d < len * (k - 0.5) * 2) continue;
                Put(p, x + (Math.Cos(a) * d * 1.2), y + (Math.Sin(a) * d), d < len * 0.3 ? White : d < len * 0.7 ? PinkHi : Pink);
            }
        }
    }

    /// <summary>
    /// The crystal in its arch, and the ruins round it turned to pink crystal: inside a ragged oval spreading from the arch, every cell of stone
    /// takes a facet of a diagonal lattice, its tone from the stone's own light, with edges of light between facets; while it spreads its front
    /// glows white; a glint flashes on one facet or another now and then.
    /// </summary>
    private static void Rest(CapSheet p, (int X, int Y) spot, int floor, CapTrialCrystal crystal, double since, double clock)
    {
        var k = Ease(Clamp01(since / 1.6));
        int cx = spot.X, cy = floor + 8;
        const double rx = 30, ry = 24;
        // only the ruins: nothing under the foot of the risen storey (the sand)
        var bottom = floor + CapGimmighoulRuins.Storey - 1;
        for (var dy = -(int)ry; dy <= ry; dy++)
        {
            for (var dx = -(int)rx; dx <= rx; dx++)
            {
                var d = Math.Sqrt(((dx / rx) * (dx / rx)) + ((dy / ry) * (dy / ry))) + ((Rnd((cx + dx) / 3, (cy + dy) / 3) - 0.5) * 0.5) + ((Rnd(cx + dx, cy + dy) - 0.5) * 0.12);
                if (d > k) continue;
                int x = cx + dx, y = cy + dy;
                // the Gimmighoul chest stands just right of the arch, on the floor over it: it keeps its own colours
                if (y > bottom || (y < floor && x >= spot.X + 8) || !p.Solid(x, y)) continue;
                var c = p.At(x, y);
                int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), b = (int)(c & 255);
                var stone = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 50 || (g > r + 20 && g > b + 12);
                if (!stone) continue;
                if (k < 1 && d > k - 0.06)
                {
                    p.Set(x, y, White);
                    continue;
                }

                // the facet: a cell of a diagonal lattice, its own tone (lit from the upper left more often than not); the stone under it shows
                // through only as darker lines where its mortar and outlines were; light along one way of the lattice, shade along the other
                var lum = ((r * 3) + (g * 6) + b) / 2550.0;
                int fu = (int)Math.Floor((x + y) / 7.0), fv = (int)Math.Floor((x - y) / 7.0);
                var tone = 1 + (int)(CapArt.Hash(fu * 7, fv + 300) % 3);
                if (lum < 0.3) tone--;
                if (((x + y) % 7 + 7) % 7 == 0) tone++;
                else if (((x - y) % 7 + 7) % 7 == 0) tone--;
                p.Set(x, y, Facet[Math.Clamp(tone, 0, Facet.Length - 1)]);
            }
        }

        // a glint on a facet now and then
        var g2 = (int)(clock * 1.7);
        if ((clock * 1.7) % 1 < 0.35 && k >= 1)
        {
            double gx = cx + ((Rnd(g2, 211) - 0.5) * rx * 1.4), gy = cy + ((Rnd(g2, 213) - 0.5) * ry * 1.2);
            if (p.Solid(R(gx), R(gy))) Star4(p, gx, gy, 2, White);
        }

        var glint = clock % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>The Feeristal in its arch for the rest of the run, with the diamonds round it.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (_, floor, _) || Spot(l, ctx) is not { } spot) return;
        Rest(p, spot, floor, crystal, 30, phase);
    }
}
