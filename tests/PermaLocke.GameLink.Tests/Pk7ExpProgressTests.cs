using PKHeX.Core;
using PermaLocke.GameLink.Data;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>How far a party Pokémon is from its next level, for the cap panel's experience bar (2026-10-10).</summary>
public sealed class Pk7ExpProgressTests
{
    private const int Pikachu = 25;

    private static PK7 At(int level, double share = 0)
    {
        var growth = PersonalTable.USUM[Pikachu].EXPGrowth;
        var floor = Experience.GetEXP((byte)level, growth);
        var next = Experience.GetEXP((byte)(level + 1), growth);
        return new PK7 { Species = Pikachu, EXP = (uint)(floor + ((next - floor) * share)) };
    }

    [Theory]
    [InlineData(5, 0.0)]
    [InlineData(20, 0.5)]
    [InlineData(42, 0.9)]
    public void The_share_is_where_the_experience_sits_between_this_level_and_the_next(int level, double share)
    {
        Assert.Equal(share, Pk7Reader.ExpProgress(At(level, share), level), 2);
    }

    [Fact]
    public void A_hundred_is_full_and_an_experience_that_disagrees_with_the_level_says_nothing()
    {
        Assert.Equal(1, Pk7Reader.ExpProgress(At(99, 0.5), 100));

        // Experience of level 30 read as if the Pokémon were level 40: the structure is not what was thought.
        Assert.Equal(-1, Pk7Reader.ExpProgress(At(30, 0.5), 40));
    }

    [Fact]
    public void A_species_beyond_the_cartridge_without_a_world_table_says_nothing_rather_than_guess_a_curve()
    {
        var pokemon = new PK7 { Species = 900, EXP = 5000 };
        Assert.Equal(-1, Pk7Reader.ExpProgress(pokemon, 20));
    }
}
