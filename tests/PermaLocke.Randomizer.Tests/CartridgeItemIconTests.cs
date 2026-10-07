using System.Buffers.Binary;

using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>The game's own item to icon table fills in what was never measured (2026-10-07: pickups drawn as a parcel).</summary>
[Collection("ItemIconIndex")]
public sealed class CartridgeItemIconTests
{
    private static byte[] Code(params (int Item, uint Icon)[] entries)
    {
        var code = new byte[ItemIconIndex.CartridgeTableOffset + (4 * ItemIconIndex.CartridgeTableItems)];
        foreach (var (item, icon) in entries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(ItemIconIndex.CartridgeTableOffset + (4 * item)), icon);
        }

        return code;
    }

    [Fact]
    public void An_item_nobody_measured_takes_the_icon_of_the_cartridge_table_and_the_blank_one_stays_unknown()
    {
        try
        {
            Assert.False(ItemIconIndex.TryGet(400, out _, 849));

            ItemIconIndex.UseCartridgeTable(Code((400, 123), (401, 768), (402, 5000)));

            Assert.True(ItemIconIndex.TryGet(400, out var icon, 849));
            Assert.Equal(123, icon);
            Assert.False(ItemIconIndex.TryGet(401, out _, 849));   // «?»: no tiene icono
            Assert.False(ItemIconIndex.TryGet(402, out _, 849));   // fuera del contenedor

            // Lo medido manda sobre la tabla: la Baya Tamate (174) es el 156 aunque la tabla diga otra cosa.
            ItemIconIndex.UseCartridgeTable(Code((174, 9)));
            Assert.Equal(156, ItemIconIndex.Of(174, 849));
        }
        finally
        {
            ItemIconIndex.UseCartridgeTable([]);
        }
    }
}
