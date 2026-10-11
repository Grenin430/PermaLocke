namespace PermaLocke.App.Views;

/// <summary>What the animations do to the picture of a creature this moment: where it is, how much of it shows, what colour washes over it.</summary>
/// <param name="Wave">How far the creature sways in the water, in cells, and <paramref name="WaveSpeed"/> how fast; both follow its life and the mood.</param>
/// <param name="HaloScale">How much of its halo shows: it dips now and then, the blink of its light.</param>
public readonly record struct CapPose(int Dx, int Dy, double Strength, bool Hidden, uint? HaloTint, uint? Wash, int WashAlpha, double Breath,
    double Wave = 0, double WaveSpeed = 0, double HaloScale = 1);

/// <summary>
/// The happenings of the sea (2026-10-10), as things that happen in water and light, not as flashes on a panel: a hit is a shock that goes out
/// through the water and a recoil; a heal, a column of pale light with crosses rising in it; a level, a ring of gold and stars; an egg
/// hatching, a burst with rays and shell flying; a status, bubbles, embers, a drifting Z, sparks or ice on the creature; a fall, the light going
/// out, the creature sinking slowly to the floor leaving a trail of bubbles, the dust of its landing, and it lying there until the run records it
/// as a fossil. All of them functions of the animation state and the time.
/// </summary>
public static class CapFx
{
    /// <summary>How long a creature takes to sink: from the moment its light goes out to the moment it lies on the floor.</summary>
    public const double SinkSeconds = 5.2;

    /// <summary>The pose of a creature's picture: recoil, sinking, flashes. The picture itself is painted by <see cref="CapCreature"/>.</summary>
    public static CapPose Pose(CapAnim? anim, int i, double t, CapPlaqueMember m, double share, bool battle, CapWorldLayout l, CapSeaContext ctx, int gravesNow)
    {
        var breath = 0.5 + (0.5 * Math.Sin((anim is null ? 0 : t) * (m.Fallen || m.Egg ? 1 : 1.4 + (4.0 * (1 - share))) + i));
        if (anim is null) return new CapPose(0, 0, m.Fallen ? 0.5 : 1, false, null, null, 0, breath);

        var dx = 0;
        var dy = 0;
        var strength = m.Fallen ? 0.5 : 1.0;
        uint? wash = null;
        var washAlpha = 0;
        uint? halo = null;

        var heal = anim.HealPulse(i, t);
        if (heal > 0.3) (wash, washAlpha) = (0xB8FFD0u, (int)(110 * (heal - 0.3)));

        var level = anim.LevelPulse(i, t);
        if (level > 0.5) (wash, washAlpha) = (0xFFE890u, (int)(150 * (level - 0.5)));

        // In a battle, someone at a quarter of their life has a red halo that beats: the fast heartbeat is in the breath.
        if (battle && !m.Fallen && !m.Egg && share > 0 && share <= 0.25) halo = 0xFF4A3Au;

        if (m.Status == CapStatus.Freeze) breath = 0.15;

        // What happened to it (a hit, a getting up) and its quiet life: the sway and the blink.
        CapFxEvents.Pose(anim, i, t, m, share, battle, ref dx, ref wash, ref washAlpha, ref strength, out var wave, out var waveSpeed, out var haloScale);

        var hidden = false;
        if (anim.SinceFall(i, t) is { } fall)
        {
            // The light goes out (a white flash), the creature leaves its place and sinks; once it has landed a pale ghost of it stays where it was.
            if (fall < 0.5) (wash, washAlpha) = (0xFFFFFFu, (int)(210 * (1 - (fall / 0.5))));
            if (fall < SinkSeconds) hidden = true;
            else strength = Math.Min(0.5, 0.2 + ((fall - SinkSeconds) * 0.3));
        }

        _ = l;
        _ = ctx;
        _ = gravesNow;
        return new CapPose(dx, dy, strength, hidden, halo, wash, washAlpha, breath, wave, waveSpeed, haloScale);
    }

    // ---------------------------------------------------------------- overlays

    /// <summary>What happens round a creature, painted after its picture.</summary>
    public static void Overlay(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapCreature c, CapPlaqueMember m, CapAnim anim, int i, double t, double phase)
    {
        var (cx, cy) = c.Centre;
        var (w, h) = c.PictureSize;

        // Hits, heals, revives, cures, statuses coming in, items and moves are CapFxEvents'.
        CapFxEvents.Overlay(p, l, pal, c, m, anim, i, t);

        var level = anim.LevelPulse(i, t);
        if (level > 0) Level(p, c, cx, cy, level, i);

        var hatch = anim.HatchPulse(i, t);
        if (hatch > 0) Hatch(p, cx, cy, hatch, i);

        if (!m.Fallen && !m.Egg && m.Status != CapStatus.None) Status(p, c, m.Status, phase, i, pal, CapFxEvents.Intensity(anim, i, t));

        // The light of a creature that falls goes out in a ring of pale light that closes in on it and a second that goes out.
        if (anim.SinceFall(i, t) is { } fall && fall < 1.2)
        {
            var k = fall / 1.2;
            Ring(p, cx, cy, 34 * (1 - k) + 2, (34 * (1 - k) + 2) * 0.6, 0xFFFFFF, (int)(220 * (1 - k)));
            Ring(p, cx, cy, 4 + (k * 46), (4 + (k * 46)) * 0.6, 0xB8C8F0, (int)(160 * (1 - k)));
        }
    }

    private static void Ring(CapSheet p, int cx, int cy, double rx, double ry, uint colour, int alpha, bool dither = true)
    {
        var steps = Math.Max(20, (int)(rx * 5));
        for (var a = 0; a < steps; a++)
        {
            if (dither && (a & 1) == 1) continue;
            var angle = a * 2 * Math.PI / steps;
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * rx), cy + (int)Math.Round(Math.Sin(angle) * ry), colour, alpha);
        }
    }

    /// <summary>A level: a ring of gold opening round the creature, eight stars going out with it, and the number flashing.</summary>
    private static void Level(CapSheet p, CapCreature c, int cx, int cy, double pulse, int i)
    {
        var k = 1 - pulse;
        Ring(p, cx, cy, 6 + (k * 30), (6 + (k * 30)) * 0.6, 0xFFD860, (int)(210 * pulse));
        var turn = (CapArt.Hash(i, 5) % 7) * 0.1;
        for (var s = 0; s < 8; s++)
        {
            var angle = (s * Math.PI / 4) + turn;
            var reach = 8 + (k * 34);
            var x = cx + (int)Math.Round(Math.Cos(angle) * reach);
            var y = cy + (int)Math.Round(Math.Sin(angle) * reach * 0.6);
            var star = s % 2 == 0 ? 0xFFFFFFu : 0xFFC030u;
            p.Set(x, y, star, (int)(255 * pulse));
            p.Set(x - 1, y, star, (int)(150 * pulse));
            p.Set(x + 1, y, star, (int)(150 * pulse));
            p.Set(x, y - 1, star, (int)(150 * pulse));
            p.Set(x, y + 1, star, (int)(150 * pulse));
        }

        if (c.LevelBox.Width > 0) p.Rect(c.LevelBox.Left, c.LevelBox.Top, c.LevelBox.Width, c.LevelBox.Height, 0xFFF2B0, (int)(150 * pulse));
    }

    /// <summary>An egg hatching: the light bursts out in rays, a white disc opens, and pieces of shell fly and fall.</summary>
    private static void Hatch(CapSheet p, int cx, int cy, double pulse, int i)
    {
        var k = 1 - pulse;
        for (var r = 0; r < 8; r++)
        {
            var angle = r * Math.PI / 4;
            var length = k * 26;
            for (var d = 3; d < length; d++)
            {
                if ((d & 1) == 0) continue;
                p.Set(cx + (int)Math.Round(Math.Cos(angle) * d * 1.4), cy + (int)Math.Round(Math.Sin(angle) * d * 0.8), 0xFFFBE0, (int)(230 * pulse));
            }
        }

        Ring(p, cx, cy, k * 22, k * 14, 0xFFFFFF, (int)(200 * pulse), dither: false);

        for (var s = 0; s < 10; s++)
        {
            var vx = ((CapArt.Hash(s, 31 + i) % 200) - 100) / 100.0;
            var vy = -((CapArt.Hash(s, 41 + i) % 100) / 100.0);
            var x = cx + (int)(vx * k * 34);
            var y = cy + (int)((vy * k * 30) + (22 * k * k));
            p.Set(x, y, 0xFFFFFF, (int)(255 * pulse));
            p.Set(x + 1, y, 0xEEEEEE, (int)(200 * pulse));
        }
    }

    /// <summary>A status, as something that happens round the creature: bubbles, embers, a Z, sparks, ice.</summary>
    private static void Status(CapSheet p, CapCreature c, CapStatus status, double phase, int i, CapSeaPalette pal, double intensity = 1)
    {
        int Few(int count) => (int)Math.Ceiling(count * intensity);
        var (cx, cy) = c.Centre;
        var (w, h) = c.PictureSize;
        var left = cx - (w / 2);
        var top = cy - (h / 2);
        var beat = (int)Math.Floor(phase * 8);

        switch (status)
        {
            case CapStatus.Poison:
                // A sickly violet tint that breathes, and bubbles of three sizes that rise, wobble and swell as they go.
                c.PaintTint(p, 0, 0, 0xA030C0, (int)((26 + (12 * Math.Sin(phase * 3))) * intensity));
                for (var k = 0; k < Few(10); k++)
                {
                    var rise = ((phase * (0.45 + ((k % 3) * 0.15))) + (k / 10.0)) % 1.0;
                    var x = left + (CapArt.Hash(i, k + 41) % Math.Max(1, w)) + (int)Math.Round(Math.Sin((phase * 2) + k) * 2);
                    var y = top + h - (int)(rise * (h + 14));
                    var r = 1 + (k % 3) + (rise > 0.6 ? 1 : 0);
                    var fade = (int)(240 * (1 - (rise * rise)));
                    Ring(p, x, y, r, r, 0xC060C0, fade, dither: false);
                    p.Set(x - (r / 2), y - (r / 2), 0xF0C0F0, fade);
                }

                break;
            case CapStatus.Burn:
                // Flames: tongues that stand on the feet and climb the sides, each a stack of rows that narrow as they go up (red outside, orange, a
                // pale core), swaying out of step; sparks with a short trail rise from them and a thin smoke goes over the head.
                c.PaintTint(p, 0, 0, 0xFF5018, (int)((24 + (10 * Math.Sin(phase * 7))) * intensity));
                var tongues = Math.Max(2, Few(7));
                for (var f = 0; f < tongues; f++)
                {
                    var side = f % 3;
                    var fx = side == 1 ? left - 1 : side == 2 ? left + w : left + (w * (f + 1) / (tongues + 1));
                    var baseY = side == 0 ? top + h : top + h - 4 - ((f * 5) % Math.Max(1, h / 2));
                    var tall = (side == 0 ? 8 : 6) + (int)Math.Round(2.5 * Math.Sin((phase * 5) + (f * 2.3))) + (CapArt.Hash((int)Math.Floor(phase * 6), f + i) % 3);
                    for (var d = 0; d < tall; d++)
                    {
                        var k = d / (double)tall;
                        var sway = (int)Math.Round(Math.Sin((phase * 6) + (f * 1.7) + (d * 0.45)) * (0.5 + (k * 1.8)));
                        var half = (int)Math.Round(3.2 * Math.Pow(1 - k, 0.7));
                        var y = baseY - d;
                        p.Rect(fx + sway - half, y, (half * 2) + 1, 1, 0xE03010, (int)(215 * (1 - (k * 0.35))));
                        if (half >= 1) p.Rect(fx + sway - half + 1, y, Math.Max(1, (half * 2) - 1), 1, 0xFF8A20, 235);
                        if (half >= 2 && k < 0.65) p.Rect(fx + sway - half + 2, y, Math.Max(1, (half * 2) - 3), 1, 0xFFE880, 245);
                    }
                }

                for (var k = 0; k < Few(8); k++)
                {
                    var rise = ((phase * (0.8 + ((k % 3) * 0.2))) + (k / 8.0)) % 1.0;
                    var x = left + (CapArt.Hash(i, k + 51) % Math.Max(1, w)) + (int)Math.Round(Math.Sin((phase * 3) + k) * 2);
                    var y = top + h - 4 - (int)(rise * (h + 12));
                    var fade = (int)(255 * (1 - (rise * rise)));
                    p.Set(x, y, 0xFFF0A0, fade);
                    p.Set(x, y + 1, 0xFF9A30, fade * 2 / 3);
                    p.Set(x, y + 2, 0xE03010, fade / 3);
                }

                for (var s2 = 0; s2 < Few(3); s2++)
                {
                    var rise = ((phase * 0.4) + (s2 / 3.0)) % 1.0;
                    var x = left + (w / 4) + (s2 * w / 4) + (int)Math.Round(Math.Sin((phase * 2) + s2) * 3);
                    var y = top - 2 - (int)(rise * 12);
                    p.Rect(x, y, 3, 1, 0xB8BAC8, (int)(130 * (1 - rise)));
                    p.Rect(x + 1, y - 1, 2, 1, 0xB8BAC8, (int)(90 * (1 - rise)));
                }

                break;
            case CapStatus.Sleep:
                c.PaintTint(p, 0, 0, 0x4060C0, (int)(30 * intensity));
                for (var k = 0; k < Few(3); k++)
                {
                    var rise = ((phase * 0.4) + (k / 3.0)) % 1.0;
                    CapGlyphs.Outlined(p, "Z", left + w + (int)(rise * 6) - 2, top - (int)(rise * 10) + 2, 0xE8E8F8, pal.Ink, alpha: (int)(255 * (1 - (rise * 0.8))));
                }

                break;
            case CapStatus.Paralysis:
                if (beat % 2 == 0 && intensity > 0.4)
                {
                    c.PaintTint(p, 0, 0, 0xFFF060, 40);
                    for (var s = 0; s < 3; s++)
                    {
                        var ax = left + (CapArt.Hash(beat, i + (s * 7)) % Math.Max(1, w));
                        var ay = top + (CapArt.Hash(beat, i + 1 + (s * 7)) % Math.Max(1, h));
                        var bx = left + (CapArt.Hash(beat, i + 2 + (s * 7)) % Math.Max(1, w));
                        var by = top + (CapArt.Hash(beat, i + 3 + (s * 7)) % Math.Max(1, h));
                        p.Line(ax, ay, ((ax + bx) / 2) + 2, ((ay + by) / 2) - 2, 0xFFF060);
                        p.Line(((ax + bx) / 2) + 2, ((ay + by) / 2) - 2, bx, by, 0xFFD030);
                        p.Set(bx, by, 0xFFFFFF);
                    }
                }

                break;
            case CapStatus.Freeze:
                c.PaintTint(p, 0, 0, 0xA8E8F8, (int)(56 * intensity));
                for (var k = 0; k < Few(6); k++)
                {
                    var gx = left + (CapArt.Hash(i, k + 61) % Math.Max(1, w));
                    var gy = top + (CapArt.Hash(i, k + 71) % Math.Max(1, h));
                    var twinkle = ((int)Math.Floor((phase * 1.3) + k) & 1) == 0;
                    p.Set(gx, gy, twinkle ? 0xFFFFFFu : 0xB8F0FFu);
                    if (twinkle)
                    {
                        p.Set(gx - 1, gy, 0xB8F0FF, 160);
                        p.Set(gx + 1, gy, 0xB8F0FF, 160);
                        p.Set(gx, gy - 1, 0xB8F0FF, 160);
                        p.Set(gx, gy + 1, 0xB8F0FF, 160);
                    }
                }

                break;
        }
    }

    /// <summary>The sonar of a new cap: a ring of light that goes out from the buoy and a second one after it.</summary>
    public static void CapRing(CapSheet p, CapWorldLayout l, double since)
    {
        var cx = l.Buoy.Left + (l.Buoy.Width / 2);
        var cy = l.Buoy.Top + (l.Buoy.Height / 2);
        var k = since / 2.0;
        Ring(p, cx, cy, 8 + (k * 90), (8 + (k * 90)) * 0.5, 0xFFE890, (int)(210 * (1 - k)));
        if (k > 0.25) Ring(p, cx, cy, 8 + ((k - 0.25) * 90), (8 + ((k - 0.25) * 90)) * 0.5, 0xFFFFFF, (int)(140 * (1 - k)));
    }

    // ---------------------------------------------------------------- the fall

    /// <summary>
    /// A fall, from the moment the light goes out: the creature leaves its place, sinks to where its fossil will be, leaving bubbles, and raises
    /// the dust of the sea floor when it lands. Until the run records it as a fossil, it lies there, pale.
    /// </summary>
    public static void Fall(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, CapCreature c, CapAnim anim, int i, double t, int gravesNow,
        int[] columns)
    {
        if (anim.SinceFall(i, t) is not { } s || c.Member is null) return;

        var (sx, sy) = c.Centre;
        var index = anim.GravesAtFall(i);
        var tx = CapSea.FossilColumn(l, index, ctx);
        var ground = CapSea.Relief(l, ctx, tx, columns);
        var ty = ground - (c.PictureSize.H / 2) + 3;

        var walk = Math.Clamp((s - 0.6) / (SinkSeconds - 1.0), 0, 1);
        var ease = walk * walk * (3 - (2 * walk));

        if (s < SinkSeconds)
        {
            // The trail: bubbles left along the path, each a little behind the one before it.
            for (var q = 1; q <= 9; q++)
            {
                var back = Math.Clamp(walk - (q * 0.025), 0, 1);
                var e = back * back * (3 - (2 * back));
                var bx = sx + (int)((tx - sx) * e) + (int)Math.Round(Math.Sin((s * 2.4) - (q * 0.5)) * 4 * (1 - back));
                var by = sy + (int)((ty - sy) * e) - (q * 2);
                p.Set(bx, by, pal.Snow, (int)(180 * (1 - (q / 10.0)) * (walk > 0 ? 1 : 0)));
            }

            var sway = (int)Math.Round(Math.Sin(s * 2.4) * 4 * (1 - walk));
            c.PaintPicture(p, ((int)((tx - sx) * ease)) + sway, (int)((ty - sy) * ease), pal.Ink, 0.5 + (0.5 * Math.Sin(s * 3)), 1.0 - (0.35 * walk), 0xC8D8FFu);
        }

        // The landing: a cloud of sediment going up, and a ring of light running out along the floor from the place.
        var landed = s - (SinkSeconds - 0.4);
        if (landed >= 0 && landed < 2.2)
        {
            var k = landed / 2.2;
            for (var d = 0; d < 26; d++)
            {
                var side = ((CapArt.Hash(d, 81) % 100) - 50) / 50.0;
                var rise = (CapArt.Hash(d, 82) % 100) / 100.0;
                var x = tx + (int)(side * (4 + (k * 24)));
                var y = ground - (int)((rise * 14 * k) + (k * 6)) - 1;
                if (((x + y) & 1) == 0) p.Set(x, y, pal.Floor[0], (int)(190 * (1 - k)));
            }

            var reach = k * 40;
            for (var side = -1; side <= 1; side += 2)
            {
                var x = tx + (int)(side * reach);
                p.Set(x, CapSea.Relief(l, ctx, x, columns) - 1, 0xFFF0B0, (int)(220 * (1 - k)));
                p.Set(x, CapSea.Relief(l, ctx, x, columns) - 2, 0xFFD860, (int)(120 * (1 - k)));
            }
        }

        // Pale on the floor, from landing until the run has the fossil (a grave more than there were when it fell), or for a minute at most.
        if (s >= SinkSeconds && s < 60 && gravesNow <= index)
        {
            c.PaintPicture(p, tx - sx, ty - sy, pal.Ink, 0.1, 0.55);
        }
    }
}
