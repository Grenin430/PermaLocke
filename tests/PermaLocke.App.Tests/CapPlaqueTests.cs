using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>The cap panel as a showcase (§239, redesigned 2026-10-10): every mode draws every scenario, inside its grid, the same every time.</summary>
public sealed class CapPlaqueTests
{
    /// <summary>A stand-in for a cartridge icon: 40 by 30, transparent around a coloured blob.</summary>
    internal static BitmapSource Icon(int seed)
    {
        var tint = (byte)((seed * 53) % 200 + 40);
        var pixels = new byte[40 * 30 * 4];
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var i = ((y * 40) + x) * 4;
                if (Math.Pow((x - 20) / 12.0, 2) + Math.Pow((y - 16) / 11.0, 2) > 1) continue;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(tint / 2), (byte)(255 - tint), tint, 255);
            }
        }

        var bitmap = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    internal static IReadOnlyList<CapScenario> Scenarios() => CapScenarios.All(Icon);

    /// <summary>The grids each mode is drawn on in the tests: its design size, and a bigger one, as a real band gives.</summary>
    internal static IEnumerable<(CapMode Mode, int Cols, int Rows)> Grids()
    {
        yield return (CapMode.Column, 132, 250);
        yield return (CapMode.Column, 178, 321);
        yield return (CapMode.Column, 200, 337);
        
        yield return (CapMode.Slim, 110, 330);
        yield return (CapMode.Compact, 60, 80);
        yield return (CapMode.Compact, 84, 96);
        yield return (CapMode.Strip, 420, 46);
        yield return (CapMode.Strip, 636, 52);
        yield return (CapMode.Strip, 954, 78);
    }

    [Fact]
    public void Every_mode_draws_every_scenario_on_its_grid_with_something_in_it()
    {
        foreach (var scenario in Scenarios())
        {
            foreach (var (mode, cols, rows) in Grids())
            {
                foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
                {
                    if (panel == CapPanelKind.Deep && mode == CapMode.Compact) continue;

                    var sheet = CapPlaque.Render(scenario.At(0), mode, cols, rows, 0, panel);
                    Assert.Equal(cols, sheet.W);
                    Assert.Equal(rows, sheet.H);
                    Assert.Contains(sheet.B.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
                }
            }
        }
    }

    [Fact]
    public void A_frame_is_the_same_wherever_and_whenever_it_is_painted()
    {
        var data = Scenarios()[0].At(0);
        foreach (var t in new[] { 0.0, 0.37, 5.5 })
        {
            var a = CapPlaque.Render(data, CapMode.Column, 178, 321, t);
            var b = CapPlaque.Render(data, CapMode.Column, 178, 321, t);
            Assert.True(a.B.AsSpan().SequenceEqual(b.B), $"two frames at {t} s differ");
        }
    }

    [Fact]
    public void The_panel_leaves_no_cell_outside_the_corners_it_cuts_and_keeps_the_cup_over_the_case()
    {
        var sheet = CapPlaque.Render(Scenarios()[0].At(0), CapMode.Column, 178, 321);

        // The cup stands above the case: the very first row has cells only in the middle.
        var firstRow = Enumerable.Range(0, sheet.W).Where(x => sheet.Solid(x, 0)).ToList();
        Assert.NotEmpty(firstRow);
        Assert.InRange(firstRow.Min(), (sheet.W / 2) - 8, sheet.W / 2);
        Assert.InRange(firstRow.Max(), sheet.W / 2, (sheet.W / 2) + 8);

        // The four corners of the case are cut.
        Assert.False(sheet.Solid(0, 12));
        Assert.False(sheet.Solid(sheet.W - 1, 12));
        Assert.False(sheet.Solid(0, sheet.H - 1));
        Assert.False(sheet.Solid(sheet.W - 1, sheet.H - 1));
    }

    [Fact]
    public void The_still_part_moves_only_where_the_moving_part_says()
    {
        // Two moments of the same scenario differ in the fuse and nowhere the still part painted a niche's icon.
        var data = Scenarios()[0].At(0);
        var a = CapPlaque.Render(data, CapMode.Column, 178, 321, 0.0);
        var b = CapPlaque.Render(data, CapMode.Column, 178, 321, 0.5);
        var layout = CapComposer.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, data, true);

        var differing = new List<(int X, int Y)>();
        for (var y = 0; y < a.H; y++)
        {
            for (var x = 0; x < a.W; x++)
            {
                for (var k = 0; k < 4; k++)
                {
                    if (a.B[(((y * a.W) + x) * 4) + k] == b.B[(((y * a.W) + x) * 4) + k]) continue;
                    differing.Add((x, y));
                    break;
                }
            }
        }

        Assert.NotEmpty(differing);

        // The flame and its sparks: everything that changed is on the fuse's rows, up to its chip, or beside a niche's HP (none here).
        var fuse = layout.Fuse;
        Assert.All(differing, c => Assert.True(c.Y >= fuse.Top - 22 && c.Y <= fuse.Bottom, $"cell {c} moved outside the fuse"));
    }

    [Fact]
    public void Snapshots_of_every_scenario_in_every_mode_are_written_when_asked()
    {
        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is not { Length: > 0 } dir) return;

        foreach (var scenario in Scenarios())
        {
            foreach (var (mode, cols, rows) in new[]
                     {
                         (CapMode.Column, 178, 321), (CapMode.Column, 132, 250), (CapMode.Slim, 100, 321), (CapMode.Compact, 70, 90),
                         (CapMode.Strip, 636, 52)
                     })
            {
                var sheet = CapPlaque.Render(scenario.At(0), mode, cols, rows);
                CapSnap.Save(Path.Combine(dir, $"cap-{scenario.Name}-{mode}-{cols}x{rows}.png"), sheet, mode == CapMode.Strip ? 2 : 3);
            }

            var memorial = CapPlaque.Render(scenario.At(0), CapMode.Column, 178, 321, 0, CapPanelKind.Deep);
            CapSnap.Save(Path.Combine(dir, $"cap-{scenario.Name}-memorial.png"), memorial, 3);
        }
    }
}
