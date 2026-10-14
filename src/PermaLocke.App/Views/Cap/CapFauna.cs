namespace PermaLocke.App.Views;

/// <summary>
/// What lives in the sea besides the party (2026-10-10), by the mood: the Pokémon that swim and fly past (<see cref="CapVisitors"/>, singleplayer
/// 2026-10-13, in place of the jellyfish, the whale and the manta), a shooting star at night, the aurora when the party is at the cap, the
/// Sharpedo that prowls in danger, the soul that rises from a fresh fossil. The school of fish in a V and the wave of light that crossed the
/// sea were taken out on 2026-10-15.
/// The rare ones are scheduled from the seed of the run and the time, nothing else: the same run sees them at the same moments, another run does not.
/// </summary>
public static class CapFauna
{
    /// <summary>One slot of a periodic event: whether it happens in this period, how far along it is (0 to 1), and a number to vary it with.</summary>
    public static (bool Active, double Progress, int Variant) EventAt(int seed, int kind, double t, double period, double duration, int chance)
    {
        var slot = (long)Math.Floor(t / period);
        var key = unchecked((int)slot);
        if (CapArt.Hash(seed + (kind * 977), key) % 100 >= chance) return (false, 0, 0);

        var start = CapArt.Hash(seed + (kind * 131), key + 7) % Math.Max(1, (int)(period - duration));
        var local = t - (slot * period) - start;
        return local >= 0 && local <= duration ? (true, local / duration, CapArt.Hash(seed, key + (kind * 53))) : (false, 0, 0);
    }

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, (CapMood Now, CapMood Before, double Blend) mood, CapSeaContext ctx, double t, double phase,
        IReadOnlyList<CapGrave> graves)
    {
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var seed = ctx.Seed;
        var hush = Math.Clamp(1 - Strength(mood, CapMood.Danger) - (0.7 * Strength(mood, CapMood.Mourning)), 0, 1);

        if (CapSeaPalette.Night(ctx.SkyHour) > 0.5 && EventAt(seed, 5, t, 50, 2.2, 80) is { Active: true } star) ShootingStar(p, l, ctx, star.Progress, star.Variant, world);

        var danger = Strength(mood, CapMood.Danger);
        if (danger > 0.05) CapVisitors.Prowl(p, l, pal, ctx, phase, danger);

        var cap = Strength(mood, CapMood.Cap);
        if (cap > 0.05) Aurora(p, l, ctx, phase, cap);

        Souls(p, l, ctx, graves, phase, Strength(mood, CapMood.Mourning));

        // The Pokémon that pass go last, in front of the souls.
        if (hush > 0.5) CapVisitors.Paint(p, l, pal, ctx, t, phase);
    }

    private static double Strength((CapMood Now, CapMood Before, double Blend) mood, CapMood which) =>
        (mood.Now == which ? mood.Blend : 0) + (mood.Before == which ? 1 - mood.Blend : 0);

    private static void ShootingStar(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double progress, int variant, int world)
    {
        if (l.SkyRows <= 2) return;
        var x0 = (variant % Math.Max(1, world)) - ctx.WorldX;
        var y0 = 1 + (variant % Math.Max(1, l.SkyRows - 8));
        var d = progress * 44;
        for (var k = 0; k < 12; k++)
        {
            var sx = x0 + (int)(d - k);
            var sy = y0 + (int)((d - k) * 0.35);
            p.Set(sx, sy, k == 0 ? 0xFFFFFFu : 0xC8D8FFu, 255 - (k * 20));
        }
    }

    // ---------------------------------------------------------------- the moods

    /// <summary>The aurora of a sea at its cap: ribbons of green and gold light that wave across the sky.</summary>
    private static void Aurora(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double phase, double strength)
    {
        if (l.SkyRows < 6) return;
        for (var x = 0; x < l.Cols; x++)
        {
            double wx = x + ctx.WorldX;
            var centre = (l.SkyRows * 0.45) + (Math.Sin((wx * 0.05) + (phase * 0.6)) * (l.SkyRows * 0.18));
            var width = 4 + (3 * Math.Sin((wx * 0.11) - (phase * 0.4)));
            for (var y = (int)(centre - width); y <= (int)(centre + width); y++)
            {
                if (y < 0 || y >= l.SurfaceY) continue;
                var edge = 1 - (Math.Abs(y - centre) / (width + 0.5));
                if (edge * 14 <= CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)]) continue;
                p.Set(x, y, Math.Sin((wx * 0.04) + (phase * 0.3)) > 0 ? 0x78F0B8u : 0xFFE890u, (int)(200 * strength * edge));
            }
        }
    }

    /// <summary>A soul that rises from each of the latest two fossils, pale, slowly: more of them while the sea mourns.</summary>
    private static void Souls(CapSheet p, CapWorldLayout l, CapSeaContext ctx, IReadOnlyList<CapGrave> graves, double phase, double mourning)
    {
        if (graves.Count == 0 || l.Mode == CapMode.Compact) return;

        var shown = Math.Min(graves.Count, 2 + (int)(mourning * 3));
        for (var n = 0; n < shown; n++)
        {
            var i = graves.Count - 1 - n;
            var x = CapSea.FossilColumn(l, i, ctx);
            var ground = CapSea.Relief(l, ctx, x);
            var rise = ((phase * 0.16) + (n * 0.37)) % 1.0;
            var y = ground - 10 - (int)(rise * 34);
            var drift = (int)Math.Round(Math.Sin((phase * 0.7) + n) * 3);
            var alpha = (int)(150 * Math.Sin(Math.PI * rise));
            if (alpha <= 8) continue;

            // A pale drop with a tail of four cells.
            p.Rect(x + drift - 1, y, 3, 3, 0xD8E4FFu, alpha);
            p.Set(x + drift, y - 1, 0xFFFFFFu, alpha);
            for (var d = 1; d <= 4; d++) p.Set(x + drift + (d & 1), y + 3 + d, 0xB8C8F0u, alpha / (d + 1));
        }
    }
}
