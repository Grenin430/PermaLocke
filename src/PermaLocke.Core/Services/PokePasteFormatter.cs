using System.Text;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Services;

/// <summary>
/// Writes Pokémon in the text format <c>pokepast.es</c> and Pokémon Showdown read.
/// </summary>
/// <remarks>
/// <para>
/// The format is documented at <c>https://pokepast.es/syntax.html</c>. It is line based and every
/// line after the first is optional, which is what makes it forgiving: a Pokémon with nothing but
/// a species is still valid paste.
/// </para>
/// <para>
/// Two rules are worth stating because getting them wrong produces a paste that looks right and
/// imports wrong. <b>Only the stats that matter are listed</b> — EVs above zero, IVs below 31 —
/// because a full list of zeroes is not the same as saying nothing. And <b>the names have to be
/// English</b>: the site keys off them, so a paste written with the Spanish names of the player's
/// own game is unusable. That is the caller's job; this only lays out what it is given.
/// </para>
/// </remarks>
public static class PokePasteFormatter
{
    /// <summary>Stat labels, in the order the games and this codebase store them.</summary>
    private static readonly string[] Stats = ["HP", "Atk", "Def", "SpA", "SpD", "Spe"];

    private const int PerfectIv = 31;

    /// <summary>The whole list, one blank line between each, as the site expects.</summary>
    public static string Write(IEnumerable<BoxedPokemon> pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        var blocks = pokemon
            .Where(p => !p.IsEgg)
            .Select(Write)
            .ToList();

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    /// <summary>One Pokémon.</summary>
    public static string Write(BoxedPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        var text = new StringBuilder();

        text.Append(Header(pokemon));

        if (!string.IsNullOrWhiteSpace(pokemon.AbilityName) && pokemon.AbilityName != "?")
        {
            text.AppendLine().Append("Ability: ").Append(pokemon.AbilityName);
        }

        // El nivel solo se escribe si no es 100, que es lo que el sitio da por supuesto.
        if (pokemon.Level != 100)
        {
            text.AppendLine().Append("Level: ").Append(pokemon.Level);
        }

        if (pokemon.IsShiny)
        {
            text.AppendLine().Append("Shiny: Yes");
        }

        if (Spread("EVs", pokemon.Evs, value => value > 0) is { } evs)
        {
            text.AppendLine().Append(evs);
        }

        if (!string.IsNullOrWhiteSpace(pokemon.NatureName) && pokemon.NatureName != "?")
        {
            text.AppendLine().Append(pokemon.NatureName).Append(" Nature");
        }

        if (Spread("IVs", pokemon.Ivs, value => value != PerfectIv) is { } ivs)
        {
            text.AppendLine().Append(ivs);
        }

        foreach (var move in pokemon.Moves.Where(move => !string.IsNullOrWhiteSpace(move) && move != "?"))
        {
            text.AppendLine().Append("- ").Append(move);
        }

        return text.ToString();
    }

    /// <summary>
    /// The first line: <c>Mote (Especie) (Sexo) @ Objeto</c>, with everything optional but the name.
    /// </summary>
    /// <remarks>
    /// The species only goes in brackets when the Pokémon has a nickname, because otherwise the
    /// name <em>is</em> the species and "Pikachu (Pikachu)" is how a paste betrays that it was
    /// written by a machine that did not check.
    /// </remarks>
    private static string Header(BoxedPokemon pokemon)
    {
        var line = new StringBuilder();

        var named = !string.IsNullOrWhiteSpace(pokemon.Nickname)
                    && !string.Equals(pokemon.Nickname, pokemon.SpeciesName, StringComparison.OrdinalIgnoreCase);

        // La forma va pegada a la especie con guion, como la escribe Showdown: «Vulpix-Alola». Sin
        // ella el sitio dibuja y valida la forma normal (§140).
        var species = string.IsNullOrEmpty(pokemon.FormName)
            ? pokemon.SpeciesName
            : $"{pokemon.SpeciesName}-{pokemon.FormName}";

        line.Append(named ? pokemon.Nickname : species);

        if (named)
        {
            line.Append(" (").Append(species).Append(')');
        }

        if (Gender(pokemon.GenderMark) is { } gender)
        {
            line.Append(" (").Append(gender).Append(')');
        }

        if (!string.IsNullOrWhiteSpace(pokemon.HeldItemName) && pokemon.HeldItemName != "?")
        {
            line.Append(" @ ").Append(pokemon.HeldItemName);
        }

        return line.ToString();
    }

    /// <summary>The Mars and Venus signs the reader produces, as the letters the format wants.</summary>
    private static string? Gender(string mark) => mark switch
    {
        "♂" => "M",
        "♀" => "F",
        _ => null
    };

    /// <summary>
    /// An <c>EVs:</c> or <c>IVs:</c> line, or null when there is nothing worth saying.
    /// </summary>
    private static string? Spread(string label, IReadOnlyList<int> values, Func<int, bool> worthWriting)
    {
        var parts = Enumerable.Range(0, Math.Min(Stats.Length, values.Count))
            .Where(index => worthWriting(values[index]))
            .Select(index => $"{values[index]} {Stats[index]}")
            .ToList();

        return parts.Count == 0 ? null : $"{label}: {string.Join(" / ", parts)}";
    }
}
