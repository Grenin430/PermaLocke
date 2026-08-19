namespace PermaLocke.Core.Abstractions;

/// <summary>
/// Says which area of the game the player is standing in.
/// </summary>
/// <remarks>
/// A port so the rules never learn how the zone is read. The number is the index into the game
/// encounter areas, which is also what the randomizer works in.
/// </remarks>
public interface IZoneProvider
{
    /// <summary>
    /// The current area, or <c>null</c> when it cannot be established.
    /// </summary>
    /// <remarks>
    /// Null is an answer, not a failure to paper over. A rule that takes items away must not
    /// fire on a zone nobody is sure about.
    /// </remarks>
    int? CurrentArea();
}

/// <summary>
/// Takes items away from the player and gives back exactly what was taken.
/// </summary>
/// <remarks>
/// A port for the same reason: the rule decides <em>when</em>, and knows nothing about bag
/// blocks or emulators. Implementations must survive being killed mid-way — what is owed is
/// written down before anything is touched.
/// </remarks>
public interface IItemWithholder
{
    /// <summary>How many of an item the player is carrying right now.</summary>
    int Carried(int itemId);

    /// <summary>How much of an item is being withheld and must be returned.</summary>
    int Owed(int itemId);

    /// <summary>Takes the item away. False when nothing was taken.</summary>
    bool Withhold(int itemId);

    /// <summary>Returns exactly what was withheld. False when nothing was owed.</summary>
    bool GiveBack(int itemId);
}
