namespace PermaLocke.Core.Domain;

/// <summary>
/// What one player's application publishes about their run so the others can see it.
/// </summary>
/// <remarks>
/// <para>
/// A <b>summary, not the run</b>. The event chain is thousands of rows and holds everything that
/// ever happened; what a competition needs to see is the scoreboard. Publishing the whole chain
/// would be sending somebody your diary so they can read the last page.
/// </para>
/// <para>
/// <b>What this is NOT: proof.</b> It is a file written by another player's copy of PermaLocke,
/// living in a folder they can open. Anybody can edit it. There is no anti-cheat here and there is
/// not going to be one pretending to be here — the honest description is "this is what their
/// application said", and every screen that shows it says so.
/// </para>
/// <para>
/// <see cref="ChainHead"/> and <see cref="EventCount"/> are a <b>fingerprint</b>, not a signature.
/// They cannot tell you a number is true. What they can do is make two snapshots of the same run
/// comparable: a count that goes backwards means the run was restored from a copy, and a chain head
/// that changes without the count moving means the history was rewritten. That is information a
/// group of friends can act on themselves, which is the most an honest system can offer without a
/// server nobody is going to run.
/// </para>
/// </remarks>
/// <param name="Schema">Format version, so an older application can refuse a file it cannot read.</param>
/// <param name="ChainHead">Hash of the last event, as the fingerprint above.</param>
public sealed record RunSnapshot
{
    /// <summary>Current format. Bump only when a reader would get it wrong otherwise.</summary>
    /// <remarks>
    /// It stayed at 1 when <see cref="BattleReady"/> was added, deliberately. A reader refuses a
    /// snapshot from a <em>newer</em> schema, so bumping it would make every friend still on the
    /// old build reject every new snapshot and see an empty scoreboard — a much worse failure than
    /// missing one field. Adding optional fields is safe in both directions: an old reader ignores
    /// what it does not know, and a new reader gets null and says "sin dato". Bump it only when a
    /// change would make an old reader get something <b>wrong</b> rather than merely miss it.
    /// </remarks>
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    public required Guid RunId { get; init; }

    public required string PlayerName { get; init; }

    /// <summary>
    /// The player's profile id. Optional, like <see cref="BattleReady"/>, so older snapshots still
    /// read; with it, one player is one row no matter how many runs they start (§123).
    /// </summary>
    public Guid? PlayerId { get; init; }

    public required string RunName { get; init; }

    /// <summary>The role as the catalogue names it, not its id: this is read by people.</summary>
    public string RoleName { get; init; } = string.Empty;

    public string SeedLabel { get; init; } = string.Empty;

    public int Points { get; init; }

    public int Registered { get; init; }

    public int Alive { get; init; }

    public int Dead { get; init; }

    public int Traded { get; init; }

    public int StagesCleared { get; init; }

    public int? LevelCap { get; init; }

    public int AchievementsUnlocked { get; init; }

    public int AchievementsTotal { get; init; }

    public int EventCount { get; init; }

    public string ChainHead { get; init; } = string.Empty;

    /// <summary>
    /// Whether this player's world can link-battle against another compatible one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A link battle is a lockstep simulation: both consoles compute the same battle and exchange
    /// only the orders. That works with two <b>different</b> randomizations as long as neither
    /// touches the data the game reads <em>during</em> a battle — and of everything PermaLocke
    /// randomizes, only two options do: the shuffled base stats and the random abilities. Wild
    /// encounters, trainers, starters, field items, shops, the extra Pokémon and the boss megas can
    /// all differ freely, because none of them is consulted mid-battle.
    /// </para>
    /// <para>
    /// Null means <b>not known</b>, which is a third answer and not a "no": a run randomized before
    /// this was recorded has no such data, and saying "cannot battle" about it would be inventing a
    /// verdict. The screen says "sin dato" and leaves it to the player.
    /// </para>
    /// </remarks>
    public bool? BattleReady { get; init; }

    /// <summary>
    /// How many species the world was generated with: 1025 on the gen 8-9 expansion, 807 on the plain
    /// cartridge. Null when not recorded.
    /// </summary>
    /// <remarks>
    /// This is what decides link battles now (§124). COMBATES swaps every randomization out for the
    /// base game, so <see cref="BattleReady"/> no longer matters when it is used; the base game still
    /// has to be the same on both sides.
    /// </remarks>
    public int? WorldSpecies { get; init; }

    /// <summary>
    /// The species leading the player's party when this was published, as their picture in the friends list and
    /// the activity (§126). Null when the save could not be read.
    /// </summary>
    public int? AvatarSpecies { get; init; }

    public DateTimeOffset RunCreatedAt { get; init; }

    /// <summary>Every Pokémon registered in the run, for the organiser's player sheet in Admin (2026-09-26). Null from older apps.</summary>
    public IReadOnlyList<PokemonEntry>? Pokemon { get; init; }

    /// <summary>Hours played in the run by PermaLocke's own sessions, for Admin (2026-09-26). Null from older apps.</summary>
    public double? PlayedHours { get; init; }

    /// <summary>When this snapshot was published, by the clock of whoever published it.</summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// File name this snapshot belongs in, derived from the run so one player publishing twice
    /// replaces their own file instead of piling up.
    /// </summary>
    /// <remarks>
    /// Keyed on the run id and not the player name: two people called "Ash" would otherwise
    /// overwrite each other, and the same person starting a second run would lose the first.
    /// </remarks>
    public string FileName => $"permalocke-{RunId:N}.json";
}
