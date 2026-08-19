namespace PermaLocke.Core.Abstractions;

/// <param name="Number">National Pokédex number.</param>
public sealed record SpeciesInfo(int Number, string Name);

/// <summary>
/// Resolves species numbers to names and back. A port so the domain never depends on where
/// the Pokédex comes from; today it is PKHeX.Core, later it could be the randomized ROM.
/// </summary>
public interface ISpeciesLookup
{
    /// <summary>Every species, ordered by Pokédex number, excluding the empty entry 0.</summary>
    IReadOnlyList<SpeciesInfo> All { get; }

    /// <summary>Returns the name, or a placeholder for an unknown number rather than throwing.</summary>
    string GetName(int species);

    /// <summary>Case and accent insensitive lookup by name.</summary>
    bool TryGetNumber(string name, out int species);
}
