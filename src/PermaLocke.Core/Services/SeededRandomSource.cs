using System.Text;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Services;

/// <summary>
/// SplitMix64. Chosen because it is a handful of lines, has no hidden state, and is specified
/// exactly, so "same seed, same result" survives a change of runtime or machine.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private ulong _state;

    public SeededRandomSource(ulong seed)
    {
        Seed = seed;
        _state = seed;
    }

    public ulong Seed { get; }

    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        // Rejection sampling: taking a plain modulo would skew the low values.
        var limit = (ulong)maxExclusive;
        var bound = ulong.MaxValue - (ulong.MaxValue % limit);
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value >= bound);

        return (int)(value % limit);
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minInclusive, maxExclusive);
        return minInclusive + Next(maxExclusive - minInclusive);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => probability > 0 && NextDouble() < probability;

    public IRandomSource Derive(string salt) => new SeededRandomSource(Seed ^ HashSalt(salt));

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// FNV-1a over UTF-8. Deliberately not <see cref="string.GetHashCode()"/>, which is
    /// randomised per process and would make runs irreproducible.
    /// </summary>
    internal static ulong HashSalt(string salt)
    {
        var hash = 0xCBF29CE484222325UL;
        foreach (var b in Encoding.UTF8.GetBytes(salt))
        {
            hash ^= b;
            hash *= 0x100000001B3UL;
        }
        return hash;
    }
}
