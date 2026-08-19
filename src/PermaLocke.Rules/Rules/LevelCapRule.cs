namespace PermaLocke.Rules.Rules;

/// <summary>
/// Enforces the level cap of the current stage: the level of the highest Pokémon of the
/// kahuna or totem the player is up against.
/// </summary>
/// <remarks>
/// This rule only reports that the cap was exceeded. Undoing it in the game — resetting
/// experience, dropping the level by one, deleting rare candies — is a write into the running
/// game and belongs to the phase that owns that, not here.
/// </remarks>
public sealed class LevelCapRule : IRule
{
    public string Id => RuleIds.LevelCap;

    public bool AppliesTo(GameAction action) => action is LevelCheck;

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var check = (LevelCheck)action;

        if (context.LevelCap is not { } cap || check.Level <= cap)
        {
            return RuleResult.Ok(Id);
        }

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.Blocked,
            Title = "CAP DE NIVEL SUPERADO",
            Message = $"{check.SpeciesName} está a nivel {check.Level} y el cap actual es {cap}.",
            Details = new Dictionary<string, string>
            {
                ["Pokémon"] = check.SpeciesName,
                ["Nivel"] = check.Level.ToString(),
                ["Cap"] = cap.ToString(),
                ["Exceso"] = (check.Level - cap).ToString()
            }
        };
    }
}
