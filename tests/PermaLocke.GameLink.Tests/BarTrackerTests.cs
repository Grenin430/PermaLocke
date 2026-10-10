using PermaLocke.GameLink.Battle;

namespace PermaLocke.GameLink.Tests;

/// <summary>When the game starts to show a change on its HP bar, from the readings of the bar, and where the bar is for each layout of the screens (2026-10-11).</summary>
public sealed class BarTrackerTests
{
    private static HpBarReading Bar(double fill) => new(fill <= 0 ? HpBarState.Empty : HpBarState.Filled, fill);

    private static readonly HpBarReading Hidden = new(HpBarState.Hidden, 0);

    /// <summary>Feeds readings one tick apart and gives back every signal with the reading it came at.</summary>
    private static List<(int Index, BarSignal Signal)> Run(BarTracker tracker, params HpBarReading[] readings)
    {
        var signals = new List<(int, BarSignal)>();
        for (var i = 0; i < readings.Length; i++)
        {
            var signal = tracker.Observe(readings[i], i * 25);
            if (signal.Kind != BarSignalKind.None) signals.Add((i, signal));
        }

        return signals;
    }

    [Fact]
    public void A_bar_that_drains_is_seen_starting_at_its_first_move_and_stopping_when_it_has_stood_still_four_readings()
    {
        var tracker = new BarTracker();
        tracker.Arm();

        var signals = Run(tracker, Bar(1), Bar(1), Bar(1), Bar(0.95), Bar(0.8), Bar(0.6), Bar(0.45), Bar(0.45), Bar(0.45), Bar(0.45), Bar(0.45), Bar(0.45));

        Assert.Equal([BarSignalKind.Started, BarSignalKind.Ended], signals.Select(s => s.Signal.Kind));
        Assert.Equal(3, signals[0].Index);
        Assert.False(signals[0].Signal.Rising);
        Assert.Equal(0.95, signals[0].Signal.Fill, 3);
        Assert.Equal(0.45, signals[1].Signal.Fill, 3);
        Assert.Equal(25 * (signals[1].Index - 3), signals[1].Signal.Duration);
        Assert.False(tracker.Armed);
    }

    [Fact]
    public void A_bar_that_moves_less_than_a_pixel_or_two_is_noise_and_a_bar_that_rises_is_a_heal()
    {
        var noise = new BarTracker();
        noise.Arm();
        Assert.Empty(Run(noise, Bar(0.6), Bar(0.605), Bar(0.59), Bar(0.6), Bar(0.61)));

        var heal = new BarTracker();
        heal.Arm();
        var signals = Run(heal, Bar(0.2), Bar(0.2), Bar(0.35), Bar(0.5));
        Assert.Equal(BarSignalKind.Started, signals[0].Signal.Kind);
        Assert.True(signals[0].Signal.Rising);
    }

    [Fact]
    public void A_hidden_bar_is_not_a_reading_and_a_box_that_comes_back_grey_after_being_hidden_is_not_a_bar_that_moved()
    {
        // The box hides under an attack animation and a message, and comes back where it was: nothing moved.
        var hidden = new BarTracker();
        hidden.Arm();
        Assert.Empty(Run(hidden, Bar(1), Hidden, Hidden, Hidden, Bar(1), Bar(1)));

        // Measured at the move menu: it hid with the bar full and came back as grey. That is not a bar that drained to zero.
        var grey = new BarTracker();
        grey.Arm();
        Assert.Empty(Run(grey, Bar(1), Hidden, Hidden, Bar(0), Bar(0)));

        // But a bar seen draining to zero without being hidden in between is one.
        var drained = new BarTracker();
        drained.Arm();
        Assert.Equal(BarSignalKind.Started, Run(drained, Bar(0.3), Bar(0.1), Bar(0))[0].Signal.Kind);
    }

    [Fact]
    public void Nothing_is_signalled_until_it_is_armed_and_arming_while_the_bar_moves_does_not_start_over()
    {
        var tracker = new BarTracker();
        Assert.Empty(Run(tracker, Bar(1), Bar(0.5), Bar(0.2), Bar(0.2), Bar(0.2), Bar(0.2), Bar(0.2)));

        tracker.Arm();
        Run(tracker, Bar(1), Bar(0.8));
        Assert.True(tracker.Armed);
        tracker.Arm();
        var after = Run(tracker, Bar(0.7), Bar(0.7), Bar(0.7), Bar(0.7), Bar(0.7));
        Assert.Equal(BarSignalKind.Ended, Assert.Single(after).Signal.Kind);
    }

    [Theory]
    [InlineData(0, false, false, 400, 480, 0, 0)]
    [InlineData(0, true, false, 400, 480, 0, 240)]
    [InlineData(1, false, false, 400, 240, 0, 0)]
    [InlineData(3, false, false, 720, 240, 0, 0)]
    [InlineData(3, true, false, 720, 240, 320, 0)]
    public void The_bar_is_looked_for_where_the_layout_puts_the_top_screen(int option, bool swapped, bool upright, int width, int height, int x, int y)
    {
        var place = HpBarGeometry.Of(new AzaharScreenLayout(option, swapped, upright));
        Assert.Equal(new HpBarPlace(width, height, x, y), place);
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, false, false)]
    [InlineData(4, false, false)]
    [InlineData(0, false, true)]
    public void A_layout_that_is_not_computed_or_that_does_not_show_the_top_screen_has_no_bar_to_look_for(int option, bool swapped, bool upright)
    {
        Assert.Null(HpBarGeometry.Of(new AzaharScreenLayout(option, swapped, upright)));
    }
}
