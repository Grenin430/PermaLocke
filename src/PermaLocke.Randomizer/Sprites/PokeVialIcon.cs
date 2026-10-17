namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// The Pokévial's icon (2026-10-16, the player picked «B» among six, prototypes in imagenes/revision/prototipo_vial): a round flask whose body
/// is a Poké Ball, glossy, its button a window of glass full of pink medicine, a glass neck with medicine in it and a cork, a golden aura and
/// two sparkles. Drawn by hand cell by cell (vial2.js) and handed to the game as the Repel's BFLIM with its pixels replaced
/// (<see cref="SuperCandyIcon.Repaint"/>), as the Incubadora Turbo; the app draws the same pixels.
/// Redrawn smaller on 2026-10-17 (prototipo_variocolor/small.js), the same flask as the Poción Variocolor: the bag shows icons under 32 and smoothed, and cut the one that reached the edge.
/// </summary>
public static class PokeVialIcon
{
    private const int Size = 32;

    private static readonly string[] Rows =
    [
        "............AAAAAAAA............",
        "...........ABBBBBBBBA...........",
        "..........ABCCCCDDEEBA..........",
        "..........ABCCFDDEEGBA..........",
        "..........ABDDEEEFHGBA..........",
        "..........ABEHHGGGGGBA..........",
        "...........ABIIIJJKBA...........",
        "...........ABIIJJKKBA...........",
        "...........ABLLLMMNBA...........",
        "...........ABMMMNNOBA...........",
        "..........AABNNPOOOBAA..........",
        ".........ABBQRRRRSSSBBA.........",
        "........ABRQRRRRSSSSSTBA........",
        ".......ABRQRSSSSSSSTTTTBA.......",
        "......ABSSSSSSSSTTTTTTUVBA......",
        "......ABSSSTTTTTTTTUUUUVBA......",
        ".....ABTTTTTTTQQWWUUUXXXVBA.....",
        ".....ABUUUUUUQLLMNYVVVVVVBA.....",
        ".....ABBBBBBBQLQMNZBBBBBBBA.....",
        ".....ABBBBBBBWMNNOZBBBBBBBA.....",
        ".....ABQQQQQQYNPOOaWWWYYYBA.....",
        ".....ABQQQQQQQZZaaWWWYYYYBA.....",
        "......ABQQQQWWWWWWWYYYYYBA......",
        "......ABWWWWWWWWYYYYYYZbBA......",
        ".......ABWWYYYYYYYYZZZcBA.......",
        "........ABYYYYYZZZZZbbBA........",
        ".........ABBZZbbbbbbBBA.........",
        "..........AABBBBBBBBAA..........",
        "............AAAAAAAA............",
        "................................",
        "................................",
        "................................",
    ];

    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['A'] = 0xFFD65A,
        ['B'] = 0x1C1624,
        ['C'] = 0xECCEA0,
        ['D'] = 0xD0A470,
        ['E'] = 0xB07C4C,
        ['F'] = 0x5C381E,
        ['G'] = 0xD29664,
        ['H'] = 0x84542E,
        ['I'] = 0xFAFEFF,
        ['J'] = 0xD6ECFA,
        ['K'] = 0xB0D2EC,
        ['L'] = 0xFFD2E6,
        ['M'] = 0xFC92BE,
        ['N'] = 0xE85C96,
        ['O'] = 0xFF78AA,
        ['P'] = 0xBA3476,
        ['Q'] = 0xFFFFFF,
        ['R'] = 0xFFB2A8,
        ['S'] = 0xFA6860,
        ['T'] = 0xDE3438,
        ['U'] = 0xA81C2C,
        ['V'] = 0xFF7864,
        ['W'] = 0xF4F6FA,
        ['X'] = 0x6E0E22,
        ['Y'] = 0xDEE0E8,
        ['Z'] = 0xB6BACA,
        ['a'] = 0x8C90A2,
        ['b'] = 0xD2DCF0,
        ['c'] = 0xF0FAFF,
    };

    /// <summary>The Repel's BFLIM (LZ-free, RGBA5551) repainted; null when it is not that.</summary>
    public static byte[]? Bflim(byte[] repel) => SuperCandyIcon.Repaint(repel, Paint);

    /// <summary>The icon's pixels (RGBA, 32 × 32); the template's own are not used.</summary>
    public static byte[] Paint(byte[] template, int width, int height)
    {
        if (width != Size || height != Size) throw new ArgumentException("El icono mide 32 × 32.");

        var o = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!Palette.TryGetValue(Rows[y][x], out var c)) continue;
                var i = ((y * Size) + x) * 4;
                (o[i], o[i + 1], o[i + 2], o[i + 3]) = ((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
            }
        }

        return o;
    }
}
