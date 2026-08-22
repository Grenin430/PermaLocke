using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>Why a bag write did or did not happen. The UI reports this, it never guesses.</summary>
public enum BagWriteOutcome
{
    /// <summary>Written and read back with the expected value.</summary>
    Ok,

    /// <summary>The bag block is not in memory: emulator closed, or no game loaded.</summary>
    BagNotFound,

    /// <summary>No pocket of the bag accepts that item id.</summary>
    UnknownPocket,

    /// <summary>The item is not there and its pocket has no free slot left.</summary>
    PocketFull,

    /// <summary>The write was sent and the memory did not change. Almost always: not the fork.</summary>
    NotApplied,

    /// <summary>Nothing to do, so nothing was written.</summary>
    NothingToDo
}

/// <param name="Previous">Quantity before the write.</param>
/// <param name="Applied">Quantity after it.</param>
public sealed record BagWriteResult(
    BagWriteOutcome Outcome, int ItemId, int Previous, int Applied, uint Address)
{
    public bool Succeeded => Outcome is BagWriteOutcome.Ok or BagWriteOutcome.NothingToDo;

    internal static BagWriteResult Failed(BagWriteOutcome outcome, int itemId) =>
        new(outcome, itemId, 0, 0, 0);
}

/// <summary>
/// Reads and writes the player bag in the running game.
/// </summary>
/// <remarks>
/// Everything goes through the bag block found by <see cref="BagLocator"/>, so an item the
/// player does not carry can be added — it goes into the first free slot of its pocket —
/// instead of the old behaviour, which could only rewrite a value it had managed to find and
/// therefore could not grant anything from zero.
///
/// Items taken away are written to disk before anything is touched. If PermaLocke is closed
/// or crashes while holding them, the next run still knows what it owes: nobody is left
/// without their Poké Balls because the app died at the wrong moment.
/// </remarks>
public sealed class BagService(
    AzaharRpcClient client,
    AzaharGameWriter writer,
    string statePath,
    string knownAddressPath,
    ILogger<BagService> logger) : IItemWithholder
{
    /// <summary>Poké Ball, confirmed against the PKHeX item table.</summary>
    public const int PokeBallItemId = 4;

    /// <summary>Most of one item a slot can hold: the quantity field is ten bits wide. The
    /// pocket may cap it lower still — medicines stop at 999 — and SetCount applies that.</summary>
    public const int MaxItemCount = BagEntry.MaxCount;

    /// <summary>Rare Candy, confirmed against the cartridge item list.</summary>
    public const int RareCandyItemId = 50;

    /// <summary>
    /// Shiny Charm, confirmed against the cartridge item list — 632, with the Oval Charm at 631
    /// beside it, which is the pairing the games have used since generation five.
    /// </summary>
    /// <remarks>A key item, so the pocket holds exactly one however many times it is given.</remarks>
    public const int ShinyCharmItemId = 632;

    private readonly BagLocator _locator = new(client);
    private BagBlock? _block;

    /// <summary>The bag block currently cached, if it has been located.</summary>
    public BagBlock? Block => _block;

    /// <summary>
    /// The bag block, located once and re-checked afterwards. The check is a single read of
    /// the pointer table, so it costs nothing to distrust the cached address every time.
    /// </summary>
    /// <remarks>
    /// The address of the last session is tried before sweeping anything. A sweep is 24.000
    /// requests, and the emulator writes a log line per reply — a single sweep left a 15 MB
    /// trail in the emulator log — so it is worth avoiding whenever the previous address still
    /// holds. Trusting it is safe because it is not trusted: the pointer table has to check out
    /// before the address is used, exactly as if it had just been found.
    /// </remarks>
    public BagBlock? Locate(CancellationToken ct = default)
    {
        if (_block is { } cached && _locator.StillValid(cached))
        {
            return cached;
        }

        if (Remembered() is { } remembered && _locator.StillValid(remembered))
        {
            _block = remembered;
            logger.LogInformation("Mochila en 0x{Address:X8}, la de la última vez, revalidada sin barrer",
                remembered.BaseAddress);

            return _block;
        }

        _block = _locator.Locate(ct);

        if (_block is null)
        {
            logger.LogWarning("No se ha encontrado el bloque de la mochila en la memoria del juego");
        }
        else
        {
            RememberAddress(_block.BaseAddress);
            logger.LogInformation("Mochila localizada en 0x{Address:X8} (tabla en 0x{Table:X8})",
                _block.BaseAddress, _block.PointerTableAddress);
        }

        return _block;
    }

    /// <summary>Address found last time, if any. Never used without revalidating it.</summary>
    private BagBlock? Remembered()
    {
        try
        {
            if (!File.Exists(knownAddressPath))
            {
                return null;
            }

            var text = File.ReadAllText(knownAddressPath).Trim();

            return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var address)
                ? new BagBlock(address, BagLayout.UltraSunMoon)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void RememberAddress(uint address)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(knownAddressPath)!);
            File.WriteAllText(knownAddressPath, address.ToString("X8"));
        }
        catch (IOException ex)
        {
            // No es grave: sin el fichero, la próxima vez se vuelve a barrer.
            logger.LogWarning(ex, "No se pudo recordar la dirección de la mochila");
        }
    }

    /// <summary>Everything the player is carrying, or an empty list if the bag is not found.</summary>
    public IReadOnlyList<BagSlot> Read(CancellationToken ct = default) =>
        Locate(ct) is { } block ? _locator.ReadContents(block) : [];

    /// <summary>How many of an item the player carries right now.</summary>
    public int CountOf(int itemId, CancellationToken ct = default) =>
        Locate(ct) is { } block ? _locator.Find(block, itemId)?.Entry.Count ?? 0 : 0;

    /// <summary>
    /// How many of an item the bag will hold, or zero when no pocket takes it.
    /// </summary>
    /// <remarks>
    /// Lets a caller tell "already at the maximum" from "the write failed" before writing
    /// anything. A key item stops at one, so handing one to somebody who has it would otherwise
    /// look exactly like a write the emulator dropped.
    /// </remarks>
    public int CapacityFor(int itemId, CancellationToken ct = default) =>
        Locate(ct) is { } block && block.Layout.PocketFor(itemId) is { } pocket
            ? Math.Min(pocket.MaxCount, BagEntry.MaxCount)
            : 0;

    /// <summary>
    /// Sets how many of an item the player carries. Adds it when they carry none, removes it
    /// when the count is zero, and reports exactly what happened.
    /// </summary>
    public BagWriteResult SetCount(int itemId, int count, CancellationToken ct = default)
    {
        if (Locate(ct) is not { } block)
        {
            return BagWriteResult.Failed(BagWriteOutcome.BagNotFound, itemId);
        }

        if (block.Layout.PocketFor(itemId) is not { } pocket)
        {
            return BagWriteResult.Failed(BagWriteOutcome.UnknownPocket, itemId);
        }

        // Los objetos clave, las MT y los cristales Z solo admiten uno.
        var wanted = Math.Clamp(count, 0, Math.Min(pocket.MaxCount, BagEntry.MaxCount));
        var current = _locator.Find(block, itemId);
        var previous = current?.Entry.Count ?? 0;

        uint address;
        BagEntry entry;

        if (current is not null)
        {
            // Se conserva el resto de la palabra: la cantidad es lo único que cambia.
            address = current.Address;
            entry = wanted == 0 ? BagEntry.Empty : current.Entry.WithCount(wanted);
        }
        else
        {
            if (wanted == 0)
            {
                return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, 0, 0, 0);
            }

            if (_locator.FirstFreeSlot(block, pocket) is not { } free)
            {
                return BagWriteResult.Failed(BagWriteOutcome.PocketFull, itemId);
            }

            // Bandera y hueco libre a cero, que es como está toda entrada de la mochila real.
            address = block.AddressOf(pocket, free);
            entry = new BagEntry(itemId, wanted, 0, false);
        }

        if (!writer.SetBagSlot(address, entry))
        {
            return new BagWriteResult(BagWriteOutcome.NotApplied, itemId, previous, previous, address);
        }

        logger.LogInformation("Mochila: objeto {Item} de {Previous} a {Applied} en 0x{Address:X8}",
            itemId, previous, wanted, address);

        return new BagWriteResult(BagWriteOutcome.Ok, itemId, previous, wanted, address);
    }

    /// <summary>Takes an item away from the player, remembering how many were taken.</summary>
    public BagWriteResult Withhold(int itemId, CancellationToken ct = default)
    {
        var carried = CountOf(itemId, ct);

        if (carried <= 0)
        {
            return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, 0, 0, 0);
        }

        // Se apunta ANTES de tocar nada: si la app muere entre ambas cosas, lo peor que pasa
        // es que se devuelvan objetos que nunca se llegaron a quitar.
        Remember(itemId, carried);

        var result = SetCount(itemId, 0, ct);

        if (!result.Succeeded)
        {
            Remember(itemId, 0);
        }

        return result;
    }

    /// <summary>Gives back exactly what was withheld, if anything was.</summary>
    public BagWriteResult GiveBack(int itemId, CancellationToken ct = default)
    {
        var owed = Owed(itemId);

        if (owed <= 0)
        {
            return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, 0, 0, 0);
        }

        var result = SetCount(itemId, owed, ct);

        if (result.Succeeded)
        {
            Remember(itemId, 0);
        }

        return result;
    }

    /// <summary>How much of an item is currently being withheld.</summary>
    public int Owed(int itemId)
    {
        if (!File.Exists(statePath))
        {
            return 0;
        }

        foreach (var line in File.ReadAllLines(statePath))
        {
            var parts = line.Split('=');

            if (parts.Length == 2 && int.TryParse(parts[0], out var id) && id == itemId
                && int.TryParse(parts[1], out var amount))
            {
                return amount;
            }
        }

        return 0;
    }


    /// <inheritdoc />
    public int Carried(int itemId) => CountOf(itemId);

    /// <inheritdoc />
    bool IItemWithholder.Withhold(int itemId) => Withhold(itemId).Succeeded;

    /// <inheritdoc />
    bool IItemWithholder.GiveBack(int itemId) => GiveBack(itemId).Succeeded;
    private void Remember(int itemId, int amount)
    {
        var lines = File.Exists(statePath)
            ? File.ReadAllLines(statePath).Where(l => !l.StartsWith($"{itemId}=")).ToList()
            : [];

        if (amount > 0)
        {
            lines.Add($"{itemId}={amount}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllLines(statePath, lines);
    }
}
