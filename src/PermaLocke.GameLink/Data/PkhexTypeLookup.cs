using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Types of a species: from the installed world's own table when it has published one, from PKHeX's Ultra Sun / Ultra
/// Moon table otherwise.
/// </summary>
/// <remarks>
/// <para>
/// PKHeX alone was correct for the cartridge — <b>types are not randomized</b>, so its shipped table is the
/// cartridge's — and wrong for the gen 8-9 expansion, because that table stops at 807: every new Pokémon from a wonder
/// trade came out typed «?». The installed world's table (<see cref="WorldLimits.Types"/>) is what the game plays
/// with, so it goes first. Type names are the same eighteen either way.
/// </para>
/// </remarks>
public sealed class PkhexTypeLookup(string language = "es") : ITypeLookup
{
    private readonly GameStrings _strings = GameInfo.GetStrings(language);

    /// <summary>
    /// The form's own types when the installed world gives it a row: a Galarian Ponyta is Psychic, not
    /// Fire. PKHeX is not asked for forms, because its gen 7 table has no Galarian or Hisuian ones. §139.
    /// </summary>
    public TypePair GetTypes(int species, int form)
    {
        if (form > 0 && WorldLimits.TypesOf(species, form) is { } world)
        {
            return new TypePair(world.First, GetName(world.First), world.Second, GetName(world.Second));
        }

        return GetTypes(species);
    }

    public TypePair GetTypes(int species)
    {
        if (WorldLimits.TypesOf(species) is { } world)
        {
            return new TypePair(world.First, GetName(world.First), world.Second, GetName(world.Second));
        }

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
