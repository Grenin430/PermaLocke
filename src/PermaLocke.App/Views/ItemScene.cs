namespace PermaLocke.App.Views;

/// <summary>When each thing happens as a picked-up item goes into the bag, in seconds (2026-09-28).</summary>
public static class ItemTimeline
{
    public const double In = 0.3;
    public const double Pop = 0.35;
    public const double Shown = 0.65;
    public const double Drop = 1.55;
    public const double Inside = 1.85;
    public const double Out = 2.7;
    public const double Fade = 0.35;
    public const double Length = Out + Fade;
}

/// <summary>
/// An item just picked up, bought or given, going into the bag (2026-09-28, asked by the organiser): the cousin of the
/// card that flies into the album (§190), small and quiet, in the dark band beside the emulator's bottom screen.
/// </summary>
/// <remarks>
/// <para>
/// A leather bag with its buckle and stitching on a patch of shadow; the item's own icon from the cartridge pops out
/// above it with a glint running across, floats, drops in, and the bag hops shut with a few sparks. Beside it a plate
/// like the game's own item tags says what it was and how many. Everything fades in and out through the dither.
/// </para>
/// <para>
/// Every cell is a pixel of the game, so it matches the emulator next to it. Pure: a moment makes a frame,
/// premultiplied BGRA, transparent around the drawing.
/// </para>
/// </remarks>
public sealed class ItemScene
{
    /// <summary>The scene, in pixels of the game.</summary>
    public const int SceneWidth = 150;
    public const int SceneHeight = 64;

    private const int BagLeft = 6;
    private const int BagBottom = 58;
    private const int BagWidth = 24;
    private const double IconX = BagLeft + (BagWidth / 2.0);
    private const double IconRestY = 20;
    private const int PlateLeft = 38;
    private const int PlateTop = 24;
    private const int PlateHeight = 24;

    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['o'] = Bgra(0x0B, 0x09, 0x10),
        ['d'] = Bgra(0x7A, 0x32, 0x1A),
        ['m'] = Bgra(0xB8, 0x52, 0x28),
        ['l'] = Bgra(0xD8, 0x74, 0x3C),
        ['h'] = Bgra(0xF2, 0xA0, 0x62),
        ['f'] = Bgra(0x94, 0x3E, 0x20),
        ['F'] = Bgra(0xC0, 0x5C, 0x30),
        ['g'] = Bgra(0xE0, 0xA8, 0x3A),
        ['G'] = Bgra(0xFF, 0xEA, 0xA0),
        ['k'] = Bgra(0x8E, 0x60, 0x1C),
        ['s'] = Bgra(0xF6, 0xC8, 0x94),
        ['p'] = Bgra(0x9A, 0x44, 0x22),
        ['q'] = Bgra(0x86, 0x3A, 0x1C),
        ['K'] = Bgra(0x24, 0x10, 0x0C),
    };

    private static readonly string[] BagClosed =
    [
        "........oooooooo........",
        ".......oddddddddo.......",
        "......odoo....oodo......",
        "......odo......odo......",
        "...oooooooooooooooooo...",
        "..oFFFFFFFFFFFFFFFFFFo..",
        ".oFfffffffffffffffffffo.",
        ".oFffffffffGgfffffffffo.",
        ".oddddddddkGGkddddddddo.",
        ".ohlmmmmmmmkgkmmmmmmmdo.",
        ".ohlmmmmmmmmmmmmmmmmmdo.",
        ".ohlmmppppppppppppmmmdo.",
        ".ohlmmpqqqqqqqqqqpmmmdo.",
        ".ohlmmpqqqqqqqqqqpmmmdo.",
        ".olmmmppppppppppppmmmdo.",
        ".olmmmmmmmmmmmmmmmmmmdo.",
        ".olmsmsmsmsmsmsmsmsmmdo.",
        ".olmmmmmmmmmmmmmmmmmmdo.",
        "..oldddddddddddddddddo..",
        "...oooooooooooooooooo...",
    ];

    private static readonly string[] BagOpen =
    [
        "........oooooooo........",
        ".......oddddddddo.......",
        "......odoo....oodo......",
        "......odo......odo......",
        "...oooooooooooooooooo...",
        "..oKKKKKKKKKKKKKKKKKKo..",
        ".oKKKKKKKKKKKKKKKKKKKKo.",
        ".odKKKKKKKKKKKKKKKKKKdo.",
        ".oddddddddddddddddddddo.",
        ".ohlmmmmmmmmmmmmmmmmmdo.",
        ".ohlmmmmmmmmmmmmmmmmmdo.",
        ".ohlmmppppppppppppmmmdo.",
        ".ohlmmpqqqqqqqqqqpmmmdo.",
        ".ohlmmpqqqqqqqqqqpmmmdo.",
        ".olmmmppppppppppppmmmdo.",
        ".olmmmmmmmmmmmmmmmmmmdo.",
        ".olmsmsmsmsmsmsmsmsmmdo.",
        ".olmmmmmmmmmmmmmmmmmmdo.",
        "..oldddddddddddddddddo..",
        "...oooooooooooooooooo...",
    ];

    private static readonly uint Outline = Bgra(0x0B, 0x09, 0x10);
    private static readonly uint Shadow = Bgra(0x14, 0x10, 0x1C);
    private static readonly uint PlateFill = Bgra(0x1C, 0x15, 0x30);
    private static readonly uint PlateLight = Bgra(0x3A, 0x2E, 0x5C);
    private static readonly uint PlateDark = Bgra(0x10, 0x0C, 0x1C);
    private static readonly uint Accent = Bgra(0xE0, 0xA8, 0x3A);
    private static readonly uint AccentLight = Bgra(0xFF, 0xEA, 0xA0);
    private static readonly uint Text = Bgra(0xF4, 0xF0, 0xFF);
    private static readonly uint TextDim = Bgra(0x9A, 0x90, 0xB8);
    private static readonly uint Gold = Bgra(0xFF, 0xDC, 0x7A);
    private static readonly uint White = Bgra(0xFA, 0xF8, 0xFF);

    private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <param name="pixel">Monitor pixels per pixel of the game.</param>
    public ItemScene(double pixel)
    {
        Pixel = Math.Max(1, pixel);
        Cell = Math.Max(1, (int)Math.Round(Pixel));
        Width = (int)Math.Ceiling(SceneWidth * Pixel);
        Height = (int)Math.Ceiling(SceneHeight * Pixel);
        Pixels = new byte[Width * Height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public double Pixel { get; }

    private int Cell { get; }

    /// <summary>The frame, premultiplied BGRA.</summary>
    public byte[] Pixels { get; }

    /// <summary>What the scene shows: the icon as BGRA, its name and how many.</summary>
    public sealed record Item(byte[] Icon, int IconWidth, int IconHeight, string Name, int Amount);

    /// <summary>How much of the drawing is on: faded in and out through the dither.</summary>
    private double _alpha;

    public void Render(Item item, double t)
    {
        Array.Clear(Pixels);
        if (t < 0 || t >= ItemTimeline.Length) return;

        _alpha = t < ItemTimeline.In ? t / ItemTimeline.In
            : t > ItemTimeline.Out ? 1 - ((t - ItemTimeline.Out) / ItemTimeline.Fade)
            : 1;
        var rise = (int)Math.Round((1 - Ease(Math.Min(1, t / ItemTimeline.In))) * 6);

        var hop = t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.2
            ? (int)Math.Round(Math.Sin((t - ItemTimeline.Inside) / 0.2 * Math.PI) * 3)
            : 0;

        DrawShadow(rise, hop);
        DrawBag(BagBottom + rise - hop, open: t is >= ItemTimeline.Pop and < ItemTimeline.Inside + 0.05);

        if (t is >= ItemTimeline.Pop and < ItemTimeline.Inside)
        {
            var (y, scale) = IconPose(t);
            DrawIcon(item, IconX, y + rise, scale, t);
        }

        if (t is >= ItemTimeline.Pop and < ItemTimeline.Pop + 0.45)
        {
            Sparks(IconX, IconRestY + rise, t - ItemTimeline.Pop, 5, 16);
        }

        if (t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.5)
        {
            Sparks(IconX, BagBottom - 16 + rise, t - ItemTimeline.Inside, 6, 12);
        }

        var plateIn = Math.Clamp((t - 0.2) / 0.3, 0, 1);
        if (plateIn > 0) DrawPlate(item, rise, (int)Math.Round((1 - Ease(plateIn)) * -8));
    }

    private static double Ease(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 3);

    /// <summary>Where the icon's middle is and how big, in game pixels per icon pixel.</summary>
    private static (double Y, double Scale) IconPose(double t)
    {
        if (t < ItemTimeline.Shown)
        {
            // Sale de la mochila hacia arriba con un rebote.
            var p = (t - ItemTimeline.Pop) / (ItemTimeline.Shown - ItemTimeline.Pop);
            var y = (BagBottom - 14) + ((IconRestY - (BagBottom - 14)) * Ease(p)) - (Math.Sin(p * Math.PI) * 4);
            return (y, 0.4 + (0.6 * Ease(p)));
        }

        if (t < ItemTimeline.Drop)
        {
            return (IconRestY + Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.5), 1);
        }

        // Cae dentro, encogiendo.
        var f = (t - ItemTimeline.Drop) / (ItemTimeline.Inside - ItemTimeline.Drop);
        return (IconRestY - (Math.Sin(f * Math.PI) * 3) + ((BagBottom - 14 - IconRestY) * f * f), 1 - (f * 0.6));
    }

    private void DrawShadow(int rise, int hop)
    {
        const double Cx = BagLeft + (BagWidth / 2.0);
        var rx = 13 - hop;
        for (var y = BagBottom - 1; y <= BagBottom + 2; y++)
        {
            for (var x = (int)(Cx - rx); x <= (int)(Cx + rx); x++)
            {
                var d = Math.Pow((x + 0.5 - Cx) / rx, 2) + Math.Pow((y + 0.5 - (BagBottom + 0.5)) / 2.5, 2);
                if (d <= 1 && Bayer[y & 3, x & 3] < (1 - d) * 22) Put(x, y + rise, Shadow);
            }
        }
    }

    private void DrawBag(int bottom, bool open)
    {
        var art = open ? BagOpen : BagClosed;
        var top = bottom - art.Length;
        for (var y = 0; y < art.Length; y++)
        {
            for (var x = 0; x < art[y].Length; x++)
            {
                if (Palette.TryGetValue(art[y][x], out var colour)) Put(BagLeft + x, top + y, colour);
            }
        }
    }

    private void DrawIcon(Item item, double cx, double cy, double scale, double t)
    {
        if (scale <= 0.05) return;

        var width = item.IconWidth * scale;
        var height = item.IconHeight * scale;
        var left = cx - (width / 2);
        var top = cy - (height / 2);

        // Un brillo en diagonal que cruza el objeto justo al salir.
        var shine = (t - ItemTimeline.Shown + 0.05) / 0.35;

        for (var gy = (int)Math.Floor(top); gy < (int)Math.Ceiling(top + height); gy++)
        {
            for (var gx = (int)Math.Floor(left); gx < (int)Math.Ceiling(left + width); gx++)
            {
                var sx = (int)Math.Floor((gx + 0.5 - left) / scale);
                var sy = (int)Math.Floor((gy + 0.5 - top) / scale);
                if (sx < 0 || sy < 0 || sx >= item.IconWidth || sy >= item.IconHeight) continue;

                var i = ((sy * item.IconWidth) + sx) * 4;
                if (item.Icon[i + 3] < 128) continue;

                var colour = item.Icon[i] | ((uint)item.Icon[i + 1] << 8) | ((uint)item.Icon[i + 2] << 16) | 0xFF000000;
                var band = ((sx + sy) / (double)(item.IconWidth + item.IconHeight)) - shine;
                if (shine is > 0 and < 1.3 && Math.Abs(band) < 0.08) colour = Lerp(colour, White, 0.7);

                Put(gx, gy, colour);
            }
        }
    }

    /// <summary>Four-point sparks thrown out from a point, fading.</summary>
    private void Sparks(double cx, double cy, double since, int count, double reach)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (i / (double)count * Math.PI * 2) + 0.6;
            var r = 4 + (reach * Ease(since / 0.45));
            var x = (int)Math.Floor(cx + (Math.Cos(a) * r));
            var y = (int)Math.Floor(cy + (Math.Sin(a) * r * 0.8));
            var big = since < 0.2;

            Put(x, y, White);
            if (big || i % 2 == 0)
            {
                var arm = big ? White : Gold;
                Put(x - 1, y, arm);
                Put(x + 1, y, arm);
                Put(x, y - 1, arm);
                Put(x, y + 1, arm);
            }
        }
    }

    /// <summary>The tag beside the bag: a gold edge, what it was, and how many.</summary>
    private void DrawPlate(Item item, int rise, int slide)
    {
        var name = Fit(item.Name, SceneWidth - PlateLeft - 12);
        var amount = item.Amount > 1 ? $"×{item.Amount}" : "NUEVO";
        var width = Math.Max(TextWidth(name), TextWidth("A LA MOCHILA") + 4 + TextWidth(amount)) + 12;
        var left = PlateLeft + slide;
        var top = PlateTop + rise;

        for (var y = 0; y < PlateHeight; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Esquinas recortadas un píxel, como las placas del kit.
                var corner = (x == 0 || x == width - 1) && (y == 0 || y == PlateHeight - 1);
                if (corner) continue;

                var edge = x == 0 || x == width - 1 || y == 0 || y == PlateHeight - 1;
                var colour = edge ? Outline
                    : x <= 2 ? (y <= 2 ? AccentLight : Accent)
                    : y == 1 ? PlateLight
                    : y == PlateHeight - 2 ? PlateDark
                    : PlateFill;
                Put(left + x, top + y, colour);
            }
        }

        // Sombra de la placa.
        for (var x = 2; x < width + 1; x++) Put(left + x, top + PlateHeight, Shadow);

        var textLeft = left + 6;
        Write(name, textLeft, top + 5, Text);
        Write("A LA MOCHILA", textLeft, top + 14, TextDim);
        Write(amount, textLeft + TextWidth("A LA MOCHILA") + 4, top + 14, Gold);
    }

    private static int TextWidth(string text) => text.Length == 0 ? 0 : (text.Length * 4) - 1;

    private static string Fit(string text, int cells)
    {
        var max = (cells + 1) / 4;
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + ".";
    }

    /// <summary>The small font, one game pixel per dot, with a shadow under it.</summary>
    private void Write(string text, int left, int top, uint colour)
    {
        foreach (var (ox, oy, c) in new[] { (1, 1, PlateDark), (0, 0, colour) })
        {
            var x = left + ox;
            foreach (var raw in text)
            {
                var ch = raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', 'Ü' => 'U', '·' => '-', _ => raw };
                if (ch != raw && raw != '·') Put(x + 1, top + oy - 2, c);
                if (raw == 'Ñ') for (var i = 0; i < 3; i++) Put(x + i, top + oy - 2, c);
                if (raw == '×')
                {
                    Put(x, top + oy + 1, c); Put(x + 2, top + oy + 1, c); Put(x + 1, top + oy + 2, c);
                    Put(x, top + oy + 3, c); Put(x + 2, top + oy + 3, c);
                }
                else if (SmallFont.Glyph(ch) is { } glyph)
                {
                    for (var gy = 0; gy < 5; gy++)
                    {
                        for (var gx = 0; gx < 3; gx++)
                        {
                            if (glyph[gy][gx] == '#') Put(x + gx, top + oy + gy, c);
                        }
                    }
                }

                x += 4;
            }
        }
    }

    /// <summary>One pixel of the game, as a block of monitor pixels, through the fade.</summary>
    private void Put(int gx, int gy, uint colour)
    {
        if (gx < 0 || gy < 0 || gx >= SceneWidth || gy >= SceneHeight) return;
        if (_alpha < 1 && Bayer[gy & 3, gx & 3] >= _alpha * 16) return;

        var left = (int)Math.Round(gx * Pixel);
        var top = (int)Math.Round(gy * Pixel);
        for (var y = top; y < Math.Min(Height, top + Cell); y++)
        {
            for (var x = left; x < Math.Min(Width, left + Cell); x++)
            {
                var i = ((y * Width) + x) * 4;
                Pixels[i] = (byte)colour;
                Pixels[i + 1] = (byte)(colour >> 8);
                Pixels[i + 2] = (byte)(colour >> 16);
                Pixels[i + 3] = 0xFF;
            }
        }
    }

    private static uint Lerp(uint a, uint b, double t)
    {
        byte Mix(int shift) => (byte)(((a >> shift) & 0xFF) + ((((int)((b >> shift) & 0xFF)) - (int)((a >> shift) & 0xFF)) * t));
        return Mix(0) | ((uint)Mix(8) << 8) | ((uint)Mix(16) << 16) | 0xFF000000;
    }

    /// <summary>A wrapped parcel, for an item whose icon the cartridge index does not have.</summary>
    public static (byte[] Pixels, int Width, int Height) Parcel()
    {
        string[] art =
        [
            "....oo..oo....",
            "...oyyooyyo...",
            "....oyyyyo....",
            "oooooooooooooo",
            "obbbbbyybbbbbo",
            "obbbbbyybbbbbo",
            "oooooooooooooo",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oooooooooooo.",
        ];
        var colours = new Dictionary<char, uint>
        {
            ['o'] = Outline, ['y'] = Bgra(0xFF, 0xDC, 0x7A), ['b'] = Bgra(0xB0, 0x7B, 0xF0), ['B'] = Bgra(0x7A, 0x4C, 0xC0)
        };

        var width = art[0].Length;
        var pixels = new byte[width * art.Length * 4];
        for (var y = 0; y < art.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!colours.TryGetValue(art[y][x], out var c)) continue;
                BitConverter.GetBytes(c).CopyTo(pixels, ((y * width) + x) * 4);
            }
        }

        return (pixels, width, art.Length);
    }

    private static uint Bgra(byte r, byte g, byte b) => b | ((uint)g << 8) | ((uint)r << 16) | 0xFF000000;
}
