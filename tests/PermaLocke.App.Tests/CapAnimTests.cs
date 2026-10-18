using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>What the cap panel remembers between frames and how it shows a change (2026-10-10).</summary>
public sealed class CapAnimTests
{
    private static CapPlaqueMember Member(uint pid, int level = 30, int hp = 100, int max = 100, bool egg = false, CapStatus status = CapStatus.None) =>
        new(null, "P" + pid, level, hp, max, egg, pid, Status: status);

    private static CapPlaqueData Data(IReadOnlyList<CapPlaqueMember> party, int cap = 40, int? highest = null, bool battle = false, string trial = "PRUEBA 3 DE 12") =>
        new(trial, cap, cap + 8, highest ?? party.Max(m => m.Level), party, battle);

    private static CapPlaqueMember[] Party(params CapPlaqueMember[] members) => members;

    [Fact]
    public void The_first_time_it_sees_a_party_it_announces_nothing()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1), Member(2, hp: 20))), 5);

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(0, anim.HitPulse(i, 5.1));
            Assert.Equal(0, anim.HealPulse(i, 5.1));
            Assert.Equal(0, anim.LevelPulse(i, 5.1));
            Assert.Null(anim.TrailFraction(i, 5.1));
        }

        Assert.Equal(0.2, anim.HpFraction(1, 5.1)!.Value, 3);
        Assert.False(anim.Busy(5.1));
    }

    [Fact]
    public void A_hit_moves_the_bar_down_and_leaves_a_trail_that_catches_up()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1))), 0);
        anim.Update(Data(Party(Member(1, hp: 40))), 2);

        // The bar takes as long as the table says for a hit of that size (60 % of the life here), and the numbers of the table are in one place.
        var seconds = anim.BarSeconds(0);
        Assert.Equal(HpSyncTable.BarSeconds(0.6, drop: true), seconds, 6);
        Assert.Equal(1.0, anim.HpFraction(0, 2.0)!.Value, 3);
        Assert.Equal(0.4, anim.HpFraction(0, 2 + seconds + 0.01)!.Value, 3);
        Assert.InRange(anim.HpFraction(0, 2 + (seconds / 2))!.Value, 0.4, 1.0);

        // The ghost stays where the bar was until the real one has got down to its place, then goes down to it and is gone.
        Assert.Equal(1.0, anim.TrailFraction(0, 2 + (seconds / 2))!.Value, 3);
        Assert.Equal(1.0, anim.TrailFraction(0, 2 + seconds)!.Value, 3);
        Assert.InRange(anim.TrailFraction(0, 2 + seconds + 0.6)!.Value, 0.4, 1.0);
        Assert.Null(anim.TrailFraction(0, 2 + seconds + 1.5));

        Assert.Equal(1, anim.HitPulse(0, 2.0), 3);
        Assert.Equal(0, anim.HitPulse(0, 2.4), 3);
        Assert.True(anim.Busy(2.2));
        Assert.False(anim.Busy(9));
    }

    [Fact]
    public void A_heal_rises_and_pulses_green()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, hp: 30))), 0);
        anim.Update(Data(Party(Member(1, hp: 100))), 1);

        Assert.Equal(0.3, anim.HpFraction(0, 1.0)!.Value, 3);
        Assert.Equal(1.0, anim.HpFraction(0, 1 + anim.BarSeconds(0) + 0.01)!.Value, 3);
        Assert.True(anim.HealPulse(0, 1.2) > 0);
        Assert.Equal(0, anim.HealPulse(0, 2.0));
        Assert.Equal(0, anim.HitPulse(0, 1.2));
    }

    [Fact]
    public void A_level_up_flashes_the_level_and_a_max_hp_that_only_grew_is_not_a_heal()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, level: 30, hp: 100, max: 100))), 0);
        anim.Update(Data(Party(Member(1, level: 31, hp: 100, max: 104))), 3);

        Assert.True(anim.LevelPulse(0, 3.1) > 0.8);
        Assert.Equal(0, anim.LevelPulse(0, 4.5));
        Assert.Equal(0, anim.HealPulse(0, 3.1));
        Assert.Equal(0, anim.HitPulse(0, 3.1));
    }

    [Fact]
    public void Two_pokemon_that_swap_places_keep_their_own_history_and_announce_nothing()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, hp: 100), Member(2, hp: 30))), 0);
        anim.Update(Data(Party(Member(2, hp: 30), Member(1, hp: 100))), 2);

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(0, anim.HitPulse(i, 2.05));
            Assert.Equal(0, anim.HealPulse(i, 2.05));
        }

        Assert.Equal(0.3, anim.HpFraction(0, 2.05)!.Value, 3);
        Assert.Equal(1.0, anim.HpFraction(1, 2.05)!.Value, 3);
    }

    [Fact]
    public void A_pokemon_hit_to_zero_waits_for_its_fall_and_is_drawn_until_it_comes()
    {
        // As the app does: the fall is told when the game has shown the faint, not read from the life.
        var feed = new CapEventFeed { FallsFromLife = false };
        var anim = new CapAnim();
        var alive = Data(Party(Member(1, hp: 10)), battle: true);
        var zero = Data(Party(Member(1, hp: 0)), battle: true);
        anim.Update(alive with { Events = feed.Observe(alive, 0, 0).Events }, 0);
        anim.Update(zero with { Events = feed.Observe(zero, 4, 4).Events }, 4);

        Assert.True(anim.AwaitingFall(0, 5));
        Assert.False(anim.AwaitingFall(0, 30));

        anim.Update(zero with { Events = feed.Fall(1, 6, (int)CapDeathKind.Stone) }, 6);
        Assert.Equal(CapDeathKind.Stone, anim.Death(0));
        Assert.False(anim.AwaitingFall(0, 6.5));
        Assert.Equal(0.5, anim.SinceFall(0, 6.5)!.Value, 3);

        // Brought back (a Pokémon Centre before the first Poké Ball): it is no longer fallen and is drawn again.
        var back = Data(Party(Member(1, hp: 100)));
        anim.Update(back with { Events = feed.Observe(back, 20, 20).Events }, 20);
        Assert.Null(anim.SinceFall(0, 21));
    }

    [Fact]
    public void A_fall_is_timed_and_does_not_turn_the_sea_grey()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, hp: 10)), battle: true), 0);
        anim.Update(Data(Party(Member(1, hp: 0)), battle: true), 4);

        Assert.Equal(0.5, anim.SinceFall(0, 4.5)!.Value, 3);
        Assert.Null(anim.SinceFall(0, 3));

        // No mourning any more (2026-10-15): the sea keeps its own mood; the fall is shown by its death.
        Assert.NotEqual(CapMood.Mourning, anim.Mood(5).Now);
        Assert.NotEqual(CapMood.Mourning, anim.Mood(5).Before);
        Assert.NotEqual(CapMood.Mourning, anim.Mood(23).Now);
    }

    [Fact]
    public void The_sea_follows_the_life_of_the_one_on_the_field_when_the_bar_says_it_and_is_calm_when_it_does_not()
    {
        var anim = new CapAnim();
        var party = Party(Member(1, hp: 17), Member(2, hp: 100));

        // Gyarados is at 17 % but Quesito is out and healthy: no danger. Switching back to Gyarados is.
        anim.Update(Data(party, battle: true) with { ActiveShare = 0.95 }, 0);
        Assert.Equal(CapMood.Battle, anim.Mood(1).Now);
        anim.Update(Data(party, battle: true) with { ActiveShare = 0.15 }, 2);
        Assert.Equal(CapMood.Danger, anim.Mood(3).Now);

        // Switching out of the weak one puts the sea back as it was, which is what it did not do.
        anim.Update(Data(party, battle: true) with { ActiveShare = 0.80 }, 4);
        Assert.Equal(CapMood.Battle, anim.Mood(6).Now);

        // The bar not seen: no danger (the lowest of the party kept the sea red after a swap).
        anim.Update(Data(party, battle: true), 8);
        Assert.Equal(CapMood.Battle, anim.Mood(10).Now);
        Assert.Equal(0.30, CapAnim.DangerShare);
    }

    [Fact]
    public void Someone_at_a_quarter_of_their_hp_in_a_battle_is_danger_and_out_of_one_is_not()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, hp: 100), Member(2, hp: 25)), battle: true) with { ActiveShare = 0.25 }, 0);
        Assert.Equal(CapMood.Danger, anim.Mood(1).Now);
        Assert.True(anim.Busy(1));

        anim.Update(Data(Party(Member(1, hp: 100), Member(2, hp: 25)), battle: false), 2);
        Assert.Equal(CapMood.Calm, anim.Mood(10).Now);

        anim.Update(Data(Party(Member(1, hp: 100), Member(2, hp: 80)), battle: true) with { ActiveShare = 0.8 }, 12);
        Assert.Equal(CapMood.Battle, anim.Mood(20).Now);
    }

    [Fact]
    public void The_party_at_the_cap_has_the_cap_light_and_an_egg_does_not_count()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, level: 40), Member(2, level: 10)), cap: 40), 0);
        Assert.Equal(CapMood.Cap, anim.Mood(2).Now);

        anim.Update(Data(Party(Member(1, level: 39), Member(2, level: 40, egg: true)), cap: 40, highest: 39), 3);
        Assert.Equal(CapMood.Calm, anim.Mood(8).Now);
    }

    [Fact]
    public void A_new_cap_burns_the_fuse_back_and_lights_it_again_towards_the_new_one()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, level: 40)), cap: 40), 0);
        anim.Update(Data(Party(Member(1, level: 40)), cap: 50, trial: "PRUEBA 4 DE 12"), 2);

        Assert.Equal(1.0, anim.FuseShare(2.0, 40, 50), 3);
        Assert.InRange(anim.FuseShare(2.3, 40, 50), 0, 1);
        Assert.Equal(0, anim.FuseShare(2.6, 40, 50), 2);
        Assert.Equal(0.8, anim.FuseShare(3.7, 40, 50), 3);
        Assert.True(anim.CapPulse(2.1) > 0);
        Assert.True(anim.HeaderDissolve(2.1) < 1);
        Assert.Equal(1, anim.HeaderDissolve(3));
    }

    [Fact]
    public void An_egg_that_hatches_flashes()
    {
        var anim = new CapAnim();
        anim.Update(Data(Party(Member(1, egg: true, hp: 10, max: 10))), 0);
        anim.Update(Data(Party(Member(1, level: 1, hp: 11, max: 11))), 2);

        Assert.True(anim.HatchPulse(0, 2.1) > 0.8);
        Assert.Equal(0, anim.HatchPulse(0, 3.5));
        Assert.Equal(0, anim.HealPulse(0, 2.1));
    }

    [Fact]
    public void Two_animations_told_the_same_things_answer_the_same_at_any_moment_and_a_reset_forgets()
    {
        CapAnim Build()
        {
            var anim = new CapAnim();
            anim.Update(Data(Party(Member(1), Member(2, hp: 50)), battle: true), 0);
            anim.Update(Data(Party(Member(1, hp: 30, level: 31), Member(2, hp: 0)), battle: true), 3);
            return anim;
        }

        var a = Build();
        var b = Build();
        foreach (var t in new[] { 3.0, 3.2, 3.9, 6, 30 })
        {
            for (var i = 0; i < 2; i++)
            {
                Assert.Equal(a.HpFraction(i, t), b.HpFraction(i, t));
                Assert.Equal(a.TrailFraction(i, t), b.TrailFraction(i, t));
                Assert.Equal(a.HitPulse(i, t), b.HitPulse(i, t));
                Assert.Equal(a.LevelPulse(i, t), b.LevelPulse(i, t));
            }

            Assert.Equal(a.Mood(t), b.Mood(t));
        }

        a.Reset();
        Assert.Null(a.HpFraction(0, 3.2));
        Assert.False(a.Busy(3.2));
    }
}
