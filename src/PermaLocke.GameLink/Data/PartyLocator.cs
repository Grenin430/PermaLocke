using PKHeX.Core;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Finds the party block in the running game's memory.
/// </summary>
/// <remarks>
/// No offset is assumed. Heap and linear memory are pulled into a local buffer and every four
/// byte aligned position is offered to PKHeX; only what parses as a real Pokémon belonging to
/// this trainer survives. Sanity is plaintext even in an encrypted block, which makes it a
/// cheap pre-filter that discards almost everything before the expensive parse.
///
/// Measured against Ultra Moon: the sweep costs about six seconds, so relocating is cheap
/// enough to redo whenever the cached address stops making sense.
/// </remarks>
public sealed class PartyLocator(AzaharRpcClient client)
{
    private const int BlockSize = 0x1000;

    /// <param name="preferredTrainer">
    /// Trainer name recorded in the run. Used as the preferred match, not as a requirement:
    /// the name typed when creating the run often differs from the one in the game, and a
    /// strict comparison would reject the player's own party.
    /// </param>
    /// <returns>Address of party slot zero and the trainer name actually found there.</returns>
    public (uint Address, string TrainerName)? Locate(string? preferredTrainer, CancellationToken ct = default)
    {
        var slotSize = Pk7Reader.PartySize;
        (uint Address, string TrainerName)? fallback = null;

        // Acotado a donde el juego guarda su estado vivo, no a todo lo que el emulador contesta.
        // Barrer 0x30000000-0x40000000 entero son 384 MB y unas 100.000 peticiones, y eso llegó a
        // tumbar el emulador durante el arranque de la app. El equipo y todas sus copias caen
        // dentro de estos 96 MB: de 0x3002E258 a 0x33F7FA44.
        foreach (var region in MemorySearch.LiveStateRegions)
        {
            var buffer = ReadRegion(region, ct);

            for (var offset = 0; offset + slotSize <= buffer.Length; offset += 4)
            {
                ct.ThrowIfCancellationRequested();

                if (BitConverter.ToUInt16(buffer, offset + 4) != 0
                    || BitConverter.ToUInt16(buffer, offset + 6) == 0)
                {
                    continue;
                }

                var pokemon = new PK7(buffer.AsSpan(offset, slotSize).ToArray());

                if (!IsPlausible(pokemon))
                {
                    continue;
                }

                var found = (region.Start + (uint)offset, pokemon.OriginalTrainerName);

                if (string.Equals(pokemon.OriginalTrainerName, preferredTrainer, StringComparison.Ordinal))
                {
                    return found;
                }

                // Keep looking for the preferred trainer, but remember this one in case the
                // run was created under a different name.
                fallback ??= found;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Rejects the false positives the sweep produces: memory that happens to decrypt into
    /// something structurally valid but with impossible stats. The HP ceiling is what
    /// discards them — real Pokémon never reach four digit HP.
    /// </summary>
    public static bool IsPlausible(PK7 pokemon) =>
        // The checksum is the decisive test: random memory does not satisfy it.
        pokemon.ChecksumValid
        && WorldLimits.IsKnownSpecies(pokemon.Species)
        && pokemon.CurrentLevel is > 0 and <= 100
        && pokemon.Stat_HPMax is > 0 and <= 999
        && pokemon.Stat_HPCurrent >= 0
        && pokemon.Stat_HPCurrent <= pokemon.Stat_HPMax
        && !string.IsNullOrWhiteSpace(pokemon.OriginalTrainerName);


    private byte[] ReadRegion(MemoryRegion region, CancellationToken ct)
    {
        var buffer = new byte[region.Size];
        var requests = 0;

        for (var offset = 0u; offset < region.Size; offset += BlockSize)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = (int)Math.Min(BlockSize, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
            }

            // An unbroken burst of requests has been seen to take the emulator down.
            if (++requests % 256 == 0)
            {
                Thread.Sleep(1);
            }
        }

        return buffer;
    }
}
