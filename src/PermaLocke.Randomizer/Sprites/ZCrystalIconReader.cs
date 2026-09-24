using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Reads the eighteen type Z-Crystals out of the player's own cartridge.
/// </summary>
/// <remarks>
/// <para>
/// They are <b>not</b> in the item icon container: <c>a/0/6/1</c> stops at 769 and the crystals are
/// items 807-824 (§34). Where they do live is inside two UI layouts, <c>a/1/5/5</c> and
/// <c>a/1/4/2</c>, as BFLIM images embedded in an ALYT — and both layouts name them
/// <c>item_807.bflim</c> … <c>item_824.bflim</c>, which is how we know what they are at all.
/// </para>
/// <para>
/// The catch is that an ALYT's <b>name table and its data are in different orders</b>, and nothing
/// in the header maps one to the other. Measured, not assumed: the first image in the data is a
/// golden crystal while the first name is <c>item_807</c>, the Normalium, which is pale. So the
/// images are carved in data order and paired with their item by <see cref="ZCrystalIndex"/>.
/// </para>
/// <para>
/// Sprites are Nintendo's. PermaLocke ships none: every player extracts them from the ROM they
/// already own, exactly as with the Pokémon and item icons.
/// </para>
/// </remarks>
public sealed class ZCrystalIconReader
{
    /// <summary>The layout that carries them. <c>a/1/4/2</c> has the same eighteen, same order.</summary>
    public const string LayoutGarcPath = "a/1/5/5";

    /// <summary>First and last type crystal, as the cartridge numbers its items.</summary>
    public const int FirstItemId = 807;

    public const int LastItemId = 824;

    /// <summary>
    /// How many opaque pixels a crystal has. All eighteen share one shape and differ only in
    /// colour, so this tells a crystal from the other 32x32 images in the same layout.
    /// </summary>
    private const int CrystalPixels = 168;

    private readonly IReadOnlyList<BflimTexture> _crystals;

    private ZCrystalIconReader(IReadOnlyList<BflimTexture> crystals) => _crystals = crystals;

    /// <summary>How many were found. Eighteen on Ultra Moon; anything else means the carve broke.</summary>
    public int Count => _crystals.Count;

    public static ZCrystalIconReader Open(string romPath, string scratchDirectory)
    {
        var reader = new RomFsReader(romPath);
        Directory.CreateDirectory(scratchDirectory);
        var extracted = Path.Combine(scratchDirectory, "zcrystals.garc");

        if (!File.Exists(extracted) && !reader.ExtractTo(LayoutGarcPath, extracted))
        {
            throw new InvalidDataException(
                $"La ROM no contiene {LayoutGarcPath}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
        }

        var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));

        if (garc.FileCount == 0)
        {
            throw new InvalidDataException($"{LayoutGarcPath} está vacío.");
        }

        return new ZCrystalIconReader(Carve(Decompress(garc.GetFile(0))));
    }

    /// <summary>The icon of one Z-Crystal, by <b>item id</b>.</summary>
    public PokemonIcon Read(int itemId)
    {
        if (!ZCrystalIndex.TryGet(itemId, out var index) || index >= _crystals.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId),
                $"El objeto {itemId} no es un cristal Z de tipo, o el contenedor solo trae {_crystals.Count}.");
        }

        var texture = _crystals[index];
        return new PokemonIcon(index, texture.Width, texture.Height, texture.Pixels);
    }

    /// <summary>
    /// Every image of the layout that is shaped like a crystal. The carving itself —footers, not the letters
    /// <c>FLIM</c>— is <see cref="AlytCarver"/>, shared with the move-category icons (§144).
    /// </summary>
    private static List<BflimTexture> Carve(byte[] layout) => [.. AlytCarver.Carve(layout).Where(IsACrystal)];

    private static bool IsACrystal(BflimTexture texture)
    {
        if (texture is not { Width: 32, Height: 32, Format: BflimFormat.Rgba5551 })
        {
            return false;
        }

        var opaque = 0;
        for (var pixel = 0; pixel < texture.Width * texture.Height; pixel++)
        {
            if (texture.Pixels[(pixel * 4) + 3] != 0)
            {
                opaque++;
            }
        }

        return opaque == CrystalPixels;
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
}
