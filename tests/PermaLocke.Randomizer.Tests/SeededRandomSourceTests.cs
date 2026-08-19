using PermaLocke.Core.Services;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The whole competition rests on "same seed, same world". These tests pin the sequence so a
/// refactor cannot quietly change what a seed produces for ten players mid-run.
/// </summary>
public class SeededRandomSourceTests
{
    [Fact]
    public void Same_seed_gives_the_same_sequence()
    {
        var a = new SeededRandomSource(20260818);
        var b = new SeededRandomSource(20260818);

        var first = Enumerable.Range(0, 500).Select(_ => a.Next(807)).ToArray();
        var second = Enumerable.Range(0, 500).Select(_ => b.Next(807)).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        var a = new SeededRandomSource(1);
        var b = new SeededRandomSource(2);

        var first = Enumerable.Range(0, 100).Select(_ => a.Next(807)).ToArray();
        var second = Enumerable.Range(0, 100).Select(_ => b.Next(807)).ToArray();

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Pinned values. If this fails, every player's world changed: that is a breaking change to
    /// the competition, not a test to update lightly.
    /// </summary>
    [Fact]
    public void Sequence_is_pinned()
    {
        var random = new SeededRandomSource(20260818);
        int[] actual = [.. Enumerable.Range(0, 8).Select(_ => random.Next(807))];

        Assert.Equal([108, 726, 316, 775, 396, 638, 757, 594], actual);
    }

    [Fact]
    public void Derived_sources_are_independent_and_reproducible()
    {
        var wild = new SeededRandomSource(99).Derive("wild-encounters");
        var trainers = new SeededRandomSource(99).Derive("trainers");
        var wildAgain = new SeededRandomSource(99).Derive("wild-encounters");

        var a = Enumerable.Range(0, 50).Select(_ => wild.Next(807)).ToArray();
        var b = Enumerable.Range(0, 50).Select(_ => trainers.Next(807)).ToArray();
        var c = Enumerable.Range(0, 50).Select(_ => wildAgain.Next(807)).ToArray();

        Assert.Equal(a, c);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Salt_hashing_does_not_depend_on_the_process()
    {
        // string.GetHashCode is randomised per process; deriving must not be, or two players
        // with the same seed would get different worlds.
        var first = new SeededRandomSource(42).Derive("wild-encounters").Seed;
        var second = new SeededRandomSource(42).Derive("wild-encounters").Seed;
        var other = new SeededRandomSource(42).Derive("trainers").Seed;

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Next_stays_inside_its_bounds()
    {
        var random = new SeededRandomSource(7);
        for (var i = 0; i < 10_000; i++)
        {
            var value = random.Next(10, 20);
            Assert.InRange(value, 10, 19);
        }
    }

    [Fact]
    public void Next_rejects_an_empty_range()
    {
        var random = new SeededRandomSource(7);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 5));
    }
}
