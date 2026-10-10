namespace PermaLocke.App.Views;

/// <summary>
/// The part of the cap panel that moves (2026-10-10): the flame of the fuse, the bars filling and their trails, the dust in the case, the
/// light on its rim, and what happens to a Pokémon (a hit, a heal, a level, an egg that hatches, a fall, a status). Painted over a copy
/// of the still part every frame, so a frame costs the copy and a few hundred cells.
/// </summary>
/// <remarks>
/// A function of the data, the animation state (<see cref="CapAnim"/>) and the time, nothing else: no clock, no random number that is
/// not a hash of what it draws. With no animation state (<c>anim</c> null) it paints the still panel: the bars at their real value and
/// the flame in one pose.
/// </remarks>
public static class CapLive
{
    public static void Paint(CapSheet p, CapLayout layout, CapPlaqueData data, CapAnim? anim, double t)
    {
        var mood = anim?.Mood(t) ?? (CapMood.Calm, CapMood.Calm, 1.0);

        if (layout.Panel == CapPanelKind.Deep)
        {
            Memorial(p, layout, anim, t, mood);
            return;
        }

        if (anim is not null) Dust(p, layout, t);
        Fuse(p, layout, data, anim, t, mood);

        for (var i = 0; i < layout.Metrics.Length && i < data.Party.Count; i++)
        {
            Niche(p, layout, data, i, anim, t);
        }

        if (anim is not null) Rim(p, layout, mood, t);
    }

    private static void Memorial(CapSheet p, CapLayout layout, CapAnim? anim, double t, (CapMood Now, CapMood Before, double Blend) mood)
    {
        // The candles burn higher for twenty seconds after someone falls.
        var flare = anim is null ? 0 : Strength(mood, CapMood.Mourning);
        var strength = 0.8 + (0.5 * flare);

        if (layout.HasCup) Flame(p, (layout.Cols / 2) - 3 + 3, 3, t, strength, 5);
        if (layout.Mode == CapMode.Strip) return;

        var shelf = CapComposer.Shelf(layout);
        foreach (var cx in CapComposer.CandleXs(layout)) Flame(p, cx, shelf.Top + 4, t + (cx * 0.13), strength, 5);

        if (anim is not null) Dust(p, layout, t);
        if (anim is not null) Rim(p, layout, mood, t);
    }

    /// <summary>How much of a mood is in the light at the moment: 0 to 1, taking the blend between two moods into account.</summary>
    private static double Strength((CapMood Now, CapMood Before, double Blend) mood, CapMood which) =>
        (mood.Now == which ? mood.Blend : 0) + (mood.Before == which ? 1 - mood.Blend : 0);

    // ---------------------------------------------------------------- the fuse

    private static void Fuse(CapSheet p, CapLayout layout, CapPlaqueData data, CapAnim? anim, double t,
        (CapMood Now, CapMood Before, double Blend) mood)
    {
        if (data.Highest is not { } highest) return;

        var (x, y, span) = CapComposer.FuseTrack(layout);
        var compact = layout.Mode == CapMode.Compact;
        var atCap = highest >= data.Cap;
        var share = anim?.FuseShare(t, highest, data.Cap) ?? Math.Clamp(highest / (double)Math.Max(1, data.Cap), 0, 1);
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

        // A lower flame for a mourning party, a taller one at the cap, a burst when the cap has just changed.
        var strength = (atCap ? 1.0 : 0.75) - (0.3 * Strength(mood, CapMood.Mourning)) + (0.5 * (anim?.CapPulse(t) ?? 0));
        Flame(p, x + tip, y - 1, t, Math.Max(0.4, strength), compact ? 4 : 7, atCap);

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

    // ---------------------------------------------------------------- one niche

    private static void Niche(CapSheet p, CapLayout layout, CapPlaqueData data, int i, CapAnim? anim, double t)
    {
        var m = data.Party[i];
        var metrics = layout.Metrics[i];
        var r = metrics.Outer;
        if (r.Width <= 0) return;

        var alive = !m.Egg && !m.Fallen;
        double share = 0;

        if (alive && metrics.HpBar.Width > 0)
        {
            share = anim?.HpFraction(i, t) ?? (m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0);
            var bar = metrics.HpBar;
            var inside = bar.Width - 2;
            var fill = share > 0 ? Math.Max(1, (int)(inside * share)) : 0;

            // The trail of a drop: the stretch that was there, light, until it catches up.
            if (anim?.TrailFraction(i, t) is { } trail && trail > share)
            {
                var to = Math.Min(inside, (int)(inside * trail));
                CapArt.BarFill(p, bar.Left, bar.Top, bar.Width, bar.Height, fill, to, 0xF4E4C0, ticks: false);
            }

            CapArt.BarFill(p, bar.Left, bar.Top, bar.Width, bar.Height, 0, fill, CapArt.HpColour(share), ticks: layout.Mode != CapMode.Compact);
        }

        ExpBar(p, data, m, metrics, anim?.LevelPulse(i, t) ?? 0, anim, t);
        if (anim is null || r.Height < 12) return;

        var inner = new CapRect(r.Left + 2, r.Top + 2, r.Width - 4, r.Height - 4);
        var beat = (int)Math.Floor(t * 24);

        var hit = anim.HitPulse(i, t);
        if (hit > 0) p.Dither(inner.Left, inner.Top, inner.Width, inner.Height, 0xFF3020, (int)(150 * hit), beat & 1);

        var heal = anim.HealPulse(i, t);
        if (heal > 0)
        {
            p.Dither(inner.Left, inner.Top, inner.Width, inner.Height, 0x58E070, (int)(110 * heal), beat & 1);
            Rise(p, metrics.Alcove, i, t, 3, 0x9CF0A8, cross: true, speed: 0.9, heal);
        }

        var hatch = anim.HatchPulse(i, t);
        if (hatch > 0)
        {
            p.Dither(inner.Left, inner.Top, inner.Width, inner.Height, 0xFFFFFF, (int)(220 * hatch), beat & 1);
            Rise(p, metrics.Alcove, i, t, 5, 0xFFF0B0, cross: true, speed: 1.2, hatch);
        }

        var level = anim.LevelPulse(i, t);
        if (level > 0 && metrics.LevelChip.Width > 0) LevelUp(p, metrics.LevelChip, level, i);

        if (anim.SinceFall(i, t) is { } since && since < 2.4) Embers(p, metrics.Alcove, i, since);

        if (alive && m.Status != CapStatus.None) StatusFx(p, metrics.Alcove, m.Status, i, t);

        // Low HP in a battle: the rim of the niche beats red, about twice a second.
        if (alive && data.InBattle && share > 0 && share <= 0.25)
        {
            var beatRed = 0.5 + (0.5 * Math.Sin(t * Math.PI * 3.2));
            Outline(p, r, 0xFF4030, (int)(70 + (150 * beatRed)));
        }
        else if (alive && data.InBattle && anim.SinceHit(i, t) is { } hitAgo && hitAgo < 6)
        {
            // The last one hit stands out for a few seconds: the game's active slot is not read, but who took the damage is.
            Outline(p, r, CapArt.BrassHigh, (int)(150 * (1 - (hitAgo / 6))));
        }
    }

    private static void ExpBar(CapSheet p, CapPlaqueData data, CapPlaqueMember m, CapNicheMetrics metrics, double levelPulse, CapAnim? anim, double t)
    {
        if (!metrics.HasExp || m.Exp < 0 || m.Egg || m.Fallen) return;

        // Blue on the way; gold and blinking when the next battle may put it at the cap; a full dim gold once it is there.
        var e = metrics.Exp;
        var atCap = m.AtCap(data.Cap);
        var near = m.NearCap(data.Cap);
        var on = !near || anim is null || ((int)Math.Floor(t * 3) & 1) == 0;
        var colour = atCap ? 0xA87828u : near ? (on ? CapArt.Gold : 0x6A4A18u) : 0x58B0D8u;
        if (levelPulse > 0) colour = CapSheet.Mix(colour, 0xFFFFFF, levelPulse);
        p.Rect(e.Left, e.Top, atCap ? e.Width : Math.Max(0, (int)(e.Width * Math.Clamp(m.Exp, 0, 1))), e.Height, colour);
    }

    /// <summary>A gold flash over the level chip and a ring of sparks opening round it.</summary>
    private static void LevelUp(CapSheet p, CapRect chip, double pulse, int i)
    {
        p.Rect(chip.Left, chip.Top, chip.Width, chip.Height, 0xFFF2B0, (int)(190 * pulse));

        var k = 1 - pulse;
        var radius = 2 + (int)(k * 10);
        var cx = chip.Left + (chip.Width / 2);
        var cy = chip.Top + (chip.Height / 2);
        var turn = (CapArt.Hash(i, 5) % 7) * 0.1;
        for (var s = 0; s < 8; s++)
        {
            var angle = (s * Math.PI / 4) + turn;
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * radius * 1.6), cy + (int)Math.Round(Math.Sin(angle) * radius * 0.7),
                s % 2 == 0 ? 0xFFFFFFu : 0xFFC030u, (int)(255 * pulse));
        }
    }

    /// <summary>Cells rising from the floor of an alcove: crosses for a heal or a hatching.</summary>
    private static void Rise(CapSheet p, CapRect area, int seed, double t, int count, uint colour, bool cross, double speed, double fade)
    {
        for (var k = 0; k < count; k++)
        {
            var phase = ((t * speed) + (k / (double)count)) % 1.0;
            var x = area.Left + 3 + (CapArt.Hash(seed, k + 11) % Math.Max(1, area.Width - 6));
            var y = area.Bottom - 2 - (int)(phase * (area.Height - 3));
            var alpha = (int)(230 * (1 - phase) * Math.Clamp(fade * 1.3, 0, 1));
            p.Set(x, y, colour, alpha);
            if (!cross) continue;
            p.Set(x - 1, y, colour, alpha);
            p.Set(x + 1, y, colour, alpha);
            p.Set(x, y - 1, colour, alpha);
            p.Set(x, y + 1, colour, alpha);
        }
    }

    /// <summary>The embers of a Pokémon that has just fallen: red and grey cells going up from its window for two seconds.</summary>
    private static void Embers(CapSheet p, CapRect area, int seed, double since)
    {
        var fade = 1 - (since / 2.4);
        for (var k = 0; k < 7; k++)
        {
            var phase = (since * 0.55) + (k * 0.09);
            if (phase > 1) continue;
            var x = area.Left + 2 + (CapArt.Hash(seed, k + 31) % Math.Max(1, area.Width - 4));
            var y = area.Bottom - 2 - (int)(phase * (area.Height + 6));
            p.Set(x, y, k % 3 == 0 ? 0xFF6A40u : k % 3 == 1 ? 0xB8433Au : 0x8C8A92u, (int)(220 * fade * (1 - phase)));
        }
    }

    /// <summary>A little life for each status, inside the Pokémon's window: bubbles, embers, a drifting Z, sparks, glints.</summary>
    private static void StatusFx(CapSheet p, CapRect a, CapStatus status, int seed, double t)
    {
        var beat = (int)Math.Floor(t * 8);
        switch (status)
        {
            case CapStatus.Poison:
                for (var k = 0; k < 3; k++)
                {
                    var phase = ((t * 0.7) + (k / 3.0)) % 1.0;
                    var x = a.Left + 3 + (CapArt.Hash(seed, k + 41) % Math.Max(1, a.Width - 6)) + (int)Math.Round(Math.Sin((t * 2) + k) * 1.5);
                    var y = a.Bottom - 3 - (int)(phase * (a.Height - 4));
                    p.Set(x, y, 0xC060C0, (int)(220 * (1 - phase)));
                    p.Set(x + 1, y, 0xE0A0E0, (int)(160 * (1 - phase)));
                }

                break;
            case CapStatus.Burn:
                for (var k = 0; k < 4; k++)
                {
                    var phase = ((t * 1.1) + (k / 4.0)) % 1.0;
                    var x = a.Left + 3 + (CapArt.Hash(seed, k + 51) % Math.Max(1, a.Width - 6));
                    p.Set(x, a.Bottom - 2 - (int)(phase * (a.Height - 2)), k % 2 == 0 ? 0xFF8030u : 0xFFD060u, (int)(240 * (1 - phase)));
                }

                break;
            case CapStatus.Sleep:
                for (var k = 0; k < 2; k++)
                {
                    var phase = ((t * 0.45) + (k * 0.5)) % 1.0;
                    var x = a.Right - 8 + (int)(phase * 3);
                    var y = a.Top + 7 - (int)(phase * 6);
                    if (a.Height >= 14) p.Small("Z", x, y, 0xE8E8F8);
                }

                break;
            case CapStatus.Paralysis:
                if (beat % 3 == 0)
                {
                    var x = a.Left + 2 + (CapArt.Hash(beat, seed) % Math.Max(1, a.Width - 4));
                    var y = a.Top + 2 + (CapArt.Hash(beat, seed + 1) % Math.Max(1, a.Height - 4));
                    p.Set(x, y, 0xFFF060);
                    p.Set(x + 1, y + 1, 0xFFF060);
                    p.Set(x - 1, y + 1, 0xFFD030);
                }

                break;
            case CapStatus.Freeze:
                p.Dither(a.Left, a.Top, a.Width, a.Height, 0xA8E8F8, 46, 0);
                var glint = CapArt.Hash(beat / 2, seed);
                p.Set(a.Left + 2 + (glint % Math.Max(1, a.Width - 4)), a.Top + 2 + ((glint / 7) % Math.Max(1, a.Height - 4)), 0xFFFFFF);
                break;
        }
    }

    /// <summary>The brass thread of a niche drawn again over the old one, in another colour.</summary>
    private static void Outline(CapSheet p, CapRect r, uint colour, int alpha)
    {
        var a = Math.Clamp(alpha, 0, 255);
        p.HLine(r.Left + 2, r.Top + 1, r.Width - 4, colour, a);
        p.HLine(r.Left + 2, r.Bottom - 2, r.Width - 4, colour, a);
        p.VLine(r.Left + 1, r.Top + 2, r.Height - 4, colour, a);
        p.VLine(r.Right - 2, r.Top + 2, r.Height - 4, colour, a);
    }

    // ---------------------------------------------------------------- the light on the case

    /// <summary>
    /// Dust drifting up in the dark of the case. It is painted only where the cell under it is dark wood, so it never lands on a niche, a
    /// plate or an icon, and it is a handful of cells.
    /// </summary>
    private static void Dust(CapSheet p, CapLayout layout, double t)
    {
        var inner = layout.Inner;
        var count = Math.Clamp(inner.Width * inner.Height / 2600, 6, 28);
        for (var k = 0; k < count; k++)
        {
            var speed = 0.6 + ((CapArt.Hash(k, 2) % 5) * 0.25);
            var span = Math.Max(1, inner.Height);
            var y = inner.Bottom - 1 - (int)(((CapArt.Hash(k, 3) % span) + (t * speed)) % span);
            var x = inner.Left + 2 + (CapArt.Hash(k, 1) % Math.Max(1, inner.Width - 4)) + (int)Math.Round(Math.Sin((t * 0.4) + k) * 2);
            if (!p.Solid(x, y)) continue;

            var c = p.At(x, y);
            var luma = (0.30 * ((c >> 16) & 255)) + (0.59 * ((c >> 8) & 255)) + (0.11 * (c & 255));
            if (luma > 60) continue;
            p.Set(x, y, 0xD8B878, 70 + (CapArt.Hash(k, 4) % 50));
        }
    }

    /// <summary>The light along the inner rim of the case, by mood: nothing when calm, cool in a battle, red pulsing in danger, a violet veil in mourning, gold running at the cap.</summary>
    private static void Rim(CapSheet p, CapLayout layout, (CapMood Now, CapMood Before, double Blend) mood, double t)
    {
        RimOf(p, layout, mood.Now, mood.Blend, t);
        if (mood.Blend < 1) RimOf(p, layout, mood.Before, 1 - mood.Blend, t);
    }

    private static void RimOf(CapSheet p, CapLayout layout, CapMood mood, double strength, double t)
    {
        if (strength <= 0 || mood == CapMood.Calm) return;

        var r = layout.Inner;
        void Ring(uint colour, int alpha, int thickness)
        {
            var a = (int)(alpha * strength);
            for (var k = 0; k < thickness; k++)
            {
                p.HLine(r.Left + k, r.Top + k, r.Width - (2 * k), colour, a);
                p.HLine(r.Left + k, r.Bottom - 1 - k, r.Width - (2 * k), colour, a);
                p.VLine(r.Left + k, r.Top + k, r.Height - (2 * k), colour, a);
                p.VLine(r.Right - 1 - k, r.Top + k, r.Height - (2 * k), colour, a);
            }
        }

        switch (mood)
        {
            case CapMood.Battle:
                Ring(0x6AA8E8, (int)(60 + (40 * Math.Sin(t * 2))), 2);
                break;
            case CapMood.Danger:
                var beat = 0.5 + (0.5 * Math.Sin(t * Math.PI * 3.2));
                Ring(0xFF4030, (int)(80 + (150 * beat)), 2);
                const int corner = 8;
                var alpha = (int)(70 * beat * strength);
                p.Dither(r.Left, r.Top, corner, corner, 0xFF3020, alpha, 0);
                p.Dither(r.Right - corner, r.Top, corner, corner, 0xFF3020, alpha, 1);
                p.Dither(r.Left, r.Bottom - corner, corner, corner, 0xFF3020, alpha, 1);
                p.Dither(r.Right - corner, r.Bottom - corner, corner, corner, 0xFF3020, alpha, 0);
                break;
            case CapMood.Mourning:
                Ring(0x6A5A9A, 90, 2);
                p.Dither(r.Left, r.Top, r.Width, 3, 0x181028, (int)(120 * strength), 0);
                p.Dither(r.Left, r.Bottom - 3, r.Width, 3, 0x181028, (int)(120 * strength), 1);
                break;
            case CapMood.Cap:
                // A bright stretch running along the top of the case.
                const int length = 18;
                var at = (int)((t * 36) % (r.Width + length)) - length;
                for (var k = 0; k < length; k++)
                {
                    var edge = 1 - (Math.Abs(k - (length / 2.0)) / (length / 2.0));
                    p.Set(r.Left + at + k, r.Top, 0xFFE070, (int)(200 * edge * strength));
                    p.Set(r.Left + at + k, r.Top + 1, 0xFFC030, (int)(110 * edge * strength));
                }

                break;
        }
    }
}
