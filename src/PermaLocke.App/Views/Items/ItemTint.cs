namespace PermaLocke.App.Views;

/// <summary>The colour an item is known by (2026-10-09), taken from its own icon so that no table of colours is written by hand.</summary>
public static class ItemTint
{
    /// <summary>
    /// The most common colour of the icon that is neither outline, nor highlight, nor grey: the red of a Fire Stone, the
    /// pink of a Pecha Berry, the half of a ball. Opaque BGRA, or 0 when the icon has nothing coloured.
    /// </summary>
    public static uint Of(byte[] bgra)
    {
        // Sixteen steps per channel are enough to tell a stone from its shading, and few enough to gather the shades of a colour.
        var count = new int[4096];
        var sum = new long[4096 * 3];

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128) continue;

            int b = bgra[i], g = bgra[i + 1], r = bgra[i + 2];
            var top = Math.Max(r, Math.Max(g, b));
            var bottom = Math.Min(r, Math.Min(g, b));
            if (top < 70 || bottom > 215 || top - bottom < 40) continue;

            var bucket = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
            count[bucket]++;
            sum[bucket * 3] += b;
            sum[(bucket * 3) + 1] += g;
            sum[(bucket * 3) + 2] += r;
        }

        var best = 0;
        for (var bucket = 1; bucket < count.Length; bucket++)
        {
            if (count[bucket] > count[best]) best = bucket;
        }

        if (count[best] == 0) return 0;

        var n = count[best];
        return 0xFF000000 | (uint)(sum[best * 3] / n) | ((uint)(sum[(best * 3) + 1] / n) << 8) | ((uint)(sum[(best * 3) + 2] / n) << 16);
    }

    /// <summary>The place of a colour on the wheel, in degrees: 0 red, 60 yellow, 120 green, 180 cyan, 240 blue, 300 magenta.</summary>
    public static double Hue(uint bgra) => Hsl(bgra).H;

    /// <summary>How far a colour is from grey, 0 to 1.</summary>
    public static double Saturation(uint bgra) => Hsl(bgra).S;

    private static (double H, double S, double L) Hsl(uint bgra)
    {
        double r = ((bgra >> 16) & 0xFF) / 255.0, g = ((bgra >> 8) & 0xFF) / 255.0, b = (bgra & 0xFF) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        var s = d == 0 ? 0 : d / (1 - Math.Abs((2 * l) - 1));
        var h = d == 0 ? 0
            : max == r ? 60 * (((g - b) / d) % 6)
            : max == g ? 60 * (((b - r) / d) + 2)
            : 60 * (((r - g) / d) + 4);
        return (h < 0 ? h + 360 : h, s, l);
    }

    /// <summary>
    /// A shade of a colour: the same one turned round the colour wheel, at the lightness asked for and with its saturation
    /// scaled. The family of colours a scene draws with is made of these, so that it stays one palette whatever the item.
    /// </summary>
    /// <param name="bgra">Opaque BGRA, as <see cref="Of"/> returns it.</param>
    /// <param name="hueShift">Degrees round the wheel.</param>
    /// <param name="lightness">0 black, 1 white, 0.5 the colour at its fullest.</param>
    public static uint Shade(uint bgra, double hueShift, double lightness, double saturationScale = 1)
    {
        var (h, s, _) = Hsl(bgra);

        h = (h + hueShift + 720) % 360;
        s = Math.Clamp(s * saturationScale, 0, 1);

        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * s;
        var x = chroma * (1 - Math.Abs(((h / 60) % 2) - 1));
        var m = lightness - (chroma / 2);
        var (r1, g1, b1) = (int)(h / 60) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };

        static byte Channel(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return Channel(b1 + m) | ((uint)Channel(g1 + m) << 8) | ((uint)Channel(r1 + m) << 16) | 0xFF000000;
    }
}
