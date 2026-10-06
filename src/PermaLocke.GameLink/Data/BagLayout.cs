using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// One bag entry as generation 7 packs it: a single 32 bit word.
/// </summary>
/// <remarks>
/// Layout confirmed against PKHeX by round-tripping a known item through
/// <see cref="InventoryPouch7"/>: item id in bits 0-9, quantity in bits 10-19, free space
/// index in bits 20-29 and the "new" badge in bit 30. Bit 31 is unused.
///
/// The two upper fields matter even though PermaLocke never sets them on purpose: an earlier
/// version rebuilt the word as <c>id | (count &lt;&lt; 10)</c> and silently wiped both.
/// </remarks>
public readonly record struct BagEntry(int ItemId, int Count, int FreeSpaceIndex, bool IsNew)
{
    public const int MaxItemId = 0x3FF;
    public const int MaxCount = 0x3FF;

    public static readonly BagEntry Empty = new(0, 0, 0, false);

    /// <summary>An empty slot is a word of zeros; the game stores nothing else there.</summary>
    public bool IsEmpty => ItemId == 0;

    public static BagEntry Unpack(uint value) => new(
        (int)(value & 0x3FF),
        (int)((value >> 10) & 0x3FF),
        (int)((value >> 20) & 0x3FF),
        ((value >> 30) & 1) != 0);

    public uint Pack() =>
        (uint)(ItemId & 0x3FF)
        | ((uint)(Count & 0x3FF) << 10)
        | ((uint)(FreeSpaceIndex & 0x3FF) << 20)
        | (IsNew ? 1u << 30 : 0u);

    /// <summary>Same entry with a different quantity, keeping the flags the game set.</summary>
    public BagEntry WithCount(int count) => this with { Count = count };
}

/// <summary>One pocket of the bag: where it starts inside the block and what it accepts.</summary>
public sealed class BagPocket
{
    private readonly HashSet<ushort> _legalItems;

    internal BagPocket(InventoryType type, int offset, int slots, int maxCount, IEnumerable<ushort> legalItems)
    {
        Type = type;
        Offset = offset;
        Slots = slots;
        MaxCount = maxCount;
        _legalItems = [.. legalItems];
    }

    public InventoryType Type { get; }

    /// <summary>Byte offset of the pocket from the start of the bag block.</summary>
    public int Offset { get; }

    public int Slots { get; }

    /// <summary>How many of one item fit in a slot. Key items and TMs are capped at one.</summary>
    public int MaxCount { get; }

    public int Bytes => Slots * 4;

    public bool CanContain(int itemId) => itemId is > 0 and <= BagEntry.MaxItemId
                                          && _legalItems.Contains((ushort)itemId);
}

/// <summary>
/// Map of the bag block that Ultra Sun and Ultra Moon keep in memory.
/// </summary>
/// <remarks>
/// Nothing here is hand copied. The offsets are derived at runtime from PKHeX by marking slot
/// zero of every pocket with a distinct sentinel and dumping the block, so a change in PKHeX
/// shows up as a failure to build the layout instead of as silently wrong addresses.
///
/// Verified against the running game (see <c>docs/ARCHITECTURE.md</c> §22): the block the game
/// keeps in RAM has exactly this layout, and is followed by a table of pointers into itself,
/// which is what makes it findable without guessing.
/// </remarks>
public sealed class BagLayout
{
    private const int SentinelItemId = BagEntry.MaxItemId;

    private BagLayout(IReadOnlyList<BagPocket> pockets, int blockSize)
    {
        Pockets = pockets;
        BlockSize = blockSize;
    }

    /// <summary>Built once: walking the pocket tables is not free.</summary>
    public static BagLayout UltraSunMoon { get; } = Build();

    public IReadOnlyList<BagPocket> Pockets { get; }

    /// <summary>Size of the whole block, pockets laid end to end. 0xE28 bytes in USUM.</summary>
    public int BlockSize { get; }

    /// <summary>
    /// The game keeps one pointer per pocket immediately after the block. That table is what
    /// identifies the bag in memory: seven words that must point back into their own block.
    /// </summary>
    public int PointerTableBytes => Pockets.Count * 4;

    /// <summary>Which pocket an item belongs to, or null if no pocket accepts it.</summary>
    public BagPocket? PocketFor(int itemId) =>
        Pockets.FirstOrDefault(pocket => pocket.CanContain(itemId));

    /// <summary>
    /// True when <paramref name="words"/> are the pocket pointer table of the block that would
    /// end at <paramref name="tableAddress"/>: every pocket pointer present, each exactly once.
    /// </summary>
    /// <remarks>
    /// This single test is what identifies the bag. It is deliberately not a score or a
    /// likeness heuristic — either the seven words point back into their own block or the
    /// candidate is rejected — because the heuristic it replaced accepted random memory.
    /// </remarks>
    public bool TryMatchPointerTable(uint tableAddress, ReadOnlySpan<byte> words, out uint baseAddress)
    {
        baseAddress = 0;

        if (words.Length < PointerTableBytes || tableAddress < (uint)BlockSize)
        {
            return false;
        }

        var candidate = tableAddress - (uint)BlockSize;
        Span<bool> seen = stackalloc bool[Pockets.Count];

        for (var index = 0; index < Pockets.Count; index++)
        {
            var pointer = BitConverter.ToUInt32(words[(index * 4)..]);
            var pocket = -1;

            for (var other = 0; other < Pockets.Count; other++)
            {
                if (candidate + (uint)Pockets[other].Offset == pointer)
                {
                    pocket = other;
                    break;
                }
            }

            if (pocket < 0 || seen[pocket])
            {
                return false;
            }

            seen[pocket] = true;
        }

        baseAddress = candidate;
        return true;
    }

    /// <summary>Every occupied slot of a block image, pocket by pocket.</summary>
    /// <remarks>
    /// Nothing is assumed about how the pockets are filled. Key items are neither packed from
    /// the front nor counted: in the real save the Sparkling Stone sat alone in slot 197 with
    /// a quantity of zero, so stopping at the first gap would have missed it.
    /// </remarks>
    public IReadOnlyList<BagSlot> DecodeBlock(uint baseAddress, ReadOnlySpan<byte> block)
    {
        var contents = new List<BagSlot>();

        foreach (var pocket in Pockets)
        {
            for (var slot = 0; slot < pocket.Slots; slot++)
            {
                var at = pocket.Offset + (slot * 4);

                if (at + 4 > block.Length)
                {
                    break;
                }

                var entry = BagEntry.Unpack(BitConverter.ToUInt32(block[at..]));

                if (!entry.IsEmpty)
                {
                    contents.Add(new BagSlot(pocket, slot,
                        baseAddress + (uint)at, entry));
                }
            }
        }

        return contents;
    }

    /// <summary>
    /// The mega stones the gen 8-9 expansion adds (Legends Z-A), which PKHeX does not know: Ultra Moon never had them.
    /// </summary>
    /// <remarks>
    /// Without them no pocket accepted the id and the shop could not deliver them (2026-09-24: a friend found it with
    /// the released build). They go in the item pocket, like every cartridge mega stone. The ids are the ones of
    /// Data/shop.json that PKHeX rejects: 505-520 are ids the expansion reuses, 961 and 995-1023 are new.
    /// </remarks>
    // Una propiedad y no un campo: UltraSunMoon se construye antes de que un campo de más abajo esté inicializado.
    private static ushort[] ExpansionMegaStones =>
        [.. Enumerable.Range(505, 16).Append(961).Concat(Enumerable.Range(995, 29)).Select(id => (ushort)id)];

    private static BagLayout Build()
    {
        var bag = new PlayerBag7USUM(new SAV7USUM());
        var pouches = bag.Pouches;

        // Un centinela distinto por bolsillo: el hueco 0 lleva un id imposible y una cantidad
        // que es el número de bolsillo, así que al volcar el bloque cada uno se reconoce solo.
        for (var index = 0; index < pouches.Count; index++)
        {
            var pouch = pouches[index];

            for (var slot = 0; slot < pouch.Items.Length; slot++)
            {
                pouch.Items[slot] = slot == 0
                    ? new InventoryItem7 { Index = SentinelItemId, Count = index + 1 }
                    : new InventoryItem7();
            }
        }

        // CopyTo escribe exactamente el bloque y acepta un span mayor, así que se le da de
        // sobra y el tamaño real se deduce de dónde acaba el último bolsillo.
        var image = new byte[0x4000];
        bag.CopyTo(image);

        var pockets = new List<BagPocket>();

        for (var index = 0; index < pouches.Count; index++)
        {
            var pouch = pouches[index];
            var sentinel = new BagEntry(SentinelItemId, index + 1, 0, false).Pack();
            var offset = IndexOfWord(image, sentinel);

            if (offset < 0)
            {
                throw new InvalidOperationException(
                    $"No se pudo situar el bolsillo {pouch.Type} dentro del bloque de mochila. "
                    + "PKHeX ha cambiado el formato y BagLayout debe revisarse.");
            }

            IEnumerable<ushort> legal = pouch.GetAllItems().ToArray();

            if (pouch.Type == InventoryType.Items)
            {
                legal = legal.Concat(ExpansionMegaStones);
            }

            // El SuperCarameloraro (2026-10-06) es un hueco libre de la expansión que PermaLocke convierte en una copia
            // del Caramelo Raro, y va donde él.
            if (pouch.Type == InventoryType.Medicine)
            {
                legal = legal.Append((ushort)PermaLocke.Core.Domain.SuperCandy.ItemId);
            }

            pockets.Add(new BagPocket(pouch.Type, offset, pouch.Items.Length, pouch.MaxCount, legal));
        }

        pockets.Sort((left, right) => left.Offset.CompareTo(right.Offset));

        var blockSize = pockets.Max(pocket => pocket.Offset + pocket.Bytes);

        // Los bolsillos van pegados, sin huecos. Si dejaran de estarlo, el bloque ya no sería
        // un rango contiguo y todo lo que hay debajo dejaría de valer.
        var expected = 0;

        foreach (var pocket in pockets)
        {
            if (pocket.Offset != expected)
            {
                throw new InvalidOperationException(
                    $"El bolsillo {pocket.Type} empieza en 0x{pocket.Offset:X} y se esperaba "
                    + $"0x{expected:X}: los bolsillos ya no son contiguos.");
            }

            expected += pocket.Bytes;
        }

        return new BagLayout(pockets, blockSize);
    }

    private static int IndexOfWord(byte[] image, uint word)
    {
        for (var offset = 0; offset + 4 <= image.Length; offset += 4)
        {
            if (BitConverter.ToUInt32(image, offset) == word)
            {
                return offset;
            }
        }

        return -1;
    }
}
