namespace PermaLocke.App.Views;

/// <summary>Where one creature lives: its picture, and the room round it for what is known of it.</summary>
/// <param name="InfoOnRight">The text goes to the right of the picture (the picture is on the left of its slot), or to the left of it.</param>
public sealed record CapCreatureSlot(int Index, CapRect Sprite, CapRect Info, bool InfoOnRight, bool Half);

/// <summary>
/// Where each thing of the sea goes, in cells (2026-10-10): the same for the party's window and for the deep's, for a mode and a grid. Pure:
/// the drawing asks it where, and so do the tests, so that nothing is drawn in a place it was not given.
/// </summary>
/// <remarks>
/// <para>
/// The sea has one rule of space: the picture of the game is the window onto it, and the band beside it shows what is above and below.
/// In a tall band the surface is at the top and the floor at the bottom; the floor is the level cap, the surface is the sky of Alola at the
/// game's own hour. In a flat band (the side by side layout) the band over the screens is the surface and the one under them the floor.
/// </para>
/// <para>
/// A window with an ear (<see cref="CapSlot.EarCols"/>) is wider below the notch: the floor takes the whole width, the sea above it only the
/// full-height part, <see cref="Main"/>.
/// </para>
/// </remarks>
public sealed class CapWorldLayout
{
    public required CapMode Mode { get; init; }

    public required CapPanelKind Panel { get; init; }

    public required int Cols { get; init; }

    public required int Rows { get; init; }

    /// <summary>The empty corner of an L-shaped window: columns from X0 up to X1, rows above Y1.</summary>
    public (int X0, int X1, int Y1) Notch { get; init; }

    /// <summary>The full-height part of the window: all of it, or all but the ear.</summary>
    public CapRect Main { get; init; }

    /// <summary>Rows of sky above the water, and the row the surface waves are centred on.</summary>
    public int SkyRows { get; init; }

    public int SurfaceY { get; init; }

    /// <summary>The first row of the sea floor on average; the relief rises above it and the strata go below.</summary>
    public int FloorTop { get; init; }

    /// <summary>The whole seabed: from its highest relief to the bottom of the window, ear included.</summary>
    public CapRect Reef { get; init; }

    /// <summary>The buoy, with the lit face the cap is written on, and the tag with the trial under it.</summary>
    public CapRect Buoy { get; init; }

    public CapRect BuoyFace { get; init; }

    public CapRect Tag { get; init; }

    /// <summary>The sounding line, the fuse of the level: vertical from (<see cref="LineAlong"/> to <see cref="LineEnd"/>) at <see cref="LineAcross"/>, or horizontal.</summary>
    public bool LineHorizontal { get; init; }

    public int LineAcross { get; init; }

    public int LineAlong { get; init; }

    public int LineEnd { get; init; }

    public CapCreatureSlot[] Creatures { get; init; } = [];

    /// <summary>The deep only: the chain of lanterns, one per trial, and the cutaway of the floor with its strata.</summary>
    public CapRect Lanterns { get; init; }

    public CapRect Strata { get; init; }

    /// <summary>The deep only: where the sun or the moon is.</summary>
    public CapRect Sun { get; init; }

    /// <summary>The creatures are drawn at half size and with fewer lines of text: the room per creature is small.</summary>
    public bool Dense { get; init; }

    /// <summary>Whether a cell is in the empty notch.</summary>
    public bool InNotch(int x, int y) => y < Notch.Y1 && x >= Notch.X0 && x < Notch.X1;
}

/// <summary>Lays the sea out (<see cref="Arrange"/>).</summary>
public static class CapWorld
{
    public const int Creatures = 6;

    public static CapWorldLayout Arrange(CapMode mode, CapPanelKind panel, int cols, int rows, (int X0, int X1, int Y1) notch)
    {
        return mode switch
        {
            CapMode.Strip => ArrangeStrip(panel, cols, rows),
            CapMode.Compact => ArrangeCompact(cols, rows),
            _ => ArrangeColumn(mode, panel, cols, rows, notch)
        };
    }

    // ---------------------------------------------------------------- the tall band

    private static CapWorldLayout ArrangeColumn(CapMode mode, CapPanelKind panel, int cols, int rows, (int X0, int X1, int Y1) notch)
    {
        var slim = mode == CapMode.Slim;
        var dense = rows < 280;

        // The part of the window that is full height: the ear is on the side that faces the game.
        var earOnLeft = notch.X1 > 0 && notch.X0 == 0;
        var x0 = notch.X1 > notch.X0 ? (earOnLeft ? notch.X1 : 0) : 0;
        var x1 = notch.X1 > notch.X0 ? (earOnLeft ? cols : notch.X0) : cols;
        var main = new CapRect(x0, 0, x1 - x0, rows);

        // The buoy sets the horizon: it floats with its waterline on the surface, so both windows put the surface where the buoy's waterline is.
        var (buoyW, buoyH) = slim ? (dense ? (56, 32) : (64, 40)) : dense ? (72, 38) : (96, 48);
        var faceInset = slim ? 9 : 14;
        // Singleplayer (2026-10-13): the sea and the sand sit lower, so there is sky for the Pokémon that fly past. The buoy goes down with the
        // surface; the floor is thinner by as much, so the six creatures keep their room.
        var air = dense ? 16 : 28;
        var waterRow = buoyH - (dense ? 7 : slim ? 8 : 9) + air;
        var surface = 2 + waterRow;
        var skyRows = waterRow;
        // The party's floor is thinner (more room for the six creatures); the deep keeps the whole seabed.
        var floorH = (slim ? (dense ? 54 : 62) : dense ? 62 : panel == CapPanelKind.Deep ? 68 : 58) - air + (panel == CapPanelKind.Deep ? 6 : 0);
        var floorTop = rows - floorH;
        var reef = new CapRect(0, floorTop - 14, cols, floorH + 14);

        if (panel == CapPanelKind.Deep)
        {
            // The deep: the sky with the sun or the moon, open water with the chain of lanterns, and the cutaway of the floor.
            var sun = new CapRect(x0, 0, main.Width, surface);
            // No counters in the deep any more: the chain of lanterns starts right under the surface and uses the water on the left.
            var lanterns = new CapRect(x0 + 6, skyRows + 12, main.Width - 12, floorTop - skyRows - 24);
            var strata = new CapRect(0, floorTop + 8, cols, floorH - 8);
            return new CapWorldLayout
            {
                Mode = mode, Panel = panel, Cols = cols, Rows = rows, Notch = notch, Main = main, SkyRows = skyRows, SurfaceY = surface,
                FloorTop = floorTop, Reef = reef, Sun = sun, Lanterns = lanterns, Strata = strata, Dense = dense
            };
        }

        // The party: the buoy on the surface, the tag under it, the line down to the floor and the six creatures between them.
        var buoy = new CapRect(x0 + ((main.Width - buoyW) / 2) + (slim ? 0 : 6), 2 + air, buoyW, buoyH);
        var face = new CapRect(buoy.Left + faceInset, buoy.Top + 9, buoy.Width - (2 * faceInset), buoy.Height - (dense ? 16 : 18));
        var tag = new CapRect(x0 + 14, buoy.Bottom + 2, main.Width - 28, 12);

        var lineX = x0 + (slim ? 6 : 9);
        // No buoy nor tag are painted any more (singleplayer, 2026-10-13): the six creatures start just under the surface.
        var teamTop = surface + (dense ? 4 : 8);
        var teamBottom = floorTop - 6;
        var slotH = Math.Max(14, (teamBottom - teamTop) / Creatures);

        var spriteW = slim ? 24 : dense ? 24 : 40;
        var spriteH = slim || dense ? 16 : Math.Min(30, slotH - 1);
        var creatures = new CapCreatureSlot[Creatures];
        var infoLeft = x0 + (slim ? 14 : 24);
        for (var i = 0; i < Creatures; i++)
        {
            var top = teamTop + (i * slotH) + Math.Max(0, (slotH - spriteH) / 2);

            // One column in every mode, the picture always on the left and the words on its right: the eye starts every row in the same place.
            var sprite = new CapRect(infoLeft, top, spriteW, spriteH);
            var info = new CapRect(sprite.Right + 4, teamTop + (i * slotH), x1 - 6 - (sprite.Right + 4), slotH);
            creatures[i] = new CapCreatureSlot(i, sprite, info, true, slim || dense);
        }

        return new CapWorldLayout
        {
            Mode = mode, Panel = panel, Cols = cols, Rows = rows, Notch = notch, Main = main, SkyRows = skyRows, SurfaceY = surface,
            FloorTop = floorTop, Reef = reef, Buoy = buoy, BuoyFace = face, Tag = tag, LineAcross = lineX, LineAlong = buoy.Bottom - 2,
            LineEnd = floorTop - 2, Creatures = creatures, Dense = dense
        };
    }

    // ---------------------------------------------------------------- the flat band

    private static CapWorldLayout ArrangeStrip(CapPanelKind panel, int cols, int rows)
    {
        var main = new CapRect(0, 0, cols, rows);
        var skyRows = 11;
        var surface = 12;

        if (panel == CapPanelKind.Deep)
        {
            // Under the screens: the floor seen from the side, with the lantern chain running along the water over it.
            var floorTop = rows - Math.Min(30, (rows * 6) / 10);
            return new CapWorldLayout
            {
                Mode = CapMode.Strip, Panel = panel, Cols = cols, Rows = rows, Main = main, SkyRows = 0, SurfaceY = 0, FloorTop = floorTop,
                Reef = new CapRect(0, floorTop - 12, cols, rows - floorTop + 12), Lanterns = new CapRect(8, 2, cols - 16, Math.Max(10, floorTop - 8)),
                Strata = new CapRect(0, floorTop + 6, cols, rows - floorTop - 6), Sun = new CapRect(cols - 40, 0, 32, 20), Dense = true
            };
        }

        // Over the screens: the buoy at the left with the cap, the line along the surface, and the six creatures swimming right.
        var buoyH = Math.Min(rows - 4, 44);
        var buoy = new CapRect(6, 2, 52, buoyH);
        var face = new CapRect(buoy.Left + 5, buoy.Top + 9, buoy.Width - 10, buoy.Height - 16);
        var tag = new CapRect(buoy.Right + 4, 14, 74, rows - 18);
        var lineStart = tag.Right + 6;
        var lineY = surface + 7;
        var teamLeft = lineStart + 4;
        var slotW = (cols - teamLeft - 6) / Creatures;
        var spriteH = rows >= 50 ? 30 : 16;
        var spriteW = rows >= 50 ? 40 : 24;
        var creatures = new CapCreatureSlot[Creatures];
        for (var i = 0; i < Creatures; i++)
        {
            var left = teamLeft + (i * slotW);
            var sprite = new CapRect(left, rows - spriteH - 3, spriteW, spriteH);
            var info = new CapRect(left + spriteW + 3, lineY + 4, slotW - spriteW - 6, rows - lineY - 6);
            creatures[i] = new CapCreatureSlot(i, sprite, info, true, spriteH < 30);
        }

        return new CapWorldLayout
        {
            Mode = CapMode.Strip, Panel = panel, Cols = cols, Rows = rows, Main = main, SkyRows = skyRows, SurfaceY = surface,
            FloorTop = rows, Buoy = buoy, BuoyFace = face, Tag = tag, LineHorizontal = true, LineAcross = lineY, LineAlong = lineStart,
            LineEnd = cols - 8, Creatures = creatures, Dense = rows < 50
        };
    }

    // ---------------------------------------------------------------- the last resort

    private static CapWorldLayout ArrangeCompact(int cols, int rows)
    {
        var main = new CapRect(0, 0, cols, rows);
        var buoy = new CapRect((cols - 36) / 2, 1, 36, 24);
        var face = new CapRect(buoy.Left + 3, buoy.Top + 5, buoy.Width - 6, buoy.Height - 8);
        var floorTop = rows - 14;
        var lights = new CapCreatureSlot[Creatures];
        var step = (cols - 8) / (double)Creatures;
        for (var i = 0; i < Creatures; i++)
        {
            var x = 4 + (int)(i * step);
            lights[i] = new CapCreatureSlot(i, new CapRect(x, buoy.Bottom + 4 + ((i % 2) * 6), (int)step - 1, 8), default, true, true);
        }

        return new CapWorldLayout
        {
            Mode = CapMode.Compact, Panel = CapPanelKind.Party, Cols = cols, Rows = rows, Main = main, SkyRows = 3, SurfaceY = 4, FloorTop = floorTop,
            Reef = new CapRect(0, floorTop - 4, cols, rows - floorTop + 4), Buoy = buoy, BuoyFace = face, Tag = new CapRect(2, buoy.Bottom, cols - 4, 6),
            LineAcross = 3, LineAlong = buoy.Bottom, LineEnd = floorTop, Creatures = lights, Dense = true
        };
    }
}
