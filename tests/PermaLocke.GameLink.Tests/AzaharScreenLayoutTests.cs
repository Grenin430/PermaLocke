using PermaLocke.GameLink;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>How Azahar lays its screens out, read from qt-config.ini (2026-10-10).</summary>
public sealed class AzaharScreenLayoutTests
{
    [Fact]
    public void The_three_settings_are_read_and_the_default_markers_are_not_taken_for_values()
    {
        var layout = AzaharScreenLayout.Parse(
        [
            @"layout_option\default=false",
            "layout_option=3",
            @"swap_screen\default=true",
            "swap_screen=true",
            @"upright_screen\default=true",
            "upright_screen=false",
            "large_screen_proportion=4"
        ]);

        Assert.Equal(new AzaharScreenLayout(3, true, false), layout);
    }

    [Fact]
    public void Nothing_in_the_file_is_the_default_top_over_bottom()
    {
        Assert.Equal(AzaharScreenLayout.Default, AzaharScreenLayout.Parse(["[UI]", "foo=1"]));
        Assert.Equal(AzaharScreenLayout.Default, AzaharScreenLayout.Read(Path.Combine(Path.GetTempPath(), "no-such-qt-config.ini")));
    }

    [Fact]
    public void A_value_that_is_not_a_number_keeps_the_default()
    {
        Assert.Equal(0, AzaharScreenLayout.Parse(["layout_option=big"]).Option);
    }
}
