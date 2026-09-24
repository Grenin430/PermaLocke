using PermaLocke.GameLink.Battle;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>The Pokémon behind a battle block, read from a real wild battle.</summary>
public sealed class BattlePokemonTests
{
    /// <summary>
    /// 296 bytes read from 0x3002FC60, the pointer of the rival's block, in a wild battle against a shiny Wooloo
    /// (species 831, level 8) on 2026-09-14, with the always-shiny test patch on. The same Pokémon was at +0x40 of the
    /// other table's pointer.
    /// </summary>
    private static readonly byte[] ShinyWooloo = Convert.FromHexString(
        "48736400A0FC0230F8FD0230B8FD0230445500000801000030FC023088FD0230E7FFFFFF200000000000000000000000" +
        "00000000000000000000000000000000CF059F8200009955AB1D34B18CDBFACBF7115695AA561D291D4C6963D8CB897C" +
        "55B08F99AA15C629119917741C6830CDA8FCD834B9410B4F9E442E560A5ADAD851CB796D8DC39DCBFFEF8F530BF4498D" +
        "C578224C228E933A8FC5EA4AFC1AD59E5F9047EFC6059DEA4A5AD39E66C89333158BCA48AD920C5FF125353168139300" +
        "297B5BFC1B4B85E0BA11753ECDD89C89A2FAA4A7DD7C23745D1425FBC5E714D3AC02C3457E41A8B197DE8C0722C33156" +
        "B5E2C744D24626FEAA9724B2D9F17F47F1443570115D43DBDBA92E0A85DF5E2DD74286AB57DFB765F39ED7C1E34481C6" +
        "938BA77B25ACD123");

    [Fact]
    public void The_wild_pokemon_is_at_0x40_from_the_pointer_and_says_it_is_shiny()
    {
        var wooloo = BattlePokemon.Parse(ShinyWooloo, species: 831);

        Assert.NotNull(wooloo);
        Assert.Equal(8, wooloo.CurrentLevel);
        Assert.Equal(0x7D64F0DEu, wooloo.PID);
        Assert.True(wooloo.IsShiny);

        // Con el TID y el SID de la ficha del jugador, 4283 y 40193: el salvaje ya los lleva.
        Assert.Equal(4283, wooloo.TID16);
        Assert.Equal(40193, wooloo.SID16);
    }

    [Fact]
    public void Another_species_than_the_block_says_is_not_believed()
    {
        Assert.Null(BattlePokemon.Parse(ShinyWooloo, species: 190));
    }

    [Fact]
    public void Bytes_that_are_not_a_pokemon_are_not_believed()
    {
        var broken = (byte[])ShinyWooloo.Clone();
        broken[BattlePokemon.Offset + 0x20] ^= 0xFF;

        Assert.Null(BattlePokemon.Parse(broken, species: 831));
    }

    [Fact]
    public void Too_few_bytes_are_nothing()
    {
        Assert.Null(BattlePokemon.Parse(ShinyWooloo.AsSpan(0, 200), species: 831));
    }
}
