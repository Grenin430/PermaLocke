namespace PermaLocke.App.Views;

/// <summary>
/// The buoy on the surface (2026-10-10), where the level cap is written: a drum of teal metal half under the water, with a lit glass face and
/// the number of the cap in letters of light, a lantern on top that breathes, and under it the tag with the trial. The picture is made when the
/// cap changes; the bob, the ripples round the waterline and the pulse of the lantern are painted every frame.
/// </summary>
public sealed class CapBuoy
{
    private static readonly uint[] Metal = [0x1B3E4Eu, 0x2F6E80u, 0x4E9AAAu, 0x86D0D6u, 0xD8F8F4u];

    public CapSheet? Body { get; private set; }

    public CapSheet? Tag { get; private set; }

    /// <summary>The row of the buoy's picture the waterline is at: below it everything is under the water.</summary>
    public int WaterRow { get; private set; }

    /// <summary>The lantern's centre in the buoy's own cells.</summary>
    public (int X, int Y) Lantern { get; private set; }

    public static string KeyOf(CapPlaqueData d) => $"{d.Cap}|{d.Trial}|{d.Next}";

    public void Build(CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data)
    {
        BuildBody(l, pal, data);
        BuildTag(l, pal, data);
    }

    private void BuildBody(CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data)
    {
        var w = l.Buoy.Width;
        var h = l.Buoy.Height;
        if (w < 20 || h < 14) return;

        var s = new CapSheet(w, h);
        WaterRow = Math.Clamp(l.SurfaceY - l.Buoy.Top, 4, h - 2);
        Lantern = (w / 2, 4);

        // The drum: a barrel that widens towards the waterline, lit from the left, with two bands and rivets, and a stripe of warning
        // orange on the lower one so that it is seen from afar.
        var top = Math.Max(8, h / 5);
        var bottom = h - 6;
        var rows = bottom - top;
        for (var y = top; y < bottom; y++)
        {
            var grow = (y - top) / (double)Math.Max(1, rows - 1);
            var inset = (int)Math.Round((1 - grow) * Math.Max(3, w / 9));
            var round = y - top < 3 ? 3 - (y - top) : 0;
            for (var x = inset + round; x < w - inset - round; x++)
            {
                var across = (x - inset) / (double)Math.Max(1, w - (2 * inset) - 1);
                var shade = across < 0.10 ? 4 : across < 0.24 ? 3 : across < 0.55 ? 2 : across < 0.82 ? 1 : 0;
                var colour = Metal[shade];
                var band = y - top < 2 || (y >= bottom - 7 && y < bottom - 4);
                if (band) colour = CapSheet.Darker(colour, 0.22);
                if (y >= bottom - 7 && y < bottom - 4 && ((x + y) / 3) % 2 == 0) colour = shade > 2 ? 0xFFB060u : 0xE07A30u;
                s.Set(x, y, colour);
            }

            // The outline on both sides, so the drum keeps its shape against the water.
            s.Set(inset + round - 1, y, Metal[0]);
            s.Set(w - inset - round, y, Metal[0]);
        }

        s.HLine(5, top, w - 10, Metal[4]);
        for (var x = 8; x < w - 7; x += 7) s.Set(x, top + 1, Metal[4]);

        // The skirt under the drum: a ballast, darker, narrowing.
        for (var y = bottom; y < h; y++)
        {
            var inset = 5 + ((y - bottom) * 3);
            for (var x = inset; x < w - inset; x++) s.Set(x, y, CapSheet.Darker(Metal[x < w / 2 ? 2 : 1], 0.35 + (0.08 * (y - bottom))));
        }

        // The lantern: a glass dome on a short mast, with a bright core.
        var cx = w / 2;
        s.VLine(cx - 2, 3, top - 2, Metal[1]);
        s.VLine(cx + 2, 3, top - 2, Metal[1]);
        s.HLine(cx - 4, top - 1, 9, Metal[3]);
        for (var y = 0; y < 8; y++)
        {
            var half = y switch { 0 => 2, 1 => 4, 2 => 5, _ => 5 };
            for (var x = -half; x <= half; x++)
            {
                var core = Math.Abs(x) <= 2 && y is >= 2 and <= 6;
                var edge = Math.Abs(x) == half;
                s.Set(cx + x, y, core ? 0xFFF8D0u : edge ? Metal[3] : CapSheet.Mix(pal.Glow, 0x86C0C8u, 0.35));
            }
        }

        // The glass face: dark, with a bright rim and a bevel, and the cap on it in letters of light with a bloom.
        var face = new CapRect(l.BuoyFace.Left - l.Buoy.Left, l.BuoyFace.Top - l.Buoy.Top, l.BuoyFace.Width, l.BuoyFace.Height);
        if (face.Width < 8 || face.Height < 9) { Body = s; return; }

        s.Rect(face.Left - 2, face.Top - 2, face.Width + 4, face.Height + 4, Metal[0]);
        s.Rect(face.Left - 1, face.Top - 1, face.Width + 2, face.Height + 2, Metal[4]);
        s.VGrad(face.Left, face.Top, face.Width, face.Height, CapSheet.Mix(pal.Water[4], 0x000000u, 0.25), CapSheet.Mix(pal.Water[6], 0x000000u, 0.4));
        s.HLine(face.Left, face.Top, face.Width, CapSheet.Mix(pal.Water[3], 0x000000u, 0.2));
        s.HLine(face.Left, face.Bottom - 1, face.Width, CapSheet.Mix(pal.Glow, pal.Water[5], 0.6), 170);

        var text = data.Cap.ToString();
        var scale = 1;
        foreach (var candidate in new[] { 4, 3, 2 })
        {
            if (CapSheet.TextWidth(text, candidate) <= face.Width - 4 && 7 * candidate <= face.Height - 2) { scale = candidate; break; }
        }

        var tw = CapSheet.TextWidth(text, scale);
        var tx = face.Left + ((face.Width - tw) / 2);
        var ty = face.Top + ((face.Height - (7 * scale)) / 2);
        var numerals = new CapSheet(face.Width, face.Height);
        CapGlyphs.Outlined(numerals, text, tx - face.Left, ty - face.Top, pal.Glow, CapSheet.Mix(pal.Ink, pal.Water[5], 0.5), big: true, scale: scale);

        // The bloom of the numerals: a dither that falls off in three cells.
        for (var y = 0; y < face.Height; y++)
        {
            for (var x = 0; x < face.Width; x++)
            {
                if (numerals.Solid(x, y)) continue;
                var ring = 0;
                for (var d = 1; d <= 3 && ring == 0; d++)
                {
                    for (var dy = -d; dy <= d && ring == 0; dy++)
                    {
                        for (var dx = -d; dx <= d; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == d && numerals.Solid(x + dx, y + dy)) { ring = d; break; }
                        }
                    }
                }

                if (ring > 0 && (((x + y) & 1) == 0 || ring == 1)) s.Set(face.Left + x, face.Top + y, pal.Glow, ring == 1 ? 80 : ring == 2 ? 45 : 22);
            }
        }

        s.Blit(numerals, face.Left, face.Top);
        Body = s;
    }

    private void BuildTag(CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data)
    {
        var box = l.Tag;
        if (box.Width < 20 || box.Height < 7) { Tag = null; return; }

        var s = new CapSheet(box.Width, box.Height);
        var trial = data.Trial;
        var big = box.Height >= 9 && CapGlyphs.Width(trial, true) + 36 <= box.Width;
        if (!big && CapGlyphs.Width(trial, false) + 34 > box.Width) trial = CapGlyphs.ShortTrial(trial);

        var next = data.Next is { } n ? $"SIG {n}" : "ULTIMO";
        var tw = CapGlyphs.Width(trial, big);
        var nw = CapGlyphs.Width(next, false);
        var gap = 6;
        var total = tw + gap + nw;
        var x = Math.Max(0, (box.Width - total) / 2);
        var y = Math.Max(0, (box.Height - (big ? 7 : 5)) / 2);
        CapGlyphs.Outlined(s, trial, x, y, pal.Glow, pal.Ink, big);
        CapGlyphs.Outlined(s, next, x + tw + gap, y + (big ? 1 : 0), CapSheet.Mix(pal.Glow, pal.Water[3], 0.35), pal.Ink);
        Tag = s;
    }

    /// <summary>The buoy and its tag at their place, bobbing; the part under the water darker and rippled.</summary>
    public void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, double phase, double glow)
    {
        if (Body is null) return;

        var bob = (int)Math.Round(1.1 * Math.Sin(phase * 1.4) * Math.Min(2.0, pal.Agitation));
        p.Blit(Body, l.Buoy.Left, l.Buoy.Top + bob);

        // The lantern breathes: a halo of dither round it, bright and then not.
        var lx = l.Buoy.Left + Lantern.X;
        var ly = l.Buoy.Top + Lantern.Y + bob;
        var breath = 0.5 + (0.5 * Math.Sin(phase * 2.2));
        for (var dy = -5; dy <= 5; dy++)
        {
            for (var dx = -8; dx <= 8; dx++)
            {
                var d = Math.Sqrt((dx * dx / 2.2) + (dy * dy));
                if (d > 5.2 || (d < 2 && glow < 0.5) || ((dx + dy) & 1) != 0) continue;
                p.Set(lx + dx, ly + dy, 0xFFF0B0u, (int)((30 + (70 * breath * glow)) * (1 - (d / 5.2))));
            }
        }

        // Ripples round the waterline: a pale dither row that swells with the waves.
        var water = l.Buoy.Top + bob + WaterRow;
        for (var x = l.Buoy.Left - 2; x < l.Buoy.Right + 2; x++)
        {
            var swell = Math.Sin((x * 0.4) + (phase * 2.5));
            if (swell > 0.3) p.Set(x, water, pal.Shaft, 90);
            if (swell > 0.75) p.Set(x, water + 1, pal.Shaft, 55);
        }

        if (Tag is { } tag) p.Blit(tag, l.Tag.Left, l.Tag.Top + bob / 2);
    }
}
