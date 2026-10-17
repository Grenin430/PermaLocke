namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// The Poción Variocolor's icon (2026-10-17, the player picked «B» of the third round and then asked for its right half all over): the Pokévial's ball flask, its top all
/// in iridescent colours and its bottom pale gold, the shiny star in its button, an iridescent neck and a cork, a golden aura and the shiny
/// sparkles. Drawn cell by cell (iconos3.js, frame 0) and handed to the game as the
/// Repel's BFLIM with its pixels replaced (<see cref="SuperCandyIcon.Repaint"/>), as the Pokévial.
/// Redrawn smaller on 2026-10-17 (small.js): the bag shows icons under 32 and smoothed, and cut the one that reached the edge.
/// </summary>
public static class ShinyPotionIcon
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
        "...........ABLMMNNOBA...........",
        "...........ABPPNQORBA...........",
        "..........AABSQTRRUBAA..........",
        ".........ABBVWWWWXXXBBA.........",
        "........ABYVWWWWXXXXZaBA........",
        ".......ABYVWbbbXXXXaaaaBA.......",
        "......ABccbbbbXXddaaaaefBA......",
        "......ABcbbggddddaahheefBA......",
        ".....ABiggggddVVjjhhekkklBA.....",
        ".....ABmmmmnnVVVjooffffllBA.....",
        ".....ABBBBBBBpppqqrBBBBBBBA.....",
        ".....ABBBBBBBjqqVrsBBBBBBBA.....",
        ".....ABttttttoorusvwwwxxxBA.....",
        ".....ABtttttttusvywwwxxxxBA.....",
        "......ABttttwwwwwwwxxxxxBA......",
        "......ABwwwwwwwwxxxxxxz0BA......",
        ".......ABwwxxxxxxxxzzz1BA.......",
        "........ABxxxxxzzzzz00BA........",
        ".........ABBzz000000BBA.........",
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
        ['L'] = 0xE0FFEB,
        ['M'] = 0xE0FAFF,
        ['N'] = 0xA6A6FF,
        ['O'] = 0xD56AEB,
        ['P'] = 0xA6F0FF,
        ['Q'] = 0x6A6AEB,
        ['R'] = 0xFF8CC6,
        ['S'] = 0x6AD5EB,
        ['T'] = 0x3737B8,
        ['U'] = 0xFFB38C,
        ['V'] = 0xFFFFFF,
        ['W'] = 0xFFE0FF,
        ['X'] = 0xFFA6B5,
        ['Y'] = 0xE6E0FF,
        ['Z'] = 0xFFE1A6,
        ['a'] = 0xEBC06A,
        ['b'] = 0xFFA6FF,
        ['c'] = 0xB5A6FF,
        ['d'] = 0xEB6A7F,
        ['e'] = 0x77B837,
        ['f'] = 0x8CFF9F,
        ['g'] = 0xEB6AEB,
        ['h'] = 0xB88D37,
        ['i'] = 0x7F6AEB,
        ['j'] = 0xF4F6FA,
        ['k'] = 0x508020,
        ['l'] = 0x8CFFFF,
        ['m'] = 0xB837B8,
        ['n'] = 0xB8374D,
        ['o'] = 0xDEE0E8,
        ['p'] = 0xFFF6B4,
        ['q'] = 0xFCD65C,
        ['r'] = 0xE8AE28,
        ['s'] = 0xB6BACA,
        ['t'] = 0xFFFAE2,
        ['u'] = 0xB27612,
        ['v'] = 0x8C90A2,
        ['w'] = 0xFAECBA,
        ['x'] = 0xE8D28C,
        ['y'] = 0xFFDC78,
        ['z'] = 0xC4AA60,
        ['0'] = 0xFFE6A0,
        ['1'] = 0xF0FAFF,
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
