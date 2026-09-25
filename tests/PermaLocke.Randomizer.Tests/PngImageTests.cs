using System.IO.Compression;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The PNG encoder is hand-written because the randomizer may not reference System.Drawing or
/// WPF, so it needs tests that read the file back instead of trusting it.
/// </summary>
public class PngImageTests
{
    [Fact]
    public void The_file_starts_with_the_PNG_signature_and_an_IHDR()
    {
        var png = PngImage.Encode(new byte[2 * 3 * 4], 2, 3);

        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(2, ReadBigEndian(png, 16));
        Assert.Equal(3, ReadBigEndian(png, 20));
        Assert.Equal(8, png[24]);   // bits per channel
        Assert.Equal(6, png[25]);   // RGBA
    }

    /// <summary>Decompresses IDAT and checks the pixels survive the round trip.</summary>
    [Fact]
    public void The_pixels_come_back_out_unchanged()
    {
        const int width = 3, height = 2;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 7);
        }

        var png = PngImage.Encode(pixels, width, height);
        var idat = FindChunk(png, "IDAT");

        using var input = new MemoryStream(idat);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var scanlines = raw.ToArray();

        Assert.Equal(height * ((width * 4) + 1), scanlines.Length);
        for (var y = 0; y < height; y++)
        {
            var offset = y * ((width * 4) + 1);
            Assert.Equal(0, scanlines[offset]);   // filter: none
            for (var x = 0; x < width * 4; x++)
            {
                Assert.Equal(pixels[(y * width * 4) + x], scanlines[offset + 1 + x]);
            }
        }
    }

    [Fact]
    public void It_refuses_a_buffer_that_is_too_small_for_the_size_given()
    {
        Assert.Throws<ArgumentException>(() => PngImage.Encode(new byte[4], 8, 8));
    }

    [Fact]
    public void Decode_reads_back_what_Encode_wrote()
    {
        const int width = 5, height = 4;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 13);
        }

        var (rgba, w, h) = PngImage.Decode(PngImage.Encode(pixels, width, height));

        Assert.Equal((width, height), (w, h));
        Assert.Equal(pixels, rgba);
    }

    [Fact]
    public void Decode_refuses_what_is_not_a_PNG()
    {
        Assert.Throws<InvalidDataException>(() => PngImage.Decode(new byte[32]));
    }

    private static int ReadBigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static byte[] FindChunk(byte[] png, string type)
    {
        var position = 8;
        while (position + 12 <= png.Length)
        {
            var length = ReadBigEndian(png, position);
            var name = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            if (name == type)
            {
                return png[(position + 8)..(position + 8 + length)];
            }
            position += 12 + length;
        }
        throw new InvalidOperationException($"El PNG no tiene un bloque {type}.");
    }
}
