using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The sixth trial's Z-Crystal, the Litostal, brought by Terapagos and freed by Miraidon (2026-10-18; the player's idea and sprites). Its own
/// film, starting <see cref="Lead"/> seconds before the film's clock. The Ultra Wormhole beats and Terapagos comes out of it, flying (its
/// fringes waving like fins, glints running over its shell, leaning the way it goes, a trail of crystal glints behind it) in slow loops across
/// both windows; where it passes, clusters of Area Zero's crystals spring up from the floor of both seas (hexagonal prisms drawn as pixel art:
/// flat facets lit on the left, an outline, a glint up each, every prism its own iridescent colour), and on the top of the low wall with the
/// big window (the spot the player marked) a big one with the Litostal inside. Terapagos leaves to the left. Then Miraidon comes out of the
/// wormhole, flying with its chest full of energy and a crackling trail, and as it sweeps over each cluster it strikes it with a bolt: the
/// crystal flashes, cracks and bursts into shards that tumble and sink. Across the abyss and back, it smashes the big one last at the film's
/// impact, and the Litostal drops onto the wall. It stays there for the rest of the run (<see cref="PaintRest"/>).
/// </summary>
public static class CapAreaZero
{
    /// <summary>The sixth trial's crystal.</summary>
    public const int Litostal = 819;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 15;

    private const int Tera = 1024, Mirai = 1008;
    private const double TeraSize = 0.7, MiraiSize = 0.55, Lying = 0.4;

    private const double Beat1 = -14.7, Beat2 = -14.4, TeraOut = -14.0, TeraParty = -10.4, TeraDeep = -10.2, TeraGone = -5.8,
        MiraiOut = -5.4, MiraiParty = -1.8, MiraiDeep = -1.5, MiraiTurn = 2.2, Hover = 3.0, Charge = 3.1, Fire = 4.0, Hit = CapTrial.Hit, Crack = 0.15;

    private const uint White = 0xFFFFFFu, Ink = 0x1E2A5Au, Spark = 0xFFE45Au, Volt = 0x7FE8FFu, BoltRim = 0x2A3A9Au;

    /// <summary>The pale iridescent tints of Area Zero's clear crystal (for its shards and glints), each a light, a middle and a dark face.</summary>
    private static readonly (uint Light, uint Mid, uint Dark)[] Hues =
    [
        (0xFFFFFF, 0xC4EEFF, 0x6A86C8), (0xFFE4F4, 0xFFC8EC, 0xB884C8), (0xFFFBE0, 0xFFF4B4, 0xC8B070),
        (0xE4FFF2, 0xC4FFE4, 0x6AB8A0), (0xEEE4FF, 0xE2D2FF, 0x8A78C8),
    ];

    /// <summary>The clusters: in which window (true for the abyss), where across it (a share of its width), and how tall.</summary>
    private static readonly (bool Deep, double At, int Size)[] Clusters =
    [
        (false, 0.93, 16), (false, 0.8, 22), (false, 0.68, 12), (false, 0.56, 18), (false, 0.42, 24), (false, 0.3, 14), (false, 0.2, 20), (false, 0.09, 16),
        (true, 0.97, 18), (true, 0.9, 12), (true, 0.84, 24), (true, 0.76, 14), (true, 0.5, 22), (true, 0.44, 14), (true, 0.37, 26),
        (true, 0.3, 12), (true, 0.24, 20), (true, 0.17, 16), (true, 0.1, 24), (true, 0.04, 14),
    ];

    /// <summary>Where the Litostal lies: on the top of the low wall with the big window; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) ? (x0 + 136, floor - CapGimmighoulRuins.Surface(136)) : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (!CritterArt.All.TryGetValue(Tera, out var tera) || !CritterArt.All.TryGetValue(Mirai, out var mirai)) return;
        var deep = l.Panel == CapPanelKind.Deep;
        if (!deep && l.Panel != CapPanelKind.Party) return;

        if (!deep && CapFarSky.WormholeAt(l) is { } core)
        {
            foreach (var beat in (ReadOnlySpan<double>)[Beat1, Beat2, MiraiOut - 0.3])
            {
                var k = Math.Exp(-Math.Pow((t - beat) / 0.1, 2));
                CapTrial.Shine(p, core.X, core.Y, 18 + (10 * k), 15 + (8 * k), Hues[(int)Math.Abs(beat) % Hues.Length].Mid, 170 * k);
            }

            foreach (var born in (ReadOnlySpan<double>)[TeraOut, MiraiOut])
            {
                var b = t - born;
                if (b < 0 || b >= 0.6) continue;
                var k = 1 - (b / 0.6);
                CapTrial.Shine(p, core.X, core.Y, 22 * k, 18 * k, White, 255 * k);
                for (var c = 0; c < 3; c++) CapTrial.Ring(p, core.X, core.Y, 5 + (b * 90) + (c * 3), 0.6, Hues[c].Mid, 200 * k);
            }
        }

        // the clusters: springing up as Terapagos passes, standing, struck by Miraidon as it sweeps over
        for (var i = 0; i < Clusters.Length; i++)
        {
            var (inDeep, at, size) = Clusters[i];
            if (inDeep != deep) continue;
            var x = at * l.Cols;
            var ground = CapSea.Relief(l, ctx, R(x)) + 1;
            var grow = Clamp01((t - Sprung(l, inDeep, x)) / 0.7);
            var struck = t - Struck(l, ctx, inDeep, x);
            if (grow > 0 && struck < Crack) Cluster(p, l, ctx, x, size, i, grow, t, struck > 0 ? struck / Crack * 0.6 : 0);
            if (struck >= 0 && struck < Crack) Cracks(p, x, ground, size, i, struck / Crack);
            if (struck >= -0.04 && struck < Crack + 0.08 && MiraiAt(l, ctx, t, deep) is { } m) Bolt(p, m.X, m.Y + 6, x, ground - (size * 0.5), i, t);
            if (struck >= Crack)
            {
                Shock(p, x, ground - (size * 0.5), size, struck - Crack);
                Shatter(p, l, ctx, x, ground, size, i, struck - Crack, false);
                Arcs(p, l, ctx, x, size, i, struck - Crack, t);
            }
        }

        Coat(p, l, t, deep);
        Sprinkles(p, l, ctx, t, deep);

        // the big one on the wall, the Litostal inside it
        if (deep && Spot(l, ctx) is { } spot)
        {
            var grow = Clamp01((t - Sprung(l, true, spot.X)) / 0.9);
            var white = t > Hit ? (t - Hit) / 0.12 : 0;
            if (t < Hit + 0.12 && grow > 0)
            {
                // one block of amber crystal grown out of the wall's top, the Litostal still inside it once it has closed round it
                Block(p, spot.X, spot.Y + 1, BigWidth, BigHeight, 0, 7, grow, white, false, Amber);
                if (grow >= 0.75) crystal.Paint(p, spot.X, R(spot.Y - 11), 0.4, 0, 0.5, -1);
                Block(p, spot.X, spot.Y + 1, BigWidth, BigHeight, 0, 7, grow, white, true, Amber);
            }

            if (t >= Fire && t < Hit + 0.12) Cracks(p, spot.X, spot.Y + 1, 26, 99, Clamp01((t - Fire) / (Hit - Fire)));
            if (t >= Charge && t < Hit && MiraiAt(l, ctx, t, true) is { } mc)
            {
                Charging(p, mc.X, mc.Y + 4, t, Clamp01((t - Charge) / (Fire - Charge)));
                if (t >= Fire) Beam(p, mc.X - 4, mc.Y + 6, spot.X, spot.Y - 10, t, Clamp01((t - Fire) / (Hit - Fire)));
            }

            if (t >= Hit)
            {
                if (t - Hit < 0.16) Burst(p, spot.X, spot.Y - 10, 34, t - Hit);
                Shock(p, spot.X, spot.Y - 10, 40, t - Hit);
                Shatter(p, l, ctx, spot.X, spot.Y, 34, 99, t - Hit - 0.12, true);
                var fall = Clamp01((t - Hit - 0.12) / 0.35);
                var rest = spot.Y - R(CapTrialCrystal.H * Lying);
                Patch(p, spot, Clamp01((t - Hit - 0.45) / 1.2), t);
                if (t >= Hit + 0.12) crystal.Paint(p, spot.X, R(spot.Y - 14 + ((rest - spot.Y + 14) * fall * fall)), Lying, 0, t - Hit < 0.7 ? 0.9 : 0.3, -1);
                if (fall >= 1) Shards(p, spot);
            }
        }

        // Terapagos flying in loops, a trail of crystal glints behind it
        if (TeraAt(l, t, deep) is { } now && TeraAt(l, t - 0.05, deep) is { } before)
        {
            for (var k = 1; k <= 10; k++)
            {
                if (TeraAt(l, t - (k * 0.09), deep) is not { } back) continue;
                var hue = Hues[(((k + (int)Math.Floor(t * 6)) % Hues.Length) + Hues.Length) % Hues.Length];
                Glint(p, back.X + ((Rnd(k, (int)(t * 10)) - 0.5) * 8), back.Y + ((Rnd(k + 3, (int)(t * 10)) - 0.5) * 8), k < 4 ? hue.Light : hue.Mid, k < 5 ? 1 : 0);
            }

            var grow = deep ? 1 : Clamp01((t - TeraOut) / 0.5);
            CapVisitors.Centred(p, tera, t, R(now.X), R(now.Y), TeraSize * (0.2 + (0.8 * grow)), false, White, 0, Tilt(before, now, false));
        }

        // Miraidon sweeping over the clusters, a crackling trail behind it
        if (MiraiAt(l, ctx, t, deep) is { } mNow && MiraiAt(l, ctx, t - 0.04, deep) is { } mBefore)
        {
            var right = mNow.X > mBefore.X;
            for (var k = 1; k <= 8; k++)
            {
                if (MiraiAt(l, ctx, t - (k * 0.035), deep) is not { } back || MiraiAt(l, ctx, t - ((k + 1) * 0.035), deep) is not { } far) continue;
                var jag = (Rnd(k, (int)(t * 30)) - 0.5) * 6;
                Line(p, back.X, back.Y + 4 + jag, far.X, far.Y + 4 - jag, k % 2 == 0 ? Spark : Volt);
            }

            var grow = deep ? 1 : Clamp01((t - MiraiOut) / 0.5);
            if (t < Charge) Orbit(p, mNow.X, mNow.Y + 4, t, 0.2);
            CapVisitors.Centred(p, mirai, t, R(mNow.X), R(mNow.Y), MiraiSize * (0.2 + (0.8 * grow)), right, White, 0, Tilt(mBefore, mNow, right) * 0.6);
        }
    }

    /// <summary>The lean of a flier along its way: nose up when it climbs, down when it dives, as far as its sheet has frames for.</summary>
    private static double Tilt((double X, double Y) from, (double X, double Y) to, bool facingRight)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < 0.01) return 0;
        var up = Math.Atan2(-dy, Math.Abs(dx)) * 180 / Math.PI;
        return Math.Clamp(up, -30, 30) * (facingRight == dx > 0 || dx == 0 ? 1 : -1);
    }

    /// <summary>Where Terapagos is in this window at a moment, or null when it is not in it.</summary>
    private static (double X, double Y)? TeraAt(CapWorldLayout l, double t, bool deep)
    {
        if (!deep)
        {
            if (t < TeraOut || t >= TeraParty || CapFarSky.WormholeAt(l) is not { } c0) return null;
            var u = (t - TeraOut) / (TeraParty - TeraOut);
            return (c0.X - (u * (c0.X + 70)) + (Math.Cos(u * 3 * Math.PI) * 24 * (1 - (u * 0.3))), c0.Y + 12 + (u * 12) + (Math.Sin(u * 3 * Math.PI) * 15));
        }

        if (t < TeraDeep || t >= TeraGone) return null;
        var v = (t - TeraDeep) / (TeraGone - TeraDeep);
        return (l.Cols + 40 - (v * (l.Cols + 110)) + (Math.Cos(v * 4 * Math.PI) * 26), (l.SurfaceY * 0.7) + (v * (l.Rows - l.SurfaceY) * 0.4) + (Math.Sin(v * 4 * Math.PI) * 20));
    }

    /// <summary>When the cluster at this column springs up: when Terapagos goes past it.</summary>
    private static double Sprung(CapWorldLayout l, bool deep, double x)
    {
        var (from, to) = deep ? (TeraDeep, TeraGone) : (TeraOut, TeraParty);
        for (var k = 0; k <= 100; k++)
        {
            var t = from + ((to - from) * k / 100.0);
            if (TeraAt(l, t, deep) is { } at && at.X <= x) return t;
        }

        return to;
    }

    /// <summary>Where Miraidon is in this window at a moment, or null when it is not in it: over the party's floor from the wormhole to the left
    /// edge, then across the abyss from the right edge to the left, then back up to the big crystal on the wall for the impact, then away.</summary>
    private static (double X, double Y)? MiraiAt(CapWorldLayout l, CapSeaContext ctx, double t, bool deep)
    {
        if (!deep)
        {
            if (t < MiraiOut || t >= MiraiParty || CapFarSky.WormholeAt(l) is not { } c0) return null;
            var u = (t - MiraiOut) / (MiraiParty - MiraiOut);
            var floor = l.SurfaceY + ((l.Rows - l.SurfaceY) * 0.55);
            return (c0.X - (u * (c0.X + 80)), c0.Y + ((floor - c0.Y) * Math.Min(1, u * 2.5)) + (Math.Sin(u * 6 * Math.PI) * 5));
        }

        var low = l.Rows - ((l.Rows - l.SurfaceY) * 0.3);
        if (t >= MiraiDeep && t < MiraiTurn)
        {
            var v = (t - MiraiDeep) / (MiraiTurn - MiraiDeep);
            return (l.Cols + 60 - (v * (l.Cols + 50)), low + (Math.Sin(v * 7 * Math.PI) * 6));
        }

        if (Spot(l, ctx) is not { } s) return null;
        if (t >= MiraiTurn && t < Hover)
        {
            var w = Ease((t - MiraiTurn) / (Hover - MiraiTurn));
            return (10 + ((s.X + 30 - 10) * w), low + ((s.Y - 40 - low) * w) - (Math.Sin(Math.PI * w) * 20));
        }

        if (t >= Hover && t < Hit) return (s.X + 30, s.Y - 40 + (Math.Sin((t - Hover) * 4) * 2));

        if (t >= Hit && t < Hit + 1.8)
        {
            var k = (t - Hit) / 1.8;
            return (s.X + 30 + (k * 140), s.Y - 40 - (k * k * 170));
        }

        return null;
    }

    /// <summary>When Miraidon strikes the cluster at this column: as it sweeps over it.</summary>
    private static double Struck(CapWorldLayout l, CapSeaContext ctx, bool deep, double x)
    {
        var (from, to) = deep ? (MiraiDeep, MiraiTurn) : (MiraiOut, MiraiParty);
        for (var k = 0; k <= 120; k++)
        {
            var t = from + ((to - from) * k / 120.0);
            if (MiraiAt(l, ctx, t, deep) is { } at && at.X <= x + 6) return t;
        }

        return to;
    }

    /// <summary>
    /// A cluster of Area Zero's crystals as in the player's references: chunky hexagonal blocks, pale and clear with rainbow streaks inside,
    /// each sunk in the floor where it stands (its own foot found under it, so none hangs over a dip), a long icy shard leaning out of some,
    /// loose chunks on the sand round them and the floor lit round their foot. They rise out of the sand rather than grow; a glint runs over
    /// them now and then. <paramref name="white"/> washes them white as they are struck.
    /// </summary>
    private static void Cluster(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double x, int size, int seed, double grow, double phase, double white)
    {
        var rise = Ease(grow);
        var s = size / 20.0;
        var ground = Foot(l, ctx, x, 8 * s);
        FloorGlow(p, x, ground, 14 * s, seed);
        if (Rnd(seed, 40) < 0.6)
        {
            var sx = x + ((Rnd(seed, 41) < 0.5 ? -1 : 1) * 5 * s);
            Prism(p, sx, Foot(l, ctx, sx, 2) + 1, 14 * s * rise, Math.Max(5, (int)(4 * s)) | 1, Ice, (Rnd(seed, 42) < 0.5 ? -1 : 1) * (0.35 + (Rnd(seed, 43) * 0.25)), white, false);
        }

        double bx = x - (6 * s), mx = x + (2 * s), rx = x + (9 * s);
        Block(p, bx, Foot(l, ctx, bx, 4 * s), 7 * s, 6 * s, -0.12, seed + 1, rise, white, false, Clear);
        Block(p, mx, Foot(l, ctx, mx, 6 * s), 11 * s, 9 * s, 0.06 + ((Rnd(seed, 44) - 0.5) * 0.1), seed, rise, white, false, Clear);
        if (Rnd(seed, 45) < 0.7) Block(p, rx, Foot(l, ctx, rx, 3 * s), 5 * s, 4 * s, 0.2, seed + 2, rise, white, false, Clear);

        // loose chunks on the sand round them
        for (var n = 0; n < 4; n++)
        {
            if (rise < 0.6) break;
            var cx = R(x + ((Rnd(seed + n, 46) - 0.5) * 30 * s));
            var cy = CapSea.Relief(l, ctx, cx);
            p.Set(cx, cy, Clear.Top);
            p.Set(cx + 1, cy, Clear.Right);
            if (n % 2 == 0) { p.Set(cx, cy - 1, Clear.Edge); p.Set(cx + 1, cy - 1, Clear.Left); }
        }

        if (grow >= 1 && white <= 0)
        {
            var g = (phase * 0.8) + (seed * 0.37);
            if (g % 3 < 0.5) Glint(p, x + ((Rnd(seed, (int)(g / 3)) - 0.5) * 10 * s), ground - (9 * s * (0.5 + (Rnd(seed + 1, (int)(g / 3)) * 0.6))), White, 1);
        }
    }

    /// <summary>Where a crystal standing at <paramref name="x"/> and reaching <paramref name="half"/> either side sits: one row into the lowest of the
    /// floor under it, so its whole foot is in the sand.</summary>
    private static int Foot(CapWorldLayout l, CapSeaContext ctx, double x, double half)
    {
        var low = 0;
        for (var xx = R(x - half); xx <= R(x + half); xx++) low = Math.Max(low, CapSea.Relief(l, ctx, xx));
        return low + 1;
    }

    /// <summary>The colours of a crystal: its top, its lit and shaded faces, the white edge, the lit side of its outline, its shade and the
    /// streaks of colour across it.</summary>
    private sealed record Look(uint Top, uint Left, uint Right, uint Edge, uint Lit, uint Shade, uint[] Streaks);

    /// <summary>The clear crystal of Area Zero: a near-white top, a pale blue face, a pale lilac one, rainbow streaks.</summary>
    private static readonly Look Clear = new(0xEEF6FF, 0xA6E2FF, 0xC6B2F2, 0xFFFFFF, 0x92D2F6, 0x4E62AE, [0xFFB0E0, 0xFFEC98, 0xA8F8D4, 0x9CC4FF]);

    /// <summary>The Litostal's crystal (asked of another colour, for the rock type): warm amber with streaks of rose and pale gold.</summary>
    private static readonly Look Amber = new(0xFFF2CC, 0xF8C670, 0xDA9250, 0xFFFBEA, 0xF8DA98, 0x8A4E2A, [0xFFC4A0, 0xFFF0A8, 0xF4A8B8, 0xFFE2C0]);

    private const double BigWidth = 18, BigHeight = 15;

    /// <summary>The long icy shards of the Area Zero caves.</summary>
    private static readonly (uint Light, uint Mid, uint Dark) Ice = (0xE4FFFF, 0x86E4FF, 0x3E92D8);

    /// <summary>
    /// A chunky hexagonal crystal sunk where it stands, seen from a little above: its top face (a flat hexagon), and below it two faces split a
    /// little left of the middle, with streaks of colour running across them; a white edge on top, the lit colour on the left, the shade on the
    /// right. It rises out of the floor as <paramref name="rise"/> goes from 0 to 1, what is still under the floor not drawn, and on sand the
    /// sand heaps a little round its foot.
    /// </summary>
    private static void Block(CapSheet p, double x, int ground, double w, double h, double lean, int seed, double rise, double white, bool edgesOnly, Look look)
    {
        if (rise <= 0 || w < 2) return;
        var half = Math.Max(1, w / 2);
        var topH = Math.Max(2, (int)Math.Round(w * 0.35));
        var total = topH + (int)Math.Round(h) + 2;                                                  // two rows sunk in the floor
        var sink = (int)Math.Round((1 - rise) * total);
        var top = ground - total + 2 + sink;
        for (var r = 0; r < total; r++)
        {
            var y = top + r;
            if (y >= ground) break;
            var cx = x + (lean * (total - r));
            var face = r >= topH;
            var reach = face ? half : half * (0.55 + (0.45 * r / topH));
            int left = R(cx - reach), right = R(cx + reach);
            var split = cx - (half * 0.15);
            for (var xx = left; xx <= right; xx++)
            {
                var edge = xx == left || xx == right || r == 0 || r == topH;
                uint c;
                if (edge) c = r == 0 || (r == topH && xx > left && xx < right) ? look.Edge : xx == right || r == topH ? look.Shade : look.Lit;
                else if (!face) c = look.Top;
                else
                {
                    c = xx < split ? look.Left : look.Right;
                    if (Math.Abs(xx - split) < 0.5) c = look.Edge;
                    var band = (((int)Math.Floor(((xx - cx) * 0.8) - (r * 0.6) + (seed * 3)) / 3) % 8 + 8) % 8;
                    if (band is 0 or 3 or 5) c = look.Streaks[(band + seed) % look.Streaks.Length];
                }

                if (edgesOnly && !edge) continue;
                if (white > 0) c = CapSheet.Mix(c, White, Math.Min(1, white));
                p.Set(xx, y, c);
            }
        }

        // the sand heaped a little against its foot (on sand only: on stone it just sits)
        if (!edgesOnly && sink < total - 2 && look == Clear)
        {
            for (var xx = R(x - half - 1); xx <= R(x + half + 1); xx++)
            {
                if ((xx + seed) % 3 == 0) continue;
                var sand = p.At(xx, ground + 1);
                if (sand != 0) p.Set(xx, ground - 1, sand);
            }
        }
    }

    // where the small crystals go on the broken tops of the ruins (columns)
    private static readonly int[] OnRuins = [6, 26, 50, 70, 112, 140, 180, 202];

    /// <summary>
    /// The small crystals that come up on the broken tops of the ruins as Terapagos passes; they burst at the film's impact.
    /// </summary>
    private static void Sprinkles(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double t, bool deep)
    {
        var burst = t - Hit;
        if (burst > 1.2) return;

        if (deep && CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, width))
        {
            for (var k = 0; k < OnRuins.Length; k++)
            {
                if (OnRuins[k] >= width) continue;
                double x = x0 + OnRuins[k];
                var y = floor - CapGimmighoulRuins.Surface(OnRuins[k]) + 1;
                var rise = Clamp01((t - Sprung(l, true, x)) / 0.6);
                if (burst < 0) Block(p, x, y, 5, 4, 0.1 * ((k % 3) - 1), k + 50, rise, 0, false, Clear);
                else Pop(p, x, y - 5, k + 50, burst);
            }
        }

    }

    /// <summary>A cloud's own cells: the white, pale and blue tones of the clouds (night and weather darken them, so this is loose).</summary>
    private static bool IsCloud(uint c)
    {
        int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), b = (int)(c & 255);
        return c != 0 && r > 110 && g > 115 && b > 130 && Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 70;
    }

    /// <summary>A small crystal popping at the impact: a white glint and a few shards falling.</summary>
    private static void Pop(CapSheet p, double x, double y, int seed, double age)
    {
        if (age < 0.15) Glint(p, x, y, White, 1);
        for (var n = 0; n < 4; n++)
        {
            var hue = Hues[(seed + n) % Hues.Length];
            var a = Rnd(n, seed) * Math.PI * 2;
            p.Set(R(x + (Math.Cos(a) * age * 14)), R(y + (Math.Sin(a) * age * 10) + (age * age * 20)), n % 2 == 0 ? hue.Light : hue.Mid);
        }
    }

    // ---------------------------------------------------------------- Miraidon's strikes (asked "más épico y detallado")

    /// <summary>The charge Miraidon carries as it sweeps: sparks orbiting it, and its crackle (stronger while it gathers for the big one).</summary>
    private static void Orbit(CapSheet p, double x, double y, double t, double power)
    {
        var count = 6 + (int)(power * 10);
        for (var n = 0; n < count; n++)
        {
            var a = (n / (double)count * 2 * Math.PI) + (t * (5 + (n % 3)));
            var r = 15 + (power * 6) + (Math.Sin((t * 9) + n) * 2);
            int px = R(x + (Math.Cos(a) * r)), py = R(y + (Math.Sin(a) * r * 0.75));
            p.Set(px, py, n % 3 == 0 ? White : n % 3 == 1 ? Spark : Volt);
            if (power > 0.3) p.Set(px + (n % 2 == 0 ? 1 : -1), py, Volt);
        }
    }

    /// <summary>
    /// Miraidon gathering itself for the big one: energy drawn in from all round to its chest (lines of light shrinking inwards), its sparks
    /// orbiting faster and wider, and a ring tightening round it.
    /// </summary>
    private static void Charging(CapSheet p, double x, double y, double t, double k)
    {
        Orbit(p, x, y, t, 0.4 + (0.6 * k));
        for (var n = 0; n < 14; n++)
        {
            var a = (Rnd(n, (int)(t * 6)) * 2 * Math.PI);
            var u = ((t * 2.2) + (n / 14.0)) % 1;
            var r = 46 * (1 - u);
            double fx = x + (Math.Cos(a) * r), fy = y + (Math.Sin(a) * r * 0.8);
            Line(p, fx, fy, fx + ((x - fx) * 0.18), fy + ((y - fy) * 0.18), n % 2 == 0 ? Spark : Volt);
        }

        var ring = 30 * (1 - ((t * 3) % 1));
        CapTrial.Ring(p, x, y, ring, 0.8, Volt, 200, add: false);
        if (k > 0.6) Glint(p, x, y + 2, White, 2);
    }

    /// <summary>
    /// A bolt from Miraidon onto a crystal: a thick jagged trunk (white heart, yellow sides, dark blue rims) and branches forking off it, the
    /// whole flickering and re-forking a few times a second.
    /// </summary>
    private static void Bolt(CapSheet p, double x0, double y0, double x1, double y1, int seed, double t)
    {
        var flick = (int)(t * 24);
        double px = x0, py = y0;
        const int steps = 8;
        for (var s = 1; s <= steps; s++)
        {
            var k = s / (double)steps;
            var jag = s == steps ? 0 : (Rnd(seed + s, flick) - 0.5) * 12;
            double nx = x0 + ((x1 - x0) * k) + jag, ny = y0 + ((y1 - y0) * k) + (jag * 0.4);
            Line(p, px - 2, py, nx - 2, ny, BoltRim);
            Line(p, px + 2, py, nx + 2, ny, BoltRim);
            Line(p, px - 1, py, nx - 1, ny, Spark);
            Line(p, px + 1, py, nx + 1, ny, Spark);
            Line(p, px, py, nx, ny, White);
            if (s > 1 && s < steps && Rnd(seed + s, flick + 50) < 0.5)
            {
                // a branch forking off
                var side = Rnd(seed + s, flick + 70) < 0.5 ? -1 : 1;
                double bx = nx + (side * (6 + (Rnd(seed, s) * 8))), by = ny + 4 + (Rnd(seed + 1, s) * 6);
                Line(p, nx, ny, bx, by, Spark);
                Line(p, bx, by, bx + (side * 3), by + 3, Volt);
            }

            px = nx;
            py = ny;
        }

        Glint(p, x1, y1, White, 2);
    }

    /// <summary>
    /// Miraidon's finishing beam onto the big crystal: a thick straight beam (white core, yellow, cyan and dark blue in bands out from it) that
    /// swells as it fires, sparks thrown off along it and a burst of light where it meets the crystal.
    /// </summary>
    private static void Beam(CapSheet p, double x0, double y0, double x1, double y1, double t, double k)
    {
        var width = 2 + (int)(k * 3) + ((int)(t * 30) % 2);
        var n = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))));
        double len = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0))), nx = -(y1 - y0) / len, ny = (x1 - x0) / len;
        for (var i = 0; i <= n; i++)
        {
            double cx = x0 + ((x1 - x0) * i / n), cy = y0 + ((y1 - y0) * i / n);
            for (var w = -width - 1; w <= width + 1; w++)
            {
                var a = Math.Abs(w);
                var c = a == width + 1 ? BoltRim : a == width ? Volt : a >= width - 1 ? Spark : White;
                p.Set(R(cx + (nx * w)), R(cy + (ny * w)), c);
            }

            if (i % 7 == (int)(t * 40) % 7)
            {
                var side = (i / 7) % 2 == 0 ? 1 : -1;
                p.Set(R(cx + (nx * side * (width + 3))), R(cy + (ny * side * (width + 3))), Spark);
                p.Set(R(cx + (nx * side * (width + 5))), R(cy + (ny * side * (width + 5)) + 1), Volt);
            }
        }

        Glint(p, x1, y1, White, 2);
        CapTrial.Ring(p, x1, y1, 4 + (k * 6) + ((t * 30) % 3), 0.8, White, 255, add: false);
    }

    /// <summary>Cracks running out over a crystal as it is struck: jagged white lines from where it was hit, longer as <paramref name="k"/> grows.</summary>
    private static void Cracks(CapSheet p, double x, int ground, int size, int seed, double k)
    {
        double cx = x + 1, cy = ground - (size * 0.45);
        for (var n = 0; n < 6; n++)
        {
            var a = (n / 6.0 * 2 * Math.PI) + (Rnd(seed, n) * 0.6);
            double px = cx, py = cy;
            var reach = size * 0.55 * k;
            for (var s = 1; s <= 4; s++)
            {
                var r = reach * s / 4;
                var turn = a + ((Rnd(seed + n, s) - 0.5) * 0.9);
                double qx = cx + (Math.Cos(turn) * r), qy = cy + (Math.Sin(turn) * r * 0.8);
                Line(p, px, py, qx, qy, s == 4 ? Clear.Lit : White);
                px = qx;
                py = qy;
            }
        }
    }

    /// <summary>The blow going through the water: two rings of bright pixels running out from the hit, and a puff of crystal dust.</summary>
    private static void Shock(CapSheet p, double x, double y, int size, double age)
    {
        if (age > 0.6) return;
        var k = age / 0.6;
        CapTrial.Ring(p, x, y, 4 + (k * size * 1.6), 0.6, White, 255 * (1 - k), add: false);
        if (age > 0.08) CapTrial.Ring(p, x, y, 2 + ((age - 0.08) / 0.52 * size * 1.1), 0.6, Volt, 220 * (1 - k), add: false);
        if (age < 0.08) Burst(p, x, y, size, age);
    }

    /// <summary>The flash of a hit in solid pixels (no soft glow): rays of white edged with yellow shooting out from it, a white heart.</summary>
    private static void Burst(CapSheet p, double x, double y, double reach, double age)
    {
        var k = Math.Min(1, age / 0.08);
        for (var n = 0; n < 12; n++)
        {
            var a = (n / 12.0 * 2 * Math.PI) + 0.13;
            var len = reach * (n % 2 == 0 ? 1 : 0.6) * (0.4 + (0.6 * k));
            double ex = x + (Math.Cos(a) * len), ey = y + (Math.Sin(a) * len * 0.8);
            Line(p, x + Math.Cos(a + 0.08), y + Math.Sin(a + 0.08), ex, ey, Spark);
            Line(p, x, y, ex, ey, White);
        }

        Glint(p, x, y, White, 2);
    }

    /// <summary>Electricity left crackling over the floor round a smashed crystal for a moment: small zigzags that jump about.</summary>
    private static void Arcs(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double x, int size, int seed, double age, double t)
    {
        if (age > 0.7) return;
        for (var n = 0; n < 4; n++)
        {
            var sx = x + ((Rnd(n, seed + (int)(t * 12)) - 0.5) * size * 1.4);
            var sy = CapSea.Relief(l, ctx, R(sx)) - 1;
            double px = sx, py = sy;
            for (var s = 0; s < 4; s++)
            {
                double qx = px + 2, qy = sy - ((s % 2 == 0) ? 2 : 0);
                Line(p, px, py, qx, qy, s % 2 == 0 ? Spark : Volt);
                px = qx;
                py = qy;
            }
        }
    }

    // ---------------------------------------------------------------- the crystal coat on the Sky Pillar and the clouds (asked)

    /// <summary>
    /// The Sky Pillar and the clouds crystallized whole as Terapagos passes, as if a coat of crystal had grown over them: every cell of them takes
    /// a face of Area Zero's clear crystal (pale tints, lighter where the cell was light, the shade where it was dark), the faces split by white
    /// edges in a slanting lattice; a band of glints runs along the edge of the coat as it spreads. At the impact it cracks off in pieces that fall.
    /// </summary>
    private static void Coat(CapSheet p, CapWorldLayout l, double t, bool deep)
    {
        var gone = t - Hit;
        if (gone > 1.0) return;
        var wormhole = deep ? null : CapFarSky.WormholeAt(l);
        var pillar = deep ? CapFarSky.PillarTopAt(l) : null;
        double front0, span;
        if (deep) { front0 = TeraDeep; span = TeraGone - TeraDeep; }
        else { front0 = TeraOut; span = TeraParty - TeraOut; }
        var start = deep ? l.Cols + 40.0 : (wormhole?.X ?? l.Cols * 0.5);
        var reach = deep ? l.Cols + 110.0 : start + 70;
        for (var y = 0; y < l.SurfaceY - 1; y++)
        {
            for (var x = 0; x < l.Cols; x++)
            {
                var c = p.At(x, y);
                if (c == 0) continue;
                var onPillar = pillar is { } top && Math.Abs(x - top.X) <= 16 && !IsSky(c);
                if (!onPillar && !IsCloud(c)) continue;
                if (wormhole is { } w && Math.Abs(x - w.X) < 34 && Math.Abs(y - w.Y) < 28) continue;
                var at = front0 + (Math.Clamp((start - x) / reach, 0, 1) * span) - 0.3;
                var since = t - at;
                if (since < 0) continue;
                if (gone >= 0 && Rnd(x, y + 900) < gone / 0.6) continue;
                p.Set(x, y, since < 0.12 && ((x + y) & 1) == 0 ? White : Crystallized(c, x, y));
            }
        }

        // the coat cracking off at the impact: pieces falling away
        if (gone >= 0)
        {
            for (var n = 0; n < 40; n++)
            {
                var x = R(Rnd(n, 910) * l.Cols);
                var y0 = Rnd(n, 911) * l.SurfaceY * 0.8;
                var y = R(y0 + (gone * gone * 60) + (gone * 8));
                if (y >= l.SurfaceY || !(IsCloud(p.At(x, R(y0))) || pillar is { } top2 && Math.Abs(x - top2.X) <= 16)) continue;
                var hue = Hues[n % Hues.Length];
                p.Set(x, y, hue.Light);
                p.Set(x + 1, y, hue.Mid);
                p.Set(x, y + 1, hue.Dark);
            }
        }
    }

    /// <summary>The colour a cell of cloud or stone takes under the crystal coat.</summary>
    private static uint Crystallized(uint c, int x, int y)
    {
        int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), b = (int)(c & 255);
        var lum = (r + g + b) / 3;
        // the faces: a slanting lattice of cells, each its own tint
        int fu = (int)Math.Floor((x + (y * 0.6)) / 6.0), fv = (int)Math.Floor((y - (x * 0.35)) / 5.0);
        int ru = (int)Math.Floor((x + 1 + (y * 0.6)) / 6.0), rv = (int)Math.Floor((y + 1 - ((x + 1) * 0.35)) / 5.0);
        if (ru != fu || rv != fv) return lum < 90 ? Clear.Shade : Clear.Edge;
        if (lum < 70) return Clear.Shade;
        var face = (int)(Rnd(fu, fv + 333) * 6);
        var tint = face switch { 0 => Clear.Top, 1 => Clear.Left, 2 => Clear.Right, 3 => Clear.Left, 4 => Clear.Streaks[(fu + fv) & 3], _ => Clear.Top };
        return lum > 200 ? CapSheet.Mix(tint, White, 0.35) : lum > 140 ? tint : CapSheet.Mix(tint, Clear.Shade, 0.35);
    }

    /// <summary>A cell of open sky: blue well over its red.</summary>
    private static bool IsSky(uint c) => (int)(c & 255) > (int)((c >> 16) & 255) + 35;

    /// <summary>The floor lit round a crystal's foot: a band of sand cells lightened towards the crystal's light, every other cell, so it stays pixel art.</summary>
    private static void FloorGlow(CapSheet p, double x, int ground, double reach, int seed)
    {
        for (var dy = 0; dy <= 2; dy++)
        {
            var span = reach * (1 - (dy * 0.25));
            for (var xx = R(x - span); xx <= R(x + span); xx++)
            {
                if (((xx + dy + seed) & 1) == 1) continue;
                var y = ground + dy;
                var under = p.At(xx, y);
                if (under != 0) p.Set(xx, y, CapSheet.Mix(under, 0xBFE8FFu, 0.35 - (dy * 0.08)));
            }
        }
    }

    private static void Prism(CapSheet p, double baseX, int ground, double height, int w, (uint Light, uint Mid, uint Dark) hue, double lean, double white, bool edgesOnly)
    {
        if (height < 1) return;
        var half = w / 2;
        var tip = Math.Min(height, w * 0.9);
        for (var r = 0; r <= height; r++)
        {
            var cx = R(baseX + (lean * r));
            var reach = r > height - tip ? half * (height - r) / tip : half;
            var span = (int)Math.Round(reach);
            for (var u = -span - 1; u <= span + 1; u++)
            {
                var outline = Math.Abs(u) > span || r >= height - 0.5;
                uint c;
                if (outline) c = Ink;
                else if (r > height - tip) c = u < 0 ? hue.Light : u > 0 ? hue.Dark : hue.Mid;
                else c = u < -half / 3.0 ? hue.Light : u > half / 3.0 ? hue.Dark : hue.Mid;
                var glint = !outline && u == -Math.Max(1, half - 1) && r > height * 0.2 && r < height * 0.7;
                if (glint) c = White;
                if (edgesOnly && !outline && !glint) continue;
                if (white > 0 && !outline) c = CapSheet.Mix(c, White, Math.Min(1, white));
                p.Set(cx + u, ground - r, c);
            }
        }
    }

    /// <summary>A four-pointed glint of solid pixels: a white heart and arms of its colour (size 0, 1 or 2).</summary>
    private static void Glint(CapSheet p, double x, double y, uint c, int size)
    {
        int cx = R(x), cy = R(y);
        p.Set(cx, cy, White);
        for (var k = 1; k <= size + 1; k++)
        {
            var arm = k == 1 ? c : CapSheet.Mix(c, Ink, 0.2);
            p.Set(cx + k, cy, arm);
            p.Set(cx - k, cy, arm);
            p.Set(cx, cy + k, arm);
            p.Set(cx, cy - k, arm);
        }
    }

    private static void Line(CapSheet p, double x0, double y0, double x1, double y1, uint c)
    {
        var n = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))));
        for (var i = 0; i <= n; i++) p.Set(R(x0 + ((x1 - x0) * i / n)), R(y0 + ((y1 - y0) * i / n)), c);
    }

    // the shapes a shard takes as it turns: little faceted pieces of three or four pixels
    private static readonly (int X, int Y, int Face)[][] ShardShapes =
    [
        [(0, 0, 0), (1, 0, 1), (0, 1, 2)], [(0, 0, 0), (0, 1, 1), (1, 1, 2)], [(0, 0, 1), (1, 0, 0), (1, 1, 2), (2, 1, 2)], [(0, 0, 0), (1, 1, 1), (0, 1, 2)],
        [(1, 0, 0), (0, 1, 0), (1, 1, 1), (2, 1, 1), (1, 2, 2)], [(0, 0, 0), (1, 0, 0), (2, 0, 1), (1, 1, 2), (2, 1, 2)],
    ];

    /// <summary>A cluster burst: its shards flying out, turning (their shape changing), slowing in the water and sinking to the floor.</summary>
    private static void Shatter(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double x, int ground, int size, int seed, double age, bool amber)
    {
        if (age < 0 || age > 1.6) return;
        var count = 14 + size;
        for (var n = 0; n < count; n++)
        {
            var a = -Math.PI * (0.1 + (Rnd(n, seed + 3) * 0.8));
            var v = (14 + (Rnd(n, seed + 4) * 26)) * (size / 18.0);
            var k = 1 - Math.Exp(-age * 2.5);
            double sx = x + ((Rnd(n, seed + 7) - 0.5) * size * 0.5), sy = ground - (Rnd(n, seed + 8) * size * 0.8);
            var px = sx + (Math.Cos(a) * v * k);
            var py = Math.Min(Math.Min(ground, CapSea.Relief(l, ctx, R(px))) - 1, sy + (Math.Sin(a) * v * k) + (age * age * 22));
            var hue = amber ? AmberHues[(seed + n) % AmberHues.Length] : Hues[(seed + n) % Hues.Length];
            var shape = ShardShapes[(int)((age * (6 + (n % 3))) + n) % ShardShapes.Length];
            foreach (var (dx, dy, face) in shape) p.Set(R(px) + dx, R(py) + dy, face == 0 ? hue.Light : face == 1 ? hue.Mid : hue.Dark);
        }

        // a flash of white facets for a moment
        if (age < 0.08) for (var n = 0; n < 8; n++) Glint(p, x + ((Rnd(n, seed) - 0.5) * size), ground - (Rnd(n + 1, seed) * size), White, 1);
    }

    /// <summary>The amber of the Litostal's crystal, for its shards: light, middle and dark faces.</summary>
    private static readonly (uint Light, uint Mid, uint Dark)[] AmberHues = [(0xFFF2CC, 0xF8C670, 0x8A4E2A), (0xFFE2C0, 0xDA9250, 0x6A3A20), (0xFFF0A8, 0xF4A8B8, 0x8A4E2A)];

    /// <summary>A few shards of the big crystal left round the Litostal on the wall.</summary>
    private static void Shards(CapSheet p, (int X, int Y) spot)
    {
        foreach (var (dx, h, c) in (ReadOnlySpan<(int, int, int)>)[(-9, 3, 0), (-6, 2, 3), (7, 3, 1), (10, 2, 4)])
        {
            for (var k = 0; k < h; k++) p.Set(spot.X + dx + (k % 2), spot.Y - 1 - k, k == h - 1 ? Hues[c].Light : Hues[c].Mid);
            p.Set(spot.X + dx - 1, spot.Y - 1, Ink);
        }
    }

    /// <summary>
    /// The rock type left on the ruins (asked): round where the Litostal lies the wall is partly crystallized, a ragged patch of Area Zero's
    /// clear crystal over its bricks (the faces of the coat on the clouds, taken from each brick's own light), thickest by the Litostal and
    /// breaking up towards its edge, a couple of small blocks of crystal standing out of it and a glint over it now and then. It spreads from
    /// the Litostal as <paramref name="k"/> goes from 0 to 1.
    /// </summary>
    private static void Patch(CapSheet p, (int X, int Y) spot, double k, double phase)
    {
        if (k <= 0) return;
        const double rx = 13, ry = 8;
        for (var dy = -(int)ry; dy <= ry; dy++)
        {
            for (var dx = -(int)rx; dx <= rx; dx++)
            {
                int x = spot.X + dx, y = spot.Y + 2 + dy;
                var d = Math.Sqrt(Math.Pow(dx / rx, 2) + Math.Pow(dy / ry, 2)) + ((Rnd(x, y + 1200) - 0.5) * 0.45);
                if (d > k * 0.95) continue;
                var c = p.At(x, y);
                if (c == 0 || IsSky(c)) continue;
                p.Set(x, y, Crystallized(c, x, y));
            }
        }

        if (k < 0.7) return;
        var rise = Clamp01((k - 0.7) / 0.3);
        Block(p, spot.X - 9, spot.Y + 1, 4, 3, -0.15, 61, rise, 0, false, Clear);
        Block(p, spot.X + 10, spot.Y + 1, 3, 3, 0.15, 62, rise, 0, false, Clear);
        var g = phase % 5;
        if (g < 0.4) Glint(p, spot.X - 8 + (int)(Rnd((int)(phase / 5), 63) * 16), spot.Y + 2 + (int)(Rnd((int)(phase / 5), 64) * 6), White, 1);
    }

    /// <summary>The Litostal lying on the wall for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Spot(l, ctx) is not { } spot) return;
        Patch(p, spot, 1, phase);
        Shards(p, spot);
        var glint = phase % 8;
        crystal.Paint(p, spot.X, spot.Y - R(CapTrialCrystal.H * Lying), Lying, 0, 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }
}
