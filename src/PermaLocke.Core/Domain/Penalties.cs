namespace PermaLocke.Core.Domain;

/// <summary>
/// What losing costs.
/// </summary>
/// <remarks>
/// Numbers of the competition, so they live in <c>Data/penalties.json</c> and not in code.
/// </remarks>
/// <param name="PerDeath">Taken away for every Pokémon that falls.</param>
/// <param name="PerWipe">
/// Taken away when the whole party is down at once, <b>on top of</b> the deaths that made it up.
/// </param>
/// <param name="MaxWipes">
/// How many wipes still cost. Past this the wipe is recorded anyway, because it happened, but it
/// no longer takes anything: the history stays complete without the punishment being endless.
/// </param>
public sealed record PenaltyRules(int PerDeath, int PerWipe, int MaxWipes)
{
    /// <summary>The worst the wipes alone can cost, which is the number the rules quote.</summary>
    public int MaxWipeCost => PerWipe * MaxWipes;
}

/// <summary>The penalties a run plays with. A port, so the domain never learns where they live.</summary>
public interface IPenaltyCatalog
{
    PenaltyRules Rules { get; }
}

/// <param name="Points">What was actually taken, which is zero once the wipe cap is reached.</param>
/// <param name="Capped">True when the rule applied but no longer costs anything.</param>
public sealed record PenaltyResult(bool Applied, int Points, int NewBalance, bool Capped = false);
