using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class StaticEncounterTableTests
{
    /// <summary>Entry strides read off the real cartridge: 35 gifts, 252 statics, 7 trades.</summary>
    [Theory]
    [InlineData(0x14, 700, 35)]
    [InlineData(0x38, 14112, 252)]
    [InlineData(0x34, 364, 7)]
    public void The_strides_match_the_real_subfile_sizes(int stride, int payloadLength, int expected)
    {
        var layout = new EncounterEntryLayout(0, stride, 0x00, 0x02, "prueba");
        Assert.Equal(expected, StaticEncounterTable.Count(new byte[payloadLength], layout));
    }

    [Fact]
    public void Species_survives_a_round_trip()
    {
        var payload = new byte[StaticEncounterTable.Gifts.Stride * 4];
        StaticEncounterTable.SetSpecies(payload, StaticEncounterTable.Gifts, 2, 722);

        Assert.Equal(722, StaticEncounterTable.GetSpecies(payload, StaticEncounterTable.Gifts, 2));
    }

    /// <summary>
    /// A form index valid for the old species need not be valid for the new one, so it is reset.
    /// Leaving an Alolan form number on a species with no forms is how you get a broken model.
    /// </summary>
    [Fact]
    public void Writing_a_species_clears_the_form()
    {
        var payload = new byte[StaticEncounterTable.Gifts.Stride];
        payload[StaticEncounterTable.Gifts.FormOffset] = 3;

        StaticEncounterTable.SetSpecies(payload, StaticEncounterTable.Gifts, 0, 129);

        Assert.Equal(0, StaticEncounterTable.GetForm(payload, StaticEncounterTable.Gifts, 0));
    }

    [Fact]
    public void Writing_one_entry_leaves_its_neighbours_alone()
    {
        var layout = StaticEncounterTable.Statics;
        var payload = new byte[layout.Stride * 3];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i * 5);
        }
        var before = (byte[])payload.Clone();

        StaticEncounterTable.SetSpecies(payload, layout, 1, 400);

        for (var i = 0; i < payload.Length; i++)
        {
            var inEntry1 = i >= layout.Stride && i < layout.Stride * 2;
            var isSpeciesOrForm = i == layout.Stride + layout.SpeciesOffset
                                  || i == layout.Stride + layout.SpeciesOffset + 1
                                  || i == layout.Stride + layout.FormOffset
                                  // Los cuatro movimientos del cartucho se borran con la especie (2026-09-27).
                                  || (i >= layout.Stride + 0x0C && i < layout.Stride + 0x14);
            if (inEntry1 && isSpeciesOrForm)
            {
                continue;
            }
            Assert.True(payload[i] == before[i], $"el byte 0x{i:X} cambió y no debía");
        }
    }

    /// <summary>
    /// A trade carries the species you receive at 0x0 and the one you must hand over at 0x2.
    /// Randomizing the requirement would leave a trade nobody can complete.
    /// </summary>
    [Fact]
    public void A_trade_keeps_the_species_it_asks_for()
    {
        var payload = new byte[StaticEncounterTable.Trades.Stride];
        BitConverter.GetBytes((ushort)25).CopyTo(payload, 0x0);   // received
        BitConverter.GetBytes((ushort)133).CopyTo(payload, 0x2);  // required

        StaticEncounterTable.SetSpecies(payload, StaticEncounterTable.Trades, 0, 700);

        Assert.Equal(700, BitConverter.ToUInt16(payload, 0x0));
        Assert.Equal(133, BitConverter.ToUInt16(payload, 0x2));
    }

    [Fact]
    public void The_layouts_point_at_the_subfiles_the_cartridge_uses()
    {
        Assert.Equal(0, StaticEncounterTable.Gifts.Subfile);
        Assert.Equal(1, StaticEncounterTable.Statics.Subfile);
        Assert.Equal(4, StaticEncounterTable.Trades.Subfile);
        Assert.Equal(3, StaticEncounterTable.StarterCount);
    }

    [Fact]
    public void A_new_species_in_a_static_takes_its_own_moves_not_the_cartridges()
    {
        var layout = StaticEncounterTable.Statics;
        var payload = new byte[layout.Stride * 2];
        for (var i = 0; i < 8; i++) payload[layout.Stride + 0x0C + i] = 0x55;

        StaticEncounterTable.SetSpecies(payload, layout, 1, 25, 0);

        Assert.All(payload.AsSpan(layout.Stride + 0x0C, 8).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(25, StaticEncounterTable.GetSpecies(payload, layout, 1));

        // Los regalos no llevan movimientos en ese sitio: no se toca nada más.
        var gifts = new byte[StaticEncounterTable.Gifts.Stride];
        gifts[0x0C] = 0x55;
        StaticEncounterTable.SetSpecies(gifts, StaticEncounterTable.Gifts, 0, 25);
        Assert.Equal(0x55, gifts[0x0C]);
    }
}
