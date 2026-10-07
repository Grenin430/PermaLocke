using System.Buffers.Binary;
using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The level cap inside the game (2026-10-06): no experience at the cap in battle, and no Rare Candy at the cap.
/// </summary>
/// <remarks>
/// <para>
/// Both places compare the level with 100, the cartridge's maximum; each <c>cmp r0,#0x64</c> becomes a call to one small
/// routine that compares with the cap PermaLocke writes in <see cref="RuleBlock"/> instead (0 or more than 100 = 100, so
/// the game is the cartridge's without PermaLocke). Found with Ghidra on the Expansion mod's files (Ultra Moon, USA 1.0
/// base), with the names from ZiouraS2/usum-re's import tables:
/// </para>
/// <list type="bullet">
/// <item><b>Battle.cro 0x91F1C</b>, in the battle Pokémon's «add experience» (it calls <c>CoreParam::SetExp</c> and
/// <c>GetLevel</c>): <c>ldrb r0,[r0,#0x18]; mov r9,#0; cmp r0,#0x64; bcs</c> skips everything at level 100, so at the cap
/// no experience and no level up. The routine sits in the zeros at the end of the code segment (0x11A700), which no
/// relocation and no instruction points to.</item>
/// <item><b>code.bin 0x4410E8</b> (file offset 0x3410E8), in the item check that
/// <c>PokeTool::ITEM_RCV_RecoverCheck</c> calls: for an item with the «level up» parameter (0x1E), <c>bl GetLevel;
/// cmp r0,#0x64; nop; bcc usable</c>. At the cap the candy then has «no effect» and is not spent. The routine sits in the
/// zeros at 0x5B9A80, past the cartridge's code end (0x5B99F8, left alone: a range descriptor in the data points to it)
/// and before the mod's own code at 0x5B9B40.</item>
/// </list>
/// <para>
/// Every site is changed only when its exact original bytes and an empty cave are there, and changing back restores
/// them. <c>code.bin</c> is never touched: its changes go in <c>exefs/code.ips</c>, which Azahar applies at boot. The
/// <c>.cro</c> has no such layer, so the installed mod's copy is patched in place.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const string BattleCro = "Battle.cro";

    /// <summary>File offsets in <c>code.bin</c> (address = offset + 0x100000).</summary>
    public const int CodeCave = 0x4B9A80, CandySite = 0x3410E8;

    /// <summary>
    /// The fallen stay down (2026-10-06): the routine right after the cap's in the same cave, and the site in the function
    /// every HP write of a party Pokémon goes through (0x322638, reached from <c>CoreParam::SetHp</c>: found with the fork's
    /// write log while the organiser healed a fallen one at a Pokémon Center, the write of its 55 HP came from there).
    /// </summary>
    public const int FallenCave = CodeCave + 36, FallenSite = 0x222644;

    /// <summary>
    /// The duplicates clause (2026-10-06). The site is in the wild slot pick (0x3A6FCC, called by
    /// <c>Encount::PokeSet::SetNormalEncountData</c>): the slot index has just been rolled in r0 and
    /// <c>add r6,r4,r0,lsl #2</c> takes its entry from the table in r4. The reroll goes in the zeros right after the
    /// cartridge's code end (0x5B9A00, eight bytes past the range descriptor's end) and its helper after the fallen routine.
    /// </summary>
    public const int DupeCave = 0x4B9A00, DupeTestCave = FallenCave + 68, DupeSite = 0x2A7064;

    /// <summary>The game's <c>rand(n)</c>, 0 to n-1, the one the same function uses for levels.</summary>
    public const int Rand = 0x2FBF68;

    private const uint AddR6R4R0 = 0xE0846100;

    /// <summary><c>blx r1</c> (the slot roll) before, <c>str r0,[r5,#0x4ec]</c> after.</summary>
    private static readonly (int Offset, uint Word)[] DupeContext =
        [(DupeSite - 4, 0xE12FFF31), (DupeSite + 4, 0xE58504EC), (Rand, 0xE92D4070), (Rand + 4, 0xE1A04000)];

    /// <summary>Everything <see cref="CodeRecords"/> writes, to take it out again.</summary>
    private static readonly int[] CodeOffsets = [CodeCave, CandySite, FallenCave, FallenSite, DupeCave, DupeTestCave, DupeSite];

    /// <summary>
    /// For slot r10 of the table in r4: Z set when its species is not a duplicate, r3 its rate (the table's header holds
    /// the ten rates at bytes 2-11, the slots start at 0xC). Uses r1, r2, r12.
    /// </summary>
    public static byte[] DupeTest() => Words(
        0xE084110A, // add    r1, r4, r10, lsl #2
        0xE1D110B0, // ldrh   r1, [r1]               ; species | form << 11
        0xE1A01A81, // mov    r1, r1, lsl #21
        0xE1A01AA1, // mov    r1, r1, lsr #21        ; species
        0xE59FC020, // ldr    r12, [pc, #32]         ; RuleBlock.Dupes
        0xE3510D11, // cmp    r1, #0x440
        0x23A02000, // movhs  r2, #0                 ; past the bitset: never a duplicate
        0x37DC21A1, // ldrblo r2, [r12, r1, lsr #3]
        0x32011007, // andlo  r1, r1, #7
        0x31A02132, // movlo  r2, r2, lsr r1
        0xE244300A, // sub    r3, r4, #10
        0xE7D3300A, // ldrb   r3, [r3, r10]          ; its rate
        0xE2122001, // ands   r2, r2, #1
        0xE12FFF1E, // bx     lr
        RuleBlock.Dupes);

    /// <summary>
    /// <c>add r6,r4,r0,lsl #2</c>, except that a duplicate among the ten normal slots is rolled again among the slots
    /// that are not, with their own rates; when all of them are, the roll stands. r7 and r10 are free here (the function
    /// saved them and sets them before using them), r1-r3 and r12 are scratch.
    /// </summary>
    public static byte[] RerollDupes()
    {
        uint At(int index) => (uint)(DupeCave + index * 4);
        return Words(
            0xE92D4001,                                    //  0       push  {r0, lr}
            0xE350000A,                                    //  1       cmp   r0, #10
            0x2A000016,                                    //  2       bhs   done
            0xE1A06000,                                    //  3       mov   r6, r0
            0xE1A0A000,                                    //  4       mov   r10, r0
            Bl((int)At(5), DupeTestCave),                  //  5       bl    test
            0x0A000012,                                    //  6       beq   done          ; not a duplicate
            0xE3A07000,                                    //  7       mov   r7, #0        ; rates of the others
            0xE3A0A000,                                    //  8       mov   r10, #0
            Bl((int)At(9), DupeTestCave),                  //  9 sum:  bl    test
            0x00877003,                                    // 10       addeq r7, r7, r3
            0xE28AA001,                                    // 11       add   r10, r10, #1
            0xE35A000A,                                    // 12       cmp   r10, #10
            0x3AFFFFFA,                                    // 13       blo   sum
            0xE1B00007,                                    // 14       movs  r0, r7
            0x01A00006,                                    // 15       moveq r0, r6        ; all duplicates: it stands
            0x0A000008,                                    // 16       beq   done
            Bl((int)At(17), Rand),                         // 17       bl    rand          ; 0 .. total - 1
            0xE1A07000,                                    // 18       mov   r7, r0
            0xE3E0A000,                                    // 19       mvn   r10, #0
            0xE28AA001,                                    // 20 pick: add   r10, r10, #1
            Bl((int)At(21), DupeTestCave),                 // 21       bl    test
            0x1AFFFFFC,                                    // 22       bne   pick
            0xE0577003,                                    // 23       subs  r7, r7, r3
            0x2AFFFFFA,                                    // 24       bhs   pick
            0xE1A0000A,                                    // 25       mov   r0, r10
            AddR6R4R0,                                     // 26 done: add   r6, r4, r0, lsl #2
            0xE8BD8002);                                   // 27       pop   {r1, pc}
    }

    /// <summary>File offsets in <c>Battle.cro</c> (its code segment starts at 0x180 and is loaded as a whole).</summary>
    public const int BattleCave = 0x11A700, ExpSite = 0x91F1C;

    private const uint CmpR0Hundred = 0xE3500064;

    /// <summary>What surrounds each site in the original, checked before and after it.</summary>
    private static readonly (int Offset, uint Word)[] CandyContext =
        [(CandySite - 4, 0xEBFB89F5), (CandySite + 4, 0xE320F000), (CandySite + 8, 0x3A00012B)];

    private static readonly (int Offset, uint Word)[] ExpContext =
        [(ExpSite - 8, 0xE5D00018), (ExpSite - 4, 0xE3A09000), (ExpSite + 4, 0x2A000099)];

    /// <summary><c>mov r5,r1</c> (the HP to store), between <c>ldr r0,[r0,#4]</c> and <c>cmp r0,#0</c>.</summary>
    private const uint MovR5R1 = 0xE1A05001;

    private static readonly (int Offset, uint Word)[] FallenContext =
        [(FallenSite - 8, 0xE1A04000), (FallenSite - 4, 0xE5900004), (FallenSite + 4, 0xE3500000)];

    /// <summary>
    /// <c>mov r5,r1</c>, except that a Pokémon whose encryption constant is in <see cref="RuleBlock.Fallen"/> gets 0 when
    /// more is asked. r4 is the Pokémon (its block pointer at +8, the constant in the block's first word, never
    /// encrypted); r0 is left alone (the caller tests it next), r1-r3 and r12 are scratch the function no longer needs.
    /// </summary>
    public static byte[] KeepFallenDown() => Words(
        0xE1A05001, //       mov   r5, r1          ; what was asked
        0xE3550000, //       cmp   r5, #0
        0x012FFF1E, //       bxeq  lr              ; zero stays zero
        0xE594C008, //       ldr   r12, [r4, #8]   ; the stored block
        0xE35C0000, //       cmp   r12, #0
        0x012FFF1E, //       bxeq  lr
        0xE59CC000, //       ldr   r12, [r12]      ; encryption constant
        0xE59F101C, //       ldr   r1, [pc, #28]   ; RuleBlock.Fallen
        0xE3A02006, //       mov   r2, #6
        0xE4913004, // loop: ldr   r3, [r1], #4
        0xE153000C, //       cmp   r3, r12
        0x03A05000, //       moveq r5, #0
        0x012FFF1E, //       bxeq  lr
        0xE2522001, //       subs  r2, r2, #1
        0x1AFFFFF9, //       bne   loop
        0xE12FFF1E, //       bx    lr
        RuleBlock.Fallen);

    private static byte[] Words(params uint[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (var i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        return bytes;
    }

    /// <summary>
    /// <c>cmp r0, cap</c>, with the cap read from the block: flags as the original <c>cmp r0,#0x64</c> would leave them,
    /// r0 untouched, only r12 used (scratch, and dead at both sites: one right after a call, one before anything sets it).
    /// </summary>
    public static byte[] CompareWithCap()
    {
        uint[] words =
        [
            0xE59FC018, // ldr   r12, [pc, #24]   ; RuleBlock.Cap
            0xE5DCC000, // ldrb  r12, [r12]
            0xE35C0000, // cmp   r12, #0
            0x03A0C064, // moveq r12, #100
            0xE35C0064, // cmp   r12, #100
            0x83A0C064, // movhi r12, #100
            0xE150000C, // cmp   r0, r12
            0xE12FFF1E, // bx    lr
            RuleBlock.Cap
        ];

        var bytes = new byte[words.Length * 4];
        for (var i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        return bytes;
    }

    /// <summary>An ARM <c>bl</c> at <paramref name="from"/> to <paramref name="to"/>, both in the same loaded image.</summary>
    public static uint Bl(int from, int to) => 0xEB000000 | (uint)(((to - (from + 8)) >> 2) & 0x00FFFFFF);

    /// <summary>The records that put the cap into <c>code.bin</c>, or null when this is not the code they were made for.</summary>
    public static IReadOnlyList<IpsRecord>? CodeRecords(ReadOnlySpan<byte> code)
    {
        var routine = CompareWithCap();
        var fallen = KeepFallenDown();
        var test = DupeTest();
        var reroll = RerollDupes();

        if (!Matches(code, CandySite, CmpR0Hundred, CandyContext) || !Matches(code, FallenSite, MovR5R1, FallenContext)
            || !Matches(code, DupeSite, AddR6R4R0, DupeContext)
            || !Empty(code, CodeCave, routine.Length + fallen.Length + test.Length) || !Empty(code, DupeCave, reroll.Length))
        {
            return null;
        }

        return
        [
            new IpsRecord(CodeCave, routine),
            new IpsRecord(CandySite, Word(Bl(CandySite, CodeCave))),
            new IpsRecord(FallenCave, fallen),
            new IpsRecord(FallenSite, Word(Bl(FallenSite, FallenCave))),
            new IpsRecord(DupeTestCave, test),
            new IpsRecord(DupeCave, reroll),
            new IpsRecord(DupeSite, Word(Bl(DupeSite, DupeCave)))
        ];
    }

    /// <summary>What <see cref="PatchBattle"/> found.</summary>
    public enum BattleState { Patched, AlreadyPatched, Unknown }

    /// <summary>
    /// Balls refused in a spent zone (2026-10-06). Battle.cro 0xB32CC is the battle menu's «can a ball be thrown» check:
    /// it rolls the game's own reasons into r4 (1 not focused, 2 PC full, 3 two Pokémon, 4 not in sight, 5-6 a trial,
    /// 7 the reserve, 8 a fused Necrozma) and joins at 0xB33D0, <c>ldr r0,[r5,#0xd8]</c>, before turning the reason into a
    /// message (0x63690). With no reason of its own, the routine takes the one PermaLocke leaves in
    /// <see cref="RuleBlock.BallRefusal"/>: the ball pocket answers with that message and no ball is thrown.
    /// </summary>
    public const int BallSite = 0xB33D0, BallCave = BattleCave + 36;

    private const uint LdrR0R5D8 = 0xE59500D8;

    /// <summary><c>movne r4,#4</c> before, <c>cpy r1,r4</c> after.</summary>
    private static readonly (int Offset, uint Word)[] BallContext =
        [(BallSite - 8, 0xE3560000), (BallSite - 4, 0x13A04004), (BallSite + 4, 0xE1A01004)];

    /// <summary><c>ldr r0,[r5,#0xd8]</c> after taking PermaLocke's reason when the game has none. r12 is dead there.</summary>
    public static byte[] RefuseBalls() => Words(
        0xE59FC010, // ldr   r12, [pc, #16]   ; RuleBlock.BallRefusal
        0xE5DCC000, // ldrb  r12, [r12]
        0xE3540000, // cmp   r4, #0
        0x01A0400C, // moveq r4, r12          ; 0 = no reason: the ball goes
        LdrR0R5D8,  // ldr   r0, [r5, #0xd8]
        0xE12FFF1E, // bx    lr
        RuleBlock.BallRefusal);

    private sealed record CroPatch(int Site, uint Original, int Cave, byte[] Routine, (int Offset, uint Word)[] Context)
    {
        public uint Call => Bl(Site, Cave);
        public bool IsOn(ReadOnlySpan<byte> cro) => Matches(cro, Site, Call, Context) && cro.Slice(Cave, Routine.Length).SequenceEqual(Routine);
        public bool IsOff(ReadOnlySpan<byte> cro) => Matches(cro, Site, Original, Context) && Empty(cro, Cave, Routine.Length);
    }

    private static CroPatch[] BattlePatches() =>
    [
        new(ExpSite, CmpR0Hundred, BattleCave, CompareWithCap(), ExpContext),
        new(BallSite, LdrR0R5D8, BallCave, RefuseBalls(), BallContext)
    ];

    /// <summary>Puts the cap and the ball refusal into <c>Battle.cro</c> in place; one already there is left as it is.</summary>
    public static BattleState PatchBattle(Span<byte> cro)
    {
        var patches = BattlePatches();
        var (on, off) = States(cro, patches);

        if (on.All(x => x)) return BattleState.AlreadyPatched;
        if (patches.Where((_, i) => !on[i] && !off[i]).Any()) return BattleState.Unknown;

        for (var i = 0; i < patches.Length; i++)
        {
            if (on[i]) continue;
            patches[i].Routine.CopyTo(cro[patches[i].Cave..]);
            BinaryPrimitives.WriteUInt32LittleEndian(cro[patches[i].Site..], patches[i].Call);
        }

        return BattleState.Patched;
    }

    /// <summary>Takes ours out of <c>Battle.cro</c>: the original words back, the caves emptied. False if nothing was ours.</summary>
    public static bool UnpatchBattle(Span<byte> cro)
    {
        var patches = BattlePatches();
        var (on, off) = States(cro, patches);

        if (patches.Where((_, i) => !on[i] && !off[i]).Any() || !on.Any(x => x)) return false;

        for (var i = 0; i < patches.Length; i++)
        {
            if (!on[i]) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(cro[patches[i].Site..], patches[i].Original);
            cro.Slice(patches[i].Cave, patches[i].Routine.Length).Clear();
        }

        return true;
    }


    /// <summary>The battle menu's text (game text file 12) and the line reason 8 shows, in the Spanish game text.</summary>
    public const int BattleMenuTextFile = 12, SpentZoneLine = 135;

    // pk3DS escribe el salto de línea del juego como «\n» literal.
    private const string NecrozmaLine = @"¡No puedes atrapar a Necrozma cuando está\nfusionado con otro Pokémon!";

    public const string SpentZoneLineText = @"¡Ya has tenido tu encuentro en esta zona!\nAquí no puedes atrapar más Pokémon.";

    /// <summary>
    /// The fused Necrozma's line in the Spanish game text (<c>a/0/3/6</c>) says the zone is spent, or says what it said.
    /// Only that line changes (<see cref="GameTextPatch"/>); a line that is neither is left alone. Null when nothing was done.
    /// </summary>
    public static string? SpentZoneText(string garcPath, bool on)
    {
        if (!File.Exists(garcPath)) return null;

        var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
        var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(garcPath));
        var lines = pk3DS.Core.TextFile.GetStrings(config, garc[BattleMenuTextFile]);

        if (lines.Length <= SpentZoneLine) return "texto del combate distinto: el mensaje de zona gastada sigue siendo el de Necrozma";

        var (from, to) = on ? (NecrozmaLine, SpentZoneLineText) : (SpentZoneLineText, NecrozmaLine);
        if (lines[SpentZoneLine] == to) return null;
        if (lines[SpentZoneLine] != from) return "texto del combate distinto: el mensaje de zona gastada sigue siendo el de Necrozma";

        garc[BattleMenuTextFile] = GameTextPatch.ReplaceLines(garc[BattleMenuTextFile],
            new Dictionary<int, ushort[]> { [SpentZoneLine] = GameTextPatch.Encode(config, to) });
        var packed = garc.Save();

        // Se relee antes de escribir: solo esa línea cambia.
        var back = pk3DS.Core.TextFile.GetStrings(config, new pk3DS.Core.CTR.GARC.LazyGARC(packed)[BattleMenuTextFile]);
        if (back[SpentZoneLine] != to || back.Where((line, i) => i != SpentZoneLine && line != lines[i]).Any())
        {
            return "el texto nuevo no se relee bien: no se toca";
        }

        File.WriteAllBytes(garcPath, packed);
        return on ? "texto: mensaje de zona gastada puesto" : "texto: mensaje de Necrozma devuelto";
    }

    private static (bool[] On, bool[] Off) States(ReadOnlySpan<byte> cro, CroPatch[] patches)
    {
        var on = new bool[patches.Length];
        var off = new bool[patches.Length];
        for (var i = 0; i < patches.Length; i++)
        {
            on[i] = patches[i].IsOn(cro);
            off[i] = patches[i].IsOff(cro);
        }

        return (on, off);
    }

    /// <summary>
    /// Puts the patches into an installed mod (<c>load/mods/&lt;title&gt;</c>), or takes them out when <paramref name="on"/>
    /// is false. With the emulator closed: <c>code.ips</c> is read at boot. Says what it did, one line per file.
    /// </summary>
    public static IReadOnlyList<string> Apply(string modFolder, bool on)
    {
        var said = new List<string>();
        var code = Path.Combine(modFolder, "exefs", "code.bin");
        var ips = Path.Combine(modFolder, "exefs", "code.ips");
        var battle = Path.Combine(modFolder, "romfs", BattleCro);

        // El SuperCarameloraro no es una regla: va siempre, con las reglas puestas o no.
        var superIcon = InstallSuperCandy(Path.Combine(modFolder, "romfs"), out var superProblem);
        if (superProblem is not null) said.Add(superProblem);
        var repelIcon = InstallInfiniteRepel(Path.Combine(modFolder, "romfs"), out var repelProblem);
        if (repelProblem is not null) said.Add(repelProblem);
        var eggIcon = InstallEggTurbo(Path.Combine(modFolder, "romfs"), out var eggProblem);
        if (eggProblem is not null) said.Add(eggProblem);

        if (File.Exists(code))
        {
            var existing = File.Exists(ips) ? Ips.Read(File.ReadAllBytes(ips)) : [];

            if (existing is null)
            {
                said.Add("code.ips no es un parche IPS: no se toca");
            }
            else
            {
                // Lo nuestro se quita siempre antes de decidir: si el code.bin ha cambiado, unos registros viejos se
                // aplicarían a un código que no es el que se midió.
                var hadOurs = existing.Any(r => CodeOffsets.Contains(r.Offset));
                var rest = existing.Where(r => !CodeOffsets.Contains(r.Offset) && !SuperCandyOffsets.Contains(r.Offset)
                                                   && !InfiniteRepelOffsets.Contains(r.Offset)
                                                   && !EggTurboOffsets.Contains(r.Offset)).ToList();
                var bytes = File.ReadAllBytes(code);
                var ours = on ? CodeRecords(bytes) : null;
                var super = superIcon is { } icon ? SuperCandyRecords(bytes, icon) : null;
                var final = ours is null ? rest : Ips.Merge(rest, ours);
                if (super is not null) final = Ips.Merge(final, super);
                var repel = repelIcon is { } repelAt ? InfiniteRepelRecords(bytes, repelAt) : null;
                if (repel is not null) final = Ips.Merge(final, repel);
                var egg = eggIcon is { } eggAt ? EggTurboRecords(bytes, eggAt) : null;
                if (egg is not null) final = Ips.Merge(final, egg);

                if (final.Count > 0) File.WriteAllBytes(ips, Ips.Write(final));
                else if (File.Exists(ips)) File.Delete(ips);

                if (ours is not null) said.Add("code.bin: cap en el Caramelo Raro, caídos sin curar y duplicados puestos (code.ips)");
                else if (on) said.Add("code.bin no es el que se conoce: sin cap en el Caramelo Raro, caídos sin curar ni duplicados");
                else if (hadOurs) said.Add("code.bin: cap en el Caramelo Raro, caídos sin curar y duplicados quitados");

                if (super is not null) said.Add("code.bin: SuperCarameloraro puesto (code.ips)");
                else if (superIcon is not null) said.Add("code.bin no es el que se conoce: el SuperCarameloraro sube un solo nivel");

                if (repel is not null) said.Add("code.bin: Repelente Infinito puesto (code.ips)");
                else if (repelIcon is not null) said.Add("code.bin no es el que se conoce: el Repelente Infinito se gasta al primer paso");

                if (egg is not null) said.Add("code.bin: Incubadora Turbo puesta (code.ips)");
                else if (eggIcon is not null) said.Add("code.bin no es el que se conoce: la Incubadora Turbo no acelera los huevos");
            }
        }

        var bag = Path.Combine(modFolder, "romfs", BagCro);
        if (repelIcon is not null && File.Exists(bag))
        {
            var bytes = File.ReadAllBytes(bag);
            var state = PatchBag(bytes);
            if (state == BattleState.Patched) File.WriteAllBytes(bag, bytes);
            if (state == BattleState.Unknown) said.Add("Bag.cro no es el que se conoce: el Repelente Infinito no se puede usar");
            else if (state == BattleState.Patched) said.Add("Bag.cro: Repelente Infinito puesto");
        }

        if (SpentZoneText(Path.Combine(modFolder, "romfs", "a", "0", "3", "6"), on) is { } text) said.Add(text);

        if (File.Exists(battle))
        {
            var bytes = File.ReadAllBytes(battle);

            if (on)
            {
                var state = PatchBattle(bytes);
                if (state == BattleState.Patched) File.WriteAllBytes(battle, bytes);
                said.Add(state switch
                {
                    BattleState.Patched => "Battle.cro: sin experiencia al cap y balls rechazadas en zona gastada puestos",
                    BattleState.AlreadyPatched => "Battle.cro: ya tenía el cap y las balls",
                    _ => "Battle.cro no es el que se conoce: sin cap ni balls en combate"
                });
            }
            else if (UnpatchBattle(bytes))
            {
                File.WriteAllBytes(battle, bytes);
                said.Add("Battle.cro: cap y balls en combate quitados");
            }
        }

        return said;
    }

    private static bool Matches(ReadOnlySpan<byte> data, int site, uint word, (int Offset, uint Word)[] context)
    {
        if (site + 12 > data.Length || BinaryPrimitives.ReadUInt32LittleEndian(data[site..]) != word) return false;

        foreach (var (offset, expected) in context)
        {
            if (offset + 4 > data.Length || BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) != expected) return false;
        }

        return true;
    }

    private static bool Empty(ReadOnlySpan<byte> data, int at, int length) =>
        at + length <= data.Length && !data.Slice(at, length).ContainsAnyExcept((byte)0);

    private static byte[] Word(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
}

/// <summary>One record of an IPS patch: these bytes at this offset.</summary>
public sealed record IpsRecord(int Offset, byte[] Bytes);

/// <summary>Reads and writes the IPS files Azahar applies to <c>code.bin</c> at boot (<c>exefs/code.ips</c>).</summary>
public static class Ips
{
    public static byte[] Write(IEnumerable<IpsRecord> records)
    {
        var output = new List<byte>("PATCH"u8.ToArray());

        foreach (var record in records.OrderBy(r => r.Offset))
        {
            if (record.Offset is < 0 or > 0xFFFFFF || record.Offset == 0x454F46 || record.Bytes.Length is 0 or > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(records), "Un parche IPS no puede llevar ese registro.");
            }

            output.AddRange([(byte)(record.Offset >> 16), (byte)(record.Offset >> 8), (byte)record.Offset]);
            output.AddRange([(byte)(record.Bytes.Length >> 8), (byte)record.Bytes.Length]);
            output.AddRange(record.Bytes);
        }

        output.AddRange("EOF"u8.ToArray());
        return [.. output];
    }

    /// <summary>The records of a patch; RLE records come out expanded. Null when it is not an IPS file.</summary>
    public static List<IpsRecord>? Read(ReadOnlySpan<byte> patch)
    {
        if (patch.Length < 8 || !patch[..5].SequenceEqual("PATCH"u8))
        {
            return null;
        }

        var records = new List<IpsRecord>();
        var at = 5;

        while (at + 3 <= patch.Length)
        {
            if (patch.Slice(at, 3).SequenceEqual("EOF"u8)) return records;
            if (at + 5 > patch.Length) return null;

            var offset = (patch[at] << 16) | (patch[at + 1] << 8) | patch[at + 2];
            var size = (patch[at + 3] << 8) | patch[at + 4];
            at += 5;

            if (size == 0)
            {
                if (at + 3 > patch.Length) return null;
                var count = (patch[at] << 8) | patch[at + 1];
                records.Add(new IpsRecord(offset, Enumerable.Repeat(patch[at + 2], count).ToArray()));
                at += 3;
            }
            else
            {
                if (at + size > patch.Length) return null;
                records.Add(new IpsRecord(offset, patch.Slice(at, size).ToArray()));
                at += size;
            }
        }

        return null;
    }

    /// <summary><paramref name="existing"/> without whatever overlaps <paramref name="ours"/>, plus <paramref name="ours"/>.</summary>
    public static List<IpsRecord> Merge(IEnumerable<IpsRecord> existing, IReadOnlyList<IpsRecord> ours) =>
    [
        .. existing.Where(e => !ours.Any(o => e.Offset < o.Offset + o.Bytes.Length && o.Offset < e.Offset + e.Bytes.Length)),
        .. ours
    ];
}
