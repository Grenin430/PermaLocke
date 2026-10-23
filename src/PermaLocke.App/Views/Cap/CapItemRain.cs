using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>An item the player has just got (2026-10-20): its icon, when the wormhole spits it out on the panel's clock, and a seed for its throw.</summary>
public sealed record CapItemDrop(BitmapSource? Icon, double Spit, int Seed);

/// <summary>
/// Items got in the game, in the abyss's window (2026-10-20, in place of the animation of the item going into the bag over the game). An Ultra
/// Wormhole seen from the front, after the player's reference picture (its shape) and the party's wormhole (its colours and light), stays for
/// good in the sky beside the tower: a blinding white core with a pale cyan rim on its right and its bloom; one soft glow round it, violet
/// outside, magenta nearer and pale pink by the core, a little bluer on the right and warmer on the left; a net of spokes out of the core and two close rings near the disc's edge; long
/// thick jagged white bolts bursting out all round with a lilac glow, crackling; a blue glow fading into the sky. It spits each item out
/// towards us (the icon growing as it comes, the core swelling); the item arcs down, falls into the sea with a splash, sinks swaying and
/// slowing with bubbles rising from it, comes to rest on the seabed and sinks into the sand in a puff, gone. Several items come one after
/// another. No letters: the abyss writes none.
/// </summary>
public static class CapItemRain
{
    /// <summary>How long after the item is got the wormhole spits it out.</summary>
    public const double Open = 0.4;

    /// <summary>How long an item is on the panel from its spit: flight, sinking, rest and burial all fit in it.</summary>
    public const double Life = 7.5;

    /// <summary>The core's radius: the rest is measured from it as in the reference (2026-10-20, a third bigger than the first try; drawn at <see cref="Size"/>).</summary>
    private const double CoreR = 9.5;

    /// <summary>The scale it is drawn at (2026-10-20: a fifth smaller than the approved one, the same picture).</summary>
    private const double Size = 0.8;

    private const double Gravity = 95, Terminal = 55, Drag = 0.28, Scale = 0.75;

    private static readonly uint[] SandPal = [0x9A8058u, 0xBCA174u, 0xD8C08Eu, 0xEEDDB2u];

    /// <summary>One bolt of the wormhole's burst: its points out from the middle, and whether it is a twig off another.</summary>
    private sealed record Bolt((double X, double Y)[] Points, bool Twig);

    /// <summary>The burst, made once: long jagged bolts all round the core, of uneven lengths as in the reference, some with a branch.</summary>
    private static readonly Bolt[] Bolts = MakeBolts();

    private static Bolt[] MakeBolts()
    {
        var bolts = new List<Bolt>();
        uint seed = 1913;
        double Rnd()
        {
            seed = (uint)CapArt.Hash((int)seed, 91);
            return seed % 10000 / 10000.0;
        }

        const int rays = 16;
        for (var i = 0; i < rays; i++)
        {
            var a = (i / (double)rays * 2 * Math.PI) + ((Rnd() - 0.5) * 0.3);
            var end = CoreR * (2.3 + (Rnd() * 1.0));
            var dist = CoreR * (1.05 + (Rnd() * 0.15));
            var points = new List<(double X, double Y)> { (Math.Cos(a) * dist, Math.Sin(a) * dist) };
            var side = 0.0;
            while (dist < end - 2)
            {
                dist += 2.2 + (Rnd() * 2.2);
                side = Math.Clamp(side + ((Rnd() - 0.5) * 4.4), -3, 3);
                points.Add(((Math.Cos(a) * dist) - (Math.Sin(a) * side), (Math.Sin(a) * dist) + (Math.Cos(a) * side)));
                // a branch now and then, out to one side
                if (Rnd() < 0.12 && dist > CoreR * 1.6)
                {
                    var fork = a + ((Rnd() < 0.5 ? 1 : -1) * (0.5 + (Rnd() * 0.4)));
                    var (bx, by) = points[^1];
                    var length = 3 + (Rnd() * 3);
                    bolts.Add(new Bolt([(bx, by), (bx + (Math.Cos(fork) * length * 0.5) + ((Rnd() - 0.5) * 1.5), by + (Math.Sin(fork) * length * 0.5)), (bx + (Math.Cos(fork) * length), by + (Math.Sin(fork) * length))], true));
                }
            }

            var tip = a + ((Rnd() - 0.5) * 0.25);
            points.Add((Math.Cos(tip) * end, Math.Sin(tip) * end));
            bolts.Add(new Bolt([.. points], false));
        }

        return [.. bolts];
    }

    /// <summary>Where the small wormhole is: in the sky of the abyss's window, to the right of the tower; null when there is no room.</summary>
    public static (int X, int Y)? WormholeAt(CapWorldLayout l)
    {
        if (l.Panel != CapPanelKind.Deep || l.SurfaceY < 40) return null;
        var x = CapFarSky.PillarTopAt(l) is { } pillar ? pillar.X + 46 : l.Main.Left + 76;
        var y = (int)Math.Round(l.SurfaceY * 0.42);
        return x + 26 >= l.Main.Right || l.InNotch(x + 24, y) ? null : (x, y);
    }

    /// <summary>
    /// The wormhole itself, painted behind what flies across the sky (2026-10-21, asked: it covered the fliers); the items it spits are painted
    /// later, in front, by <see cref="Paint"/>.
    /// </summary>
    public static void PaintWormhole(CapSheet p, CapWorldLayout l, IReadOnlyList<CapItemDrop> drops, double t)
    {
        if (WormholeAt(l) is not { } at) return;

        // there for good (asked 2026-10-20); its core swells as it spits
        var flare = 0.0;
        for (var i = 0; i < drops.Count; i++)
        {
            var since = t - drops[i].Spit;
            if (since is >= -0.15 and < 0.35) flare = Math.Max(flare, since < 0 ? 1 + (since / 0.15) : 1 - (since / 0.35));
        }

        Wormhole(p, l, at.X, at.Y, Size, flare, t);
    }

    /// <summary>The items the wormhole spits, falling into the sea and burying themselves, in front of everything in the sky.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, IReadOnlyList<CapItemDrop> drops, double t)
    {
        if (WormholeAt(l) is not { } at) return;
        for (var i = 0; i < drops.Count; i++)
        {
            var since = t - drops[i].Spit;
            if (since is >= 0 and < Life) Fall(p, l, ctx, drops[i], at, since);
        }
    }

    private static double Smooth(double k)
    {
        k = Math.Clamp(k, 0, 1);
        return k * k * (3 - (2 * k));
    }

    /// <summary>A cell over what is there, faded by <paramref name="alpha"/> (0-255).</summary>
    private static void Put(CapSheet p, CapWorldLayout l, int x, int y, uint c, double alpha)
    {
        var a = (int)Math.Round(Math.Clamp(alpha, 0, 255));
        if (a <= 0 || x < 0 || y < 0 || x >= l.Cols || y >= l.Rows || l.InNotch(x, y)) return;
        p.Set(x, y, c, a);
    }

    /// <summary>A line of cells (Bresenham), each faded by alpha; drawn a cell either side as a glow when <paramref name="glow"/>.</summary>
    private static void Line(CapSheet p, CapWorldLayout l, int cx, int cy, double s, double x0, double y0, double x1, double y1, uint c, double alpha, bool glow)
    {
        int ax = cx + (int)Math.Round(x0 * s), ay = cy + (int)Math.Round(y0 * s), bx = cx + (int)Math.Round(x1 * s), by = cy + (int)Math.Round(y1 * s);
        int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay), sx = ax < bx ? 1 : -1, sy = ay < by ? 1 : -1, err = dx + dy;
        while (true)
        {
            if (glow)
            {
                Put(p, l, ax + 1, ay, c, alpha); Put(p, l, ax - 1, ay, c, alpha); Put(p, l, ax, ay + 1, c, alpha); Put(p, l, ax, ay - 1, c, alpha);
            }
            else
            {
                Put(p, l, ax, ay, c, alpha);
            }

            if (ax == bx && ay == by) return;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; ax += sx; }
            if (e2 <= dx) { err += dx; ay += sy; }
        }
    }

    /// <summary>
    /// The wormhole from the front, at <paramref name="s"/> of its size, shaped after the reference and lit as the party's: one soft glow;
    /// spokes out of the core and two close rings round it, thin and white,
    /// turning slowly; the bolts, thick and jagged, white over a lilac glow, crackling ten times a second (one now and then flashing); the white
    /// core and its bloom, pulsing; <paramref name="flare"/> swells it as it spits.
    /// </summary>
    private static void Wormhole(CapSheet p, CapWorldLayout l, int cx, int cy, double s, double flare, double t)
    {
        var pulse = 0.5 + (0.5 * Math.Sin(t * 2 * Math.PI / 1.6));
        var core = (CoreR + (0.5 * pulse) + (2.5 * flare)) * s;

        // the glow, one soft light as the party's wormhole has it (2026-10-20: the first try, a disc with an aurora and a warm patch stuck on,
        // looked forced): violet outside, magenta nearer, pale pink by the core; a little bluer to the right, a touch warmer to the left
        var g = CoreR * 3.1 * s;
        for (var dy = -(int)g; dy <= g; dy++)
        {
            for (var dx = -(int)g; dx <= g; dx++)
            {
                var d = Math.Sqrt(((dx + 1) * (dx + 1)) + (dy * dy)) / g;
                if (d > 1 || (dx * dx) + (dy * dy) <= core * core) continue;
                var c = CapSheet.Mix(0x7048E0u, 0xE050D0u, Math.Clamp((0.9 - d) / 0.45, 0, 1));
                c = CapSheet.Mix(c, 0xFFC8F0u, Math.Clamp((0.42 - d) / 0.22, 0, 1));
                if (dx > 0) c = CapSheet.Mix(c, 0x5A7CF0u, dx / g * 0.9);
                var warm = Math.Sqrt(Math.Pow((dx + (g * 0.32)) / (g * 0.22), 2) + Math.Pow((dy + 2) / (g * 0.2), 2));
                if (warm < 1) c = CapSheet.Mix(c, 0xFFA868u, (1 - warm) * 0.55);
                Put(p, l, cx + dx, cy + dy, c, Math.Min(255, (300 + (40 * pulse)) * Math.Pow(1 - d, 1.1)));
            }
        }

        // the net: spokes out of the core across the disc, two close rings near its edge
        for (var k = 0; k < 24; k++)
        {
            var a = (k / 24.0 * 2 * Math.PI) + (t * 0.12);
            Line(p, l, cx, cy, s, Math.Cos(a) * CoreR * 1.02, Math.Sin(a) * CoreR * 1.02, Math.Cos(a) * CoreR * 1.9, Math.Sin(a) * CoreR * 1.9, 0xEAF6FFu, 130, false);
        }

        foreach (var ring in (ReadOnlySpan<double>)[1.45, 1.68])
        {
            var r = CoreR * ring * s;
            var steps = (int)(r * 7) + 8;
            for (var k = 0; k < steps; k++)
            {
                var a = k / (double)steps * 2 * Math.PI;
                Put(p, l, cx + (int)Math.Round(Math.Cos(a) * r), cy + (int)Math.Round(Math.Sin(a) * r), 0xF0F8FFu, 200);
            }
        }

        // the bolts: a lilac glow, then white two cells thick; crackling
        var tick = Math.Floor(t * 10);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var b = 0; b < Bolts.Length; b++)
            {
                var points = Bolts[b].Points;
                var light = (Bolts[b].Twig ? 0.8 : 1) * (0.78 + (0.22 * Frac(CapArt.Hash((int)tick, b + 900))));
                var flash = CapArt.Hash((int)tick, b + 400) % 13 == 0;
                for (var i = 0; i + 1 < points.Length; i++)
                {
                    var (x0, y0) = Shaken(points, i, b, tick);
                    var (x1, y1) = Shaken(points, i + 1, b, tick);
                    // across the disc a bolt is a thin line, so the net shows through; outside it, thick and glowing
                    var outside = Math.Sqrt((((x0 + x1) / 2) * ((x0 + x1) / 2)) + (((y0 + y1) / 2) * ((y0 + y1) / 2))) > CoreR * 1.75;
                    if (pass == 0 && !outside) continue;
                    if (pass == 0)
                    {
                        Line(p, l, cx, cy, s, x0, y0, x1, y1, 0xD6B4FFu, (flash ? 110 : 40) * light, true);
                    }
                    else
                    {
                        Line(p, l, cx, cy, s, x0, y0, x1, y1, 0xFFFFFFu, flash ? 255 : 245 * light, false);
                        if (outside && !Bolts[b].Twig && i < points.Length - 2) Line(p, l, cx, cy, s, x0 + 0.6, y0 + 0.6, x1 + 0.6, y1 + 0.6, 0xF8F0FFu, 200 * light, false);
                    }
                }
            }
        }

        // the core and its bloom
        var bloom = core + 2.5 + (3 * flare);
        for (var dy = -(int)bloom - 1; dy <= bloom + 1; dy++)
        {
            for (var dx = -(int)bloom - 1; dx <= bloom + 1; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d <= core) Put(p, l, cx + dx, cy + dy, d > core - 1.2 && dx > 2 ? 0xC8FFF8u : 0xFFFFFFu, 255);
                else if (d < bloom) Put(p, l, cx + dx, cy + dy, 0xF8F0FFu, (170 + (60 * pulse)) * Math.Pow((bloom - d) / (bloom - core), 1.1));
            }
        }
    }

    private static (double X, double Y) Shaken((double X, double Y)[] points, int i, int b, double tick)
    {
        var (x, y) = points[i];
        if (i == 0) return (x, y);
        return (x + ((Frac(CapArt.Hash((int)tick, (b * 131) + (i * 7))) - 0.5) * 1.0), y + ((Frac(CapArt.Hash((int)tick + 7, (b * 131) + (i * 7))) - 0.5) * 1.0));
    }

    private static double Frac(int h) => (uint)h % 10000 / 10000.0;

    /// <summary>
    /// An item from its spit: out of the core towards us (growing), in an arc down to the water, a splash, sinking with the water's drag to a
    /// slow fall while it sways and bubbles rise from it, darker the deeper it goes; at rest on the seabed a moment, then sinking into the sand
    /// in a puff until it is gone.
    /// </summary>
    private static void Fall(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapItemDrop drop, (int X, int Y) at, double since)
    {
        if (ToastPixels.SpritePixels.From(drop.Icon) is not { } icon) return;
        var h = (uint)CapArt.Hash(drop.Seed, 5);
        var dir = h % 2 == 0 ? -1 : 1;
        var vx = dir * (10 + (h % 17));
        const double Vy = -26, Emerge = 0.28;
        var half = (int)Math.Round(icon.Height * Scale / 2);

        // out of the core, growing towards us
        if (since < Emerge)
        {
            var k = since / Emerge;
            DrawIcon(p, l, icon, at.X, at.Y, Scale * (0.25 + (0.75 * k)), 0, int.MaxValue);
            return;
        }

        // the arc down to the water: where it meets it
        var ft = since - Emerge;
        var surface = l.SurfaceY;
        var y0 = (double)at.Y;
        var tHit = (-Vy + Math.Sqrt((Vy * Vy) + (2 * Gravity * (surface - y0)))) / Gravity;
        if (ft < tHit)
        {
            DrawIcon(p, l, icon, at.X + (int)Math.Round(vx * ft), (int)Math.Round(y0 + (Vy * ft) + (0.5 * Gravity * ft * ft)), Scale, 0, int.MaxValue);
            return;
        }

        // in the water: the drag slows it from the speed it hit at towards a slow fall; it sways
        var xe = at.X + (vx * tHit);
        var ve = Vy + (Gravity * tHit);
        var w = ft - tHit;
        var decay = 1 - Math.Exp(-w / Drag);
        var x = xe + (vx * Drag * decay) + (Math.Sin((w * 3.2) + drop.Seed) * 3 * Math.Min(1, w * 2));
        var y = surface + (Terminal * w) + ((ve - Terminal) * Drag * decay);
        var ix = (int)Math.Round(x);
        var bed = CapSea.Relief(l, ctx, Math.Clamp(ix, 0, l.Cols - 1));
        Splash(p, l, (int)Math.Round(xe), surface, w);

        // when it reaches the seabed: worked out by stepping its fall, so the rest and the burial follow on
        var rest = -1.0;
        for (var step = 0.0; step < Life; step += 1 / 30.0)
        {
            var dk = 1 - Math.Exp(-step / Drag);
            var sx = (int)Math.Round(xe + (vx * Drag * dk) + (Math.Sin((step * 3.2) + drop.Seed) * 3 * Math.Min(1, step * 2)));
            if (surface + (Terminal * step) + ((ve - Terminal) * Drag * dk) + half >= CapSea.Relief(l, ctx, Math.Clamp(sx, 0, l.Cols - 1)))
            {
                rest = step;
                break;
            }
        }

        var murk = Math.Clamp((y - surface) / Math.Max(1, l.FloorTop - surface), 0, 1) * 0.35;
        if (rest < 0 || w < rest)
        {
            DrawIcon(p, l, icon, ix, (int)Math.Round(y), Scale, murk, int.MaxValue);
            Bubbles(p, l, ix, (int)Math.Round(y) - half, w, drop.Seed, surface);
            return;
        }

        // at rest, then into the sand
        var after = w - rest;
        var restX = (int)Math.Round(xe + (vx * Drag * (1 - Math.Exp(-rest / Drag))) + (Math.Sin((rest * 3.2) + drop.Seed) * 3 * Math.Min(1, rest * 2)));
        var floor = CapSea.Relief(l, ctx, Math.Clamp(restX, 0, l.Cols - 1));
        var sink = (int)Math.Round(Smooth((after - 0.45) / 0.9) * ((half * 2) + 2));
        if (sink < (half * 2) + 2) DrawIcon(p, l, icon, restX, floor - half + sink, Scale, murk, floor);
        Puffs(p, l, restX, floor, after - 0.4);
    }

    /// <summary>The icon at a scale, centred on a point, mixed towards the deep water by <paramref name="murk"/>, cut off below a row.</summary>
    private static void DrawIcon(CapSheet p, CapWorldLayout l, ToastPixels.SpritePixels icon, int cx, int cy, double scale, double murk, int clip)
    {
        int w = Math.Max(1, (int)Math.Round(icon.Width * scale)), h = Math.Max(1, (int)Math.Round(icon.Height * scale));
        int left = cx - (w / 2), top = cy - (h / 2);
        for (var sy = 0; sy < h; sy++)
        {
            var y = top + sy;
            if (y >= clip) break;
            var srcY = Math.Min(icon.Height - 1, (int)(sy / scale));
            for (var sx = 0; sx < w; sx++)
            {
                var i = ((srcY * icon.Width) + Math.Min(icon.Width - 1, (int)(sx / scale))) * 4;
                if (icon.Bgra[i + 3] < 128) continue;
                var c = CapSheet.Rgb(icon.Bgra[i + 2], icon.Bgra[i + 1], icon.Bgra[i]);
                Put(p, l, left + sx, y, murk > 0 ? CapSheet.Mix(c, 0x1E4C8Au, murk) : c, 255);
            }
        }
    }

    /// <summary>The splash where it went in: a ring of foam spreading on the surface, drops thrown up and falling back.</summary>
    private static void Splash(CapSheet p, CapWorldLayout l, int x, int surface, double w)
    {
        if (w > 0.7) return;
        var r = 3 + (w * 22);
        var fade = 255 * (1 - (w / 0.7));
        for (var dx = -(int)r; dx <= r; dx++)
        {
            if (Math.Abs(dx) < r - 2) continue;
            Put(p, l, x + dx, surface, 0xFFFFFFu, fade);
            Put(p, l, x + dx, surface + 1, 0xC8ECFFu, fade * 0.7);
        }

        for (var n = 0; n < 7; n++)
        {
            var a = (n - 3) * 0.35;
            double vx = Math.Sin(a) * 26, vy = -30 - (n % 3 * 8);
            int px = x + (int)Math.Round(vx * w), py = surface + (int)Math.Round((vy * w) + (0.5 * Gravity * w * w));
            if (py > surface) continue;
            Put(p, l, px, py, n % 2 == 0 ? 0xFFFFFFu : 0xBFE8FFu, fade);
        }
    }

    /// <summary>Bubbles rising from it while it sinks, wobbling, gone at the surface.</summary>
    private static void Bubbles(CapSheet p, CapWorldLayout l, int x, int y, double w, int seed, int surface)
    {
        for (var n = 0; n < 4; n++)
        {
            var age = ((w * 1.4) + (n * 0.25)) % 1;
            var by = y - (int)Math.Round(age * 26);
            if (by <= surface + 1) continue;
            var bx = x + (int)Math.Round(Math.Sin((age * 8) + n + seed) * 2) + ((n % 2 * 4) - 2);
            Put(p, l, bx, by, 0xE8F8FFu, 200 * (1 - age));
            Put(p, l, bx + 1, by, 0x9CD4F4u, 150 * (1 - age));
            Put(p, l, bx, by + 1, 0x9CD4F4u, 150 * (1 - age));
        }
    }

    /// <summary>Sand puffing up on both sides as it sinks in: little clouds of the seabed's colours spreading and settling.</summary>
    private static void Puffs(CapSheet p, CapWorldLayout l, int x, int floor, double k)
    {
        if (k is < 0 or > 1.4) return;
        for (var side = -1; side <= 1; side += 2)
        {
            for (var n = 0; n < 3; n++)
            {
                var spread = Math.Min(1, k / 0.8);
                int cx = x + (side * (5 + n * 4 + (int)Math.Round(spread * 6))), cy = floor - 2 - (int)Math.Round(Math.Sin(Math.Min(1, k / 1.2) * Math.PI) * (3 + n));
                var r = 1.5 + (spread * 2) - (n * 0.3);
                var keep = 1 - (k / 1.4);
                for (var dy = -3; dy <= 3; dy++)
                {
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        var d = Math.Sqrt((dx * dx) + (dy * dy));
                        if (d > r) continue;
                        int px = cx + dx, py = cy + dy;
                        if ((CapSeaPalette.Bayer[((py & 3) * 4) + (px & 3)] + 0.5) / 16.0 > keep) continue;
                        Put(p, l, px, py, CapSeaPalette.Dithered(SandPal, 0.75 - ((dx + dy) / r * 0.2), px, py), 255);
                    }
                }
            }
        }
    }
}
