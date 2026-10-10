namespace PermaLocke.App.Views;

/// <summary>
/// The history of the run, written into the sea (2026-10-10): the strata of the floor are the trials cleared, each fossil is a Pokémon that
/// fell, and above them a chain of lanterns, one for each trial of the run, lit as they are cleared. Nothing here is a list or a panel: it is
/// the floor and the water, with writing on them. The writing is still (painted into the scene); the lanterns' light moves.
/// </summary>
public static class CapDeep
{
    /// <summary>How the floor's strata are cut: how many, how many rows of sand over them and how thick each is. The same arithmetic as the floor's.</summary>
    public static (int Strata, int SandRows, int LayerRows) StrataOf(CapWorldLayout l, CapPlaqueData data)
    {
        var strata = Math.Clamp(data.Cleared, 0, 12);
        const int sandRows = 5;
        var layerRows = strata == 0 ? 0 : Math.Clamp((l.Rows - l.FloorTop - sandRows - 4) / Math.Max(1, strata), 2, 6);
        return (strata, sandRows, layerRows);
    }

    // ---------------------------------------------------------------- the writing

    /// <summary>The stills of the history: the numbers of the strata, the counters, and the names of the latest fossils with leaders up to them.</summary>
    public static void Writing(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, CapSeaContext ctx, int[] fossilColumns)
    {
        var graves = data.Graves ?? [];

        // Only the deep tells the story in writing; the party's floor shows the fossils and leaves the names to it.
        if (l.Panel != CapPanelKind.Deep) return;

        Counters(p, l, pal, data, graves.Count);
        if (l.Mode == CapMode.Strip) NamesInTheSediment(p, l, pal, graves, ctx, fossilColumns);
        else FossilNames(p, l, pal, graves, ctx, fossilColumns);
    }

    private static void Counters(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, int fallen)
    {
        if (l.Mode == CapMode.Strip) return;

        var x = l.Main.Left + 7;
        var y = l.SurfaceY + 8;
        var trials = data.Trials > 0 ? $"PRUEBAS {Math.Min(data.Cleared, data.Trials)}/{data.Trials}" : data.Trial;
        CapGlyphs.Outlined(p, trials, x, y, pal.Glow, pal.Ink, big: l.Main.Width >= 90);
        var cost = fallen == 0 ? "NINGUN CAIDO" : fallen == 1 ? "1 CAIDO" : $"{fallen} CAIDOS";
        CapGlyphs.Outlined(p, cost, x, y + 12, fallen == 0 ? pal.Glow : 0xFF8A78, pal.Ink, big: l.Main.Width >= 90);
    }

    /// <summary>
    /// In a flat band there is no water over the fossils to write in, so the name goes under each one, in the sediment: name and level on one line,
    /// for the latest ones the width allows.
    /// </summary>
    private static void NamesInTheSediment(CapSheet p, CapWorldLayout l, CapSeaPalette pal, IReadOnlyList<CapGrave> graves, CapSeaContext ctx, int[] columns)
    {
        var right = int.MinValue;
        for (var n = 0; n < graves.Count; n++)
        {
            var i = graves.Count - 1 - n;
            var x = columns[i];
            var text = graves[i].Name.ToUpperInvariant();
            if (text.Length > 9) text = text[..9];
            if (graves[i].Level > 0) text += $" {graves[i].Level}";

            var width = CapGlyphs.Width(text, false);
            var left = Math.Clamp(x - (width / 2), 2, l.Cols - width - 2);

            // Newest first and from the right: a name that would touch the one already written is left out.
            if (n > 0 && left + width + 3 > right) continue;
            var y = CapSea.Relief(l, ctx, x, columns) + 5;
            if (y + 5 >= l.Rows) continue;

            CapGlyphs.Outlined(p, text, left, y, pal.Glow, pal.Ink);
            right = left;
        }
    }

    private static void FossilNames(CapSheet p, CapWorldLayout l, CapSeaPalette pal, IReadOnlyList<CapGrave> graves, CapSeaContext ctx, int[] columns)
    {
        if (graves.Count == 0) return;

        // The deep names the latest ones; the party's floor names only the latest one: the rest of the story is in the other window.
        var deep = l.Panel == CapPanelKind.Deep;
        var named = deep ? Math.Min(graves.Count, 10) : 1;
        var tiers = deep ? 4 : 1;
        var placed = Enumerable.Range(0, tiers).Select(_ => new List<(int Left, int Right)>()).ToArray();

        // The chain of lanterns takes the left of a tall deep: the names keep to the right of it.
        var minX = deep && l.Mode != CapMode.Strip && l.Lanterns.Width > 0 ? l.Lanterns.Left + 30 : 2;
        var sparse = graves.Count <= 4 && deep && l.Mode == CapMode.Column;

        for (var n = 0; n < named; n++)
        {
            var i = graves.Count - 1 - n;
            var grave = graves[i];
            var x = columns[i];
            var ground = CapSea.Relief(l, ctx, x, columns);

            var name = grave.Name.ToUpperInvariant();
            if (name.Length > 9) name = name[..9];
            var lines = new List<(string Text, uint Colour)> { (name, pal.Glow) };
            if (grave.Level > 0) lines.Add(($"NV{grave.Level}", CapSheet.Mix(pal.Glow, pal.Floor[1], 0.3)));
            if (sparse && grave.FellTo is { Length: > 0 } fell) lines.Add(("ANTE " + (fell.Length > 9 ? fell[..9] : fell).ToUpperInvariant(), CapSheet.Mix(pal.Glow, 0xFF8A78u, 0.5)));

            var width = lines.Max(line => CapGlyphs.Width(line.Text, false));
            var height = (lines.Count * 7) - 2;
            var left = Math.Clamp(x - (width / 2), minX, Math.Max(minX, l.Cols - width - 2));
            var right = left + width;

            // The lowest tier where it does not run into another name, or none (the fossil is still there, only unnamed).
            var tier = -1;
            for (var t = 0; t < tiers && tier < 0; t++)
            {
                if (!placed[t].Any(o => left < o.Right + 3 && right + 3 > o.Left)) tier = t;
            }

            if (tier < 0) continue;
            placed[tier].Add((left, right));

            var top = ground - 22 - height - (tier * 22);
            if (top < l.SurfaceY + 6) continue;

            // The leader: a dotted thread from the writing down to the fossil.
            var fossilTop = ground - (CapSea.FossilScale(l, graves.Count) == 1 ? 18 : 9);
            for (var y = top + height + 1; y < fossilTop; y++)
            {
                if (((y + x) & 1) == 0) p.Set(x, y, pal.Glow, 130);
            }

            for (var k = 0; k < lines.Count; k++) CapGlyphs.Outlined(p, lines[k].Text, left, top + (k * 7), lines[k].Colour, pal.Ink);
        }
    }

    // ---------------------------------------------------------------- the lanterns

    /// <summary>
    /// One lantern for each trial, hung on a rope from the surface to the floor (along under it, in a flat band): lit gold the ones cleared, the next
    /// one breathing, the rest dark glass. Painted every frame because the light breathes.
    /// </summary>
    public static void Lanterns(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapPlaqueData data, double phase)
    {
        if (l.Panel != CapPanelKind.Deep || data.Trials <= 0 || l.Lanterns.Width <= 0) return;

        var horizontal = l.Mode == CapMode.Strip;
        var count = Math.Min(12, data.Trials);
        var area = l.Lanterns;

        // The rope.
        int RopeX() => area.Left + 6;
        if (horizontal)
        {
            var y = area.Top + 2;
            for (var x = area.Left; x < area.Right; x++) p.Set(x, y, pal.Glow, 90);
        }
        else
        {
            var x = RopeX();
            for (var y = area.Top; y < area.Bottom; y++)
            {
                if (y % 3 != 0) p.Set(x, y, pal.Glow, 90);
            }
        }

        for (var k = 0; k < count; k++)
        {
            var lit = k < data.Cleared;
            var next = k == data.Cleared;
            var pulse = 0.5 + (0.5 * Math.Sin((phase * 2.2) + (k * 0.8)));

            int cx;
            int cy;
            if (horizontal)
            {
                cx = area.Left + 10 + (int)(k * (area.Width - 24) / (double)Math.Max(1, count - 1));
                cy = area.Top + 8;
            }
            else
            {
                cx = RopeX();
                cy = area.Top + 8 + (int)(k * (area.Height - 20) / (double)Math.Max(1, count - 1));
            }

            Lantern(p, cx, cy, lit, next, pulse, pal);

            var label = (k + 1).ToString();
            var lx = horizontal ? cx - (CapGlyphs.Width(label, false) / 2) : cx + 7;
            var ly = horizontal ? cy + 8 : cy - 2;
            CapGlyphs.Outlined(p, label, lx, ly, lit ? 0xFFE9A8u : next ? pal.Glow : CapSheet.Mix(pal.Glow, pal.Water[4], 0.55), pal.Ink);
        }
    }

    private static void Lantern(CapSheet p, int cx, int cy, bool lit, bool next, double pulse, CapSeaPalette pal)
    {
        // A glass lantern four cells wide and six tall, with a cap and a ring on top.
        var glass = lit ? 0xFFD870u : next ? CapSheet.Mix(0xFFD870u, 0x2A3A58u, 0.55 - (0.3 * pulse)) : 0x1C2A44u;
        var frame = lit ? 0x8A5A20u : 0x0E1828u;
        p.Rect(cx - 2, cy - 3, 5, 7, frame);
        p.Rect(cx - 1, cy - 2, 3, 5, glass);
        p.HLine(cx - 2, cy - 4, 5, lit ? 0xE8B040u : 0x2A3A58u);
        p.Set(cx, cy - 5, lit ? 0xE8B040u : 0x2A3A58u);
        if (!lit && !next) return;

        // Its light: a halo of dither, bigger and brighter when lit, and a pale core.
        var radius = lit ? 7 : 5;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > radius || d < 2.5 || ((dx + dy) & 1) != 0) continue;
                var alpha = (lit ? 70 + (40 * pulse) : 30 + (50 * pulse)) * (1 - (d / radius));
                p.Set(cx + dx, cy + dy, lit ? 0xFFE8A0u : pal.Glow, (int)alpha);
            }
        }

        if (lit) p.Set(cx, cy, 0xFFFBE0u);
    }
}
