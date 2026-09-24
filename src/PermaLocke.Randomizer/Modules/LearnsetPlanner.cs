using PermaLocke.Core.Abstractions;

namespace PermaLocke.Randomizer.Modules;

/// <param name="GoodDamagingPercent">How much of a learnset is forced to be a real attack.</param>
/// <param name="SameTypePercent">
/// How often a pick is taken from the Pokémon's own types, as a percentage, split between the two of a dual type.
/// Zero leaves every pick open.
/// </param>
/// <param name="DamagingFloor">Base power a move needs before it counts as an attack.</param>
/// <param name="PerfectAccuracy">The value the game writes for a move that cannot miss.</param>
/// <param name="PowerTolerance">
/// How far, as a share of its strength, a replacement attack may land from the one the cartridge had in that slot. Zero
/// turns it off and goes back to drawing from the whole catalogue with the quota of real attacks.
/// </param>
public sealed record LearnsetRules(
    int GoodDamagingPercent,
    int SameTypePercent,
    int DamagingFloor,
    int PerfectAccuracy,
    double PowerTolerance = 0,
    bool ReorderByPower = false);

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

    private readonly MoveFacts[] _attacks = [.. valid.Where(move => move.Physical is not null)];

    /// <param name="slots">How many moves this learnset holds. Levels are not touched.</param>
    /// <param name="lastLevelOne">
    /// Index of the last move learnt at level one, or -1 when it learns nothing there. That slot
    /// gets the guaranteed attack, which is where the reference puts it.
    /// </param>
    /// <param name="types">The Pokémon's types, for the bias. A repeat means it has only one.</param>
    /// <param name="attack">Base Attack, against <paramref name="specialAttack"/>.</param>
    /// <param name="original">
    /// The move the cartridge has in each slot. With a <see cref="LearnsetRules.PowerTolerance"/> above zero, each slot
    /// is filled after it: see <see cref="PlanAlongTheCurve"/>.
    /// </param>
    public IReadOnlyList<int> Plan(int slots, int lastLevelOne, (int First, int Second) types,
        int attack, int specialAttack, IRandomSource random, IReadOnlyList<MoveFacts>? original = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (slots <= 0 || valid.Count == 0)
        {
            return [];
        }

        if (rules.PowerTolerance > 0 && original is not null && original.Count == slots)
        {
            return PlanAlongTheCurve(original, lastLevelOne, types, attack, specialAttack, random);
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

        if (rules.ReorderByPower)
        {
            ReorderByPower(chosen);
        }

        return chosen;
    }

    /// <summary>
    /// The attacks, weakest first, into the slots that already held attacks; status moves stay where they are.
    /// </summary>
    /// <remarks>
    /// Universal Pokémon Randomizer's «reorder damaging moves», and pk3DS's <c>ReorderMovesPower</c>. The slot of the
    /// guaranteed attack at level one is one of those slots, so it still gets an attack: the weakest one, which is what
    /// a level one move should be.
    /// </remarks>
    private void ReorderByPower(List<int> chosen)
    {
        var facts = valid.ToDictionary(move => move.Id);
        var slots = Enumerable.Range(0, chosen.Count)
            .Where(slot => facts.TryGetValue(chosen[slot], out var move) && move.Physical is not null)
            .ToList();
        var sorted = slots.Select(slot => facts[chosen[slot]]).OrderBy(move => move.Strength).ToList();

        for (var i = 0; i < slots.Count; i++)
        {
            chosen[slots[i]] = sorted[i].Id;
        }
    }

    /// <summary>
    /// Fills each slot with a move like the one the cartridge has there: an attack of about the same strength, a status
    /// move for a status move, a fixed-damage move for a fixed-damage one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found playing on 2026-09-21: «almost every move I have or that hits me is 80 power or more». Measured on the
    /// installed world, the player was right. At level 5, 59% of the attacks a Pokémon carries were 80 or more and 89% of
    /// Pokémon carried one, against 5% and 8% on the cartridge, and the same at every level up to 30. The draw above
    /// takes every slot from the whole catalogue whatever its level, and half the catalogue's attacks are 80 or more.
    /// Level 1 looked like level 50.
    /// </para>
    /// <para>
    /// The references tackle it by reordering. pk3DS sorts the damaging moves by power and makes the first one a
    /// weak one, and Universal Pokémon Randomizer has the same «reorder damaging moves» option that was never copied
    /// here with the rest of its rules; that is <see cref="LearnsetRules.ReorderByPower"/>, and it is what the player
    /// chose. This is the other way, kept and tested but off: it keeps the cartridge's own curve. Which move,
    /// and of what type, is still random; how hard it hits at that level, and whether it is an attack at all, is what
    /// the game designed. The status share stays the cartridge's too, which the quota had lowered to half.
    /// </para>
    /// <para>
    /// The guaranteed attack at level one stays: a slot that was a status move there becomes an attack of about the
    /// damaging floor. No repeats, as before. Nothing is shuffled afterwards, because shuffling is exactly what would
    /// throw the curve away.
    /// </para>
    /// </remarks>
    private IReadOnlyList<int> PlanAlongTheCurve(IReadOnlyList<MoveFacts> original, int lastLevelOne,
        (int First, int Second) types, int attack, int specialAttack, IRandomSource random)
    {
        var chosen = new List<int>(original.Count);

        for (var slot = 0; slot < original.Count; slot++)
        {
            var was = original[slot];
            var hurts = was.Physical is not null;
            IReadOnlyList<MoveFacts> pool;

            if (hurts || slot == lastLevelOne)
            {
                pool = Near(hurts ? was.Strength : rules.DamagingFloor, chosen);

                var wantsPhysical = random.NextDouble() < Ratio(attack, specialAttack);
                var byCategory = pool.Where(move => move.Physical == wantsPhysical).ToList();

                if (Free(byCategory, chosen))
                {
                    pool = byCategory;
                }
            }
            else
            {
                var alike = valid.Where(move => move.Physical is null && move.FixedDamage == was.FixedDamage).ToList();
                pool = Free(alike, chosen) ? alike : valid;
            }

            if (Wanted(types, random, rules.SameTypePercent) is { } type)
            {
                var themed = pool.Where(move => move.Type == type).ToList();

                if (Free(themed, chosen))
                {
                    pool = themed;
                }
            }

            chosen.Add(Pick(pool, chosen, random));
        }

        return chosen;
    }

    /// <summary>Least width of the band, so a 20-power slot is not held to moves of exactly 15 to 25.</summary>
    private const int MinimumBand = 10;

    /// <summary>
    /// Attacks within the tolerance of <paramref name="strength"/>, the band doubling while it has nothing left to give.
    /// </summary>
    private IReadOnlyList<MoveFacts> Near(int strength, List<int> chosen)
    {
        var width = Math.Max(MinimumBand, strength * rules.PowerTolerance);

        for (var widening = 0; widening < 4; widening++, width *= 2)
        {
            var near = _attacks.Where(move => Math.Abs(move.Strength - strength) <= width).ToList();

            if (Free(near, chosen))
            {
                return near;
            }
        }

        return Free(_attacks, chosen) ? _attacks : valid;
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

        if (Wanted(types, random, rules.SameTypePercent) is { } type)
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
    private static int? Wanted((int First, int Second) types, IRandomSource random, int percent)
    {
        if (percent <= 0)
        {
            return null;
        }

        var share = Math.Min(percent, 100) / 100.0;
        var picked = random.NextDouble();

        // Un solo tipo se lleva la parte entera; dos, la mitad cada uno, que es como la reparte el Universal
        // Pokémon Randomizer con su cuarenta por ciento.
        return types.First == types.Second
            ? picked < share ? types.First : null
            : picked < share / 2 ? types.First
            : picked < share ? types.Second
            : null;
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
