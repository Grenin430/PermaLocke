namespace PermaLocke.App.Views;

/// <summary>
/// The loss of the Locke (2026-10-30), after the last life has broken and its death has played out: the panel drains to grey and dark, a padlock
/// (the Locke itself) falls into the middle of the party's window with its shackle open, snaps shut with a flash, chains run in from the corners
/// and bind it, and «LOCKE PERDIDO» is written under it. The deep's window gets the grey and the chains. It stays like that.
/// </summary>
public static class CapLockLoss
{
    /// <summary>From the first frame to the padlock and the writing standing still: when the app may close the game.</summary>
    public const double Seconds = 5.0;

    /// <summary>The least wait before it starts; it also waits for the last death to end (CapWorldView), so the whole death is seen first.</summary>
    public const double DeathWait = 1.5;

    /// <summary>Set once the loss has been on screen to its end: the guard may close the game.</summary>
    public static volatile bool Finished;

    private const uint Ink = 0x1A0F08u, Gold = 0xC9A23Au, GoldLight = 0xF0D070u, GoldDark = 0x7A5E1Eu, Steel = 0xB8C0CCu, SteelDark = 0x6A7280u;

    public static void Paint(CapSheet p, CapWorldLayout l, double since)
    {
        if (since < 0) return;
        if (since >= Seconds + 1.5) Finished = true;

        Drain(p, CapDeath.Ease(CapDeath.Span(0, 1.6, since)));

        var cx = l.Main.Left + (l.Main.Width / 2);
        var cy = (l.Rows / 2) - 10;
        Chains(p, l, cx, cy + 7, CapDeath.Span(2.6, 3.8, since));
        if (l.Panel == CapPanelKind.Party) Lock(p, cx, cy, since);
    }

    /// <summary>Everything painted loses its colour and some of its light.</summary>
    private static void Drain(CapSheet p, double k)
    {
        if (k <= 0) return;
        var b = p.B;
        for (var i = 0; i < b.Length; i += 4)
        {
            if (b[i + 3] == 0) continue;
            var luma = (0.11 * b[i]) + (0.59 * b[i + 1]) + (0.3 * b[i + 2]);
            var level = luma * (1 - (0.55 * k));
            for (var c = 0; c < 3; c++) b[i + c] = (byte)((b[i + c] * (1 - k)) + (level * k));
        }
    }

    private static void Chains(CapSheet p, CapWorldLayout l, int tx, int ty, double progress)
    {
        if (progress <= 0) return;
        foreach (var (sx, sy) in new[] { (l.Main.Left, 0), (l.Main.Left + l.Main.Width - 1, 0), (l.Main.Left, l.Rows - 1), (l.Main.Left + l.Main.Width - 1, l.Rows - 1) })
        {
            var dx = tx - sx;
            var dy = ty - sy;
            var links = (int)(Math.Sqrt((dx * dx) + (dy * dy)) / 4);
            var shown = (int)(links * progress);
            for (var i = 0; i < shown; i++)
            {
                var x = sx + (int)Math.Round(dx * (i + 0.5) / links);
                var y = sy + (int)Math.Round(dy * (i + 0.5) / links);
                var wide = i % 2 == 0;
                var (w, h) = wide ? (5, 3) : (3, 5);
                p.Rect(x - (w / 2), y - (h / 2), w, h, SteelDark);
                p.Rect(x - (w / 2) + 1, y - (h / 2) + 1, w - 2, h - 2, 0x2A2E38u);
                p.Set(x - (w / 2), y - (h / 2), Steel);
            }
        }
    }

    private static void Lock(CapSheet p, int cx, int cy, double since)
    {
        // falls from above between 1.0 and 1.9 s, bounces a little, and the shackle shuts at 2.3 to 2.5 s
        var fall = CapDeath.Ease(CapDeath.Span(1.0, 1.9, since));
        var bounce = -3 * Math.Sin(Math.PI * CapDeath.Span(1.9, 2.2, since));
        var shake = since is >= 2.5 and < 2.7 ? (int)Math.Round(Math.Sin(since * 90) * 1.5) : 0;
        var by = (int)Math.Round(((-30 * (1 - fall)) + (cy * fall)) + bounce);
        var x = cx + shake;
        var open = 1 - CapDeath.Ease(CapDeath.Span(2.3, 2.5, since));
        if (since < 1.0) return;

        // the shackle: two legs and a bar; open, it is lifted and its right leg comes out of the body
        var lift = (int)Math.Round(4 * open);
        p.Rect(x - 8, by - 12 - lift, 16, 4, Ink);
        p.Rect(x - 7, by - 11 - lift, 14, 2, Steel);
        p.Rect(x - 8, by - 12 - lift, 4, 13 + lift, Ink);
        p.Rect(x - 7, by - 11 - lift, 2, 12 + lift, Steel);
        var rightBottom = open > 0.3 ? by - 6 : by + 1;
        p.Rect(x + 4, by - 12 - lift, 4, rightBottom - (by - 12 - lift), Ink);
        p.Rect(x + 5, by - 11 - lift, 2, rightBottom - (by - 11 - lift), SteelDark);

        // the body, lit on top and shaded below, with its keyhole
        p.Rect(x - 11, by, 22, 16, Ink);
        p.Rect(x - 10, by + 1, 20, 14, Gold);
        p.Rect(x - 10, by + 1, 20, 2, GoldLight);
        p.Rect(x - 10, by + 12, 20, 3, GoldDark);
        p.Rect(x + 8, by + 1, 2, 14, GoldDark);
        p.Disc(x, by + 7, 2.2, Ink);
        p.Rect(x - 1, by + 8, 2, 4, Ink);

        // the flash when it shuts
        if (since is >= 2.45 and < 2.8)
        {
            var k = 1 - ((since - 2.45) / 0.35);
            for (var r = 12; r < 18; r++)
            {
                for (var a = 0; a < 360; a += 6)
                {
                    var rad = a * Math.PI / 180;
                    p.Set(x + (int)Math.Round(Math.Cos(rad) * r * (2 - k)), by + 8 + (int)Math.Round(Math.Sin(rad) * r * (2 - k) * 0.7), 0xFFFFFFu, (int)(110 * k));
                }
            }
        }

        // the writing, from 3.2 s on
        var write = CapDeath.Span(3.2, 4.0, since);
        if (write <= 0) return;
        var alpha = (int)(255 * write);
        foreach (var (text, row, colour) in new[] { ("LOCKE", 22, 0xFFE0D0u), ("PERDIDO", 31, 0xE04040u) })
        {
            CapGlyphs.Outlined(p, text, x - ((CapGlyphs.Width(text, false) - 1) / 2), by + row, colour, Ink, false, 1, alpha);
        }
    }
}
