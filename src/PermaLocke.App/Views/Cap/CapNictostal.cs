using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The ninth trial's Z-Crystal, the Nictostal, brought by Yveltal (2026-10-19, second prototype: the player's choice of two, their sprite,
/// flying). Its own film, starting <see cref="Lead"/> seconds before the film's clock:
/// <list type="number">
/// <item>Yveltal flies in from the right over the ruins and hangs there beating its wings;</item>
/// <item>it gathers Oblivion Wing: the water reddens, streaks of red and black energy rush into an orb at its mouth that swells and throbs;</item>
/// <item>it fires: impact frames, then a great red beam down into the ruins, layered from a black edge to a white core, two strands of dark
/// energy coiling round it, rings running down it, a ragged dark aura flickering along it; where it strikes, an explosion of red and black,
/// stone chips flying, a ring over the ground, the window shaking;</item>
/// <item>the drain: the life of the ruins rises out of them in green motes and is drawn up the beam into Yveltal (the move heals its user),
/// while round the strike the moss and weed wither grey and the bricks darken to a burnt black, spreading out, its edge smouldering;</item>
/// <item>the beam dies, the crystal comes down its path into an arch, and Yveltal flies off.</item>
/// </list>
/// The darkened, withered patch stays for the rest of the run (<see cref="PaintRest"/>).
/// </summary>
public static class CapNictostal
{
    /// <summary>The ninth trial's crystal.</summary>
    public const int Nictostal = 822;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 5.0;

    private const int Yveltal = 717;

    /// <summary>The arch of the risen storey it falls into (the player's choice).</summary>
    private const int ArchIndex = 2;

    private const double Lying = 0.4, Size = 0.8;

    private const double In = -4.8, Stop = -1.2, Charge = -0.8, Fire = 1.2, Cease = 3.4, Drop = 3.6, Hit = CapTrial.Hit, Off = 5.6, Gone = 8.4;

    // the beam, outside in, and its energy
    private const uint Ink = 0x140408u, Crimson = 0x6A0818u, Red = 0xD8203Cu, Bright = 0xFF4060u, Pink = 0xFF9AAAu, Core = 0xFFF0F2u,
        Murk = 0x2A0610u, Life = 0x7CF0A0u, LifeHi = 0xD8FFE4u;

    // what the drain leaves: dead growth, burnt brick, the smouldering edge
    private const uint Dead = 0x6E6656u, DeadHi = 0x8A8270u, DeadLo = 0x4E4840u, Burnt = 0x34303Cu, BurntHi = 0x46424Eu, BurntLo = 0x26222Cu,
        Ember = 0x8A1A2Au, Cinder = 0x3A1C26u;

    private const double PatchX = 32, PatchY = 24;

    /// <summary>Where the crystal lies: on the floor of one of the risen storey's arches; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _)
            ? (x0 + CapGimmighoulRuins.Arches[ArchIndex] + (CapGimmighoulRuins.ArchW / 2),
               floor + CapGimmighoulRuins.Storey - CapGimmighoulRuins.Plinth - R(CapTrialCrystal.H * Lying))
            : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (l.Panel != CapPanelKind.Deep || CapGimmighoulRuins.Anchor(l, ctx) is not var (_, floor, _) || Spot(l, ctx) is not { } spot) return;
        var clock = t + Lead;
        var hover = (X: spot.X + 22.0, Y: floor - 84.0);
        // its mouth on the flying sprite, from the drawing's middle
        var mouth = (X: hover.X + (2 * Size), Y: hover.Y - (14 * Size));
        var target = (X: (double)spot.X, Y: floor - 2.0);

        // the water reddening while it gathers and fires
        var murk = Span(Charge, Fire, t) * (1 - Span(Cease, Cease + 1.2, t));
        if (murk > 0) Tint(p, l, murk);

        // the patch it drains, spreading from the strike while the beam lasts
        var drain = Span(Fire + 0.1, Cease, t);
        if (drain > 0) Drained(p, spot.X, floor + 2, drain, t < Cease + 0.6, clock, CapShaymin.Spot(l, ctx));

        if (CritterArt.All.TryGetValue(Yveltal, out var art) && t >= In && t < Gone)
        {
            double x, y, tilt = 0;
            if (t < Stop)
            {
                var k = Ease(Span(In, Stop, t));
                x = l.Cols + 80 + ((hover.X - l.Cols - 80) * k);
                y = hover.Y - 34 + (34 * k) - (Math.Sin(k * Math.PI) * 12);
                tilt = 8 * (1 - k);
            }
            else if (t < Off)
            {
                (x, y) = (hover.X, hover.Y);
                // pushed back a little by the beam
                if (t >= Fire && t < Cease) x += 1 + ((int)(clock * 16) % 2);
            }
            else
            {
                var k = Ease(Span(Off, Gone, t));
                x = hover.X - (k * (hover.X + 90));
                y = hover.Y - (k * 46);
                tilt = 10 * k;
            }

            CapVisitors.Centred(p, art, clock, R(x), R(y), Size, false, 0xFFFFFF, 0, tilt);
        }

        if (t >= Charge && t < Fire) Gather(p, mouth, Span(Charge, Fire, t), clock);

        if (t >= Fire && t < Cease + 0.25)
        {
            // it reaches the ground in a few frames, and narrows to nothing when it stops
            var reach = Clamp01((t - Fire) / 0.15);
            var width = t < Cease ? 6 + ((int)(clock * 18) % 2) : 6 * (1 - ((t - Cease) / 0.25));
            var end = (X: mouth.X + ((target.X - mouth.X) * reach), Y: mouth.Y + ((target.Y - mouth.Y) * reach));
            Beam(p, mouth, end, width, clock, t - Fire);
            Orb(p, mouth.X, mouth.Y, 6, clock);
            if (reach >= 1 && t < Cease) Strike(p, target, t - Fire, clock);
            if (t < Cease) CapTrial.Shake(p, (t - Fire) % 0.5, 2, 0.5);
        }

        // the life drawn out of the ruins and up the beam
        if (t >= Fire + 0.3 && t < Cease + 0.3) Motes(p, target, mouth, t, clock);

        if (t >= Drop && t < Hit)
        {
            var f = Span(Drop, Hit, t);
            crystal.Paint(p, R(mouth.X + ((spot.X - mouth.X) * f)), R(mouth.Y + ((spot.Y - mouth.Y) * f * f)), Lying, 0, 0.9, -1);
        }

        if (t >= Hit)
        {
            var since = t - Hit;
            if (since < 0.4) CapTrial.Shake(p, since, 1.5, 0.4);
            Rest(p, spot, crystal, since, t);
        }

        // the impact frames, over everything: as it fires, and as it strikes
        ImpactFrames(p, t - Fire);
        ImpactFrames(p, t - Fire - 0.2);
    }

    /// <summary>The whole abyss's water reddened towards a dark crimson, flat.</summary>
    private static void Tint(CapSheet p, CapWorldLayout l, double k)
    {
        var a = (int)(90 * k);
        for (var y = l.SurfaceY + 2; y < l.Rows; y++)
        {
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, Murk, a);
        }
    }

    /// <summary>The gathering: streaks of red and black rushing in to its mouth, and the orb there swelling.</summary>
    private static void Gather(CapSheet p, (double X, double Y) mouth, double k, double clock)
    {
        for (var n = 0; n < 18; n++)
        {
            var a = (n * 0.349) + (n % 2 * 0.17);
            var run = ((clock * 1.6) + (n * 0.137)) % 1;
            var r = 34 * (1 - run);
            for (var s = 0; s < 4; s++)
            {
                var rr = r + (s * 1.2);
                Put(p, mouth.X + (Math.Cos(a) * rr), mouth.Y + (Math.Sin(a) * rr * 0.8), s == 0 ? (n % 3 == 0 ? Core : Bright) : n % 2 == 0 ? Red : Ink);
            }
        }

        Orb(p, mouth.X, mouth.Y, 1 + (k * 5), clock);
        // a ring throbbing out of it now and then
        var throb = (clock * 2.5) % 1;
        if (k > 0.3) CapTrial.Ring(p, mouth.X, mouth.Y, 4 + (throb * 14), 1, throb < 0.5 ? Bright : Crimson, 220 * (1 - throb), false);
    }

    /// <summary>The orb of energy at its mouth: a black edge, red, and a pale throbbing heart.</summary>
    private static void Orb(CapSheet p, double x, double y, double r, double clock)
    {
        var beat = r + (Math.Sin(clock * 20) > 0 ? 0.6 : 0);
        for (var dy = -(int)beat - 2; dy <= beat + 2; dy++)
        {
            for (var dx = -(int)beat - 2; dx <= beat + 2; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > beat + 1) continue;
                Put(p, x + dx, y + dy, d > beat ? Ink : d > beat * 0.7 ? Red : d > beat * 0.35 ? Pink : Core);
            }
        }
    }

    /// <summary>
    /// The beam: solid layers from a black edge through crimson, red and pink to a white core, a ragged dark aura flickering along its sides,
    /// two strands of dark energy coiling round it and rings running down it.
    /// </summary>
    private static void Beam(CapSheet p, (double X, double Y) a, (double X, double Y) b, double width, double clock, double age)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1) return;
        double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        for (var s = 0.0; s <= len; s += 0.25)
        {
            double cx = a.X + (ux * s), cy = a.Y + (uy * s);
            // the aura: tongues of dark flame running up each side, each side at its own pace
            var lickL = (int)Math.Round(1.5 + (1.5 * Math.Sin((s * 0.45) + (clock * 14))));
            var lickR = (int)Math.Round(1.5 + (1.5 * Math.Sin((s * 0.38) + (clock * 11) + 2)));
            for (var o = -width - 1 - lickL; o <= width + 1 + lickR; o++)
            {
                var d = Math.Abs(o);
                var lick = o < 0 ? lickL : lickR;
                uint c;
                if (d > width + 1) c = d == width + 1 + lick ? Ink : Crimson;
                else if (d > width) c = Ink;
                else if (d > width - 1) c = Crimson;
                else if (d > width * 0.6) c = Red;
                else if (d > width * 0.3) c = Bright;
                else c = d > 0.6 ? Pink : Core;
                Put(p, cx + (nx * o), cy + (ny * o), c);
            }

            // the two strands coiling round it, two cells thick, hidden behind it where they pass at the back
            for (var strand = 0; strand < 2; strand++)
            {
                var phase = (s * 0.26) - (clock * 9) + (strand * Math.PI);
                var off = Math.Sin(phase) * (width + 3);
                if (Math.Cos(phase) < 0 && Math.Abs(off) < width + 1) continue;
                for (var w = -1; w <= 1; w++) Put(p, cx + (nx * (off + w)), cy + (ny * (off + w)), w == 0 ? Crimson : Ink);
            }
        }

        // rings running down it, one every quarter of a second
        for (var k = 0; k < 6; k++)
        {
            var along = (((age * 4) + (k / 6.0 * 1.5)) % 1.5) / 1.5 * len;
            double cx = a.X + (ux * along), cy = a.Y + (uy * along);
            for (var o = -width - 4; o <= width + 4; o++)
            {
                // a band of light across it, bowed back at its ends, outlined in black
                var bow = Math.Abs(o) > width + 1 ? -1 : 0;
                double rx = cx + (nx * o) + (ux * bow), ry = cy + (ny * o) + (uy * bow);
                Put(p, rx, ry, Core);
                Put(p, rx - ux, ry - uy, Ink);
                Put(p, rx + ux, ry + uy, Pink);
            }
        }
    }

    /// <summary>Where the beam strikes: a throbbing burst of red and black, dark spikes thrown out, stone chips flying, a ring over the ground.</summary>
    private static void Strike(CapSheet p, (double X, double Y) at, double age, double clock)
    {
        var frame = (int)(clock * 15);
        var r = 7 + (frame % 2);
        for (var dy = -r; dy <= 2; dy++)
        {
            for (var dx = -r - 3; dx <= r + 3; dx++)
            {
                var d = Math.Sqrt((dx * dx * 0.6) + (dy * dy));
                if (d > r) continue;
                Put(p, at.X + dx, at.Y + dy, d > r - 1 ? Ink : d > r * 0.65 ? Red : d > r * 0.35 ? Pink : Core);
            }
        }

        for (var n = 0; n < 10; n++)
        {
            var a = -Math.PI * (0.05 + (Rnd(n + frame, 41) * 0.9));
            var length = 8 + (Rnd(n + frame, 43) * 14);
            for (var d = r - 2; d < r + length; d++)
            {
                Put(p, at.X + (Math.Cos(a) * d * 1.3), at.Y + (Math.Sin(a) * d), d < r + (length / 2) ? Red : Ink);
            }
        }

        for (var n = 0; n < 16; n++)
        {
            var life = 0.5 + (Rnd(n, 45) * 0.4);
            var born = Rnd(n, 47) * 2;
            var fly = (age - born) % life;
            if (fly < 0) continue;
            var a = -Math.PI * (0.1 + (Rnd(n, 49) * 0.8));
            var v = 20 + (Rnd(n, 51) * 30);
            double x = at.X + (Math.Cos(a) * v * fly), y = at.Y + (Math.Sin(a) * v * fly) + (60 * fly * fly);
            Put(p, x, y, n % 3 == 0 ? 0x5C5C52u : 0x8C8C7Cu);
            Put(p, x + 1, y, 0x3A3A34u);
        }

        var ring = (age * 2) % 1;
        CapTrial.Ring(p, at.X, at.Y + 1, 6 + (ring * 34), 0.3, ring < 0.5 ? Bright : Crimson, 230 * (1 - ring), false);
    }

    /// <summary>The life of the ruins drawn out: green motes rising off the patch, curving into the beam and running up it to Yveltal.</summary>
    private static void Motes(CapSheet p, (double X, double Y) from, (double X, double Y) to, double t, double clock)
    {
        for (var n = 0; n < 22; n++)
        {
            var life = 1.0 + (Rnd(n, 61) * 0.5);
            var u = (((t - Fire) + (Rnd(n, 63) * life)) % life) / life;
            // off the patch, round into the beam, up it
            var sx = from.X + ((Rnd(n, 65) - 0.5) * PatchX * 1.6);
            var sy = from.Y + 6 + (Rnd(n, 67) * 10);
            double x, y;
            if (u < 0.35)
            {
                var k = u / 0.35;
                x = sx + ((from.X - sx) * k * k);
                y = sy - (k * 14);
            }
            else
            {
                var k = (u - 0.35) / 0.65;
                x = from.X + ((to.X - from.X) * k) + (Math.Sin((k * 9) + n) * 3);
                y = from.Y - 14 + ((to.Y - from.Y + 14) * k);
            }

            Put(p, x, y, (int)(clock * 10 + n) % 3 == 0 ? LifeHi : Life);
        }
    }

    /// <summary>
    /// The drained patch round the strike, inside a ragged oval: the moss and weed withered grey-brown, the bricks and their mortar darkened to a
    /// burnt black after their own light; while it spreads its edge smoulders red. <paramref name="k"/> grows it from the middle. The Fitostal
    /// lying in it (<paramref name="keep"/>) stays green, only flecked with a few small black spots (asked by the player).
    /// </summary>
    private static void Drained(CapSheet p, int cx, int cy, double k, bool spreading, double clock, (int X, int Y)? keep)
    {
        for (var dy = -(int)PatchY; dy <= PatchY; dy++)
        {
            for (var dx = -(int)PatchX; dx <= PatchX; dx++)
            {
                var d = Math.Sqrt(((dx / PatchX) * (dx / PatchX)) + ((dy / PatchY) * (dy / PatchY))) + ((Rnd(cx + dx, cy + dy) - 0.5) * 0.22);
                if (d > k) continue;
                int x = cx + dx, y = cy + dy;
                if (!p.Solid(x, y)) continue;
                var c = p.At(x, y);
                if (keep is { } f && Math.Abs(x - f.X) <= 7 && y >= f.Y - 5 && y <= f.Y + 6)
                {
                    // the green crystal: a black fleck here and there, never two side by side
                    if (((c >> 8) & 255) > ((c >> 16) & 255) + 20 && CapArt.Hash(x * 3, y * 5) % 3 == 0 && (x + y) % 2 == 0) p.Set(x, y, BurntLo);
                    continue;
                }

                int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), b = (int)(c & 255);
                var lum = ((r * 3) + (g * 6) + b) / 10;
                var green = g > r + 20 && g > b + 12;
                var stone = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 48;
                if (!green && !stone) continue;
                // the edge smoulders while it spreads, and stays a dark cinder after
                if (d > k - 0.07 && k < 1)
                {
                    p.Set(x, y, spreading && (CapArt.Hash(x + (int)(clock * 8), y) % 3 != 0) ? Ember : Cinder);
                    continue;
                }

                p.Set(x, y, green ? (lum > 150 ? DeadHi : lum > 90 ? Dead : DeadLo) : lum > 120 ? BurntHi : lum > 70 ? Burnt : BurntLo);
            }
        }
    }

    /// <summary>Two impact frames (a twentieth of a second each): the scene turned to black and pale red, then its light turned inside out.</summary>
    private static void ImpactFrames(CapSheet p, double age)
    {
        if (age < 0 || age >= 0.1) return;
        var frame = (int)(age / 0.05);
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (!p.Solid(x, y)) continue;
                var c = p.At(x, y);
                var lum = ((((c >> 16) & 255) * 3) + (((c >> 8) & 255) * 6) + (c & 255)) / 10;
                p.Set(x, y, frame == 0 ? (lum > 96 ? Ink : Core) : lum > 96 ? Red : Ink);
            }
        }
    }

    private static double Ease(double k) => k * k * (3 - (2 * k));

    private static void Rest(CapSheet p, (int X, int Y) spot, CapTrialCrystal crystal, double since, double phase)
    {
        var glint = phase % 7;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>The Nictostal in its arch for the rest of the run, in the middle of the patch Yveltal drained.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (CapGimmighoulRuins.Anchor(l, ctx) is not var (_, floor, _) || Spot(l, ctx) is not { } spot) return;
        Drained(p, spot.X, floor + 2, 1, false, phase, CapShaymin.Spot(l, ctx));
        Rest(p, spot, crystal, 30, phase);
    }
}
