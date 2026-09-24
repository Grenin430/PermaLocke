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
    /// The cartridge's last ability: 233, «Fuerza Cerebral». Only the roulette still stops here.
    /// </summary>
    /// <remarks>
    /// It used to be the ceiling for everything that hands out an ability, on two premises that
    /// were both measured wrong. That a gen 7 Pokémon keeps its ability in one byte: the Ursaluna
    /// that was announced with «General Supremo» (293) and arrived with «Potencia» (37) was the
    /// builder writing only the byte, and the expansion mod keeps a ninth bit in 0x15 (§134). And
    /// that the mod's new abilities have a name and do nothing: that was a randomizer dropping the
    /// ninth bit (§132). The gacha and the wonder trade deal the new ones since §136. The roulette
    /// names its abilities and resolves them against PKHeX's gen 7 table, which is why it keeps
    /// this ceiling.
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
