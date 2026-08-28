using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The mega stone icons, measured by eye against the cartridge.
/// </summary>
/// <remarks>
/// The block 656-685 was pinned at both ends: icon 521 is purple (Gengarita), 523 yellow
/// (Ampharosita), 524 green (Venusaurita), 550 blue (Latiosita), and 551 is already a berry —
/// which is the item right after the block. What these tests protect is the <b>shape</b> of that
/// answer, so a later edit cannot quietly shift the block by one and show every stone as its
/// neighbour, which is a mistake nobody would ever notice (§45).
/// </remarks>
public sealed class MegaStoneIconTests
{
    [Fact]
    public void The_first_block_of_stones_is_the_measured_run()
    {
        Assert.True(ItemIconIndex.TryGet(656, out var first));
        Assert.Equal(521, first);

        Assert.True(ItemIconIndex.TryGet(685, out var last));
        Assert.Equal(550, last);
    }

    /// <summary>Thirty stones, thirty icons, no gaps and no repeats.</summary>
    [Fact]
    public void All_thirty_are_there_and_none_shares_a_picture()
    {
        var icons = new List<int>();

        for (var item = 656; item <= 685; item++)
        {
            Assert.True(ItemIconIndex.TryGet(item, out var icon), $"falta el objeto {item}");
            icons.Add(icon);
        }

        Assert.Equal(30, icons.Distinct().Count());
        Assert.Equal(icons.OrderBy(i => i), icons);
    }

    /// <summary>
    /// The second block is deliberately absent, and that has to stay a decision rather than
    /// becoming a bug somebody "fixes" by extrapolating.
    /// </summary>
    /// <remarks>
    /// It starts at 752 (Swampertita, icon 617, blue) but the offset breaks before the end: with
    /// the same -135 the last would land on icon 629, and 629 is a tool. Until somebody measures
    /// where it breaks, no icon is better than the wrong one.
    /// </remarks>
    [Fact]
    public void The_second_block_is_left_without_an_icon_on_purpose()
    {
        foreach (var item in (int[])[752, 758, 764, 767, 770])
        {
            Assert.False(ItemIconIndex.TryGet(item, out _),
                $"el objeto {item} tiene icono sin haberse medido");
        }
    }
}
