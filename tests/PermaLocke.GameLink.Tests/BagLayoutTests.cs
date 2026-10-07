using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Guards the map of the bag and the test that identifies it in memory.
/// </summary>
/// <remarks>
/// Written after the Rare Candy button reported a hit at 0x081D55B0 with no Rare Candies in
/// the bag at all. The old locator searched for a value and judged the neighbourhood; these
/// tests cover what replaced it — a block whose pocket pointers must point back into itself.
/// </remarks>
public sealed class BagLayoutTests
{
    private static readonly BagLayout Layout = BagLayout.UltraSunMoon;

    /// <summary>Ultra Sun and Ultra Moon: seven pockets in 0xE28 bytes.</summary>
    [Fact]
    public void The_block_is_seven_pockets_laid_end_to_end()
    {
        Assert.Equal(7, Layout.Pockets.Count);
        Assert.Equal(0xE28, Layout.BlockSize);
        Assert.Equal(28, Layout.PointerTableBytes);

        var expected = 0;

        foreach (var pocket in Layout.Pockets)
        {
            Assert.Equal(expected, pocket.Offset);
            expected += pocket.Bytes;
        }

        Assert.Equal(Layout.BlockSize, expected);
    }

    /// <summary>
    /// The offsets are derived from PKHeX, not copied, so this pins the ones the running game
    /// was verified against: a change in either would otherwise pass unnoticed.
    /// </summary>
    [Theory]
    [InlineData(InventoryType.Items, 0x000, 427)]
    [InlineData(InventoryType.KeyItems, 0x6AC, 198)]
    [InlineData(InventoryType.TMHMs, 0x9C4, 108)]
    [InlineData(InventoryType.Medicine, 0xB74, 60)]
    [InlineData(InventoryType.Berries, 0xC64, 67)]
    [InlineData(InventoryType.ZCrystals, 0xD70, 35)]
    [InlineData(InventoryType.BattleItems, 0xDFC, 11)]
    public void Every_pocket_sits_where_the_game_keeps_it(InventoryType type, int offset, int slots)
    {
        var pocket = Layout.Pockets.Single(p => p.Type == type);

        Assert.Equal(offset, pocket.Offset);
        Assert.Equal(slots, pocket.Slots);
    }

    [Theory]
    [InlineData(BagService.PokeBallItemId, InventoryType.Items)]
    [InlineData(BagService.RareCandyItemId, InventoryType.Medicine)]
    [InlineData(BagService.ShinyCharmItemId, InventoryType.KeyItems)]
    [InlineData(BagService.HeartScaleItemId, InventoryType.Items)]
    [InlineData(328, InventoryType.TMHMs)]
    [InlineData(671, InventoryType.Items)]   // Pinsirita, del cartucho
    [InlineData(514, InventoryType.Items)]   // Lucarionite Z, de la expansión (id reusado)
    [InlineData(961, InventoryType.Items)]   // megapiedra nueva de la expansión
    [InlineData(PermaLocke.Core.Domain.SuperCandy.ItemId, InventoryType.Medicine)]   // el SuperCarameloraro, junto al Caramelo Raro
    [InlineData(PermaLocke.Core.Domain.InfiniteRepel.ItemId, InventoryType.KeyItems)] // el Repelente Infinito, objeto clave
    [InlineData(PermaLocke.Core.Domain.EggTurbo.ItemId, InventoryType.KeyItems)] // la Incubadora Turbo, objeto clave
    [InlineData(1017, InventoryType.Items)]  // Hawluchanita, de la expansión
    public void An_item_is_routed_to_its_own_pocket(int itemId, InventoryType expected)
    {
        Assert.Equal(expected, Layout.PocketFor(itemId)?.Type);
    }

    /// <summary>
    /// A key item is one and only one. The tool that hands out the Shiny Charm leans on this to
    /// tell "you already have it" from "the write did not take", so it is worth pinning down.
    /// </summary>
    [Fact]
    public void A_key_item_pocket_holds_exactly_one_of_each()
    {
        var pocket = Layout.PocketFor(BagService.ShinyCharmItemId);

        Assert.NotNull(pocket);
        Assert.Equal(1, pocket.MaxCount);
    }

    /// <summary>
    /// The Heart Scale <b>stacks</b>, which is the whole difference between its button and the
    /// Shiny Charm's: pressing "+10" twice really does leave twenty. If its pocket ever capped at
    /// one, the second press would report "you already have it" and the button would be a lie.
    /// </summary>
    [Fact]
    public void Heart_scales_pile_up_rather_than_capping_at_one()
    {
        var pocket = Layout.PocketFor(BagService.HeartScaleItemId);

        Assert.NotNull(pocket);
        Assert.True(pocket.MaxCount >= 20);
    }

    /// <summary>
    /// The ids are typed by hand from the cartridge table, and a wrong one would hand over the
    /// wrong object without anything failing. PKHeX is the same table the game ships.
    /// </summary>
    [Theory]
    [InlineData(BagService.RareCandyItemId, "Caramelo Raro")]
    [InlineData(BagService.ShinyCharmItemId, "Amuleto Iris")]
    [InlineData(BagService.HeartScaleItemId, "Escama Corazón")]
    [InlineData(BagService.PokeBallItemId, "Poké Ball")]
    public void The_item_ids_name_what_they_are_meant_to(int itemId, string expected)
    {
        Assert.Equal(expected, new PkhexItemLookup().GetName(itemId));
    }

    [Fact]
    public void An_item_no_pocket_accepts_has_nowhere_to_go()
    {
        Assert.Null(Layout.PocketFor(0));
        // 1023 (BagEntry.MaxItemId) ya no vale aquí: la expansión lo usa para una megapiedra.
    }

    /// <summary>
    /// The bug this class exists for: rebuilding the word as id and count alone wiped the free
    /// space index and the "new" badge, which live in the upper bits.
    /// </summary>
    [Fact]
    public void Packing_an_entry_keeps_every_field()
    {
        var entry = new BagEntry(50, 92, 813, true);
        var round = BagEntry.Unpack(entry.Pack());

        Assert.Equal(entry, round);
    }

    /// <summary>Checked against PKHeX, which is what the save file itself is written with.</summary>
    [Fact]
    public void An_entry_packs_the_way_PKHeX_packs_it()
    {
        var bag = new PlayerBag7USUM(new SAV7USUM());
        var pouch = bag.Pouches.Single(p => p.Type == InventoryType.Medicine);

        pouch.Items[0] = new InventoryItem7 { Index = 50, Count = 92, IsNew = true };

        var image = new byte[0xE28];
        bag.CopyTo(image);

        var medicine = Layout.Pockets.Single(p => p.Type == InventoryType.Medicine);
        var written = BitConverter.ToUInt32(image, medicine.Offset);

        Assert.Equal(written, new BagEntry(50, 92, 0, true).Pack());
    }

    [Fact]
    public void A_word_of_zeros_is_an_empty_slot()
    {
        Assert.True(BagEntry.Unpack(0).IsEmpty);
        Assert.False(BagEntry.Unpack(new BagEntry(4, 1, 0, false).Pack()).IsEmpty);
    }

    /// <summary>
    /// A key item is stored with a quantity of zero and is not packed at the front: in the
    /// real save the Sparkling Stone sat alone in slot 197. Reading has to survive both.
    /// </summary>
    [Fact]
    public void A_key_item_with_no_quantity_in_the_last_slot_is_still_read()
    {
        var keyItems = Layout.Pockets.Single(p => p.Type == InventoryType.KeyItems);
        var image = new byte[Layout.BlockSize];

        BitConverter.GetBytes(new BagEntry(845, 0, 0, false).Pack())
            .CopyTo(image, keyItems.Offset + ((keyItems.Slots - 1) * 4));

        var slot = Assert.Single(Layout.DecodeBlock(0x33011934, image));

        Assert.Equal(InventoryType.KeyItems, slot.Pocket.Type);
        Assert.Equal(keyItems.Slots - 1, slot.Index);
        Assert.Equal(845, slot.Entry.ItemId);
        Assert.Equal(0, slot.Entry.Count);
    }

    /// <summary>Reads back the bag the running game really had, byte for byte.</summary>
    [Fact]
    public void The_bag_seen_in_the_verified_session_decodes_slot_by_slot()
    {
        const uint BaseAddress = 0x33011934;
        var image = new byte[Layout.BlockSize];

        Put(image, InventoryType.Items, 0, new BagEntry(4, 5, 0, false));
        Put(image, InventoryType.Items, 1, new BagEntry(305, 1, 0, false));
        Put(image, InventoryType.Items, 2, new BagEntry(884, 1, 0, false));
        Put(image, InventoryType.Medicine, 0, new BagEntry(17, 5, 0, false));

        var contents = Layout.DecodeBlock(BaseAddress, image);

        Assert.Equal(4, contents.Count);

        var potion = contents.Single(slot => slot.Entry.ItemId == 17);

        // Medicinas empieza en +0xB74, que es la dirección donde se verificó la escritura.
        Assert.Equal(BaseAddress + 0xB74, potion.Address);
        Assert.Equal(5, potion.Entry.Count);
    }

    /// <summary>The whole point: the table has to point back into its own block.</summary>
    [Fact]
    public void The_pointer_table_of_the_real_bag_is_recognised()
    {
        const uint BaseAddress = 0x33011934;
        var table = PointerTableFor(BaseAddress);

        Assert.True(Layout.TryMatchPointerTable(BaseAddress + (uint)Layout.BlockSize, table,
            out var found));
        Assert.Equal(BaseAddress, found);
    }

    /// <summary>The order the game writes the pointers in is not assumed anywhere.</summary>
    [Fact]
    public void The_pointers_may_come_in_any_order()
    {
        const uint BaseAddress = 0x33011934;
        var table = new byte[Layout.PointerTableBytes];
        var pockets = Layout.Pockets.Reverse().ToList();

        for (var index = 0; index < pockets.Count; index++)
        {
            BitConverter.GetBytes(BaseAddress + (uint)pockets[index].Offset).CopyTo(table, index * 4);
        }

        Assert.True(Layout.TryMatchPointerTable(BaseAddress + (uint)Layout.BlockSize, table, out var found));
        Assert.Equal(BaseAddress, found);
    }

    [Fact]
    public void Random_memory_is_not_a_pointer_table()
    {
        var junk = new byte[Layout.PointerTableBytes];
        new Random(20260819).NextBytes(junk);

        Assert.False(Layout.TryMatchPointerTable(0x081D55B0, junk, out _));
    }

    /// <summary>
    /// The near miss that matters: a table whose pointers are all valid but one is repeated
    /// instead of pointing at its own pocket. Accepting it would place the block one pocket off.
    /// </summary>
    [Fact]
    public void A_table_with_a_repeated_pointer_is_rejected()
    {
        const uint BaseAddress = 0x33011934;
        var table = PointerTableFor(BaseAddress);

        table.AsSpan(0, 4).CopyTo(table.AsSpan(4));

        Assert.False(Layout.TryMatchPointerTable(BaseAddress + (uint)Layout.BlockSize, table, out _));
    }

    /// <summary>
    /// The same seven pointers found anywhere other than the end of their own block are not a
    /// bag: a copy of the table elsewhere in memory must not be taken for one.
    /// </summary>
    [Fact]
    public void A_table_that_does_not_end_its_own_block_is_rejected()
    {
        const uint BaseAddress = 0x33011934;
        var table = PointerTableFor(BaseAddress);

        Assert.False(Layout.TryMatchPointerTable(BaseAddress + (uint)Layout.BlockSize + 4, table, out _));
        Assert.False(Layout.TryMatchPointerTable(0x0A000000, table, out _));
    }

    [Fact]
    public void A_truncated_table_is_rejected_instead_of_read_past_its_end()
    {
        const uint BaseAddress = 0x33011934;
        var table = PointerTableFor(BaseAddress);

        Assert.False(Layout.TryMatchPointerTable(BaseAddress + (uint)Layout.BlockSize,
            table.AsSpan(0, Layout.PointerTableBytes - 4), out _));
    }

    private static byte[] PointerTableFor(uint baseAddress)
    {
        var table = new byte[Layout.PointerTableBytes];

        for (var index = 0; index < Layout.Pockets.Count; index++)
        {
            BitConverter.GetBytes(baseAddress + (uint)Layout.Pockets[index].Offset)
                .CopyTo(table, index * 4);
        }

        return table;
    }

    private static void Put(byte[] image, InventoryType type, int slot, BagEntry entry)
    {
        var pocket = Layout.Pockets.Single(p => p.Type == type);
        BitConverter.GetBytes(entry.Pack()).CopyTo(image, pocket.Offset + (slot * 4));
    }
}
