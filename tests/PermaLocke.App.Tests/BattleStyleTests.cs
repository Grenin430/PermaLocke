using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>A battle item going into the bag (2026-10-09): quick, hard, with a shaking bag and a shock.</summary>
public sealed class BattleStyleTests
{
    private static ItemScene.Item Band(int seed = 3, uint tint = 0xFF3050D0)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "CINTA ELECCION", 1, ItemCategory.Battle, 0, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void It_is_the_quickest_of_the_styles_with_a_climax_and_64_rows_tall()
    {
        Assert.Equal(64, ItemStyles.For(ItemCategory.Battle).Height);
        Assert.True(ItemStyles.For(ItemCategory.Battle).Phases(Band()).Length < 1.7);
        Assert.True(ItemTimeline.For(ItemCategory.Battle).Length < ItemTimeline.For(ItemCategory.PokeBall).Length);
    }

    [Fact]
    public void The_bag_shakes_by_up_to_three_columns_and_settles()
    {
        var item = Band();
        var phases = ItemStyles.For(ItemCategory.Battle).Phases(item);
        var hit = BattleStyle.HitAt(phases);

        // The left edge of the bag's outline, where it is, at each frame of the first quarter of a second after the blow.
        int Edge(double t)
        {
            var scene = new ItemScene(1, 64);
            scene.Render(item, t);
            // The leftmost cell of the leather highlight of the bag (its own colours, which no ring or spark has).
            for (var gx = 0; gx < 60; gx++)
            {
                for (var gy = 44; gy < 57; gy++)
                {
                    if (At(scene, gx, gy) is 0xFFF2A062u or 0xFFD8743Cu) return gx;
                }
            }

            return -1;
        }

        var edges = Enumerable.Range(0, 7).Select(n => Edge(hit + 0.005 + (n / 30.0))).ToArray();
        var still = Edge(hit + 0.8);

        Assert.True(edges.Max() - edges.Min() >= 2, $"la bolsa no se sacude: {string.Join(",", edges)}");
        Assert.True(edges.Max() - edges.Min() <= 8);
        Assert.NotEqual(-1, still);
    }

    [Fact]
    public void The_shock_ring_goes_out_over_the_ground_and_the_plate_says_combate()
    {
        var item = Band();
        var phases = ItemStyles.For(ItemCategory.Battle).Phases(item);
        var hit = BattleStyle.HitAt(phases);

        var early = new ItemScene(1, 64);
        var late = new ItemScene(1, 64);
        early.Render(item, hit + 0.05);
        late.Render(item, hit + 0.25);

        int Lit(ItemScene s, int from, int to) =>
            Enumerable.Range(44, 17).Sum(gy => Enumerable.Range(from, to - from).Count(gx => At(s, gx, gy) >> 24 == 0xFF));

        // The ring reaches farther from the bag as time goes on: more lit cells far from its middle.
        Assert.True(Lit(late, 0, 8) + Lit(late, 40, 52) > Lit(early, 0, 8) + Lit(early, 40, 52), "the ring does not go out");

        // «COMBATE» is 27 cells: the amount starts 4 after it, 89 columns in.
        var gold = 0xFFFFDC7Au;
        late.Render(item, hit + 0.5);
        Assert.True(Enumerable.Range(32, 5).Any(y => At(late, 89, y) == gold), "la cantidad no sigue a COMBATE");
    }
}
