using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Types come from the world being played. PKHeX's gen 7 table stops at 807, and every Pokémon of the gen 8-9
/// expansion came out of a wonder trade typed «?» (§121).
/// </summary>
[Collection("WorldLimits")]
public sealed class WorldTypesTests : IDisposable
{
    private const int Dragapult = 887;
    private const int Charizard = 6;
    private const int Dragon = 15;
    private const int Ghost = 7;

    public void Dispose() => WorldLimits.Types = [];

    [Fact]
    public void Without_a_world_table_an_expansion_pokemon_has_no_types()
    {
        WorldLimits.Types = [];

        Assert.Equal("?", new PkhexTypeLookup().GetTypes(Dragapult).FirstName);
    }

    [Fact]
    public void With_the_world_table_Dragapult_is_Dragon_and_Ghost()
    {
        var types = new byte[(Dragapult + 1) * 2];
        types[Dragapult * 2] = Dragon;
        types[(Dragapult * 2) + 1] = Ghost;
        WorldLimits.Types = types;

        var pair = new PkhexTypeLookup().GetTypes(Dragapult);

        Assert.Equal(Dragon, pair.First);
        Assert.Equal(Ghost, pair.Second);
        Assert.True(pair.IsDual);
        Assert.NotEqual("?", pair.FirstName);
    }

    [Fact]
    public void The_cartridge_species_still_read_without_a_world_table()
    {
        WorldLimits.Types = [];

        var pair = new PkhexTypeLookup().GetTypes(Charizard);

        Assert.Equal(9, pair.First);
        Assert.Equal(2, pair.Second);
    }
}
