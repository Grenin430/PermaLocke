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

    /// <summary>
    /// Whether an item is a TM, <b>by its id range only</b>. Incomplete on purpose-free grounds:
    /// see <see cref="TechnicalMachines"/>.
    /// </summary>
    /// <remarks>
    /// This covers MT01 to MT92 and <b>misses eight</b>. The hundred TMs are not one range — they
    /// are 328-419, 618-620 and 690-694, measured in §62 — so MT93 to MT100 fall outside it. That
    /// gap cost a real bug: a gold Poké Ball holding MT97 was not recognised as a machine, went
    /// into the ordinary shuffle, and handed the player a Potion where a TM should have been.
    /// <para>
    /// Kept because the shop code reads a contiguous range for its own reasons. Anything deciding
    /// "is this a TM" must use <see cref="TechnicalMachines"/>, which asks the cartridge.
    /// </para>
    /// </remarks>
    public static bool IsInMachineRange(int item) =>
        item is >= FirstTechnicalMachine and <= LastTechnicalMachine;

    /// <summary>
    /// Every TM the loaded game has, found by name rather than by a range written down here.
    /// </summary>
    /// <remarks>
    /// The cartridge names them <c>MT01</c>..<c>MT100</c> in Spanish and <c>TM01</c>.. in English,
    /// and the HMs <c>MO</c>/<c>HM</c>, so a two letter prefix plus digits identifies them exactly
    /// and leaves the HMs out. Reading it from the text means a mod that adds or moves machines is
    /// followed automatically, which is the whole point: the ids are not ours to assume.
    /// </remarks>
    public static int[] TechnicalMachines(string[] itemNames) =>
    [
        .. Enumerable.Range(0, itemNames.Length).Where(id => IsMachineName(itemNames[id]))
    ];

    /// <summary>«MT01», «TM100». Two letters and then nothing but digits.</summary>
    private static bool IsMachineName(string name) =>
        name.Length > 2
        && (name.StartsWith("MT", StringComparison.Ordinal)
            || name.StartsWith("TM", StringComparison.Ordinal))
        && name.Skip(2).All(char.IsAsciiDigit);

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
            if (!IsInMachineRange(GetItem(cro, shop, slot)))
            {
                return false;
            }
        }
        return shop.Count > 0;
    }
}
