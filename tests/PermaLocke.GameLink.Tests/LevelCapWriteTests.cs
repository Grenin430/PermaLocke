using PKHeX.Core;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// When the level cap is allowed to touch a slot of the party in memory.
/// </summary>
/// <remarks>
/// This file exists because the cap once raised a Pokémon instead of lowering it. The game keeps
/// the party in several structures and only one of them lays its battle stats out where a PK7 has
/// them; in the others <c>Stat_Level</c> lands on unrelated bytes and reads anything at all. A
/// version that trusted that field read <b>145</b> for a level 4 Ledyba, decided it was over the
/// cap of 24, wrote 24 — and the game evolved it into a Ledian.
/// </remarks>
public class LevelCapWriteTests
{
    private const uint Pid = 0x544CA127;

    /// <summary>
    /// The real Ledyba the bug hit: level 4 by experience, and the byte the reader took for
    /// <c>Stat_Level</c> holding 145.
    /// </summary>
    private static PK7 Ledyba(int level = 4, int bogusStatLevel = 145, uint pid = Pid)
    {
        var pokemon = new PK7
        {
            Species = (ushort)Species.Ledyba,
            PID = pid,
            OriginalTrainerName = "Grenin430",
            CurrentLevel = (byte)level,
        };

        // Lo que de verdad había en esa dirección: el nivel por experiencia es correcto y el campo
        // que el lector toma por Stat_Level es basura, porque ahí no está.
        pokemon.Stat_Level = (byte)bogusStatLevel;
        pokemon.RefreshChecksum();

        return pokemon;
    }

    /// <summary>The regression, stated as plainly as it can be: it must not be touched.</summary>
    [Fact]
    public void A_level_4_pokemon_is_never_capped_however_wild_the_stat_level_byte_reads()
    {
        var ledyba = Ledyba();

        Assert.Equal(4, ledyba.CurrentLevel);
        Assert.Equal(145, ledyba.Stat_Level);

        Assert.False(AzaharGameWriter.NeedsCapping(ledyba, Pid, cap: 24));
    }

    [Fact]
    public void Only_the_experience_decides_whether_it_is_over_the_cap()
    {
        Assert.True(AzaharGameWriter.NeedsCapping(Ledyba(level: 45), Pid, cap: 24));
        Assert.False(AzaharGameWriter.NeedsCapping(Ledyba(level: 24), Pid, cap: 24));
        Assert.False(AzaharGameWriter.NeedsCapping(Ledyba(level: 23), Pid, cap: 24));
    }

    /// <summary>
    /// A slot holding somebody else is left alone. Without this, a stale or misaligned copy gets
    /// corrected too, and what gets corrected is the Pokémon next door.
    /// </summary>
    [Fact]
    public void A_slot_holding_a_different_pokemon_is_left_alone()
    {
        var somebodyElse = Ledyba(level: 45, pid: 0xDEADBEEF);

        Assert.False(AzaharGameWriter.NeedsCapping(somebodyElse, Pid, cap: 24));
    }

    [Fact]
    public void Noise_is_not_a_pokemon()
    {
        Assert.False(AzaharGameWriter.NeedsCapping(null, Pid, cap: 24));

        var broken = Ledyba(level: 45);
        broken.Checksum = (ushort)(broken.Checksum + 1);

        Assert.False(AzaharGameWriter.NeedsCapping(broken, Pid, cap: 24));
    }
}
