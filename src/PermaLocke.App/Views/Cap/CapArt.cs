using System.Windows.Media.Imaging;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// The pieces the cap panel is made of (2026-10-10): walnut, brass, the cup, the shield, the bars, the chips, the gems and the
/// little graves. Each one draws on a <see cref="CapSheet"/> in cells and decides nothing: where it goes is
/// <see cref="CapComposer"/>'s, and what moves is <see cref="CapLive"/>'s.
/// </summary>
public static class CapArt
{
    public const uint Ink = 0x1A0A04;
    public const uint Walnut = 0x5A3018;
    public const uint WalnutDark = 0x2A1408;
    public const uint WalnutLight = 0x8A5A30;
    public const uint Back = 0x2A1A10;
    public const uint BackDeep = 0x1C110A;
    public const uint Brown = 0x3A2410;
    public const uint Gold = 0xE8B83C;
    public const uint GoldDim = 0x8A6A24;
    public const uint Engraved = 0x3A2808;
    public const uint Shine = 0xFFF2C0;
    public const uint BrassHigh = 0xF4D88A;
    public const uint BrassLow = 0xA87828;
    public const uint HpGreen = 0x58A56E;
    public const uint HpAmber = 0xD8A83C;
    public const uint HpRed = 0xB8433A;
    public const uint Paper = 0xE8D0A0;
    public const uint Dim = 0x6A4A18;

    /// <summary>The tints of a niche floor for a Pokémon with no type to give it one.</summary>
    public static readonly uint[] Hues = [0x4A6A8A, 0x8A4A3A, 0x4A7A5A, 0x7A5A8A, 0x8A7A3A, 0x3A7A7A];

    /// <summary>The three letters of each type, in Spanish, by the game's type number.</summary>
    private static readonly string[] TypeNames =
        ["NOR", "LUC", "VOL", "VEN", "TIE", "ROC", "BIC", "FAN", "ACE", "FUE", "AGU", "PLA", "ELE", "PSI", "HIE", "DRA", "SIN", "HAD"];

    public static string TypeName(int type) => type >= 0 && type < TypeNames.Length ? TypeNames[type] : "???";

    public static uint TypeColour(int type)
    {
        var c = TypeColours.Of(type);
        return CapSheet.Rgb(c.R, c.G, c.B);
    }

    public static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text) hash = unchecked((hash * 31) + ch);
        return hash & 0x7FFFFFFF;
    }

    /// <summary>A number out of two, the same every time: what the decoration that looks random is made of.</summary>
    public static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = (h ^ (h >> 13)) * 1274126177;
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    // ---------------------------------------------------------------- wood, brass, frame

    public static void Wood(CapSheet p, int x, int y, int w, int h, uint baseColour, int seed)
    {
        p.VGrad(x, y, w, h, CapSheet.Lighter(baseColour, 0.12), CapSheet.Darker(baseColour, 0.14));
        for (var line = 0; line < h; line += 4)
        {
            p.HLine(x, y + line, w, CapSheet.Darker(baseColour, 0.22), 90);
            for (var k = 0; k < 4; k++)
            {
                var gx = x + (Hash(seed, (line * 7) + k) % Math.Max(1, w - 8));
                p.HLine(gx, y + line + 2, 3 + (Hash(seed + 1, (line * 7) + k) % 6), CapSheet.Darker(baseColour, 0.12), 110);
            }
        }
    }

    /// <summary>A brass plate screwed on: bevel, sheen and a screw at each end.</summary>
    public static void Brass(CapSheet p, int x, int y, int w, int h, bool screws = true)
    {
        p.Notched(x + 1, y + 1, w, h, Ink, 2, 120);
        p.Notched(x, y, w, h, 0x5A3A10, 2);
        p.GradNotched(x + 1, y + 1, w - 2, h - 2, BrassHigh, BrassLow, 1);
        p.HLine(x + 2, y + 1, w - 4, Shine);
        p.HLine(x + 2, y + h - 2, w - 4, 0x7A5418);
        for (var i = 0; i < h - 4; i++) p.Set(x + 8 + i, y + 2 + i, 0xFFFFFF, 22);
        if (!screws || w < 14 || h < 9) return;
        foreach (var sx in new[] { x + 3, x + w - 4 })
        {
            var sy = y + (h / 2.0);
            p.Disc(sx + 0.5, sy, 1.6, 0x6A5030);
            p.Set(sx, (int)sy, 0x2A1A08);
            p.Set(sx - 1, (int)sy, 0x2A1A08);
        }
    }

    /// <summary>
    /// The walnut case of the panel: a bevelled frame with brass corners and a recessed back panel. Returns the inside, where the
    /// content goes. <paramref name="top"/> is where the frame starts, to leave room above it for the cup.
    /// </summary>
    public static CapRect Case(CapSheet p, int cols, int rows, int top, int thickness, int seed)
    {
        var h = rows - top;
        Wood(p, 0, top, cols, h, Walnut, seed);

        // The outline, the light edge on top and left, the dark edge on the right and at the bottom.
        p.Notched(0, top, cols, h, Ink, 3, 0);
        for (var x = 0; x < cols; x++)
        {
            p.Set(x, top, Ink);
            p.Set(x, rows - 1, Ink);
        }

        for (var y = top; y < rows; y++)
        {
            p.Set(0, y, Ink);
            p.Set(cols - 1, y, Ink);
        }

        p.HLine(1, top + 1, cols - 2, WalnutLight);
        p.HLine(1, top + 2, cols - 2, CapSheet.Mix(WalnutLight, Walnut, 0.5));
        p.VLine(1, top + 1, h - 2, WalnutLight);
        p.VLine(cols - 2, top + 1, h - 2, WalnutDark);
        p.VLine(cols - 3, top + 2, h - 3, CapSheet.Mix(WalnutDark, Walnut, 0.4));
        p.HLine(1, rows - 2, cols - 2, WalnutDark);

        // Chunky corners: the four are notched, and a brass bracket sits on each.
        foreach (var (cx, cy) in new[] { (0, top), (cols - 1, top), (0, rows - 1), (cols - 1, rows - 1) })
        {
            var sx = cx == 0 ? 1 : -1;
            var sy = cy == top ? 1 : -1;
            p.Erase(cx, cy);
            for (var i = 0; i < 2; i++)
            {
                p.Erase(cx + (sx * i), cy);
                p.Erase(cx, cy + (sy * i));
            }

            for (var i = 0; i < 6; i++)
            {
                p.Set(cx + (sx * (i + 1)), cy + (sy * 2), i < 4 ? BrassHigh : BrassLow);
                p.Set(cx + (sx * 2), cy + (sy * (i + 1)), i < 4 ? BrassHigh : BrassLow);
                p.Set(cx + (sx * (i + 1)), cy + (sy * 3), BrassLow);
                p.Set(cx + (sx * 3), cy + (sy * (i + 1)), BrassLow);
            }

            p.Set(cx + (sx * 2), cy + (sy * 2), Shine);
        }

        // The recess: a dark back panel with a faint grain and a brass thread round it.
        var inner = new CapRect(thickness, top + thickness, cols - (2 * thickness), h - (2 * thickness));
        p.Rect(inner.Left - 1, inner.Top - 1, inner.Width + 2, inner.Height + 2, GoldDim);
        p.Rect(inner.Left, inner.Top, inner.Width, inner.Height, Back);
        p.VGrad(inner.Left, inner.Top, inner.Width, inner.Height, CapSheet.Lighter(Back, 0.07), BackDeep);
        for (var x = inner.Left + 7; x < inner.Right - 2; x += 14)
        {
            p.VLine(x + (Hash(seed, x) % 3), inner.Top, inner.Height, CapSheet.Darker(Back, 0.35), 90);
        }

        p.HLine(inner.Left, inner.Top, inner.Width, BackDeep);
        p.HLine(inner.Left, inner.Top + 1, inner.Width, CapSheet.Darker(Back, 0.25), 160);
        p.VLine(inner.Left, inner.Top, inner.Height, BackDeep);
        p.VLine(inner.Left + 1, inner.Top, inner.Height, CapSheet.Darker(Back, 0.25), 160);
        return inner;
    }

    // ---------------------------------------------------------------- the cup and the shield

    private static readonly string[] CupArt =
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

    public const int CupWidth = 13;
    public const int CupHeight = 14;

    /// <summary>The brass cup, with its walnut foot, standing on whatever is at <paramref name="y"/> + 14.</summary>
    public static void Cup(CapSheet p, int x, int y, uint colour = Gold)
    {
        for (var gy = 0; gy < CupArt.Length; gy++)
        {
            for (var gx = 0; gx < CupArt[gy].Length; gx++)
            {
                if (CupArt[gy][gx] != '#') continue;
                var shade = gx < 5 ? CapSheet.Lighter(colour, 0.35) : gx > 8 ? CapSheet.Darker(colour, 0.3) : colour;
                if (gy >= 11) shade = gy == 11 ? 0x6A3A1Cu : CapSheet.Darker(0x6A3A1C, 0.25);
                p.Set(x + gx, y + gy, shade);
            }
        }

        p.Set(x + 3, y + 1, 0xFFF6C8);
        p.Set(x + 4, y + 2, 0xFFF6C8);
    }

    /// <summary>
    /// A heater shield: flat on top, straight sides, ending in a point. A brass rim round a walnut face. Returns the face, where
    /// the cap is engraved.
    /// </summary>
    public static CapRect Shield(CapSheet p, int x, int y, int w, int h)
    {
        int Half(int row, int inset)
        {
            // How many cells each side is pulled in at a row: none down to 55% of the height, then a curve to the point.
            var from = (int)(h * 0.55);
            if (row <= from) return inset;
            var t = (row - from) / (double)Math.Max(1, h - 1 - from);
            return inset + (int)Math.Round(Math.Pow(t, 0.9) * ((w / 2.0) - inset - 1));
        }

        for (var row = 0; row < h; row++)
        {
            var i = Half(row, 0);
            p.Rect(x + i, y + row, w - (2 * i), 1, Ink);
        }

        for (var row = 1; row < h - 1; row++)
        {
            var i = Half(row, 1);
            var shade = CapSheet.Mix(BrassHigh, BrassLow, row / (double)h);
            p.Rect(x + i, y + row, w - (2 * i), 1, shade);
        }

        for (var row = 3; row < h - 3; row++)
        {
            var i = Half(row, 3);
            var shade = CapSheet.Mix(0x4A2A14, 0x241208, row / (double)h);
            p.Rect(x + i, y + row, w - (2 * i), 1, shade);
        }

        p.HLine(x + 3, y + 1, w - 6, Shine);
        p.HLine(x + 4, y + 3, w - 8, CapSheet.Mix(0x4A2A14, 0xFFFFFF, 0.12));
        for (var row = 4; row < h * 0.5; row++) p.Set(x + 4, y + row, CapSheet.Mix(0x4A2A14, 0xFFFFFF, 0.1));

        // The two rivets on the rim.
        p.Set(x + 2, y + 2, Shine);
        p.Set(x + w - 3, y + 2, BrassLow);
        return new CapRect(x + 4, y + 4, w - 8, (int)(h * 0.62));
    }

    // ---------------------------------------------------------------- chips, gems, bars

    public static void Chip(CapSheet p, int x, int y, string text, uint fill, uint ink, uint rim)
    {
        var w = CapSheet.SmallWidth(text) + 4;
        p.Notched(x, y, w, 7, rim, 1);
        p.Notched(x + 1, y + 1, w - 2, 5, fill, 1);
        p.Small(text, x + 2, y + 1, ink);
    }

    public static int ChipWidth(string text) => CapSheet.SmallWidth(text) + 4;

    public static void TypeChip(CapSheet p, int x, int y, int type)
    {
        var colour = TypeColour(type);
        var name = TypeName(type);
        var w = ChipWidth(name);
        p.Notched(x, y, w, 7, CapSheet.Darker(colour, 0.55), 1);
        p.Notched(x + 1, y + 1, w - 2, 5, colour, 1);
        p.HLine(x + 2, y + 1, w - 4, CapSheet.Lighter(colour, 0.35));
        var ink = Luma(colour) > 150 ? 0x241408u : 0xFFFFFFu;
        p.Small(name, x + 2, y + 1, ink);
    }

    public static int TypeChipWidth(int type) => ChipWidth(TypeName(type));

    private static double Luma(uint c) => (0.30 * ((c >> 16) & 255)) + (0.59 * ((c >> 8) & 255)) + (0.11 * (c & 255));

    public static (string Text, uint Colour) StatusLook(CapStatus status) => status switch
    {
        CapStatus.Poison => ("VEN", 0xA040A0u),
        CapStatus.Burn => ("QUE", 0xF08030u),
        CapStatus.Paralysis => ("PAR", 0xF8D030u),
        CapStatus.Sleep => ("DOR", 0x8C888Cu),
        CapStatus.Freeze => ("CON", 0x98D8D8u),
        _ => ("", 0u)
    };

    public static void StatusChip(CapSheet p, int x, int y, CapStatus status)
    {
        if (status == CapStatus.None) return;
        var (text, colour) = StatusLook(status);
        var w = ChipWidth(text);
        p.Notched(x, y, w, 7, Ink, 1);
        p.Notched(x + 1, y + 1, w - 2, 5, colour, 1);
        var ink = Luma(colour) > 140 ? 0x241408u : 0xFFFFFFu;
        p.Small(text, x + 2, y + 1, ink);
    }

    public static int StatusChipWidth(CapStatus status) => ChipWidth(StatusLook(status).Text);

    /// <summary>A trial gem, a diamond: lit gold when it is cleared, an empty dark setting when not.</summary>
    public static void Gem(CapSheet p, int x, int y, int size, bool lit)
    {
        var centre = (size - 1) / 2.0;
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                var distance = Math.Abs(col - centre) + Math.Abs(row - centre);
                if (distance > centre + 0.5) continue;

                var edge = distance > centre - 0.6;
                uint colour = lit ? (edge ? 0xC08A20u : row < centre ? Shine : Gold) : (edge ? 0x4A3418u : 0x241810u);
                p.Set(x + col, y + row, colour);
            }
        }
    }


    /// <summary>The groove of an HP bar, with a bevelled rim: the fill is put in it by <see cref="BarFill"/>.</summary>
    public static void BarFrame(CapSheet p, int x, int y, int w, int h)
    {
        p.Rect(x, y, w, h, Ink);
        p.Rect(x + 1, y + 1, w - 2, h - 2, 0x20150C);
        p.HLine(x + 1, y + h - 1, w - 2, 0x5A3A10, 140);
    }

    /// <summary>The fill of a bar, in a groove drawn by <see cref="BarFrame"/>; <paramref name="from"/> and <paramref name="to"/> are cells of the inside.</summary>
    public static void BarFill(CapSheet p, int x, int y, int w, int h, int from, int to, uint fill, bool ticks = true)
    {
        if (to <= from) return;
        var left = x + 1 + from;
        var width = Math.Min(to, w - 2) - from;
        if (width <= 0) return;
        p.Rect(left, y + 1, width, h - 2, fill);
        if (h >= 4) p.HLine(left, y + 1, width, CapSheet.Lighter(fill, 0.45));
        if (h >= 5) p.HLine(left, y + h - 2, width, CapSheet.Darker(fill, 0.3));
        if (ticks && h >= 4)
        {
            for (var t = x + 6; t < left + width; t += 5)
            {
                if (t >= left) p.Set(t, y + (h / 2), CapSheet.Darker(fill, 0.22));
            }
        }
    }

    public static uint HpColour(double share) => share > 0.5 ? HpGreen : share > 0.2 ? HpAmber : HpRed;

    // ---------------------------------------------------------------- alcoves, sprites, graves

    /// <summary>
    /// A Pokémon's window: a rimmed box whose floor takes the colour of its first type, dithered towards the bottom. Returns the
    /// inside.
    /// </summary>
    public static CapRect Alcove(CapSheet p, int x, int y, int w, int h, uint tint, bool fallen, uint rim = Brown)
    {
        p.Notched(x, y, w, h, rim, 2);
        var floor = fallen ? 0x2E2A28u : CapSheet.Mix(0x2A1C10, tint, 0.45);
        p.Notched(x + 1, y + 1, w - 2, h - 2, floor, 2);
        var from = (int)(h * 0.62);
        for (var yy = from; yy < h - 1; yy++)
        {
            for (var xx = 1; xx < w - 1; xx++)
            {
                if ((xx + yy) % 2 == 0 && (yy > from + 1 || xx % 2 == 0)) p.Set(x + xx, y + yy, CapSheet.Lighter(floor, 0.16));
            }
        }

        p.HLine(x + 2, y + 1, w - 4, CapSheet.Lighter(floor, 0.28));
        return new CapRect(x + 1, y + 1, w - 2, h - 2);
    }

    /// <summary>
    /// The icon in a window: at its own size (<paramref name="half"/> false) or at half, one cell of each four. A fallen one is
    /// grey and faint. Clipped to the window.
    /// </summary>
    public static void Sprite(CapSheet p, CapRect window, BitmapSource? sprite, bool half, bool fallen)
    {
        if (ToastPixels.SpritePixels.From(sprite) is not { } pixels) return;

        var w = half ? Math.Min(window.Width, pixels.Width / 2) : Math.Min(window.Width, pixels.Width);
        var h = half ? Math.Min(window.Height, pixels.Height / 2) : Math.Min(window.Height, pixels.Height);
        var left = window.Left + ((window.Width - w) / 2);
        var top = window.Top + ((window.Height - h) / 2);

        // Sources bigger than the window are cropped from their middle, not from their corner.
        var srcW = half ? w * 2 : w;
        var srcH = half ? h * 2 : h;
        var offX = Math.Max(0, (pixels.Width - srcW) / 2);
        var offY = Math.Max(0, (pixels.Height - srcH) / 2);

        for (var sy = 0; sy < h; sy++)
        {
            for (var sx = 0; sx < w; sx++)
            {
                if (!half)
                {
                    PutSource(p, pixels, left + sx, top + sy, offX + sx, offY + sy, fallen);
                    continue;
                }

                foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                {
                    if (!pixels.Solid(offX + (sx * 2) + dx, offY + (sy * 2) + dy)) continue;
                    PutSource(p, pixels, left + sx, top + sy, offX + (sx * 2) + dx, offY + (sy * 2) + dy, fallen);
                    break;
                }
            }
        }
    }

    private static void PutSource(CapSheet p, ToastPixels.SpritePixels pixels, int x, int y, int sx, int sy, bool fallen)
    {
        if (!pixels.Solid(sx, sy)) return;
        var c = pixels.At(sx, sy);
        var rgb = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        if (fallen)
        {
            var luma = (int)Math.Round((0.30 * c.R) + (0.59 * c.G) + (0.11 * c.B));
            rgb = ((uint)luma << 16) | ((uint)luma << 8) | (uint)luma;
        }

        p.Set(x, y, rgb, fallen ? 105 : 255);
    }

    /// <summary>A tombstone with an arched top: grey stone, a cross cut in it, and the Pokémon's face grey and faint if there is one.</summary>
    public static void Tombstone(CapSheet p, int x, int y, int w, int h, BitmapSource? sprite, int seed)
    {
        var arch = Math.Max(3, w / 2);
        for (var row = 0; row < h; row++)
        {
            var inset = row < arch ? arch - (int)Math.Round(Math.Sqrt((arch * arch) - ((arch - row - 0.5) * (arch - row - 0.5)))) : 0;
            var shade = CapSheet.Mix(0x8C8A92, 0x4E4C56, row / (double)h);
            p.Rect(x + inset, y + row, w - (2 * inset), 1, Ink);
            if (row >= 1 && row < h - 1) p.Rect(x + inset + 1, y + row, w - (2 * inset) - 2, 1, shade);
        }

        p.VLine(x + 1, y + arch, h - arch - 1, 0xB8B6C0);
        p.VLine(x + w - 2, y + arch, h - arch - 1, 0x3A3842);
        p.HLine(x + 2, y + h - 2, w - 4, 0x3A3842);

        // Moss at the foot, a different patch on each.
        for (var i = 0; i < 4; i++) p.Set(x + 2 + (Hash(seed, i) % Math.Max(1, w - 4)), y + h - 3 - (Hash(seed, i + 9) % 2), 0x4A6A3A);

        if (sprite is not null)
        {
            var window = new CapRect(x + 2, y + 2, w - 4, Math.Min(h - 5, 15));
            Sprite(p, window, sprite, half: true, fallen: true);
        }
        else
        {
            var cx = x + (w / 2);
            p.VLine(cx, y + 4, 8, 0x3A3842);
            p.HLine(cx - 2, y + 6, 5, 0x3A3842);
        }
    }

    // ---------------------------------------------------------------- the pennant and the plinth

    public static void Pennant(CapSheet p, int x, int y, uint colour)
    {
        p.VLine(x, y, 13, Brown);
        for (var i = 0; i < 5; i++) p.Rect(x + 1, y + 1 + i, 8 - (i * 2), 1, colour);
        p.Set(x + 1, y + 1, CapSheet.Lighter(colour, 0.4));
    }

    /// <summary>The base of the case: a plank with a brass strip, and, if it is tall enough, the name engraved on it.</summary>
    public static void Plinth(CapSheet p, CapRect box, string? engraved)
    {
        p.VGrad(box.Left, box.Top, box.Width, box.Height, CapSheet.Lighter(WalnutDark, 0.1), WalnutDark);
        p.HLine(box.Left, box.Top, box.Width, Ink);
        p.HLine(box.Left, box.Top + 1, box.Width, GoldDim);
        if (engraved is null || box.Height < 12) return;

        var width = CapSheet.SmallWidth(engraved);
        var x = box.Left + ((box.Width - width) / 2);
        var y = box.Top + 2 + ((box.Height - 2 - 5) / 2);
        p.Small(engraved, x, y + 1, Ink);
        p.Small(engraved, x, y, GoldDim);

        // A rule either side of it.
        var rule = (box.Width - width - 16) / 2;
        if (rule < 6) return;
        p.HLine(box.Left + 6, y + 2, rule - 4, GoldDim, 150);
        p.HLine(box.Right - 6 - (rule - 4), y + 2, rule - 4, GoldDim, 150);
    }
}
