namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Finds every field item of a zone: the Poké Balls lying on the ground, gold ones for TMs, and
/// the berry piles.
/// <para>
/// They live in subfile <c>zone * 11</c> of encdata, which is an <c>ED</c> mini pack; entry 10
/// of it is an <c>EI</c> pack of item placements and entry 11 an <c>EB</c> pack of berry piles.
/// Locations taken from the Universal Pokémon Randomizer ZX (GPLv3), whose gen 7 handler already
/// solved this, and checked against the cartridge.
/// </para>
/// <para>
/// Only the item id is rewritten, in place. The mini packs are never repacked: pk3DS's
/// <c>PackMini</c> shrinks the header from 128 bytes to 80, which is exactly what left the game
/// without wild encounters the first time round.
/// </para>
/// </summary>
public static class FieldItemTable
{
    /// <summary>Subfiles each zone owns inside encdata.</summary>
    public const int SubfilesPerZone = 11;

    /// <summary>Entry of the ED pack holding the item placements.</summary>
    private const int ItemEntry = 10;

    /// <summary>Entry of the ED pack holding the berry piles.</summary>
    private const int BerryEntry = 11;

    private const int ItemRecordSize = 64;
    private const int ItemIdOffset = 52;

    private const int BerryRecordSize = 68;
    private const int BerryRecordsOffset = 4;
    private const int BerryIdsOffset = 54;
    private const int BerriesPerPile = 7;

    /// <summary>
    /// Byte offsets, within the zone's ED payload, of every u16 item id it places.
    /// Empty when the zone has none, or is not an ED pack at all.
    /// </summary>
    public static IReadOnlyList<int> Locate(byte[] environment)
    {
        var slots = new List<int>();
        if (!TryGetEntry(environment, 0, "ED", out var edStart, out var edEnd))
        {
            return slots;
        }

        if (TryGetChild(environment, edStart, edEnd, ItemEntry, "EI", out var eiStart, out var eiEnd))
        {
            CollectItems(environment, eiStart, eiEnd, slots);
        }

        if (TryGetChild(environment, edStart, edEnd, BerryEntry, "EB", out var ebStart, out var ebEnd))
        {
            CollectBerries(environment, ebStart, ebEnd, slots);
        }

        return slots;
    }

    public static int GetItem(byte[] environment, int slot) => BitConverter.ToUInt16(environment, slot);

    public static void SetItem(byte[] environment, int slot, int item) =>
        BitConverter.GetBytes((ushort)item).CopyTo(environment, slot);

    private static void CollectItems(byte[] data, int start, int end, List<int> slots)
    {
        for (var entry = 0; TryGetChild(data, start, end, entry, null, out var s, out var e); entry++)
        {
            if (e - s < 1)
            {
                continue;
            }

            var count = data[s];
            for (var record = 0; record < count; record++)
            {
                var at = s + (record * ItemRecordSize) + ItemIdOffset;
                if (at + 2 <= e)
                {
                    slots.Add(at);
                }
            }
        }
    }

    private static void CollectBerries(byte[] data, int start, int end, List<int> slots)
    {
        for (var entry = 0; TryGetChild(data, start, end, entry, null, out var s, out var e); entry++)
        {
            if (e - s < 1)
            {
                continue;
            }

            var count = data[s];
            for (var pile = 0; pile < count; pile++)
            {
                for (var berry = 0; berry < BerriesPerPile; berry++)
                {
                    var at = s + BerryRecordsOffset + (pile * BerryRecordSize) + BerryIdsOffset + (berry * 2);
                    if (at + 2 <= e)
                    {
                        slots.Add(at);
                    }
                }
            }
        }
    }

    /// <summary>Bounds of the whole pack, checking its two character identifier.</summary>
    private static bool TryGetEntry(byte[] data, int start, string ident, out int packStart, out int packEnd)
    {
        packStart = start;
        packEnd = data.Length;
        return data.Length >= 4 && data[start] == ident[0] && data[start + 1] == ident[1];
    }

    /// <summary>
    /// Bounds of one entry of a mini pack: a two byte identifier, a u16 count, and then one u32
    /// offset per entry plus a closing one.
    /// </summary>
    private static bool TryGetChild(byte[] data, int packStart, int packEnd, int index, string? ident,
        out int start, out int end)
    {
        start = end = 0;
        if (packStart + 4 > packEnd)
        {
            return false;
        }

        var count = BitConverter.ToUInt16(data, packStart + 2);
        if (index >= count)
        {
            return false;
        }

        var table = packStart + 4 + (index * 4);
        if (table + 8 > packEnd)
        {
            return false;
        }

        start = packStart + BitConverter.ToInt32(data, table);
        end = packStart + BitConverter.ToInt32(data, table + 4);
        if (start < packStart || end > packEnd || end < start)
        {
            return false;
        }

        return ident is null || (end - start >= 2 && data[start] == ident[0] && data[start + 1] == ident[1]);
    }
}
