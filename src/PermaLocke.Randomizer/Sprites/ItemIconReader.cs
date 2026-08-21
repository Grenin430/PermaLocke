using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Reads the item icons out of the player's own cartridge.
/// </summary>
/// <remarks>
/// <para>
/// They live in the GARC at <c>a/0/6/1</c>, next door to the Pokémon icons of <see cref="PokemonIconReader"/>
/// and in the same shape: LZ11-compressed BFLIM, 32x32, RGBA5551. The index is simply
/// <b>item id minus one</b>, which the sixteen Poké Balls prove on sight — they are the first
/// sixteen icons, in the order the game numbers them, from the Master Ball to the Cherish Ball.
/// </para>
/// <para>
/// The container stops at 769 icons, the last of which is the "?" the game shows for an item it
/// has no picture for. Items numbered above that — the Z-Crystals among them — are not in here.
/// </para>
/// <para>
/// Sprites are Nintendo's. PermaLocke ships none: every player extracts them from the ROM they
/// already own, exactly as with the Pokémon icons.
/// </para>
/// </remarks>
public sealed class ItemIconReader
{
    /// <summary>The item icon GARC. Not in <see cref="GameFiles.All"/>: the randomizer never touches it.</summary>
    public const string IconGarcPath = "a/0/6/1";

    /// <summary>Item id of the ordinary Poké Ball, which is the one a trade animation wants.</summary>
    public const int PokeBallItemId = 4;

    /// <summary>Item id of the last ball, so the whole run of balls can be pulled in one go.</summary>
    public const int LastBallItemId = 16;

    private readonly GARC.MemGARC _garc;

    private ItemIconReader(GARC.MemGARC garc) => _garc = garc;

    /// <summary>How many icons the cartridge carries. 769 on Ultra Moon.</summary>
    public int Count => _garc.FileCount;

    public static ItemIconReader Open(string romPath, string scratchDirectory)
    {
        var reader = new RomFsReader(romPath);
        Directory.CreateDirectory(scratchDirectory);
        var extracted = Path.Combine(scratchDirectory, "item-icons.garc");

        if (!File.Exists(extracted) && !reader.ExtractTo(IconGarcPath, extracted))
        {
            throw new InvalidDataException(
                $"La ROM no contiene {IconGarcPath}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
        }

        return new ItemIconReader(new GARC.MemGARC(File.ReadAllBytes(extracted)));
    }

    /// <summary>True when this item has a picture of its own in the cartridge.</summary>
    public bool Has(int itemId) => itemId > 0 && itemId - 1 < Count;

    /// <summary>Decodes the icon of one item, cropped to what is actually drawn.</summary>
    public PokemonIcon Read(int itemId)
    {
        if (!Has(itemId))
        {
            throw new ArgumentOutOfRangeException(nameof(itemId),
                $"El objeto {itemId} no tiene icono: el contenedor llega a {Count}.");
        }

        var raw = _garc.GetFile(itemId - 1);
        return Crop(itemId, BflimTexture.Decode(Decompress(raw)));
    }

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

    /// <summary>Trims the transparent margin, so a layout that centres things centres the icon.</summary>
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
