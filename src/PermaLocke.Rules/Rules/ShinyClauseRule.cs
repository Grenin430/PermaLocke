namespace PermaLocke.Rules.Rules;

/// <summary>
/// A shiny may be caught even in a zone whose encounter is spent. It does not invalidate the
/// original capture, and whether it burns the zone is configurable.
/// </summary>
/// <remarks>
/// Declares the rules it overrides rather than assuming the engine will guess: the exception
/// is explicit and stays visible in the history as a RULE_EXCEPTION.
/// </remarks>
public sealed class ShinyClauseRule : IRule
{
    public string Id => RuleIds.ShinyClause;

    public bool AppliesTo(GameAction action) => action is AttemptCapture { IsShiny: true };

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var capture = (AttemptCapture)action;

        // With the zone still free there is nothing to excuse: the normal rules allow it.
        var zoneIsSpent = context.UsedZones.ContainsKey(capture.LocationId);
        var speciesIsDuplicate = context.HasSpecies(capture.Species);

        if (!zoneIsSpent && !speciesIsDuplicate)
        {
            return RuleResult.Ok(Id);
        }

        var settings = context.Configuration.For(Id);

        var details = new Dictionary<string, string>
        {
            ["Pokémon"] = capture.SpeciesName,
            ["Zona"] = capture.LocationName,
            ["Consume el encuentro"] = settings.ConsumesEncounter ? "sí" : "no"
        };

        if (context.UsedZones.TryGetValue(capture.LocationId, out var used))
        {
            details["Encuentro original"] = used.SpeciesName;
        }

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.AllowedWithException,
            Title = "✨ SHINY DETECTADO",
            Message = $"{capture.SpeciesName} es shiny. La Shiny Clause permite registrarlo "
                      + "aunque esta zona ya tenga un encuentro; el original sigue siendo válido.",
            Details = details,
            OverriddenRuleIds = [RuleIds.FirstEncounter, RuleIds.DupesClause, RuleIds.SpeciesClause]
        };
    }
}
