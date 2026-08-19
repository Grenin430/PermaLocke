namespace PermaLocke.Randomizer.Modules;

/// <param name="Index">Position in the shop list.</param>
/// <param name="Offset">Byte offset of its first item inside Shop.cro.</param>
/// <param name="Count">How many things it sells.</param>
public sealed record ShopInventory(int Index, int Offset, int Count);

/// <summary>
/// Byte-level view of the mart inventories inside <c>Shop.cro</c>.
/// <para>
/// A CRO is a relocatable module, not a GARC. Editing one does not work on real hardware unless
/// the RO module's signature check is patched, which is why pk3DS warns about it; Azahar,
/// however, never checks: <c>CROHelper::VerifyHash</c> is an empty stub and <c>LoadCRR</c> is
/// marked STUBBED. That makes this viable here and only here.
/// </para>
/// </summary>
public static class ShopTable
{
    /// <summary>First item of the first inventory. Items are u16 ids, back to back.</summary>
    private const int ItemsOffset = 0x50BC;

    /// <summary>One byte per inventory saying how many items it holds; ends at the first zero.</summary>
    private const int CountsOffset = 0x52DD;

    /// <summary>The first eight inventories are the ordinary counter, which grows as trials are cleared.</summary>
    public const int RegularMartCount = 8;

    /// <summary>
    /// Ultra Moon ships 92 TMs in a contiguous block, verified against the cartridge item list:
    /// 328 is MT01 and 419 is MT92. What follows is MO01-MO06, the gen 6 HM slots, which do
    /// nothing here, and then two unnamed ids. pk3DS bans up to 427, a leftover from gen 6.
    /// </summary>
    public const int FirstTechnicalMachine = 328;
    public const int LastTechnicalMachine = 419;

    public static bool IsTechnicalMachine(int item) =>
        item is >= FirstTechnicalMachine and <= LastTechnicalMachine;

    /// <summary>Walks the length table and returns where every inventory lives.</summary>
    public static IReadOnlyList<ShopInventory> Read(byte[] cro)
    {
        var inventories = new List<ShopInventory>();
        var offset = ItemsOffset;

        for (var index = 0; ; index++)
        {
            var count = (sbyte)cro[CountsOffset + index];
            if (count <= 0)
            {
                break;
            }

            inventories.Add(new ShopInventory(index, offset, count));
            offset += count * 2;
        }

        return inventories;
    }

    public static int GetItem(byte[] cro, ShopInventory shop, int slot) =>
        BitConverter.ToUInt16(cro, shop.Offset + (slot * 2));

    public static void SetItem(byte[] cro, ShopInventory shop, int slot, int item) =>
        BitConverter.GetBytes((ushort)item).CopyTo(cro, shop.Offset + (slot * 2));

    /// <summary>True when everything the shop sells is a TM.</summary>
    public static bool SellsTechnicalMachines(byte[] cro, ShopInventory shop)
    {
        for (var slot = 0; slot < shop.Count; slot++)
        {
            if (!IsTechnicalMachine(GetItem(cro, shop, slot)))
            {
                return false;
            }
        }
        return shop.Count > 0;
    }
}
