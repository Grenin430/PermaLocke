using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Rom;
using GameConfig = pk3DS.Core.GameConfig;
using Pk3dsVersion = pk3DS.Core.GameVersion;
using TextFile = pk3DS.Core.TextFile;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The starter scene naming the Pokémon the player will really get (§146), and the text writer that
/// changes only the lines it is asked to.
/// </summary>
/// <remarks>
/// The lines here are made up in the shape of the game's, not copied from it: what the rules look at is
/// the shape — a name, «de tipo X», a variable, a line break — and not the wording. The line break is
/// <see cref="N"/>, the two characters pk3DS reads, and not a C# newline: that confusion is the first bug
/// this module had, and it would have left the three descriptions talking about Rowlet.
/// </remarks>
public sealed class StarterTextTests
{
    /// <summary>The game's line break as pk3DS writes it.</summary>
    private const string N = StarterTextRandomizer.Break;

    private static readonly GameConfig Config = new(Pk3dsVersion.UM);

    private static readonly StarterLabel[] Cartridge =
    [
        new("Rowlet", "Planta"), new("Litten", "Fuego"), new("Popplio", "Agua"),
    ];

    private static readonly StarterLabel[] Randomized =
    [
        new("Jangmo-o", "Dragón"), new("Gothita", "Psíquico"), new("Bulbasaur", "Planta", "Veneno"),
    ];

    private static byte[] File(params string[] lines) => TextFile.GetBytes(Config, lines);

    // ------------------------------------------------------------------ el escritor

    /// <summary>With nothing to replace, the file comes out exactly as it went in.</summary>
    [Fact]
    public void Nothing_to_replace_gives_back_the_same_bytes()
    {
        var original = File("Hola.[VAR 0114(0005)]", "Adiós, [VAR 0101(0000)].", $"¿Seguro?{N}Sí.");

        Assert.Equal(original, GameTextPatch.ReplaceLines(original, new Dictionary<int, ushort[]>()));
    }

    /// <summary>
    /// The field after each line's length is kept: the cartridge sets it to 4 on some lines, pk3DS
    /// writes zero, and nobody here knows what it means.
    /// </summary>
    [Fact]
    public void A_replaced_line_keeps_its_flag_and_the_others_keep_their_bytes()
    {
        var original = File("Uno.", "Dos.", "Tres.");
        BitConverter.GetBytes((ushort)4).CopyTo(original, 0x10 + 4 + 8 + 6);

        var patched = GameTextPatch.ReplaceLines(original,
            new Dictionary<int, ushort[]> { [1] = GameTextPatch.Encode(Config, "Un texto bastante más largo.") });

        Assert.Equal(["Uno.", "Un texto bastante más largo.", "Tres."], TextFile.GetStrings(Config, patched));
        Assert.Equal(4, BitConverter.ToUInt16(patched, 0x10 + 4 + 8 + 6));
    }

    /// <summary>The length is the words up to the terminator, padding left out, like the cartridge's.</summary>
    [Fact]
    public void The_length_does_not_count_the_padding()
    {
        var original = File("A", "B");
        var words = GameTextPatch.Encode(Config, "Tres");

        Assert.Equal(5, words.Length);

        var patched = GameTextPatch.ReplaceLines(original, new Dictionary<int, ushort[]> { [0] = words });

        Assert.Equal(5, BitConverter.ToUInt16(patched, 0x10 + 4 + 4));
        Assert.Equal(["Tres", "B"], TextFile.GetStrings(Config, patched));
    }

    /// <summary>
    /// A variable can carry a zero, so «up to the first zero» would cut it in half. The walk skips
    /// over variables.
    /// </summary>
    [Fact]
    public void A_variable_with_a_zero_is_not_mistaken_for_the_end()
    {
        var words = GameTextPatch.Encode(Config, "¡Elegiste a [VAR 0101(0000)]!");
        var patched = GameTextPatch.ReplaceLines(File("x"), new Dictionary<int, ushort[]> { [0] = words });

        Assert.Equal(["¡Elegiste a [VAR 0101(0000)]!"], TextFile.GetStrings(Config, patched));
    }

    /// <summary>
    /// The written line break goes in as the game's break, the word 0x000A, and reads back as the same two
    /// characters — which is the notation every rule has to look for.
    /// </summary>
    [Fact]
    public void The_line_break_survives_the_round_trip()
    {
        var line = $"¡Gothita es un Pokémon{N}de tipo Psíquico![VAR 0114(0005)]";
        var words = GameTextPatch.Encode(Config, line);
        var patched = GameTextPatch.ReplaceLines(File("x"), new Dictionary<int, ushort[]> { [0] = words });

        Assert.Equal([line], TextFile.GetStrings(Config, patched));
        Assert.Contains((ushort)0x000A, words);
    }

    [Fact]
    public void A_line_that_does_not_exist_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameTextPatch.ReplaceLines(File("x"), new Dictionary<int, ushort[]> { [3] = [0] }));
    }

    // ------------------------------------------------------------------ lo que dice cada línea

    private static Dictionary<int, string> Rewrite(params string[] lines) =>
        StarterTextRandomizer.Rewrite(lines, Cartridge, Randomized);

    [Fact]
    public void The_presentation_says_the_new_name_and_its_type()
    {
        var changed = Rewrite("Este es Rowlet, un Pokémon de tipo Planta.[VAR 0114(0005)]");

        Assert.Equal("Este es Jangmo-o, un Pokémon de tipo Dragón.[VAR 0114(0005)]", changed[0]);
    }

    /// <summary>Two types, both said, and the sentence breaks where it would run off the box.</summary>
    [Fact]
    public void A_dual_type_is_said_whole()
    {
        var changed = Rewrite("Y aquí Popplio, un Pokémon de tipo Agua.");

        Assert.Equal($"Y aquí Bulbasaur,{N}un Pokémon de tipo Planta y Veneno.", changed[0]);
    }

    /// <summary>
    /// The confirmation prints a variable. Writing the name out is right whatever the script puts
    /// in it, and the line for the second starter is the one with the second variable.
    /// </summary>
    [Fact]
    public void The_confirmation_writes_the_name_instead_of_the_variable()
    {
        var changed = Rewrite($"¿Te quedas con [VAR 0101(0002)], el Pokémon{N}de tipo Fuego?[VAR 0114(0008)]");

        Assert.Equal($"¿Te quedas con Gothita, el Pokémon{N}de tipo Psíquico?[VAR 0114(0008)]", changed[0]);
    }

    /// <summary>A line with a starter's variable but no type is somebody else's line.</summary>
    [Fact]
    public void A_variable_without_the_type_is_left_alone()
    {
        Assert.Empty(Rewrite("¡Parece que [VAR 0101(0001)] te ha aceptado!", "¡Elegiste a [VAR 0101(0000)]!"));
    }

    /// <summary>The descriptions talk about the old species, so they are replaced whole.</summary>
    [Fact]
    public void A_description_is_replaced_whole_and_keeps_its_codes()
    {
        var changed = Rewrite($"¡Rowlet planea sin hacer{N}ruido por el bosque![VAR 0114(0005)]",
            $"Litten escupe fuego, ¡pero{N}es muy tranquilo![VAR 0114(0005)]");

        Assert.Equal($"¡Jangmo-o es un Pokémon{N}de tipo Dragón![VAR 0114(0005)]", changed[0]);
        Assert.Equal($"¡Gothita es un Pokémon{N}de tipo Psíquico![VAR 0114(0005)]", changed[1]);
    }

    [Fact]
    public void The_menu_and_the_bare_names()
    {
        var changed = Rewrite("Rowlet, de tipo Planta", "Popplio, de tipo Agua", "Litten");

        Assert.Equal("Jangmo-o, de tipo Dragón", changed[0]);
        Assert.Equal("Bulbasaur, Planta/Veneno", changed[1]);
        Assert.Equal("Gothita", changed[2]);
    }

    /// <summary>The cries are not names: «Pop» is not Popplio, and they stay as they are.</summary>
    [Fact]
    public void The_cries_are_not_touched()
    {
        Assert.Empty(Rewrite("¿Pop?", "Row...", "¡Poppliii!", "¡Liiit!"));
    }

    /// <summary>A long name with two long types would run off the box, so the sentence breaks.</summary>
    [Fact]
    public void A_line_too_wide_for_the_box_is_broken_in_two()
    {
        var changed = StarterTextRandomizer.Rewrite(
            ["Y ahora este otro es Popplio, un Pokémon de tipo Agua.[VAR 0114(0005)]"],
            Cartridge, [.. Randomized[..2], new StarterLabel("Crabominable", "Eléctrico", "Siniestro")]);

        Assert.Equal(
            $"Y ahora este otro es Crabominable,{N}un Pokémon de tipo Eléctrico y Siniestro.[VAR 0114(0005)]",
            changed[0]);
        Assert.True(StarterTextRandomizer.Width(changed[0]) <= 44);
    }

    /// <summary>
    /// Nothing the rules write is a real newline: the game would not mind, but the read-back compares text
    /// and would refuse it.
    /// </summary>
    [Fact]
    public void No_rewritten_line_carries_a_real_newline()
    {
        var changed = Rewrite(
            "Este es Rowlet, un Pokémon de tipo Planta.",
            $"¡Litten es muy{N}tranquilo!",
            "Y aquí Popplio, un Pokémon de tipo Agua.");

        Assert.All(changed.Values, line => Assert.DoesNotContain('\n', line));
    }

    /// <summary>A new name that happens to be an old one is not rewritten twice.</summary>
    [Fact]
    public void Each_line_is_rewritten_once()
    {
        var changed = StarterTextRandomizer.Rewrite(
            ["¡Rowlet te mira!"], Cartridge, [new("Litten", "Fuego"), new("Popplio", "Agua"), new("Rowlet", "Planta")]);

        Assert.Equal("¡Litten te mira!", changed[0]);
    }

    /// <summary>The three files and how many lines each must change: the guard that stops a different text.</summary>
    [Fact]
    public void The_measured_scene_is_twenty_one_lines_in_three_files()
    {
        Assert.Equal(21, StarterTextRandomizer.ExpectedLines.Values.Sum());
        Assert.Equal([722, 725, 728], StarterTextRandomizer.CartridgeStarters);
    }
}
