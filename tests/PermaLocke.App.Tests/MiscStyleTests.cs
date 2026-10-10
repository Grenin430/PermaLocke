using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>Everything else going into the bag (2026-10-09): the simplest style, which must never tire.</summary>
public sealed class MiscStyleTests
{
    private static ItemScene.Item Nugget(int seed = 2, uint tint = 0xFFE0C040)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "PEPITA", 1, ItemCategory.Misc, 0, tint, seed);
    }

    [Fact]
    public void It_is_the_shortest_there_is_and_has_the_height_of_the_common_scene()
    {
        Assert.Equal(64, ItemStyles.For(ItemCategory.Misc).Height);

        var lengths = Enum.GetValues<ItemCategory>().Select(category => ItemTimeline.For(category).Length).ToArray();
        Assert.Equal(lengths.Min(), ItemTimeline.For(ItemCategory.Misc).Length);
        Assert.InRange(ItemStyles.For(ItemCategory.Misc).Phases(Nugget()).Length, 1.3, 1.6);
    }

    [Fact]
    public void It_draws_little_a_scene_with_much_less_in_it_than_a_climax()
    {
        var misc = new ItemScene(1, 64);
        var mega = new ItemScene(1, 112);

        int Lit(ItemScene s) => Enumerable.Range(0, s.Width * s.Height).Count(i => s.Pixels[(i * 4) + 3] != 0);

        var phases = ItemStyles.For(ItemCategory.Misc).Phases(Nugget());
        misc.Render(Nugget(), phases.ClimaxAt + 0.05);
        mega.Render(Nugget() with { Category = ItemCategory.MegaStone }, 1.8);

        Assert.True(Lit(misc) < Lit(mega), "a common item draws more than a Mega Stone");
        Assert.InRange(Lit(misc), 1, 4000);
    }

    [Fact]
    public void The_seed_only_changes_which_side_it_leans_to_and_which_side_the_star_is_on()
    {
        var phases = ItemStyles.For(ItemCategory.Misc).Phases(Nugget());
        var a = new ItemScene(1, 64);
        var b = new ItemScene(1, 64);
        var left = Enumerable.Range(0, 20).Select(n => ItemScene.SeedFor(92, n)).First(s => new ItemSeed(s).Pick(1, 2) == 0);
        var right = Enumerable.Range(0, 20).Select(n => ItemScene.SeedFor(92, n)).First(s => new ItemSeed(s).Pick(1, 2) == 1);

        a.Render(Nugget(left), phases.ClimaxAt + 0.25);
        b.Render(Nugget(right), phases.ClimaxAt + 0.25);
        Assert.NotEqual(a.Pixels, b.Pixels);

        // Before the drop there is nothing a seed changes.
        a.Render(Nugget(left), phases.ClimaxAt + 0.05);
        b.Render(Nugget(right), phases.ClimaxAt + 0.05);
        Assert.Equal(a.Pixels, b.Pixels);
    }
}
