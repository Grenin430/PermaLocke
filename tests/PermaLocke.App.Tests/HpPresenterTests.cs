using PermaLocke.App.Services;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>When the panel shows a change of life (2026-10-11): with the game's bar, as soon as it is read, or three seconds after; held back only in a battle.</summary>
public sealed class HpPresenterTests
{
    private const uint A = 1, B = 2;

    [Fact]
    public void The_first_reading_of_a_pokemon_is_taken_as_it_is_and_announces_nothing()
    {
        var presenter = new HpPresenter();
        Assert.False(presenter.Observe(A, 90, 143, 0, inBattle: true));
        Assert.Equal(90, presenter.Shown(A, 0));
        Assert.False(presenter.HasWaiting);
    }

    [Fact]
    public void While_the_bar_of_the_game_is_seen_the_life_shown_follows_it_as_it_drains()
    {
        var presenter = new HpPresenter(HpSyncMode.Synchronized);
        presenter.Observe(A, 100, 100, 0, true, barSeen: true);
        presenter.Observe(A, 40, 100, 1.0, true, barSeen: true);

        // The attack's animation: the bar has not moved, nothing is shown, and it is not given up on in the usual few seconds.
        presenter.Follow(1.0, 2.0);
        Assert.Equal(100, presenter.Shown(A, 7.0));

        // It drains: the panel goes with it, and never past where memory says it ends.
        presenter.Follow(0.8, 7.1);
        Assert.Equal(80, presenter.Shown(A, 7.1));
        presenter.Follow(0.55, 7.3);
        Assert.Equal(55, presenter.Shown(A, 7.3));
        presenter.Follow(0.40, 7.5);
        Assert.Equal(40, presenter.Shown(A, 7.5));
        Assert.False(presenter.HasWaiting);
    }

    [Fact]
    public void In_the_synchronised_mode_a_hit_is_held_until_the_bar_of_the_game_starts_and_then_shown_at_once()
    {
        var presenter = new HpPresenter(HpSyncMode.Synchronized);
        presenter.Observe(A, 143, 143, 0, true);

        Assert.True(presenter.Observe(A, 90, 143, 1.0, true));
        Assert.Equal(143, presenter.Shown(A, 1.0));
        Assert.Equal(143, presenter.Shown(A, 1.5));
        Assert.True(presenter.HasWaiting);

        presenter.BarStarted(2.0);
        Assert.Equal(90, presenter.Shown(A, 2.0));
        Assert.False(presenter.HasWaiting);
    }

    [Fact]
    public void Without_the_bar_the_hit_comes_out_after_a_delay_worked_out_from_how_much_it_took_and_a_bigger_hit_waits_longer()
    {
        var small = new HpPresenter();
        small.Observe(A, 143, 143, 0, true);
        small.Observe(A, 133, 143, 0, true);
        var big = new HpPresenter();
        big.Observe(A, 143, 143, 0, true);
        big.Observe(A, 43, 143, 0, true);

        var smallDue = small.NextDue!.Value;
        var bigDue = big.NextDue!.Value;
        Assert.InRange(smallDue, HpSyncTable.CalculatedBaseSeconds, HpSyncTable.CalculatedBaseSeconds + 0.2);
        Assert.True(bigDue > smallDue);
        Assert.Equal(143, big.Shown(A, bigDue - 0.01));
        Assert.Equal(43, big.Shown(A, bigDue));
    }

    [Fact]
    public void The_bar_releases_one_change_at_a_time_the_oldest_first_and_only_those_read_before_it()
    {
        var presenter = new HpPresenter();
        presenter.Observe(A, 143, 143, 0, true);
        presenter.Observe(B, 122, 122, 0, true);
        presenter.Observe(A, 100, 143, 1.0, true);
        presenter.Observe(B, 60, 122, 1.2, true);
        presenter.Observe(A, 40, 143, 3.0, true);

        presenter.BarStarted(1.5);
        Assert.Equal(100, presenter.Shown(A, 1.5));
        Assert.Equal(122, presenter.Shown(B, 1.5));

        presenter.BarStarted(2.0);
        Assert.Equal(60, presenter.Shown(B, 2.0));
        Assert.Equal(100, presenter.Shown(A, 2.0));

        // The last was read at 3.0: a bar that moved at 2.5 is not for it.
        presenter.BarStarted(2.5);
        Assert.Equal(100, presenter.Shown(A, 2.5));
        presenter.BarStarted(3.5);
        Assert.Equal(40, presenter.Shown(A, 3.5));
    }

    [Fact]
    public void A_hit_after_a_hit_shows_in_order_and_never_skips_the_first()
    {
        var presenter = new HpPresenter();
        presenter.Observe(A, 143, 143, 0, true);
        presenter.Observe(A, 100, 143, 1, true);
        presenter.Observe(A, 50, 143, 2, true);

        presenter.BarStarted(1.5);
        Assert.Equal(100, presenter.Shown(A, 1.5));
        presenter.BarStarted(2.5);
        Assert.Equal(50, presenter.Shown(A, 2.5));
    }

    [Fact]
    public void Out_of_a_battle_a_change_is_shown_at_once_and_so_is_a_change_of_the_maximum_and_the_end_of_the_battle_flushes_what_is_held()
    {
        var presenter = new HpPresenter();
        presenter.Observe(A, 143, 143, 0, true);

        presenter.Observe(A, 120, 143, 1, inBattle: false);
        Assert.Equal(120, presenter.Shown(A, 1));

        // A level: the maximum moves and the life with it. Not a hit, not held.
        presenter.Observe(A, 125, 148, 2, inBattle: true);
        Assert.Equal(125, presenter.Shown(A, 2));
        Assert.False(presenter.HasWaiting);

        presenter.Observe(A, 70, 148, 3, true);
        Assert.Equal(125, presenter.Shown(A, 3));
        presenter.Flush();
        Assert.Equal(70, presenter.Shown(A, 3));
        Assert.False(presenter.HasWaiting);
    }

    [Fact]
    public void The_instant_mode_shows_what_is_read_and_the_delayed_mode_three_seconds_after_and_neither_asks_for_the_bar()
    {
        var instant = new HpPresenter(HpSyncMode.Instant);
        instant.Observe(A, 143, 143, 0, true);
        Assert.False(instant.Observe(A, 50, 143, 1, true));
        Assert.Equal(50, instant.Shown(A, 1));

        var delayed = new HpPresenter(HpSyncMode.Delayed);
        delayed.Observe(A, 143, 143, 0, true);
        Assert.False(delayed.Observe(A, 50, 143, 1, true));
        Assert.Equal(143, delayed.Shown(A, 3.9));
        Assert.Equal(50, delayed.Shown(A, 4.0));

        // The bar means nothing to it.
        delayed.Observe(A, 20, 143, 5, true);
        delayed.BarStarted(5.1);
        Assert.Equal(50, delayed.Shown(A, 5.1));
    }

    [Fact]
    public void A_pokemon_that_leaves_the_party_is_forgotten_and_a_reset_takes_the_next_reading_as_the_first()
    {
        var presenter = new HpPresenter();
        presenter.Observe(A, 100, 143, 0, true);
        presenter.Observe(B, 100, 122, 0, true);
        presenter.Keep([B]);
        Assert.False(presenter.Knows(A));
        Assert.True(presenter.Knows(B));

        presenter.Observe(B, 20, 122, 1, true);
        presenter.Reset();
        Assert.False(presenter.Observe(B, 5, 122, 2, true));
        Assert.Equal(5, presenter.Shown(B, 2));
    }
}
