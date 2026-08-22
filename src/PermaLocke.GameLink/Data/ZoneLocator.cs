using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Data;

/// <param name="Anchor">
/// The word 0x10 before the area field. It is a handle the game keeps per record and it is
/// never zero, which is what tells a live record apart from blank memory.
/// </param>
public readonly record struct ZoneReading(uint Anchor, ushort Area)
{
    /// <summary>
    /// False for a record whose handle is zero, which is what a wrong anchor looks like when it
    /// lands on blank memory. Area 0 is a real area, so without this a bad anchor would report
    /// Route 1 instead of reporting that it does not know.
    /// </summary>
    /// <remarks>
    /// The words immediately around the area field are no good for this: they change with the
    /// zone and do go to zero. Measured in the same record, the word at -0x04 held 0x000F0111 in
    /// Pueblo Lilii and 0x00000000 in Route 1. The handle at -0x10 held a value in both.
    /// </remarks>
    public bool LooksLive => Anchor != 0;
}

/// <summary>
/// Reads which area of the game the player is standing in.
/// </summary>
/// <remarks>
/// <para>
/// The value is the index into the 336 areas of <c>encdata</c> — the same numbering the
/// randomizer uses, so the current zone and the encounter tables need no translation between
/// them. Established by measuring two zones and ruling out the three other candidate tables;
/// see <c>docs/ARCHITECTURE.md</c> §23.
/// </para>
/// <para>
/// The game keeps four copies, two records of 0x1A0 apart, duplicated 0x121E00 further on. They
/// are not searched for: they are read at a fixed distance from the bag block, which is the one
/// structure that can be located on its own (§22). The anchor is only as good as the checks on
/// top of it, so a reading is believed only when the four copies agree and each one sits in
/// memory that is not blank. When they disagree, PermaLocke says it does not know where the
/// player is rather than guessing — the same rule taken for the battle flag in §16.
/// </para>
/// </remarks>
public sealed class ZoneLocator(AzaharRpcClient client)
{
    /// <summary>
    /// Areas in <c>encdata</c> for Ultra Sun and Ultra Moon: 3.696 subfiles, 11 per area.
    /// Read from the cartridge, not assumed; see §19.
    /// </summary>
    public const int UltraSunMoonAreaCount = 336;

    /// <summary>
    /// Distance from the start of the bag block to each copy of the area field.
    /// </summary>
    /// <remarks>
    /// Measured with the bag at 0x33011934 and the copies at 0x330DDCA8, 0x330DDE48,
    /// 0x331FFAA8 and 0x331FFC48. The two pairs are 0x1A0 apart, which is the size of the
    /// record, and the pairs themselves 0x121E00 apart. Both distances survived a full restart
    /// of the emulator.
    /// </remarks>
    public static IReadOnlyList<uint> CopyOffsets { get; } = [0xCC374, 0xCC514, 0x1EE174, 0x1EE314];

    /// <summary>Distance back from the area field to the record handle.</summary>
    private const int HandleOffset = 0x10;

    /// <summary>
    /// The area the player is in, or null when it cannot be established.
    /// </summary>
    public int? Read(BagBlock bag)
    {
        var readings = new ZoneReading[CopyOffsets.Count];

        for (var copy = 0; copy < CopyOffsets.Count; copy++)
        {
            // Se lee desde el asa del registro hasta el campo de zona de una vez.
            if (!client.TryReadMemory(bag.BaseAddress + CopyOffsets[copy] - HandleOffset,
                    HandleOffset + 4, out var data))
            {
                return null;
            }

            readings[copy] = new ZoneReading(
                BitConverter.ToUInt32(data, 0),
                BitConverter.ToUInt16(data, HandleOffset));
        }

        return TryResolve(readings, out var area) ? area : null;
    }

    /// <summary>
    /// Decides whether the copies say the same thing, and whether that thing can be believed.
    /// </summary>
    /// <remarks>
    /// Pure on purpose: this is the part that decides whether PermaLocke acts on a zone, and it
    /// has to be testable without an emulator in front of it.
    /// </remarks>
    /// <summary>
    /// True when this copy could be a zone record at all: a handle that is not blank, and a number
    /// that is one of the areas <c>encdata</c> has.
    /// </summary>
    public static bool CouldBeAnArea(ZoneReading copy) =>
        copy.LooksLive && copy.Area < UltraSunMoonAreaCount;

    /// <summary>
    /// Decides whether the copies say the same thing, and whether that thing can be believed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure on purpose: this is the part that decides whether PermaLocke acts on a zone, and it
    /// has to be testable without an emulator in front of it.
    /// </para>
    /// <para>
    /// <b>The four have to agree.</b> Letting a copy abstain — skipping the ones that cannot be an
    /// area and believing whatever the rest said — was tried and undone the same day. It looked
    /// right on a still reading: two copies agreed on a plausible area and the other two held
    /// obvious rubbish, so counting the rubbish as <em>disagreement</em> seemed like the reason the
    /// zone was never established. Then the player walked, and those two "usable" copies calmly
    /// agreed on the Hauoli cemetery while he stood in the Pokémon Center.
    /// </para>
    /// <para>
    /// The strict rule was refusing for an imprecise reason and still arriving at the right answer,
    /// which for a rule that confiscates the player's items is the answer that matters. Requiring
    /// all four is what makes «no lo sé» the default.
    /// </para>
    /// </remarks>
    public static bool TryResolve(ReadOnlySpan<ZoneReading> copies, out int area)
    {
        area = -1;

        if (copies.Length == 0)
        {
            return false;
        }

        foreach (var copy in copies)
        {
            // Una sola copia que no pueda ser una zona basta para no fiarse de ninguna.
            if (!CouldBeAnArea(copy) || copy.Area != copies[0].Area)
            {
                return false;
            }
        }

        area = copies[0].Area;
        return true;
    }
}
