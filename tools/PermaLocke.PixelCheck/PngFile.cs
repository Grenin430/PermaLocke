using System.IO.Compression;

namespace PermaLocke.PixelCheck;

/// <summary>BGRA pixels to a PNG file, each one as a square of <c>scale</c> pixels: for looking at drawings.</summary>
internal static class PngFile
{
    public static void Write(byte[] bgra, int width, int height, int scale, string path)
    {
        int w = width * scale, h = height * scale;
        var raw = new byte[h * ((w * 4) + 1)];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var from = (((y / scale) * width) + (x / scale)) * 4;
                var o = (y * ((w * 4) + 1)) + 1 + (x * 4);
                raw[o] = bgra[from + 2];
                raw[o + 1] = bgra[from + 1];
                raw[o + 2] = bgra[from];
                raw[o + 3] = bgra[from + 3];
            }
        }

        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BigEndian(header, 0, w);
        BigEndian(header, 4, h);
        header[8] = 8;
        header[9] = 6;
        Chunk(file, "IHDR", header);
        using var packed = new MemoryStream();
        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, true))
        {
            zlib.Write(raw);
        }

        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void BigEndian(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BigEndian(length, 0, data.Length);
        stream.Write(length);
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(body);
        var crc = new byte[4];
        BigEndian(crc, 0, (int)Crc(body));
        stream.Write(crc);
    }

    private static uint Crc(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            c ^= b;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
        }

        return c ^ 0xFFFFFFFF;
    }
}
