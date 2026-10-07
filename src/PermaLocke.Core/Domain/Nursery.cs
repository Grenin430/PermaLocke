namespace PermaLocke.Core.Domain;

/// <summary>
/// The NURSERY of the MONOTYPE roles (§221): the numbers it runs on, read from <c>Data/guarderia.json</c>.
/// </summary>
/// <remarks>
/// Spins are <b>owed</b> exactly like the LUDÓPATA wheel's: one per trial cleared, three for the league, two more for the
/// rematch, all of it read from the achievements and the history, so nothing is a counter that can drift. The egg's
/// strength grows with the trials cleared: the target total starts at the weakest species of the type and rises one
/// <see cref="Divisions"/>th of the way to the strongest per trial, never past <see cref="TopBaseStatTotal"/>.
/// </remarks>
public interface INurseryCatalog
{
    int SpinsPerTrial { get; }

    int SpinsForLeague { get; }

    int SpinsForRematch { get; }

    /// <summary>Achievement ids that owe a spin each, and that count as progress.</summary>
    IReadOnlyList<string> TrialAchievements { get; }

    string LeagueAchievement { get; }

    string RematchAchievement { get; }

    /// <summary>The highest base stat total an egg aims for, however far the run is.</summary>
    int TopBaseStatTotal { get; }

    /// <summary>In how many steps the target goes from the weakest species of the type to the strongest.</summary>
    int Divisions { get; }

    /// <summary>
    /// How many species have to fall in the band before an egg is drawn from it: the band opens one percent at a time
    /// around the target until this many do.
    /// </summary>
    int MinCandidates { get; }
}
