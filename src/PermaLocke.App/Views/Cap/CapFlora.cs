namespace PermaLocke.App.Views;

/// <summary>
/// The kelp that grows on the floor (2026-10-10): stalks that sway with the current, taller where the window has an ear (the sea is open there,
/// beside the bottom screen, and there is the room), each tipped with a point of light that breathes. They keep clear of the fossils.
/// </summary>
public static class CapFlora
{
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, int[] fossilColumns, double phase)
    {
        if (l.Mode == CapMode.Compact) return;

        var count = Math.Clamp(l.Cols / 11, 4, 28);
        var ear = (l.Notch.X0, l.Notch.X1);
        var darkest = CapSheet.Mix(pal.Water[4], 0x0E4A3Au, 0.6);
        var middle = CapSheet.Mix(pal.Water[3], 0x2E9A6Au, 0.5);
        var lightest = CapSheet.Mix(pal.Water[2], 0x78E0A0u, 0.55);

        for (var k = 0; k < count; k++)
        {
            var x = 4 + (CapArt.Hash(ctx.Seed + ctx.WorldX, k + 200) % Math.Max(1, l.Cols - 8));
            if (fossilColumns.Any(f => Math.Abs(f - x) < 13)) continue;

            var inEar = ear.X1 > ear.X0 && x >= ear.X0 && x < ear.X1;
            var room = inEar ? Math.Max(10, l.FloorTop - l.Notch.Y1 - 10) : 22;
            var height = 7 + (CapArt.Hash(ctx.Seed, k + 300) % Math.Max(4, Math.Min(room, inEar ? 40 : 22) - 7));
            var ground = CapSea.Relief(l, ctx, x, fossilColumns);
            var bend = 0.6 + ((CapArt.Hash(ctx.Seed, k + 400) % 5) * 0.2);

            var tipX = x;
            var tipY = ground;
            for (var j = 0; j < height; j++)
            {
                // The stalk leans with the current more the higher it is.
                var lean = Math.Sin((phase * 0.9 * pal.Agitation) + (k * 1.7) + (j * 0.18)) * bend * (j / (double)height) * 3.2;
                var sx = x + (int)Math.Round(lean);
                var sy = ground - j;
                var colour = j < height / 3 ? darkest : j < (2 * height) / 3 ? middle : lightest;
                p.Set(sx, sy, colour);
                if (j < height * 0.65) p.Set(sx + 1, sy, CapSheet.Darker(colour, 0.2));
                if (j % 5 == 3) p.Set(sx - 1, sy, colour, 200);
                tipX = sx;
                tipY = sy;
            }

            var glow = 0.5 + (0.5 * Math.Sin((phase * 1.7) + k));
            p.Set(tipX, tipY - 1, 0xC8FFE0, (int)(120 + (120 * glow)));
            p.Set(tipX + 1, tipY, pal.Glow, (int)(40 + (60 * glow)));
        }
    }
}
