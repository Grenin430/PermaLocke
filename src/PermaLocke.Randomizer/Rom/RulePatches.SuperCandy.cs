using System.Buffers.Binary;

using PermaLocke.Core.Domain;


namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The SuperCarameloraro (2026-10-06): a Rare Candy that raises five levels at once, never past the cap, and that only
/// PermaLocke hands out (MISCELÁNEA writes it into the bag like the Rare Candies).
/// </summary>
/// <remarks>
/// <para>
/// It is item <see cref="SuperCandyItem"/>, one of the expansion's unused slots (named «(?)», with empty data): its data
/// becomes a copy of the Rare Candy's with one difference, the price word, which is how the code tells them apart.
/// Name and description go into the Spanish game text, and its icon is the Rare Candy's in another colour, added at the
/// end of <c>a/0/6/1</c> and pointed to from the item→icon table the expansion's code uses (<c>code.bin</c> 0x5BC8BC).
/// </para>
/// <para>
/// The effect: in the item effect function (0x445754, called by <c>PokeTool::ITEM_RCV_Recover</c>), an item with the
/// «level up» parameter does <c>mov r1,#1; cpy r0,r7; bl 0x325ab8</c>, and 0x325ab8 raises the level by r1 (stopping at
/// 100). The <c>mov</c> becomes a call that gives 1 for any other item and, for this one, the cap minus the level, at
/// most five (no cap, or a stale one, gives five). The usability check already refuses it at the cap
/// (<see cref="CompareWithCap"/>). There was no room for the routine in one piece: it is split over the three gaps left
/// at the end of the code segment.
/// </para>
/// </remarks>
public static partial class RulePatches
{
    public const int SuperCandyItem = SuperCandy.ItemId, RareCandyItem = 50, SuperCandyLevels = SuperCandy.Levels;

    public const string SuperCandyName = SuperCandy.Name;

    // pk3DS escribe el salto de línea del juego como «\n» literal.
    public const string SuperCandyDescription = @"Caramelo prodigioso que sube a un Pokémon\ncinco niveles de golpe.";

    /// <summary>The item's raw price word (the game shows ten times it). The Rare Candy's is 1000.</summary>
    public const ushort SuperCandyPrice = 0x400;

    /// <summary>Item names and descriptions in the game text (<c>a/0/3/6</c>): the name, its plural (what the bag shows for
    /// more than one) and the form the messages use, which carries how the plural is made (1 letter added, 0 taken away).</summary>
    public const int ItemFlavorFile = 39, ItemNamesFile = 40, ItemPluralFile = 41, ItemMessageFile = 42;

    /// <summary>Each text line of the item: what an unused slot has there, and what it gets.</summary>
    private static readonly (int File, int Line, string Unused, string Wanted)[] SuperCandyLines =
    [
        (ItemFlavorFile, SuperCandyItem, "", SuperCandyDescription),
        (ItemNamesFile, SuperCandyItem, "(?)", SuperCandyName),
        (ItemPluralFile, SuperCandyItem, "(?)", SuperCandyName + "s"),
        (ItemMessageFile, SuperCandyItem, "(?)[VAR 1101(00FE,0000)]", SuperCandyName + "[VAR 1101(00FE,0100)]s")
    ];

    /// <summary>File offsets in <c>code.bin</c>: the site, the three gaps and the item→icon table.</summary>
    public const int SuperCandySite = 0x345990, SuperCaveA = 0x4B9B24, SuperCaveB = 0x4B9BA8, SuperCaveC = 0x4B9A70;

    public const int ItemIconTable = 0x4BC8BC;

    /// <summary>The icon an item without one points to: the «?».</summary>
    private const uint BlankIcon = 768;

    private const uint MovR1One = 0xE3A01001;

    /// <summary><c>cmp r8,#0x64; bge</c> before, <c>cpy r0,r7</c> after.</summary>
    private static readonly (int Offset, uint Word)[] SuperCandyContext =
        [(SuperCandySite - 8, 0xE3580064), (SuperCandySite - 4, 0xAA000003), (SuperCandySite + 4, 0xE1A00007)];

    /// <summary>
    /// The skipped levels' moves. <c>CoreParam::LearnNewWazaOnCurrentLevel</c> (0x32555C), which the bag calls again and
    /// again after a level up until it answers 3 («nothing more»), walks the learnset with the level in r7 and each
    /// entry's level in r8: <c>cmp r7,r8; bcc done; bne next</c>, so only moves of exactly the current level. The
    /// <c>bne</c> becomes a <c>blne</c> to a check that also takes the entries down to <see cref="RuleBlock.SkippedLevels"/>
    /// below, and the «done» return (0x3257C0, <c>mov r0,#3</c>) puts that byte back to 0, so nothing after this check
    /// (a battle, a Rare Candy) sees it. Both routines live inside the same function, in two of the empty asserts the
    /// compiler left (<c>mov r3,#0; cpy r2..r0,r3; cpy r0,r0; nop; nop; b</c>): each one's guard now jumps straight to
    /// where the assert went, which only differs in r0-r3 being left as they were, and they are rewritten before use.
    /// </summary>
    public const int LearnSite = 0x225614, LearnDoneSite = 0x2257C0, LearnCave = 0x225678, LearnDoneCave = 0x225744;

    private const int LearnAssert = 0x225670, LearnNext = 0x2257B0, MovesAssert = 0x22573C;

    private static readonly (int Offset, uint Word)[] LearnContext =
    [
        (LearnSite - 8, 0xE1570008), (LearnSite - 4, 0x3A000069), (LearnSite + 4, 0xE35C0000),
        (LearnDoneSite - 4, 0xE28DD010), (LearnDoneSite + 4, 0xE8BD9FF0),
        (LearnAssert - 4, 0xE3500037), (LearnAssert, 0x3A000007), (LearnAssert + 0x24, 0xE0861080),
        (MovesAssert - 4, 0xE3540004), (MovesAssert, 0x3A000007), (MovesAssert + 0x24, 0xE3A01005)
    ];

    /// <summary>What each assert held, from its <c>mov r3,#0</c> to its <c>b</c>.</summary>
    private static readonly uint[] EmptyAssert = [0xE3A03000, 0xE1A02003, 0xE1A01003, 0xE1A00003, 0xE1A00000, 0xE320F000, 0xE320F000];

    private static int SuperIconEntry => ItemIconTable + 4 * SuperCandyItem;

    private static readonly int[] SuperCandyOffsets =
    [
        SuperCandySite, SuperCaveA, SuperCaveB, SuperCaveC, SuperIconEntry,
        LearnSite, LearnDoneSite, LearnAssert, LearnCave, MovesAssert, LearnDoneCave
    ];

    /// <summary>An ARM branch with condition <paramref name="cond"/> (14 = always) at <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static uint B(int from, int to, uint cond = 14, bool link = false) =>
        cond << 28 | (link ? 0x0B000000u : 0x0A000000u) | (uint)(((to - (from + 8)) >> 2) & 0x00FFFFFF);

    /// <summary>
    /// First piece: r1 = 1 unless the item (its data at [sp,#0x18], the caller's frame: nothing is pushed) carries the
    /// SuperCarameloraro's price.
    /// </summary>
    public static byte[] SuperCandyA() => Words(
        0xE59D1018,                         // ldr   r1, [sp, #0x18]  ; the item's data
        0xE1D110B0,                         // ldrh  r1, [r1]         ; its price word
        0xE3510B01,                         // cmp   r1, #0x400
        0x13A01001,                         // movne r1, #1
        0x112FFF1E,                         // bxne  lr
        B(SuperCaveA + 20, SuperCaveB),     // b     second piece
        RuleBlock.Cap);

    /// <summary>Second piece: r1 = cap - level (r8 holds the level); nothing to go up to means no cap: five.</summary>
    public static byte[] SuperCandyB() => Words(
        0xE51FC000 | (uint)(SuperCaveB + 8 - (SuperCaveA + 24)), // ldr r12, [pc, #-n] ; RuleBlock.Cap, in the first piece
        0xE5DC1000,                         // ldrb  r1, [r12]
        0xE0511008,                         // subs  r1, r1, r8
        0xD3A01000 | SuperCandyLevels,      // movle r1, #5
        0xE3510000 | SuperCandyLevels,      // cmp   r1, #5
        B(SuperCaveB + 20, SuperCaveC));    // b     third piece

    /// <summary>Third piece: at most five, and the levels past the first noted for the move check.</summary>
    public static byte[] SuperCandyC() => Words(
        0xC3A01000 | SuperCandyLevels,      // movgt r1, #5
        0xE2412001,                         // sub   r2, r1, #1
        0xE5CC2000 | (RuleBlock.SkippedLevels - RuleBlock.Cap), // strb r2, [r12, #8] ; RuleBlock.SkippedLevels
        0xE12FFF1E);                        // bx    lr

    /// <summary>
    /// Reached with <c>blne</c> when the entry's level (r8) is below the current one (r7): back to the candidate path when
    /// it is no lower than the current minus the skipped levels, to the next entry otherwise. r2 and r3 are free there.
    /// </summary>
    public static byte[] LearnSkipped() => Words(
        0xE59F2010,                         // ldr   r2, [pc, #16]    ; RuleBlock.SkippedLevels
        0xE5D22000,                         // ldrb  r2, [r2]
        0xE0473002,                         // sub   r3, r7, r2
        0xE1580003,                         // cmp   r8, r3
        0x212FFF1E,                         // bxhs  lr
        B(LearnCave + 20, LearnNext),       // b     next
        RuleBlock.SkippedLevels);

    /// <summary>«Nothing more»: the skipped levels back to 0, then <c>mov r0,#3</c>. r1 is scratch at a return.</summary>
    public static byte[] LearnDone() => Words(
        0xE59F100C,                         // ldr   r1, [pc, #12]    ; RuleBlock.SkippedLevels
        0xE3A00000,                         // mov   r0, #0
        0xE5C10000,                         // strb  r0, [r1]
        0xE3A00003,                         // mov   r0, #3
        0xE12FFF1E,                         // bx    lr
        RuleBlock.SkippedLevels);

    /// <summary>The records for the SuperCarameloraro, or null when this is not the code they were made for.</summary>
    public static IReadOnlyList<IpsRecord>? SuperCandyRecords(ReadOnlySpan<byte> code, int icon)
    {
        byte[] a = SuperCandyA(), b = SuperCandyB(), c = SuperCandyC();

        if (!Matches(code, SuperCandySite, MovR1One, SuperCandyContext)
            || !Matches(code, LearnSite, 0x1A000065, LearnContext) || !Matches(code, LearnDoneSite, 0xE3A00003, LearnContext)
            || !Holds(code, LearnAssert + 4, EmptyAssert) || !Holds(code, MovesAssert + 4, EmptyAssert)
            || !Empty(code, SuperCaveA, a.Length) || !Empty(code, SuperCaveB, b.Length) || !Empty(code, SuperCaveC, c.Length)
            || SuperIconEntry + 4 > code.Length || BinaryPrimitives.ReadUInt32LittleEndian(code[SuperIconEntry..]) != BlankIcon)
        {
            return null;
        }

        return
        [
            new IpsRecord(SuperCaveA, a),
            new IpsRecord(SuperCaveB, b),
            new IpsRecord(SuperCaveC, c),
            new IpsRecord(SuperCandySite, Word(Bl(SuperCandySite, SuperCaveA))),
            new IpsRecord(SuperIconEntry, Word((uint)icon)),
            // Cada guardia salta adonde iba su assert (lleno: el final del caso; si no, lo que seguía al assert).
            new IpsRecord(LearnAssert, Words(B(LearnAssert, LearnAssert + 0x38, cond: 2), B(LearnAssert + 4, LearnAssert + 0x24))),
            new IpsRecord(LearnCave, LearnSkipped()),
            new IpsRecord(LearnSite, Word(B(LearnSite, LearnCave, cond: 1, link: true))),
            new IpsRecord(MovesAssert, Words(B(MovesAssert, MovesAssert + 0x64, cond: 2), B(MovesAssert + 4, MovesAssert + 0x24))),
            new IpsRecord(LearnDoneCave, LearnDone()),
            new IpsRecord(LearnDoneSite, Word(Bl(LearnDoneSite, LearnDoneCave)))
        ];
    }

    private static bool Holds(ReadOnlySpan<byte> code, int at, uint[] words)
    {
        if (at + words.Length * 4 > code.Length) return false;
        for (var i = 0; i < words.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(code[(at + i * 4)..]) != words[i]) return false;
        }

        return true;
    }

    /// <summary>
    /// Puts the item into the installed mod's files: its data, its name and description, its icon. Returns the icon's
    /// index (for the code records), or null with what went wrong in <paramref name="problem"/>.
    /// </summary>
    public static int? InstallSuperCandy(string romfs, out string? problem)
    {
        problem = null;
        var itemData = Path.Combine(romfs, "a", "0", "1", "9");
        var text = Path.Combine(romfs, "a", "0", "3", "6");
        var icons = Path.Combine(romfs, "a", "0", "6", "1");

        if (!File.Exists(itemData) || !File.Exists(text) || !File.Exists(icons))
        {
            problem = "faltan ficheros del mod: sin SuperCarameloraro";
            return null;
        }

        var pockets = Path.Combine(romfs, "a", "1", "0", "3");
        if (!File.Exists(pockets))
        {
            problem = "falta la tabla de bolsillos del mod: sin SuperCarameloraro";
            return null;
        }

        problem = ItemData(itemData, SuperCandyItem, RareCandyItem, SuperCandyPrice, SuperCandyName, ownSort: true)
                  ?? ItemText(text, SuperCandyLines, SuperCandyName)
                  ?? ItemPocket(pockets, SuperCandyItem, RareCandyItem, SuperCandyName);
        if (problem is not null) return null;

        return AppendIcon(icons, Sprites.SuperCandyIcon.Bflim, RareCandyItem, SuperCandyName, out problem);
    }

    /// <summary>
    /// Makes the unused slot <paramref name="item"/> a copy of <paramref name="model"/>'s data with the price word
    /// <paramref name="price"/>. Null when done (or already so), what went wrong otherwise.
    /// </summary>
    /// <param name="ownSort">
    /// Give it a place of its own in its pocket's order (the byte at <see cref="SortOffset"/>), the first free one after the model's, instead of
    /// sharing the model's (2026-10-15). With the Rare Candy's, the bag came back from a move to learn or an evolution with the cursor on the Rare
    /// Candy, not on the SuperCarameloraro being used: a guess at why, still to be seen in the game.
    /// </param>
    private static string? ItemData(string path, int item, int model, ushort price, string name, bool ownSort = false)
    {
        var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(path));
        if (garc.FileCount <= item) return $"datos de objetos distintos: sin {name}";

        var copy = (byte[])garc[model].Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(copy, price);
        var wanted = (byte[])copy.Clone();
        if (ownSort && FreeSortAfter(garc, model, item) is { } sort) wanted[SortOffset] = sort;

        var current = garc[item];
        if (current.AsSpan().SequenceEqual(wanted)) return null;
        // Un hueco libre de la expansión solo lleva el byte 0x0D puesto; el que dejó una versión anterior (la copia sin orden propio) se rehace.
        if (!current.AsSpan().SequenceEqual(copy) && current.Where((value, i) => i != 0x0D && value != 0).Any()) return $"el objeto {item} ya se usa: sin {name}";

        garc[item] = wanted;
        File.WriteAllBytes(path, garc.Save());
        return null;
    }

    /// <summary>
    /// Gives <paramref name="item"/> the pocket of <paramref name="model"/> in the bag's pocket table (<c>a/1/0/3</c>, 2026-10-16): half a byte per
    /// item (odd items the low half), loaded at boot and read by <c>Savedata::MyItem::Sub</c> and <c>Check</c> (through <c>code.bin</c> 0x276B90)
    /// to know which pocket to look in. The item's own data says medicine, but this table still had the unused slot's 0, so the bag looked for
    /// the SuperCarameloraro in the wrong pocket and never took one away: it was never spent. Null when done (or already so).
    /// </summary>
    private static string? ItemPocket(string path, int item, int model, string name)
    {
        var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(path));
        if (garc.FileCount < 1 || garc[0].Length <= Math.Max(item, model) / 2) return $"tabla de bolsillos distinta: el {name} no se gastaría";

        var table = (byte[])garc[0].Clone();
        static int Nibble(byte[] t, int i) => (i & 1) == 1 ? t[i >> 1] & 0xF : t[i >> 1] >> 4;
        var pocket = Nibble(table, model);
        var current = Nibble(table, item);
        if (current == pocket) return null;
        if (current != 0) return $"el objeto {item} ya tiene bolsillo: el {name} no se gastaría";

        table[item >> 1] = (byte)((item & 1) == 1 ? (table[item >> 1] & 0xF0) | pocket : (table[item >> 1] & 0x0F) | (pocket << 4));
        garc[0] = table;
        File.WriteAllBytes(path, garc.Save());
        return null;
    }

    /// <summary>Where an item's place in its pocket's order is (pk3DS's <c>Item.SortIndex</c>).</summary>
    private const int SortOffset = 0x0F;

    /// <summary>The pocket an item goes in, from its packed word (bits 7-10).</summary>
    private static int PocketOf(byte[] data) => (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8)) >> 7) & 0xF;

    /// <summary>The first place in the model's pocket after the model's own that no other item there takes (the item itself aside).</summary>
    private static byte? FreeSortAfter(pk3DS.Core.CTR.GARC.LazyGARC garc, int model, int item)
    {
        var pocket = PocketOf(garc[model]);
        var taken = Enumerable.Range(1, garc.FileCount - 1)
            .Where(i => i != item && garc[i] is { Length: > SortOffset } d && PocketOf(d) == pocket)
            .Select(i => (int)garc[i][SortOffset]).ToHashSet();
        for (var sort = garc[model][SortOffset] + 1; sort < 255; sort++) if (!taken.Contains(sort)) return (byte)sort;
        return null;
    }

    /// <summary>Writes each line that still holds what it had unused; a line holding anything else stops it all.</summary>
    private static string? ItemText(string path, (int File, int Line, string Unused, string Wanted)[] lines, string name)
    {
        var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
        var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(path));
        var files = lines.Select(line => line.File).Distinct().ToArray();
        var before = files.ToDictionary(file => file, file => pk3DS.Core.TextFile.GetStrings(config, garc[file]));

        if (lines.Any(line => before[line.File].Length <= line.Line)) return $"texto del juego distinto: sin {name}";
        if (lines.All(line => before[line.File][line.Line] == line.Wanted)) return null;
        if (lines.Any(line => before[line.File][line.Line] != line.Unused && before[line.File][line.Line] != line.Wanted))
        {
            return $"el texto del {name} ya está ocupado: no se toca";
        }

        foreach (var file in files)
        {
            garc[file] = GameTextPatch.ReplaceLines(garc[file], lines.Where(line => line.File == file)
                .ToDictionary(line => line.Line, line => GameTextPatch.Encode(config, line.Wanted)));
        }

        var packed = garc.Save();

        // Se relee antes de escribir: en cada fichero solo cambian las líneas pedidas.
        var back = new pk3DS.Core.CTR.GARC.LazyGARC(packed);
        foreach (var file in files)
        {
            var read = pk3DS.Core.TextFile.GetStrings(config, back[file]);
            var wanted = lines.Where(line => line.File == file).ToDictionary(line => line.Line, line => line.Wanted);
            if (read.Length != before[file].Length
                || read.Where((line, i) => line != (wanted.TryGetValue(i, out var w) ? w : before[file][i])).Any())
            {
                return $"el texto del {name} no se relee bien: no se toca";
            }
        }

        File.WriteAllBytes(path, packed);
        return null;
    }

    /// <summary>
    /// The icon of <paramref name="model"/> repainted by <paramref name="paint"/>, added at the end of <c>a/0/6/1</c>; when
    /// one of the files added after the cartridge's already is it, that one is used.
    /// </summary>
    private static int? AppendIcon(string path, Func<byte[], byte[]?> paint, int model, string name, out string? problem)
    {
        problem = null;
        var bytes = File.ReadAllBytes(path);
        var garc = new pk3DS.Core.CTR.GARC.LazyGARC(bytes);
        var icon = paint(garc[model - 1]);

        if (icon is null)
        {
            problem = $"el icono del que sale el {name} no es el que se conoce: sin {name}";
            return null;
        }

        for (var index = garc.FileCount - 1; index > model; index--)
        {
            if (garc[index].AsSpan().SequenceEqual(icon)) return index;
        }

        if (Garc.Append(bytes, Compress(icon)) is not { } grown
            || new pk3DS.Core.CTR.GARC.LazyGARC(grown) is var back && back.FileCount != garc.FileCount + 1
            || !back[back.FileCount - 1].AsSpan().SequenceEqual(icon))
        {
            problem = $"no se pudo añadir el icono del {name}";
            return null;
        }

        File.WriteAllBytes(path, grown);
        return garc.FileCount;
    }

    /// <summary>LZ11, as every icon of the container. pk3DS only compresses files.</summary>
    private static byte[] Compress(byte[] data)
    {
        var source = Path.GetTempFileName();
        var target = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(source, data);
            pk3DS.Core.CTR.LZSS.Compress(source, target);
            return File.ReadAllBytes(target);
        }
        finally
        {
            File.Delete(source);
            File.Delete(target);
        }
    }

    /// <summary>Adds one file at the end of a version 6 GARC whose entries hold one file each. Null when it is not one.</summary>
    internal static class Garc
    {
        public static byte[]? Append(byte[] garc, byte[] file)
        {
            if (garc.Length < 0x24 || BinaryPrimitives.ReadUInt32LittleEndian(garc) != 0x47415243
                || BinaryPrimitives.ReadUInt16LittleEndian(garc.AsSpan(0xA)) != 0x0600)
            {
                return null;
            }

            var headerSize = BinaryPrimitives.ReadInt32LittleEndian(garc.AsSpan(4));
            var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(garc.AsSpan(0x10));
            var pad = Math.Max(1, BinaryPrimitives.ReadInt32LittleEndian(garc.AsSpan(0x20)));
            var fato = headerSize;
            var fatoSize = BinaryPrimitives.ReadInt32LittleEndian(garc.AsSpan(fato + 4));
            var count = BinaryPrimitives.ReadUInt16LittleEndian(garc.AsSpan(fato + 8));
            var fatb = fato + fatoSize;
            var fatbSize = BinaryPrimitives.ReadInt32LittleEndian(garc.AsSpan(fatb + 4));
            var fimb = fatb + fatbSize;
            var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(garc.AsSpan(fimb + 8));

            if (count == ushort.MaxValue || fimb + 0xC != dataOffset || fatbSize != 0xC + 16 * count) return null;
            for (var i = 0; i < count; i++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(garc.AsSpan(fatb + 0xC + 16 * i)) != 1) return null;
            }

            var padded = (file.Length + pad - 1) / pad * pad;
            var output = new byte[garc.Length + 4 + 16 + padded];
            var at = 0;

            void Put(ReadOnlySpan<byte> bytes) { bytes.CopyTo(output.AsSpan(at)); at += bytes.Length; }
            void U32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at), value); at += 4; }

            Put(garc.AsSpan(0, headerSize));
            Put(garc.AsSpan(fato, 0xC + 4 * count));
            U32((uint)(16 * count));
            Put(garc.AsSpan(fatb, 0xC + 16 * count));
            U32(1);
            U32(dataSize);
            U32(dataSize + (uint)padded);
            U32((uint)file.Length);
            Put(garc.AsSpan(fimb, garc.Length - fimb));
            Put(file);

            var span = output.AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x10..], (uint)(dataOffset + 20));
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], (uint)output.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x18..], Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(span[0x18..]), (uint)padded));
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x1C..], Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(span[0x1C..]), (uint)file.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(span[(fato + 4)..], (uint)(fatoSize + 4));
            BinaryPrimitives.WriteUInt16LittleEndian(span[(fato + 8)..], (ushort)(count + 1));
            BinaryPrimitives.WriteUInt32LittleEndian(span[(fatb + 4 + 4)..], (uint)(fatbSize + 16));
            BinaryPrimitives.WriteUInt32LittleEndian(span[(fatb + 4 + 8)..], (uint)(count + 1));
            BinaryPrimitives.WriteUInt32LittleEndian(span[(fimb + 20 + 8)..], dataSize + (uint)padded);
            return output;
        }
    }

    /// <summary>
    /// <c>Bag.cro</c>: after a level up that leads to a move to learn or an evolution, the bag reopens on a hard-coded 50
    /// (<c>mov r2,#0x32</c> handed to its «item in use» setter, 0x9530, at 0xE8BC and 0xEA58), and the «are there more»
    /// check before using another asks for 50 too (0xE8F4). All three now read the item actually used, which the bag keeps
    /// at <c>[this+0x170]</c> (stored zero-extended at 0xE810), so a SuperCarameloraro brings the cursor back to itself.
    /// With a Rare Candy that word is 50, so nothing changes for it.
    /// </summary>
    public const int BagCursorSiteA = 0xE8BC, BagCursorSiteB = 0xEA58, BagMoreSite = 0xE8F4;

    private const uint MovR2Fifty = 0xE3A02032, MovR1Fifty = 0xE3A01032, LdrR2Used = 0xE5942170, LdrR1Used = 0xE5941170;

    private static readonly (int Offset, uint Word)[] BagCursorContext =
    [
        (0xE804, 0xE5940170), (0xE810, 0xE5845170),
        (BagCursorSiteA - 4, 0xE3A03000), (BagCursorSiteA + 4, 0xE3A01003), (BagCursorSiteA + 12, Bl(BagCursorSiteA + 12, 0x9530)),
        (BagCursorSiteB - 4, 0xE1D533B0), (BagCursorSiteB + 4, 0xE3A01002), (BagCursorSiteB + 12, Bl(BagCursorSiteB + 12, 0x9530)),
        (BagMoreSite - 4, 0xE3A02001), (BagMoreSite + 4, Bl(BagMoreSite + 4, 0x6D8))
    ];

    /// <summary>
    /// The «use another» loop, in three places: <c>ldrh r0,[r0,#0x4c]; cmp r0,#50; bne out; ldr r0,[rB,#0x3c]; mov r2,#1;
    /// mov r1,#50; bl Check</c> carries on only with Rare Candies. Rewritten in the same seven words as <c>ldrh r1; cmp r1,#50;
    /// cmpne r1,#113; bne out; ldr r0; mov r2,#1; bl Check</c>, so the SuperCarameloraro asks for itself. Every «out» sets
    /// r0 and r1 before reading them, so r1 holding the item there instead of r0 changes nothing.
    /// </summary>
    private static readonly (int Site, int Out)[] BagLoops = [(0xD86C, 0xD8A8), (0xDA28, 0xDA68), (0x13DD4, 0x13E8C)];

    private static uint[] BagLoopBefore(ReadOnlySpan<byte> cro, int at, int @out) =>
    [
        0xE1D004BC, 0xE3500032, B(at + 8, @out, 1),
        BinaryPrimitives.ReadUInt32LittleEndian(cro[(at + 12)..]) is var ldr && (ldr & 0xFFF0FFFF) == 0xE590003C ? ldr : 0,
        0xE3A02001, MovR1Fifty, Bl(at + 24, 0x6D8)
    ];

    private static uint[] BagLoopAfter(uint ldr, int at, int @out) =>
        [0xE1D014BC, 0xE3510032, 0x13510000 | SuperCandyItem, B(at + 12, @out, 1), ldr, 0xE3A02001, Bl(at + 24, 0x6D8)];

    private static bool LoopDone(ReadOnlySpan<byte> cro, int site, int @out) =>
        Holds(cro, site, BagLoopAfter(BinaryPrimitives.ReadUInt32LittleEndian(cro[(site + 16)..]), site, @out));

    private static bool CursorAs(ReadOnlySpan<byte> cro, uint siteWord, uint moreWord) =>
        Matches(cro, BagCursorSiteA, siteWord, BagCursorContext)
        && BinaryPrimitives.ReadUInt32LittleEndian(cro[BagCursorSiteB..]) == siteWord
        && BinaryPrimitives.ReadUInt32LittleEndian(cro[BagMoreSite..]) == moreWord;

    /// <summary>Points the bag's return cursor at the item used and lets the loop go on with it, in place.</summary>
    public static BattleState PatchBagCursor(Span<byte> cro)
    {
        var loopsDone = true;
        foreach (var (site, @out) in BagLoops)
        {
            if (site + 28 > cro.Length) return BattleState.Unknown;
            if (LoopDone(cro, site, @out)) continue;
            if (!Holds(cro, site, BagLoopBefore(cro, site, @out))) return BattleState.Unknown;
            loopsDone = false;
        }

        // Una mochila con el cursor ya puesto (de la versión anterior) recibe solo los bucles.
        var cursorDone = CursorAs(cro, LdrR2Used, LdrR1Used);
        if (!cursorDone && !CursorAs(cro, MovR2Fifty, MovR1Fifty)) return BattleState.Unknown;
        if (cursorDone && loopsDone) return BattleState.AlreadyPatched;

        BinaryPrimitives.WriteUInt32LittleEndian(cro[BagCursorSiteA..], LdrR2Used);
        BinaryPrimitives.WriteUInt32LittleEndian(cro[BagCursorSiteB..], LdrR2Used);
        BinaryPrimitives.WriteUInt32LittleEndian(cro[BagMoreSite..], LdrR1Used);
        foreach (var (site, @out) in BagLoops)
        {
            if (LoopDone(cro, site, @out)) continue;
            var words = BagLoopAfter(BinaryPrimitives.ReadUInt32LittleEndian(cro[(site + 12)..]), site, @out);
            for (var i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(cro[(site + 4 * i)..], words[i]);
        }

        return BattleState.Patched;
    }
}
