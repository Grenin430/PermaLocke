using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>An upgrade going into the bag (2026-10-09): arrows in beats, a meter, gold and lime and never the item's colours.</summary>
public sealed class BoostStyleTests
{
    private static ItemScene.Item Candy(int power = 2, int seed = 6, uint tint = 0xFF4060C0)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "CARAMELO RARO", 1, ItemCategory.Boost, power, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void The_stronger_the_upgrade_the_more_arrows()
    {
        Assert.Equal([2, 3, 4, 5], Enumerable.Range(0, 4).Select(BoostStyle.ArrowsFor).ToArray());
        Assert.Equal(72, ItemStyles.For(ItemCategory.Boost).Height);
        Assert.True(ItemTimeline.For(ItemCategory.Boost, 3).Length > ItemTimeline.For(ItemCategory.Boost, 0).Length);
    }

    [Fact]
    public void The_arrows_come_in_beats_and_not_gradually()
    {
        var item = Candy(0);
        var phases = ItemStyles.For(ItemCategory.Boost).Phases(item);
        var born = phases.ClimaxAt + 0.05;

        var a = new ItemScene(1, 72);
        var b = new ItemScene(1, 72);
        var c = new ItemScene(1, 72);
        a.Render(item, born + 0.09);
        b.Render(item, born + 0.12);
        c.Render(item, born + 0.20);

        // Two moments of the same beat are the same frame; the next beat is another one.
        Assert.Equal(a.Pixels, b.Pixels);
        Assert.NotEqual(b.Pixels, c.Pixels);
    }

    [Fact]
    public void The_colours_are_gold_and_lime_whatever_the_item_is_and_the_plate_says_mejora()
    {
        var phases = ItemStyles.For(ItemCategory.Boost).Phases(Candy());
        var blue = new ItemScene(1, 72);
        var red = new ItemScene(1, 72);
        blue.Render(Candy(2, 6, 0xFF2040D0), phases.ClimaxAt + 0.3);
        red.Render(Candy(2, 6, 0xFFD02020), phases.ClimaxAt + 0.3);
        Assert.Equal(blue.Pixels, red.Pixels);

        // «MEJORA» is 23 cells: the amount starts 4 after it, 91 columns in.
        var gold = 0xFFFFDC7Au;
        Assert.True(Enumerable.Range(36, 5).Any(y => At(blue, 91, y) == gold), "la cantidad no sigue a MEJORA");
    }

    [Fact]
    public void The_bag_grows_for_an_instant_when_it_has_it_and_does_not_shrink_first()
    {
        var item = Candy();
        var phases = ItemStyles.For(ItemCategory.Boost).Phases(item);
        var landing = phases.WrapAt - 0.02 + 0.22;

        int Lit(double t)
        {
            var scene = new ItemScene(1, 72);
            scene.Render(item, t);
            var rows = 0;

            // Rows of the bag's column that have anything in them: the bag is taller when it grows.
            for (var gy = 0; gy < 72; gy++)
            {
                if (Enumerable.Range(14, 22).Any(gx => At(scene, gx, gy) >> 24 == 0xFF)) rows++;
            }

            return rows;
        }

        Assert.True(Lit(landing + 0.07) > Lit(landing + 0.6) - 2, "the bag does not grow");
    }
}
