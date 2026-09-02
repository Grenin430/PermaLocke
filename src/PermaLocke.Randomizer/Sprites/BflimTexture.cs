namespace PermaLocke.Randomizer.Sprites;

/// <summary>How the pixels of a BFLIM are encoded. Only what Ultra Moon's icons actually use.</summary>
public enum BflimFormat
{
    L8 = 0,
    A8 = 1,
    La4 = 2,
    La8 = 3,
    HiLo8 = 4,
    Rgb565 = 5,
    Rgb8 = 6,
    Rgba5551 = 7,
    Rgba4444 = 8,
    Rgba8888 = 9,
    Etc1 = 10,
    Etc1A4 = 11,
    L4 = 12,
    A4 = 13,
}

/// <summary>
/// A 3DS BFLIM texture decoded to straight RGBA8888.
/// <para>
/// The format is documented by its own 0x28-byte footer, which sits at the <em>end</em> of the
/// file: <c>FLIM</c> header plus an <c>imag</c> block carrying width, height, alignment, pixel
/// format and a swizzle byte. Written from that layout, not copied from anyone's code.
/// </para>
/// </summary>
public sealed class BflimTexture
{
    private const int FooterSize = 0x28;

    private BflimTexture(int width, int height, BflimFormat format, byte[] pixels)
    {
        Width = width;
        Height = height;
        Format = format;
        Pixels = pixels;
    }

    /// <summary>Width of the image as displayed, after any swizzle has been undone.</summary>
    public int Width { get; }

    public int Height { get; }

    public BflimFormat Format { get; }

    /// <summary>RGBA8888, row-major, four bytes per pixel.</summary>
    public byte[] Pixels { get; }

    /// <summary>
    /// Reads a BFLIM. Throws when the footer is missing or the pixel format is one PermaLocke
    /// has never seen in this cartridge: guessing would produce a picture that is quietly wrong.
    /// </summary>
    public static BflimTexture Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length < FooterSize)
        {
            throw new InvalidDataException("El fichero es más corto que la cola de un BFLIM.");
        }

        var footer = file[^FooterSize..];
        if (footer[0] != (byte)'F' || footer[1] != (byte)'L' || footer[2] != (byte)'I' || footer[3] != (byte)'M')
        {
            throw new InvalidDataException("No lleva la firma FLIM al final: no es un BFLIM.");
        }

        // 'imag' block: width, height, alignment, format, swizzle, data size.
        var width = BitConverter.ToUInt16(footer[0x1C..]);
        var height = BitConverter.ToUInt16(footer[0x1E..]);
        var format = (BflimFormat)footer[0x22];
        var swizzle = footer[0x23];
        var dataSize = (int)BitConverter.ToUInt32(footer[0x24..]);

        if (dataSize <= 0 || dataSize > file.Length - FooterSize)
        {
            throw new InvalidDataException($"La cola declara {dataSize} bytes de píxeles y el fichero no los tiene.");
        }

        // A rotated texture is stored with its axes swapped, so the tiles must be walked over the
        // stored dimensions and only then turned back.
        var rotated = swizzle == 4;
        var storedWidth = rotated ? height : width;
        var storedHeight = rotated ? width : height;

        var pixels = Untile(file[..dataSize], storedWidth, storedHeight, format);
        if (rotated)
        {
            pixels = RotateCounterClockwise(pixels, storedWidth, storedHeight);
        }

        return new BflimTexture(width, height, format, pixels);
    }

    /// <summary>
    /// 3DS textures are stored as 8x8 tiles, and within a tile the pixels follow Morton (Z)
    /// order: the even bits of the index give X and the odd bits give Y.
    /// </summary>
    private static byte[] Untile(ReadOnlySpan<byte> data, int width, int height, BflimFormat format)
    {
        // ETC1 is block compression, not pixels in an order, so it untiles itself.
        if (format is BflimFormat.Etc1 or BflimFormat.Etc1A4)
        {
            return Etc1Texture.Decode(data, width, height, format == BflimFormat.Etc1A4);
        }

        var bytesPerPixel = format switch
        {
            BflimFormat.Rgba5551 or BflimFormat.Rgba4444 or BflimFormat.Rgb565 or BflimFormat.La8 => 2,
            BflimFormat.Rgba8888 => 4,
            BflimFormat.Rgb8 => 3,
            BflimFormat.L8 or BflimFormat.A8 or BflimFormat.La4 => 1,
            _ => throw new NotSupportedException(
                $"Formato de textura {format} no soportado. Los iconos de Ultra Luna son RGBA5551."),
        };

        var output = new byte[width * height * 4];
        var index = 0;

        for (var tileY = 0; tileY < height / 8; tileY++)
        for (var tileX = 0; tileX < width / 8; tileX++)
        for (var p = 0; p < 64; p++)
        {
            var localX = (p & 1) | ((p >> 1) & 2) | ((p >> 2) & 4);
            var localY = ((p >> 1) & 1) | ((p >> 2) & 2) | ((p >> 3) & 4);
            var x = (tileX * 8) + localX;
            var y = (tileY * 8) + localY;

            var source = index * bytesPerPixel;
            index++;
            var target = ((y * width) + x) * 4;
            WritePixel(data, source, format, output, target);
        }

        return output;
    }

    private static void WritePixel(ReadOnlySpan<byte> data, int source, BflimFormat format,
        byte[] output, int target)
    {
        switch (format)
        {
            case BflimFormat.Rgba5551:
            {
                var value = BitConverter.ToUInt16(data[source..]);
                output[target] = Expand5((value >> 11) & 0x1F);
                output[target + 1] = Expand5((value >> 6) & 0x1F);
                output[target + 2] = Expand5((value >> 1) & 0x1F);
                output[target + 3] = (byte)((value & 1) * 255);
                break;
            }

            case BflimFormat.Rgba4444:
            {
                var value = BitConverter.ToUInt16(data[source..]);
                output[target] = Expand4((value >> 12) & 0xF);
                output[target + 1] = Expand4((value >> 8) & 0xF);
                output[target + 2] = Expand4((value >> 4) & 0xF);
                output[target + 3] = Expand4(value & 0xF);
                break;
            }

            case BflimFormat.Rgba8888:
                output[target] = data[source + 3];
                output[target + 1] = data[source + 2];
                output[target + 2] = data[source + 1];
                output[target + 3] = data[source];
                break;

            case BflimFormat.Rgb565:
            {
                var value = BitConverter.ToUInt16(data[source..]);
                output[target] = Expand5((value >> 11) & 0x1F);
                output[target + 1] = (byte)(((value >> 5) & 0x3F) * 255 / 63);
                output[target + 2] = Expand5(value & 0x1F);
                output[target + 3] = 255;
                break;
            }

            case BflimFormat.Rgb8:
                output[target] = data[source + 2];
                output[target + 1] = data[source + 1];
                output[target + 2] = data[source];
                output[target + 3] = 255;
                break;

            case BflimFormat.L8:
                output[target] = output[target + 1] = output[target + 2] = data[source];
                output[target + 3] = 255;
                break;

            case BflimFormat.A8:
                output[target] = output[target + 1] = output[target + 2] = 255;
                output[target + 3] = data[source];
                break;

            case BflimFormat.La4:
                output[target] = output[target + 1] = output[target + 2] = Expand4(data[source] >> 4);
                output[target + 3] = Expand4(data[source] & 0xF);
                break;

            case BflimFormat.La8:
                output[target] = output[target + 1] = output[target + 2] = data[source + 1];
                output[target + 3] = data[source];
                break;

            default:
                throw new NotSupportedException($"Formato de textura {format} no soportado.");
        }
    }

    private static byte Expand5(int value) => (byte)(value * 255 / 31);

    private static byte Expand4(int value) => (byte)(value * 255 / 15);

    /// <summary>Turns a stored texture back the way the header says it should be seen.</summary>
    private static byte[] RotateCounterClockwise(byte[] pixels, int width, int height)
    {
        var output = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var target = (((width - 1 - x) * height) + y) * 4;
            Array.Copy(pixels, ((y * width) + x) * 4, output, target, 4);
        }
        return output;
    }
}
