using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>Writing a species into a static encounter, with and without a form.</summary>
public sealed class StaticFormTests
{
    private static readonly EncounterEntryLayout Layout = StaticEncounterTable.Statics;

    [Fact]
    public void The_ordinary_setter_clears_the_form()
    {
        var payload = new byte[Layout.Stride * 2];
        StaticEncounterTable.SetSpecies(payload, Layout, 1, 800, 3);

        StaticEncounterTable.SetSpecies(payload, Layout, 1, 25);

        Assert.Equal(25, StaticEncounterTable.GetSpecies(payload, Layout, 1));
        Assert.Equal(0, StaticEncounterTable.GetForm(payload, Layout, 1));
    }

    /// <summary>
    /// And the other one keeps it, which is the whole point: a mega is a form of its species.
    /// </summary>
    [Fact]
    public void The_form_setter_writes_both()
    {
        var payload = new byte[Layout.Stride * 2];

        StaticEncounterTable.SetSpecies(payload, Layout, 1, 6, 2);

        Assert.Equal(6, StaticEncounterTable.GetSpecies(payload, Layout, 1));
        Assert.Equal(2, StaticEncounterTable.GetForm(payload, Layout, 1));
    }

    /// <summary>Neither one touches the entry next door.</summary>
    [Fact]
    public void Writing_one_entry_leaves_the_others_alone()
    {
        var payload = new byte[Layout.Stride * 3];
        StaticEncounterTable.SetSpecies(payload, Layout, 0, 149, 1);
        StaticEncounterTable.SetSpecies(payload, Layout, 2, 384, 1);

        StaticEncounterTable.SetSpecies(payload, Layout, 1, 6, 2);

        Assert.Equal(149, StaticEncounterTable.GetSpecies(payload, Layout, 0));
        Assert.Equal(1, StaticEncounterTable.GetForm(payload, Layout, 0));
        Assert.Equal(384, StaticEncounterTable.GetSpecies(payload, Layout, 2));
    }
}
