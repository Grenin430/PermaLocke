using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>One of the four moves printed on a card, as the installed world describes it.</summary>
/// <param name="Type">Type id, for the colour of its energy dot; -1 when the world could not say.</param>
/// <param name="Power">Base power, 0 for a status move or one without a fixed power.</param>
public sealed record TcgMove(string Name, int Type, int Power, int Accuracy, int PP, string Category);

/// <summary>
/// A Pokémon of the player's save as a trading card of the album (§186): everything printed on it, already read and
/// resolved, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Every field comes from the save or from the installed world. The card prints no damage number the game does not
/// have, no weakness and no retreat cost: a TCG card has them, a Pokémon of Ultra Luna does not, and making them up
/// would be a card that lies (rule 3).
/// </para>
/// <para>
/// Data only, so the drawing (<see cref="TcgCardArt"/>) decides nothing and the view model decides nothing about
/// pixels.
/// </para>
/// </remarks>
/// <param name="Name">Its nickname, or its species name when it has none.</param>
/// <param name="Stage">«BÁSICO», «FASE 1» or «FASE 2», from where it sits in its evolution family.</param>
/// <param name="Hp">Its maximum PS as the game computes them.</param>
/// <param name="Types">One or two type ids.</param>
/// <param name="Rarity">The gacha tier it is, 0 to 4, or -1 when it has none.</param>
/// <param name="FromGacha">It came out of the gacha, so the rarity is the tier it was pulled from.</param>
/// <param name="Fallen">The run counts it as dead, by PID: the card is crumpled and half burnt.</param>
/// <param name="Sprite">Its icon from the player's cartridge, shiny if it is; null when there is none.</param>
/// <param name="NatureUp">Stat index the nature raises, or -1 for a neutral one.</param>
/// <param name="Seed">Its PID: decides where the burn starts and how it creases, so a card is always burnt the same.</param>
public sealed record TcgCard(
    string Name,
    string SpeciesName,
    int Species,
    string Stage,
    int Level,
    int Hp,
    IReadOnlyList<int> Types,
    IReadOnlyList<TcgMove> Moves,
    string Ability,
    string Nature,
    int NatureUp,
    int NatureDown,
    string Item,
    string MetLocation,
    int MetLevel,
    int Rarity,
    bool FromGacha,
    bool Shiny,
    bool Egg,
    bool Fallen,
    RoomSprite? Sprite,
    IReadOnlyList<int> Stats,
    IReadOnlyList<int> Ivs,
    IReadOnlyList<int> Evs,
    uint Seed)
{
    /// <summary>The first type, which paints the card; Normal when the world did not say.</summary>
    public int MainType => Types.Count > 0 && Types[0] >= 0 ? Types[0] : 0;

    /// <summary>Whether anything on the card moves: the foil of a shiny, the embers of a fallen one.</summary>
    public bool IsLive => Shiny && !Egg || Fallen;
}

/// <summary>Which of the two card designs: the full one of the 3×3 pages and the zoom, or the small one of 4×4.</summary>
public enum TcgLayout
{
    Full,
    Mini
}

/// <summary>
/// A small BGRA canvas of cells with transparency, where a card or a page of the album is painted before it is put
/// on screen.
/// </summary>
/// <remarks>
/// Transparency matters: a burnt card is missing where it burnt, and what shows through is the pocket behind it.
/// </remarks>
public sealed class CellCanvas
{
    public CellCanvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Bgra = new byte[Width * Height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Bgra { get; }

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public void Put(int x, int y, Color colour)
    {
        if (!Inside(x, y))
        {
            return;
        }

        var at = ((y * Width) + x) * 4;
        Bgra[at] = colour.B;
        Bgra[at + 1] = colour.G;
        Bgra[at + 2] = colour.R;
        Bgra[at + 3] = 255;
    }

    /// <summary>Makes a cell see-through again.</summary>
    public void Erase(int x, int y)
    {
        if (Inside(x, y))
        {
            Bgra[(((y * Width) + x) * 4) + 3] = 0;
        }
    }

    public bool IsSet(int x, int y) => Inside(x, y) && Bgra[(((y * Width) + x) * 4) + 3] > 0;

    public Color At(int x, int y)
    {
        if (!Inside(x, y))
        {
            return Colors.Transparent;
        }

        var at = ((y * Width) + x) * 4;
        return Color.FromArgb(Bgra[at + 3], Bgra[at + 2], Bgra[at + 1], Bgra[at]);
    }

    public void Rect(int x, int y, int width, int height, Color colour)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                Put(xx, yy, colour);
            }
        }
    }

    public void Clear() => Array.Clear(Bgra);

    public CellCanvas Clone()
    {
        var copy = new CellCanvas(Width, Height);
        Buffer.BlockCopy(Bgra, 0, copy.Bgra, 0, Bgra.Length);
        return copy;
    }

    /// <summary>Paints every solid cell of <paramref name="source"/> here, its corner at (<paramref name="left"/>, <paramref name="top"/>).</summary>
    public void Stamp(CellCanvas source, int left, int top)
    {
        for (var y = 0; y < source.Height; y++)
        {
            var ty = top + y;
            if (ty < 0 || ty >= Height)
            {
                continue;
            }

            for (var x = 0; x < source.Width; x++)
            {
                var tx = left + x;
                if (tx < 0 || tx >= Width)
                {
                    continue;
                }

                var from = ((y * source.Width) + x) * 4;
                if (source.Bgra[from + 3] == 0)
                {
                    continue;
                }

                var to = ((ty * Width) + tx) * 4;
                Bgra[to] = source.Bgra[from];
                Bgra[to + 1] = source.Bgra[from + 1];
                Bgra[to + 2] = source.Bgra[from + 2];
                Bgra[to + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Paints <paramref name="source"/> squeezed or stretched to <paramref name="width"/> columns, keeping whole cells:
    /// each column shown is one column of the source, none is blended. For a card or a page turning over.
    /// </summary>
    public void StampColumns(CellCanvas source, int left, int top, int width, bool mirror = false)
    {
        if (width <= 0)
        {
            return;
        }

        for (var x = 0; x < width; x++)
        {
            var sx = Math.Min(source.Width - 1, (int)((x + 0.5) * source.Width / width));
            if (mirror)
            {
                sx = source.Width - 1 - sx;
            }

            var tx = left + x;
            if (tx < 0 || tx >= Width)
            {
                continue;
            }

            for (var y = 0; y < source.Height; y++)
            {
                var ty = top + y;
                if (ty < 0 || ty >= Height)
                {
                    continue;
                }

                var from = ((y * source.Width) + sx) * 4;
                if (source.Bgra[from + 3] == 0)
                {
                    continue;
                }

                var to = ((ty * Width) + tx) * 4;
                Bgra[to] = source.Bgra[from];
                Bgra[to + 1] = source.Bgra[from + 1];
                Bgra[to + 2] = source.Bgra[from + 2];
                Bgra[to + 3] = 255;
            }
        }
    }

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>A flat mix of two colours: <paramref name="t"/> 0 is <paramref name="a"/>, 1 is <paramref name="b"/>.</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(a.R + ((b.R - a.R) * t)),
            (byte)Math.Round(a.G + ((b.G - a.G) * t)),
            (byte)Math.Round(a.B + ((b.B - a.B) * t)));
    }

    /// <summary>
    /// Ordered dithering on a 4×4 Bayer matrix: whether a cell is lit for a share <paramref name="level"/> from 0 to 1.
    /// The pixel-art way to show a half tone without a gradient.
    /// </summary>
    public static bool Dither(int x, int y, double level)
    {
        ReadOnlySpan<int> bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        return bayer[((y & 3) * 4) + (x & 3)] < level * 16;
    }

    /// <summary>A stable number in [0, 1) for a cell and a seed: the same card always creases and burns the same.</summary>
    public static double Hash(int x, int y, uint seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393) + (uint)(y * 668265263) + (seed * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
