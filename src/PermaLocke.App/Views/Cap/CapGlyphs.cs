using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// The writing of the sea (2026-10-10): letters of light with a dark outline in the colour of the water, so they read over the lit surface and
/// over the abyss alike, at one pixel a cell and at four. Plus the little things that stand for numbers: the beads of the HP, the pearls of the
/// experience, the spots of the types.
/// </summary>
public static class CapGlyphs
{
    /// <summary>«PRUEBA 8 DE 12» as «PRUEBA 8/12», for a tag that is too narrow for it; any other name as it is.</summary>
    public static string ShortTrial(string trial) =>
        trial.StartsWith("PRUEBA ", StringComparison.Ordinal) ? trial.Replace(" DE ", "/", StringComparison.Ordinal) : trial;

    /// <summary>How many cells wide a text is: the 5×7 letters (<paramref name="big"/>) or the 3×5 ones.</summary>
    public static int Width(string text, bool big, int scale = 1) => big ? CapSheet.TextWidth(text, scale) : CapSheet.SmallWidth(text);

    /// <summary>The height of a line of letters.</summary>
    public static int Height(bool big, int scale = 1) => (big ? 7 : 5) * scale;

    /// <summary>
    /// Letters with an outline all round them, drawn in two passes so that the outline of one letter never covers the next. <paramref name="alpha"/>
    /// fades the whole thing.
    /// </summary>
    public static void Outlined(CapSheet p, string text, int x, int y, uint colour, uint ink, bool big = false, int scale = 1, int alpha = 255)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0) return;

        var inkAlpha = Math.Min(alpha, 235);
        Pass(p, text, x, y, big, scale, (px, py) =>
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0 || dy != 0) p.Set(px + dx, py + dy, ink, inkAlpha);
                }
            }
        });
        Pass(p, text, x, y, big, scale, (px, py) => p.Set(px, py, colour, alpha));
    }

    private static void Pass(CapSheet p, string text, int x, int y, bool big, int scale, Action<int, int> cell)
    {
        if (big)
        {
            // The font leaves room above the capitals for accents: <paramref name="y"/> is where the capitals begin, so that a line is as tall as it looks.
            PixelFont.Draw(text, 0, 0, (px, py) =>
            {
                for (var sy = 0; sy < scale; sy++)
                {
                    for (var sx = 0; sx < scale; sx++) cell(x + (px * scale) + sx, y + ((py - PixelFont.Above) * scale) + sy);
                }
            });
            return;
        }

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
                    if (glyph[gy][gx] == '#') cell(x + gx, y + gy);
                }
            }

            x += glyph[0].Length + 1;
        }
    }

    /// <summary>A row of <paramref name="count"/> beads, <paramref name="lit"/> of them alight: each three cells with a glint, four apart.</summary>
    public static void Beads(CapSheet p, int x, int y, int count, int lit, uint colour, uint ink, double glow, int alpha = 255)
    {
        for (var i = 0; i < count; i++)
        {
            var bx = x + (i * 4);
            var on = i < lit;
            var body = on ? colour : CapSheet.Mix(ink, colour, 0.14);

            // The body: a plus with its corners, three by three, and a darker edge below.
            p.Rect(bx, y, 3, 3, CapSheet.Mix(body, ink, on ? 0.12 : 0.3), alpha);
            p.Set(bx + 1, y + 1, on ? CapSheet.Lighter(body, 0.25) : body, alpha);
            if (on)
            {
                p.Set(bx, y, CapSheet.Lighter(colour, 0.7), alpha);
                if (glow > 0)
                {
                    p.Set(bx - 1, y + 1, colour, (int)(70 * glow));
                    p.Set(bx + 3, y + 1, colour, (int)(70 * glow));
                    p.Set(bx + 1, y - 1, colour, (int)(60 * glow));
                    p.Set(bx + 1, y + 3, colour, (int)(60 * glow));
                }
            }
        }
    }

    /// <summary>A line of <paramref name="count"/> pearls, one cell each, two apart; <paramref name="lit"/> of them pale and bright.</summary>
    public static void Pearls(CapSheet p, int x, int y, int count, int lit, uint colour, uint ink, int alpha = 255)
    {
        for (var i = 0; i < count; i++)
        {
            p.Set(x + (i * 2), y, i < lit ? colour : CapSheet.Mix(ink, colour, 0.2), alpha);
        }
    }

    /// <summary>A coloured spot with a dark rim and a glint: the mark of a type or a status, five cells across.</summary>
    public static void Spot(CapSheet p, int x, int y, uint colour, uint ink, int alpha = 255)
    {
        p.Set(x + 1, y, ink, alpha);
        p.Set(x + 2, y, ink, alpha);
        p.Set(x + 3, y, ink, alpha);
        p.Set(x, y + 1, ink, alpha);
        p.Set(x + 4, y + 1, ink, alpha);
        p.Set(x, y + 2, ink, alpha);
        p.Set(x + 4, y + 2, ink, alpha);
        p.Set(x, y + 3, ink, alpha);
        p.Set(x + 4, y + 3, ink, alpha);
        p.Set(x + 1, y + 4, ink, alpha);
        p.Set(x + 2, y + 4, ink, alpha);
        p.Set(x + 3, y + 4, ink, alpha);
        p.Rect(x + 1, y + 1, 3, 3, colour, alpha);
        p.Set(x + 1, y + 1, CapSheet.Lighter(colour, 0.6), alpha);
        p.Set(x + 3, y + 3, CapSheet.Darker(colour, 0.3), alpha);
    }
}
