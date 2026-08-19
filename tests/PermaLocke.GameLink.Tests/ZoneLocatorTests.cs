using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Guards the decision of whether PermaLocke knows which zone the player is in.
/// </summary>
/// <remarks>
/// It matters more than it looks: the zone decides whether the Poké Balls are taken away. Being
/// wrong about it is worse than not knowing, so every case here is about refusing to answer.
/// </remarks>
public sealed class ZoneLocatorTests
{
    /// <summary>
    /// The reading taken in Route 1: area 10, and the record handle that was actually there.
    /// In that zone the words next to the area field are zero, which is what broke the first
    /// attempt at a liveness check and is why this case is pinned.
    /// </summary>
    private static ZoneReading Route1() => new(0x0461BB94, 10);

    private static ZoneReading[] FourCopies(ushort area) =>
        [.. Enumerable.Repeat(new ZoneReading(0x0461BB94, area), 4)];

    [Fact]
    public void Four_copies_that_agree_give_the_area()
    {
        Assert.True(ZoneLocator.TryResolve(FourCopies(10), out var area));
        Assert.Equal(10, area);
    }

    /// <summary>Area 1 is Pueblo Lilii, measured in game. Area 10 is Route 1.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(335)]
    public void Every_real_area_is_accepted(ushort area)
    {
        Assert.True(ZoneLocator.TryResolve(FourCopies(area), out var resolved));
        Assert.Equal(area, resolved);
    }

    [Fact]
    public void One_copy_out_of_step_is_enough_to_refuse()
    {
        var copies = FourCopies(10);
        copies[2] = copies[2] with { Area = 11 };

        Assert.False(ZoneLocator.TryResolve(copies, out _));
    }

    /// <summary>
    /// The case a wrong anchor produces: blank memory reads as area 0, which is a real area.
    /// Without the liveness check this would confidently report Route 1.
    /// </summary>
    [Fact]
    public void Blank_memory_is_not_area_zero()
    {
        var blank = Enumerable.Repeat(new ZoneReading(0, 0), 4).ToArray();

        Assert.False(ZoneLocator.TryResolve(blank, out _));
    }

    [Fact]
    public void A_single_blank_copy_is_enough_to_refuse()
    {
        var copies = FourCopies(10);
        copies[1] = copies[1] with { Anchor = 0 };

        Assert.False(ZoneLocator.TryResolve(copies, out _));
    }

    [Fact]
    public void An_area_the_cartridge_does_not_have_is_refused()
    {
        Assert.False(ZoneLocator.TryResolve(FourCopies(ZoneLocator.UltraSunMoonAreaCount), out _));
        Assert.False(ZoneLocator.TryResolve(FourCopies(60000), out _));
    }

    [Fact]
    public void Nothing_read_means_nothing_answered()
    {
        Assert.False(ZoneLocator.TryResolve([], out _));
    }

    /// <summary>
    /// The offsets reproduce the distances measured in game: 0x1A0 between the two records of a
    /// pair, and 0x121E00 between the pairs. Pinning them here means a careless edit shows up.
    /// </summary>
    [Fact]
    public void The_offsets_keep_the_shape_seen_in_memory()
    {
        var offsets = ZoneLocator.CopyOffsets;

        Assert.Equal(4, offsets.Count);
        Assert.Equal(0xCC374u, offsets[0]);
        Assert.Equal(0x1A0u, offsets[1] - offsets[0]);
        Assert.Equal(0x1A0u, offsets[3] - offsets[2]);
        Assert.Equal(0x121E00u, offsets[2] - offsets[0]);

        // Con la mochila donde se verificó, las direcciones son las medidas a mano.
        const uint Bag = 0x33011934;
        Assert.Equal(0x330DDCA8u, Bag + offsets[0]);
        Assert.Equal(0x330DDE48u, Bag + offsets[1]);
        Assert.Equal(0x331FFAA8u, Bag + offsets[2]);
        Assert.Equal(0x331FFC48u, Bag + offsets[3]);
    }

    [Fact]
    public void The_reading_taken_in_Route_1_is_live()
    {
        Assert.True(Route1().LooksLive);
    }
}
