using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>A berry going into the bag (2026-10-09): thrown, bouncing, chewed, with a hiccup.</summary>
public sealed class BerryStyleTests
{
    private static ItemScene.Item Berry(int seed = 4, uint tint = 0xFFE05070)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "BAYA ZIDRA", 1, ItemCategory.Berry, 0, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void A_seed_gives_one_or_two_bounces_and_two_or_three_chews()
    {
        var seeds = Enumerable.Range(0, 80).Select(n => ItemScene.SeedFor(157, n)).ToArray();

        Assert.Equal([1, 2], seeds.Select(BerryStyle.BouncesFor).Distinct().Order().ToArray());
        Assert.Equal([2, 3], seeds.Select(BerryStyle.ChewsFor).Distinct().Order().ToArray());
        Assert.Equal(64, ItemStyles.For(ItemCategory.Berry).Height);
    }

    [Fact]
    public void The_berry_leaves_the_bag_goes_above_the_rim_and_the_bag_chews_after()
    {
        var item = Berry();
        var phases = ItemStyles.For(ItemCategory.Berry).Phases(item);
        var scene = new ItemScene(1, 64);
        var seed = new ItemSeed(item.Seed);

        // At some moment of the flight there is something painted in the air over the bag that is not the plate.
        var inTheAir = false;
        for (var t = phases.In; t < phases.ClimaxAt + 0.3 && !inTheAir; t += 1 / 60.0)
        {
            scene.Render(item, t);
            for (var gy = 4; gy < 30 && !inTheAir; gy++)
            {
                for (var gx = 12; gx < 36; gx++)
                {
                    if (At(scene, gx, gy) >> 24 == 0xFF) inTheAir = true;
                }
            }
        }

        Assert.True(inTheAir, "the berry does not go up over the bag");

        // The bag is not the same shape while it chews as when it rests: the frames differ.
        var chews = BerryStyle.ChewsFor(item.Seed);
        var wrap = phases.ClimaxAt + 0.12 + (BerryStyle.BouncesFor(item.Seed) == 2 ? 0.20 : 0) + 0.16 + 0.04;
        var a = new ItemScene(1, 64);
        var b = new ItemScene(1, 64);
        a.Render(item, wrap + 0.03);
        b.Render(item, wrap + 0.09);
        Assert.NotEqual(a.Pixels, b.Pixels);
        Assert.InRange(chews, 2, 3);
        Assert.InRange(seed.Pick(1, 3), 0, 2);
    }

    [Fact]
    public void The_plate_says_baya_and_the_juice_is_the_colour_of_the_berry()
    {
        var scene = new ItemScene(1, 64);
        scene.Render(Berry(), 1.2);

        // «BAYA» is 15 cells: the amount starts 4 after it, 77 columns in.
        var gold = 0xFFFFDC7Au;
        Assert.True(Enumerable.Range(32, 5).Any(y => At(scene, 77, y) == gold), "la cantidad no sigue a BAYA");

        var red = new ItemScene(1, 64);
        var blue = new ItemScene(1, 64);
        red.Render(Berry(4, 0xFFD02020), 0.75);
        blue.Render(Berry(4, 0xFF2040D0), 0.75);
        Assert.NotEqual(red.Pixels, blue.Pixels);
    }
}
