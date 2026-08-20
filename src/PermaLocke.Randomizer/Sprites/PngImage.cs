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
