namespace PermaLocke.Core.Abstractions;

/// <summary>
/// Deterministic randomness. The same seed must yield the same sequence on every machine and
/// every build, because a run's randomization and its gacha rolls have to be reproducible and
/// auditable. That rules out <see cref="System.Random"/>, whose algorithm is not contractual.
/// </summary>
public interface IRandomSource
{
    /// <summary>The seed this source was built from. Stored in the events it takes part in.</summary>
    ulong Seed { get; }

    /// <summary>Uniform in [0, maxExclusive).</summary>
    int Next(int maxExclusive);

    /// <summary>Uniform in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>Uniform in [0, 1).</summary>
    double NextDouble();

    /// <summary>True with the given probability, expressed in [0, 1].</summary>
    bool Chance(double probability);

    /// <summary>
    /// A child source for one subsystem. Deriving by name is what lets a module be switched on
    /// or off without shifting the results of every other module.
    /// </summary>
    IRandomSource Derive(string salt);
}
