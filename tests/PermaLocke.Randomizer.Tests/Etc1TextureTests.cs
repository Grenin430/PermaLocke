using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The ETC1 decoder, which is what let PermaLocke read the cartridge's big artwork.
/// </summary>
/// <remarks>
/// <para>
/// Honest about what these are: the decoder was <b>verified by looking</b> — decoding the region
/// map out of the cartridge and seeing Hau'oli City — because for an image the eye is the only
/// oracle that catches the failures that matter. A wrong byte order gave a picture with the right
/// silhouette, the right transparency and colour that was pure noise; no assertion on sizes or
/// checksums would have said a word about it.
/// </para>
/// <para>
/// So these are regression guards, not a proof of the format: they pin the behaviour that was
/// confirmed by eye, so that changing the decoder has to be deliberate.
/// </para>
/// </remarks>
public sealed class Etc1TextureTests
{
    /// <summary>Builds one 4×4 colour block in individual mode with both halves the same flat colour.</summary>
    /// <param name="alpha">The four-bit alpha to give all sixteen pixels.</param>
    private static byte[] FlatBlock(int r, int g, int b, int alpha)
    {
        // Individual mode: two four-bit base colours, no difference bit, table 0, and every pixel
        // index left at zero, which selects the smallest positive brightness step.
        ulong block = 0;
        block |= (ulong)(r & 0xF) << 60;
        block |= (ulong)(r & 0xF) << 56;
        block |= (ulong)(g & 0xF) << 52;
        block |= (ulong)(g & 0xF) << 48;
        block |= (ulong)(b & 0xF) << 44;
        block |= (ulong)(b & 0xF) << 40;

        var bytes = new byte[16];
        var nibbles = 0UL;

        for (var i = 0; i < 16; i++)
        {
            nibbles |= (ulong)(alpha & 0xF) << (i * 4);
        }

        BitConverter.TryWriteBytes(bytes.AsSpan(0), nibbles);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), block);
        return bytes;
    }

    /// <summary>A flat block decodes to a flat colour: base value plus the smallest step.</summary>
    [Fact]
    public void A_flat_block_decodes_to_one_colour()
    {
        // 8x8 has four blocks; only the first is filled, so read the top-left pixel.
        var data = new byte[16 * 4];
        FlatBlock(0xA, 0x4, 0x2, 0xF).CopyTo(data, 0);

        var pixels = Etc1Texture.Decode(data, 8, 8, withAlpha: true);

        // Four bits stretched to eight by repeating them, then +2 from table 0 at index 0.
        Assert.Equal((byte)(0xAA + 2), pixels[0]);
        Assert.Equal((byte)(0x44 + 2), pixels[1]);
        Assert.Equal((byte)(0x22 + 2), pixels[2]);
        Assert.Equal(255, pixels[3]);
    }

    /// <summary>Four-bit alpha reaches the output stretched over the full range, not scaled by 16.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 136)]
    [InlineData(15, 255)]
    public void Alpha_nibbles_stretch_to_the_whole_range(int nibble, int expected)
    {
        var data = new byte[16 * 4];
        FlatBlock(0x8, 0x8, 0x8, nibble).CopyTo(data, 0);

        var pixels = Etc1Texture.Decode(data, 8, 8, withAlpha: true);

        Assert.Equal((byte)expected, pixels[3]);
    }

    /// <summary>Without alpha every pixel is opaque and blocks are half the size.</summary>
    [Fact]
    public void Without_alpha_the_blocks_are_eight_bytes_and_everything_is_opaque()
    {
        var withAlpha = FlatBlock(0x9, 0x9, 0x9, 0);
        var data = new byte[8 * 4];
        withAlpha.AsSpan(8, 8).CopyTo(data);

        var pixels = Etc1Texture.Decode(data, 8, 8, withAlpha: false);

        Assert.Equal((byte)(0x99 + 2), pixels[0]);
        Assert.Equal(255, pixels[3]);
    }

    /// <summary>Brightness steps that overshoot are clamped rather than wrapped.</summary>
    /// <remarks>
    /// Wrapping is the failure that looks like static: a nearly-white pixel comes back nearly
    /// black, one pixel at a time, and the image reads as noise on top of a correct shape.
    /// </remarks>
    [Fact]
    public void An_overshooting_step_clamps_instead_of_wrapping()
    {
        var data = new byte[16 * 4];
        FlatBlock(0xF, 0xF, 0xF, 0xF).CopyTo(data, 0);

        var pixels = Etc1Texture.Decode(data, 8, 8, withAlpha: true);

        Assert.Equal(255, pixels[0]);
    }

    /// <summary>Short data stops rather than reading past the end.</summary>
    /// <remarks>
    /// The sweep that found the map fed this decoder every subfile of 235 archives, most of which
    /// are not textures at all. Falling over on one of them would have hidden the map.
    /// </remarks>
    [Fact]
    public void Data_that_runs_out_stops_instead_of_throwing()
    {
        var pixels = Etc1Texture.Decode(new byte[20], 64, 64, withAlpha: true);

        Assert.Equal(64 * 64 * 4, pixels.Length);
    }
}
