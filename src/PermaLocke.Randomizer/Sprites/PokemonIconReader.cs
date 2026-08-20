using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>One decoded box icon: straight RGBA8888, already cropped to what is drawn.</summary>
public sealed record PokemonIcon(int Index, int Width, int Height, byte[] Pixels);

/// <summary>
/// Reads the Pokémon box icons out of the player's own cartridge.
/// <para>
/// They live in the GARC at <c>a/0/6/2</c>: 1154 subfiles, each LZ11-compressed, each a BFLIM
/// storing 32x64 pixels in RGBA5551 with a rotation flag that turns it into the 64x32 icon the
/// game draws. No ETC1 involved, so none of the pk3DS image code that was dropped in the trim
/// (see ARCHITECTURE.md §19) is needed.
/// </para>
/// <para>
/// Sprites are Nintendo's. PermaLocke ships none: every player extracts them from the ROM they
/// already own, exactly as with the randomizer.
/// </para>
/// </summary>
public sealed class PokemonIconReader
{
    /// <summary>The icon GARC. Not in <see cref="GameFiles.All"/>: the randomizer never touches it.</summary>
    public const string IconGarcPath = "a/0/6/2";

    private readonly GARC.MemGARC _garc;

    private PokemonIconReader(GARC.MemGARC garc) => _garc = garc;

    /// <summary>How many icons the cartridge carries. 1154 on Ultra Moon, index 0 being the egg.</summary>
    public int Count => _garc.FileCount;

    /// <summary>Opens the icon container straight from the cartridge, without extracting the RomFS.</summary>
    public static PokemonIconReader Open(string romPath, string scratchDirectory)
    {
        var reader = new RomFsReader(romPath);
        Directory.CreateDirectory(scratchDirectory);
        var extracted = Path.Combine(scratchDirectory, "pokemon-icons.garc");

        if (!File.Exists(extracted) && !reader.ExtractTo(IconGarcPath, extracted))
        {
            throw new InvalidDataException(
                $"La ROM no contiene {IconGarcPath}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
        }

        return new PokemonIconReader(new GARC.MemGARC(File.ReadAllBytes(extracted)));
    }

    /// <summary>Decodes one icon by its position in the container.</summary>
    /// <param name="crop">
    /// Trim the transparent margin. The stored canvas is 64x32 with the sprite floating in it,
    /// which looks wrong in a layout that centres things.
    /// </param>
    public PokemonIcon Read(int index, bool crop = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var raw = _garc.GetFile(index);
        var texture = BflimTexture.Decode(Decompress(raw));

        return crop
            ? Crop(index, texture)
            : new PokemonIcon(index, texture.Width, texture.Height, texture.Pixels);
    }

    /// <summary>Subfiles are LZ11 unless they already start with something else.</summary>
    private static byte[] Decompress(byte[] data)
    {
        if (data.Length == 0 || data[0] != 0x11)
        {
            return data;
        }

        using var output = new MemoryStream();
        LZSS.Decompress(new MemoryStream(data), data.Length, output);
        return output.ToArray();
    }

    private static PokemonIcon Crop(int index, BflimTexture texture)
    {
        int minX = texture.Width, minY = texture.Height, maxX = -1, maxY = -1;

        for (var y = 0; y < texture.Height; y++)
        for (var x = 0; x < texture.Width; x++)
        {
            if (texture.Pixels[(((y * texture.Width) + x) * 4) + 3] == 0)
            {
                continue;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (maxX < 0)
        {
            // Fully transparent. Returning the empty canvas is honest; inventing a box is not.
            return new PokemonIcon(index, texture.Width, texture.Height, texture.Pixels);
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(texture.Pixels, (((minY + y) * texture.Width) + minX) * 4,
                pixels, y * width * 4, width * 4);
        }

        return new PokemonIcon(index, width, height, pixels);
    }
}
