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
    /// <remarks>
    /// How far the write may reach is no longer asked of the caller. It used to be passed in as
    /// «this is the party stride», and the stride turned out not to be the fact: two structures
    /// with the same <c>0x104</c> stride read 118/131 and 42649/10902 for the same Gyarados on the
    /// same second. <see cref="PartyStats.AreHere"/> decides it from the entry itself now, so the
    /// answer cannot drift from what is actually in the slot.
    /// </remarks>
    public MemoryWriteResult EnforceLevelCap(uint slotAddress, int cap, uint expectedPid)
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

        // ¿Es la copia que lee el juego, con las estadísticas en 0x158 (§99)? Se mide ANTES de bajar la experiencia:
        // después, el nivel de la cola ya no coincide con el de la experiencia y AreHere diría que no.
        var live = ReadAuthoritative(slotAddress);
        var statsAtLiveOffset = live is { ChecksumValid: true } && live.PID == expectedPid && Data.PartyStats.AreHere(live);
        var statsContiguous = Data.PartyStats.AreHere(current);

        var result = Modify(slotAddress, $"cap de nivel {cap}",
            pokemon =>
            {
                Data.GameLevels.Set(pokemon, cap);

                // Donde la cola son estadísticas de verdad, que acompañen al nivel.
                if (statsContiguous)
                {
                    Data.StatCalculator.Restat(pokemon);
                }
            },
            statsContiguous ? null : StoredSize);

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

        if (!statsAtLiveOffset)
        {
            return result;
        }

        var tail = CapLiveStats(slotAddress, cap);

        return new MemoryWriteResult(result.Written + tail.Written, result.Verified + tail.Verified);
    }

    /// <summary>
    /// Brings the level the game shows, and the stats that go with it, down to the cap in the structure it reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found on 2026-09-21: the cap had stopped working on screen. The log said «corregido y releído» and the party menu
    /// still said 15 with a cap of 14. The cap was written before §99 found where the game keeps the battle stats of
    /// the structure it reads — 28 bytes at <c>0x158</c> — so it only ever lowered the <b>experience</b>, inside the
    /// encrypted block, and the <b>level the menu shows</b> lives in that tail. The app read the experience back, saw
    /// 14, and called it done.
    /// </para>
    /// <para>
    /// The level alone would not do: twenty rare candies and a cap would leave a level 14 with level 34 stats. So the
    /// stats are worked out again with <see cref="Data.StatCalculator.Restat"/>, the same code ENTRENAR EV writes with,
    /// from the installed world's base stats. With no world table there is no honest number, so the level is written
    /// and the stats are left as they were, and the log says so. The current PS follow the maximum down, and a Pokémon at
    /// zero stays at zero: in this project that is a death (§98).
    /// </para>
    /// <para>
    /// Only the tail is written, and only the bytes that differ, the same way the death mark writes it; and it is read
    /// back as the game reads it before anything is called done.
    /// </para>
    /// </remarks>
    private MemoryWriteResult CapLiveStats(uint entryAddress, int cap)
    {
        var statsAt = entryAddress + Data.PartyLayoutLocator.AuthoritativeStatsOffset;

        if (ReadAuthoritative(entryAddress) is not { ChecksumValid: true } pokemon
            || !client.TryReadMemory(statsAt, PartySize - StoredSize, out var before))
        {
            return MemoryWriteResult.Nothing;
        }

        pokemon.Stat_Level = (byte)cap;

        if (!Data.StatCalculator.Restat(pokemon))
        {
            logger.LogWarning("0x{Address:X8}: sin estadísticas base del mundo instalado; se baja el nivel y las "
                              + "estadísticas se quedan como estaban", entryAddress);
        }

        var encrypted = new byte[PartySize];
        pokemon.WriteEncryptedDataParty(encrypted);

        Backup(statsAt, before, $"cap de nivel {cap} (estadísticas)");

        var touched = 0;

        for (var i = 0; i < before.Length; i++)
        {
            if (encrypted[StoredSize + i] != before[i])
            {
                client.WriteMemory((uint)(statsAt + i), [encrypted[StoredSize + i]]);
                touched++;
            }
        }

        if (touched == 0)
        {
            return MemoryWriteResult.Nothing;
        }

        var after = ReadAuthoritative(entryAddress);

        if (after is null || after.Stat_Level != cap || !Data.PartyStats.AreHere(after))
        {
            logger.LogWarning("0x{Address:X8}: escritas las estadísticas del cap pero el nivel que enseña el juego es {Level}",
                entryAddress, after is null ? "ilegible" : after.Stat_Level.ToString());

            return new MemoryWriteResult(touched, 0);
        }

        logger.LogInformation("0x{Address:X8}: nivel {Level} y estadísticas recalculadas en la copia que el juego lee "
                              + "(PS {Hp}/{Max})", entryAddress, cap, after.Stat_HPCurrent, after.Stat_HPMax);

        return new MemoryWriteResult(touched, touched);
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
