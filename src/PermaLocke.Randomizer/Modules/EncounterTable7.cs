namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Byte-level view of one Ultra Sun/Moon wild encounter table, living inside the decompressed
/// payload of an encdata subfile.
/// <para>
/// PermaLocke reads the layout with pk3DS but does not write with it: pk3DS's
/// <c>Area7.GetDayNightTableBinary</c> rebuilds the containing block and, doing so, zeroes the
/// four byte prefix each table carries (non-zero in the vanilla cartridge) and shrinks the mini
/// header from 128 to 80 bytes. The result loads in Ultra Moon and yields no encounters at all.
/// Patching the slots in place leaves every unknown field exactly where it was.
/// </para>
/// </summary>
public readonly struct EncounterTable7(byte[] payload, int offset)
{
    /// <summary>Slot sets: the base table plus seven SOS/weather variants, ten slots each.</summary>
    public const int SlotSets = 8;
    public const int SlotsPerSet = 10;

    /// <summary>Six extra SOS slots sit after the sets.</summary>
    public const int AdditionalSosSlots = 6;

    private const int RatesOffset = 0x02;
    private const int SlotsOffset = 0x0C;
    private const int AdditionalSosOffset = 0x14C;

    /// <summary>Bytes one table occupies. Day and night tables sit back to back.</summary>
    public const int Size = 0x164;

    /// <summary>Offset of the day table within a mini entry; the first four bytes are a prefix.</summary>
    public const int DayTableOffset = 0x004;

    public const int NightTableOffset = DayTableOffset + Size;

    /// <summary>Bytes a mini entry needs before it can hold both tables.</summary>
    public const int MinimumEntrySize = NightTableOffset + Size;

    public byte MinLevel
    {
        get => payload[offset];
        set => payload[offset] = value;
    }

    public byte MaxLevel
    {
        get => payload[offset + 1];
        set => payload[offset + 1] = value;
    }

    /// <summary>Encounter rate of a slot, in percent.</summary>
    public byte GetRate(int slot) => payload[offset + RatesOffset + slot];

    /// <summary>
    /// Every slot position in the table: the eight sets and then the additional SOS slots.
    /// </summary>
    public static IEnumerable<int> SlotOffsets()
    {
        for (var set = 0; set < SlotSets; set++)
        {
            for (var slot = 0; slot < SlotsPerSet; slot++)
            {
                yield return SlotsOffset + (set * SlotsPerSet * 4) + (slot * 4);
            }
        }

        for (var extra = 0; extra < AdditionalSosSlots; extra++)
        {
            yield return AdditionalSosOffset + (extra * 4);
        }
    }

    /// <summary>Species of the slot at <paramref name="slotOffset"/>. Zero means empty.</summary>
    public int GetSpecies(int slotOffset) => (int)(BitConverter.ToUInt32(payload, offset + slotOffset) & 0x7FF);

    public int GetForme(int slotOffset) => (int)((BitConverter.ToUInt32(payload, offset + slotOffset) >> 11) & 0x1F);

    /// <summary>
    /// Replaces species and forme, leaving every other bit of the word alone. The upper half is
    /// not documented and pk3DS discards it; keeping it costs nothing and risks nothing.
    /// </summary>
    public void SetSpecies(int slotOffset, int species, int forme = 0)
    {
        var at = offset + slotOffset;
        var raw = BitConverter.ToUInt32(payload, at);
        var updated = (raw & ~0xFFFFu) | ((uint)species & 0x7FF) | (((uint)forme & 0x1F) << 11);
        BitConverter.GetBytes(updated).CopyTo(payload, at);
    }
}
