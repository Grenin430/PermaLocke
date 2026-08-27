namespace PermaLocke.Core.Abstractions;

/// <summary>
/// Resolves ability names to the ids the cartridge stores, and back.
/// </summary>
/// <remarks>
/// A port, like <see cref="IItemLookup"/>. It exists because the roulette names its abilities in
/// words: a list of sixty numbers in a config file is unreadable and a typo in it is invisible,
/// whereas a name that does not resolve can be reported the moment the file is read.
/// </remarks>
public interface IAbilityLookup
{
    /// <summary>The id of an ability by name, or 0 when this game has no such ability.</summary>
    int GetId(string name);

    /// <summary>Name of an ability, or a readable fallback when the id is unknown.</summary>
    string GetName(int abilityId);

    /// <summary>
    /// The highest ability id this game knows.
    /// </summary>
    /// <remarks>
    /// PKHeX's table runs past it, into abilities from later generations. Writing one of those
    /// into a gen 7 Pokémon stores a number the cartridge cannot name, so anything above this is
    /// dropped rather than handed to the game.
    /// </remarks>
    int LastAbility { get; }
}
