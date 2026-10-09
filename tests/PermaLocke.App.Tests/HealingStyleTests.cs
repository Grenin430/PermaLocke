using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>A healing item going into the bag (2026-10-09): four entries that never repeat, the extra gesture, the power.</summary>
public sealed class HealingStyleTests
{
    private static ItemScene.Item Potion(int count = 0, int power = 0, int seed = 8, uint tint = 0xFF80C060)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "POCION", 1, ItemCategory.Healing, power, tint, seed, null, -1, count);
    }

    [Fact]
    public void The_entry_is_never_the_one_of_the_time_before_and_all_four_come_up()
    {
        var variants = Enumerable.Range(0, 2000).Select(HealingStyle.VariantFor).ToArray();

        for (var i = 1; i < variants.Length; i++)
        {
            Assert.True(variants[i] != variants[i - 1], $"la entrada {i} repite la anterior ({variants[i]})");
        }

        Assert.Equal([0, 1, 2, 3], variants.Distinct().Order().ToArray());

        // Each block of four is the four, once each.
        for (var block = 0; block < 100; block++)
        {
            Assert.Equal([0, 1, 2, 3], variants.Skip(block * 4).Take(4).Order().ToArray());
        }
    }

    [Fact]
    public void Every_fifth_pickup_has_the_extra_gesture_of_the_bag()
    {
        Assert.Equal(40, Enumerable.Range(0, 200).Count(HealingStyle.ExtraFor));
        Assert.True(HealingStyle.ExtraFor(4));
        Assert.False(HealingStyle.ExtraFor(5));

        var landing = ItemStyles.For(ItemCategory.Healing).Phases(Potion()).WrapAt - 0.10 + 0.25;
        var plain = new ItemScene(1, 72);
        var extra = new ItemScene(1, 72);
        plain.Render(Potion(3), landing + 0.2);
        extra.Render(Potion(4), landing + 0.2);

        // The count changes the variant too, so the comparison is the same variant at a moment of the extra.
        var sameVariantPlain = Enumerable.Range(0, 40).First(c => c % 5 != 4 && HealingStyle.VariantFor(c) == HealingStyle.VariantFor(4));
        plain.Render(Potion(sameVariantPlain), landing + 0.2);
        Assert.NotEqual(plain.Pixels, extra.Pixels);
    }

    [Fact]
    public void A_stronger_item_has_more_of_it_and_is_taller_than_the_common_scene()
    {
        Assert.Equal(72, ItemStyles.For(ItemCategory.Healing).Height);

        // The same entry at power 0 and at power 3: more cells lit at the middle of the climax.
        var phases = ItemStyles.For(ItemCategory.Healing).Phases(Potion());
        foreach (var count in new[] { 0, 1, 2, 3 })
        {
            var weak = new ItemScene(1, 72);
            var strong = new ItemScene(1, 72);

            int Lit(ItemScene s) => Enumerable.Range(0, s.Width * s.Height).Count(i => s.Pixels[(i * 4) + 3] != 0);

            // Over the whole climax, not at one moment: a heart is small on the half of its beat.
            var weakTotal = 0;
            var strongTotal = 0;
            for (var at = phases.ClimaxAt + 0.05; at < phases.ClimaxAt + 0.6; at += 0.05)
            {
                weak.Render(Potion(count, 0), at);
                strong.Render(Potion(count, 3), at);
                weakTotal += Lit(weak);
                strongTotal += Lit(strong);
            }

            // The heart (3) is not more cells but more beats, in a longer climax: it is told by the length, below.
            if (HealingStyle.VariantFor(count) != 3) Assert.True(strongTotal >= weakTotal, $"entrada {HealingStyle.VariantFor(count)}: lo fuerte tiene menos");
        }

        // And the climax lasts longer with power.
        Assert.True(ItemTimeline.For(ItemCategory.Healing, 3).Length > ItemTimeline.For(ItemCategory.Healing, 0).Length);
    }
}
