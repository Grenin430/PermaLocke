using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// What PermaLocke hands to the player's game, gacha and wonder trade alike.
/// </summary>
[Collection("WorldLimits")]
public sealed class PokemonBuilderTests
{
    private static NewPokemon Spec(int species = 25, int level = 20) =>
        new(species, level, Nature: 0, AbilityId: 0, Ivs: [31, 20, 15, 10, 5, 0], IsShiny: false);

    private static SAV7USUM Save(int language = (int)LanguageID.Spanish) =>
        new() { OT = "Grenin", TID16 = 12345, SID16 = 54321, Language = language };

    /// <summary>
    /// The nickname field is not a fallback: the game shows what is in it, so leaving it blank
    /// meant everything PermaLocke delivered arrived nameless.
    /// </summary>
    [Fact]
    public void It_arrives_with_its_species_name()
    {
        var pokemon = PokemonBuilder.Build(Spec(species: 25), Save());

        Assert.Equal("Pikachu", pokemon.Nickname);
        Assert.False(pokemon.IsNicknamed);
    }

    /// <summary>The name follows the save's language, not PermaLocke's.</summary>
    [Fact]
    public void The_name_is_in_the_language_of_the_save()
    {
        var french = PokemonBuilder.Build(Spec(species: 4), Save((int)LanguageID.French));
        var spanish = PokemonBuilder.Build(Spec(species: 4), Save());

        Assert.Equal("Salamèche", french.Nickname);
        Assert.Equal("Charmander", spanish.Nickname);
    }

    /// <summary>
    /// Not marked as nicknamed, so the game keeps treating it as the plain species name and
    /// renames it on evolution instead of leaving the old name stuck to it.
    /// </summary>
    [Fact]
    public void A_delivered_pokemon_is_never_marked_as_nicknamed()
    {
        foreach (var species in (int[])[1, 150, 493, 807])
        {
            var pokemon = PokemonBuilder.Build(Spec(species), Save());

            Assert.False(pokemon.IsNicknamed);
            Assert.False(string.IsNullOrWhiteSpace(pokemon.Nickname));
        }
    }

    /// <summary>Naming must not disturb what was already right: it is the player's own Pokémon.</summary>
    [Fact]
    public void It_is_still_the_players_own_pokemon()
    {
        var save = Save();

        var pokemon = PokemonBuilder.Build(Spec(species: 25, level: 33), save);

        Assert.Equal(save.OT, pokemon.OriginalTrainerName);
        Assert.Equal(save.TID16, pokemon.TID16);
        Assert.Equal(save.SID16, pokemon.SID16);
        Assert.Equal(33, pokemon.CurrentLevel);
        Assert.True(pokemon.ChecksumValid);
    }

    /// <summary>
    /// A Pokémon of the mod arrives at the level it was asked for, on its own curve.
    /// </summary>
    /// <remarks>
    /// §134: the level went through PKHeX, whose gen 7 table stops at 807 and gives everything past
    /// it Medium Fast. A Dragapult — Slow — asked for at level 40 got the experience for 40 on the
    /// wrong curve and arrived at 37, and the delivery's own check read it back with the same wrong
    /// curve, so it passed.
    /// </remarks>
    [Fact]
    public void A_mod_species_arrives_at_the_level_asked_for()
    {
        const int Dragapult = 887;
        const byte Slow = 5;

        try
        {
            var rates = new byte[Dragapult + 1];
            rates[Dragapult] = Slow;
            WorldLimits.GrowthRates = rates;

            var pokemon = PokemonBuilder.Build(Spec(species: Dragapult, level: 40), Save());

            Assert.Equal(40, GameLevels.Of(pokemon));
            Assert.Equal(Experience.GetEXP(40, Slow), pokemon.EXP);
        }
        finally
        {
            WorldLimits.GrowthRates = [];
        }
    }

    /// <summary>An ability of the mod arrives whole, ninth bit included.</summary>
    [Fact]
    public void A_mod_ability_arrives_whole()
    {
        var spec = Spec(species: 25) with { AbilityId = 278 };

        var pokemon = PokemonBuilder.Build(spec, Save());

        Assert.Equal(278, PokemonAbility.Of(pokemon));
        Assert.Equal(1, pokemon.AbilityNumber);
        Assert.True(pokemon.ChecksumValid);
    }
}
