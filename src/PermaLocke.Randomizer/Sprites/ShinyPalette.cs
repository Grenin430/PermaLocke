namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Works out how a box icon changes colour when its Pokémon is shiny, from a pair of reference renders.
/// </summary>
/// <remarks>
/// <para>
/// The cartridge has no shiny icons: <c>a/0/6/2</c> holds one picture per form, and the game shows a shiny's
/// colours only on its 3D model. A normal render and a shiny render taken from the same camera line up pixel for
/// pixel, so together they say what every colour of the body turns into. That change is carried over to the icon.
/// </para>
/// <para>
/// The icon is lit differently from the render, so its colours are matched to the render's in LCh, weighing hue
/// over lightness, and the change is applied relatively: hue turned, chroma scaled, lightness shifted. Each colour
/// takes the <b>median</b> change of its nearest render colours and not the mean, because a body and a flame of
/// the same hue — Charizard — would otherwise blend into a colour neither of them has.
/// </para>
/// <para>
/// It is an approximation and is said so: where the icon has a colour the render does not, it gets the change
/// of the closest one. The result is a table of colours, not a picture: the picture stays the player's own.
/// </para>
/// </remarks>
public static class ShinyPalette
{
    /// <summary>How much lightness counts when matching; measured on Charizard, which separates body and flame at 1.5.</summary>
    public const double LightnessWeight = 1.5;

    /// <summary>Render colours each icon colour looks at.</summary>
    public const int Neighbours = 16;

    /// <summary>
    /// Below this overlap of the two silhouettes the renders are not the same pose, and a pixel of one says nothing
    /// about the same pixel of the other (Showdown's Ogerpon). Measured on the 1246 pairs downloaded: the 16 in another pose
    /// stay under 0.84, and every other one is at 0.92 or above.
    /// </summary>
    public const double MinimumOverlap = 0.9;

    /// <summary>Below this share of changed pixels the two renders are the same picture: the reference has no shiny.</summary>
    public const double MinimumChange = 0.02;

    /// <summary>
    /// The shiny colour of every colour of <paramref name="icon"/>, as 0xRRGGBB, or null when the renders cannot
    /// teach it: different sizes, different poses, or no change at all.
    /// </summary>
    /// <param name="normal">RGBA8888 of the normal render.</param>
    /// <param name="shiny">RGBA8888 of the shiny render, the same size.</param>
    /// <param name="icon">RGBA8888 of the icon; fully transparent pixels are ignored.</param>
    public static IReadOnlyDictionary<int, int>? Build(ReadOnlySpan<byte> normal, ReadOnlySpan<byte> shiny, ReadOnlySpan<byte> icon)
    {
        if (normal.Length != shiny.Length)
        {
            return null;
        }

        int both = 0, either = 0;
        for (var i = 3; i < normal.Length; i += 4)
        {
            var a = normal[i] >= 128;
            var b = shiny[i] >= 128;
            both += a && b ? 1 : 0;
            either += a || b ? 1 : 0;
        }

        if (either == 0 || both < either * MinimumOverlap)
        {
            return null;
        }

        var from = new List<double[]>();
        var to = new List<double[]>();
        var changed = 0;

        for (var i = 0; i < normal.Length / 4; i++)
        {
            if (normal[i * 4 + 3] < 250 || shiny[i * 4 + 3] < 250)
            {
                continue;
            }

            from.Add(Lch(normal.Slice(i * 4, 3)));
            to.Add(Lch(shiny.Slice(i * 4, 3)));

            if (Math.Abs(normal[i * 4] - shiny[i * 4]) > 12 || Math.Abs(normal[i * 4 + 1] - shiny[i * 4 + 1]) > 12
                || Math.Abs(normal[i * 4 + 2] - shiny[i * 4 + 2]) > 12)
            {
                changed++;
            }
        }

        if (from.Count == 0 || changed < from.Count * MinimumChange)
        {
            return null;
        }

        var map = new Dictionary<int, int>();
        for (var i = 0; i < icon.Length / 4; i++)
        {
            if (icon[i * 4 + 3] == 0)
            {
                continue;
            }

            var key = (icon[i * 4] << 16) | (icon[i * 4 + 1] << 8) | icon[i * 4 + 2];
            if (!map.ContainsKey(key))
            {
                map[key] = Recolour(Lch(icon.Slice(i * 4, 3)), from, to);
            }
        }

        return map;
    }

    private static int Recolour(double[] colour, List<double[]> from, List<double[]> to)
    {
        var near = Enumerable.Range(0, from.Count)
            .Select(t => (t, d: Distance(colour, from[t])))
            .OrderBy(x => x.d)
            .Take(Neighbours)
            .Select(x => x.t)
            .ToList();

        var lightness = colour[0] + Median(near.Select(t => to[t][0] - from[t][0]));
        var coloured = near.Where(t => from[t][1] >= 6).ToList();
        double chroma, hue;

        // A grey of the render tells nothing about how to turn a colour; when most neighbours are grey, the
        // icon colour takes the nearest one's shiny colour outright.
        if (coloured.Count * 2 < near.Count)
        {
            chroma = to[near[0]][1];
            hue = to[near[0]][2];
        }
        else
        {
            chroma = colour[1] * Median(coloured.Select(t => to[t][1] / from[t][1]));
            hue = colour[2] + Median(coloured.Select(t => Wrap(to[t][2] - from[t][2])));
        }

        return ToRgb(Math.Clamp(lightness, 0, 100), chroma, hue);
    }

    /// <summary>Hue counts only where both colours have some; lightness is down-weighted because the lighting differs.</summary>
    private static double Distance(double[] a, double[] b)
    {
        var hueTerm = 2 * Math.Min(a[1], b[1]) * Math.Sin(Math.Abs(Wrap(a[2] - b[2])) * Math.PI / 360);
        return hueTerm * hueTerm * 2 + (a[1] - b[1]) * (a[1] - b[1]) * 0.5 + LightnessWeight * (a[0] - b[0]) * (a[0] - b[0]);
    }

    private static double Wrap(double degrees) => degrees > 180 ? degrees - 360 : degrees < -180 ? degrees + 360 : degrees;

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static double Linear(byte v)
    {
        var c = v / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;

    private static double FInverse(double t) => t * t * t > 0.008856 ? t * t * t : (t - 16.0 / 116) / 7.787;

    /// <summary>sRGB to CIE LCh (D65): lightness, chroma, hue in degrees.</summary>
    private static double[] Lch(ReadOnlySpan<byte> rgb)
    {
        double r = Linear(rgb[0]), g = Linear(rgb[1]), b = Linear(rgb[2]);
        var x = F((0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047);
        var y = F(0.2126 * r + 0.7152 * g + 0.0722 * b);
        var z = F((0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883);
        double l = 116 * y - 16, a = 500 * (x - y), bb = 200 * (y - z);
        return [l, Math.Sqrt(a * a + bb * bb), Math.Atan2(bb, a) * 180 / Math.PI];
    }

    private static int ToRgb(double l, double c, double h)
    {
        var a = c * Math.Cos(h * Math.PI / 180);
        var b = c * Math.Sin(h * Math.PI / 180);
        var fy = (l + 16) / 116;
        double x = FInverse(fy + a / 500) * 0.95047, y = FInverse(fy), z = FInverse(fy - b / 200) * 1.08883;

        static int Channel(double v)
        {
            v = Math.Clamp(v, 0, 1);
            return (int)Math.Round(255 * (v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055));
        }

        return (Channel(3.2406 * x - 1.5372 * y - 0.4986 * z) << 16)
            | (Channel(-0.9689 * x + 1.8758 * y + 0.0415 * z) << 8)
            | Channel(0.0557 * x - 0.2040 * y + 1.0570 * z);
    }
}
