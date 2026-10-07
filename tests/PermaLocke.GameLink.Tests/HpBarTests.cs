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

    /// <summary>
    /// The orange floor of the Iki Town battle ring, measured along the bar's own rows on the killcam of a death by
    /// poison on 2026-09-22, when the box had already gone (§165).
    /// </summary>
    private static readonly (byte R, byte G, byte B) Floor = (170, 110, 65);

    /// <summary>The brightest cell of that same floor.</summary>
    private static readonly (byte R, byte G, byte B) BrightFloor = (186, 125, 85);

    /// <summary>
    /// With no box on screen the floor showed through where the bar goes, and read as a bar at 97 % of colour: two
    /// deaths waited the whole six seconds for a bar that was not there. The floor is warm and saturated, which was
    /// all the old test asked for; the bar's colours are its own three.
    /// </summary>
    [Fact]
    public void The_floor_where_the_box_is_not_is_never_a_bar()
    {
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((Floor, 85))));
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((BrightFloor, 85))));
        Assert.Equal(HpBarState.Hidden, HpBar.Classify(Row((Floor, 60), (BrightFloor, 25))));
    }

    /// <summary>
    /// A bar seen through the emulator's scaling is not pixel-exact, and still has to read as colour.
    /// </summary>
    [Theory]
    [InlineData(120, 230, 70)]
    [InlineData(240, 190, 60)]
    [InlineData(230, 30, 40)]
    public void A_bar_a_little_off_its_measured_colour_is_still_a_bar(byte r, byte g, byte b) =>
        Assert.Equal(HpBarState.Filled, HpBar.Classify(Row(((r, g, b), 85))));

    /// <summary>
    /// Poison and Stealth Rock, measured on 2026-09-22: the tables only give the fall once the game has already shown
    /// it, so the watch starts with the bar at zero and no colour ever comes. An empty box is then the fall itself.
    /// </summary>
    [Fact]
    public void An_empty_box_with_no_colour_before_it_is_a_fall()
    {
        var watch = new HpBar.ZeroWatch();

        Assert.False(watch.Observe(Hidden, 0));
        Assert.False(watch.Observe(Empty, 689));
        Assert.False(watch.Observe(Empty, 699));
        Assert.True(watch.Observe(Empty, 709));
    }

    /// <summary>And a single empty reading among hidden ones is not: that is a frame, not a bar at zero.</summary>
    [Fact]
    public void One_empty_reading_between_hidden_ones_is_not_a_fall()
    {
        var watch = new HpBar.ZeroWatch();

        Assert.False(watch.Observe(Empty, 100));
        Assert.False(watch.Observe(Hidden, 110));
        Assert.False(watch.Observe(Empty, 120));
        Assert.False(watch.Observe(Hidden, 130));
    }

    /// <summary>
    /// With the box never on screen there is nothing to wait for: in the fifty falls measured the bar was seen at zero
    /// within 1889 ms at the worst, so a box that has not appeared by then is one that already went.
    /// </summary>
    [Fact]
    public void A_box_that_never_appears_is_not_waited_for_the_whole_six_seconds()
    {
        var watch = new HpBar.ZeroWatch();

        watch.Observe(Hidden, 0);
        Assert.False(watch.GiveUpWithoutBox(1_900));
        Assert.True(watch.GiveUpWithoutBox(HpBar.ZeroWatch.NoBoxLimit));

        // Pero una barra que sí se ha visto se espera hasta el final: es una animación larga tapándola.
        var seen = new HpBar.ZeroWatch();
        seen.Observe(Colour(0.5), 0);
        seen.Observe(Hidden, 100);
        Assert.False(seen.GiveUpWithoutBox(5_000));
    }


    /// <summary>
    /// Two real deaths (2026-10-06 and 10-07) read red, then hidden, and never empty: the game took the box away without showing
    /// it at zero, and the ceremony waited its six seconds with the Pokémon long gone (§234).
    /// </summary>
    [Fact]
    public void A_bar_draining_to_red_whose_box_then_goes_is_the_fall()
    {
        var watch = new HpBar.ZeroWatch();

        Assert.False(watch.Observe(Colour(0.49), 188));
        Assert.False(watch.Observe(Colour(0.24), 223));
        Assert.False(watch.Observe(Colour(0.06), 251));
        Assert.False(watch.Observe(Hidden, 266));            // una sola lectura oculta puede ser una captura fallida
        Assert.True(watch.Observe(Hidden, 276));
    }

    [Fact]
    public void A_box_hidden_with_a_steady_low_bar_is_a_fall_only_after_a_while()
    {
        var watch = new HpBar.ZeroWatch();

        Assert.False(watch.Observe(Colour(0.06), 0));
        Assert.False(watch.Observe(Hidden, 30));              // una lectura sin bajar: puede ser el principio de una animación
        Assert.False(watch.GiveUpHiddenAfterLow(2_000));       // un ataque largo tapa la caja más que esto
        Assert.True(watch.GiveUpHiddenAfterLow(30 + HpBar.ZeroWatch.HiddenAfterLowLimit));

        // Una barra con mucho color no cuenta: es el menú o una animación (el caso del §114 quater).
        var full = new HpBar.ZeroWatch();
        full.Observe(Colour(0.80), 0);
        full.Observe(Hidden, 30);
        Assert.False(full.GiveUpHiddenAfterLow(30_000));
    }
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
