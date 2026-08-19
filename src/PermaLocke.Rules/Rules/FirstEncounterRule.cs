namespace PermaLocke.Rules.Rules;

/// <summary>
/// One encounter per zone. Which encounter types burn the zone is configuration, not code:
/// a gift or a static Pokémon may well be exempt depending on the run.
/// </summary>
public sealed class FirstEncounterRule : IRule
{
    public string Id => RuleIds.FirstEncounter;

    public bool AppliesTo(GameAction action) => action is AttemptCapture;

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var capture = (AttemptCapture)action;

        if (!context.ConsumesZone(capture.EncounterType))
        {
            return RuleResult.Ok(Id);
        }

        if (!context.UsedZones.TryGetValue(capture.LocationId, out var used))
        {
            return RuleResult.Ok(Id);
        }

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.Blocked,
            Title = "CAPTURA BLOQUEADA",
            Message = $"Ya has utilizado el encuentro de {used.LocationName}.",
            Details = new Dictionary<string, string>
            {
                ["Primer Pokémon"] = used.SpeciesName,
                ["Zona"] = used.LocationName,
                ["Registrado"] = used.RegisteredAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm"),
                ["Intento"] = capture.SpeciesName
            }
        };
    }
}
