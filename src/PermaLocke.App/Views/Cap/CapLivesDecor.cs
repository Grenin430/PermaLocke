namespace PermaLocke.App.Views;

/// <summary>
/// The lives of the run in the overlay (2026-10-29): <paramref name="Total"/> it started with, <paramref name="Left"/> of them whole. When some
/// are lost, <paramref name="LostAt"/> is the moment (on the panel's clock) and <paramref name="From"/> how many there were before: the ones from
/// <paramref name="Left"/> up to <paramref name="From"/> go one after another, the rightmost first. <paramref name="LossAt"/> is when the loss
/// of the Locke begins (CapLockLoss), infinity while there are lives.
/// </summary>
public sealed record CapLivesState(int Total, int Left, double LostAt = double.NegativeInfinity, int From = 0, double LossAt = double.PositiveInfinity);

/// <summary>
/// The lives in the overlay (2026-10-30, after many prototypes): a Heal Ball for every life lying in the sand of the party's window, drawn as a
/// real sphere from the player's model; a lost one cracks and stays, for good. Over the cap's sign, a heart and how many are left.
/// </summary>
public static class CapLivesDecor
{
    private enum Fate { Alive, Flashing, Breaking, Gone }

    private static (Fate Fate, double Since) Of(CapLivesState lives, int i, double t)
    {
        if (i < lives.Left) return (Fate.Alive, 0);
        var since = i < lives.From ? t - lives.LostAt - ((lives.From - 1 - i) * HealStagger) : double.NegativeInfinity;
        if (since < 0 && i < lives.From && t >= lives.LostAt) return (Fate.Alive, 0);
        if (since is >= 0 and < 0.15) return (Fate.Flashing, since);
        if (since >= 0.15 && since < 0.15 + HealBreak) return (Fate.Breaking, since - 0.15);
        return (Fate.Gone, 0);
    }

    /// <summary>The Heal Balls, in the party's window of the tall band; <paramref name="items"/> is the stretch of sand the overlay's items take.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapLivesState lives, double t, Func<int, int> relief, (int From, int To) items = default)
    {
        if (l.Panel != CapPanelKind.Party || l.Mode is not (CapMode.Column or CapMode.Slim) || lives.Total <= 0) return;
        HealBalls(p, l, lives, t, relief, items.From, items.To);
    }

    private const uint DeepWater = 0x123E6Au;

    private static uint Fog(uint colour, double fog) => fog > 0 ? CapSheet.Mix(colour, DeepWater, fog) : colour;

    // ================================================================ the heart, drawn at any size

    // Tones: 0 ink, 1 shadow, 2 body, 3 light, 4 glint, 5 the light the sea throws back on the lower right edge.
    private static readonly uint[] Ruby = [0x3A0614u, 0x8E1430u, 0xD0283Eu, 0xF2566Au, 0xFFE4EAu, 0xC2456Au];
    private static readonly uint[] Dead = [0x0C0C14u, 0x1C1C28u, 0x2A2A38u, 0x383848u, 0x56566Au, 0x30304Au];

    /// <summary>A heart <see cref="W"/> by <see cref="H"/> cells (its outline included): the tone of each cell, -1 where it is empty.</summary>
    private sealed record HeartArt(int W, int H, sbyte[] T);

    private static readonly Dictionary<int, HeartArt> Arts = [];

    /// <summary>
    /// The heart of a width, from the curve (x² + y² − 1)³ = x²y³ sampled cell by cell: an ink outline round it, the light from the upper left
    /// (lighter the further a cell is from the edge, like a rounded thing), a shadow along its lower right edge with a little light thrown back
    /// from the water, and a glint on the left lobe.
    /// </summary>
    private static HeartArt Art(int w)
    {
        if (Arts.TryGetValue(w, out var made)) return made;

        var h = Math.Max(5, (int)Math.Round(w * 0.9));
        var cw = w + 2;
        var ch = h + 2;
        var inside = new bool[cw * ch];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var hx = (((x + 0.5) / w * 2) - 1) * 1.16;
                var hy = 1.24 - ((y + 0.5) / h * 2.26);
                var q = (hx * hx) + (hy * hy) - 1;
                if ((q * q * q) - (hx * hx * hy * hy * hy) <= 0) inside[((y + 1) * cw) + x + 1] = true;
            }
        }

        // how many steps each cell is from the outside
        var depth = new int[cw * ch];
        var queue = new Queue<int>();
        for (var i = 0; i < depth.Length; i++)
        {
            if (inside[i]) depth[i] = int.MaxValue;
            else queue.Enqueue(i);
        }

        var deepest = 1;
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            foreach (var n in new[] { i - 1, i + 1, i - cw, i + cw })
            {
                if (n < 0 || n >= depth.Length || depth[n] <= depth[i] + 1) continue;
                depth[n] = depth[i] + 1;
                deepest = Math.Max(deepest, depth[n]);
                queue.Enqueue(n);
            }
        }

        var tones = new sbyte[cw * ch];
        for (var y = 0; y < ch; y++)
        {
            for (var x = 0; x < cw; x++)
            {
                var i = (y * cw) + x;
                tones[i] = -1;
                if (!inside[i])
                {
                    if ((x > 0 && inside[i - 1]) || (x < cw - 1 && inside[i + 1]) || (y > 0 && inside[i - cw]) || (y < ch - 1 && inside[i + cw])) tones[i] = 0;
                    continue;
                }

                var u = (((x - 1 + 0.5) / w) * 2) - 1;
                var v = 1 - (((y - 1 + 0.5) / h) * 2);
                var light = (0.42 * depth[i] / deepest) + (0.32 * (v - u) * 0.5) + 0.33;
                sbyte tone = light < 0.42 ? (sbyte)1 : light < 0.62 ? (sbyte)2 : (sbyte)3;
                if (depth[i] == 1 && u - v > 0.15) tone = 1;
                if (depth[i] == 1 && u > 0.3 && v is < 0.1 and > -0.6) tone = 5;
                var gx = (u + 0.52) / 0.2;
                var gy = (v - 0.55) / 0.16;
                if ((gx * gx) + (gy * gy) <= 1) tone = 4;
                if (w >= 13 && Math.Abs(u + 0.18) < 0.07 && Math.Abs(v - 0.7) < 0.07) tone = 4;
                tones[i] = tone;
            }
        }

        made = new HeartArt(cw, ch, tones);
        Arts[w] = made;
        return made;
    }

    /// <summary>The heart's cells in a palette. <paramref name="tint"/> may change a cell (column, row, tone, colour); <paramref name="shift"/> moves a row sideways.</summary>
    private static void Sprite(CapSheet p, HeartArt a, int x, int y, uint[] pal, Func<int, int, int, uint, uint>? tint = null, Func<int, int>? shift = null,
        bool facets = false, int alpha = 255)
    {
        for (var j = 0; j < a.H; j++)
        {
            var dx = shift?.Invoke(j) ?? 0;
            for (var k = 0; k < a.W; k++)
            {
                int tone = a.T[(j * a.W) + k];
                if (tone < 0) continue;
                // a crystal is cut in facets: thin lighter planes running down to the right
                if (facets && tone is 2 or 3 && (k + (2 * j)) % 6 == 0) tone++;
                if (facets && tone == 2 && (k - j + 40) % 7 == 0) tone = 1;
                var c = pal[tone];
                if (tint is not null) c = tint(k, j, tone, c);
                p.Set(x + k + dx, y + j, c, alpha);
            }
        }
    }

    // ================================================================ 22: Heal Balls in the sand

    /// <summary>How long a Heal Ball takes to break (it shakes, charges, bursts, its lid lands), and the wait between two that break together.</summary>
    public const double HealBreak = 2.0, HealStagger = 0.5;

    /// <summary>
    /// A Heal Ball for every life in the sand of the party's window, every one of them in sight: the biggest size (12 at most) at which they all fit
    /// without covering each other, the first row not rising over the bottom of the party's writing and the last not going out of the window;
    /// the extra rows lie further down the sand, nearer. The stretch where the overlay's items stand (the Pokévial, the Incubadora Turbo, the
    /// Repelente Infinito, from <paramref name="itemsFrom"/> to <paramref name="itemsTo"/>) is left free: the balls go either side of it.
    /// </summary>
    private static void HealBalls(CapSheet p, CapWorldLayout l, CapLivesState lives, double t, Func<int, int> relief,
        int itemsFrom = 0, int itemsTo = 0)
    {
        // the floor takes the whole width of the window, the ear under the notch included (the cap's sign stands there)
        var x0 = 4;
        var x1 = l.Main.Left + l.Main.Width - 6;
        var ground0 = relief((x0 + x1) / 2) + 2;

        // the sand there is to lie on: one stretch, or two either side of the items
        var stretches = itemsTo > itemsFrom
            ? new[] { (From: x0, To: Math.Max(x0, itemsFrom)), (From: Math.Min(x1, itemsTo), To: x1) }
            : [(From: x0, To: x1)];

        int Room(int r) => stretches.Sum(s => Math.Max(0, s.To - s.From) / ((2 * r) + 2));

        // a big ball sits lower in the sand rather than rising into the writing: its top never goes over the party's last row
        int Seat(int ground, int r) => Math.Max(ground, l.FloorTop - 4 + (2 * r));

        var (radius, rows) = (3, 1);
        for (var r = 12; r >= 3; r--)
        {
            var fits = Math.Max(1, Room(r));
            var n = (lives.Total + fits - 1) / fits;
            var bottom = Seat(ground0, r) + 3 + ((n - 1) * 2 * r);
            (radius, rows) = (r, n);
            if (bottom <= l.Rows - 2) break;
        }

        var perRow = (lives.Total + rows - 1) / rows;
        var room = Math.Max(1, Room(radius));
        var i = 0;
        for (var row = 0; row < rows; row++)
        {
            var inRow = Math.Min(perRow, lives.Total - (row * perRow));
            var placed = 0;
            for (var s = 0; s < stretches.Length; s++)
            {
                var (from, to) = stretches[s];
                var cap = Math.Max(0, to - from) / ((2 * radius) + 2);
                // each stretch takes its share of the row, the last whatever is left
                var count = s == stretches.Length - 1 ? inRow - placed : Math.Min(cap, (int)Math.Round(inRow * cap / (double)room));
                if (count <= 0) continue;
                var pitch = (to - from) / (double)count;
                var slack = Math.Max(0, pitch - ((2 * radius) + 2));
                for (var c = 0; c < count; c++, i++, placed++)
                {
                    var (fate, since) = Of(lives, i, t);
                    var jitter = (int)((((CapArt.Hash(i, 17) % 100) / 100.0) - 0.5) * slack);
                    var cx = from + (int)(((c + 0.5) * pitch) + (row % 2 == 1 && count < cap ? pitch / 2 : 0)) + jitter;
                    cx = Math.Clamp(cx, from + radius, to - radius);
                    // the extra rows lie further down the sand, nearer: nothing goes above the first, so no ball ends behind the party's writing
                    var ground = Seat(relief(cx) + 2, radius) + (row * 2 * radius);
                    var tilt = (((CapArt.Hash(i, 23) % 100) / 100.0) - 0.5) * 1.1;
                    HealBall(p, cx, ground - radius + 2, radius, tilt, fate == Fate.Flashing ? Fate.Alive : fate, since, i, t, 0);
                }
            }
        }
    }

    /// <summary>
    /// Over the cap's sign: a heart and how many lives are left, «×30». When one is lost the number jumps and flashes, and the heart beats faster
    /// the fewer there are.
    /// </summary>
    public static void Counter(CapSheet p, CapLivesState lives, double t, CapRect board)
    {
        var a = Art(9);
        var text = $"x{lives.Left}";
        var textW = CapGlyphs.Width(text, true);
        var w = a.W + 2 + textW;
        var x = board.Left + ((board.Width - w) / 2);
        var y = board.Top - a.H - 3;
        var sinceLoss = t - lives.LostAt;
        var jump = sinceLoss is >= 0 and < 0.5 ? (int)Math.Round(-3 * Math.Sin(sinceLoss / 0.5 * Math.PI)) : 0;
        var rate = 1.2 + (2.5 * (1 - (lives.Left / (double)Math.Max(1, lives.Total))));
        var beat = Math.Max(0, Math.Sin(t * rate * Math.PI * 2));
        if (lives.Left > 0) p.Disc(x + (a.W / 2.0), y + (a.H / 2.0), (a.W * 0.6) + (beat * 1.5), 0xFF4060u, (int)(30 + (40 * beat)));
        Sprite(p, a, x, y - (int)Math.Round(beat), lives.Left > 0 ? Ruby : Dead);
        var colour = sinceLoss is >= 0 and < 0.5 ? 0xFFFFFFu : lives.Left > 0 ? 0xFFE0E8u : 0x9A9AA8u;
        CapGlyphs.Outlined(p, text, x + a.W + 2, y + 1 + jump, colour, 0x1A0F08u, true);
    }

    // the Heal Ball's colours, as three tones each (lit, body, shadow), from the model
    private static readonly uint[] HealPink = [0xFFB0D8u, 0xF07AB8u, 0xD8288Cu];
    private static readonly uint[] HealCream = [0xFFFFE0u, 0xFFF4B0u, 0xE8C878u];
    private static readonly uint[] HealBlue = [0x48B8E8u, 0x2090C8u, 0x0E5E96u];
    private static readonly uint[] HealPeach = [0xFFE4D4u, 0xFFC4ACu, 0xE88A84u];
    private static readonly uint[] HealButton = [0x9CC0FFu, 0x6E9CF0u, 0x3E64C0u];

    /// <summary>
    /// A Heal Ball drawn as a sphere (2026-10-30, from the model the player gave): every cell of the disc is a point on the ball, turned by its tilt
    /// and its roll, and takes the colour of the model's band at that latitude, lit from the upper left in three tones, with a glint and a dark rim.
    /// A lost one stays where it was, for good, cracked: its button goes out, a crack runs across it in four jumps (a snap of light at its tip and
    /// a chip falling at each) and its colours lose their strength.
    /// </summary>
    private static void HealBall(CapSheet p, int cx, int cy, int r, double tilt, Fate fate, double since, int seed, double t, double fog)
    {

        var breaking = fate == Fate.Breaking;
        var gone = fate == Fate.Gone;
        var u = gone ? 99 : breaking ? since : -1;

        // how far its colours have faded, 0 to 1
        var k = u < 0 ? 0 : CapDeath.Clamp01(u / 1.6);
        var buttonLit = u < 0 ? 1 : CapDeath.Clamp01(1 - (u / 0.4));

        // the crack: four jumps, at these moments, each reaching further along its path
        double[] jumps = [0.25, 0.55, 0.85, 1.15];
        var reached = 0;
        var sinceJump = 99.0;
        foreach (var at in jumps)
        {
            if (u >= at)
            {
                reached++;
                sinceJump = u - at;
            }
        }

        var jitter = u is >= 0 and < 1.3 && sinceJump < 0.08 ? (int)Math.Round(Math.Sin(u * 90)) : 0;
        var bx0 = cx + jitter;
        var by0 = cy;

        var roll = fate == Fate.Alive ? 0.35 * Math.Sin((t * 0.8) + seed) : 0.2;
        var spin = ((((CapArt.Hash(seed, 31) % 100) / 100.0) - 0.5) * 1.2) + roll;
        var (ct, st) = (Math.Cos(tilt), Math.Sin(tilt));
        var (cs, ss) = (Math.Cos(spin), Math.Sin(spin));

        // its shadow on the sand
        for (var x = -r; x <= r; x++) p.Set(cx + x + 1, cy + r + 1, 0x101820u, 60);

        const double lx = -0.5, ly = -0.62, lz = 0.6;
        for (var y = -r; y <= r; y++)
        {
            for (var x = -r; x <= r; x++)
            {
                var px = (x + 0.5) / (r + 0.5);
                var py = (y + 0.5) / (r + 0.5);
                var d2 = (px * px) + (py * py);
                if (d2 > 1) continue;
                var pz = Math.Sqrt(1 - d2);

                uint c;
                var rim = d2 > 0.8;
                if (rim)
                {
                    c = 0x2A1420u;
                }
                else
                {
                    var ox = (px * ct) + (py * st);
                    var oy = (-px * st) + (py * ct);
                    var oz = pz;
                    var bx = (ox * cs) - (oz * ss);
                    var bz = (ox * ss) + (oz * cs);
                    var lat = -oy;
                    var lon = Math.Atan2(bx, bz);

                    var cream = 0.36 + (0.07 * Math.Sin((3 * lon) + 0.6));
                    var blue = -0.05 + (0.06 * Math.Sin((2 * lon) + 1.2));
                    var ramp = lat > cream + 0.13 ? HealPink
                        : lat > cream - 0.06 ? HealCream
                        : lat > blue + 0.11 ? HealPink
                        : lat > blue - 0.09 ? HealBlue
                        : HealPeach;

                    var toFront = Math.Sqrt((bx * bx) + (oy * oy));
                    var button = bz > 0 && toFront < 0.34;
                    if (button) ramp = HealButton;

                    var light = Math.Max(0, (px * lx) + (py * ly) + (pz * lz));
                    var tone = light > 0.62 ? 0 : light > 0.28 ? 1 : 2;
                    c = ramp[tone];
                    if (button && toFront < 0.17) c = CapSheet.Mix(0x10102Au, bx * oy > 0 ? 0x1414B8u : 0x6E8090u, buttonLit);
                    if (button && Math.Abs(toFront - 0.34) < 0.05) c = 0x1E2C70u;

                    var spec = light - 0.9;
                    if (spec > 0) c = CapSheet.Mix(c, 0xFFFFFFu, Math.Min(1, spec * 9) * (1 - k));
                }

                // the colours lose their strength: towards their own grey and a little darker, never all the way
                if (!rim && k > 0) c = Faded(c, k * 0.6);
                p.Set(bx0 + x, by0 + y, Fog(c, fog));
            }
        }

        if (reached > 0) Crack(p, bx0, by0, r, seed, reached, sinceJump, u, gone);

        // a sparkle now and then on a living one
        if (fate == Fate.Alive && ((t * 0.7) + (seed * 0.37)) % 3.0 < 0.18)
        {
            p.Set(cx - (r / 2), cy - (r / 2), 0xFFFFFFu);
            p.Set(cx - (r / 2) - 1, cy - (r / 2), 0xFFE0EEu, 160);
            p.Set(cx - (r / 2), cy - (r / 2) - 1, 0xFFE0EEu, 160);
        }

        // the sand heaped against its foot
        for (var x = -r - 1; x <= r + 1; x++) p.Set(cx + x, cy + r - 1 + (Math.Abs(x) > r - 1 ? 0 : 1), Fog(0xC8B880u, fog), 200);
    }

    /// <summary>A colour losing its strength: towards its own grey, and a little darker, by <paramref name="k"/>.</summary>
    private static uint Faded(uint c, double k)
    {
        var luma = (0.3 * ((c >> 16) & 255)) + (0.59 * ((c >> 8) & 255)) + (0.11 * (c & 255));
        var grey = CapSheet.Rgb((int)(luma * 0.92), (int)(luma * 0.92), (int)Math.Min(255, luma * 1.0));
        return CapSheet.Mix(CapSheet.Mix(c, grey, k), 0x000000u, k * 0.18);
    }

    /// <summary>
    /// The crack across a lost Heal Ball: a jagged path from one side of its rim towards the other, with a short branch, reaching further at each
    /// of its jumps; dark, with a lit lip under it so it reads as cut into the shell; a snap of light at its tip and a chip falling at each jump.
    /// </summary>
    private static void Crack(CapSheet p, int cx, int cy, int r, int seed, int reached, double sinceJump, double u, bool gone)
    {
        // the path, the same for the same ball: from the upper left of the rim, zigzagging down to the right
        var path = new List<(int X, int Y)>();
        var start = -2.3 + ((CapArt.Hash(seed, 3) % 100) / 100.0 * 0.6);
        double x = Math.Cos(start) * (r - 1);
        double y = Math.Sin(start) * (r - 1);
        var heading = start + Math.PI + 0.25;
        for (var q = 0; q < (2 * r) + 2; q++)
        {
            path.Add(((int)Math.Round(x), (int)Math.Round(y)));
            var zig = (CapArt.Hash(seed * 7, q) % 3) - 1;
            heading += zig * 0.55;
            x += Math.Cos(heading);
            y += Math.Sin(heading);
            if ((x * x) + (y * y) > (r - 1) * (r - 1)) break;
        }

        var shown = (int)Math.Ceiling(path.Count * Math.Min(4, reached) / 4.0);
        for (var q = 0; q < shown; q++)
        {
            var (px, py) = path[q];
            p.Set(cx + px, cy + py, 0x1E0E16u);
            p.Set(cx + px, cy + py + 1, 0xFFE8F0u, 110);
        }

        // a short branch off its middle, once the crack has passed it
        if (shown > path.Count / 2 && path.Count > 4)
        {
            var (bx, by) = path[path.Count / 2];
            for (var q = 1; q <= Math.Max(2, r / 3); q++) p.Set(cx + bx + q, cy + by - q + (q / 2), 0x1E0E16u);
        }

        if (gone || shown == 0 || sinceJump > 0.25) return;

        // the snap of light at its tip, and a chip falling away
        var (tx, ty) = path[shown - 1];
        var a = (int)(255 * (1 - (sinceJump / 0.25)));
        p.Set(cx + tx, cy + ty, 0xFFFFFFu, a);
        p.Set(cx + tx - 1, cy + ty, 0xFFF0F6u, a / 2);
        p.Set(cx + tx + 1, cy + ty, 0xFFF0F6u, a / 2);
        p.Set(cx + tx, cy + ty - 1, 0xFFF0F6u, a / 2);
        p.Set(cx + tx, cy + ty + 1, 0xFFF0F6u, a / 2);
        var fall = sinceJump / 0.25;
        p.Set(cx + tx + 1 + (int)(fall * 2), cy + ty + (int)(fall * fall * 6), 0xF07AB8u, a);
    }
}
