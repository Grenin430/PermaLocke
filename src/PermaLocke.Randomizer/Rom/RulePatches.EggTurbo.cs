using System.Buffers.Binary;

using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The Incubadora Turbo (2026-10-07): a key item that, used from the bag, makes every egg of the party hatch after two steps,
/// and used again, stops. Only PermaLocke hands it out (MISCELÁNEA).
/// </summary>
/// <remarks>
/// <para>
/// It is item <see cref="EggTurboItem"/>, the slot after the Repelente Infinito's, made the same way (a copy of the Exp.
/// Share's data, its text and an icon), and the bag runs it through the same hook (<c>Bag.cro</c>): its own routine flips a byte
/// of the rule block (<see cref="RuleBlock.EggTurbo"/>) and says which way with two lines of the bag's text.
/// </para>
/// <para>
/// The eggs, found in <c>FieldRo.cro</c> with Ghidra (function at 0x25354, called once per step) and the names of
/// ZiouraS2/usum-re: every step adds <c>Rotom::CalcHatch(364)</c> to a counter (<c>Situation::GetEggStepCount</c>), and when
/// it passes 0x10000 every egg of the party loses one hatch cycle (<c>CoreParam::SubOriginalFamiliarity(egg, 1)</c>, or 2 with
/// Flame Body or Magma Armor in the party) and the counter starts again; an egg with no cycles left hatches. Two changes,
/// both in <c>code.bin</c>, because the field module is not part of the mod:
/// </para>
/// <list type="bullet">
/// <item><c>CalcHatch</c> (0x27DF30, the Rotom Power's 1.5×) is rewritten in a shorter form, so that with the byte on it returns
/// 0x8001: two steps cross 0x10000 and the tick comes.</item>
/// <item><c>SubOriginalFamiliarity</c> (0x22244C), which only the field module calls, gets its <c>cpy r5,r1</c> (the amount) replaced by
/// a call to a stub that does the same and, with the byte on, makes the amount 255: it floors at zero, so the egg has no cycles
/// left and hatches on that very step. The stub lives in the room the shorter <c>CalcHatch</c> leaves.</item>
/// </list>
/// <para>
/// With the byte off, nothing changes: the stub copies the amount and <c>CalcHatch</c> gives the cartridge's numbers. The byte is
/// in RAM and not in the save, so the game starts with the incubator off.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const int EggTurboItem = EggTurbo.ItemId;

    public const string EggTurboName = EggTurbo.Name;

    public const string EggTurboDescription =
        @"Los huevos de tu equipo eclosionan\nal dar dos pasos. Úsala para activarla\no desactivarla.";

    /// <summary>Two more empty lines of the bag's text (the Repel took 59 and 60), 91 and 92.</summary>
    public const int EggTurboOnLine = 91, EggTurboOffLine = 92;

    public const string EggTurboOnText = "Has activado la Incubadora Turbo.", EggTurboOffText = "Has desactivado la Incubadora Turbo.";

    private static readonly (int File, int Line, string Unused, string Wanted)[] EggTurboLines =
    [
        (ItemFlavorFile, EggTurboItem, "", EggTurboDescription),
        (ItemNamesFile, EggTurboItem, "(?)", EggTurboName),
        (ItemPluralFile, EggTurboItem, "(?)", "Incubadoras Turbo"),
        (ItemMessageFile, EggTurboItem, "(?)[VAR 1101(00FE,0000)]", EggTurboName + "[VAR 1101(00FE,0000)]"),
        (BagTextFile, EggTurboOnLine, $"[~ {EggTurboOnLine}]", EggTurboOnText),
        (BagTextFile, EggTurboOffLine, $"[~ {EggTurboOffLine}]", EggTurboOffText)
    ];

    /// <summary><c>Rotom::CalcHatch</c> and <c>CoreParam::SubOriginalFamiliarity</c> in <c>code.bin</c> (file offsets).</summary>
    public const int HatchSite = 0x27DF30, FamiliaritySite = 0x22244C;

    /// <summary>The instruction of <c>SubOriginalFamiliarity</c> that becomes the call: <c>cpy r5,r1</c>.</summary>
    public const int FamiliarityAmountSite = FamiliaritySite + 0xC;

    private const int HatchWords = 26, HatchStub = HatchSite + 11 * 4, HatchFlag = HatchSite + 25 * 4;

    /// <summary>What <c>CalcHatch</c> and its three constants hold in the cartridge's code: 26 words.</summary>
    private static readonly uint[] HatchOriginal =
    [
        0xE92D4070, 0xE1A04001, 0xE5D01028, 0xE1A00004, 0xE3510001, 0x1A00000F, 0xE59F503C, 0xE1540005, 0x3A000004,
        0xE3A03000, 0xE1A02003, 0xE1A01003, 0xE1A00003, 0xE1A00000, 0xE1540005, 0x259F001C, 0x2A000004, 0xE3A01096,
        0xE59F0014, 0xE0010194, 0xE0801190, 0xE1A002A0, 0xE8BD8070, 0x01B4E81B, 0x028F5C28, 0x51EB851F
    ];

    /// <summary>What pins <c>SubOriginalFamiliarity</c>: its frame, the copy of the amount and the call after it.</summary>
    private static readonly (int Offset, uint Word)[] FamiliarityContext =
    [
        (FamiliaritySite, 0xE92D4070), (FamiliaritySite + 4, 0xE1A04000), (FamiliaritySite + 8, 0xE590000C),
        (FamiliarityAmountSite, 0xE1A05001), (FamiliarityAmountSite + 4, 0xEB062D75)
    ];

    private static int EggTurboIconEntry => ItemIconTable + 4 * EggTurboItem;

    private static readonly int[] EggTurboOffsets = [HatchSite, FamiliarityAmountSite, EggTurboIconEntry];

    /// <summary>
    /// The rewritten <c>CalcHatch</c> followed by the stub and the byte's address. <c>CalcHatch(this, n)</c>: with the byte on,
    /// 0x8001; otherwise <c>n</c>, or 1.5 <c>n</c> when the Rotom Power says so (the cartridge's own numbers, without the
    /// guard for a <c>n</c> in the hundreds of millions).
    /// </summary>
    public static byte[] EggTurboHatch()
    {
        // ldr r2,[pc,#x] / ldr r12,[pc,#x]: the literal is the last word of the 26.
        uint Literal(uint register, int at) => 0xE59F0000 | (register << 12) | (uint)(HatchFlag - (at + 8));

        var words = new uint[HatchWords];
        var code = new uint[]
        {
            Literal(2, HatchSite),              //  0 ldr    r2, [flag]
            0xE5D22000,                         //  1 ldrb   r2, [r2]
            0xE3520000,                         //  2 cmp    r2, #0
            0x13A00902,                         //  3 movne  r0, #0x8000
            0x12800001,                         //  4 addne  r0, r0, #1          ; 0x8001
            0x112FFF1E,                         //  5 bxne   lr
            0xE5D02028,                         //  6 ldrb   r2, [r0, #0x28]     ; the Rotom Power's hatch state
            0xE1A00001,                         //  7 cpy    r0, r1
            0xE3520001,                         //  8 cmp    r2, #1
            0x008100A1,                         //  9 addeq  r0, r1, r1, lsr #1  ; 1.5 n
            0xE12FFF1E,                         // 10 bx     lr
            0xE1A05001,                         // 11 stub: cpy   r5, r1         ; what SubOriginalFamiliarity did
            Literal(12, HatchStub + 4),         // 12       ldr   r12, [flag]
            0xE5DCC000,                         // 13       ldrb  r12, [r12]
            0xE35C0000,                         // 14       cmp   r12, #0
            0x13A050FF,                         // 15       movne r5, #255       ; every cycle at once
            0xE12FFF1E                          // 16       bx    lr
        };

        code.CopyTo(words, 0);
        words[HatchWords - 1] = RuleBlock.EggTurbo;
        return Words(words);
    }

    /// <summary>The code.bin records for the item, or null when this is not the code they were made for.</summary>
    public static IReadOnlyList<IpsRecord>? EggTurboRecords(ReadOnlySpan<byte> code, int icon)
    {
        for (var i = 0; i < HatchWords; i++)
        {
            if (!Holds(code, HatchSite + i * 4, [HatchOriginal[i]])) return null;
        }

        foreach (var (offset, word) in FamiliarityContext)
        {
            if (offset + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[offset..]) != word) return null;
        }

        if (EggTurboIconEntry + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[EggTurboIconEntry..]) != BlankIcon)
        {
            return null;
        }

        return
        [
            new IpsRecord(HatchSite, EggTurboHatch()),
            new IpsRecord(FamiliarityAmountSite, Word(Bl(FamiliarityAmountSite, HatchStub))),
            new IpsRecord(EggTurboIconEntry, Word((uint)icon))
        ];
    }

    /// <summary>
    /// Puts the item into the installed mod's files: its data, its text, its icon. Returns the icon's index (for the code
    /// records), or null with what went wrong in <paramref name="problem"/>.
    /// </summary>
    public static int? InstallEggTurbo(string romfs, out string? problem)
    {
        problem = null;
        var itemData = Path.Combine(romfs, "a", "0", "1", "9");
        var text = Path.Combine(romfs, "a", "0", "3", "6");
        var icons = Path.Combine(romfs, "a", "0", "6", "1");

        if (!File.Exists(itemData) || !File.Exists(text) || !File.Exists(icons))
        {
            problem = "faltan ficheros del mod: sin Incubadora Turbo";
            return null;
        }

        problem = ItemData(itemData, EggTurboItem, ExpShareItem, 0, EggTurboName)
                  ?? ItemText(text, EggTurboLines, EggTurboName);
        if (problem is not null) return null;

        return AppendIcon(icons, Sprites.EggTurboIcon.Bflim, RepelItem, EggTurboName, out problem);
    }
}
