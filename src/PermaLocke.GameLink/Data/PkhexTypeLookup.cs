using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Types from the Ultra Sun / Ultra Moon personal table that PKHeX ships.
/// </summary>
/// <remarks>
/// Correct for this run because <b>types are not randomized</b>: the randomizer leaves evolution
/// lines and types as the cartridge has them, so the shipped table is the cartridge's table. Base
/// stats are a different matter and come from the ROM itself (<c>Data/species.json</c>).
/// </remarks>
public sealed class PkhexTypeLookup(string language = "es") : ITypeLookup
{
    private readonly GameStrings _strings = GameInfo.GetStrings(language);

    public TypePair GetTypes(int species)
    {
        if (species <= 0 || species > PersonalTable.USUM.MaxSpeciesID)
        {
            return new TypePair(-1, "?", -1, "?");
        }

        var personal = PersonalTable.USUM[species];
        return new TypePair(personal.Type1, GetName(personal.Type1), personal.Type2, GetName(personal.Type2));
    }

    public string GetName(int type) =>
        type >= 0 && type < _strings.Types.Count && !string.IsNullOrWhiteSpace(_strings.Types[type])
            ? _strings.Types[type]
            : $"Tipo {type}";
}
