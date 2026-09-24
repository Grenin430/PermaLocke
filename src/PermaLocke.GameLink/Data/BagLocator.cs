using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Data;

/// <summary>The bag block found in the running game.</summary>
public sealed record BagBlock(uint BaseAddress, BagLayout Layout)
{
    /// <summary>The game keeps the pocket pointer table immediately after the block.</summary>
    public uint PointerTableAddress => BaseAddress + (uint)Layout.BlockSize;

    public uint AddressOf(BagPocket pocket, int slot) =>
        BaseAddress + (uint)(pocket.Offset + (slot * 4));
}

/// <param name="Index">Slot number inside its pocket.</param>
public sealed record BagSlot(BagPocket Pocket, int Index, uint Address, BagEntry Entry);

/// <summary>
/// Finds the bag in the running game by its structure, not by hunting for a value.
/// </summary>
/// <remarks>
/// <para>
/// The earlier version searched memory for the word that decodes as a given item, and decided
/// with a neighbourhood heuristic whether the hit looked like a bag entry. It produced false
/// positives — with no Rare Candies in the bag at all it reported one at 0x081D55B0 — and a
/// write there would have gone into unknown memory while the button claimed success.
/// </para>
/// <para>
/// What replaces it: the game stores the bag as one contiguous block of pockets and keeps a
/// table of pointers into that block immediately after it, so a candidate is accepted only
/// when every one of those pointers lands exactly on its own pocket. Swept over the 96 MB the
/// game keeps its state in, it yields exactly one block and zero false positives, at the same
/// address the earlier investigation had reached by hand.
/// </para>
/// </remarks>
public sealed class BagLocator(AzaharRpcClient client, BagLayout? layout = null)
{
    private const int PageSize = 0x1000;
    private const int WindowSize = 0x10000;

    private readonly BagLayout _layout = layout ?? BagLayout.UltraSunMoon;

    /// <summary>
    /// Sweeps memory for bag blocks. Costs about six seconds and tens of thousands of requests,
    /// so callers cache the address and re-check it with <see cref="StillValid"/> instead.
    /// </summary>
    public IReadOnlyList<BagBlock> LocateAll(CancellationToken ct = default)
    {
        var selectedProcess = client.GetProcess();
        if (selectedProcess == uint.MaxValue)
            throw new AzaharRpcException("No hay un proceso seleccionado para buscar la mochila.");
        var found = new List<BagBlock>();
        var tableBytes = _layout.PointerTableBytes;
        var buffer = new byte[WindowSize + tableBytes];
        var requests = 0;

        foreach (var region in MemorySearch.LiveStateRegions)
        {
            for (var offset = 0u; offset < region.Size; offset += WindowSize)
            {
                ct.ThrowIfCancellationRequested();

                // La ventana se lee con cola, porque la tabla puede quedar partida entre dos.
                if (client.GetProcess() != selectedProcess)
                    throw new AzaharRpcException("El proceso del juego cambió durante la búsqueda de la mochila.");
                var length = (int)Math.Min(buffer.Length, region.Size - offset);
                ReadWindow(region.Start + offset, buffer, length, ref requests, ct);

                for (var at = 0; at < WindowSize && at + tableBytes <= length; at += 4)
                {
                    if (_layout.TryMatchPointerTable(region.Start + offset + (uint)at,
                            buffer.AsSpan(at), out var baseAddress))
                    {
                        found.Add(new BagBlock(baseAddress, _layout));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>The bag block, or null when it is not there.</summary>
    public BagBlock? Locate(CancellationToken ct = default) => LocateAll(ct).FirstOrDefault();

    /// <summary>
    /// Re-checks a cached block with a single read of the pointer table. Cheap enough to run
    /// before every write, which is the point: the address is never trusted twice on faith.
    /// </summary>
    public bool StillValid(BagBlock block) =>
        client.TryReadMemory(block.PointerTableAddress, _layout.PointerTableBytes, out var table)
        && _layout.TryMatchPointerTable(block.PointerTableAddress, table, out var found)
        && found == block.BaseAddress;

    /// <summary>Every occupied slot of the bag, pocket by pocket.</summary>
    public IReadOnlyList<BagSlot> ReadContents(BagBlock block) =>
        client.TryReadMemory(block.BaseAddress, _layout.BlockSize, out var data)
            ? _layout.DecodeBlock(block.BaseAddress, data)
            : [];

    /// <summary>Where an item sits in the bag right now, or null when the player has none.</summary>
    public BagSlot? Find(BagBlock block, int itemId) =>
        ReadContents(block).FirstOrDefault(slot => slot.Entry.ItemId == itemId);

    /// <summary>
    /// First free slot of a pocket, so an item the player does not carry can still be added.
    /// Returns null when the pocket is full.
    /// </summary>
    public int? FirstFreeSlot(BagBlock block, BagPocket pocket)
    {
        if (!client.TryReadMemory(block.AddressOf(pocket, 0), pocket.Bytes, out var data))
        {
            return null;
        }

        for (var slot = 0; slot < pocket.Slots; slot++)
        {
            if (BagEntry.Unpack(BitConverter.ToUInt32(data, slot * 4)).IsEmpty)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// Fills the window page by page. A page that cannot be read is left as zeros instead of
    /// losing the whole window, because unmapped pages are scattered all over the regions.
    /// </summary>
    private void ReadWindow(uint address, byte[] buffer, int length, ref int requests, CancellationToken ct)
    {
        Array.Clear(buffer, 0, length);

        for (var offset = 0; offset < length; offset += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            var size = Math.Min(PageSize, length - offset);

            if (client.TryReadMemory(address + (uint)offset, size, out var page))
            {
                page.CopyTo(buffer.AsSpan(offset));
            }

            // Una ráfaga ininterrumpida de peticiones ha llegado a tumbar el emulador, y además
            // el servidor RPC escribe una línea de log por respuesta: un barrido deja unos 15 MB
            // en el log de Azahar. Se cede el hilo a menudo y se barre lo menos posible.
            if (++requests % 64 == 0)
            {
                if (ct.WaitHandle.WaitOne(5)) ct.ThrowIfCancellationRequested();
            }
        }
    }
}
