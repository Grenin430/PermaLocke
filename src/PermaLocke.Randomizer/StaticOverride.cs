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
/// <param name="Level">
/// The cartridge level of the entry, when species and form are not enough to tell them apart.
/// </param>
/// <param name="IndependentDraw">
/// <para>
/// When true, the rule is applied <b>after</b> the ordinary draw and from a stream of its own, so
/// adding it changes the entries it names and nothing else.
/// </para>
/// <para>
/// The rules that are false run <em>before</em> the draw, on the same stream, and take their entries
/// out of it: each one that is added spends tiradas the rest of the table would have used and
/// removes entries from the draw, and every encounter after it moves. Measured on the world a player
/// was in the middle of: adding the two Necrozma fusions that way changed <b>249 of 252 statics and
/// all 7 trades</b> — Totems and legendaries still ahead of him included — to fix two. Hence this.
/// </para>
/// <para>
/// It is not the default because the rules already in <c>Data/randomizer.json</c> generated worlds
/// people are playing; switching them over would re-roll those worlds just the same. New rules on a
/// world already installed want it on.
/// </para>
/// </param>
/// <remarks>
/// Nihilego is why this exists: it sits in the static table <b>four times</b>, at levels 27, 55, 55
/// and 60, and the table says nothing about where any of them is. The one the player means — the
/// first Ultra Beast, at Aether Paradise — is a level 55, so the level is the only thing on hand
/// that separates it from the Ultra Space ones. Left null the rule takes every entry of that
/// species and form, which is what the Necrozma rules want.
/// </remarks>
public sealed record StaticOverride(
    int Species,
    int Form,
    StaticOverrideRule Rule,
    int MinimumBaseStatTotal = 0,
    string Note = "",
    int? Level = null,
    bool IndependentDraw = false);
