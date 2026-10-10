using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The cap panel as a sea (2026-10-10): every mode draws every scenario, inside its window, outside the notch, the same every time, and legibly.</summary>
public sealed class CapSeaTests
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

    /// <summary>The grids each mode is drawn on in the tests: its design size and a bigger one, as a real band gives; with and without an ear.</summary>
    internal static IEnumerable<(CapMode Mode, int Cols, int Rows, int Ear)> Grids()
    {
        yield return (CapMode.Column, 132, 250, 0);
        yield return (CapMode.Column, 180, 321, 27);
        yield return (CapMode.Column, 200, 337, 0);
        yield return (CapMode.Slim, 76, 210, 0);
        yield return (CapMode.Slim, 110, 330, 20);
        yield return (CapMode.Compact, 60, 80, 0);
        yield return (CapMode.Compact, 84, 96, 0);
        yield return (CapMode.Strip, 420, 46, 0);
        yield return (CapMode.Strip, 636, 52, 0);
        yield return (CapMode.Strip, 954, 78, 0);
    }

    /// <summary>The notch of a window of <paramref name="cols"/> with an ear on the side facing the game: the party's is on the left, the deep's on the right.</summary>
    internal static (int, int, int) NotchOf(CapPanelKind panel, int cols, int rows, int ear) =>
        ear == 0 ? (0, 0, 0) : panel == CapPanelKind.Party ? (0, ear, rows / 2) : (cols - ear, cols, rows / 2);

    [Fact]
    public void Every_mode_draws_every_scenario_on_its_grid_with_something_in_it_and_nothing_in_the_notch()
    {
        foreach (var scenario in Scenarios())
        {
            foreach (var (mode, cols, rows, ear) in Grids())
            {
                foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
                {
                    if (panel == CapPanelKind.Deep && mode == CapMode.Compact) continue;

                    var notch = NotchOf(panel, cols, rows, ear);
                    var sheet = CapPlaque.Replay(scenario, mode, cols, rows, 1.0, panel, notch);
                    Assert.Equal(cols, sheet.W);
                    Assert.Equal(rows, sheet.H);
                    Assert.True(sheet.B.Where((_, i) => i % 4 == 3).Count(a => a == 255) > cols * rows / 2, $"{scenario.Name} {mode} {panel} is mostly empty");

                    for (var y = 0; y < notch.Item3; y++)
                    {
                        for (var x = notch.Item1; x < notch.Item2; x++)
                        {
                            Assert.False(sheet.Solid(x, y), $"{scenario.Name} {mode} {panel}: a cell in the notch at {x},{y}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void A_frame_is_the_same_wherever_and_whenever_it_is_painted_and_in_any_order()
    {
        foreach (var scenario in Scenarios())
        {
            var times = new[] { 0.0, 2.05, 2.4, 3.1, 6.0, 12.0, 30.0 };
            var first = times.Select(t => CapPlaque.Replay(scenario, CapMode.Column, 180, 321, t).B.ToArray()).ToList();
            var again = times.Reverse().Select(t => CapPlaque.Replay(scenario, CapMode.Column, 180, 321, t).B.ToArray()).Reverse().ToList();
            for (var i = 0; i < times.Length; i++)
            {
                Assert.True(first[i].AsSpan().SequenceEqual(again[i]), $"{scenario.Name} at {times[i]} s differs");
            }
        }
    }

    [Fact]
    public void Every_picture_of_a_creature_stays_in_its_slot_and_its_text_in_its_box()
    {
        var data = Scenarios()[0].At(0);
        foreach (var (mode, cols, rows) in new[] { (CapMode.Column, 180, 321), (CapMode.Column, 132, 250), (CapMode.Slim, 100, 321), (CapMode.Strip, 636, 52) })
        {
            var view = new CapWorldView(mode, CapPanelKind.Party, cols, rows, (0, 0, 0));
            view.SetData(data);
            for (var i = 0; i < CapWorld.Creatures; i++)
            {
                var c = view.Creature(i)!;
                Assert.NotNull(c.Info);
                Assert.Equal(c.Slot.Info.Width, c.Info!.W);
                Assert.Equal(c.Slot.Info.Height, c.Info.H);

                // The beads of the life and the ticker are inside the creature's own box.
                if (c.Beads > 0)
                {
                    Assert.InRange(c.BeadsAt.X, c.Slot.Info.Left, c.Slot.Info.Right);
                    Assert.InRange(c.BeadsAt.X + (c.Beads * 4), c.Slot.Info.Left, c.Slot.Info.Right + 1);
                    Assert.InRange(c.BeadsAt.Y, c.Slot.Info.Top, c.Slot.Info.Bottom);
                }
            }
        }
    }

    [Fact]
    public void The_cap_is_as_big_as_the_buoy_lets_it_and_never_smaller_than_the_font_can_read()
    {
        var data = Scenarios()[0].At(0);
        foreach (var (mode, cols, rows, minHeight) in new[]
                 {
                     (CapMode.Column, 180, 321, 21), (CapMode.Column, 132, 250, 14), (CapMode.Slim, 100, 321, 14), (CapMode.Slim, 76, 210, 14)
                 })
        {
            var layout = CapWorld.Arrange(mode, CapPanelKind.Party, cols, rows, (0, 0, 0));
            var buoy = new CapBuoy();
            buoy.Build(layout, CapSeaPalette.Of(CapMood.Calm), data);
            Assert.NotNull(buoy.Body);

            // The lit numerals are the brightest cells of the face: find their height.
            var face = layout.BuoyFace;
            var lit = new List<int>();
            for (var y = face.Top - layout.Buoy.Top; y < face.Bottom - layout.Buoy.Top; y++)
            {
                for (var x = face.Left - layout.Buoy.Left; x < face.Right - layout.Buoy.Left; x++)
                {
                    var c = buoy.Body!.At(x, y);
                    if (((c >> 8) & 255) > 230) { lit.Add(y); break; }
                }
            }

            Assert.True(lit.Count >= minHeight, $"{mode} {cols}x{rows}: the cap is {lit.Count} cells tall, wanted {minHeight}");
        }
    }

    [Theory]
    [InlineData(CapMood.Calm)]
    [InlineData(CapMood.Battle)]
    [InlineData(CapMood.Danger)]
    [InlineData(CapMood.Mourning)]
    [InlineData(CapMood.Cap)]
    public void The_writing_reads_over_every_water_of_every_mood(CapMood mood)
    {
        var pal = CapSeaPalette.Of(mood);

        double Luma(uint c) => ((0.2126 * ((c >> 16) & 255)) + (0.7152 * ((c >> 8) & 255)) + (0.0722 * (c & 255))) / 255.0;
        double Ratio(uint a, uint b)
        {
            var (hi, lo) = Luma(a) > Luma(b) ? (Luma(a), Luma(b)) : (Luma(b), Luma(a));
            return (hi + 0.05) / (lo + 0.05);
        }

        // The letters against their own outline, and the outline against the lightest and the darkest water: the outline is what keeps them whole.
        Assert.True(Ratio(pal.Glow, pal.Ink) >= 10, $"{mood}: letters and outline {Ratio(pal.Glow, pal.Ink):F1}:1");
        Assert.True(Ratio(0xFFD860, pal.Ink) >= 9, $"{mood}: gold and outline");
        Assert.True(Ratio(pal.Ink, pal.Water[0]) >= 2.5, $"{mood}: the outline against the surface water {Ratio(pal.Ink, pal.Water[0]):F1}:1");

        // Every step of the water is lighter or darker than the one before, towards the abyss: a ramp, not noise.
        for (var i = 1; i < pal.Water.Length; i++) Assert.True(Luma(pal.Water[i]) < Luma(pal.Water[i - 1]), $"{mood}: step {i} is not darker");
    }

    [Fact]
    public void Snapshots_at_two_pixels_and_at_one_are_written_when_asked()
    {
        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is not { Length: > 0 } dir) return;

        foreach (var scenario in Scenarios())
        {
            foreach (var (mode, cols, rows, ear) in new[] { (CapMode.Column, 180, 321, 27), (CapMode.Slim, 100, 321, 0), (CapMode.Compact, 70, 90, 0), (CapMode.Strip, 636, 52, 0) })
            {
                var party = CapPlaque.Replay(scenario, mode, cols, rows, 3, CapPanelKind.Party, NotchOf(CapPanelKind.Party, cols, rows, ear));
                CapSnap.Save(Path.Combine(dir, $"mar-{scenario.Name}-{mode}-{(mode == CapMode.Compact ? "1px" : "2px")}.png"), party, mode == CapMode.Compact ? 1 : 2);
                if (mode == CapMode.Compact) continue;
                var deep = CapPlaque.Replay(scenario, mode, cols, rows, 3, CapPanelKind.Deep, NotchOf(CapPanelKind.Deep, cols, rows, ear));
                CapSnap.Save(Path.Combine(dir, $"mar-{scenario.Name}-{mode}-fondo-2px.png"), deep, 2);
            }
        }
    }
}
