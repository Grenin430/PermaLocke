namespace PermaLocke.App.Views;

/// <summary>The shiny look of the overlay: one since 2026-10-17, when the violet «every colour to its shiny counterpart» was taken out.</summary>
public enum CapShinyKind
{
    /// <summary>
    /// Each thing fills with a colour of its own and keeps turning through the rainbow (2026-10-16). Since 2026-10-17 the sky is one more thing
    /// and the water a single one, with no bands, and the colour sums are kept per colour so a frame no longer stalls the overlay.
    /// </summary>
    Things
}

/// <summary>A shiny in this battle: which look, when it began and, once the battle is over, when it ended (on the panel's clock).</summary>
public sealed record CapShinyTurn(CapShinyKind Kind, double Start, double? End = null)
{
    /// <summary>Whether anything of it is still to be drawn at a moment.</summary>
    public bool Over(double t) => End is { } end && t > end + CapShiny.Back + 1;
}

/// <summary>
/// The overlay when the wild Pokémon is shiny (2026-10-16, prototypes in imagenes/revision/prototipo_shiny, approved): a shiny sparkle at the edge
/// next to the game, then the change runs out from the game across the window as a dithered front with sparkles on it, and stays until the battle
/// ends, when it runs back. Drawn over the finished frame, cell by cell; pixel art only (a 4x4 Bayer dither for every change in time, hard steps of
/// hue). Text, greys and the sky keep their colours. A function of the data and the moment: no clock and no random of its own.
/// </summary>
public sealed class CapShiny
{
    /// <summary>How long the change takes to cross a window, and to go back.</summary>
    public const double Wave = 2.0, Back = 1.6;

    private const double Delay = 0.45;
    private const double Band = 0.1;

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
    private static readonly (double At, double To)[] HueMap =
        [(0, 42), (40, 26), (75, 44), (140, 48), (160, 276), (215, 292), (250, 322), (290, 210), (330, 200), (360, 42)];

    private static readonly Dictionary<uint, uint> ShinyOf = [];

    // What the segmentation of a frame needs, kept between frames: the cells' things, and the HSL of every colour met (a frame has few).
    private int[] _comp = [];
    private int[] _stack = [];
    private int[] _cells = [];
    private readonly List<Thing> _things = [];
    private readonly Dictionary<uint, (double H, double S, double L)> _hsl = [];

    // The frame read once (colour of each cell, 0 for none, and its class: 1 water, 2 coloured), and the painted colour of each source colour
    // at each hue, kept between frames (2026-10-17: reading cell by cell and converting colours every time cost the panel its frames).
    private uint[] _col = [];
    private byte[] _cls = [];
    private readonly Dictionary<uint, byte> _classOf = [];
    private readonly Dictionary<long, uint> _painted = [];

    private struct Thing
    {
        public int First, Count, Top, Bottom, Key;
        public double X, Y, Sat;
        public bool Big;
    }

    // The cells the passing shiny Pokémon took in this frame (CapVisitors): the rainbow leaves them as they are. One frame at a time per thread.
    [ThreadStatic] private static bool[]? _kept;
    [ThreadStatic] private static List<int>? _keptList;
    [ThreadStatic] private static int _keptW;

    /// <summary>A new frame of a window <paramref name="width"/> cells wide: nothing kept yet.</summary>
    public static void BeginFrame(int width)
    {
        if (_kept is not null && _keptList is not null) foreach (var i in _keptList) if (i < _kept.Length) _kept[i] = false;
        (_keptList ??= []).Clear();
        _keptW = width;
    }

    /// <summary>A cell a shiny Pokémon took: the rainbow does not paint over it.</summary>
    public static void Keep(int x, int y)
    {
        if (x < 0 || x >= _keptW || y < 0) return;
        var i = (y * _keptW) + x;
        if (_kept is null || _kept.Length <= i) Array.Resize(ref _kept, Math.Max(i + 1, (_kept?.Length ?? 0) * 2));
        if (_kept[i]) return;
        _kept[i] = true;
        _keptList!.Add(i);
    }

    private static bool Kept(int index) => _keptList is { Count: > 0 } && _kept is { } kept && index < kept.Length && kept[index];

    /// <summary>The sparkles a shiny Pokémon leaves behind it as it passes: a short tail of twinkles that the rainbow leaves alone.</summary>
    public static void Trail(CapSheet p, int cx, int cy, int size, int dir, double since)
    {
        var frame = (int)Math.Floor(since * 10);
        for (var k = 1; k <= 5; k++)
        {
            if ((frame + k) % 3 == 0) continue;
            var x = cx - (dir * ((size / 2) + (k * 5)));
            var y = cy + (int)Math.Round(Math.Sin((since * 5) + k) * (2 + (k * 0.6)));
            Star(p, x, y, k <= 2 && (frame + k) % 3 == 1 ? 2 : 1, k % 2 == 0 ? 0xFFF6C4u : 0xFFFFFFu);
            for (var a = -2; a <= 2; a++)
            {
                Keep(x + a, y);
                Keep(x, y + a);
            }
        }
    }

    private (double H, double S, double L) HslOf(uint c)
    {
        if (_hsl.TryGetValue(c, out var known)) return known;
        if (_hsl.Count > 20000) _hsl.Clear();
        return _hsl[c] = Hsl(c);
    }



    /// <summary>Paints the shiny over the finished frame of a window.</summary>
    public void Paint(CapSheet p, CapWorldLayout l, CapShinyTurn turn, double t)
    {
        if (t < turn.Start || turn.Over(t)) return;
        var since = t - turn.Start;
        var innerX = l.Panel == CapPanelKind.Deep ? p.W - 1 : 0;
        var originY = Math.Max(0, l.SurfaceY - 10);
        var reach = p.W + (p.H * 0.6);

        Things(p, l, turn, t, innerX, originY, reach);

        Sparkles(p, l, turn, t, since, innerX, originY, reach);
    }

    /// <summary>Where a cell is from the game, 0 at the edge next to it and 1 at the far corner.</summary>
    private static double Distance(int x, int y, int innerX, int originY, double reach) =>
        (Math.Abs(x - innerX) + (Math.Abs(y - originY) * 0.6)) / reach;

    /// <summary>How far the change has gone at a cell, 0 to 1: out from the game, then back the same way when the battle has ended.</summary>
    private static double Reached(double d, CapShinyTurn turn, double t)
    {
        var front = (t - turn.Start - Delay) / Wave * (1 + Band);
        var inn = Math.Clamp((front - d) / Band, 0, 1);
        if (turn.End is not { } end) return inn;
        var back = (t - end) / Back * (1 + Band);
        return inn * (1 - Math.Clamp((back - d) / Band, 0, 1));
    }

    /// <summary>The shiny counterpart of a colour: a continuous map round the hue circle, the lightness kept; greys stay.</summary>
    public static uint Shiny(uint c)
    {
        lock (ShinyOf)
        {
            if (ShinyOf.TryGetValue(c, out var known)) return known;
            var (h, s, l) = Hsl(c);
            var made = s < 0.12 ? c : FromHsl(MapHue(h), Math.Min(1, s * 0.95), l);
            ShinyOf[c] = made;
            return made;
        }
    }

    private static double MapHue(double h)
    {
        for (var i = 0; i + 1 < HueMap.Length; i++)
        {
            var (a, x) = HueMap[i];
            var (b, y) = HueMap[i + 1];
            if (h < a || h > b) continue;
            var d = y - x;
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            return x + (d * (h - a) / (b - a));
        }

        return h;
    }

    // ---------------------------------------------------------------- each thing its own colour

    private static bool Near(uint a, uint b) =>
        Math.Abs((int)((a >> 16) & 255) - (int)((b >> 16) & 255)) + Math.Abs((int)((a >> 8) & 255) - (int)((b >> 8) & 255)) + Math.Abs((int)(a & 255) - (int)(b & 255)) <= 30;

    private void Things(CapSheet p, CapWorldLayout l, CapShinyTurn turn, double t, int innerX, int originY, double reach)
    {
        var n = p.W * p.H;
        if (_comp.Length < n)
        {
            _comp = new int[n];
            _stack = new int[n];
            _cells = new int[n];
        }

        if (_col.Length < n)
        {
            _col = new uint[n];
            _cls = new byte[n];
        }

        var bytes = p.B;
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            if (bytes[o + 3] == 0 || Kept(i)) { _col[i] = 0; _cls[i] = 0; continue; }
            var c = ((uint)bytes[o + 2] << 16) | ((uint)bytes[o + 1] << 8) | bytes[o];
            _col[i] = c | 0x1000000u;
            if (!_classOf.TryGetValue(c, out var cls))
            {
                if (_classOf.Count > 20000) _classOf.Clear();
                var (hh, ss, _) = HslOf(c);
                cls = (byte)((ss > 0.15 && hh is >= 150 and <= 250 ? 1 : 0) | (ss >= 0.08 ? 2 : 0));
                _classOf[c] = cls;
            }

            _cls[i] = cls;
        }

        Array.Fill(_comp, -1, 0, n);
        _things.Clear();
        var used = 0;
        var surface = Math.Max(0, l.SurfaceY);

        for (var s = 0; s < n; s++)
        {
            var sx = s % p.W;
            var sy = s / p.W;
            if (_comp[s] >= 0 || _col[s] == 0) continue;
            var sky = sy < surface;
            var id = _things.Count;
            var top = 0;
            var first = used;
            _stack[top++] = s;
            _comp[s] = id;
            while (top > 0)
            {
                var c = _stack[--top];
                _cells[used++] = c;
                int x = c % p.W, y = c / p.W;
                var col = _col[c] & 0xFFFFFF;
                var wet = (_cls[c] & 1) != 0;
                var coloured = (_cls[c] & 2) != 0;
                for (var k = 0; k < 4; k++)
                {
                    var nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    var ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nx >= p.W || ny < 0 || ny >= p.H || (ny < surface) != sky) continue;
                    var m = (ny * p.W) + nx;
                    if (_comp[m] >= 0 || _col[m] == 0) continue;
                    var join = sky ? coloured && (_cls[m] & 2) != 0 : wet ? (_cls[m] & 1) != 0 : (_cls[m] & 1) == 0 && Near(col, _col[m] & 0xFFFFFF);
                    if (!join) continue;
                    _comp[m] = id;
                    _stack[top++] = m;
                }
            }

            int topRow = p.H, bottomRow = 0;
            double xs = 0, ys = 0, r = 0, g = 0, b = 0;
            for (var i = first; i < used; i++)
            {
                int x = _cells[i] % p.W, y = _cells[i] / p.W;
                var col = _col[_cells[i]] & 0xFFFFFF;
                topRow = Math.Min(topRow, y);
                bottomRow = Math.Max(bottomRow, y);
                xs += x;
                ys += y;
                r += (col >> 16) & 255;
                g += (col >> 8) & 255;
                b += col & 255;
            }

            var count = used - first;
            var mean = CapSheet.Rgb((int)(r / count), (int)(g / count), (int)(b / count));
            _things.Add(new Thing
            {
                First = first, Count = count, Top = topRow, Bottom = bottomRow, X = xs / count, Y = ys / count, Sat = HslOf(mean).S,
                Big = count > 400, Key = (int)(((mean >> 21) << 6) | (((mean >> 13) & 7) << 3) | ((mean >> 5) & 7))
            });
        }

        foreach (var th in _things)
        {
            if (th.Sat < 0.12) continue;
            var d = Distance((int)th.X, (int)th.Y, innerX, originY, reach);
            var start = turn.Start + Delay + (d * Wave * 0.9);
            var fill = th.Big ? 0.9 : 0.45;
            var level = Math.Clamp((t - start) / fill, 0, 1);
            // going home (2026-10-17): the far things first, the colour gathered towards the game in a spiral
            var drain = turn.End is { } end ? Math.Clamp((t - (end + ((1 - d) * Back * 0.9))) / fill, 0, 1) : 0;
            if (level <= 0 || drain >= 1) continue;

            var span = Math.Max(1, th.Bottom - th.Top + 1);
            var since = t - turn.Start;
            var hue = Math.Round(((Hash(th.Key, 77) % 360) + (since * (25 + (Hash(th.Key, 5) % 30)))) / 20) * 20;
            for (var i = th.First; i < th.First + th.Count; i++)
            {
                int x = _cells[i] % p.W, y = _cells[i] / p.W;
                var h = (th.Bottom - y + 0.5) / span;                        // 0 at its foot, 1 at its top
                if (h > level || 1 - h < drain) continue;
                var rising = level < 1 && h > level - (1.2 / span);         // the rising line, a step lighter
                var src = _col[_cells[i]] & 0xFFFFFF;
                var key = ((long)src << 16) | ((long)(((int)hue % 360 + 360) % 360) << 2) | (th.Big ? 2L : 0) | (rising ? 1L : 0);
                if (!_painted.TryGetValue(key, out var made))
                {
                    if (_painted.Count > 60000) _painted.Clear();
                    var (_, sat, light) = HslOf(src);
                    var tone = th.Big ? Math.Clamp(sat, 0.45, 0.75) : Math.Clamp(sat * 1.15, 0.6, 1);
                    made = FromHsl(hue, tone, Math.Min(0.85, rising ? light + 0.18 : light));
                    _painted[key] = made;
                }

                p.Set(x, y, made);
            }
        }

        Thrown(p, l, turn, t, innerX, originY, reach);
    }

    /// <summary>
    /// The stars of the shiny sparkle (2026-10-17): thrown from the edge next to the game, each lands on a thing just as its colour starts; when the
    /// battle ends, each thing's colour goes home in a spiral as a star, the far ones first, and a last flash at the edge.
    /// </summary>
    private void Thrown(CapSheet p, CapWorldLayout l, CapShinyTurn turn, double t, int innerX, int originY, double reach)
    {
        var ex = l.Panel == CapPanelKind.Deep ? p.W - 8 : 8;
        var shown = 0;
        foreach (var th in _things.Where(th => th.Sat >= 0.12).OrderByDescending(th => th.Count))
        {
            if (++shown > 110) break;
            var d = Distance((int)th.X, (int)th.Y, innerX, originY, reach);
            var colour = (th.Key & 1) == 0 ? 0xFFFFFFu : 0xFFF6C4u;

            // out: a short arc from the edge onto the thing, arriving when its colour starts
            var land = turn.Start + Delay + (d * Wave * 0.9);
            var flight = 0.35 + (d * 0.45);
            var u = (t - (land - flight)) / flight;
            if (u is >= 0 and <= 1)
            {
                var x = ex + ((th.X - ex) * u);
                var y = originY + ((th.Y - originY) * u) - (Math.Sin(u * Math.PI) * 18);
                Star(p, (int)Math.Round(x), (int)Math.Round(y), u < 0.85 ? 2 : 1, colour);
                Star(p, (int)Math.Round(ex + ((th.X - ex) * (u - 0.06))), (int)Math.Round(originY + ((th.Y - originY) * (u - 0.06))), 0, colour);
                Star(p, (int)Math.Round(ex + ((th.X - ex) * (u - 0.12))), (int)Math.Round(originY + ((th.Y - originY) * (u - 0.12))), 0, colour);
            }

            // where it lands: a burst of four little stars flying apart
            var after = t - land;
            if (after is >= 0 and < 0.35)
            {
                var r = 2 + (after * 30);
                for (var k = 0; k < 4; k++)
                {
                    var a = (k * Math.PI / 2) + (Math.PI / 4);
                    Star(p, (int)Math.Round(th.X + (Math.Cos(a) * r)), (int)Math.Round(th.Y + (Math.Sin(a) * r)), after < 0.15 ? 1 : 0, colour);
                }

                Star(p, (int)th.X, (int)th.Y, after < 0.12 ? 3 : 2, 0xFFFFFF);
            }

            // home: a spiral from the thing back to the edge, a turn and a half, when its colour drains
            if (turn.End is not { } end) continue;
            var leave = end + ((1 - d) * Back * 0.9);
            var w = (t - leave) / 0.8;
            if (w is < 0 or > 1) continue;
            var angle = w * Math.PI * 3;
            double rx = (th.X - ex) * (1 - w), ry = (th.Y - originY) * (1 - w);
            var sx = ex + (rx * Math.Cos(angle)) - (ry * Math.Sin(angle));
            var sy = originY + (rx * Math.Sin(angle)) + (ry * Math.Cos(angle));
            Star(p, (int)Math.Round(sx), (int)Math.Round(sy), w < 0.7 ? 2 : 1, colour);
        }

        // the opening: a white flash over the window, dithered and quick, and two rings that run out from the game
        var since = t - turn.Start;
        if (since is >= 0.15 and < 0.45) Flash(p, 1 - ((since - 0.15) / 0.3));
        Ring(p, ex, originY, since - 0.2, 1.4, p.W + p.H);
        Ring(p, ex, originY, since - 0.45, 1.6, p.W + p.H);

        // while it lasts: now and then a big sparkle somewhere, as the game's
        if (turn.End is null && since > Delay + Wave)
        {
            for (var n = 0; n < 5; n++)
            {
                var beat = (since * 0.8) + (n * 0.41);
                var slot = (int)Math.Floor(beat);
                var age = beat - slot;
                if (age > 0.45) continue;
                var x = (int)(Rnd(n + 40, slot) * p.W);
                var y = (int)(Rnd(slot, n + 90) * p.H);
                Star(p, x, y, age < 0.12 ? 3 : age < 0.3 ? 2 : 1, n % 2 == 0 ? 0xFFFFFFu : 0xFFF6C4u);
            }
        }

        // the last flash, when everything is home
        if (turn.End is { } over)
        {
            Ring(p, ex, originY, t - (over + Back + 0.15), 1.2, p.W + p.H);
            var flash = t - (over + Back + 0.15);
            if (flash is >= 0 and < 0.5)
            {
                var r = flash < 0.2 ? 4 : flash < 0.35 ? 3 : 1;
                Star(p, ex, originY, r, 0xFFFFFF);
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Math.PI / 4;
                    var dist = 3 + (flash * 40);
                    Star(p, ex + (int)Math.Round(Math.Cos(a) * dist), originY + (int)Math.Round(Math.Sin(a) * dist * 0.8), 1, k % 2 == 0 ? 0xFFFFFFu : 0xFFF6C4u);
                }
            }
        }
    }

    // ---------------------------------------------------------------- sparkles

    private static void Sparkles(CapSheet p, CapWorldLayout l, CapShinyTurn turn, double t, double since, int innerX, int originY, double reach)
    {
        const uint White = 0xFFFFFF, Pale = 0xFFF6C4;
        var ex = l.Panel == CapPanelKind.Deep ? p.W - 8 : 8;

        // the game's own shiny sparkle, big, at the edge next to the game: it grows and then throws the stars (Thrown)
        if (since < Delay + 0.2)
        {
            var g = since < 0.25 ? 2 + (int)(since * 16) : since < Delay ? 6 : 3;
            Star(p, ex, originY, g, 0xFFFFFF);
            Star(p, ex, originY, Math.Max(1, g - 2), 0xFFF6C4);
        }

        // sparkles riding on the front, in and back
        var frame = (int)Math.Floor(t * 12);
        for (var n = 0; n < 90; n++)
        {
            var x = (int)(Rnd(n, 7) * p.W);
            var y = (int)(Rnd(7, n) * p.H);
            if (!p.Solid(x, y)) continue;
            var k = Reached(Distance(x, y, innerX, originY, reach), turn, t);
            var life = (frame + n) % 6;
            if (k is > 0.15 and < 0.85 && life < 3) Star(p, x, y, life == 1 ? 2 : 1, n % 3 != 0 ? White : Pale);
        }

        // while it lasts, a few slow twinkles
        if (since > Delay + Wave && turn.End is null)
        {
            for (var n = 0; n < 7; n++)
            {
                var beat = (since * 1.5) + (n * 0.37);
                var slot = (int)Math.Floor(beat);
                var x = (int)(Rnd(n, slot) * p.W);
                var y = (int)(Rnd(slot, n + 3) * p.H);
                var age = beat - slot;
                if (p.Solid(x, y) && age < 0.5) Star(p, x, y, age < 0.2 ? 2 : 1, n % 2 == 1 ? White : Pale);
            }
        }
    }

    /// <summary>A dithered white flash over the window, <paramref name="k"/> from 0 to 1.</summary>
    private static void Flash(CapSheet p, double k)
    {
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (p.Solid(x, y) && Bayer[y & 3, x & 3] < k * 9) p.Set(x, y, 0xFFFFFF);
            }
        }
    }

    /// <summary>A ring running out from a point: a dithered band of light <paramref name="age"/> seconds old that crosses <paramref name="far"/> cells in <paramref name="life"/> seconds and fades.</summary>
    private static void Ring(CapSheet p, int cx, int cy, double age, double life, double far)
    {
        if (age < 0 || age > life) return;
        var radius = far * (age / life);
        var fade = 1 - (age / life);
        var y0 = Math.Max(0, (int)(cy - (radius / 0.8) - 4));
        var y1 = Math.Min(p.H - 1, (int)(cy + (radius / 0.8) + 4));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                var d = Math.Sqrt(((x - cx) * (x - cx)) + (((y - cy) * 1.25) * ((y - cy) * 1.25)));
                var off = Math.Abs(d - radius);
                if (off > 4 || !p.Solid(x, y)) continue;
                var k = fade * (1 - (off / 4));
                if (Bayer[y & 3, x & 3] < k * 14) p.Set(x, y, off < 1.5 ? 0xFFFFFFu : 0xFFF6C4u);
            }
        }
    }

    internal static void Star(CapSheet p, int x, int y, int r, uint c)
    {
        void Plot(int px, int py)
        {
            if (p.Solid(px, py)) p.Set(px, py, c);
        }

        Plot(x, y);
        for (var k = 1; k <= r; k++)
        {
            Plot(x + k, y);
            Plot(x - k, y);
            Plot(x, y + k);
            Plot(x, y - k);
        }

        if (r < 2) return;
        Plot(x + 1, y + 1);
        Plot(x - 1, y - 1);
        Plot(x + 1, y - 1);
        Plot(x - 1, y + 1);
    }

    // ---------------------------------------------------------------- colour

    private static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    private static double Rnd(int a, int b) => (Hash(a, b) % 10000) / 10000.0;

    private static (double H, double S, double L) Hsl(uint c)
    {
        double r = ((c >> 16) & 255) / 255.0, g = ((c >> 8) & 255) / 255.0, b = (c & 255) / 255.0;
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), l = (mx + mn) / 2;
        if (mx == mn) return (0, 0, l);
        var d = mx - mn;
        var s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
        var h = mx == r ? ((g - b) / d) + (g < b ? 6 : 0) : mx == g ? ((b - r) / d) + 2 : ((r - g) / d) + 4;
        return (h * 60, s, l);
    }

    private static uint FromHsl(double h, double s, double l)
    {
        h = (((h % 360) + 360) % 360) / 360;
        if (s <= 0)
        {
            var v = (int)Math.Round(l * 255);
            return CapSheet.Rgb(v, v, v);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        double F(double t)
        {
            t = (t + 1) % 1;
            return t < 1 / 6.0 ? p + ((q - p) * 6 * t) : t < 0.5 ? q : t < 2 / 3.0 ? p + ((q - p) * ((2 / 3.0) - t) * 6) : p;
        }

        return CapSheet.Rgb((int)Math.Round(F(h + (1 / 3.0)) * 255), (int)Math.Round(F(h) * 255), (int)Math.Round(F(h - (1 / 3.0)) * 255));
    }
}
