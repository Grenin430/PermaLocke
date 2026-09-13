using PermaLocke.GameLink.Battle;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The battle Pokémon blocks and the moment a faint is decided, with the bytes and the sequence of
/// values read from a real battle (§114).
/// </summary>
public class BattleTableTests
{
    /// <summary>
    /// Ferrocuello's block in the display table, read at 0x30002738 in the middle of a battle: 174 of
    /// 179 HP, species 993, battle position 0.
    /// </summary>
    private static readonly byte[] Ferrocuello =
    [
        0x44, 0x55, 0x00, 0x00, 0x20, 0x03, 0x00, 0x00, 0xFC, 0x26, 0x00, 0x30, 0x68, 0x2A, 0x00, 0x30,
        0xE7, 0xFF, 0xFF, 0xFF, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x18, 0xE5, 0x02, 0x30, 0x00, 0x00, 0x00, 0x00, 0x12, 0x04, 0x04, 0x00, 0xE1, 0x03, 0xB3, 0x00,
        0xAE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x32, 0x00, 0x3B, 0x00, 0x50, 0x6C, 0x80, 0x00, 0x00, 0x00
    ];

    [Fact]
    public void A_real_block_reads_as_the_pokemon_on_screen()
    {
        var block = BattleLayout.Parse(0x30002738, Ferrocuello);

        Assert.NotNull(block);
        Assert.Equal(0x30002748u, block.Address);
        Assert.Equal(0x3002E518u, block.Pointer);
        Assert.Equal(993, block.Species);
        Assert.Equal(179, block.MaxHp);
        Assert.Equal(174, block.CurrentHp);
        Assert.Equal(0, block.BattleId);
        Assert.True(block.IsPlayers);
    }

    /// <summary>
    /// The calculation table's memory right after the battle ended, as read: freed and reused. It must
    /// not read as anything.
    /// </summary>
    [Fact]
    public void A_freed_block_is_not_a_block()
    {
        byte[] reused =
        [
            0x80, 0x98, 0x01, 0x00, 0x58, 0x98, 0x01, 0x00, 0x78, 0x2F, 0x29, 0x02, 0x80, 0x24, 0x04, 0x00,
            0x5A, 0x24, 0x04, 0x00, 0xF8, 0xC7, 0x2A, 0x02, 0x00, 0xE5, 0x05, 0x00, 0xA3, 0xE4, 0x05, 0x00,
            0x78, 0xEC, 0x2E, 0x02, 0x80, 0xC6, 0x03, 0x00, 0x5E, 0xC6, 0x03, 0x00, 0x78, 0xD1, 0x34, 0x02,
            0x00, 0x00, 0x03, 0x00, 0x9A, 0xFF, 0x02, 0x00, 0xF8, 0x97, 0x38, 0x02, 0x00, 0x89, 0x03, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        Assert.Null(BattleLayout.Parse(0x30009720, reused));
    }

    /// <summary>The table has room for more positions than the battle uses; the unused ones are all zeros.</summary>
    [Fact]
    public void An_unused_position_is_not_a_pokemon()
    {
        var empty = Ferrocuello.ToArray();
        Array.Clear(empty, BattleLayout.HeaderSize + 0x20, empty.Length - BattleLayout.HeaderSize - 0x20);

        Assert.Null(BattleLayout.Parse(0x30002738, empty));
    }

    [Fact]
    public void Hp_above_the_maximum_is_somebody_elses_memory()
    {
        var wrong = Ferrocuello.ToArray();
        wrong[BattleLayout.HeaderSize + 0x30] = 0xFF;

        Assert.Null(BattleLayout.Parse(0x30002738, wrong));
    }

    [Fact]
    public void A_truncated_read_is_not_a_block() =>
        Assert.Null(BattleLayout.Parse(0x30002738, Ferrocuello.AsSpan(0, 40)));

    /// <summary>The addresses measured: the wild Pokémon sits exactly twelve positions after the first.</summary>
    [Fact]
    public void Blocks_are_grouped_into_their_tables_by_position()
    {
        BattleBlock[] blocks =
        [
            new(0x30002748, 0x3002E518, 993, 179, 174, 0),
            new(0x30002A78, 0x3002E6FC, 373, 199, 199, 1),
            new(0x30004D88, 0x30030804, 859, 32, 32, 12),
            new(0x30009730, 0x3002F0BC, 993, 179, 174, 0),
            new(0x3000BD70, 0x30030804, 859, 32, 32, 12)
        ];

        var tables = BattleTable.Group(blocks);

        Assert.Equal(2, tables.Count);
        Assert.Contains(tables, table => table.Origin == 0x30002748 && table.Blocks.Count == 3);
        Assert.Contains(tables, table => table.Origin == 0x30009730 && table.Blocks.Count == 2);
        Assert.Equal(0x30004D78u, tables.Single(table => table.Origin == 0x30002748).HeaderOf(12));
    }

    /// <summary>
    /// After a battle only the display table is left, with the party's last HP and no opponent: a
    /// fallen one in it is a leftover, not a death.
    /// </summary>
    [Fact]
    public void The_leftover_display_table_is_not_a_battle()
    {
        var tracker = new BattleFaintTracker();
        var leftover = Table(0x30002748, (0, 993, 179, 171), (2, 1020, 192, 0));

        Assert.Empty(tracker.Observe([leftover]));
        Assert.False(tracker.InBattle);
    }

    /// <summary>The measured order: the calculation table drops first, the display table follows the bar.</summary>
    [Fact]
    public void A_faint_is_reported_once_when_both_tables_reach_zero()
    {
        var tracker = new BattleFaintTracker();

        Assert.Empty(tracker.Observe(Battle(salamenceInstant: 199, salamenceBar: 199)));
        Assert.True(tracker.InBattle);

        // La tabla del cálculo ya está a cero; la barra todavía baja.
        Assert.Empty(tracker.Observe(Battle(salamenceInstant: 0, salamenceBar: 97)));

        var faint = Assert.Single(tracker.Observe(Battle(salamenceInstant: 0, salamenceBar: 0)));
        Assert.Equal(1, faint.BattleId);
        Assert.Equal(373, faint.Species);
        Assert.True(faint.IsPlayers);

        Assert.Empty(tracker.Observe(Battle(salamenceInstant: 0, salamenceBar: 0)));
    }

    /// <summary>The run's fallen sit at zero from the first reading. They are not news.</summary>
    [Fact]
    public void Already_fallen_at_the_start_is_never_reported()
    {
        var tracker = new BattleFaintTracker();

        for (var i = 0; i < 3; i++)
        {
            Assert.DoesNotContain(tracker.Observe(Battle(salamenceInstant: 199, salamenceBar: 199)),
                faint => faint.BattleId == 2);
        }
    }

    [Fact]
    public void An_opponent_faint_is_reported_as_not_the_players()
    {
        var tracker = new BattleFaintTracker();

        tracker.Observe(Battle(salamenceInstant: 199, salamenceBar: 199, wild: 32));

        var faint = Assert.Single(tracker.Observe(Battle(salamenceInstant: 199, salamenceBar: 199, wild: 0)));
        Assert.Equal(12, faint.BattleId);
        Assert.False(faint.IsPlayers);
    }

    [Fact]
    public void Another_battle_starts_from_nothing()
    {
        var tracker = new BattleFaintTracker();

        tracker.Observe(Battle(salamenceInstant: 199, salamenceBar: 199));
        Assert.Single(tracker.Observe(Battle(salamenceInstant: 0, salamenceBar: 0)));

        // Fuera de combate, y otro combate en otras direcciones.
        tracker.Observe([]);
        Assert.False(tracker.InBattle);

        tracker.Observe(Battle(salamenceInstant: 50, salamenceBar: 50, offset: 0x10000));
        Assert.Single(tracker.Observe(Battle(salamenceInstant: 0, salamenceBar: 0, offset: 0x10000)));
    }

    /// <summary>Two different Pokémon at the same position means the reading is not to be trusted.</summary>
    [Fact]
    public void A_position_that_disagrees_between_tables_is_not_judged()
    {
        var tracker = new BattleFaintTracker();

        BattleTable[] standing =
        [
            Table(0x30002748, (0, 993, 179, 100), (12, 859, 32, 32)),
            Table(0x30009730, (0, 993, 179, 100), (12, 859, 32, 32))
        ];

        BattleTable[] mixed =
        [
            Table(0x30002748, (0, 993, 179, 0), (12, 859, 32, 32)),
            Table(0x30009730, (0, 373, 199, 0), (12, 859, 32, 32))
        ];

        tracker.Observe(standing);
        Assert.Empty(tracker.Observe(mixed));
    }

    /// <summary>
    /// The battle as measured: Ferrocuello, Salamence, Flamariete already fallen, and a wild Impidimp,
    /// in two tables.
    /// </summary>
    private static BattleTable[] Battle(int salamenceInstant, int salamenceBar, int wild = 32, uint offset = 0) =>
    [
        Table(0x30009730 + offset, (0, 993, 179, 174), (1, 373, 199, salamenceInstant), (2, 1020, 192, 0), (12, 859, 32, wild)),
        Table(0x30002748 + offset, (0, 993, 179, 174), (1, 373, 199, salamenceBar), (2, 1020, 192, 0), (12, 859, 32, wild))
    ];

    private static BattleTable Table(uint origin, params (int Id, int Species, int Max, int Hp)[] rows) =>
        new(origin, [.. rows.Select(row => new BattleBlock(origin + (uint)(row.Id * BattleLayout.Stride), 0x3002E518,
            row.Species, row.Max, row.Hp, row.Id))]);
}
