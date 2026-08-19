using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class EncounterTable7Tests
{
    /// <summary>A mini entry: four byte prefix, day table, night table, then trailing padding.</summary>
    private static byte[] MakeEntry()
    {
        var payload = new byte[0x300];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i * 7); // recognisable filler, so any stray write shows up
        }

        payload[0] = 0x06; // the prefix pk3DS zeroes; non-zero in the real cartridge
        payload[1] = payload[2] = payload[3] = 0x00;
        return payload;
    }

    [Fact]
    public void There_are_eighty_six_slots_and_they_all_fit()
    {
        var offsets = EncounterTable7.SlotOffsets().ToArray();

        Assert.Equal((EncounterTable7.SlotSets * EncounterTable7.SlotsPerSet) + EncounterTable7.AdditionalSosSlots, offsets.Length);
        Assert.Equal(offsets.Length, offsets.Distinct().Count());
        Assert.All(offsets, o => Assert.InRange(o, 0, EncounterTable7.Size - 4));
    }

    [Fact]
    public void The_day_and_night_tables_sit_back_to_back_after_the_prefix()
    {
        Assert.Equal(0x004, EncounterTable7.DayTableOffset);
        Assert.Equal(0x168, EncounterTable7.NightTableOffset);
        Assert.Equal(0x2CC, EncounterTable7.MinimumEntrySize);
    }

    [Fact]
    public void Species_and_forme_survive_a_round_trip()
    {
        var payload = MakeEntry();
        var table = new EncounterTable7(payload, EncounterTable7.DayTableOffset);
        var slot = EncounterTable7.SlotOffsets().First();

        table.SetSpecies(slot, 722, forme: 3);

        Assert.Equal(722, table.GetSpecies(slot));
        Assert.Equal(3, table.GetForme(slot));
    }

    /// <summary>
    /// The upper half of the slot word is undocumented. pk3DS discards it; keeping it costs
    /// nothing, so a value that is there in the cartridge stays there.
    /// </summary>
    [Fact]
    public void The_undocumented_high_bits_are_left_alone()
    {
        var payload = new byte[0x300];
        var table = new EncounterTable7(payload, EncounterTable7.DayTableOffset);
        var slot = EncounterTable7.SlotOffsets().First();
        var at = EncounterTable7.DayTableOffset + slot;

        BitConverter.GetBytes(0xABCD1234u).CopyTo(payload, at);
        table.SetSpecies(slot, 129);

        Assert.Equal(129, table.GetSpecies(slot));
        Assert.Equal(0xABCD0000u, BitConverter.ToUInt32(payload, at) & 0xFFFF0000u);
    }

    /// <summary>
    /// The regression this whole design exists for. pk3DS's rebuild zeroed the four byte prefix
    /// and shrank the mini header, and Ultra Moon then produced no wild encounters at all.
    /// Patching slots must leave every byte outside the slots exactly as it was.
    /// </summary>
    [Fact]
    public void Patching_every_slot_touches_nothing_but_the_slots()
    {
        var payload = MakeEntry();
        var before = (byte[])payload.Clone();

        foreach (var tableOffset in (int[])[EncounterTable7.DayTableOffset, EncounterTable7.NightTableOffset])
        {
            var table = new EncounterTable7(payload, tableOffset);
            foreach (var slot in EncounterTable7.SlotOffsets())
            {
                table.SetSpecies(slot, 129);
            }
        }

        var slotBytes = new HashSet<int>();
        foreach (var tableOffset in (int[])[EncounterTable7.DayTableOffset, EncounterTable7.NightTableOffset])
        {
            foreach (var slot in EncounterTable7.SlotOffsets())
            {
                for (var b = 0; b < 4; b++)
                {
                    slotBytes.Add(tableOffset + slot + b);
                }
            }
        }

        for (var i = 0; i < payload.Length; i++)
        {
            if (slotBytes.Contains(i))
            {
                continue;
            }
            Assert.True(payload[i] == before[i], $"el byte 0x{i:X} cambió y no es parte de ningún hueco");
        }

        Assert.Equal(0x06, payload[0]); // the prefix pk3DS lost
    }

    [Fact]
    public void Levels_and_rates_are_read_where_the_cartridge_keeps_them()
    {
        var payload = new byte[0x300];
        payload[EncounterTable7.DayTableOffset] = 12;
        payload[EncounterTable7.DayTableOffset + 1] = 15;
        payload[EncounterTable7.DayTableOffset + 2] = 20;

        var table = new EncounterTable7(payload, EncounterTable7.DayTableOffset);

        Assert.Equal(12, table.MinLevel);
        Assert.Equal(15, table.MaxLevel);
        Assert.Equal(20, table.GetRate(0));
    }
}
