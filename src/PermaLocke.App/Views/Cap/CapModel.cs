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
public sealed record CapMove(int Type, bool Empty);

/// <summary>
/// One Pokémon on the cap panel. An egg shows nothing but itself (§230). Everything after <c>Egg</c> is optional and comes only
/// from what the game or the installed world can really say: nothing is shown that was not read.
/// </summary>
/// <param name="Pid">What the animations follow it by, so a level-up or a heal is seen on the right one (0 when unknown).</param>
/// <param name="Types">One or two type ids, from the installed world.</param>
/// <param name="Item">The name of the held item, or null for none.</param>
/// <param name="Exp">Progress to its next level, 0 to 1; -1 when it could not be read.</param>
public sealed record CapPlaqueMember(BitmapSource? Sprite, string Name, int Level, int Hp, int MaxHp, bool Egg,
    uint Pid = 0, IReadOnlyList<int>? Types = null, string? Item = null, CapStatus Status = CapStatus.None,
    double Exp = -1, IReadOnlyList<CapMove>? Moves = null)
{
    public bool Fallen => !Egg && Hp <= 0;
}

/// <summary>One of the run's fallen, for the memorial and for the little graves under the party.</summary>
/// <param name="FellTo">The rival it fell to, when the run recorded it.</param>
/// <param name="Level">The level it had when it fell, or 0 when the run did not keep it.</param>
public sealed record CapGrave(BitmapSource? Sprite, string Name, string? FellTo = null, int Level = 0);

/// <summary>
/// What the cap panel says: the stage, its cap, the next one, the party's strongest, the party, and what the run knows besides.
/// </summary>
/// <param name="InBattle">A battle is going on, so the HP are the battle's own and the status of the party is not known.</param>
/// <param name="Graves">The run's fallen, the latest first; null when the run's record could not be read.</param>
/// <param name="Cleared">How many stages the player has cleared.</param>
/// <param name="Trials">How many trials there are in all: zero hides the gems.</param>
public sealed record CapPlaqueData(string Trial, int Cap, int? Next, int? Highest, IReadOnlyList<CapPlaqueMember> Party,
    bool InBattle = false, IReadOnlyList<CapGrave>? Graves = null, int Cleared = 0, int Trials = 0);
