using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The history of the run in the sea (2026-10-10): the floor, the fossils and the lanterns tell what the run has cost and how far it has come.</summary>
public sealed class CapDeepTests
{
    private static CapPlaqueData Data(int cleared, int graves) =>
        CapSeaTests.Scenarios().First(s => s.Name == "completo").At(0) with
        {
            Cleared = cleared,
            Graves = [.. Enumerable.Range(0, graves).Select(i => new CapGrave(CapSeaTests.Icon(i + 3), "Caido" + i, "Rival", 10 + i, 1 + i))]
        };

    [Fact]
    public void The_floor_is_the_same_sea_in_both_windows_whatever_the_column_it_starts_at()
    {
        var layout = CapWorld.Arrange(CapMode.Column, CapPanelKind.Party, 180, 321, (0, 0, 0));
        var here = new CapSeaContext(Seed: 7421, WorldX: 0);
        var further = new CapSeaContext(Seed: 7421, WorldX: 250);

        // A column of one window is the column of the other at the same place of the sea: the floor runs on from one to the other.
        for (var x = 0; x < 120; x++) Assert.Equal(CapSea.Relief(layout, here, x + 250), CapSea.Relief(layout, further, x));

        // And another run has another floor.
        var other = new CapSeaContext(Seed: 99, WorldX: 0);
        Assert.Contains(Enumerable.Range(0, 180), x => CapSea.Relief(layout, here, x) != CapSea.Relief(layout, other, x));
    }

    [Fact]
    public void The_floor_never_leaves_the_window_and_a_fossil_raises_a_mound_under_it()
    {
        foreach (var (mode, cols, rows) in new[] { (CapMode.Column, 180, 321), (CapMode.Column, 132, 250), (CapMode.Slim, 100, 321), (CapMode.Strip, 636, 52), (CapMode.Compact, 70, 90) })
        {
            var layout = CapWorld.Arrange(mode, CapPanelKind.Deep, cols, rows, (0, 0, 0));
            var ctx = new CapSeaContext(Seed: 5);
            var fossils = CapSea.FossilColumns(layout, 6, ctx);
            Assert.Equal(6, fossils.Length);
            Assert.True(fossils.SequenceEqual(fossils.OrderBy(x => x)), "the fossils are in order");
            Assert.All(fossils, x => Assert.InRange(x, 0, cols - 1));

            for (var x = 0; x < cols; x++) Assert.InRange(CapSea.Relief(layout, ctx, x, fossils), 6, rows - 4);
            Assert.True(CapSea.Relief(layout, ctx, fossils[2], fossils) <= CapSea.Relief(layout, ctx, fossils[2], []), $"{mode}: no mound");
        }
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(3, 12)]
    [InlineData(7, 12)]
    [InlineData(12, 12)]
    public void The_strata_of_the_floor_are_the_trials_cleared_and_never_more_than_the_room_allows(int cleared, int trials)
    {
        var layout = CapWorld.Arrange(CapMode.Column, CapPanelKind.Deep, 180, 321, (0, 0, 0));
        var (strata, sand, rows) = CapDeep.StrataOf(layout, Data(cleared, 2) with { Trials = trials });

        Assert.Equal(cleared, strata);
        Assert.True(strata == 0 || rows >= 2);
        Assert.True(sand + (strata * rows) <= layout.Rows - layout.FloorTop + 8, "the strata fit under the floor");
    }

    [Fact]
    public void A_lantern_is_lit_for_each_trial_cleared_and_dark_for_the_rest()
    {
        var layout = CapWorld.Arrange(CapMode.Column, CapPanelKind.Deep, 180, 321, (0, 0, 0));
        foreach (var cleared in new[] { 0, 5, 12 })
        {
            var data = Data(cleared, 0) with { Trials = 12 };
            var frame = CapPlaque.Render(data, CapMode.Column, 180, 321, 0, CapPanelKind.Deep, new CapAnim());

            // The lanterns hang on the rope at the left of the chain, evenly: count the gold ones.
            var area = layout.Lanterns;
            var lit = 0;
            for (var k = 0; k < 12; k++)
            {
                var cy = area.Top + 8 + (int)(k * (area.Height - 20) / 11.0);
                var cx = area.Left + 6;
                var c = frame.At(cx - 1, cy + 1);
                var (r, g, b) = ((int)((c >> 16) & 255), (int)((c >> 8) & 255), (int)(c & 255));
                if (r > 220 && g > 160 && b < 160) lit++;
            }

            Assert.Equal(cleared, lit);
        }
    }

    [Fact]
    public void The_ear_of_the_deep_and_of_the_party_hold_the_floor_and_kelp_and_nothing_above_the_notch()
    {
        foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
        {
            var notch = CapSeaTests.NotchOf(panel, 207, 321, 27);
            var layout = CapWorld.Arrange(CapMode.Column, panel, 207, 321, notch);
            var frame = CapPlaque.Render(Data(7, 5), CapMode.Column, 207, 321, 5, panel, new CapAnim(), notch);

            // Below the notch the ear is sea, down to the floor and the strata: every cell is painted.
            var painted = 0;
            var total = 0;
            for (var y = notch.Item3 + 5; y < 321; y++)
            {
                for (var x = notch.Item1; x < notch.Item2; x++)
                {
                    total++;
                    painted += frame.Solid(x, y) ? 1 : 0;
                }
            }

            Assert.Equal(total, painted);
            Assert.True(layout.Main.Width == 207 - 27);
        }
    }
}
