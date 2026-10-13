using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The Sharpedo of a sea in danger (2026-10-14): drawn from its sheet now that Sharpedo has one, and faint as the danger is faint.</summary>
public sealed class CapProwlTests
{
    private static int Painted(double strength, double phase)
    {
        var layout = CapWorld.Arrange(CapMode.Column, CapPanelKind.Deep, 180, 321, (0, 0, 0));
        var sheet = new CapSheet(layout.Cols, layout.Rows);
        CapVisitors.Prowl(sheet, layout, CapSeaPalette.Of(CapMood.Danger), new CapSeaContext(Seed: 3), phase, strength);
        var n = 0;
        for (var y = 0; y < sheet.H; y++) for (var x = 0; x < sheet.W; x++) if (sheet.Solid(x, y)) n++;
        return n;
    }

    [Fact]
    public void The_danger_shark_is_painted_from_the_sharpedo_sheet()
    {
        Assert.NotNull(CritterArt.All[319].Sheet);
        // somewhere along its crossing it is inside the window and paints a body's worth of pixels
        var most = Enumerable.Range(0, 80).Max(k => Painted(1, k * 2.5));
        Assert.True(most > 600, $"painted {most}");

        // PERMALOCKE_SNAP_DIR: the frame where it paints most, as raw BGRA (<w>x<h>), to look at
        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is { Length: > 0 } dir)
        {
            var best = Enumerable.Range(0, 80).MaxBy(k => Painted(1, k * 2.5));
            var layout = CapWorld.Arrange(CapMode.Column, CapPanelKind.Deep, 180, 321, (0, 0, 0));
            var sheet = new CapSheet(layout.Cols, layout.Rows);
            CapVisitors.Prowl(sheet, layout, CapSeaPalette.Of(CapMood.Danger), new CapSeaContext(Seed: 3), best * 2.5, 1);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"prowl_{sheet.W}x{sheet.H}.bgra"), sheet.B);
        }
    }

    [Fact]
    public void Without_danger_there_is_no_shark()
    {
        Assert.Equal(0, Enumerable.Range(0, 80).Max(k => Painted(0, k * 2.5)));
    }
}
