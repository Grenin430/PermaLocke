using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The Z-Crystal of a trial, as the trial film draws it (2026-10-15): the cartridge's own bag icon of that crystal at twice its size. Its shape
/// (a wide rhombus with a girdle), its facets and every colour are read from the 32×32 icon the app extracts from the player's ROM; the icon
/// carries its type's symbol too faint to read at that size, so the symbol is drawn here, eleven by eleven, in the icon's two tones.
/// </summary>
public sealed class CapTrialCrystal
{
    public const int W = 19;
    public const int H = 10;

    private static readonly Dictionary<int, CapTrialCrystal> Made = [];

    // The type symbols of the twelve trial crystals, as the icons carry them.
    private static readonly Dictionary<int, string[]> Symbols = new()
    {
        [807] = ["...#####...", ".#########.", ".###...###.", "###.....###", "##.......##", "##.......##", "##.......##", "###.....###", ".###...###.", ".#########.", "...#####..."],
        [813] = ["..##.##.##.", ".#########.", ".#.##.##.#.", ".#########.", ".#########.", "##########.", "###.######.", ".#..######.", "....#####..", "....####...", "..........."],
        [809] = [".....#.....", "....###....", "....###....", "...#####...", "..#######..", ".#########.", ".##.######.", ".##.######.", ".###.#####.", "..#######..", "...#####..."],
        [808] = [".....#.....", "....##.....", "...###..#..", "...####.#..", "..#######..", ".##.#####..", ".##..####..", ".###..###..", "..###.##...", "...#####...", "....###...."],
        [811] = [".......###.", ".....#####.", "....######.", "...###.###.", "..###.####.", "..##.####..", ".##.####...", ".#.####....", "..####.....", ".#.........", "#.........."],
        [819] = ["...######..", "..########.", ".####.####.", ".###.#####.", "####.######", "###.#######", "####.######", ".#####.###.", ".########..", "..######...", "..........."],
        [810] = ["......####.", ".....####..", "....####...", "...####....", "..#######..", ".#######...", "....####...", "...####....", "..###......", "..##.......", ".##........"],
        [820] = ["...#####...", "..#######..", ".#########.", ".##.###.##.", ".##.###.##.", ".#########.", ".#########.", ".#########.", ".#########.", ".##.###.##.", ".#...#...#."],
        [822] = ["...#####...", "..####.....", ".####......", ".###.......", "####.......", "####.......", "####.......", ".###.......", ".####......", "..####.....", "...#####..."],
        [821] = ["#.........#", "##.......##", "###.....###", ".###...###.", ".####.####.", "..#######..", "..#######..", "...#####...", "...#####...", "....###....", ".....#....."],
        [824] = [".....#.....", ".....#.....", "....###....", "....###....", "..#######..", "###########", "..#######..", "....###....", "....###....", ".....#.....", ".....#....."],
        [815] = ["...........", "...#####...", "..#######..", "...........", ".#########.", "###########", "...........", "###########", "###########", "...........", "..........."],
    };

    private readonly Dictionary<(int X, int Y), uint> _cells = [];
    private readonly HashSet<(int X, int Y)> _symbol = [];

    public uint Ink { get; private init; }

    public uint Deep { get; private init; }

    /// <summary>The crystal's own light, a little richer than its body: what it shines on the water.</summary>
    public uint Glow { get; private init; }

    /// <summary>Its light at its brightest, nearly white.</summary>
    public uint Shine { get; private init; }

    /// <summary>The crystal of an item, made once; null without its icon (no ROM, or not a type crystal).</summary>
    public static CapTrialCrystal? Of(int item, BitmapSource? icon)
    {
        if (item <= 0 || icon is null) return null;
        lock (Made)
        {
            if (Made.TryGetValue(item, out var made)) return made;
            var crystal = Read(item, icon);
            if (crystal is not null) Made[item] = crystal;
            return crystal;
        }
    }

    private static CapTrialCrystal? Read(int item, BitmapSource icon)
    {
        var source = icon.Format == PixelFormats.Bgra32 ? icon : new FormatConvertedBitmap(icon, PixelFormats.Bgra32, null, 0);
        int w = source.PixelWidth, h = source.PixelHeight;
        var px = new byte[w * h * 4];
        source.CopyPixels(px, w * 4, 0);
        var cells = new List<(int X, int Y, uint C)>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y * w) + x) * 4;
                if (px[i + 3] > 0) cells.Add((x, y, CapSheet.Rgb(px[i + 2], px[i + 1], px[i])));
            }
        }

        if (cells.Count < 20) return null;

        // where its colours are on the icon: the light upper left, the upper right, the girdle (the widest row), the two lower halves, the
        // deepest, the outline, and the two tones of its symbol in the middle
        var girdle = cells.GroupBy(q => q.Y).OrderByDescending(g => g.Count()).First().Key;
        var mid = (cells.Min(q => q.X) + cells.Max(q => q.X)) / 2.0;
        var ink = cells.MinBy(q => CapDeath.Luma(q.C)).C;
        var body = cells.Where(q => Dist(q.C, ink) > 30).ToList();
        uint Most(Func<(int X, int Y, uint C), bool> pred, uint fallback = 0x808080u) =>
            body.Where(pred).GroupBy(q => q.C).OrderByDescending(g => g.Count()).Select(g => g.Key).DefaultIfEmpty(fallback).First();
        var light = Most(q => q.Y < girdle && q.X < mid - 3);
        var light2 = Most(q => q.Y == girdle && q.X < mid - 3);
        var up = Most(q => q.Y < girdle && q.X > mid + 3);
        var gird = Most(q => q.Y == girdle && q.X > mid + 3);
        var lowL = Most(q => q.Y > girdle && q.X < mid - 2);
        var lowR = Most(q => q.Y > girdle && q.X > mid + 2);
        var deep = body.Where(q => q.Y > girdle + 2).Select(q => q.C).DefaultIfEmpty(lowR).MinBy(CapDeath.Luma);
        var symU = Most(q => q.Y < girdle && Math.Abs(q.X - mid) < 3 && Dist(q.C, light) > 40 && Dist(q.C, up) > 40, CapSheet.Darker(up, 0.3));
        var symL = Most(q => q.Y > girdle && Math.Abs(q.X - mid) < 3 && Dist(q.C, lowL) > 30 && Dist(q.C, lowR) > 30, CapSheet.Darker(lowR, 0.3));
        var glow = Saturate(CapSheet.Mix(up, gird, 0.5), 1.6);
        var crystal = new CapTrialCrystal { Ink = ink, Deep = deep, Glow = glow, Shine = CapSheet.Mix(glow, 0xFFFFFFu, 0.55) };

        // the crystal at twice the icon: the same rhombus, girdle and facets, the same colours
        static bool Inside(int x, int y) => (Math.Abs(x) / (double)W) + (Math.Abs(y) / (double)H) <= 1;
        for (var dy = -H; dy <= H; dy++)
        {
            for (var dx = -W; dx <= W; dx++)
            {
                if (!Inside(dx, dy)) continue;
                if (CapDeath.Round4.Any(o => !Inside(dx + o.X, dy + o.Y)))
                {
                    crystal._cells[(dx, dy)] = ink;
                    continue;
                }

                var rim = CapDeath.Round4.Any(o => !Inside(dx + (2 * o.X), dy + (2 * o.Y)));
                uint c;
                if (dy < 0)
                {
                    var lx = (W / 2.0) * ((dy + H) / (double)H);
                    c = dx < -lx ? light : dx > lx ? up : CapSheet.Mix(light, up, 0.45);
                    if (Math.Abs(Math.Abs(dx) - lx) < 0.55) c = CapSheet.Mix(c, 0xFFFFFFu, 0.35);          // the facet edges catch the light
                    if (rim && dx < 0) c = light2;
                    else if (rim) c = CapSheet.Mix(up, gird, 0.5);
                }
                else if (dy == 0)
                {
                    c = dx < 0 ? light2 : gird;
                }
                else
                {
                    var lx = (W / 2.0) * ((H - dy) / (double)H);
                    c = dx < -lx ? lowL : dx > lx ? lowR : CapSheet.Mix(lowL, lowR, 0.5);
                    if (Math.Abs(Math.Abs(dx) - lx) < 0.55) c = CapSheet.Mix(c, deep, 0.3);
                    if (rim && dx > -2) c = CapSheet.Mix(lowR, deep, 0.6);
                    if (dy >= H - 3) c = CapSheet.Mix(c, deep, 0.4);
                }

                crystal._cells[(dx, dy)] = c;
            }
        }

        // a sparkle on the upper-left facet, as on the icon
        foreach (var (gx, gy, k) in new[] { (-10, -3, 1.0), (-9, -4, 1.0), (-8, -5, 0.6), (-11, -2, 0.5) })
        {
            if (crystal._cells.TryGetValue((gx, gy), out var c)) crystal._cells[(gx, gy)] = CapSheet.Mix(c, 0xFFFFFFu, k);
        }

        // the symbol, the icon's two tones deepened to read, with a lit edge to its lower right as if cut into the stone
        if (Symbols.TryGetValue(item, out var mask))
        {
            for (var r = 0; r < 11; r++)
            {
                for (var k = 0; k < 11; k++)
                {
                    if (mask[r][k] != '#') continue;
                    var key = (k - 5, r - 5);
                    if (!crystal._cells.ContainsKey(key)) continue;
                    crystal._symbol.Add(key);
                    var tone = key.Item2 < 0 ? symU : key.Item2 > 0 ? symL : CapSheet.Mix(symU, symL, 0.5);
                    crystal._cells[key] = CapSheet.Mix(tone, ink, 0.4);
                }
            }

            foreach (var (x, y) in crystal._symbol.ToList())
            {
                var lit = (x + 1, y + 1);
                if (!crystal._symbol.Contains(lit) && crystal._cells.TryGetValue(lit, out var c) && c != ink) crystal._cells[lit] = CapSheet.Mix(c, 0xFFFFFFu, 0.3);
            }
        }

        return crystal;
    }

    /// <summary>
    /// The crystal as it is at a moment: its size, its turn about its long (upright) axis, how much its symbol burns, a glint running over its
    /// facets (0 to 1, or below 0 for none), what is under the sand cut away, and a wash of white.
    /// </summary>
    public void Paint(CapSheet p, int cx, int cy, double scale = 1, double spin = 0, double burn = 0, double glint = -1, int cut = int.MaxValue, int alpha = 255, double white = 0)
    {
        var s = Math.Cos(spin);
        var w = W * Math.Abs(s) * scale;
        var h = H * scale;
        if (w < 1.2)
        {
            // edge on: a line of light
            for (var dy = -(int)Math.Round(h); dy <= Math.Round(h); dy++)
            {
                if (cy + dy <= cut) p.Set(cx, cy + dy, CapSheet.Mix(Shine, 0xFFFFFFu, 0.5), (int)(alpha * (1 - (Math.Abs(dy) / (h + 1) * 0.5))));
            }

            return;
        }

        for (var dy = -(int)Math.Ceiling(h); dy <= h; dy++)
        {
            if (cy + dy > cut) continue;
            for (var dx = -(int)Math.Ceiling(w); dx <= w; dx++)
            {
                var key = (CapDeath.R(dx / (s * scale)), CapDeath.R(dy / scale));
                if (!_cells.TryGetValue(key, out var c)) continue;
                if (s < 0) c = CapSheet.Mix(c, Deep, 0.25);
                if (burn > 0 && _symbol.Contains(key)) c = CapSheet.Mix(c, CapSheet.Mix(Shine, 0xFFFFFFu, burn * 0.6), Math.Min(1, burn));
                if (glint >= 0 && c != Ink)
                {
                    var g = (key.Item1 * 0.55) + key.Item2 - (-H - 6 + (glint * ((2 * H) + 18)));
                    if (Math.Abs(g) < 1.4) c = CapSheet.Mix(c, 0xFFFFFFu, 0.85 * (1 - (Math.Abs(g) / 1.4)));
                }

                if (white > 0) c = CapSheet.Mix(c, 0xFFFFFFu, white);
                p.Set(cx + dx, cy + dy, c, alpha);
            }
        }
    }

    private static int Dist(uint a, uint b) =>
        Math.Abs(CapDeath.Red(a) - CapDeath.Red(b)) + Math.Abs(CapDeath.Green(a) - CapDeath.Green(b)) + Math.Abs(CapDeath.Blue(a) - CapDeath.Blue(b));

    private static uint Saturate(uint c, double k)
    {
        var l = CapDeath.Luma(c);
        int Ch(int v) => (int)Math.Clamp(Math.Round(l + ((v - l) * k)), 0, 255);
        return CapSheet.Rgb(Ch(CapDeath.Red(c)), Ch(CapDeath.Green(c)), Ch(CapDeath.Blue(c)));
    }
}
