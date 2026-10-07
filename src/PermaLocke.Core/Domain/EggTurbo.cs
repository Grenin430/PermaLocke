namespace PermaLocke.Core.Domain;

/// <summary>
/// The Incubadora Turbo (2026-10-07): a key item that, used from the bag, makes every egg of the party hatch after two steps,
/// and used again, stops. PermaLocke puts it into the game (see <c>RulePatches.EggTurbo</c>) and is the only way to get it.
/// </summary>
public static class EggTurbo
{
    /// <summary>One of the expansion's unused item slots, the one after the Repelente Infinito's.</summary>
    public const int ItemId = 115;

    public const string Name = "Incubadora Turbo";
}
