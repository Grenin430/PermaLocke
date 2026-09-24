using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The four levers of the trainer difficulty module (§122), each as the rule the player asked for.
/// </summary>
public sealed class TrainerDifficultyTests
{
    /// <summary>
    /// Bits are added, never assigned: doubles, No Whiteout and Use Item describe the battle, and a
    /// trainer that already had more keeps it.
    /// </summary>
    [Theory]
    [InlineData(0x00, 0x07)]
    [InlineData(0x01, 0x07)]
    [InlineData(0x05, 0x07)]
    [InlineData(0x08, 0x0F)]
    [InlineData(0x10, 0x17)]
    [InlineData(0x85, 0x87)]
    [InlineData(0x87, 0x87)]
    [InlineData(0x8F, 0x8F)]
    public void Ai_gains_the_three_levels_and_keeps_everything_else(int before, int after) =>
        Assert.Equal(after, TrainerDifficultyRandomizer.WithAiFlags(before, 0x07));

    /// <summary>
    /// A percentage of what the IV already is, rounded up, capped at 31. The cartridge's most common
    /// value is 15, and zero stays zero because twenty per cent of nothing is nothing.
    /// </summary>
    [Theory]
    [InlineData(15, 10, 17)]
    [InlineData(15, 20, 18)]
    [InlineData(20, 10, 22)]
    [InlineData(25, 20, 30)]
    [InlineData(30, 10, 31)]
    [InlineData(31, 20, 31)]
    [InlineData(0, 20, 0)]
    public void Ivs_rise_by_a_share_of_themselves(int iv, int percent, int expected) =>
        Assert.Equal(expected, TrainerDifficultyRandomizer.RaiseIv(iv, percent));

    // Base stats in EV order: HP, Attack, Defense, Sp. Atk, Sp. Def, Speed.
    private static readonly int[] FastPhysical = [70, 120, 60, 50, 60, 100];
    private static readonly int[] SlowSpecial = [90, 50, 70, 115, 95, 40];

    [Fact]
    public void A_fast_attacker_gets_attack_and_speed()
    {
        var evs = TrainerDifficultyRandomizer.SpreadEvs(504, FastPhysical, 80);

        Assert.Equal(new byte[] { 0, 252, 0, 0, 0, 252 }, evs);
    }

    [Fact]
    public void A_slow_special_attacker_gets_special_attack_and_hp()
    {
        var evs = TrainerDifficultyRandomizer.SpreadEvs(504, SlowSpecial, 80);

        Assert.Equal(new byte[] { 252, 0, 0, 252, 0, 0 }, evs);
    }

    /// <summary>The total the cartridge gave is kept exactly, including its odd leftovers.</summary>
    [Theory]
    [InlineData(252)]
    [InlineData(504)]
    [InlineData(510)]
    [InlineData(1)]
    public void The_amount_of_evs_does_not_change(int total)
    {
        var evs = TrainerDifficultyRandomizer.SpreadEvs(total, FastPhysical, 80);

        Assert.Equal(total, evs.Sum(b => b));
        Assert.All(evs, ev => Assert.InRange(ev, 0, TrainerDifficultyRandomizer.MaxEvPerStat));
    }

    [Fact]
    public void The_six_left_over_go_to_the_third_stat_in_line()
    {
        Assert.Equal(new byte[] { 6, 252, 0, 0, 0, 252 }, TrainerDifficultyRandomizer.SpreadEvs(510, FastPhysical, 80));
        Assert.Equal(new byte[] { 252, 0, 0, 252, 6, 0 }, TrainerDifficultyRandomizer.SpreadEvs(510, SlowSpecial, 80));
    }

    /// <summary>The cartridge: 1139 trainer Pokémon, 114 of them holding something.</summary>
    [Fact]
    public void Items_are_given_until_a_quarter_hold_one()
    {
        Assert.Equal(171, TrainerDifficultyRandomizer.ItemsToGive(1139, 114, 25));
        Assert.Equal(0, TrainerDifficultyRandomizer.ItemsToGive(100, 40, 25));
        Assert.Equal(0, TrainerDifficultyRandomizer.ItemsToGive(100, 10, 0));
    }

    /// <summary>
    /// A form reads its own row. Two species and one alternate form: species 2 points its forms at
    /// row 3 and declares two forms, so form 1 is row 3.
    /// </summary>
    [Fact]
    public void A_form_uses_its_own_base_stats()
    {
        var packed = new byte[4 * PersonalEntry7.Size];

        void Row(int row, int hp, int atk, int def, int spe, int spa, int spd)
        {
            var at = row * PersonalEntry7.Size;
            packed[at] = (byte)hp; packed[at + 1] = (byte)atk; packed[at + 2] = (byte)def;
            packed[at + 3] = (byte)spe; packed[at + 4] = (byte)spa; packed[at + 5] = (byte)spd;
        }

        Row(1, 10, 20, 30, 40, 50, 60);
        Row(2, 78, 84, 78, 100, 109, 85);
        Row(3, 78, 130, 111, 100, 130, 85);
        BitConverter.GetBytes((ushort)3).CopyTo(packed, (2 * PersonalEntry7.Size) + PersonalEntry7.FormStatsIndexOffset);
        packed[(2 * PersonalEntry7.Size) + 0x20] = 2;

        // Returned in EV order: the table's Speed (4th) moves to the end.
        Assert.Equal([78, 84, 78, 109, 85, 100], TrainerDifficultyRandomizer.BaseStatsOf(packed, 2, 0)!);
        Assert.Equal([78, 130, 111, 130, 85, 100], TrainerDifficultyRandomizer.BaseStatsOf(packed, 2, 1)!);

        // A form the species does not declare falls back to the species, and a species off the
        // table is nobody.
        Assert.Equal([78, 84, 78, 109, 85, 100], TrainerDifficultyRandomizer.BaseStatsOf(packed, 2, 5)!);
        Assert.Null(TrainerDifficultyRandomizer.BaseStatsOf(packed, 9, 0));
    }

    [Fact]
    public void A_misnamed_item_stops_everything()
    {
        var names = Enumerable.Repeat("", 300).ToArray();
        names[234] = "Restos";

        var right = new TrainerDifficultyOptions { HeldItemsAny = [new(234, "Restos")] };
        var wrong = new TrainerDifficultyOptions { HeldItemsAny = [new(235, "Restos")] };

        TrainerDifficultyRandomizer.CheckItemNames(names, right);
        Assert.Throws<InvalidDataException>(() => TrainerDifficultyRandomizer.CheckItemNames(names, wrong));
    }

    [Fact]
    public void An_ivs_write_touches_one_stat_and_keeps_the_flags()
    {
        var party = new byte[TrainerPokemonTable.EntrySize];
        BitConverter.GetBytes(0x40000000u | (15u << 5) | 15u).CopyTo(party, 0x08);

        TrainerPokemonTable.SetIv(party, 0, 1, 18);

        Assert.Equal(15, TrainerPokemonTable.GetIv(party, 0, 0));
        Assert.Equal(18, TrainerPokemonTable.GetIv(party, 0, 1));
        Assert.Equal(0x40000000u, BitConverter.ToUInt32(party, 0x08) & 0xC0000000u);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>What the player asked for on 2026-09-14, as shipped.</summary>
    [Fact]
    public void The_shipped_configuration_is_what_was_asked()
    {
        var shipped = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json")).TrainerDifficulty;

        Assert.True(shipped.Enabled);
        Assert.Equal(0x07, shipped.AiFlagsAdded);
        Assert.Equal((10, 20), (shipped.IvRaiseMinPercent, shipped.IvRaiseMaxPercent));
        Assert.True(shipped.RecalculateEvs);
        Assert.Equal(25, shipped.HeldItemPercent);
        Assert.NotEmpty(shipped.HeldItemsAny);
        Assert.NotEmpty(shipped.HeldItemsPhysical);
        Assert.NotEmpty(shipped.HeldItemsSpecial);
    }

    /// <summary>Without the block, the trainers stay as the cartridge made them.</summary>
    [Fact]
    public void Missing_configuration_changes_nothing() =>
        Assert.False(new RandomizerOptions().TrainerDifficulty.Enabled);
}
