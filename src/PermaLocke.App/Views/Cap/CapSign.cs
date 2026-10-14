using System.Globalization;

namespace PermaLocke.App.Views;

/// <summary>
/// The sign of the level cap (2026-10-15), where the buoy used to say it: a board on a post planted in the sand at the left of the party's floor.
/// It reads CAP ACTUAL and, under it, LVL in small letters and the cap in big ones. When the party is at the cap the number glows, gold, with a
/// halo that breathes. It stays clear of the six pictures (it takes the room the sand has beside them) and is left out where there is none.
/// </summary>
public static class CapSign
{
    private static readonly uint[] Wood = [0x1E120Bu, 0x4A2E1Cu, 0x7A4D2Cu, 0xA06A3Eu];
    private const uint Ink = 0x1A0F08u;
    private const int Left = 3;

    private readonly record struct Fit(int Left, int Top, int Width, int Height, int Scale, int Ground, int PostX, int RowX, int NumberW);

    private static int Small(string text) => CapGlyphs.Width(text, false) - 1;

    private static Fit? Place(CapWorldLayout l, CapPlaqueData data, CapSeaContext ctx, IReadOnlyList<int> fossils)
    {
        if (l.Mode != CapMode.Column || l.Panel != CapPanelKind.Party || l.Creatures is not { Length: > 0 } || data.Cap <= 0) return null;

        var number = data.Cap.ToString(CultureInfo.InvariantCulture);
        var room = l.Creatures[0].Sprite.Left - 3 - Left;
        for (var scale = 2; scale >= 1; scale--)
        {
            var numberW = CapGlyphs.Width(number, true, scale);
            var rowW = Small("LVL") + 3 + numberW;
            var width = Math.Max(Small("CAP ACTUAL"), rowW) + 6;
            if (width > room) continue;

            var height = 14 + (7 * scale);
            var postX = Left + (width / 2);
            var ground = CapSea.Relief(l, ctx, postX, fossils);
            var top = ground - 6 - height;
            if (top <= l.SurfaceY + 4) continue;
            return new Fit(Left, top, width, height, scale, ground, postX, Left + ((width - rowW) / 2), numberW);
        }

        return null;
    }

    /// <summary>The columns the sign takes, a little round it, or null when it is not there: the coral keeps out of them.</summary>
    public static (int Left, int Right)? Extent(CapWorldLayout l, CapPlaqueData data, CapSeaContext ctx, IReadOnlyList<int> fossils) =>
        Place(l, data, ctx, fossils) is { } f ? (f.Left - 2, f.Left + f.Width + 2) : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, CapPlaqueData data, IReadOnlyList<int> fossils, double atCap, double phase)
    {
        if (Place(l, data, ctx, fossils) is not { } f) return;

        // A little under water: the wood leans towards the blue of the sea, the light does not.
        uint Wet(uint c) => CapSheet.Mix(c, pal.Water[5], 0.2);
        void Fill(int x, int y, int w, int h, uint c) => p.Rect(x, y, w, h, Wet(c));

        var boardBottom = f.Top + f.Height;

        // the post, lit on its left and shaded on its right, and the sand heaped round its foot
        for (var y = boardBottom; y <= f.Ground + 1; y++)
        {
            p.Set(f.PostX - 2, y, Wet(Wood[3]));
            p.Set(f.PostX - 1, y, Wet(Wood[2]));
            p.Set(f.PostX, y, Wet(Wood[2]));
            p.Set(f.PostX + 1, y, Wet(Wood[2]));
            p.Set(f.PostX + 2, y, Wet(Wood[1]));
        }

        var sand = p.At(f.PostX + 7, f.Ground + 2);
        for (var i = 0; i < 11; i++)
        {
            var rise = 1 + (int)Math.Round(Math.Sin(i / 10.0 * Math.PI) * 1.5);
            for (var j = 0; j < rise; j++) p.Set(f.PostX - 5 + i, f.Ground - j, j == rise - 1 ? CapSheet.Lighter(sand, 0.09) : sand);
        }

        // the board: dark edge, plank grooves, a lit top edge and a nail in each corner
        Fill(f.Left, f.Top, f.Width, f.Height, Wood[0]);
        Fill(f.Left + 1, f.Top + 1, f.Width - 2, f.Height - 2, Wood[2]);
        Fill(f.Left + 1, f.Top + 1, f.Width - 2, 1, Wood[3]);
        Fill(f.Left + 1, f.Top + 9, f.Width - 2, 1, Wood[1]);
        foreach (var (nx, ny) in new[] { (f.Left + 2, f.Top + 2), (f.Left + f.Width - 3, f.Top + 2), (f.Left + 2, boardBottom - 3), (f.Left + f.Width - 3, boardBottom - 3) })
        {
            p.Set(nx, ny, Wet(0xC8CCD4u));
        }

        // at the cap the board glows: a halo round it that breathes
        if (atCap > 0.02)
        {
            var breath = 0.6 + (0.4 * Math.Sin(phase * 1.6));
            for (var y = -4; y < f.Height + 4; y++)
            {
                for (var x = -4; x < f.Width + 4; x++)
                {
                    var dx = Math.Max(0, Math.Max(-x, x - (f.Width - 1)));
                    var dy = Math.Max(0, Math.Max(-y, y - (f.Height - 1)));
                    var d = Math.Sqrt((dx * dx) + (dy * dy));
                    if (d > 0 && d < 4.5) p.Set(f.Left + x, f.Top + y, 0xFFE070u, (int)(54 * atCap * breath * (1 - (d / 4.5))));
                }
            }
        }

        // the writing: the title in cream, LVL small beside the cap in big gold letters, both bottom-aligned
        var title = "CAP ACTUAL";
        CapGlyphs.Outlined(p, title, f.Left + ((f.Width - Small(title)) / 2), f.Top + 3, 0xF2E2B0u, Ink);
        var numberY = f.Top + 11;
        var number = data.Cap.ToString(CultureInfo.InvariantCulture);
        CapGlyphs.Outlined(p, "LVL", f.RowX, numberY + (7 * f.Scale) - 5, 0xE8D8A0u, Ink);
        CapGlyphs.Outlined(p, number, f.RowX + Small("LVL") + 3, numberY, 0xFFD860u, Ink, true, f.Scale);
    }
}
