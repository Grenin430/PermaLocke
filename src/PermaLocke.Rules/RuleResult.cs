namespace PermaLocke.Rules;

public enum RuleOutcome
{
    Allowed,
    Warning,
    AllowedWithException,
    Blocked
}

/// <summary>How strictly a rule is applied. Configured globally and per rule.</summary>
public enum RuleMode
{
    /// <summary>The rule is not evaluated at all.</summary>
    Disabled,

    /// <summary>Blocks are downgraded to warnings: the player is told, but may proceed.</summary>
    WarnOnly,

    Block,

    /// <summary>Blocks and writes a RULE_VIOLATION event to the audit log.</summary>
    BlockAndLog
}

/// <summary>The verdict of a single rule, worded for the player.</summary>
public sealed record RuleResult
{
    public required string RuleId { get; init; }

    public required RuleOutcome Outcome { get; init; }

    /// <summary>Headline, e.g. "CAPTURA BLOQUEADA".</summary>
    public required string Title { get; init; }

    public required string Message { get; init; }

    /// <summary>Extra facts for the dialog and the audit event, e.g. the Pokémon already registered.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Rules this exception overrides. The shiny clause uses it to lift the block the
    /// first-encounter rule would otherwise impose.
    /// </summary>
    public IReadOnlyList<string> OverriddenRuleIds { get; init; } = [];

    /// <summary>Set by the engine when another rule's exception lifted this block.</summary>
    public bool Overridden { get; init; }

    public static RuleResult Ok(string ruleId) => new()
    {
        RuleId = ruleId,
        Outcome = RuleOutcome.Allowed,
        Title = string.Empty,
        Message = string.Empty
    };
}

/// <summary>The combined verdict of every rule that had something to say about an action.</summary>
public sealed record RuleEvaluation
{
    public required RuleOutcome Outcome { get; init; }

    public required IReadOnlyList<RuleResult> Results { get; init; }

    /// <summary>True when the run's configuration wants this outcome recorded as a violation.</summary>
    public bool ShouldAudit { get; init; }

    public bool IsBlocked => Outcome == RuleOutcome.Blocked;

    public bool IsException => Outcome == RuleOutcome.AllowedWithException;

    /// <summary>The result the UI should lead with: the block, then the exception, then the warning.</summary>
    public RuleResult? Primary =>
        Results.FirstOrDefault(r => r.Outcome == RuleOutcome.Blocked && !r.Overridden)
        ?? Results.FirstOrDefault(r => r.Outcome == RuleOutcome.AllowedWithException)
        ?? Results.FirstOrDefault(r => r.Outcome == RuleOutcome.Warning);

    public static RuleEvaluation Allowed { get; } = new()
    {
        Outcome = RuleOutcome.Allowed,
        Results = []
    };
}
