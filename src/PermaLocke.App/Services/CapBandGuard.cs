using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>
/// Checks that the black band is where the layout says it is (2026-10-10). The layout comes from <c>qt-config.ini</c>, which Azahar
/// writes when it closes, and F10 (screen layout), F9 (swap) and F8 (rotate) change it live: for the rest of the session the file
/// would be a lie, and a panel placed by it would sit on the game.
/// </summary>
/// <remarks>
/// <para>
/// It looks at the screen, in the two thin strips of every slot's band that the panel never covers: the one next to the picture and
/// the one at the edge of the window. A black band, or one of any colour the player chose, is the same in both; a game that has
/// moved into the first is not. Three pixels wide, twice a second at most, a few thousand pixels: nothing.
/// </para>
/// <para>
/// It can only say «wrong» when it sees the game where the band should be, so a game that happens to be black there goes
/// unnoticed until it is not: the first frame of a loading screen. That is what the margin of
/// <see cref="CapGeometry.Safety"/> is for.
/// </para>
/// </remarks>
public static class CapBandGuard
{
    private const int Strip = 3;

    /// <summary>True when the bands look like bands, false when the game shows in one, null when it could not be told.</summary>
    public static bool? LooksRight(CapPlan plan, CapRect picture)
    {
        var judged = false;

        foreach (var slot in new[] { plan.Main, plan.Companion })
        {
            if (slot is null) continue;
            if (Strips(slot, plan, picture) is not { } strips) continue;

            var far = Read(strips.Far);
            if (far is null) continue;

            var (r, g, b, flat) = Average(far);
            if (!flat) continue;

            foreach (var strip in strips.Near)
            {
                var near = Read(strip);
                if (near is null) continue;

                judged = true;
                var bad = 0;
                for (var i = 0; i < near.Length; i += 4)
                {
                    var d = Math.Abs(near[i + 2] - r) + Math.Abs(near[i + 1] - g) + Math.Abs(near[i] - b);
                    if (d > 40) bad++;
                }

                if (bad * 100 > near.Length / 4) return false;
            }
        }

        return judged ? true : null;
    }

    /// <summary>
    /// The strips a slot's band is checked by: the ones next to the picture and the one at the outer edge of the window. All of them are in
    /// the margin the panel never covers; in a window with an ear that is the notch (above it, beside the top screen) and the gap between the
    /// ear and the bottom screen.
    /// </summary>
    public static (IReadOnlyList<CapRect> Near, CapRect Far)? Strips(CapSlot slot, CapPlan plan, CapRect picture)
    {
        var content = plan.Content;
        var ear = slot.EarCols > 0;
        var notchBottom = ear ? slot.NotchBox.Bottom : picture.Bottom - 4;
        var bottom = ear && plan.Screens is { Count: 2 } screens ? screens[1] : (CapRect?)null;

        switch (slot.Side)
        {
            case CapSide.Right:
            {
                var near = new List<CapRect> { new(content.Right + 2, picture.Top + 4, Strip, notchBottom - picture.Top - 4) };
                if (bottom is { } b) near.Add(new CapRect(b.Right + 2, notchBottom, Strip, picture.Bottom - 4 - notchBottom));
                return (near, new CapRect(picture.Right - Strip - 1, picture.Top + 4, Strip, picture.Height - 8));
            }

            case CapSide.Left:
            {
                var near = new List<CapRect> { new(content.Left - 2 - Strip, picture.Top + 4, Strip, notchBottom - picture.Top - 4) };
                if (bottom is { } b) near.Add(new CapRect(b.Left - 2 - Strip, notchBottom, Strip, picture.Bottom - 4 - notchBottom));
                return (near, new CapRect(picture.Left + 1, picture.Top + 4, Strip, picture.Height - 8));
            }

            case CapSide.Below:
                return ([new CapRect(picture.Left + 4, content.Bottom + 2, picture.Width - 8, Strip)],
                    new CapRect(picture.Left + 4, picture.Bottom - Strip - 1, picture.Width - 8, Strip));

            case CapSide.Above:
                return ([new CapRect(picture.Left + 4, content.Top - 2 - Strip, picture.Width - 8, Strip)],
                    new CapRect(picture.Left + 4, picture.Top + 1, picture.Width - 8, Strip));
        }

        return null;
    }

    private static byte[]? Read(CapRect box)
    {
        if (box.Width <= 0 || box.Height <= 0) return null;

        var image = OverlayWindows.Capture((box.Left, box.Top, box.Width, box.Height));
        if (image is null) return null;

        var converted = new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels;
    }

    private static (int R, int G, int B, bool Flat) Average(byte[] pixels)
    {
        long r = 0, g = 0, b = 0;
        var n = pixels.Length / 4;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            b += pixels[i];
            g += pixels[i + 1];
            r += pixels[i + 2];
        }

        if (n == 0) return (0, 0, 0, false);
        int ar = (int)(r / n), ag = (int)(g / n), ab = (int)(b / n);
        var flat = true;
        for (var i = 0; i < pixels.Length && flat; i += 4)
        {
            flat = Math.Abs(pixels[i + 2] - ar) + Math.Abs(pixels[i + 1] - ag) + Math.Abs(pixels[i] - ab) <= 24;
        }

        return (ar, ag, ab, flat);
    }
}
