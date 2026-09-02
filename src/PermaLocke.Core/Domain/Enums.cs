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
    Special,

    /// <summary>
    /// Nobody has said how this one was met.
    /// </summary>
    /// <remarks>
    /// What automatic registration uses. The encounter type is not decoration: it decides whether
    /// a capture spends the zone's one encounter, and it drives the gift and static rules. The
    /// game knows the answer, but PermaLocke does not read the ball from memory and the met
    /// location alone does not say it, so filling in "wild" would quietly spend a zone the player
    /// never used. The honest answer is that it is unknown. It spends no zone and fires no special
    /// rule; what it does do is <em>exist</em>, so the watcher can see that Pokémon live and die.
    /// </remarks>
    Unknown
}

/// <summary>
/// What happened at a zone.s single encounter.
/// </summary>
/// <remarks>
/// Anything but <see cref="ZoneOutcome.Free"/> spends the zone. Which of the three it was does not
/// change that -- it changes what the player sees, and a Nuzlocke map is mostly read, not counted.
/// </remarks>
public enum ZoneOutcome
{
    /// <summary>Nothing has happened here yet.</summary>
    Free,

    /// <summary>Something was caught here.</summary>
    Caught,

    /// <summary>The first Pokémon here died.</summary>
    Died,

    /// <summary>The first encounter fled, which spends the zone all the same.</summary>
    Fled
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
    AchievementProgressed,

    /// <summary>
    /// The player rewrote a Pokémon's effort values from the viewer.
    /// </summary>
    /// <remarks>
    /// Its own type rather than an admin adjustment: this edits the partida directly, outside
    /// anything the game did, and a log that hides that behind a generic label stops being an
    /// audit. Costs no points; it is a change, not a purchase.
    /// </remarks>
    EvsTrained,

    /// <summary>
    /// A Pokémon the run granted reached the player's game, and the game gave it an identity.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="GachaRoll"/> and <see cref="WonderTrade"/> because those record a
    /// <em>decision</em> and this records an <em>arrival</em>: the roll happens whether or not the
    /// save can be written, and only this one can carry the PID, which does not exist until the
    /// Pokémon is built. Without it the run owns Pokémon it cannot recognise in memory.
    /// </remarks>
    PokemonDelivered,

    /// <summary>
    /// A one-off prize for reaching a milestone was collected.
    /// </summary>
    /// <remarks>
    /// Its own type because it is also the <b>lock</b>: a reward is claimable exactly once, and
    /// what makes that true is that this event is in the history. Folding it into
    /// <see cref="TestItemGranted"/> would mean any testing tool press looked like a claim.
    /// </remarks>
    RewardClaimed,

    /// <summary>
    /// The LUDOPATA wheel was turned once, and this is what it did.
    /// </summary>
    /// <remarks>
    /// Its own type because it is also the <b>counter</b>: how many spins a run has taken is how
    /// many of these are in the history, and that is what decides whether another one is owed. It
    /// carries its own <c>PointsDelta</c> when the face was a points face, so the wheel moves
    /// points with one entry instead of two.
    /// </remarks>
    RouletteSpun,

    /// <summary>
    /// A run changed role after its LayeredFS world was regenerated for the new role.
    /// </summary>
    /// <remarks>
    /// Roles normally belong to run creation because they shape the ROM. This type is the narrow,
    /// explicit migration path for correcting an existing run: the old and new ids stay in the
    /// chain, instead of somebody editing <c>run.json</c> and erasing why its world changed.
    /// </remarks>
    RoleChanged,

    /// <summary>
    /// The randomized world was set aside so the player could link-battle, or put back afterwards.
    /// </summary>
    /// <remarks>
    /// A link battle is a lockstep simulation, so every player has to be running the same species
    /// data or the two consoles compute different battles and drift apart — measured, not assumed
    /// (§80). Setting the mod aside puts everybody on the cartridge for the duration.
    /// <para>
    /// It leaves an event because it is a change to the world the run is being played in, and a
    /// competition should be able to see it. "Was he on vanilla when he caught that?" is a
    /// question the history has to be able to answer.
    /// </para>
    /// </remarks>
    BattleModeChanged,

    /// <summary>
    /// The player said on the map which zone a capture spent, so the zone counts as used.
    /// </summary>
    /// <remarks>
    /// It exists because the seventh generation stores <b>no field saying an encounter was wild</b>
    /// — measured on a real save, where a gift and a wild capture are identical down to the ball.
    /// So the watcher files every automatic capture as <see cref="EncounterType.Unknown"/>, which
    /// spends no zone, and until somebody says otherwise the first-encounter rule has nothing to
    /// compare against. Measured on the real run: fourteen Pokémon, zero zones spent, two captures
    /// in Ruta 1 and not a word.
    /// <para>
    /// Confirming is therefore a claim by the player, not a deduction, and it is recorded as one.
    /// </para>
    /// </remarks>
    ZoneConfirmed,

    /// <summary>
    /// What happened at a zone.s one encounter, said by the player on the map.
    /// </summary>
    /// <remarks>
    /// It replaces pinning a capture to a zone, and it says more than that could. A Nuzlocke zone
    /// is spent by <b>whatever</b> happened there first, and two of the three outcomes have no
    /// Pokémon to pin: a first encounter that fled leaves nothing behind, and neither does one that
    /// died before a ball was thrown. The old shape could not express either, so those zones sat
    /// looking free.
    /// </remarks>
    ZoneOutcomeSet,

    /// <summary>The player took back a zone confirmation, freeing the zone again.</summary>
    /// <remarks>
    /// A mis-click on a map has to be undoable, and undoing it by editing the database would break
    /// the chain. So it is its own event: the zone was spent, and then it was not.
    /// </remarks>
    ZoneCleared,

    /// <summary>
    /// Spins handed to the player outside the milestones that normally pay for them.
    /// </summary>
    /// <remarks>
    /// Its own type because it is the <b>other half of the counter</b>. How many spins a run owes
    /// is what the milestones earned minus the <see cref="RouletteSpun"/> entries, and a spin
    /// cannot be un-spun: there is no way to delete one event, by design, and deleting the run is
    /// the only DELETE there is. So giving a spin back has to be an addition, and an addition that
    /// says who decided it and why â otherwise the only way to do it would be editing the database
    /// underneath the chain, which is exactly what the chain exists to make impossible.
    /// <para>
    /// It carries <c>tiradas</c>, how many, and the reason in its description. It is deliberately
    /// visible in the history and in the shared standings' event count: a granted spin is not the
    /// same as an earned one, and nothing here pretends it is.
    /// </para>
    /// </remarks>
    RouletteGranted
}
