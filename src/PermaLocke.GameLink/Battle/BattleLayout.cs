using System.Buffers.Binary;

namespace PermaLocke.GameLink.Battle;

/// <summary>One Pokémon as the battle holds it, read from its block.</summary>
/// <param name="Address">Where the block's data starts, just after its heap header.</param>
/// <param name="Pointer">The party structure (stride 0x1E4) this Pokémon was built from.</param>
/// <param name="BattleId">Its position in the battle: 0-5 the player's party in order, 12 a wild opponent.</param>
public sealed record BattleBlock(uint Address, uint Pointer, int Species, int MaxHp, int CurrentHp, int BattleId)
{
    public bool IsPlayers => BattleId is >= 0 and < BattleLayout.PartySlots;
}

/// <summary>
/// The shape of a battle Pokémon block, as measured against a running game (§114).
/// </summary>
/// <remarks>
/// <para>
/// Found with a real hit: Ferrocuello at 176, one attack, 174 on screen, and of 567 addresses holding
/// its HP beside its maximum only two dropped. Around them sat an 800-byte heap block, and searching
/// for that block's shape found one per Pokémon in the battle — the six of the party in order, the
/// fallen included, and the wild one at position 12.
/// </para>
/// <para>
/// Everything here is <b>checked, not assumed</b>, because the blocks are freed when the battle ends
/// and their memory is reused at once: a block only counts if its heap header still says «in use»
/// with the right size, its data still starts the way every battle block starts, and its numbers
/// hold together. A read that fails any of that is somebody else's memory.
/// </para>
/// </remarks>
public static class BattleLayout
{
    /// <summary>Bytes of data in a block.</summary>
    public const int BlockSize = 0x320;

    /// <summary>The heap header in front of each block: signature, attributes, size and two links.</summary>
    public const int HeaderSize = 0x10;

    /// <summary>From one block's data to the next one's: blocks sit back to back in a table.</summary>
    public const int Stride = BlockSize + HeaderSize;

    public const int PartySlots = 6;

    private const int PointerOffset = 0x20;
    private const int SpeciesOffset = 0x2C;
    private const int MaxHpOffset = 0x2E;
    public const int CurrentHpOffset = 0x30;
    private const int BattleIdOffset = 0x39;

    /// <summary>How much to read, counted from the header, to parse a block.</summary>
    public const int ReadLength = HeaderSize + BattleIdOffset + 1;

    /// <summary>
    /// «DU» — the signature NintendoWare's expanded heap gives a block in use — and the block size,
    /// followed by the eight bytes every battle block's data starts with.
    /// </summary>
    public static ReadOnlySpan<byte> SearchPattern =>
    [
        0x44, 0x55, 0x00, 0x00, 0x20, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xE7, 0xFF, 0xFF, 0xFF, 0x20, 0x00, 0x00, 0x00
    ];

    /// <summary>The attributes and the two heap links change from block to block; the rest must match.</summary>
    public static ReadOnlySpan<byte> SearchMask =>
    [
        0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
    ];

    /// <summary>
    /// Reads a block from bytes that start at its heap header, or null when they are not a live
    /// battle block holding a Pokémon.
    /// </summary>
    /// <param name="headerAddress">Where <paramref name="bytes"/> were read from.</param>
    public static BattleBlock? Parse(uint headerAddress, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < ReadLength)
        {
            return null;
        }

        var pattern = SearchPattern;
        var mask = SearchMask;

        for (var i = 0; i < pattern.Length; i++)
        {
            if ((bytes[i] & mask[i]) != (pattern[i] & mask[i]))
            {
                return null;
            }
        }

        var data = bytes[HeaderSize..];
        var pointer = BinaryPrimitives.ReadUInt32LittleEndian(data[PointerOffset..]);
        var species = BinaryPrimitives.ReadUInt16LittleEndian(data[SpeciesOffset..]);
        var max = BinaryPrimitives.ReadUInt16LittleEndian(data[MaxHpOffset..]);
        var hp = BinaryPrimitives.ReadUInt16LittleEndian(data[CurrentHpOffset..]);
        var id = data[BattleIdOffset];

        // Los bloques vacíos de la tabla -posiciones que este combate no usa- tienen la misma cabecera
        // y todo a cero: no son un Pokémon. Y unos PS por encima del máximo son memoria de otro.
        if (pointer == 0 || species == 0 || max == 0 || hp > max)
        {
            return null;
        }

        return new BattleBlock(headerAddress + HeaderSize, pointer, species, max, hp, id);
    }
}

/// <summary>A table of battle blocks, back to back and indexed by battle position.</summary>
/// <param name="Origin">Where position 0's data would start.</param>
public sealed record BattleTable(uint Origin, IReadOnlyList<BattleBlock> Blocks)
{
    /// <summary>
    /// Whether an opponent is in it. The display table outlives the battle with the party still in it
    /// but not the opponent, whose block is reused at once — measured — so this is what tells a battle
    /// in progress from the leftovers of the last one.
    /// </summary>
    public bool HasOpponent => Blocks.Any(block => !block.IsPlayers);

    public BattleBlock? Block(int battleId) => Blocks.FirstOrDefault(block => block.BattleId == battleId);

    /// <summary>Where the header of a position's block is.</summary>
    public uint HeaderOf(int battleId) => Origin + (uint)(battleId * BattleLayout.Stride) - BattleLayout.HeaderSize;

    /// <summary>
    /// Sorts blocks into their tables: two blocks share one when they sit at the right distance for
    /// their positions.
    /// </summary>
    public static IReadOnlyList<BattleTable> Group(IEnumerable<BattleBlock> blocks) =>
        [.. blocks
            .GroupBy(block => block.Address - (uint)(block.BattleId * BattleLayout.Stride))
            .Select(group => new BattleTable(group.Key, [.. group.OrderBy(block => block.BattleId)]))];
}
