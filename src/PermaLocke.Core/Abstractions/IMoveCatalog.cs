namespace PermaLocke.Core.Abstractions;

/// <summary>One entry of a level-up learnset: the move and the level it is learnt at.</summary>
/// <param name="Level">
/// As the game stores it. <b>Zero is «al evolucionar»</b> — learnt the moment the Pokémon becomes this species —
/// and one is what it starts with; both are always within reach, whatever its level.
/// </param>
public sealed record LevelUpMove(int Move, int Level)
{
    /// <summary>The game's code for a move learnt on evolving into the species.</summary>
    public const int OnEvolution = 0;

    public bool IsOnEvolution => Level == OnEvolution;
}

/// <summary>What a move is, as the game being played describes it.</summary>
/// <param name="Category">
/// 0 status, 1 physical, 2 special — already translated from the game's own byte —, or -1 when it cannot be known.
/// </param>
/// <param name="Power">Zero when it does no direct damage, or when it cannot be known.</param>
/// <param name="Accuracy">Zero when it never misses, or when it cannot be known.</param>
/// <param name="PP">Zero only for a move that cannot be used: the game will not let it be picked.</param>
/// <param name="Description">What the game says the move does, as one paragraph; empty when it cannot be read.</param>
public sealed record MoveSheet(int Id, string Name, int Type, string TypeName, int Category, int Power, int Accuracy,
    int PP, string Description = "")
{
    public const int Unknown = -1;
    public const int Status = 0;
    public const int Physical = 1;
    public const int Special = 2;

    public string CategoryName => Category switch
    {
        Physical => "Físico",
        Special => "Especial",
        Status => "Estado",
        _ => "?"
    };
}

/// <summary>
/// What each Pokémon learns and what each move is, in the world the player is actually playing.
/// </summary>
/// <remarks>
/// <para>
/// A port, because the answer lives in two different places. With a randomized world installed it is
/// that world's own tables — the learnsets are randomized, so a Pokémon learns different things than it
/// would on the cartridge, and <b>a different species learns differently even within one family</b>. Without
/// one, the cartridge's, which PKHeX ships.
/// </para>
/// <para>
/// Null is «no se sabe», never «no aprende nada». A learnset nobody could read and an empty one are different
/// answers, and the move reminder says which it got.
/// </para>
/// </remarks>
public interface IMoveCatalog
{
    /// <summary>The level-up learnset of a species in a form, in the order the game lists it.</summary>
    IReadOnlyList<LevelUpMove>? LevelUp(int species, int form);

    /// <summary>The move, or null when the world has no such move.</summary>
    MoveSheet? Describe(int move);

    /// <summary>Where the answers come from, in words: the installed world or the cartridge.</summary>
    string Source { get; }

    /// <summary>A move nobody may be taught (§162). None by default.</summary>
    bool IsBanned(int move) => false;
}
