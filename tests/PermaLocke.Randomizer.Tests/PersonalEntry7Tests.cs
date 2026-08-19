using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class PersonalEntry7Tests
{
    private static byte[] MakeEntry(params byte[] stats)
    {
        var entry = new byte[PersonalEntry7.Size * 2];
        for (var i = 0; i < stats.Length; i++)
        {
            entry[PersonalEntry7.StatOffsets[i]] = stats[i];
        }
        return entry;
    }

    [Fact]
    public void An_entry_is_the_size_the_cartridge_uses()
    {
        Assert.Equal(0x54, PersonalEntry7.Size);
        Assert.Equal(6, PersonalEntry7.StatOffsets.Length);
        Assert.Equal(3, PersonalEntry7.AbilityOffsets.Length);
    }

    /// <summary>
    /// The whole point of shuffling instead of rolling: the encounter and trainer modules match
    /// replacements by base stat total, so that total has to survive.
    /// </summary>
    [Fact]
    public void Shuffling_never_changes_the_base_stat_total()
    {
        var random = new SeededRandomSource(11);
        for (var trial = 0; trial < 500; trial++)
        {
            var entry = MakeEntry(68, 55, 55, 42, 50, 50);
            var before = PersonalEntry7.BaseStatTotal(entry, 0);

            PersonalEntry7.ShuffleStats(entry, 0, random.Next);

            Assert.Equal(before, PersonalEntry7.BaseStatTotal(entry, 0));
        }
    }

    [Fact]
    public void Shuffling_keeps_exactly_the_same_six_values()
    {
        var random = new SeededRandomSource(12);
        var entry = MakeEntry(68, 55, 55, 42, 50, 90);
        var before = PersonalEntry7.StatOffsets.Select(o => entry[o]).OrderBy(v => v).ToArray();

        PersonalEntry7.ShuffleStats(entry, 0, random.Next);

        var after = PersonalEntry7.StatOffsets.Select(o => entry[o]).OrderBy(v => v).ToArray();
        Assert.Equal(before, after);
    }

    [Fact]
    public void Shuffling_actually_moves_things_around()
    {
        var random = new SeededRandomSource(13);
        var moved = 0;
        for (var trial = 0; trial < 200; trial++)
        {
            var entry = MakeEntry(10, 20, 30, 40, 50, 60);
            PersonalEntry7.ShuffleStats(entry, 0, random.Next);
            if (entry[PersonalEntry7.StatOffsets[0]] != 10)
            {
                moved++;
            }
        }
        Assert.True(moved > 100, $"solo {moved} de 200 barajados movieron la primera estadística");
    }

    [Fact]
    public void Shuffling_one_entry_leaves_its_neighbour_alone()
    {
        var random = new SeededRandomSource(14);
        var entry = MakeEntry(10, 20, 30, 40, 50, 60);
        for (var i = PersonalEntry7.Size; i < entry.Length; i++)
        {
            entry[i] = 0x7F;
        }

        PersonalEntry7.ShuffleStats(entry, 0, random.Next);

        for (var i = PersonalEntry7.Size; i < entry.Length; i++)
        {
            Assert.Equal(0x7F, entry[i]);
        }
    }

    [Fact]
    public void A_single_type_species_is_recognised_as_such()
    {
        var entry = new byte[PersonalEntry7.Size];

        PersonalEntry7.SetTypes(entry, 0, 11, 11);
        Assert.True(PersonalEntry7.IsMonoType(entry, 0));

        PersonalEntry7.SetTypes(entry, 0, 11, 2);
        Assert.False(PersonalEntry7.IsMonoType(entry, 0));
        Assert.Equal((11, 2), PersonalEntry7.GetTypes(entry, 0));
    }

    [Fact]
    public void Abilities_survive_a_round_trip_in_every_slot()
    {
        var entry = new byte[PersonalEntry7.Size];
        for (var slot = 0; slot < PersonalEntry7.AbilityOffsets.Length; slot++)
        {
            PersonalEntry7.SetAbility(entry, 0, slot, 50 + slot);
        }
        for (var slot = 0; slot < PersonalEntry7.AbilityOffsets.Length; slot++)
        {
            Assert.Equal(50 + slot, PersonalEntry7.GetAbility(entry, 0, slot));
        }
    }

    /// <summary>A base stat of zero would be a Pokémon with no HP at all.</summary>
    [Fact]
    public void A_stat_can_never_be_written_as_zero_or_overflow()
    {
        var entry = new byte[PersonalEntry7.Size];

        PersonalEntry7.SetStat(entry, 0, 0, 0);
        Assert.Equal(1, PersonalEntry7.GetStat(entry, 0, 0));

        PersonalEntry7.SetStat(entry, 0, 0, 9999);
        Assert.Equal(255, PersonalEntry7.GetStat(entry, 0, 0));
    }
}
