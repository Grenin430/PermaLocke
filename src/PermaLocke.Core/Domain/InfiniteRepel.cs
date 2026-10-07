namespace PermaLocke.Core.Domain;

/// <summary>
/// The Repelente Infinito (2026-10-07): a key item that, used from the bag, turns a never-ending Repel on and, used again,
/// off. PermaLocke puts it into the game (see <c>RulePatches.InfiniteRepel</c>) and is the only way to get it.
/// </summary>
public static class InfiniteRepel
{
    /// <summary>One of the expansion's unused item slots, which the patch turns into this item.</summary>
    public const int ItemId = 114;

    public const string Name = "Repelente Infinito";
}
