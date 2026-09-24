using System.Buffers.Binary;
using System.Text;

namespace PermaLocke.GameLink.Rpc;

/// <param name="Start">First 3DS virtual address of the region.</param>
public sealed record MemoryRegion(uint Start, uint End, string Label)
{
    public uint Size => End - Start;

    public override string ToString() => $"{Label} 0x{Start:X8}-0x{End:X8} ({Size / 1024} KB)";
}

/// <summary>
/// Finds where the game keeps its data, by scanning the emulated address space over the RPC.
/// </summary>
/// <remarks>
/// The method is the classic one: read a value now, have the player change it in game, read
/// again, keep only the addresses that changed the way they should. Nothing here assumes any
/// address in advance — every offset PermaLocke ends up using must be discovered and verified.
/// </remarks>
public sealed class MemorySearch(AzaharRpcClient client)
{
    private const int PageSize = 0x1000;

    /// <summary>
    /// Regions worth scanning on a 3DS: the application heap and the linear heap. Chosen from
    /// where the reference implementation's own scan log found its hits, not from guesswork.
    /// </summary>
    /// <remarks>
    /// Extents confirmed with the coarse sweep against a running Ultra Moon: 32 MB of heap
    /// and 64 MB of linear heap. The 0x14000000 range is left out because it aliases the same
    /// physical memory and would only produce duplicate hits.
    /// </remarks>
    public static IReadOnlyList<MemoryRegion> DefaultRegions { get; } =
    [
        new(0x08000000, 0x10000000, "heap"),
        new(0x30000000, 0x40000000, "linear")
    ];

    /// <summary>
    /// The extents where live game state has actually been found, as opposed to the wider
    /// ranges the emulator will answer reads for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything located so far sits inside these 64 MB of linear heap: the party copies between
    /// 0x3002E258 and 0x33F7FA44, the bag block at 0x33011934, the wild Pokémon copies around
    /// 0x3254F4AC and the battle flags at 0x330D6CA8. The emulator answers reads across the whole
    /// of 0x30000000-0x40000000, so sweeping that instead costs four times as long for memory
    /// that has never held anything.
    /// </para>
    /// <para>
    /// <b>The application heap at 0x08000000 was here too, and it is out since 2026-09-21</b>, because
    /// searching it crashed the emulator. Opening the game, the party key search of §152 ran while the
    /// game was still playing its intro, when most of that heap is not mapped yet: 28,871 «unmapped
    /// ReadBlock» lines from 0x08420000 to 0x09AB4000 in a third of a second, and Azahar died on the
    /// last of them. Reading memory that does not exist had frozen it before (§114 ter). And it bought
    /// nothing: in every log of every run, the heap's only hit was on 2026-08-18, a «party» of one
    /// Pokémon whose trainer name was noise, and a Rare Candy false positive at 0x081D55B0 (§22).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MemoryRegion> LiveStateRegions { get; } =
    [
        new(0x30000000, 0x34000000, "linear")
    ];

    /// <summary>
    /// Wider sweep for a first pass, covering the address ranges a 3DS title can use.
    /// Probed coarsely, because most of it is unmapped and every miss costs a timeout.
    /// </summary>
    public static IReadOnlyList<MemoryRegion> WideRegions { get; } =
    [
        new(0x00100000, 0x00A00000, "code"),
        new(0x08000000, 0x0A000000, "heap"),
        new(0x0C000000, 0x0E000000, "heap-alt"),
        new(0x14000000, 0x1C000000, "linear-alt"),
        new(0x1E800000, 0x1F000000, "vram"),
        new(0x30000000, 0x34000000, "linear")
    ];

    /// <summary>Coarse pass: which megabytes answer at all.</summary>
    public IReadOnlyList<uint> MapCoarse(MemoryRegion region, uint step = 0x100000)
    {
        var mapped = new List<uint>();

        for (var address = region.Start; address < region.End; address += step)
        {
            if (client.TryReadMemory(address, 4, out _))
            {
                mapped.Add(address);
            }
        }

        return mapped;
    }

    /// <summary>Probes four bytes per page to find out what is actually mapped.</summary>
    public IReadOnlyList<MemoryRegion> MapReadableRanges(MemoryRegion region)
    {
        var readable = new List<MemoryRegion>();
        uint? runStart = null;

        for (var address = region.Start; address < region.End; address += PageSize)
        {
            if (client.TryReadMemory(address, 4, out _))
            {
                runStart ??= address;
            }
            else if (runStart is { } start)
            {
                readable.Add(new MemoryRegion(start, address, region.Label));
                runStart = null;
            }
        }

        if (runStart is { } tail)
        {
            readable.Add(new MemoryRegion(tail, region.End, region.Label));
        }

        return readable;
    }

    /// <summary>Every address in the readable ranges holding this 16 bit value.</summary>
    public List<uint> ScanUInt16(IEnumerable<MemoryRegion> regions, ushort value)
    {
        var matches = new List<uint>();

        foreach (var (address, chunk) in ReadChunks(regions))
        {
            for (var offset = 0; offset + 2 <= chunk.Length; offset += 2)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(chunk.AsSpan(offset)) == value)
                {
                    matches.Add((uint)(address + offset));
                }
            }
        }

        return matches;
    }

    /// <summary>Keeps only the candidates that now hold <paramref name="value"/>.</summary>
    public List<uint> Refine(IEnumerable<uint> candidates, ushort value)
    {
        var survivors = new List<uint>();

        foreach (var address in candidates)
        {
            if (client.TryReadMemory(address, 2, out var data)
                && BinaryPrimitives.ReadUInt16LittleEndian(data) == value)
            {
                survivors.Add(address);
            }
        }

        return survivors;
    }

    /// <summary>
    /// Finds UTF-16LE text, which is how the 3DS stores trainer names and nicknames. Usually
    /// the fastest way in: a nickname pins down the party block on the first try.
    /// </summary>
    public List<uint> ScanText(IEnumerable<MemoryRegion> regions, string text)
    {
        var needle = Encoding.Unicode.GetBytes(text);
        var matches = new List<uint>();

        foreach (var (address, chunk) in ReadChunks(regions))
        {
            for (var offset = 0; offset + needle.Length <= chunk.Length; offset += 2)
            {
                if (chunk.AsSpan(offset, needle.Length).SequenceEqual(needle))
                {
                    matches.Add((uint)(address + offset));
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Reads a region in blocks. Blocks overlap by two bytes so a value straddling a boundary
    /// is not missed.
    /// </summary>
    private IEnumerable<(uint Address, byte[] Data)> ReadChunks(IEnumerable<MemoryRegion> regions)
    {
        const int blockSize = 0x1000;

        // An uninterrupted burst of tens of thousands of UDP requests has been observed to
        // take the emulator down. Yielding the thread periodically keeps it responsive.
        var requests = 0;

        foreach (var region in regions)
        {
            for (var address = region.Start; address < region.End; address += blockSize - 2)
            {
                var size = (int)Math.Min(blockSize, region.End - address);

                if (client.TryReadMemory(address, size, out var data))
                {
                    yield return (address, data);
                }

                if (++requests % 256 == 0)
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}
