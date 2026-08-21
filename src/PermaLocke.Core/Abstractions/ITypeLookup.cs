namespace PermaLocke.Core.Abstractions;

/// <summary>
/// The one or two types of a species.
/// </summary>
/// <param name="Second">Same as <paramref name="First"/> when the species has a single type,
/// which is how the cartridge itself stores it.</param>
public sealed record TypePair(int First, string FirstName, int Second, string SecondName)
{
    public bool IsDual => Second != First;

    /// <summary>"Fuego · Volador", or just "Fuego" when there is only one.</summary>
    public string Label => IsDual ? $"{FirstName} · {SecondName}" : FirstName;
}

/// <summary>
/// Resolves the types of a species.
/// </summary>
/// <remarks>
/// A port, like the Pokédex and the item table. Types are not randomized in this run, so the
/// cartridge tables PKHeX ships are the right answer; a randomized ROM could supply its own later
/// without anything upstream noticing.
/// </remarks>
public interface ITypeLookup
{
    TypePair GetTypes(int species);

    /// <summary>Name of a type id, or a readable fallback when it is unknown.</summary>
    string GetName(int type);
}
