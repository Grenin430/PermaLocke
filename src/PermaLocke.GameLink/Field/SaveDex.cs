using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// The species the Pokédex of the last save says were caught. Only reads.
/// </summary>
/// <remarks>
/// From the save and not from memory, because it is a question about the past: a species caught since the last
/// save is also in the run's own register, and the rule that asks this unites both. Caught and not seen — the
/// seen flag goes on the moment a wild Pokémon appears, so asking for it would call every wild Pokémon a
/// duplicate of itself.
/// </remarks>
public sealed class SaveDex(SavedGameCache saved) : IOwnedSpecies
{
    public Task<IReadOnlySet<int>?> CaughtAsync(CancellationToken ct = default) => Task.Run<IReadOnlySet<int>?>(() =>
    {
        if (saved.Load() is not { } game)
        {
            return null;
        }

        var caught = new HashSet<int>();

        for (ushort species = 1; species <= game.Save.MaxSpeciesID; species++)
        {
            if (game.Save.GetCaught(species))
            {
                caught.Add(species);
            }
        }

        return caught;
    }, ct);
}
