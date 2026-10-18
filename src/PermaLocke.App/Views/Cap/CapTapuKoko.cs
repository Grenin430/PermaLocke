namespace PermaLocke.App.Views;

/// <summary>
/// Shiny Tapu Koko brings the first trial's Z-Crystal (2026-10-18, prototype: <see cref="CapCrystalFall.Variant"/> 4, the player's idea and
/// sprite). At the trial film's birth it bursts out of the Ultra Wormhole with the crystal in its hands, leaning forward, its shells beating;
/// it streaks right to left across the sky of both windows (fast over the party, slowing over the abyss) with a crackling trail of lightning
/// behind it, arcs jumping round its body and shiny stars bursting all round it; over the ruins, at the film's impact, it lets the crystal go
/// with a bolt of lightning down into the sea, then shoots off to the left and is gone. The crystal's fall is <see cref="CapCrystalFall"/>'s.
/// Laid out on the one sea (world columns); each window draws what falls in it. Pixel art: lines, specks and dither, no blur. A function of
/// the moment: no clock and no random.
/// </summary>
public static class CapTapuKoko
{
    /// <summary>Its sprite: the drawing kept with the visitors' (<see cref="CritterArt"/>), not in any lane.</summary>
    private const int Art = 785;

    // The timeline, on the trial film's clock.
    private const double Emerge = CapTrial.Born + 0.05, OverParty = 2.45, OverDeep = 2.8, Release = CapTrial.Hit, Gone = Release + 1.3;

    /// <summary>How big it is drawn, and the crystal in its hands.</summary>
    private const double Size = 0.5, Held = 0.3;

    /// <summary>How far left of the landing spot it lets go: the crystal keeps some of its speed as it falls.</summary>
    private const double Lead = 12;

    private const uint Volt = 0xFFD820u, Rim = 0x8A5C08u, Ember = 0xF2963Cu, White = 0xFFFFFFu, Bolt = 0xFFF27Au, BoltDeep = 0xF2C230u, Spark = 0xB8F4FFu, Shine = 0xFFF6C8u, Ice = 0xD8F2FFu;
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>Where it is at a moment in this window's cells, how big, and its tilt; null before it comes and after it has gone.</summary>
    public static (double X, double Y, double Scale, double Tilt)? At(CapWorldLayout l, CapSeaContext ctx, double t)
    {
        if (t < Emerge || t > Gone) return null;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;

        // it comes out of the Ultra Wormhole (exactly where the party's window has it; seen from the abyss, about there)
        (double X, double Y) start = l.Panel == CapPanelKind.Party && CapFarSky.WormholeAt(l) is { } core
            ? (core.X + ctx.WorldX, core.Y)
            : (world - (l.Cols * 0.45), l.SurfaceY * 0.3);
        var drop = DropColumn(l, ctx);
        var cruise = Math.Max(24, l.SurfaceY * 0.42);

        // across the party's window gathering speed, through behind the game in a flash, over the abyss slowing down to the ruins
        // (the windows are taken as being as wide as each other: from one window the other's edges are guessed)
        var partyLeft = l.Panel == CapPanelKind.Party ? ctx.WorldX : world - l.Cols;
        double deepRight = l.Cols;
        double x, y;
        if (t < Release)
        {
            if (t < OverParty)
            {
                var u = (t - Emerge) / (OverParty - Emerge);
                x = start.X + ((partyLeft - start.X) * u * u);
            }
            else if (t < OverDeep)
            {
                var u = (t - OverParty) / (OverDeep - OverParty);
                x = partyLeft + ((deepRight - partyLeft) * u);
            }
            else
            {
                var u = (t - OverDeep) / (Release - OverDeep);
                x = deepRight + ((drop - deepRight) * (1 - Math.Pow(1 - u, 2.4)));
            }

            y = start.Y + ((cruise - start.Y) * Math.Min(1, (t - Emerge) / 0.8)) + (Math.Sin(t * 5.2) * 3);
        }
        else
        {
            // gone off to the left, climbing
            var k = t - Release;
            x = drop - (40 * k) - (140 * k * k);
            y = cruise + (Math.Sin(t * 5.2) * 3) - (26 * k * k);
        }

        // it grows out of the wormhole; it pitches a little with each rise and dip of its flight
        var grow = Math.Min(1, (t - Emerge) / 0.35);
        return (x - ctx.WorldX, y, Size * (0.15 + (0.85 * grow)), Math.Cos(t * 5.2) * 3);
    }

    /// <summary>The world column it lets the crystal go over: a little past the landing spot on the tower.</summary>
    private static double DropColumn(CapWorldLayout l, CapSeaContext ctx) =>
        (l.Panel == CapPanelKind.Deep && CapCrystalFall.LandingSpot(l, ctx) is { } spot ? spot.X + ctx.WorldX : 34) + Lead;

    /// <summary>Where the crystal is in its hands at a moment, in this window's cells.</summary>
    public static (double X, double Y)? Hands(CapWorldLayout l, CapSeaContext ctx, double t) =>
        At(l, ctx, t) is { } at ? (at.X - (4 * at.Scale / Size), at.Y + (12 * at.Scale / Size)) : null;

    /// <summary>Paints it, its lightning and its stars in one window, and the crystal while it holds it.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (At(l, ctx, t) is not { } at || !CritterArt.All.TryGetValue(Art, out var art)) return;

        Trail(p, l, ctx, t);
        if (t - Emerge < 0.5) Burst(p, at.X, at.Y, (t - Emerge) / 0.5);
        // afterimages of it in yellow while it goes fast
        for (var n = 3; n >= 1; n--)
        {
            if (At(l, ctx, t - (n * 0.05)) is not { } ghost || Math.Abs(ghost.X - at.X) < 6 * n) continue;
            CapVisitors.Centred(p, art, t - (n * 0.05), (int)Math.Round(ghost.X), (int)Math.Round(ghost.Y), ghost.Scale, false, Bolt, 0.75, ghost.Tilt, 0.45 - (n * 0.12));
        }

        Arcs(p, at.X, at.Y, at.Scale, t);
        CapVisitors.Centred(p, art, t, (int)Math.Round(at.X), (int)Math.Round(at.Y), at.Scale, false, White, t - Emerge < 0.25 ? 1 - ((t - Emerge) / 0.25) : 0,
            at.Tilt);

        // the crystal in its hands, turning slowly, until it lets go
        if (t < Release && Hands(l, ctx, t) is { } hands)
        {
            crystal.Paint(p, (int)Math.Round(hands.X), (int)Math.Round(hands.Y), Held * at.Scale / Size, Math.Sin(t * 3) * 0.6, 0.3 + (0.2 * Math.Sin(t * 7)));
        }

        Stars(p, at.X, at.Y, at.Scale, t);
        if (t >= Release - 0.35 && t < Release) Charge(p, at.X, at.Y, at.Scale, (t - Release + 0.35) / 0.35);
        if (t >= Release && t < Release + 1.2 && At(l, ctx, Release) is { } letGo) Strike(p, l, ctx, letGo, t - Release);
    }

    /// <summary>
    /// What it leaves in the air behind it: shards of lightning left where it has been, a little apart from it, each crackling on its own
    /// (a short forked zigzag, a new shape as it flickers), white when new, yellow, then orange as it dies, flickering out; and motes of gold
    /// drifting down from them. Nothing touches it: the first ones are born a breath behind.
    /// </summary>
    private static void Trail(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double t)
    {
        const int Shards = 36;
        const double Every = 0.024, Gap = 0.09, Life = 0.75;
        var flicker = (int)Math.Floor(t * 18);
        // each shard belongs to a moment on a fixed grid of time, so it stays where it was born while it lives
        var newest = Math.Floor((t - Gap) / Every);
        for (var n = 0; n < Shards; n++)
        {
            var slot = (int)newest - n;
            var born = slot * Every;
            var age = t - born;
            if (age < Gap || age > Life || At(l, ctx, born) is not { } q) continue;
            var k = q.Scale / Size;
            var life = (age - Gap) / (Life - Gap);

            // where it is left: behind its body, scattered round its line, drifting a little
            var spread = ((CapArt.Hash(slot, 101) % 100) / 100.0) - 0.5;
            var x = q.X + (12 * k) + ((CapArt.Hash(slot, 102) % 9) - 4) + (life * 3);
            var y = q.Y + 2 + (spread * 18 * k) + (life * 4);

            // a new shape each flicker, and sometimes dark for a moment
            if (CapArt.Hash(slot, flicker) % 5 == 0 || Bayer[(int)y & 3, (int)x & 3] >= (1 - (life * life)) * 17) continue;
            var a = (CapArt.Hash(slot, flicker + 7) % 628) / 100.0;
            var length = (3 + (CapArt.Hash(slot, 103) % 5)) * (1 - (life * 0.4)) * k;
            var end = (X: x + (Math.Cos(a) * length), Y: y + (Math.Sin(a) * length * 0.7));
            Shard(p, (x, y), end, (slot * 13) + flicker, life);

            // a mote of gold falling from it
            if (CapArt.Hash(slot, 104) % 2 == 0)
            {
                int mx = (int)Math.Round(x + (Math.Sin((age * 9) + slot) * 1.5)), my = (int)Math.Round(y + 2 + (age * 16));
                p.Set(mx, my, life < 0.5 ? Shine : Ember, (int)(220 * (1 - life)));
            }
        }
    }

    /// <summary>One shard of lightning: a zigzag with a kink or two and maybe a twig, its colour by its age (white, yellow, orange).</summary>
    private static void Shard(CapSheet p, (double X, double Y) a, (double X, double Y) b, int seed, double life)
    {
        var colour = life < 0.25 ? White : life < 0.6 ? Volt : Ember;
        var edge = life < 0.25 ? Volt : life < 0.6 ? Ember : Rim;
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var kink = (((CapArt.Hash(seed, 1) % 100) / 100.0) - 0.5) * 3;
        var mid = (X: ((a.X + b.X) / 2) - (dy * 0.3 * kink), Y: ((a.Y + b.Y) / 2) + (dx * 0.3 * kink));
        Line(p, a, mid, colour, edge);
        Line(p, mid, b, colour, edge);
        if (CapArt.Hash(seed, 2) % 3 == 0) Line(p, mid, (mid.X + (dy * 0.5), mid.Y - (dx * 0.5)), edge, edge);
    }

    /// <summary>A thin line of a colour with a cell of its edge colour under it.</summary>
    private static void Line(CapSheet p, (double X, double Y) a, (double X, double Y) b, uint colour, uint edge)
    {
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var u = i / (double)steps;
            int x = (int)Math.Round(a.X + ((b.X - a.X) * u)), y = (int)Math.Round(a.Y + ((b.Y - a.Y) * u));
            p.Set(x, y + 1, edge, 200);
            p.Set(x, y, colour);
        }
    }

    /// <summary>A little zigzag of a spark: three cells stepping one way and back, white tipped.</summary>
    private static void Zap(CapSheet p, double x, double y, int side)
    {
        int cx = (int)Math.Round(x), cy = (int)Math.Round(y);
        p.Set(cx, cy, White);
        p.Set(cx + 1, cy + side, Bolt);
        p.Set(cx + 2, cy, Bolt);
        p.Set(cx + 3, cy + side, Shine);
    }

    /// <summary>
    /// A bolt of lightning from <paramref name="a"/> to <paramref name="b"/>, broken again and again at a point pushed sideways (by up to
    /// <paramref name="rough"/> times the stretch), with a branch forking off now and then; drawn as <paramref name="thick"/> cells of white
    /// with a yellow edge (and a pale halo when <paramref name="halo"/>). The same seed gives the same bolt.
    /// </summary>
    private static void Fractal(CapSheet p, (double X, double Y) a, (double X, double Y) b, double rough, int depth, int seed, int thick, bool halo)
    {
        if (depth == 0)
        {
            Thick(p, a, b, thick, halo);
            return;
        }

        double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 2)
        {
            Thick(p, a, b, thick, halo);
            return;
        }

        var push = (((CapArt.Hash(seed, depth) % 1000) / 1000.0) - 0.5) * Math.Min(rough, length * 0.45);
        var mid = (X: ((a.X + b.X) / 2) - (dy / length * push), Y: ((a.Y + b.Y) / 2) + (dx / length * push));
        Fractal(p, a, mid, rough * 0.55, depth - 1, (seed * 3) + 1, thick, halo);
        Fractal(p, mid, b, rough * 0.55, depth - 1, (seed * 3) + 2, thick, halo);

        // a branch forks off the middle now and then, thinner, a little way out
        if (depth >= 3 && CapArt.Hash(seed, 77) % 5 == 0)
        {
            var turn = (CapArt.Hash(seed, 78) % 2 == 0 ? 1 : -1) * 0.7;
            double c = Math.Cos(turn), s = Math.Sin(turn);
            var end = (X: mid.X + (((dx * c) - (dy * s)) * 0.35), Y: mid.Y + (((dx * s) + (dy * c)) * 0.35));
            Fractal(p, mid, end, rough * 0.4, depth - 2, seed + 991, Math.Max(1, thick - 1), false);
        }
    }

    /// <summary>One straight stretch of a bolt: its core white, a cell of yellow either side, a dithered pale halo beyond.</summary>
    private static void Thick(CapSheet p, (double X, double Y) a, (double X, double Y) b, int thick, bool halo)
    {
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))) + 1;
        var across = Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y);
        for (var i = 0; i <= steps; i++)
        {
            var u = i / (double)steps;
            int x = (int)Math.Round(a.X + ((b.X - a.X) * u)), y = (int)Math.Round(a.Y + ((b.Y - a.Y) * u));
            // a white core one cell wide (two for a heavy bolt), a saturated yellow edge, and on the main bolt a dark gold rim so it reads
            // against a pale sky
            var r = thick + (halo ? 1 : 0);
            for (var o = -r; o <= r; o++)
            {
                int px = across ? x : x + o, py = across ? y + o : y;
                var d = Math.Abs(o);
                if (d < thick && (thick == 1 || o >= 0)) p.Set(px, py, White);
                else if (d <= thick) p.Set(px, py, Volt);
                else p.Set(px, py, Rim, 170);
            }
        }
    }

    /// <summary>One stretch of a bolt: a white core with a yellow edge (the main strand) or a thin yellow line, fading by dither.</summary>
    private static void Segment(CapSheet p, (double X, double Y) a, (double X, double Y) b, double fade, bool main)
    {
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var u = i / (double)steps;
            int x = (int)Math.Round(a.X + ((b.X - a.X) * u)), y = (int)Math.Round(a.Y + ((b.Y - a.Y) * u));
            if (Bayer[y & 3, x & 3] >= fade * 17) continue;
            if (main)
            {
                // a halo of pale light round the bolt, dithered, then the bolt itself: two cells of white with a yellow edge
                for (var h = -3; h <= 3; h++)
                {
                    if (Math.Abs(h) >= 2 && ((x + y + h) & 1) == 0) p.Set(x, y + h, Shine, 110);
                }

                p.Set(x, y - 1, Bolt);
                p.Set(x, y + 2, BoltDeep);
                p.Set(x, y, fade > 0.5 ? White : Shine);
                p.Set(x, y + 1, fade > 0.7 ? White : Bolt);
            }
            else
            {
                p.Set(x, y, fade > 0.5 ? Bolt : BoltDeep, 210);
            }
        }
    }

    /// <summary>Arcs of electricity jumping round its body: short zigzags between points on a ring round it, a new set every few frames.</summary>
    private static void Arcs(CapSheet p, double cx, double cy, double scale, double t)
    {
        var flicker = (int)Math.Floor(t * 15);
        var r = 20 * scale / Size;
        for (var n = 0; n < 4; n++)
        {
            if (CapArt.Hash(n, flicker) % 3 == 0) continue;
            var a0 = (CapArt.Hash(n, flicker + 11) % 628) / 100.0;
            var a1 = a0 + 0.6 + ((CapArt.Hash(n, flicker + 13) % 80) / 100.0);
            (double X, double Y) from = (cx + (Math.Cos(a0) * r), cy + (Math.Sin(a0) * r * 0.85));
            for (var s = 1; s <= 4; s++)
            {
                var a = a0 + ((a1 - a0) * s / 4);
                var rr = r * (0.8 + (((CapArt.Hash(n * 7 + s, flicker) % 100) / 100.0) * 0.5));
                var to = (X: cx + (Math.Cos(a) * rr), Y: cy + (Math.Sin(a) * rr * 0.85));
                Segment(p, from, to, 1, false);
                from = to;
            }
        }
    }

    /// <summary>
    /// The shiny stars: a cloud of four-pointed stars round it, each popping up, growing and shrinking away on its own beat, white, pale gold and
    /// pale blue; and every so often a ring of them bursting outwards, as a shiny's does when it appears.
    /// </summary>
    private static void Stars(CapSheet p, double cx, double cy, double scale, double t)
    {
        var k = scale / Size;
        for (var n = 0; n < 22; n++)
        {
            var beat = ((t * (1.3 + ((n % 5) * 0.12))) + ((CapArt.Hash(n, 61) % 100) / 100.0)) % 1;
            var cycle = (int)Math.Floor((t * (1.3 + ((n % 5) * 0.12))) + ((CapArt.Hash(n, 61) % 100) / 100.0));
            var a = (CapArt.Hash(n, cycle * 7) % 628) / 100.0;
            var r = (24 + (CapArt.Hash(n, cycle * 7 + 1) % 16)) * k;
            var size = beat < 0.15 ? 1 : beat < 0.35 ? 3 : beat < 0.55 ? 4 : beat < 0.75 ? 2 : beat < 0.9 ? 1 : 0;
            if (size == 0) continue;
            var colour = n % 3 == 0 ? Shine : n % 3 == 1 ? Ice : White;
            Star(p, cx + (Math.Cos(a) * r) + (beat * 6), cy + (Math.Sin(a) * r * 0.8), size, colour);
        }

        // a ring of stars bursting out every 0.9 s
        var ring = (t - Emerge) % 0.9;
        if (ring < 0.45)
        {
            var u = ring / 0.45;
            for (var n = 0; n < 8; n++)
            {
                var a = (n / 8.0 * Math.PI * 2) + 0.3;
                var r = (22 + (u * 22)) * k;
                Star(p, cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r * 0.8), u < 0.5 ? 3 : u < 0.8 ? 2 : 1, n % 2 == 0 ? White : Ice);
            }
        }
    }

    /// <summary>Its coming out of the wormhole: a white ring of stars opening and a flash of lightning round it.</summary>
    private static void Burst(CapSheet p, double cx, double cy, double u)
    {
        for (var n = 0; n < 14; n++)
        {
            var a = n / 14.0 * Math.PI * 2;
            var r = 6 + (u * 40);
            Star(p, cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r * 0.8), u < 0.4 ? 3 : u < 0.7 ? 2 : 1, n % 2 == 0 ? White : Bolt);
        }
    }

    /// <summary>Before it lets go: sparks drawn into it from all round and a ring of light closing on it, faster and faster.</summary>
    private static void Charge(CapSheet p, double cx, double cy, double scale, double u)
    {
        var k = scale / Size;
        for (var n = 0; n < 24; n++)
        {
            var a = (n / 24.0 * Math.PI * 2) + (u * 2);
            var r = (44 - (u * 36)) * k * (0.8 + (((CapArt.Hash(n, 81) % 100) / 100.0) * 0.4));
            Zap(p, cx + (Math.Cos(a) * r), cy + (Math.Sin(a) * r * 0.8), n % 2 == 0 ? 1 : -1);
        }

        var ring = (30 * (1 - u)) * k;
        for (var n = 0; n < 48; n++)
        {
            var a = n / 48.0 * Math.PI * 2;
            int x = (int)Math.Round(cx + (Math.Cos(a) * ring)), y = (int)Math.Round(cy + (Math.Sin(a) * ring * 0.8));
            p.Set(x, y, n % 2 == 0 ? White : Bolt);
        }
    }

    /// <summary>
    /// The bolt it lets the crystal go with: thick, forked, from it down into the sea where the crystal will fall. It flickers (on, off, on,
    /// on) taking a new shape each time, cracks along the surface where it strikes and throws foam up, then dwindles to a thin thread
    /// shedding sparks.
    /// </summary>
    private static void Strike(CapSheet p, CapWorldLayout l, CapSeaContext ctx, (double X, double Y, double Scale, double Tilt) at, double age)
    {
        var target = (X: DropColumn(l, ctx) - Lead - ctx.WorldX, Y: l.SurfaceY + 2.0);
        var from = (X: at.X - (4 * at.Scale / Size), Y: at.Y + (10 * at.Scale / Size));
        // on under the film's flash, off, then on and off twice more, each time a new shape, then a thread dwindling
        var shape = age < 0.1 ? 0 : age < 0.15 ? -1 : age < 0.45 ? 1 : age < 0.5 ? -1 : age < 0.8 ? 2 : 3;
        if (shape >= 0)
        {
            // jagged at the scale of the whole bolt, then finer and finer; a fork or two off it
            var length = Math.Abs(target.Y - from.Y);
            var thick = shape is 0 or 1 ? 2 : 1;
            var rough = length * (shape == 3 ? 0.08 : 0.18);
            Fractal(p, from, target, rough, 6, 4000 + (shape * 101), thick, shape < 3);
            if (shape < 3)
            {
                Fractal(p, from, (target.X - 12, target.Y - (length * 0.25)), rough * 0.7, 4, 5000 + (shape * 37), 1, false);
            }
        }

        // where it strikes: cracks of light running along the surface, foam thrown up
        if (age < 0.6)
        {
            var k = age / 0.6;
            for (var side = -1; side <= 1; side += 2)
            {
                Fractal(p, (target.X, target.Y - 1), (target.X + (side * (8 + (k * 26))), target.Y - 1), 3, 4, 6000 + (side * 13) + (int)(age * 30), 1, false);
            }

            for (var n = 0; n < 18; n++)
            {
                var v = 14 + (CapArt.Hash(n, 91) % 22);
                var a = Math.PI * (0.2 + (((CapArt.Hash(n, 92) % 100) / 100.0) * 0.6));
                int x = (int)Math.Round(target.X - (Math.Cos(a) * v * age)), y = (int)Math.Round(target.Y - (Math.Sin(a) * v * age) + (70 * age * age));
                if (y <= target.Y && Bayer[y & 3, x & 3] < (1 - k) * 16) p.Set(x, y, n % 3 == 0 ? White : Ice);
            }
        }

        // the sparks it sheds as it dwindles
        if (age > 0.4)
        {
            for (var n = 0; n < 12; n++)
            {
                var u = (n + 0.5) / 12.0;
                var fall = (age - 0.4) * (20 + (n % 4 * 6));
                int x = (int)Math.Round(from.X + ((target.X - from.X) * u) + ((CapArt.Hash(n, 93) % 5) - 2)), y = (int)Math.Round(from.Y + ((target.Y - from.Y) * u) + fall);
                if (Bayer[y & 3, x & 3] < (1 - ((age - 0.4) / 0.8)) * 16) p.Set(x, y, n % 2 == 0 ? White : Volt);
            }
        }
    }

    private static void Star(CapSheet p, double x, double y, int size, uint colour)
    {
        int cx = (int)Math.Round(x), cy = (int)Math.Round(y);
        p.Set(cx, cy, White);
        for (var d = 1; d <= size; d++)
        {
            var c = d == size ? colour : White;
            p.Set(cx + d, cy, c);
            p.Set(cx - d, cy, c);
            p.Set(cx, cy + d, c);
            p.Set(cx, cy - d, c);
        }

        if (size >= 3)
        {
            p.Set(cx + 1, cy + 1, colour);
            p.Set(cx - 1, cy + 1, colour);
            p.Set(cx + 1, cy - 1, colour);
            p.Set(cx - 1, cy - 1, colour);
        }
    }
}
