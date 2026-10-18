namespace PermaLocke.Core.Domain;

/// <summary>
/// Abilities that hang the game with the gen 8-9 mod, never handed out whatever the configuration says (2026-10-17). The list in
/// <c>Data/randomizer.json</c> (<c>bannedAbilities</c>) is what the player may change; this one is not.
/// </summary>
public static class BrokenAbilities
{
    /// <summary>268 Lingering Aroma (Olor Persistente): passing itself on to a Pokémon that hit by contact froze the game, seen playing.</summary>
    public const int LingeringAroma = 268;

    public static readonly IReadOnlySet<int> All = new HashSet<int> { LingeringAroma };
}
