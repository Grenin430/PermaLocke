using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The rules a randomized level-up learnset has to obey.
/// </summary>
/// <remarks>
/// PermaLocke used to draw each move uniformly at random, with replacement and no other rule, and
/// the three things that fixes are all here: no repeats, something to attack with at level one, and
/// a share of moves that actually hurt. They are worth pinning rather than eyeballing a generated
/// mod, because the cases that matter are the awkward ones — a Pokémon with more slots than there
/// are good attacks, or one that learns nothing at level one at all — and those are rare enough in
/// a real cartridge to go unnoticed for months.
/// </remarks>
public class LearnsetPlannerTests
{
    private static readonly LearnsetRules Rules = new(
        GoodDamagingPercent: 30, SameTypePercent: 0, DamagingFloor: 50, PerfectAccuracy: 101);

    /// <summary>A catalogue with both kinds, so the filters have somewhere to go.</summary>
    private static List<MoveFacts> Catalogue(int attacks = 40, int status = 40)
    {
        var moves = new List<MoveFacts>();

        for (var i = 0; i < attacks; i++)
        {
            // Potencia 80 y precision 100: buen movimiento de dano por las dos mitades de la regla.
            moves.Add(new MoveFacts(1000 + i, i % 18, Power: 80, Accuracy: 100, Hits: 1,
                Physical: i % 2 == 0));
        }

        for (var i = 0; i < status; i++)
        {
            moves.Add(new MoveFacts(2000 + i, i % 18, Power: 0, Accuracy: 100, Hits: 1, Physical: null));
        }

        return moves;
    }

    private static LearnsetPlanner Planner(List<MoveFacts>? catalogue = null, LearnsetRules? rules = null) =>
        new(catalogue ?? Catalogue(), rules ?? Rules);

    [Fact]
    public void No_pokemon_learns_the_same_move_twice()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var moves = Planner().Plan(slots: 20, lastLevelOne: 1, (10, 4), 100, 60,
                new SeededRandomSource((ulong)seed));

            Assert.Equal(20, moves.Count);
            Assert.Equal(moves.Count, moves.Distinct().Count());
        }
    }

    /// <summary>
    /// The one that costs an encounter: a freshly caught Pokémon with four status moves cannot do
    /// anything at all, and in a Nuzlocke that is the Pokémon thrown away.
    /// </summary>
    [Fact]
    public void The_last_move_learnt_at_level_one_is_always_an_attack()
    {
        var catalogue = Catalogue();

        for (var seed = 0; seed < 40; seed++)
        {
            var moves = Planner(catalogue).Plan(slots: 12, lastLevelOne: 2, (10, 4), 100, 60,
                new SeededRandomSource((ulong)seed));

            var guaranteed = catalogue.Single(move => move.Id == moves[2]);

            Assert.True(guaranteed.IsGoodDamaging(Rules.DamagingFloor, Rules.PerfectAccuracy),
                $"la semilla {seed} dejó el hueco de nivel 1 con un movimiento de estado");
        }
    }

    [Fact]
    public void A_share_of_the_learnset_really_hurts()
    {
        var catalogue = Catalogue();

        for (var seed = 0; seed < 20; seed++)
        {
            var moves = Planner(catalogue).Plan(slots: 20, lastLevelOne: 0, (10, 4), 100, 60,
                new SeededRandomSource((ulong)seed));

            var hurting = moves.Count(id =>
                catalogue.Single(move => move.Id == id)
                    .IsGoodDamaging(Rules.DamagingFloor, Rules.PerfectAccuracy));

            // Seis por la cuota del 30% mas el garantizado de nivel 1, y nunca menos.
            Assert.True(hurting >= 6, $"la semilla {seed} solo dio {hurting} ataques de 20");
        }
    }

    /// <summary>
    /// A Pokémon that learns nothing at level one is not a special case to crash on.
    /// </summary>
    [Fact]
    public void A_learnset_with_no_level_one_move_is_planned_all_the_same()
    {
        var moves = Planner().Plan(slots: 8, lastLevelOne: -1, (10, 4), 100, 60,
            new SeededRandomSource(7));

        Assert.Equal(8, moves.Count);
        Assert.Equal(moves.Count, moves.Distinct().Count());
    }

    /// <summary>
    /// More slots than there are moves: it fills them and comes back, which is the case that turns
    /// a «pick until it is not a repeat» loop into a hang.
    /// </summary>
    [Fact]
    public void More_slots_than_moves_fills_them_instead_of_spinning_for_ever()
    {
        var tiny = Catalogue(attacks: 3, status: 2);

        var moves = Planner(tiny).Plan(slots: 9, lastLevelOne: 0, (10, 4), 100, 60,
            new SeededRandomSource(3));

        Assert.Equal(9, moves.Count);
        Assert.All(moves, id => Assert.Contains(tiny, move => move.Id == id));
    }

    /// <summary>
    /// A Pokémon with no Special Attack at all gets physical attacks, deterministically: the ratio
    /// is one, so the draw cannot land anywhere else.
    /// </summary>
    [Fact]
    public void A_pure_physical_attacker_is_handed_physical_attacks()
    {
        var catalogue = Catalogue();

        var moves = Planner(catalogue).Plan(slots: 16, lastLevelOne: 0, (10, 4),
            attack: 130, specialAttack: 0, new SeededRandomSource(11));

        var forced = catalogue.Single(move => move.Id == moves[0]);

        Assert.True(forced.Physical);
    }

    /// <summary>Attacks of every strength from 10 to 150, and status moves, so power can be told apart.</summary>
    private static List<MoveFacts> Graded()
    {
        var moves = new List<MoveFacts>();

        for (var i = 0; i < 60; i++)
        {
            moves.Add(new MoveFacts(3000 + i, i % 18, Power: 10 + (i % 15) * 10, Accuracy: 100, Hits: 1,
                Physical: i % 2 == 0));
        }

        for (var i = 0; i < 30; i++)
        {
            moves.Add(new MoveFacts(4000 + i, i % 18, Power: 0, Accuracy: 100, Hits: 1, Physical: null));
        }

        return moves;
    }

    /// <summary>
    /// Chosen by the player on 2026-09-21, after noticing nearly every move was 80 or more from level 5 on: the
    /// attacks go weakest first, as Universal Pokémon Randomizer's «reorder damaging moves» and pk3DS do.
    /// </summary>
    [Fact]
    public void Reordered_attacks_go_from_weakest_to_strongest()
    {
        var catalogue = Graded();
        var facts = catalogue.ToDictionary(move => move.Id);
        var rules = Rules with { ReorderByPower = true };

        for (var seed = 0; seed < 30; seed++)
        {
            var moves = Planner(catalogue, rules).Plan(slots: 18, lastLevelOne: 1, (10, 4), 100, 60,
                new SeededRandomSource((ulong)seed));

            var strengths = moves.Select(id => facts[id]).Where(move => move.Physical is not null)
                .Select(move => move.Strength).ToList();

            Assert.Equal(strengths.Order(), strengths);
            Assert.Equal(moves.Count, moves.Distinct().Count());
            Assert.NotNull(facts[moves[1]].Physical);   // el garantizado de nivel 1 sigue siendo un ataque
        }
    }

    /// <summary>Reordering only moves attacks among the slots that held attacks: the status moves stay put.</summary>
    [Fact]
    public void Reordering_leaves_the_status_moves_where_they_were()
    {
        var catalogue = Graded();
        var facts = catalogue.ToDictionary(move => move.Id);

        var plain = Planner(catalogue).Plan(18, 1, (10, 4), 100, 60, new SeededRandomSource(9));
        var sorted = Planner(catalogue, Rules with { ReorderByPower = true }).Plan(18, 1, (10, 4), 100, 60,
            new SeededRandomSource(9));

        for (var slot = 0; slot < plain.Count; slot++)
        {
            if (facts[plain[slot]].Physical is null)
            {
                Assert.Equal(plain[slot], sorted[slot]);
            }
        }

        Assert.Equal(plain.Order(), sorted.Order());
    }

    /// <summary>
    /// The other way, off but kept: each slot gets a move like the cartridge's there — an attack near its strength, a
    /// status move for a status move.
    /// </summary>
    [Fact]
    public void Along_the_curve_each_slot_follows_the_cartridge_s_move()
    {
        var catalogue = Graded();
        var facts = catalogue.ToDictionary(move => move.Id);
        var rules = Rules with { PowerTolerance = 0.25 };
        MoveFacts[] original =
        [
            new(1, 0, 40, 100, 1, true), new(2, 0, 0, 100, 1, null), new(3, 0, 60, 100, 1, false),
            new(4, 0, 0, 100, 1, null), new(5, 0, 90, 100, 1, true), new(6, 0, 120, 100, 1, false)
        ];

        for (var seed = 0; seed < 30; seed++)
        {
            var moves = Planner(catalogue, rules).Plan(original.Length, lastLevelOne: 0, (10, 4), 100, 60,
                new SeededRandomSource((ulong)seed), original);

            for (var slot = 0; slot < original.Length; slot++)
            {
                var was = original[slot];
                var now = facts[moves[slot]];

                if (was.Physical is null)
                {
                    Assert.Null(now.Physical);
                }
                else
                {
                    Assert.InRange(now.Strength, was.Strength - Math.Max(10, was.Strength / 4),
                        was.Strength + Math.Max(10, was.Strength / 4));
                }
            }

            Assert.Equal(moves.Count, moves.Distinct().Count());
        }
    }

    /// <summary>Along the curve too, a status move in the last level one slot becomes an attack.</summary>
    [Fact]
    public void Along_the_curve_the_level_one_slot_is_still_an_attack()
    {
        var catalogue = Graded();
        var facts = catalogue.ToDictionary(move => move.Id);
        MoveFacts[] original = [new(1, 0, 0, 100, 1, null), new(2, 0, 0, 100, 1, null), new(3, 0, 80, 100, 1, true)];

        var moves = Planner(catalogue, Rules with { PowerTolerance = 0.25 }).Plan(3, lastLevelOne: 1, (10, 4), 100, 60,
            new SeededRandomSource(4), original);

        Assert.Null(facts[moves[0]].Physical);
        Assert.NotNull(facts[moves[1]].Physical);
    }

    /// <summary>
    /// How much of a learnset lands in the Pokémon's own types, with a catalogue spread evenly over the eighteen.
    /// </summary>
    private static double OwnTypeShare(int percent)
    {
        var catalogue = Catalogue(attacks: 180, status: 180);
        var facts = catalogue.ToDictionary(move => move.Id);
        var planner = Planner(catalogue, Rules with { SameTypePercent = percent });
        int own = 0, total = 0;

        for (var species = 0; species < 200; species++)
        {
            var types = (species % 18, (species * 7) % 18);

            foreach (var move in planner.Plan(12, lastLevelOne: 1, types, 100, 60, new SeededRandomSource((ulong)species)))
            {
                total++;
                if (facts[move].Type == types.Item1 || facts[move].Type == types.Item2) own++;
            }
        }

        return own / (double)total;
    }

    /// <summary>
    /// Asked for on 2026-09-22: «my Pokémon all learn nearly the same thing». They did — every learnset was drawn from
    /// all eighteen types, so none of them had a type of its own. The share asked for is the share of the draws, and
    /// what comes out is more, because the open draws land on its types too (§166).
    /// </summary>
    [Fact]
    public void The_share_of_moves_in_the_pokemons_own_types_follows_the_setting()
    {
        var none = OwnTypeShare(0);
        var some = OwnTypeShare(20);
        var lots = OwnTypeShare(40);

        Assert.InRange(none, 0.05, 0.16);
        Assert.InRange(some, none + 0.08, none + 0.25);
        Assert.True(lots > some + 0.08, $"{lots:P0} no es bastante más que {some:P0}");
    }

    /// <summary>The competition asks for the reference's share, and the old boolean still means forty.</summary>
    [Fact]
    public void The_configuration_asks_for_the_share_of_the_reference()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(MegaFloorTests.Root(), "Data", "randomizer.json"));

        Assert.Equal(20, options.EffectiveSameTypePercent());
        Assert.Equal(40, new RandomizerOptions { LearnsetPreferSameType = true }.EffectiveSameTypePercent());
        Assert.Equal(0, new RandomizerOptions().EffectiveSameTypePercent());
    }

    /// <summary>
    /// And with no attacking move anywhere, it still returns a full learnset rather than throwing:
    /// the guarantee is «an attack if one exists», not «an attack or nothing».
    /// </summary>
    [Fact]
    public void A_catalogue_with_no_attacks_still_produces_a_learnset()
    {
        var moves = Planner(Catalogue(attacks: 0, status: 12)).Plan(slots: 6, lastLevelOne: 1,
            (10, 4), 100, 60, new SeededRandomSource(5));

        Assert.Equal(6, moves.Count);
        Assert.Equal(moves.Count, moves.Distinct().Count());
    }
}
