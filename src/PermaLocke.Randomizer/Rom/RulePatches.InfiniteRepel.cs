using System.Buffers.Binary;

using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The Repelente Infinito (2026-10-07): a key item that, used from the bag, turns a Repel that never wears off on, and used
/// again, off. Only PermaLocke hands it out (MISCELÁNEA).
/// </summary>
/// <remarks>
/// <para>
/// It is item <see cref="InfiniteRepelItem"/>, another of the expansion's unused slots. Its data is a copy of the Exp.
/// Share's: the key items pocket, never spent, and the same «field use» routine as the Repels (5), which in the bag means
/// «look the item up in a table». Name, description and icon go in as with the SuperCarameloraro.
/// </para>
/// <para>
/// The Repel lives in <c>Field::EventWork</c>: steps left at +0xA58 and the item that set them at +0xA3E.
/// <c>DecMushiyokeCount</c> (<c>code.bin</c> 0x2A8EB8) takes one step off and says when they reach zero; rewritten in its
/// own twelve words, it leaves them alone when the item is this one. So one step, set by this item, lasts for ever.
/// </para>
/// <para>
/// The bag (<c>Bag.cro</c>) looks the item up in a table of thirteen (Repels, Escape Rope, Honey, Exp. Share, Roto
/// coupons) and calls the routine found with the bag and the item. Before that lookup a <c>bl</c> sends this item to
/// its own routine, which lives in the zeros the expansion left at the end of the code segment: if the Repel is on and
/// was set by this item, <c>SetMushiyokeCount(item, 0)</c> turns it off; otherwise <c>(item, 1)</c> turns it on (over any
/// normal Repel). Then the message, as the Exp. Share does, from two empty lines of the bag's text, and back to the bag
/// without spending anything.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const int InfiniteRepelItem = InfiniteRepel.ItemId, RepelItem = 79, ExpShareItem = 216;

    public const string InfiniteRepelName = InfiniteRepel.Name;

    public const string InfiniteRepelDescription =
        @"Evita encuentros con Pokémon salvajes débiles\nsin agotarse nunca. Úsalo para activarlo\no desactivarlo.";

    public const string BagCro = "Bag.cro";

    /// <summary>The bag's messages in the game text, and the two empty lines that get the item's.</summary>
    public const int BagTextFile = 1, RepelOnLine = 59, RepelOffLine = 60;

    public const string RepelOnText = "Has activado el Repelente Infinito.", RepelOffText = "Has desactivado el Repelente Infinito.";

    private static readonly (int File, int Line, string Unused, string Wanted)[] InfiniteRepelLines =
    [
        (ItemFlavorFile, InfiniteRepelItem, "", InfiniteRepelDescription),
        (ItemNamesFile, InfiniteRepelItem, "(?)", InfiniteRepelName),
        (ItemPluralFile, InfiniteRepelItem, "(?)", "Repelentes Infinitos"),
        (ItemMessageFile, InfiniteRepelItem, "(?)[VAR 1101(00FE,0000)]", InfiniteRepelName + "[VAR 1101(00FE,0000)]"),
        (BagTextFile, RepelOnLine, $"[~ {RepelOnLine}]", RepelOnText),
        (BagTextFile, RepelOffLine, $"[~ {RepelOffLine}]", RepelOffText)
    ];

    /// <summary><c>Field::EventWork::DecMushiyokeCount</c> in <c>code.bin</c> (file offset), and what it held.</summary>
    public const int RepelCountSite = 0x2A8EB8;

    private static readonly uint[] RepelCountOriginal =
    [
        0xE2800C0A, 0xE1D015B8, 0xE3510000, 0x0A000005, 0xE2411001, 0xE1A01801,
        0xE1B01821, 0xE1C015B8, 0x03A00001, 0x0A000000, 0xE3A00000, 0xE12FFF1E
    ];

    private static int RepelIconEntry => ItemIconTable + 4 * InfiniteRepelItem;

    private static readonly int[] InfiniteRepelOffsets = [RepelCountSite, RepelIconEntry];

    /// <summary>
    /// The steps go down unless the item that set them is this one. r0 is the EventWork; returns 1 when the step just
    /// taken was the last, as before.
    /// </summary>
    public static byte[] RepelCount() => Words(
        0xE2800C0A,                         // add    r0, r0, #0xa00
        0xE1D023BE,                         // ldrh   r2, [r0, #0x3e]  ; the item that set the steps
        0xE3520000 | InfiniteRepelItem,     // cmp    r2, #114
        0x11D015B8,                         // ldrhne r1, [r0, #0x58]  ; the steps
        0x13510000,                         // cmpne  r1, #0
        0x03A00000,                         // moveq  r0, #0
        0x012FFF1E,                         // bxeq   lr
        0xE2511001,                         // subs   r1, r1, #1
        0xE1C015B8,                         // strh   r1, [r0, #0x58]
        0x03A00001,                         // moveq  r0, #1
        0x13A00000,                         // movne  r0, #0
        0xE12FFF1E);                        // bx     lr

    /// <summary>The code.bin records for the item, or null when this is not the code they were made for.</summary>
    public static IReadOnlyList<IpsRecord>? InfiniteRepelRecords(ReadOnlySpan<byte> code, int icon)
    {
        if (!Holds(code, RepelCountSite, RepelCountOriginal)
            || RepelIconEntry + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[RepelIconEntry..]) != BlankIcon)
        {
            return null;
        }

        return [new IpsRecord(RepelCountSite, RepelCount()), new IpsRecord(RepelIconEntry, Word((uint)icon))];
    }

    /// <summary>
    /// <c>Bag.cro</c> offsets (the file is loaded whole, so they are also distances between code): the lookup's first
    /// compare, the cave at the end of the code segment and the routines it calls.
    /// </summary>
    public const int BagLookupSite = 0x12B54, BagCave = 0x16C00;

    private const int SetRepelVeneer = 0x738, MessageDataVeneer = 0x3A8, StringBufferVeneer = 0x460, GetStringVeneer = 0x590;

    private const int CloseMessage = 0xA5E4, ShowString = 0xAE20;

    private const uint CmpR3R5 = 0xE1530005, VeneerJump = 0xE51FF004;

    /// <summary>
    /// What pins each of those: the lookup's frame and compare, the Repel routine's call to <c>SetMushiyokeCount</c>, the
    /// message routine's start and its Exp. Share case (line 0x4F), and the Exp. Share routine closing the message.
    /// </summary>
    private static readonly (int Offset, uint Word)[] BagContext =
    [
        (0x12B24, 0xE92D40F8), (0x12B50, 0xE6FF5070), (0x12B58, 0xE3A00000),
        (0x6CD8, 0xE594004C), (0x6CDC, 0xE1A01005), (0x6CE0, Bl(0x6CE0, SetRepelVeneer)),
        (0xA274, 0xE2800004), (0xA278, Bl(0xA278, MessageDataVeneer)), (0xA27C, 0xE1A0A000),
        (0xA280, 0xE3A01001), (0xA284, Bl(0xA284, StringBufferVeneer)),
        (0xA534, 0xE3A0204F), (0xA53C, Bl(0xA53C, GetStringVeneer)), (0xA550, B(0xA550, ShowString)),
        (0xF13C, Bl(0xF13C, CloseMessage)),
        (SetRepelVeneer, VeneerJump), (MessageDataVeneer, VeneerJump), (StringBufferVeneer, VeneerJump),
        (GetStringVeneer, VeneerJump)
    ];

    private const int BagCheck = BagCave, BagHandler = BagCave + 0x38, BagShow = BagHandler + 0x68, BagEgg = BagShow + 0x48;

    /// <summary>
    /// In place of the lookup's <c>cmp r3,r5</c> (r5 the item, r3 the table's first): this item leaves the lookup's frame
    /// and goes to its routine with (bag, item); any other gets the compare back.
    /// </summary>
    public static byte[] BagLookup() => Words(
        0xE3550000 | InfiniteRepelItem,     //  0 cmp   r5, #114
        0x0A000003,                         //  1 beq   repel
        0xE3550000 | EggTurboItem,          //  2 cmp   r5, #115
        0x0A000005,                         //  3 beq   incubator
        CmpR3R5,                            //  4 cmp   r3, r5
        0xE12FFF1E,                         //  5 bx    lr
        0xE1A00004,                         //  6 repel: cpy r0, r4
        0xE1A01005,                         //  7 cpy   r1, r5
        0xE8BD40F8,                         //  8 ldmia sp!, {r3-r7, lr} ; the lookup's frame
        B(BagCheck + 9 * 4, BagHandler),    //  9 b     handler
        0xE1A00004,                         // 10 incubator: cpy r0, r4
        0xE1A01005,                         // 11 cpy   r1, r5
        0xE8BD40F8,                         // 12 ldmia sp!, {r3-r7, lr}
        B(BagCheck + 13 * 4, BagEgg));      // 13 b     incubator's routine

    /// <summary>The routine: on or off, the message, back to the bag (state 2) with nothing spent.</summary>
    public static byte[] BagHandlerCode() => Words(
        0xE92D4070,                         // stmdb sp!, {r4-r6, lr}
        0xE1A04000,                         // cpy   r4, r0           ; the bag
        0xE1A05001,                         // cpy   r5, r1           ; the item
        0xE594004C,                         // ldr   r0, [r4, #0x4c]  ; EventWork
        0xE2803C0A,                         // add   r3, r0, #0xa00
        0xE1D325B8,                         // ldrh  r2, [r3, #0x58]  ; steps left
        0xE1D333BE,                         // ldrh  r3, [r3, #0x3e]  ; the item that set them
        0xE0536005,                         // subs  r6, r3, r5      ; 0 when it is ours
        0x13A06001,                         // movne r6, #1          ; not ours: on
        0xE3520000,                         // cmp   r2, #0
        0x03A06001,                         // moveq r6, #1          ; no steps left: on (only ours with steps goes off)
        0xE1A02006,                         // cpy   r2, r6
        0xE1A01005,                         // cpy   r1, r5
        Bl(BagHandler + 0x34, SetRepelVeneer), // bl EventWork::SetMushiyokeCount
        0xE5940070,                         // ldr   r0, [r4, #0x70]  ; the message window
        0xE3560000,                         // cmp   r6, #0
        0x03A01000 | RepelOffLine,          // moveq r1, #60
        0x13A01000 | RepelOnLine,           // movne r1, #59
        Bl(BagHandler + 0x48, BagShow),     // bl    show
        0xE5940070,                         // ldr   r0, [r4, #0x70]
        0xE3A01000,                         // mov   r1, #0
        Bl(BagHandler + 0x54, CloseMessage),// bl    0xa5e4
        0xE3A00002,                         // mov   r0, #2
        0xE5840004,                         // str   r0, [r4, #4]
        0xE3A00000,                         // mov   r0, #0
        0xE8BD8070);                        // ldmia sp!, {r4-r6, pc}

    /// <summary>Shows line r1 of the bag's text in window r0: the message routine's start plus its Exp. Share case.</summary>
    public static byte[] BagShowCode() => Words(
        0xE92D4E30,                         // stmdb sp!, {r4, r5, r9-r11, lr}
        0xE1A04000,                         // cpy   r4, r0
        0xE1A05001,                         // cpy   r5, r1
        0xE2800004,                         // add   r0, r0, #4
        Bl(BagShow + 0x10, MessageDataVeneer),
        0xE1A0A000,                         // cpy   r10, r0          ; the text
        0xE3A01001,                         // mov   r1, #1
        Bl(BagShow + 0x1C, StringBufferVeneer),
        0xE1A09000,                         // cpy   r9, r0           ; a string buffer
        0xE1A01000,                         // cpy   r1, r0
        0xE1A02005,                         // cpy   r2, r5
        0xE1A0000A,                         // cpy   r0, r10
        Bl(BagShow + 0x30, GetStringVeneer),
        0xE1A01009,                         // cpy   r1, r9
        0xE1A00004,                         // cpy   r0, r4
        0xE3A02001,                         // mov   r2, #1
        0xE8BD4E30,                         // ldmia sp!, {r4, r5, r9-r11, lr}
        B(BagShow + 0x44, ShowString));     // b     0xae20

    /// <summary>The Incubadora Turbo: flips the rule block's byte, says which way, back to the bag (state 2) with nothing spent.</summary>
    public static byte[] BagEggCode() => Words(
        0xE92D4070,                         //  0 stmdb sp!, {r4-r6, lr}
        0xE1A04000,                         //  1 cpy   r4, r0           ; the bag
        0xE59F2038,                         //  2 ldr   r2, [byte]
        0xE5D23000,                         //  3 ldrb  r3, [r2]
        0xE2236001,                         //  4 eor   r6, r3, #1      ; the new state
        0xE5C26000,                         //  5 strb  r6, [r2]
        0xE5940070,                         //  6 ldr   r0, [r4, #0x70] ; the message window
        0xE3560000,                         //  7 cmp   r6, #0
        0x03A01000 | EggTurboOffLine,       //  8 moveq r1, #92
        0x13A01000 | EggTurboOnLine,        //  9 movne r1, #91
        Bl(BagEgg + 10 * 4, BagShow),       // 10 bl    show
        0xE5940070,                         // 11 ldr   r0, [r4, #0x70]
        0xE3A01000,                         // 12 mov   r1, #0
        Bl(BagEgg + 13 * 4, CloseMessage),  // 13 bl    0xa5e4
        0xE3A00002,                         // 14 mov   r0, #2
        0xE5840004,                         // 15 str   r0, [r4, #4]
        0xE3A00000,                         // 16 mov   r0, #0
        0xE8BD8070,                         // 17 ldmia sp!, {r4-r6, pc}
        RuleBlock.EggTurbo);                // 18 the byte's address

    private static byte[] BagCaveCode() => [.. BagLookup(), .. BagHandlerCode(), .. BagShowCode(), .. BagEggCode()];

    /// <summary>Puts the item into <c>Bag.cro</c>, in place.</summary>
    public static BattleState PatchBag(Span<byte> cro)
    {
        var cave = BagCaveCode();
        var hook = Word(Bl(BagLookupSite, BagCheck));

        // Con el gancho ya puesto la cueva es nuestra, de esta versión o de una anterior: se deja como debe estar.
        if (BagCave + cave.Length <= cro.Length && Matches(cro, BagLookupSite, BinaryPrimitives.ReadUInt32LittleEndian(hook), BagContext))
        {
            if (cro.Slice(BagCave, cave.Length).SequenceEqual(cave)) return BattleState.AlreadyPatched;

            cave.CopyTo(cro[BagCave..]);
            return BattleState.Patched;
        }

        if (!Matches(cro, BagLookupSite, CmpR3R5, BagContext) || !Empty(cro, BagCave, cave.Length)) return BattleState.Unknown;

        cave.CopyTo(cro[BagCave..]);
        hook.CopyTo(cro[BagLookupSite..]);
        return BattleState.Patched;
    }

    /// <summary>
    /// Puts the item into the installed mod's files: its data, its text, its icon. Returns the icon's index (for the code
    /// records), or null with what went wrong in <paramref name="problem"/>.
    /// </summary>
    public static int? InstallInfiniteRepel(string romfs, out string? problem)
    {
        problem = null;
        var itemData = Path.Combine(romfs, "a", "0", "1", "9");
        var text = Path.Combine(romfs, "a", "0", "3", "6");
        var icons = Path.Combine(romfs, "a", "0", "6", "1");

        if (!File.Exists(itemData) || !File.Exists(text) || !File.Exists(icons))
        {
            problem = "faltan ficheros del mod: sin Repelente Infinito";
            return null;
        }

        problem = ItemData(itemData, InfiniteRepelItem, ExpShareItem, 0, InfiniteRepelName)
                  ?? ItemText(text, InfiniteRepelLines, InfiniteRepelName);
        if (problem is not null) return null;

        return AppendIcon(icons, Sprites.InfiniteRepelIcon.Bflim, RepelItem, InfiniteRepelName, out problem);
    }
}
