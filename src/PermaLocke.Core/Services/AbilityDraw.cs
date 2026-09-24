using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Services;

/// <summary>
/// The ability the gacha and the wonder trade hand out: any the game names, bar the banned ones.
/// </summary>
/// <remarks>
/// <para>
/// From every ability in the game, not the species' own: it is a gacha, and a Magikarp with
/// Levitate is part of the fun. The position in the list is the id the game stores, so the draw is
/// over the list as it stands and holes are thrown back rather than squeezed out, which would shift
/// every id after them.
/// </para>
/// <para>
/// Until §136 it also threw back everything above 233, on the belief that a gen 7 Pokémon keeps its
/// ability in one byte. It does not: the expansion mod keeps a ninth bit in 0x15 (§134), and the
/// builder writes it. What stays out now is only what cannot be dealt — ids with no name — and the
/// abilities tied to one Pokémon's forms. Throwing back instead of narrowing the draw keeps changes
/// to a minimum: a roll that already landed on a valid ability comes out the same.
/// </para>
/// <para>
/// Shared by both services because the loop used to live twice, and two copies of «which abilities
/// may be dealt» are how one of them ends up handing out what the other refuses.
/// </para>
/// </remarks>
public static class AbilityDraw
{
    /// <summary>Tries before giving up and dealing no ability, which the builder leaves at slot one.</summary>
    public const int Attempts = 12;

    /// <summary>The highest id a Pokémon can hold: one byte plus the mod's ninth bit.</summary>
    public const int MaxStorable = 0x1FF;

    /// <returns>An ability id, or 0 when every attempt landed on something that cannot be dealt.</returns>
    public static int Roll(IRandomSource source, IReadOnlyList<string> names, IReadOnlyCollection<int> banned)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(banned);

        for (var attempt = 0; attempt < Attempts && names.Count > 1; attempt++)
        {
            var candidate = source.Next(1, names.Count);

            if (IsDealable(names, candidate, banned))
            {
                return candidate;
            }
        }

        return 0;
    }

    public static bool IsDealable(IReadOnlyList<string> names, int id, IReadOnlyCollection<int> banned) =>
        id > 0 && id < names.Count && id <= MaxStorable && HasName(names[id]) && !banned.Contains(id);

    /// <summary>The mod marks its holes with «-»; older lists used «—».</summary>
    private static bool HasName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim() is not ("-" or "—");
}
