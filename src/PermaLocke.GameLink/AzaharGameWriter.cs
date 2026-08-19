using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <param name="Nickname">Shown in the game, so the player sees the Pokémon is gone.</param>
public sealed record DeathTransform(
    int Species = 292,
    string Nickname = "MUERTO",
    int Level = 1,
    int Ability = 0,
    bool ClearMoves = true);

/// <summary>
/// Writes into the running game.
/// </summary>
/// <remarks>
/// Every write is preceded by a backup of the exact bytes being replaced, kept on disk, so a
/// mistake is always undoable. Only the bytes that differ are sent, which keeps the window in
/// which the structure is half-written as small as possible.
///
/// Two facts about the game shape this class, both established by experiment:
/// the block is stored **encrypted**, so originals and modifications must be compared in that
/// same form; and level and battle stats are derived fields the game recomputes when a battle
/// starts, so a level change is not visible until then.
/// </remarks>
public sealed class AzaharGameWriter(
    AzaharRpcClient client,
    string backupFolder,
    ILogger<AzaharGameWriter> logger)
{
    private static readonly int PartySize = new PK7().SIZE_PARTY;

    /// <summary>Turns a Pokémon into the run's death marker. Returns false if nothing was written.</summary>
    public bool ApplyDeath(uint slotAddress, DeathTransform transform)
    {
        return Modify(slotAddress, "muerte", pokemon =>
        {
            pokemon.Species = (ushort)transform.Species;
            pokemon.Form = 0;
            pokemon.Ability = transform.Ability;
            pokemon.CurrentLevel = (byte)transform.Level;

            if (transform.ClearMoves)
            {
                pokemon.Move1 = pokemon.Move2 = pokemon.Move3 = pokemon.Move4 = 0;
                pokemon.Move1_PP = pokemon.Move2_PP = pokemon.Move3_PP = pokemon.Move4_PP = 0;
                pokemon.Move1_PPUps = pokemon.Move2_PPUps = pokemon.Move3_PPUps = pokemon.Move4_PPUps = 0;
            }

            pokemon.Nickname = transform.Nickname;
            pokemon.IsNicknamed = true;
        });
    }

    /// <summary>
    /// Brings a Pokémon back to the level cap with no progress into the next level. This covers
    /// the three cases the run defines — rare candies, experience gained at the cap, and
    /// levelling past it — because all three end in the same state.
    /// </summary>
    public bool EnforceLevelCap(uint slotAddress, int cap)
    {
        var current = Read(slotAddress);

        if (current is null || current.CurrentLevel <= cap)
        {
            return false;
        }

        return Modify(slotAddress, $"cap de nivel {cap}", pokemon => pokemon.CurrentLevel = (byte)cap);
    }

    /// <summary>
    /// Writes one bag slot: backs the word up, writes it, and reads it back to confirm.
    /// </summary>
    /// <remarks>
    /// The read back is what keeps the caller honest. The official Azahar refuses to write to
    /// the region the bag lives in and answers OK anyway, so without re-reading, a button
    /// would report success on an emulator where nothing happened.
    ///
    /// The whole word is written, flags included, so callers must pass an entry derived from
    /// the one already there. Rebuilding it as id and count alone wipes the free space index
    /// and the "new" badge the game keeps in the upper bits.
    /// </remarks>
    public bool SetBagSlot(uint address, BagEntry entry)
    {
        if (!client.TryReadMemory(address, 4, out var before))
        {
            logger.LogWarning("No se pudo leer la entrada de mochila en 0x{Address:X8}", address);
            return false;
        }

        Backup(address, before, "mochila");

        var packed = entry.Pack();
        client.WriteMemory(address, BitConverter.GetBytes(packed));

        if (!client.TryReadMemory(address, 4, out var after) || BitConverter.ToUInt32(after) != packed)
        {
            logger.LogWarning(
                "La escritura en 0x{Address:X8} no ha cuajado: se pidió {Wanted:X8} y quedó {Got}. "
                + "El Azahar oficial acepta la escritura y no la aplica; hace falta el fork.",
                address, packed, after.Length == 4 ? BitConverter.ToUInt32(after).ToString("X8") : "ilegible");
            return false;
        }

        logger.LogInformation("Mochila 0x{Address:X8}: objeto {Item} x{Count}",
            address, entry.ItemId, entry.Count);

        return true;
    }

    public PK7? Read(uint slotAddress) =>
        client.TryReadMemory(slotAddress, PartySize, out var bytes) ? new PK7(bytes) : null;

    /// <summary>Puts back exactly what was there, from the newest backup of that address.</summary>
    public bool Restore(uint address)
    {
        var file = Directory.Exists(backupFolder)
            ? Directory.GetFiles(backupFolder, $"{address:X8}-*.bin").OrderBy(f => f).LastOrDefault()
            : null;

        if (file is null)
        {
            return false;
        }

        client.WriteMemory(address, File.ReadAllBytes(file));
        logger.LogInformation("Restaurado 0x{Address:X8} desde {File}", address, Path.GetFileName(file));
        return true;
    }

    private bool Modify(uint slotAddress, string reason, Action<PK7> change)
    {
        if (!client.TryReadMemory(slotAddress, PartySize, out var original))
        {
            logger.LogWarning("No se pudo leer el Pokémon en 0x{Address:X8}", slotAddress);
            return false;
        }

        // PK7 wraps the array it is given, so each view needs its own copy.
        var reference = new byte[PartySize];
        new PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

        var working = new PK7((byte[])original.Clone());

        if (!working.ChecksumValid)
        {
            logger.LogWarning("El bloque en 0x{Address:X8} no es un Pokémon válido; no se escribe",
                slotAddress);
            return false;
        }

        change(working);
        working.RefreshChecksum();

        var modified = new byte[PartySize];
        working.WriteEncryptedDataParty(modified);

        Backup(slotAddress, original, reason);

        var written = 0;

        for (var i = 0; i < PartySize; i++)
        {
            if (modified[i] != reference[i])
            {
                client.WriteMemory((uint)(slotAddress + i), [modified[i]]);
                written++;
            }
        }

        logger.LogInformation("0x{Address:X8}: {Reason}, {Written} bytes escritos",
            slotAddress, reason, written);

        return written > 0;
    }

    private void Backup(uint address, byte[] bytes, string reason)
    {
        try
        {
            Directory.CreateDirectory(backupFolder);
            var name = $"{address:X8}-{DateTime.Now:yyyyMMdd-HHmmss}-{reason.Replace(' ', '_')}.bin";
            File.WriteAllBytes(Path.Combine(backupFolder, name), bytes);
        }
        catch (IOException ex)
        {
            // A failed backup must stop the write: never modify what cannot be undone.
            logger.LogError(ex, "No se pudo guardar la copia de seguridad de 0x{Address:X8}", address);
            throw;
        }
    }
}
