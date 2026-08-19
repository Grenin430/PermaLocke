using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using static PermaLocke.Rules.Tests.RuleTestContext;

namespace PermaLocke.Rules.Tests;

public sealed class LevelCapRuleTests
{
    [Fact]
    public void A_Pokemon_below_the_cap_is_fine()
    {
        var result = Engine().Evaluate(new LevelCheck(Guid.NewGuid(), "Rowlet", 13), Context(levelCap: 14));

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void A_Pokemon_exactly_at_the_cap_is_fine()
    {
        var result = Engine().Evaluate(new LevelCheck(Guid.NewGuid(), "Rowlet", 14), Context(levelCap: 14));

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void Going_over_the_cap_is_blocked_and_reports_the_excess()
    {
        var result = Engine().Evaluate(new LevelCheck(Guid.NewGuid(), "Rowlet", 17), Context(levelCap: 14));

        Assert.True(result.IsBlocked);
        Assert.Equal("CAP DE NIVEL SUPERADO", result.Primary!.Title);
        Assert.Equal("3", result.Primary.Details["Exceso"]);
    }

    [Fact]
    public void With_no_stage_reached_there_is_no_cap_to_break()
    {
        var result = Engine().Evaluate(new LevelCheck(Guid.NewGuid(), "Rowlet", 80), Context(levelCap: null));

        Assert.Equal(RuleOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void The_cap_rule_ignores_captures()
    {
        var result = Engine().Evaluate(
            new AttemptCapture(731, "Pikipek", "ruta-1", "Ruta 1", EncounterType.Wild),
            Context(levelCap: 14));

        Assert.DoesNotContain(result.Results, r => r.RuleId == RuleIds.LevelCap);
    }
}

public sealed class CombinedRuleTests
{
    [Fact]
    public void A_blocked_zone_and_a_duplicate_species_both_report()
    {
        var context = Context(
            configuration: With((RuleIds.DupesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(19, "Rattata", "ruta-1")],
            usedZones: [Zone("ruta-3", "Ruta 3", 731, "Pikipek")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-3", "Ruta 3", EncounterType.Wild),
            context);

        Assert.True(result.IsBlocked);
        Assert.Equal(2, result.Results.Count(r => r.Outcome == RuleOutcome.Blocked));

        // The zone rule leads, because it is the one the player has to resolve first.
        Assert.Equal(RuleIds.FirstEncounter, result.Primary!.RuleId);
    }

    [Fact]
    public void A_shiny_duplicate_in_a_spent_zone_lifts_every_block()
    {
        var context = Context(
            configuration: With((RuleIds.DupesClause, new RuleSettings { Mode = RuleMode.Block })),
            pokemon: [Caught(19, "Rattata", "ruta-1")],
            usedZones: [Zone("ruta-3", "Ruta 3", 731, "Pikipek")]);

        var result = Engine().Evaluate(
            new AttemptCapture(19, "Rattata", "ruta-3", "Ruta 3", EncounterType.Wild, IsShiny: true),
            context);

        Assert.Equal(RuleOutcome.AllowedWithException, result.Outcome);
        Assert.All(result.Results.Where(r => r.Outcome == RuleOutcome.Blocked),
            r => Assert.True(r.Overridden));
    }

    [Fact]
    public void A_gift_Pokemon_only_produces_its_own_advisory()
    {
        var result = Engine().Evaluate(
            new AttemptCapture(722, "Rowlet", "iki", "Pueblo Iki", EncounterType.Starter),
            Context());

        Assert.Equal(RuleOutcome.Warning, result.Outcome);
        var single = Assert.Single(result.Results);
        Assert.Equal(RuleIds.GiftPokemon, single.RuleId);
        Assert.Contains("No consume el encuentro", single.Message);
    }

    [Fact]
    public void A_static_Pokemon_reports_as_static()
    {
        var result = Engine().Evaluate(
            new AttemptCapture(791, "Solgaleo", "altar", "Altar del Sol", EncounterType.Legendary),
            Context());

        var single = Assert.Single(result.Results);
        Assert.Equal(RuleIds.StaticPokemon, single.RuleId);
    }
}

public sealed class RulesConfigurationTests
{
    [Fact]
    public void The_shipped_rules_json_loads_with_the_expected_settings()
    {
        var path = FindDataFile("rules.json");
        if (path is null)
        {
            return;
        }

        var configuration = RulesConfigurationLoader.Load(path);

        Assert.Equal(RuleMode.Block, configuration.For(RuleIds.FirstEncounter).Mode);
        Assert.False(configuration.For(RuleIds.ShinyClause).ConsumesEncounter);
        Assert.True(configuration.For(RuleIds.DupesClause).IgnoreEvolutionaryLine);
        Assert.False(configuration.For(RuleIds.SpeciesClause).Enabled);
        Assert.Equal(
            [EncounterType.Wild, EncounterType.Fishing, EncounterType.Sos],
            configuration.EncounterTypesThatConsumeZone);
    }

    [Fact]
    public void A_missing_file_falls_back_to_the_defaults()
    {
        var configuration = RulesConfigurationLoader.Load(
            Path.Combine(Path.GetTempPath(), "no-existe-rules.json"));

        Assert.Equal(RuleMode.Block, configuration.For(RuleIds.FirstEncounter).Mode);
    }

    [Fact]
    public void An_unlisted_rule_inherits_the_global_default()
    {
        var configuration = new RulesConfiguration { DefaultRuleMode = RuleMode.WarnOnly };

        var settings = configuration.For("reglaQueNoExiste");

        Assert.True(settings.Enabled);
        Assert.Equal(RuleMode.WarnOnly, settings.Mode);
    }

    private static string? FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
