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
    /// The second block, which was left without an icon for a long time on the strength of a
    /// reading that turned out to be wrong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comment that used to sit here said the block began at icon 617 and that its offset
    /// "broke before the end". Both halves were false: 617 is white, blue and black — Glalita —
    /// and the offset is a clean -146 from end to end. The mistake had been sitting in a comment
    /// nobody could check, which is exactly the kind of claim §45 says has to be measured.
    /// </para>
    /// <para>
    /// What these assertions protect is the <b>shape</b>: thirteen stones, a gap of two, four more.
    /// That shape is what makes the alignment falsifiable — the two items in the gap are not stones
    /// (766 is the Megabrazalete, and its icon is a bracelet), so an off-by-one would put a stone
    /// on a bracelet and the count of thirteen would break.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_second_block_is_thirteen_then_a_gap_of_two_then_four()
    {
        for (var item = 752; item <= 764; item++)
        {
            Assert.True(ItemIconIndex.TryGet(item, out var icon), $"falta el objeto {item}");
            Assert.Equal(item - 146, icon);
        }

        for (var item = 767; item <= 770; item++)
        {
            Assert.True(ItemIconIndex.TryGet(item, out var icon), $"falta el objeto {item}");
            Assert.Equal(item - 146, icon);
        }
    }

    /// <summary>
    /// The anchor that catches an off-by-one, and the only one in the block that is not a stone.
    /// </summary>
    /// <remarks>
    /// Colour tells a Swampertita from a Sceptilita, but every stone is a stone: shifting the whole
    /// block by one would still land each item on something round and shiny. The Megabrazalete is
    /// a <b>bracelet</b>, so it only fits one icon, and it sits outside the run being measured.
    /// </remarks>
    [Fact]
    public void The_bracelet_anchors_the_block_from_outside_it()
    {
        Assert.True(ItemIconIndex.TryGet(766, out var bracelet));
        Assert.Equal(620, bracelet);
    }

    /// <summary>Every mega stone the shop sells can show a picture.</summary>
    /// <remarks>
    /// The reason the block was finished at all. A stone with no icon is not a crash: <c>Of</c>
    /// throws, and the shop would have shown a hole where the item is.
    /// </remarks>
    [Fact]
    public void Both_blocks_together_cover_the_forty_seven_the_shop_sells()
    {
        int[] stones = [.. Enumerable.Range(656, 30), .. Enumerable.Range(752, 13),
            .. Enumerable.Range(767, 4)];

        Assert.Equal(47, stones.Length);

        var icons = stones.Select(ItemIconIndex.Of).ToArray();
        Assert.Equal(47, icons.Distinct().Count());
    }
}
