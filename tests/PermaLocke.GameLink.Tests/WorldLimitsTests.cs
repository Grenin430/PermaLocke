using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The species ceiling the live readers use to tell a Pokémon from heap rubbish.
/// </summary>
/// <remarks>
/// The tests restore the default afterwards because the value is process-wide: a test that left it
/// raised would make every other test in the assembly accept species the cartridge cannot hold,
/// and they would pass for the wrong reason.
/// </remarks>
[Collection("WorldLimits")]
public sealed class WorldLimitsTests : IDisposable
{
    public void Dispose() => WorldLimits.MaxSpecies = WorldLimits.CartridgeMaxSpecies;

    [Fact]
    public void Por_defecto_es_el_cartucho()
    {
        Assert.Equal(807, WorldLimits.CartridgeMaxSpecies);
        Assert.Equal(807, WorldLimits.MaxSpecies);

        Assert.True(WorldLimits.IsKnownSpecies(807));
        Assert.False(WorldLimits.IsKnownSpecies(808));
    }

    [Fact]
    public void Cero_y_negativos_nunca_son_una_especie()
    {
        Assert.False(WorldLimits.IsKnownSpecies(0));
        Assert.False(WorldLimits.IsKnownSpecies(-1));

        WorldLimits.MaxSpecies = 1025;

        Assert.False(WorldLimits.IsKnownSpecies(0));
        Assert.False(WorldLimits.IsKnownSpecies(-1));
    }

    [Fact]
    public void Con_el_mod_instalado_llega_hasta_la_1025()
    {
        WorldLimits.MaxSpecies = 1025;

        Assert.True(WorldLimits.IsKnownSpecies(808));    // Meltan
        Assert.True(WorldLimits.IsKnownSpecies(1025));   // Pecharunt
        Assert.False(WorldLimits.IsKnownSpecies(1026));
    }

    /// <summary>
    /// Below the cartridge's own count is a mistake, and it has to be loud.
    /// </summary>
    /// <remarks>
    /// The number that belongs here is what the <b>game</b> can hold. Setting it to a randomizer cap
    /// — "this run only uses the first generation" — would make the live readers throw away every
    /// real Pokémon above that cap as if it were rubbish, and nothing would report it: no death
    /// counted, no capture detected, the §68 silence all over again.
    /// </remarks>
    [Fact]
    public void Por_debajo_del_cartucho_se_niega_en_vez_de_recortar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldLimits.MaxSpecies = 151);
        Assert.Equal(807, WorldLimits.MaxSpecies);
    }
}
