using System.Buffers.Binary;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Decodes the 3DS's ETC1 and ETC1A4 textures, which is where the cartridge keeps its big artwork.
/// </summary>
/// <remarks>
/// <para>
/// §28 got away without this: the Pokémon icons are plain RGBA5551, so nothing of what pk3DS had
/// cut was needed. The large sheets are not — all 1225 images of 128×128 or more in the RomFS are
/// <c>Etc1A4</c> — so reading any of them means decoding the format.
/// </para>
/// <para>
/// ETC1 is a published Khronos format, so the maths here is not guesswork: each 4×4 block carries
/// two base colours, two brightness tables and a two-bit index per pixel. What is <b>not</b>
/// published is how the 3DS arranges the blocks in memory, and that part was pinned by decoding and
/// looking: a wrong tile order does not produce subtly wrong colours, it produces a visibly
/// scrambled image.
/// </para>
/// </remarks>
public static class Etc1Texture
{
    /// <summary>
    /// Brightness modifiers, indexed by the block's table and then by the pixel's own two bits.
    /// </summary>
    private static readonly int[][] Modifiers =
    [
        [2, 8, -2, -8],
        [5, 17, -5, -17],
        [9, 29, -9, -29],
        [13, 42, -13, -42],
        [18, 60, -18, -60],
        [24, 80, -24, -80],
        [33, 106, -33, -106],
        [47, 183, -47, -183]
    ];

    /// <param name="withAlpha">
    /// ETC1A4, which puts eight bytes of four-bit alpha in front of every colour block.
    /// </param>
    public static byte[] Decode(ReadOnlySpan<byte> data, int width, int height, bool withAlpha)
    {
        var output = new byte[width * height * 4];
        var blockBytes = withAlpha ? 16 : 8;
        var offset = 0;

        // Tiles of 8×8 in raster order, and inside each one four 4×4 blocks left to right, top to
        // bottom. Both halves of that were settled by looking at the result.
        for (var tileY = 0; tileY < height; tileY += 8)
        {
            for (var tileX = 0; tileX < width; tileX += 8)
            {
                for (var block = 0; block < 4; block++)
                {
                    if (offset + blockBytes > data.Length)
                    {
                        return output;
                    }

                    var alpha = withAlpha ? BitConverter.ToUInt64(data[offset..]) : ulong.MaxValue;
                    var colour = BitConverter.ToUInt64(data[(offset + (withAlpha ? 8 : 0))..]);
                    offset += blockBytes;

                    Block(colour, alpha, output, width, height,
                        tileX + ((block & 1) * 4), tileY + ((block >> 1) * 4));
                }
            }
        }

        return output;
    }

    private static void Block(ulong stored, ulong alpha, byte[] output, int width, int height,
        int originX, int originY)
    {
        // Read straight as little-endian, with no byte swap. Reversing it first is the obvious
        // reading of a spec that numbers its bits big-endian, and it is wrong here: it produced an
        // image with the right silhouette and the right alpha and pure noise for colour — which is
        // exactly the shape of an error that a checksum or a size check would never catch.
        var block = stored;

        var differential = (block & (1UL << 33)) != 0;
        var flipped = (block & (1UL << 32)) != 0;

        int r1, g1, b1, r2, g2, b2;

        if (differential)
        {
            var r = (int)((block >> 59) & 0x1F);
            var g = (int)((block >> 51) & 0x1F);
            var b = (int)((block >> 43) & 0x1F);

            r1 = Five(r);
            g1 = Five(g);
            b1 = Five(b);
            r2 = Five(r + Signed3((int)((block >> 56) & 7)));
            g2 = Five(g + Signed3((int)((block >> 48) & 7)));
            b2 = Five(b + Signed3((int)((block >> 40) & 7)));
        }
        else
        {
            r1 = Four((int)((block >> 60) & 0xF));
            g1 = Four((int)((block >> 52) & 0xF));
            b1 = Four((int)((block >> 44) & 0xF));
            r2 = Four((int)((block >> 56) & 0xF));
            g2 = Four((int)((block >> 48) & 0xF));
            b2 = Four((int)((block >> 40) & 0xF));
        }

        var table1 = Modifiers[(block >> 37) & 7];
        var table2 = Modifiers[(block >> 34) & 7];
        var indices = (uint)block;

        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < 4; y++)
            {
                var pixelX = originX + x;
                var pixelY = originY + y;

                if (pixelX >= width || pixelY >= height)
                {
                    continue;
                }

                // Pixel n of the block is column-major, and its two bits live sixteen apart.
                var n = (x * 4) + y;
                var index = (int)(((indices >> (n + 16)) & 1) << 1 | ((indices >> n) & 1));

                // Which half of the block a pixel belongs to: split across, or split down.
                var second = flipped ? y >= 2 : x >= 2;
                var modifier = (second ? table2 : table1)[index];

                var at = ((pixelY * width) + pixelX) * 4;
                output[at + 0] = Clamp((second ? r2 : r1) + modifier);
                output[at + 1] = Clamp((second ? g2 : g1) + modifier);
                output[at + 2] = Clamp((second ? b2 : b1) + modifier);

                var nibble = (int)((alpha >> (n * 4)) & 0xF);
                output[at + 3] = (byte)(nibble * 17);
            }
        }
    }

    /// <summary>Four bits stretched to eight by repeating them, as the format specifies.</summary>
    private static int Four(int value) => (value << 4) | value;

    private static int Five(int value)
    {
        var clamped = value & 0x1F;
        return (clamped << 3) | (clamped >> 2);
    }

    /// <summary>A three-bit two's-complement delta, so 5..7 mean -3..-1.</summary>
    private static int Signed3(int value) => value >= 4 ? value - 8 : value;

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
}
