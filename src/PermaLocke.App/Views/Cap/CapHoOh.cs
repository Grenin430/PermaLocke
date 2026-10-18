using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The fourth trial's Z-Crystal, the Pirostal, brought to the Gimmighoul ruins by Ho-Oh (2026-10-18, first simple prototype: the player's
/// choice and sprite). Its own film, starting <see cref="Lead"/> seconds before the film's clock: the Ultra Wormhole beats and Ho-Oh bursts out
/// of it, beating its wings, and flies left across the party's sky and on over the abyss's, a rainbow left in the sky behind it; over the
/// ruins it lets fall a rainbow feather wrapped in sacred fire with the crystal in it, which sways down like a leaf through the sky and the sea
/// onto the top of the small wall on the right (the spot the player marked); there it flares up in fire of seven colours, burns away to
/// sparks and leaves the crystal lying on the stone. The rainbow fades. It stays there for the rest of the run (<see cref="PaintRest"/>).
/// </summary>
public static class CapHoOh
{
    /// <summary>The fourth trial's crystal.</summary>
    public const int Pirostal = 808;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 1.5;

    private const int Art = 20250;
    private const double Size = 0.6, Lying = 0.4;

    private const double Beat1 = -1.3, Beat2 = -1.0, Emerge = -0.6, OffParty = 1.0, OnDeep = 1.1, OffDeep = 4.6, Hit = CapTrial.Hit, Flare = 1.4;

    private const uint White = 0xFFFFFFu, Foam = 0xCFF4FFu;

    /// <summary>The seven colours of its fire and its rainbow, red to violet.</summary>
    private static readonly uint[] Rainbow = [0xFF3B30u, 0xFF9230u, 0xFFE040u, 0x5AD84Au, 0x3AB8F0u, 0x4A62E8u, 0xA050E0u];

    /// <summary>Where it lies: on the top of the small wall at the right of the ruins; null when the ruins are not drawn or are too narrow.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, width) && width > 200
            ? (x0 + 191, floor - CapGimmighoulRuins.Surface(191) - R(CapTrialCrystal.H * Lying) + 1)
            : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (!CritterArt.All.TryGetValue(Art, out var hooh)) return;
        var deep = l.Panel == CapPanelKind.Deep;
        if (!deep)
        {
            if (CapFarSky.WormholeAt(l) is not { } core) return;
            foreach (var beat in (ReadOnlySpan<double>)[Beat1, Beat2])
            {
                var k = Math.Exp(-Math.Pow((t - beat) / 0.1, 2));
                CapTrial.Shine(p, core.X, core.Y, 18 + (10 * k), 15 + (8 * k), Rainbow[1], 170 * k);
            }

            var b = t - Emerge;
            if (b >= 0 && b < 0.6)
            {
                var k = 1 - (b / 0.6);
                CapTrial.Shine(p, core.X, core.Y, 26 * k, 20 * k, White, 255 * k);
                for (var c = 0; c < Rainbow.Length; c++) CapTrial.Ring(p, core.X, core.Y, 5 + (b * 90) + (c * 2), 0.6, Rainbow[c], 200 * k);
            }

            if (t < Emerge || t > OffParty + 4) return;
            (double X, double Y) from = (core.X, core.Y), to = (-60, l.SurfaceY * 0.5);
            Trail(p, l, t, u => Lerp(from, to, u), Emerge, OffParty);
            if (t < OffParty)
            {
                var u = (t - Emerge) / (OffParty - Emerge);
                var (x, y) = Lerp(from, to, u);
                var grow = Clamp01(b / 0.4);
                CapVisitors.Centred(p, hooh, t, R(x), R(y + (Math.Sin(t * 4) * 2)), Size * (0.2 + (0.8 * grow)), false, White, grow < 1 ? 1 - grow : 0, -5);
            }

            return;
        }

        if (Spot(l, ctx) is not { } spot) return;
        (double X, double Y) enter = (l.Cols + 70, l.SurfaceY * 0.5), leave = (-80, l.SurfaceY * 0.55);
        // it lets the feather go as it passes over the spot
        var drop = OnDeep + ((enter.X - spot.X) / (enter.X - leave.X) * (OffDeep - OnDeep));
        Trail(p, l, t, u => Lerp(enter, leave, u), OnDeep, OffDeep);
        if (t >= OnDeep && t < OffDeep)
        {
            var u = (t - OnDeep) / (OffDeep - OnDeep);
            var (x, y) = Lerp(enter, leave, u);
            CapVisitors.Centred(p, hooh, t, R(x), R(y + (Math.Sin(t * 4) * 2)), Size, false, White, 0, -5 + (Math.Sin(t * 2) * 3));
        }

        // the feather: let go over the ruins, swaying down like a leaf through the sky and the sea, wrapped in sacred fire, the crystal in it
        var dropAt = Lerp(enter, leave, (drop - OnDeep) / (OffDeep - OnDeep));
        (double X, double Y) feather = (spot.X, spot.Y);
        if (t >= drop && t < Hit)
        {
            // falling as a feather does: swinging from side to side, tipping up at each end of its swing, slower as it goes
            var u = (t - drop) / (Hit - drop);
            var swing = Math.Sin(u * Math.PI * 4);
            feather = (Math.Min(dropAt.X, spot.X - 30) + ((spot.X - 15 - Math.Min(dropAt.X, spot.X - 30)) * u) + (swing * 10 * (1 - (u * 0.7))), dropAt.Y + 16 + ((spot.Y - 8 - dropAt.Y - 16) * u) - (Math.Abs(swing) * 3));
            var tilt = 0.15 + (Math.Cos(u * Math.PI * 4) * 0.45);
            for (var n = 0; n < 14; n++)
            {
                var age = ((t * 1.4) + (n / 14.0)) % 1;
                Put(p, feather.X + ((Rnd(n, 31) - 0.5) * 30), feather.Y - 4 - (age * 18) + ((Rnd(n, 32) - 0.5) * 6), Rainbow[n % 7], 230 * (1 - age));
            }

            CapTrial.Shine(p, feather.X, feather.Y, 22, 14, Rainbow[1], 70);
            SacredFire(p, feather.X, feather.Y, t, 0.45);
            // its tip to the left, its root (and the crystal) towards the wall
            Feather(p, feather.X, feather.Y, Math.PI + tilt);
            // the crystal tied at its root
            double rootX = feather.X + (Math.Cos(tilt) * 15), rootY = feather.Y + (Math.Sin(tilt) * 15);
            crystal.Paint(p, R(rootX), R(rootY), 0.3, 0, 0.7, -1);
            if (Math.Abs(feather.Y - l.SurfaceY) < 3) CapTrial.Shine(p, feather.X, l.SurfaceY, 14, 4, White, 200);
        }

        if (t >= Hit)
        {
            var f = t - Hit;
            if (f < Flare + 1.2) Flare_(p, spot, f);

            Lie(p, spot, crystal, t, f < 0.4 ? 0.9 : 0.3, Clamp01((f - 0.3) / 1.2));
        }
    }

    private static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double u) =>
        (a.X + ((b.X - a.X) * u), a.Y + ((b.Y - a.Y) * u) + (Math.Sin(u * Math.PI) * 10));

    /// <summary>
    /// The rainbow it leaves along its way: a ribbon of the seven colours, two cells each, red on top, coming out from under its tail, waving
    /// a little and widening as it ages; it thins out (dithered, never blurred) from its edges and its oldest end, and glints twinkle along it.
    /// </summary>
    private static void Trail(CapSheet p, CapWorldLayout l, double t, Func<double, (double X, double Y)> at, double from, double to)
    {
        if (t < from) return;
        var reached = Clamp01((t - from) / (to - from));
        var span = to - from;
        for (var s = 0; s <= 700; s++)
        {
            var u = s / 700.0 * reached;
            var age = (reached - u) * span;
            if (age < 0.06) continue;
            var keep = Math.Min(1, (age - 0.06) * 6) * (1 - Clamp01((age - 1.6) / 2.6)) * (1 - Clamp01((t - to - 1.5) / 2.5));
            if (keep <= 0) continue;
            var (x, y) = at(u);
            var (x2, y2) = at(Math.Min(1, u + 0.004));
            double dx = x2 - x, dy = y2 - y, length = Math.Max(0.001, Math.Sqrt((dx * dx) + (dy * dy)));
            double nx = -dy / length, ny = dx / length;
            if (ny < 0) { nx = -nx; ny = -ny; }
            var wave = Math.Sin((u * 40) - (t * 3)) * 1.5;
            var widen = 1 + (Math.Min(age, 2) * 0.12);
            for (var o = 0.0; o < 14 * widen; o += 0.7)
            {
                var band = Math.Min(6, (int)(o / (2 * widen)));
                var edge = Math.Min(o, (14 * widen) - o) / (2 * widen);
                var px = x + (nx * (o + 6 + wave));
                var py = y + (ny * (o + 6 + wave));
                var solid = keep * Math.Min(1, 0.45 + edge);
                if (Bayer[R(py) & 3, R(px) & 3] >= solid * 16) continue;
                p.Set(R(px), R(py), Rainbow[band]);
            }
        }

        // glints twinkling along it
        for (var n = 0; n < 14; n++)
        {
            var u = Rnd(n, 41) * reached;
            var age = (reached - u) * span;
            var twinkle = Math.Sin((t * 6) + (n * 1.7));
            if (twinkle < 0.6 || age < 0.1 || age > 3.5) continue;
            var (x, y) = at(u);
            CapTrial.Star(p, x, y + 6 + (Rnd(n, 42) * 14), White, 255 * (twinkle - 0.6) / 0.4, 1);
        }
    }

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <summary>A smooth noise in 0..1 over a grid of hashed values: what the flames are made of.</summary>
    private static double Noise(double x, double y)
    {
        int gx = (int)Math.Floor(x), gy = (int)Math.Floor(y);
        double fx = x - gx, fy = y - gy;
        fx = fx * fx * (3 - (2 * fx));
        fy = fy * fy * (3 - (2 * fy));
        double V(int a, int b) => (CapArt.Hash(a + 911, b * 7919) % 1000) / 1000.0;
        var top = V(gx, gy) + ((V(gx + 1, gy) - V(gx, gy)) * fx);
        var bottom = V(gx, gy + 1) + ((V(gx + 1, gy + 1) - V(gx, gy + 1)) * fx);
        return top + ((bottom - top) * fy);
    }

    /// <summary>
    /// Sacred fire on the stone where the crystal lands: tongues of flame narrowing as they rise, made of noise running upwards so they lick
    /// and flicker, a white and pale yellow heart, yellow, orange, and a rim that runs through the seven colours; it leaps up, burns, and sinks
    /// down; embers rise from it with little tails, and the stone glows with its heat.
    /// </summary>
    private static void Flare_(CapSheet p, (int X, int Y) spot, double f)
    {
        var height = f < 0.15 ? 34 * EaseOut(f / 0.15) : f < Flare * 0.6 ? 34 : 34 * Math.Max(0, 1 - ((f - (Flare * 0.6)) / (Flare * 0.6)));
        const double Width = 16;
        if (height > 1)
        {
            for (var y = spot.Y - (int)height - 4; y <= spot.Y + 1; y++)
            {
                var h = (spot.Y - y) / height;
                for (var x = spot.X - (int)Width - 2; x <= spot.X + Width + 2; x++)
                {
                    var dx = x - spot.X;
                    var narrow = Width * (1 - (Math.Clamp(h, 0, 1) * 0.75));
                    var env = 1 - (Math.Abs(dx) / Math.Max(1, narrow));
                    if (env <= 0) continue;
                    var tongues = 0.65 + (0.35 * Math.Sin((dx * 0.7) + (f * 13)));
                    var n = Noise((x * 0.32) + 3, (y * 0.22) + (f * 9));
                    var v = (env * tongues * Math.Pow(Math.Max(0, 1 - h), 0.55) * 1.25) + ((n - 0.5) * 0.7) - (Math.Max(0, h) * 0.35);
                    if (v < 0.16) continue;
                    uint c = v > 0.9 ? White : v > 0.72 ? 0xFFF2A0u : v > 0.52 ? 0xFFC93Au : v > 0.34 ? 0xFF7A22u : Rainbow[(int)Math.Abs(Math.Floor((x * 0.35) + (y * 0.2) - (f * 10))) % 7];
                    p.Set(x, y, c);
                }
            }
        }

        // embers rising with a little tail, swaying
        for (var n = 0; n < 30; n++)
        {
            var age = f - (Rnd(n, 11) * Flare);
            if (age < 0 || age > 1.2) continue;
            var x = spot.X + ((Rnd(n, 12) - 0.5) * 26) + (Math.Sin((age * 5) + n) * 3);
            var y = spot.Y - 6 - (age * (30 + (Rnd(n, 13) * 20)));
            var c = n % 3 == 0 ? Rainbow[n % 7] : n % 2 == 0 ? 0xFFE070u : 0xFF9A3Cu;
            Put(p, x, y, c, 255 * (1 - (age / 1.2)));
            Put(p, x - (Math.Cos((age * 5) + n) * 0.8), y + 1, CapSheet.Mix(c, 0x401010u, 0.4), 160 * (1 - (age / 1.2)));
        }

        // the stone glowing with the heat, cooling
        var heat = Math.Max(0, 1 - (f / (Flare + 1.2)));
        CapTrial.Shine(p, spot.X, spot.Y + 2, 20, 6, 0xFF7A22u, 170 * heat);
    }

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    /// <summary>Sacred fire round the feather: little flames in turn of each colour flickering up round it.</summary>
    private static void SacredFire(CapSheet p, double cx, double cy, double t, double k)
    {
        for (var n = 0; n < 16; n++)
        {
            var age = ((t * 2.5) + (n / 16.0)) % 1;
            var a = n / 16.0 * 2 * Math.PI;
            var x = cx + (Math.Cos(a) * 17) + (Math.Sin((age * 7) + n) * 1.5);
            var y = cy + (Math.Sin(a) * 6) - (age * 8);
            Put(p, x, y, age < 0.25 ? White : Rainbow[n % 7], 230 * (1 - age) * k);
        }

        CapTrial.Shine(p, cx, cy, 12, 14, Rainbow[1], 110 * k);
    }

    /// <summary>
    /// The rainbow feather, big enough to read: a quill, a curved shaft, the vane wider on one side than the other with two splits in it, the
    /// seven colours running from root to tip (dithered where they meet), barbs slanting along it, the near side lit; a dark edge round it.
    /// <paramref name="angle"/> turns it (clockwise); its tip is to its right before it turns.
    /// </summary>
    private static void Feather(CapSheet p, double cx, double cy, double angle)
    {
        const double Half = 17;
        double co = Math.Cos(angle), si = Math.Sin(angle);

        // 0 outside, 1 vane, 2 shaft, 3 quill
        int Part(int x, int y, out double u, out double w)
        {
            double dx = x - cx, dy = y - cy;
            u = (co * dx) + (si * dy);
            var v = (-si * dx) + (co * dy);
            var shaft = 0.05 * ((u * u) - (Half * Half)) / Half;
            w = v - shaft;
            if (u < -Half - 4 || u > Half) return 0;
            if (Math.Abs(w) < 0.6) return u < -Half * 0.72 ? 3 : 2;
            var along = (u + (Half * 0.72)) / (Half * 1.72);
            if (along < 0 || along > 1) return 0;
            var shape = Math.Pow(Math.Sin(Math.PI * Math.Min(1, along * 1.08)), 0.55);
            var width = (w < 0 ? 6 : 4.2) * shape;
            if (Math.Abs(w) > width) return 0;

            // two splits, slanting along the barbs
            var slant = u - (Math.Abs(w) * 0.9);
            if (Math.Abs(w) > 1.6 && ((w < 0 && Math.Abs(slant - 2) < 0.55) || (w > 0 && Math.Abs(slant + 5) < 0.55))) return 0;
            return 1;
        }

        for (var y = (int)cy - 22; y <= cy + 22; y++)
        {
            for (var x = (int)cx - 22; x <= cx + 22; x++)
            {
                var part = Part(x, y, out var u, out var w);
                if (part == 0) continue;
                var edge = Part(x - 1, y, out _, out _) == 0 || Part(x + 1, y, out _, out _) == 0 || Part(x, y - 1, out _, out _) == 0 || Part(x, y + 1, out _, out _) == 0;
                uint c;
                if (part >= 2)
                {
                    c = part == 3 ? 0xE8DCC0u : 0xFFF6E0u;
                    if (edge) c = 0x8A7A60u;
                }
                else
                {
                    // the colour of where it is along the feather, dithered into the next
                    var along = Clamp01((u + (Half * 0.72)) / (Half * 1.72)) * 6.999;
                    var band = (int)along;
                    if (band < 6 && (along - band) > 0.6 && ((x + y) & 1) == 0) band++;
                    var colour = Rainbow[band];
                    var barb = ((int)Math.Floor(u - (Math.Abs(w) * 0.9) + 40) % 2) == 0;
                    c = edge ? CapSheet.Mix(colour, 0x200C10u, 0.55)
                        : w < -3.5 ? CapSheet.Mix(colour, White, 0.35)
                        : barb ? CapSheet.Mix(colour, 0x200C10u, 0.18)
                        : colour;
                }

                p.Set(x, y, c);
            }
        }
    }

    /// <summary>The Pirostal lying on the top of the wall, its symbol warm, a glint over it now and then.</summary>
    private static void Lie(CapSheet p, (int X, int Y) spot, CapTrialCrystal crystal, double phase, double burn, double formed = 1)
    {
        Magma(p, spot, phase, formed);
        var glint = phase % 8;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, burn, glint < 0.6 ? glint / 0.6 : -1);
    }

    /// <summary>
    /// The stone where it lies, turned to magma by its fire (2026-10-18, asked by the player: each crystal changes the ruins by its type): a
    /// crust of dark basalt round it on the top of the wall and down its face, cracked with lava that throbs from yellow to deep red, its
    /// heart under the crystal molten; lava dripping slowly down the face of the wall; the stone glowing with its heat; bubbles of steam rising.
    /// <paramref name="formed"/> grows it out from under the crystal after the flare.
    /// </summary>
    private static void Magma(CapSheet p, (int X, int Y) spot, double phase, double formed)
    {
        if (formed <= 0) return;
        var top = spot.Y + R(CapTrialCrystal.H * Lying) - 1;
        double rx = 11 * formed, ry = 6 * formed;
        CapTrial.Shine(p, spot.X, top + 2, 16 * formed, 7 * formed, 0xFF6A1Cu, 70 + (30 * Math.Sin(phase * 2)));
        for (var dy = -1; dy <= ry; dy++)
        {
            for (var dx = -(int)rx; dx <= rx; dx++)
            {
                var d = ((dx / Math.Max(1, rx)) * (dx / Math.Max(1, rx))) + ((dy / Math.Max(1, ry)) * (dy / Math.Max(1, ry)));
                // (the wall's left edge is nine cells to the left of the crystal)
                if (d > 1 || dx < -8 || dx > 12) continue;
                int x = spot.X + dx, y = top + dy;
                var n = Noise((x * 0.55) + 7, (y * 0.7) + (phase * 0.25));
                var pulse = 0.5 + (0.5 * Math.Sin((phase * 2.2) - (Math.Sqrt(d) * 3)));
                uint c;
                if (d < 0.18) c = n > 0.55 ? 0xFFC93Au : n > 0.35 ? 0xFF7A22u : 0x3A2420u;
                else if (Math.Abs(n - 0.5) < 0.06 + (0.05 * (1 - d))) c = pulse > 0.66 ? 0xFFC93Au : pulse > 0.33 ? 0xFF7A22u : 0xC83A12u;
                else c = d > 0.8 ? 0x4A3028u : ((x + y) % 5 == 0 ? 0x3E2622u : 0x2A1C1Au);
                p.Set(x, y, c);
            }
        }

        // lava dripping down the face of the wall, slowly, and starting again
        for (var i = 0; i < 3; i++)
        {
            var dx = i == 0 ? -7 : i == 1 ? 1 : 6;
            if (Math.Abs(dx) > rx) continue;
            var k = ((phase * 0.22) + (i * 0.37)) % 1;
            var length = 2 + (k * 7);
            for (var d = 0; d < length; d++) p.Set(spot.X + dx, top + (int)ry + d, d > length - 1.5 ? 0xFFE070u : d > length - 3 ? 0xFF7A22u : 0xA0280Eu);
        }

        // steam rising off it
        for (var n = 0; n < 5; n++)
        {
            var age = ((phase * 0.5) + (n / 5.0)) % 1;
            Put(p, spot.X + ((n - 2) * 4) + (Math.Sin((age * 7) + n) * 1.5), top - 2 - (age * 22), n % 2 == 0 ? 0xE8F4FFu : 0xCFF4FFu, 190 * (1 - age) * formed);
        }
    }

    /// <summary>The Pirostal on the wall for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Spot(l, ctx) is { } spot) Lie(p, spot, crystal, phase, 0.3);
    }
}
