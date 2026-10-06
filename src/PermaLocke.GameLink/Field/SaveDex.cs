using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// The species the Pokédex says were caught: the one the running game holds when it can be read, the last save's
/// otherwise. Only reads.
/// </summary>
/// <remarks>
/// <para>
/// Caught and not seen — the seen flag goes on the moment a wild Pokémon appears, so asking for it would call every wild
/// Pokémon a duplicate of itself.
/// </para>
/// <para>
/// Until 2026-10-06 only the save's: a species caught or received since the last save was missing (a Zweilous from the
/// Totem Stickers, then a wild Zweilous that kept its Poké Balls, 2026-09-24). The live Pokédex (<see cref="LiveSave"/>)
/// is used only when it holds every species the file does: captures are never undone, so one that lost any is not the
/// game's Pokédex.
/// </para>
/// </remarks>
public sealed class SaveDex(SavedGameCache saved, LiveSave? live = null) : IOwnedSpecies
{
    public Task<IReadOnlySet<int>?> CaughtAsync(CancellationToken ct = default) => Task.Run<IReadOnlySet<int>?>(() =>
    {
        if (live?.Load(LiveSave.ZukanBlock) is { } both)
        {
            var file = Caught(both.File);
            var now = Caught(both.Live);

            return now.IsSupersetOf(file) ? now : file;
        }

        return saved.Load() is { } game ? Caught(game.Save) : null;
    }, ct);

    private static HashSet<int> Caught(PKHeX.Core.SAV7USUM save)
    {
        var caught = new HashSet<int>();

        for (ushort species = 1; species <= save.MaxSpeciesID; species++)
        {
            if (save.GetCaught(species))
            {
                caught.Add(species);
            }
        }

        return caught;
    }
}
