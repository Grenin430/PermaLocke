namespace PermaLocke.App.Views;

/// <summary>One cell of a fossil: where it is in the window, the colour of the Pokémon's own sprite there, and the cold stone it is turned into.</summary>
public readonly record struct CapFossilCell(int X, int Y, uint Colour, uint Stone);

/// <summary>Where a fossil lies and what it says without a word: how big, how deep, how overgrown and how bright its soul is.</summary>
/// <param name="Index">The order of falling: 0 is the oldest.</param>
/// <param name="Scale">How many cells of the real icon make one cell of the fossil: 2 is the biggest, 4 the smallest.</param>
/// <param name="Sunk">How many rows of the sprite are under the sand: the oldest lie the deepest.</param>
/// <param name="Growth">0 to 4: how much kelp has grown round it, from the days it has lain there.</param>
/// <param name="Soul">0 to 1: the brightness of its soul, from the level it had when it fell.</param>
public sealed record CapFossilSpec(int Index, int X, int Left, int Top, int Width, int Height, int Ground, int Scale, int Sunk, int Growth, double Soul);

/// <summary>A fossil with its cells.</summary>
public sealed record CapFossil(CapFossilSpec Spec, CapFossilCell[] Cells, (int X, int Y)[] Rim);

/// <summary>
/// The fallen as fossils (2026-10-10): each is the Pokémon's own icon turned to cold stone and half buried in the floor, and nothing is written
/// about it. What was a name and a level is now how it lies. How big it is says how recent it is (the latest lies big in front of the pile, against the highest of
/// the fallen); how deep it is sunk says how long ago, in the order of falling; how much kelp has grown round it says how many days it has lain
/// there; how bright its soul is, the same level again. Pure: the same graves and the same sea give the same fossils.
/// </summary>
public static class CapFossils
{
    /// <summary>How big the icon is drawn for a fossil: the biggest the room allows, so that it reads at two pixels a cell and at one.</summary>
    public static int ScaleFor(CapWorldLayout l, int count, int rank)
    {
        // The party's floor is under the creatures and keeps small ones; the deep is all floor and gives them the room.
        if (l.Panel != CapPanelKind.Deep) return CapSea.FossilScale(l, count);
        if (l.Mode == CapMode.Strip) return rank == 0 ? 2 : rank <= 5 ? 3 : 4;

        // The newest lies big in front, the ones before it smaller and further back, the oldest are small: a pile of the story with the latest on top.
        return rank == 0 ? 1 : rank <= 5 ? 2 : rank <= 11 ? 3 : 4;
    }

    public static CapFossil[] Build(CapWorldLayout l, IReadOnlyList<CapGrave> graves, CapSeaContext ctx, int[] columns)
    {
        var result = new CapFossil[graves.Count];
        var highest = graves.Count == 0 ? 0 : graves.Max(g => g.Level);
        for (var i = 0; i < graves.Count; i++)
        {
            var grave = graves[i];
            var x = columns[i];
            var ground = CapSea.Relief(l, ctx, x, columns);

            // When the run did not keep the level, the fossil is of middle size and middle light.
            var share = grave.Level > 0 && highest > 0 ? grave.Level / (double)highest : 0.5;
            var rank = graves.Count - 1 - i;
            var scale = ScaleFor(l, graves.Count, rank);
            var pixels = ToastPixels.SpritePixels.From(grave.Sprite);
            var w = pixels is null ? 1 : Math.Max(1, pixels.Width / scale);
            var h = pixels is null ? 1 : Math.Max(1, pixels.Height / scale);
            var sunk = (int)Math.Round(h * (0.2 + (0.5 * (graves.Count <= 1 ? 0 : rank / (double)(graves.Count - 1)))));
            var left = x - (w / 2);
            var top = ground - (h - sunk);
            var spec = new CapFossilSpec(i, x, left, top, w, h, ground, scale, sunk, Math.Min(4, grave.AgeDays / 3), share);
            var cells = pixels is null ? [] : Cells(l, ctx, columns, pixels, spec);
            result[i] = new CapFossil(spec, cells, RimOf(cells));
        }

        return result;
    }

    /// <summary>The cells just outside a fossil: the pale rim it gets when the focus is on it, so that even a dark icon stands out from the water.</summary>
    private static (int X, int Y)[] RimOf(CapFossilCell[] cells)
    {
        var solid = cells.Select(c => (c.X, c.Y)).ToHashSet();
        var rim = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in solid)
        {
            foreach (var n in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (!solid.Contains(n)) rim.Add(n);
            }
        }

        return [.. rim];
    }

    private static CapFossilCell[] Cells(CapWorldLayout l, CapSeaContext ctx, int[] columns, ToastPixels.SpritePixels pixels, CapFossilSpec spec)
    {
        var cells = new List<CapFossilCell>(spec.Width * spec.Height);
        for (var sy = 0; sy < spec.Height; sy++)
        {
            for (var sx = 0; sx < spec.Width; sx++)
            {
                var (found, c) = Sample(pixels, sx * spec.Scale, sy * spec.Scale, spec.Scale);
                if (!found) continue;
                var cx = spec.Left + sx;
                var cy = spec.Top + sy;
                if (cy > CapSea.Relief(l, ctx, cx, columns) + 1) continue;

                // Stone: the colour's light turned into a cold grey-blue, with a lit edge on top.
                var luma = ((0.30 * c.R) + (0.59 * c.G) + (0.11 * c.B)) / 255.0;
                var stone = CapSheet.Mix(0x2A3A58u, 0xDCE6F6u, luma * 0.9);
                if (!Sample(pixels, sx * spec.Scale, (sy - 1) * spec.Scale, spec.Scale).Found) stone = CapSheet.Lighter(stone, 0.3);
                cells.Add(new CapFossilCell(cx, cy, CapSheet.Rgb(c.R, c.G, c.B), stone));
            }
        }

        return [.. cells];
    }

    private static (bool Found, System.Windows.Media.Color Colour) Sample(ToastPixels.SpritePixels pixels, int x, int y, int scale)
    {
        if (y < 0) return (false, default);

        // The first solid pixel of the block it stands for, like the half-scale icons of the rest of the app.
        for (var dy = 0; dy < scale; dy++)
        {
            for (var dx = 0; dx < scale; dx++)
            {
                if (pixels.Solid(x + dx, y + dy)) return (true, pixels.At(x + dx, y + dy));
            }
        }

        return (false, default);
    }
}
