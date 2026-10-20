using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>The alteration a Pokémon carries between battles, as the game stores it.</summary>
public enum CapStatus
{
    None,
    Poison,
    Burn,
    Paralysis,
    Sleep,
    Freeze
}

/// <summary>One of a Pokémon's moves, reduced to what the panel draws: the colour of its type, and whether it has no PP left.</summary>
/// <param name="Type">The type id (0 to 17), or -1 when the world could not say.</param>
public sealed record CapMove(int Type, bool Empty, string? Name = null);

/// <summary>
/// One Pokémon on the cap panel. An egg shows nothing but itself (§230). Everything after <c>Egg</c> is optional and comes only
/// from what the game or the installed world can really say: nothing is shown that was not read.
/// </summary>
/// <param name="Pid">What the animations follow it by, so a level-up or a heal is seen on the right one (0 when unknown).</param>
/// <param name="Types">One or two type ids, from the installed world.</param>
/// <param name="Item">The name of the held item, or null for none.</param>
/// <param name="ItemIcon">The cartridge's icon of the held item, when it has one; the panel draws it small beside the level.</param>
/// <param name="Ability">The name of its ability, read from its structure in the game (singleplayer, 2026-10-13).</param>
/// <param name="Exp">Progress to its next level, 0 to 1; -1 when it could not be read.</param>
public sealed record CapPlaqueMember(BitmapSource? Sprite, string Name, int Level, int Hp, int MaxHp, bool Egg,
    uint Pid = 0, IReadOnlyList<int>? Types = null, string? Item = null, CapStatus Status = CapStatus.None,
    double Exp = -1, IReadOnlyList<CapMove>? Moves = null, BitmapSource? ItemIcon = null, string? Ability = null)
{
    public bool Fallen => !Egg && Hp <= 0;

    /// <summary>At the cap or over it: it can learn nothing more from the next battle.</summary>
    public bool AtCap(int cap) => !Egg && !Fallen && Level >= cap;

    /// <summary>One level under the cap and most of the way to the next: the next battle may put it at the cap.</summary>
    public bool NearCap(int cap) => !Egg && !Fallen && Level == cap - 1 && Exp >= 0.8;
}

/// <summary>One of the run's fallen, for the memorial and for the little graves under the party.</summary>
/// <param name="FellTo">The rival it fell to, when the run recorded it.</param>
/// <param name="Level">The level it had when it fell, from the 'nivel' of the death event; 0 when the run did not keep it.</param>
/// <param name="AgeDays">Days since it fell: how overgrown its fossil is.</param>
public sealed record CapGrave(BitmapSource? Sprite, string Name, string? FellTo = null, int Level = 0, int AgeDays = 0);

/// <summary>
/// What the cap panel says: the stage, its cap, the next one, the party's strongest, the party, and what the run knows besides.
/// </summary>
/// <param name="InBattle">A battle is going on, so the HP are the battle's own and the status of the party is not known.</param>
/// <param name="Graves">The run's fallen, the latest first; null when the run's record could not be read.</param>
/// <param name="Cleared">How many stages the player has cleared.</param>
/// <param name="Trials">How many trials there are in all: zero hides the lanterns.</param>
/// <param name="Hour">The game's hour of the day, 0 to 24, or -1 when it cannot be known: the sky of the sea follows it.</param>
/// <param name="ActiveShare">The life of the Pokémon on the field in a battle, 0 to 1, read from the game's bar; null when it cannot be seen. The sea's danger follows it.</param>
/// <param name="Seed">A number out of the run (its id): the relief of the floor, the coral and the rare events come from it.</param>
/// <param name="DeathScenes">Whether a fall is shown as one of the deaths of the panel (CapDeath).</param>
/// <param name="Crystals">Every trial's Z-Crystal cleared so far, in order, with its icon: they stay set in the abyss's sand.</param>
/// <param name="CrystalItem">The Z-Crystal of the last trial cleared (its item id), 0 when there is none: the trial film (CapTrial) shows it.</param>
/// <param name="Crystal">That crystal's icon, out of the player's cartridge.</param>
/// <param name="Encounter">A route's first encounter being shown (CapEncounter), or null.</param>
public sealed record CapPlaqueData(string Trial, int Cap, int? Next, int? Highest, IReadOnlyList<CapPlaqueMember> Party,
    bool InBattle = false, IReadOnlyList<CapGrave>? Graves = null, int Cleared = 0, int Trials = 0, double Hour = -1, int Seed = 0,
    IReadOnlyList<CapEvent>? Events = null, double? ActiveShare = null, bool DeathScenes = true, int CrystalItem = 0, BitmapSource? Crystal = null,
    IReadOnlyList<(int Item, BitmapSource? Icon)>? Crystals = null, CapShinyTurn? Shiny = null, CapVialState? Vial = null,
    CapToggle? Repel = null, CapToggle? Turbo = null, CapEncounterTurn? Encounter = null,
    IReadOnlyList<CapItemDrop>? Items = null);

/// <summary>
/// The Pokévial in the overlay (2026-10-16): its uses left and the most it holds, and when it was last used (on the panel's clock) with the uses it had
/// before, for the animation of the healing.
/// </summary>
public sealed record CapVialState(int Uses, int Max, double UsedAt = double.NegativeInfinity, int From = 0);

/// <summary>
/// An item of the overlay turned on and off, the Repelente Infinito or the Incubadora Turbo (2026-10-16): when it was last turned on and off, on
/// the panel's clock. On between the two; never on, both minus infinity.
/// </summary>
public sealed record CapToggle(double OnAt = double.NegativeInfinity, double OffAt = double.PositiveInfinity)
{
    public static CapToggle Off { get; } = new(double.NegativeInfinity, double.NegativeInfinity);
}

/// <summary>What can happen to one Pokémon that the sea shows as something that happens to its creature.</summary>
public enum CapEventKind
{
    /// <summary>It lost life: <see cref="CapEvent.Amount"/> points.</summary>
    Hit,

    /// <summary>It got life back: <see cref="CapEvent.Amount"/> points.</summary>
    Heal,

    /// <summary>It got up from zero life: <see cref="CapEvent.Amount"/> points.</summary>
    Revive,

    /// <summary>It went up a level.</summary>
    Level,

    /// <summary>It fell: its life reached zero.</summary>
    Fell,

    /// <summary>An egg hatched.</summary>
    Hatch,

    /// <summary>It got a status: <see cref="CapEvent.Status"/>.</summary>
    StatusIn,

    /// <summary>It lost a status (cured): <see cref="CapEvent.Status"/> is the one it had.</summary>
    StatusOut,

    /// <summary>The held item changed: <see cref="CapEvent.Extra"/> 0 it got one, 1 it lost it, 2 it swapped it.</summary>
    ItemChanged,

    /// <summary>It learned a move: <see cref="CapEvent.Extra"/> is the type of the move, -1 when unknown.</summary>
    MoveLearned
}

/// <summary>
/// Something that happened to a Pokémon, with the moment it is shown (2026-10-11): seconds on the panel's clock, counted from when the data said it —
/// the reading of the fast path or the bar of the game — and never from when it was painted, so that the delay of one is not added to the other.
/// Events live a few seconds in the data and are then dropped; <see cref="Seq"/> is how the animation tells a new one from one it has seen.
/// </summary>
public sealed record CapEvent(long Seq, CapEventKind Kind, uint Pid, int Slot, double At, int Amount = 0, CapStatus Status = CapStatus.None, int Extra = 0);
