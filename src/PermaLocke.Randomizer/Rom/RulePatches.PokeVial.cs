using System.Buffers.Binary;

using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The Pokévial (2026-10-16): a key item that, used from the bag, heals the whole party (HP, status and PP) anywhere, except the fallen, while it
/// has uses left. Only PermaLocke hands it out (MISCELÁNEA).
/// </summary>
/// <remarks>
/// <para>
/// It is item <see cref="PokeVialItem"/>, another of the expansion's unused slots (120), made as the Repelente Infinito and the Incubadora Turbo:
/// a copy of the Exp. Share's data (the key items pocket, never spent), its text and an icon. The bag runs it through the same hook
/// (<c>Bag.cro</c>) to its own routine.
/// </para>
/// <para>
/// The routine does not heal: the game keeps no «heal the party» the bag can call that has been found and tried. It takes one use from the byte
/// PermaLocke writes (<see cref="RuleBlock.PokeVialUses"/>), raises <see cref="RuleBlock.PokeVialRequest"/> and says so; PermaLocke sees the
/// request, heals the party in the game's memory (what the party menu reads) and puts the request back. With no uses left it only says the vial
/// is empty. With PermaLocke closed nobody answers the request: the use goes and nothing is healed, which is why the byte starts at 0.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const int PokeVialItem = PokeVial.ItemId;

    public const string PokeVialName = PokeVial.Name;

    public const string PokeVialDescription =
        @"Cura los PS, el estado y los PP de todo tu\nequipo en cualquier sitio. Sus usos se\nrecargan en el Centro Pokémon.";

    /// <summary>Two more empty lines of the bag's text, 149 and 150.</summary>
    public const int PokeVialUsedLine = 149, PokeVialEmptyLine = 150;

    public const string PokeVialUsedText = "¡Tu equipo se ha recuperado con el Pokévial!",
        PokeVialEmptyText = @"El Pokévial está vacío. Recárgalo en un\nCentro Pokémon.";

    private static readonly (int File, int Line, string Unused, string Wanted)[] PokeVialLines =
    [
        (ItemFlavorFile, PokeVialItem, "", PokeVialDescription),
        (ItemNamesFile, PokeVialItem, "(?)", PokeVialName),
        (ItemPluralFile, PokeVialItem, "(?)", "Pokéviales"),
        (ItemMessageFile, PokeVialItem, "(?)[VAR 1101(00FE,0000)]", PokeVialName + "[VAR 1101(00FE,0000)]"),
        (BagTextFile, PokeVialUsedLine, $"[~ {PokeVialUsedLine}]", PokeVialUsedText),
        (BagTextFile, PokeVialEmptyLine, $"[~ {PokeVialEmptyLine}]", PokeVialEmptyText)
    ];

    private static int PokeVialIconEntry => ItemIconTable + 4 * PokeVialItem;

    private static readonly int[] PokeVialOffsets = [PokeVialIconEntry];

    /// <summary>The code.bin record for the item (its icon), or null when this is not the code it was made for.</summary>
    public static IReadOnlyList<IpsRecord>? PokeVialRecords(ReadOnlySpan<byte> code, int icon)
    {
        if (PokeVialIconEntry + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[PokeVialIconEntry..]) != BlankIcon) return null;
        return [new IpsRecord(PokeVialIconEntry, Word((uint)icon))];
    }

    /// <summary>
    /// The Pokévial in the bag: with uses left, one goes and the healing is asked for; then the message (used or empty), back to the bag (state
    /// 2) with nothing spent.
    /// </summary>
    public static byte[] BagVialCode() => Words(
        0xE92D4070,                         //  0 stmdb  sp!, {r4-r6, lr}
        0xE1A04000,                         //  1 cpy    r4, r0           ; the bag
        0xE59F2040,                         //  2 ldr    r2, [uses]
        0xE5D23000,                         //  3 ldrb   r3, [r2]
        0xE3530000,                         //  4 cmp    r3, #0
        0x12433001,                         //  5 subne  r3, r3, #1
        0x15C23000,                         //  6 strbne r3, [r2]         ; one use less
        0x13A06001,                         //  7 movne  r6, #1
        0x15C26001,                         //  8 strbne r6, [r2, #1]     ; heal, PermaLocke
        0xE5940070,                         //  9 ldr    r0, [r4, #0x70]  ; the message window
        0x03A01000 | PokeVialEmptyLine,     // 10 moveq  r1, #150
        0x13A01000 | PokeVialUsedLine,      // 11 movne  r1, #149
        Bl(BagVial + 12 * 4, BagShow),      // 12 bl     show
        0xE5940070,                         // 13 ldr    r0, [r4, #0x70]
        0xE3A01000,                         // 14 mov    r1, #0
        Bl(BagVial + 15 * 4, CloseMessage), // 15 bl     0xa5e4
        0xE3A00002,                         // 16 mov    r0, #2
        0xE5840004,                         // 17 str    r0, [r4, #4]
        0xE3A00000,                         // 18 mov    r0, #0
        0xE8BD8070,                         // 19 ldmia  sp!, {r4-r6, pc}
        RuleBlock.PokeVialUses);            // 20 the byte's address

    /// <summary>
    /// Puts the item into the installed mod's files: its data, its text, its icon. Returns the icon's index (for the code record), or null with
    /// what went wrong in <paramref name="problem"/>.
    /// </summary>
    public static int? InstallPokeVial(string romfs, out string? problem)
    {
        problem = null;
        var itemData = Path.Combine(romfs, "a", "0", "1", "9");
        var text = Path.Combine(romfs, "a", "0", "3", "6");
        var icons = Path.Combine(romfs, "a", "0", "6", "1");

        if (!File.Exists(itemData) || !File.Exists(text) || !File.Exists(icons))
        {
            problem = "faltan ficheros del mod: sin Pokévial";
            return null;
        }

        problem = ItemData(itemData, PokeVialItem, ExpShareItem, 0, PokeVialName)
                  ?? ItemText(text, PokeVialLines, PokeVialName);
        if (problem is not null) return null;

        return AppendIcon(icons, Sprites.PokeVialIcon.Bflim, RepelItem, PokeVialName, out problem);
    }
}
