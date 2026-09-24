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

        // Los techos salen del juego que se randomiza, no de pk3DS: GameInfo trae 233 habilidades y
        // 728 movimientos clavados, los del cartucho, y con el mod de expansión dejaban fuera todo lo
        // nuevo dijera lo que dijera la configuración. La misma trampa que el 807 de las especies (§136).
        var abilityNames = workspace.Config.GetText(TextName.AbilityNames);
        var maxAbility = options.EffectiveMaxAbility(abilityNames.Length - 1);
        var maxMove = options.EffectiveMaxMove(workspace.Config.Moves.Length - 1);
        var abilityPool = AbilityTable.Assignable(abilityNames, maxAbility, [.. options.BannedAbilities]);

        if (options.RandomizeAbilities && abilityPool.Count == 0)
        {
            throw new InvalidOperationException(
                "No queda ninguna habilidad que repartir: revisa maxAbility y bannedAbilities.");
        }

        // Cada parte con su propia fuente, por la misma razón que cada módulo tiene la suya
        // (§20): activar o desactivar una no debe mover los resultados de las demás. Aquí faltaba,
        // y desactivar las evoluciones desplazaba los aprendizajes de una partida ya empezada.
        //
        // 'personal' se queda con la fuente raíz a propósito: es la primera que consumía, así que
        // mantenerla deja los tipos, las estadísticas y las habilidades EXACTAMENTE como estaban
        // en las partidas ya en curso. Derivarla también sería más limpio, pero cambiaría el
        // mundo de quien ya está jugando.
        var entries = RandomizePersonal(mod, random, typeCount, abilityPool, ct);
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
        IReadOnlyList<int> abilityPool, CancellationToken ct)
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
            random.Derive("abilities-256"));

        for (var row = 1; row < rows; row++) // row 0 is a placeholder, not a species
        {
            ct.ThrowIfCancellationRequested();
            RandomizeEntry(packed, row * PersonalEntry7.Size, sources, typeCount, abilityPool);
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

    /// <summary>One random stream per aspect, so switching one off leaves the others alone.</summary>

    /// <param name="ByteZeroAbilities">
    /// For the one kind of slot the old one-byte reader could not see: an ability whose own byte is zero and whose
    /// ninth bit is set, which is exactly 256 (§132).
    /// </param>
    private readonly record struct EntrySources(
        IRandomSource Types, IRandomSource Stats, IRandomSource Abilities, IRandomSource ByteZeroAbilities);

    private void RandomizeEntry(byte[] table, int at, EntrySources sources, int typeCount,
        IReadOnlyList<int> abilityPool)
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

        if (options.RandomizeAbilities)
        {
            RandomizeAbilities(table, at, sources.Abilities, sources.ByteZeroAbilities, abilityPool);
        }
    }

    /// <summary>
    /// Deals a new ability to every slot of one entry that has one, from <paramref name="pool"/>.
    /// </summary>
    /// <remarks>
    /// Public and static so that the rule can be tested without a ROM: the whole module needs a workspace, and this
    /// is the part that got the ninth bit wrong (§132).
    /// </remarks>
    /// <param name="byteZero">
    /// Where the draws for a slot holding exactly 256 come from; see <see cref="EntrySources.ByteZeroAbilities"/>.
    /// </param>
    /// <param name="pool">What may be dealt, from <see cref="AbilityTable.Assignable"/>. Never empty.</param>
    public static void RandomizeAbilities(byte[] table, int at, IRandomSource abilities, IRandomSource byteZero,
        IReadOnlyList<int> pool)
    {
        for (var slot = 0; slot < PersonalEntry7.AbilityOffsets.Length; slot++)
        {
            // Ability 0 means "no ability"; a slot that was empty stays empty.
            var current = PersonalEntry7.GetAbility(table, at, slot);

            if (current == 0)
            {
                continue;
            }

            // Una habilidad de 256 justos tiene su byte a cero, y el lector de un solo byte la tomaba por
            // hueco vacío: ni se randomizaba ni gastaba tirada. Ahora se randomiza, pero de su propia
            // fuente, porque si gastara de la de siempre desplazaría las habilidades de todas las filas de
            // detrás en un mundo que ya se está jugando (§132).
            var source = (current & 0xFF) == 0 ? byteZero : abilities;

            // SetAbility escribe también el noveno bit: una habilidad de hasta 255 lo apaga, que es lo
            // que antes no pasaba y dejaba «la 50» convertida en la 306.
            // Un índice en la lista: con la lista entera 1..máx es la misma tirada que el Next(1, máx + 1)
            // de antes, así que un mundo sin exclusiones sale idéntico (§136).
            PersonalEntry7.SetAbility(table, at, slot, pool[source.Next(pool.Count)]);
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

        var planner = new LearnsetPlanner(Facts(teachable), new LearnsetRules(
            options.LearnsetGoodDamagingPercent,
            options.EffectiveSameTypePercent(),
            options.LearnsetDamagingFloor,
            MoveCatalog.DetectPerfectAccuracy(
                [.. workspace.Config.Moves.Select(move => move.Accuracy)],
                workspace.Config.GetText(TextName.MoveNames)),
            options.LearnsetPowerTolerance,
            options.LearnsetReorderByPower));
        var factOf = FactBuilder();

        // Los tipos y las estadisticas se leen del fichero YA generado, no del cartucho: si esta
        // randomizacion ha cambiado los tipos, el sesgo tiene que seguir a los tipos que el
        // jugador va a ver, no a los que el Pokemon tenia antes.
        var personal = ReadPackedPersonal(mod);

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

            var at = species * PersonalEntry7.Size;
            var known = personal is not null && at + PersonalEntry7.Size <= personal.Length;

            // Lo que el cartucho tiene en cada hueco, leído antes de escribir: la curva que se conserva.
            var original = Enumerable.Range(0, pairs).Select(pair => factOf(BitConverter.ToUInt16(entry, pair * 4))).ToList();

            var moves = planner.Plan(
                pairs,
                LastLevelOne(entry, pairs),
                known ? PersonalEntry7.GetTypes(personal!, at) : (0, 0),
                known ? PersonalEntry7.GetStat(personal!, at, AttackStat) : 0,
                known ? PersonalEntry7.GetStat(personal!, at, SpecialAttackStat) : 0,
                random,
                original);

            for (var pair = 0; pair < pairs && pair < moves.Count; pair++)
            {
                BitConverter.GetBytes((ushort)moves[pair]).CopyTo(entry, pair * 4);
                changed++;
            }

            patcher.Write(species, entry);
        }

        return changed;
    }

    /// <summary>Index of the base Attack and Special Attack inside a personal entry.</summary>
    /// <remarks>
    /// Gen 6 and 7 order the six as HP, Attack, Defence, Speed, Special Attack, Special Defence,
    /// which is <b>not</b> the order they are shown in. Getting these two the wrong way round would
    /// hand every physical attacker special moves and would never fail.
    /// </remarks>
    private const int AttackStat = 1;
    private const int SpecialAttackStat = 4;

    /// <summary>The last slot learnt at level one, or -1 when it learns nothing there.</summary>
    /// <remarks>
    /// That slot is the one that gets the guaranteed attack, following the reference. An entry
    /// stores each move as (move, level), so the level is the halfword after the move.
    /// </remarks>
    private static int LastLevelOne(byte[] entry, int pairs)
    {
        var last = -1;

        for (var pair = 0; pair < pairs; pair++)
        {
            if (BitConverter.ToUInt16(entry, (pair * 4) + 2) <= 1)
            {
                last = pair;
            }
        }

        return last;
    }

    /// <summary>The packed personal table as this generation left it, or null if it cannot be read.</summary>
    private static byte[]? ReadPackedPersonal(LayeredFsMod mod)
    {
        try
        {
            using var patcher = new GarcPatcher(mod.Stage(GameFiles.Personal));

            return patcher.Read(patcher.FileCount - 1);
        }
        catch (Exception)
        {
            // Sin la tabla se sigue: lo que se pierde es el sesgo de tipo y el reparto fisico o
            // especial, no las reglas que importan -- sin repetidos y un ataque al nivel 1-.
            return null;
        }
    }

    /// <summary>What the planner needs to know about each move it may hand out.</summary>
    private IReadOnlyList<MoveFacts> Facts(IReadOnlyList<int> teachable)
    {
        var factOf = FactBuilder();

        return [.. teachable.Where(id => id < workspace.Config.Moves.Length).Select(factOf)];
    }

    /// <summary>What the planner knows about any move of this game, teachable or not: the originals are compared too.</summary>
    private Func<int, MoveFacts> FactBuilder()
    {
        var moves = workspace.Config.Moves;
        var names = workspace.Config.GetText(TextName.MoveNames);

        var (physical, special) = MoveCatalog.DetectCategories(
            [.. moves.Select(move => move.Category)], names);

        return id => id <= 0 || id >= moves.Length
            ? new MoveFacts(id, 0, 0, 0, 1, null)
            : new MoveFacts(
                id,
                moves[id].Type,
                moves[id].Power,
                moves[id].Accuracy,
                Math.Max(1, moves[id].HitMax),
                moves[id].Power == 0 ? null : moves[id].Category == physical,
                FixedDamage: moves[id].Power == 0 && (moves[id].Category == physical || moves[id].Category == special));
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
