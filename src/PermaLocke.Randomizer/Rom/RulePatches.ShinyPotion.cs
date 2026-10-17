using System.Buffers.Binary;

using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The Poción Variocolor (2026-10-17): used from the bag it is spent, and the next Pokémon the game makes at random (a wild one, nearly always)
/// comes out shiny. The ground of a new world hides a few (<c>FieldItemRandomizer</c>); MISCELÁNEA hands them out for testing.
/// </summary>
/// <remarks>
/// <para>
/// It is item <see cref="ShinyPotionItem"/>, another of the expansion's unused slots (121), a copy of the Repel's data (the items pocket, used
/// in the field, spent) with a sort place of its own, its text and an icon. The bag runs it through the same hook (<c>Bag.cro</c>) to its own
/// routine: with no potion waiting it sets <see cref="RuleBlock.ShinyPotion"/> and goes on with the Repel's own tail, which says the item was
/// used and spends it (state 4, the item at <c>[bag+0x170]</c>); with one waiting it says so and spends nothing.
/// </para>
/// <para>
/// The shiny, found in <c>code.bin</c> from the Shiny Charm (632): the Pokémon maker reads a PID mode in its spec (<c>+0x8</c>):
/// <c>…01</c> never shiny (<c>PokeTool</c>'s MakeNotRare, 0x2235B0), <c>…02</c> always (MakeRare, 0x22354C), <c>…03</c> at random with
/// <c>[+0x23]</c> rolls, which the Charm raises. Wild Pokémon take the random one. Its first read of the rolls (0x2205E4,
/// <c>ldrb r0,[r4,#0x23]</c>) becomes a call that, with the byte set, puts it back to 0 and jumps to the «always» branch (0x220644). The
/// routine lives in two empty asserts of MakeRare itself (0x223594 and 0x2235F0), whose guards now always jump past them: they only ran
/// when a shiny PID had failed to come out shiny. The maker does not spend the potion: PermaLocke does, when it reads the wild Pokémon of a
/// battle (the game makes another Pokémon at random just before, which would take it). With PermaLocke closed it lasts until it is open
/// for a wild battle, and everything the game makes at random meanwhile comes out shiny.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const int ShinyPotionItem = ShinyPotion.ItemId;

    public const string ShinyPotionName = ShinyPotion.Name;

    public const string ShinyPotionDescription =
        @"Brebaje que hace que el siguiente Pokémon\nsalvaje que aparezca sea variocolor.";

    /// <summary>Another empty line of the bag's text: a potion already waiting.</summary>
    public const int ShinyPotionWaitingLine = 151;

    public const string ShinyPotionWaitingText = @"Ya hay una Poción Variocolor haciendo\nefecto: espera al próximo Pokémon salvaje.";

    private static readonly (int File, int Line, string Unused, string Wanted)[] ShinyPotionLines =
    [
        (ItemFlavorFile, ShinyPotionItem, "", ShinyPotionDescription),
        (ItemNamesFile, ShinyPotionItem, "(?)", ShinyPotionName),
        (ItemPluralFile, ShinyPotionItem, "(?)", "Pociones Variocolor"),
        (ItemMessageFile, ShinyPotionItem, "(?)[VAR 1101(00FE,0000)]", ShinyPotionName + "[VAR 1101(00FE,0000)]"),
        (BagTextFile, ShinyPotionWaitingLine, $"[~ {ShinyPotionWaitingLine}]", ShinyPotionWaitingText)
    ];

    /// <summary>File offsets in <c>code.bin</c>: the first read of the rolls, the «always shiny» branch and the two asserts with their guards.</summary>
    public const int ShinyRollsSite = 0x2205E4, ShinyAlwaysBranch = 0x220644, ShinyCaveA = 0x223594, ShinyCaveB = 0x2235F0;

    private const int ShinyGuardA = ShinyCaveA - 4, ShinyGuardB = ShinyCaveB - 4, MakeRare = 0x22354C;

    private const uint LdrbRolls = 0xE5D40023;

    private static int ShinyPotionIconEntry => ItemIconTable + 4 * ShinyPotionItem;

    private static readonly int[] ShinyPotionOffsets = [ShinyRollsSite, ShinyGuardA, ShinyCaveA, ShinyGuardB, ShinyCaveB, ShinyPotionIconEntry];

    /// <summary>What pins the maker: the words round the read, the «always» branch's call to MakeRare and the guards before the asserts.</summary>
    private static readonly (int Offset, uint Word)[] ShinyContext =
    [
        (ShinyRollsSite + 4, 0xE3A05000), (ShinyRollsSite + 8, 0xE3500000),
        (ShinyAlwaysBranch, 0xE320F000), (ShinyAlwaysBranch + 0x14, Bl(ShinyAlwaysBranch + 0x14, MakeRare)),
        (ShinyGuardA, B(ShinyGuardA, ShinyCaveA + 0x14, 3)), (ShinyGuardB, B(ShinyGuardB, ShinyCaveB + 0x14, 2))
    ];

    /// <summary>First half: the byte (r12 keeps its address for the second).</summary>
    public static byte[] ShinyCaveACode() => Words(
        0xE59FC008,                         // 0 ldr    r12, [byte]
        0xE5DC0000,                         // 1 ldrb   r0, [r12]
        0xE3500000,                         // 2 cmp    r0, #0
        B(ShinyCaveA + 12, ShinyCaveB),     // 3 b      second half
        RuleBlock.ShinyPotion);             // 4 the byte's address

    /// <summary>
    /// Second half: no potion, the rolls as before; a potion, the «always shiny» branch. The byte stays set: the game makes another Pokémon at
    /// random just before a wild one (2026-10-17, measured: the byte went to 0 and the wild one was not shiny), so PermaLocke puts it back to 0
    /// when it reads the wild Pokémon of the battle (<c>EncounterGuard</c>).
    /// </summary>
    public static byte[] ShinyCaveBCode() => Words(
        0x05D40023,                         // 0 ldrbeq r0, [r4, #0x23] ; what the maker read
        0x012FFF1E,                         // 1 bxeq   lr
        B(ShinyCaveB + 8, ShinyAlwaysBranch), // 2 b    always shiny
        0xE320F000,                         // 3 nop
        0xE320F000);                        // 4 nop

    /// <summary>The code.bin records for the item, or null when this is not the code they were made for.</summary>
    public static IReadOnlyList<IpsRecord>? ShinyPotionRecords(ReadOnlySpan<byte> code, int icon)
    {
        if (!Matches(code, ShinyRollsSite, LdrbRolls, ShinyContext)) return null;
        if (!Holds(code, ShinyCaveA, EmptyAssert[..5]) || !Holds(code, ShinyCaveB, EmptyAssert[..5])) return null;
        if (ShinyPotionIconEntry + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[ShinyPotionIconEntry..]) != BlankIcon) return null;

        return
        [
            new IpsRecord(ShinyRollsSite, Word(Bl(ShinyRollsSite, ShinyCaveA))),
            new IpsRecord(ShinyGuardA, Word(B(ShinyGuardA, ShinyCaveA + 0x14))),
            new IpsRecord(ShinyCaveA, ShinyCaveACode()),
            new IpsRecord(ShinyGuardB, Word(B(ShinyGuardB, ShinyCaveB + 0x14))),
            new IpsRecord(ShinyCaveB, ShinyCaveBCode()),
            new IpsRecord(ShinyPotionIconEntry, Word((uint)icon))
        ];
    }

    /// <summary>The Repel's routine in <c>Bag.cro</c>: where it says the item was used and spends it, and where it says it cannot be used.</summary>
    private const int RepelUsedTail = 0x6CF8, RepelRefusedTail = 0x6D34;

    /// <summary>
    /// The potion in the bag, with the Repel routine's frame (its tails pop it): none waiting, the byte set and on to the Repel's «used» tail
    /// (r4 the bag, r5 the item, r6 0); one waiting, the message and on to its «cannot» tail (back to the bag, nothing spent).
    /// </summary>
    public static byte[] BagShinyCode() => Words(
        0xE92D40F8,                         //  0 stmdb  sp!, {r3-r7, lr}
        0xE1A04000,                         //  1 cpy    r4, r0           ; the bag
        0xE1A05001,                         //  2 cpy    r5, r1           ; the item
        0xE3A06000,                         //  3 mov    r6, #0
        0xE59F2020,                         //  4 ldr    r2, [byte]
        0xE5D23000,                         //  5 ldrb   r3, [r2]
        0xE3530000,                         //  6 cmp    r3, #0
        0x03A03001,                         //  7 moveq  r3, #1
        0x05C23000,                         //  8 strbeq r3, [r2]         ; the next wild one, shiny
        B(BagShiny + 9 * 4, RepelUsedTail, 0), //  9 beq the Repel's «used»
        0xE5940070,                         // 10 ldr    r0, [r4, #0x70]  ; the message window
        0xE3A01000 | ShinyPotionWaitingLine,// 11 mov    r1, #151
        Bl(BagShiny + 12 * 4, BagShow),     // 12 bl     show
        B(BagShiny + 13 * 4, RepelRefusedTail), // 13 b  the Repel's «cannot»
        RuleBlock.ShinyPotion);             // 14 the byte's address

    /// <summary>
    /// Puts the item into the installed mod's files: its data, its text, its pocket, its icon. Returns the icon's index (for the code records),
    /// or null with what went wrong in <paramref name="problem"/>.
    /// </summary>
    public static int? InstallShinyPotion(string romfs, out string? problem)
    {
        problem = null;
        var itemData = Path.Combine(romfs, "a", "0", "1", "9");
        var text = Path.Combine(romfs, "a", "0", "3", "6");
        var icons = Path.Combine(romfs, "a", "0", "6", "1");
        var pockets = Path.Combine(romfs, "a", "1", "0", "3");

        if (!File.Exists(itemData) || !File.Exists(text) || !File.Exists(icons) || !File.Exists(pockets))
        {
            problem = "faltan ficheros del mod: sin Poción Variocolor";
            return null;
        }

        problem = ItemData(itemData, ShinyPotionItem, RepelItem, 0, ShinyPotionName, ownSort: true)
                  ?? ItemText(text, ShinyPotionLines, ShinyPotionName)
                  ?? ItemPocket(pockets, ShinyPotionItem, RepelItem, ShinyPotionName);
        if (problem is not null) return null;

        return AppendIcon(icons, Sprites.ShinyPotionIcon.Bflim, RepelItem, ShinyPotionName, out problem);
    }
}
