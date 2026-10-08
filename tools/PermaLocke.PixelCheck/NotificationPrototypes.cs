using PermaLocke.App.Views;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>
/// Prototypes for the rework of the notices over the game (2026-10-08): five looks, each drawn for the same sample notices over a
/// real frame of the game, as PNG when <c>PERMALOCKE_PIXEL_DIR</c> is set. <c>PERMALOCKE_PROTO_ASSETS</c> holds the frames and icons.
/// Nothing here is shipped: it exists to choose.
/// </summary>
public sealed partial class NotificationPrototypes
{
    // ---------------------------------------------------------------- drawing kit

    private static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

    private static uint Mix(uint a, uint b, double t)
    {
        int Ch(int shift) => (int)Math.Round((((a >> shift) & 255) * (1 - t)) + (((b >> shift) & 255) * t));
        return Rgb(Ch(16), Ch(8), Ch(0));
    }

    private static uint Lighter(uint c, double t) => Mix(c, 0xFFFFFF, t);

    private static uint Darker(uint c, double t) => Mix(c, 0x000000, t);

    private sealed class Pix(int width, int height)
    {
        public int W { get; } = width;
        public int H { get; } = height;
        public byte[] B { get; } = new byte[width * height * 4];

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

        public void Rect(int x, int y, int w, int h, uint rgb, int alpha = 255)
        {
            for (var yy = y; yy < y + h; yy++)
            {
                for (var xx = x; xx < x + w; xx++) Set(xx, yy, rgb, alpha);
            }
        }

        /// <summary>A rectangle with its four corners notched by <paramref name="n"/> cells, the way the app's boxes are.</summary>
        public void Notched(int x, int y, int w, int h, uint rgb, int n = 1, int alpha = 255)
        {
            for (var yy = 0; yy < h; yy++)
            {
                var inset = yy < n ? n - yy : yy >= h - n ? yy - (h - n) + 1 : 0;
                for (var xx = inset; xx < w - inset; xx++) Set(x + xx, y + yy, rgb, alpha);
            }
        }

        public void VGrad(int x, int y, int w, int h, uint top, uint bottom, int alpha = 255)
        {
            for (var yy = 0; yy < h; yy++) Rect(x, y + yy, w, 1, Mix(top, bottom, h <= 1 ? 0 : yy / (double)(h - 1)), alpha);
        }

        public void HLine(int x, int y, int w, uint rgb, int alpha = 255) => Rect(x, y, w, 1, rgb, alpha);

        public void VLine(int x, int y, int h, uint rgb, int alpha = 255) => Rect(x, y, 1, h, rgb, alpha);

        public void Outline(int x, int y, int w, int h, uint rgb, int alpha = 255)
        {
            HLine(x, y, w, rgb, alpha);
            HLine(x, y + h - 1, w, rgb, alpha);
            VLine(x, y, h, rgb, alpha);
            VLine(x + w - 1, y, h, rgb, alpha);
        }

        /// <summary>The pixel font at <paramref name="scale"/> cells per cell; the first row of its box is at <paramref name="y"/>.</summary>
        public void Text(string text, int x, int y, uint rgb, int scale = 1, uint? shadow = null, bool bold = false)
        {
            void Block(int px, int py, uint c)
            {
                for (var sy = 0; sy < scale; sy++)
                {
                    for (var sx = 0; sx < scale; sx++) Set(px + sx, py + sy, c);
                }
            }

            if (shadow is { } dark)
            {
                PixelFont.Draw(text, 0, 0, (px, py) => Block(x + (px * scale) + scale, y + (py * scale) + scale, dark));
            }

            PixelFont.Draw(text, 0, 0, (px, py) =>
            {
                Block(x + (px * scale), y + (py * scale), rgb);
                if (bold) Block(x + (px * scale) + 1, y + (py * scale), rgb);
            });
        }

        public void Small(string text, int x, int y, uint rgb)
        {
            foreach (var raw in text.ToUpperInvariant())
            {
                var ch = raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', _ => raw };
                if (ch == ' ')
                {
                    x += 3;
                    continue;
                }

                var glyph = SmallFont.Glyph(ch);
                if (glyph is null)
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

        public void Blit(Pix source, int x, int y, int alpha = 255)
        {
            for (var sy = 0; sy < source.H; sy++)
            {
                for (var sx = 0; sx < source.W; sx++)
                {
                    var i = ((sy * source.W) + sx) * 4;
                    var a = source.B[i + 3];
                    if (a == 0) continue;
                    Set(x + sx, y + sy, Rgb(source.B[i + 2], source.B[i + 1], source.B[i]), a * alpha / 255);
                }
            }
        }

        public void Art(string[] rows, int x, int y, IReadOnlyDictionary<char, uint> key)
        {
            for (var gy = 0; gy < rows.Length; gy++)
            {
                for (var gx = 0; gx < rows[gy].Length; gx++)
                {
                    if (key.TryGetValue(rows[gy][gx], out var colour)) Set(x + gx, y + gy, colour);
                }
            }
        }

        public Pix Scaled(int scale)
        {
            var big = new Pix(W * scale, H * scale);
            for (var y = 0; y < big.H; y++)
            {
                for (var x = 0; x < big.W; x++)
                {
                    var from = (((y / scale) * W) + (x / scale)) * 4;
                    Buffer.BlockCopy(B, from, big.B, ((y * big.W) + x) * 4, 4);
                }
            }

            return big;
        }
    }

    /// <summary>A minimal PNG reader: 8 bit RGBA or RGB, no interlace. Enough for the game frames and the icons.</summary>
    private static Pix? ReadPng(string path)
    {
        if (!File.Exists(path)) return null;
        var data = File.ReadAllBytes(path);
        int w = 0, h = 0, type = 0;
        using var joined = new MemoryStream();
        for (var at = 8; at + 8 <= data.Length;)
        {
            var length = (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
            var name = System.Text.Encoding.ASCII.GetString(data, at + 4, 4);
            if (name == "IHDR")
            {
                w = (data[at + 8] << 24) | (data[at + 9] << 16) | (data[at + 10] << 8) | data[at + 11];
                h = (data[at + 12] << 24) | (data[at + 13] << 16) | (data[at + 14] << 8) | data[at + 15];
                type = data[at + 17];
            }
            else if (name == "IDAT")
            {
                joined.Write(data, at + 8, length);
            }

            at += 12 + length;
        }

        var bytes = type == 6 ? 4 : 3;
        joined.Position = 0;
        using var zlib = new System.IO.Compression.ZLibStream(joined, System.IO.Compression.CompressionMode.Decompress);
        var raw = new byte[h * ((w * bytes) + 1)];
        var read = 0;
        while (read < raw.Length)
        {
            var n = zlib.Read(raw, read, raw.Length - read);
            if (n == 0) break;
            read += n;
        }

        var stride = w * bytes;
        var rows = new byte[h * stride];
        for (var y = 0; y < h; y++)
        {
            var filter = raw[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                int cur = raw[(y * (stride + 1)) + 1 + x];
                int a = x >= bytes ? rows[(y * stride) + x - bytes] : 0;
                int b = y > 0 ? rows[((y - 1) * stride) + x] : 0;
                int c = x >= bytes && y > 0 ? rows[((y - 1) * stride) + x - bytes] : 0;
                var value = filter switch
                {
                    1 => cur + a,
                    2 => cur + b,
                    3 => cur + ((a + b) / 2),
                    4 => cur + Paeth(a, b, c),
                    _ => cur
                };
                rows[(y * stride) + x] = (byte)value;
            }
        }

        var pix = new Pix(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var from = (y * stride) + (x * bytes);
                var to = ((y * w) + x) * 4;
                (pix.B[to], pix.B[to + 1], pix.B[to + 2], pix.B[to + 3]) = (rows[from + 2], rows[from + 1], rows[from], bytes == 4 ? rows[from + 3] : (byte)255);
            }
        }

        return pix;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    // ---------------------------------------------------------------- the notices

    private enum Mark { Skull, Star, Ghost, Ball, Gift, Bell, Warn, Arrow }

    private sealed record Sample(string Label, string Title, string Message, uint Accent, Mark Mark, string Icon, bool Compact = false);

    private static readonly Sample[] Samples =
    [
        new("BAJA", "pedicure ha caído", "-25 puntos · Nv. 20", 0xB8433A, Mark.Skull, "icon0094"),
        new("VARIOCOLOR", "¡Variocolor!", "Charizard · siempre se captura", 0xD8A83C, Mark.Star, "icon0006"),
        new("DUPLICADO", "Ya lo tienes", "Frogadier · captúralo o pasa", 0x4FB8B0, Mark.Ball, "icon0130"),
        new("FANTASMA", "Juanmaa perdió a Volvo", "Cruzará tu emulador", 0x9CC8E0, Mark.Ghost, "icon0214"),
        new("PERMALOCKE", "Bavi está jugando", "", 0xB07BF0, Mark.Bell, "icon0025", Compact: true)
    ];

    private static readonly IReadOnlyDictionary<Mark, string[]> MarkArt = new Dictionary<Mark, string[]>
    {
        [Mark.Skull] = [".#####.", "#######", "##.#.##", "#######", ".#####.", ".#.#.#.", ".#.#.#."],
        [Mark.Star] = ["...#...", "...#...", "#######", ".#####.", "..###..", ".##.##.", ".#...#."],
        [Mark.Ghost] = [".#####.", "#######", "#.###.#", "#######", "#######", "#######", "#.#.#.#"],
        [Mark.Ball] = [".#####.", "#######", "#######", "###.###", "#######", "#######", ".#####."],
        [Mark.Gift] = ["#.###.#", ".#####.", "#######", "###.###", "#######", "###.###", "#######"],
        [Mark.Bell] = ["...#...", "..###..", ".#####.", ".#####.", "#######", "#######", "...#..."],
        [Mark.Warn] = ["...#...", "..###..", "..#.#..", ".##.##.", ".##.##.", "#######", "#######"],
        [Mark.Arrow] = ["...#...", "..###..", ".#####.", "#######", "..###..", "..###..", "..###.."]
    };

    private static void DrawMark(Pix p, Mark mark, int x, int y, uint colour)
    {
        var art = MarkArt[mark];
        for (var gy = 0; gy < art.Length; gy++)
        {
            for (var gx = 0; gx < art[gy].Length; gx++)
            {
                if (art[gy][gx] == '#') p.Set(x + gx, y + gy, colour);
            }
        }
    }

    private static void Icon(Pix target, Pix? icon, int centreX, int centreY)
    {
        if (icon is null) return;
        target.Blit(icon, centreX - (icon.W / 2), centreY - (icon.H / 2));
    }

    private const int Width = 160;

    private static string Fit(string text, int cells) => PixelFont.Measure(text) <= cells ? text : PixelFont.Trim(text, cells);

    private static string FitSmall(string text, int cells) => text.Length * 4 <= cells ? text : text[..Math.Max(1, (cells / 4) - 1)] + ".";

    /// <summary>Segments of the countdown: <paramref name="lit"/> of twelve.</summary>
    private static int Lit => 9;

    // ---------------------------------------------------------------- A: the Rotom Dex

    private static Pix RotomDex(Sample s, Pix? icon)
    {
        if (s.Compact) return RotomCompact(s, icon);
        var p = new Pix(Width, 46);
        uint casing = 0xD2402F;
        uint hi = 0xF26A4F;
        uint low = 0x8E2519;

        // El casco de plástico rojo del aparato, con su reflejo y su sombra.
        p.Notched(0, 0, Width - 2, 44, 0x2A0C08, 3);
        p.Notched(1, 1, Width - 4, 42, casing, 3);
        p.VGrad(2, 2, Width - 6, 14, hi, casing);
        p.VGrad(2, 28, Width - 6, 14, casing, low);
        p.HLine(5, 2, Width - 12, Lighter(hi, 0.5));
        p.Notched(2, 44, Width - 4, 2, 0x12040A, 1, 160);

        // La lente redonda: el Pokémon se mira por ella.
        p.Notched(7, 6, 32, 32, 0x2A0C08, 8);
        p.Notched(8, 7, 30, 30, 0xE8E4F0, 8);
        p.Notched(10, 9, 26, 26, s.Accent, 7);
        p.Notched(11, 10, 24, 24, Darker(s.Accent, 0.55), 6);
        for (var y = 0; y < 9; y++) p.HLine(14, 12 + y, 8 - (y / 2), Lighter(Darker(s.Accent, 0.25), 0.25), 120);
        Icon(p, icon, 23, 23);
        p.Set(13, 13, 0xFFFFFF);
        p.Set(14, 12, 0xFFFFFF);

        // La pantalla: cristal verdoso con líneas de barrido.
        p.Notched(43, 6, Width - 52, 32, 0x0B1A1A, 2);
        p.VGrad(44, 7, Width - 54, 30, 0x103A38, 0x0A2524);
        for (var y = 8; y < 37; y += 2) p.HLine(44, y, Width - 54, 0x000000, 46);
        p.HLine(44, 7, Width - 54, 0x4FD8C8, 90);

        // El rótulo: una etiqueta en el borde de arriba.
        var labelW = (s.Label.Length * 4) + 7;
        p.Notched(46, 2, labelW, 8, s.Accent, 1);
        p.Small(s.Label, 49, 3, 0x1A0A08);

        p.Text(Fit(s.Title, 102), 47, 12, 0xC8FFF4, shadow: 0x0A4A44);
        p.Small(FitSmall(s.Message, 102), 47, 26, 0x6FD4C4);

        // El piloto de cada tipo y la cuenta atrás, en diodos.
        p.Rect(46, 33, 3, 3, s.Accent);
        for (var i = 0; i < 12; i++) p.Rect(52 + (i * 7), 34, 5, 2, i < Lit ? s.Accent : 0x16403C);
        for (var i = 0; i < 3; i++) p.Rect(Width - 14 + (i * 3), 41, 1, 1, 0x2A0C08);
        return p;
    }

    private static Pix RotomCompact(Sample s, Pix? icon)
    {
        var p = new Pix(100, 17);
        p.Notched(0, 0, 98, 16, 0x2A0C08, 2);
        p.Notched(1, 1, 96, 14, 0xD2402F, 2);
        p.VGrad(2, 2, 94, 6, 0xF26A4F, 0xD2402F);
        p.Notched(3, 3, 10, 10, 0xE8E4F0, 3);
        p.Notched(4, 4, 8, 8, s.Accent, 2);
        p.Notched(16, 3, 78, 10, 0x0B2A28, 1);
        p.Small("BAVI ESTÁ JUGANDO", 19, 5, 0x9FF0E0);
        return p;
    }

    // ---------------------------------------------------------------- B: the Alola sign

    private static void Wood(Pix p, int x, int y, int w, int h, uint baseColour, int seed)
    {
        p.VGrad(x, y, w, h, Lighter(baseColour, 0.12), Darker(baseColour, 0.14));
        var random = new Random(seed);
        for (var line = 0; line < h; line += 4)
        {
            p.HLine(x, y + line, w, Darker(baseColour, 0.22), 90);
            for (var k = 0; k < 4; k++)
            {
                var gx = x + random.Next(w - 8);
                p.HLine(gx, y + line + 2, 3 + random.Next(6), Darker(baseColour, 0.12), 110);
            }
        }
    }

    private static Pix AlolaSign(Sample s, Pix? icon)
    {
        if (s.Compact) return SignCompact(s, icon);
        var p = new Pix(Width, 52);
        uint plank = 0xA8703A;

        // Las cuerdas de las que cuelga.
        for (var y = 0; y < 7; y++)
        {
            p.VLine(20, y, 1, 0xD8C080);
            p.VLine(21, y, 1, 0x806030);
            p.VLine(Width - 24, y, 1, 0xD8C080);
            p.VLine(Width - 23, y, 1, 0x806030);
        }

        p.Rect(19, 6, 4, 2, 0x4A3018);
        p.Rect(Width - 25, 6, 4, 2, 0x4A3018);

        // El tablón, con su marco tallado.
        p.Notched(0, 7, Width - 2, 41, 0x2E1A0A, 2);
        Wood(p, 1, 8, Width - 4, 39, plank, 7);
        p.Outline(3, 10, Width - 8, 35, Darker(plank, 0.45));
        p.Outline(4, 11, Width - 10, 33, Lighter(plank, 0.18), 120);
        p.Notched(2, 47, Width - 4, 2, 0x1A0E06, 1, 150);

        // La placa oscura donde va el texto.
        p.Notched(46, 14, Width - 56, 27, 0x3A220F, 2);
        p.Notched(47, 15, Width - 58, 25, 0x4D2F16, 2);
        p.Text(Fit(s.Title, 100), 51, 16, 0xFFF0CC, shadow: 0x1E1006);
        p.Small(FitSmall(s.Message, 100), 51, 30, Lighter(s.Accent, 0.35));

        // La flor del tipo, en una esquina (un hibisco de cinco pétalos).
        var fx = Width - 14;
        foreach (var (dx, dy) in new[] { (0, -3), (3, -1), (2, 3), (-2, 3), (-3, -1) })
        {
            p.Rect(fx + dx - 1, 10 + dy - 1, 3, 3, s.Accent);
            p.Set(fx + dx, 10 + dy - 1, Lighter(s.Accent, 0.4));
        }

        p.Rect(fx - 1, 9, 3, 3, 0xFFE070);

        // Hojas en las esquinas.
        foreach (var (lx, ly, dir) in new[] { (5, 13, 1), (Width - 12, 40, -1) })
        {
            for (var i = 0; i < 6; i++)
            {
                p.Rect(lx + (i * dir), ly + (i / 2), 2, 2, i % 2 == 0 ? 0x3C9A48u : 0x2A7A38u);
            }
        }

        // El medallón del Pokémon: un círculo de concha.
        p.Notched(8, 14, 32, 29, 0x2E1A0A, 9);
        p.Notched(9, 15, 30, 27, 0xF4E8D0, 9);
        p.Notched(11, 17, 26, 23, Mix(s.Accent, 0xFFFFFF, 0.55), 8);
        Icon(p, icon, 24, 29);

        // La tablilla colgada del rótulo.
        var labelW = (s.Label.Length * 4) + 9;
        p.Notched(46, 0, labelW, 10, 0x2E1A0A, 1);
        Wood(p, 47, 1, labelW - 2, 8, 0x7A4A22, 3);
        p.Small(s.Label, 51, 3, 0xFFF0CC);
        p.Rect(46 + labelW - 3, 2, 2, 6, s.Accent);

        // La cuenta atrás: cuentas de collar.
        for (var i = 0; i < 12; i++) p.Rect(52 + (i * 7), 42, 4, 3, i < Lit ? s.Accent : 0x5C3A1C);
        return p;
    }

    private static Pix SignCompact(Sample s, Pix? icon)
    {
        var p = new Pix(100, 17);
        p.Notched(0, 0, 98, 16, 0x2E1A0A, 2);
        Wood(p, 1, 1, 96, 14, 0xA8703A, 11);
        p.Notched(4, 3, 10, 10, 0xF4E8D0, 3);
        p.Notched(5, 4, 8, 8, s.Accent, 2);
        p.Small("BAVI ESTÁ JUGANDO", 18, 6, 0xFFF0CC);
        return p;
    }

    // ---------------------------------------------------------------- C: the game's own text box

    private static Pix GameBox(Sample s, Pix? icon)
    {
        if (s.Compact) return GameBoxCompact(s, icon);
        var p = new Pix(Width, 46);

        // La caja de texto de los combates: azul profundo con el borde claro.
        p.Notched(0, 5, Width - 2, 39, 0xF4F8FF, 3);
        p.Notched(1, 6, Width - 4, 37, 0x0A1740, 3);
        p.VGrad(2, 7, Width - 6, 35, 0x1B3C9A, 0x0A1A52);
        p.HLine(4, 7, Width - 10, 0x6EA0F0, 150);
        p.Outline(3, 8, Width - 8, 33, 0x2C58C8, 160);
        p.Notched(2, 44, Width - 4, 2, 0x050A20, 1, 150);

        // La placa del que habla.
        var labelW = (s.Label.Length * 4) + 20;
        p.Notched(8, 0, labelW, 11, 0xF4F8FF, 2);
        p.Notched(9, 1, labelW - 2, 9, s.Accent, 2);
        p.Small(s.Label, 19, 3, 0xFFFFFF);
        DrawMark(p, s.Mark, 10, 2, 0xFFFFFF);

        // El Pokémon sobre su disco blanco.
        p.Notched(7, 13, 28, 27, 0xFFFFFF, 8);
        p.Notched(8, 14, 26, 25, Mix(s.Accent, 0xFFFFFF, 0.72), 7);
        Icon(p, icon, 21, 27);

        p.Text(Fit(s.Title, 102), 42, 15, 0xFFFFFF, shadow: 0x050A30);
        p.Small(FitSmall(s.Message, 102), 42, 28, 0xBDD4FF);

        // El triángulo que parpadea, como en el juego, y la cuenta atrás: una línea que se acorta.
        for (var i = 0; i < 4; i++) p.HLine(Width - 16 + i, 32 + (i / 2), 7 - (i * 2), 0xFFFFFF);
        p.Rect(42, 38, 92, 2, 0x0A1A52);
        p.Rect(42, 38, 92 * Lit / 12, 2, s.Accent);
        p.Rect(42, 38, 92 * Lit / 12, 1, Lighter(s.Accent, 0.4));
        return p;
    }

    private static Pix GameBoxCompact(Sample s, Pix? icon)
    {
        var p = new Pix(100, 17);
        p.Notched(0, 0, 98, 16, 0xF4F8FF, 2);
        p.Notched(1, 1, 96, 14, 0x12307F, 2);
        p.Notched(4, 3, 10, 10, 0xFFFFFF, 3);
        p.Notched(5, 4, 8, 8, s.Accent, 2);
        p.Small("BAVI ESTÁ JUGANDO", 18, 6, 0xFFFFFF);
        return p;
    }

    // ---------------------------------------------------------------- D: the medal

    private static Pix Medal(Sample s, Pix? icon)
    {
        if (s.Compact) return MedalCompact(s, icon);
        var p = new Pix(Width, 50);
        var gold = s.Accent;
        var goldLo = Darker(gold, 0.45);
        var goldHi = Lighter(gold, 0.55);

        // La placa de metal oscuro, con un filo del color del tipo.
        p.Notched(8, 4, Width - 10, 42, 0x07050C, 2);
        p.Notched(9, 5, Width - 12, 40, 0x1A1426, 2);
        p.VGrad(10, 6, Width - 14, 38, 0x2C2442, 0x120E1E);
        p.HLine(11, 6, Width - 16, goldHi, 200);
        p.HLine(11, 43, Width - 16, goldLo);
        p.VLine(10, 8, 34, goldLo);
        p.VLine(Width - 5, 8, 34, goldLo);
        p.Notched(10, 46, Width - 14, 2, 0x050308, 1, 140);

        // El brillo que cruza la placa en diagonal.
        for (var y = 7; y < 43; y++)
        {
            var x = 62 + ((43 - y) * 2);
            p.Rect(x, y, 5, 1, 0xFFFFFF, 14);
            p.Rect(x + 6, y, 2, 1, 0xFFFFFF, 10);
        }

        // La medalla: aro, cinta con las dos puntas y el Pokémon dentro.
        p.Rect(10, 30, 8, 18, Darker(gold, 0.2));
        p.Rect(20, 30, 8, 18, gold);
        p.Rect(10, 30, 2, 18, goldLo);
        p.Rect(26, 30, 2, 18, goldHi);
        p.Rect(12, 44, 4, 4, 0x07050C);
        p.Rect(22, 44, 4, 4, 0x07050C);
        p.Notched(1, 2, 36, 36, 0x07050C, 11);
        p.Notched(2, 3, 34, 34, gold, 10);
        p.Notched(3, 4, 32, 32, goldHi, 10, 190);
        p.Notched(5, 6, 28, 28, goldLo, 9);
        p.Notched(6, 7, 26, 26, Darker(gold, 0.72), 8);
        Icon(p, icon, 19, 21);
        for (var i = 0; i < 5; i++) p.Set(9 + i, 8 - (i / 2), 0xFFFFFF, 200);

        // La cinta del rótulo, con las puntas en cola de golondrina.
        var labelW = (s.Label.Length * 4) + 18;
        p.Rect(42, 0, labelW, 9, gold);
        p.VGrad(42, 0, labelW, 9, goldHi, gold);
        p.Rect(42, 8, labelW, 1, goldLo);
        for (var i = 0; i < 4; i++)
        {
            p.Rect(42 + labelW + i, i, 1, 9 - (2 * i), gold);
            p.Rect(42 - 1 - i, i, 1, 9 - (2 * i), gold);
        }

        DrawMark(p, s.Mark, 45, 1, Darker(gold, 0.7));
        p.Small(s.Label, 55, 2, Darker(gold, 0.78));

        p.Text(Fit(s.Title, 104), 44, 13, 0xFFFFFF, shadow: 0x000000, bold: true);
        p.Small(FitSmall(s.Message, 104), 44, 27, Mix(gold, 0xFFFFFF, 0.35));

        // La cuenta atrás: gemas.
        for (var i = 0; i < 12; i++)
        {
            var x = 44 + (i * 8);
            var on = i < Lit;
            p.Rect(x + 1, 36, 4, 3, on ? gold : 0x2A2238);
            p.Rect(x, 37, 6, 1, on ? gold : 0x2A2238);
            if (on) p.Set(x + 2, 36, 0xFFFFFF);
        }

        // Destellos de metal.
        foreach (var (sx, sy) in new[] { (Width - 12, 10), (Width - 20, 38), (96, 14) })
        {
            p.Set(sx, sy, 0xFFFFFF);
            p.Set(sx - 1, sy, 0xFFFFFF, 140);
            p.Set(sx + 1, sy, 0xFFFFFF, 140);
            p.Set(sx, sy - 1, 0xFFFFFF, 140);
            p.Set(sx, sy + 1, 0xFFFFFF, 140);
        }

        return p;
    }

    private static Pix MedalCompact(Sample s, Pix? icon)
    {
        var p = new Pix(100, 17);
        p.Notched(0, 0, 98, 16, 0x07050C, 2);
        p.VGrad(1, 1, 96, 14, 0x2C2442, 0x120E1E);
        p.HLine(2, 1, 94, Lighter(s.Accent, 0.55), 200);
        p.Notched(3, 2, 12, 12, s.Accent, 4);
        p.Notched(5, 4, 8, 8, Darker(s.Accent, 0.6), 3);
        p.Small("BAVI ESTÁ JUGANDO", 19, 6, 0xFFFFFF);
        return p;
    }

    // ---------------------------------------------------------------- E: neon glass

    private static Pix NeonGlass(Sample s, Pix? icon)
    {
        if (s.Compact) return NeonCompact(s, icon);
        var p = new Pix(Width, 46);
        var glow = s.Accent;

        // El resplandor, en puntos tramados para que siga siendo píxel.
        for (var y = 0; y < 45; y++)
        {
            for (var x = 0; x < Width - 2; x++)
            {
                var edge = Math.Min(Math.Min(x, Width - 3 - x), Math.Min(y, 41 - y));
                if (edge < 3 && ((x + y) % 2 == 0) && y < 42) p.Set(x, y, glow, 34 - (edge * 10));
            }
        }

        p.Notched(3, 3, Width - 8, 38, 0x06050C, 3);
        p.Notched(4, 4, Width - 10, 36, 0x14101F, 3);
        p.VGrad(5, 5, Width - 12, 34, 0x1E1830, 0x0E0A18, 245);
        p.Outline(4, 4, Width - 10, 36, glow);
        p.HLine(8, 4, Width - 18, Lighter(glow, 0.5));
        p.VLine(4, 8, 28, Lighter(glow, 0.3), 200);

        // El chip del icono: cuadrado con el color del tipo detrás.
        p.Notched(9, 9, 28, 28, Darker(glow, 0.58), 3);
        p.Outline(9, 9, 28, 28, glow, 150);
        Icon(p, icon, 23, 23);

        // Arriba el rótulo con la marca del tipo; debajo el título; debajo el detalle; al final la luz.
        DrawMark(p, s.Mark, 43, 9, glow);
        p.Small(s.Label, 53, 10, glow);
        p.Text(Fit(s.Title, 104), 43, 15, 0xFFFFFF, shadow: 0x000000);
        p.Small(FitSmall(s.Message, 104), 43, 28, 0x9A92B8);

        // La cuenta atrás: una línea fina de luz que se apaga de derecha a izquierda.
        p.Rect(43, 35, Width - 58, 1, 0x2A2440);
        p.Rect(43, 35, (Width - 58) * Lit / 12, 1, glow);
        p.Rect(43 + ((Width - 58) * Lit / 12) - 3, 35, 3, 1, 0xFFFFFF);
        return p;
    }

    private static Pix NeonCompact(Sample s, Pix? icon)
    {
        var p = new Pix(100, 17);
        p.Notched(0, 0, 98, 16, 0x06050C, 3);
        p.Notched(1, 1, 96, 14, 0x16112A, 3);
        p.Outline(1, 1, 96, 14, s.Accent, 200);
        p.Notched(4, 4, 8, 8, s.Accent, 2);
        p.Small("BAVI ESTÁ JUGANDO", 16, 6, 0xFFFFFF);
        return p;
    }

    // ---------------------------------------------------------------- composing

    private delegate Pix Look(Sample sample, Pix? icon);

    private static (string Name, string Title, Look Look)[] Looks => [.. RoundOne, .. RoundTwo, .. RoundThree];

    private static (string Name, string Title, Look Look)[] RoundOne =>
    [
        ("A", "ROTOM DEX", RotomDex),
        ("B", "CARTEL DE ALOLA", AlolaSign),
        ("C", "CAJA DE TEXTO DEL JUEGO", GameBox),
        ("D", "MEDALLA", Medal),
        ("E", "CRISTAL NEÓN", NeonGlass)
    ];

    private static string? Dir(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>One full-screen picture of a look: the game's frame, a clean screen behind it, and the notices stacked bottom right.</summary>
    private static Pix Screen(Look look, string assets, string background, int scale)
    {
        const int cell = 3;
        var game = ReadPng(Path.Combine(assets, background));
        var screen = new Pix(1280, 720);
        screen.VGrad(0, 0, 1280, 720, 0x0A0A12, 0x0A0A12);

        if (game is not null)
        {
            var big = game.Scaled(3);
            screen.Blit(big, (1280 - big.W) / 2, 0);
        }

        // La parte de abajo, la pantalla táctil, en negro suave: lo que hay donde se apilan los avisos.
        screen.Rect(0, 720 - 40, 1280, 40, 0x0A0A12);

        var y = 720 - 20;
        var all = new[] { Samples[0], Samples[1], Samples[2], Samples[4] }
            .Select(sample => look(sample, ReadPng(Path.Combine(assets, sample.Icon + ".png")))).ToList();
        var cards = new List<Pix>();
        var used = 0;
        foreach (var candidate in all)
        {
            used += (candidate.H * cell) + 9;
            if (used > 640 && cards.Count >= 2 && candidate.H > 30) continue;
            cards.Add(candidate);
        }
        for (var i = cards.Count - 1; i >= 0; i--)
        {
            var big = cards[i].Scaled(cell);
            y -= big.H + 9;
            screen.Blit(big, 1280 - big.W - 30, y);
        }

        return screen;
    }

    [Fact]
    public void Every_look_draws_every_notice_with_pixels_in_it()
    {
        foreach (var (_, _, look) in Looks)
        {
            foreach (var sample in Samples)
            {
                var card = look(sample, null);
                Assert.Contains(card.B.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
            }
        }
    }

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Dir("PERMALOCKE_PIXEL_DIR");
        var assets = Dir("PERMALOCKE_PROTO_ASSETS");
        if (folder is null || assets is null) return;

        Directory.CreateDirectory(folder);
        foreach (var (name, _, look) in Looks)
        {
            var screen = Screen(look, assets, "bg1.png", 1);
            PngFile.Write(screen.B, screen.W, screen.H, 1, Path.Combine(folder, $"aviso-{name}.png"));
        }

        // La comparación: la misma baja y el mismo duplicado en los cinco, para verlos de un vistazo.
        var compare = new Pix(1100, 1210);
        compare.Rect(0, 0, 1100, 1210, 0x0A0A12);
        var game = ReadPng(Path.Combine(assets, "bg3.png"));
        var row = 0;
        foreach (var (name, title, look) in RoundTwo)
        {
            var top = row * 240;
            if (game is not null) compare.Blit(game, 1100 - 400, top - 20 + 0, 255);
            compare.Rect(0, top, 1100, 1, 0x2A2640);
            compare.Text(name, 14, top + 12, 0xFFFFFF, scale: 4);
            compare.Text(title, 14, top + 62, 0xB0A8D0, scale: 2);
            var dead = look(Samples[0], ReadPng(Path.Combine(assets, Samples[0].Icon + ".png"))).Scaled(3);
            compare.Blit(dead, 150, top + 6);
            var friend = look(Samples[4], null).Scaled(3);
            compare.Blit(friend, 150 + dead.W + 30, top + 6 + 40);
            row++;
        }

        PngFile.Write(compare.B, compare.W, compare.H, 1, Path.Combine(folder, "aviso-comparar2.png"));

        var third = new Pix(1100, RoundThree.Length * 240);
        third.Rect(0, 0, 1100, third.H, 0x0A0A12);
        row = 0;
        foreach (var (name, title, look) in RoundThree)
        {
            var top = row * 240;
            if (game is not null) third.Blit(game, 1100 - 400, top - 20, 255);
            third.Rect(0, top, 1100, 1, 0x2A2640);
            third.Text(name, 14, top + 12, 0xFFFFFF, scale: 4);
            third.Text(title, 14, top + 62, 0xB0A8D0, scale: 2);
            var dead = look(Samples[0], ReadPng(Path.Combine(assets, Samples[0].Icon + ".png"))).Scaled(3);
            third.Blit(dead, 150, top + 6);
            var friend = look(Samples[4], null).Scaled(3);
            third.Blit(friend, 150 + dead.W + 30, top + 6 + 40);
            row++;
        }

        PngFile.Write(third.B, third.W, third.H, 1, Path.Combine(folder, "aviso-comparar3.png"));
    }
}
