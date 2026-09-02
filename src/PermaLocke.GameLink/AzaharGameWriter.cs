using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// What happened to a write into the game's memory.
/// </summary>
/// <param name="Written">Bytes that differed from what was there and were sent.</param>
/// <param name="Verified">How many of those the game really holds afterwards.</param>
/// <remarks>
/// The two are separate because they disagree, and that disagreement is the whole point. The
/// official Azahar accepts writes to some regions and never applies them, answering OK either
/// way, so a writer that does not read back reports success it has not earned.
/// </remarks>
public readonly record struct MemoryWriteResult(int Written, int Verified)
{
    public static MemoryWriteResult Nothing => default;

    /// <summary>Everything that was sent is now in the game.</summary>
    public bool Applied => Written > 0 && Verified == Written;

    /// <summary>Bytes were sent and the game did not take them.</summary>
    public bool Rejected => Written > 0 && Verified < Written;
}

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

    /// <summary>The encrypted block, whose layout the checksum vouches for wherever it appears.</summary>
    private static readonly int StoredSize = new PK7().SIZE_STORED;

    /// <summary>Turns a Pokémon into the run's death marker.</summary>
    /// <param name="expectedPid">
    /// The Pokémon that must be in that slot. The same guard as the level cap and for the same
    /// reason: the party lives in several structures, not all of them read alike, and writing into
    /// the wrong slot destroys a Pokémon that is still alive.
    /// </param>
    public MemoryWriteResult ApplyDeath(uint slotAddress, DeathTransform transform, uint expectedPid,
        bool partyStatsAreHere = true)
    {
        if (Read(slotAddress) is not { ChecksumValid: true } slot || slot.PID != expectedPid)
        {
            logger.LogDebug("0x{Address:X8}: ahí no está el PID {Wanted:X8}; no se transforma",
                slotAddress, expectedPid);

            return MemoryWriteResult.Nothing;
        }

        return Modify(slotAddress, "muerte", pokemon =>
        {
            pokemon.Species = (ushort)transform.Species;
            pokemon.Form = 0;
            pokemon.Ability = transform.Ability;
            Data.GameLevels.Set(pokemon, transform.Level);

            if (transform.ClearMoves)
            {
                pokemon.Move1 = pokemon.Move2 = pokemon.Move3 = pokemon.Move4 = 0;
                pokemon.Move1_PP = pokemon.Move2_PP = pokemon.Move3_PP = pokemon.Move4_PP = 0;
                pokemon.Move1_PPUps = pokemon.Move2_PPUps = pokemon.Move3_PPUps = pokemon.Move4_PPUps = 0;
            }

            pokemon.Nickname = transform.Nickname;
            pokemon.IsNicknamed = true;
        }, partyStatsAreHere ? null : StoredSize);
    }

    /// <summary>
    /// Whether the Pokémon in a slot should be brought down to the cap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pulled out of the write so it can be tested on its own. Three conditions, and every one of
    /// them earned: the block has to be a real Pokémon, it has to be <em>that</em> Pokémon, and its
    /// <b>experience</b> has to be over the cap.
    /// </para>
    /// <para>
    /// Experience and never <c>Stat_Level</c>. The game keeps the party in several structures and
    /// only one lays its battle stats out where a PK7 has them; in the others that offset belongs
    /// to something else and reads anything at all. Trusting it read <b>145</b> for a level 4
    /// Ledyba, decided it was over a cap of 24, wrote 24 — and the game evolved it into a Ledian.
    /// The experience lives inside the encrypted block, which the checksum vouches for.
    /// </para>
    /// </remarks>
    public static bool NeedsCapping(PK7? slot, uint expectedPid, int cap) =>
        slot is { ChecksumValid: true }
        && slot.PID == expectedPid
        && Data.GameLevels.Of(slot) > cap;

    /// <summary>
    /// Brings a Pokémon back to the level cap with no progress into the next level. This covers
    /// the three cases the run defines — rare candies, experience gained at the cap, and
    /// levelling past it — because all three end in the same state.
    /// </summary>
    /// <param name="expectedPid">
    /// The Pokémon that must be in that slot: it is written only if it is really there.
    /// </param>
    /// <param name="partyStatsAreHere">
    /// True only for the structure whose entries are laid out as party Pokémon. False keeps the
    /// write inside the encrypted block, so nothing is put into bytes whose meaning is unknown.
    /// </param>
    public MemoryWriteResult EnforceLevelCap(uint slotAddress, int cap, uint expectedPid,
        bool partyStatsAreHere = true)
    {
        var current = Read(slotAddress);

        if (!NeedsCapping(current, expectedPid, cap))
        {
            logger.LogDebug(
                "0x{Address:X8}: el cap no lo toca (PID {Found}, esperado {Wanted:X8}, nivel {Level}, cap {Cap})",
                slotAddress, current is null ? "ilegible" : current.PID.ToString("X8"),
                expectedPid, current is null ? null : Data.GameLevels.Of(current), cap);

            return MemoryWriteResult.Nothing;
        }

        var result = Modify(slotAddress, $"cap de nivel {cap}",
            pokemon => Data.GameLevels.Set(pokemon, cap),
            partyStatsAreHere ? null : StoredSize);

        if (!result.Applied)
        {
            return result;
        }

        // Y la comprobación que de verdad importa: no que los bytes estén, sino que el Pokémon
        // haya bajado. Por experiencia, que es lo único fiable en todas las estructuras.
        var after = Read(slotAddress);

        if (after is null || Data.GameLevels.Of(after) > cap)
        {
            logger.LogWarning(
                "0x{Address:X8}: los bytes del cap se escribieron pero el Pokémon sigue a nivel {Level}, "
                + "cap {Cap}", slotAddress, after is null ? null : Data.GameLevels.Of(after), cap);

            return new MemoryWriteResult(result.Written, 0);
        }

        return result;
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

    /// <param name="limit">
    /// How many bytes of the record may be touched. The party stats live in the last 28 of a 260
    /// byte entry, and only the structure with the party stride actually keeps them there: in the
    /// others those offsets belong to something else, so writes stop at the encrypted block, whose
    /// layout the checksum vouches for.
    /// </param>
    private MemoryWriteResult Modify(uint slotAddress, string reason, Action<PK7> change, int? limit = null)
    {
        if (!client.TryReadMemory(slotAddress, PartySize, out var original))
        {
            logger.LogWarning("No se pudo leer el Pokémon en 0x{Address:X8}", slotAddress);
            return MemoryWriteResult.Nothing;
        }

        // PK7 wraps the array it is given, so each view needs its own copy.
        var reference = new byte[PartySize];
        new PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

        var working = new PK7((byte[])original.Clone());

        if (!working.ChecksumValid)
        {
            logger.LogWarning("El bloque en 0x{Address:X8} no es un Pokémon válido; no se escribe",
                slotAddress);
            return MemoryWriteResult.Nothing;
        }

        change(working);
        working.RefreshChecksum();

        var modified = new byte[PartySize];
        working.WriteEncryptedDataParty(modified);

        Backup(slotAddress, original, reason);

        var touched = new List<int>();
        var last = Math.Min(limit ?? PartySize, PartySize);

        for (var i = 0; i < last; i++)
        {
            if (modified[i] != reference[i])
            {
                client.WriteMemory((uint)(slotAddress + i), [modified[i]]);
                touched.Add(i);
            }
        }

        if (touched.Count == 0)
        {
            return MemoryWriteResult.Nothing;
        }

        var verified = Verify(slotAddress, modified, touched);

        if (verified == touched.Count)
        {
            logger.LogInformation("0x{Address:X8}: {Reason}, {Written} bytes escritos y releídos",
                slotAddress, reason, touched.Count);
        }
        else
        {
            logger.LogWarning(
                "0x{Address:X8}: {Reason}, se escribieron {Written} bytes y al releer solo {Verified} "
                + "estaban puestos. La escritura no ha cuajado; el juego manda esa memoria desde otro sitio "
                + "o el emulador la ha descartado.",
                slotAddress, reason, touched.Count, verified);
        }

        return new MemoryWriteResult(touched.Count, verified);
    }

    /// <summary>
    /// Reads the slot back and counts how many of the bytes just sent the game actually holds.
    /// </summary>
    /// <remarks>
    /// Only the bytes that were touched are compared. The rest of a party entry moves on its own
    /// while the game runs — current HP, status — so comparing the whole 260 would report a
    /// failure every time somebody took a step.
    /// </remarks>
    private int Verify(uint slotAddress, byte[] expected, List<int> touched)
    {
        if (!client.TryReadMemory(slotAddress, PartySize, out var after))
        {
            logger.LogWarning("No se pudo releer 0x{Address:X8} para comprobar la escritura", slotAddress);
            return 0;
        }

        return touched.Count(i => after[i] == expected[i]);
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
