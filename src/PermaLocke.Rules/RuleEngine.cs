namespace PermaLocke.Rules;

public interface IRule
{
    /// <summary>Matches a key in Data/rules.json.</summary>
    string Id { get; }

    bool AppliesTo(GameAction action);

    /// <summary>
    /// The rule's own verdict, ignoring configuration modes. The engine applies the mode,
    /// so a rule never has to know whether it is set to warn or to block.
    /// </summary>
    RuleResult Evaluate(GameAction action, RuleContext context);
}

public interface IRuleEngine
{
    RuleEvaluation Evaluate(GameAction action, RuleContext context);
}

/// <summary>
/// Runs every applicable rule and combines the verdicts. Rules stay independent and testable
/// in isolation; ordering, modes and exception overrides are decided here, once.
/// </summary>
public sealed class RuleEngine(IEnumerable<IRule> rules) : IRuleEngine
{
    private readonly IReadOnlyList<IRule> _rules = [.. rules];

    public RuleEvaluation Evaluate(GameAction action, RuleContext context)
    {
        var results = new List<RuleResult>();
        var shouldAudit = false;

        foreach (var rule in _rules)
        {
            var settings = context.Configuration.For(rule.Id);

            if (!settings.IsActive || !rule.AppliesTo(action))
            {
                continue;
            }

            var result = rule.Evaluate(action, context);

            if (result.Outcome == RuleOutcome.Allowed)
            {
                continue;
            }

            // Warn-only turns a block into an advisory; the player decides.
            if (result.Outcome == RuleOutcome.Blocked && settings.Mode == RuleMode.WarnOnly)
            {
                result = result with { Outcome = RuleOutcome.Warning };
            }

            if (result.Outcome == RuleOutcome.Blocked && settings.Mode == RuleMode.BlockAndLog)
            {
                shouldAudit = true;
            }

            results.Add(result);
        }

        ApplyExceptionOverrides(results);

        return new RuleEvaluation
        {
            Outcome = Combine(results),
            Results = results,
            ShouldAudit = shouldAudit
        };
    }

    /// <summary>
    /// Lets an exception lift the blocks it declares. The shiny clause uses this to allow a
    /// capture the first-encounter rule had rejected, without deleting that rule's verdict:
    /// both stay in the results so the history shows what happened and why.
    /// </summary>
    private static void ApplyExceptionOverrides(List<RuleResult> results)
    {
        var overridden = results
            .Where(r => r.Outcome == RuleOutcome.AllowedWithException)
            .SelectMany(r => r.OverriddenRuleIds)
            .ToHashSet(StringComparer.Ordinal);

        if (overridden.Count == 0)
        {
            return;
        }

        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Outcome == RuleOutcome.Blocked && overridden.Contains(results[i].RuleId))
            {
                results[i] = results[i] with { Overridden = true };
            }
        }
    }

    private static RuleOutcome Combine(List<RuleResult> results)
    {
        if (results.Any(r => r.Outcome == RuleOutcome.Blocked && !r.Overridden))
        {
            return RuleOutcome.Blocked;
        }

        if (results.Any(r => r.Outcome == RuleOutcome.AllowedWithException))
        {
            return RuleOutcome.AllowedWithException;
        }

        return results.Any(r => r.Outcome == RuleOutcome.Warning)
            ? RuleOutcome.Warning
            : RuleOutcome.Allowed;
    }
}
