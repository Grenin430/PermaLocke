namespace PermaLocke.App.Views;

/// <summary>
/// The history of the run, in the deep window, without a single letter (2026-10-10): the strata of the floor are the trials cleared, a chain of
/// lanterns hangs one for each trial, and each Pokémon fallen is a fossil of its own icon half buried in the floor (<see cref="CapFossils"/>).
/// What a name and a level used to say is said by the light: a focus goes over the fossils, newest to oldest, and the one
/// it falls on gets back the colours of its icon and does something of its own (its soul goes up, its dust rises, a bubble leaves it). Everything
/// here is a function of the data and the time.
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

    private static double Ease(double k) { k = Math.Clamp(k, 0, 1); return k * k * (3 - (2 * k)); }

    // ---------------------------------------------------------------- the focus

    /// <summary>How long the focus stays on the newest fossil and on each of the others, in seconds.</summary>
    public const double NewestSeconds = 5.0, EachSeconds = 3.2;

    /// <summary>
    /// Which fossil the focus is on at a moment (counted from the oldest, 0), how far along it is in its time, and how lit it is, 0 to 1. The focus goes
    /// from the newest to the oldest and starts again.
    /// </summary>
    public static (int Index, double Local, double Length, double Lit) FocusAt(int count, double t)
    {
        if (count <= 0) return (-1, 0, 0, 0);
        var total = NewestSeconds + (EachSeconds * (count - 1));
        var at = ((t % total) + total) % total;
        var step = at < NewestSeconds ? 0 : 1 + (int)((at - NewestSeconds) / EachSeconds);
        var local = step == 0 ? at : (at - NewestSeconds) % EachSeconds;
        var length = step == 0 ? NewestSeconds : EachSeconds;
        return (count - 1 - step, local, length, Ease(Math.Min(local / 0.55, (length - local) / 0.55)));
    }

    /// <summary>The colours of the fossil the focus is on, a halo round it, and the small thing that fossil does.</summary>
    public static void Focus(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapFossil[] fossils, CapSeaContext ctx, double t)
    {
        if (l.Panel != CapPanelKind.Deep || fossils.Length == 0) return;

        var (index, local, length, lit) = FocusAt(fossils.Length, t);
        if (lit <= 0.01) return;
        var fossil = fossils[index];
        var spec = fossil.Spec;
        var night = CapSeaPalette.Night(ctx.SkyHour) > 0.5;
        var beam = night ? 0xCFE0FFu : 0xFFF0C8u;

        // No beam from the sun or the moon into the sea any more (2026-10-17, asked by the player): only the fossil lights up.

        // The fossil gets the colours of its icon back, with a halo of light round it.
        var (fx, fy) = (spec.X, spec.Top + (spec.Height / 2));
        p.Disc(fx, fy, (spec.Width / 2) + 5, beam, (int)(30 * lit));
        foreach (var (rx, ry) in fossil.Rim) p.Set(rx, ry, beam, (int)(170 * lit));
        foreach (var cell in fossil.Cells) p.Set(cell.X, cell.Y, CapSheet.Mix(cell.Colour, 0xFFFFFFu, 0.12), (int)(250 * lit));

        // What it does: its soul goes up, its dust rises, a bubble leaves it; each fossil always does the same, by its place in the story.
        switch (spec.Index % 3)
        {
            case 0:
                Soul(p, spec, local, lit);
                break;
            case 1:
                Dust(p, spec, local, length);
                break;
            default:
                Bubble(p, spec, local);
                break;
        }
    }

    /// <summary>A pale wisp that goes up from the fossil with a tail behind it, as bright as the soul is.</summary>
    private static void Soul(CapSheet p, CapFossilSpec spec, double local, double lit)
    {
        var rise = Math.Max(0, local - 0.35);
        for (var k = 0; k < 9; k++)
        {
            var y = spec.Top - 2 - (int)(rise * 9) - (k * 2);
            var x = spec.X + (int)Math.Round(Math.Sin((rise * 3) + (k * 0.45)) * 2.5);
            var alpha = (int)((230 - (k * 22)) * lit * (0.5 + (0.5 * spec.Soul)));
            p.Set(x, y, k == 0 ? 0xFFFFFFu : 0xDDE8FFu, alpha);
            if (k < 2) p.Set(x + 1, y, 0xB8C8F0u, alpha / 2);
        }
    }

    /// <summary>Sediment lifted round the base that goes up and settles again.</summary>
    private static void Dust(CapSheet p, CapFossilSpec spec, double local, double length)
    {
        var k = Math.Clamp((local - 0.3) / (length - 0.9), 0, 1);
        for (var d = 0; d < 22; d++)
        {
            var side = ((CapArt.Hash(spec.Index, d + 100) % 100) - 50) / 50.0;
            var rise = (CapArt.Hash(spec.Index, d + 200) % 100) / 100.0;
            var x = spec.X + (int)(side * (spec.Width / 2.0 + 2 + (k * 12)));
            var y = spec.Ground - (int)((rise * 11 * Math.Sin(Math.Min(1, k) * Math.PI)) + 1);
            if (((x + y) & 1) == 0) p.Set(x, y, 0xC8B890u, (int)(190 * (1 - k)));
        }
    }

    /// <summary>A bubble that wobbles up from the top of the fossil and pops.</summary>
    private static void Bubble(CapSheet p, CapFossilSpec spec, double local)
    {
        var rise = local - 0.4;
        if (rise < 0) return;
        var y = spec.Top - 3 - (int)(rise * 8);
        var x = spec.X + (int)Math.Round(Math.Sin(rise * 3) * 3);
        if (rise < 2.2)
        {
            foreach (var (dx, dy) in new[] { (-1, -2), (0, -2), (1, -2), (-2, -1), (2, -1), (-2, 0), (2, 0), (-2, 1), (2, 1), (-1, 2), (0, 2), (1, 2) })
            {
                p.Set(x + dx, y + dy, 0xDDEEFFu, 200);
            }

            p.Set(x - 1, y - 1, 0xFFFFFFu, 255);
        }
        else if (rise < 2.7)
        {
            var burst = (rise - 2.2) / 0.5;
            for (var s = 0; s < 6; s++)
            {
                var angle = s * Math.PI / 3;
                p.Set(x + (int)Math.Round(Math.Cos(angle) * (3 + (burst * 5))), y + (int)Math.Round(Math.Sin(angle) * (3 + (burst * 5))), 0xFFFFFFu, (int)(230 * (1 - burst)));
            }
        }
    }

    // ---------------------------------------------------------------- the souls

    /// <summary>
    /// A small light over each fossil for as long as the run lasts, the dimmer the lower the level it fell at: the water over the floor counts the
    /// fallen as a sky of faint stars counts nights. Painted every frame because they drift.
    /// </summary>
    public static void Souls(CapSheet p, CapWorldLayout l, CapFossil[] fossils, double phase)
    {
        if (l.Panel != CapPanelKind.Deep) return;
        foreach (var fossil in fossils)
        {
            var spec = fossil.Spec;
            var bob = (int)Math.Round(Math.Sin((phase * 0.9) + (spec.Index * 1.7)) * 1.5);
            var x = spec.X + (int)Math.Round(Math.Sin((phase * 0.5) + (spec.Index * 2.3)) * 2);
            var y = spec.Top - 9 - ((spec.Index % 3) * 4) + bob;
            var alpha = 40 + (int)(110 * spec.Soul);
            p.Set(x, y, 0xDDE8FFu, alpha);
            p.Set(x - 1, y, 0xB8C8F0u, alpha / 3);
            p.Set(x + 1, y, 0xB8C8F0u, alpha / 3);
            p.Set(x, y - 1, 0xB8C8F0u, alpha / 3);
            p.Set(x, y + 1, 0xB8C8F0u, alpha / 3);
        }
    }

    // ---------------------------------------------------------------- the lanterns

    /// <summary>
    /// One lantern for each trial, hung on a rope from the surface to the floor (along under it, in a flat band): lit gold the ones cleared, the next
    /// one breathing, the rest dark glass. No numbers: the count is the lights. Painted every frame because the light breathes.
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
