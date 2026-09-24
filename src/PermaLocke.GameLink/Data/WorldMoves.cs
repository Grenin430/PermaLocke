using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>What one move is in the installed world, before any name is attached.</summary>
/// <param name="Category">Already translated to <see cref="MoveSheet"/>'s 0 status, 1 physical, 2 special.</param>
/// <param name="Accuracy">Zero for a move that never misses.</param>
public sealed record WorldMove(int Type, int Category, int Power, int Accuracy, int PP);

/// <summary>
/// The installed world's level-up learnsets and move table, for everything that has to agree with the game being
/// played about what a Pokémon learns.
/// </summary>
/// <remarks>
/// <para>
/// Process-wide and set once at startup, the same contract as <see cref="WorldLimits"/> and for the same reason:
/// the Pokémon builder is a static helper and threading a service through it would touch every delivery path for
/// one table. Empty means «ask the cartridge», and every reader has to cope with that.
/// </para>
/// <para>
/// It matters because learnsets are randomized in this competition. A move list taken from PKHeX is the cartridge's,
/// which is the right answer for a vanilla game and the wrong one here: a randomized Pokémon would be offered, or
/// built with, moves its species does not learn in the world it lives in (§142).
/// </para>
/// </remarks>
public static class WorldMoves
{
    /// <summary>Learnset per row of the personal table, in the order the game lists it. Null when not published.</summary>
    public static IReadOnlyList<IReadOnlyList<LevelUpMove>>? Learnsets { get; set; }

    /// <summary>
    /// The row a form reads its learnset from, for the forms that have their own row. Every other form reads its
    /// species' row, which is the game's rule (<c>PersonalEntry7.RowOf</c>).
    /// </summary>
    public static IReadOnlyDictionary<(int Species, int Form), int> FormRows { get; set; } =
        new Dictionary<(int Species, int Form), int>();

    /// <summary>The move table, by move id. Null when not published.</summary>
    public static IReadOnlyList<WorldMove>? Moves { get; set; }

    /// <summary>What the game says each move does, by move id, as one paragraph. Null when not published.</summary>
    public static IReadOnlyList<string>? MoveDescriptions { get; set; }

    /// <summary>The move names the game shows, by move id. Null when not published.</summary>
    public static IReadOnlyList<string>? MoveNames { get; set; }

    /// <summary>The learnset of a species in a form, or null when the world has not published its learnsets.</summary>
    public static IReadOnlyList<LevelUpMove>? LevelUpOf(int species, int form)
    {
        if (Learnsets is not { } all || species <= 0)
        {
            return null;
        }

        var row = form > 0 && FormRows.TryGetValue((species, form), out var own) ? own : species;

        if (row >= all.Count)
        {
            return null;
        }

        return Banned.Count == 0 ? all[row] : [.. all[row].Where(entry => !Banned.Contains(entry.Move))];
    }

    /// <summary>
    /// Moves nobody may learn or be handed, from <c>bannedMoves</c> in <c>Data/randomizer.json</c> (§162).
    /// </summary>
    /// <remarks>
    /// The randomizer already takes them out of a world it generates. This is for everything the application builds
    /// or teaches on its own — the move reminder, the gacha, the wonder trade — so a world generated before the ban,
    /// or the cartridge's own learnsets when no world is installed, cannot hand one out either.
    /// </remarks>
    public static IReadOnlySet<int> Banned { get; set; } = new HashSet<int>();

    /// <summary>A move of the installed world, or null when there is no table or no such move.</summary>
    public static WorldMove? MoveOf(int move) =>
        Moves is { } all && move > 0 && move < all.Count ? all[move] : null;

    /// <summary>
    /// The four moves a Pokémon of this species knows at this level, the way the game fills a wild one: the last four
    /// it has learnt, in order. Null when the world has not published its learnsets.
    /// </summary>
    public static int[]? MovesAt(int species, int form, int level)
    {
        if (LevelUpOf(species, form) is not { } learnset)
        {
            return null;
        }

        var known = new List<int>(4);

        foreach (var entry in learnset.Where(e => e.Level <= level && e.Move > 0).OrderBy(e => e.Level))
        {
            if (known.Contains(entry.Move))
            {
                continue;
            }

            if (known.Count == 4)
            {
                known.RemoveAt(0);
            }

            known.Add(entry.Move);
        }

        var moves = new int[4];
        known.CopyTo(moves);
        return moves;
    }
}
