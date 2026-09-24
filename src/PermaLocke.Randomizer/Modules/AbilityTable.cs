namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Which abilities a randomized species may be given.
/// </summary>
/// <remarks>
/// <para>
/// The range comes from <b>the game being randomized</b>, from its own list of ability names, and
/// not from pk3DS: <c>GameInfo.MaxAbilityID</c> is a constant for the plain cartridge, 233, and it
/// capped the expansion mod's world whatever the configuration said — the same trap as the species
/// ceiling stuck at 807. §136.
/// </para>
/// <para>
/// An id without a name is a hole, not an ability: the mod leaves 301-304 and 317-318 as «-». And
/// <see cref="RandomizerOptions.BannedAbilities"/> takes out the ones tied to a single Pokémon's
/// forms, whose code in the mod may expect that Pokémon.
/// </para>
/// <para>
/// With nothing banned and every name present the pool is exactly 1..max, and drawing an index into
/// it gives the very same ability the old <c>Next(1, max + 1)</c> did, draw for draw. That is what
/// keeps a world generated before this change identical when it is generated again.
/// </para>
/// </remarks>
public static class AbilityTable
{
    /// <param name="names">The game's ability names, indexed by id.</param>
    /// <param name="maxAbility">The ceiling already resolved against the configuration.</param>
    /// <param name="banned">Ids never handed out.</param>
    public static IReadOnlyList<int> Assignable(IReadOnlyList<string> names, int maxAbility,
        IReadOnlyCollection<int> banned)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(banned);

        return
        [
            .. Enumerable.Range(1, Math.Max(0, maxAbility))
                .Where(id => id < names.Count && HasName(names[id]) && !banned.Contains(id))
        ];
    }

    private static bool HasName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim() != "-";
}
