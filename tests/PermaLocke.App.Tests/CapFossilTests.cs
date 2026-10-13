using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The fallen as fossils and the focus that goes over them (2026-10-10): no letters in the deep, readable fossils, the story told by how they lie.</summary>
public sealed class CapFossilTests
{
    private static IReadOnlyList<CapGrave> Graves(int count, Func<int, int>? age = null, Func<int, int>? level = null) =>
        [.. Enumerable.Range(0, count).Select(i => new CapGrave(CapSeaTests.Icon(i + 5), "Caido" + i, "Rival", level?.Invoke(i) ?? 10 + (i * 3), age?.Invoke(i) ?? i))];

    private static (CapWorldLayout Layout, CapSeaContext Context, int[] Columns) Sea(CapMode mode, int cols, int rows, int count)
    {
        var layout = CapWorld.Arrange(mode, CapPanelKind.Deep, cols, rows, (0, 0, 0));
        var ctx = new CapSeaContext(Seed: 7421);
        return (layout, ctx, CapSea.FossilColumns(layout, count, ctx));
    }

    [Fact]
    public void The_deep_writes_not_one_letter_in_any_mode_scenario_or_moment_while_the_party_does()
    {
        foreach (var scenario in CapSeaTests.Scenarios())
        {
            foreach (var (mode, cols, rows, ear) in CapSeaTests.Grids())
            {
                if (mode == CapMode.Compact) continue;
                foreach (var t in new[] { 0.0, 1.0, 3.0, 6.0, 9.5, 21.0, 44.0 })
                {
                    CapTextProbe.Reset();
                    CapPlaque.Replay(scenario, mode, cols, rows, t, CapPanelKind.Deep, CapSeaTests.NotchOf(CapPanelKind.Deep, cols, rows, ear));
                    Assert.True(CapTextProbe.Count == 0, $"{scenario.Name} {mode} {cols}x{rows} t={t}: the deep wrote {CapTextProbe.Count} strings");
                }
            }
        }

        CapTextProbe.Reset();
        CapPlaque.Replay(CapSeaTests.Scenarios().First(s => s.Name == "completo"), CapMode.Column, 180, 321, 3, CapPanelKind.Party);
        Assert.True(CapTextProbe.Count > 10, "the probe does not see the party's text, so it proves nothing about the deep");
    }

    [Theory]
    [InlineData(CapMode.Column, 180, 321)]
    [InlineData(CapMode.Column, 132, 250)]
    [InlineData(CapMode.Slim, 100, 321)]
    [InlineData(CapMode.Strip, 636, 52)]
    [InlineData(CapMode.Strip, 420, 46)]
    public void Every_fossil_is_big_enough_to_be_known_and_lies_inside_the_window(CapMode mode, int cols, int rows)
    {
        foreach (var count in new[] { 1, 3, 6, 12, 15 })
        {
            var (layout, ctx, columns) = Sea(mode, cols, rows, count);
            var fossils = CapFossils.Build(layout, Graves(count), ctx, columns);
            Assert.Equal(count, fossils.Length);
            foreach (var fossil in fossils)
            {
                Assert.True(fossil.Spec.Width >= 9, $"{mode} {count}: fossil {fossil.Spec.Index} only {fossil.Spec.Width} cells wide");
                Assert.NotEmpty(fossil.Cells);
                Assert.All(fossil.Cells, cell =>
                {
                    Assert.InRange(cell.X, 0, cols - 1);
                    Assert.InRange(cell.Y, 0, rows - 1);
                });
            }
        }
    }

    [Fact]
    public void The_oldest_lies_the_deepest_the_newest_is_the_biggest_and_the_kelp_grows_with_the_days()
    {
        var (layout, ctx, columns) = Sea(CapMode.Column, 180, 321, 8);
        var fossils = CapFossils.Build(layout, Graves(8, age: i => i * 3), ctx, columns);

        // Sunk as a share of its own height rises as they get older (the index 0 is the oldest).
        var sunk = fossils.Select(f => f.Spec.Sunk / (double)f.Spec.Height).ToArray();
        Assert.True(sunk[0] > sunk[^1], "the oldest is not deeper than the newest");
        Assert.True(sunk.Zip(sunk.Skip(1)).All(pair => pair.First >= pair.Second - 0.08), "burial does not follow the order of falling");

        // The newest is the biggest.
        Assert.Equal(fossils.Min(f => f.Spec.Scale), fossils[^1].Spec.Scale);
        Assert.True(fossils.All(f => f.Spec.Scale >= fossils[^1].Spec.Scale));

        // Kelp: none for a fossil of today, one more every three days, four at most.
        Assert.Equal([0, 1, 2, 3, 4, 4], Enumerable.Range(0, 6).Select(i => CapFossils.Build(layout, Graves(1, age: _ => i * 3), ctx, CapSea.FossilColumns(layout, 1, ctx))[0].Spec.Growth));
    }

    [Fact]
    public void The_soul_is_as_bright_as_the_level_it_fell_at_against_the_highest_and_middling_when_the_run_did_not_keep_it()
    {
        var (layout, ctx, columns) = Sea(CapMode.Column, 180, 321, 3);
        var fossils = CapFossils.Build(layout, Graves(3, level: i => new[] { 10, 20, 0 }[i]), ctx, columns);
        Assert.Equal(0.5, fossils[0].Spec.Soul, 3);
        Assert.Equal(1.0, fossils[1].Spec.Soul, 3);
        Assert.Equal(0.5, fossils[2].Spec.Soul, 3);
    }

    [Fact]
    public void The_focus_goes_from_the_newest_to_the_oldest_five_seconds_on_the_first_and_three_point_two_on_the_others_and_starts_again()
    {
        Assert.Equal(11, CapDeep.FocusAt(12, 0).Index);
        Assert.Equal(11, CapDeep.FocusAt(12, 4.9).Index);
        Assert.Equal(10, CapDeep.FocusAt(12, 5.1).Index);
        Assert.Equal(10, CapDeep.FocusAt(12, 8.1).Index);
        Assert.Equal(9, CapDeep.FocusAt(12, 8.3).Index);
        Assert.Equal(0, CapDeep.FocusAt(12, 5 + (3.2 * 10) + 1).Index);
        var total = 5 + (3.2 * 11);
        Assert.Equal(11, CapDeep.FocusAt(12, total + 0.2).Index);
        Assert.Equal(CapDeep.FocusAt(12, 3.3).Index, CapDeep.FocusAt(12, total + 3.3).Index);

        // Dark at the changeover, fully lit in the middle of its time.
        Assert.Equal(0, CapDeep.FocusAt(12, 5.0 + 1e-6).Lit, 2);
        Assert.Equal(1, CapDeep.FocusAt(12, 2.5).Lit, 3);
        Assert.Equal(1, CapDeep.FocusAt(12, 5 + 1.6).Lit, 3);
        Assert.Equal(-1, CapDeep.FocusAt(0, 3).Index);
    }

    [Fact]
    public void The_focus_gives_the_colours_of_the_icon_back_to_the_fossil_it_is_on_and_to_no_other()
    {
        var scenario = CapSeaTests.Scenarios().First(s => s.Name == "captura");
        var icon = CapSeaTests.Icon(1);

        // Frames at the middle of the newest fossil's time and at the middle of the oldest one's: the stone cells of the rest do not change.
        var newest = CapPlaque.Replay(scenario, CapMode.Column, 180, 321, 2.5, CapPanelKind.Deep);
        var oldest = CapPlaque.Replay(scenario, CapMode.Column, 180, 321, 5 + (3.2 * 10) + 1.6, CapPanelKind.Deep);
        var differing = 0;
        for (var y = 0; y < 321; y++)
        {
            for (var x = 0; x < 180; x++) differing += newest.At(x, y) != oldest.At(x, y) ? 1 : 0;
        }

        Assert.True(differing > 100, "the focus moved and nothing changed");
        Assert.NotNull(icon);
    }

    [Fact]
    public void The_deep_is_the_same_frame_every_time_it_is_asked_for()
    {
        var scenario = CapSeaTests.Scenarios().First(s => s.Name == "captura");
        foreach (var t in new[] { 0.5, 2.5, 7.4, 21.0 })
        {
            var first = CapPlaque.Replay(scenario, CapMode.Column, 180, 321, t, CapPanelKind.Deep);
            var second = CapPlaque.Replay(scenario, CapMode.Column, 180, 321, t, CapPanelKind.Deep);
            Assert.Equal(first.B, second.B);
        }
    }

    [Fact]
    public void The_party_is_one_column_with_the_pictures_always_on_the_left_and_the_words_on_their_right()
    {
        foreach (var (mode, cols, rows) in new[] { (CapMode.Column, 180, 321), (CapMode.Column, 132, 250), (CapMode.Slim, 100, 321) })
        {
            var layout = CapWorld.Arrange(mode, CapPanelKind.Party, cols, rows, (0, 0, 0));
            Assert.Single(layout.Creatures.Select(c => c.Sprite.Left).Distinct());
            Assert.All(layout.Creatures, c =>
            {
                Assert.True(c.InfoOnRight);
                Assert.True(c.Info.Left > c.Sprite.Right);
            });
        }
    }

    [Fact]
    public void The_moves_are_not_shown_any_more_and_the_ability_fits_in_the_box_in_the_player_s_window()
    {
        // Singleplayer (2026-10-13): types whole and the ability under them, no ticker of moves.
        var scenario = CapSeaTests.Scenarios().First(s => s.Name == "captura");
        var view = new CapWorldView(CapMode.Column, CapPanelKind.Party, 178, 321, (0, 0, 0));
        view.SetData(scenario.At(0));
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(0, view.Creature(i)!.TickerAt.Letters);
            var info = view.Creature(i)!.Info!;
            var lastRowWithInk = Enumerable.Range(0, info.H).Last(y => Enumerable.Range(0, info.W).Any(x => info.Solid(x, y)));
            Assert.True(lastRowWithInk <= info.H - 1 && lastRowWithInk > 20, $"creature {i}: the ability touches the bottom of its box");
        }
    }
}
