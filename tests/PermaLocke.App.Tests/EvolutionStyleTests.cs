using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>An evolution item going into the bag (2026-10-09): a gesture, with the transformation of two white frames.</summary>
public sealed class EvolutionStyleTests
{
    private static ItemScene.Item Stone(int power = 1, int seed = 9, uint tint = 0xFF40A0E0)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "PIEDRA AGUA", 1, ItemCategory.Evolution, power, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void It_is_72_rows_tall_and_shorter_than_a_mega_stone_in_everything()
    {
        var evolution = ItemStyles.For(ItemCategory.Evolution);
        var mega = ItemStyles.For(ItemCategory.MegaStone);

        Assert.Equal(72, evolution.Height);
        Assert.True(evolution.Height < mega.Height);
        Assert.True(ItemTimeline.For(ItemCategory.Evolution).Length < ItemTimeline.For(ItemCategory.MegaStone).Length - 1.5);
        Assert.Equal(2, EvolutionStyle.OrbitsFor(1));
        Assert.Equal(1, EvolutionStyle.OrbitsFor(0));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void The_bag_is_a_white_silhouette_for_two_frames_at_any_frame_rate(int framesPerSecond)
    {
        var item = Stone();
        var scene = new ItemScene(1, 72);
        var phases = ItemStyles.For(ItemCategory.Evolution).Phases(item);
        var landing = phases.WrapAt - 0.05 + 0.30;
        var white = 0xFFFAF8FFu;
        var light = ItemTint.Shade(0xFF40A0E0, 0, 0.72);
        var step = 1.0 / framesPerSecond;

        for (var shift = 0; shift < 10; shift++)
        {
            int whites = 0, lights = 0;
            for (var t = landing - 0.15 + (shift * step / 10); t < landing + 0.15; t += step)
            {
                scene.Render(item, t);
                var cell = At(scene, 22, 60);
                if (cell == white) whites++;
                if (cell == light) lights++;
            }

            Assert.True(whites >= 1 && lights >= 1, $"{framesPerSecond} fps, desfase {shift}: blanco {whites}, claro {lights}");
        }
    }

    [Fact]
    public void A_stone_and_a_trade_item_do_not_play_the_same_and_the_plate_says_evolucion()
    {
        var stone = new ItemScene(1, 72);
        var trade = new ItemScene(1, 72);
        stone.Render(Stone(1), 1.2);
        trade.Render(Stone(0), 1.2);
        Assert.NotEqual(stone.Pixels, trade.Pixels);

        // The amount starts after «EVOLUCION» (35 cells), 103 columns in.
        var gold = 0xFFFFDC7Au;
        Assert.True(Enumerable.Range(36, 5).Any(y => At(stone, 103, y) == gold), "la cantidad no sigue a EVOLUCION");
    }
}
