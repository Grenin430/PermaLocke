using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using static PermaLocke.Rules.Tests.RuleTestContext;

namespace PermaLocke.Rules.Tests;

public sealed class ShinyClauseTests
{
    [Fact]
    public void A_shiny_in_a_spent_zone_is_allowed_as_an_exception()
    {
        var context = Context(usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-1", "Ruta 1", EncounterType.Wild, IsShiny: true),
            context);

        Assert.Equal(RuleOutcome.AllowedWithException, result.Outcome);
        Assert.False(result.IsBlocked);
        Assert.Equal(RuleIds.ShinyClause, result.Primary!.RuleId);
    }

    [Fact]
    public void The_block_it_lifted_stays_visible_in_the_results()
    {
        var context = Context(usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-1", "Ruta 1", EncounterType.Wild, IsShiny: true),
            context);

        // The first-encounter verdict is not deleted, only marked as overridden, so the
        // history can show exactly what was excused.
        var firstEncounter = Assert.Single(result.Results, r => r.RuleId == RuleIds.FirstEncounter);
        Assert.Equal(RuleOutcome.Blocked, firstEncounter.Outcome);
        Assert.True(firstEncounter.Overridden);
    }

    [Fact]
    public void With_the_clause_disabled_a_shiny_is_blocked_like_any_other_encounter()
    {
        var context = Context(
            configuration: With((RuleIds.ShinyClause, new RuleSettings { Enabled = false, Mode = RuleMode.Disabled })),
            usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-1", "Ruta 1", EncounterType.Wild, IsShiny: true),
            context);

        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void A_shiny_in_a_free_zone_needs_no_exception()
    {
        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-1", "Ruta 1", EncounterType.Wild, IsShiny: true),
            Context());

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }
}

public sealed class DupesClauseTests
{
    [Fact]
    public void An_already_obtained_species_triggers_the_clause()
    {
        var context = Context(pokemon: [Caught(19, "Rattata", "ruta-1")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        // Warn-only by default: the player chooses whether to reroll.
        Assert.Equal(RuleOutcome.Warning, result.Outcome);
        Assert.Equal("DUPES CLAUSE", result.Primary!.Title);
        Assert.Equal("especie exacta", result.Primary.Details["Criterio"]);
    }

    [Fact]
    public void A_new_species_passes()
    {
        var context = Context(pokemon: [Caught(19, "Rattata", "ruta-1")]);

        var result = Engine().Evaluate(
            new AttemptCapture(731, "Pikipek", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void In_block_mode_the_duplicate_is_rejected()
    {
        var context = Context(
            configuration: With((RuleIds.DupesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(19, "Rattata", "ruta-1")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void Family_matching_uses_the_evolution_provider_when_it_has_data()
    {
        var context = Context(
            configuration: With((RuleIds.DupesClause,
                new RuleSettings { Mode = RuleMode.Block, IgnoreEvolutionaryLine = false })),
            pokemon: [Caught(25, "Pikachu", "ruta-1")],
            evolutionLines: new FakeEvolutionLines(new() { [25] = 10, [172] = 10 }));

        var result = Engine().Evaluate(
            new AttemptCapture(172, "Pichu", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.True(result.IsBlocked);
        Assert.Equal("línea evolutiva", result.Primary!.Details["Criterio"]);
    }
}

public sealed class SpeciesClauseTests
{
    [Fact]
    public void It_is_off_by_default()
    {
        var context = Context(pokemon: [Caught(25, "Pikachu", "ruta-1")]);

        var result = Engine().Evaluate(
            new AttemptCapture(172, "Pichu", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.DoesNotContain(result.Results, r => r.RuleId == RuleIds.SpeciesClause);
    }

    [Fact]
    public void Enabled_without_evolution_data_it_warns_instead_of_pretending_to_check()
    {
        var context = Context(
            configuration: With((RuleIds.SpeciesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(25, "Pikachu", "ruta-1")]);

        var result = Engine().Evaluate(
            new AttemptCapture(172, "Pichu", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        var speciesClause = Assert.Single(result.Results, r => r.RuleId == RuleIds.SpeciesClause);
        Assert.Equal(RuleOutcome.Warning, speciesClause.Outcome);
        Assert.Contains("no hay tabla de líneas evolutivas", speciesClause.Message);
    }

    [Fact]
    public void With_evolution_data_a_relative_of_a_registered_Pokemon_is_blocked()
    {
        var context = Context(
            configuration: With((RuleIds.SpeciesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(25, "Pikachu", "ruta-1")],
            evolutionLines: new FakeEvolutionLines(new() { [25] = 10, [172] = 10 }));

        var result = Engine().Evaluate(
            new AttemptCapture(172, "Pichu", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.True(result.IsBlocked);
        Assert.Equal("Pikachu", result.Primary!.Details["Misma línea que"]);
    }

    [Fact]
    public void An_unrelated_species_passes()
    {
        var context = Context(
            configuration: With((RuleIds.SpeciesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(25, "Pikachu", "ruta-1")],
            evolutionLines: new FakeEvolutionLines(new() { [25] = 10, [731] = 44 }));

        var result = Engine().Evaluate(
            new AttemptCapture(731, "Pikipek", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.False(result.IsBlocked);
    }
}

internal sealed class FakeEvolutionLines(Dictionary<int, int> lines) : IEvolutionLineProvider
{
    public bool HasData => true;

    public int GetLineId(int species) => lines.TryGetValue(species, out var line) ? line : species;
}
