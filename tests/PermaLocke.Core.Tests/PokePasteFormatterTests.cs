using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The paste format is checked line by line because it fails <b>quietly</b>: pokepast.es accepts
/// almost anything and simply drops what it does not understand, so a wrong line does not raise an
/// error, it produces a team with a Pokémon missing.
/// </summary>
public class PokePasteFormatterTests
{
    private static BoxedPokemon Make(
        string species = "Kommo-o",
        string nickname = "",
        int level = 50,
        bool shiny = false,
        bool egg = false,
        string gender = "\u2642",
        string nature = "Adamant",
        string ability = "Bulletproof",
        string item = "Leftovers",
        IReadOnlyList<string>? moves = null,
        IReadOnlyList<int>? ivs = null,
        IReadOnlyList<int>? evs = null) =>
        new(
            Box: 0, Slot: 0, Species: 784, Form: 0,
            SpeciesName: species, Nickname: nickname, Level: level,
            IsShiny: shiny, IsEgg: egg, GenderMark: gender,
            NatureName: nature, AbilityName: ability, HeldItemName: item,
            BallName: "Poke Ball", TrainerName: "Grenin", MetLocationName: "Route 1", MetLevel: 5,
            Moves: moves ?? ["Clanging Scales", "Close Combat", "Poison Jab", "Dragon Dance"],
            Stats: [1, 2, 3, 4, 5, 6],
            Ivs: ivs ?? [31, 31, 31, 31, 31, 31],
            Evs: evs ?? [0, 252, 0, 0, 4, 252],
            Friendship: 70, Pid: 1);

    [Fact]
    public void Writes_the_documented_shape()
    {
        var lines = PokePasteFormatter.Write(Make()).Split(Environment.NewLine);

        Assert.Equal("Kommo-o (M) @ Leftovers", lines[0]);
        Assert.Equal("Ability: Bulletproof", lines[1]);
        Assert.Equal("Level: 50", lines[2]);
        Assert.Equal("EVs: 252 Atk / 4 SpD / 252 Spe", lines[3]);
        Assert.Equal("Adamant Nature", lines[4]);
        Assert.Equal("- Clanging Scales", lines[5]);
        Assert.Equal("- Dragon Dance", lines[8]);
    }

    /// <summary>"Pikachu (Pikachu)" is how a paste betrays that nobody checked.</summary>
    [Fact]
    public void Only_brackets_the_species_when_there_is_a_real_nickname()
    {
        Assert.StartsWith("Kommo-o (M)", PokePasteFormatter.Write(Make()));
        Assert.StartsWith("Escamitas (Kommo-o) (M)", PokePasteFormatter.Write(Make(nickname: "Escamitas")));

        // Un mote que es el nombre de la especie no es un mote.
        Assert.StartsWith("Kommo-o (M)", PokePasteFormatter.Write(Make(nickname: "Kommo-o")));
    }

    /// <summary>A list of zeroes is not the same as saying nothing.</summary>
    [Fact]
    public void Leaves_out_what_the_format_assumes()
    {
        var text = PokePasteFormatter.Write(Make(
            level: 100, evs: [0, 0, 0, 0, 0, 0], ivs: [31, 31, 31, 31, 31, 31], item: ""));

        Assert.DoesNotContain("Level:", text);
        Assert.DoesNotContain("EVs:", text);
        Assert.DoesNotContain("IVs:", text);
        Assert.DoesNotContain("@", text);
    }

    [Fact]
    public void Writes_only_the_imperfect_ivs()
    {
        var text = PokePasteFormatter.Write(Make(ivs: [31, 0, 31, 30, 31, 31]));

        Assert.Contains("IVs: 0 Atk / 30 SpA", text);
    }

    [Fact]
    public void Marks_shiny_and_the_female_sign()
    {
        var text = PokePasteFormatter.Write(Make(shiny: true, gender: "\u2640"));

        Assert.Contains("(F)", text);
        Assert.Contains("Shiny: Yes", text);
    }

    /// <summary>Sin sexo no se escribe el paréntesis: un Magnemite no es macho ni hembra.</summary>
    [Fact]
    public void Genderless_gets_no_bracket()
    {
        Assert.StartsWith("Kommo-o @ Leftovers", PokePasteFormatter.Write(Make(gender: "")));
    }

    [Fact]
    public void Eggs_are_not_exported()
    {
        var text = PokePasteFormatter.Write([Make(), Make(species: "Ditto", egg: true), Make(species: "Mudsdale")]);

        Assert.DoesNotContain("Ditto", text);
        Assert.Contains("Mudsdale", text);

        // Un bloque por Pokémon, separados por una línea en blanco.
        Assert.Equal(2, text.Split(Environment.NewLine + Environment.NewLine).Length);
    }

    /// <summary>The reader writes "?" where it could not resolve a name; that is not a name.</summary>
    [Fact]
    public void Never_writes_a_question_mark_as_if_it_were_a_name()
    {
        var text = PokePasteFormatter.Write(Make(
            ability: "?", nature: "?", item: "?", moves: ["Tackle", "?", "?", "?"]));

        Assert.DoesNotContain("?", text);
        Assert.Contains("- Tackle", text);
        Assert.Single(text.Split(Environment.NewLine), line => line.StartsWith("- "));
    }
}
