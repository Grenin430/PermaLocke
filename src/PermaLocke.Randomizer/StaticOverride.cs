namespace PermaLocke.Randomizer;

/// <summary>What a static encounter is replaced by, when the ordinary draw is not what is wanted.</summary>
public enum StaticOverrideRule
{
    /// <summary>A random mega: a species from the cartridge's mega table, wearing that form.</summary>
    Mega,

    /// <summary>Something worth keeping: a base form whose stats add up to at least the minimum.</summary>
    Strong
}

/// <param name="Species">The species the cartridge has at that entry, which is how it is found.</param>
/// <param name="Form">Its form there. Necrozma is four different encounters and only the form says which.</param>
/// <param name="MinimumBaseStatTotal">Only for <see cref="StaticOverrideRule.Strong"/>.</param>
/// <param name="Note">Why this one is special, carried into the report.</param>
public sealed record StaticOverride(
    int Species,
    int Form,
    StaticOverrideRule Rule,
    int MinimumBaseStatTotal = 0,
    string Note = "");
