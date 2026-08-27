namespace PermaLocke.Core.Domain;

/// <summary>
/// One way of playing the competition: how points move, and how hard the cartridge is made.
/// </summary>
/// <remarks>
/// <para>
/// The role is chosen <b>before anything else</b>, because part of it is baked into the ROM. Once
/// a run has been randomized for a role, changing role means randomizing again — and that changes
/// the world of a run already in progress.
/// </para>
/// <para>
/// Configuration and not an enum, so a competition can add a role without a new build.
/// </para>
/// </remarks>
/// <param name="Earn">Multiplies points <em>gained</em>. Never what is spent.</param>
/// <param name="Lose">Multiplies points <em>lost</em> to penalties. Zero means a role never loses.</param>
/// <param name="EnemyLevelPercent">
/// How much the trainers' levels are raised over the cartridge, as a percentage. The competition's
/// base edit is 20 for everyone; a harder role goes above it.
/// </param>
/// <param name="PlayerCapPercent">
/// How much the player's own level cap is raised over the cartridge. Deliberately separate from
/// <paramref name="EnemyLevelPercent"/>: a role gets harder precisely by letting the rivals
/// climb without letting the player follow.
/// </param>
/// <param name="ExtraTrainerPokemon">Pokémon added to the important battles.</param>
/// <param name="Roulette">
/// True when the run has to spin a wheel after every milestone and live with what comes out.
/// <para>
/// A flag and not a fourth number because it is not a dial: it adds a screen, a duty and a set of
/// consequences that no multiplier can express. Every other role leaves it false.
/// </para>
/// </param>
public sealed record Role(
    string Id,
    string Name,
    string Summary,
    string Description,
    double Earn,
    double Lose,
    int EnemyLevelPercent,
    int PlayerCapPercent,
    int ExtraTrainerPokemon,
    bool Roulette = false)
{
    /// <summary>What a reward of <paramref name="amount"/> is really worth in this role.</summary>
    /// <remarks>
    /// Rounded away from zero so a half-point reward is worth something: with the halving role, a
    /// 25 point achievement pays 13 rather than quietly paying 12 and looking like a rounding bug.
    /// </remarks>
    public int Reward(int amount) => Scale(amount, Earn);

    /// <summary>What a penalty of <paramref name="amount"/> really costs in this role.</summary>
    public int Penalty(int amount) => Scale(amount, Lose);

    private static int Scale(int amount, double factor) =>
        (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero);

    /// <summary>True when the role changes what a number is worth, so the screen can say so.</summary>
    public bool ChangesPoints => Math.Abs(Earn - 1.0) > 0.001 || Math.Abs(Lose - 1.0) > 0.001;

    /// <summary>The level cap of a milestone, raised by whatever this role gives the player.</summary>
    public int CapFor(int cartridgeLevel) =>
        Math.Clamp((int)Math.Round(cartridgeLevel * (1 + (PlayerCapPercent / 100.0)),
            MidpointRounding.AwayFromZero), 1, 100);

    /// <summary>A trainer's level, raised by whatever this role gives the trainers.</summary>
    public int TrainerLevelFor(int cartridgeLevel) =>
        Math.Clamp((int)Math.Round(cartridgeLevel * (1 + (EnemyLevelPercent / 100.0)),
            MidpointRounding.AwayFromZero), 1, 100);
}

/// <summary>
/// The roles a competition offers. A port so nothing learns they come from a JSON file.
/// </summary>
public interface IRoleCatalog
{
    IReadOnlyList<Role> All { get; }

    /// <summary>
    /// Trainer classes that count as an important battle, by id.
    /// </summary>
    /// <remarks>
    /// Shared by every role, because which battles matter is a property of the competition and
    /// not of the difficulty. By <b>id</b> and not by name on purpose: Giovanni and the Rainbow
    /// Rocket grunts share a class name, and only the id tells them apart.
    /// </remarks>
    IReadOnlySet<int> ImportantTrainerClasses { get; }

    /// <summary>
    /// The role of an id, or null when it is unknown.
    /// </summary>
    /// <remarks>
    /// Null and not a default: a run whose role cannot be resolved must say so, because guessing
    /// "normal" would silently pay a Cagoneta double and a Experto two thirds.
    /// </remarks>
    Role? Find(string? id);
}
