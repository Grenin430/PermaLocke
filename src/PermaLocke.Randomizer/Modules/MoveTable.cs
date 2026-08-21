namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// The cartridge's move table, read for the one thing the randomizer needs from it: which moves
/// may be handed out and which must never be.
/// </summary>
public static class MoveTable
{
    /// <summary>
    /// The PP that marks a move as untouchable.
    /// </summary>
    /// <remarks>
    /// Every Z-move in the cartridge carries exactly one PP, and nothing else does except Struggle
    /// and Sketch — neither of which belongs in a learnset either, Struggle being what the game
    /// falls back to when a Pokémon has no moves left. So the rule needs no list of ids written
    /// down and kept in step with anything: it is read from the ROM being randomized.
    /// </remarks>
    public const int ForbiddenPp = 1;

    /// <summary>
    /// Every move a randomized learnset may hand out, in id order.
    /// </summary>
    /// <param name="pp">PP of each move, indexed by move id, as the cartridge stores them.</param>
    /// <param name="maxMove">Highest move id the game knows.</param>
    public static IReadOnlyList<int> Teachable(IReadOnlyList<int> pp, int maxMove) =>
    [
        .. Enumerable.Range(1, Math.Max(0, maxMove))
            .Where(id => id < pp.Count && pp[id] > ForbiddenPp)
    ];
}
