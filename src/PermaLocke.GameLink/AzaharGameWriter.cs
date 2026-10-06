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

    /// <summary>
    /// The level cap into the block the patched game reads (<see cref="PermaLocke.Core.Domain.RuleBlock"/>, 2026-10-06).
    /// A fixed address outside anything the game uses, so nothing is searched and nothing of the game's is touched.
    /// True when it reads back as written.
    /// </summary>
    public bool WriteRuleFallen(IReadOnlyCollection<uint> encryptionConstants)
    {
        var list = new byte[PermaLocke.Core.Domain.RuleBlock.FallenSlots * 4];
        var i = 0;
        foreach (var constant in encryptionConstants.Take(PermaLocke.Core.Domain.RuleBlock.FallenSlots))
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(list.AsSpan(i++ * 4), constant);
        }

        client.WriteMemory(PermaLocke.Core.Domain.RuleBlock.Fallen, list);
        return client.TryReadMemory(PermaLocke.Core.Domain.RuleBlock.Fallen, list.Length, out var back) && back.AsSpan().SequenceEqual(list);
    }

    /// <summary>The reason the patched battle menu gives for not throwing a ball (<see cref="PermaLocke.Core.Domain.RuleBlock.BallRefusal"/>; 0 = it can).</summary>
    public bool WriteRuleBallRefusal(byte reason)
    {
        client.WriteMemory(PermaLocke.Core.Domain.RuleBlock.BallRefusal, [reason]);
        return client.TryReadMemory(PermaLocke.Core.Domain.RuleBlock.BallRefusal, 1, out var back) && back[0] == reason;
    }

    /// <summary>
    /// The species the duplicates clause rerolls into the block the patched game reads (<see cref="PermaLocke.Core.Domain.RuleBlock.Dupes"/>).
    /// An empty set clears it, and the game rolls as the cartridge does.
    /// </summary>
    public bool WriteRuleDupes(IReadOnlyCollection<int> species)
    {
        var bits = new byte[PermaLocke.Core.Domain.RuleBlock.DupesBytes];
        foreach (var s in species.Where(s => s is > 0 and < PermaLocke.Core.Domain.RuleBlock.DupesSpeciesLimit))
        {
            bits[s >> 3] |= (byte)(1 << (s & 7));
        }

        // El hueco vacío (especie 0) cuenta como repetido en cuanto hay alguno: así nunca sale al volver a tirar.
        if (bits.Any(b => b != 0)) bits[0] |= 1;

        client.WriteMemory(PermaLocke.Core.Domain.RuleBlock.Dupes, bits);
        return client.TryReadMemory(PermaLocke.Core.Domain.RuleBlock.Dupes, bits.Length, out var back) && back.AsSpan().SequenceEqual(bits);
    }

    /// <summary>
    /// The level cap into the block the patched game reads. See <see cref="WriteRuleFallen"/> for the fallen list, which
    /// the patched game uses to keep their HP at zero.
    /// </summary>
    public bool WriteRuleCap(int cap)
    {
        var block = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(block, PermaLocke.Core.Domain.RuleBlock.Magic);
        block[PermaLocke.Core.Domain.RuleBlock.CapOffset] = (byte)Math.Clamp(cap, 0, 100);

        client.WriteMemory(PermaLocke.Core.Domain.RuleBlock.Address, block);
        return client.TryReadMemory(PermaLocke.Core.Domain.RuleBlock.Address, block.Length, out var back) && back.AsSpan().SequenceEqual(block);
    }

    /// <summary>The encrypted block, whose layout the checksum vouches for wherever it appears.</summary>
    private static readonly int StoredSize = new PK7().SIZE_STORED;


    /// <summary>
    /// Sets a party Pokémon's current HP in memory. <b>The game does not read it back.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists to be measured against the screen, and nothing in the application calls it. §98
    /// wrote <b>120</b> here — correctly, through the encryption, read back as 120 — and with the
    /// party menu open the game went on saying <b>128</b>, and memory still said 120 afterwards.
    /// The party entry it writes into is a mirror the game fills and never consults. The death
    /// marker lives in the save file instead; see <c>SaveDeathEnforcer</c>.
    /// </para>
    /// <para>
    /// §93 had reached the same conclusion by a broken route — a plaintext byte into an encrypted
    /// field — so this goes through <see cref="Modify"/>, which decrypts, changes the field,
    /// re-encrypts and reads back. That was worth building: it turned "we think it cannot" into a
    /// measurement, and the measurement is what closed the question.
    /// </para>
    /// <para>
    /// It refuses any slot whose tail is not really the party stats, measured and not assumed. HP
    /// at <c>0xF0</c> means HP only where the structure is identified; elsewhere it is somebody
    /// else's bytes, which is how a cap once evolved a Ledyba.
    /// </para>
    /// </remarks>
    /// <param name="expectedPid">The Pokémon that must be in that slot, or nothing is written.</param>
    public MemoryWriteResult SetHp(uint slotAddress, int hp, uint expectedPid)
    {
        if (Read(slotAddress) is not { ChecksumValid: true } slot || slot.PID != expectedPid)
        {
            logger.LogDebug("0x{Address:X8}: ahí no está el PID {Wanted:X8}; no se toca",
                slotAddress, expectedPid);

            return MemoryWriteResult.Nothing;
        }

        if (!Data.PartyStats.AreHere(slot))
        {
            logger.LogDebug("0x{Address:X8}: la cola de esta copia no son las estadísticas; no se toca",
                slotAddress);

            return MemoryWriteResult.Nothing;
        }

        if (hp < 0 || hp > slot.Stat_HPMax)
        {
            logger.LogWarning("0x{Address:X8}: {Hp} PS no cabe en un máximo de {Max}",
                slotAddress, hp, slot.Stat_HPMax);

            return MemoryWriteResult.Nothing;
        }

        if (slot.Stat_HPCurrent == hp)
        {
            return MemoryWriteResult.Nothing;
        }

        var result = Modify(slotAddress, $"PS a {hp}", pokemon => pokemon.Stat_HPCurrent = hp);

        if (!result.Applied)
        {
            return result;
        }

        // Y la comprobación que importa no es que los bytes estén, sino que el Pokémon los tenga.
        // Releída y descifrada, que es como el juego la mira.
        var after = Read(slotAddress);

        if (after is null || after.Stat_HPCurrent != hp)
        {
            logger.LogWarning("0x{Address:X8}: la escritura se aceptó pero los PS son {Found}, no {Hp}",
                slotAddress, after is null ? "ilegibles" : after.Stat_HPCurrent.ToString(), hp);

            return MemoryWriteResult.Nothing;
        }

        logger.LogInformation("0x{Address:X8}: PS {Hp} de {Max} confirmado", slotAddress, hp, after.Stat_HPMax);

        return result;
    }

    /// <summary>
    /// Reads an entry of the structure the game actually reads, whose stats are not where a PK7
    /// puts them.
    /// </summary>
    /// <remarks>
    /// It is a party <see cref="PK7"/> in two pieces: the 232 byte block at the start and the 28
    /// bytes of battle stats at <see cref="PartyLayoutLocator.AuthoritativeStatsOffset"/>. Put back
    /// together they decrypt as one, and that was checked by hand before any of this was written:
    /// those 28 bytes are <b>byte for byte</b> what the mirror carries at <c>0xF0</c>, so the
    /// keystream is the same and PKHeX needs no help.
    /// </remarks>
    public PK7? ReadAuthoritative(uint entryAddress)
    {
        if (!client.TryReadMemory(entryAddress, StoredSize, out var stored)
            || !client.TryReadMemory(entryAddress + Data.PartyLayoutLocator.AuthoritativeStatsOffset,
                PartySize - StoredSize, out var stats))
        {
            return null;
        }

        var whole = new byte[PartySize];

        stored.CopyTo(whole, 0);
        stats.CopyTo(whole, StoredSize);

        return new PK7(whole);
    }

    /// <summary>
    /// Sets the current HP where the game reads it, and reads it back to prove it landed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one that reaches the screen. <see cref="SetHp"/> writes into the mirror, which
    /// the game fills and never consults — measured three times in §98 — and this writes into the
    /// authoritative structure, which was verified the other way round: 77 written here and the
    /// party menu said 77.
    /// </para>
    /// <para>
    /// Only the 28 byte tail is written, and only the bytes that differ. Nothing outside it is
    /// touched, which matters more here than usual: between <c>0xE8</c> and <c>0x158</c> lie bytes
    /// nobody has identified and the game moves on its own, and §97 is what writing over those
    /// costs.
    /// </para>
    /// </remarks>
    public MemoryWriteResult SetLiveHp(uint entryAddress, int hp, uint expectedPid)
    {
        var statsAt = entryAddress + Data.PartyLayoutLocator.AuthoritativeStatsOffset;

        if (ReadAuthoritative(entryAddress) is not { ChecksumValid: true } pokemon
            || pokemon.PID != expectedPid)
        {
            logger.LogDebug("0x{Address:X8}: ahí no está el PID {Wanted:X8}; no se toca",
                entryAddress, expectedPid);

            return MemoryWriteResult.Nothing;
        }

        // La cola tiene que leerse como estadísticas de verdad antes de escribir en ella: es la
        // misma exigencia del §53, solo que ahora se hace donde de verdad están.
        if (!Data.PartyStats.AreHere(pokemon))
        {
            logger.LogDebug("0x{Address:X8}: la cola de 0x158 no cuadra como estadísticas", entryAddress);
            return MemoryWriteResult.Nothing;
        }

        if (hp < 0 || hp > pokemon.Stat_HPMax || pokemon.Stat_HPCurrent == hp)
        {
            return MemoryWriteResult.Nothing;
        }

        if (!client.TryReadMemory(statsAt, PartySize - StoredSize, out var before))
        {
            return MemoryWriteResult.Nothing;
        }

        pokemon.Stat_HPCurrent = hp;

        var encrypted = new byte[PartySize];
        pokemon.WriteEncryptedDataParty(encrypted);

        Backup(statsAt, before, $"PS a {hp}");

        var touched = new List<int>();

        for (var i = 0; i < before.Length; i++)
        {
            if (encrypted[StoredSize + i] != before[i])
            {
                client.WriteMemory((uint)(statsAt + i), [encrypted[StoredSize + i]]);
                touched.Add(i);
            }
        }

        if (touched.Count == 0)
        {
            return MemoryWriteResult.Nothing;
        }

        // Y lo que decide no es que los bytes esten, sino que el Pokemon tenga esos PS al releerlo
        // descifrado, que es como el juego lo mira.
        var after = ReadAuthoritative(entryAddress);

        if (after is null || after.Stat_HPCurrent != hp)
        {
            logger.LogWarning("0x{Address:X8}: escrito pero los PS son {Found}, no {Hp}",
                entryAddress, after is null ? "ilegibles" : after.Stat_HPCurrent.ToString(), hp);

            return new MemoryWriteResult(touched.Count, 0);
        }

        logger.LogInformation("0x{Address:X8}: PS {Hp} de {Max} en la copia que el juego lee",
            entryAddress, hp, after.Stat_HPMax);

        return new MemoryWriteResult(touched.Count, touched.Count);
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

    /// <summary>
    /// Writes a nickname into one party copy with the game running (2026-09-28, the voted nicknames), only if the slot
    /// holds that PID.
    /// </summary>
    /// <remarks>
    /// The name lives in the stored block, so only that is written (<see cref="StoredSize"/>): the stats tail, and with it
    /// the current PS that corrupted a save once (1.0.6.2), is never touched. The level cap writes the same block the same
    /// way while playing.
    /// </remarks>
    public MemoryWriteResult SetNickname(uint slotAddress, string nickname, uint expectedPid)
    {
        if (Read(slotAddress) is not { ChecksumValid: true } current || current.PID != expectedPid || current.IsEgg)
        {
            return MemoryWriteResult.Nothing;
        }

        return Modify(slotAddress, $"mote «{nickname}»", pokemon => pokemon.SetNickname(nickname), StoredSize);
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
