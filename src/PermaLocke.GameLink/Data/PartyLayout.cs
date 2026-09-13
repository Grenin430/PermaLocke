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
public sealed class PartyLayoutLocator(AzaharRpcClient client)
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

    /// <summary>
    /// Every copy of the party found in memory. The game keeps several and only one of them is
    /// the one it reads; they cannot be told apart by their contents or their stride, since
    /// four of the six share it. Callers deal with that instead of guessing: reads use a copy
    /// that carries battle stats, and writes go to all of them.
    /// </summary>
    public IReadOnlyList<PartyLayout> LocateAll(string? preferredTrainer, CancellationToken ct = default)
    {
        var candidates = new List<PartyLayout>();

        // Acotado a donde el juego guarda su estado vivo, no a todo lo que el emulador contesta.
        // Barrer 0x30000000-0x40000000 entero son 384 MB y unas 100.000 peticiones, y eso llegó a
        // tumbar el emulador durante el arranque de la app. El equipo y todas sus copias caen
        // dentro de estos 96 MB: de 0x3002E258 a 0x33F7FA44.
        foreach (var region in MemorySearch.LiveStateRegions)
        {
            var buffer = ReadRegion(region, ct);

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

    private byte[] ReadRegion(MemoryRegion region, CancellationToken ct)
    {
        var buffer = new byte[region.Size];
        var requests = 0;

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = (int)Math.Min(0x1000, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
            }

            if (++requests % 256 == 0)
            {
                Thread.Sleep(1);
            }
        }

        return buffer;
    }
}
