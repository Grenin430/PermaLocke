namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// The application's own pixel font: capitals seven cells tall, lower case with descenders, accents and the Ñ,
/// proportional widths and tabular digits.
/// </summary>
/// <remarks>
/// <para>
/// Drawn here and not taken from a font file for two reasons. A TrueType pixel font is smoothed by WPF at any size
/// that is not exactly its own, and at 125 % scaling that is every size; and this one is drawn in the same cells as
/// the scenes, so it stays sharp at any scaling the way they do (§116, §171).
/// </para>
/// <para>
/// A line is <see cref="LineRows"/> cells: three above the capitals for accents, seven for the capitals, two below for
/// descenders. Accented capitals are composed — the plain letter with its accent above — so the table stays small.
/// </para>
/// </remarks>
public static class PixelFont
{
    /// <summary>Rows above the capitals, where the accents of capitals go.</summary>
    public const int Above = 3;

    public const int CapRows = 7;

    /// <summary>Rows below the baseline, for g, j, p, q, y and the comma.</summary>
    public const int Below = 2;

    public const int LineRows = Above + CapRows + Below;

    /// <summary>Rows between one line and the next.</summary>
    public const int Leading = 1;

    public const int SpaceWidth = 3;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
        ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["####", "#...", "#...", "###.", "#...", "#...", "####"],
        ['F'] = ["####", "#...", "#...", "###.", "#...", "#...", "#..."],
        ['G'] = [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
        ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['I'] = ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
        ['J'] = ["..##", "...#", "...#", "...#", "#..#", "#..#", ".##."],
        ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['L'] = ["#...", "#...", "#...", "#...", "#...", "#...", "####"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
        ['O'] = [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
        ['Q'] = [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
        ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
        ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['V'] = ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['W'] = ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
        ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
        ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['Z'] = ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],

        ['a'] = [".....", ".....", ".###.", "....#", ".####", "#...#", ".####"],
        ['b'] = ["#....", "#....", "####.", "#...#", "#...#", "#...#", "####."],
        ['c'] = ["....", "....", ".###", "#...", "#...", "#...", ".###"],
        ['d'] = ["....#", "....#", ".####", "#...#", "#...#", "#...#", ".####"],
        ['e'] = [".....", ".....", ".###.", "#...#", "#####", "#....", ".###."],
        ['f'] = ["..##", ".#..", "####", ".#..", ".#..", ".#..", ".#.."],
        ['g'] = [".....", ".....", ".####", "#...#", "#...#", "#...#", ".####", "....#", ".###."],
        ['h'] = ["#....", "#....", "####.", "#...#", "#...#", "#...#", "#...#"],
        ['i'] = [".#.", "...", "##.", ".#.", ".#.", ".#.", "###"],
        ['j'] = ["...#", "....", "..##", "...#", "...#", "...#", "...#", "#..#", ".##."],
        ['k'] = ["#...", "#...", "#..#", "#.#.", "##..", "#.#.", "#..#"],
        ['l'] = ["##.", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
        ['m'] = [".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#.#.#"],
        ['n'] = [".....", ".....", "####.", "#...#", "#...#", "#...#", "#...#"],
        ['o'] = [".....", ".....", ".###.", "#...#", "#...#", "#...#", ".###."],
        ['p'] = [".....", ".....", "####.", "#...#", "#...#", "#...#", "####.", "#....", "#...."],
        ['q'] = [".....", ".....", ".####", "#...#", "#...#", "#...#", ".####", "....#", "....#"],
        ['r'] = ["....", "....", "#.##", "##..", "#...", "#...", "#..."],
        ['s'] = [".....", ".....", ".####", "#....", ".###.", "....#", "####."],
        ['t'] = [".#..", ".#..", "####", ".#..", ".#..", ".#..", "..##"],
        ['u'] = [".....", ".....", "#...#", "#...#", "#...#", "#...#", ".####"],
        ['v'] = [".....", ".....", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['w'] = [".....", ".....", "#...#", "#...#", "#.#.#", "#.#.#", ".#.#."],
        ['x'] = [".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#"],
        ['y'] = [".....", ".....", "#...#", "#...#", "#...#", "#...#", ".####", "....#", ".###."],
        ['z'] = [".....", ".....", "#####", "...#.", "..#..", ".#...", "#####"],
        ['á'] = ["...#.", "..#..", ".###.", "....#", ".####", "#...#", ".####"],
        ['é'] = ["...#.", "..#..", ".###.", "#...#", "#####", "#....", ".###."],
        ['í'] = ["..#", ".#.", "##.", ".#.", ".#.", ".#.", "###"],
        ['ó'] = ["...#.", "..#..", ".###.", "#...#", "#...#", "#...#", ".###."],
        ['ú'] = ["...#.", "..#..", "#...#", "#...#", "#...#", "#...#", ".####"],
        ['ü'] = [".#.#.", ".....", "#...#", "#...#", "#...#", "#...#", ".####"],
        ['ñ'] = [".#.#.", "#.#..", "####.", "#...#", "#...#", "#...#", "#...#"],

        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = [".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."],

        ['.'] = [".", ".", ".", ".", ".", ".", "#"],
        [','] = ["..", "..", "..", "..", "..", ".#", ".#", "#."],
        [':'] = [".", ".", "#", ".", ".", "#", "."],
        [';'] = ["..", "..", ".#", "..", "..", ".#", ".#", "#."],
        ['!'] = ["#", "#", "#", "#", "#", ".", "#"],
        ['¡'] = ["#", ".", "#", "#", "#", "#", "#"],
        ['?'] = [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
        ['¿'] = ["..#..", ".....", "..#..", ".#...", "#....", "#...#", ".###."],
        ['\''] = ["#", "#", ".", ".", ".", ".", "."],
        ['"'] = ["#.#", "#.#", "...", "...", "...", "...", "..."],
        ['('] = ["..#", ".#.", "#..", "#..", "#..", ".#.", "..#"],
        [')'] = ["#..", ".#.", "..#", "..#", "..#", ".#.", "#.."],
        ['['] = ["##", "#.", "#.", "#.", "#.", "#.", "##"],
        [']'] = ["##", ".#", ".#", ".#", ".#", ".#", "##"],
        ['-'] = ["....", "....", "....", "####", "....", "....", "...."],
        ['+'] = [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
        ['='] = ["....", "....", "####", "....", "####", "....", "...."],
        ['/'] = ["....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...."],
        ['%'] = ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
        ['#'] = [".#.#.", "#####", ".#.#.", ".#.#.", ".#.#.", "#####", ".#.#."],
        ['·'] = [".", ".", ".", "#", ".", ".", "."],
        ['…'] = [".....", ".....", ".....", ".....", ".....", ".....", "#.#.#"],
        ['«'] = [".....", "..#.#", ".#.#.", "#.#..", ".#.#.", "..#.#", "....."],
        ['»'] = [".....", "#.#..", ".#.#.", "..#.#", ".#.#.", "#.#..", "....."],
        ['→'] = [".....", "..#..", "...#.", "#####", "...#.", "..#..", "....."],
        ['←'] = [".....", "..#..", ".#...", "#####", ".#...", "..#..", "....."],
        ['×'] = [".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "....."],
        ['_'] = ["....", "....", "....", "....", "....", "....", "....", "####"],
        ['°'] = [".#.", "#.#", ".#.", "...", "...", "...", "..."],
        ['*'] = [".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."],
        ['&'] = [".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#"],
        ['@'] = [".###.", "#...#", "#.###", "#.#.#", "#.###", "#....", ".###."],
        ['♀'] = [".###.", "#...#", "#...#", ".###.", "..#..", "#####", "..#.."],
        ['♂'] = ["..###", "...##", ".##.#", "#..#.", "#..#.", ".##..", "....."],
        ['|'] = ["#", "#", "#", "#", "#", "#", "#"],
        ['▶'] = ["#...", "##..", "###.", "####", "###.", "##..", "#..."],
        ['◀'] = ["...#", "..##", ".###", "####", ".###", "..##", "...#"],
        ['ª'] = [".##", "#.#", ".##", "...", "###", "...", "..."],
        ['º'] = [".#.", "#.#", ".#.", "...", "###", "...", "..."],
        ['§'] = [".###", "#...", ".##.", "#..#", ".##.", "...#", "###."],
        ['<'] = ["...#", "..#.", ".#..", "#...", ".#..", "..#.", "...#"],
        ['>'] = ["#...", ".#..", "..#.", "...#", "..#.", ".#..", "#..."],
        ['~'] = ["....", "....", ".#.#", "#.#.", "....", "....", "...."],
        ['▲'] = [".....", "..#..", "..#..", ".###.", ".###.", "#####", "....."],
        ['▼'] = [".....", "#####", ".###.", ".###.", "..#..", "..#..", "....."],
    };

    /// <summary>What a character is drawn as when the table does not have it: its plain letter, or an outlined box.</summary>
    private static char Plain(char ch) => ch switch
    {
        'Á' or 'À' or 'Â' or 'Ä' => 'A',
        'É' or 'È' or 'Ê' or 'Ë' => 'E',
        'Í' or 'Ì' or 'Î' or 'Ï' => 'I',
        'Ó' or 'Ò' or 'Ô' or 'Ö' => 'O',
        'Ú' or 'Ù' or 'Û' or 'Ü' => 'U',
        'Ñ' => 'N',
        'à' or 'â' or 'ä' => 'á',
        'è' or 'ê' or 'ë' => 'é',
        'ì' or 'î' or 'ï' => 'í',
        'ò' or 'ô' or 'ö' => 'ó',
        'ù' or 'û' => 'ú',
        '−' or '–' or '—' => '-',
        '“' or '”' or '„' => '"',
        '‘' or '’' or '´' or '`' => '\'',
        'ç' => 'c',
        'Ç' => 'C',
        _ => ch,
    };

    /// <summary>Capitals whose accent is drawn on top of the plain letter, and which accent.</summary>
    private static int AccentOf(char ch) => ch switch
    {
        'Á' or 'É' or 'Í' or 'Ó' or 'Ú' => 1,
        'Ü' => 2,
        'Ñ' => 3,
        _ => 0,
    };

    /// <summary>Cells a character takes, without the column that separates it from the next.</summary>
    public static int WidthOf(char ch)
    {
        if (ch == ' ') return SpaceWidth;
        return Glyphs.TryGetValue(Plain(ch), out var glyph) ? glyph[0].Length : 4;
    }

    /// <summary>Cells a line of text takes, one column between characters.</summary>
    public static int Measure(string text)
    {
        var width = 0;
        for (var i = 0; i < text.Length; i++)
        {
            width += WidthOf(text[i]) + (i < text.Length - 1 ? 1 : 0);
        }

        return width;
    }

    /// <summary>
    /// Breaks text into lines no wider than <paramref name="maxWidth"/> cells, at spaces where it can and inside a
    /// word only when one word alone does not fit. Explicit line breaks are kept.
    /// </summary>
    public static List<string> Wrap(string text, int maxWidth)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = string.Empty;
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (Measure(candidate) <= maxWidth || line.Length == 0 && Measure(word) <= maxWidth)
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                {
                    lines.Add(line);
                }

                // Una palabra que sola no cabe se parte donde haga falta: mejor cortada que fuera de la caja.
                var rest = word;
                while (Measure(rest) > maxWidth && rest.Length > 1)
                {
                    var cut = rest.Length - 1;
                    while (cut > 1 && Measure(rest[..cut]) > maxWidth) cut--;
                    lines.Add(rest[..cut]);
                    rest = rest[cut..];
                }

                line = rest;
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Text cut to fit <paramref name="maxWidth"/> cells, with an ellipsis where it was cut.</summary>
    public static string Trim(string text, int maxWidth)
    {
        if (Measure(text) <= maxWidth) return text;

        var cut = text.Length;
        while (cut > 0 && Measure(text[..cut].TrimEnd() + "…") > maxWidth) cut--;
        return cut <= 0 ? "…" : text[..cut].TrimEnd() + "…";
    }

    /// <summary>
    /// Writes a line into a canvas: <paramref name="put"/> is called for every lit cell, with the line's top at row
    /// zero — so the capitals start at row <see cref="Above"/>.
    /// </summary>
    /// <param name="compact">Accents of capitals right on top of the letter, for a line that leaves out the top row.</param>
    public static void Draw(string text, int left, int top, Action<int, int> put, bool compact = false)
    {
        var mark = compact ? top + 1 : top;
        var x = left;
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                x += SpaceWidth + 1;
                continue;
            }

            var plain = Plain(ch);
            if (!Glyphs.TryGetValue(plain, out var glyph))
            {
                // Lo que no está en la tabla sale como una caja: se ve que falta, no se inventa otra letra.
                for (var gy = 0; gy < CapRows; gy++)
                {
                    put(x, top + Above + gy);
                    put(x + 3, top + Above + gy);
                }

                for (var gx = 1; gx < 3; gx++)
                {
                    put(x + gx, top + Above);
                    put(x + gx, top + Above + CapRows - 1);
                }

                x += 5;
                continue;
            }

            for (var gy = 0; gy < glyph.Length; gy++)
            {
                for (var gx = 0; gx < glyph[gy].Length; gx++)
                {
                    if (glyph[gy][gx] == '#') put(x + gx, top + Above + gy);
                }
            }

            var width = glyph[0].Length;
            switch (AccentOf(ch))
            {
                case 1:
                    put(x + (width / 2) + 1, mark);
                    put(x + (width / 2), mark + 1);
                    break;
                case 2:
                    put(x + 1, mark + 1);
                    put(x + width - 2, mark + 1);
                    break;
                case 3:
                    put(x + 1, mark);
                    put(x + 3, mark);
                    put(x, mark + 1);
                    put(x + 2, mark + 1);
                    break;
            }

            x += width + 1;
        }
    }

    /// <summary>Every glyph the table knows, for the test that checks they are well formed.</summary>
    public static IEnumerable<KeyValuePair<char, string[]>> All => Glyphs;
}
