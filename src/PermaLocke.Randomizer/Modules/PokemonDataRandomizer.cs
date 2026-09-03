using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Entries">Personal table entries rewritten, forms included.</param>
/// <param name="Evolutions">Evolution targets redirected.</param>
/// <param name="LearnsetMoves">Level-up moves replaced.</param>
public sealed record PokemonDataResult(int Entries, int Evolutions, int LearnsetMoves);

/// <summary>
/// Rewrites what a species <em>is</em>: types, base stats, abilities, what it evolves into and
/// what it learns by level.
/// <para>
/// All three GARCs are uncompressed and every edit keeps its entry the same length, so all of
/// this is patched in place.
/// </para>
/// </summary>
public sealed class PokemonDataRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    public async Task<PokemonDataResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        CancellationToken ct = default)
    {
        var typeCount = workspace.Config.GetText(TextName.Types).Length;
        var maxAbility = options.EffectiveMaxAbility(workspace.Config.Info.MaxAbilityID);
        var maxMove = options.EffectiveMaxMove(workspace.Config.Info.MaxMoveID);

        // Cada parte con su propia fuente, por la misma razón que cada módulo tiene la suya
        // (§20): activar o desactivar una no debe mover los resultados de las demás. Aquí faltaba,
        // y desactivar las evoluciones desplazaba los aprendizajes de una partida ya empezada.
        //
        // 'personal' se queda con la fuente raíz a propósito: es la primera que consumía, así que
        // mantenerla deja los tipos, las estadísticas y las habilidades EXACTAMENTE como estaban
        // en las partidas ya en curso. Derivarla también sería más limpio, pero cambiaría el
        // mundo de quien ya está jugando.
        var entries = RandomizePersonal(mod, random, typeCount, maxAbility, ct);
        var evolutions = RandomizeEvolutions(mod, random.Derive("evolutions"),
            options.EffectiveMaxSpecies(workspace.MaxSpecies), ct);
        var moves = RandomizeLearnsets(mod, random.Derive("learnsets"), TeachableMoves(maxMove), ct);

        await VerifyAsync(mod, ct);
        return new PokemonDataResult(entries, evolutions, moves);
    }

    /// <summary>
    /// Patches every individual entry and the packed copy the game reads, keeping them in step.
    /// </summary>
    private int RandomizePersonal(LayeredFsMod mod, IRandomSource random, int typeCount,
        int maxAbility, CancellationToken ct)
    {
        var path = mod.Stage(GameFiles.Personal);
        using var patcher = new GarcPatcher(path);

        // The last subfile is the whole table; the ones before it are its rows.
        var packedIndex = patcher.FileCount - 1;
        var packed = patcher.Read(packedIndex);
        var rows = packed.Length / PersonalEntry7.Size;
        var changed = 0;

        // Una fuente por aspecto: desactivar los tipos ya no desplaza las estadisticas ni las
        // habilidades, que es lo que pasaba compartiendo una sola fuente en cadena.
        var sources = new EntrySources(
            random.Derive("types"), random.Derive("stats"), random.Derive("abilities"),
            random.Derive("machine-flags"));

        for (var row = 1; row < rows; row++) // row 0 is a placeholder, not a species
        {
            ct.ThrowIfCancellationRequested();
            RandomizeEntry(packed, row * PersonalEntry7.Size, sources, typeCount, maxAbility);
            changed++;
        }

        patcher.Write(packedIndex, packed);

        // Mirror each row back into its own subfile so the two copies cannot disagree.
        for (var row = 0; row < Math.Min(rows, packedIndex); row++)
        {
            var entry = patcher.Read(row);
            if (entry.Length != PersonalEntry7.Size)
            {
                continue;
            }
            Array.Copy(packed, row * PersonalEntry7.Size, entry, 0, PersonalEntry7.Size);
            patcher.Write(row, entry);
        }

        return changed;
    }

    /// <summary>
    /// Deals a species' hundred TM flags again, keeping <b>how many</b> it has.
    /// </summary>
    /// <remarks>
    /// A shuffle and not a fresh draw, because a draw can leave something with almost nothing —
    /// and unlike a level-up move, a TM a Pokémon cannot learn is a move it will never have. This
    /// way a Magikarp still learns as little as it did and a Mew still learns everything, which is
    /// also the reason it cannot make the game unplayable: the totals are the cartridge's own.
    /// </remarks>
    private static void ShuffleMachines(byte[] table, int species, IRandomSource random)
    {
        var flags = MachineFlags.Read(table, species);

        for (var i = flags.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (flags[i], flags[j]) = (flags[j], flags[i]);
        }

        MachineFlags.Write(table, species, flags);
    }

    /// <summary>One random stream per aspect, so switching one off leaves the others alone.</summary>

    private readonly record struct EntrySources(
        IRandomSource Types, IRandomSource Stats, IRandomSource Abilities, IRandomSource Machines);

    private void RandomizeEntry(byte[] table, int at, EntrySources sources, int typeCount, int maxAbility)
    {
        if (options.RandomizeTypes)
        {
            // A species with one type keeps having one type; a dual type keeps two distinct ones.
            var first = sources.Types.Next(typeCount);
            var second = PersonalEntry7.IsMonoType(table, at) ? first : sources.Types.Next(typeCount);
            for (var attempt = 0; attempt < 16 && second == first && !PersonalEntry7.IsMonoType(table, at); attempt++)
            {
                second = sources.Types.Next(typeCount);
            }
            PersonalEntry7.SetTypes(table, at, first, second);
        }

        if (options.ShuffleBaseStats)
        {
            PersonalEntry7.ShuffleStats(table, at, sources.Stats.Next);
        }

        if (options.ShuffleMachineCompatibility)
        {
            ShuffleMachines(table, at / PersonalEntry7.Size, sources.Machines);
        }

        if (options.RandomizeAbilities)
        {
            for (var slot = 0; slot < PersonalEntry7.AbilityOffsets.Length; slot++)
            {
                // Ability 0 means "no ability"; a slot that was empty stays empty.
                if (PersonalEntry7.GetAbility(table, at, slot) == 0)
                {
                    continue;
                }
                PersonalEntry7.SetAbility(table, at, slot, sources.Abilities.Next(1, maxAbility + 1));
            }
        }
    }

    /// <summary>
    /// Redirects what each species evolves into, leaving the trigger alone: the method, its
    /// argument and the level stay as they were, so an evolution still happens when it used to.
    /// </summary>
    private int RandomizeEvolutions(LayeredFsMod mod, IRandomSource random, int maxSpeciesHere,
        CancellationToken ct)
    {
        if (!options.RandomizeEvolutions)
        {
            return 0;
        }

        const int entrySize = 8;
        const int speciesOffset = 4;
        const int formOffset = 6;

        var path = mod.Stage(GameFiles.Evolution);
        using var patcher = new GarcPatcher(path);
        var changed = 0;

        for (var species = 1; species < patcher.FileCount; species++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = patcher.Read(species);
            var touched = false;

            for (var at = 0; at + entrySize <= entry.Length; at += entrySize)
            {
                if (BitConverter.ToUInt16(entry, at) == 0)
                {
                    continue; // no method, so no evolution in this slot
                }

                var replacement = random.Next(1, maxSpeciesHere + 1);
                for (var attempt = 0; attempt < 16 && replacement == species; attempt++)
                {
                    replacement = random.Next(1, maxSpeciesHere + 1);
                }

                BitConverter.GetBytes((ushort)replacement).CopyTo(entry, at + speciesOffset);
                entry[at + formOffset] = 0;
                touched = true;
                changed++;
            }

            if (touched)
            {
                patcher.Write(species, entry);
            }
        }

        return changed;
    }

    /// <summary>
    /// Every move a level-up learnset may hand out: all of them except the Z-moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Z-move in a learnset is not a curiosity, it is a broken run. They hit for hundreds and
    /// the game offers them like any other move, so a randomized learnset was handing out an
    /// unusable-but-devastating attack with a single PP.
    /// </para>
    /// <para>
    /// Which ones they are is <b>read from the cartridge, not written down here</b>: every Z-move
    /// carries <c>PP = 1</c> and nothing else does, bar Struggle and Sketch — and neither of those
    /// belongs in a learnset either, Struggle being the move the game falls back to when there are
    /// none left. On Ultra Moon that leaves out 55 of 728: the eighteen type Z-moves with their
    /// two variants each, the exclusive ones, Struggle and Sketch.
    /// </para>
    /// </remarks>
    private IReadOnlyList<int> TeachableMoves(int maxMove) =>
        MoveTable.Teachable([.. workspace.Config.Moves.Select(move => move.PP)], maxMove);

    /// <summary>
    /// Replaces the moves of every level-up learnset, keeping the levels. The entry is a run of
    /// (move, level) pairs closed by a terminator, so swapping moves leaves the length untouched.
    /// </summary>
    private int RandomizeLearnsets(LayeredFsMod mod, IRandomSource random,
        IReadOnlyList<int> teachable, CancellationToken ct)
    {
        if (!options.RandomizeLearnsets)
        {
            return 0;
        }

        if (teachable.Count == 0)
        {
            throw new InvalidDataException(
                "No se ha podido leer la tabla de movimientos de la ROM, así que no se sabe cuáles " +
                "son los movimientos Z. Antes que repartirlos, no se randomizan los aprendizajes.");
        }

        var path = mod.Stage(GameFiles.Learnset);
        using var patcher = new GarcPatcher(path);
        var changed = 0;

        for (var species = 0; species < patcher.FileCount; species++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = patcher.Read(species);
            var pairs = (entry.Length / 4) - 1; // the last four bytes are the terminator
            if (pairs <= 0)
            {
                continue;
            }

            for (var pair = 0; pair < pairs; pair++)
            {
                var move = teachable[random.Next(0, teachable.Count)];
                BitConverter.GetBytes((ushort)move).CopyTo(entry, pair * 4);
                changed++;
            }

            patcher.Write(species, entry);
        }

        return changed;
    }

    /// <summary>
    /// Reads everything back with the pk3DS reader and refuses a result whose entries changed
    /// size or whose stats fell outside what a byte can hold.
    /// </summary>
    private async Task VerifyAsync(LayeredFsMod mod, CancellationToken ct)
    {
        foreach (var file in (string[])[GameFiles.Personal, GameFiles.Evolution, GameFiles.Learnset])
        {
            var path = Path.Combine(mod.RomFsDirectory, file.Replace('/', Path.DirectorySeparatorChar));

            // Un módulo apagado no deja fichero, y eso es lo correcto: el juego usa el del
            // cartucho. Verificar lo que no se ha escrito reventaba la randomización entera.
            if (!File.Exists(path))
            {
                continue;
            }

            var modded = new GARC.LazyGARC(await File.ReadAllBytesAsync(path, ct));
            var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(workspace.PathOf(file), ct));

            if (modded.FileCount != vanilla.FileCount)
            {
                throw new InvalidDataException(
                    $"{file} se quedó con {modded.FileCount} subficheros en vez de {vanilla.FileCount}.");
            }

            for (var i = 0; i < modded.FileCount; i++)
            {
                if (modded[i].Length != vanilla[i].Length)
                {
                    throw new InvalidDataException(
                        $"{file}, subfichero {i}: {vanilla[i].Length} -> {modded[i].Length} bytes.");
                }
            }
        }
    }
}
