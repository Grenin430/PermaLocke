namespace PermaLocke.Core.Abstractions;

/// <summary>
/// The map the player is on, as the game numbers it, and the zone of the run it belongs to.
/// </summary>
/// <param name="Map">Index into <c>zonedata</c>, the game's own map numbering.</param>
/// <param name="World">The world that map belongs to. Read from the game and checked against the cartridge.</param>
/// <param name="LocationId">The normalised location id the run, the map screen and every capture use.</param>
public sealed record FieldZone(int Map, int World, string LocationId, string LocationName);

/// <summary>
/// Says where the player is.
/// </summary>
/// <remarks>
/// A port so the rules never learn how the zone is read. Null is an answer, not a failure to paper over:
/// a rule that takes items away must not fire on a zone nobody is sure about.
/// </remarks>
public interface IZoneProvider
{
    /// <summary>The zone right now, or null when it cannot be established this instant.</summary>
    FieldZone? CurrentZone();

    /// <summary>
    /// The last zone that was established, and when, or null if none has been since connecting.
    /// </summary>
    /// <remarks>
    /// During a battle the game drops the records the zone is read from, so the zone of a battle is the one
    /// confirmed just before it started. A wild battle cannot move the player to another zone.
    /// </remarks>
    (FieldZone Zone, DateTimeOffset At)? LastConfirmed { get; }
}

/// <summary>
/// The game's own trainer-card counters that tell what a battle was and how it ended.
/// </summary>
/// <param name="WildBattles">Record 4. Goes up the moment a wild battle starts, fleeing included.</param>
/// <param name="Caught">Record 6.</param>
/// <param name="Fled">Record 46.</param>
/// <param name="ShinyEncountered">Record 127.</param>
public sealed record BattleCounters(int WildBattles, int Caught, int Fled, int ShinyEncountered);

/// <summary>Reads <see cref="BattleCounters"/> from the running game.</summary>
public interface IBattleCounters
{
    /// <summary>Why encounters cannot currently be detected, or null when the reader is ready.</summary>
    string? Problem => null;

    /// <summary>The counters now, or null when they cannot be read and trusted.</summary>
    BattleCounters? Read();
}

/// <summary>The species the player's Pokédex says were caught.</summary>
public interface IOwnedSpecies
{
    /// <summary>Species flagged as caught, or null when the Pokédex cannot be read.</summary>
    Task<IReadOnlySet<int>?> CaughtAsync(CancellationToken ct = default);
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

    /// <summary>How many of each item the player is carrying, in one read of the bag.</summary>
    /// <returns>An empty dictionary when the bag cannot be read, which means «unknown», not «none».</returns>
    IReadOnlyDictionary<int, int> CarriedAll(IReadOnlyCollection<int> itemIds);

    /// <summary>How much of an item <paramref name="run"/> took away and must return.</summary>
    /// <remarks>
    /// What is owed belongs to the run that took it (§147). The ledger outlives the run on disk, and the first
    /// version answered for any run: after starting over, a new game on Route 1 received the old one's twelve
    /// kinds of ball, a Master Ball among them. Owed to another run is owed to nobody here.
    /// </remarks>
    int Owed(Guid run, int itemId);

    /// <summary>
    /// Takes away everything carried of the item, adding it to what <paramref name="run"/> already owes. False
    /// when nothing was taken.
    /// </summary>
    bool Withhold(Guid run, int itemId);

    /// <summary>
    /// Returns what <paramref name="run"/> owes <b>on top of</b> whatever the player carries now — balls picked up
    /// or bought while the rest were withheld are not lost. False when nothing was owed.
    /// </summary>
    bool GiveBack(Guid run, int itemId);

    /// <summary>
    /// Writes off what <paramref name="run"/> owes of an item without giving anything back. Returns how much was
    /// written off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the one case where returning them would be wrong: the balls were taken from the <b>live bag</b> and the
    /// player never saved afterwards, so the saved game still holds them and the debt is to nobody.
    /// </para>
    /// <para>
    /// Measured on 2026-09-21. A route was spent by mistake, the bag was emptied at 02:50:49 and again at 02:51:44 —
    /// the game had reloaded in between and the balls were back — so the ledger, which adds (§147), ended up owing
    /// <b>20 Poké Ball and 20 Super Ball</b> when the player had only ever had ten of each. The saved game, written
    /// at 02:50:44, still had all of them. Giving that debt back would have handed over twenty balls that were
    /// never taken.
    /// </para>
    /// <para>
    /// Deliberately separate from <see cref="GiveBack"/> and never called on its own by the application: writing off
    /// a debt is a decision about somebody's things, and it belongs to whoever can see the bag and the ledger side
    /// by side.
    /// </para>
    /// </remarks>
    int Forget(Guid run, int itemId);
}
