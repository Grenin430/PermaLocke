using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>The types of every species out of a personal table, for the wonder trade of an expanded world (§121).</summary>
public sealed class PersonalTypesTests
{
    private const int Dragapult = 887;

    /// <summary>Types sit at 0x06 and 0x07 of each 0x54-byte entry; the result is indexed by species, two per species.</summary>
    [Fact]
    public void The_types_are_read_by_species()
    {
        var packed = new byte[(Dragapult + 1) * PersonalEntry7.Size];
        packed[(Dragapult * PersonalEntry7.Size) + 0x06] = 15;
        packed[(Dragapult * PersonalEntry7.Size) + 0x07] = 7;

        var types = PersonalEntry7.Types(packed, Dragapult);

        Assert.Equal((Dragapult + 1) * 2, types.Length);
        Assert.Equal(15, types[Dragapult * 2]);
        Assert.Equal(7, types[(Dragapult * 2) + 1]);
    }
}
