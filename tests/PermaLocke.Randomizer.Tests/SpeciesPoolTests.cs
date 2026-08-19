using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class SpeciesPoolTests
{
    /// <summary>Twenty species with base stat totals from 200 to 780, in steps.</summary>
    private static int[] Totals(int count = 20)
    {
        var totals = new int[count + 1];
        for (var i = 1; i <= count; i++)
        {
            totals[i] = 200 + (i * 29);
        }
        return totals;
    }

    private static RandomizerOptions Options(RandomizerOptions? seed = null) =>
        (seed ?? new RandomizerOptions()) with { MaxSpecies = 20 };

    [Fact]
    public void A_banned_species_is_never_handed_out()
    {
        var options = Options() with { BannedSpecies = [3, 7, 11], SimilarStrength = false };
        var pool = new SpeciesPool(Totals(), options);
        var random = new SeededRandomSource(1);

        for (var i = 0; i < 5_000; i++)
        {
            Assert.DoesNotContain(pool.Pick(random, 5), (int[])[3, 7, 11]);
        }
    }

    [Fact]
    public void Every_pick_is_a_real_species()
    {
        var pool = new SpeciesPool(Totals(), Options() with { SimilarStrength = false });
        var random = new SeededRandomSource(2);

        for (var i = 0; i < 5_000; i++)
        {
            Assert.InRange(pool.Pick(random, 5), 1, 20);
        }
    }

    [Fact]
    public void One_to_one_maps_a_species_to_the_same_replacement_every_time()
    {
        var pool = new SpeciesPool(Totals(), Options() with { PickMode = SpeciesPickMode.OneToOne });
        var random = new SeededRandomSource(3);

        var first = pool.Pick(random, 9);
        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(first, pool.Pick(random, 9));
        }
    }

    [Fact]
    public void Per_slot_does_not_reuse_the_same_replacement()
    {
        var pool = new SpeciesPool(Totals(), Options() with { PickMode = SpeciesPickMode.PerSlot, SimilarStrength = false });
        var random = new SeededRandomSource(4);

        var picks = Enumerable.Range(0, 200).Select(_ => pool.Pick(random, 9)).Distinct().Count();
        Assert.True(picks > 1, "PerSlot debería variar entre huecos");
    }

    [Fact]
    public void Similar_strength_keeps_the_replacement_in_the_same_league()
    {
        var totals = Totals();
        var options = Options() with { SimilarStrength = true, StrengthTolerance = 0.15 };
        var pool = new SpeciesPool(totals, options);
        var random = new SeededRandomSource(5);

        const int original = 10;
        var target = totals[original];

        for (var i = 0; i < 2_000; i++)
        {
            var picked = pool.Pick(random, original);
            Assert.InRange(totals[picked], target * 0.85, target * 1.15);
        }
    }

    /// <summary>
    /// Shedinja-shaped case: nothing sits near the target, so the band has to widen instead of
    /// throwing or looping forever.
    /// </summary>
    [Fact]
    public void An_outlier_still_gets_a_replacement()
    {
        var totals = new int[] { 0, 1, 800, 810, 820 };
        var pool = new SpeciesPool(totals, new RandomizerOptions { MaxSpecies = 4, SimilarStrength = true, StrengthTolerance = 0.01 });
        var random = new SeededRandomSource(6);

        Assert.InRange(pool.Pick(random, 1), 1, 4);
    }

    [Fact]
    public void The_same_seed_produces_the_same_picks()
    {
        var options = Options();
        var first = Enumerable.Range(0, 100)
            .Select(i => new { Pool = new SpeciesPool(Totals(), options), Random = new SeededRandomSource(77) })
            .First();
        var second = new { Pool = new SpeciesPool(Totals(), options), Random = new SeededRandomSource(77) };

        var a = Enumerable.Range(1, 100).Select(i => first.Pool.Pick(first.Random, (i % 20) + 1)).ToArray();
        var b = Enumerable.Range(1, 100).Select(i => second.Pool.Pick(second.Random, (i % 20) + 1)).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void Banning_everything_is_rejected_rather_than_silently_ignored()
    {
        var options = Options() with { BannedSpecies = [.. Enumerable.Range(1, 20)] };
        Assert.Throws<ArgumentException>(() => new SpeciesPool(Totals(), options));
    }
}
