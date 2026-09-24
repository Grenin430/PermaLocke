using System.Text;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Sprites;

/// <summary>Which of the three a move is: the icons the game draws next to a move.</summary>
public enum MoveCategoryIcon
{
    Status,
    Physical,
    Special
}

/// <summary>
/// Reads the three official move-category icons — the grey one, the red-orange burst and the blue rings — out of the
/// player's own cartridge (§144).
/// </summary>
/// <remarks>
/// <para>
/// They live in the UI layout <c>a/0/6/6</c>, the one that names <c>waza_icon_all.bflim</c>: a single ALYT whose
/// images include one 64×64 RGBA8888 sheet with the three icons stacked, 41×18 each, one row of empty pixels between
/// them. Found by listing every image name of every layout in the RomFS and dumping the candidates to look at.
/// </para>
/// <para>
/// Which band is which is <b>read from the colours, not assumed from the order</b>: the Z-Crystals taught that an
/// ALYT's names and its data do not keep the same order (§61). Grey is status, the red-orange one is physical and the
/// blue one special; a sheet that does not have exactly one of each is refused rather than guessed.
/// </para>
/// <para>
/// Nintendo's, like every sprite: PermaLocke ships none, each player extracts them from the ROM they own.
/// </para>
/// </remarks>
public static class MoveCategoryIconReader
{
    public const string LayoutGarcPath = "a/0/6/6";

    /// <summary>The three icons, by category, from a ROM.</summary>
    public static IReadOnlyDictionary<MoveCategoryIcon, PokemonIcon> Open(string romPath, string scratchDirectory)
    {
        var reader = new RomFsReader(romPath);
        Directory.CreateDirectory(scratchDirectory);
        var extracted = Path.Combine(scratchDirectory, "categorias.garc");

        if (!File.Exists(extracted) && !reader.ExtractTo(LayoutGarcPath, extracted))
        {
            throw new InvalidDataException($"La ROM no contiene {LayoutGarcPath}.");
        }

        var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));

        foreach (var texture in AlytCarver.Carve(Decompress(garc.GetFile(0))))
        {
            if (Split(texture) is { } icons)
            {
                return icons;
            }
        }

        throw new InvalidDataException($"{LayoutGarcPath} no trae la lámina de los tres iconos de categoría.");
    }

    /// <summary>
    /// The three icons of a sheet, or null when this texture is not the sheet: it has to hold exactly three opaque
    /// bands of the same size, and one of them has to be grey, one red and one blue.
    /// </summary>
    public static IReadOnlyDictionary<MoveCategoryIcon, PokemonIcon>? Split(BflimTexture texture) =>
        Split(texture.Width, texture.Height, texture.Pixels);

    /// <summary>The same, from bare RGBA8888 pixels, which is what makes it testable without a ROM.</summary>
    public static IReadOnlyDictionary<MoveCategoryIcon, PokemonIcon>? Split(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != width * height * 4)
        {
            return null;
        }

        var texture = new Sheet(width, height, pixels);
        var bands = Bands(texture);

        if (bands.Count != 3 || bands.Any(b => b.Height != bands[0].Height || b.Width != bands[0].Width)
            || bands[0].Width < 16)
        {
            return null;
        }

        var icons = new Dictionary<MoveCategoryIcon, PokemonIcon>();

        foreach (var band in bands)
        {
            if (Classify(texture, band) is not { } category || icons.ContainsKey(category))
            {
                return null;
            }

            icons[category] = Crop(texture, band, (int)category);
        }

        return icons;
    }

    private sealed record Band(int Left, int Top, int Width, int Height);

    private sealed record Sheet(int Width, int Height, byte[] Pixels);

    /// <summary>Runs of rows with something opaque in them, each with the columns it spans.</summary>
    private static List<Band> Bands(Sheet texture)
    {
        var bands = new List<Band>();
        var start = -1;
        int left = int.MaxValue, right = -1;

        for (var y = 0; y <= texture.Height; y++)
        {
            var (rowLeft, rowRight) = y < texture.Height ? Span(texture, y) : (-1, -1);

            if (rowRight >= 0)
            {
                if (start < 0)
                {
                    start = y;
                }

                left = Math.Min(left, rowLeft);
                right = Math.Max(right, rowRight);
                continue;
            }

            if (start >= 0)
            {
                bands.Add(new Band(left, start, right - left + 1, y - start));
                start = -1;
                left = int.MaxValue;
                right = -1;
            }
        }

        return bands;
    }

    private static (int Left, int Right) Span(Sheet texture, int y)
    {
        int left = -1, right = -1;

        for (var x = 0; x < texture.Width; x++)
        {
            if (texture.Pixels[(((y * texture.Width) + x) * 4) + 3] != 0)
            {
                left = left < 0 ? x : left;
                right = x;
            }
        }

        return (left, right);
    }

    /// <summary>
    /// Grey, red or blue, by the average colour of the band: the burst and the rings are drawn over a background that
    /// fills most of the icon, so the average is that background's.
    /// </summary>
    private static MoveCategoryIcon? Classify(Sheet texture, Band band)
    {
        long r = 0, g = 0, b = 0, count = 0;

        for (var y = band.Top; y < band.Top + band.Height; y++)
        {
            for (var x = band.Left; x < band.Left + band.Width; x++)
            {
                var at = ((y * texture.Width) + x) * 4;

                if (texture.Pixels[at + 3] == 0)
                {
                    continue;
                }

                r += texture.Pixels[at];
                g += texture.Pixels[at + 1];
                b += texture.Pixels[at + 2];
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        var (red, green, blue) = (r / (double)count, g / (double)count, b / (double)count);
        var spread = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));

        return spread < 40 ? MoveCategoryIcon.Status
            : red > blue + 40 && red >= green ? MoveCategoryIcon.Physical
            : blue > red + 40 && blue >= green ? MoveCategoryIcon.Special
            : null;
    }

    private static PokemonIcon Crop(Sheet texture, Band band, int index)
    {
        var pixels = new byte[band.Width * band.Height * 4];

        for (var y = 0; y < band.Height; y++)
        {
            Array.Copy(texture.Pixels, (((band.Top + y) * texture.Width) + band.Left) * 4,
                pixels, y * band.Width * 4, band.Width * 4);
        }

        return new PokemonIcon(index, band.Width, band.Height, pixels);
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

/// <summary>
/// Every image an ALYT layout carries, carved by their footers.
/// </summary>
/// <remarks>
/// A BFLIM has no header — the pixels come first and a 0x28-byte footer last — so the way in is to find the footers.
/// Looking for the four letters <c>FLIM</c> is not enough: they turn up inside the pixels of other images. A real
/// footer also carries the byte order mark, an <c>imag</c> block, and a declared size equal to its pixels plus the
/// footer (§61). Images in a format PermaLocke does not decode are skipped.
/// </remarks>
public static class AlytCarver
{
    public static IEnumerable<BflimTexture> Carve(byte[] layout)
    {
        for (var at = 0; at + 0x28 <= layout.Length; at++)
        {
            if (layout[at] != 'F' || layout[at + 1] != 'L' || layout[at + 2] != 'I' || layout[at + 3] != 'M')
            {
                continue;
            }

            if (BitConverter.ToUInt16(layout, at + 4) != 0xFEFF
                || Encoding.ASCII.GetString(layout, at + 0x14, 4) != "imag")
            {
                continue;
            }

            var declared = (int)BitConverter.ToUInt32(layout, at + 0x0C);
            var pixels = (int)BitConverter.ToUInt32(layout, at + 0x24);

            if (pixels <= 0 || pixels > at || declared != pixels + 0x28)
            {
                continue;
            }

            BflimTexture? texture;

            try
            {
                texture = BflimTexture.Decode(layout.AsSpan(at - pixels, pixels + 0x28));
            }
            catch (Exception)
            {
                texture = null;
            }

            if (texture is not null)
            {
                yield return texture;
            }
        }
    }
}
