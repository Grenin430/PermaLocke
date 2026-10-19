namespace PermaLocke.App.Views;

/// <summary>
/// Arceus brings the first trial's Z-Crystal, the Normastal (<see cref="CapCrystalFall.Variant"/> 4): a Normal-type Pokémon for the Normal
/// crystal (2026-10-19, the player's sprite, in the place of the shiny Tapu Koko of 2026-10-18, and without its shiny stars: this one is not
/// shiny). Its film starts <see cref="Lead"/> seconds before the trial film's clock: it comes out of the Ultra Wormhole with the crystal,
/// running on the air, its crest and tail rippling, and crosses the sky of both windows right to left, leaving nothing behind it; over the
/// ruins it stands on the air and calls down Judgment, its own move, as Legends: Arceus shows it (the player's video): needles of every colour
/// gathered over its back, a golden sun, the sun gone small and orange, the first comets, a white flash, a fountain of comets and a shower of
/// fire across the sky, and the blast on the sea as it lets the crystal go, the window red and shaking. Then it shoots off to the left. The
/// crystal's fall is <see cref="CapCrystalFall"/>'s. Laid out on the one sea (world columns); each window draws what falls in it. Pixel art:
/// solid lines and shapes, no blur. A function of the moment: no clock and no random.
/// </summary>
public static class CapArceus
{
    /// <summary>Its sprite: the drawing kept with the visitors' (<see cref="CritterArt"/>), not in any lane.</summary>
    private const int Art = 493;

    /// <summary>How long its film starts before the trial film's clock (slower, asked: it could not be followed at the old speed).</summary>
    public const double Lead = 4.5;

    // The timeline, on the trial film's clock: out of the wormhole, across the party's window, behind the game, over the abyss to the ruins;
    // Judgment there as Legends: Arceus shows it (2026-10-19, from the video the player sent); then away to the left.
    private const double Emerge = -Lead + CapTrial.Born + 0.05, OverParty = -1.0, OverDeep = -0.5, Arrive = 1.0, Release = CapTrial.Hit, Leave = Release + 1.6,
        Gone = Leave + 1.4, Gather = 1.4, Sun = 2.0, Shrink = 2.9, Comets = 3.0, Flare = 3.6, Fountain0 = 3.75, Shower = 3.8, Blast = Release + 0.05;

    /// <summary>How big it is drawn, and the crystal in its hands.</summary>
    private const double Size = 0.65, Held = 0.3;

    /// <summary>How far left of the landing spot it lets go: the crystal keeps some of its speed as it falls.</summary>
    private const double Ahead = 12;

    private const uint White = 0xFFFFFFu, Gold = 0xFFD84Au, GoldDeep = 0xC8960Cu, GoldDark = 0x8A6008u, Pale = 0xFFF4C0u, Foam = 0xD8F2FFu;

    /// <summary>Where it is at a moment in this window's cells, how big, and its tilt; null before it comes and after it has gone.</summary>
    public static (double X, double Y, double Scale, double Tilt)? At(CapWorldLayout l, CapSeaContext ctx, double t)
    {
        if (t < Emerge || t > Gone) return null;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;

        // it comes out of the Ultra Wormhole (exactly where the party's window has it; seen from the abyss, about there)
        (double X, double Y) start = l.Panel == CapPanelKind.Party && CapFarSky.WormholeAt(l) is { } core
            ? (core.X + ctx.WorldX, core.Y)
            : (world - (l.Cols * 0.45), l.SurfaceY * 0.3);
        // over the ruins it stands a little right of the tower and lower, so it and its light read against open sky
        var drop = DropColumn(l, ctx) + 22;
        var cruise = Math.Max(24, l.SurfaceY * 0.42);
        var stand = Math.Max(cruise, l.SurfaceY * 0.66);

        // across the party's window gathering speed, through behind the game, over the abyss slowing down to the ruins, standing on the air
        // over them through Judgment (the windows are taken as being as wide as each other: from one window the other's edges are guessed)
        var partyLeft = l.Panel == CapPanelKind.Party ? ctx.WorldX : world - l.Cols;
        double deepRight = l.Cols;
        double x, y;
        if (t < Leave)
        {
            if (t < OverParty)
            {
                var u = (t - Emerge) / (OverParty - Emerge);
                x = start.X + ((partyLeft - start.X) * Math.Pow(u, 1.6));
            }
            else if (t < OverDeep)
            {
                var u = (t - OverParty) / (OverDeep - OverParty);
                x = partyLeft + ((deepRight - partyLeft) * u);
            }
            else if (t < Arrive)
            {
                var u = (t - OverDeep) / (Arrive - OverDeep);
                x = deepRight + ((drop - deepRight) * (1 - Math.Pow(1 - u, 2.4)));
            }
            else
            {
                x = drop;
            }

            y = start.Y + ((cruise - start.Y) * Math.Min(1, (t - Emerge) / 1.2)) + ((stand - cruise) * Ease01((t - OverDeep) / (Arrive - OverDeep)))
                + (Math.Sin(t * 3.2) * 2.5);
        }
        else
        {
            // gone off to the left, climbing
            var k = t - Leave;
            x = drop - (30 * k) - (90 * k * k);
            y = stand + (Math.Sin(t * 3.2) * 2.5) - (20 * k * k);
        }

        // it grows out of the wormhole; it pitches a little with each rise and dip of its flight
        var grow = Math.Min(1, (t - Emerge) / 0.6);
        return (x - ctx.WorldX, y, Size * (0.15 + (0.85 * grow)), Math.Cos(t * 3.2) * 3);
    }

    private static double Ease01(double k) { k = Math.Clamp(k, 0, 1); return k * k * (3 - (2 * k)); }

    /// <summary>The world column it lets the crystal go over: a little past the landing spot on the tower.</summary>
    private static double DropColumn(CapWorldLayout l, CapSeaContext ctx) =>
        (l.Panel == CapPanelKind.Deep && CapCrystalFall.LandingSpot(l, ctx) is { } spot ? spot.X + ctx.WorldX : 34) + Ahead;

    /// <summary>Where the crystal is in its hands at a moment, in this window's cells.</summary>
    public static (double X, double Y)? Hands(CapWorldLayout l, CapSeaContext ctx, double t) =>
        At(l, ctx, t) is { } at ? (at.X - (4 * at.Scale / Size), at.Y + (12 * at.Scale / Size)) : null;

    /// <summary>Paints it and its Judgment in one window, and the crystal while it holds it.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (At(l, ctx, t) is not { } at || !CritterArt.All.TryGetValue(Art, out var art)) return;
        var k = at.Scale / Size;

        // glowing while it calls the light down
        var glow = t >= Gather && t < Blast ? Math.Sin(Math.Clamp((t - Gather) / (Blast - Gather), 0, 1) * Math.PI) * 0.35 : 0;
        // the clock of its sprite always runs forward (the film's is negative before it starts)
        CapVisitors.Centred(p, art, t + Lead, (int)Math.Round(at.X), (int)Math.Round(at.Y), at.Scale, false, Pale,
            Math.Max(glow, t - Emerge < 0.4 ? 1 - ((t - Emerge) / 0.4) : 0), at.Tilt);

        // the crystal in its hands, turning slowly, until it lets go
        if (t < Release && Hands(l, ctx, t) is { } hands)
        {
            crystal.Paint(p, (int)Math.Round(hands.X), (int)Math.Round(hands.Y), Held * at.Scale / Size, Math.Sin(t * 3) * 0.6, 0.3 + (0.2 * Math.Sin(t * 7)));
        }

        if (t < Gather || t > Blast + 2.3) return;
        (double X, double Y) orb = (at.X + (4 * k), at.Y - (24 * k));
        (double X, double Y) target = (Target(l, ctx), l.SurfaceY + 1.0);

        if (t < Sun) Colours(p, orb, (t - Gather) / (Sun - Gather));
        else if (t < Shrink) SunOf(p, orb, (t - Sun) / (Shrink - Sun), t);
        else if (t < Blast) Ember(p, orb, t);
        if (t >= Comets && t < Blast + 1.5) Fountain(p, orb, target, t);
        if (t >= Flare && t < Flare + 0.18) Flared(p, orb, (t - Flare) / 0.18);
        if (t >= Shower && t < Blast + 1.3) Meteors(p, l, target, t);
        if (t >= Blast) Explosion(p, l, target, t - Blast);
    }

    /// <summary>Where the crystal goes into the sea, in this window's cells.</summary>
    private static double Target(CapWorldLayout l, CapSeaContext ctx) => DropColumn(l, ctx) - Ahead - ctx.WorldX;

    private const uint Orange = 0xFF8A20u, OrangeDeep = 0xD04808u, Ember0 = 0x8A2008u, Yellow = 0xFFE060u, Tint = 0x7A0A10u;

    /// <summary>The colours of the needles it gathers the light with, as in Legends: Arceus (the types' colours).</summary>
    private static readonly uint[] Needles =
    [
        0x78E050u, 0xFFE060u, 0xFFFFFFu, 0xF06040u, 0x68A8F8u, 0x40D8A0u, 0xF8B030u, 0xC8F070u, 0xF070C0u, 0xA0F0F8u, 0xE8E8A0u, 0x60C860u,
    ];

    /// <summary>
    /// First: a burst of thin needles of many colours over its back, pointing out from one spot, closing in on it as the light is gathered
    /// (a little like a firework turned back), the spot white.
    /// </summary>
    private static void Colours(CapSheet p, (double X, double Y) at, double u)
    {
        u = Math.Clamp(u, 0, 1);
        for (var n = 0; n < 40; n++)
        {
            var a = (n * 2.39996) + 0.3;
            var reach = (24 + ((n * 7) % 17)) * (1 - (u * 0.75));
            var len = 5 + ((n * 3) % 7);
            var colour = Needles[n % Needles.Length];
            for (var d = reach - len; d < reach; d++)
            {
                if (d < 2) continue;
                Put(p, at.X + (Math.Cos(a) * d), at.Y + (Math.Sin(a) * d * 0.9), d > reach - 2 ? White : colour);
            }
        }

        Dot(p, at.X, at.Y, 1 + (u * 2), White, Yellow);
    }

    /// <summary>
    /// Then: a golden sun: a white heart, pale and gold round it, thin needles of light all round it of many lengths (gold, white at their
    /// tips), turning slowly, and a thin ring round it all; it swells, its needles growing long as it flares at the end.
    /// </summary>
    private static void SunOf(CapSheet p, (double X, double Y) at, double u, double t)
    {
        u = Math.Clamp(u, 0, 1);
        var r = 4 + (u * 4);
        var flare = u > 0.75 ? (u - 0.75) / 0.25 : 0;
        for (var n = 0; n < 32; n++)
        {
            var a = (n / 32.0 * Math.PI * 2) + (t * 0.4) + ((n % 2) * 0.05);
            var len = (6 + ((n * 5) % 9)) * (1 + (flare * 1.6)) * (0.6 + (u * 0.6));
            for (var d = r + 1; d < r + 1 + len; d++)
            {
                Put(p, at.X + (Math.Cos(a) * d), at.Y + (Math.Sin(a) * d), d > r + len - 1 ? White : n % 3 == 0 ? Yellow : Gold);
            }
        }

        // the thin ring round it
        var ring = r + 9 + (u * 4);
        var steps = (int)(ring * 7);
        for (var i = 0; i < steps; i++)
        {
            var a = i / (double)steps * Math.PI * 2;
            Put(p, at.X + (Math.Cos(a) * ring), at.Y + (Math.Sin(a) * ring), i % 6 == 0 ? White : Gold);
        }

        Dot(p, at.X, at.Y, r, White, Gold);
    }

    /// <summary>Then: the sun gone small and orange-hot, two ribbons of flame curling round it.</summary>
    private static void Ember(CapSheet p, (double X, double Y) at, double t)
    {
        for (var s = 0; s < 2; s++)
        {
            for (var i = 0; i < 26; i++)
            {
                var a = (t * 6) + (s * Math.PI) + (i * 0.16);
                var r = 7 + (i * 0.35);
                var c = i < 4 ? White : i < 10 ? Yellow : i < 18 ? Orange : OrangeDeep;
                Put(p, at.X + (Math.Cos(a) * r), at.Y + (Math.Sin(a) * r * 0.8), c);
                if (i < 14) Put(p, at.X + (Math.Cos(a) * (r + 1)), at.Y + (Math.Sin(a) * (r + 1) * 0.8), Orange);
            }
        }

        Dot(p, at.X, at.Y, 5, White, Orange);
    }

    /// <summary>Then: a white flash at it, a star of long spikes.</summary>
    private static void Flared(CapSheet p, (double X, double Y) at, double u)
    {
        for (var n = 0; n < 12; n++)
        {
            var a = (n / 12.0 * Math.PI * 2) + 0.2;
            var len = (n % 2 == 0 ? 26 : 14) * (0.5 + (u * 0.6));
            for (var d = 3.0; d < len; d++)
            {
                var w = d < len * 0.4 ? 1 : 0;
                for (var o = -w; o <= w; o++) Put(p, at.X + (Math.Cos(a) * d) - (Math.Sin(a) * o), at.Y + (Math.Sin(a) * d) + (Math.Cos(a) * o), d < len * 0.6 ? White : Pale);
            }
        }

        Dot(p, at.X, at.Y, 7, White, Pale);
    }

    /// <summary>When each comet of the fountain is thrown (the first three alone, the rest all at once after the flash), and how high it climbs.</summary>
    private static readonly (double At, double Climb, double Spread)[] Throws =
    [
        (Comets, 30, -0.3), (Comets + 0.15, 38, 0.2), (Comets + 0.3, 26, 0.5),
        (Fountain0, 44, -0.6), (Fountain0 + 0.04, 50, -0.2), (Fountain0 + 0.08, 40, 0.3), (Fountain0 + 0.12, 56, 0.1), (Fountain0 + 0.16, 36, -0.45),
        (Fountain0 + 0.2, 48, 0.55), (Fountain0 + 0.24, 42, -0.05), (Fountain0 + 0.28, 52, 0.4), (Fountain0 + 0.32, 34, -0.7),
    ];

    /// <summary>
    /// The comets: thick ribbons of flame thrown up out of it and arching over onto the sea where the crystal goes, a white head and a tail going
    /// yellow, orange and deep orange, thinner towards its end; where each comes down the sea burns a moment and splashes (<see cref="Splash"/>).
    /// </summary>
    private static void Fountain(CapSheet p, (double X, double Y) from, (double X, double Y) to, double t)
    {
        const double Flight = 0.42;
        foreach (var (at, climb, spread) in Throws)
        {
            var since = t - at;
            if (since < 0 || since > Flight + 1.4) continue;
            (double X, double Y) end = (to.X + (spread * 14), to.Y - 1);
            (double X, double Y) bend = (from.X + ((end.X - from.X) * 0.25) + (spread * 40), from.Y - climb);
            var head = Math.Min(1, since / Flight);
            var tail = Math.Max(0, head - 0.45) + (since > Flight ? (since - Flight) / 0.35 * 0.55 : 0);
            for (var s = 0; s <= 40; s++)
            {
                var v = head - ((head - tail) * s / 40.0);
                if (v < tail) break;
                var (x, y) = Bezier(from, bend, end, v);
                var u = s / 40.0;
                var w = u < 0.2 ? 3 : u < 0.45 ? 2 : u < 0.75 ? 1 : 0;
                var c = u < 0.08 ? White : u < 0.25 ? Yellow : u < 0.6 ? Orange : OrangeDeep;
                for (var oy = -w; oy <= w; oy++) for (var ox = -w; ox <= w; ox++) if (Math.Abs(ox) + Math.Abs(oy) <= w) Put(p, x + ox, y + oy, c);
            }

            if (since >= Flight)
            {
                if (since < Flight + 0.3) Burning(p, end.X, end.Y, (since - Flight) / 0.3);
                Splash(p, end.X, (int)Math.Round(end.Y) + 1, since - Flight, 1, (int)(at * 100));
            }
        }
    }

    /// <summary>The sea burning a moment where a comet came down: a little star of flame and sparks.</summary>
    private static void Burning(CapSheet p, double x, double y, double u)
    {
        for (var n = 0; n < 7; n++)
        {
            var a = Math.PI * (1.1 + (n / 6.0 * 0.8));
            var len = (4 + (n % 3 * 2)) * (1 - (u * 0.5));
            for (var d = 1.0; d < len; d++) Put(p, x + (Math.Cos(a) * d), y + (Math.Sin(a) * d), d < 2 ? White : d < len * 0.6 ? Yellow : Orange);
        }
    }

    /// <summary>
    /// The shower: thin streaks of fire falling slantwise all across the sky onto the sea round the crystal, each a white-hot head with a tail of
    /// orange behind it, one after another; each splashing a little where it comes down.
    /// </summary>
    private static void Meteors(CapSheet p, CapWorldLayout l, (double X, double Y) target, double t)
    {
        for (var n = 0; n < 26; n++)
        {
            var at = Shower + (n * 0.035);
            var since = t - at;
            const double Fall = 0.38;
            if (since < 0 || since > Fall + 1.0) continue;
            var x0 = target.X - 90 + ((n * 37) % 200);
            var y0 = -4.0 - ((n * 13) % 30);
            var x1 = target.X + (((n * 23) % 70) - 35);
            if (since > Fall)
            {
                Splash(p, x1, (int)Math.Round(target.Y), since - Fall, 0.4, n + 50);
                continue;
            }

            var u = since / Fall;
            double hx = x0 + ((x1 - x0) * u), hy = y0 + ((target.Y - y0) * u);
            double dx = (x1 - x0) / Math.Max(1, target.Y - y0), len = 14;
            for (var s = 0; s < len; s++)
            {
                var c = s < 2 ? White : s < 5 ? Yellow : s < 10 ? Orange : OrangeDeep;
                Put(p, hx - (dx * s), hy - s, c);
            }
        }
    }

    private const uint Spray = 0xD8F2FFu, WaterHi = 0x9FE0F0u, WaterMid = 0x58B8D0u, WaterDeep = 0x2A7FA8u, Steam = 0xE4E6EAu, SteamLo = 0xB8BCC8u,
        Smoke = 0x4A3A34u, SmokeHi = 0x6A5A50u, SmokeLo = 0x2E2420u;

    /// <summary>
    /// Fire striking the sea at a size (1 for a comet; less for a streak of the shower, more for the blast): a crown of water thrown up round
    /// it, its walls pale blue with white rims, breaking into drops that arc out and fall back; rings opening on the surface one after another;
    /// steam rising off it in puffs and thinning away.
    /// </summary>
    private static void Splash(CapSheet p, double x, int surface, double s, double size, int seed)
    {
        if (s < 0 || s > 1.4) return;

        // the crown: two walls of water leaning out, rising and falling back
        var crown = s < 0.5 ? Math.Sin(s / 0.5 * Math.PI) : 0;
        if (crown > 0)
        {
            var h = (int)Math.Round(9 * size * crown);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var y = 0; y < h; y++)
                {
                    var lean = (y * 0.45) + (s * 10 * size);
                    var cx = x + (side * (2 + lean));
                    Put(p, cx, surface - y, y == h - 1 ? White : WaterHi);
                    Put(p, cx + side, surface - y, y > h - 3 ? Spray : WaterMid);
                    if (size > 0.8) Put(p, cx - side, surface - y, Spray);
                }
            }
        }

        // the drops it breaks into
        var drops = (int)(10 + (12 * size));
        for (var n = 0; n < drops; n++)
        {
            var life = 0.45 + ((CapArt.Hash(n, seed + 31) % 40) / 100.0);
            if (s > life) continue;
            var a = Math.PI * (0.18 + ((CapArt.Hash(n, seed + 32) % 64) / 100.0));
            var v = (16 + (CapArt.Hash(n, seed + 33) % 22)) * Math.Sqrt(size);
            double dx = x - (Math.Cos(a) * v * s), dy = surface - (Math.Sin(a) * v * s) + (75 * s * s);
            if (dy > surface) continue;
            Put(p, dx, dy, n % 4 == 0 ? White : n % 2 == 0 ? Spray : WaterHi);
            if (n % 3 == 0) Put(p, dx, dy + 1, WaterMid);
        }

        // rings opening on the surface, one after another
        for (var c = 0; c < 3; c++)
        {
            var age = s - (c * 0.16);
            if (age < 0 || age > 0.9) continue;
            var rx = (3 + (age * 34)) * size;
            var steps = Math.Max(12, (int)(rx * 5));
            for (var i = 0; i < steps; i++)
            {
                var a = i / (double)steps * Math.PI * 2;
                // broken up as it spreads and weakens
                if (age > 0.5 && i % 3 == 0) continue;
                Put(p, x + (Math.Cos(a) * rx), surface + 1 + (Math.Sin(a) * Math.Max(1, rx * 0.16)), age < 0.3 ? White : Spray);
            }
        }

        // steam off it
        for (var n = 0; n < 3; n++)
        {
            var age = s - 0.08 - (n * 0.12);
            if (age < 0 || age > 1.1) continue;
            var r = (1.5 + (age * 3)) * Math.Sqrt(size) * (1 - (age / 1.3));
            double px = x + ((n - 1) * 3 * size) + (Math.Sin((age * 5) + n) * 1.5), py = surface - 3 - (age * 18 * Math.Sqrt(size));
            Puff(p, px, py, r, Steam, SteamLo);
        }
    }

    /// <summary>A round puff lit from above: its top <paramref name="hi"/>, the rest <paramref name="lo"/>.</summary>
    private static void Puff(CapSheet p, double x, double y, double r, uint hi, uint lo)
    {
        if (r < 0.6) return;
        int cx = (int)Math.Round(x), cy = (int)Math.Round(y), ri = (int)Math.Ceiling(r);
        for (var dy = -ri; dy <= ri; dy++)
        {
            for (var dx = -ri; dx <= ri; dx++)
            {
                if ((dx * dx) + (dy * dy) > (r * r) + 0.3) continue;
                p.Set(cx + dx, cy + dy, dy < -r * 0.2 ? hi : lo);
            }
        }
    }

    /// <summary>
    /// The end: the blast on the sea where the crystal goes in. Everything goes red and shakes; a fireball swells on the water in layers (white
    /// heart, yellow, orange, a deep orange rim) with tongues of flame and spikes of light round it, cooling to smoke at its edge as it grows;
    /// the sea is thrown up in a column of water that rises, spreads at its top and comes down in rain; two waves run off along the surface
    /// either way; dark smoke rolls up; and under the water, where the crystal plunges, a gulp of bubbles and a shaft of golden light going
    /// down to the ruins.
    /// </summary>
    private static void Explosion(CapSheet p, CapWorldLayout l, (double X, double Y) at, double s)
    {
        if (s > 2.2) return;
        var surface = (int)at.Y;

        // the red over everything, fading
        var red = s < 0.15 ? s / 0.15 : 1 - ((s - 0.15) / 1.4);
        if (red > 0)
        {
            var alpha = (int)(100 * red);
            for (var y = 0; y < p.H; y++) for (var x = 0; x < p.W; x++) if (p.Solid(x, y)) p.Set(x, y, Tint, alpha);
        }

        if (s < 0.06) CapTrial.Flash(p, 200);

        Waves(p, l, at.X, surface, s);
        Geyser(p, at.X, surface, s - 0.35);
        Splash(p, at.X, surface, s, 1.8, 7);

        // smoke rolling up after it
        for (var n = 0; n < 6; n++)
        {
            var age = s - 0.4 - (n * 0.08);
            if (age < 0 || age > 1.6) continue;
            var r = (3 + (age * 5)) * (1 - (age / 2.2));
            double px = at.X + ((n - 2.5) * 6) + (Math.Sin((age * 2) + n) * 3), py = surface - 10 - (age * 26) - (n % 2 * 4);
            Puff(p, px, py, r, SmokeHi, Smoke);
            if (r > 2) p.Set((int)Math.Round(px), (int)Math.Round(py + r), SmokeLo);
        }

        Fireball(p, at.X, surface, s);

        // sparks flung out and falling
        for (var n = 0; n < 34; n++)
        {
            var life = 0.6 + ((n % 5) * 0.14);
            if (s > life) continue;
            var a = Math.PI * (1.05 + (((n * 17) % 90) / 100.0));
            var v = 34 + ((n * 13) % 46);
            double sx = at.X + (Math.Cos(a) * v * s), sy = surface - 4 + (Math.Sin(a) * v * s) + (60 * s * s);
            if (sy > surface) continue;
            Put(p, sx, sy, n % 3 == 0 ? White : n % 3 == 1 ? Yellow : Orange);
            if (n % 2 == 0) Put(p, sx - (Math.Cos(a) * 2), sy - (Math.Sin(a) * 2), OrangeDeep);
        }

        Plunge(p, l, at.X, surface, s - 0.35);
        CapTrial.Shake(p, s, 3.5, 1.0);
    }

    /// <summary>
    /// The fireball sitting on the water: a dome of fire in bands from a white heart out to a deep orange rim, its edge rippling with tongues
    /// of flame; growing, its heart shrinking and its rim going dark to smoke as it cools; spikes of light round it at first.
    /// </summary>
    private static void Fireball(CapSheet p, double x, int surface, double s)
    {
        if (s > 1.0) return;
        var grow = 1 - Math.Pow(1 - Math.Min(1, s / 0.35), 2);
        var big = 8 + (26 * grow);
        var cool = Math.Clamp((s - 0.3) / 0.7, 0, 1);
        var ri = (int)Math.Ceiling(big * 1.3) + 2;
        for (var dy = -ri; dy <= 2; dy++)
        {
            for (var dx = -ri; dx <= ri; dx++)
            {
                var a = Math.Atan2(dy, dx);
                var edge = big * (1 + (0.18 * Math.Sin((a * 7) + (s * 9))) + (0.1 * Math.Sin((a * 13) - (s * 14))));
                var d = Math.Sqrt((dx * dx) + (dy * dy * 1.3));
                if (d > edge) continue;
                var u = d / edge;
                uint c = u < 0.35 * (1 - cool) ? White : u < 0.55 * (1 - (cool * 0.5)) ? Yellow : u < 0.78 ? Orange : u < 0.92 ? OrangeDeep : Ember0;
                if (cool > 0.4 && u > 1 - ((cool - 0.4) * 1.2)) c = u > 0.95 ? SmokeLo : Smoke;
                p.Set((int)Math.Round(x) + dx, surface + dy, c);
            }
        }

        // spikes of light at first
        var k = s < 0.2 ? s / 0.2 : 1 - ((s - 0.2) / 0.4);
        if (k > 0)
        {
            for (var n = 0; n < 22; n++)
            {
                var a = Math.PI * (1.0 + (n / 21.0));
                var len = big + ((14 + ((n * 11) % 34)) * k);
                var w = n % 3 == 0 ? 1 : 0;
                for (var d = big * 0.9; d < len; d++)
                {
                    var c = d < len * 0.7 ? White : d < len * 0.88 ? Yellow : Orange;
                    for (var o = -w; o <= w; o++) Put(p, x + (Math.Cos(a) * d) - (Math.Sin(a) * o), surface + (Math.Sin(a) * d * 0.85) + (Math.Cos(a) * o), c);
                }
            }
        }
    }

    /// <summary>
    /// The column of water the blast throws up: rising fast and slowing, wide at its foot and narrowing, pale water with white streaks up it and
    /// white rims, a spreading head of spray at its top; then falling back in a rain of drops.
    /// </summary>
    private static void Geyser(CapSheet p, double x, int surface, double s)
    {
        if (s < 0 || s > 1.8) return;
        var rise = s < 0.6 ? 1 - Math.Pow(1 - (s / 0.6), 2) : 1;
        var fall = s > 0.8 ? Math.Min(1, (s - 0.8) / 0.6) : 0;
        var tall = 58 * rise * (1 - fall);
        for (var y = 0; y < tall; y++)
        {
            var u = y / Math.Max(1, tall);
            var half = (7 * (1 - u)) + 2 + (Math.Sin((y * 0.5) + (s * 20)) * 0.8);
            for (var dx = -(int)Math.Ceiling(half); dx <= half; dx++)
            {
                var rim = Math.Abs(dx) > half - 1;
                uint c = rim ? White : (dx + 400 + (int)(s * 30)) % 4 == 0 ? White : Math.Abs(dx) < half * 0.4 ? Spray : WaterHi;
                if (!rim && Math.Abs(dx) > half - 2) c = WaterMid;
                Put(p, x + dx, surface - y, c);
            }
        }

        // the head of spray spreading at its top, and then the rain falling back
        for (var n = 0; n < 40; n++)
        {
            var a = (n / 40.0 * Math.PI) + Math.PI;
            var v = 10 + ((n * 7) % 16);
            var age = Math.Max(0, s - 0.3);
            double dx = x + (Math.Cos(a) * v * age * 2), dy = surface - (58 * rise) - (Math.Sin(a) * v * age) + (age * age * 60);
            if (dy > surface || age <= 0) continue;
            Put(p, dx, dy, n % 3 == 0 ? White : n % 3 == 1 ? Spray : WaterHi);
            Put(p, dx, dy + 1, WaterMid);
        }
    }

    /// <summary>
    /// The waves the blast sends off along the surface either way: a crest of water a few cells high with a white top and a foam trail behind
    /// it, running out and flattening as it goes.
    /// </summary>
    private static void Waves(CapSheet p, CapWorldLayout l, double x, int surface, double s)
    {
        if (s > 1.8) return;
        var h = 5 * (1 - (s / 1.8));
        for (var side = -1; side <= 1; side += 2)
        {
            var crest = x + (side * (12 + (s * 90)));
            for (var i = 0; i < 18; i++)
            {
                var cx = crest - (side * i);
                if (cx < 0 || cx >= l.Cols) continue;
                var height = (int)Math.Round(h * Math.Exp(-i * 0.28));
                for (var y = 0; y <= height; y++) Put(p, cx, surface - y, y == height ? White : y > height - 2 ? Spray : WaterHi);
                // foam left behind it on the water
                if (i > 3 && (i + (int)(s * 20)) % 3 == 0) Put(p, cx - (side * 6), surface + 1, Spray);
            }
        }
    }

    /// <summary>
    /// Under the water where the crystal plunges: a gulp of bubbles rising round its way down, and a shaft of golden light down it to the ruins,
    /// fading from the top.
    /// </summary>
    private static void Plunge(CapSheet p, CapWorldLayout l, double x, int surface, double s)
    {
        if (s < 0 || s > 1.6) return;
        var depth = Math.Min(1, s / 1.0) * (l.Rows - surface) * 0.75;
        var from = surface + 2 + (Math.Max(0, s - 0.6) * 60);
        for (var y = (int)from; y < surface + depth; y++)
        {
            var u = (y - surface) / Math.Max(1, depth);
            var w = u < 0.3 ? 1 : 0;
            for (var o = -w; o <= w; o++) p.Set((int)Math.Round(x) + o, y, o == 0 && u < 0.6 ? Pale : Gold, (int)(200 * (1 - u)));
        }

        for (var n = 0; n < 18; n++)
        {
            var age = (s * 1.2) - (n * 0.05);
            if (age < 0 || age > 1.2) continue;
            var start = surface + 6 + ((n * 11) % 40);
            double bx = x + ((n % 5) - 2) * 2 + (Math.Sin((age * 8) + n) * 1.5), by = start - (age * 30);
            if (by < surface + 1) continue;
            var r = n % 3 == 0 ? 1.5 : 0.8;
            if (r > 1) { Put(p, bx - 1, by, Spray); Put(p, bx + 1, by, Spray); Put(p, bx, by - 1, White); Put(p, bx, by + 1, WaterHi); }
            else Put(p, bx, by, Spray);
        }
    }

    /// <summary>A round blob of light: <paramref name="core"/> inside, a rim of <paramref name="rim"/>.</summary>
    private static void Dot(CapSheet p, double x, double y, double r, uint core, uint rim)
    {
        if (r < 0.5) return;
        int cx = (int)Math.Round(x), cy = (int)Math.Round(y), ri = (int)Math.Ceiling(r);
        for (var dy = -ri; dy <= ri; dy++)
        {
            for (var dx = -ri; dx <= ri; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > r) continue;
                p.Set(cx + dx, cy + dy, d > r - 1.2 ? rim : core);
            }
        }
    }

    private static void Put(CapSheet p, double x, double y, uint c) => p.Set((int)Math.Round(x), (int)Math.Round(y), c);

    private static (double X, double Y) Bezier((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, double u) =>
        (((1 - u) * (1 - u) * a.X) + (2 * (1 - u) * u * b.X) + (u * u * c.X), ((1 - u) * (1 - u) * a.Y) + (2 * (1 - u) * u * b.Y) + (u * u * c.Y));
}
