namespace PermaLocke.Rules.Rules;

/// <summary>
/// Treats a whole evolutionary family as one species: with a Pikachu registered, a Pichu
/// counts as a duplicate. Kept separate from the dupes clause on purpose — they answer
/// different questions and a run may want one without the other.
/// </summary>
/// <remarks>
/// Requires real evolution data. With <see cref="NullEvolutionLineProvider"/> in place it
/// allows everything rather than pretending to check, and says so in the message.
/// </remarks>
public sealed class SpeciesClauseRule : IRule
{
    public string Id => RuleIds.SpeciesClause;

    public bool AppliesTo(GameAction action) => action is AttemptCapture;

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var capture = (AttemptCapture)action;

        if (!context.EvolutionLines.HasData)
        {
            return new RuleResult
            {
                RuleId = Id,
                Outcome = RuleOutcome.Warning,
                Title = "SPECIES CLAUSE SIN DATOS",
                Message = "La regla está activada pero todavía no hay tabla de líneas evolutivas, "
                          + "así que no puede comprobar nada. Se leerá de la ROM randomizada.",
                Details = new Dictionary<string, string> { ["Pokémon"] = capture.SpeciesName }
            };
        }

        var line = context.EvolutionLines.GetLineId(capture.Species);

        var relative = context.Pokemon
            .FirstOrDefault(p => p.Species != capture.Species
                                 && context.EvolutionLines.GetLineId(p.Species) == line);

        if (relative is null)
        {
            return RuleResult.Ok(Id);
        }

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.Blocked,
            Title = "SPECIES CLAUSE",
            Message = $"{capture.SpeciesName} pertenece a la misma línea evolutiva que "
                      + $"{relative.SpeciesName}, que ya está registrado.",
            Details = new Dictionary<string, string>
            {
                ["Encuentro"] = capture.SpeciesName,
                ["Misma línea que"] = relative.SpeciesName
            }
        };
    }
}
