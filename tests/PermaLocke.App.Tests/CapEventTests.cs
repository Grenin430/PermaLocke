using System.IO;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// What happened to each Pokémon, and what the sea does about it (2026-10-11): the events are right and never false, they pile up in order, they live a
/// few seconds, and what draws them is pure, quiet when nothing happens and cheap.
/// </summary>
public sealed class CapEventTests
{
    private static CapPlaqueMember Member(uint pid, int hp = 100, int max = 100, int level = 30, CapStatus status = CapStatus.None, bool egg = false, string? item = null,
        params string[] moves) =>
        new(null, "P" + pid, level, hp, max, egg, pid, Status: status, Item: item, Moves: moves.Length == 0 ? null : [.. moves.Select((m, k) => new CapMove(k, false, m))]);

    private static CapPlaqueData Data(bool battle = false, params CapPlaqueMember[] party) =>
        new("PRUEBA 3 DE 12", 40, 48, party.Max(m => m.Level), party, battle, [], 3, 12);

    private static CapEventKind[] Kinds(CapPlaqueData data, long after = 0) => [.. data.Events!.Where(e => e.Seq > after).Select(e => e.Kind)];

    [Fact]
    public void The_first_time_it_sees_a_pokemon_it_announces_nothing_and_neither_does_a_steady_party()
    {
        var feed = new CapEventFeed();
        Assert.Empty(feed.Observe(Data(false, Member(1, 10), Member(2)), 0, 0).Events!);
        Assert.Empty(feed.Observe(Data(false, Member(1, 10), Member(2)), 1, 1).Events!);

        // A Pokémon that joins later is a first sight too, and so is one that left and came back.
        Assert.Empty(feed.Observe(Data(false, Member(1, 10), Member(2), Member(3, 5)), 2, 2).Events!);
        feed.Observe(Data(false, Member(1, 10)), 3, 3);
        Assert.Empty(feed.Observe(Data(false, Member(1, 10), Member(2, 77)), 4, 4).Events!);
    }

    [Fact]
    public void A_hit_is_the_life_it_lost_and_a_hit_to_zero_is_also_a_fall_and_a_heal_from_zero_is_a_revive()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(true, Member(1, 100)), 0, 0);

        var hit = feed.Observe(Data(true, Member(1, 60)), 1, 1).Events!;
        Assert.Equal([CapEventKind.Hit], hit.Select(e => e.Kind));
        Assert.Equal(40, hit[0].Amount);
        Assert.Equal(1, hit[0].At);

        var fell = feed.Observe(Data(true, Member(1, 0)), 2, 2).Events!;
        Assert.Equal([CapEventKind.Hit, CapEventKind.Hit, CapEventKind.Fell], fell.Select(e => e.Kind));
        Assert.Equal(60, fell[1].Amount);

        var up = feed.Observe(Data(false, Member(1, 50)), 3, 3).Events!;
        Assert.Equal(CapEventKind.Revive, up[^1].Kind);
        Assert.Equal(50, up[^1].Amount);

        var heal = feed.Observe(Data(false, Member(1, 90)), 4, 4).Events!;
        Assert.Equal(CapEventKind.Heal, heal[^1].Kind);
        Assert.Equal(40, heal[^1].Amount);
    }

    [Fact]
    public void A_level_that_moves_the_maximum_is_not_a_heal_and_not_a_hit_and_neither_is_any_change_of_the_maximum()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(false, Member(1, 100, 100, 30)), 0, 0);

        // Level up: the maximum goes up and the game adds the same to the life.
        var level = feed.Observe(Data(false, Member(1, 104, 104, 31)), 1, 1).Events!;
        Assert.Equal([CapEventKind.Level], level.Select(e => e.Kind));

        // Evolution, a swap of the structure, a form: the maximum moves and the life does whatever; nothing was hit.
        Assert.Equal([CapEventKind.Level], feed.Observe(Data(false, Member(1, 130, 160, 31)), 2, 2).Events!.Select(e => e.Kind));
        Assert.Equal([CapEventKind.Level], feed.Observe(Data(false, Member(1, 20, 150, 31)), 3, 3).Events!.Select(e => e.Kind));
    }

    [Fact]
    public void A_status_is_news_only_between_two_readings_out_of_battle_and_never_at_the_edges_of_one()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(false, Member(1, status: CapStatus.Poison)), 0, 0);

        // The battle starts: the status stops being known, the panel shows none. That is not a cure.
        Assert.Empty(feed.Observe(Data(true, Member(1)), 1, 1).Events!);
        Assert.Empty(feed.Observe(Data(true, Member(1)), 2, 2).Events!);

        // The battle ends and the status is known again: not a new status either.
        Assert.Empty(feed.Observe(Data(false, Member(1, status: CapStatus.Poison)), 3, 3).Events!);

        // Out of battle, an antidote: a cure. Then burnt: a status in. Then a different one: out and in.
        var cured = feed.Observe(Data(false, Member(1)), 4, 4).Events!;
        Assert.Equal((CapEventKind.StatusOut, CapStatus.Poison), (cured[^1].Kind, cured[^1].Status));
        var burnt = feed.Observe(Data(false, Member(1, status: CapStatus.Burn)), 5, 5).Events!;
        Assert.Equal((CapEventKind.StatusIn, CapStatus.Burn), (burnt[^1].Kind, burnt[^1].Status));
        var swapped = feed.Observe(Data(false, Member(1, status: CapStatus.Paralysis)), 6, 6).Events!.TakeLast(2).ToArray();
        Assert.Equal([(CapEventKind.StatusOut, CapStatus.Burn), (CapEventKind.StatusIn, CapStatus.Paralysis)], swapped.Select(e => (e.Kind, e.Status)));
    }

    [Fact]
    public void An_egg_says_nothing_until_it_hatches_and_an_item_and_a_move_are_news_when_they_change()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(false, Member(1, egg: true, hp: 10), Member(2, item: "Restos", moves: ["Surf", "Placaje"])), 0, 0);

        // The egg's life moving is nothing; an item swapped and a move learned are.
        var hatched = feed.Observe(Data(false, Member(1, egg: true, hp: 1), Member(2, item: "Baya", moves: ["Surf", "Placaje"])), 1, 1).Events!;
        Assert.Equal([(CapEventKind.ItemChanged, 2)], hatched.Select(e => (e.Kind, e.Extra)));

        var learned = feed.Observe(Data(false, Member(1, hp: 11), Member(2, item: null, moves: ["Surf", "Hiperrayo"])), 2, 2).Events!.Skip(1).ToArray();
        Assert.Equal([CapEventKind.Hatch, CapEventKind.ItemChanged, CapEventKind.MoveLearned], learned.Select(e => e.Kind));
        Assert.Equal(1, learned[1].Extra);

        // The PP running down changes no name: no move was learned.
        var last = learned.Max(e => e.Seq);
        Assert.DoesNotContain(CapEventKind.MoveLearned, Kinds(feed.Observe(Data(false, Member(1, hp: 11), Member(2, item: null, moves: ["Surf", "Hiperrayo"])), 3, 3), last));
    }

    [Fact]
    public void Events_come_in_the_order_they_were_seen_with_rising_numbers_live_six_seconds_and_a_new_start_forgets_what_it_saw()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(true, Member(1, 100), Member(2, 100)), 0, 0);
        feed.Observe(Data(true, Member(1, 90), Member(2, 100)), 1, 1);
        var data = feed.Observe(Data(true, Member(1, 90), Member(2, 40)), 2, 2);

        Assert.Equal([1u, 2u], data.Events!.Select(e => e.Pid));
        Assert.True(data.Events!.Zip(data.Events!.Skip(1)).All(pair => pair.First.Seq < pair.Second.Seq));

        Assert.Equal(2, feed.Observe(Data(true, Member(1, 90), Member(2, 40)), 5, 6.5).Events!.Count);
        Assert.Single(feed.Observe(Data(true, Member(1, 90), Member(2, 40)), 5, 7.5).Events!);
        Assert.Empty(feed.Observe(Data(true, Member(1, 90), Member(2, 40)), 9, 9).Events!);

        feed.Rebaseline();
        Assert.Empty(feed.Observe(Data(true, Member(1, 1), Member(2, 1)), 10, 10).Events!);
    }

    [Fact]
    public void Swapping_two_in_the_party_moves_their_histories_with_them_and_announces_nothing()
    {
        var feed = new CapEventFeed();
        feed.Observe(Data(false, Member(1, 10), Member(2, 90)), 0, 0);
        Assert.Empty(feed.Observe(Data(false, Member(2, 90), Member(1, 10)), 1, 1).Events!);
    }

    // ---------------------------------------------------------------- the animation

    [Fact]
    public void The_animation_takes_the_events_from_the_data_once_each_and_ignores_the_ones_from_before_it_opened()
    {
        var feed = new CapEventFeed();
        var anim = new CapAnim();
        var first = feed.Observe(Data(true, Member(1, 100)), 0, 0);
        feed.Observe(Data(true, Member(1, 80)), 1, 1);

        // It opens late: the hit of a second ago is not shown.
        anim.Update(feed.Observe(Data(true, Member(1, 80)), 1.2, 1.2), 1.2);
        Assert.Null(anim.SinceHit(0, 1.3));
        _ = first;

        var after = feed.Observe(Data(true, Member(1, 50)), 3, 3);
        anim.Update(after, 3.0);
        Assert.InRange(anim.SinceHit(0, 3.2)!.Value, 0.19, 0.21);

        // The same data again: the same event is not shown twice, so its moment does not move.
        anim.Update(after, 3.5);
        Assert.InRange(anim.SinceHit(0, 3.6)!.Value, 0.59, 0.61);
    }

    [Fact]
    public void Hits_in_a_row_are_kept_newest_first_so_that_their_numbers_stack_and_a_bigger_hit_makes_a_bigger_shake()
    {
        var feed = new CapEventFeed();
        var anim = new CapAnim();
        anim.Update(feed.Observe(Data(true, Member(1, 100)), 0, 0), 0);
        foreach (var (hp, t) in new[] { (95, 1.0), (60, 1.4), (20, 1.8), (10, 2.0) }) anim.Update(feed.Observe(Data(true, Member(1, hp)), t, t), t);

        Span<(double Since, int Amount)> hits = stackalloc (double, int)[CapAnim.HitMemory];
        var n = anim.Hits(0, 2.1, hits);
        Assert.Equal(CapAnim.HitMemory, n);
        Assert.Equal([10, 40, 35], hits[..n].ToArray().Select(h => h.Amount));
        Assert.True(hits[0].Since < hits[1].Since && hits[1].Since < hits[2].Since);

        Assert.Equal(1, CapFxEvents.Tier(0.05));
        Assert.Equal(2, CapFxEvents.Tier(0.2));
        Assert.Equal(3, CapFxEvents.Tier(0.5));
    }

    [Fact]
    public void The_quiet_life_follows_the_life_that_is_left_and_is_nervous_in_danger_and_the_light_blinks_now_and_then()
    {
        var anim = new CapAnim();
        var scene = Data(true, Member(1, 100), Member(2, 20));
        anim.Update(scene, 0);

        double Wave(CapPlaqueMember m, double share, bool battle)
        {
            int dx = 0; uint? wash = null; var alpha = 0; var strength = 1.0;
            CapFxEvents.Pose(anim, 0, 1.0, m, share, battle, ref dx, ref wash, ref alpha, ref strength, out var wave, out var speed, out _);
            return wave * speed;
        }

        Assert.True(Wave(Member(1, 100), 1.0, false) > Wave(Member(1, 40), 0.4, false), "a tired creature does not sway as much");
        Assert.True(Wave(Member(2, 20), 0.2, true) > Wave(Member(2, 20), 0.2, false) * 2, "it is not nervous in danger");
        Assert.Equal(0, Wave(Member(3, 0), 0, true));

        // The blink: a dip in the halo of about a quarter of a second, and for the rest of the time the full light.
        var dips = 0;
        for (var t = 0.0; t < 20; t += 0.05)
        {
            int dx = 0; uint? wash = null; var alpha = 0; var strength = 1.0;
            CapFxEvents.Pose(anim, 0, t, Member(1), 1, false, ref dx, ref wash, ref alpha, ref strength, out _, out _, out var halo);
            dips += halo < 0.5 ? 1 : 0;
        }

        Assert.InRange(dips, 3, 30);
    }

    [Fact]
    public void Every_event_scenario_draws_the_same_frame_every_time_and_something_changes_when_it_happens()
    {
        foreach (var name in new[] { "ev-golpe", "ev-cura", "ev-antidoto", "ev-curas", "ev-revivir", "ev-estado", "ev-objeto", "ev-movimiento" })
        {
            var scenario = CapSeaTests.Scenarios().First(s => s.Name == name);
            var change = scenario.Steps[1].At;
            var before = CapPlaque.Replay(scenario, CapMode.Column, 178, 321, change - 0.2, CapPanelKind.Party);
            var during = CapPlaque.Replay(scenario, CapMode.Column, 178, 321, change + 0.4, CapPanelKind.Party);
            var again = CapPlaque.Replay(scenario, CapMode.Column, 178, 321, change + 0.4, CapPanelKind.Party);

            Assert.Equal(during.B, again.B);
            Assert.NotEqual(before.B, during.B);
        }
    }

    [Fact]
    public void Nothing_in_the_cap_drawing_reads_a_clock_or_a_random_number_and_a_steady_frame_costs_almost_no_memory()
    {
        // The drawing is a function of the data and the time it is given: no Random, no clock of its own.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        foreach (var file in Directory.GetFiles(Path.Combine(dir.FullName, "src", "PermaLocke.App", "Views", "Cap"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("new Random", text);
            Assert.DoesNotContain("Random.Shared", text);
            Assert.DoesNotContain("DateTime.", text);
            Assert.DoesNotContain("Stopwatch", text);
        }

        // A frame of the party with nothing happening, after the first ones: the garbage per frame stays small (the sheet is reused).
        var scenario = CapSeaTests.Scenarios().First(s => s.Name == "captura");
        var view = new CapWorldView(CapMode.Column, CapPanelKind.Party, 178, 321, (0, 0, 0));
        var anim = new CapAnim();
        var data = scenario.At(0);
        view.SetData(data);
        anim.Update(data, 0);
        var frame = new CapSheet(178, 321);
        for (var k = 0; k < 20; k++) view.Paint(frame, k * 0.1, anim);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var k = 0; k < 100; k++) view.Paint(frame, 5 + (k * 0.04), anim);
        var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / 100;
        Assert.True(perFrame < 64 * 1024, $"a steady frame allocates {perFrame} bytes");
    }
}
