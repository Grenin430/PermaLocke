using PermaLocke.GameLink;

namespace PermaLocke.App.Views;

/// <summary>A box of screen pixels (or, inside a panel, of cells).</summary>
public readonly record struct CapRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool Intersects(CapRect other) =>
        Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

    public bool Contains(CapRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;
}

/// <summary>How the cap panel is composed for the room there is, from the richest to the poorest.</summary>
public enum CapMode
{
    /// <summary>Nothing fits: the panel stays away rather than cover the game.</summary>
    Hidden,

    /// <summary>The last resort: the buoy with the cap, six lights and the seabed.</summary>
    Compact,

    /// <summary>A narrow column: the same sea in fewer cells, one creature per line.</summary>
    Slim,

    /// <summary>The tall column of a wide band: the whole sea, from the surface to the floor.</summary>
    Column,

    /// <summary>The horizontal band over (or under) the screens, in the side by side layout: the surface, or the floor, laid on its side.</summary>
    Strip
}

/// <summary>Which of the two windows of the world a slot is: the sea with the party and the cap, or the deep that goes on in the other band.</summary>
public enum CapPanelKind
{
    Party,
    Deep
}

/// <summary>Where in the picture a slot's band is: the side it is on tells which edge faces the game.</summary>
public enum CapSide
{
    Right,
    Left,
    Above,
    Below
}

/// <summary>What the player chose, in the words the geometry needs.</summary>
/// <param name="Forced">A mode asked for from CONFIGURACIÓN or the hotkey: the richest one that fits, up to this. Null is automatic.</param>
/// <param name="MaxCell">The biggest cell, in screen pixels, the player lets it use (0 is no limit).</param>
/// <param name="Companion">Whether the other band takes the deep when there is room for it.</param>
public sealed record CapOptions(CapMode? Forced = null, int MaxCell = 0, bool Companion = true);

/// <summary>
/// One window of the cap: where it goes in screen pixels and how it is drawn. In a stacked layout the black band beside the bottom screen
/// is wider than the one beside the top (the bottom screen is narrower): the window then reaches <see cref="EarCols"/> cells further
/// towards the game from <see cref="NotchRows"/> down, and the part above that, which is over the top screen, is empty: the <b>notch</b>.
/// </summary>
/// <param name="Cell">Screen pixels per cell, a whole number: the plaque never blurs.</param>
/// <param name="EarCols">How many of <paramref name="Cols"/> are the ear, on the side that faces the game.</param>
/// <param name="NotchRows">How many rows from the top the ear is cut off for, because the top screen is beside them.</param>
public sealed record CapSlot(CapPanelKind Panel, CapMode Mode, CapRect Box, int Cell, int Cols, int Rows, CapSide Side = CapSide.Right,
    int EarCols = 0, int NotchRows = 0)
{
    /// <summary>The empty corner of the window, in cells: columns from <c>X0</c> up to <c>X1</c>, rows above <c>Y1</c>. Nothing is ever drawn there.</summary>
    public (int X0, int X1, int Y1) Notch =>
        EarCols <= 0 ? (0, 0, 0) : Side == CapSide.Right ? (0, EarCols, NotchRows) : (Cols - EarCols, Cols, NotchRows);

    /// <summary>The same corner in screen pixels.</summary>
    public CapRect NotchBox
    {
        get
        {
            var (x0, x1, y1) = Notch;
            return EarCols <= 0 ? default : new CapRect(Box.Left + (x0 * Cell), Box.Top, (x1 - x0) * Cell, y1 * Cell);
        }
    }
}

/// <summary>Where the cap goes over the emulator: the party's window and, when there is a second band, the deep's.</summary>
/// <param name="Content">The part of the picture the game itself draws, which no slot ever touches except in its notch.</param>
/// <param name="Bands">The black bands left over, in the order they were considered, with the margin already taken off.</param>
/// <param name="Screens">The 3DS screens themselves, a pixel bigger on every side: what no window may ever cover, ears included.</param>
public sealed record CapPlan(CapSlot? Main, CapSlot? Companion, CapRect Content, IReadOnlyList<CapRect> Bands, string Why,
    IReadOnlyList<CapRect>? Screens = null);

/// <summary>
/// The black band of the emulator and the modes of the cap panel that fit it (2026-10-10).
/// </summary>
/// <remarks>
/// <para>
/// Pure: from the box of the picture (<c>GameWindow.RenderBox</c>) and the layout of the screens
/// (<see cref="AzaharScreenLayout"/>) it computes where the 3DS image is drawn and what is left. Azahar scales the emulated
/// picture (400×480 for top over bottom) to fit the window and centres it, so the band is what the window has beyond it:
/// left and right in a wide window, over and under in a flat one.
/// </para>
/// <para>
/// Never touches the game: every slot is inside a band, kept <see cref="Safety"/> pixels from the picture and from the edge of
/// the window, except the ear beside the bottom screen, which is wider because that screen is, and whose notch above it is empty. A test
/// checks all of it for sizes from a postage stamp to a 4K screen. The layouts it does not compute (hybrid, custom, separate windows)
/// give <see cref="CapMode.Hidden"/>: better no panel than one on top of what has to be read.
/// </para>
/// </remarks>
public static class CapGeometry
{
    /// <summary>The design of each mode, in cells: the smallest grid it is drawn on, and the smallest cell it is worth drawing at.</summary>
    public static (int Cols, int Rows, int MinCell) Design(CapMode mode) => mode switch
    {
        CapMode.Column => (132, 250, 2),
        CapMode.Strip => (420, 46, 2),
        CapMode.Slim => (76, 210, 2),
        CapMode.Compact => (60, 80, 1),
        _ => (0, 0, 1)
    };

    /// <summary>The widest a column is drawn; a band wider than that keeps the rest as black margin.</summary>
    public const int MaxColumnCols = 200;

    /// <summary>The most cells an ear may add to a window.</summary>
    public const int MaxEarCols = 64;

    /// <summary>How far the room has to exceed a mode's design to climb back to it, so that a window being resized does not flicker between two.</summary>
    public const double Hysteresis = 1.06;

    /// <summary>Screen pixels kept clear between a slot and the picture, and between a slot and the edge of the window.</summary>
    public static int Safety(double dpiScale) => Math.Max(6, (int)Math.Round(6 * dpiScale));

    /// <summary>The size of the picture in 3DS pixels for a layout, or null when the layout is not one this computes.</summary>
    public static (int Width, int Height)? PictureOf(AzaharScreenLayout layout)
    {
        (int, int)? size = layout.Option switch
        {
            0 => (400, 480),
            1 => layout.Swapped ? (320, 240) : (400, 240),
            3 => (720, 240),
            _ => null
        };

        return size is { } s ? (layout.Upright ? (s.Item2, s.Item1) : s) : null;
    }

    /// <summary>The part of the render box the 3DS picture takes, in screen pixels: Azahar scales it to fit and centres it.</summary>
    public static CapRect ContentOf(CapRect picture, (int Width, int Height) size)
    {
        var scale = Math.Min(picture.Width / (double)size.Width, picture.Height / (double)size.Height);
        var width = size.Width * scale;
        var height = size.Height * scale;
        var left = picture.Left + ((picture.Width - width) / 2);
        var top = picture.Top + ((picture.Height - height) / 2);

        // Conservative: the picture is taken as a pixel bigger on every side, so a rounding never lets a slot touch it.
        var l = (int)Math.Floor(left) - 1;
        var t = (int)Math.Floor(top) - 1;
        var r = (int)Math.Ceiling(left + width) + 1;
        var b = (int)Math.Ceiling(top + height) + 1;
        return new CapRect(l, t, r - l, b - t);
    }

    /// <summary>The screens of a layout in screen pixels, each a pixel bigger on every side; a layout turned or with one screen is the whole picture.</summary>
    public static IReadOnlyList<CapRect> ScreensOf(CapRect picture, AzaharScreenLayout layout, CapRect content)
    {
        if (layout.Upright || layout.Option == 1) return [content];

        var size = PictureOf(layout)!.Value;
        var scale = Math.Min(picture.Width / (double)size.Width, picture.Height / (double)size.Height);
        var left = picture.Left + ((picture.Width - (size.Width * scale)) / 2);
        var top = picture.Top + ((picture.Height - (size.Height * scale)) / 2);

        CapRect Box(double x, double y, double w, double h)
        {
            var l = (int)Math.Floor(left + (x * scale)) - 1;
            var t = (int)Math.Floor(top + (y * scale)) - 1;
            var r = (int)Math.Ceiling(left + ((x + w) * scale)) + 1;
            var b = (int)Math.Ceiling(top + ((y + h) * scale)) + 1;
            return new CapRect(l, t, r - l, b - t);
        }

        return layout.Option == 0 ? [Box(0, 0, 400, 240), Box(40, 240, 320, 240)] : [Box(0, 0, 400, 240), Box(400, 0, 320, 240)];
    }

    /// <summary>Where the bottom screen is beside the top one, for the layouts that stack them: the edges of the wider band's ear.</summary>
    private readonly record struct Ear(double LeftEdge, double RightEdge, double TopRow);

    private static Ear? EarOf(CapRect picture, AzaharScreenLayout layout, int safety)
    {
        // Only top over bottom, unturned: the bottom screen is 320 wide under a 400 wide top screen, centred.
        if (layout.Option != 0 || layout.Upright) return null;

        var scale = Math.Min(picture.Width / 400.0, picture.Height / 480.0);
        var left = picture.Left + ((picture.Width - (400 * scale)) / 2);
        var top = picture.Top + ((picture.Height - (480 * scale)) / 2);

        // One pixel of the picture's own: the same care as the content's inflation.
        return new Ear(left + (40 * scale) - 1 - safety, left + (360 * scale) + 1 + safety, top + (240 * scale) + 1 + safety);
    }

    /// <summary>
    /// The plan for a picture: the black bands, the mode that fits the biggest one and the window of the party and, if there
    /// is a second band, of the deep.
    /// </summary>
    /// <param name="previousMain">The mode of the last plan, to climb back to a richer one only with room to spare.</param>
    public static CapPlan Plan(CapRect picture, AzaharScreenLayout layout, double dpiScale, CapOptions options,
        CapMode? previousMain = null)
    {
        if (PictureOf(layout) is not { } size || picture.Width <= 0 || picture.Height <= 0)
        {
            return new CapPlan(null, null, picture, [], "layout sin calcular: sin panel", []);
        }

        var content = ContentOf(picture, size);
        var safety = Safety(dpiScale);
        var ear = EarOf(picture, layout, safety);

        // The four strips around the picture, less the safety margin on the picture's side and on the window's.
        var left = new CapRect(picture.Left + safety, picture.Top + safety, content.Left - picture.Left - (2 * safety), picture.Height - (2 * safety));
        var right = new CapRect(content.Right + safety, picture.Top + safety, picture.Right - content.Right - (2 * safety), picture.Height - (2 * safety));
        var above = new CapRect(picture.Left + safety, picture.Top + safety, picture.Width - (2 * safety), content.Top - picture.Top - (2 * safety));
        var below = new CapRect(picture.Left + safety, content.Bottom + safety, picture.Width - (2 * safety), picture.Bottom - content.Bottom - (2 * safety));

        var vertical = (Math.Max(0, left.Width) * left.Height) + (Math.Max(0, right.Width) * right.Height);
        var horizontal = (Math.Max(0, above.Height) * above.Width) + (Math.Max(0, below.Height) * below.Width);
        var sideways = vertical >= horizontal;

        // The party goes to the right band (or the one over the screens); the deep takes the other.
        var (first, second) = sideways ? (right, left) : (above, below);
        var (firstSide, secondSide) = sideways ? (CapSide.Right, CapSide.Left) : (CapSide.Above, CapSide.Below);
        IReadOnlyList<CapRect> bands = [first, second];

        var main = Fit(CapPanelKind.Party, first, firstSide, ear, options, previousMain);
        if (main is null)
        {
            return new CapPlan(null, null, content, bands, "ninguna banda deja sitio", ScreensOf(picture, layout, content));
        }

        CapSlot? companion = null;
        if (options.Companion && main.Mode is CapMode.Column or CapMode.Slim or CapMode.Strip)
        {
            // The deep is drawn at the same cell as the party so the two read as one sea, and only if it fits as a column.
            companion = Fit(CapPanelKind.Deep, second, secondSide, ear, options with { Forced = main.Mode, MaxCell = main.Cell }, null);
            if (companion is not null && companion.Mode is CapMode.Compact or CapMode.Hidden) companion = null;
        }

        var ears = main.EarCols > 0 ? $" con oreja de {main.EarCols} celdas" : string.Empty;
        return new CapPlan(main, companion, content, bands, $"{main.Mode} a {main.Cell}x en {(sideways ? "banda lateral" : "banda horizontal")}{ears}",
            ScreensOf(picture, layout, content));
    }

    /// <summary>The richest mode that fits a band, or null when not even the compact one does.</summary>
    private static CapSlot? Fit(CapPanelKind panel, CapRect band, CapSide side, Ear? ear, CapOptions options, CapMode? previous)
    {
        if (band.Width <= 0 || band.Height <= 0) return null;

        var vertical = side is CapSide.Right or CapSide.Left;
        CapMode[] candidates = vertical
            ? [CapMode.Column, CapMode.Slim, CapMode.Compact]
            : [CapMode.Strip, CapMode.Compact];

        foreach (var mode in candidates)
        {
            if (options.Forced is { } forced && Rank(mode) > Rank(forced)) continue;

            var (cols, rows, minCell) = Design(mode);
            var margin = mode == previous ? 1.0 : Hysteresis;
            var cell = Math.Min((int)(band.Width / (cols * margin)), (int)(band.Height / (rows * margin)));
            if (options.MaxCell > 0) cell = Math.Min(cell, options.MaxCell);
            if (cell < minCell) continue;

            var gridCols = band.Width / cell;
            var gridRows = band.Height / cell;
            if (mode == CapMode.Column) gridCols = Math.Min(gridCols, MaxColumnCols);
            if (mode == CapMode.Compact) (gridCols, gridRows) = (Math.Min(gridCols, 84), Math.Min(gridRows, 96));
            if (mode == CapMode.Slim) gridCols = Math.Min(gridCols, 110);

            var width = gridCols * cell;
            var height = gridRows * cell;
            var left = band.Left + ((band.Width - width) / 2);
            var top = band.Top + ((band.Height - height) / 2);

            // The compact one hangs from the top of its band instead of floating in the middle.
            if (mode == CapMode.Compact) top = band.Top;

            // The ear: the band beside the bottom screen is wider, so below the top screen the window reaches further towards the game.
            var earCols = 0;
            var notchRows = 0;
            if (ear is { } e && mode is CapMode.Column or CapMode.Slim)
            {
                var spare = side == CapSide.Right ? left - e.RightEdge : e.LeftEdge - (left + width);
                earCols = Math.Clamp((int)Math.Floor(spare / cell), 0, MaxEarCols);
                notchRows = (int)Math.Ceiling((e.TopRow - top) / cell);
                if (earCols < 6 || notchRows < 20 || notchRows > gridRows - 40) (earCols, notchRows) = (0, 0);
            }

            if (earCols > 0)
            {
                if (side == CapSide.Right) left -= earCols * cell;
                width += earCols * cell;
                gridCols += earCols;
            }

            return new CapSlot(panel, mode, new CapRect(left, top, width, height), cell, gridCols, gridRows, side, earCols, notchRows);
        }

        return null;
    }

    private static int Rank(CapMode mode) => mode switch
    {
        CapMode.Strip => 4,
        CapMode.Column => 4,
        CapMode.Slim => 3,
        CapMode.Compact => 2,
        _ => 0
    };
}
