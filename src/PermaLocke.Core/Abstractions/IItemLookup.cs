namespace PermaLocke.Core.Abstractions;

/// <summary>
/// Resolves the item ids the game stores into names players recognise.
/// </summary>
/// <remarks>
/// A port, like <see cref="ILocationLookup"/>: the bag, the shop and the level cap all deal in
/// item ids, and none of them should know that the names come from PKHeX today.
/// </remarks>
public interface IItemLookup
{
    /// <summary>Name of an item, or a readable fallback when the id is unknown.</summary>
    string GetName(int itemId);
}
