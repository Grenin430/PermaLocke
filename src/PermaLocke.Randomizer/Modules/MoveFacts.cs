namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// What a level-up randomizer needs to know about one move.
/// </summary>
/// <param name="Id">Move number, as the game indexes them.</param>
/// <param name="Type">Its type, for the same-type bias.</param>
/// <param name="Power">Base power. Zero for status moves and for the fixed-damage ones.</param>
/// <param name="Accuracy">Hit chance. The game writes perfect accuracy as a value of its own.</param>
/// <param name="Hits">Most times it can hit in a turn, so a multi-hit move counts for what it does.</param>
/// <param name="Physical">True physical, false special, null for a status move.</param>
public sealed record MoveFacts(int Id, int Type, int Power, int Accuracy, int Hits, bool? Physical)
{
    /// <summary>
    /// Whether this is worth handing out as an attack, by the reference randomizer's rule.
    /// </summary>
    /// <remarks>
    /// Copied from Universal Pokémon Randomizer's <c>Move.isGoodDamaging</c>, numbers included:
    /// twice the floor on its own, or the floor with accuracy of ninety or better. The point is
    /// that a weak but reliable move counts and a strong but unreliable one does not, which is the
    /// judgement a player makes and not one worth reinventing.
    /// </remarks>
    public bool IsGoodDamaging(int floor, int perfectAccuracy) =>
        Power * Hits >= 2 * floor
        || (Power * Hits >= floor && (Accuracy >= 90 || Accuracy == perfectAccuracy));
}

/// <summary>
/// Turns the game's own move table into <see cref="MoveFacts"/>, without assuming its encoding.
/// </summary>
/// <remarks>
/// The category is a byte and nothing says which value means physical. Guessing it would put every
/// physical attacker on special moves and would never fail — the §45 mistake wearing a different
/// hat — so it is <b>anchored</b>: two moves whose class nobody disputes are looked up by name in
/// the cartridge's own text, and the mapping is read off them. If the two anchors disagree, or
/// either is missing, this throws instead of picking one.
/// </remarks>
public static class MoveCatalog
{
    /// <summary>A physical move and a special one, by the name the cartridge gives them.</summary>
    private const string PhysicalAnchor = "Placaje";
    private const string SpecialAnchor = "Ascuas";

    /// <param name="categories">Raw category byte of each move, indexed by move id.</param>
    /// <param name="names">The cartridge's move names, indexed by move id.</param>
    public static (int Physical, int Special) DetectCategories(
        IReadOnlyList<int> categories, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(names);

        var physical = Find(names, PhysicalAnchor);
        var special = Find(names, SpecialAnchor);

        if (physical >= categories.Count || special >= categories.Count)
        {
            throw new InvalidDataException(
                $"Las anclas «{PhysicalAnchor}» y «{SpecialAnchor}» caen fuera de la tabla de movimientos.");
        }

        var (first, second) = (categories[physical], categories[special]);

        if (first == second)
        {
            throw new InvalidDataException(
                $"«{PhysicalAnchor}» y «{SpecialAnchor}» declaran la misma categoría ({first}), así que "
                + "no se puede saber cuál es física y cuál especial. No se randomizan los aprendizajes.");
        }

        return (first, second);
    }

    /// <summary>The move that never misses, so the value the game writes for that can be read off it.</summary>
    private const string PerfectAnchor = "Rapidez";

    /// <summary>
    /// What accuracy this game writes for a move that cannot miss.
    /// </summary>
    /// <remarks>
    /// Read from Swift rather than written down, which is what the reference randomizer does too.
    /// A never-miss move is not stored as «100»; it carries a value of its own, and hard-coding
    /// which one would be a number nobody measured — and it would only show up as slightly wrong
    /// move choices, which is the kind of wrong that never gets noticed.
    /// </remarks>
    public static int DetectPerfectAccuracy(IReadOnlyList<int> accuracies, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(accuracies);
        ArgumentNullException.ThrowIfNull(names);

        var swift = Find(names, PerfectAnchor);

        if (swift >= accuracies.Count)
        {
            throw new InvalidDataException(
                $"«{PerfectAnchor}» cae fuera de la tabla de movimientos.");
        }

        return accuracies[swift];
    }

    private static int Find(IReadOnlyList<string> names, string wanted)
    {
        for (var id = 0; id < names.Count; id++)
        {
            if (string.Equals(names[id], wanted, StringComparison.Ordinal))
            {
                return id;
            }
        }

        throw new InvalidDataException(
            $"No está el movimiento «{wanted}» en la tabla del cartucho, así que no se puede anclar "
            + "la categoría. No se randomizan los aprendizajes.");
    }
}
