using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using static PermaLocke.Rules.Tests.RuleTestContext;

namespace PermaLocke.Rules.Tests;

public sealed class FirstEncounterRuleTests
{
    private static AttemptCapture Capture(
        int species = 19, string name = "Rattata", EncounterType type = EncounterType.Wild, bool shiny = false) =>
        new(species, name, "ruta-1", "Ruta 1", type, shiny);

    [Fact]
    public void The_first_capture_of_a_zone_is_allowed()
    {
        var result = Engine().Evaluate(Capture(731, "Pikipek"), Context());

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void A_second_capture_in_the_same_zone_is_blocked()
    {
        var context = Context(usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(), context);

        Assert.True(result.IsBlocked);
        Assert.Equal("CAPTURA BLOQUEADA", result.Primary!.Title);
        Assert.Equal("Pikipek", result.Primary.Details["Primer Pokémon"]);
        Assert.Equal("Ruta 1", result.Primary.Details["Zona"]);
    }

    [Fact]
    public void A_capture_in_a_different_zone_is_unaffected()
    {
        var context = Context(usedZones: [Zone("ruta-2", "Ruta 2", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(), context);

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void In_warn_only_mode_the_block_becomes_a_warning()
    {
        var context = Context(
            configuration: With((RuleIds.FirstEncounter, new RuleSettings { Mode = RuleMode.WarnOnly })),
            usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(), context);

        Assert.Equal(RuleOutcome.Warning, result.Outcome);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public void A_disabled_rule_is_not_evaluated_at_all()
    {
        var context = Context(
            configuration: With(
                (RuleIds.FirstEncounter, new RuleSettings { Enabled = false, Mode = RuleMode.Disabled }),
                (RuleIds.DupesClause, new RuleSettings { Enabled = false, Mode = RuleMode.Disabled })),
            usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(), context);

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
        Assert.DoesNotContain(result.Results, r => r.RuleId == RuleIds.FirstEncounter);
    }

    [Fact]
    public void BlockAndLog_marks_the_evaluation_for_auditing()
    {
        var context = Context(
            configuration: With((RuleIds.FirstEncounter, new RuleSettings { Mode = RuleMode.BlockAndLog })),
            usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(), context);

        Assert.True(result.IsBlocked);
        Assert.True(result.ShouldAudit);
    }

    [Theory]
    [InlineData(EncounterType.Gift)]
    [InlineData(EncounterType.Static)]
    [InlineData(EncounterType.Legendary)]
    [InlineData(EncounterType.Starter)]
    public void Encounter_types_that_do_not_consume_the_zone_are_not_blocked(EncounterType type)
    {
        var context = Context(usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(type: type), context);

        Assert.False(result.IsBlocked);
    }

    [Theory]
    [InlineData(EncounterType.Wild)]
    [InlineData(EncounterType.Fishing)]
    [InlineData(EncounterType.Sos)]
    public void Encounter_types_that_consume_the_zone_are_blocked(EncounterType type)
    {
        var context = Context(usedZones: [Zone("ruta-1", "Ruta 1", 731, "Pikipek")]);

        var result = Engine().Evaluate(Capture(type: type), context);

        Assert.True(result.IsBlocked);
    }
}
