namespace PermaLocke.App.Views;

/// <summary>
/// What happens to one creature, in proportion to what happened (2026-10-11): a scratch is a number and a bubble; a blow shakes the picture, throws the
/// water and cracks the light; a small heal is a few crosses, a big one a column of light, a revive a shaft from the surface and a sweep of gold down
/// the body; each status leaves in its own way, with a clean flash, and enters the same way, converging on the creature; an item changing hands is a
/// glint where it is worn, and a move learned a star on its pip. And the quiet life of a creature when nothing happens: it sways, its light blinks, its
/// rhythm is the life it has left. All of it a function of the animation state and the time, with no allocation in a frame.
/// </summary>
public static class CapFxEvents
{
    private static double Ease(double k) { k = Math.Clamp(k, 0, 1); return k * k * (3 - (2 * k)); }

    private static readonly (int Dx, int Dy)[] Cross = [(0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)];

    private static readonly (int Dx, int Dy)[] Bubble = [(-1, -2), (0, -2), (1, -2), (-2, -1), (2, -1), (-2, 0), (2, 0), (-1, 1), (0, 1), (1, 1)];

    /// <summary>The size of a hit: a scratch, a blow, the worst, by the share of the maximum life it took.</summary>
    public static int Tier(double share) => share < 0.12 ? 1 : share < 0.40 ? 2 : 3;

    /// <summary>How big everything is drawn: the narrow modes, and the compact one, have little room and get it smaller.</summary>
    private static double Size(CapWorldLayout l) => l.Dense || l.Mode == CapMode.Compact ? 0.6 : 1.0;

    // ---------------------------------------------------------------- the pose

    /// <summary>The recoil and the washes of a hit, the strength of someone getting up, and the quiet life of the creature.</summary>
    public static void Pose(CapAnim anim, int i, double t, CapPlaqueMember m, double share, bool battle, ref int dx, ref uint? wash, ref int washAlpha,
        ref double strength, out double wave, out double waveSpeed, out double haloScale)
    {
        if (anim.SinceHit(i, t) is { } hit)
        {
            var tier = Tier(anim.HitShare(i));
            var length = tier == 1 ? 0.25 : tier == 2 ? 0.4 : 0.65;
            if (hit < length)
            {
                var k = hit / length;
                var amplitude = tier == 1 ? 1 : tier == 2 ? 3 : 5;
                dx = (int)Math.Round(amplitude * (1 - k) * Math.Cos(k * (tier == 3 ? 30 : 18)));
                var flash = tier == 1 ? 0.06 : tier == 2 ? 0.12 : 0.16;
                if (hit < flash) (wash, washAlpha) = (0xFFFFFFu, (int)(210 * (1 - (hit / flash))));
                else if (tier == 3) (wash, washAlpha) = (0xFF3020u, (int)(130 * (1 - k)));
                else if (tier == 2) (wash, washAlpha) = (0xFF7050u, (int)(70 * (1 - k)));
            }
        }

        if (anim.SinceRevive(i, t) is { } revive && revive < 1.8) strength = Math.Min(strength, 0.45 + (0.55 * Ease(revive / 1.8)));

        // The quiet life: a creature sways in the water with a rhythm that is its life: slower and smaller the less it has left, nervous when it is in danger.
        var alive = !m.Fallen && !m.Egg;
        var danger = battle && alive && share > 0 && share <= 0.25;
        wave = alive ? (0.45 + (0.45 * share)) * (danger ? 1.25 : 1) : 0;
        waveSpeed = alive ? (danger ? 3.4 : 1.1 * (0.45 + (0.55 * share))) : 0;

        // Its light blinks now and then, each creature on its own beat (a quick dip of a quarter of a second, every four to seven seconds).
        var period = 4.5 + ((CapArt.Hash(i, 91) % 30) / 10.0);
        var inPeriod = (t + (i * 1.3)) % period;
        haloScale = alive && inPeriod < 0.25 ? 1 - (0.75 * Math.Sin(inPeriod / 0.25 * Math.PI)) : 1;

        // In danger it trembles: a cell to either side, a few times a second.
        if (danger && dx == 0) dx = (CapArt.Hash((int)Math.Floor(t * 9), i) % 3) - 1;
    }

    /// <summary>How far a status has come in, 0 to 1: its particles are let in as this grows.</summary>
    public static double Intensity(CapAnim? anim, int i, double t) => anim?.SinceStatus(i, t) is { } since && since < 1.3 ? Ease((since - 0.35) / 0.85) : 1;

    // ---------------------------------------------------------------- the overlay

    public static void Overlay(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapCreature c, CapPlaqueMember m, CapAnim anim, int i, double t)
    {
        if (m.Egg) return;
        var (cx, cy) = c.Centre;
        var (w, h) = c.PictureSize;
        if (w == 0) return;
        var s = Size(l);

        if (anim.SinceHit(i, t) is { } hit) Hit(p, l, pal, anim, i, t, hit, cx, cy, w, h, s, m.MaxHp);

        if (anim.SinceHeal(i, t) is { } heal && heal < 2.8)
        {
            if (anim.SinceRevive(i, t) is { } revive) Revive(p, l, pal, c, anim, i, revive, cx, cy, w, h);
            else Heal(p, pal, anim, i, heal, cx, cy, w, h, s);
        }

        if (anim.SinceCure(i, t) is { } cure && cure.Since < 2.0) Cure(p, pal, c, cure.Status, cure.Since, i, cx, cy, w, h);
        if (m.Status != CapStatus.None && anim.SinceStatus(i, t) is { } entering && entering < 1.0) Entrance(p, c, m.Status, entering, cx, cy, s);
        if (anim.SinceItem(i, t) is { } item && item.Since < 1.6 && c.ItemAt.X > 0) Item(p, c.ItemAt, item.Since, item.Kind);
        if (anim.SinceMove(i, t) is { } move && move.Since < 1.6 && c.TickerAt.Letters > 0) Move(p, c.TickerAt, move.Since, move.Type);
    }

    private static void Ring(CapSheet p, int cx, int cy, double rx, double ry, uint colour, int alpha)
    {
        var steps = Math.Max(20, (int)(rx * 5));
        for (var a = 0; a < steps; a += 2)
        {
            var angle = a * 2 * Math.PI / steps;
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * rx), cy + (int)Math.Round(Math.Sin(angle) * ry), colour, alpha);
        }
    }

    /// <summary>The highest a number may float: under the head of the panel, so that the creature at the top never writes over the cap.</summary>
    private static int Ceiling(CapWorldLayout l) => l.Mode is CapMode.Column or CapMode.Slim ? l.SurfaceY + 1 : 0;

    // ---------------------------------------------------------------- hit

    private static void Hit(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapAnim anim, int i, double t, double hit, int cx, int cy, int w, int h, double s, int maxHp)
    {
        var tier = Tier(anim.HitShare(i));

        // The water takes the shock: one ring for a scratch, two for a blow, three and a burst for the worst.
        for (var r = 0; r < tier; r++)
        {
            var k = (hit - (r * 0.14)) / (tier == 1 ? 0.45 : 0.7);
            if (k is <= 0 or >= 1) continue;
            var reach = (tier == 1 ? 12 : tier == 2 ? 26 : 38) * k * s;
            Ring(p, cx, cy, 4 + reach, (4 + reach) * 0.55, r == 0 ? 0xFF8050u : 0xFFC090u, (int)(200 * (1 - k)));
        }

        // Bubbles thrown: more of them for a heavier blow.
        var bubbles = tier == 1 ? 3 : tier == 2 ? 7 : 14;
        var kb = Math.Clamp(hit / 1.1, 0, 1);
        for (var b = 0; b < bubbles; b++)
        {
            var angle = (CapArt.Hash(b, 11) % 628) / 100.0;
            var reach = kb * (10 + (CapArt.Hash(b, 12) % (tier * 9))) * s;
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * reach), cy + (int)Math.Round(Math.Sin(angle) * reach * 0.7) - (int)(kb * 9), pal.Snow, (int)(230 * (1 - kb)));
        }

        // The worst: the light cracks in lines that go out from the creature.
        if (tier == 3 && hit < 0.55)
        {
            var k = hit / 0.55;
            for (var line = 0; line < 6; line++)
            {
                var angle = (line * Math.PI / 3) + ((CapArt.Hash(line, 5) % 40) / 100.0);
                var length = (6 + (k * 22)) * (0.7 + ((CapArt.Hash(line, 6) % 40) / 100.0)) * s;
                var x0 = cx + (int)Math.Round(Math.Cos(angle) * (w / 2.0));
                var y0 = cy + (int)Math.Round(Math.Sin(angle) * (h / 2.0));
                p.Line(x0, y0, x0 + (int)Math.Round(Math.Cos(angle + 0.25) * length), y0 + (int)Math.Round(Math.Sin(angle + 0.25) * length * 0.8), 0xFFE0C0u, (int)(220 * (1 - k)));
            }
        }

        // The numbers, floating up, the latest hits one over the other: small for a scratch, big for a blow, double for the worst.
        Span<(double Since, int Amount)> hits = stackalloc (double, int)[CapAnim.HitMemory];
        var n = anim.Hits(i, t, hits);
        var ceiling = Ceiling(l);
        for (var k = 0; k < n; k++)
        {
            var (since, amount) = hits[k];
            if (amount <= 0 || since >= 1.5) continue;
            var share = maxHp > 0 ? amount / (double)maxHp : 0.15;
            var big = Tier(share) >= 2 && s >= 1;
            var scale = Tier(share) == 3 && s >= 1 ? 2 : 1;
            var text = "-" + amount;
            var rise = (int)(16 * s * Ease(Math.Min(1, since / 1.0)));
            var width = CapGlyphs.Width(text, big, scale);
            var y = Math.Max(cy - (h / 2) - 6 - rise - CapGlyphs.Height(big, scale) - (k * 9), ceiling);
            var colour = Tier(share) == 3 ? 0xFF6A50u : Tier(share) == 2 ? 0xFFA060u : 0xFFE0C0u;
            CapGlyphs.Outlined(p, text, cx - (width / 2), y, colour, pal.Ink, big, scale, (int)(255 * Math.Min(1, (1.5 - since) / 0.5)));
        }
    }

    // ---------------------------------------------------------------- heal

    private static void Heal(CapSheet p, CapSeaPalette pal, CapAnim anim, int i, double heal, int cx, int cy, int w, int h, double s)
    {
        var big = anim.HealShare(i) >= 0.25;
        var fade = Math.Clamp(1 - (heal / (big ? 1.8 : 1.1)), 0, 1);

        if (big && heal < 1.8)
        {
            // A column of pale light over the creature, from above, that fills out and goes.
            var grow = Math.Clamp(heal / 0.35, 0, 1);
            for (var y = cy - (h / 2) - 26; y < cy + (h / 2) + 4; y++)
            {
                for (var x = cx - (w / 2) - 3; x <= cx + (w / 2) + 3; x++)
                {
                    if (((x + y) & 1) != 0) continue;
                    var edge = Math.Abs(x - cx) / (w / 2.0 + 3);
                    var down = (y - (cy - (h / 2) - 26)) / (double)(h + 30);
                    p.Set(x, y, 0xB8FFD0u, (int)(70 * fade * grow * (1 - (edge * 0.7)) * (0.35 + down)));
                }
            }
        }

        for (var r = 0; r < (big ? 2 : 1); r++)
        {
            var k = Math.Clamp((heal - (r * 0.2)) / 0.9, 0, 1);
            if (k is <= 0 or >= 1) continue;
            var reach = (big ? 26 : 14) * (1 - k) * s;
            Ring(p, cx, cy, 4 + reach, (4 + reach) * 0.6, 0x9CF0A8u, (int)(190 * (1 - k)));
        }

        // Crosses of pale light rising: three for a little, nine for a lot.
        var crosses = big ? 9 : 3;
        for (var q = 0; q < crosses; q++)
        {
            var rise = ((heal * 0.9) + (q * 0.13)) % 1.0;
            var x = cx + ((CapArt.Hash(q, 21) % Math.Max(3, w)) - (w / 2));
            var y = cy + (h / 2) - (int)(rise * (h + 18));
            var alpha = (int)(230 * fade * (1 - rise));
            foreach (var (dx, dy) in Cross) p.Set(x + dx, y + dy, 0xC8FFD8u, alpha);
        }

        var amount = anim.HealAmount(i);
        if (amount > 0 && heal < 1.6 && s >= 1)
        {
            var text = "+" + amount;
            var scale = big ? 2 : 1;
            var width = CapGlyphs.Width(text, true, scale);
            var rise = (int)(14 * Ease(Math.Min(1, heal / 0.9)));
            CapGlyphs.Outlined(p, text, cx - (width / 2), cy - (h / 2) - 6 - rise - CapGlyphs.Height(true, scale), 0x8CFFB0u, pal.Ink, true, scale, (int)(255 * Math.Min(1, (1.6 - heal) / 0.5)));
        }
    }

    // ---------------------------------------------------------------- revive

    private static void Revive(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapCreature c, CapAnim anim, int i, double since, int cx, int cy, int w, int h)
    {
        // A shaft of light let down from the surface onto the creature: its head comes down fast, it stays, it goes. Solid and edged in dark, so that it
        // shows over the lit water of the day as well as over the deep.
        var top = l.SurfaceY;
        var reach = top + (int)((cy - top) * Ease(since / 0.55));
        var fade = since < 1.7 ? 1 : Math.Clamp(1 - ((since - 1.7) / 0.9), 0, 1);
        var half = (w / 2) + 5;
        for (var y = top; y <= reach; y++)
        {
            var spread = half + (int)Math.Round(Math.Sin((y * 0.3) + (since * 6)));
            for (var x = cx - spread; x <= cx + spread; x++)
            {
                var edge = Math.Abs(x - cx) / (double)spread;
                if (edge > 0.86) p.Set(x, y, pal.Ink, (int)(95 * fade));
                else p.Set(x, y, edge < 0.25 ? 0xFFFFFFu : 0xFFE27Au, (int)((edge < 0.25 ? 150 : 105) * fade * (1 - (edge * 0.5))));
            }
        }

        // The sweep: a band of gold goes down the body and leaves it in colour.
        if (since is > 0.15 and < 1.7)
        {
            var from = (int)(Ease((since - 0.15) / 1.4) * (h + 10)) - 8;
            c.PaintBand(p, 0, 0, 0xFFF0A0u, 200, from, from + 5);
            c.PaintBand(p, 0, 0, 0xFFE890u, 90, 0, from);
        }

        // Rays, turning, and two rings that open on the creature.
        if (since < 2.2)
        {
            var k = since / 2.2;
            for (var r = 0; r < 8; r++)
            {
                var angle = (r * Math.PI / 4) + (since * 0.9);
                var length = 8 + (14 * Math.Sin(Math.Min(1, k * 1.4) * Math.PI));
                for (var d = 4; d < length; d += 2)
                {
                    p.Set(cx + (int)Math.Round(Math.Cos(angle) * d * 1.4), cy + (int)Math.Round(Math.Sin(angle) * d * 0.8), 0xFFFBE0u, (int)(220 * (1 - k)));
                }
            }

            Ring(p, cx, cy, 4 + (k * 34), (4 + (k * 34)) * 0.6, 0xFFE890u, (int)(210 * (1 - k)));
            Ring(p, cx, cy, 4 + (Math.Max(0, k - 0.15) * 34), (4 + (Math.Max(0, k - 0.15) * 34)) * 0.6, 0xFFFFFFu, (int)(150 * (1 - k)));
        }

        var amount = anim.HealAmount(i);
        if (amount > 0 && since is > 0.5 and < 2.3)
        {
            var text = "+" + amount;
            var width = CapGlyphs.Width(text, true, 2);
            var rise = (int)(16 * Ease((since - 0.5) / 1.0));
            CapGlyphs.Outlined(p, text, cx - (width / 2), Math.Max(cy - (h / 2) - 6 - rise - 14, Ceiling(l)), 0xFFE890u, pal.Ink, true, 2, (int)(255 * Math.Min(1, (2.3 - since) / 0.5)));
        }
    }

    // ---------------------------------------------------------------- cure and entrance

    /// <summary>How a status goes: it comes apart in its own way, there is a clean flash, and the little bottle of what was used rises and fades.</summary>
    private static void Cure(CapSheet p, CapSeaPalette pal, CapCreature c, CapStatus status, double since, int i, int cx, int cy, int w, int h)
    {
        var (_, colour) = CapArt.StatusLook(status);
        var left = cx - (w / 2);
        var top = cy - (h / 2);
        const double Flash = 0.38;

        switch (status)
        {
            case CapStatus.Poison:
                // The bubbles swell, then pop one after another into specks.
                for (var b = 0; b < 6; b++)
                {
                    var popAt = 0.12 + (0.07 * b);
                    var x = left + (CapArt.Hash(i, b + 41) % Math.Max(1, w));
                    var y = top + h - (int)(((b + 0.5) / 6) * (h + 8)) - (int)(since * 6);
                    if (since < popAt)
                    {
                        var r = 1 + (int)(since * 8);
                        Ring(p, x, y, r, r, 0xC060C0u, 230);
                        p.Set(x - 1, y - 1, 0xF0C0F0u, 200);
                    }
                    else if (since < popAt + 0.4)
                    {
                        var k = (since - popAt) / 0.4;
                        for (var sp = 0; sp < 4; sp++)
                        {
                            var angle = (sp * Math.PI / 2) + 0.7;
                            p.Set(x + (int)Math.Round(Math.Cos(angle) * (3 + (k * 8))), y + (int)Math.Round(Math.Sin(angle) * (3 + (k * 8))), sp % 2 == 0 ? 0xFFFFFFu : 0xE0A0E0u, (int)(240 * (1 - k)));
                        }
                    }
                }

                break;
            case CapStatus.Burn:
                // The embers go dark as they fall and give off steam.
                for (var b = 0; b < 7; b++)
                {
                    var x = left + (CapArt.Hash(i, b + 51) % Math.Max(1, w));
                    var y = top + (h / 2) - (((b % 3) - 1) * 3) + (int)(since * (10 + b));
                    p.Set(x, y, CapSheet.Mix(0x6A6A74u, 0xFF8030u, Math.Clamp(1 - (since / 0.9), 0, 1)), (int)(240 * (1 - Math.Clamp(since / 1.4, 0, 1))));
                }

                for (var b = 0; b < 4; b++)
                {
                    var rise = (since - 0.1 - (b * 0.08)) / 1.2;
                    if (rise is <= 0 or >= 1) continue;
                    var x = left + (CapArt.Hash(i, b + 91) % Math.Max(1, w)) + (int)Math.Round(Math.Sin((since * 4) + b) * 1.5);
                    var y = top + h - (int)(rise * (h + 10));
                    for (var d = 0; d < 3; d++)
                    {
                        if (((d + b) & 1) == 0) p.Set(x + d - 1, y, 0xD8DCE8u, (int)(150 * (1 - rise)));
                    }
                }

                break;
            case CapStatus.Sleep:
                // The Z's rise, brighten and turn into stars that go out; a small sun comes up over the head.
                for (var z = 0; z < 2; z++)
                {
                    var rise = Math.Clamp((since * 0.9) + (z * 0.25), 0, 1.4);
                    var x = left + w + (int)(rise * 4) - 2;
                    var y = top - (int)(rise * 7) + 2;
                    if (rise < 0.9) CapGlyphs.Outlined(p, "Z", x, y, 0xE8E8F8u, pal.Ink);
                    else
                    {
                        var k = Math.Clamp((rise - 0.9) / 0.5, 0, 1);
                        foreach (var (dx, dy) in Cross) p.Set(x + 2 + dx, y + 2 + dy, 0xFFFFE0u, (int)(255 * (1 - k)));
                    }
                }

                for (var r = 0; r < 5; r++)
                {
                    var angle = Math.PI + (r * Math.PI / 4);
                    var length = Math.Min(1, since / 0.35) * (6 + (Math.Sin(since * 8) * 1.5)) * (since < 1.2 ? 1 : 0);
                    for (var d = 3; d < length + 3; d++) p.Set(cx + (int)Math.Round(Math.Cos(angle) * (d + (w / 3.0))), top + 1 + (int)Math.Round(Math.Sin(angle) * d), 0xFFE070u, 210);
                }

                break;
            case CapStatus.Paralysis:
                // The sparks thin out and the last arc discharges to the floor.
                if (since < 0.5)
                {
                    var beat = (int)Math.Floor(since * 20);
                    for (var a = 0; a < 3 - (int)(since * 5); a++)
                    {
                        var ax = left + (CapArt.Hash(beat, i + a) % Math.Max(1, w));
                        var ay = top + (CapArt.Hash(beat, i + a + 3) % Math.Max(1, h));
                        p.Line(ax, ay, ax + 3, ay + 4, 0xFFF060u);
                        p.Line(ax + 3, ay + 4, ax + 1, ay + 8, 0xFFD030u);
                    }
                }
                else if (since < 1.0)
                {
                    var k = (since - 0.5) / 0.5;
                    var x = cx;
                    var y = cy + (h / 2);
                    for (var seg = 0; seg < 7; seg++)
                    {
                        var nx = x + ((seg % 2 == 0 ? 1 : -1) * (2 + (CapArt.Hash(seg, i) % 3)));
                        var ny = y + 4;
                        p.Line(x, y, nx, ny, seg % 2 == 0 ? 0xFFF9A0u : 0xFFD030u, (int)(255 * (1 - k)));
                        (x, y) = (nx, ny);
                    }
                }

                break;
            case CapStatus.Freeze:
                // The ice cracks across the body and breaks into shards that fall away.
                if (since < 0.45)
                {
                    var k = since / 0.45;
                    for (var line = 0; line < 5; line++)
                    {
                        var angle = (line * 1.26) + 0.4;
                        p.Line(cx, cy, cx + (int)Math.Round(Math.Cos(angle) * (w / 2.0) * k * 1.2), cy + (int)Math.Round(Math.Sin(angle) * (h / 2.0) * k * 1.2), 0xFFFFFFu, 230);
                    }

                    c.PaintTint(p, 0, 0, 0xA8E8F8u, 50);
                }
                else
                {
                    var k = Math.Clamp((since - 0.45) / 1.0, 0, 1);
                    for (var sh = 0; sh < 10; sh++)
                    {
                        var angle = (sh * 0.628) + 0.2;
                        p.Rect(cx + (int)Math.Round(Math.Cos(angle) * (4 + (k * 18))), cy + (int)Math.Round(Math.Sin(angle) * (3 + (k * 8))) + (int)(14 * k * k), 2, 2, sh % 2 == 0 ? 0xE8FAFFu : 0x98D8F0u, (int)(240 * (1 - k)));
                    }
                }

                break;
        }

        // The clean flash, and the ring of the colour of the status, going out for the last time.
        if (since is >= Flash and < Flash + 0.25)
        {
            var k = (since - Flash) / 0.25;
            c.PaintTint(p, 0, 0, 0xFFFFFFu, (int)(190 * (1 - k)));
            Ring(p, cx, cy, 4 + (k * 24), (4 + (k * 24)) * 0.6, colour, (int)(220 * (1 - k)));
        }

        // Sparkles rising after it: the creature is clean.
        if (since is > Flash and < 1.6)
        {
            for (var sp = 0; sp < 6; sp++)
            {
                var rise = ((since - Flash) * 0.8) + (sp * 0.1);
                var x = cx + ((CapArt.Hash(sp, 71) % Math.Max(3, w)) - (w / 2));
                var y = cy + (h / 2) - (int)(rise * (h + 10));
                var alpha = (int)(230 * Math.Clamp(1 - ((since - Flash) / 1.2), 0, 1));
                p.Set(x, y, 0xFFFFFFu, alpha);
                p.Set(x - 1, y, 0xB8F0FFu, alpha / 2);
                p.Set(x + 1, y, 0xB8F0FFu, alpha / 2);
            }
        }

        // The little bottle of what was used, over the creature's shoulder: it comes up, stays, and goes.
        if (since < 1.5) Bottle(p, cx + (w / 2) - 1, top - 3 - (int)(6 * Ease(since / 0.5)), colour, (int)(255 * Math.Clamp(Math.Min(since / 0.15, (1.5 - since) / 0.4), 0, 1)));
    }

    /// <summary>A flask seven cells wide and nine tall, with the colour of what it cures.</summary>
    private static void Bottle(CapSheet p, int x, int y, uint liquid, int alpha)
    {
        if (alpha <= 0) return;
        p.Rect(x + 2, y, 3, 1, 0xB8C0D0u, alpha);
        p.Rect(x + 2, y + 1, 3, 1, 0x7A889Cu, alpha);
        p.Rect(x + 1, y + 2, 5, 1, 0xE8F0FFu, alpha);
        p.Rect(x, y + 3, 7, 6, 0xE8F0FFu, alpha);
        p.Rect(x + 1, y + 3, 5, 5, 0x28334Au, alpha);
        p.Rect(x + 1, y + 5, 5, 3, liquid, alpha);
        p.Set(x + 1, y + 3, 0xFFFFFFu, alpha);
        p.Set(x + 1, y + 4, 0xFFFFFFu, alpha);
        p.Set(x, y + 8, 0x98A4BCu, alpha);
        p.Set(x + 6, y + 8, 0x98A4BCu, alpha);
    }

    /// <summary>A status coming in: a ring of its colour closes on the creature, specks fly in to it, and the creature takes its colour for an instant.</summary>
    private static void Entrance(CapSheet p, CapCreature c, CapStatus status, double since, int cx, int cy, double s)
    {
        var (_, colour) = CapArt.StatusLook(status);
        var k = Math.Clamp(since / 0.7, 0, 1);
        var radius = 4 + (26 * s * (1 - Ease(k)));
        Ring(p, cx, cy, radius, radius * 0.6, colour, (int)(230 * (1 - (k * 0.4))));
        for (var sp = 0; sp < 8; sp++)
        {
            var angle = (sp * Math.PI / 4) + 0.3;
            var r = 4 + (30 * s * (1 - Ease((since - (sp * 0.03)) / 0.7)));
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * r * 1.3), cy + (int)Math.Round(Math.Sin(angle) * r * 0.8), sp % 2 == 0 ? colour : 0xFFFFFFu, (int)(240 * (1 - k)));
        }

        if (since is > 0.55 and < 0.9) c.PaintTint(p, 0, 0, colour, (int)(120 * (1 - ((since - 0.55) / 0.35))));
    }

    // ---------------------------------------------------------------- item and move

    /// <summary>The held item changing hands: a glint where it is worn; if it was given up, a bubble that leaves it and floats away.</summary>
    private static void Item(CapSheet p, (int X, int Y) at, double since, int kind)
    {
        var k = since / 1.6;
        var (x, y) = at;

        // Got one: a ring of gold closes on the place and a four-pointed glint stays a moment.
        if (kind is 0 or 2)
        {
            Ring(p, x, y, 2 + (8 * (1 - Ease(Math.Min(1, since / 0.5)))), 2 + (8 * (1 - Ease(Math.Min(1, since / 0.5)))), 0xFFE070u, (int)(230 * (1 - k)));
            if (since > 0.2)
            {
                var a = (int)(255 * Math.Clamp(1 - ((since - 0.2) / 1.2), 0, 1));
                foreach (var (dx, dy) in Cross) p.Set(x + (dx * 2), y + (dy * 2), 0xFFFFFFu, a);
                p.Set(x, y, 0xFFFFFFu, a);
            }
        }

        // Lost it (or swapped it): a bubble leaves the place and goes up, wobbling.
        if (kind is 1 or 2)
        {
            var by = y - (int)(since * 14);
            var bx = x + (int)Math.Round(Math.Sin(since * 5) * 2);
            var a = (int)(230 * (1 - k));
            foreach (var (dx, dy) in Bubble) p.Set(bx + dx, by + dy, 0xDDEEFFu, a);
        }
    }

    /// <summary>A move learned: its pip flares with the colour of its type and a star opens over it.</summary>
    private static void Move(CapSheet p, (int X, int Y, int Letters) ticker, double since, int type)
    {
        var colour = type >= 0 ? CapArt.TypeColour(type) : 0xFFE070u;
        var x = ticker.X + 2;
        var y = ticker.Y + 2;
        var k = since / 1.6;
        Ring(p, x, y, 2 + (10 * Ease(Math.Min(1, since / 0.7))), 2 + (10 * Ease(Math.Min(1, since / 0.7))), colour, (int)(220 * (1 - k)));
        var length = (int)(7 * Math.Sin(Math.Min(1, since / 0.8) * Math.PI));
        var a = (int)(255 * Math.Clamp(1 - k, 0, 1));
        for (var d = 1; d <= length; d++)
        {
            p.Set(x - d, y, 0xFFFFFFu, a);
            p.Set(x + d, y, 0xFFFFFFu, a);
            p.Set(x, y - d, 0xFFFFFFu, a);
            p.Set(x, y + d, 0xFFFFFFu, a);
        }
    }
}
