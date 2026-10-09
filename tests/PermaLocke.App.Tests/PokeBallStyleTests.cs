using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>A Poké Ball going into the bag (2026-10-09): bounces, the click of the button, the boing of the bag.</summary>
public sealed class PokeBallStyleTests
{
    private static ItemScene.Item Ball(int power = 0, int seed = 2, uint tint = 0xFFE03030)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "POKÉ BALL", 1, ItemCategory.PokeBall, power, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void A_seed_gives_two_or_three_bounces_and_the_click_is_after_them()
    {
        var seeds = Enumerable.Range(0, 60).Select(n => ItemScene.SeedFor(4, n)).ToArray();
        Assert.Equal([2, 3], seeds.Select(PokeBallStyle.BouncesFor).Distinct().Order().ToArray());

        var phases = ItemStyles.For(ItemCategory.PokeBall).Phases(Ball());
        Assert.True(PokeBallStyle.ClickAt(phases, 3) > PokeBallStyle.ClickAt(phases, 2));
        Assert.True(PokeBallStyle.ClickAt(phases, 3) + 0.15 < phases.OutAt);
        Assert.Equal(64, ItemStyles.For(ItemCategory.PokeBall).Height);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void The_click_is_two_frames_at_any_frame_rate(int framesPerSecond)
    {
        var item = Ball();
        var scene = new ItemScene(1, 64);
        var phases = ItemStyles.For(ItemCategory.PokeBall).Phases(item);
        var click = PokeBallStyle.ClickAt(phases, PokeBallStyle.BouncesFor(item.Seed));
        var step = 1.0 / framesPerSecond;
        var white = 0xFFFAF8FFu;

        for (var shift = 0; shift < 10; shift++)
        {
            var seen = 0;
            var ring = ItemTint.Shade(0xFFE03030, 0, 0.90, 0.6);
            var rings = 0;
            for (var t = click - 0.1 + (shift * step / 10); t < click + 0.15; t += step)
            {
                scene.Render(item, t);

                // The ball is over the mouth at the click: the disc of white covers its middle in the first frame and the ring is
                // round it in the second, seven cells from it.
                if (Enumerable.Range(42, 7).Any(y => At(scene, 24, y) == white)) seen++;
                if (Enumerable.Range(42, 7).Any(y => At(scene, 31, y) == ring)) rings++;
            }

            Assert.True(seen >= 1 && rings >= 1, $"{framesPerSecond} fps, desfase {shift}: blanco {seen}, anillo {rings}");
        }
    }

    [Fact]
    public void A_master_ball_has_its_ring_of_stars_and_a_plain_one_does_not()
    {
        var plain = new ItemScene(1, 64);
        var master = new ItemScene(1, 64);
        var phases = ItemStyles.For(ItemCategory.PokeBall).Phases(Ball());
        var at = phases.ClimaxAt + 0.3;
        plain.Render(Ball(0), at);
        master.Render(Ball(3), at);

        Assert.NotEqual(plain.Pixels, master.Pixels);
    }
}
