using System.Text;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The icon decoder, tested against BFLIMs built here byte by byte.
/// <para>
/// These fix the three things that were got wrong while working the format out against the real
/// cartridge: the footer offsets, the Morton order inside a tile, and the rotation flag.
/// </para>
/// </summary>
public class BflimTextureTests
{
    /// <summary>Builds a BFLIM footer exactly as Ultra Moon's icons carry it.</summary>
    private static byte[] Footer(int width, int height, BflimFormat format, byte swizzle, int dataSize)
    {
        var footer = new byte[0x28];
        Encoding.ASCII.GetBytes("FLIM").CopyTo(footer, 0);
        footer[4] = 0xFF;
        footer[5] = 0xFE;                                       // byte order mark
        BitConverter.GetBytes((ushort)0x14).CopyTo(footer, 6);  // header size
        BitConverter.GetBytes(dataSize + 0x28).CopyTo(footer, 0x0C);
        BitConverter.GetBytes((ushort)1).CopyTo(footer, 0x10);  // one data block
        Encoding.ASCII.GetBytes("imag").CopyTo(footer, 0x14);
        BitConverter.GetBytes(0x10).CopyTo(footer, 0x18);
        BitConverter.GetBytes((ushort)width).CopyTo(footer, 0x1C);
        BitConverter.GetBytes((ushort)height).CopyTo(footer, 0x1E);
        BitConverter.GetBytes((ushort)0x80).CopyTo(footer, 0x20);
        footer[0x22] = (byte)format;
        footer[0x23] = swizzle;
        BitConverter.GetBytes(dataSize).CopyTo(footer, 0x24);
        return footer;
    }

    private static ushort Rgba5551(int r, int g, int b, bool opaque) =>
        (ushort)((r << 11) | (g << 6) | (b << 1) | (opaque ? 1 : 0));

    /// <summary>One 8x8 tile: enough to pin the Morton order, which is where guessing hurts.</summary>
    private static byte[] SingleTile(Func<int, ushort> pixelAt, int width, int height,
        byte swizzle = 0)
    {
        var data = new byte[width * height * 2];
        for (var i = 0; i < width * height; i++)
        {
            BitConverter.GetBytes(pixelAt(i)).CopyTo(data, i * 2);
        }
        return [.. data, .. Footer(width, height, BflimFormat.Rgba5551, swizzle, data.Length)];
    }

    [Fact]
    public void The_footer_says_where_the_image_is_and_what_size()
    {
        var file = SingleTile(_ => Rgba5551(31, 0, 0, true), 8, 8);
        var texture = BflimTexture.Decode(file);

        Assert.Equal(8, texture.Width);
        Assert.Equal(8, texture.Height);
        Assert.Equal(BflimFormat.Rgba5551, texture.Format);
        Assert.Equal(255, texture.Pixels[0]);   // R
        Assert.Equal(0, texture.Pixels[1]);
        Assert.Equal(255, texture.Pixels[3]);   // opaque
    }

    /// <summary>
    /// Inside a tile the pixels are in Morton order: even bits of the index are X, odd bits Y.
    /// Data index 2 is therefore (0,1) and index 4 is (2,0), not (2,0) and (4,0).
    /// </summary>
    [Fact]
    public void Pixels_inside_a_tile_follow_Morton_order()
    {
        // Only data index 2 is opaque; every other pixel is transparent.
        var file = SingleTile(i => i == 2 ? Rgba5551(0, 31, 0, true) : (ushort)0, 8, 8);
        var texture = BflimTexture.Decode(file);

        var opaque = new List<(int x, int y)>();
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (texture.Pixels[(((y * 8) + x) * 4) + 3] != 0)
            {
                opaque.Add((x, y));
            }
        }

        Assert.Equal([(0, 1)], opaque);
    }

    [Fact]
    public void Tiles_are_walked_left_to_right_then_down()
    {
        // 16x8: two tiles side by side. The first pixel of the second tile is data index 64.
        var file = SingleTile(i => i == 64 ? Rgba5551(0, 0, 31, true) : (ushort)0, 16, 8);
        var texture = BflimTexture.Decode(file);

        Assert.Equal(255, texture.Pixels[(((0 * 16) + 8) * 4) + 3]);
        Assert.Equal(0, texture.Pixels[(((1 * 16) + 0) * 4) + 3]);
    }

    /// <summary>
    /// Swizzle 4 means the texture is stored with its axes swapped: an icon declared 16x8 sits
    /// in memory as 8 wide by 16 tall and has to be turned back, or every sprite comes out lying
    /// on its side. That is exactly what happened against the real cartridge.
    /// </summary>
    [Fact]
    public void A_rotated_texture_is_turned_back_to_the_declared_size()
    {
        // The footer declares 16x8 while the pixels sit in memory as 8 wide by 16 tall.
        // Data index 0 is stored (0,0), which after turning counter-clockwise lands at the
        // bottom-left corner of the 16x8 image.
        var data = new byte[8 * 16 * 2];
        BitConverter.GetBytes(Rgba5551(31, 31, 31, true)).CopyTo(data, 0);
        var file = (byte[])[.. data, .. Footer(16, 8, BflimFormat.Rgba5551, 4, data.Length)];
        var texture = BflimTexture.Decode(file);

        Assert.Equal(16, texture.Width);
        Assert.Equal(8, texture.Height);
        Assert.Equal(255, texture.Pixels[(((7 * 16) + 0) * 4) + 3]);
    }

    [Fact]
    public void A_file_without_the_FLIM_signature_is_rejected()
    {
        var bytes = new byte[0x40];
        Assert.Throws<InvalidDataException>(() => BflimTexture.Decode(bytes));
    }

    /// <summary>A format the cartridge does not use must fail loudly, not draw something wrong.</summary>
    /// <remarks>
    /// This used to be <c>Etc1A4</c>, which is now decoded: it turned out to be the format of every
    /// large image in the cartridge, the region map included. <c>HiLo8</c> takes its place because
    /// nothing has ever needed it, so it is still the honest example of refusing rather than
    /// guessing.
    /// </remarks>
    [Fact]
    public void An_unsupported_pixel_format_is_refused()
    {
        var data = new byte[64];
        var file = (byte[])[.. data, .. Footer(8, 8, BflimFormat.HiLo8, 0, data.Length)];
        Assert.Throws<NotSupportedException>(() => BflimTexture.Decode(file));
    }

    /// <summary>And ETC1A4 no longer is: it decodes to a full image.</summary>
    [Fact]
    public void The_compressed_format_of_the_big_artwork_now_decodes()
    {
        var data = new byte[16 * 4];
        var file = (byte[])[.. data, .. Footer(8, 8, BflimFormat.Etc1A4, 0, data.Length)];

        var texture = BflimTexture.Decode(file);

        Assert.Equal(8, texture.Width);
        Assert.Equal(8 * 8 * 4, texture.Pixels.Length);
    }
}
