using System.IO;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>Where everything of the sea goes (2026-10-10): inside its window, outside the notch, never over another thing.</summary>
public sealed class CapWorldLayoutTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var panel in new[] { CapPanelKind.Party, CapPanelKind.Deep })
        {
            foreach (var (mode, cols, rows) in new[]
                     {
                         (CapMode.Column, 132, 250), (CapMode.Column, 178, 321), (CapMode.Column, 200, 337), (CapMode.Column, 150, 280),
                         (CapMode.Slim, 76, 210), (CapMode.Slim, 100, 321), (CapMode.Slim, 110, 330),
                         (CapMode.Compact, 60, 80), (CapMode.Compact, 84, 96),
                         (CapMode.Strip, 420, 46), (CapMode.Strip, 636, 52), (CapMode.Strip, 954, 78)
                     })
            {
                // With and without an ear on either side.
                foreach (var ear in new[] { 0, 27, -27 })
                {
                    if (ear != 0 && mode is CapMode.Strip or CapMode.Compact) continue;
                    yield return [panel, mode, cols + Math.Abs(ear), rows, ear];
                }
            }
        }
    }

    private static (int, int, int) NotchOf(int ear, int cols, int rows) =>
        ear == 0 ? (0, 0, 0) : ear > 0 ? (0, ear, rows / 2) : (cols + ear, cols, rows / 2);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Everything_is_inside_the_window_and_outside_the_notch_and_the_creatures_do_not_overlap(
        CapPanelKind panel, CapMode mode, int cols, int rows, int ear)
    {
        var layout = CapWorld.Arrange(mode, panel, cols, rows, NotchOf(ear, cols, rows));
        var window = new CapRect(0, 0, cols, rows);
        var notch = new CapRect(layout.Notch.X0, 0, layout.Notch.X1 - layout.Notch.X0, layout.Notch.Y1);

        var things = new List<(string Name, CapRect Box)>
        {
            ("main", layout.Main), ("reef", layout.Reef), ("buoy", layout.Buoy), ("face", layout.BuoyFace), ("tag", layout.Tag),
            ("lanterns", layout.Lanterns), ("strata", layout.Strata), ("sun", layout.Sun)
        };
        things.AddRange(layout.Creatures.SelectMany(c => new[] { ($"sprite {c.Index}", c.Sprite), ($"info {c.Index}", c.Info) }));

        foreach (var (name, box) in things)
        {
            if (box.Width == 0 && box.Height == 0) continue;
            Assert.True(window.Contains(box), $"{panel} {mode} {cols}x{rows}: {name} {box} outside the window");

            // Everything above the floor lives in the full-height part; only the reef and the strata may use the ear.
            if (notch.Width > 0 && name is not ("reef" or "strata" or "main") && box.Top < notch.Bottom)
            {
                Assert.False(box.Intersects(notch), $"{panel} {mode} {cols}x{rows} ear {ear}: {name} {box} in the notch");
            }
        }

        // The sprites and their texts do not overlap one another.
        for (var i = 0; i < layout.Creatures.Length; i++)
        {
            var a = layout.Creatures[i];
            Assert.False(a.Sprite.Intersects(a.Info), $"{panel} {mode}: sprite {i} over its own text");
            for (var j = i + 1; j < layout.Creatures.Length; j++)
            {
                var b = layout.Creatures[j];
                Assert.False(a.Sprite.Intersects(b.Sprite), $"{panel} {mode} {cols}x{rows}: sprites {i} and {j} overlap");
                Assert.False(a.Info.Intersects(b.Info), $"{panel} {mode} {cols}x{rows}: texts {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void The_party_has_six_creatures_and_the_deep_has_none()
    {
        Assert.Equal(6, CapWorld.Arrange(CapMode.Column, CapPanelKind.Party, 178, 321, (0, 0, 0)).Creatures.Length);
        Assert.Empty(CapWorld.Arrange(CapMode.Column, CapPanelKind.Deep, 178, 321, (0, 0, 0)).Creatures);
    }

    [Fact]
    public void Wireframes_of_the_compositions_are_written_when_asked()
    {
        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is not { Length: > 0 } dir) return;

        foreach (var (name, mode, panel, cols, rows, ear) in new[]
                 {
                     ("columna-party", CapMode.Column, CapPanelKind.Party, 207, 321, 27), ("columna-deep", CapMode.Column, CapPanelKind.Deep, 207, 321, -27),
                     ("estrecha", CapMode.Slim, CapPanelKind.Party, 100, 321, 0), ("compacta", CapMode.Compact, CapPanelKind.Party, 70, 90, 0),
                     ("tira-party", CapMode.Strip, CapPanelKind.Party, 636, 52, 0), ("tira-deep", CapMode.Strip, CapPanelKind.Deep, 636, 52, 0)
                 })
        {
            var layout = CapWorld.Arrange(mode, panel, cols, rows, NotchOf(ear, cols, 321 / 2 * 2 == 0 ? rows : rows));
            CapSnap.Save(Path.Combine(dir, $"esquema-{name}.png"), CapWireframe.Paint(layout), mode == CapMode.Strip ? 2 : 3);
        }
    }
}
