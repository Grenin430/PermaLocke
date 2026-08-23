using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The Z-Crystal table.
/// </summary>
/// <remarks>
/// What is guarded here is the <b>shape</b> of the pairing and not each individual call: the
/// pairing is by colour and says so (see <see cref="ZCrystalIndex"/>). What must never break is
/// that the eighteen items map onto eighteen different pictures — a repeat would put the same
/// crystal on two trials, and a gap would leave one without any.
/// </remarks>
public class ZCrystalIndexTests
{
    [Fact]
    public void The_eighteen_type_crystals_are_all_there()
    {
        Assert.Equal(18, ZCrystalIndex.All.Count());
        Assert.Equal(ZCrystalIconReader.FirstItemId, ZCrystalIndex.All.First());
        Assert.Equal(ZCrystalIconReader.LastItemId, ZCrystalIndex.All.Last());
    }

    /// <summary>A repeated index would show one trial the crystal of another.</summary>
    [Fact]
    public void No_two_crystals_share_a_picture()
    {
        var indices = ZCrystalIndex.All
            .Select(id => ZCrystalIndex.TryGet(id, out var index) ? index : -1)
            .ToList();

        Assert.Equal(18, indices.Distinct().Count());
        Assert.DoesNotContain(-1, indices);
    }

    /// <summary>
    /// The eighteen positions are 0 to 17 with nothing missing, which is what makes the list of
    /// carved images and the list of items the same list.
    /// </summary>
    [Fact]
    public void The_positions_cover_the_whole_carved_list()
    {
        var indices = ZCrystalIndex.All
            .Select(id => ZCrystalIndex.TryGet(id, out var index) ? index : -1)
            .Order()
            .ToList();

        Assert.Equal(Enumerable.Range(0, 18), indices);
    }

    [Fact]
    public void Anything_that_is_not_a_type_crystal_is_refused()
    {
        Assert.False(ZCrystalIndex.TryGet(806, out _));   // Mewstal Z: de especie, no de tipo
        Assert.False(ZCrystalIndex.TryGet(825, out _));
        Assert.False(ZCrystalIndex.TryGet(4, out _));     // Poké Ball
        Assert.False(ZCrystalIndex.TryGet(0, out _));
    }

    /// <summary>
    /// The twelve trials of the competition each name their crystal, and every one of those has to
    /// have a picture: a trial card with a hole in it is the one thing this table exists to stop.
    /// </summary>
    [Theory]
    [InlineData(807)] [InlineData(813)] [InlineData(809)] [InlineData(808)]
    [InlineData(811)] [InlineData(819)] [InlineData(810)] [InlineData(820)]
    [InlineData(822)] [InlineData(821)] [InlineData(824)] [InlineData(815)]
    public void Every_trial_crystal_has_a_picture(int itemId) =>
        Assert.True(ZCrystalIndex.TryGet(itemId, out _));
}
