namespace PermaLocke.App.Views;

/// <summary>
/// What lives on the sand besides the kelp (2026-10-15), all of it out of the Pokémon world: the pink coral of Corsola with its pale tips, a
/// purple fan of coral, a Staryu lying on the sand with its red core pulsing, a Shellder that opens and closes its shell on a pearl, and a Poké
/// Ball half buried. Each sits in its own slot of the one sea (counted from the seed and the world column, so the two windows agree about what
/// grows where), keeps clear of the fossils and of the sign, and lets a few bubbles go.
/// </summary>
public static class CapReefLife
{
    private const int Slot = 19;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, int[] fossilColumns, (int Left, int Right)? clear, double phase)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip) return;

        var party = l.Panel == CapPanelKind.Party;
        var first = (int)Math.Floor((ctx.WorldX - 8) / (double)Slot);
        var last = (int)Math.Floor((ctx.WorldX + l.Cols + 8) / (double)Slot);
        for (var s = first; s <= last; s++)
        {
            var x = (s * Slot) + 4 + (CapArt.Hash(ctx.Seed + 53, s) % (Slot - 8)) - ctx.WorldX;
            if (x < 5 || x > l.Cols - 6) continue;
            if (fossilColumns.Any(f => Math.Abs(f - x) < 12)) continue;
            if (clear is { } c && x > c.Left - 7 && x < c.Right + 7) continue;

            var roll = CapArt.Hash(ctx.Seed + 911, s) % 100;
            var seed = CapArt.Hash(ctx.Seed + 17, s);
            var ground = CapSea.Relief(l, ctx, x, fossilColumns);
            var flip = (seed & 1) == 0;
            uint Tint(uint colour) => CapSheet.Mix(colour, pal.Water[4], 0.16);

            if (roll < 36)
            {
                var height = (party ? 10 : 13) + (seed % (party ? 4 : 7));
                Corsola(p, Tint, x, ground, height, seed, phase, flip);
                // a colony: most of them have a younger one beside them
                var mate = x + (flip ? -7 : 7);
                if (seed % 3 != 0 && mate > 5 && mate < l.Cols - 6 && !fossilColumns.Any(f => Math.Abs(f - mate) < 10)
                    && !(clear is { } k && mate > k.Left - 5 && mate < k.Right + 5))
                {
                    Corsola(p, Tint, mate, CapSea.Relief(l, ctx, mate, fossilColumns), (height * 2 / 3) + 1, seed + 5, phase, !flip);
                }
            }
            else if (roll < 56) Fan(p, Tint, x, ground, (party ? 6 : 7) + (seed % 3), seed, phase);
            else if (roll < 68) Staryu(p, Tint, x, ground, flip, phase, seed);
            else if (roll < 80) Shellder(p, Tint, x, ground, phase, seed);
            else if (roll < 90) PokeBall(p, Tint, x, ground, phase, seed);
            else continue;

            if (roll >= 36 && roll < 56) continue;
            Bubbles(p, x, ground - (roll < 36 ? 12 : roll < 68 ? 8 : 6), phase, seed);
        }
    }

    /// <summary>Corsola's coral: a short thick trunk, pink, with horns that fork upward ending in pale tips; it sways with the current.</summary>
    private static void Corsola(CapSheet p, Func<uint, uint> tint, int x, int ground, int height, int seed, double phase, bool flip)
    {
        uint dark = tint(0x8A3558u), shade = tint(0xB04870u), pink = tint(0xE46A92u), light = tint(0xFF9EBAu), tip = tint(0xFFE6EFu);
        var trunk = Math.Max(4, (height / 2) + 1);
        var sway = Math.Sin((phase * 0.7) + seed);

        int Lean(int j) => (int)Math.Round(sway * 0.8 * j / Math.Max(1, height));

        for (var j = 0; j < trunk; j++)
        {
            var sx = x + Lean(j);
            var y = ground - 1 - j;
            p.Set(sx - 1, y, light);
            p.Set(sx, y, pink);
            p.Set(sx + 1, y, shade);
            if (j < 2) p.Set(sx + 2, y, dark);
        }

        var count = 3 + (seed % 2);
        for (var b = 0; b < count; b++)
        {
            var side = (b % 2 == 0 ? -1 : 1) * (flip ? -1 : 1);
            var startJ = 2 + (b * Math.Max(1, trunk - 3) / count);
            var length = 3 + ((seed >> (b * 3)) & 3) + (height / 6);
            var bx = x + Lean(startJ);
            var by = ground - 1 - startJ;
            var lastX = bx;
            var lastY = by;
            for (var i = 1; i <= length; i++)
            {
                var px = bx + (side * (int)Math.Round(i * 0.85)) + Lean(startJ + i);
                var py = by - (int)Math.Round((i * 1.1) + (i * i * 0.05));
                var tipCell = i >= length - 1;
                var colour = tipCell ? tip : i > length / 2 ? light : pink;
                p.Set(px, py, colour);
                if (i <= length / 2 + 1) p.Set(px + 1, py, i == 1 ? shade : pink);
                if (i > 1 && !tipCell) p.Set(px - side, py + 1, shade);
                lastX = px;
                lastY = py;
            }

            p.Set(lastX, lastY - 1, tip);
        }

        // the trunk's own top: a little cluster of pale tips
        var topX = x + Lean(trunk);
        var topY = ground - trunk;
        p.Set(topX, topY - 1, tip);
        p.Set(topX - 1, topY, light);
        p.Set(topX + 1, topY, light);
    }

    /// <summary>A purple fan of coral: a short stem and a half disc of ribs and netting that sways a little.</summary>
    private static void Fan(CapSheet p, Func<uint, uint> tint, int x, int ground, int radius, int seed, double phase)
    {
        uint dark = tint(0x5A3A86u), mid = tint(0x8E5FC0u), light = tint(0xC09AE6u), rim = tint(0xE6D4FAu);
        var sway = Math.Sin((phase * 0.6) + seed);
        for (var j = 0; j < 3; j++) p.Set(x, ground - 1 - j, dark);

        var centreY = ground - 3;
        for (var dy = -radius; dy <= 0; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > radius + 0.2 || (dx == 0 && dy == 0)) continue;
                var angle = Math.Atan2(-dy, dx);
                var onRib = Math.Abs(Math.Sin(angle * 5.5)) < 0.2;
                if (!onRib && ((dx + dy) & 1) != 0 && d > radius * 0.4) continue;
                var colour = d > radius - 1.2 ? rim : onRib ? light : d < radius * 0.5 ? dark : mid;
                p.Set(x + dx + (int)Math.Round(sway * (-dy) / (double)radius), centreY + dy, colour);
            }
        }
    }

    private static readonly string[] StarBitmap =
    [
        "....l....",
        "...lbd...",
        "llbbbbbdd",
        ".lbbgbbd.",
        "..bbbbd..",
        ".bbd.bbd.",
        ".bd...bd."
    ];

    /// <summary>A Staryu lying on the sand, its core a red jewel that pulses.</summary>
    private static void Staryu(CapSheet p, Func<uint, uint> tint, int x, int ground, bool flip, double phase, int seed)
    {
        uint body = tint(0xC8A040u), light = tint(0xE8C868u), dark = tint(0x8A6A28u);
        var pulse = 0.5 + (0.5 * Math.Sin((phase * 1.9) + seed));
        for (var r = 0; r < StarBitmap.Length; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var ch = StarBitmap[r][flip ? 8 - c : c];
                if (ch == '.') continue;
                var colour = ch switch { 'l' => light, 'd' => dark, 'g' => CapSheet.Mix(0xB02030u, 0xFF8090u, pulse), _ => body };
                p.Set(x - 4 + c, ground - StarBitmap.Length + r + 1, colour);
            }
        }

        p.Set(x - 1 + (flip ? 1 : 0), ground - 4, 0xFFFFFFu, (int)(110 + (110 * pulse)));
    }

    /// <summary>A Shellder: a purple shell with ridges that opens on a pearl and closes again, slowly.</summary>
    private static void Shellder(CapSheet p, Func<uint, uint> tint, int x, int ground, double phase, int seed)
    {
        uint body = tint(0x8E5FB5u), ridge = tint(0x5E3A86u), top = tint(0xB892DCu), inside = tint(0xE8A0B8u);
        var open = Math.Clamp(0.5 + (0.9 * Math.Sin((phase * 0.45) + seed)), 0, 1);
        var gap = (int)Math.Round(open * 4);
        var hinge = ground - 1;

        // the lower shell: a low dome on the sand
        for (var dx = -5; dx <= 5; dx++)
        {
            var h = (int)Math.Round(2.6 * Math.Sqrt(Math.Max(0, 1 - Math.Pow(dx / 5.4, 2))));
            for (var j = 0; j < h; j++) p.Set(x + dx, hinge - j, j == h - 1 ? top : Math.Abs(dx) % 3 == 0 ? ridge : body);
        }

        // between the shells: the soft inside and the pearl
        if (gap >= 2)
        {
            for (var dx = -3; dx <= 3; dx++) for (var j = 1; j < gap; j++) p.Set(x + dx, hinge - 2 - j + 1, inside);
            p.Set(x, hinge - 3, 0xFFFFFFu);
            p.Set(x + 1, hinge - 3, 0xF4E4F0u);
            p.Set(x, hinge - 4, 0xF4E4F0u);
            p.Set(x - 1, hinge - 3, 0xDCC8E0u);
        }

        // the upper shell, lifted by the gap
        for (var dx = -5; dx <= 5; dx++)
        {
            var h = (int)Math.Round(3.2 * Math.Sqrt(Math.Max(0, 1 - Math.Pow(dx / 5.4, 2))));
            for (var j = 0; j < h; j++) p.Set(x + dx, hinge - 2 - gap - j, j == h - 1 ? top : Math.Abs(dx) % 3 == 0 ? ridge : body);
        }
    }

    /// <summary>A Poké Ball half buried in the sand: red over white, the band, the button that now and then catches the light.</summary>
    private static void PokeBall(CapSheet p, Func<uint, uint> tint, int x, int ground, double phase, int seed)
    {
        uint red = tint(0xE03040u), redLight = tint(0xFF7080u), white = tint(0xE8E8F0u), shade = tint(0xB0B0C4u), black = tint(0x1A1A24u);
        var cy = ground - 3;
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                var y = cy + dy;
                if (d > 4.3 || y >= ground) continue;
                uint colour;
                if (d > 3.4) colour = black;
                else if (Math.Abs(dy) <= 0) colour = black;
                else if (dy < 0) colour = dx < -1 && dy < -1 ? redLight : red;
                else colour = dx > 1 ? shade : white;
                p.Set(x + dx, y, colour);
            }
        }

        // the button: a ring round a pale centre that glints for a moment every few seconds
        var glint = Math.Sin((phase * 0.8) + seed) > 0.93;
        p.Set(x, cy, glint ? 0xFFF8C0u : white);
        p.Set(x - 1, cy, black);
        p.Set(x + 1, cy, black);
    }

    /// <summary>A couple of bubbles that let go of something on the sand and rise a little.</summary>
    private static void Bubbles(CapSheet p, int x, int y, double phase, int seed)
    {
        for (var k = 0; k < 2; k++)
        {
            var age = ((phase * 0.22) + (k * 0.5) + ((seed % 7) * 0.14)) % 1.0;
            var bx = x + (int)Math.Round(Math.Sin((phase * 1.2) + (k * 2.1) + seed) * 1.6);
            var by = y - (int)(age * 14);
            var alpha = (int)(175 * Math.Sin(Math.PI * age));
            if (alpha <= 10) continue;
            p.Set(bx, by, 0xD8F4FFu, alpha);
            p.Set(bx, by - 1, 0xFFFFFFu, alpha / 2);
        }
    }
}
