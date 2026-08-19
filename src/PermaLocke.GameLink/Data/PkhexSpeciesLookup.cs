using System.Globalization;
using System.Text;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Species names from PKHeX.Core, so PermaLocke neither hardcodes a Pokédex nor invents one.
/// Limited to generation 7, which is the last one Ultra Sun and Ultra Moon know about.
/// </summary>
public sealed class PkhexSpeciesLookup : ISpeciesLookup
{
    /// <summary>Highest national dex number present in Ultra Sun / Ultra Moon (Melmetal excluded).</summary>
    private const int MaxSpecies = (int)Species.Zeraora;

    private readonly Dictionary<string, int> _byName;

    public PkhexSpeciesLookup(string language = "es")
    {
        var names = GameInfo.GetStrings(language).specieslist;

        var all = new List<SpeciesInfo>(MaxSpecies);
        _byName = new Dictionary<string, int>(MaxSpecies, StringComparer.Ordinal);

        for (var number = 1; number <= MaxSpecies && number < names.Length; number++)
        {
            var name = names[number];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            all.Add(new SpeciesInfo(number, name));
            _byName[Normalise(name)] = number;
        }

        All = all;
    }

    public IReadOnlyList<SpeciesInfo> All { get; }

    public string GetName(int species) =>
        All.FirstOrDefault(s => s.Number == species)?.Name ?? $"#{species}";

    public bool TryGetNumber(string name, out int species) =>
        _byName.TryGetValue(Normalise(name), out species);

    /// <summary>Lowercase, accents stripped, so "Nidoran♀" or "farfetch'd" still match.</summary>
    private static string Normalise(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
