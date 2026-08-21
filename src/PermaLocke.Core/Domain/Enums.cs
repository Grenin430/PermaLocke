namespace PermaLocke.Core.Domain;

/// <summary>Supported games. PermaLocke targets Ultra Moon first.</summary>
public enum GameVersion
{
    UltraSun,
    UltraMoon
}

/// <summary>
/// How a Pokémon was met. Rules decide which of these consume the encounter of a zone;
/// that decision lives in configuration, never hardcoded.
/// </summary>
public enum EncounterType
{
    Wild,
    Gift,
    Static,
    Legendary,
    Starter,
    Trade,
    Fishing,
    Sos,
    Special
}

public enum PokemonStatus
{
    Alive,
    Dead,
    Released,
    Traded
}

/// <summary>Where a Pokémon entered the run from.</summary>
public enum PokemonOrigin
{
    Capture,
    Gacha,
    WonderTrade,
    Gift,
    Starter,
    AdminGrant
}

public enum IslandState
{
    Locked,
    InProgress,
    Completed
}

/// <summary>Who or what caused an event. Critical for auditing.</summary>
public enum EventSource
{
    /// <summary>Entered by the player in the UI.</summary>
    Player,

    /// <summary>Detected automatically from the running game or the save file.</summary>
    AutoDetect,

    /// <summary>Performed from PermaLocke.Admin.</summary>
    Admin,

    /// <summary>Produced by PermaLocke itself (run creation, migrations, integrity checks).</summary>
    System
}

public enum GameEventType
{
    RunCreated,
    RunCompleted,

    PointsEarned,
    PointsSpent,
    PointsAdjusted,

    PokemonCaught,
    PokemonDied,
    PokemonReleased,
    PokemonTraded,

    GachaRoll,
    ShopPurchase,
    WonderTrade,

    AchievementUnlocked,

    RuleViolation,
    RuleException,
    LevelCapEnforced,

    AdminAdjustment,
    GameStateSynced,

    /// <summary>A randomization was generated for the run.</summary>
    RomRandomized,

    /// <summary>An item was written straight into the bag by a testing tool.</summary>
    TestItemGranted,

    /// <summary>Poké Balls were taken away because the zone had spent its encounter.</summary>
    BallsWithheld,

    /// <summary>Poké Balls were given back, exactly as many as were taken.</summary>
    BallsReturned,

    /// <summary>
    /// Points taken away by a rule of the competition, not by a purchase.
    /// </summary>
    /// <remarks>
    /// New values go at the <b>end</b> of this enum and nowhere else: the store keeps the number,
    /// so inserting one in the middle would silently rewrite the meaning of every event already
    /// recorded.
    /// </remarks>
    PointsPenalty,

    /// <summary>The whole party was down at once.</summary>
    TeamWiped,

    /// <summary>
    /// The player moved an achievement's counter by hand, because PermaLocke cannot see the thing
    /// it counts. Recorded separately from the automatic events so the log always says which of
    /// the two it was.
    /// </summary>
    AchievementProgressed
}
