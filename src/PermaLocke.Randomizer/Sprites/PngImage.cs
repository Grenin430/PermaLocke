using System.IO.Compression;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// A minimal PNG encoder for RGBA8888 buffers.
/// <para>
/// Written by hand on purpose: <c>System.Drawing</c> is Windows-only and WPF's imaging lives
/// behind PresentationCore, and <c>PermaLocke.Randomizer</c> is not allowed to reference either
/// (see CLAUDE.md). PNG needs only zlib, which the framework already provides.
/// </para>
/// </summary>
public static class PngImage
{
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("El búfer no tiene píxeles suficientes para ese tamaño.", nameof(rgba));
        }

        using var output = new MemoryStream();
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;      // bits per channel
        header[9] = 6;      // colour type: RGBA
        WriteChunk(output, "IHDR", header);

        // Each scanline is prefixed with its filter type; 0 means "no filter", which keeps the
        // encoder honest and small. zlib still compresses these sprites down to a few hundred bytes.
        var raw = new byte[height * ((width * 4) + 1)];
        for (var y = 0; y < height; y++)
        {
            var target = y * ((width * 4) + 1);
            raw[target] = 0;
            rgba.Slice(y * width * 4, width * 4).CopyTo(raw.AsSpan(target + 1));
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>
    /// Reads an 8-bit PNG back to RGBA8888: truecolour, truecolour with alpha, or palette with its optional
    /// <c>tRNS</c>. Interlacing and other bit depths are refused, not guessed at.
    /// </summary>
    /// <remarks>Used by RomTool to read reference renders, which come in those three flavours.</remarks>
    public static (byte[] Rgba, int Width, int Height) Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G')
        {
            throw new InvalidDataException("No es un PNG.");
        }

        int width = 0, height = 0, type = -1;
        byte[] palette = [], alpha = [];
        using var idat = new MemoryStream();

        for (var p = 8; p + 12 <= png.Length;)
        {
            var length = ReadBigEndian(png, p);
            var name = System.Text.Encoding.ASCII.GetString(png.Slice(p + 4, 4));
            var data = png.Slice(p + 8, length);

            switch (name)
            {
                case "IHDR":
                    width = ReadBigEndian(data, 0);
                    height = ReadBigEndian(data, 4);
                    type = data[9];
                    if (data[8] != 8 || data[12] != 0 || type is not (2 or 3 or 6))
                    {
                        throw new InvalidDataException($"PNG no admitido: {data[8]} bits, tipo {type}, entrelazado {data[12]}.");
                    }
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": alpha = data.ToArray(); break;
                case "IDAT": idat.Write(data); break;
            }

            p += 12 + length;
        }

        var channels = type switch { 2 => 3, 6 => 4, _ => 1 };
        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        idat.Position = 0;
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
        {
            zlib.ReadExactly(raw);
        }

        var rows = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                int a = x >= channels ? rows[y * stride + x - channels] : 0;
                int b = y > 0 ? rows[(y - 1) * stride + x] : 0;
                int c = x >= channels && y > 0 ? rows[(y - 1) * stride + x - channels] : 0;
                int v = raw[y * (stride + 1) + 1 + x];
                rows[y * stride + x] = (byte)(filter switch
                {
                    0 => v,
                    1 => v + a,
                    2 => v + b,
                    3 => v + ((a + b) >> 1),
                    4 => v + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"Filtro PNG desconocido: {filter}.")
                });
            }
        }

        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            switch (type)
            {
                case 6: rows.AsSpan(i * 4, 4).CopyTo(rgba.AsSpan(i * 4)); break;
                case 2: rows.AsSpan(i * 3, 3).CopyTo(rgba.AsSpan(i * 4)); rgba[i * 4 + 3] = 255; break;
                default:
                    var entry = rows[i];
                    palette.AsSpan(entry * 3, 3).CopyTo(rgba.AsSpan(i * 4));
                    rgba[i * 4 + 3] = entry < alpha.Length ? alpha[entry] : (byte)255;
                    break;
            }
        }

        return (rgba, width, height);
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int ReadBigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++)
        {
            body[i] = (byte)type[i];
        }
        data.CopyTo(body.AsSpan(4));
        stream.Write(body);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, unchecked((int)Crc32(body)));
        stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(crc & 1));
            }
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
