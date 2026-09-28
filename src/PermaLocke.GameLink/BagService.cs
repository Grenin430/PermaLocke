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
    ILogger<BagService> logger,
    Field.SavedGameCache? saved = null) : IItemWithholder
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

    /// <summary>
    /// Heart Scale, confirmed against the cartridge item list. It sits at the end of the treasure
    /// run the games have kept in order since generation three -- 88 Perla, 89 Perla Grande,
    /// 90 Polvo Estelar, 92 Pepita de Oro, 93 Escama Corazón -- which is what anchors it.
    /// </summary>
    /// <remarks>
    /// An ordinary item, so the pocket stacks it: pressing twice really does hand over twenty.
    /// The move relearner charges one per move, and a Nuzlocke wants them right before the league.
    /// </remarks>
    public const int HeartScaleItemId = 93;

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

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, (DateTime At, int Count)> _ownWrites = new();

    /// <summary>When PermaLocke itself last wrote an item (returned balls, shop, gifts) and the count it left.</summary>
    public (DateTime At, int Count) LastOwnWrite(int itemId) => _ownWrites.GetValueOrDefault(itemId, (DateTime.MinValue, -1));

    /// <summary>
    /// The bag's contents only if it is already located and still valid, else null: never sweeps (2026-09-28, the item
    /// animation reads it every couple of seconds, and a sweep is what brings Azahar down).
    /// </summary>
    public IReadOnlyList<BagSlot>? ReadKnown() =>
        _block is { } block && _locator.StillValid(block) ? _locator.ReadContents(block) : null;

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

        _ownWrites[itemId] = (DateTime.UtcNow, wanted);
        if (!writer.SetBagSlot(address, entry))
        {
            return new BagWriteResult(BagWriteOutcome.NotApplied, itemId, previous, previous, address);
        }

        logger.LogInformation("Mochila: objeto {Item} de {Previous} a {Applied} en 0x{Address:X8}",
            itemId, previous, wanted, address);

        return new BagWriteResult(BagWriteOutcome.Ok, itemId, previous, wanted, address);
    }

    /// <summary>
    /// Takes away everything the player carries of an item, adding it to what is already owed.
    /// </summary>
    /// <remarks>
    /// Adding and not replacing: balls bought or picked up while the rest were withheld are taken too, and
    /// the first batch is still owed. Replacing is what the first version did, and it would have lost the
    /// first batch the second time round.
    /// </remarks>
    public BagWriteResult Withhold(Guid run, int itemId, CancellationToken ct = default)
    {
        var carried = CountOf(itemId, ct);

        if (carried <= 0)
        {
            return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, 0, 0, 0);
        }

        var owedBefore = ReloadUndidTheDebt(run, itemId, carried) ? 0 : Owed(run, itemId);

        // Se apunta ANTES de tocar nada: si la app muere entre ambas cosas, lo peor que pasa
        // es que se devuelvan objetos que nunca se llegaron a quitar.
        _ledger.Remember(run, itemId, owedBefore + carried);

        var result = SetCount(itemId, 0, ct);

        if (result.Outcome != BagWriteOutcome.Ok)
        {
            _ledger.Remember(run, itemId, owedBefore);
        }

        return result;
    }

    /// <summary>
    /// Gives back what is owed on top of what the player carries now.
    /// </summary>
    /// <remarks>
    /// On top: the first version wrote the owed amount as the new count, so five balls picked up while the
    /// rest were withheld vanished the moment the rest came back. What does not fit in the pocket stays owed
    /// instead of being dropped.
    /// </remarks>
    public BagWriteResult GiveBack(Guid run, int itemId, CancellationToken ct = default)
    {
        var owed = Owed(run, itemId);

        if (owed <= 0)
        {
            return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, 0, 0, 0);
        }

        var current = CountOf(itemId, ct);

        // El juego ya se las ha devuelto él solo al recargar, y devolverlas otra vez las duplicaría. No es un
        // caso raro: es lo que pasa cada vez que alguien cierra el emulador sin guardar con la ruta gastada.
        if (ReloadUndidTheDebt(run, itemId, current))
        {
            _ledger.Remember(run, itemId, 0);

            return new BagWriteResult(BagWriteOutcome.NothingToDo, itemId, current, current, 0);
        }

        var result = SetCount(itemId, current + owed, ct);

        if (result.Outcome == BagWriteOutcome.Ok)
        {
            _ledger.Remember(run, itemId, Math.Max(0, owed - (result.Applied - current)));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<int, int> CarriedAll(IReadOnlyCollection<int> itemIds)
    {
        if (Locate() is not { } block)
        {
            return new Dictionary<int, int>();
        }

        var slots = _locator.ReadContents(block);

        return itemIds.Distinct().ToDictionary(
            id => id,
            id => slots.Where(slot => slot.Entry.ItemId == id).Sum(slot => slot.Entry.Count));
    }

    /// <summary>How much of an item <paramref name="run"/> has withheld and owes back.</summary>
    public int Owed(Guid run, int itemId) => _ledger.Owed(run, itemId);

    /// <inheritdoc />
    public int Carried(int itemId) => CountOf(itemId);

    /// <inheritdoc />
    bool IItemWithholder.Withhold(Guid run, int itemId) => Withhold(run, itemId).Outcome == BagWriteOutcome.Ok;

    /// <inheritdoc />
    bool IItemWithholder.GiveBack(Guid run, int itemId) => GiveBack(run, itemId).Outcome == BagWriteOutcome.Ok;

    /// <summary>
    /// Whether the debt already on the books was undone by the player reloading, and so must not be added to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Balls are taken out of the <b>live</b> bag. If the player reloads before saving, the game puts them back on
    /// its own — and the app, seeing a full bag in a spent route, takes them again. <see cref="Withhold"/> adds
    /// (§147, and rightly: balls bought while the rest were away are not lost), so the same ten get counted twice.
    /// </para>
    /// <para>
    /// Measured on 2026-09-21: taken at 02:50:49 and again at 02:51:44 with the game reloaded in between, and the
    /// ledger ended up owing <b>20 Poké Ball and 20 Super Ball</b> to a player who had only ever had ten of each.
    /// The saved game, written at 02:50:44, still had all twenty.
    /// </para>
    /// <para>
    /// Two questions, and both have to say yes, because either alone is wrong:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Was the debt written after the last save?</b> If the save is newer, the withholding is in it: the balls
    /// really are gone and really are owed.
    /// </item>
    /// <item>
    /// <b>Is the bag holding exactly what the save holds?</b> That is what a reload leaves behind. Without this,
    /// a player who simply found more balls in the same session would have the first batch written off — the save
    /// is older there too.
    /// </item>
    /// </list>
    /// <para>
    /// It corrects itself either way: if a reload happens later, the next withholding sees the bag match the save
    /// and settles the books then. And with no readable save it answers no, which is today's behaviour.
    /// </para>
    /// </remarks>
    private bool ReloadUndidTheDebt(Guid run, int itemId, int carried)
    {
        if (saved is null)
        {
            return false;
        }

        var game = saved.Load();

        var inTheSave = game?.Save.Inventory.Pouches
            .SelectMany(pouch => pouch.Items)
            .Where(item => item.Index == itemId)
            .Sum(item => item.Count);

        var undone = DebtWasUndoneByAReload(
            _ledger.Owed(run, itemId),
            _ledger.WrittenAt(run),
            game is null ? null : new DateTimeOffset(game.Value.WrittenAt, TimeSpan.Zero),
            carried,
            inTheSave);

        if (undone)
        {
            logger.LogWarning(
                "El objeto {Objeto} vuelve a estar en la mochila y la partida guardada tiene los mismos {Cantidad}: "
                + "la retirada anterior nunca llegó a guardarse, así que no se suma a lo que ya se debía",
                itemId, carried);
        }

        return undone;
    }

    /// <summary>The decision on its own, so it can be checked without a save or an emulator.</summary>
    /// <param name="owed">What the ledger already says is owed of this item.</param>
    /// <param name="debtWrittenAt">When that was written down, or null when it is not known.</param>
    /// <param name="saveWrittenAt">When the player last saved, or null when the save cannot be read.</param>
    /// <param name="carried">What the live bag holds now.</param>
    /// <param name="inTheSave">What the saved game holds, or null when it cannot be read.</param>
    /// <remarks>
    /// <b>The bag has to hold some.</b> A reload is only visible when it brings the balls back. With the bag at zero and
    /// the save at zero the two «match» and prove nothing — and that is exactly the bag right after a withholding, when
    /// the balls were picked up after the last save. Found in the friends' test folder on 2026-09-21: the rule withheld
    /// ten Poké Balls in a spent route, the player walked into the next route, and instead of giving them back it
    /// wrote the debt off as «undone by a reload». The same line is in the player's own log that morning. Losing balls
    /// stops a game; the one case this now gets wrong — a reload to a save from before the balls were given, whose
    /// story then gives them again — only hands over ten more.
    /// </remarks>
    internal static bool DebtWasUndoneByAReload(int owed, DateTimeOffset? debtWrittenAt,
        DateTimeOffset? saveWrittenAt, int carried, int? inTheSave) =>
        owed > 0
        && carried > 0
        && debtWrittenAt is { } wroteDebt
        && saveWrittenAt is { } wroteSave
        && wroteDebt > wroteSave
        && inTheSave == carried;

    /// <inheritdoc />
    /// <remarks>The bag is not touched: the whole point is that what is owed never left the saved game.</remarks>
    public int Forget(Guid run, int itemId)
    {
        var owed = Owed(run, itemId);

        if (owed > 0)
        {
            _ledger.Remember(run, itemId, 0);
            logger.LogWarning("Se da por saldado lo retenido de {Objeto}: {Cantidad}", itemId, owed);
        }

        return owed;
    }

    /// <summary>What is owed, and to which run (§147).</summary>
    private readonly WithheldLedger _ledger = new(statePath, logger);
}
