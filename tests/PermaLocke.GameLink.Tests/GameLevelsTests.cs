using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// A level is computed, and the curve to compute it with belongs to the game being played.
/// </summary>
/// <remarks>
/// Anchored on a real measurement, not on a table lookup: a Dragapult in the player's party with
/// <b>53593</b> experience. That is 1.25 × 35³ to the unit — level 35 on the Slow curve — and the
/// game's own <c>Stat_Level</c> field said 35. PermaLocke said 37, because PKHeX's gen 7 table
/// stops at 807 and falls back to Medium Fast for everything the expansion mod adds.
/// </remarks>
[Collection("WorldLimits")]
public sealed class GameLevelsTests : IDisposable
{
    private const int Dragapult = 887;

    private const int Slow = 5;

    private const uint Measured = 53593;

    public void Dispose() => WorldLimits.GrowthRates = [];

    private static void WorldSays(int species, byte growth)
    {
        var rates = new byte[species + 1];
        rates[species] = growth;
        WorldLimits.GrowthRates = rates;
    }

    [Fact]
    public void The_measured_Dragapult_is_level_35_and_not_37()
    {
        var pokemon = new PK7 { Species = Dragapult, EXP = Measured };

        // Lo que se venía leyendo: la tabla de PKHeX no llega y cae en crecimiento Medio.
        Assert.Equal(37, pokemon.CurrentLevel);

        WorldSays(Dragapult, Slow);

        Assert.Equal(35, GameLevels.Of(pokemon));
    }

    /// <summary>
    /// Correcting to the cap has to mean the cap, not three levels below it.
    /// </summary>
    /// <remarks>
    /// The old write put «the experience for 34» on the wrong curve. On the Slow curve that same
    /// number is level 31, so a cap that was over by one took three levels away.
    /// </remarks>
    [Fact]
    public void Setting_the_cap_lands_on_the_cap()
    {
        WorldSays(Dragapult, Slow);

        var pokemon = new PK7 { Species = Dragapult, EXP = Measured };
        GameLevels.Set(pokemon, 34);

        Assert.Equal(34, GameLevels.Of(pokemon));

        // Y los dos campos de acuerdo: el juego lee uno u otro según dónde mire (§53).
        Assert.Equal(34, pokemon.Stat_Level);

        // Lo que habría escrito el código viejo, para que se vea el tamaño del error.
        Assert.Equal(31, Experience.GetLevel(Experience.GetEXP(34, 0), Slow));
    }

    /// <summary>Without a published table PKHeX is right, and is used unchanged.</summary>
    [Fact]
    public void A_plain_cartridge_species_is_left_to_PKHeX()
    {
        var pokemon = new PK7 { Species = (int)Species.Scyther, CurrentLevel = 29 };

        Assert.Equal(29, GameLevels.Of(pokemon));

        // Y una tabla que no cubre a esa especie tampoco inventa una curva.
        WorldSays(Dragapult, Slow);
        Assert.Equal(29, GameLevels.Of(pokemon));
    }
}
