namespace PermaLocke.App.Views;

/// <summary>
/// The fuse of the level, as the sea has it (2026-10-10): a line of light let down from the buoy to the deepest creature, with a lure at the
/// end of it that flickers like a flame. What is left of the line, dotted and dim down to the floor, is the levels still to go, and when the
/// party is at the cap the lure rests on the floor. In a flat band the line runs along under the surface instead of down.
/// </summary>
public static class CapSounding
{
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, CapCreature[] creatures, CapAnim? anim, double t, double phase)
    {
        if (l.LineEnd <= l.LineAlong + 4) return;

        var horizontal = l.LineHorizontal;
        var length = l.LineEnd - l.LineAlong;

        // The deepest creature is the one of the highest level (the first of them): the lure hangs beside it.
        var best = -1;
        var level = -1;
        for (var i = 0; i < data.Party.Count && i < creatures.Length; i++)
        {
            var m = data.Party[i];
            if (m.Egg || m.Fallen || m.Level <= level) continue;
            level = m.Level;
            best = i;
        }

        var atCap = level >= data.Cap && level > 0;
        var orb = l.LineAlong + 8;
        if (best >= 0)
        {
            var (cx, cy) = creatures[best].Centre;
            orb = horizontal ? creatures[best].Slot.Sprite.Left : cy;
            _ = cx;
        }

        // At the cap the lure goes all the way down and rests on the floor.
        if (atCap) orb = l.LineEnd - 2;
        orb = Math.Clamp(orb, l.LineAlong + 6, l.LineEnd - 2);

        void Cell(int along, int across, uint colour, int alpha)
        {
            if (horizontal) p.Set(along, across, colour, alpha);
            else p.Set(across, along, colour, alpha);
        }

        // The dotted dim line, all the way.
        for (var a = l.LineAlong; a <= l.LineEnd; a++)
        {
            if (a % 3 != 0) Cell(a, l.LineAcross, pal.Glow, 60);
        }

        // The lit part: a thread with a shimmer running down it, and a few packets of light going to the lure.
        var shimmer = anim is null ? 0 : phase;
        for (var a = l.LineAlong; a <= orb; a++)
        {
            var alpha = (int)(165 + (55 * Math.Sin((shimmer * 3) - (a * 0.3))));
            Cell(a, l.LineAcross, pal.Glow, alpha);
            Cell(a, l.LineAcross - 1, pal.Glow, 36);
            Cell(a, l.LineAcross + 1, pal.Glow, 36);
        }

        var lit = Math.Max(1, orb - l.LineAlong);
        if (anim is not null)
        {
            for (var k = 0; k < 3; k++)
            {
                var at = l.LineAlong + (int)(((phase * 11) + (k * lit / 3.0)) % lit);
                for (var d = 0; d < 3; d++) Cell(at + d, l.LineAcross, 0xFFFFFFu, 220 - (d * 60));
            }
        }

        // What is left to the floor: ticks, one for each level still to go (eight at most).
        var togo = Math.Max(0, data.Cap - Math.Max(0, level));
        var span = l.LineEnd - orb;
        for (var k = 1; k <= Math.Min(8, togo); k++)
        {
            var at = orb + (int)(span * k / (Math.Min(8, togo) + 0.0));
            if (at > l.LineEnd - 1) continue;
            Cell(at, l.LineAcross - 2, pal.Glow, 90);
            Cell(at, l.LineAcross + 2, pal.Glow, 90);
            Cell(at, l.LineAcross - 1, pal.Glow, 90);
            Cell(at, l.LineAcross + 1, pal.Glow, 90);
        }

        PaintLure(p, l, pal, orb, atCap, anim is null ? 0 : t, horizontal);

        // How many levels to go, in light, at the floor end of the line (or at the end of it, in a flat band).
        var text = atCap ? "TOPE" : togo > 0 ? $"+{togo}" : string.Empty;
        if (text.Length > 0)
        {
            var width = CapGlyphs.Width(text, false);
            if (horizontal) CapGlyphs.Outlined(p, text, l.LineEnd - width, l.LineAcross + 5, atCap ? 0xFFD860u : pal.Glow, pal.Ink);
            else CapGlyphs.Outlined(p, text, Math.Max(1, l.LineAcross - (width / 2)), l.LineEnd + 5, atCap ? 0xFFD860u : pal.Glow, pal.Ink);
        }
    }

    /// <summary>The lure: a warm core, a halo of dither that breathes and a flame of two cells that flickers, and at the cap a ring that opens on the floor.</summary>
    private static void PaintLure(CapSheet p, CapWorldLayout l, CapSeaPalette pal, int along, bool atCap, double t, bool horizontal)
    {
        var x = horizontal ? along : l.LineAcross;
        var y = horizontal ? l.LineAcross : along;
        var beat = (int)Math.Floor(t * 12);
        var flick = CapArt.Hash(beat, along) % 3;
        var breath = 0.5 + (0.5 * Math.Sin(t * 3.4));

        for (var dy = -5; dy <= 5; dy++)
        {
            for (var dx = -5; dx <= 5; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 5.2 || d < 1.6 || ((dx + dy) & 1) != 0) continue;
                p.Set(x + dx, y + dy, atCap ? 0xFFE070u : 0xFFD890u, (int)((35 + (60 * breath)) * (1 - (d / 5.2))));
            }
        }

        p.Rect(x - 1, y - 1, 3, 3, 0xFFF4C0u);
        p.Set(x, y, 0xFFFFFFu);
        p.Set(x - 1, y - 1, 0xFFFFFFu);

        // The flame above it (or in front of it, in a flat band): a body and a tip, moving.
        var sway = (CapArt.Hash(beat, along + 3) % 3) - 1;
        if (horizontal)
        {
            p.Set(x + 2, y + sway, 0xFFB84Au);
            p.Set(x + 3, y + sway, 0xF08A3Au);
            if (flick == 0) p.Set(x + 4, y + sway, 0xE85A20u);
        }
        else
        {
            p.Set(x + sway, y - 2, 0xFFB84Au);
            p.Set(x + sway, y - 3, 0xF08A3Au);
            if (flick == 0) p.Set(x + sway, y - 4, 0xE85A20u);
        }

        if (atCap)
        {
            // The ring of the cap: it opens from the lure over a second and starts again.
            var ring = ((t * 1.1) % 1.0) * 14;
            for (var a = 0; a < 40; a++)
            {
                var angle = a * Math.PI / 20;
                p.Set(x + (int)Math.Round(Math.Cos(angle) * ring * 1.5), y + (int)Math.Round(Math.Sin(angle) * ring * 0.6), 0xFFE070u, (int)(160 * (1 - (ring / 14))));
            }
        }
    }
}
