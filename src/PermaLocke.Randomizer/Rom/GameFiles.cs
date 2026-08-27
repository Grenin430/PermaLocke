namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// The RomFS paths PermaLocke touches, for Pokémon Ultra Moon.
/// <para>
/// Taken from pk3DS's <c>GARCReference_UM</c> table and confirmed against the real cartridge:
/// the ROM has exactly 333 files under <c>a/</c>, which is how pk3DS identifies USUM, and
/// <c>a/0/8/2</c> is present with zero bytes, which is how it tells Ultra Sun from Ultra Moon.
/// </para>
/// </summary>
public static class GameFiles
{
    public const string Move = "a/0/1/1";
    public const string EggMove = "a/0/1/2";
    public const string Learnset = "a/0/1/3";
    public const string Evolution = "a/0/1/4";
    public const string MegaEvolution = "a/0/1/5";
    public const string Personal = "a/0/1/7";
    public const string Item = "a/0/1/9";
    public const string ZoneData = "a/0/7/7";

    /// <summary>Present but empty on Ultra Moon. Needed anyway: pk3DS probes its length.</summary>
    public const string EncounterDataUltraSun = "a/0/8/2";

    /// <summary>~460 MB: it carries map data alongside the encounter tables.</summary>
    public const string EncounterDataUltraMoon = "a/0/8/3";

    public const string WorldData = "a/0/9/1";
    public const string TrainerClass = "a/1/0/5";
    public const string TrainerData = "a/1/0/6";
    public const string TrainerPokemon = "a/1/0/7";

    /// <summary>Starters, the eleven fossils, gift and static encounters, trades.</summary>
    public const string EncounterStatic = "a/1/5/9";

    public const string Pickup = "a/2/7/1";

    /// <summary>Special mart inventories. A CRO module, not a GARC.</summary>
    public const string Shop = "Shop.cro";

    /// <summary>Game text, one file per language. Add the language index to get the path.</summary>
    public static string GameText(int language) => $"a/0/3/{language}";

    /// <summary>Index of the species name list inside the game text GARC.</summary>
    public const int SpeciesNameFile = 60;

    /// <summary>Every file the randomizer may need, so the workspace can extract in one pass.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Move, EggMove, Learnset, Evolution, MegaEvolution, Personal, Item,
        ZoneData, EncounterDataUltraSun, EncounterDataUltraMoon, WorldData,
        TrainerClass, TrainerData, TrainerPokemon, EncounterStatic, Pickup, Shop,
        .. Enumerable.Range(0, 10).Select(GameText),
    ];
}
