using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// Pictures of the gacha to look at (§189): with <c>PERMALOCKE_PIXEL_DIR</c> set, a sheet of frames of a pull of each
/// kind — a plain one, one that climbs twice to a legendary, a shiny — at the moments that matter, as PNG.
/// </summary>
public sealed class GachaPreviews
{
    private static readonly CapsuleBanner Pocho = new("POCHO", 1, [0.15, 0.60, 0.25, 0, 0], "75", false);
    private static readonly CapsuleBanner Bueno = new("BUENO", 3, [0, 0, 0.15, 0.60, 0.25], "300", true);

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PIXEL_DIR");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var sprite = Previews.Dragon;
        IReadOnlyList<CapsuleShelfItem> shelf = [new(sprite, 4), new(sprite, 1), new(null, 0), new(sprite, 2)];

        var pulls = new (string Name, CapsuleBanner Banner, CapsuleRoll Roll)[]
        {
            ("comun", Pocho, new CapsuleRoll(CapsulePlay.StepsFor(0, 0), sprite, false, false, 12)),
            ("legendario", Bueno, new CapsuleRoll(CapsulePlay.StepsFor(2, 4), sprite, false, true, 7)),
            ("variocolor", Pocho, new CapsuleRoll(CapsulePlay.StepsFor(0, 2), sprite, true, false, 31, Streak: 7)),
            ("bueno", Bueno, new CapsuleRoll(CapsulePlay.StepsFor(2, 3), sprite, false, false, 44, Streak: 2)),
        };

        double[] times =
        [
            0.3, 1.5, 3.3, 4.2,
            CapsuleTimeline.WobbleStart + 0.2, CapsuleTimeline.UpgradeAt(0) + 0.03, CapsuleTimeline.UpgradeAt(1) + 0.05,
            CapsuleTimeline.UpgradeAt(2) + 0.03,
            CapsuleTimeline.Open - 0.2, CapsuleTimeline.Open + 0.03, CapsuleTimeline.Open + 0.2, CapsuleTimeline.Emerge + 0.23,
            CapsuleTimeline.Emerge + 0.5, CapsuleTimeline.Revealed + 0.2, CapsuleTimeline.Revealed + 1.0, CapsuleTimeline.Revealed + 2.2
        ];

        foreach (var (name, banner, roll) in pulls)
        {
            const int columns = 4;
            var scene = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);
            var rows = (times.Length + columns - 1) / columns;
            var sheet = new byte[scene.Width * columns * scene.Height * rows * 4];

            for (var i = 0; i < times.Length; i++)
            {
                scene.Render(new CapsuleSceneState(banner, 5, roll, times[i], shelf), 20 + times[i], toScreen: false);
                var left = (i % columns) * scene.Width;
                var top = (i / columns) * scene.Height;
                for (var y = 0; y < scene.Height; y++)
                {
                    Buffer.BlockCopy(scene.Pixels, y * scene.Width * 4, sheet, (((top + y) * scene.Width * columns) + left) * 4, scene.Width * 4);
                }
            }

            PngFile.Write(sheet, scene.Width * columns, scene.Height * rows, 2, Path.Combine(folder, $"gacha-{name}.png"));
        }
    }
}
