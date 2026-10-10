using PermaLocke.App.Views;
using PermaLocke.GameLink;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The black band of the emulator and the modes of the cap panel that fit it (2026-10-10).</summary>
public sealed class CapGeometryTests
{
    private static readonly AzaharScreenLayout Stacked = AzaharScreenLayout.Default;
    private static readonly AzaharScreenLayout Side = new(3, false, false);
    private static readonly AzaharScreenLayout Single = new(1, false, false);

    /// <summary>The player's window: maximised on a 1920×1080 screen, the picture the 1920×976 under the menu bar.</summary>
    private static readonly CapRect Player = new(0, 56, 1920, 976);

    public static IEnumerable<object[]> Windows()
    {
        foreach (var (w, h) in new[] { (320, 240), (640, 480), (800, 450), (960, 540), (1280, 720), (1366, 768), (1600, 900), (1920, 976), (2560, 1300), (3840, 2064), (900, 900), (500, 1200) })
        {
            foreach (var layout in new[] { 0, 1, 3 })
            {
                foreach (var swapped in new[] { false, true })
                {
                    foreach (var upright in new[] { false, true })
                    {
                        yield return [w, h, layout, swapped, upright];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void No_panel_ever_touches_the_game_or_leaves_the_window(int w, int h, int option, bool swapped, bool upright)
    {
        var picture = new CapRect(100, 56, w, h);
        var layout = new AzaharScreenLayout(option, swapped, upright);

        foreach (var dpi in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var plan = CapGeometry.Plan(picture, layout, dpi, new CapOptions());

            foreach (var slot in new[] { plan.Main, plan.Memorial }.OfType<CapSlot>())
            {
                Assert.False(slot.Box.Intersects(plan.Content), $"{slot.Mode} over the game at {w}x{h} dpi {dpi}");
                Assert.True(slot.Box.Left >= picture.Left && slot.Box.Top >= picture.Top, $"{slot.Mode} out at the top or left");
                Assert.True(slot.Box.Right <= picture.Right && slot.Box.Bottom <= picture.Bottom, $"{slot.Mode} out at the bottom or right");
                Assert.Equal(slot.Cols * slot.Cell, slot.Box.Width);
                Assert.Equal(slot.Rows * slot.Cell, slot.Box.Height);
                Assert.True(slot.Cell >= 1);
            }

            if (plan.Main is { } main && plan.Memorial is { } memorial)
            {
                Assert.False(main.Box.Intersects(memorial.Box));
                Assert.Equal(main.Cell, memorial.Cell);
            }
        }
    }

    [Fact]
    public void The_players_window_gets_the_whole_column_filling_the_right_band_and_a_memorial_on_the_left()
    {
        var plan = CapGeometry.Plan(Player, Stacked, 1, new CapOptions());

        // 400×480 scaled to 976 high is 813 wide, centred: the band is what is left of the 1920.
        Assert.InRange(plan.Content.Left, 550, 556);
        Assert.InRange(plan.Content.Right, 1366, 1372);

        Assert.NotNull(plan.Main);
        Assert.Equal(CapMode.Column, plan.Main!.Mode);
        Assert.Equal(CapPanelKind.Party, plan.Main.Panel);
        Assert.True(plan.Main.Box.Left > plan.Content.Right, "the party is in the right band");

        // It fills the band: within one cell of its height, and wide in cells (up to the widest a column is drawn).
        Assert.InRange(plan.Main.Box.Height, 976 - 12 - plan.Main.Cell, 976 - 12);
        Assert.InRange(plan.Main.Cols, 132, CapGeometry.MaxColumnCols);

        Assert.NotNull(plan.Memorial);
        Assert.Equal(CapPanelKind.Memorial, plan.Memorial!.Panel);
        Assert.True(plan.Memorial.Box.Right < plan.Content.Left, "the memorial is in the left band");
    }

    [Fact]
    public void Side_by_side_puts_the_strip_over_the_screens_and_the_memorial_under_them()
    {
        var plan = CapGeometry.Plan(Player, Side, 1, new CapOptions());

        Assert.Equal(CapMode.Strip, plan.Main!.Mode);
        Assert.True(plan.Main.Box.Bottom < plan.Content.Top);
        Assert.NotNull(plan.Memorial);
        Assert.True(plan.Memorial!.Box.Top > plan.Content.Bottom);
    }

    [Fact]
    public void A_layout_that_is_not_computed_hides_the_panel_instead_of_guessing()
    {
        foreach (var option in new[] { 2, 4, 5, 6, 7, 99 })
        {
            var plan = CapGeometry.Plan(Player, new AzaharScreenLayout(option, false, false), 1, new CapOptions());
            Assert.Null(plan.Main);
            Assert.Null(plan.Memorial);
        }
    }

    [Fact]
    public void The_mode_follows_the_room_from_the_column_down_to_the_compact_plaque_and_then_nothing()
    {
        // A window that keeps the shape of the picture and shrinks: the band shrinks with it.
        var modes = new List<CapMode>();
        foreach (var width in new[] { 2400, 1920, 1600, 1280, 1100, 960, 800, 640, 480, 360, 180 })
        {
            var plan = CapGeometry.Plan(new CapRect(0, 0, width, (int)(width * 0.5)), Stacked, 1, new CapOptions());
            modes.Add(plan.Main?.Mode ?? CapMode.Hidden);
        }

        // Never richer while smaller.
        var rank = modes.Select(m => m switch { CapMode.Column => 3, CapMode.Slim => 2, CapMode.Compact => 1, _ => 0 }).ToList();
        for (var i = 1; i < rank.Count; i++) Assert.True(rank[i] <= rank[i - 1], string.Join(",", modes));

        Assert.Equal(CapMode.Column, modes[0]);
        Assert.Contains(CapMode.Slim, modes);
        Assert.Equal(CapMode.Hidden, modes[^1]);
    }

    [Fact]
    public void A_forced_mode_is_the_richest_that_fits_up_to_it()
    {
        var compact = CapGeometry.Plan(Player, Stacked, 1, new CapOptions(Forced: CapMode.Compact));
        Assert.Equal(CapMode.Compact, compact.Main!.Mode);
        Assert.Null(compact.Memorial);

        var slim = CapGeometry.Plan(Player, Stacked, 1, new CapOptions(Forced: CapMode.Slim));
        Assert.Equal(CapMode.Slim, slim.Main!.Mode);

        // Forcing the column in a window that cannot hold it gives what does fit.
        var small = CapGeometry.Plan(new CapRect(0, 0, 900, 450), Stacked, 1, new CapOptions(Forced: CapMode.Column));
        Assert.NotEqual(CapMode.Column, small.Main?.Mode);
    }

    [Fact]
    public void The_cell_never_passes_what_the_player_allows()
    {
        var plan = CapGeometry.Plan(Player, Stacked, 1, new CapOptions(MaxCell: 2));
        Assert.Equal(2, plan.Main!.Cell);
        Assert.Equal(2, plan.Memorial!.Cell);
    }

    [Fact]
    public void A_window_on_the_edge_between_two_modes_does_not_flicker_while_it_is_resized_a_pixel_at_a_time()
    {
        // Column needs about 132×250 cells at 2 px: a band 264 wide and 500 high is its edge.
        CapMode? previous = null;
        var changes = 0;
        CapMode? last = null;

        for (var width = 1100; width < 1180; width++)
        {
            // The picture grows pixel by pixel and shrinks back, the way a hand drags a corner.
            foreach (var w in new[] { width, width - 1, width })
            {
                var plan = CapGeometry.Plan(new CapRect(0, 0, w, 560), Stacked, 1, new CapOptions(), previous);
                previous = plan.Main?.Mode;
                if (previous != last) changes++;
                last = previous;
            }
        }

        Assert.True(changes <= 6, $"{changes} changes of mode while dragging 80 pixels");
    }

    [Fact]
    public void The_dpi_only_widens_the_safety_margin()
    {
        var one = CapGeometry.Plan(Player, Stacked, 1, new CapOptions());
        var two = CapGeometry.Plan(Player, Stacked, 2, new CapOptions());
        Assert.True(two.Main!.Box.Left >= one.Main!.Box.Left - one.Main.Cell);
        Assert.Equal(6, CapGeometry.Safety(1));
        Assert.Equal(12, CapGeometry.Safety(2));
    }

    [Fact]
    public void The_size_of_the_picture_follows_the_layout_and_the_turn()
    {
        Assert.Equal((400, 480), CapGeometry.PictureOf(Stacked));
        Assert.Equal((480, 400), CapGeometry.PictureOf(new AzaharScreenLayout(0, false, true)));
        Assert.Equal((400, 240), CapGeometry.PictureOf(Single));
        Assert.Equal((320, 240), CapGeometry.PictureOf(new AzaharScreenLayout(1, true, false)));
        Assert.Equal((720, 240), CapGeometry.PictureOf(Side));
        Assert.Null(CapGeometry.PictureOf(new AzaharScreenLayout(2, false, false)));
    }
}
