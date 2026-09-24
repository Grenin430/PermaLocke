using PKHeX.Core;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Data;

/// <param name="Address">Address of party slot zero.</param>
/// <param name="Stride">Bytes between consecutive members.</param>
/// <param name="TrainerName">Trainer the party belongs to, as read from the game.</param>
public sealed record PartyLayout(uint Address, uint Stride, string TrainerName)
{
    public uint SlotAddress(int slot) => (uint)(Address + slot * Stride);
}

public interface IPartyLayoutLocator
{
    IReadOnlyList<PartyLayout> LocateAll(string? preferredTrainer, CancellationToken ct = default);

    /// <summary>
    /// Every copy of the party, found from the encryption constants of the saved party. Empty when none is in memory.
    /// </summary>
    IReadOnlyList<PartyLayout> LocateByKeys(IReadOnlyCollection<uint> keys, CancellationToken ct = default) => [];
}
/// <summary>
/// Locates the party the game actually reads from, as opposed to the copies it keeps in sync.
/// </summary>
/// <remarks>
/// The game holds several copies of the party. They were told apart by writing a different
/// nickname into each one and seeing which the game displayed: the authoritative copy is the
/// one whose members are <see cref="AuthoritativeStride"/> bytes apart, while the copies inside
/// the save block use the plain party stride. That is an empirical finding, so the locator
/// prefers the wider stride but still returns a narrower match rather than nothing — a copy is
/// good enough for reading, and callers that intend to write check <see cref="PartyLayout"/>.
/// </remarks>

public sealed class PartyLayoutLocator(AzaharRpcClient client) : IPartyLayoutLocator
{
    /// <summary>Stride of the structure the game reads from. Determined against the real game.</summary>
    public const uint AuthoritativeStride = 0x1E4;

    /// <summary>
    /// Where the authoritative structure keeps the twenty eight bytes of battle stats.
    /// </summary>
    /// <remarks>
    /// §53 said this copy kept its stats «somewhere else» and stopped there, and for months that
    /// meant PermaLocke only ever wrote into the mirror — which is the one copy the game does not
    /// read. Found on 2026-09-06 by asking the emulator who writes the HP into the mirror: at save
    /// time the game runs two memcpys, 232 bytes of Pokémon from <c>[r4+8]</c> and <b>28 bytes of
    /// stats</b> from <c>[r4+4]</c>, and the second source was this offset. Verified against the
    /// screen: 77 written here, the party menu said 77. See §99.
    /// </remarks>
    public const uint AuthoritativeStatsOffset = 0x158;

    /// <summary>Stride of the save-resident copies.</summary>
    public const uint CopyStride = 0x104;

    /// <summary>
    /// Drops the layouts that are the same structure seen from a later slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sweep starts a candidate wherever a Pokémon header appears, so a party of five turns
    /// into five "layouts": one per member, each one a view of the same block starting further in.
    /// The real party had two structures and the locator reported eight.
    /// </para>
    /// <para>
    /// That is not cosmetic. Writes go to <c>SlotAddress(slot)</c> of every layout, so a view that
    /// starts at slot 1 sends slot 4's correction to slot 5 — a different Pokémon. Keeping only the
    /// earliest address of each run makes every write land where it was aimed.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Which structure to believe when more than one reads: the authoritative one, always.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count is the <b>tie-break</b> and not the first question, and getting that the wrong way
    /// round cost a death. The mirror at <see cref="CopyStride"/> is staging for the save block:
    /// the game writes it and never reads it, so its HP lags (§98, §99). Sorting by "whichever
    /// reads most members" meant that any poll where the authoritative structure had one slot the
    /// reader would not accept — a battle stat momentarily out of range is enough — handed the
    /// whole read over to the mirror, where a Pokémon that had just fainted still showed its old
    /// HP. The watcher decides death on <c>CurrentHp == 0</c>, so it saw nothing.
    /// </para>
    /// <para>
    /// The cost of preferring authority is reading <em>fewer</em> party members on such a poll,
    /// which is the right way to be wrong: a slot not read is a slot not judged, while a slot read
    /// from a stale copy is a wrong answer stated confidently.
    /// </para>
    /// </remarks>
    /// <param name="candidates">Each layout with how many party members it managed to read.</param>
    public static PartyLayout? Preferred(IEnumerable<(PartyLayout Layout, int Read)> candidates) =>
        candidates
            .Where(candidate => candidate.Read > 0)
            .OrderBy(candidate => candidate.Layout.Stride == AuthoritativeStride ? 0 : 1)
            .ThenByDescending(candidate => candidate.Read)
            .Select(candidate => candidate.Layout)
            .FirstOrDefault();

    /// <summary>
    /// True when the copy chosen from last session's addresses is only a mirror, so the structure
    /// the game reads has moved and the whole list has to be located again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Preferred"/> takes the authoritative structure whenever it reads anybody, so
    /// ending up with a mirror means none of the remembered authoritative addresses holds the
    /// party any more. The mirror sits at the same address every session; the authoritative
    /// structure does not.
    /// </para>
    /// <para>
    /// Measured on 2026-09-18 (§135): the remembered list was eight days old, every session since
    /// had connected through it «without sweeping», and it kept being accepted because the mirror
    /// read fine. The party was read from the mirror, whose HP lags, and the pass that puts the
    /// fallen back at zero HP wrote to addresses where they no longer were — a Bouffalant died,
    /// was healed and fought again, and nothing was logged.
    /// </para>
    /// </remarks>
    public static bool NeedsLocatingAgain(PartyLayout chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        return chosen.Stride != AuthoritativeStride;
    }

    public static IReadOnlyList<PartyLayout> Distinct(IReadOnlyList<PartyLayout> layouts)
    {
        const int PartySlots = 6;
        var kept = new List<PartyLayout>();

        foreach (var layout in layouts.OrderBy(l => l.Stride).ThenBy(l => l.Address))
        {
            var covered = kept.Any(other =>
                other.Stride == layout.Stride
                && layout.Address > other.Address
                && layout.Address < other.Address + (other.Stride * PartySlots)
                && (layout.Address - other.Address) % other.Stride == 0);

            if (!covered)
            {
                kept.Add(layout);
            }
        }

        return kept;
    }

    private static readonly int StoredSize = new PK7().SIZE_STORED;

    private static readonly int PartySize = new PK7().SIZE_PARTY;

    /// <summary>
    /// Every copy of the party, found by the encryption constants of the saved party instead of a sweep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two faults of the sweep, found getting the friends' folder ready on 2026-09-21, and both go away here. It
    /// confirms a party by finding a <b>second</b> Pokémon one stride further, so a player with only their starter is
    /// never found — and on a fresh install there is no address from last time to fall back on, so nothing at all
    /// runs until a second Pokémon is caught: not the first-encounter rule, not a notice, not the route. And it is
    /// about a hundred thousand reads, the burst that four crashes of Azahar ended inside.
    /// </para>
    /// <para>
    /// A stored PK7 begins with its encryption constant in the clear and a zero sanity word, so the fork's search
    /// finds every copy of a saved Pokémon in one request per region. From each hit the entries before it are walked
    /// back while they still hold a Pokémon, which finds the first slot whichever member was hit. Which stride a
    /// start really has is <b>measured</b>, not assumed: with one Pokémon there is no second entry to tell them
    /// apart, so the stats are looked for where each structure keeps them — right after the stored block for the
    /// copies, at 0x158 for the one the game reads — and accepted only where <see cref="PartyStats.AreHere"/> says so:
    /// the level the tail holds is the level the experience gives. A view of the other structure with the wrong stride
    /// reads its tail from something else and fails that.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PartyLayout> LocateByKeys(IReadOnlyCollection<uint> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var known = keys.Where(key => key != 0).ToHashSet();

        if (known.Count == 0)
        {
            return [];
        }

        var pattern = new byte[6];
        var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();
        var starts = new HashSet<(uint Address, uint Stride)>();

        foreach (var key in known)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pattern, key);

            foreach (var region in MemorySearch.LiveStateRegions)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var hit in client.SearchMemory(region.Start, region.Size, pattern, mask))
                {
                    foreach (var stride in (uint[])[AuthoritativeStride, CopyStride])
                    {
                        starts.Add((FirstSlot(hit, stride), stride));
                    }
                }
            }
        }

        var layouts = new List<PartyLayout>();

        foreach (var (address, stride) in starts.OrderBy(start => start.Address))
        {
            var statsOffset = stride == AuthoritativeStride ? AuthoritativeStatsOffset : (uint)StoredSize;

            if (Entry(address, statsOffset) is { } first && PartyStats.AreHere(first))
            {
                layouts.Add(new PartyLayout(address, stride, first.OriginalTrainerName));
            }
        }

        return layouts;
    }

    /// <summary>Walks back from a hit while the entries before it still hold a Pokémon: the party's first slot.</summary>
    private uint FirstSlot(uint hit, uint stride)
    {
        var first = hit;

        for (var back = 0; back < 5 && first >= stride; back++)
        {
            var previous = first - stride;

            if (!client.TryReadMemory(previous, StoredSize, out var bytes) || bytes.Length < StoredSize
                || !LooksLikeHeader(bytes, 0) || Parse(bytes, 0) is null)
            {
                break;
            }

            first = previous;
        }

        return first;
    }

    /// <summary>The stored block and the stats this structure keeps at <paramref name="statsOffset"/>, as one PK7.</summary>
    private PK7? Entry(uint address, uint statsOffset)
    {
        if (!client.TryReadMemory(address, StoredSize, out var stored) || stored.Length < StoredSize
            || !client.TryReadMemory(address + statsOffset, PartySize - StoredSize, out var stats)
            || stats.Length < PartySize - StoredSize)
        {
            return null;
        }

        var data = new byte[PartySize];
        stored.CopyTo(data, 0);
        stats.CopyTo(data, StoredSize);
        return new PK7(data);
    }

    /// <summary>
    /// Every copy of the party found in memory. The game keeps several and only one of them is
    /// the one it reads; they cannot be told apart by their contents or their stride, since
    /// four of the six share it. Callers deal with that instead of guessing: reads use a copy
    /// that carries battle stats, and writes go to all of them.
    /// </summary>
    public IReadOnlyList<PartyLayout> LocateAll(string? preferredTrainer, CancellationToken ct = default)
    {
        var candidates = new List<PartyLayout>();
        var selectedProcess = client.GetProcess();
        if (selectedProcess == uint.MaxValue)
            throw new AzaharRpcException("No hay un proceso seleccionado para buscar el equipo.");

        // Acotado a donde el juego guarda su estado vivo, no a todo lo que el emulador contesta.
        // Barrer 0x30000000-0x40000000 entero son 384 MB y unas 100.000 peticiones, y eso llegó a
        // tumbar el emulador durante el arranque de la app. El equipo y todas sus copias caen
        // dentro de estos 64 MB: de 0x3002E258 a 0x33F7FA44 (el heap de 0x08000000 salió el 2026-09-21, ver LiveStateRegions).
        foreach (var region in MemorySearch.LiveStateRegions)
        {
            var buffer = ReadRegion(region, selectedProcess, ct);

            for (var offset = 0; offset + StoredSize <= buffer.Length; offset += 4)
            {
                ct.ThrowIfCancellationRequested();

                if (!LooksLikeHeader(buffer, offset))
                {
                    continue;
                }

                var first = Parse(buffer, offset);

                if (first is null)
                {
                    continue;
                }

                // A single valid Pokémon is not a party. The stride is confirmed by checking
                // that a second one sits exactly that far away.
                var stride = DetectStride(buffer, offset);

                if (stride is null)
                {
                    continue;
                }

                candidates.Add(new PartyLayout(region.Start + (uint)offset, stride.Value,
                    first.OriginalTrainerName));
            }
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var matchesTrainer = candidates
            .Where(c => string.Equals(c.TrainerName, preferredTrainer, StringComparison.Ordinal))
            .ToList();

        return matchesTrainer.Count > 0 ? matchesTrainer : candidates;
    }

    /// <summary>Sanity is zero and the checksum is non-zero in every real block; cheap pre-filter.</summary>
    private static bool LooksLikeHeader(byte[] buffer, int offset) =>
        BitConverter.ToUInt16(buffer, offset + 4) == 0
        && BitConverter.ToUInt16(buffer, offset + 6) != 0;

    private static PK7? Parse(byte[] buffer, int offset)
    {
        var block = new byte[new PK7().SIZE_PARTY];
        buffer.AsSpan(offset, StoredSize).CopyTo(block);

        var pokemon = new PK7(block);

        return pokemon.ChecksumValid
               && WorldLimits.IsKnownSpecies(pokemon.Species)
               && pokemon.CurrentLevel is > 0 and <= 100
               && !string.IsNullOrWhiteSpace(pokemon.OriginalTrainerName)
            ? pokemon
            : null;
    }

    /// <summary>Returns the stride when a second valid Pokémon follows at a known distance.</summary>
    private static uint? DetectStride(byte[] buffer, int offset)
    {
        foreach (var stride in (uint[])[AuthoritativeStride, CopyStride])
        {
            var next = offset + (int)stride;

            if (next + StoredSize <= buffer.Length && LooksLikeHeader(buffer, next)
                && Parse(buffer, next) is not null)
            {
                return stride;
            }
        }

        return null;
    }

    private byte[] ReadRegion(MemoryRegion region, uint selectedProcess, CancellationToken ct)
    {
        var buffer = new byte[region.Size];
        var requests = 0;

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            ct.ThrowIfCancellationRequested();

            // An emulator restart resets selection but still answers reads with zeroes.
            // Stop within 64 KB instead of issuing the rest of a 96 MB sweep to that new session.
            if (offset % 0x10000 == 0 && client.GetProcess() != selectedProcess)
                throw new AzaharRpcException("El proceso del juego cambió durante la búsqueda del equipo.");

            var chunk = (int)Math.Min(0x1000, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
            }

            if (++requests % 64 == 0)
            {
                if (ct.WaitHandle.WaitOne(5)) ct.ThrowIfCancellationRequested();
            }
        }

        return buffer;
    }
}
