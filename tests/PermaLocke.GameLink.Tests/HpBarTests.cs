using PermaLocke.GameLink.Battle;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The HP bar read from screen pixels, with the colours measured on a recording of a real death (§114 ter).
/// </summary>
public class HpBarTests
{
    private static readonly (byte R, byte G, byte B) Green = (148, 254, 48);
    private static readonly (byte R, byte G, byte B) Yellow = (253, 201, 43);
    private static readonly (byte R, byte G, byte B) Red = (251, 0, 20);
    private static readonly (byte R, byte G, byte B) Track = (67, 67, 64);

    /// <summary>The message box that covered the bar in the recording.</summary>
    private static readonly (byte R, byte G, byte B) MessageBox = (14, 44, 67);

    /// <summary>The grass behind the box.</summary>
    private static readonly (byte R, byte G, byte B) Grass = (3, 22, 32);

    [Fact]
    public void A_full_bar_is_filled() =>
        Assert.Equal(HpBarState.Filled, HpBar.Classify(Row((Green, 85))));

    /// <summary>6, 4 and 2 of 12 as they were counted on the frames: 53, 36 and 18 cells of colour.</summary>
    [Theory]
    [InlineData(53)]
    [InlineData(36)]
    [InlineData(18)]
    public void A_draining_bar_is_still_filled(int cells) =>
        Assert.Equal(HpBarState.Filled, HpBar.Classify(Row((cells > 40 ? Green : cells > 20 ? Yellow : Red, cells), (Track, 85 - cells))));

    [Fact]
    public void One_last_cell_of_red_is_not_zero() =>
        Assert.Equal(HpBarState.Filled, HpBar.Classify(Row((Red, 1), (Track, 84))));

    [Fact]
    public void All_track_is_zero() =>
        Assert.Equal(HpBarState.Empty, HpBar.Classify(Row((Track, 85))));

    /// <summary>
    /// Covered by a message box there is neither colour nor track. It must not read as an empty bar, or
    /// the ceremony would start during every attack animation.
    /// </summary>
    [Fact]
    public void A_covered_bar_is_hidden_not_empty()
    {
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((MessageBox, 85))));
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((Grass, 85))));
    }

    [Fact]
    public void A_bar_half_covered_is_hidden() =>
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((Track, 40), (MessageBox, 45))));

    [Fact]
    public void Rows_decide_by_majority()
    {
        Assert.Equal(HpBarState.Empty, HpBar.Combine([HpBarState.Empty, HpBarState.Empty, HpBarState.Hidden]));
        Assert.Equal(HpBarState.Hidden, HpBar.Combine([HpBarState.Empty, HpBarState.Filled, HpBarState.Hidden]));
    }

    [Fact]
    public void A_bar_draining_in_sight_reaches_zero()
    {
        var watch = new HpBar.ZeroWatch();

        Assert.False(watch.Observe(Hidden, 0));
        Assert.False(watch.Observe(Colour(0.62), 100));
        Assert.False(watch.Observe(Colour(0.42), 130));
        Assert.False(watch.Observe(Colour(0.12), 160));
        Assert.True(watch.Observe(Empty, 190));
    }

    /// <summary>
    /// The first real death with the bar watcher ended in its six-second fallback: the player's box does
    /// not linger at zero. Low red, a moment hidden, then empty, is a fall.
    /// </summary>
    [Fact]
    public void Low_red_then_a_short_hidden_moment_then_empty_is_a_fall()
    {
        var watch = new HpBar.ZeroWatch();

        watch.Observe(Colour(0.05), 1000);
        watch.Observe(Hidden, 1100);
        Assert.True(watch.Observe(Empty, 1300));
    }

    [Fact]
    public void Low_red_long_ago_does_not_make_a_later_empty_a_fall()
    {
        var watch = new HpBar.ZeroWatch();

        watch.Observe(Colour(0.05), 1000);
        watch.Observe(Hidden, 1100);
        Assert.False(watch.Observe(Empty, 1000 + HpBar.ZeroWatch.MaxGap + 50));
        Assert.False(watch.Observe(Empty, 1000 + HpBar.ZeroWatch.MaxGap + 80));
    }

    /// <summary>
    /// Measured at the move menu with nobody hit: the box hid with the bar full and came back as seven
    /// seconds of grey. That is not a fall.
    /// </summary>
    [Fact]
    public void Grey_arriving_after_a_full_bar_and_a_hidden_stretch_is_not_a_fall()
    {
        var watch = new HpBar.ZeroWatch();

        watch.Observe(Colour(1.0), 0);
        watch.Observe(Hidden, 59_700);

        for (var ms = 61_064; ms < 68_000; ms += 100)
        {
            Assert.False(watch.Observe(Empty, ms));
        }
    }

    [Fact]
    public void A_high_bar_straight_to_empty_needs_two_readings()
    {
        var watch = new HpBar.ZeroWatch();

        watch.Observe(Colour(0.6), 0);
        Assert.False(watch.Observe(Empty, 15));
        Assert.True(watch.Observe(Empty, 30));
    }

    [Fact]
    public void Colour_measures_how_much_is_left() =>
        Assert.Equal(53 / 85.0, HpBar.Measure(Row((Green, 53), (Track, 32))).Fill, 3);

    private static HpBarReading Hidden => new(HpBarState.Hidden, 0);

    private static HpBarReading Empty => new(HpBarState.Empty, 0);

    private static HpBarReading Colour(double fill) => new(HpBarState.Filled, fill);

    private static byte[] Row(params ((byte R, byte G, byte B) Colour, int Count)[] runs)
    {
        var pixels = new List<byte>();

        foreach (var (colour, count) in runs)
        {
            for (var i = 0; i < count; i++)
            {
                pixels.AddRange([colour.B, colour.G, colour.R, 255]);
            }
        }

        return [.. pixels];
    }
}
