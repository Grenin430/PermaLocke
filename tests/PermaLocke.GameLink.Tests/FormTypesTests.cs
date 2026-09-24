using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Types by form, which is what the wonder trade announces (§139).
/// </summary>
/// <remarks>WorldLimits is global state: this assembly runs its classes one at a time.</remarks>
[Collection("WorldLimits")]
public sealed class FormTypesTests : IDisposable
{
    private const int Vulpix = 37;
    private const byte Fire = 9;
    private const byte Ice = 14;

    public FormTypesTests()
    {
        var types = new byte[(Vulpix + 1) * 2];
        types[Vulpix * 2] = Fire;
        types[(Vulpix * 2) + 1] = Fire;
        WorldLimits.Types = types;
        WorldLimits.FormTypes = new Dictionary<(int Species, int Form), (byte First, byte Second)>
        {
            [(Vulpix, 1)] = (Ice, Ice)
        };
    }

    public void Dispose()
    {
        WorldLimits.Types = [];
        WorldLimits.FormTypes = new Dictionary<(int Species, int Form), (byte First, byte Second)>();
    }

    [Fact]
    public void An_alolan_vulpix_is_ice_and_a_kantonian_one_fire()
    {
        Assert.Equal((Ice, Ice), WorldLimits.TypesOf(Vulpix, 1));
        Assert.Equal((Fire, Fire), WorldLimits.TypesOf(Vulpix, 0));
    }

    /// <summary>A form without a row of its own is built like its species, by the game's rule.</summary>
    [Fact]
    public void A_form_without_its_own_row_has_its_species_types()
    {
        Assert.Equal((Fire, Fire), WorldLimits.TypesOf(Vulpix, 2));
    }

    [Fact]
    public void The_type_lookup_names_the_forms_types()
    {
        var types = new PkhexTypeLookup().GetTypes(Vulpix, 1);

        Assert.Equal(Ice, types.First);
        Assert.Equal("Hielo", types.FirstName);
    }

    /// <summary>The form reaches the Pokémon that goes into the save.</summary>
    [Fact]
    public void A_regional_form_arrives_as_that_form()
    {
        var save = new SAV7USUM { OT = "Grenin", TID16 = 1, SID16 = 2, Language = (int)LanguageID.Spanish };

        var pokemon = PokemonBuilder.Build(new NewPokemon(Vulpix, 20, 0, 0, [31, 31, 31, 31, 31, 31], false, 1), save);

        Assert.Equal(1, pokemon.Form);
        Assert.True(pokemon.ChecksumValid);
    }
}
