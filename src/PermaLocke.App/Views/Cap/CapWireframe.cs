namespace PermaLocke.App.Views;

/// <summary>The layout of the sea drawn as boxes (2026-10-10): what the lab and the tests look at to judge a composition before any art is in it.</summary>
public static class CapWireframe
{
    public static CapSheet Paint(CapWorldLayout l)
    {
        var p = new CapSheet(l.Cols, l.Rows) { NotchX0 = l.Notch.X0, NotchX1 = l.Notch.X1, NotchY1 = l.Notch.Y1 };
        p.VGrad(0, 0, l.Cols, l.Rows, 0x0A2A3A, 0x050A1A);
        p.Rect(0, 0, l.Cols, l.SkyRows, 0x3A5A7A);
        p.HLine(0, l.SurfaceY, l.Cols, 0x9AD8F0);
        if (l.FloorTop < l.Rows) p.Rect(0, l.FloorTop, l.Cols, l.Rows - l.FloorTop, 0x4A3A2A);
        Box(p, l.Reef, 0x8A6A3A);
        Box(p, l.Main, 0x2A6A4A);
        Box(p, l.Buoy, 0xF0D060);
        Box(p, l.BuoyFace, 0xFFFFFF);
        Box(p, l.Tag, 0xC0A0F0);
        Box(p, l.Lanterns, 0xF08030);
        Box(p, l.Strata, 0xB06030);
        Box(p, l.Sun, 0xFFE070);
        if (l.LineHorizontal) p.HLine(l.LineAlong, l.LineAcross, l.LineEnd - l.LineAlong, 0xFFF0A0);
        else if (l.LineEnd > l.LineAlong) p.VLine(l.LineAcross, l.LineAlong, l.LineEnd - l.LineAlong, 0xFFF0A0);

        foreach (var c in l.Creatures)
        {
            Box(p, c.Sprite, 0x40E0A0);
            Box(p, c.Info, 0xE06080);
        }

        return p;
    }

    private static void Box(CapSheet p, CapRect r, uint colour)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        p.HLine(r.Left, r.Top, r.Width, colour);
        p.HLine(r.Left, r.Bottom - 1, r.Width, colour);
        p.VLine(r.Left, r.Top, r.Height, colour);
        p.VLine(r.Right - 1, r.Top, r.Height, colour);
    }
}
