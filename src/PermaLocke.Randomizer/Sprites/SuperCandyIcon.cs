using System.Buffers.Binary;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// The SuperCarameloraro's icon (2026-10-06, the organiser picked it among four): the Rare Candy in deep red, with a
/// golden aura around it and three sparkles. The game gets it as a BFLIM laid out like the Rare Candy's; the app draws
/// the same pixels.
/// </summary>
public static class SuperCandyIcon
{
    private static readonly (int R, int G, int B) Dark = (120, 0, 20), Mid = (230, 30, 60), Light = (255, 200, 200);
    private static readonly (int R, int G, int B) Aura = (255, 200, 0), Sparkle = (255, 230, 80), White = (255, 255, 255);

    /// <summary>Paints the Rare Candy's pixels (RGBA, <paramref name="width"/> × <paramref name="height"/>, uncropped).</summary>
    public static byte[] Paint(byte[] rareCandy, int width, int height)
    {
        var output = (byte[])rareCandy.Clone();

        // El caramelo, de rojo; el contorno oscuro se queda como está.
        for (var at = 0; at < output.Length; at += 4)
        {
            if (rareCandy[at + 3] == 0) continue;
            var light = (0.3 * rareCandy[at] + 0.59 * rareCandy[at + 1] + 0.11 * rareCandy[at + 2]) / 255.0;
            if (light < 0.18) continue;

            var colour = light < 0.6
                ? Lerp(Dark, Mid, Math.Clamp((light - 0.18) / 0.42, 0, 1))
                : Lerp(Mid, Light, Math.Clamp((light - 0.6) / 0.4, 0, 1));
            Put(output, at, colour);
        }

        // El aura: un píxel dorado alrededor de todo lo dibujado.
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (rareCandy[(y * width + x) * 4 + 3] != 0) continue;

            var touches = false;
            for (var dy = -1; dy <= 1 && !touches; dy++)
            for (var dx = -1; dx <= 1 && !touches; dx++)
            {
                int nx = x + dx, ny = y + dy;
                touches = nx >= 0 && ny >= 0 && nx < width && ny < height && rareCandy[(ny * width + nx) * 4 + 3] != 0;
            }

            if (touches) Put(output, (y * width + x) * 4, Aura);
        }

        Star(output, width, height, 5, 6, 2, Sparkle);
        Star(output, width, height, 26, 25, 2, Sparkle);
        Star(output, width, height, 27, 5, 1, White);
        return output;
    }

    /// <summary>The Rare Candy's BFLIM (LZ-free, RGBA5551) repainted; null when it is not that.</summary>
    public static byte[]? Bflim(byte[] rareCandy) => Repaint(rareCandy, Paint);

    /// <summary>
    /// An item icon's BFLIM (LZ-free, RGBA5551) repainted by <paramref name="paint"/>, with its own layout and footer;
    /// null when it is not one.
    /// </summary>
    /// <remarks>
    /// The layout (8×8 tiles in Morton order, maybe stored rotated) is not worked out again: the decoder is handed a copy
    /// whose every stored pixel carries its own index, and where each index lands says where each pixel goes back.
    /// </remarks>
    public static byte[]? Repaint(byte[] icon, Func<byte[], int, int, byte[]> paint)
    {
        BflimTexture texture;
        try
        {
            texture = BflimTexture.Decode(icon);
        }
        catch (Exception)
        {
            return null;
        }

        var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(icon.AsSpan(icon.Length - 0x28 + 0x24));
        if (texture.Format != BflimFormat.Rgba5551 || size != texture.Width * texture.Height * 2 || size > 2048) return null;

        var probe = (byte[])icon.Clone();
        for (var index = 0; index < size / 2; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(probe.AsSpan(index * 2), (ushort)((index & 31) << 11 | (index >> 5) << 6 | 1));
        }

        var where = BflimTexture.Decode(probe).Pixels;
        var painted = paint(texture.Pixels, texture.Width, texture.Height);
        var output = (byte[])icon.Clone();

        for (var pixel = 0; pixel < texture.Width * texture.Height; pixel++)
        {
            var at = pixel * 4;
            var index = (where[at] >> 3) | (where[at + 1] >> 3) << 5;
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(index * 2), (ushort)(
                (painted[at] >> 3) << 11 | (painted[at + 1] >> 3) << 6 | (painted[at + 2] >> 3) << 1 | (painted[at + 3] >= 128 ? 1 : 0)));
        }

        return output;
    }

    internal static void Star(byte[] pixels, int width, int height, int cx, int cy, int size, (int R, int G, int B) colour)
    {
        for (var d = -size; d <= size; d++)
        {
            Dot(cx + d, cy, colour);
            Dot(cx, cy + d, colour);
        }

        Dot(cx, cy, White);

        void Dot(int x, int y, (int R, int G, int B) c)
        {
            if (x >= 0 && y >= 0 && x < width && y < height) Put(pixels, (y * width + x) * 4, c);
        }
    }

    internal static void Put(byte[] pixels, int at, (int R, int G, int B) colour)
    {
        pixels[at] = (byte)colour.R;
        pixels[at + 1] = (byte)colour.G;
        pixels[at + 2] = (byte)colour.B;
        pixels[at + 3] = 255;
    }

    internal static (int R, int G, int B) Lerp((int R, int G, int B) a, (int R, int G, int B) b, double t) =>
        ((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}
