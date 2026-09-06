using PermaLocke.Core.Abstractions;

namespace PermaLocke.Randomizer.Modules;

/// <param name="GoodDamagingPercent">How much of a learnset is forced to be a real attack.</param>
/// <param name="PreferSameType">Bias the picks towards the Pokémon's own types.</param>
/// <param name="DamagingFloor">Base power a move needs before it counts as an attack.</param>
/// <param name="PerfectAccuracy">The value the game writes for a move that cannot miss.</param>
public sealed record LearnsetRules(
    int GoodDamagingPercent,
    bool PreferSameType,
    int DamagingFloor,
    int PerfectAccuracy);

/// <summary>
/// Chooses the moves of one Pokémon's level-up learnset.
/// </summary>
/// <remarks>
/// <para>
/// This is Universal Pokémon Randomizer's <c>randomizeMovesLearnt</c>, rule for rule, because a
/// straight uniform draw — which is what PermaLocke did — produces learnsets that are unplayable
/// in ways a Nuzlocke cannot absorb. Three of its rules matter more than the rest:
/// </para>
/// <para>
/// <b>No repeats.</b> A uniform draw with replacement gives a Pokémon the same move three times,
/// and a moveset of three identical attacks is a Pokémon with one attack.
/// </para>
/// <para>
/// <b>Something to hit with at level one.</b> The last slot learnt at level one is forced to be a
/// real attack. Without it a freshly caught Pokémon can come with four status moves and be unable
/// to do anything at all, which in a Nuzlocke is an encounter thrown away.
/// </para>
/// <para>
/// <b>A share of real attacks.</b> Beyond that first one, a percentage of the learnset is drawn
/// from moves that actually hurt, so a Pokémon does not spend twenty levels learning nothing but
/// stat drops.
/// </para>
/// <para>
/// The physical or special choice follows the Pokémon's own Attack against Special Attack, so a
/// bruiser tends to be handed physical moves. It is a nicety rather than a fix, but it is free
/// once the category is known.
/// </para>
/// <para>
/// Kept apart from the file writing on purpose: everything here is decided from numbers, so the
/// rules can be tested without a ROM, which is the only way to know that «no repeats» really holds
/// for a Pokémon with more slots than there are good attacks.
/// </para>
/// </remarks>
public sealed class LearnsetPlanner(IReadOnlyList<MoveFacts> valid, LearnsetRules rules)
{
    private readonly MoveFacts[] _damaging =
        [.. valid.Where(move => move.IsGoodDamaging(rules.DamagingFloor, rules.PerfectAccuracy))];

    /// <param name="slots">How many moves this learnset holds. Levels are not touched.</param>
    /// <param name="lastLevelOne">
    /// Index of the last move learnt at level one, or -1 when it learns nothing there. That slot
    /// gets the guaranteed attack, which is where the reference puts it.
    /// </param>
    /// <param name="types">The Pokémon's types, for the bias. A repeat means it has only one.</param>
    /// <param name="attack">Base Attack, against <paramref name="specialAttack"/>.</param>
    public IReadOnlyList<int> Plan(int slots, int lastLevelOne, (int First, int Second) types,
        int attack, int specialAttack, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (slots <= 0 || valid.Count == 0)
        {
            return [];
        }

        var chosen = new List<int>(slots);
        var quota = (int)Math.Round(rules.GoodDamagingPercent / 100.0 * slots);
        var guaranteed = 0;

        for (var slot = 0; slot < slots; slot++)
        {
            var mustHurt = slot == lastLevelOne || quota > 0;
            var pool = Pool(mustHurt, types, attack, specialAttack, chosen, random);
            var move = Pick(pool, chosen, random);

            if (slot == lastLevelOne)
            {
                guaranteed = move;
            }
            else if (mustHurt)
            {
                quota--;
            }

            chosen.Add(move);
        }

        // Barajado, y despues el ataque garantizado vuelve a su hueco. Sin barajar, la cuota de
        // ataques se amontona al principio del aprendizaje y el Pokemon deja de aprender nada util
        // a partir de la mitad; el intercambio de despues es lo que hace que barajar no se lleve
        // por delante lo unico que este metodo promete.
        Shuffle(chosen, random);

        if (lastLevelOne >= 0 && lastLevelOne < chosen.Count && chosen[lastLevelOne] != guaranteed)
        {
            var at = chosen.IndexOf(guaranteed);

            (chosen[at], chosen[lastLevelOne]) = (chosen[lastLevelOne], guaranteed);
        }

        return chosen;
    }

    /// <summary>
    /// The narrowest list that still has something left to give, widening as each filter runs dry.
    /// </summary>
    /// <remarks>
    /// Every narrowing is checked for a move this Pokémon has not learnt yet before it is taken.
    /// Without that, a learnset with more slots than there are, say, physical Fire attacks would
    /// spin for ever looking for one that does not repeat.
    /// </remarks>
    private IReadOnlyList<MoveFacts> Pool(bool mustHurt, (int First, int Second) types,
        int attack, int specialAttack, List<int> chosen, IRandomSource random)
    {
        IReadOnlyList<MoveFacts> pool = valid;

        if (mustHurt && Free(_damaging, chosen))
        {
            pool = _damaging;
        }

        if (rules.PreferSameType && Wanted(types, random) is { } type)
        {
            var themed = pool.Where(move => move.Type == type).ToList();

            if (Free(themed, chosen))
            {
                pool = themed;
            }
        }

        if (!mustHurt)
        {
            return pool;
        }

        // Fisico o especial segun a que pegue mejor este Pokemon. Con ambos ataques iguales sale
        // mitad y mitad, que es lo que hace la referencia.
        var wantsPhysical = random.NextDouble() < Ratio(attack, specialAttack);
        var byCategory = pool.Where(move => move.Physical == wantsPhysical).ToList();

        return Free(byCategory, chosen) ? byCategory : pool;
    }

    /// <summary>
    /// How likely a forced attack is physical: the Pokémon's own Attack over the two together.
    /// </summary>
    private static double Ratio(int attack, int specialAttack) =>
        attack + specialAttack <= 0 ? 0.5 : (double)attack / (attack + specialAttack);

    /// <summary>Which type to lean towards, or null to leave it open. The reference's odds.</summary>
    private static int? Wanted((int First, int Second) types, IRandomSource random)
    {
        var picked = random.NextDouble();
        var dual = types.First != types.Second;

        if (!dual)
        {
            // Un solo tipo: 40% suyo, 60% libre.
            return picked < 0.4 ? types.First : null;
        }

        return picked switch
        {
            < 0.2 => types.First,
            < 0.4 => types.Second,
            _ => null
        };
    }

    private static bool Free(IReadOnlyList<MoveFacts> pool, List<int> chosen) =>
        pool.Any(move => !chosen.Contains(move.Id));

    private static int Pick(IReadOnlyList<MoveFacts> pool, List<int> chosen, IRandomSource random)
    {
        // Sin repetir, y sin poder colgarse: Pool solo entrega listas con algo libre, asi que
        // recorrer la lista desde un punto al azar siempre encuentra uno.
        var start = random.Next(0, pool.Count);

        for (var step = 0; step < pool.Count; step++)
        {
            var move = pool[(start + step) % pool.Count];

            if (!chosen.Contains(move.Id))
            {
                return move.Id;
            }
        }

        // Solo si TODO el catalogo ya esta aprendido, que exige un Pokemon con mas huecos que
        // movimientos hay. Entonces se repite, porque un hueco hay que llenarlo con algo.
        return pool[start].Id;
    }

    private static void Shuffle(List<int> moves, IRandomSource random)
    {
        for (var i = moves.Count - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);

            (moves[i], moves[j]) = (moves[j], moves[i]);
        }
    }
}
