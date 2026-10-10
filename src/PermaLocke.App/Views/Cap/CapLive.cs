namespace PermaLocke.App.Views;

/// <summary>
/// The part of the cap panel that moves (2026-10-10): the bars filling, the flame of the fuse, the candle of the memorial. Painted
/// over a copy of the still part every frame, so a frame costs the copy and a few dozen cells.
/// </summary>
public static class CapLive
{
    public static void Paint(CapSheet p, CapLayout layout, CapPlaqueData data, CapAnim? anim, double t)
    {
        if (layout.Panel == CapPanelKind.Memorial)
        {
            if (layout.HasCup) Flame(p, (layout.Cols / 2) - 3 + 3, 3, t, 0.8, 5);
            var shelf = CapComposer.Shelf(layout);
            if (layout.Mode != CapMode.Strip)
            {
                foreach (var cx in CapComposer.CandleXs(layout)) Flame(p, cx, shelf.Top + 4, t + (cx * 0.13), 0.8, 5);
            }

            return;
        }

        Fuse(p, layout, data, t);
        Bars(p, layout, data, anim, t);
    }

    // ---------------------------------------------------------------- the fuse

    private static void Fuse(CapSheet p, CapLayout layout, CapPlaqueData data, double t)
    {
        if (data.Highest is not { } highest) return;

        var (x, y, span) = CapComposer.FuseTrack(layout);
        var compact = layout.Mode == CapMode.Compact;
        var atCap = highest >= data.Cap;
        var share = Math.Clamp(highest / (double)Math.Max(1, data.Cap), 0, 1);
        var tip = (int)Math.Round(span * share);
        var rope = 0xF2DDA8u;
        var ash = 0x7A5A38u;

        // The rope, two cells thick in the big modes, with its twist; the ash beyond the flame.
        var thick = compact ? 1 : 2;
        for (var i = 0; i < span; i++)
        {
            if (i < tip - 1)
            {
                var light = (i / 2) % 2 == 0;
                p.Set(x + i, y, light ? rope : CapSheet.Lighter(rope, 0.2));
                if (thick > 1) p.Set(x + i, y + 1, light ? CapSheet.Darker(rope, 0.18) : CapSheet.Darker(rope, 0.3));
            }
            else if (i > tip + 1 && i % 3 != 0)
            {
                p.Set(x + i, y, ash, 170);
            }
        }

        Flame(p, x + tip, y - 1, t, atCap ? 1.0 : 0.75, compact ? 4 : 7, atCap);

        if (!compact)
        {
            var chip = $"NV {highest}";
            var w = CapArt.ChipWidth(chip);
            // Over the rope, but not over the name of the fuse when there is no room above it.
            var minX = y - 13 < layout.Fuse.Top + 6 ? x + CapSheet.SmallWidth(CapComposer.FuseLabel(layout)) + 3 : x;
            var cx = Math.Clamp(x + tip - (w / 2), Math.Min(minX, x + span - w), x + span - w);
            CapArt.Chip(p, cx, y - 13, chip, atCap ? CapArt.Gold : CapArt.Engraved, atCap ? 0x4A2E00u : 0xF4D88Au, CapArt.Brown);
        }
    }

    /// <summary>
    /// A flame standing on (<paramref name="x"/>, <paramref name="y"/>) and growing up: a core, an orange body, and sparks. The shape
    /// flickers at twelve beats a second, from the time alone.
    /// </summary>
    public static void Flame(CapSheet p, int x, int y, double t, double strength, int height, bool bright = false)
    {
        var beat = (int)Math.Floor(t * 12);
        var flick = CapArt.Hash(beat, x) % 3;
        var h = Math.Max(3, (int)Math.Round(height * (0.8 + (0.1 * flick)) * strength));

        for (var i = 0; i < h; i++)
        {
            var row = y - i;
            var narrow = i > h * 0.55;
            var core = i < h * 0.45;
            var sway = i > 1 ? (CapArt.Hash(beat, i) % 3) - 1 : 0;
            if (!narrow)
            {
                p.Set(x - 1 + sway, row, 0xE85A20);
                p.Set(x + 1 + sway, row, 0xE85A20);
            }

            p.Set(x + sway, row, core ? (bright ? 0xFFF0B0u : 0xFFE070u) : 0xF08A3A);
            if (core && !narrow) p.Set(x + sway, row, bright ? 0xFFFFFFu : 0xFFE070u);
        }

        // Two sparks, riding up from the tip and fading.
        for (var s = 0; s < 2; s++)
        {
            var phase = ((t * 1.7) + (s * 0.5)) % 1.0;
            var sx = x + (int)Math.Round(Math.Sin((t * 3) + (s * 2.1)) * 2) + (s == 0 ? -1 : 1);
            var sy = y - h - (int)Math.Round(phase * 6);
            p.Set(sx, sy, 0xFFC060, (int)(220 * (1 - phase)));
        }
    }

    // ---------------------------------------------------------------- the bars

    private static void Bars(CapSheet p, CapLayout layout, CapPlaqueData data, CapAnim? anim, double t)
    {
        for (var i = 0; i < layout.Metrics.Length && i < data.Party.Count; i++)
        {
            var m = data.Party[i];
            var metrics = layout.Metrics[i];
            if (m.Egg || m.Fallen || metrics.HpBar.Width <= 0) continue;

            var share = anim?.HpFraction(i, t) ?? (m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0);
            var bar = metrics.HpBar;
            var inside = bar.Width - 2;
            var fill = share > 0 ? Math.Max(1, (int)(inside * share)) : 0;
            CapArt.BarFill(p, bar.Left, bar.Top, bar.Width, bar.Height, 0, fill, CapArt.HpColour(share), ticks: layout.Mode != CapMode.Compact);

            if (metrics.HasExp && m.Exp >= 0)
            {
                // Blue on the way; gold and blinking when the next battle may put it at the cap; a full dim gold once it is there.
                var e = metrics.Exp;
                var atCap = m.AtCap(data.Cap);
                var near = m.NearCap(data.Cap);
                var on = !near || ((int)Math.Floor(t * 3) & 1) == 0;
                var colour = atCap ? 0xA87828u : near ? (on ? CapArt.Gold : 0x6A4A18u) : 0x58B0D8u;
                p.Rect(e.Left, e.Top, atCap ? e.Width : Math.Max(0, (int)(e.Width * Math.Clamp(m.Exp, 0, 1))), e.Height, colour);
            }
        }
    }
}
