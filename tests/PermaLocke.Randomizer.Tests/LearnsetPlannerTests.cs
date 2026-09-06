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
        GoodDamagingPercent: 30, PreferSameType: false, DamagingFloor: 50, PerfectAccuracy: 101);

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
