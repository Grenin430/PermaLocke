namespace PermaLocke.Rules.Rules;

/// <summary>
/// The encounter repeats a Pokémon already obtained, so the run may allow rerolling it.
/// Matches on exact species by default; set <c>ignoreEvolutionaryLine</c> to false to match
/// on the whole family, which needs evolution data to be available.
/// </summary>
public sealed class DupesClauseRule : IRule
{
    public string Id => RuleIds.DupesClause;

    public bool AppliesTo(GameAction action) => action is AttemptCapture;

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var capture = (AttemptCapture)action;

        if (!context.ConsumesZone(capture.EncounterType))
        {
            return RuleResult.Ok(Id);
        }

        var settings = context.Configuration.For(Id);
        var matchWholeFamily = !settings.IgnoreEvolutionaryLine && context.EvolutionLines.HasData;

        var duplicate = matchWholeFamily
            ? FindInSameFamily(capture.Species, context)
            : context.FindBySpecies(capture.Species);

        if (duplicate is null)
        {
            return RuleResult.Ok(Id);
        }

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.Blocked,
            Title = "DUPES CLAUSE",
            Message = $"{duplicate.SpeciesName} ya ha sido obtenido. "
                      + "Este encuentro puede ignorarse según las reglas actuales.",
            Details = new Dictionary<string, string>
            {
                ["Encuentro"] = capture.SpeciesName,
                ["Ya obtenido"] = duplicate.SpeciesName,
                ["Obtenido en"] = duplicate.LocationId ?? "—",
                ["Criterio"] = matchWholeFamily ? "línea evolutiva" : "especie exacta"
            }
        };
    }

    private static Core.Domain.PokemonEntry? FindInSameFamily(int species, RuleContext context)
    {
        var line = context.EvolutionLines.GetLineId(species);
        return context.Pokemon.FirstOrDefault(p => context.EvolutionLines.GetLineId(p.Species) == line);
    }
}
