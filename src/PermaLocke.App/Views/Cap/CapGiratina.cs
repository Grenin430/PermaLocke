using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The eighth trial's Z-Crystal, the Spectrostal, brought by Giratina from the Distortion World (2026-10-19, second prototype: the player's choice,
/// their references for the portal and its inside, the Origin Forme sprite they sent, flying). Its own film, starting <see cref="Lead"/> seconds
/// before the film's clock:
/// <list type="number">
/// <item>the abyss's water darkens and a round vortex winds up in it, like the one over Mount Coronet: hard rings from black through purple and
/// violet to lilac, twisted into arms, with streaks of pale cloud curling round its outer edge; loose pebbles, bubbles and bits of weed are
/// pulled in spiralling;</item>
/// <item>its eye tears open onto the Distortion World of the player's pictures: the deep blue void with darker clouds in it, slabs of rose stone
/// paving with dark star marks and boulders on top and dark blue sides lit by a bright blue edge, one hanging upside down, fern-like trees and
/// dark puffball trees on them, a pale waterfall pouring off one into the void, dark slabs far off, and Giratina's own shadow, black with red
/// stripes, passing behind; everything drifts at its own depth;</item>
/// <item>Giratina comes out of it flying, from far away, growing as it nears, with the crystal before it, and roars;</item>
/// <item>Shadow Force: it sinks into its shadow and is gone, and comes out of it again over the ruins' risen storey with a burst of shadow;</item>
/// <item>it lets the crystal fall into an arch, ghostly flames going round it as it falls; then it flies back into the portal, which winds
/// shut and collapses with a last pull.</item>
/// </list>
/// The arch it fell into stays a little window onto the Distortion World, with bricks floating over it, for the rest of the run
/// (<see cref="PaintRest"/>).
/// </summary>
public static class CapGiratina
{
    /// <summary>The eighth trial's crystal.</summary>
    public const int Spectrostal = 820;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 7.0;

    private const int Art = 10487;

    /// <summary>The arch of the risen storey it falls into (the player's choice).</summary>
    private const int ArchIndex = 3;

    private const double Lying = 0.4, Big = 0.95, Far = 0.14;

    // the portal: the eye's radius in cells (round: the player asked for a circle), and how far past it the vortex reaches
    private const double Eye = 30, Vortex = 27;

    private const double Wind = -6.8, Wound = -4.4, Tear = -4.6, Torn = -2.6, Shade = -3.6, Shaded = -0.3, Out = 0.0, Near = 2.2, Roar = 2.0,
        Sink = 2.5, Sunk = 3.0, Rise = 3.2, Risen = 3.6, Drop = 3.8, Hit = CapTrial.Hit, Return = 5.6, Home = 7.4, Shut = 7.6, Shutting = 8.8,
        Unwind = 8.4, Gone = 10.0;

    private const uint Black = 0x08040Eu, Dusk = 0x1A0A2Au, Shadow = 0x0C0A2Eu, ShadeHi = 0x24204Eu, ShadeRim = 0x4A2C8Cu, Red = 0xD8203Cu, RedHi = 0xFF5068u, Ghost = 0x9A48B4u,
        GhostHi = 0xE4B8EEu;

    /// <summary>The vortex's rings, inside out: black at the eye, through deep purple and violet to lilac.</summary>
    private static readonly uint[] Rings = [0x08040Eu, 0x24103Au, 0x461A62u, 0x6E2A8Au, 0x9A48B4u, 0xC478D4u];

    /// <summary>The pale cloud streaked round its outer edge: lit, plain and the shadow under each streak.</summary>
    private const uint CloudHi = 0xF6E2FAu, Cloud = 0xE4B8EEu, CloudLo = 0xA88CC8u;

    // the Distortion World, after the player's pictures
    private const uint Void = 0x2A2ACCu, VoidDeep = 0x1E1EA6u, VoidHi = 0x3C46E0u, FarSlab = 0x1C1C8Eu, FarSlabHi = 0x24249Eu,
        Pave = 0xC87A6Eu, PaveDark = 0xB06458u, PaveHi = 0xE8A898u, PaveLit = 0xD8948Au, Star = 0x6A3446u, Side = 0x14124Eu, SideHi = 0x1C1A66u,
        Edge = 0x3A5CF0u, EdgeDim = 0x2C44C8u, Tip = 0x0C0A36u, Fern = 0x24604Au, FernHi = 0x3E8C68u, FernLo = 0x163C30u, Puff = 0x4A3A4Cu,
        PuffHi = 0x6A5A6Au, PuffLo = 0x2E2432u, Trunk = 0x2A2030u, Boulder = 0x8C7C8Au, BoulderHi = 0xB0A0AEu, BoulderLo = 0x5A4A58u,
        Fall0 = 0xD8ECFFu, Fall1 = 0xA0CCF4u, Fall2 = 0x6EA0E8u;

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>The two layers of the other side, drawn once: the near one with its slabs and trees, the far one with dark slabs.</summary>
    private static readonly Lazy<(CapSheet Near, CapSheet Far)> Inside = new(Build);

    private const int WorldW = 112, WorldH = 66;

    /// <summary>Where the waterfall pours off the big slab, on the near layer.</summary>
    private const int FallX = 40, FallY = 42;

    /// <summary>Where the crystal lies: on the floor of one of the risen storey's arches; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _)
            ? (x0 + CapGimmighoulRuins.Arches[ArchIndex] + (CapGimmighoulRuins.ArchW / 2),
               floor + CapGimmighoulRuins.Storey - CapGimmighoulRuins.Plinth - R(CapTrialCrystal.H * Lying))
            : null;

    /// <summary>The portal's middle: open water over the ruins, left of the game.</summary>
    private static (int X, int Y) Middle(CapWorldLayout l, int x0, int floor) => (x0 + 96, (l.SurfaceY + floor - 44) / 2);

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel != CapPanelKind.Deep || CapGimmighoulRuins.Anchor(l, ctx) is not var (x0, floor, _) || Spot(l, ctx) is not { } spot) return;
        var mid = Middle(l, x0, floor);
        var clock = t + Lead;

        // the water darkening while the portal is there
        var dusk = Span(Wind, Wound, t) * (1 - Span(Unwind, Gone, t));
        if (dusk > 0) Darken(p, l, dusk);

        // the vortex winding up and down, the eye tearing open and shutting
        var swirl = Ease(Span(Wind, Wound, t)) * (1 - Ease(Span(Unwind, Gone - 0.3, t)));
        var open = Ease(Span(Tear, Torn, t)) * (1 - Ease(Span(Shut, Shutting, t)));
        CritterArt.All.TryGetValue(Art, out var art);
        if (swirl > 0)
        {
            Pulled(p, mid, t, swirl);
            Portal(p, mid, swirl, open, clock, t, art);
        }

        // the last pull as it collapses: a ring rushing in and a flash of its lilac
        if (t >= Gone - 0.6 && t < Gone + 0.3)
        {
            var k = Span(Gone - 0.6, Gone, t);
            CapTrial.Ring(p, mid.X, mid.Y, (Eye + Vortex) * (1 - k), 1, Rings[5], 220 * (1 - Span(Gone, Gone + 0.3, t)), false);
            if (t >= Gone && t < Gone + 0.08) CapTrial.Flash(p, 90);
        }

        if (CritterArt.All.TryGetValue(Art, out var giratina)) Fly(p, giratina, crystal, mid, spot, floor, t, clock);

        if (t >= Hit)
        {
            var since = t - Hit;
            if (since < 0.7)
            {
                // the landing: shadow puffing out of the arch and wisps of ghost fire, the window jolting
                for (var n = 0; n < 22; n++)
                {
                    var life = 0.35 + (Rnd(n, 81) * 0.3);
                    if (since > life) continue;
                    var a = Math.PI * (1.05 + (Rnd(n, 83) * 0.9));
                    var v = 14 + (Rnd(n, 85) * 24);
                    var c = n % 4 == 0 ? GhostHi : n % 2 == 0 ? Ghost : Shadow;
                    Blob(p, spot.X + (Math.Cos(a) * v * since), spot.Y + 2 + (Math.Sin(a) * v * since * 0.6) - (since * 8), c, n % 3 == 0 ? 1 : 0);
                }

                CapTrial.Shake(p, since, 2.5, 0.5);
            }

            Rest(p, x0, floor, spot, crystal, t, since);
        }
    }

    /// <summary>
    /// Giratina: out of the portal flying, growing as it nears; Shadow Force (gone into its shadow, out of it again over the ruins); the crystal
    /// dropped; back into the portal.
    /// </summary>
    private static void Fly(CapSheet p, CritterArt giratina, CapTrialCrystal crystal, (int X, int Y) mid, (int X, int Y) spot, int floor, double t,
        double clock)
    {
        if (t < Out || t >= Home) return;
        var front = (X: mid.X - 8.0, Y: mid.Y + 16.0);
        var hover = (X: spot.X + 8.0, Y: floor - 70.0);
        double gx, gy, scale, keep = 1, blue = 0, tilt = 0;
        if (t < Sink)
        {
            // out of the eye from far off, coming at us
            var k = Ease(Span(Out, Near, t));
            gx = mid.X + ((front.X - mid.X) * k);
            gy = mid.Y + ((front.Y - mid.Y) * k);
            scale = Far + ((Big - Far) * k);
            blue = 0.7 * (1 - Clamp01(k / 0.5));
            tilt = -10 * (1 - k);
        }
        else if (t < Rise)
        {
            // Shadow Force: it sinks into its shadow where it is
            gx = front.X;
            gy = front.Y + (Span(Sink, Sunk, t) * 6);
            scale = Big;
            keep = 1 - Span(Sink, Sunk, t);
            ShadowPool(p, R(front.X), R(front.Y + 26), Span(Sink, Sunk, t), clock);
        }
        else if (t < Return)
        {
            // and comes out of it over the ruins
            gx = hover.X;
            gy = hover.Y + ((1 - Span(Rise, Risen, t)) * 8);
            scale = Big;
            keep = Span(Rise, Risen, t);
            if (t < Risen + 0.4) ShadowPool(p, R(hover.X), R(hover.Y + 26), 1 - Span(Risen, Risen + 0.4, t), clock);
            if (t >= Rise && t < Rise + 0.5) Burst(p, hover.X, hover.Y + 10, (t - Rise) / 0.5);
        }
        else
        {
            // back into the portal, getting smaller, lost in the blue
            var k = Ease(Span(Return, Home, t));
            gx = hover.X + ((mid.X - hover.X) * k);
            gy = hover.Y + ((mid.Y - hover.Y) * k);
            scale = Big + ((Far - Big) * k);
            blue = 0.7 * Clamp01((k - 0.5) / 0.5);
            tilt = 10 * k;
        }

        if (keep > 0) CapVisitors.Centred(p, giratina, clock, R(gx), R(gy), scale, false, Void, blue, tilt, keep);

        // the roar: rings of shadow off it, the window shaking
        if (t >= Roar && t < Roar + 0.8)
        {
            var r = t - Roar;
            for (var c = 0; c < 3; c++)
            {
                var age = r - (c * 0.16);
                if (age > 0 && age < 0.5) CapTrial.Ring(p, gx + 6, gy - 6, 6 + (age * 60), 0.8, c == 1 ? Ghost : Rings[2], 230, false);
            }

            CapTrial.Shake(p, r, 2.5, 0.6);
        }

        // the crystal before it, with it into the shadow and out again, then falling with ghost fire round it
        if (t < Drop)
        {
            if (keep > 0) crystal.Paint(p, R(gx - (6 * scale)), R(gy + (22 * scale)), Lying * Math.Max(0.35, scale / Big), 0, 0.6, -1, int.MaxValue, (int)(255 * keep));
        }
        else if (t < Hit)
        {
            var f = Span(Drop, Hit, t);
            var from = (X: hover.X - (6 * Big), Y: hover.Y + (22 * Big));
            double cx = from.X + ((spot.X - from.X) * f), cy = from.Y + ((spot.Y - from.Y) * f * f);
            Flames(p, cx, cy + 3, clock);
            crystal.Paint(p, R(cx), R(cy), Lying, 0, 0.8, -1);
        }
    }

    /// <summary>Three wisps of ghost fire going round a point: each a little flame, pale at its heart, flickering, with a trail.</summary>
    private static void Flames(CapSheet p, double x, double y, double clock)
    {
        for (var k = 0; k < 3; k++)
        {
            var a = (clock * 4) + (k * 2.094);
            double fx = x + (Math.Cos(a) * 7), fy = y + (Math.Sin(a) * 3);
            var tall = 2 + (int)((clock * 9) + k) % 2;
            for (var h = 0; h <= tall; h++)
            {
                p.Set(R(fx), R(fy) - h, h == 0 ? GhostHi : h == tall ? Rings[3] : Ghost);
                if (h < tall - 1) p.Set(R(fx) + 1, R(fy) - h, Ghost);
            }

            p.Set(R(fx - (Math.Cos(a + 1.57) * 2)), R(fy - (Math.Sin(a + 1.57))), Rings[3]);
        }
    }

    /// <summary>The pool of shadow it sinks into and comes out of: a flat black oval with a purple rim, wisps rising off it.</summary>
    private static void ShadowPool(CapSheet p, int x, int y, double k, double clock)
    {
        if (k <= 0) return;
        var rx = 4 + (18 * k);
        var ry = 1.5 + (4 * k);
        for (var dy = -(int)ry; dy <= ry; dy++)
        {
            for (var dx = -(int)rx; dx <= rx; dx++)
            {
                var d = ((dx / rx) * (dx / rx)) + ((dy / ry) * (dy / ry));
                if (d > 1) continue;
                p.Set(x + dx, y + dy, d > 0.7 ? Rings[2] : Black);
            }
        }

        for (var n = 0; n < 6; n++)
        {
            var age = ((clock * 0.9) + (n / 6.0)) % 1;
            Blob(p, x + ((Rnd(n, 91) - 0.5) * rx * 1.6), y - (age * 14 * k), age < 0.5 ? Shadow : Rings[2], 1);
        }
    }

    /// <summary>The burst of shadow it comes out of: dark flames flung up and out round it.</summary>
    private static void Burst(CapSheet p, double x, double y, double k)
    {
        for (var n = 0; n < 26; n++)
        {
            var a = -Math.PI * (0.05 + (Rnd(n, 95) * 0.9));
            var v = 18 + (Rnd(n, 97) * 26);
            Blob(p, x + (Math.Cos(a) * v * k), y + (Math.Sin(a) * v * k * 0.8), n % 3 == 0 ? Rings[3] : Shadow, k < 0.6 ? 1 : 0);
        }
    }

    /// <summary>A cell, or a little plus of cells.</summary>
    private static void Blob(CapSheet p, double x, double y, uint c, int size)
    {
        int cx = R(x), cy = R(y);
        p.Set(cx, cy, c);
        if (size < 1) return;
        p.Set(cx + 1, cy, c);
        p.Set(cx - 1, cy, c);
        p.Set(cx, cy + 1, c);
        p.Set(cx, cy - 1, c);
    }

    /// <summary>The whole abyss's water darkened towards a purple dusk, as flat as the rest of the art.</summary>
    private static void Darken(CapSheet p, CapWorldLayout l, double k)
    {
        var a = (int)(120 * k);
        for (var y = l.SurfaceY + 2; y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, Dusk, a);
        }
    }

    /// <summary>Loose things pulled into the vortex while it winds: pebbles, bubbles and bits of weed spiralling in from the ruins and the water.</summary>
    private static void Pulled(CapSheet p, (int X, int Y) mid, double t, double swirl)
    {
        for (var n = 0; n < 16; n++)
        {
            var start = Wind + 0.5 + (Rnd(n, 101) * 6.0);
            var life = 2.2 + (Rnd(n, 103) * 1.2);
            var age = (t - start) / life;
            if (age < 0 || age > 1 || swirl < 0.3) continue;
            var a0 = Rnd(n, 105) * Math.PI * 2;
            var r = (Eye + Vortex + 16) * (1 - (age * age));
            var a = a0 + (age * age * 5);
            double x = mid.X + (Math.Cos(a) * r), y = mid.Y + (Math.Sin(a) * r);
            var c = n % 3 == 0 ? 0x8C8C7Cu : n % 3 == 1 ? 0xCFF4FFu : 0x3E7A4Au;
            p.Set(R(x), R(y), c);
            if (n % 3 != 1 && age < 0.7) p.Set(R(x) + 1, R(y), n % 3 == 0 ? 0x5C5C52u : 0x2A5A36u);
        }
    }

    /// <summary>
    /// The portal: hard rings from black at the eye to lilac, twisted into arms that turn in, pale cloud curling in streaks round its outer edge;
    /// once torn open, the Distortion World inside the eye behind a ragged black edge. <paramref name="swirl"/> grows it all, <paramref name="open"/>
    /// the eye.
    /// </summary>
    private static void Portal(CapSheet p, (int X, int Y) mid, double swirl, double open, double clock, double t, CritterArt? art)
    {
        var eye = Eye * open;
        var outer = (Eye + Vortex) * swirl;
        // the streaks reach a little past the vortex and taper to nothing there: room for all of them
        var reach = (int)Math.Ceiling(outer * 1.3) + 4;
        var turn = clock * 0.55;
        var shade = art is not null && t >= Shade && t < Shaded && Shadowed(art, Span(Shade, Shaded, t), clock);
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > reach) continue;
                var a = Math.Atan2(dy, dx);
                int x = mid.X + dx, y = mid.Y + dy;

                // the eye: its edge ragged by a couple of cells, black
                var ragged = eye - (CapArt.Hash((int)((a + Math.PI) * 9), 7) % 3 == 0 ? 1 : 0);
                if (r < ragged - 1.5 && open > 0.05)
                {
                    p.Set(x, y, World(dx, dy, clock, shade));
                    continue;
                }

                if (r < ragged + 1 && open > 0.05)
                {
                    p.Set(x, y, Black);
                    continue;
                }

                // through the vortex: 0 at the eye, 1 at its outer edge, bent into arms turning in
                var w = (r - Math.Max(eye, 2)) / Math.Max(1, outer - Math.Max(eye, 2));
                var arm = Math.Sin((4 * (a - turn)) + (r * 0.16)) * 0.09;
                var bent = w + arm;

                // the cloud round its edge: streaks along the spiral, lit on their inner side, a shadow under them
                if (bent > 0.78)
                {
                    var streak = Math.Sin((7 * (a - (turn * 1.3))) + (r * 0.32));
                    // each streak thins out to a point past the vortex's edge instead of stopping
                    var thin = bent > 0.92 ? -0.1 + ((bent - 0.92) * 4.2) : -0.1;
                    if (streak < thin || thin > 0.98) continue;
                    var lit = Math.Sin((7 * (a - (turn * 1.3))) + ((r - 1) * 0.32));
                    p.Set(x, y, streak > 0.75 ? CloudHi : lit < streak - 0.05 ? CloudLo : Cloud);
                    continue;
                }

                if (bent < 0) bent = 0;
                p.Set(x, y, Rings[Math.Min(Rings.Length - 1, (int)(bent / 0.78 * Rings.Length))]);
            }
        }
    }

    /// <summary>What one cell of the eye shows: the near layer, the far one behind it, Giratina's shadow passing, and the void.</summary>
    private static uint World(int dx, int dy, double clock, bool shade)
    {
        var (near, far) = Inside.Value;
        var ny = dy + (WorldH / 2);
        var nx = Wrap(dx + (int)(clock * 1.6), WorldW);
        if (ny >= 0 && ny < WorldH)
        {
            // the waterfall off the big slab, its stripes running down
            if (nx >= FallX && nx < FallX + 4 && ny >= FallY && !near.Solid(nx, ny))
            {
                var stripe = (((ny - (int)(clock * 12)) % 5) + 5) % 5;
                return nx == FallX || nx == FallX + 3 ? Fall2 : stripe == 0 ? Fall0 : stripe < 3 ? Fall1 : Fall2;
            }

            if (near.Solid(nx, ny)) return near.At(nx, ny);
        }

        // Giratina's shadow passing behind before it comes out
        if (shade && _shade!.Solid(dx + ShadeHalf, dy + ShadeHalf)) return _shade.At(dx + ShadeHalf, dy + ShadeHalf);

        var fy = dy + (WorldH / 2);
        var fx = Wrap(dx + (int)(clock * 0.6), WorldW);
        if (fy >= 0 && fy < WorldH && far.Solid(fx, fy)) return far.At(fx, fy);
        return Back(dx, dy, clock);
    }

    /// <summary>The deep blue of the void with darker clouds drifting in it and lighter wisps, all in solid patches.</summary>
    private static uint Back(int dx, int dy, double clock)
    {
        var x = dx + (clock * 0.3);
        var n = Math.Sin((x * 0.21) + (dy * 0.13)) + Math.Sin((dy * 0.27) - (x * 0.09) + 1.7);
        return n > 1.15 ? VoidDeep : n < -1.35 ? VoidHi : Void;
    }

    /// <summary>The sheet Giratina's shadow is drawn into each frame before the eye shows it (one per painting thread).</summary>
    [ThreadStatic]
    private static CapSheet? _shade;

    private const int ShadeHalf = 40;

    /// <summary>
    /// Giratina's shadow crossing the other side far behind, before it comes out: its own flying sprite, smaller, turned to the right, every cell
    /// of it made the black of the Distortion World's shadow in three depths after its own light (so its tentacles, crest and spikes still
    /// read), its red stripes and tips kept glowing. False when none of it is in sight.
    /// </summary>
    private static bool Shadowed(CritterArt art, double k, double clock)
    {
        _shade ??= new CapSheet(ShadeHalf * 2, ShadeHalf * 2);
        _shade.Clear();
        var x = -70 + (k * 140);
        var y = -9 + (Math.Sin((k * 5) + 0.6) * 4);
        CapVisitors.Centred(_shade, art, clock, ShadeHalf + R(x), ShadeHalf + R(y), 0.72, true, 0xFFFFFF, 0, 6 * Math.Sin(k * 5));
        var any = false;
        for (var sy = 0; sy < ShadeHalf * 2; sy++)
        {
            for (var sx = 0; sx < ShadeHalf * 2; sx++)
            {
                if (!_shade.Solid(sx, sy)) continue;
                any = true;
                var c = _shade.At(sx, sy);
                int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), bl = (int)(c & 255);
                var lum = ((r * 3) + (g * 6) + bl) / 2550.0;
                var red = r > 120 && g < 100 && bl < 120;
                _shade.Set(sx, sy, red ? (r > 200 ? RedHi : Red) : lum > 0.62 ? ShadeHi : lum > 0.3 ? Shadow : Black);
            }
        }

        // a rim of the void's own violet round it, so the black shape stands out of the dark blue
        for (var sy = 0; sy < ShadeHalf * 2; sy++)
        {
            for (var sx = 0; sx < ShadeHalf * 2; sx++)
            {
                if (_shade.Solid(sx, sy)) continue;
                if (Body(sx + 1, sy) || Body(sx - 1, sy) || Body(sx, sy + 1) || Body(sx, sy - 1)) _shade.Set(sx, sy, ShadeRim);
            }
        }

        return any;

        static bool Body(int x, int y) => _shade!.Solid(x, y) && _shade.At(x, y) != ShadeRim;
    }

    private static int Wrap(int x, int span) => ((x % span) + span) % span;

    private static double Ease(double k) => k * k * (3 - (2 * k));

    /// <summary>Draws the other side's two layers once.</summary>
    private static (CapSheet Near, CapSheet Far) Build()
    {
        var near = new CapSheet(WorldW, WorldH);
        var far = new CapSheet(WorldW, WorldH);

        // far off: dark slabs and the outlines of their trees
        FarSlabAt(far, 2, 12, 14, 3);
        FarSlabAt(far, 30, 26, 12, 2);
        FarSlabAt(far, 50, 8, 16, 3);
        FarSlabAt(far, 74, 30, 12, 2);
        FarSlabAt(far, 92, 10, 14, 3);
        FarSlabAt(far, 16, 54, 12, 2);
        FarSlabAt(far, 66, 56, 14, 2);

        // near: the big slab with the waterfall, one hanging upside down over it, and three more, close enough that the eye always holds a few
        Slab(near, 2, 36, 38, 10, 10, false, 1);
        Fernt(near, 8, 39, 16, 1);
        PuffTree(near, 18, 40, 8, 3);
        Fernt(near, 30, 38, 13, 2);
        Rock(near, 24, 44);
        Rock(near, 12, 45);
        Rock(near, 34, 43);

        Slab(near, 42, 1, 30, 8, 8, true, 2);
        PuffTree(near, 48, 17, -6, 4);
        Fernt(near, 58, 17, -11, 6);
        PuffTree(near, 66, 17, -7, 5);

        Slab(near, 60, 40, 30, 9, 8, false, 3);
        Fernt(near, 65, 42, 15, 3);
        PuffTree(near, 74, 43, 7, 6);
        Fernt(near, 83, 41, 12, 4);
        Rock(near, 79, 47);

        Slab(near, 88, 14, 22, 7, 7, false, 4);
        PuffTree(near, 94, 16, 7, 7);
        Fernt(near, 103, 15, 10, 5);

        Slab(near, 22, 18, 12, 5, 5, false, 5);
        PuffTree(near, 28, 19, 5, 8);
        return (near, far);
    }

    private static void FarSlabAt(CapSheet s, int x, int y, int w, int h)
    {
        for (var dy = 0; dy < h + 3; dy++)
        {
            var inset = dy < h ? (dy == 0 ? 1 : 0) : (dy - h + 1) * 2;
            for (var dx = inset; dx < w - inset; dx++) s.Set(x + dx, y + dy, dy == 0 ? FarSlabHi : FarSlab);
        }

        // a far puffball or two
        for (var k = 0; k < w / 8; k++)
        {
            var tx = x + 3 + (k * 7);
            for (var dy = 1; dy <= 3; dy++) s.Set(tx, y - dy, FarSlab);
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++) s.Set(tx + dx, y - 5 + dy, FarSlab);
        }
    }

    /// <summary>
    /// A slab of the Distortion World: a top of rose paving in perspective (rounded corners, a lit far edge, darker and lighter stones, dark
    /// star marks), dark blue sides lit along their edges in bright blue, narrowing ragged underneath; upside down, the sides are over the top.
    /// </summary>
    private static void Slab(CapSheet s, int x, int y, int w, int top, int side, bool hanging, int seed)
    {
        var dir = hanging ? -1 : 1;
        var y0 = hanging ? y + side + top : y;
        for (var dy = 0; dy < top; dy++)
        {
            var corner = dy == 0 ? 2 : dy == 1 || dy == top - 1 ? 1 : 0;
            for (var dx = corner; dx < w - corner; dx++)
            {
                var h = CapArt.Hash((x + dx) * 7, (y + dy) * 13 + seed);
                var c = dy == 0 || dx == corner ? PaveHi : h % 7 == 0 ? PaveDark : h % 11 == 0 ? PaveLit : Pave;
                // the paving's joints, every few stones
                if (dy > 0 && dx > corner && (dx + (dy * 3) + seed) % 9 == 0) c = PaveDark;
                s.Set(x + dx, y0 + (dy * dir), c);
            }
        }

        // the star marks on the paving
        for (var k = 0; k < w / 9; k++)
        {
            int sx = x + 4 + (k * 9) + (int)(CapArt.Hash(seed, k) % 4), sy = y0 + ((2 + (int)(CapArt.Hash(k, seed) % Math.Max(1, top - 4))) * dir);
            s.Set(sx, sy, Star);
            s.Set(sx - 1, sy, Star);
            s.Set(sx + 1, sy, Star);
            s.Set(sx, sy - 1, Star);
            s.Set(sx, sy + 1, Star);
        }

        // the sides under it, narrowing to a ragged edge, lit along the top and the ends in bright blue
        for (var dy = 0; dy < side; dy++)
        {
            var inset = (dy * dy) / Math.Max(1, side - 1) / 2;
            for (var dx = inset; dx < w - inset; dx++)
            {
                var ragged = dy == side - 1 && CapArt.Hash(x + dx, seed + 50) % 2 == 0;
                if (ragged) continue;
                var c = dy == 0 ? Edge : dx == inset || dx == w - inset - 1 ? EdgeDim : dy == side - 1 ? Tip : (dx + seed) % 5 == 0 ? SideHi : Side;
                s.Set(x + dx, y0 + ((top + dy) * dir), c);
            }
        }
    }

    /// <summary>A fern-like tree of the Distortion World: a stem that zigzags up with pairs of leaves slanting up, dark green lit on one side.</summary>
    private static void Fernt(CapSheet s, int x, int foot, int height, int seed)
    {
        var dir = Math.Sign(height);
        height = Math.Abs(height);
        for (var h = 0; h < height; h++)
        {
            var sx = x + ((h / 2) % 2);
            var y = foot - (h * dir);
            s.Set(sx, y, FernLo);
            if (h < 2 || h % 2 != 0) continue;
            var len = h > height - 3 ? 1 : 2 + (int)(CapArt.Hash(seed, h) % 2);
            for (var k = 1; k <= len; k++)
            {
                s.Set(sx - k, y - (k / 2 * dir), k == len ? FernHi : Fern);
                s.Set(sx + k, y - (k / 2 * dir), Fern);
            }
        }

        s.Set(x + ((height / 2) % 2), foot - (height * dir), FernHi);
    }

    /// <summary>A dark puffball tree: a thin trunk and a round, jagged crown, lit up and left; a negative height hangs it down.</summary>
    private static void PuffTree(CapSheet s, int x, int foot, int height, int seed)
    {
        var dir = Math.Sign(height);
        var tall = Math.Abs(height);
        for (var h = 0; h < tall; h++) s.Set(x, foot - (h * dir), Trunk);
        var cy = foot - ((tall + 2) * dir);
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var d = (dx * dx) + (dy * dy);
                var jag = CapArt.Hash(dx + 20, dy + seed) % 3 == 0 ? 1 : 0;
                if (d > 13 + (jag * 4)) continue;
                var c = d > 11 ? PuffLo : dx + dy < -2 ? PuffHi : dx + dy > 3 ? PuffLo : Puff;
                s.Set(x + dx, cy + dy, c);
            }
        }
    }

    /// <summary>A boulder lying on a slab.</summary>
    private static void Rock(CapSheet s, int x, int y)
    {
        s.Set(x, y - 1, BoulderHi);
        s.Set(x + 1, y - 1, Boulder);
        s.Set(x - 1, y, BoulderHi);
        s.Set(x, y, Boulder);
        s.Set(x + 1, y, Boulder);
        s.Set(x + 2, y, BoulderLo);
        s.Set(x, y + 1, BoulderLo);
        s.Set(x + 1, y + 1, BoulderLo);
    }

    /// <summary>
    /// The crystal in its arch, and the arch become a little window onto the Distortion World: the void with a slab of rose paving drifting
    /// across it, and a few of its bricks lifted off it, floating over its keystone.
    /// </summary>
    private static void Rest(CapSheet p, int x0, int floor, (int X, int Y) spot, CapTrialCrystal crystal, double phase, double since)
    {
        var show = Clamp01(since / 1.2);
        var ax = CapGimmighoulRuins.Arches[ArchIndex];
        var foot = floor + CapGimmighoulRuins.Storey;
        var (near, _) = Inside.Value;
        for (var dx = 0; dx < CapGimmighoulRuins.ArchW; dx++)
        {
            for (var up = CapGimmighoulRuins.Plinth; up < CapGimmighoulRuins.ArchH; up++)
            {
                if (CapGimmighoulRuins.ArchAt(ax + dx, up) is not { Rim: false } || Bayer[((up & 3) * 4) + (dx & 3)] / 16.0 >= show) continue;
                int x = x0 + ax + dx, y = foot - up;
                // a small piece of the near layer drifting past, over the void
                int nx = Wrap(dx + (int)(phase * 1.2), WorldW), ny = 30 + (CapGimmighoulRuins.ArchH - up);
                p.Set(x, y, near.Solid(nx, ny) ? near.At(nx, ny) : Back(dx, up, phase));
            }
        }

        // the bricks floating over the keystone, each bobbing at its own pace, with their dark underside
        for (var b = 0; b < 3; b++)
        {
            var bx = x0 + ax + 1 + (b * 5);
            var by = foot - CapGimmighoulRuins.ArchH - 7 - (b % 2 * 2) - R(Math.Sin((phase * 0.9) + (b * 2.1)) * 1.2) - R((1 - show) * 4);
            for (var x = 0; x < 4; x++)
            {
                p.Set(bx + x, by - 1, 0x1A1612u);
                p.Set(bx + x, by, x == 0 ? 0xB4B4A4u : 0x9C9C8Cu);
                p.Set(bx + x, by + 1, x == 0 ? 0x7C7C70u : 0x6E6E62u);
                p.Set(bx + x, by + 2, 0x1A1612u);
            }

            p.Set(bx - 1, by, 0x1A1612u);
            p.Set(bx - 1, by + 1, 0x1A1612u);
            p.Set(bx + 4, by, 0x1A1612u);
            p.Set(bx + 4, by + 1, 0x1A1612u);
            // a wisp of the other side's purple under each now and then
            if (((phase * 0.7) + b) % 3 < 0.5) p.Set(bx + 1 + (b % 2), by + 4, Ghost);
        }

        var glint = phase % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>The Spectrostal in its arch for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) && Spot(l, ctx) is { } spot) Rest(p, x0, floor, spot, crystal, phase, 30);
    }
}
