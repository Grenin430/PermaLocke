using PermaLocke.App.Views;

namespace PermaLocke.App.ViewModels;

/// <summary>Where each spread of the album starts: which binder, and its first page.</summary>
public readonly record struct AlbumSpreadPlace(int Binder, int Page);

/// <summary>
/// How the album is paged (§186): every binder starts on a new spread, and its pages come two by two.
/// </summary>
/// <remarks>
/// A binder is as many pages as its slots need at <paramref name="perPage"/> pockets a page, rounded up to an even
/// number so two boxes never share a spread. The pocket of a slot is its place in the box, gaps included: the album
/// keeps the order of the PC. Pure, so it is tested without a screen.
/// </remarks>
public static class AlbumPaging
{
    public static int PagesOf(int slots, int perPage)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(slots / (double)perPage));
        return pages + (pages % 2);
    }

    public static IReadOnlyList<AlbumSpreadPlace> Spreads(IReadOnlyList<int> slotsPerBinder, int perPage)
    {
        var spreads = new List<AlbumSpreadPlace>();
        for (var binder = 0; binder < slotsPerBinder.Count; binder++)
        {
            for (var page = 0; page < PagesOf(slotsPerBinder[binder], perPage); page += 2)
            {
                spreads.Add(new AlbumSpreadPlace(binder, page));
            }
        }

        return spreads;
    }

    /// <summary>The pockets of one page: the cards of its slots, null where the slot is empty or past the binder.</summary>
    public static TcgCard?[] Page(IReadOnlyList<TcgCard?> slots, int page, int perPage)
    {
        var pockets = new TcgCard?[perPage];
        for (var i = 0; i < perPage; i++)
        {
            var slot = (page * perPage) + i;
            pockets[i] = slot < slots.Count ? slots[slot] : null;
        }

        return pockets;
    }
}
