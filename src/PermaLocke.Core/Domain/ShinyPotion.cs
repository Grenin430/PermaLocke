namespace PermaLocke.Core.Domain;

/// <summary>
/// The Poción Variocolor (2026-10-17), as the Indigo Disk's: used from the bag, the next wild Pokémon the game makes comes out shiny. It is
/// spent on use, and the ground of a new world hides a few (see <c>FieldItemRandomizer</c>). PermaLocke puts it into the game
/// (<c>RulePatches.ShinyPotion</c>).
/// </summary>
public static class ShinyPotion
{
    /// <summary>One of the expansion's unused item slots (its name «(?)» in the game text, its data empty), the first after the Pokévial's.</summary>
    public const int ItemId = 121;

    public const string Name = "Poción Variocolor";
}
