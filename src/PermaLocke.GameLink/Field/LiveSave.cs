using PermaLocke.GameLink.Rpc;
using PKHeX.Core;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// The save as the running game holds it now, not as it was last written to the file (2026-10-06).
/// </summary>
/// <remarks>
/// <para>
/// The game keeps its save data in memory and updates it as things happen; the file only catches up when the player saves.
/// The GameManager sits at a fixed address of the mod's <c>code.bin</c> (0x6A3984, what <c>FUN_001048b4</c> returns), its
/// GameData at +0x24, and GameData +4 points at the save data. Inside it the blocks are not where the file has them; each
/// was located on the organiser's game right after saving, every non-zero byte of the file block equal in memory:
/// </para>
/// <code>
/// block  5 EventWork   file 0x01E00   memory +0x015DC
/// block  6 ZukanData   file 0x02C00   memory +0x023E0
/// block 28 Record      file 0x6A200   memory +0x68120   (21 of 21 non-zero records equal)
/// </code>
/// <para>
/// Nothing is parsed by hand: the file's bytes are copied, the live blocks laid over them and PKHeX reads the result, so a
/// caught flag or a record means exactly what it means in the file. Callers still check what they get against the file
/// (a Pokédex only gains captures, records only go up): a chain that leads somewhere else must not pass for the game.
/// </para>
/// </remarks>
public sealed class LiveSave(AzaharRpcClient client, SavedGameCache saved)
{
    private const uint GameManagerPointer = 0x006A3984, GameDataOffset = 0x24, SaveDataOffset = 0x04;
    private const uint LinearHeap = 0x30000000, LinearHeapEnd = 0x34000000;

    public const int EventWorkBlock = 5, ZukanBlock = 6, RecordBlock = 28;

    /// <summary>Where each block sits from the save data's start, in memory.</summary>
    public static readonly IReadOnlyDictionary<int, uint> MemoryOffsets = new Dictionary<int, uint>
    {
        [EventWorkBlock] = 0x015DC,
        [ZukanBlock] = 0x023E0,
        [RecordBlock] = 0x68120,
    };

    /// <summary>The start of the save data in memory, or null when the chain does not lead into the heap.</summary>
    public uint? Base()
    {
        static bool InHeap(uint address) => address is >= LinearHeap and < LinearHeapEnd;

        try
        {
            if (!client.TryReadMemory(GameManagerPointer, 4, out var m) || BitConverter.ToUInt32(m) is var manager && !InHeap(manager)
                || !client.TryReadMemory(manager + GameDataOffset, 4, out var d) || BitConverter.ToUInt32(d) is var data && !InHeap(data)
                || !client.TryReadMemory(data + SaveDataOffset, 4, out var s) || BitConverter.ToUInt32(s) is var save && !InHeap(save))
            {
                return null;
            }

            return save;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Where a block is in memory now, or null.</summary>
    public uint? AddressOf(int block) => Base() is { } start ? start + MemoryOffsets[block] : null;

    /// <summary>
    /// The last save with these blocks as the game holds them now, and the file's own; null when there is no save. When
    /// a block cannot be read, it stays as the file has it.
    /// </summary>
    public (SAV7USUM Live, SAV7USUM File)? Load(params int[] blocks)
    {
        if (saved.Load() is not { } game)
        {
            return null;
        }

        var data = game.Save.Data.ToArray();

        if (Base() is { } start)
        {
            foreach (var block in blocks)
            {
                var info = game.Save.AllBlocks[block];

                if (client.TryReadMemory(start + MemoryOffsets[block], info.Length, out var live))
                {
                    live.CopyTo(data, info.Offset);
                }
            }
        }

        return (new SAV7USUM(data), game.Save);
    }
}
