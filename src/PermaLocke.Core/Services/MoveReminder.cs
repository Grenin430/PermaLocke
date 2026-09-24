using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Services;

/// <summary>Why a move is on the reminder's list.</summary>
public enum RememberedFrom
{
    /// <summary>It knew it when PermaLocke first saw it, or the game keeps it as a move it can always relearn.</summary>
    FirstKnown,

    /// <summary>Its species learns it on evolving into it.</summary>
    Evolution,

    /// <summary>Its species learns it at a level it has already reached.</summary>
    Level
}

/// <summary>One move the reminder can teach.</summary>
/// <param name="Level">The learnset level for <see cref="RememberedFrom.Level"/>, zero otherwise.</param>
public sealed record RememberableMove(int Move, RememberedFrom From, int Level = 0);

/// <summary>
/// Which moves a Pokémon can be reminded of. The rule, and nothing else: no save, no screen.
/// </summary>
/// <remarks>
/// <para>
/// Modelled on the reminder of Pokémon Añil in its randomlocke format, which the player picked as the
/// reference, and on the game's own. Three sources:
/// </para>
/// <list type="number">
/// <item>The moves it <b>knew when it was obtained</b> — Añil's «iniciales», always on offer. Ultra Moon has
/// the same idea built in: every Pokémon carries four «relearn» slots (egg moves when it hatched, the special
/// moves of a gift), and those count too.</item>
/// <item>What its <b>current species</b> learns on evolving (level 0 in the game's tables).</item>
/// <item>What its <b>current species</b> learns by level, up to and including the level it is at.</item>
/// </list>
/// <para>
/// «Current species» is the whole point in a randomlocke. Every species has its own randomized learnset, so a
/// family does not share moves: once a Pokémon evolves, the list is the new species' list, and what it knew as
/// the previous one is only reachable through the first source. That is exactly Añil's rule, and it is why the
/// reminder cannot be a list per family.
/// </para>
/// <para>
/// Moves it already knows are left out, as the game does, and a move reachable by two roads appears once, by
/// the more specific: learnset before first-known, so the screen can say at what level.
/// </para>
/// </remarks>
public static class MoveReminder
{
    public static IReadOnlyList<RememberableMove> Options(IReadOnlyList<LevelUpMove> learnset, int level,
        IEnumerable<int> known, IEnumerable<int> firstKnown)
    {
        ArgumentNullException.ThrowIfNull(learnset);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(firstKnown);

        var skip = known.Where(move => move > 0).ToHashSet();
        var taken = new HashSet<int>();
        var fromLearnset = new List<RememberableMove>();

        // Los de evolución primero y después por nivel, que es como los enseña la ficha del juego.
        foreach (var entry in learnset.Where(e => e.IsOnEvolution))
        {
            if (entry.Move > 0 && !skip.Contains(entry.Move) && taken.Add(entry.Move))
            {
                fromLearnset.Add(new RememberableMove(entry.Move, RememberedFrom.Evolution));
            }
        }

        foreach (var entry in learnset.Where(e => !e.IsOnEvolution && e.Level <= level).OrderBy(e => e.Level))
        {
            if (entry.Move > 0 && !skip.Contains(entry.Move) && taken.Add(entry.Move))
            {
                fromLearnset.Add(new RememberableMove(entry.Move, RememberedFrom.Level, entry.Level));
            }
        }

        // Los de cuando lo conseguiste van delante, como en Añil, pero solo los que la lista de su especie no
        // explica ya: repetidos no dicen nada y el nivel es más útil.
        var first = firstKnown
            .Where(move => move > 0 && !skip.Contains(move) && taken.Add(move))
            .Select(move => new RememberableMove(move, RememberedFrom.FirstKnown))
            .ToList();

        return [.. first, .. fromLearnset];
    }

    /// <summary>
    /// Moves written down as a comma separated list of ids, as the events keep them. Anything that is not a
    /// positive number is dropped rather than guessed at.
    /// </summary>
    public static IReadOnlyList<int> ParseList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : [.. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.TryParse(part, out var move) ? move : 0)
                .Where(move => move > 0)];

    /// <summary>The same list written down, for an event.</summary>
    public static string FormatList(IEnumerable<int> moves) =>
        string.Join(",", moves.Where(move => move > 0));
}
