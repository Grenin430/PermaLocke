using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The key item going into the bag (2026-10-09): solemn, tall, with a beam and a plate of its own.</summary>
public sealed class KeyItemStyleTests
{
    private static ItemScene.Item Key(int seed = 5, uint tint = 0xFFD0A030, string name = "INCUBADORA TURBO")
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, name, 1, ItemCategory.Key, 0, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void It_is_the_longest_of_the_common_animations_and_88_rows_tall()
    {
        var style = ItemStyles.For(ItemCategory.Key);

        Assert.Equal(88, style.Height);
        Assert.InRange(style.Phases(Key()).Length, 3.1, 3.3);
    }

    [Fact]
    public void The_beam_comes_down_and_goes_away_and_the_plate_has_its_second_frame()
    {
        var scene = new ItemScene(1, 88);

        // The column of light above the bag at the middle of the scene, and nothing there before it and after it.
        scene.Render(Key(), 0.1);
        Assert.Equal(0u, At(scene, 36, 20) >> 24);
        scene.Render(Key(), 1.6);
        Assert.Equal(0xFFu, At(scene, 36, 20) >> 24);
        scene.Render(Key(), 3.05);
        Assert.Equal(0u, At(scene, 36, 20) >> 24);

        // The inner frame of the plate, three rows under its top edge and ten columns in, in the colour of its edge.
        scene.Render(Key(), 1.6);
        Assert.Equal(0xFFE0A83Au, At(scene, 68 + 10, 30 + 3));
    }

    [Fact]
    public void It_leans_towards_the_item_one_way_or_the_other_by_seed_and_never_bounces()
    {
        var left = new ItemScene(1, 88);
        var right = new ItemScene(1, 88);
        var sides = Enumerable.Range(0, 20).Select(n => ItemScene.SeedFor(216, n)).ToArray();

        // Two seeds that bow on different sides give different frames at the height of the bow.
        var a = sides.First(s => new ItemSeed(s).Pick(1, 2) == 0);
        var b = sides.First(s => new ItemSeed(s).Pick(1, 2) == 1);
        left.Render(Key(a), 1.8);
        right.Render(Key(b), 1.8);
        Assert.NotEqual(left.Pixels, right.Pixels);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void The_white_is_seen_at_any_frame_rate(int framesPerSecond)
    {
        var item = Key();
        var scene = new ItemScene(1, 88);
        var phases = ItemStyles.For(ItemCategory.Key).Phases(item);
        var flashAt = phases.ClimaxAt + 0.25;
        var white = 0xFFFAF8FFu;
        var step = 1.0 / framesPerSecond;

        for (var shift = 0; shift < 10; shift++)
        {
            var seen = 0;
            for (var t = flashAt - 0.2 + (shift * step / 10); t < flashAt + 0.2; t += step)
            {
                scene.Render(item, t);
                if (At(scene, 28, 31) == white) seen++;
            }

            Assert.True(seen >= 1, $"{framesPerSecond} fps, desfase {shift}: no salió el blanco.");
        }
    }
}
