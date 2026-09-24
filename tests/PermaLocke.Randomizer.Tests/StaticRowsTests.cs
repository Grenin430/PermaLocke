using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Finding a static encounter by what the cartridge holds there, which is how the allowed static captures of §119
/// are told apart from everything else in the table.
/// </summary>
public sealed class StaticRowsTests
{
    /// <summary>The first 0x08 bytes of real Ultra Moon rows: species, form, level, held item, then byte 0x07.</summary>
    private static byte[] Table(params string[] heads)
    {
        var stride = StaticEncounterTable.Statics.Stride;
        var payload = new byte[heads.Length * stride];

        for (var row = 0; row < heads.Length; row++)
        {
            Convert.FromHexString(heads[row]).CopyTo(payload, row * stride);
        }

        return payload;
    }

    /// <summary>Rows 99, 129, 135 and 161 of the cartridge, in that order.</summary>
    private static readonly byte[] Cartridge = Table(
        "2003004BFFFF1201",  // Necrozma forma 0, Nv 75: PKHeX no lo reconoce como encuentro de Ultra Luna
        "1103003CFFFF1201",  // Tapu Koko, Nv 60
        "1103003CFFFF1202",  // Tapu Koko, Nv 60 otra vez, solo cambia el byte 0x07
        "200300419B031201"); // Necrozma forma 0, Nv 65: el del Monte Lanakila

    [Fact]
    public void The_Lanakila_Necrozma_is_the_level_65_one_and_only_that()
    {
        Assert.Equal([3], StaticEncounterTable.RowsOf(Cartridge, StaticEncounterTable.Statics, 800, 0, 65));
    }

    [Fact]
    public void Two_identical_Tapu_Koko_both_come_back()
    {
        Assert.Equal([1, 2], StaticEncounterTable.RowsOf(Cartridge, StaticEncounterTable.Statics, 785, 0, 60));
    }

    [Fact]
    public void A_wrong_form_or_level_finds_nothing()
    {
        Assert.Empty(StaticEncounterTable.RowsOf(Cartridge, StaticEncounterTable.Statics, 800, 3, 65));
        Assert.Empty(StaticEncounterTable.RowsOf(Cartridge, StaticEncounterTable.Statics, 800, 0, 60));
    }

    [Fact]
    public void A_table_without_levels_cannot_be_asked()
    {
        Assert.Throws<ArgumentException>(() =>
            StaticEncounterTable.RowsOf(new byte[0x14], StaticEncounterTable.Gifts, 785, 0, 60));
    }
}
