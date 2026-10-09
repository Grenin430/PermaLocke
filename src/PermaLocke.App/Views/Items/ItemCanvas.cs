namespace PermaLocke.App.Views;

/// <summary>
/// The trunk every item animation is drawn on (2026-10-09): the pixels, the dither that fades the scene in and out, and the
/// pieces they all share, the bag, its shadow, the item's icon and the plate with its name. A style
/// (<see cref="ItemStyle"/>) draws on it and nothing else, so a category can be added or retouched without touching the others.
/// </summary>
/// <remarks>
/// <para>
/// Every cell is a pixel of the game, so it matches the emulator next to it. One game pixel is a block of monitor pixels
/// that goes from <c>round(gx * Pixel)</c> to <c>round((gx + 1) * Pixel)</c>: whatever the size, the blocks tile the frame with
/// no hole and no overlap (§242).
/// </para>
/// <para>
/// Premultiplied BGRA, transparent around the drawing, and nothing allocated per frame.
/// </para>
/// </remarks>
public sealed class ItemCanvas
{
    private static readonly uint[] BagColours = BuildBagColours();

    public static readonly uint Outline = Bgra(0x0B, 0x09, 0x10);
    public static readonly uint Shadow = Bgra(0x14, 0x10, 0x1C);
    public static readonly uint White = Bgra(0xFA, 0xF8, 0xFF);
    public static readonly uint Gold = Bgra(0xFF, 0xDC, 0x7A);

    private static readonly uint PlateFill = Bgra(0x1C, 0x15, 0x30);
    private static readonly uint PlateLight = Bgra(0x3A, 0x2E, 0x5C);
    private static readonly uint PlateDark = Bgra(0x10, 0x0C, 0x1C);
    private static readonly uint Accent = Bgra(0xE0, 0xA8, 0x3A);
    private static readonly uint AccentLight = Bgra(0xFF, 0xEA, 0xA0);
    private static readonly uint Text = Bgra(0xF4, 0xF0, 0xFF);
    private static readonly uint TextDim = Bgra(0x9A, 0x90, 0xB8);

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

    /// <summary>The 4x4 ordered dither: a cell is on when its number is under the density times sixteen.</summary>
    public static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// <param name="pixel">Monitor pixels per pixel of the game.</param>
    /// <param name="gameHeight">The height of the scene in pixels of the game, which its style declares.</param>
    public ItemCanvas(double pixel, int gameHeight)
    {
        Pixel = Math.Max(1, pixel);
        GameHeight = gameHeight;
        Width = (int)Math.Ceiling(ItemScene.SceneWidth * Pixel);
        Height = (int)Math.Ceiling(gameHeight * Pixel);
        Pixels = new byte[Width * Height * 4];
    }

    public double Pixel { get; }

    public int GameHeight { get; }

    /// <summary>The frame in monitor pixels.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame, premultiplied BGRA.</summary>
    public byte[] Pixels { get; }

    /// <summary>How much of the drawing is on: 0 to 1, faded through the dither. A style may lower it for a piece of its own.</summary>
    public double Alpha { get; set; } = 1;

    /// <summary>
    /// How many times something was asked to be drawn outside the scene since <see cref="Clear"/>. A style that stays inside the
    /// height it declares always leaves it at zero: the tests look at it.
    /// </summary>
    public int Overdraw { get; private set; }

    public void Clear()
    {
        Array.Clear(Pixels);
        Overdraw = 0;
        Alpha = 1;
    }

    public bool Contains(int gx, int gy) => gx >= 0 && gy >= 0 && gx < ItemScene.SceneWidth && gy < GameHeight;

    /// <summary>One pixel of the game, as a block of monitor pixels, through the fade.</summary>
    public void Put(int gx, int gy, uint colour)
    {
        if (Alpha < 1 && Bayer[gy & 3, gx & 3] >= Alpha * 16) return;

        if (!Contains(gx, gy))
        {
            Overdraw++;
            return;
        }

        // From where this pixel starts to where the next one does: a block of a fixed size left holes (transparent lines
        // across the drawing) whenever the game's pixel was not a whole number of monitor pixels.
        var left = (int)Math.Round(gx * Pixel);
        var top = (int)Math.Round(gy * Pixel);
        var right = Math.Max(left + 1, (int)Math.Round((gx + 1) * Pixel));
        var bottom = Math.Max(top + 1, (int)Math.Round((gy + 1) * Pixel));
        for (var y = top; y < Math.Min(Height, bottom); y++)
        {
            for (var x = left; x < Math.Min(Width, right); x++)
            {
                var i = ((y * Width) + x) * 4;
                Pixels[i] = (byte)colour;
                Pixels[i + 1] = (byte)(colour >> 8);
                Pixels[i + 2] = (byte)(colour >> 16);
                Pixels[i + 3] = 0xFF;
            }
        }
    }

    /// <summary>A patch of shadow on the ground, in dither.</summary>
    /// <param name="centreX">Middle of the patch.</param>
    /// <param name="groundY">The row the bag stands on.</param>
    /// <param name="radius">Half of its width.</param>
    public void ShadowPatch(double centreX, int groundY, int radius)
    {
        for (var y = groundY - 1; y <= groundY + 2; y++)
        {
            for (var x = (int)(centreX - radius); x <= (int)(centreX + radius); x++)
            {
                if (!Contains(x, y)) continue;

                var d = Math.Pow((x + 0.5 - centreX) / radius, 2) + Math.Pow((y + 0.5 - (groundY + 0.5)) / 2.5, 2);
                if (d <= 1 && Bayer[y & 3, x & 3] < (1 - d) * 22) Put(x, y, Shadow);
            }
        }
    }

    /// <summary>
    /// The leather bag. Squashed or stretched by resampling its rows and columns, so it stays pixel art at any shape.
    /// </summary>
    /// <param name="left">Left of the art at its natural size.</param>
    /// <param name="bottom">Row under its last one: the bag keeps its feet on it.</param>
    /// <param name="scaleX">Width, 1 is natural.</param>
    /// <param name="scaleY">Height, 1 is natural.</param>
    /// <param name="shiver">Columns it is moved sideways, for a tremble.</param>
    /// <param name="stitch">Colour for its stitching and buckle shine, or 0 for the leather's own.</param>
    /// <param name="bulgeRow">A row of the art (0 is the handle's top) where the leather swells, for a swallow; below 0 for none.</param>
    /// <param name="bulge">How much it swells there, as a fraction of the bag's width.</param>
    /// <param name="wash">A column of the art (0 to 23) where a band of colour is passing over the leather, in three hard steps;
    /// below 0 for none.</param>
    /// <param name="washColour">The colour of the band.</param>
    /// <param name="washWidth">How many columns it reaches to each side.</param>
    /// <param name="lean">Columns the top of the bag is displaced against its bottom: it bows or leans, row by row.</param>
    /// <param name="flat">When not 0, every cell of the bag but its outline is this colour: a silhouette.</param>
    public void Bag(int left, int bottom, bool open, double scaleX = 1, double scaleY = 1, int shiver = 0, uint stitch = 0,
        double bulgeRow = -1, double bulge = 0, double wash = -1, uint washColour = 0, double washWidth = 6, double lean = 0,
        uint flat = 0)
    {
        var art = open ? BagOpen : BagClosed;
        var rows = art.Length;
        var columns = art[0].Length;
        var height = Math.Max(1, (int)Math.Round(rows * scaleY));
        var top = bottom - height;

        for (var dy = 0; dy < height; dy++)
        {
            var sy = Math.Min(rows - 1, (int)((dy + 0.5) * rows / height));

            // Each row has its own width, so that a bulge can travel down the bag like something being swallowed.
            var swell = bulge > 0 ? 1 + (bulge * Math.Exp(-((sy - bulgeRow) * (sy - bulgeRow)) / 5)) : 1;
            var width = Math.Max(1, (int)Math.Round(columns * scaleX * swell));
            var start = left + ((columns - width) / 2) + shiver;
            if (lean != 0) start += (int)Math.Round(lean * (height - 1 - dy) / Math.Max(1, height - 1));

            for (var dx = 0; dx < width; dx++)
            {
                var sx = Math.Min(columns - 1, (int)((dx + 0.5) * columns / width));
                var ch = art[sy][sx];
                if (ch == '.') continue;

                var colour = stitch != 0 && ch is 's' or 'G' ? stitch : BagColours[ch];

                // Its silhouette in one colour, outline apart: what a bag looks like for a frame when something changes in it.
                if (flat != 0 && ch != 'o') colour = flat;
                if (wash >= 0)
                {
                    // Three hard steps and no blend between them: a band of recoloured leather moving across the bag.
                    var away = Math.Abs(sx - wash) / washWidth;
                    if (away < 1) colour = Lerp(colour, washColour, away < 0.34 ? 0.85 : away < 0.67 ? 0.55 : 0.30);
                }

                Put(start + dx, top + dy, colour);
            }
        }
    }

    /// <summary>
    /// The item's own icon from the cartridge, centred on a point and scaled in the two directions with nearest neighbour.
    /// </summary>
    /// <param name="shine">Where a diagonal glint is across the icon, 0 to 1.3; below 0 for none.</param>
    /// <param name="mix">A colour the icon is pulled towards.</param>
    /// <param name="mixAmount">How much: 0 is the icon as it is, 1 is that colour flat.</param>
    public void Icon(ItemScene.Item item, double centreX, double centreY, double scaleX, double scaleY,
        double shine = -1, uint mix = 0, double mixAmount = 0)
    {
        if (scaleX <= 0.05 || scaleY <= 0.05) return;

        var width = item.IconWidth * scaleX;
        var height = item.IconHeight * scaleY;
        var left = centreX - (width / 2);
        var top = centreY - (height / 2);

        for (var gy = (int)Math.Floor(top); gy < (int)Math.Ceiling(top + height); gy++)
        {
            for (var gx = (int)Math.Floor(left); gx < (int)Math.Ceiling(left + width); gx++)
            {
                var sx = (int)Math.Floor((gx + 0.5 - left) / scaleX);
                var sy = (int)Math.Floor((gy + 0.5 - top) / scaleY);
                if (sx < 0 || sy < 0 || sx >= item.IconWidth || sy >= item.IconHeight) continue;
                if (!Contains(gx, gy)) continue;

                var i = ((sy * item.IconWidth) + sx) * 4;
                if (item.Icon[i + 3] < 128) continue;

                var colour = item.Icon[i] | ((uint)item.Icon[i + 1] << 8) | ((uint)item.Icon[i + 2] << 16) | 0xFF000000;
                var band = ((sx + sy) / (double)(item.IconWidth + item.IconHeight)) - shine;
                if (shine is > 0 and < 1.3 && Math.Abs(band) < 0.08) colour = Lerp(colour, White, 0.7);
                if (mixAmount > 0) colour = Lerp(colour, mix, Math.Min(1, mixAmount));

                Put(gx, gy, colour);
            }
        }
    }

    private ItemScene.Item? _plateItem;
    private int _plateLeft;
    private int _plateReserved;
    private string _plateName = string.Empty;
    private string _plateAmount = string.Empty;

    /// <summary>The tag beside the bag: an edge, what it was, and how many. At most what is left of the scene's width.</summary>
    /// <param name="left">Where it sits when it has arrived.</param>
    /// <param name="slide">Columns it is still short of that, while it comes in.</param>
    /// <param name="caption">The second line, instead of «A LA MOCHILA»: the kind of item it is.</param>
    /// <param name="accent">Colour of the edge, instead of gold; <paramref name="accentLight"/> its highlight.</param>
    /// <param name="badge">Cells of a mark of five by five that says the kind at a glance, in the top right corner.</param>
    /// <param name="ornate">A second frame inside the edge, for the plates that are to be solemn.</param>
    public void Plate(ItemScene.Item item, int left, int top, int slide, string? caption = null, uint accent = 0,
        uint accentLight = 0, IReadOnlyList<(int X, int Y)>? badge = null, bool ornate = false)
    {
        const int Height = 24;
        var reserved = badge is null ? 0 : 8;

        // The two strings of the plate are made once for an item and a place, not at every frame: nothing is allocated per frame.
        if (!ReferenceEquals(item, _plateItem) || left != _plateLeft || reserved != _plateReserved)
        {
            _plateItem = item;
            _plateLeft = left;
            _plateReserved = reserved;
            _plateName = Fit(item.Name, ItemScene.SceneWidth - left - 12 - reserved);
            _plateAmount = item.Amount > 1 ? $"×{item.Amount}" : "NUEVO";
        }

        var name = _plateName;
        var amount = _plateAmount;
        var line = caption ?? "A LA MOCHILA";
        var width = Math.Max(TextWidth(name) + reserved, TextWidth(line) + 4 + TextWidth(amount)) + 12;
        var at = left + slide;
        var edge1 = accent != 0 ? accent : Accent;
        var edge2 = accentLight != 0 ? accentLight : AccentLight;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Esquinas recortadas un píxel, como las placas del kit.
                var corner = (x == 0 || x == width - 1) && (y == 0 || y == Height - 1);
                if (corner) continue;

                var edge = x == 0 || x == width - 1 || y == 0 || y == Height - 1;
                var colour = edge ? Outline
                    : x <= 2 ? (y <= 2 ? edge2 : edge1)
                    : y == 1 ? PlateLight
                    : y == Height - 2 ? PlateDark
                    : PlateFill;

                // A second frame inside the first, in the colour of the edge: what the plate of a key item has.
                if (ornate && !edge && x >= 4 && (y == 3 || y == Height - 4 || x == width - 4) && y >= 3 && y <= Height - 4) colour = edge1;

                Put(at + x, top + y, colour);
            }
        }

        // Sombra de la placa.
        for (var x = 2; x < width + 1; x++)
        {
            if (Contains(at + x, top + Height)) Put(at + x, top + Height, Shadow);
        }

        var textLeft = at + 6;
        Write(name, textLeft, top + 5, Text);
        Write(line, textLeft, top + 14, TextDim);
        Write(amount, textLeft + TextWidth(line) + 4, top + 14, Gold);

        if (badge is not null)
        {
            // By index: walking a list through its interface makes an enumerator at every frame.
            for (var i = 0; i < badge.Count; i++)
            {
                Put(at + width - 9 + badge[i].X, top + 4 + badge[i].Y, edge2);
            }
        }
    }

    /// <summary>
    /// The outline of the icon, a cell or two out from its edge, in one colour: what a pulse is drawn with, so that the beat of
    /// a stone shows in a single frame.
    /// </summary>
    /// <param name="reach">How many cells away from the icon still count.</param>
    public void IconOutline(ItemScene.Item item, double centreX, double centreY, double scaleX, double scaleY, uint colour, int reach)
    {
        var width = item.IconWidth * scaleX;
        var height = item.IconHeight * scaleY;
        var left = centreX - (width / 2);
        var top = centreY - (height / 2);

        bool Solid(int gx, int gy)
        {
            var sx = (int)Math.Floor((gx + 0.5 - left) / scaleX);
            var sy = (int)Math.Floor((gy + 0.5 - top) / scaleY);
            return sx >= 0 && sy >= 0 && sx < item.IconWidth && sy < item.IconHeight && item.Icon[(((sy * item.IconWidth) + sx) * 4) + 3] >= 128;
        }

        for (var gy = (int)Math.Floor(top) - reach; gy < (int)Math.Ceiling(top + height) + reach; gy++)
        {
            for (var gx = (int)Math.Floor(left) - reach; gx < (int)Math.Ceiling(left + width) + reach; gx++)
            {
                if (!Contains(gx, gy) || Solid(gx, gy)) continue;

                var near = false;
                for (var dy = -reach; dy <= reach && !near; dy++)
                {
                    for (var dx = -reach; dx <= reach && !near; dx++)
                    {
                        near = (dx != 0 || dy != 0) && Solid(gx + dx, gy + dy);
                    }
                }

                if (near) Put(gx, gy, colour);
            }
        }
    }

    public static double Ease(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 3);

    /// <summary>Slow, fast, slow: for a move that leaves and arrives.</summary>
    public static double Smooth(double p)
    {
        var x = Math.Clamp(p, 0, 1);
        return x * x * (3 - (2 * x));
    }

    /// <summary>Ease out that goes past the end and comes back: an overshoot.</summary>
    public static double Back(double p)
    {
        const double C1 = 1.70158;
        var x = Math.Clamp(p, 0, 1) - 1;
        return 1 + ((C1 + 1) * x * x * x) + (C1 * x * x);
    }

    public static uint Lerp(uint a, uint b, double t)
    {
        byte Mix(int shift) => (byte)(((a >> shift) & 0xFF) + ((((int)((b >> shift) & 0xFF)) - (int)((a >> shift) & 0xFF)) * t));
        return Mix(0) | ((uint)Mix(8) << 8) | ((uint)Mix(16) << 16) | 0xFF000000;
    }

    public static uint Bgra(byte r, byte g, byte b) => b | ((uint)g << 8) | ((uint)r << 16) | 0xFF000000;

    private static int TextWidth(string text) => text.Length == 0 ? 0 : (text.Length * 4) - 1;

    private static string Fit(string text, int cells)
    {
        var max = (cells + 1) / 4;
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + ".";
    }

    /// <summary>The small font, one game pixel per dot, with a shadow under it.</summary>
    private void Write(string text, int left, int top, uint colour)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var (ox, oy, c) = pass == 0 ? (1, 1, PlateDark) : (0, 0, colour);
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

    private static uint[] BuildBagColours()
    {
        var colours = new uint[128];
        colours['o'] = Bgra(0x0B, 0x09, 0x10);
        colours['d'] = Bgra(0x7A, 0x32, 0x1A);
        colours['m'] = Bgra(0xB8, 0x52, 0x28);
        colours['l'] = Bgra(0xD8, 0x74, 0x3C);
        colours['h'] = Bgra(0xF2, 0xA0, 0x62);
        colours['f'] = Bgra(0x94, 0x3E, 0x20);
        colours['F'] = Bgra(0xC0, 0x5C, 0x30);
        colours['g'] = Bgra(0xE0, 0xA8, 0x3A);
        colours['G'] = Bgra(0xFF, 0xEA, 0xA0);
        colours['k'] = Bgra(0x8E, 0x60, 0x1C);
        colours['s'] = Bgra(0xF6, 0xC8, 0x94);
        colours['p'] = Bgra(0x9A, 0x44, 0x22);
        colours['q'] = Bgra(0x86, 0x3A, 0x1C);
        colours['K'] = Bgra(0x24, 0x10, 0x0C);
        return colours;
    }
}
