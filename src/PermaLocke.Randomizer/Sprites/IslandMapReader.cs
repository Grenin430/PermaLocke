using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Sprites;

/// <param name="Name">The island's key: melemele, akala, ulaula, poni.</param>
public sealed record IslandMap(string Name, int Width, int Height, byte[] Pixels);

/// <summary>
/// The four island maps of Alola, cut out of the player's own cartridge.
/// </summary>
/// <remarks>
/// <para>
/// They live in <c>a/1/6/3</c> as 866 tiles of 128×64, and getting from those to four island maps
/// took four failed attempts, all recorded here so nobody repeats them:
/// </para>
/// <para>
/// <b>The width is not the same for every map.</b> The small area maps are four tiles wide, and
/// the island maps are <b>eight</b>. Assuming four everywhere produced maps sheared diagonally,
/// and no threshold fixes that.
/// </para>
/// <para>
/// <b>Where a map starts matters and the seams cannot find it.</b> A shift applied equally to
/// every row leaves tile <i>i</i> and <i>i+8</i> vertically adjacent, so the vertical seams stay
/// perfect while every row comes out rotated — the island splits and its halves touch both edges.
/// Measuring the phase from the sideways seams failed too: an island map is mostly ocean, and
/// ocean matches itself at any offset.
/// </para>
/// <para>
/// <b>What does work is specific to what is being looked for: a well-framed island has sea on all
/// four edges.</b> That cannot happen by accident — a shifted start puts land against the border —
/// and it is what pins both the start and the height. On ties the <em>largest</em> frame wins,
/// because any crop landing in open water scores just as well as the right one; with only the
/// sideways edges checked, Akala came out with its ranch cut off the bottom.
/// </para>
/// <para>
/// Finally the map is cropped to its land. The cartridge frames each island in a corner of a much
/// larger sea — that empty water is where the game draws the other islands as you zoom out — so
/// Poni filled less than half its sheet.
/// </para>
/// </remarks>
public sealed class IslandMapReader
{
    /// <summary>The map tile container. The randomizer never touches it.</summary>
    public const string MapGarcPath = "a/1/6/3";

    /// <summary>Eight tiles across, measured: 5,92 of seam against 25 for every other width.</summary>
    public const int Columns = 8;

    /// <summary>
    /// Roughly where each island's tiles begin, from segmenting the container. Approximate on
    /// purpose: the framing search below decides the exact tile, and it is the one that can tell.
    /// </summary>
    public static readonly (int Around, string Name)[] Islands =
    [
        (366, "melemele"), (494, "akala"), (614, "ulaula"), (710, "poni")
    ];

    private readonly List<BflimTexture> _tiles = [];

    private IslandMapReader(GARC.MemGARC garc)
    {
        // Si una lamina no se deja leer, se para. Saltarsela seria peor que fallar: todas las
        // siguientes se correrian un puesto y los mapas saldrian en diagonal sin que nada avisara.
        for (var i = 0; i < garc.FileCount; i++)
        {
            _tiles.Add(BflimTexture.Decode(Decompress(garc.GetFile(i))));
        }
    }

    public int TileCount => _tiles.Count;

    public static IslandMapReader Open(string romPath, string scratchDirectory)
    {
        Directory.CreateDirectory(scratchDirectory);
        var extracted = Path.Combine(scratchDirectory, "island-maps.garc");

        if (!File.Exists(extracted) && !new RomFsReader(romPath).ExtractTo(MapGarcPath, extracted))
        {
            throw new InvalidDataException(
                $"La ROM no contiene {MapGarcPath}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
        }

        return new IslandMapReader(new GARC.MemGARC(File.ReadAllBytes(extracted)));
    }

    /// <summary>Reads one island, framed and cropped.</summary>
    public IslandMap Read(int around, string name, int margin = 24)
    {
        var tileWidth = _tiles[0].Width;
        var tileHeight = _tiles[0].Height;

        var from = Math.Max(0, around - (Columns * 4));
        var available = Math.Min(_tiles.Count - from, Columns * 24);

        var bestStart = from;
        var bestHeight = 12;
        var bestScore = double.MaxValue;

        // De mayor a menor: cuando varios marcos empatan a cero gana el mas grande, que es el que
        // no deja fuera ningun trozo de isla.
        for (var height = 17; height >= 10; height--)
        {
            for (var start = from; start + (Columns * height) <= from + available; start++)
            {
                double edge = 0;

                for (var row = 0; row < height; row++)
                {
                    edge += EdgeLand(_tiles[start + (row * Columns)], vertical: true, first: true);
                    edge += EdgeLand(_tiles[start + (row * Columns) + Columns - 1], vertical: true, first: false);
                }

                for (var c = 0; c < Columns; c++)
                {
                    edge += EdgeLand(_tiles[start + c], vertical: false, first: true);
                    edge += EdgeLand(_tiles[start + ((height - 1) * Columns) + c], vertical: false, first: false);
                }

                // Por lamina de borde, para que un marco mas alto no gane solo por tener mas bordes.
                var score = edge / ((height * 2) + (Columns * 2));

                if (score < bestScore)
                {
                    bestScore = score;
                    bestStart = start;
                    bestHeight = height;
                }
            }
        }

        var width = Columns * tileWidth;
        var full = new byte[width * bestHeight * tileHeight * 4];

        for (var row = 0; row < bestHeight; row++)
        {
            for (var c = 0; c < Columns; c++)
            {
                var tile = _tiles[bestStart + (row * Columns) + c];

                for (var y = 0; y < tile.Height; y++)
                {
                    Array.Copy(tile.Pixels, y * tile.Width * 4, full,
                        ((((row * tileHeight) + y) * width) + (c * tileWidth)) * 4, tile.Width * 4);
                }
            }
        }

        var pixels = CropToLand(full, width, bestHeight * tileHeight, margin,
            out var croppedWidth, out var croppedHeight);

        return new IslandMap(name, croppedWidth, croppedHeight, pixels);
    }

    /// <summary>All four, ready to be written as PNG.</summary>
    public IEnumerable<IslandMap> ReadAll()
    {
        foreach (var (around, name) in Islands)
        {
            yield return Read(around, name);
        }
    }

    /// <summary>Whether a pixel is the map's open sea, which is a strong flat blue.</summary>
    private static bool IsSea(byte r, byte g, byte b) => b > 120 && b > r + 60 && b > g + 40;

    private static double EdgeLand(BflimTexture tile, bool vertical, bool first)
    {
        var land = 0;
        var steps = vertical ? tile.Height : tile.Width;

        for (var i = 0; i < steps; i++)
        {
            var at = vertical
                ? ((i * tile.Width) + (first ? 0 : tile.Width - 1)) * 4
                : ((((first ? 0 : tile.Height - 1) * tile.Width) + i) * 4);

            if (!IsSea(tile.Pixels[at], tile.Pixels[at + 1], tile.Pixels[at + 2]))
            {
                land++;
            }
        }

        return (double)land / steps;
    }

    private static byte[] CropToLand(byte[] pixels, int width, int height, int margin,
        out int outWidth, out int outHeight)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;

                if (IsSea(pixels[at], pixels[at + 1], pixels[at + 2]))
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0)
        {
            outWidth = width;
            outHeight = height;
            return pixels;
        }

        minX = Math.Max(0, minX - margin);
        minY = Math.Max(0, minY - margin);
        maxX = Math.Min(width - 1, maxX + margin);
        maxY = Math.Min(height - 1, maxY + margin);

        outWidth = maxX - minX + 1;
        outHeight = maxY - minY + 1;
        var output = new byte[outWidth * outHeight * 4];

        for (var y = 0; y < outHeight; y++)
        {
            Array.Copy(pixels, (((minY + y) * width) + minX) * 4,
                output, y * outWidth * 4, outWidth * 4);
        }

        return output;
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
