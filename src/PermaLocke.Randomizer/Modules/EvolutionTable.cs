using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Who evolves into whom, read out of <c>a/0/1/4</c>.
/// </summary>
/// <remarks>
/// <para>
/// One subfile per species, entries of eight bytes: the method is a <c>u16</c> at 0 and the target
/// species a <c>u16</c> at 4. A method of zero is an unused slot. That is the same layout the data
/// randomizer patches, read here instead of written.
/// </para>
/// <para>
/// Built to answer one question — <b>does this species have two evolutions ahead of it</b> — which
/// is what the competition asks of a starter. Deliberately not a general evolution model: it does
/// not care about methods, levels or items, only about the shape of the family.
/// </para>
/// </remarks>
public sealed class EvolutionTable
{
    private const int EntrySize = 8;
    private const int MethodOffset = 0;
    private const int SpeciesOffset = 4;

    private readonly IReadOnlyList<int>[] _into;
    private readonly bool[] _isEvolution;
    private readonly int?[] _stages;

    private EvolutionTable(IReadOnlyList<int>[] into, bool[] isEvolution)
    {
        _into = into;
        _isEvolution = isEvolution;
        _stages = new int?[into.Length];
    }

    public int Count => _into.Length - 1;

    /// <summary>
    /// Builds one from the targets directly, indexed by species; index 0 is unused.
    /// </summary>
    /// <remarks>
    /// So the part with actual logic — depth, branching, loops — can be tested without a cartridge.
    /// <see cref="Read"/> is then only the byte reading, which is anchored against the real ROM.
    /// </remarks>
    public static EvolutionTable FromTargets(IReadOnlyList<int>[] into)
    {
        ArgumentNullException.ThrowIfNull(into);

        var isEvolution = new bool[into.Length];
        var clean = new IReadOnlyList<int>[into.Length];

        for (var species = 0; species < into.Length; species++)
        {
            // Se limpia igual que en Read, y no solo se ignora al marcar: una especie que
            // evoluciona en si misma dejada en la lista se cuenta como una etapa mas.
            var targets = (into[species] ?? [])
                .Where(target => target > 0 && target < into.Length && target != species)
                .Distinct()
                .ToList();

            clean[species] = targets;

            foreach (var target in targets)
            {
                isEvolution[target] = true;
            }
        }

        return new EvolutionTable(clean, isEvolution);
    }

    public static EvolutionTable Read(string garcPath)
    {
        using var patcher = new GarcPatcher(garcPath);

        var into = new IReadOnlyList<int>[patcher.FileCount];
        var isEvolution = new bool[patcher.FileCount];

        for (var species = 0; species < patcher.FileCount; species++)
        {
            var entry = patcher.Read(species);
            var targets = new List<int>();

            for (var at = 0; at + EntrySize <= entry.Length; at += EntrySize)
            {
                if (BitConverter.ToUInt16(entry, at + MethodOffset) == 0)
                {
                    continue;
                }

                var target = BitConverter.ToUInt16(entry, at + SpeciesOffset);

                // Una especie que evoluciona en si misma no es una etapa mas, es ruido.
                if (target > 0 && target < patcher.FileCount && target != species && !targets.Contains(target))
                {
                    targets.Add(target);
                    isEvolution[target] = true;
                }
            }

            into[species] = targets;
        }

        return new EvolutionTable(into, isEvolution);
    }

    /// <summary>True when nothing evolves into this species, so it is the start of its family.</summary>
    public bool IsBase(int species) =>
        species > 0 && species < _isEvolution.Length && !_isEvolution[species];

    /// <summary>
    /// How many stages the longest chain from here has, counting this one.
    /// </summary>
    /// <remarks>
    /// The longest and not the shortest, because a branching family — Wurmple, Eevee — is as deep
    /// as its deepest branch. Guarded against a loop, which the vanilla cartridge does not have but
    /// a randomized evolution table can: without the guard this would recurse until the stack ran
    /// out, and a randomizer is exactly where that gets built.
    /// </remarks>
    public int Stages(int species)
    {
        if (species <= 0 || species >= _into.Length)
        {
            return 0;
        }

        var cut = false;
        return Depth(species, [], ref cut);
    }

    /// <param name="cut">
    /// Set when a loop had to be broken somewhere below. Such an answer depends on where the walk
    /// started, so it is <b>not</b> memoised: caching it would make the table give different
    /// answers depending on the order it was asked, which is the worst kind of wrong.
    /// </param>
    private int Depth(int species, HashSet<int> seen, ref bool cut)
    {
        if (_stages[species] is { } known)
        {
            return known;
        }

        if (!seen.Add(species))
        {
            // Ya estabamos aqui. Se corta contando cero: volver por donde se vino no es una etapa.
            cut = true;
            return 0;
        }

        var deepest = 0;
        var cutBelow = false;

        foreach (var target in _into[species])
        {
            var cutHere = false;
            deepest = Math.Max(deepest, Depth(target, seen, ref cutHere));
            cutBelow |= cutHere;
        }

        seen.Remove(species);

        var stages = deepest + 1;

        if (!cutBelow)
        {
            _stages[species] = stages;
        }

        cut = cutBelow;
        return stages;
    }

    /// <summary>
    /// A family of exactly three stages, seen from its first one: two evolutions ahead.
    /// </summary>
    public bool HasTwoEvolutionsAhead(int species) => IsBase(species) && Stages(species) == 3;

    /// <summary>
    /// Where this species' line ends: the last stage it can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A species that does not evolve is already its own final form and comes back unchanged, so a
    /// caller never has to ask whether it should call this.
    /// </para>
    /// <para>
    /// A branching family has more than one ending — Eevee has eight — and this walks the
    /// <b>deepest</b> branch, taking the lowest species id to break a tie. Deepest because that is
    /// what «fully evolved» means when the branches differ in length (Wurmple), and lowest id
    /// because the alternative is picking at random, and then the same trainer would come out
    /// different every time the same seed was used. A randomizer that cannot be recomputed cannot
    /// be audited.
    /// </para>
    /// <para>
    /// Loops get the same treatment as in <see cref="Depth"/> and for the same reason: the vanilla
    /// cartridge has none, but a randomized evolution table can, and this is exactly the code that
    /// would recurse until the stack ran out.
    /// </para>
    /// </remarks>
    public int FinalOf(int species)
    {
        if (species <= 0 || species >= _into.Length)
        {
            return species;
        }

        return Last(species, []);
    }

    private int Last(int species, HashSet<int> seen)
    {
        if (!seen.Add(species) || _into[species].Count == 0)
        {
            return species;
        }

        var best = species;
        var bestDepth = 0;

        foreach (var target in _into[species])
        {
            var end = Last(target, seen);
            var depth = Stages(target);

            if (depth > bestDepth || (depth == bestDepth && end < best))
            {
                best = end;
                bestDepth = depth;
            }
        }

        seen.Remove(species);
        return best;
    }
}
