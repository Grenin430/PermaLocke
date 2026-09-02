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
    /// <summary>
    /// The highest ability id anything may hand to this game: 233, «Fuerza Cerebral».
    /// </summary>
    /// <remarks>
    /// Two separate ceilings that happen to meet here, and it takes both to explain it. A gen 7
    /// Pokémon stores its ability in <b>one byte</b>, so anything from 256 up wraps round in
    /// silence: «General Supremo» is 293, and 293 minus 256 is 37, which is «Potencia». That is
    /// not a display bug, it is a different ability, and it happened — a gacha Ursaluna was
    /// announced with one and turned up in the box with the other. And the expansion mod fills
    /// 234-255 with abilities that <b>have a name and do nothing</b>, which is worse than an
    /// error because it looks like it worked.
    /// <para>
    /// So 233 is the last one that both fits and runs, and it is the same number the randomizer
    /// caps at. It is a constant rather than configuration on purpose: it is not a knob to tune,
    /// it is the shape of the cartridge, and a file that could set it to 320 would put the bug
    /// straight back.
    /// </para>
    /// </remarks>
    public const int LastUsableAbility = 233;

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
