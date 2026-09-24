using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <summary>How the TM compatibility bits are dealt again.</summary>
public enum MachineCompatibility
{
    /// <summary>Left exactly as the cartridge has it.</summary>
    Unchanged,

    /// <summary>
    /// Each species keeps <b>how many</b> TMs it can learn, and only which ones changes.
    /// </summary>
    Shuffle,

    /// <summary>
    /// Rolled per TM, more likely when the move matches the species' own type.
    /// </summary>
    PreferType,
}

/// <param name="Changed">Species whose flags moved.</param>
/// <param name="Learnable">TMs learnable across the whole table afterwards.</param>
public sealed record MachineCompatibilityResult(int Changed, int Species, int Learnable, int Before);

/// <summary>
/// Deals out which TMs each species can be taught.
/// </summary>
/// <remarks>
/// <para>
/// Its own module, and it runs <b>after</b> the TM shuffle, which is the whole reason it is not
/// part of the Pokémon data step: preferring a move's own type is meaningless unless you know what
/// each TM finally teaches, and the data step runs long before the TMs are dealt.
/// </para>
/// <para>
/// <see cref="MachineCompatibility.PreferType"/> is how Universal Pokémon Randomizer does it, and
/// the numbers are its numbers: nine in ten when the move shares a type with the species, one in
/// two for a Normal move, one in four otherwise. It is the mode that actually changes the game —
/// a shuffle keeps every total, so a Magikarp stays useless and a Mew stays universal.
/// </para>
/// </remarks>
public sealed class MachineCompatibilityRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    /// <summary>Type id of Normal, which the middle probability is for.</summary>
    private const int Normal = 0;

    public async Task<MachineCompatibilityResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        string? baseLayerExefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(mod);

        if (options.MachineCompatibility == MachineCompatibility.Unchanged)
        {
            return new MachineCompatibilityResult(0, 0, 0, 0);
        }

        var machines = ReadMachines(mod, baseLayerExefs);

        if (options.MachineCompatibility == MachineCompatibility.PreferType && machines is null)
        {
            throw new InvalidDataException(
                "Sin code.bin no se sabe qué enseña cada MT, así que no se puede preferir el tipo. "
                + "Pon machineCompatibility en «shuffle» o en «unchanged».");
        }

        var path = mod.Stage(GameFiles.Personal);
        int changed = 0, learnable = 0, before = 0, rows;
        var moves = workspace.Config.Moves;

        using (var patcher = new GarcPatcher(path))
        {
            var index = patcher.FileCount - 1;
            var table = patcher.Read(index);
            rows = table.Length / PersonalEntry7.Size;

            for (var species = 1; species < rows; species++)
            {
                ct.ThrowIfCancellationRequested();

                var was = MachineFlags.Read(table, species);
                before += was.Count(on => on);

                var now = options.MachineCompatibility == MachineCompatibility.Shuffle
                    ? Shuffled(was, random)
                    : Rolled(table, species, machines!, moves, random);

                MachineFlags.Write(table, species, now);
                learnable += now.Count(on => on);

                if (!was.SequenceEqual(now))
                {
                    changed++;
                }
            }

            patcher.Write(index, table);

            // Y en la copia suelta de cada fila. El fichero guarda la tabla dos veces —una por
            // especie y forma, y la entera al final— y esto solo escribía la entera: 94 de las 100 MT
            // decían cosas distintas en las dos copias para el equipo del jugador (§141). El módulo de
            // datos ya copia sus filas a las dos; este era el único que no.
            for (var row = 0; row < Math.Min(rows, index); row++)
            {
                var entry = patcher.Read(row);

                if (entry.Length != PersonalEntry7.Size)
                {
                    continue;
                }

                MachineFlags.Write(entry, 0, MachineFlags.Read(table, row));
                patcher.Write(row, entry);
            }
        }

        // Releido del fichero, que es lo unico que convierte «escrito» en «hecho»: la tabla entera
        // con lo que se escribió, y cada copia suelta igual que su fila de la tabla.
        using (var back = new GarcPatcher(path))
        {
            var written = back.Read(back.FileCount - 1);
            var total = 0;

            for (var species = 1; species < rows; species++)
            {
                total += MachineFlags.Read(written, species).Count(on => on);
            }

            if (total != learnable)
            {
                throw new InvalidDataException(
                    $"La tabla de compatibilidad quedó con {total} MT aprendibles y se escribieron {learnable}.");
            }

            for (var row = 0; row < Math.Min(rows, back.FileCount - 1); row++)
            {
                var entry = back.Read(row);

                if (entry.Length == PersonalEntry7.Size
                    && !MachineFlags.Read(entry, 0).SequenceEqual(MachineFlags.Read(written, row)))
                {
                    throw new InvalidDataException(
                        $"La copia suelta de la fila {row} no tiene las MT de la tabla entera.");
                }
            }
        }

        return new MachineCompatibilityResult(changed, rows - 1, learnable, before);

    }

    private static bool[] Shuffled(bool[] flags, IRandomSource random)
    {
        var copy = (bool[])flags.Clone();

        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    /// <summary>
    /// One roll per TM, weighted by whether the move is of the species' own type.
    /// </summary>
    /// <remarks>
    /// The odds are drawn out of a thousand rather than as a fraction so the stream stays integer:
    /// the whole randomizer is seeded and reproducible, and a float comparison is one more thing
    /// that could differ between machines.
    /// </remarks>
    private static bool[] Rolled(byte[] table, int species, int[] machines,
        IReadOnlyList<pk3DS.Core.Structures.Move> moves, IRandomSource random)
    {
        var (first, second) = PersonalEntry7.GetTypes(table, species * PersonalEntry7.Size);
        var flags = new bool[MachineFlags.Count];

        for (var i = 0; i < flags.Length && i < machines.Length; i++)
        {
            var move = machines[i];
            var type = move > 0 && move < moves.Count ? moves[move].Type : -1;

            flags[i] = random.Next(1000) < ChanceOf(type, first, second);
        }

        return flags;
    }

    /// <summary>
    /// Odds out of a thousand that a species can be taught a move of this type.
    /// </summary>
    /// <remarks>
    /// Universal Pokémon Randomizer's numbers: nine in ten for a move of the species' own type,
    /// one in two for a Normal move, one in four otherwise. Out of a thousand rather than as a
    /// fraction so the random stream stays integer — the whole randomizer is seeded and has to
    /// reproduce, and a float comparison is one more thing that could differ between machines.
    /// <para>
    /// A move whose type could not be read gets the low odds rather than the high ones: not
    /// knowing is not a reason to be generous.
    /// </para>
    /// </remarks>
    public static int ChanceOf(int moveType, int firstType, int secondType) =>
        moveType < 0 ? 250
        : moveType == firstType || moveType == secondType ? 900
        : moveType == Normal ? 500
        : 250;

    /// <summary>What each TM teaches, from the already-patched executable when there is one.</summary>
    private static int[]? ReadMachines(LayeredFsMod mod, string? baseLayerExefs)
    {
        var patched = Path.Combine(Path.GetDirectoryName(mod.RomFsDirectory)!, "exefs", "code.bin");
        var source = File.Exists(patched)
            ? patched
            : baseLayerExefs is null ? null : Path.Combine(baseLayerExefs, "code.bin");

        if (source is null || !File.Exists(source))
        {
            return null;
        }

        var code = File.ReadAllBytes(source);
        var at = MachineTable.Find(code, [15, 19, 57, 70, 127, 249, 291]);

        return at < 0 ? null : MachineTable.Read(code, at);
    }
}
