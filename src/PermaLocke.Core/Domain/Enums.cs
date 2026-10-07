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
    AdminGrant,

    /// <summary>An egg from the NURSERY of a MONOTYPE role (§221). At the end: stored as a number.</summary>
    Nursery
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
    /// What happened at a zone.s one encounter, as the map shows it.
    /// </summary>
    /// <remarks>
    /// It replaces pinning a capture to a zone, and it says more than that could. A Nuzlocke zone
    /// is spent by <b>whatever</b> happened there first, and two of the three outcomes have no
    /// Pokémon to pin: a first encounter that fled leaves nothing behind, and neither does one that
    /// died before a ball was thrown. The old shape could not express either, so those zones sat
    /// looking free.
    /// <para>
    /// Written by PermaLocke as <see cref="EventSource.AutoDetect"/> when a route's first wild battle
    /// ends. The ones with <see cref="EventSource.Player"/> are from when the map was clicked, before
    /// §118; they still count, and never override a detected one.
    /// </para>
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
    RouletteGranted,

    /// <summary>
    /// A death that PermaLocke recorded and should not have: the Pokémon is alive again and the
    /// penalty is paid back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same shape as <see cref="ZoneCleared"/>: the chain cannot lose an event, so undoing one is
    /// an addition. The <see cref="PokemonDied"/> stays in the history — it did get recorded — and
    /// this says, with a reason and a person, that it was wrong. It carries the id of the death it
    /// revokes, so a death cannot be revoked twice and anything that counts deaths can leave it out.
    /// </para>
    /// <para>
    /// The case it is for is a death marked by hand on the wrong Pokémon: MARCAR COMO CAÍDO takes
    /// points and holds the Pokémon at zero, and until this existed a slip there could only be undone
    /// by editing the database underneath the chain. It was built while chasing a death believed to
    /// be a misreading of the game; that death turned out to be real (§111), which is worth saying
    /// here so nobody reads this as proof that the watcher invents deaths.
    /// </para>
    /// <para>
    /// Appended at the end on purpose: the type is stored as a number, and a new value anywhere else
    /// would turn every event after it into something else.
    /// </para>
    /// </remarks>
    DeathRevoked,

    /// <summary>
    /// The first wild battle in a route started, so the route's single encounter is spent.
    /// </summary>
    /// <remarks>
    /// Detected, not claimed: the game's own wild battle counter went up while the player stood in a route
    /// of the map screen that had not spent its encounter. Written at the <b>start</b> of the battle, not at
    /// the end, because that is when the rule says the encounter is used — fleeing, fainting it or catching
    /// it all spend it. How the battle ended is written afterwards as a <see cref="ZoneOutcomeSet"/>. A
    /// duplicate does not spend the route and does not produce one of these (§117).
    /// </remarks>
    ZoneEncounterSpent,

    /// <summary>
    /// A run that existed before player profiles was tied to this machine's player (§123).
    /// </summary>
    /// <remarks>
    /// Once per run and only for runs with no owner: a run that already belongs to somebody is never
    /// reassigned, because that would let one player publish another's run as their own.
    /// </remarks>
    PlayerLinked,

    /// <summary>
    /// The official rules from the competition folder replaced this machine's configuration.
    /// </summary>
    /// <remarks>
    /// Carries each file with the hash it had before and after, so which prices and which
    /// achievements a run was played under is in its own history and not only in a folder (§123).
    /// </remarks>
    RulesAdopted,

    /// <summary>
    /// The player collected a gift the admin left in the shared folder (§129).
    /// </summary>
    /// <remarks>
    /// One of these per gift, carrying its id, and that is what makes a gift collectable exactly once: the
    /// check is «is there already an event for this id», the same way a one-off prize is one-off (§60). The
    /// admin cannot write here — only the player's own application can — so the event says who sent it and why.
    /// </remarks>
    AdminGiftClaimed,

    /// <summary>
    /// The move reminder taught a Pokémon a move it could remember, over the one it forgot (§142).
    /// </summary>
    /// <remarks>
    /// Its own type for the reason <see cref="EvsTrained"/> has one: it edits the partida outside anything the game
    /// did, and the log has to say so by name. Carries the move, the one it replaced and why it was allowed.
    /// </remarks>
    MoveRemembered,

    /// <summary>
    /// The first time PermaLocke saw a Poké Ball in the player's bag in this run (§149).
    /// </summary>
    /// <remarks>
    /// Routes start counting from here: before the first ball the story walks you through tall grass, and a
    /// battle you could not have caught anything in does not spend the route. An event and not a check of the
    /// bag because the bag empties — a player who has thrown every ball has still had them.
    /// </remarks>
    FirstPokeBallSeen,

    /// <summary>
    /// A <see cref="TeamWiped"/> that should not have been charged, paid back and taken off the count of wipes (§161).
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="DeathRevoked"/>: the wipe stays in the chain and this says it was wrong, who said
    /// so and why, and refunds exactly what that wipe took. Written for the second wipe the watcher charged on
    /// 2026-09-21 when the Pokémon Centre healed a fallen party and PermaLocke put it back down. Appended at the end
    /// because the type is stored as a number.
    /// </remarks>
    WipeRevoked,

    /// <summary>The player changed a Pokémon's nickname from the viewer, written into the save (2026-09-26). At the end: stored as a number.</summary>
    PokemonRenamed,

    /// <summary>
    /// Something that looks like getting round the rules by reloading, seen by PermaLocke and shown only to the organiser
    /// (2026-09-26): a save state, the game played or restored without PermaLocke, or the emulator closed mid-battle.
    /// </summary>
    /// <remarks>
    /// Evidence, not a sentence: it moves no points and changes no Pokémon; the organiser reads it in Admin and decides.
    /// Its <c>tipo</c> is one of <see cref="PermaLocke.Core.Services.IntegrityKinds"/>. At the end: stored as a number.
    /// </remarks>
    IntegrityFlag,

    /// <summary>
    /// The organiser closed JUGAR for this player (<c>cerrado</c> = true) or opened it again, with a reason (2026-09-26).
    /// </summary>
    /// <remarks>An event, so the lock and its reason stay in the history. At the end: stored as a number.</remarks>
    PlayLock,

    /// <summary>
    /// An egg of the NURSERY of a MONOTYPE role (§221), written into the save. Each one is a spin spent. At the end: stored as a number.
    /// </summary>
    NurseryEgg,

    /// <summary>
    /// Spins of the NURSERY handed over by a testing tool (2026-10-07); the number is in the data («tiradas»). At the end: stored as a number.
    /// </summary>
    NurseryGrant,

    /// <summary>
    /// An egg of the NURSERY hatched: its entry takes the species it turned out to be (§230). At the end: stored as a number.
    /// </summary>
    NurseryHatch
}
