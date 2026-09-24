using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>Reading a learnset entry of <c>a/0/1/3</c> (§142).</summary>
public sealed class WorldMoveTablesTests
{
    private static byte[] Entry(params ushort[] halves) => [.. halves.SelectMany(BitConverter.GetBytes)];

    /// <summary>Pairs of (move, level), up to the 0xFFFF that closes the entry and not past it: entries are padded.</summary>
    [Fact]
    public void It_reads_pairs_up_to_the_terminator()
    {
        var moves = WorldMoveTables.Parse(Entry(33, 1, 45, 0, 52, 9, 0xFFFF, 0xFFFF, 99, 99));

        Assert.Equal([(33, 1), (45, 0), (52, 9)], moves.Select(m => (m.Move, m.Level)));
        Assert.True(moves[1].IsOnEvolution);
    }

    /// <summary>
    /// The game's descriptions come broken into lines for the handheld's screen (§144): on a wider screen they read as one
    /// paragraph, with the breaks gone and no doubled or trailing spaces.
    /// </summary>
    [Fact]
    public void A_description_reads_as_one_paragraph()
    {
        Assert.Equal("Restaura todos los PS y cura todos los problemas de estado del usuario.",
            WorldMoveTables.Flatten("Restaura todos los PS y cura todos los problemas de \\nestado del usuario. "));
        Assert.Equal("Uno dos", WorldMoveTables.Flatten("Uno\ndos"));
        Assert.Equal(string.Empty, WorldMoveTables.Flatten(""));
    }

    /// <summary>A truncated entry yields what it clearly says and no more.</summary>
    [Fact]
    public void A_truncated_entry_stops_at_its_last_whole_pair()
    {
        var bytes = Entry(33, 1, 45);

        Assert.Equal([33], WorldMoveTables.Parse(bytes).Select(m => m.Move));
        Assert.Empty(WorldMoveTables.Parse([]));
    }
}
