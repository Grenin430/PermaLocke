using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>One Pokémon on the cap plaque. An egg shows nothing but itself (§230).</summary>
public sealed record CapPlaqueMember(BitmapSource? Sprite, string Name, int Level, int Hp, int MaxHp, bool Egg);

/// <summary>What the cap plaque says: the stage, its cap, the next one, the party's strongest and the party.</summary>
public sealed record CapPlaqueData(string Trial, int Cap, int? Next, int? Highest, IReadOnlyList<CapPlaqueMember> Party);

/// <summary>
/// The level cap over the game as a trophy plaque (2026-10-08, chosen from the prototypes, §239): a walnut shield ending in a
/// point, a brass cup screwed on top, a big engraved brass plate with the cap and the stage, the party's level against the cap
/// as the burning fuse of the notices, and one brass plate per Pokémon.
/// </summary>
/// <remarks>
/// Drawn whole, cell by cell, like the notices: text in the pixel fonts, hard shadows, no blur. <see cref="CellPixels"/> is a
/// whole number of screen pixels so every cell stays square; whoever places it picks it for the size of the game.
/// </remarks>
public sealed class CapPlaque : FrameworkElement
{
    public const int Columns = 112;

    private const int PlateTop = 22;
    private const int PlateHeight = 40;
    private const int GaugeHeight = 30;
    private const int RowHeight = 21;
    private const int Point = 30;

    private CapPlaqueData? _data;
    private int _cell = 2;

    public CapPlaque()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public CapPlaqueData? Data
    {
        get => _data;
        set
        {
            _data = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Screen pixels per cell.</summary>
    public int CellPixels
    {
        get => _cell;
        set
        {
            var cell = Math.Max(1, value);
            if (cell == _cell) return;
            _cell = cell;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>How many cells tall the plaque is for this data: the plate, the fuse if there is a reading, a plate per Pokémon, the point.</summary>
    public static int Rows(CapPlaqueData data) => PartyTop(data) + (data.Party.Count * RowHeight) + 7 + Point;

    private static int PartyTop(CapPlaqueData data) => PlateTop + PlateHeight + 4 + (data.Highest is null ? 0 : GaugeHeight) + 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_data is null) return new Size(0, 0);
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Size(Columns * _cell / dpi.DpiScaleX, Rows(_data) * _cell / dpi.DpiScaleY);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_data is null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var sheet = Paint(_data);
        drawingContext.DrawImage(sheet.ToBitmap(), new Rect(0, 0, sheet.W * _cell / dpi.DpiScaleX, sheet.H * _cell / dpi.DpiScaleY));
    }

    // ---------------------------------------------------------------- the drawing

    private const uint Brown = 0x3A2410;
    private const uint Gold = 0xE8B83C;
    private const uint Engraved = 0x3A2808;
    private const uint Shine = 0xFFF2C0;
    private const uint HpGreen = 0x58A56E;
    private const uint HpAmber = 0xD8A83C;
    private const uint HpRed = 0xB8433A;
    private static readonly uint[] Hues = [0x4A6A8A, 0x8A4A3A, 0x4A7A5A, 0x7A5A8A, 0x8A7A3A, 0x3A7A7A];

    private static readonly string[] TrophyArt =
    [
        "#############",
        "#.#########.#",
        "#.#########.#",
        "#..#######..#",
        ".#.#######.#.",
        "..#.#####.#..",
        "....#####....",
        ".....###.....",
        "......#......",
        "......#......",
        ".....###.....",
        "...#######...",
        "...#######...",
        "..#########.."
    ];

    /// <summary>The whole plaque in cells. Static and without WPF layout, so it can be looked at in a test.</summary>
    public static Sheet Paint(CapPlaqueData data)
    {
        const int w = Columns;
        var h = Rows(data);
        var p = new Sheet(w, h);
        const int top = 14;

        // El escudo de nogal: vetas, filo claro arriba y a la izquierda, oscuro a la derecha, y la punta abajo.
        Wood(p, 0, top, w, h - top, 0x5A3018, 9);
        for (var y = top; y < h; y++)
        {
            var inset = y > h - Point ? (int)Math.Round((y - (h - Point)) * (w / 2.0) / Point) : 0;
            for (var x = 0; x < w; x++)
            {
                var outside = x < inset || x >= w - inset || (y == top && (x < 3 || x >= w - 3));
                if (outside) p.Erase(x, y);
                else if (x == inset || x == w - 1 - inset || y == top) p.Set(x, y, 0x1A0A04);
                else if (x == inset + 1 || y == top + 1) p.Set(x, y, 0x8A5A30);
                else if (x == w - 2 - inset) p.Set(x, y, 0x2A1408);
            }
        }

        // La copa de latón atornillada encima.
        Trophy(p, (w / 2) - 6, 1, Gold);
        p.Rect((w / 2) - 9, 15, 19, 3, 0x5A3A10);
        p.HLine((w / 2) - 8, 15, 17, Gold);

        // La placa grande grabada: el tope, la etapa y el siguiente.
        Brass(p, 10, PlateTop, w - 20, PlateHeight);
        const string head = "CAP DE NIVEL";
        var headX = (w / 2) - (((head.Length * 4) - 1) / 2);
        p.Small(head, headX + 1, 27, Shine);
        p.Small(head, headX, 26, Engraved);

        var cap = data.Cap.ToString();
        var scale = PixelFont.Measure(cap) * 2 <= 30 ? 2 : 1;
        p.Small("NV", 22, 42, Engraved);
        p.Text(cap, 32, scale == 2 ? 35 : 40, Engraved, scale, Shine);

        var lines = TrialLines(data.Trial);
        for (var i = 0; i < lines.Count; i++) p.Small(lines[i], 64, 36 + (i * 7), Engraved);
        p.Small(data.Next is { } next ? $"SIG: {next}" : "ULTIMO", 64, 52, Engraved);

        var y0 = PlateTop + PlateHeight + 4;
        if (data.Highest is { } highest)
        {
            Gauge(p, 14, y0 + 2, w - 28, highest, data.Cap, 0xF2DDA8, 0x7A5A38, Engraved, 0xF4D88A, 0xE8D0A0);
        }

        var partyTop = PartyTop(data);
        for (var i = 0; i < data.Party.Count; i++)
        {
            var y = partyTop + (i * RowHeight);
            Brass(p, 8, y, w - 16, 20);
            Entry(p, 13, y + 2, w - 28, data.Party[i], data.Cap, Engraved, 0x6A4A18, 0x5A3A10, 0x3A2808);
        }

        return p;
    }

    /// <summary>«PRUEBA 3 DE 12» in two lines; any other stage name wrapped to the plate's right column.</summary>
    private static List<string> TrialLines(string trial)
    {
        var at = trial.IndexOf(" DE ", StringComparison.Ordinal);
        if (trial.StartsWith("PRUEBA ", StringComparison.Ordinal) && at > 0) return [trial[..at], trial[(at + 1)..]];

        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in trial.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (candidate.Length <= 9 || line.Length == 0)
            {
                line = candidate.Length > 9 ? candidate[..9] : candidate;
                continue;
            }

            lines.Add(line);
            line = word.Length > 9 ? word[..9] : word;
        }

        if (line.Length > 0) lines.Add(line);
        return lines.Take(2).ToList();
    }

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text) hash = unchecked((hash * 31) + ch);
        return hash & 0x7FFFFFFF;
    }

    private static void Trophy(Sheet p, int x, int y, uint colour)
    {
        for (var gy = 0; gy < TrophyArt.Length; gy++)
        {
            for (var gx = 0; gx < TrophyArt[gy].Length; gx++)
            {
                if (TrophyArt[gy][gx] != '#') continue;
                var shade = gx < 5 ? Lighter(colour, 0.35) : gx > 8 ? Darker(colour, 0.3) : colour;
                if (gy >= 11) shade = gy == 11 ? 0x6A3A1Cu : Darker(0x6A3A1C, 0.25);
                p.Set(x + gx, y + gy, shade);
            }
        }

        p.Set(x + 3, y + 1, 0xFFF6C8);
        p.Set(x + 4, y + 2, 0xFFF6C8);
    }

    private static void Wood(Sheet p, int x, int y, int w, int h, uint baseColour, int seed)
    {
        p.VGrad(x, y, w, h, Lighter(baseColour, 0.12), Darker(baseColour, 0.14));
        var random = new Random(seed);
        for (var line = 0; line < h; line += 4)
        {
            p.HLine(x, y + line, w, Darker(baseColour, 0.22), 90);
            for (var k = 0; k < 4; k++)
            {
                var gx = x + random.Next(Math.Max(1, w - 8));
                p.HLine(gx, y + line + 2, 3 + random.Next(6), Darker(baseColour, 0.12), 110);
            }
        }
    }

    /// <summary>A brass plate screwed on: bevel, sheen and a screw at each end.</summary>
    private static void Brass(Sheet p, int x, int y, int w, int h)
    {
        p.Notched(x + 1, y + 1, w, h, 0x1A0A04, 2, 120);
        p.Notched(x, y, w, h, 0x5A3A10, 2);
        p.GradNotched(x + 1, y + 1, w - 2, h - 2, 0xF4D88A, 0xA87828, 1);
        p.HLine(x + 2, y + 1, w - 4, 0xFFF2C0);
        p.HLine(x + 2, y + h - 2, w - 4, 0x7A5418);
        for (var i = 0; i < h - 4; i++) p.Set(x + 8 + i, y + 2 + i, 0xFFFFFF, 40);
        foreach (var sx in new[] { x + 3, x + w - 4 })
        {
            var sy = y + (h / 2.0);
            p.Disc(sx + 0.5, sy, 1.6, 0x6A5030);
            p.Set(sx, (int)sy, 0x2A1A08);
            p.Set(sx - 1, (int)sy, 0x2A1A08);
        }
    }

    private static void Chip(Sheet p, int x, int y, string text, uint fill, uint ink, uint rim)
    {
        var w = (text.Length * 4) + 3;
        p.Notched(x, y, w, 7, rim, 1);
        p.Notched(x + 1, y + 1, w - 2, 5, fill, 1);
        p.Small(text, x + 2, y + 1, ink);
    }

    /// <summary>The fuse of the notices: rope up to the strongest one's level, the flame at its tip, ash beyond.</summary>
    private static void Rope(Sheet p, int x, int y, int w, double share, bool atCap, uint rope, uint ash)
    {
        var tip = (int)Math.Round(w * share);
        for (var i = 0; i < w; i++)
        {
            if (i < tip - 1) p.Set(x + i, y + 1, (i / 2) % 2 == 0 ? rope : Lighter(rope, 0.2));
            else if (i > tip + 1 && i % 3 != 0) p.Set(x + i, y + 1, ash, 170);
        }

        p.Set(x + tip, y + 1, atCap ? 0xFFC030u : 0xFFE070u);
        p.Set(x + tip - 1, y + 1, 0xF08A3A);
        p.Set(x + tip + 1, y + 1, 0xE85A20);
        p.Set(x + tip, y, 0xF08A3A);
        p.Set(x + tip, y + 2, 0xE85A20);
    }

    private static void Pennant(Sheet p, int x, int y, uint colour)
    {
        p.VLine(x, y, 13, Brown);
        for (var i = 0; i < 5; i++) p.Rect(x + 1, y + 1 + i, 8 - (i * 2), 1, colour);
        p.Set(x + 1, y + 1, Lighter(colour, 0.4));
    }

    /// <summary>The party against the cap: a chip with the strongest one's level over the flame, the cap on a pennant, a ruler under it.</summary>
    private static void Gauge(Sheet p, int x, int y, int w, int highest, int cap, uint rope, uint ash, uint chipFill, uint chipInk, uint label)
    {
        var atCap = highest >= cap;
        p.Small("NIVEL DEL EQUIPO", x, y, label);
        var share = Math.Clamp(highest / (double)Math.Max(1, cap), 0, 1);
        var span = w - 12;
        var tip = (int)Math.Round(span * share);
        Chip(p, Math.Clamp(x + tip - 9, x, x + span - 16), y + 8, $"NV {highest}", atCap ? Gold : chipFill, atCap ? 0x4A2E00u : chipInk, Brown);
        Rope(p, x, y + 18, span, share, atCap, rope, ash);
        for (var t = 0; t <= span; t += 6) p.VLine(x + t, y + 22, t % 12 == 0 ? 3 : 2, label, 140);
        Pennant(p, x + span + 3, y + 8, Gold);
        p.Small($"{cap}", x + span - 1, y + 25, label);
    }

    private static void Bar(Sheet p, int x, int y, int w, double share, uint fill, uint outline, uint groove)
    {
        p.Rect(x, y, w, 5, outline);
        p.Rect(x + 1, y + 1, w - 2, 3, groove);
        var f = share > 0 ? Math.Max(1, (int)((w - 2) * share)) : 0;
        p.Rect(x + 1, y + 1, f, 3, fill);
        p.HLine(x + 1, y + 1, f, Lighter(fill, 0.45));
        p.HLine(x + 1, y + 3, f, Darker(fill, 0.3));
        for (var t = x + 6; t < x + 1 + f; t += 5) p.Set(t, y + 2, Darker(fill, 0.22));
        p.Set(x + 2, y + 1, 0xFFFFFF, 190);
    }

    /// <summary>The Pokémon's window: a rimmed box with a dithered floor in its own tint, the icon at half size.</summary>
    private static void Medallion(Sheet p, int x, int y, BitmapSource? sprite, bool fallen, int hue, uint rim, bool atCap)
    {
        p.Notched(x, y, 22, 17, rim, 2);
        var floor = fallen ? 0x2E2A28u : Mix(0x2A1C10, Hues[hue % Hues.Length], 0.42);
        p.Notched(x + 1, y + 1, 20, 15, floor, 2);
        for (var yy = 11; yy < 16; yy++)
        {
            for (var xx = 1; xx < 21; xx++)
            {
                if ((xx + yy) % 2 == 0 && (yy > 12 || xx % 2 == 0)) p.Set(x + xx, y + yy, Lighter(floor, 0.16));
            }
        }

        p.HLine(x + 2, y + 1, 18, Lighter(floor, 0.28));

        // Media escala: un píxel de cada cuatro, el primero que no sea transparente. Si se pasa de la ventana, se recorta.
        if (ToastPixels.SpritePixels.From(sprite) is { } pixels)
        {
            var hw = Math.Min(20, pixels.Width / 2);
            var hh = Math.Min(15, pixels.Height / 2);
            var left = x + 1 + ((20 - hw) / 2);
            var topY = y + 1 + ((15 - hh) / 2);
            for (var sy = 0; sy < hh; sy++)
            {
                for (var sx = 0; sx < hw; sx++)
                {
                    foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                    {
                        if (!pixels.Solid((sx * 2) + dx, (sy * 2) + dy)) continue;
                        var c = pixels.At((sx * 2) + dx, (sy * 2) + dy);
                        var rgb = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                        if (fallen)
                        {
                            var luma = (int)Math.Round((0.30 * c.R) + (0.59 * c.G) + (0.11 * c.B));
                            rgb = ((uint)luma << 16) | ((uint)luma << 8) | (uint)luma;
                        }

                        p.Set(left + sx, topY + sy, rgb, fallen ? 105 : 255);
                        break;
                    }
                }
            }
        }

        if (fallen)
        {
            p.Line(x + 4, y + 3, x + 17, y + 14, HpRed, 200);
            p.Line(x + 17, y + 3, x + 4, y + 14, HpRed, 200);
        }

        if (atCap)
        {
            foreach (var (dx, dy) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) }) p.Set(x + 19 + dx, y + 1 + dy, Gold);
            p.Set(x + 19, y + 1, 0xFFF2B0);
        }
    }

    /// <summary>One Pokémon in two lines: name and level chip, then HP bar and numbers. An egg, its name alone.</summary>
    private static void Entry(Sheet p, int x, int y, int w, CapPlaqueMember m, int cap, uint ink, uint dim, uint rim, uint groove)
    {
        var fallen = !m.Egg && m.Hp <= 0;
        var atCap = !m.Egg && !fallen && m.Level >= cap;
        Medallion(p, x, y, m.Sprite, fallen, StableHash(m.Name), rim, atCap);

        var tx = x + 25;
        var name = m.Egg ? "HUEVO" : m.Name.ToUpperInvariant();
        if (name.Length > 9) name = name[..9];
        p.Small(name, tx, y + 1, fallen ? dim : ink);
        if (m.Egg) return;
        if (fallen) p.HLine(tx, y + 3, (name.Length * 4) - 1, HpRed, 220);

        var level = $"NV{m.Level}";
        var chipW = (level.Length * 4) + 3;
        // El nivel en una chapita oscura con letras de latón: en dorado si ya está en el tope, apagada si ha caído.
        Chip(p, x + w - chipW, y, level, atCap ? Gold : fallen ? 0x6A5A48u : Engraved, atCap ? 0x4A2E00u : fallen ? 0xD8CCB0u : 0xF4D88Au, atCap ? 0x8A5A10u : rim);

        var bar = w - 25 - 24;
        if (fallen)
        {
            p.Small("KO", tx, y + 10, HpRed);
            p.Small("SIN PS", tx + 11, y + 10, dim);
            return;
        }

        var share = m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0;
        Bar(p, tx, y + 9, bar, share, share > 0.5 ? HpGreen : share > 0.2 ? HpAmber : HpRed, rim, groove);
        p.Small($"{m.Hp}/{m.MaxHp}", tx + bar + 2, y + 10, dim);
    }

    private static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

    private static uint Mix(uint a, uint b, double t)
    {
        int Ch(int shift) => (int)Math.Round((((a >> shift) & 255) * (1 - t)) + (((b >> shift) & 255) * t));
        return Rgb(Ch(16), Ch(8), Ch(0));
    }

    private static uint Lighter(uint c, double t) => Mix(c, 0xFFFFFF, t);

    private static uint Darker(uint c, double t) => Mix(c, 0x000000, t);

    /// <summary>A canvas of cells with alpha blending, the pixel fonts and a few shapes.</summary>
    public sealed class Sheet(int width, int height)
    {
        public int W { get; } = width;
        public int H { get; } = height;
        public byte[] B { get; } = new byte[Math.Max(1, width * height * 4)];

        public void Set(int x, int y, uint rgb, int alpha = 255)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0) return;
            var i = ((y * W) + x) * 4;
            var r = (int)((rgb >> 16) & 255);
            var g = (int)((rgb >> 8) & 255);
            var b = (int)(rgb & 255);
            if (alpha >= 255 || B[i + 3] == 0)
            {
                (B[i], B[i + 1], B[i + 2], B[i + 3]) = ((byte)b, (byte)g, (byte)r, (byte)Math.Max(alpha, B[i + 3] == 0 ? alpha : 255));
                return;
            }

            var a = alpha / 255.0;
            B[i] = (byte)((B[i] * (1 - a)) + (b * a));
            B[i + 1] = (byte)((B[i + 1] * (1 - a)) + (g * a));
            B[i + 2] = (byte)((B[i + 2] * (1 - a)) + (r * a));
            B[i + 3] = 255;
        }

        public void Erase(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            var i = ((y * W) + x) * 4;
            B[i] = B[i + 1] = B[i + 2] = B[i + 3] = 0;
        }

        public void Rect(int x, int y, int w, int h, uint rgb, int alpha = 255)
        {
            for (var yy = y; yy < y + h; yy++)
            {
                for (var xx = x; xx < x + w; xx++) Set(xx, yy, rgb, alpha);
            }
        }

        public void HLine(int x, int y, int w, uint rgb, int alpha = 255) => Rect(x, y, w, 1, rgb, alpha);

        public void VLine(int x, int y, int h, uint rgb, int alpha = 255) => Rect(x, y, 1, h, rgb, alpha);

        /// <summary>A rectangle with its corners notched by <paramref name="n"/> cells.</summary>
        public void Notched(int x, int y, int w, int h, uint rgb, int n = 1, int alpha = 255)
        {
            for (var yy = 0; yy < h; yy++)
            {
                var inset = yy < n ? n - yy : yy >= h - n ? yy - (h - n) + 1 : 0;
                for (var xx = inset; xx < w - inset; xx++) Set(x + xx, y + yy, rgb, alpha);
            }
        }

        public void GradNotched(int x, int y, int w, int h, uint top, uint bottom, int n)
        {
            for (var yy = 0; yy < h; yy++)
            {
                var inset = yy < n ? n - yy : yy >= h - n ? yy - (h - n) + 1 : 0;
                Rect(x + inset, y + yy, w - (2 * inset), 1, Mix(top, bottom, h <= 1 ? 0 : yy / (double)(h - 1)));
            }
        }

        public void VGrad(int x, int y, int w, int h, uint top, uint bottom)
        {
            for (var yy = 0; yy < h; yy++) Rect(x, y + yy, w, 1, Mix(top, bottom, h <= 1 ? 0 : yy / (double)(h - 1)));
        }

        public void Disc(double cx, double cy, double radius, uint colour, int alpha = 255)
        {
            for (var y = (int)Math.Floor(cy - radius); y <= (int)Math.Ceiling(cy + radius); y++)
            {
                for (var x = (int)Math.Floor(cx - radius); x <= (int)Math.Ceiling(cx + radius); x++)
                {
                    var dx = x + 0.5 - cx;
                    var dy = y + 0.5 - cy;
                    if ((dx * dx) + (dy * dy) <= radius * radius) Set(x, y, colour, alpha);
                }
            }
        }

        public void Line(int x0, int y0, int x1, int y1, uint colour, int alpha = 255)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, colour, alpha);
                if (x0 == x1 && y0 == y1) return;
                var e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>The 5×7 pixel font at <paramref name="scale"/> cells per font pixel, with a shadow one cell down and right.</summary>
        public void Text(string text, int x, int y, uint rgb, int scale = 1, uint? shadow = null)
        {
            void Block(int px, int py, uint c)
            {
                for (var sy = 0; sy < scale; sy++)
                {
                    for (var sx = 0; sx < scale; sx++) Set(px + sx, py + sy, c);
                }
            }

            if (shadow is { } dark) PixelFont.Draw(text, 0, 0, (px, py) => Block(x + (px * scale) + 1, y + (py * scale) + 1, dark));
            PixelFont.Draw(text, 0, 0, (px, py) => Block(x + (px * scale), y + (py * scale), rgb));
        }

        /// <summary>The 3×5 capitals; accents are dropped, unknown characters leave a gap.</summary>
        public void Small(string text, int x, int y, uint rgb)
        {
            foreach (var raw in text.ToUpperInvariant())
            {
                var ch = raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', 'Ü' => 'U', _ => raw };
                if (ch == ' ')
                {
                    x += 3;
                    continue;
                }

                if (SmallFont.Glyph(ch) is not { } glyph)
                {
                    x += 4;
                    continue;
                }

                for (var gy = 0; gy < glyph.Length; gy++)
                {
                    for (var gx = 0; gx < glyph[gy].Length; gx++)
                    {
                        if (glyph[gy][gx] == '#') Set(x + gx, y + gy, rgb);
                    }
                }

                x += glyph[0].Length + 1;
            }
        }

        /// <summary>The cells as a frozen bitmap, redrawn in the theme's shades when it has them (the Game Boy).</summary>
        public BitmapSource ToBitmap()
        {
            var pixels = B;
            if (PixelTheme.Current.Shades is not null)
            {
                pixels = (byte[])B.Clone();
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] == 0) continue;
                    var mapped = PixelTheme.Current.Map(Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]));
                    (pixels[i], pixels[i + 1], pixels[i + 2]) = (mapped.B, mapped.G, mapped.R);
                }
            }

            var bitmap = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, pixels, W * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
