namespace PermaLocke.Core.Domain;

/// <summary>
/// The Pokévial (2026-10-16), as the Poké Vial of the Indigo Disk: a key item that heals the whole party anywhere, HP, status and PP, except the
/// fallen, and has a few uses that a Pokémon Centre fills again. PermaLocke puts it into the game (see <c>RulePatches.PokeVial</c>) and hands it
/// out (MISCELÁNEA).
/// </summary>
public static class PokeVial
{
    /// <summary>One of the expansion's unused item slots (its name «(?)» in the game text, its data empty).</summary>
    public const int ItemId = 120;

    public const string Name = "Pokévial";

    /// <summary>How many uses it holds at most for the trials cleared: one, a second after the fourth trial and a third after the sixth.</summary>
    public static int MaxUses(int clearedTrials) => 1 + (clearedTrials >= 4 ? 1 : 0) + (clearedTrials >= 6 ? 1 : 0);
}
