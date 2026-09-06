using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Whether a party entry's tail really holds the battle stats, which decides how far a write may
/// reach.
/// </summary>
/// <remarks>
/// <para>
/// The numbers here are the real measurement of 2026-09-06, taken with the player's Gyarados on
/// screen at <b>118 of 131</b>. Two structures in memory, <b>the same 0x104 stride</b>, read at the
/// same second: <c>0x330128E4</c> gave 118/131 and level 42, and <c>0x3254EE60</c> gave 42649/10902
/// and level 54. So the stride, which is what the code used to ask, does not answer the question.
/// </para>
/// <para>
/// What answers it is that a party Pokémon carries its level <b>twice</b> — as experience inside
/// the encrypted block, which the checksum vouches for, and as <c>Stat_Level</c> in the tail. Where
/// the tail is genuine they agree.
/// </para>
/// </remarks>
public class PartyStatsTests
{
    private const uint Pid = 0x2A15A6EF;

    /// <summary>The Gyarados as the good structure held it: level 42, hurt, everything sane.</summary>
    private static PK7 Gyarados(int level = 42, int statLevel = 42, int hp = 118, int max = 131)
    {
        var pokemon = new PK7
        {
            Species = (ushort)Species.Gyarados,
            PID = Pid,
            OriginalTrainerName = "Grenin430",
            CurrentLevel = (byte)level
        };

        pokemon.Stat_Level = (byte)statLevel;
        pokemon.Stat_HPCurrent = hp;
        pokemon.Stat_HPMax = max;
        pokemon.Stat_ATK = 130;
        pokemon.Stat_DEF = 98;
        pokemon.Stat_SPE = 87;
        pokemon.Stat_SPA = 70;
        pokemon.Stat_SPD = 111;
        pokemon.RefreshChecksum();

        return pokemon;
    }

    [Fact]
    public void The_structure_that_matched_the_screen_is_accepted()
    {
        Assert.True(PartyStats.AreHere(Gyarados()));
    }

    /// <summary>
    /// The other copy of the same Gyarados, same stride, at the same moment. Every number in it is
    /// what was actually read.
    /// </summary>
    [Fact]
    public void The_copy_that_read_42649_of_10902_is_refused()
    {
        Assert.False(PartyStats.AreHere(Gyarados(statLevel: 54, hp: 42649, max: 10902)));
    }

    /// <summary>The level disagreeing is enough on its own, even with believable stats.</summary>
    [Fact]
    public void A_tail_whose_level_contradicts_the_experience_is_refused()
    {
        Assert.False(PartyStats.AreHere(Gyarados(statLevel: 43)));
    }

    /// <summary>More HP than the maximum is nobody's HP.</summary>
    [Fact]
    public void Current_hp_above_the_maximum_is_refused()
    {
        Assert.False(PartyStats.AreHere(Gyarados(hp: 132)));
    }

    /// <summary>
    /// A fainted Pokémon is still a perfectly good party entry, and this is the case that matters:
    /// it is exactly the state the run puts its dead in, and refusing it would mean the entry could
    /// never be read back to confirm the write.
    /// </summary>
    [Fact]
    public void A_pokemon_on_the_floor_is_still_a_real_party_entry()
    {
        Assert.True(PartyStats.AreHere(Gyarados(hp: 0)));
    }

    [Fact]
    public void Nothing_and_a_broken_block_are_refused()
    {
        Assert.False(PartyStats.AreHere(null));

        var broken = Gyarados();
        broken.Species = (ushort)Species.Magikarp; // sin RefreshChecksum: el bloque deja de cuadrar

        Assert.False(PartyStats.AreHere(broken));
    }
}
