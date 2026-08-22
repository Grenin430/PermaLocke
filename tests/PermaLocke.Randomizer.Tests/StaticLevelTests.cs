using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The level inside a static encounter entry, which is where the Totem Pokémon live.
/// </summary>
/// <remarks>
/// It sits at 0x03 of a 0x38 byte entry. Found by looking for a byte that is always between 1 and
/// 100 and varies, then confirmed against things whose level admits no doubt: Solgaleo and Lunala
/// at 60, Totem Gumshoos at 12, Totem Wishiwashi at 20, Totem Salazzle at 22.
/// </remarks>
public sealed class StaticLevelTests
{
    private static byte[] Entries(params int[] levels)
    {
        var payload = new byte[levels.Length * StaticEncounterTable.Statics.Stride];
        for (var i = 0; i < levels.Length; i++)
        {
            payload[(i * StaticEncounterTable.Statics.Stride) + 3] = (byte)levels[i];
        }

        return payload;
    }

    [Fact]
    public void The_level_is_read_from_where_the_cartridge_keeps_it()
    {
        var payload = Entries(12, 60, 75);

        Assert.Equal(12, StaticEncounterTable.GetLevel(payload, StaticEncounterTable.Statics, 0));
        Assert.Equal(60, StaticEncounterTable.GetLevel(payload, StaticEncounterTable.Statics, 1));
        Assert.Equal(75, StaticEncounterTable.GetLevel(payload, StaticEncounterTable.Statics, 2));
    }

    [Fact]
    public void Writing_a_level_leaves_the_rest_of_the_entry_alone()
    {
        var payload = Entries(12);
        payload[0] = 0x2A;   // especie
        payload[2] = 0x01;   // forma
        payload[4] = 0x77;   // lo que sea que haya después

        StaticEncounterTable.SetLevel(payload, StaticEncounterTable.Statics, 0, 15);

        Assert.Equal(15, payload[3]);
        Assert.Equal(0x2A, payload[0]);
        Assert.Equal(0x01, payload[2]);
        Assert.Equal(0x77, payload[4]);
    }

    /// <summary>A level cannot leave what a byte the game reads as a level can hold.</summary>
    [Fact]
    public void A_level_is_clamped_between_one_and_a_hundred()
    {
        var payload = Entries(60);

        StaticEncounterTable.SetLevel(payload, StaticEncounterTable.Statics, 0, 250);
        Assert.Equal(100, payload[3]);

        StaticEncounterTable.SetLevel(payload, StaticEncounterTable.Statics, 0, -4);
        Assert.Equal(1, payload[3]);
    }

    /// <summary>
    /// Gifts and trades carry no level, and must not be given one: a starter or a fossil is
    /// something the player receives, so raising it would be a present and not a difficulty.
    /// </summary>
    [Theory]
    [InlineData("regalos")]
    [InlineData("intercambios")]
    public void The_tables_the_player_receives_from_have_no_level(string table)
    {
        var layout = table == "regalos" ? StaticEncounterTable.Gifts : StaticEncounterTable.Trades;
        var payload = new byte[layout.Stride * 2];
        payload[3] = 40;

        Assert.Null(layout.LevelOffset);
        Assert.Equal(0, StaticEncounterTable.GetLevel(payload, layout, 0));

        StaticEncounterTable.SetLevel(payload, layout, 0, 99);
        Assert.Equal(40, payload[3]);
    }

    /// <summary>
    /// A Totem is recognised by both marks at once: the kind byte and the aura.
    /// </summary>
    /// <remarks>
    /// Tapu Koko carries the kind byte and no aura, and asking for both is what keeps it out
    /// without anyone having to name it in a list.
    /// </remarks>
    [Fact]
    public void A_totem_needs_both_marks()
    {
        var payload = new byte[StaticEncounterTable.Statics.Stride * 3];

        // 0: los dos marcadores. 1: solo el tipo, como Tapu Koko. 2: nada.
        payload[StaticEncounterTable.KindOffset] = StaticEncounterTable.TotemKind;
        payload[StaticEncounterTable.AuraOffset] = 0xFF;
        payload[StaticEncounterTable.AuraOffset + 1] = 0x99;

        payload[StaticEncounterTable.Statics.Stride + StaticEncounterTable.KindOffset] =
            StaticEncounterTable.TotemKind;

        Assert.True(StaticEncounterTable.IsTotem(payload, StaticEncounterTable.Statics, 0));
        Assert.False(StaticEncounterTable.IsTotem(payload, StaticEncounterTable.Statics, 1));
        Assert.False(StaticEncounterTable.IsTotem(payload, StaticEncounterTable.Statics, 2));
    }

    /// <summary>A table with no level has no Totems either.</summary>
    [Fact]
    public void A_table_without_levels_has_no_totems()
    {
        var payload = new byte[StaticEncounterTable.Gifts.Stride * 2];
        payload[StaticEncounterTable.KindOffset] = StaticEncounterTable.TotemKind;

        Assert.False(StaticEncounterTable.IsTotem(payload, StaticEncounterTable.Gifts, 0));
    }

    /// <summary>
    /// The Totems at the levels the cartridge really has them, raised by each role. These are the
    /// eight trial bosses, and the whole reason the statics needed raising at all.
    /// </summary>
    [Theory]
    [InlineData(12, 14, 15)]   // Verdant Cavern
    [InlineData(20, 24, 25)]   // Brooklet Hill
    [InlineData(22, 26, 28)]   // Wela Volcano
    [InlineData(24, 29, 30)]   // Lush Jungle
    [InlineData(33, 40, 42)]   // Hokulani
    [InlineData(35, 42, 44)]   // Thrifty Megamart
    [InlineData(49, 59, 62)]   // Vast Poni Canyon
    [InlineData(55, 66, 70)]   // Mina
    public void The_totems_climb_with_everything_else(int cartridge, int normal, int expert)
    {
        Assert.Equal(normal, TrainerRandomizer.Raise(cartridge, 20));
        Assert.Equal(expert, TrainerRandomizer.Raise(cartridge, 27));
    }
}
