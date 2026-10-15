using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The happenings of the sea (2026-10-10): they are timed, they stay inside their window, and the rare ones belong to the run.</summary>
public sealed class CapPhenomenaTests
{
    private static CapPlaqueMember Member(uint pid, int hp = 100, int level = 30, CapStatus status = CapStatus.None, bool egg = false) =>
        new(null, "P" + pid, level, hp, 100, egg, pid, Status: status);

    private static CapPlaqueData Data(IReadOnlyList<CapPlaqueMember> party, int graves = 0, bool battle = false, int cap = 40) =>
        new("PRUEBA 3 DE 12", cap, cap + 8, party.Max(m => m.Level), party, battle, [.. Enumerable.Range(0, graves).Select(i => new CapGrave(null, "G" + i))], 3, 12);

    private static CapWorldLayout Layout() => CapWorld.Arrange(CapMode.Column, CapPanelKind.Party, 180, 321, (0, 0, 0));

    [Fact]
    public void Deaths_that_happen_together_are_shown_one_after_another_each_with_the_death_of_its_pid()
    {
        var anim = new CapAnim();
        var alive = Data([Member(11, 10), Member(12), Member(13, 5)], 3, battle: true);
        anim.Update(alive, 0);
        var fallen = Data([Member(11, 0), Member(12), Member(13, 0)], 3, battle: true);
        anim.Update(fallen, 2);

        Assert.Empty(CapDeath.Schedule(fallen, anim, 1.9));
        var turns = CapDeath.Schedule(fallen, anim, 2.5);
        Assert.Equal([0, 2], turns.Select(d => d.Index));
        Assert.Equal(2, turns[0].Start, 3);
        Assert.Equal(turns[0].End, turns[1].Start, 3);
        Assert.Equal(CapDeath.KindOf(11, 0), turns[0].Kind);
        Assert.Equal(CapDeath.KindOf(13, 2), turns[1].Kind);

        // The one that did not fall has no turn, and the same PID always draws the same death.
        Assert.DoesNotContain(turns, d => d.Index == 1);
        Assert.Equal(CapDeath.KindOf(11, 0), CapDeath.KindOf(11, 4));
        Assert.Equal(Enum.GetValues<CapDeathKind>().Length, Enumerable.Range(1, 400).Select(p => CapDeath.KindOf((uint)p, 0)).Distinct().Count());
    }

    [Fact]
    public void A_hit_pushes_the_picture_aside_and_flashes_it_white_and_a_heal_and_a_level_wash_it()
    {
        var anim = new CapAnim();
        anim.Update(Data([Member(1), Member(2, 40)]), 0);
        anim.Update(Data([Member(1, 60), Member(2, 100, 31)]), 5);
        var layout = Layout();
        var ctx = new CapSeaContext();

        var hit = CapFx.Pose(anim, 0, 5.02, Member(1, 60), 0.6, true, layout, ctx, 0);
        Assert.NotEqual(0, hit.Dx);
        Assert.Equal(0xFFFFFFu, hit.Wash);

        var later = CapFx.Pose(anim, 0, 6.5, Member(1, 60), 0.6, true, layout, ctx, 0);
        Assert.Equal(0, later.Dx);
        Assert.Null(later.Wash);

        var healed = CapFx.Pose(anim, 1, 5.05, Member(2, 100, 31), 1, false, layout, ctx, 0);
        Assert.NotNull(healed.Wash);
    }

    [Fact]
    public void Someone_at_a_quarter_of_their_life_in_a_battle_gets_a_red_halo_that_beats_faster_than_a_healthy_one()
    {
        var anim = new CapAnim();
        anim.Update(Data([Member(1, 20), Member(2)], battle: true), 0);
        var layout = Layout();
        var ctx = new CapSeaContext();

        Assert.Equal(0xFF4A3Au, CapFx.Pose(anim, 0, 1, Member(1, 20), 0.2, true, layout, ctx, 0).HaloTint);
        Assert.Null(CapFx.Pose(anim, 1, 1, Member(2), 1, true, layout, ctx, 0).HaloTint);
        Assert.Null(CapFx.Pose(anim, 0, 1, Member(1, 20), 0.2, false, layout, ctx, 0).HaloTint);

        // The breath of the halo swings more times in four seconds the lower the life is.
        int Swings(double share)
        {
            var count = 0;
            var last = CapFx.Pose(anim, 0, 0, Member(1, 20), share, true, layout, ctx, 0).Breath;
            var rising = true;
            for (var t = 0.01; t < 4; t += 0.01)
            {
                var b = CapFx.Pose(anim, 0, t, Member(1, 20), share, true, layout, ctx, 0).Breath;
                if (rising && b < last)
                {
                    count++;
                    rising = false;
                }
                else if (!rising && b > last)
                {
                    rising = true;
                }

                last = b;
            }

            return count;
        }

        Assert.True(Swings(0.1) > Swings(1.0), "the heartbeat does not quicken");
    }

    [Fact]
    public void The_floor_drops_away_for_a_second_and_a_half_and_comes_back_after_a_new_cap_and_only_then()
    {
        var anim = new CapAnim();
        anim.Update(Data([Member(1)], cap: 40), 0);
        Assert.Equal(0, anim.FloorDrop(1, 30));

        anim.Update(Data([Member(1)], cap: 50), 5);
        Assert.Equal(0, anim.FloorDrop(5, 30));
        Assert.InRange(anim.FloorDrop(5.75, 30), 5, 25);
        Assert.Equal(30, anim.FloorDrop(6.5, 30));
        Assert.InRange(anim.FloorDrop(7.5, 30), 1, 29);
        Assert.Equal(0, anim.FloorDrop(8.3, 30));
        Assert.Null(anim.SinceCap(4));
        Assert.Equal(1.5, anim.SinceCap(6.5)!.Value, 3);
    }

    [Fact]
    public void The_rare_events_belong_to_the_seed_of_the_run_and_the_time_and_to_nothing_else()
    {
        // The same run at the same moment sees the same thing, in any order; another run does not see the same.
        for (var kind = 1; kind <= 5; kind++)
        {
            for (var t = 0.0; t < 3600; t += 7.3)
            {
                Assert.Equal(CapFauna.EventAt(1234, kind, t, 300, 40, 70), CapFauna.EventAt(1234, kind, t, 300, 40, 70));
            }
        }

        var mine = Enumerable.Range(0, 3600).Select(s => CapFauna.EventAt(1234, 1, s, 480, 80, 70).Active).ToList();
        var other = Enumerable.Range(0, 3600).Select(s => CapFauna.EventAt(9876, 1, s, 480, 80, 70).Active).ToList();
        Assert.False(mine.SequenceEqual(other), "two runs see the whale at the same moments");

        // In an hour with a whale due every eight minutes there are some, and no event is further along than it can be.
        Assert.InRange(mine.Count(a => a), 80, 600);
        for (var s = 0; s < 3600; s++)
        {
            var e = CapFauna.EventAt(1234, 1, s, 480, 80, 70);
            if (e.Active) Assert.InRange(e.Progress, 0, 1);
        }

        // At a chance of zero nothing happens, and at a hundred something happens in every period.
        Assert.DoesNotContain(Enumerable.Range(0, 2000), s => CapFauna.EventAt(5, 2, s, 200, 30, 0).Active);
        Assert.All(Enumerable.Range(0, 10), period => Assert.Contains(Enumerable.Range(period * 200, 200), s => CapFauna.EventAt(5, 2, s, 200, 30, 100).Active));
    }

    [Fact]
    public void Everything_that_moves_stays_out_of_the_notch_at_every_moment_of_every_scenario()
    {
        foreach (var scenario in CapSeaTests.Scenarios())
        {
            foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
            {
                var notch = CapSeaTests.NotchOf(panel, 207, 321, 27);
                foreach (var t in new[] { 0.0, 1.0, 2.1, 2.3, 2.6, 3.4, 4.5, 6.0, 6.3, 8.0, 10.3, 14.5, 21.0, 130.0, 470.0 })
                {
                    var frame = CapPlaque.Replay(scenario, CapMode.Column, 207, 321, t, panel, notch, panel == CapPanelKind.Party ? 400 : 0, 600);
                    for (var y = 0; y < notch.Item3; y++)
                    {
                        for (var x = notch.Item1; x < notch.Item2; x++) Assert.False(frame.Solid(x, y), $"{scenario.Name} {panel} t={t}: a cell in the notch at {x},{y}");
                    }
                }
            }
        }
    }
}
