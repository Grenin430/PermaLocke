namespace PermaLocke.App.Views;

/// <summary>
/// The Pokévial in the party's window (2026-10-16, prototype imagenes/revision/prototipo_vial_overlay/vial3d.js, approved): the flask of the bag
/// icon with volume, rendered once from a small 3D model (a ball red over white with its band and its button, a glass neck, a cork) into cells
/// of the overlay, tilted to the right and leaning to the viewer so the cork's top shows, five tones per material, an ink outline and the icon's
/// gold aura. It stands in the sand below and to the right of the cap's sign, its foot sunk in it. The window of the button holds the medicine:
/// as full as the uses left of the most; the neck only while it is full. The uses in a gold number on the white of the ball.
/// </summary>
/// <remarks>
/// Used: the cork strains, pops in a burst of rays, spins high with a blur and drops back into the neck, squashing, foam welling out meanwhile; the medicine leaps out in a ribbon to each Pokémon of the party; on landing a crown of drops and
/// a ring of foam, the medicine runs down the Pokémon, a white flash, sparkles, a heart and crosses rising, a light along its HP bar; the window
/// sinks and the number turns. A function of the data and the moment: no clock and no random of its own.
/// </remarks>
public sealed class CapVial
{
    private const double Ball = 11.5, Roll = -0.36, Pitch = 0.3;
    private const double NeckFrom = Ball * 0.8, NeckTo = Ball + 5.5, NeckR = 2.9, CorkFrom = Ball + 5.0, CorkTo = Ball + 9.2, CorkR = 3.5;

    private static readonly uint[] Red = [0x680C1E, 0xA01A2A, 0xD83236, 0xF6645C, 0xFFA89E];
    private static readonly uint[] White = [0x808496, 0xACB0C2, 0xD6D8E4, 0xEEF0F8, 0xFFFFFF];
    private static readonly uint[] Pink = [0x781A50, 0xB02E70, 0xE45692, 0xFA8CBA, 0xFFCEE4];
    private static readonly uint[] Glass = [0x56709C, 0x7CA0C8, 0xAACEEA, 0xD2EAFA, 0xF8FCFF];
    private static readonly uint[] Cork = [0x52301A, 0x7C4E2A, 0xA87446, 0xCC9E6A, 0xE8C898];
    private static readonly uint[] Band = [0x14101C, 0x14101C, 0x2C2838, 0x2C2838, 0x3C3848];
    private const uint Ink = 0x14101C, Aura = 0xFFD65A, Gold = 0xFFD65C, GoldHi = 0xFFF6BE, GoldDeep = 0xB07416, WhiteC = 0xFFFFFF, Heal = 0xBEFFC8, Star = 0xFFE680;
    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    private static readonly string[][] Digits =
    [
        [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."], ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"], [".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."]
    ];

    private readonly record struct Cell(int X, int Y, uint C, bool Ball);

    private static readonly Dictionary<int, Cell[]> Models = [];

    /// <summary>Where the window looks: at the viewer, a little to the left.</summary>
    private static readonly (double X, double Y, double Z) WindowDir = Norm((-0.18, -0.05, 1));

    private static readonly (double X, double Y, double Z) Up = Turn((0, 1, 0));

    /// <summary>The use lasts this long, from the moment it was used.</summary>
    public const double UseLength = 3.4;

    private uint[] _sand = [];
    private readonly int[] _tops = new int[45];

    /// <summary>The party's members the medicine goes to: the middle of each sprite, its box, and the row of its HP bar.</summary>
    public readonly record struct Target(int X, int Y, CapRect Sprite, int BarX, int BarY, int BarLength);

    /// <summary>Paints the flask (and, while it lasts, its use) into the party's window, right of the sign.</summary>
    public void Paint(CapSheet p, CapRect sign, int ground, CapVialState vial, IReadOnlyList<Target> targets, double t)
    {
        var s = t - vial.UsedAt;
        var using_ = s is >= 0 and < UseLength && vial.From > vial.Uses;
        var max = Math.Max(1, vial.Max);
        var level = !using_ ? vial.Uses / (double)max
            : s < 0.4 ? vial.From / (double)max
            : vial.From / (double)max - ((vial.From - vial.Uses) / (double)max * EaseOut((s - 0.4) / 1.4));

        var cx = sign.Left + sign.Width + 13;
        // At the level of the trials' crystals (2026-10-16): their sand line is ten rows above the bottom of the window, and the ball sinks three cells into it.
        var line = p.H - 10;
        var cy = line + 3 - (int)Math.Round(Ball);
        var shake = using_ && s < 0.35 ? ((int)(s * 24) % 2 == 0 ? -1 : 1) : 0;
        var corkOn = !using_ || s < 0.08 || s >= 3.0;

        // The sand in front of its foot, kept to be drawn back over it: its surface read column by column before the flask is drawn.
        const int Left = -22, Right = 22, Depth = 16;
        if (_sand.Length < (Right - Left + 1) * Depth) _sand = new uint[(Right - Left + 1) * Depth];
        for (var dx = Left; dx <= Right; dx++)
        {
            var top = line - 3 + (int)Math.Round(Math.Sin((cx + dx) * 0.45));
            _tops[dx - Left] = top;
            for (var k = 0; k < Depth; k++) _sand[((dx - Left) * Depth) + k] = top >= 0 && p.Solid(cx + dx, top + 3 + k) ? p.At(cx + dx, top + 3 + k) | 0xFF000000u : 0;
        }

        foreach (var c in Model(level, corkOn)) p.Set(cx + c.X + shake, cy + c.Y, c.C);

        for (var dx = Left; dx <= Right; dx++)
        {
            var top = _tops[dx - Left];
            if (top < 0) continue;
            for (var k = 0; k < Depth; k++)
            {
                var kept = _sand[((dx - Left) * Depth) + k];
                if (kept != 0) p.Set(cx + dx, top + 3 + k, kept & 0xFFFFFF);
            }
        }

        // Bubbles in the window, and now and then a glint across the ball.
        if (level > 0.2)
        {
            for (var n = 0; n < 3; n++)
            {
                var life = ((t * 0.6) + (n * 0.33)) % 1;
                if (life > 0.85) continue;
                p.Set(cx + (int)Math.Round(WindowDir.X * Ball * 0.98) - 2 + (n * 2) + shake, cy - (int)Math.Round(WindowDir.Y * Ball * 0.98) + 3 - (int)Math.Round(life * 6 * Math.Clamp(level / 0.8, 0, 1)), WhiteC, 200);
            }
        }

        var g = (t % 5) / 0.5;
        if (g < 1)
        {
            foreach (var c in Model(level, corkOn))
            {
                if (c.Ball && c.C != Ink && Math.Abs(c.X - (-Ball + (g * 2 * Ball)) + (c.Y * 0.5)) < 0.6) p.Set(cx + c.X + shake, cy + c.Y, WhiteC, 160);
            }
        }

        // Sparkles twinkling round the flask, as in the bag icon: four-pointed stars of pale gold, each on its own beat.
        for (var n = 0; n < Glints.Length; n++)
        {
            var life = ((t / 2.6) + (n * 0.29)) % 1;
            var size = life < 0.12 ? 1 : life < 0.3 ? 2 : life < 0.42 ? 1 : life < 0.5 ? 0 : -1;
            if (size >= 0) Twinkle(p, cx + Glints[n].X + shake, cy + Glints[n].Y, size);
        }

        var corkAt = (X: cx + (Up.X * (CorkFrom + CorkTo) / 2), Y: cy - (Up.Y * (CorkFrom + CorkTo) / 2));
        if (using_) Use(p, s, corkAt, targets, shake);
        Number(p, cx + 6 + shake, cy + 3, using_ && s < 2.4 ? vial.From : vial.Uses, using_ && s is >= 2.4 and < 2.9 ? (s - 2.4) / 0.5 : -1);
    }

    // ---------------------------------------------------------------- the use

    private static void Use(CapSheet p, double s, (double X, double Y) mouth, IReadOnlyList<Target> targets, int shake)
    {
        var (mx, my) = mouth;

        // The cork: pushed up by the pressure, then the pop (a burst of rays), a high spinning flight with its blur, a glint at the top, back into
        // the neck squashing and bouncing, and a ring of sparkles when it seats. Foam wells up out of the open neck all the while.
        var rest = Math.Atan2(Up.X, Up.Y);
        if (s is >= 0.08 and < 3.0)
        {
            if (s < 0.35)
            {
                var push = (s - 0.08) / 0.27;
                CorkAt(p, mx + shake, my - Math.Round(push * push * 2), rest + (Math.Sin(s * 70) * 0.12), 1, 1, 0);
            }
            else if (s < 2.6)
            {
                for (var ghost = 2; ghost >= 0; ghost--)
                {
                    var u = Math.Clamp(((s - 0.35) / 2.25) - (ghost * 0.025), 0, 1);
                    var (fx, fy) = (mx + Math.Round(Math.Sin(u * Math.PI) * 8), my - Math.Round(Math.Sin(u * Math.PI) * 26));
                    CorkAt(p, fx, fy, rest + (Math.PI * 6 * Smooth(u)), 1, 1, ghost);
                    if (ghost == 0 && Math.Abs(u - 0.5) < 0.07) Twinkle(p, (int)fx + 4, (int)fy - 4, Math.Abs(u - 0.5) < 0.035 ? 2 : 1);
                }
            }
            else
            {
                var u = (s - 2.6) / 0.4;
                var wob = Math.Sin(u * Math.PI * 3) * (1 - u);
                CorkAt(p, mx, my - Math.Round(Math.Abs(Math.Sin(u * Math.PI * 2)) * 2 * (1 - u)), rest, 1 + (wob * 0.3), 1 - (wob * 0.3), 0);
                if (u < 0.6)
                {
                    for (var q = 0; q < 6; q++)
                    {
                        var a = (q / 6.0 * Math.PI * 2) + 0.3;
                        var r = 4 + (u * 16);
                        Twinkle(p, (int)Math.Round(mx + (Math.Cos(a) * r)), (int)Math.Round(my + (Math.Sin(a) * r * 0.6)), u < 0.25 ? 1 : 0);
                    }
                }
            }
        }

        // The pop: rays of light bursting out of the neck, and a puff of drops.
        if (s is > 0.35 and < 0.62)
        {
            var k = (s - 0.35) / 0.27;
            for (var ray = 0; ray < 8; ray++)
            {
                var a = (ray / 8.0 * Math.PI * 2) + 0.2;
                for (var r = 3 + (k * 9); r < 6 + (k * 12); r += 0.7)
                {
                    int x = (int)Math.Round(mx + (Math.Cos(a) * r)), y = (int)Math.Round(my + (Math.Sin(a) * r * 0.8));
                    if (Bayer[y & 3, x & 3] < (1 - k) * 18) p.Set(x, y, ray % 2 == 0 ? WhiteC : GoldHi);
                }
            }
        }

        if (s is > 0.35 and < 0.8)
        {
            for (var n = 0; n < 9; n++)
            {
                var a = -Math.PI * (0.15 + (n / 8.0 * 0.7));
                var r = 2 + ((s - 0.35) * 22);
                p.Set((int)Math.Round(mx + (Math.Cos(a) * r)), (int)Math.Round(my + (Math.Sin(a) * r * 0.8) + ((s - 0.35) * (s - 0.35) * 40)), n % 3 == 0 ? WhiteC : Pink[3]);
            }
        }

        // Foam: bubbles welling up out of the open neck, growing and popping.
        if (s is > 0.4 and < 2.7)
        {
            for (var b = 0; b < 5; b++)
            {
                if (s - 0.4 < b * 0.11) continue;
                var age = ((s - 0.4) / 0.55) + (b * 0.2);
                var life = age - Math.Floor(age);
                var x = (int)Math.Round(mx + ((Rnd(b, (int)age) - 0.5) * 5) + (Math.Sin((life * 6) + b) * 0.8));
                var y = (int)Math.Round(my - 1 - (life * 9));
                if (life < 0.8)
                {
                    p.Set(x, y, life < 0.3 ? Pink[3] : Pink[4]);
                    if (life > 0.3)
                    {
                        foreach (var (dx, dy) in Four) p.Set(x + dx, y + dy, Pink[3]);
                        p.Set(x - 1, y - 1, WhiteC);
                    }
                }
                else
                {
                    foreach (var (dx, dy) in Diagonals) p.Set(x + (dx * 2), y + (dy * 2), Pink[4]);
                }
            }
        }

        for (var n = 0; n < targets.Count; n++)
        {
            var target = targets[n];
            var leave = 0.45 + (n * 0.1);
            var fly = 0.5 + ((targets.Count - 1 - n) * 0.04);
            var u = (s - leave) / fly;
            var ctrlX = mx - 16 - (n * 2);
            var ctrlY = target.Y - 12.0;
            (double X, double Y) Point(double k) =>
                (((1 - k) * (1 - k) * mx) + (2 * (1 - k) * k * ctrlX) + (k * k * (target.X - 3)),
                 ((1 - k) * (1 - k) * my) + (2 * (1 - k) * k * ctrlY) + (k * k * (target.Y - 4)));

            // the ribbon: a light core, darker edges, its tail thinning; drops falling off it
            if (u is > 0 and < 1.3)
            {
                for (var k = 0; k < 46; k++)
                {
                    var v = Math.Min(1, u) - (k * 0.006);
                    if (v <= 0) continue;
                    var (x, y) = Point(v);
                    int ix = (int)Math.Round(x), iy = (int)Math.Round(y);
                    p.Set(ix, iy, k < 3 ? WhiteC : Pink[3]);
                    if (k < 30)
                    {
                        p.Set(ix + 1, iy, Pink[2]);
                        p.Set(ix, iy + 1, Pink[1]);
                        p.Set(ix - 1, iy, Pink[2]);
                    }

                    if (k < 4)
                    {
                        p.Set(ix, iy - 1, Pink[4]);
                        p.Set(ix + 1, iy - 1, Pink[3]);
                    }
                }

                for (var d = 0; d < 6; d++)
                {
                    var age = s - (leave + (d * fly / 6));
                    if (age is < 0 or > 0.6) continue;
                    var (x, y) = Point(Math.Min(1, (d / 6.0) + 0.08));
                    p.Set((int)Math.Round(x + ((Rnd(d, n) - 0.5) * 2)), (int)Math.Round(y + (age * age * 60) + (age * 4)), d % 2 == 0 ? Pink[2] : Pink[3]);
                }
            }

            Splash(p, s - leave - fly, target, n);
        }
    }

    private static void Splash(CapSheet p, double h, Target target, int n)
    {
        if (h is <= 0 or > 1.9) return;
        var (ix, iy) = (target.X, target.Y);

        // a crown of drops and a ring of foam
        if (h < 0.75)
        {
            for (var d = 0; d < 14; d++)
            {
                var a = -Math.PI * (d / 13.0);
                var sp = 8 + (Rnd(d, n + 7) * 9);
                p.Set((int)Math.Round(ix - 3 + (Math.Cos(a) * sp * h * 1.3)), (int)Math.Round(iy - 5 + (Math.Sin(a) * sp * h) + (34 * h * h)), d % 3 == 0 ? WhiteC : Pink[3]);
            }
        }

        if (h < 0.35)
        {
            for (var a = 0; a < 16; a++)
            {
                if (a % 2 == 0 && h >= 0.15) continue;
                var r = 3 + (h * 26);
                p.Set((int)Math.Round(ix - 3 + (Math.Cos(a / 16.0 * Math.PI * 2) * r)), (int)Math.Round(iy - 4 + (Math.Sin(a / 16.0 * Math.PI * 2) * r * 0.45)), Pink[4], 230);
            }
        }

        // the Pokémon: the medicine runs down it from the top, a white flash, its colours back through the dither
        var box = target.Sprite;
        var run = (h - 0.05) / 0.35;
        for (var y = box.Top; y < box.Top + box.Height; y++)
        {
            uint sea1 = p.At(box.Left - 2, y), sea2 = p.At(box.Left - 3, y), sea3 = p.At(box.Left + box.Width + 1, y), sea4 = p.At(box.Left + box.Width + 2, y);
            for (var x = box.Left; x < box.Left + box.Width; x++)
            {
                if (!p.Solid(x, y)) continue;
                var c = p.At(x, y);
                if (Near(c, sea1) || Near(c, sea2) || Near(c, sea3) || Near(c, sea4) || Luma(c) < 30) continue;
                var down = (y - box.Top) / (double)Math.Max(1, box.Height);
                if (h < 0.42 && down < run) p.Set(x, y, CapSheet.Mix(c, Pink[3], 0.7));
                else if (h is >= 0.42 and < 0.5) p.Set(x, y, WhiteC);
                else if (h is >= 0.5 and < 0.9 && Bayer[y & 3, x & 3] < (1 - ((h - 0.5) / 0.4)) * 16) p.Set(x, y, CapSheet.Mix(c, Pink[4], 0.55));
            }
        }

        // sparkles twinkling round it
        for (var q = 0; q < 5; q++)
        {
            var life = h - 0.5 - (q * 0.12);
            if (life is < 0 or > 0.5) continue;
            var a = (q * 1.3) + n;
            int x = (int)Math.Round(ix - 3 + (Math.Cos(a) * 15)), y = (int)Math.Round(iy - 2 + (Math.Sin(a) * 11)), r = life < 0.2 ? 2 : 1;
            p.Set(x, y, WhiteC);
            for (var k = 1; k <= r; k++)
            {
                p.Set(x + k, y, k == 1 ? Pink[4] : WhiteC);
                p.Set(x - k, y, k == 1 ? Pink[4] : WhiteC);
                p.Set(x, y + k, Pink[4]);
                p.Set(x, y - k, Pink[4]);
            }
        }

        // a heart and two crosses rising, swaying, fading through the dither
        for (var q = 0; q < 3; q++)
        {
            var life = h - 0.55 - (q * 0.15);
            if (life is < 0 or > 1.1) continue;
            var heart = q == 1;
            var fx = ix - 3 + ((q - 1) * 7) + (Math.Sin((life * 5) + q) * 1.3);
            var fy = iy - 10 - (life * 14);
            var keep = 1 - (life / 1.1);
            foreach (var (dx, dy) in heart ? HeartShape : CrossShape)
            {
                int x = (int)Math.Round(fx) + dx, y = (int)Math.Round(fy) + dy;
                if (Bayer[y & 3, x & 3] >= keep * 16) continue;
                p.Set(x, y, heart ? (dx == -1 && dy == -1 ? WhiteC : Pink[3]) : (dx != 0 || dy != 0 ? Heal : WhiteC));
            }
        }

        // its HP bar: a light runs along it
        var r2 = (h - 0.45) / 0.55;
        if (target.BarLength > 0 && r2 is > 0 and < 1.4)
        {
            var head = target.BarX + (r2 * target.BarLength);
            for (var x = target.BarX; x < target.BarX + target.BarLength; x++)
            {
                if (x <= head && x > head - 4) p.Set(x, target.BarY, x > head - 1 ? WhiteC : Heal);
                else if (x <= head && r2 < 1.3 && Bayer[target.BarY & 3, x & 3] < 8) p.Set(x, target.BarY, Heal, 160);
            }
        }
    }

    private static readonly (int, int)[] HeartShape = [(-1, -1), (1, -1), (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0), (-1, 1), (0, 1), (1, 1), (0, 2)];
    private static readonly (int, int)[] CrossShape = [(0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)];

    /// <summary>The cork alone, a barrel turned to <paramref name="angle"/> (0 upright, clockwise), squashed by <paramref name="wide"/> and
    /// <paramref name="tall"/>; its top face lighter, its pores turning with it. A ghost (1, 2) is its blur: fewer cells, no outline.</summary>
    private static void CorkAt(CapSheet p, double x0, double y0, double angle, double wide, double tall, int ghost)
    {
        const int R = 8;
        const double HalfW = 3.5, HalfH = 2.8;
        double ux = Math.Sin(angle), uy = -Math.Cos(angle);
        var inside = new bool[(2 * R) + 1, (2 * R) + 1];
        var local = new (double X, double Y)[(2 * R) + 1, (2 * R) + 1];
        for (var y = -R; y <= R; y++)
        {
            for (var x = -R; x <= R; x++)
            {
                var lx = (x * -uy) + (y * ux);
                var ly = -((x * ux) + (y * uy));
                local[y + R, x + R] = (lx, ly);
                inside[y + R, x + R] = Math.Abs(lx) <= HalfW * wide && Math.Abs(ly) <= HalfH * tall
                    && !(Math.Abs(lx) > (HalfW * wide) - 0.8 && Math.Abs(ly) > (HalfH * tall) - 0.8);
            }
        }

        int ix = (int)Math.Round(x0), iy = (int)Math.Round(y0);
        for (var y = -R; y <= R; y++)
        {
            for (var x = -R; x <= R; x++)
            {
                if (!inside[y + R, x + R])
                {
                    var touches = Math.Abs(x) < R && Math.Abs(y) < R
                        && (inside[y + R + 1, x + R] || inside[y + R - 1, x + R] || inside[y + R, x + R + 1] || inside[y + R, x + R - 1]);
                    if (ghost == 0 && touches) p.Set(ix + x, iy + y, Ink);
                    continue;
                }

                if (ghost > 0 && Bayer[(iy + y) & 3, (ix + x) & 3] >= 8 / ghost) continue;
                var (lx, ly) = local[y + R, x + R];
                var side = -lx / HalfW;
                var tone = ly > (HalfH * tall) - 1.3 ? 4 : side > 0.45 ? 3 : side > -0.15 ? 2 : side > -0.65 ? 1 : 0;
                if (tone < 4 && Hash((int)Math.Round(lx * 1.5) + 9, (int)Math.Round(ly * 1.5) + 9) % 6 == 0) tone = Math.Max(0, tone - 1);
                p.Set(ix + x, iy + y, ghost > 0 ? Cork[2] : Cork[tone]);
            }
        }
    }

    /// <summary>A four-pointed star of pale gold, as the icon's: size 0 a dot, 1 a small cross, 2 a long one.</summary>
    private static void Twinkle(CapSheet p, int x, int y, int size)
    {
        p.Set(x, y, WhiteC);
        for (var k = 1; k <= size; k++)
        {
            var c = k == size && size == 2 ? Star : GoldHi;
            p.Set(x + k, y, c);
            p.Set(x - k, y, c);
            p.Set(x, y + k, c);
            p.Set(x, y - k, c);
        }
    }

    private static readonly (int X, int Y)[] Glints = [(-15, -11), (11, -20), (17, -3), (-17, 3)];
    private static readonly (int, int)[] Diagonals = [(1, 1), (-1, 1), (1, -1), (-1, -1)];

    private static double Smooth(double k) => k * k * (3 - (2 * k));

    private static void Number(CapSheet p, int x0, int y0, int value, double turn)
    {
        var rows = Digits[Math.Clamp(value, 0, 3)];
        var k = turn < 0 ? 1 : Math.Abs(Math.Cos(Math.Clamp(turn, 0, 1) * Math.PI));
        for (var pass = 0; pass < 2; pass++)
        {
            for (var y = 0; y < 7; y++)
            {
                for (var x = 0; x < 5; x++)
                {
                    if (rows[y][x] != '#') continue;
                    var yy = y0 + (int)Math.Round(3 + ((y - 3) * k));
                    if (pass == 0)
                    {
                        foreach (var (dx, dy) in Around) p.Set(x0 + x + dx, yy + dy, Ink);   // the gold pass covers what falls inside the digit
                    }
                    else p.Set(x0 + x, yy, y == 0 ? GoldHi : y >= 5 ? GoldDeep : Gold);
                }
            }
        }
    }

    private static readonly (int, int)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1)];

    // ---------------------------------------------------------------- the model

    /// <summary>The flask at a level (in 24 steps) with or without its cork: rendered once, then kept.</summary>
    private static Cell[] Model(double level, bool corkOn)
    {
        var step = (int)Math.Round(Math.Clamp(level, 0, 1) * 24);
        var key = (step * 2) + (corkOn ? 1 : 0);
        lock (Models)
        {
            if (Models.TryGetValue(key, out var made)) return made;
            made = Render(step / 24.0, corkOn);
            Models[key] = made;
            return made;
        }
    }

    private static Cell[] Render(double level, bool corkOn)
    {
        var light = Norm((-0.55, 0.75, 0.6));
        var cells = new List<Cell>();
        var lit = new HashSet<(int, int)>();
        for (var sy = -24; sy <= 24; sy++)
        {
            for (var sx = -20; sx <= 20; sx++)
            {
                double z = 40;
                var what = -1;
                for (var i = 0; i < 80 && z > -40; i++)
                {
                    var (d, w) = Scene((sx, sy, z), corkOn);
                    if (d < 0.02)
                    {
                        what = w;
                        break;
                    }

                    z -= Math.Max(0.05, d);
                }

                if (what < 0) continue;
                (double X, double Y, double Z) pt = (sx, sy, z);
                const double E = 0.05;
                var n = Norm((Scene((sx + E, sy, z), corkOn).D - Scene((sx - E, sy, z), corkOn).D,
                    Scene((sx, sy + E, z), corkOn).D - Scene((sx, sy - E, z), corkOn).D,
                    Scene((sx, sy, z + E), corkOn).D - Scene((sx, sy, z - E), corkOn).D));
                var lamb = Dot(n, light);
                var reflect = Norm(((2 * Dot(n, light) * n.X) - light.X, (2 * Dot(n, light) * n.Y) - light.Y, (2 * Dot(n, light) * n.Z) - light.Z));
                var h = Dot(pt, Up);
                uint[] ramp;
                if (what == 2) ramp = h > CorkTo - 0.4 ? [.. Cork.Select(c => CapSheet.Mix(c, WhiteC, 0.12))] : Cork;
                else if (what == 1) ramp = level >= 0.95 && h < NeckTo - 1.5 ? Pink : Glass;
                else
                {
                    var dir = Norm(pt);
                    var front = Dot(dir, WindowDir);
                    if (front > Math.Cos(0.42))
                    {
                        var winH = Ball * Math.Sin(0.42);
                        var upward = pt.Y - (WindowDir.Y * Ball);
                        ramp = upward < -winH + (Math.Clamp(level / 0.8, 0, 1) * 2 * winH) ? Pink : Glass;
                    }
                    else if (front > Math.Cos(0.55)) ramp = White;
                    else if (front > Math.Cos(0.62)) ramp = [Ink, Ink, Ink, Ink, Ink];
                    else if (Math.Abs(h) < 1.5) ramp = Band;
                    else ramp = h > 0 ? Red : White;
                }

                var tone = lamb > 0.72 ? 4 : lamb > 0.4 ? 3 : lamb > 0.05 ? 2 : lamb > -0.3 ? 1 : 0;
                var col = ramp[tone];
                if (reflect.Z > 0.93 && what != 2) col = WhiteC;
                cells.Add(new Cell(sx, -sy, col, what == 0));
                lit.Add((sx, -sy));
            }
        }

        // ink outline, then the gold aura round it
        var edge = new List<Cell>();
        var taken = new HashSet<(int, int)>(lit);
        foreach (var c in cells)
        {
            foreach (var (dx, dy) in Four)
            {
                if (taken.Add((c.X + dx, c.Y + dy))) edge.Add(new Cell(c.X + dx, c.Y + dy, Ink, false));
            }
        }

        var aura = new List<Cell>();
        foreach (var c in edge)
        {
            foreach (var (dx, dy) in Four)
            {
                if (taken.Add((c.X + dx, c.Y + dy))) aura.Add(new Cell(c.X + dx, c.Y + dy, Aura, false));
            }
        }

        return [.. aura, .. edge, .. cells];
    }

    private static readonly (int, int)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private static (double D, int W) Scene((double X, double Y, double Z) p, bool corkOn)
    {
        var ball = Len(p) - Ball;
        var neck = Cylinder(p, NeckFrom, NeckTo, NeckR);
        var cork = corkOn ? Cylinder(p, CorkFrom, CorkTo, CorkR) : 1e9;
        var d = Math.Min(ball, Math.Min(neck, cork));
        return (d, d == cork ? 2 : d == neck ? 1 : 0);
    }

    private static double Cylinder((double X, double Y, double Z) p, double from, double to, double r)
    {
        var h = Dot(p, Up);
        var radial = Len((p.X - (Up.X * h), p.Y - (Up.Y * h), p.Z - (Up.Z * h)));
        var dr = radial - r;
        var dh = Math.Max(from - h, h - to);
        return Math.Min(Math.Max(dr, dh), 0) + Math.Sqrt((Math.Max(dr, 0) * Math.Max(dr, 0)) + (Math.Max(dh, 0) * Math.Max(dh, 0)));
    }

    /// <summary>The flask's own up, turned: rolled to the right and pitched towards the viewer.</summary>
    private static (double X, double Y, double Z) Turn((double X, double Y, double Z) v)
    {
        var p = (X: v.X, Y: (v.Y * Math.Cos(Pitch)) - (v.Z * Math.Sin(Pitch)), Z: (v.Y * Math.Sin(Pitch)) + (v.Z * Math.Cos(Pitch)));
        return ((p.X * Math.Cos(Roll)) + (p.Y * Math.Sin(Roll)), (-p.X * Math.Sin(Roll)) + (p.Y * Math.Cos(Roll)), p.Z);
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static double Len((double X, double Y, double Z) a) => Math.Sqrt(Dot(a, a));

    private static (double X, double Y, double Z) Norm((double X, double Y, double Z) a)
    {
        var l = Len(a);
        return (a.X / l, a.Y / l, a.Z / l);
    }

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Math.Clamp(k, 0, 1), 3);

    private static bool Near(uint a, uint b) =>
        Math.Abs((int)((a >> 16) & 255) - (int)((b >> 16) & 255)) + Math.Abs((int)((a >> 8) & 255) - (int)((b >> 8) & 255)) + Math.Abs((int)(a & 255) - (int)(b & 255)) < 46;

    private static double Luma(uint c) => (0.3 * ((c >> 16) & 255)) + (0.59 * ((c >> 8) & 255)) + (0.11 * (c & 255));

    private static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    private static double Rnd(int a, int b) => (Hash(a, b) % 10000) / 10000.0;
}
