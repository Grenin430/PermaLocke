namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// The Repelente Infinito's icon (2026-10-07, the organiser picked the «galaxy» among five): the Repel's can painted as a
/// night sky from blue to magenta, with stars, a golden ∞ over the Poké Ball emblem and a blue glow. The game gets it as a
/// BFLIM laid out like the Repel's (<see cref="SuperCandyIcon.Repaint"/>); the app draws the same pixels.
/// </summary>
public static class InfiniteRepelIcon
{
    private static readonly (int R, int G, int B) SkyLow = (30, 20, 120), SkyHigh = (200, 60, 200), White = (255, 255, 255);
    private static readonly (int R, int G, int B) CapDark = (90, 100, 140), CapLight = (230, 235, 255);
    private static readonly (int R, int G, int B) Gold = (255, 240, 140), Glow = (120, 160, 255), Twinkle = (200, 220, 255);

    private static readonly (int X, int Y)[] Stars = [(12, 14), (19, 16), (11, 23), (20, 24), (16, 15)];

    private static readonly string[] Infinity = [".##..##.", "#..##..#", "#..##..#", ".##..##."];

    /// <summary>The Repel's BFLIM (LZ-free, RGBA5551) repainted; null when it is not that.</summary>
    public static byte[]? Bflim(byte[] repel) => SuperCandyIcon.Repaint(repel, Paint);

    /// <summary>Paints the Repel's pixels (RGBA, <paramref name="width"/> × <paramref name="height"/>, uncropped).</summary>
    public static byte[] Paint(byte[] repel, int width, int height)
    {
        var output = (byte[])repel.Clone();

        bool Drawn(int x, int y) => repel[(y * width + x) * 4 + 3] != 0;
        int Channel(int x, int y, int c) => repel[(y * width + x) * 4 + c];
        double Light(int x, int y) => (0.3 * Channel(x, y, 0) + 0.59 * Channel(x, y, 1) + 0.11 * Channel(x, y, 2)) / 255.0;

        // La lata es lo verde de abajo; el emblema, la Poké Ball clara que lleva delante; la tapa, lo que no es oscuro arriba.
        bool Body(int x, int y) => Drawn(x, y) && y >= 12
            && Channel(x, y, 1) > Channel(x, y, 0) + 25 && Channel(x, y, 1) > Channel(x, y, 2) + 25;
        bool Emblem(int x, int y) => x is >= 11 and <= 20 && y is >= 17 and <= 23 && Drawn(x, y)
            && Channel(x, y, 0) + Channel(x, y, 1) > 120;
        bool Cap(int x, int y) => y <= 13 && Drawn(x, y) && !Body(x, y)
            && Math.Max(Channel(x, y, 0), Math.Max(Channel(x, y, 1), Channel(x, y, 2))) >= 70;

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var at = (y * width + x) * 4;

            if (Body(x, y) || Emblem(x, y))
            {
                var light = Emblem(x, y) ? 0.55 : Light(x, y);
                var sky = SuperCandyIcon.Lerp(SkyLow, SkyHigh, Math.Clamp((x - 10 + (y - 12) * 0.5) / 16.0, 0, 1));
                SuperCandyIcon.Put(output, at, SuperCandyIcon.Lerp(sky, White, Math.Max(0, light - 0.6)));
            }
            else if (Cap(x, y))
            {
                SuperCandyIcon.Put(output, at, SuperCandyIcon.Lerp(CapDark, CapLight, Math.Clamp(Light(x, y), 0, 1)));
            }
        }

        foreach (var (x, y) in Stars) Dot(output, width, height, x, y, White);

        for (var row = 0; row < Infinity.Length; row++)
        for (var column = 0; column < Infinity[row].Length; column++)
        {
            if (Infinity[row][column] == '#') Dot(output, width, height, 12 + column, 18 + row, Gold);
        }

        // El brillo: un píxel azul alrededor de todo lo dibujado.
        var painted = (byte[])output.Clone();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (painted[(y * width + x) * 4 + 3] != 0) continue;

            var touches = false;
            for (var dy = -1; dy <= 1 && !touches; dy++)
            for (var dx = -1; dx <= 1 && !touches; dx++)
            {
                int nx = x + dx, ny = y + dy;
                touches = nx >= 0 && ny >= 0 && nx < width && ny < height && painted[(ny * width + nx) * 4 + 3] != 0;
            }

            if (touches) SuperCandyIcon.Put(output, (y * width + x) * 4, Glow);
        }

        SuperCandyIcon.Star(output, width, height, 6, 8, 2, Twinkle);
        SuperCandyIcon.Star(output, width, height, 26, 23, 1, White);
        return output;
    }

    private static void Dot(byte[] pixels, int width, int height, int x, int y, (int R, int G, int B) colour)
    {
        if (x >= 0 && y >= 0 && x < width && y < height) SuperCandyIcon.Put(pixels, (y * width + x) * 4, colour);
    }
}
