using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Which row of the species table a form reads, and the forms that come out of it with their own base stats.
/// </summary>
/// <remarks>
/// The table built here mimics the cartridge's layout: species first, alternate forms after them. Species 2 has
/// two forms of its own in rows 4 and 5, species 3 has three forms that only look different and share its row.
/// </remarks>
public sealed class PersonalFormsTests
{
    private static byte[] Table()
    {
        var packed = new byte[6 * PersonalEntry7.Size];

        void Row(int row, params byte[] tableOrder)
        {
            for (var stat = 0; stat < tableOrder.Length; stat++)
            {
                packed[(row * PersonalEntry7.Size) + PersonalEntry7.StatOffsets[stat]] = tableOrder[stat];
            }
        }

        void Forms(int species, int from, int count)
        {
            BitConverter.GetBytes((ushort)from)
                .CopyTo(packed, (species * PersonalEntry7.Size) + PersonalEntry7.FormStatsIndexOffset);
            packed[(species * PersonalEntry7.Size) + PersonalEntry7.FormCountOffset] = (byte)count;
        }

        // En el orden de la tabla: PS, Ataque, Defensa, VELOCIDAD, At. Esp., Def. Esp.
        Row(1, 10, 20, 30, 40, 50, 60);
        Row(2, 60, 90, 55, 110, 90, 80);
        Row(3, 40, 40, 40, 40, 40, 40);
        Row(4, 60, 85, 50, 110, 95, 85);
        Row(5, 70, 70, 70, 70, 70, 70);

        Forms(2, from: 4, count: 3);
        Forms(3, from: 0, count: 3);

        return packed;
    }

    /// <summary>A form with a row of its own has that row's types; the wonder trade announces them (§139).</summary>
    [Fact]
    public void A_form_with_its_own_row_has_its_own_types()
    {
        var packed = Table();
        PersonalEntry7.SetTypes(packed, 2 * PersonalEntry7.Size, 9, 9);
        PersonalEntry7.SetTypes(packed, 4 * PersonalEntry7.Size, 14, 14);
        PersonalEntry7.SetTypes(packed, 5 * PersonalEntry7.Size, 14, 17);

        var types = PersonalEntry7.FormTypes(packed, 3);

        Assert.Equal(((byte)14, (byte)14), types[(2, 1)]);
        Assert.Equal(((byte)14, (byte)17), types[(2, 2)]);

        // Species 3 declares forms but has no rows for them: its own types, so nothing here.
        Assert.False(types.ContainsKey((3, 1)));
    }

    [Fact]
    public void A_form_the_species_declares_reads_its_own_row()
    {
        var packed = Table();

        Assert.Equal(2, PersonalEntry7.RowOf(packed, 2, 0));
        Assert.Equal(4, PersonalEntry7.RowOf(packed, 2, 1));
        Assert.Equal(5, PersonalEntry7.RowOf(packed, 2, 2));
    }

    /// <summary>The game's rule: past the declared count, or with no form rows at all, a form is built like its species.</summary>
    [Fact]
    public void Any_other_form_reads_the_species_row()
    {
        var packed = Table();

        Assert.Equal(2, PersonalEntry7.RowOf(packed, 2, 3));
        Assert.Equal(3, PersonalEntry7.RowOf(packed, 3, 2));
        Assert.Equal(1, PersonalEntry7.RowOf(packed, 1, 1));
    }

    [Fact]
    public void A_species_off_the_table_is_nobody()
    {
        Assert.Null(PersonalEntry7.RowOf(Table(), 9, 0));
        Assert.Null(PersonalEntry7.RowOf(Table(), 0, 0));
    }

    /// <summary>
    /// Only forms with a row of their own, and in the summary screen's order: the table's Velocidad, fourth, moves
    /// to the end.
    /// </summary>
    [Fact]
    public void The_form_table_holds_the_forms_with_their_own_row_in_screen_order()
    {
        var forms = PersonalEntry7.FormBaseStatsInScreenOrder(Table(), speciesCount: 3);

        Assert.Equal([(2, 1), (2, 2)], forms.Keys.OrderBy(key => key).ToArray());
        Assert.Equal([60, 85, 50, 95, 85, 110], forms[(2, 1)]);
        Assert.Equal([70, 70, 70, 70, 70, 70], forms[(2, 2)]);
    }
}
