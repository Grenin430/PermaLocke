using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// A canvas of cells with alpha blending, the pixel fonts and a few shapes: where the cap panel is painted before it is put on
/// screen. One of them is the still part of the panel and another is the frame being shown, copied from it every time, so a frame
/// costs a copy and the few things that move.
/// </summary>
public sealed class CapSheet(int width, int height)
{
    public int W { get; } = width;

    public int H { get; } = height;

    public byte[] B { get; } = new byte[Math.Max(1, width * height * 4)];

    public static uint Rgb(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

    public static uint Mix(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int Ch(int shift) => (int)Math.Round((((a >> shift) & 255) * (1 - t)) + (((b >> shift) & 255) * t));
        return Rgb(Ch(16), Ch(8), Ch(0));
    }

    public static uint Lighter(uint c, double t) => Mix(c, 0xFFFFFF, t);

    public static uint Darker(uint c, double t) => Mix(c, 0x000000, t);

    /// <summary>
    /// The empty corner of an L-shaped window (the notch): columns from <c>NotchX0</c> up to <c>NotchX1</c>, rows above <c>NotchY1</c>. Nothing
    /// is ever painted there, whoever asks: it is over the game.
    /// </summary>
    public int NotchX0 { get; set; }

    public int NotchX1 { get; set; }

    public int NotchY1 { get; set; }

    public void Clear() => Array.Clear(B);

    /// <summary>Whether the theme repaints the cells in its own shades (the Game Boy), which a frame has to do on a copy.</summary>
    public static bool ThemeShades => PixelTheme.Current.Shades is not null;

    /// <summary>
    /// Puts another sheet on this one at a place, cell by cell, over what is there. <paramref name="alpha"/> fades it; from <paramref name="dimFromRow"/>
    /// down (a row of the other sheet) its colours are mixed towards <paramref name="dimColour"/> by <paramref name="dim"/>: what is under the water.
    /// </summary>
    public void Blit(CapSheet src, int ox, int oy, int alpha = 255, int dimFromRow = int.MaxValue, uint dimColour = 0, double dim = 0)
    {
        for (var y = 0; y < src.H; y++)
        {
            for (var x = 0; x < src.W; x++)
            {
                var i = ((y * src.W) + x) * 4;
                var a = src.B[i + 3];
                if (a == 0) continue;
                var rgb = Rgb(src.B[i + 2], src.B[i + 1], src.B[i]);
                if (y >= dimFromRow) rgb = Mix(rgb, dimColour, dim);
                Set(ox + x, oy + y, rgb, a == 255 ? alpha : Math.Min(a, alpha));
            }
        }
    }

    /// <summary>Takes everything from a sheet of the same size.</summary>
    public void CopyFrom(CapSheet other) => Buffer.BlockCopy(other.B, 0, B, 0, Math.Min(B.Length, other.B.Length));

    public void Set(int x, int y, uint rgb, int alpha = 255)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0 || (y < NotchY1 && x >= NotchX0 && x < NotchX1)) return;
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

    /// <summary>Whether a cell has anything painted on it.</summary>
    public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && B[(((y * W) + x) * 4) + 3] != 0;

    /// <summary>What colour a painted cell has, as 0xRRGGBB (0 for an empty one).</summary>
    public uint At(int x, int y)
    {
        if (!Solid(x, y)) return 0;
        var i = ((y * W) + x) * 4;
        return Rgb(B[i + 2], B[i + 1], B[i]);
    }

    public void Erase(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || (y < NotchY1 && x >= NotchX0 && x < NotchX1)) return;
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

    /// <summary>A checkerboard of every other cell, the dither of the rest of the app: <paramref name="phase"/> 0 or 1 picks which half.</summary>
    public void Dither(int x, int y, int w, int h, uint rgb, int alpha = 255, int phase = 0)
    {
        for (var yy = y; yy < y + h; yy++)
        {
            for (var xx = x; xx < x + w; xx++)
            {
                if (((xx + yy + phase) & 1) == 0) Set(xx, yy, rgb, alpha);
            }
        }
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

    /// <summary>How many cells wide <see cref="Text"/> is at a scale.</summary>
    public static int TextWidth(string text, int scale = 1) => PixelFont.Measure(text) * scale;

    /// <summary>The 3×5 capitals; accents are dropped, unknown characters leave a gap.</summary>
    public void Small(string text, int x, int y, uint rgb)
    {
        foreach (var raw in text.ToUpperInvariant())
        {
            var ch = Plain(raw);
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

    /// <summary>How many cells wide <see cref="Small"/> is.</summary>
    public static int SmallWidth(string text)
    {
        var width = 0;
        foreach (var raw in text.ToUpperInvariant())
        {
            var ch = Plain(raw);
            width += ch == ' ' ? 3 : SmallFont.Glyph(ch) is { } glyph ? glyph[0].Length + 1 : 4;
        }

        return Math.Max(0, width - 1);
    }

    private static char Plain(char raw) => raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', 'Ü' => 'U', _ => raw };

    /// <summary>The cells as a frozen bitmap, redrawn in the theme's shades when it has them (the Game Boy). For tests and the lab.</summary>
    public BitmapSource ToBitmap()
    {
        var pixels = B;
        if (PixelTheme.Current.Shades is not null)
        {
            pixels = (byte[])B.Clone();
            Shade(pixels);
        }

        var bitmap = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, pixels, W * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Redraws a copy of the cells in the theme's shades, in place on <paramref name="pixels"/>.</summary>
    public static void Shade(byte[] pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var mapped = PixelTheme.Current.Map(Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]));
            (pixels[i], pixels[i + 1], pixels[i + 2]) = (mapped.B, mapped.G, mapped.R);
        }
    }
}
