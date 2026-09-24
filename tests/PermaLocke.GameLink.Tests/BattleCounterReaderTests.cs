using System.Buffers.Binary;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Field;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The trainer card counters in memory: where each record is, and when a block of memory is this player's records.
/// </summary>
public sealed class BattleCounterReaderTests
{
    /// <summary>Records 0-9 of the player's save on 2026-09-14, the same ten found in memory at 0x33079A48.</summary>
    private static readonly int[] Saved = [58536, 180, 0, 300, 198, 101, 15, 0, 1, 5];

    private static byte[] Block(Func<int, int> value)
    {
        var block = new byte[(100 * 4) + (100 * 2)];

        for (var record = 0; record < 100; record++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(record * 4), value(record));
        }

        for (var record = 100; record < 200; record++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(400 + ((record - 100) * 2)), (ushort)value(record));
        }

        return block;
    }

    private static int SavedRecord(int record) => record switch
    {
        < 10 => Saved[record],
        46 => 31,
        127 => 4,
        _ => 0
    };

    [Fact]
    public void The_first_hundred_are_four_bytes_and_the_rest_two()
    {
        Assert.Equal(16, BattleCounterReader.OffsetOf(BattleCounterReader.WildBattlesRecord));
        Assert.Equal(184, BattleCounterReader.OffsetOf(BattleCounterReader.FledRecord));
        Assert.Equal(454, BattleCounterReader.OffsetOf(BattleCounterReader.ShinyRecord));
    }

    [Fact]
    public void The_counters_are_read_from_their_records()
    {
        var counters = BattleCounterReader.Parse(Block(SavedRecord));

        Assert.Equal(new BattleCounters(198, 15, 31, 4), counters);
    }

    [Fact]
    public void Memory_a_few_battles_after_the_save_is_still_this_players_records()
    {
        // Tres combates salvajes y una captura después de guardar, que fue lo medido.
        var live = Block(record => record switch
        {
            0 => 58900,
            3 => 303,
            4 => 201,
            6 => 16,
            _ => SavedRecord(record)
        });

        Assert.True(BattleCounterReader.MatchesSave(live, SavedRecord));
    }

    [Fact]
    public void A_different_number_of_saves_is_another_block()
    {
        var other = Block(record => record == 1 ? 181 : SavedRecord(record));

        Assert.False(BattleCounterReader.MatchesSave(other, SavedRecord));
    }

    [Fact]
    public void A_counter_below_the_save_is_another_block()
    {
        var other = Block(record => record == 4 ? 150 : SavedRecord(record));

        Assert.False(BattleCounterReader.MatchesSave(other, SavedRecord));
    }

    [Fact]
    public void A_wrong_shiny_counter_does_not_take_the_others_down()
    {
        var block = Block(record => record == 127 ? 9999 : SavedRecord(record));

        Assert.True(BattleCounterReader.MatchesSave(block, SavedRecord));
        Assert.False(BattleCounterReader.ShinyMatchesSave(block, SavedRecord));
    }

    [Fact]
    public void Counters_only_go_up_and_not_by_much()
    {
        var before = new BattleCounters(198, 15, 31, 4);

        Assert.True(BattleCounterReader.Plausible(before, before with { WildBattles = 199 }));
        Assert.False(BattleCounterReader.Plausible(before, before with { WildBattles = 197 }));
        Assert.False(BattleCounterReader.Plausible(before, before with { Caught = 4000 }));
    }
}
