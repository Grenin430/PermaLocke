namespace PermaLocke.Core.Domain;

/// <summary>
/// The SuperCarameloraro (2026-10-06): a Rare Candy that raises five levels at once, never past the cap. PermaLocke puts
/// it into the game (see <c>RulePatches.SuperCandy</c>) and is the only way to get it.
/// </summary>
public static class SuperCandy
{
    /// <summary>One of the expansion's unused item slots, which the patch turns into this item.</summary>
    public const int ItemId = 113;

    public const string Name = "SuperCarameloraro";

    public const int Levels = 5;
}
