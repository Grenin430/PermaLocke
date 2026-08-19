using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Rules;

/// <summary>
/// Shared behaviour for encounter types that are not ordinary wild encounters. Whether they
/// are allowed and whether they burn the zone is configuration; the rule only reports.
/// </summary>
public abstract class SpecialEncounterRule(EncounterType[] types, string label) : IRule
{
    public abstract string Id { get; }

    public bool AppliesTo(GameAction action) =>
        action is AttemptCapture capture && types.Contains(capture.EncounterType);

    public RuleResult Evaluate(GameAction action, RuleContext context)
    {
        var capture = (AttemptCapture)action;
        var settings = context.Configuration.For(Id);

        return new RuleResult
        {
            RuleId = Id,
            Outcome = RuleOutcome.Warning,
            Title = label.ToUpperInvariant(),
            Message = $"{capture.SpeciesName} es un {label.ToLowerInvariant()}. "
                      + (settings.ConsumesEncounter
                          ? "Consume el encuentro de la zona."
                          : "No consume el encuentro de la zona."),
            Details = new Dictionary<string, string>
            {
                ["Pokémon"] = capture.SpeciesName,
                ["Tipo de encuentro"] = capture.EncounterType.ToString(),
                ["Zona"] = capture.LocationName
            }
        };
    }
}

public sealed class GiftPokemonRule() : SpecialEncounterRule([EncounterType.Gift, EncounterType.Starter], "Pokémon de regalo")
{
    public override string Id => RuleIds.GiftPokemon;
}

public sealed class StaticEncounterRule()
    : SpecialEncounterRule([EncounterType.Static, EncounterType.Legendary], "Pokémon estático")
{
    public override string Id => RuleIds.StaticPokemon;
}
