using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The Z-Crystal going into the bag (2026-10-09): its own height, the two frames of white at any rate, the plate.</summary>
public sealed class ZCrystalStyleTests
{
    private static ItemScene.Item Crystal(int seed = 3, uint tint = 0xFFE05030, string name = "FIRIUM Z", int amount = 1)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, name, amount, ItemCategory.ZCrystal, 0, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void It_asks_for_88_rows_and_the_scene_is_that_tall()
    {
        Assert.Equal(88, ItemStyles.For(ItemCategory.ZCrystal).Height);
        Assert.Equal(88, ItemScene.HeightFor(Crystal()));
        Assert.InRange(ItemStyles.For(ItemCategory.ZCrystal).Phases(Crystal()).Length, 2.9, 3.1);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void The_flash_gives_two_visible_frames_whatever_the_frame_rate(int framesPerSecond)
    {
        var item = Crystal();
        var scene = new ItemScene(1, 88);
        var impact = ZCrystalStyle.ImpactAt(ItemStyles.For(ItemCategory.ZCrystal).Phases(item));
        var white = 0xFFFAF8FFu;
        var step = 1.0 / framesPerSecond;

        for (var shift = 0; shift < 10; shift++)
        {
            int disc = 0, ring = 0;
            for (var t = impact - 0.2 + (shift * step / 10); t < impact + 0.2; t += step)
            {
                scene.Render(item, t);

                // Off the arms of the cross: the disc reaches here and the ring does not, and the ring reaches the other cell.
                if (At(scene, 30, 30) == white) disc++;
                if (At(scene, 20, 30) == white) ring++;
            }

            Assert.True(disc >= 1 && ring >= 1, $"{framesPerSecond} fps, desfase {shift}: disco {disc}, anillo {ring}");
        }
    }

    [Fact]
    public void A_seed_gives_six_eight_or_ten_tips_and_the_emblem_is_drawn_then_gone()
    {
        var tips = Enumerable.Range(0, 60).Select(n => ZCrystalStyle.TipsFor(ItemScene.SeedFor(808, n))).Distinct().Order().ToArray();
        Assert.Equal([6, 8, 10], tips);

        var item = Crystal();
        var scene = new ItemScene(1, 88);
        var impact = ZCrystalStyle.ImpactAt(ItemStyles.For(ItemCategory.ZCrystal).Phases(item));

        int Lit() => Enumerable.Range(0, scene.Width * scene.Height).Count(i => scene.Pixels[(i * 4) + 3] != 0);

        scene.Render(item, impact + 0.45);
        var with = Lit();
        scene.Render(item, impact + 0.95);
        var without = Lit();

        // Two different moments after the impact: the emblem, and then not.
        Assert.True(with > without);
    }

    [Fact]
    public void The_plate_says_cristal_z_and_every_type_gets_its_own_colour()
    {
        var scene = new ItemScene(1, 88);
        scene.Render(Crystal(), 2.3);

        // The amount starts right after «CRISTAL Z» (35 cells), 115 columns in.
        var gold = 0xFFFFDC7Au;
        Assert.True(Enumerable.Range(44, 5).Any(y => At(scene, 115, y) == gold), "la cantidad no sigue a CRISTAL Z");

        var red = new ItemScene(1, 88);
        var blue = new ItemScene(1, 88);
        red.Render(Crystal(3, 0xFFD02020), 2.0);
        blue.Render(Crystal(3, 0xFF2040D0), 2.0);
        Assert.NotEqual(red.Pixels, blue.Pixels);
    }
}
