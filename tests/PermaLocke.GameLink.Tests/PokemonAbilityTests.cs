using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The ability of a Pokémon with its ninth bit, where the expansion mod keeps it: bit 4 of 0x15.
/// </summary>
/// <remarks>
/// §134, disassembled from the block the mod adds to <c>code.bin</c>: the game stores the low byte
/// at 0x14 and sets or clears 0x10 at 0x15, whose low bits are the ability slot.
/// </remarks>
public sealed class PokemonAbilityTests
{
    private const int CambioHeroico = 278;

    [Fact]
    public void A_new_ability_keeps_its_ninth_bit()
    {
        var pokemon = new PK7 { Species = 964, AbilityNumber = 1 };

        PokemonAbility.Set(pokemon, CambioHeroico);

        Assert.Equal(CambioHeroico, PokemonAbility.Of(pokemon));
        Assert.Equal(0x16, pokemon.Data[0x14]);
        Assert.Equal(0x11, pokemon.Data[0x15]);

        // Lo que leía todo lo demás: un byte, que nombra otra habilidad (22, Imán).
        Assert.Equal(22, pokemon.Ability);
    }

    /// <summary>The slot lives in the same byte, and neither write disturbs the other.</summary>
    [Fact]
    public void The_ability_slot_survives_in_both_directions()
    {
        var pokemon = new PK7 { AbilityNumber = 4 };

        PokemonAbility.Set(pokemon, CambioHeroico);
        Assert.Equal(4, pokemon.AbilityNumber);

        pokemon.AbilityNumber = 2;
        Assert.Equal(CambioHeroico, PokemonAbility.Of(pokemon));
    }

    [Fact]
    public void An_old_ability_clears_the_bit_a_new_one_left()
    {
        var pokemon = new PK7 { AbilityNumber = 1 };
        PokemonAbility.Set(pokemon, CambioHeroico);

        PokemonAbility.Set(pokemon, 22);

        Assert.Equal(22, PokemonAbility.Of(pokemon));
        Assert.Equal(1, pokemon.Data[0x15]);
    }

    [Fact]
    public void A_cartridge_pokemon_reads_the_same_as_before()
    {
        var pokemon = new PK7 { Ability = 65, AbilityNumber = 2 };

        Assert.Equal(65, PokemonAbility.Of(pokemon));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(512)]
    public void What_one_byte_and_one_bit_cannot_hold_is_refused(int ability)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PokemonAbility.Set(new PK7(), ability));
    }
}
