using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The competition's first-encounter rule as the player set it on 2026-09-14: the first wild battle in a route spends
/// it, a duplicate line cannot be caught and does not spend it, a shiny can always be caught.
/// </summary>
public sealed class EncounterPolicyTests
{
    private static readonly FieldZone Route2 = new(7, 0, "ruta-2", "Ruta 2");

    private static EncounterDecision Decide(EncounterSituation situation, bool shinyConsumes = false) =>
        EncounterPolicy.Decide(situation, shinyConsumes);

    [Fact]
    public void Nowhere_known_keeps_the_balls()
    {
        var decision = Decide(new EncounterSituation(null, IsRoute: false, Spent: false, InWildBattle: false));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
    }

    /// <summary>§179: una zona de prueba sin su cristal Z no tiene Poké Balls ni gasta la ruta; un variocolor, sí.</summary>
    [Fact]
    public void A_trial_zone_before_its_crystal_has_no_balls_and_spends_nothing()
    {
        var walking = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: false, PendingTrial: "prueba de Lulú"));
        var battle = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true, WildSpecies: 19,
            PendingTrial: "prueba de Lulú"));
        var shiny = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true, WildSpecies: 19,
            Shiny: true, PendingTrial: "prueba de Lulú"));
        var after = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true, WildSpecies: 19));

        Assert.Equal(BallAction.Withhold, walking.Action);
        Assert.Equal(BallAction.Withhold, battle.Action);
        Assert.False(battle.SpendZone);
        Assert.Equal(BallAction.GiveBack, shiny.Action);
        Assert.Equal(BallAction.GiveBack, after.Action);
        Assert.True(after.SpendZone);
    }

    private static readonly FieldZone VolcanoTunnel = new(0, 0, "tunel-del-volcan", "Túnel del Volcán");
    private static readonly FieldZone Lanakila = new(200, 136, "monte-lanakila", "Monte Lanakila");
    private static readonly FieldZone ConflictRuins = new(22, 9, "ruinas-de-la-guerra", "Ruinas de la Guerra");

    /// <summary>§119: the places left off the map were left off on purpose.</summary>
    [Fact]
    public void Walking_off_the_map_takes_the_balls()
    {
        var decision = Decide(new EncounterSituation(VolcanoTunnel, IsRoute: false, Spent: false, InWildBattle: false));

        Assert.Equal(BallAction.Withhold, decision.Action);
        Assert.Contains("no está en el mapa", decision.Reason);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public void A_wild_battle_off_the_map_cannot_be_caught_and_spends_nothing(int? species, bool overdue)
    {
        var decision = Decide(new EncounterSituation(VolcanoTunnel, IsRoute: false, Spent: false, InWildBattle: true,
            WildSpecies: species, SpeciesOverdue: overdue));

        Assert.Equal(BallAction.Withhold, decision.Action);
        Assert.False(decision.SpendZone);
    }

    [Fact]
    public void A_shiny_off_the_map_can_still_be_caught()
    {
        var decision = Decide(new EncounterSituation(VolcanoTunnel, IsRoute: false, Spent: false, InWildBattle: true,
            WildSpecies: 10, Shiny: true), shinyConsumes: true);

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
    }

    [Fact]
    public void An_allowed_static_off_the_map_can_be_caught_and_spends_nothing()
    {
        var decision = Decide(new EncounterSituation(ConflictRuins, IsRoute: false, Spent: false, InWildBattle: true,
            WildSpecies: 939, AllowedStatic: "Tapu Koko", HasAllowedStatic: true));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
        Assert.Contains("Tapu Koko", decision.Reason);
    }

    /// <summary>Monte Lanakila is a route: the Necrozma there is catchable even with the route spent, and spends nothing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_Lanakila_Necrozma_can_be_caught_whether_the_route_is_spent_or_not(bool spent)
    {
        var decision = Decide(new EncounterSituation(Lanakila, IsRoute: true, spent, InWildBattle: true,
            WildSpecies: 1001, Duplicate: true, AllowedStatic: "Necrozma del Monte Lanakila", HasAllowedStatic: true));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
    }

    /// <summary>The same mountain's ordinary wild Pokémon follow the route rule.</summary>
    [Fact]
    public void Any_other_battle_on_Lanakila_follows_the_route()
    {
        var decision = Decide(new EncounterSituation(Lanakila, IsRoute: true, Spent: true, InWildBattle: true,
            WildSpecies: 215, HasAllowedStatic: true));

        Assert.Equal(BallAction.Withhold, decision.Action);
    }

    [Fact]
    public void Off_the_map_an_unreadable_battle_where_an_allowed_capture_lives_goes_to_the_player()
    {
        var decision = Decide(new EncounterSituation(ConflictRuins, IsRoute: false, Spent: false, InWildBattle: true,
            SpeciesOverdue: true, HasAllowedStatic: true));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
    }

    [Fact]
    public void An_allowed_static_is_only_allowed_in_a_battle()
    {
        var decision = Decide(new EncounterSituation(ConflictRuins, IsRoute: false, Spent: false, InWildBattle: false,
            AllowedStatic: "Tapu Koko", HasAllowedStatic: true));

        Assert.Equal(BallAction.Withhold, decision.Action);
    }

    [Fact]
    public void Walking_in_a_spent_route_takes_the_balls()
    {
        Assert.Equal(BallAction.Withhold,
            Decide(new EncounterSituation(Route2, IsRoute: true, Spent: true, InWildBattle: false)).Action);
    }

    [Fact]
    public void Walking_in_a_free_route_keeps_them()
    {
        Assert.Equal(BallAction.GiveBack,
            Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: false)).Action);
    }

    [Fact]
    public void The_first_wild_battle_of_a_route_can_be_caught_and_spends_it()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true, WildSpecies: 506));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.True(decision.SpendZone);
    }

    [Fact]
    public void A_battle_in_a_spent_route_cannot_be_caught()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: true, InWildBattle: true, WildSpecies: 506));

        Assert.Equal(BallAction.Withhold, decision.Action);
        Assert.False(decision.SpendZone);
    }

    [Fact]
    public void Until_the_species_is_read_there_are_no_balls_and_nothing_is_spent()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true));

        Assert.Equal(BallAction.Withhold, decision.Action);
        Assert.False(decision.SpendZone);
    }

    [Fact]
    public void A_species_that_never_becomes_readable_goes_in_the_players_favour()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true, SpeciesOverdue: true));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.True(decision.SpendZone);
    }

    [Fact]
    public void A_duplicate_cannot_be_caught_and_leaves_the_route_free()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true,
            WildSpecies: 657, Duplicate: true));

        Assert.Equal(BallAction.Withhold, decision.Action);
        Assert.False(decision.SpendZone);
        Assert.Contains("sigue libre", decision.Reason);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void A_shiny_can_always_be_caught(bool spent, bool duplicate)
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, spent, InWildBattle: true,
            WildSpecies: 506, Shiny: true, Duplicate: duplicate));

        Assert.Equal(BallAction.GiveBack, decision.Action);
    }

    [Fact]
    public void A_shiny_even_when_it_is_a_duplicate()
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true,
            WildSpecies: 657, Shiny: true, Duplicate: true));

        Assert.Equal(BallAction.GiveBack, decision.Action);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Whether_a_shiny_spends_the_route_is_the_shiny_clause_setting(bool consumes, bool spends)
    {
        var decision = Decide(new EncounterSituation(Route2, IsRoute: true, Spent: false, InWildBattle: true,
            WildSpecies: 506, Shiny: true), consumes);

        Assert.Equal(spends, decision.SpendZone);
    }

    [Fact]
    public void A_battle_whose_zone_is_unknown_keeps_the_balls_and_spends_nothing()
    {
        var decision = Decide(new EncounterSituation(null, IsRoute: false, Spent: false, InWildBattle: true, WildSpecies: 506));

        Assert.Equal(BallAction.GiveBack, decision.Action);
        Assert.False(decision.SpendZone);
    }

    /// <summary>The trainer card at the start of a battle: wild battles, caught, fled, shiny.</summary>
    private static readonly BattleCounters Before = new(198, 15, 31, 4);

    [Fact]
    public void A_capture_counted_by_the_game_marks_caught_even_if_the_tables_saw_it_at_zero()
    {
        // Un salvaje atrapado no llega a cero, pero la captura manda sobre cualquier otra lectura.
        var (outcome, _) = EncounterPolicy.Ending(Before, Before with { Caught = 16 }, wildFainted: true);

        Assert.Equal(ZoneOutcome.Caught, outcome);
    }

    [Fact]
    public void A_flee_counted_by_the_game_marks_fled()
    {
        Assert.Equal(ZoneOutcome.Fled, EncounterPolicy.Ending(Before, Before with { Fled = 32 }, wildFainted: false).Outcome);
    }

    [Fact]
    public void A_wild_pokemon_seen_at_zero_marks_died()
    {
        Assert.Equal(ZoneOutcome.Died, EncounterPolicy.Ending(Before, Before, wildFainted: true).Outcome);
    }

    /// <summary>
    /// Nothing counted and nothing seen falling still marks the route: it is spent, and nobody can mark it by hand.
    /// </summary>
    [Fact]
    public void An_ending_with_nothing_counted_is_marked_as_got_away_and_says_so()
    {
        var (outcome, how) = EncounterPolicy.Ending(Before, Before, wildFainted: false);

        Assert.Equal(ZoneOutcome.Fled, outcome);
        Assert.Contains("Se escapó o perdiste", how);
    }
}
