using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The moving part of the cap panel (2026-10-10): it stays in its place, it is the same whenever it is asked for, and it shows what happened.</summary>
public sealed class CapLiveTests
{
    private static IReadOnlyList<CapScenario> Scenarios() => CapPlaqueTests.Scenarios();

    private static IEnumerable<(CapMode Mode, int Cols, int Rows)> SomeGrids() =>
    [
        (CapMode.Column, 178, 321), (CapMode.Column, 132, 250), (CapMode.Slim, 100, 321), (CapMode.Compact, 70, 90), (CapMode.Strip, 636, 52)
    ];

    private static bool Red(CapSheet s, int x, int y)
    {
        var c = s.At(x, y);
        int r = (int)((c >> 16) & 255), g = (int)((c >> 8) & 255), b = (int)(c & 255);
        return s.Solid(x, y) && r > g + 50 && r > b + 50;
    }

    [Fact]
    public void The_moving_part_draws_only_inside_the_case_and_over_the_cup()
    {
        foreach (var scenario in Scenarios())
        {
            foreach (var (mode, cols, rows) in SomeGrids())
            {
                foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
                {
                    if (panel == CapPanelKind.Deep && mode == CapMode.Compact) continue;

                    var data = scenario.At(0);
                    var layout = CapComposer.Arrange(mode, panel, cols, rows, data, true);
                    var still = new CapSheet(cols, rows);
                    CapComposer.Paint(still, layout, data);

                    foreach (var t in new[] { 0.0, 1.0, 2.1, 2.3, 3.0, 5.0, 8.0, 21.0 })
                    {
                        var frame = CapPlaque.Replay(scenario, mode, cols, rows, t, panel);
                        for (var y = 0; y < rows; y++)
                        {
                            for (var x = 0; x < cols; x++)
                            {
                                if (still.Solid(x, y) || !frame.Solid(x, y)) continue;

                                var overTheCup = layout.HasCup && y < layout.Top && Math.Abs(x - (cols / 2)) <= 10;
                                Assert.True(overTheCup, $"{scenario.Name} {mode} {panel} t={t}: a cell at {x},{y} outside the case");
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Every_moment_of_every_scenario_is_the_same_whenever_it_is_asked_for_and_in_any_order()
    {
        foreach (var scenario in Scenarios())
        {
            var times = new[] { 0.0, 2.05, 2.4, 3.1, 6.0, 12.0, 30.0 };
            var first = times.Select(t => CapPlaque.Replay(scenario, CapMode.Column, 178, 321, t).B.ToArray()).ToList();
            var again = times.Reverse().Select(t => CapPlaque.Replay(scenario, CapMode.Column, 178, 321, t).B.ToArray()).Reverse().ToList();
            for (var i = 0; i < times.Length; i++)
            {
                Assert.True(first[i].AsSpan().SequenceEqual(again[i]), $"{scenario.Name} at {times[i]} s differs");
            }
        }
    }

    [Fact]
    public void A_hit_paints_the_niche_red_for_a_moment()
    {
        var hit = Scenarios().First(s => s.Name == "golpe");
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, hit.At(0), true);
        var niche = layout.Niches[0];

        int Reds(CapSheet s)
        {
            var n = 0;
            for (var y = niche.Top + 3; y < niche.Bottom - 3; y++)
            {
                for (var x = niche.Left + 3; x < niche.Right - 3; x++) n += Red(s, x, y) ? 1 : 0;
            }

            return n;
        }

        var before = Reds(CapPlaque.Replay(hit, CapMode.Column, 178, 321, 1.9));
        var during = Reds(CapPlaque.Replay(hit, CapMode.Column, 178, 321, 2.05));
        var after = Reds(CapPlaque.Replay(hit, CapMode.Column, 178, 321, 3.5));
        Assert.True(during > before + 100, $"red cells {before} then {during}");
        Assert.True(after < during / 2, $"red cells {during} then {after}");
    }

    [Fact]
    public void A_level_up_flashes_the_chip_of_the_level()
    {
        var up = Scenarios().First(s => s.Name == "subida");
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, up.At(3), true);
        var chip = CapNiche.Measure(layout.Niches[0], up.At(3).Party[0], false).LevelChip;

        long Light(CapSheet s)
        {
            long sum = 0;
            for (var y = chip.Top; y < chip.Bottom; y++)
            {
                for (var x = chip.Left; x < chip.Right; x++) sum += (s.At(x, y) >> 8) & 255;
            }

            return sum;
        }

        Assert.True(Light(CapPlaque.Replay(up, CapMode.Column, 178, 321, 2.05)) > Light(CapPlaque.Replay(up, CapMode.Column, 178, 321, 1.0)) + 400);
    }

    [Fact]
    public void Danger_lights_the_rim_of_the_case_red_and_nothing_does_when_everyone_is_well()
    {
        var combat = Scenarios().First(s => s.Name == "combate");
        var calm = Scenarios().First(s => s.Name == "completo");
        var rim = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, combat.At(0), true).Inner;

        var lit = 0;
        var dark = 0;
        for (var k = 0; k < 8; k++)
        {
            var t = 3.0 + (0.07 * k);
            var danger = CapPlaque.Replay(combat, CapMode.Column, 178, 321, t);
            var well = CapPlaque.Replay(calm, CapMode.Column, 178, 321, t);
            for (var x = rim.Left + 20; x < rim.Left + 80; x++)
            {
                lit += Red(danger, x, rim.Top) ? 1 : 0;
                dark += Red(well, x, rim.Top) ? 1 : 0;
            }
        }

        Assert.True(lit > 60, $"{lit} red cells on the rim in danger");
        Assert.Equal(0, dark);
    }

    [Fact]
    public void A_status_gives_its_niche_a_little_life_and_none_gives_it_nothing()
    {
        var states = Scenarios().First(s => s.Name == "estados");
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, states.At(0), true);

        // Each status moves inside its Pokémon's window: two moments differ there, for the five that have one.
        for (var i = 0; i < 5; i++)
        {
            var window = CapNiche.Measure(layout.Niches[i], states.At(0).Party[i], false).Alcove;
            var changed = false;
            foreach (var t in new[] { 0.5, 1.1, 1.9, 2.7 })
            {
                var a = CapPlaque.Replay(states, CapMode.Column, 178, 321, t);
                var b = CapPlaque.Replay(states, CapMode.Column, 178, 321, t + 0.23);
                for (var y = window.Top; y < window.Bottom && !changed; y++)
                {
                    for (var x = window.Left; x < window.Right && !changed; x++) changed = a.At(x, y) != b.At(x, y);
                }
            }

            Assert.True(changed, $"the status of niche {i} does not move");
        }
    }

    [Fact]
    public void With_no_animation_state_the_panel_is_the_still_one()
    {
        var data = Scenarios()[3].At(0);
        var a = CapPlaque.Render(data, CapMode.Column, 178, 321, 0, anim: null);
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, data, true);
        var still = new CapSheet(178, 321);
        CapComposer.Paint(still, layout, data);
        var frame = new CapSheet(178, 321);
        CapPlaque.Compose(frame, still, null, layout, data, null, 0);
        Assert.True(a.B.AsSpan().SequenceEqual(frame.B));
    }

    [Fact]
    public void A_change_dissolves_the_old_drawing_into_the_new_one_inside_its_niche_and_nowhere_else()
    {
        var hit = Scenarios().First(s => s.Name == "golpe");
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, hit.At(0), true);

        // Just after the change the frame is mostly the old drawing, and after the dissolve it is the new one: the cells of the niche
        // that are not the bar or the effects equal the new still part.
        var after = CapPlaque.Replay(hit, CapMode.Column, 178, 321, 2.0 + CapAnim.DissolveSeconds + 0.5);
        var newData = hit.At(2.1);
        var still = new CapSheet(178, 321);
        CapComposer.Paint(still, layout, newData);

        var niche = layout.Niches[1];
        var same = 0;
        var total = 0;
        for (var y = niche.Top; y < niche.Bottom; y++)
        {
            for (var x = niche.Left; x < niche.Right; x++)
            {
                total++;
                same += after.At(x, y) == still.At(x, y) ? 1 : 0;
            }
        }

        // Niche 1 did not change: apart from its bars (painted by the moving part) and the dust, it is the still part.
        Assert.True(same > total * 0.8, $"{same} of {total}");

        // Niche 0 did: at the moment of the change much of it is still the old drawing, and a second later none is.
        var one = layout.Niches[0];
        int Equal(CapSheet s)
        {
            var n = 0;
            for (var y = one.Top; y < one.Bottom; y++)
            {
                for (var x = one.Left; x < one.Right; x++) n += s.At(x, y) == still.At(x, y) ? 1 : 0;
            }

            return n;
        }

        Assert.True(Equal(CapPlaque.Replay(hit, CapMode.Column, 178, 321, 2.02)) < Equal(after), "the change did not dissolve");
    }
}
