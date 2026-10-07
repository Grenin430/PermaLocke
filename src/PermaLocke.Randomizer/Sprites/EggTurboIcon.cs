namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// The Incubadora Turbo's icon (2026-10-07, the organiser picked the «rayo» among seven): a cream egg with green spots, a golden
/// lightning bolt across it, speed lines on the left and a golden outline. Drawn from nothing, but handed to the game as the
/// Repel's BFLIM with its pixels replaced (<see cref="SuperCandyIcon.Repaint"/>), which keeps the layout the game reads; the app
/// draws the same pixels.
/// </summary>
public static class EggTurboIcon
{
    private const int Size = 32;

    private static readonly (int R, int G, int B) Cream = (246, 236, 196), CreamHi = (255, 252, 232), CreamShade = (214, 198, 146),
        CreamDeep = (168, 148, 98), Ink = (28, 22, 36), Gold = (255, 226, 80), Spot = (120, 190, 110), SpotDark = (80, 150, 85);

    /// <summary>The Repel's BFLIM (LZ-free, RGBA5551) repainted; null when it is not that.</summary>
    public static byte[]? Bflim(byte[] repel) => SuperCandyIcon.Repaint(repel, Paint);

    /// <summary>The icon's pixels (RGBA, 32 × 32, uncropped); the template's own are not used.</summary>
    public static byte[] Paint(byte[] template, int width, int height)
    {
        if (width != Size || height != Size) throw new ArgumentException("El icono mide 32 × 32.");

        var o = new byte[Size * Size * 4];
        Egg(o);
        Outline(o, Ink);

        // El rayo: borde oscuro, cuerpo amarillo y filo claro.
        Poly(o, [(20.5, 3), (12, 17), (17, 17), (13.5, 29), (25, 12.5), (19.5, 12.5), (23.5, 3)], (190, 110, 0));
        Poly(o, [(21.5, 4), (13.5, 17.5), (18.5, 17.5), (14.5, 28), (24, 13.5), (18.5, 13.5), (22.5, 4)], (255, 205, 20));
        Poly(o, [(21, 5), (15, 16), (19.5, 16), (16.5, 24), (22.5, 14.5), (17, 14.5), (21.5, 5)], (255, 245, 150));
        Outline(o, Ink);
        Glow(o, Gold);

        // Las líneas de velocidad, a la izquierda, y las estrellas.
        foreach (var (y, length) in new[] { (11, 5), (16, 7), (21, 5) })
        {
            for (var x = 0; x < length; x++) Put(o, 1 + x, y, x == 0 ? Gold : (255, 255, 255));
        }

        SuperCandyIcon.Star(o, Size, Size, 4, 5, 2, (255, 235, 100));
        SuperCandyIcon.Star(o, Size, Size, 28, 26, 1, (255, 255, 200));
        return o;
    }

    /// <summary>The egg: narrower at the top, lit from the upper left, with five spots.</summary>
    private static void Egg(byte[] o)
    {
        const double cx = 18, cy = 16, rx = 8, ry = 11;
        var mask = new bool[Size, Size];

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var v = (y + 0.5 - cy) / ry;
            if (v < -1 || v > 1) continue;

            var half = rx * Math.Sqrt(1 - v * v) * (1 + 0.14 * v);
            if (Math.Abs(x + 0.5 - cx) > half) continue;

            mask[x, y] = true;
            var u = (x + 0.5 - cx) / rx;
            var light = 0.55 - 0.45 * u - 0.38 * v;
            Put(o, x, y, light > 0.82 ? CreamHi : light > 0.45 ? Cream : light > 0.18 ? CreamShade : CreamDeep);
        }

        foreach (var (sx, sy, r) in new[] { (-3, -3, 1), (3, 1, 2), (-2, 4, 1), (1, -6, 1), (-5, 0, 1) })
        for (var dy = 0; dy <= r; dy++)
        for (var dx = 0; dx <= r + (dy == 0 ? 1 : 0); dx++)
        {
            int px = (int)cx + sx + dx, py = (int)cy + sy + dy;
            if (px >= 0 && py >= 0 && px < Size && py < Size && mask[px, py]) Put(o, px, py, dy == r ? SpotDark : Spot);
        }
    }

    private static bool Filled(byte[] o, int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && o[(y * Size + x) * 4 + 3] != 0;

    private static void Put(byte[] o, int x, int y, (int R, int G, int B) colour)
    {
        if (x >= 0 && y >= 0 && x < Size && y < Size) SuperCandyIcon.Put(o, (y * Size + x) * 4, colour);
    }

    /// <summary>A one-pixel ring round everything drawn.</summary>
    private static void Outline(byte[] o, (int R, int G, int B) colour)
    {
        var before = (byte[])o.Clone();

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (Filled(before, x, y)) continue;
            if (Filled(before, x - 1, y) || Filled(before, x + 1, y) || Filled(before, x, y - 1) || Filled(before, x, y + 1)) Put(o, x, y, colour);
        }
    }

    /// <summary>Like <see cref="Outline"/>, but counting the diagonals too.</summary>
    private static void Glow(byte[] o, (int R, int G, int B) colour)
    {
        var before = (byte[])o.Clone();

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (Filled(before, x, y)) continue;

            var touches = false;
            for (var dy = -1; dy <= 1 && !touches; dy++)
            for (var dx = -1; dx <= 1 && !touches; dx++) touches = Filled(before, x + dx, y + dy);

            if (touches) Put(o, x, y, colour);
        }
    }

    /// <summary>Fills a polygon, pixel centres inside.</summary>
    private static void Poly(byte[] o, (double X, double Y)[] points, (int R, int G, int B) colour)
    {
        for (var y = 0; y < Size; y++)
        {
            var crossings = new List<double>();

            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                if ((a.Y <= y + 0.5 && b.Y > y + 0.5) || (b.Y <= y + 0.5 && a.Y > y + 0.5))
                {
                    crossings.Add(a.X + (y + 0.5 - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
            }

            crossings.Sort();

            for (var k = 0; k + 1 < crossings.Count; k += 2)
            for (var x = (int)Math.Ceiling(crossings[k] - 0.5); x <= (int)Math.Floor(crossings[k + 1] - 0.5); x++) Put(o, x, y, colour);
        }
    }
}
