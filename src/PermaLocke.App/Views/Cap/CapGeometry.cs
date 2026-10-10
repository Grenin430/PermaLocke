using PermaLocke.GameLink;

namespace PermaLocke.App.Views;

/// <summary>A box of screen pixels.</summary>
public readonly record struct CapRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool Intersects(CapRect other) =>
        Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
}

/// <summary>How the cap panel is composed for the room there is, from the richest to the poorest.</summary>
public enum CapMode
{
    /// <summary>Nothing fits: the panel stays away rather than cover the game.</summary>
    Hidden,

    /// <summary>The last resort: the shield with the cap, the fuse and six little bars.</summary>
    Compact,

    /// <summary>A narrow column: the cap, the fuse and one line per Pokémon.</summary>
    Slim,

    /// <summary>The tall column of a wide band: the whole showcase, with a niche per Pokémon and the graveyard.</summary>
    Column,

    /// <summary>The horizontal band over and under the screens (side by side): the showcase laid on its side.</summary>
    Strip
}

/// <summary>Which of the two panels a window shows: the party, or the memorial of the fallen and of the run.</summary>
public enum CapPanelKind
{
    Party,
    Memorial
}

/// <summary>What the player chose, in the words the geometry needs.</summary>
/// <param name="Forced">A mode asked for from CONFIGURACIÓN or the hotkey: the richest one that fits, up to this. Null is automatic.</param>
/// <param name="MaxCell">The biggest cell, in screen pixels, the player lets it use (0 is no limit).</param>
/// <param name="Memorial">Whether the other band takes the memorial when there is room for it.</param>
public sealed record CapOptions(CapMode? Forced = null, int MaxCell = 0, bool Memorial = true);

/// <summary>One window of the cap: where it goes in screen pixels and how it is drawn.</summary>
/// <param name="Cell">Screen pixels per cell, a whole number: the plaque never blurs.</param>
public sealed record CapSlot(CapPanelKind Panel, CapMode Mode, CapRect Box, int Cell, int Cols, int Rows);

/// <summary>Where the cap goes over the emulator: the party's window and, when there is a second band, the memorial's.</summary>
/// <param name="Content">The part of the picture the game itself draws, which no slot ever touches.</param>
/// <param name="Bands">The black bands left over, in the order they were considered, with the margin already taken off.</param>
public sealed record CapPlan(CapSlot? Main, CapSlot? Memorial, CapRect Content, IReadOnlyList<CapRect> Bands, string Why);

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
/// the window, and a test checks it for sizes from a postage stamp to a 4K screen. The layouts it does not compute (hybrid,
/// custom, separate windows) give <see cref="CapMode.Hidden"/>: better no panel than one on top of what has to be read.
/// </para>
/// </remarks>
public static class CapGeometry
{
    /// <summary>The design of each mode, in cells: the smallest grid it is drawn on, and the smallest cell it is worth drawing at.</summary>
    public static (int Cols, int Rows, int MinCell) Design(CapMode mode) => mode switch
    {
        CapMode.Column => (132, 250, 2),
        CapMode.Strip => (420, 46, 2),
        CapMode.Slim => (76, 170, 2),
        CapMode.Compact => (60, 64, 1),
        _ => (0, 0, 1)
    };

    /// <summary>The widest a column is drawn; a band wider than that keeps the rest as black margin.</summary>
    public const int MaxColumnCols = 200;

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

    /// <summary>
    /// The plan for a picture: the black bands, the mode that fits the biggest one and the window of the party and, if there
    /// is a second band, of the memorial.
    /// </summary>
    /// <param name="previousMain">The mode of the last plan, to climb back to a richer one only with room to spare.</param>
    public static CapPlan Plan(CapRect picture, AzaharScreenLayout layout, double dpiScale, CapOptions options,
        CapMode? previousMain = null)
    {
        if (PictureOf(layout) is not { } size || picture.Width <= 0 || picture.Height <= 0)
        {
            return new CapPlan(null, null, picture, [], "layout sin calcular: sin panel");
        }

        var content = ContentOf(picture, size);
        var safety = Safety(dpiScale);

        // The four strips around the picture, less the safety margin on the picture's side and on the window's.
        var left = new CapRect(picture.Left + safety, picture.Top + safety, content.Left - picture.Left - (2 * safety), picture.Height - (2 * safety));
        var right = new CapRect(content.Right + safety, picture.Top + safety, picture.Right - content.Right - (2 * safety), picture.Height - (2 * safety));
        var above = new CapRect(picture.Left + safety, picture.Top + safety, picture.Width - (2 * safety), content.Top - picture.Top - (2 * safety));
        var below = new CapRect(picture.Left + safety, content.Bottom + safety, picture.Width - (2 * safety), picture.Bottom - content.Bottom - (2 * safety));

        var vertical = (Math.Max(0, left.Width) * left.Height) + (Math.Max(0, right.Width) * right.Height);
        var horizontal = (Math.Max(0, above.Height) * above.Width) + (Math.Max(0, below.Height) * below.Width);
        var sideways = vertical >= horizontal;

        // The party goes to the right band (or the one over the screens); the memorial takes the other.
        var (first, second) = sideways ? (right, left) : (above, below);
        IReadOnlyList<CapRect> bands = [first, second];

        var main = Fit(CapPanelKind.Party, first, sideways, options, previousMain);
        if (main is null)
        {
            return new CapPlan(null, null, content, bands, "ninguna banda deja sitio");
        }

        CapSlot? memorial = null;
        if (options.Memorial && main.Mode is CapMode.Column or CapMode.Slim or CapMode.Strip)
        {
            // The memorial is drawn at the same cell as the party so the two read as one piece, and only if it fits as a column.
            memorial = Fit(CapPanelKind.Memorial, second, sideways, options with { Forced = main.Mode, MaxCell = main.Cell }, null);
            if (memorial is not null && memorial.Mode is CapMode.Compact or CapMode.Hidden) memorial = null;
        }

        return new CapPlan(main, memorial, content, bands, $"{main.Mode} a {main.Cell}x en {(sideways ? "banda lateral" : "banda horizontal")}");
    }

    /// <summary>The richest mode that fits a band, or null when not even the compact one does.</summary>
    private static CapSlot? Fit(CapPanelKind panel, CapRect band, bool vertical, CapOptions options, CapMode? previous)
    {
        if (band.Width <= 0 || band.Height <= 0) return null;

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

            // The compact one is the old corner plaque: it hangs from the top of its band instead of floating in the middle.
            if (mode == CapMode.Compact) top = band.Top;
            return new CapSlot(panel, mode, new CapRect(left, top, width, height), cell, gridCols, gridRows);
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
