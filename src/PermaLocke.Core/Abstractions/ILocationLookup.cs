namespace PermaLocke.Core.Abstractions;

/// <summary>
/// Resolves the zone identifiers the game stores into names players recognise.
/// </summary>
/// <remarks>
/// A port, so the domain never depends on where the table comes from. Today it is PKHeX,
/// which ships the Ultra Sun and Ultra Moon location tables; a randomized ROM could supply
/// its own later.
/// </remarks>
public interface ILocationLookup
{
    /// <summary>Name of a met location, or a readable fallback when the id is unknown.</summary>
    string GetName(int locationId);
}
