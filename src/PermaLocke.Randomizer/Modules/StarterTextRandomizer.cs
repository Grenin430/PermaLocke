using System.Text.RegularExpressions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;
using pk3DS.Core;
using GameConfig = pk3DS.Core.GameConfig;
using Pk3dsVersion = pk3DS.Core.GameVersion;

namespace PermaLocke.Randomizer.Modules;

/// <summary>One starter as the story text names it: its name, and its types in words.</summary>
public sealed record StarterLabel(string Name, string FirstType, string? SecondType = null)
{
    /// <summary>«Planta» or «Planta y Veneno», for «el Pokémon de tipo …».</summary>
    public string Types => SecondType is null ? FirstType : $"{FirstType} y {SecondType}";

    /// <summary>The choice in the menu. Kept short: nobody has measured how wide that menu is.</summary>
    public string Menu => SecondType is null ? $"{Name}, de tipo {FirstType}" : $"{Name}, {FirstType}/{SecondType}";
}

/// <param name="Lines">Lines of the story text rewritten.</param>
/// <param name="Starters">The three names the text now says, in the game's order.</param>
public sealed record StarterTextResult(int Lines, IReadOnlyList<string> Starters);

/// <summary>
/// Makes the starter scene name the Pokémon the player will actually get.
/// </summary>
/// <remarks>
/// <para>
/// The randomizer puts other species in the three gift entries, but the scene where Kukui presents them
/// and the menu where you choose are story text, and the story text says Rowlet, Litten and Popplio in
/// plain letters: 21 lines across three files of <c>a/0/4/6</c>, found by searching for the names. The
/// player asked for them to say the real ones (2026-09-19, §146).
/// </para>
/// <para>
/// What changes is the name and the type — «Ese es Jangmo-o, un Pokémon de tipo Dragón» —, the three
/// descriptions, which talk about Rowlet flying and Popplio making balloons and would be false, and the
/// confirmation, where the game prints a variable that is replaced by the name written out: right
/// whichever value the script puts in that variable. The cries («¡Rooow!») stay: they belong to the
/// Pokémon on screen, and whether the scene shows the cartridge's model or the randomized one has not
/// been checked.
/// </para>
/// <para>
/// Every line is checked against what was measured, and if the count of rewritten lines is not exactly
/// the expected one per file, <b>nothing is written</b>: a text that is not the one measured is
/// somebody else's, and editing it by pattern would be guessing.
/// </para>
/// </remarks>
public sealed class StarterTextRandomizer(RomWorkspace workspace)
{
    /// <summary>The Pokémon the three gift entries hold on the cartridge: Rowlet, Litten, Popplio.</summary>
    public static readonly int[] CartridgeStarters = [722, 725, 728];

    /// <summary>The story text files with the starter scene, and how many lines each must rewrite.</summary>
    public static readonly IReadOnlyDictionary<int, int> ExpectedLines = new Dictionary<int, int>
    {
        [38] = 3,   // Kukui los presenta
        [39] = 9,   // descripción, confirmación y nombre, versión antigua de la escena
        [51] = 9,   // el menú, la confirmación y «te está mirando fijamente»
    };

    /// <summary>A line wider than this is broken in two. The game's own reach about 48.</summary>
    private const int MaxLineWidth = 44;

    /// <summary>
    /// A bare configuration for reading and writing the text, not the workspace's.
    /// </summary>
    /// <remarks>
    /// The workspace's knows the variables' names and prints the confirmation as
    /// <c>[VAR PKNAME(0001)]</c>; the scene was measured as <c>[VAR 0101(0001)]</c>, and the rules look
    /// for that. With the workspace's the guard caught it at once — six lines instead of nine — and wrote
    /// nothing. Both notations encode to the same words.
    /// </remarks>
    private static readonly GameConfig TextConfig = new(Pk3dsVersion.UM);

    public async Task<StarterTextResult?> ApplyAsync(LayeredFsMod mod, CancellationToken ct = default)
    {
        // Sin iniciales cambiados no hay nada que decir distinto.
        if (!mod.StagedFiles.Contains(GameFiles.EncounterStatic))
        {
            return null;
        }

        var staticsPath = Path.Combine(mod.RomFsDirectory,
            GameFiles.EncounterStatic.Replace('/', Path.DirectorySeparatorChar));
        var gifts = GarcPatcher.ReadOnly(staticsPath, StaticEncounterTable.Gifts.Subfile);

        // La tabla de especies ENTERA, la que lee el juego (§141), del mundo tal como queda.
        var personalPath = mod.StagedFiles.Contains(GameFiles.Personal)
            ? Path.Combine(mod.RomFsDirectory, GameFiles.Personal.Replace('/', Path.DirectorySeparatorChar))
            : workspace.PathOf(GameFiles.Personal);
        var packed = GarcPatcher.ReadOnly(personalPath, GarcPatcher.CountReadOnly(personalPath) - 1);
        var cartridge = GarcPatcher.ReadOnly(workspace.PathOf(GameFiles.Personal),
            GarcPatcher.CountReadOnly(workspace.PathOf(GameFiles.Personal)) - 1);

        var species = workspace.Config.GetText(TextName.SpeciesNames);
        var types = workspace.Config.GetText(TextName.Types);

        var before = CartridgeStarters
            .Select(id => Label(species, types, cartridge, id, 0, onlyFirstType: true))
            .ToArray();
        var after = Enumerable.Range(0, StaticEncounterTable.StarterCount)
            .Select(i => Label(species, types, packed,
                StaticEncounterTable.GetSpecies(gifts, StaticEncounterTable.Gifts, i),
                StaticEncounterTable.GetForm(gifts, StaticEncounterTable.Gifts, i)))
            .ToArray();

        var storyPath = workspace.PathOf(GameFiles.StoryText(RomWorkspace.SpanishLanguage));
        var garc = new GARC.LazyGARC(await File.ReadAllBytesAsync(storyPath, ct));
        var original = new GARC.LazyGARC(await File.ReadAllBytesAsync(storyPath, ct));
        var expected = new Dictionary<int, string[]>();
        var rewritten = 0;

        foreach (var (file, lineCount) in ExpectedLines)
        {
            var lines = TextFile.GetStrings(TextConfig, garc[file]);
            var changed = Rewrite(lines, before, after);

            if (changed.Count != lineCount)
            {
                throw new InvalidDataException(
                    $"El texto {file} de la escena de los iniciales no es el que se midió: salen {changed.Count} "
                    + $"líneas que cambiar y se esperaban {lineCount}. No se toca el texto.");
            }

            garc[file] = GameTextPatch.ReplaceLines(garc[file],
                changed.ToDictionary(c => c.Key, c => GameTextPatch.Encode(TextConfig, c.Value)));

            foreach (var (index, text) in changed)
            {
                lines[index] = text;
            }

            expected[file] = lines;
            rewritten += changed.Count;
        }

        // Se relee ANTES de escribir: un texto que no dice lo que se quería no llega a la carpeta del mod.
        var packedText = garc.Save();
        Verify(packedText, original, expected);
        await mod.WriteAsync(GameFiles.StoryText(RomWorkspace.SpanishLanguage), packedText, ct);

        return new StarterTextResult(rewritten, [.. after.Select(a => a.Name)]);
    }

    /// <summary>
    /// The lines of one text file that name a starter, rewritten for the new ones, by line index.
    /// </summary>
    /// <param name="before">The cartridge's three, with only the type the text mentions.</param>
    /// <param name="after">The ones the gift entries hold now, in the same order.</param>
    public static Dictionary<int, string> Rewrite(IReadOnlyList<string> lines,
        IReadOnlyList<StarterLabel> before, IReadOnlyList<StarterLabel> after)
    {
        var result = new Dictionary<int, string>();

        for (var i = 0; i < lines.Count; i++)
        {
            for (var k = 0; k < before.Count; k++)
            {
                if (RewriteLine(lines[i], k, before[k], after[k]) is { } line)
                {
                    result[i] = line;
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>One line for one starter, or null when the line is not about that starter.</summary>
    private static string? RewriteLine(string line, int k, StarterLabel old, StarterLabel now)
    {
        var oldType = $"de tipo {old.FirstType}";
        var newType = $"de tipo {now.Types}";

        // Solo el nombre.
        if (line == old.Name)
        {
            return now.Name;
        }

        // La opción del menú.
        if (line == $"{old.Name}, de tipo {old.FirstType}")
        {
            return now.Menu;
        }

        // La confirmación, que imprime una variable: el nombre escrito vale sea cual sea su valor.
        var variable = $"[VAR 0101(000{k + 1})]";

        if (line.Contains(variable, StringComparison.Ordinal) && line.Contains($"{oldType}?", StringComparison.Ordinal))
        {
            return line.Replace(variable, now.Name, StringComparison.Ordinal)
                .Replace(oldType, newType, StringComparison.Ordinal);
        }

        if (!Regex.IsMatch(line, $@"(?<![\p{{L}}]){Regex.Escape(old.Name)}(?![\p{{L}}])"))
        {
            return null;
        }

        // La descripción de la especie: habla de volar o de hacer globos, así que se sustituye entera.
        if (line.Contains(Break, StringComparison.Ordinal)
            && (line.StartsWith(old.Name, StringComparison.Ordinal)
                || line.StartsWith($"¡{old.Name}", StringComparison.Ordinal)))
        {
            return $"¡{now.Name} es un Pokémon{Break}de tipo {now.Types}!{TrailingCodes(line)}";
        }

        // La presentación y «te está mirando fijamente»: el nombre, y el tipo si lo dice.
        var rewritten = line.Replace(old.Name, now.Name, StringComparison.Ordinal)
            .Replace(oldType, newType, StringComparison.Ordinal);

        return Width(rewritten) > MaxLineWidth
            ? rewritten.Replace(", un Pokémon", $",{Break}un Pokémon", StringComparison.Ordinal)
            : rewritten;
    }

    /// <summary>
    /// The game's line break as pk3DS <b>reads</b> it: a backslash and an n, two characters, not a newline.
    /// </summary>
    /// <remarks>
    /// Written, both come out as the same word, 0x000A, so the bytes of the first run were right. What was
    /// wrong was reading: the rules looked for a real newline in lines that carry the two characters, so
    /// the three descriptions were not recognised and would have said «¡Jangmo-o es capaz de volar…». The
    /// read-back compares text, saw a newline go in and <c>\n</c> come out, and stopped the run before a
    /// byte reached the mod folder.
    /// </remarks>
    public const string Break = @"\n";

    /// <summary>The same, for «clear the box and go on», <c>\c</c>.</summary>
    private const string Clear = @"\c";

    /// <summary>The control codes a line ends with, such as <c>[VAR 0114(0005)]</c>, which wait for a key.</summary>
    private static string TrailingCodes(string line)
    {
        var match = Regex.Match(line, @"(\[[^\]]*\])+$");
        return match.Success ? match.Value : string.Empty;
    }

    /// <summary>The widest row of a line, in letters, without its control codes.</summary>
    public static int Width(string line) =>
        Regex.Replace(line, @"\[[^\]]*\]", string.Empty)
            .Split([Break, Clear], StringSplitOptions.None)
            .Max(row => row.Length);

    private static StarterLabel Label(string[] species, string[] types, byte[] packed, int id, int form,
        bool onlyFirstType = false)
    {
        if (id <= 0 || id >= species.Length)
        {
            throw new InvalidDataException($"El inicial {id} no tiene nombre en el texto del juego.");
        }

        var row = form > 0 && PersonalEntry7.RowOf(packed, id, form) is { } own ? own : id;
        var (first, second) = PersonalEntry7.GetTypes(packed, row * PersonalEntry7.Size);

        return new StarterLabel(species[id], types[first],
            onlyFirstType || second == first ? null : types[second]);
    }

    /// <summary>
    /// Reads the packed text back: the three files must say exactly what was meant, and every other
    /// file must be byte for byte the cartridge's.
    /// </summary>
    private void Verify(byte[] packed, GARC.LazyGARC original, IReadOnlyDictionary<int, string[]> expected)
    {
        var back = new GARC.LazyGARC(packed);

        if (back.FileCount != original.FileCount)
        {
            throw new InvalidDataException(
                $"El texto de historia se quedó con {back.FileCount} ficheros en vez de {original.FileCount}.");
        }

        for (var file = 0; file < back.FileCount; file++)
        {
            if (expected.TryGetValue(file, out var lines))
            {
                var read = TextFile.GetStrings(TextConfig, back[file]);

                if (!read.SequenceEqual(lines))
                {
                    throw new InvalidDataException($"El texto {file} no dice lo que se escribió.");
                }
            }
            else if (!back[file].AsSpan().SequenceEqual(original[file]))
            {
                throw new InvalidDataException($"El texto {file} cambió y no se había tocado.");
            }
        }
    }
}
