namespace PermaLocke.Core.Domain;

/// <summary>What a face of the wheel does when it wins.</summary>
/// <remarks>
/// Named rather than free-form so a typo in <c>Data/roulette.json</c> is a face that refuses to
/// load instead of a face that quietly does nothing on the day it comes out.
/// </remarks>
public enum RouletteEffect
{
    /// <summary>Free gacha rolls, one per banner named.</summary>
    Gacha,

    /// <summary>A good ability onto N party Pokémon.</summary>
    HabilidadBuena,

    /// <summary>A bad ability onto N party Pokémon.</summary>
    HabilidadMala,

    /// <summary>Points, signed. Never multiplied by the role: the wheel says what it says.</summary>
    Puntos,

    /// <summary>N different healing items, <c>Each</c> of each.</summary>
    DarCurativos,

    /// <summary>N different healing items taken away, <c>Each</c> of each.</summary>
    QuitarCurativos,

    /// <summary>N TMs the player does not already carry.</summary>
    DarMt,

    /// <summary>N TMs from the ones the player does carry.</summary>
    QuitarMt,

    /// <summary>The six IVs to 31 on N party Pokémon.</summary>
    IvPerfectos,

    /// <summary>The six IVs to 0 on N party Pokémon.</summary>
    IvCero,

    /// <summary>N party Pokémon die. It costs no points: the wheel is not a penalty.</summary>
    Muerte
}

/// <param name="Good">Which half of the pool it belongs to. Only ever shown, never used to pick.</param>
/// <param name="Amount">
/// How many. Pokémon for the ones that touch the party, points for <see cref="RouletteEffect.Puntos"/>
/// — signed —, distinct items for the healing ones, TMs for the TM ones.
/// </param>
/// <param name="Each">How many of each item. Only the healing faces use it.</param>
/// <param name="Short">
/// What the wedge says, in two or three words. <see cref="Name"/> is a sentence and a wedge is not
/// a place for a sentence; when it is missing the wedge falls back to the name rather than to
/// nothing.
/// </param>
/// <param name="Figure">
/// The number the wedge shows big — «+200», «×3», «31». Written out rather than derived from
/// <see cref="Amount"/> because the same amount reads differently on different faces: three
/// distinct healing items at three of each is nine objects, not three.
/// </param>
/// <param name="ItemIcon">
/// Item whose cartridge icon stands for this face, or zero. Only ids whose icon has actually been
/// measured draw anything, so a wrong number is a blank wedge and never the wrong picture.
/// </param>
/// <param name="SpeciesIcon">Species whose sprite stands for this face, or zero.</param>
public sealed record RouletteFace(
    string Id,
    string Name,
    string Detail,
    bool Good,
    RouletteEffect Effect,
    int Amount = 0,
    int Each = 1,
    IReadOnlyList<string>? Banners = null,
    string Short = "",
    string Figure = "",
    int ItemIcon = 0,
    int SpeciesIcon = 0)
{
    public IReadOnlyList<string> BannerIds { get; } = Banners ?? [];

    /// <summary>What to write on the wedge: the short form when there is one, else the name.</summary>
    public string Label => string.IsNullOrWhiteSpace(Short) ? Name : Short;
}

/// <summary>Everything the wheel is made of, read from configuration rather than compiled in.</summary>
public interface IRouletteCatalog
{
    IReadOnlyList<RouletteFace> Faces { get; }

    /// <summary>Ability ids, resolved from the names in the file. Empty when nothing resolved.</summary>
    IReadOnlyList<int> GoodAbilities { get; }

    IReadOnlyList<int> BadAbilities { get; }

    /// <summary>Item ids of the healing items the wheel hands out and takes away.</summary>
    IReadOnlyList<int> HealingItems { get; }

    /// <summary>How many spins each milestone owes.</summary>
    int SpinsPerTrial { get; }

    int SpinsForLeague { get; }

    int SpinsForRematch { get; }

    /// <summary>Achievement ids that owe a spin each.</summary>
    IReadOnlyList<string> TrialAchievements { get; }

    string LeagueAchievement { get; }

    string RematchAchievement { get; }
}

/// <param name="Faces">The six the wheel shows, in the order they sit on it.</param>
/// <param name="WinningIndex">Which of the six it stops on.</param>
/// <remarks>
/// The six are drawn from all sixteen together, so a wheel of six good faces or six bad ones is a
/// perfectly ordinary wheel. Nothing balances it, on purpose.
/// </remarks>
public sealed record RouletteWheel(IReadOnlyList<RouletteFace> Faces, int WinningIndex, int Number, ulong Seed)
{
    public RouletteFace Winner => Faces[WinningIndex];
}

/// <summary>One Pokémon of the party, as the wheel needs to know it.</summary>
/// <param name="Slot">Zero-based party slot, which is where it will be written back.</param>
/// <param name="Pid">What identifies it. Nothing is written to a slot whose PID has changed.</param>
public sealed record RoulettePokemon(int Slot, uint Pid, int Species, string Name, int Level);

/// <summary>What the wheel can see of the player's game before it decides anything.</summary>
/// <param name="TmsAll">Every TM this cartridge has, straight from the bag's own list.</param>
/// <param name="TmsHeld">The ones the player carries.</param>
/// <param name="HealingHeld">How many of each healing item the player carries.</param>
public sealed record RouletteWorld(
    IReadOnlyList<RoulettePokemon> Party,
    IReadOnlyList<int> TmsAll,
    IReadOnlyList<int> TmsHeld,
    IReadOnlyDictionary<int, int> HealingHeld)
{
    public static RouletteWorld Empty { get; } = new([], [], [], new Dictionary<int, int>());
}

/// <param name="ItemId">Cartridge item id.</param>
/// <param name="Delta">How many to add. Negative takes away.</param>
public sealed record RouletteItemChange(int ItemId, int Delta);

/// <param name="Pokemon">Who it lands on, already chosen. Empty when the face touches nobody.</param>
/// <param name="Abilities">One per Pokémon, in the same order. Empty unless it is an ability face.</param>
public sealed record RouletteAction(
    RouletteEffect Effect,
    IReadOnlyList<RoulettePokemon> Pokemon,
    IReadOnlyList<int> Abilities,
    IReadOnlyList<RouletteItemChange> Items,
    int Points)
{
    public static RouletteAction Nothing(RouletteEffect effect) => new(effect, [], [], [], 0);

    /// <summary>True when there is genuinely nothing to write: an empty party, an empty bag.</summary>
    public bool IsEmpty => Pokemon.Count == 0 && Items.Count == 0;
}

/// <param name="Lines">One line per thing that really happened, for the screen and the log.</param>
public sealed record RouletteApplyResult(bool Applied, string Message, IReadOnlyList<string> Lines)
{
    public static RouletteApplyResult Nothing(string message) => new(false, message, []);
}

/// <summary>
/// The part of the roulette that touches the player's game.
/// </summary>
/// <remarks>
/// A port, so the wheel never learns whether abilities, IVs, deaths and items live in a save file
/// or in the emulator's memory. Today it is the save file, and that is not an accident: the
/// sixteen faces between them need the party, the bag and the boxes, and only the save has all
/// three behind one door. One open, one backup, one write, one re-read.
/// </remarks>
public interface IRouletteWorldPort
{
    /// <summary>True when the wheel could act right now, with the reason when it could not.</summary>
    bool CanActNow(out string reason);

    /// <summary>What the wheel needs to see before deciding: the party and the bag.</summary>
    Task<RouletteWorld> ReadAsync(CancellationToken ct = default);

    /// <summary>Writes an already-decided action. Reports only what it saw land.</summary>
    Task<RouletteApplyResult> ApplyAsync(RouletteAction action, CancellationToken ct = default);
}

/// <summary>How a spin ended.</summary>
public enum RouletteOutcome
{
    /// <summary>Spun, applied and recorded.</summary>
    Spun,

    /// <summary>Nothing owed. The wheel only turns when a milestone has paid for it.</summary>
    NothingOwed,

    /// <summary>This run does not play with the wheel.</summary>
    NotThisRole,

    /// <summary>The game is not there to write into. Nothing spun, nothing owed away.</summary>
    GameUnreachable,

    /// <summary>Something went wrong while applying. The spin is not consumed.</summary>
    Failed
}

/// <param name="Wheel">The six faces and the winner, so the screen can play it back.</param>
/// <param name="Owed">Spins still owed after this one.</param>
public sealed record RouletteSpinResult(
    RouletteOutcome Outcome,
    RouletteWheel? Wheel,
    IReadOnlyList<string> Lines,
    int Owed,
    string Message)
{
    public bool Succeeded => Outcome == RouletteOutcome.Spun;
}
