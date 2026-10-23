using System.Globalization;
using System.Text;

namespace PermaLocke.App.Views;

/// <summary>
/// What the portal of the randomizer shows at one moment (2026-10-12): how long it has been open, the steps the randomizer
/// has finished (each one a pulse), and whether it has ended and how.
/// </summary>
/// <param name="Since">Seconds since the player pressed GENERAR.</param>
/// <param name="PulseTimes">The moment, in the same seconds, each real step of the randomizer was done.</param>
/// <param name="FinaleAt">When the finale starts, in the same seconds; null while the randomizer is still working.</param>
/// <param name="Failed">The randomizer failed: the portal collapses instead of throwing the world out.</param>
/// <param name="LastStep">The name of the last step done, written under the portal.</param>
/// <param name="Sprites">Pictures from the cartridge chosen at random, not those of the world being made.</param>
public sealed record WormholeState(double Since, IReadOnlyList<double> PulseTimes, double? FinaleAt, bool Failed, string? LastStep,
    IReadOnlyList<RoomSprite> Sprites);

/// <summary>
/// The Ultra Wormhole that opens while the world is randomized (2026-10-12): a ring of blue panels round a tunnel of light, a
/// violet halo, lightning on its rim, and Pokémon, items and machines of the cartridge swallowed in a spiral. Each step the
/// randomizer really finishes is a pulse; at the end it implodes, flashes, and throws everything out changed.
/// </summary>
/// <remarks>
/// <para>
/// Every frame is drawn from the time alone, with the particles laid out by a hash of their number: no state carried from one
/// frame to the next, so a slow frame never makes anything jump. It writes the canvas directly with whole colours (no WPF
/// <c>Color</c>, whose scRGB maths cost too much per cell, §188).
/// </para>
/// <para>
/// Decorative on purpose: the pictures are not those of the world being generated, and nothing here says what came out.
/// What the randomizer really did is the pulses, one per finished step, and the step's name under the portal.
/// </para>
/// </remarks>
public sealed class UltraWormholeScene : PixelScene
{
    public const int DesignWidth = 320;
    public const int DesignRows = 180;

    /// <summary>How long the opening lasts; a finale asked for sooner waits for it.</summary>
    public const double OpenLength = 1.5;

    /// <summary>From the start of the finale to the portal gone.</summary>
    public const double FinaleLength = 2.6;

    /// <summary>A failure is shorter: no burst, only the collapse.</summary>
    public const double FailLength = 1.3;

    private const double Squash = 1.0;          // a perfect circle, seen straight on          // the portal is seen a little from below: an ellipse, not a circle
    private const int Panels = 12;
    private const double ChaseSpeed = 4.0;   // panels a second
    private const double ChaseTail = 3.0;    // panels lit behind the head

    // The rings of the frame, as fractions of the radius: tunnel, cyan ring, lavender band, panels, teeth.
    private const double TunnelEdge = 0.58;
    private const double InnerEdge = TunnelEdge;
    private const double LavenderIn = 0.74;
    private const double PanelIn = 0.8;
    private const double PanelOut = 1.0;
    private const double TeethOut = 1.07;

    // The palette: the blues and cyans of the reference, the app's violet for the halo.
    private const uint Space = 0x07050F;
    private const uint SpaceLift = 0x0E0A1E;
    private const uint StarDim = 0x2C2850;
    private const uint StarMid = 0x5E5A96;
    private const uint StarHi = 0xC8D4FF;
    private const uint HaloFar = 0x2A1440;
    private const uint HaloMid = 0x6A2C8E;
    private const uint HaloNear = 0xB45CD6;
    private const uint HaloHot = 0xF0B4FA;
    private const uint CoreWhite = 0xFCFEFF;
    private const uint CoreIce = 0xD4FAFF;
    private const uint CoreCyan = 0x8EE2FF;
    private const uint CoreSky = 0x56B4F6;
    private const uint CoreBlue = 0x3A76E2;
    private const uint CoreDeep = 0x2848B8;
    private const uint RimCyan = 0x8CF2FF;
    private const uint Cyan = 0x6CC6F2;
    private const uint CyanLit = 0xA6E6FF;
    private const uint CyanShade = 0x3E8ED8;
    private const uint Lavender = 0xC6CCF8;
    private const uint LavenderShade = 0x8E98E2;
    private const uint PanelDark = 0x1A2266;
    private const uint PanelLow = 0x24308A;
    private const uint PanelMid = 0x3242AE;
    private const uint PanelHigh = 0x4A62D2;
    private const uint PanelShine = 0x9CB2F6;
    private const uint PanelOutline = 0xE6EAFF;
    private const uint Side = 0x10164A;
    private const uint SideEdge = 0x2A3480;
    private const uint Bolt = 0xFAF0FF;
    private const uint BoltGlow = 0xC898F0;
    private const uint Grey = 0x8A8698;
    private const uint GreyDark = 0x4A4658;

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    public UltraWormholeScene(int width, int height) : base(width, height, DesignWidth, DesignRows)
    {
    }

    // ============================================================================================= the frame

    public void Render(WormholeState state, bool toScreen = true)
    {
        var t = state.Since;
        var cx = Width / 2.0;
        var cy = (Height / 2.0) - 6;

        // When the finale starts, at the earliest once the portal is fully open.
        double? finale = state.FinaleAt is { } at ? Math.Max(at, OpenLength) : null;
        var f = finale is { } start ? t - start : -1;
        var failed = state.Failed;

        // How big the portal is now: it opens, swells a little in each pulse, implodes in the finale and closes.
        var steps = state.PulseTimes.Count(p => p <= t);
        var pulse = Pulse(state.PulseTimes, t);
        var radius = 52.0 * OpenScale(t);
        var slit = SlitScale(t);
        var energy = Math.Min(1, 0.35 + (steps * 0.06)) + (pulse * 0.6);

        if (f >= 0)
        {
            if (failed)
            {
                var k = Math.Clamp(f / FailLength, 0, 1);
                radius *= 1 - Ease(k);
                slit *= 1 - (k * k);
                energy *= 1 - k;
            }
            else
            {
                // Swallow (0-0.55), implode (0.55-0.75), burst and swell (0.75-1.4), close (1.4-2.1).
                var swell = f < 0.55 ? 1 + (f * 0.25)
                    : f < 0.75 ? 1.14 - ((f - 0.55) / 0.2 * 0.6)
                    : f < 1.4 ? 0.54 + (Overshoot((f - 0.75) / 0.65) * 0.56)
                    : 1.1 * (1 - Ease(Math.Clamp((f - 1.4) / 0.7, 0, 1)));
                radius *= swell;
                if (f > 1.4) slit *= 1 - Math.Clamp((f - 1.6) / 0.5, 0, 1);
                energy = f < 0.75 ? 1.2 + f : Math.Max(0, 1.5 - ((f - 0.75) * 0.8));
            }
        }

        var shake = f is >= 0 and < 0.7 && !failed ? (int)Math.Round(Math.Sin(f * 90) * (f * 2.5)) : 0;
        cx += shake;

        Background(t, cx, cy, energy, f, failed);
        if (radius > 0.5)
        {
            // a real Ultra Wormhole, as the player's photo (2026-10-22): its glow, what it swallows going in, its net, its bolts, its core
            var grow = Grow(t);
            Glow(cx, cy, radius, slit, energy * GlowIn(t), pulse, failed);
            Particles(state, t, finale, cx, cy, radius, slit, inside: true);
            Net(cx, cy, radius, slit, t, grow, failed);
            Filaments(cx, cy, radius, slit, t, energy * GlowIn(t), failed);
            Bolts(cx, cy, radius, slit, t, energy, grow, failed);
            Motes(cx, cy, radius, slit, t, grow, failed);
            Particles(state, t, finale, cx, cy, radius, slit, inside: false);
            Core(cx, cy, radius, slit, pulse, failed);
            Shockwaves(state.PulseTimes, t, cx, cy, radius, slit);
            Sparks(cx, cy, radius, slit, t, energy, failed);
        }

        if (finale is not null && !failed) Burst(state.Sprites, f, cx, cy);
        Opening(t, cx, cy);
        if (f >= 0 && !failed) WhiteOut(f);
        Caption(state, t, f);

        if (toScreen) Present();
    }

    // ============================================================================================= the wormhole of the photo

    /// <summary>The core's radius against the portal's: the rest is measured from it as in the reference (the items' wormhole, CapItemRain).</summary>
    private const double CoreShare = 0.32;

    /// <summary>One bolt of the burst round the core: its points out from the middle (in cores), and whether it is a twig off another.</summary>
    private sealed record Ray((double X, double Y)[] Points, bool Twig);

    /// <summary>The burst, made once: long jagged bolts all round the core, of uneven lengths as in the reference, some with a branch.</summary>
    private static readonly Ray[] Rays = MakeRays();

    /// <summary>The bolts that are not twigs, for the sparks that run along them.</summary>
    private static readonly int[] MainRays = [.. Enumerable.Range(0, Rays.Length).Where(i => !Rays[i].Twig)];

    private static Ray[] MakeRays()
    {
        var rays = new List<Ray>();
        uint seed = 1913;
        double Rnd()
        {
            seed = (uint)(Hash((int)seed, 91) * 1000003);
            return Hash((int)seed, 17);
        }

        const int count = 18;
        for (var i = 0; i < count; i++)
        {
            var a = (i / (double)count * 2 * Math.PI) + ((Rnd() - 0.5) * 0.3);
            var end = 2.3 + (Rnd() * 1.1);
            var dist = 1.05 + (Rnd() * 0.15);
            var points = new List<(double X, double Y)> { (Math.Cos(a) * dist, Math.Sin(a) * dist) };
            var side = 0.0;
            while (dist < end - 0.2)
            {
                dist += 0.2 + (Rnd() * 0.2);
                side = Math.Clamp(side + ((Rnd() - 0.5) * 0.45), -0.32, 0.32);
                points.Add(((Math.Cos(a) * dist) - (Math.Sin(a) * side), (Math.Sin(a) * dist) + (Math.Cos(a) * side)));
                if (Rnd() < 0.12 && dist > 1.6)
                {
                    var fork = a + ((Rnd() < 0.5 ? 1 : -1) * (0.5 + (Rnd() * 0.4)));
                    var (bx, by) = points[^1];
                    var length = 0.3 + (Rnd() * 0.3);
                    rays.Add(new Ray([(bx, by), (bx + (Math.Cos(fork) * length * 0.5), by + (Math.Sin(fork) * length * 0.5)), (bx + (Math.Cos(fork) * length), by + (Math.Sin(fork) * length))], true));
                }
            }

            var tip = a + ((Rnd() - 0.5) * 0.25);
            points.Add((Math.Cos(tip) * end, Math.Sin(tip) * end));
            rays.Add(new Ray([.. points], false));
        }

        return [.. rays];
    }

    /// <summary>A colour, grey when the randomizer failed.</summary>
    private static uint Tone(uint c, bool failed) => failed ? Desaturate(c) : c;

    /// <summary>
    /// The glow round it, one soft light as the photo has it: violet outside, magenta nearer, pale pink by the core; a little bluer to the
    /// right, a touch warmer to the left; brighter as it works harder and in each pulse.
    /// </summary>
    private void Glow(double cx, double cy, double radius, double slit, double energy, double pulse, bool failed)
    {
        var core = radius * CoreShare;
        var g = core * 3.1;
        var strength = Math.Min(1.25, (0.75 + (energy * 0.35) + (pulse * 0.2)) * Math.Min(1, energy * 3));
        for (var y = (int)(cy - (g * slit)) - 1; y <= cy + (g * slit) + 1; y++)
        {
            for (var x = (int)(cx - g) - 1; x <= cx + g + 1; x++)
            {
                double dx = x + 0.5 - cx, dy = (y + 0.5 - cy) / slit;
                var d = Math.Sqrt(((dx + 1) * (dx + 1)) + (dy * dy)) / g;
                if (d > 1 || (dx * dx) + (dy * dy) <= core * core) continue;
                var c = Mix(0x7048E0u, 0xE050D0u, Math.Clamp((0.9 - d) / 0.45, 0, 1));
                c = Mix(c, 0xFFC8F0u, Math.Clamp((0.42 - d) / 0.22, 0, 1));
                if (dx > 0) c = Mix(c, 0x5A7CF0u, dx / g * 0.9);
                var warm = Math.Sqrt(Math.Pow((dx + (g * 0.32)) / (g * 0.22), 2) + Math.Pow((dy + 2) / (g * 0.2), 2));
                if (warm < 1) c = Mix(c, 0xFFA868u, (1 - warm) * 0.55);
                // in five bands, stepped through the Bayer dither, so it reads as pixel art and not as a blur
                var level = Math.Min(1, 1.2 * strength * Math.Pow(1 - d, 1.1));
                var band = Math.Floor((level * 5) + (Bayer[((y & 3) * 4) + (x & 3)] / 16.0)) / 5;
                Blend(x, y, Tone(c, failed), band);
            }
        }
    }

    /// <summary>The net: spokes out of the core across the disc and two close rings near its edge, thin and white, turning slowly.</summary>
    private void Net(double cx, double cy, double radius, double slit, double t, double grow, bool failed)
    {
        var core = radius * CoreShare;
        var reach = 1.02 + (0.88 * Ease(grow));
        for (var k = 0; k < 24; k++)
        {
            var a = (k / 24.0 * 2 * Math.PI) + (t * 0.12);
            double ca = Math.Cos(a), sa = Math.Sin(a);
            Thin(cx + (ca * core * 1.02), cy + (sa * core * 1.02 * slit), cx + (ca * core * reach), cy + (sa * core * reach * slit), Tone(0xEAF6FFu, failed), k % 2 == 0 ? 0.55 : 0.35);

            // a bright knot where a spoke crosses a ring
            foreach (var ring in (ReadOnlySpan<double>)[1.45, 1.68])
            {
                if (reach < ring) continue;
                Put((int)Math.Round(cx + (ca * core * ring)), (int)Math.Round(cy + (sa * core * ring * slit)), Tone(0xFFFFFFu, failed));
            }
        }

        // the rings draw themselves round as it opens; the third one, inside, is faint and dotted
        var sweep = Math.Clamp((grow - 0.25) / 0.6, 0, 1);
        foreach (var (ring, alpha, dotted) in (ReadOnlySpan<(double, double, bool)>)[(1.45, 0.8, false), (1.68, 0.8, false), (1.22, 0.4, true)])
        {
            var r = core * ring;
            var steps = (int)(r * 7) + 8;
            var from = (ring * 2.1) + (t * (dotted ? -0.3 : 0.12));
            for (var k = 0; k < steps * sweep; k++)
            {
                if (dotted && k % 3 != 0) continue;
                var a = from + (k / (double)steps * 2 * Math.PI);
                Blend((int)Math.Round(cx + (Math.Cos(a) * r)), (int)Math.Round(cy + (Math.Sin(a) * r * slit)), Tone(0xF0F8FFu, failed), alpha);
            }
        }
    }

    /// <summary>
    /// Fine crackle in the glow between the bolts: short thin lilac threads out of the core's edge, new ones ten times a second.
    /// </summary>
    private void Filaments(double cx, double cy, double radius, double slit, double t, double energy, bool failed)
    {
        var core = radius * CoreShare;
        var tick = (int)Math.Floor(t * 10);
        var count = (int)(16 + (Math.Min(1, energy) * 20));
        for (var i = 0; i < count; i++)
        {
            var seed = (tick * 97) + i;
            var a = Hash(seed, 3) * 2 * Math.PI;
            var dist = 1.04;
            var side = 0.0;
            double px = cx + (Math.Cos(a) * core * dist), py = cy + (Math.Sin(a) * core * dist * slit);
            var segments = 3 + (int)(Hash(seed, 5) * 4);
            for (var j = 0; j < segments; j++)
            {
                dist += 0.12 + (Hash(seed + j, 7) * 0.14);
                side += (Hash(seed + j, 11) - 0.5) * 0.3;
                var nx = cx + (((Math.Cos(a) * dist) - (Math.Sin(a) * side)) * core);
                var ny = cy + (((Math.Sin(a) * dist) + (Math.Cos(a) * side)) * core * slit);
                Thin(px, py, nx, ny, Tone(j == 0 ? 0xF4E8FFu : 0xD8B8FFu, failed), 0.55 - (j * 0.07));
                px = nx;
                py = ny;
            }
        }
    }

    /// <summary>Sparks running out along the bolts from the core to their tips, a white point with a lilac trail.</summary>
    private void Motes(double cx, double cy, double radius, double slit, double t, double grow, bool failed)
    {
        if (grow < 1) return;
        var core = radius * CoreShare;
        for (var m = 0; m < 14; m++)
        {
            var b = MainRays[(int)(Hash(m, 21) * MainRays.Length) % MainRays.Length];
            var points = Rays[b].Points;
            var u = Frac((t * (0.9 + (Hash(m, 23) * 0.6))) + Hash(m, 25));
            for (var trail = 0; trail < 3; trail++)
            {
                var along = Math.Max(0, u - (trail * 0.04)) * (points.Length - 1);
                var i = Math.Min(points.Length - 2, (int)along);
                var f = along - i;
                var x = points[i].X + ((points[i + 1].X - points[i].X) * f);
                var y = points[i].Y + ((points[i + 1].Y - points[i].Y) * f);
                var px = (int)Math.Round(cx + (x * core));
                var py = (int)Math.Round(cy + (y * core * slit));
                if (trail == 0)
                {
                    Put(px, py, Tone(0xFFFFFFu, failed));
                    Blend(px + 1, py, Tone(0xE8D8FFu, failed), 0.8);
                    Blend(px, py + 1, Tone(0xE8D8FFu, failed), 0.8);
                }
                else
                {
                    Blend(px, py, Tone(0xD8B8FFu, failed), 0.7 - (trail * 0.2));
                }
            }
        }
    }

    /// <summary>
    /// The bolts: thick and jagged, white over a lilac glow, crackling ten times a second (one now and then flashing), a thin line where they
    /// cross the disc so the net shows through; more of them lit as it works harder.
    /// </summary>
    private void Bolts(double cx, double cy, double radius, double slit, double t, double energy, double grow, bool failed)
    {
        var core = radius * CoreShare;
        var tick = (int)Math.Floor(t * 10);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var b = 0; b < Rays.Length; b++)
            {
                var points = Rays[b].Points;

                // how much of it has torn out of the core: each one a little after the last, the twigs only once the trunks are out
                var frac = Rays[b].Twig ? Math.Clamp(((grow * 1.35) - 0.55) / 0.8, 0, 1) : Math.Clamp((grow * 1.35) - (Hash(b, 33) * 0.35), 0, 1);
                var shown = frac * (points.Length - 1);
                if (shown <= 0) continue;
                var light = (Rays[b].Twig ? 0.8 : 1) * (0.78 + (0.22 * Hash(tick, b + 900))) * Math.Min(1, 0.6 + (energy * 0.4));
                var flash = Hash(tick, b + 400) < 0.08;
                for (var i = 0; i + 1 < points.Length && i < shown; i++)
                {
                    var (x0, y0) = Shake(points, i, b, tick);
                    var (x1, y1) = Shake(points, i + 1, b, tick);
                    if (i + 1 > shown)
                    {
                        var part = shown - i;
                        x1 = x0 + ((x1 - x0) * part);
                        y1 = y0 + ((y1 - y0) * part);
                    }
                    var outside = Math.Sqrt((((x0 + x1) / 2) * ((x0 + x1) / 2)) + (((y0 + y1) / 2) * ((y0 + y1) / 2))) > 1.75;
                    double ax = cx + (x0 * core), ay = cy + (y0 * core * slit), bx = cx + (x1 * core), by = cy + (y1 * core * slit);
                    if (pass == 0)
                    {
                        if (!outside) continue;
                        Thick(ax, ay, bx, by, Tone(0xD6B4FFu, failed), (flash ? 0.45 : 0.18) * light, 2);
                        Thick(ax, ay, bx, by, Tone(0xC8A6FFu, failed), (flash ? 0.8 : 0.55) * light, 1);
                    }
                    else
                    {
                        Thin(ax, ay, bx, by, Tone(0xFFFFFFu, failed), flash ? 1 : 0.96 * light);
                        if (outside && !Rays[b].Twig && i < points.Length - 2) Thin(ax + 1, ay + 1, bx + 1, by + 1, Tone(0xF8F0FFu, failed), 0.8 * light);
                    }
                }
            }
        }
    }

    /// <summary>A point of a bolt, nudged a little every tick so it crackles; its root and its tip stay.</summary>
    private static (double X, double Y) Shake((double X, double Y)[] points, int i, int b, int tick)
    {
        var (x, y) = points[i];
        if (i == 0 || i == points.Length - 1) return (x, y);
        return (x + ((Hash(tick + (i * 31), b) - 0.5) * 0.12), y + ((Hash(tick + (i * 17), b + 50) - 0.5) * 0.12));
    }

    /// <summary>The white core and its bloom, pulsing and swelling in each pulse; its edge a ring of cyan on the right, as in the photo.</summary>
    private void Core(double cx, double cy, double radius, double slit, double pulse, bool failed)
    {
        var core = radius * CoreShare * (1 + (pulse * 0.18));
        var bloom = core + (radius * 0.05) + (pulse * radius * 0.06);
        for (var y = (int)(cy - (bloom * slit)) - 1; y <= cy + (bloom * slit) + 1; y++)
        {
            for (var x = (int)(cx - bloom) - 1; x <= cx + bloom + 1; x++)
            {
                double dx = x + 0.5 - cx, dy = (y + 0.5 - cy) / slit;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > bloom) continue;
                if (d > core)
                {
                    var fall = 1 - ((d - core) / (bloom - core));
                    Blend(x, y, Tone(d < core + 1 ? 0xE0C8FFu : 0xFFF0FCu, failed), Math.Floor(((0.85 * fall) * 4) + (Bayer[((y & 3) * 4) + (x & 3)] / 16.0)) / 4);
                    continue;
                }

                // the edge: cyan on its right, pink-white on its left; a faint ice ring inside; the heart pure white
                var side = dx / Math.Max(0.001, d);
                uint c = d > core - 1.6 ? (side > -0.15 ? 0x9CE8FFu : 0xFFE6FAu)
                    : d > core * 0.68 && d < core * 0.76 ? 0xE6FAFFu
                    : 0xFFFFFFu;
                Put(x, y, Tone(c, failed));
            }
        }
    }

    private void Thin(double x0, double y0, double x1, double y1, uint c, double a)
    {
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var u = i / (double)steps;
            Blend((int)Math.Round(x0 + ((x1 - x0) * u)), (int)Math.Round(y0 + ((y1 - y0) * u)), c, a);
        }
    }

    private void Thick(double x0, double y0, double x1, double y1, uint c, double a, int w)
    {
        for (var oy = -w; oy <= w; oy++)
        {
            for (var ox = -w; ox <= w; ox++)
            {
                if ((ox * ox) + (oy * oy) > w * w) continue;
                Thin(x0 + ox, y0 + oy, x1 + ox, y1 + oy, c, a);
            }
        }
    }

    private static uint Mix(uint a, uint b, double k)
    {
        k = Math.Clamp(k, 0, 1);
        uint Ch(int shift) => (uint)Math.Round((((a >> shift) & 255) * (1 - k)) + (((b >> shift) & 255) * k));
        return (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    /// <summary>
    /// How big it is: a spark first (0-0.25 s), a small crackling knot (0.25-0.45), then it tears open round, a touch too far, and
    /// settles by 1.35 s.
    /// </summary>
    private static double OpenScale(double t)
    {
        if (t < 0.25) return 0;
        if (t < 0.45) return 0.1 * Ease((t - 0.25) / 0.2);
        return 0.1 + (0.9 * Overshoot((t - 0.45) / 0.9));
    }

    /// <summary>It is round from the start: the old tall slit is gone (kept as 1 so the finale can still flatten it).</summary>
    private static double SlitScale(double t) => 1;

    /// <summary>How far the bolts and the net have grown out of the core, a little behind the core itself.</summary>
    private static double Grow(double t) => Math.Clamp((t - 0.3) / 1.05, 0, 1);

    /// <summary>The glow comes in as it tears open.</summary>
    private static double GlowIn(double t) => Ease(Math.Clamp((t - 0.35) / 0.8, 0, 1));

    private static double Pulse(IReadOnlyList<double> pulses, double t)
    {
        var strongest = 0.0;
        foreach (var p in pulses)
        {
            var s = t - p;
            if (s is >= 0 and < 0.9) strongest = Math.Max(strongest, s < 0.08 ? s / 0.08 : 1 - ((s - 0.08) / 0.82));
        }

        return strongest;
    }

    // ============================================================================================= space

    /// <summary>Deep space, with stars streaming into the portal like a warp: faster as it works harder.</summary>
    private void Background(double t, double cx, double cy, double energy, double f, bool failed)
    {
        for (var y = 0; y < Height; y++)
        {
            // A faint vertical lift towards the centre, dithered.
            var lift = 1 - Math.Abs((y - cy) / (Height * 0.6));
            for (var x = 0; x < Width; x++)
            {
                Put(x, y, Bayer[((y & 3) * 4) + (x & 3)] < lift * 7 ? SpaceLift : Space);
            }
        }

        var diag = Math.Sqrt((Width * Width) + (Height * Height)) / 2;
        var speed = failed && f >= 0 ? 0.05 : 0.08 + (energy * 0.12);
        for (var i = 0; i < 170; i++)
        {
            var angle = Hash(i, 1) * Math.PI * 2;
            var phase = Frac(Hash(i, 2) + (t * speed * (0.6 + Hash(i, 3))));
            var r = diag * (1 - phase);
            var x = cx + (Math.Cos(angle) * r);
            var y = cy + (Math.Sin(angle) * r * Squash);
            var colour = phase > 0.85 ? StarHi : phase > 0.5 ? StarMid : StarDim;
            Put((int)x, (int)y, colour);

            // A streak behind the faster ones.
            if (phase > 0.4 && energy > 0.5)
            {
                var back = 1 + (int)(energy * 3 * phase);
                for (var k = 1; k <= back; k++)
                {
                    var rr = r + (k * 1.2);
                    Blend((int)(cx + (Math.Cos(angle) * rr)), (int)(cy + (Math.Sin(angle) * rr * Squash)), StarDim, 0.6 - (k * 0.1));
                }
            }
        }
    }

    // ============================================================================================= the portal

    /// <summary>The pink and lilac glow round the portal, breathing with the work (the first reference: magenta outside, lilac near the rim).</summary>
    private void Halo(double cx, double cy, double radius, double slit, double t, double energy, bool failed)
    {
        var rx = radius;
        var ry = radius * Squash * slit;
        var reach = 1.7 + (energy * 0.25);
        ForEllipse(cx, cy, rx * reach, ry * reach, (x, y, d) =>
        {
            var dd = d * reach;
            if (dd < 0.98) return;
            var dx = x + 0.5 - cx;
            var dy = (y + 0.5 - cy) / (Squash * slit);
            var a = Math.Atan2(dy, dx);
            // Brighter to the upper right, like the reference, and flickering along the rim.
            var side = 0.75 + (0.25 * Math.Cos(a + 0.9));
            var breathe = 0.5 + (0.5 * Math.Sin((t * 2.2) - (dd * 3) + (Math.Sin(a * 3 + t) * 0.6)));
            var glow = (1 - ((dd - 1) / (reach - 1))) * (0.6 + (0.4 * breathe)) * side * Math.Min(1.25, energy + 0.15);
            var threshold = Bayer[((y & 3) * 4) + (x & 3)] / 16.0;
            if (glow > 0.92 && threshold < (glow - 0.92) * 6) Put(x, y, failed ? Grey : HaloHot);
            else if (glow > 0.7 && threshold < (glow - 0.45) * 1.6) Put(x, y, failed ? GreyDark : HaloNear);
            else if (glow > 0.4 && threshold < glow - 0.1) Put(x, y, failed ? GreyDark : HaloMid);
            else if (glow > 0.1 && threshold < glow * 1.3) Put(x, y, failed ? Space : HaloFar);
        });
    }

    /// <summary>
    /// The thickness of the ring: the same ring a few cells lower in the colour of its side, painted first, so only its lower
    /// edge shows under the front face. That is what makes it a ring and not a drawing of one.
    /// </summary>
    private void Thickness(double cx, double cy, double radius, double slit, bool failed)
    {
        var depth = Math.Max(1, radius * 0.09) * slit;
        ForEllipse(cx, cy + depth, radius * TeethOut, radius * Squash * slit * TeethOut, (x, y, d) =>
        {
            var dd = d * TeethOut;
            if (dd < InnerEdge - 0.02) return;
            var colour = dd > TeethOut - 0.05 || dd < InnerEdge + 0.02 ? SideEdge : Side;
            Put(x, y, failed ? Desaturate(colour) : colour);
        });
    }

    /// <summary>
    /// The tunnel: rings that sink towards a point a little up and to the right of the centre, each one paler than the one
    /// before, running inwards; white at the bottom of it.
    /// </summary>
    private void Tunnel(double cx, double cy, double radius, double slit, double t, double energy, double pulse, bool failed)
    {
        var ry = radius * Squash * slit;
        var vx = 0.12;
        var vy = -0.14;
        var mouth = TunnelEdge;
        ForEllipse(cx, cy, radius * mouth, ry * mouth, (x, y, d) =>
        {
            var qx = (x + 0.5 - cx) / radius;
            var qy = (y + 0.5 - cy) / ry;

            // The depth s of this cell: on the ring centred at c + v·s with radius mouth·(1 − s).
            var a = (vx * vx) + (vy * vy) - (mouth * mouth);
            var b = (-2 * ((qx * vx) + (qy * vy))) + (2 * mouth * mouth);
            var c = (qx * qx) + (qy * qy) - (mouth * mouth);
            var disc = (b * b) - (4 * a * c);
            var s = disc < 0 ? 1 : Math.Clamp((-b + Math.Sqrt(disc)) / (2 * a), 0, 1);

            var band = Frac((s * 6) - (t * (0.7 + (energy * 0.6))));
            var threshold = Bayer[((y & 3) * 4) + (x & 3)] / 16.0;
            var light = s + (pulse * 0.25) + ((threshold - 0.5) * 0.14);
            uint colour = light switch
            {
                > 0.86 => CoreWhite,
                > 0.7 => CoreIce,
                > 0.5 => CoreCyan,
                > 0.3 => CoreSky,
                > 0.14 => CoreBlue,
                _ => CoreDeep,
            };

            // The lip of each sinking ring: a thin bright line.
            if (band < 0.1 && s < 0.86) colour = band < 0.05 ? CoreWhite : CoreIce;
            Put(x, y, failed ? Desaturate(colour) : colour);
        });
    }

    /// <summary>
    /// The three rings of the frame, as in the second reference. From outside in: twelve navy panels with a pale outline, a
    /// lighter window in each and a tooth on the outer edge; a lavender band; and a cyan ring cut into sixteen segments by white
    /// spokes. In the opening each panel flies in to its place; at every step the outlines flash cyan.
    /// </summary>
    private void FrameRing(double cx, double cy, double radius, double slit, double t, double energy, double pulse, bool failed)
    {
        var turn = (t * 0.03) % 1;
        var counter = (t * -0.09) % 1;
        // The running light goes round at one steady speed. It went with the energy, and since the energy jumps at every step
        // and in the finale, t·speed jumped too: the light leapt round and back and a few panels at the top blinked (2026-10-12).
        var chase = (t * ChaseSpeed) % Panels;
        var rx = radius * TeethOut;
        var ry = radius * Squash * slit * TeethOut;

        ForEllipse(cx, cy, rx, ry, (x, y, d) =>
        {
            var dd = d * TeethOut;
            if (dd < TunnelEdge) return;

            var dx = x + 0.5 - cx;
            var dy = (y + 0.5 - cy) / (Squash * slit);
            var a = (Math.Atan2(dy, dx) / (Math.PI * 2)) + 0.5;
            var lit = (-Math.Cos((a * Math.PI * 2) + 0.8) + 1) / 2; // light from the upper left
            uint colour;

            if (dd < LavenderIn)
            {
                // The cyan ring and its spokes.
                var seg = Frac(a + counter) * 16;
                var within = seg - Math.Floor(seg);
                var radial = (dd - TunnelEdge) / (LavenderIn - TunnelEdge);
                colour = radial < 0.14 ? CoreWhite
                    : within < 0.07 || within > 0.93 ? CoreWhite
                    : radial > 0.84 ? CyanShade
                    : lit > 0.55 ? CyanLit
                    : lit > 0.25 ? Cyan
                    : CyanShade;
                if (pulse > 0.4 && colour != CoreWhite) colour = CyanLit;
            }
            else if (dd < PanelIn)
            {
                // The lavender band, with its own shadow underneath.
                colour = dd > PanelIn - 0.012 ? PanelOutline : lit > 0.35 ? Lavender : LavenderShade;
            }
            else
            {
                var slot = Frac(a + turn) * Panels;
                var panel = (int)slot;
                var within = slot - panel;

                // The opening: each panel comes in from further out and locks into place, one after another.
                var arrive = Math.Clamp((t - 0.55 - (panel * 0.045)) / 0.35, 0, 1);
                if (arrive <= 0) return;
                dd -= (1 - Ease(arrive)) * 0.35;
                if (dd < PanelIn) return;

                var tooth = within is > 0.32 and < 0.68;
                if (dd > PanelOut && !tooth) return;
                if (dd > TeethOut) return;

                var outline = within < 0.045 || within > 0.955 || dd < PanelIn + 0.022 || (dd > PanelOut - 0.022 && !tooth)
                              || (tooth && dd > PanelOut && (dd > TeethOut - 0.022 || within < 0.35 || within > 0.65));
                var window = dd is > 0.86 and < 0.95 && within is > 0.17 and < 0.83;

                // The running light and the pulse brighten the panel; the outline flashes cyan.
                // A comet: the head on the chase, a tail of three panels fading behind it.
                var behind = ((chase - (panel + 0.5)) % Panels + Panels) % Panels;
                var glow = Math.Max(behind < ChaseTail ? 1 - (behind / ChaseTail) : 0, pulse * 0.8);
                if (arrive < 1) glow = Math.Max(glow, 1 - arrive);

                if (outline) colour = glow > 0.5 ? RimCyan : PanelOutline;
                else if (window) colour = dd > 0.935 ? PanelShine : lit > 0.5 ? PanelHigh : PanelMid;
                else colour = lit > 0.62 ? PanelMid : lit > 0.3 ? PanelLow : PanelDark;

                if (!outline && glow > 0.55) colour = Brighter(colour);
            }

            Put(x, y, failed ? Desaturate(colour) : colour);
        });
    }

    /// <summary>Wisps of lilac lightning streaming off the rim (the first reference), redrawn a few times a second.</summary>
    private void Lightning(double cx, double cy, double radius, double slit, double t, double energy, bool failed)
    {
        var beat = (int)Math.Floor(t / 0.08);
        var count = 3 + (int)(energy * 6);
        for (var b = 0; b < count; b++)
        {
            var seed = (beat * 31) + b;
            if (Hash(seed, 9) < 0.2) continue;

            // More of them up and to the right, where the glow is.
            var a = -0.9 + ((Hash(seed, 1) - 0.5) * Math.PI * 1.7);
            if (Hash(seed, 8) < 0.35) a = Hash(seed, 1) * Math.PI * 2;
            var length = (6 + (Hash(seed, 2) * 18)) * (0.6 + energy);
            var x = cx + (Math.Cos(a) * radius * TeethOut);
            var y = cy + (Math.Sin(a) * radius * Squash * slit * TeethOut);
            var steps = (int)(length / 2.5);
            var dir = a + ((Hash(seed, 3) - 0.5) * 0.6);

            for (var s = 0; s < steps; s++)
            {
                var nx = x + (Math.Cos(dir) * 2.5);
                var ny = y + (Math.Sin(dir) * 2.5 * Squash);
                Line((int)x, (int)y, (int)nx, (int)ny, failed ? Grey : (s < steps / 2 ? Bolt : HaloHot), failed ? GreyDark : BoltGlow);
                x = nx;
                y = ny;
                dir += (Hash(seed, 10 + s) - 0.5) * 1.1;
            }
        }
    }

    /// <summary>Specks of light circling just outside the ring.</summary>
    private void Sparks(double cx, double cy, double radius, double slit, double t, double energy, bool failed)
    {
        for (var i = 0; i < 24; i++)
        {
            var orbit = 1.12 + (Hash(i, 4) * 0.3);
            var a = (i * Math.PI * 2 / 24) + (t * 0.5);
            var x = (int)(cx + (Math.Cos(a) * radius * orbit));
            var y = (int)(cy + (Math.Sin(a) * radius * Squash * slit * orbit));
            var twinkle = Frac((t * 2) + Hash(i, 7));
            if (twinkle < 0.35) continue;
            Put(x, y, failed ? Grey : twinkle > 0.8 ? CoreWhite : HaloHot);
            if (twinkle > 0.88 && energy > 0.6)
            {
                Put(x - 1, y, BoltGlow);
                Put(x + 1, y, BoltGlow);
            }
        }
    }

    /// <summary>A ring of light that runs out from the portal at every step the randomizer finishes.</summary>
    private void Shockwaves(IReadOnlyList<double> pulses, double t, double cx, double cy, double radius, double slit)
    {
        foreach (var p in pulses)
        {
            var s = t - p;
            if (s is < 0 or > 0.9) continue;
            var k = s / 0.9;
            var r = radius * (1.1 + (Ease(k) * 2.2));
            var fade = 1 - k;
            for (var i = 0; i < 360; i++)
            {
                if (Bayer[i & 15] > fade * 16) continue;
                var a = i * Math.PI / 180;
                var x = (int)(cx + (Math.Cos(a) * r));
                var y = (int)(cy + (Math.Sin(a) * r * Squash * slit));
                Put(x, y, k < 0.4 ? CoreIce : RimCyan);
            }
        }
    }

    // ============================================================================================= what it swallows

    private const int Arms = 6;
    private const double ArmSpacing = 0.3;   // seconds between two pictures of the same arm
    private const double Travel = 3.4;       // seconds from the edge of the screen to the portal
    private const double Spin = 0.32;        // turns of the whole galaxy, radians a second
    private const double Twist = 2.4;        // how much an arm curls on its way in

    /// <summary>
    /// The pictures go in along six spiral arms that turn together like a galaxy: every picture of an arm follows the same
    /// curve, evenly spaced, the same size at the same distance, shrinking and going white-blue as they reach the light. At every
    /// finished step a ring of twelve comes in at once. In the finale every arm is drawn in quickly.
    /// </summary>
    /// <param name="inside">The ones already in the tunnel, drawn under the frame; the rest go on top of it.</param>
    private void Particles(WormholeState state, double t, double? finale, double cx, double cy, double radius, double slit, bool inside)
    {
        var sprites = state.Sprites;
        if (sprites.Count == 0) return;

        var start = Math.Sqrt((Width * Width) + (Height * Height)) / 2 + 12;
        var stop = finale ?? double.MaxValue;
        var suck = finale is { } fs ? Math.Max(0, t - fs) : 0;

        for (var arm = 0; arm < Arms; arm++)
        {
            var offset = arm * ArmSpacing / Arms;
            var first = (int)Math.Floor((t - 0.9 - offset - Travel) / ArmSpacing);
            var last = (int)Math.Floor((Math.Min(t, stop) - 0.9 - offset) / ArmSpacing);
            for (var seq = Math.Max(0, first); seq <= last; seq++)
            {
                var born = 0.9 + offset + (seq * ArmSpacing);
                var k = ((t - born) / Travel) + (suck * suck * 2.6);
                var id = (arm * 100_003) + seq;
                Place(sprites[Pick(id, sprites.Count)], arm * Math.PI * 2 / Arms, k, t, start, cx, cy, radius, slit, inside, state.Failed);
            }
        }

        // A ring of twelve at every step.
        for (var p = 0; p < state.PulseTimes.Count; p++)
        {
            var born = state.PulseTimes[p] - 0.2;
            if (born > t || born > stop) continue;
            var k = ((t - born) / 1.4) + (suck * suck * 2.6);
            for (var j = 0; j < 12; j++)
            {
                Place(sprites[Pick(900_000 + (p * 12) + j, sprites.Count)], (j * Math.PI / 6) + (p * 0.26), k, t, start * 0.8, cx, cy, radius, slit,
                    inside, state.Failed);
            }
        }
    }

    /// <summary>One picture on its way in: <paramref name="k"/> from 0 (at the edge) to 1 (gone into the light).</summary>
    private void Place(RoomSprite sprite, double arm, double k, double t, double start, double cx, double cy, double radius, double slit, bool inside,
        bool failed)
    {
        if (k is <= 0 or >= 1) return;
        var e = Math.Pow(k, 1.6);
        var r = start * (1 - e);
        var deep = r < radius * 0.75;   // before it would turn to a silhouette of light (light 0.5 at 0.68)
        if (deep != inside) return;

        var angle = arm + (t * Spin) + (e * Twist);
        var x = cx + (Math.Cos(angle) * r);
        var y = cy + (Math.Sin(angle) * r * Squash * slit);

        // Size and light depend only on the distance, so an arm reads as one line.
        var scale = 0.28 + (0.6 * Math.Min(1, r / (radius * 2.4)));
        var light = Math.Clamp(1.15 - (r / (radius * 1.05)), 0, 1);
        // past the net it is only a speck of light falling into the core, never a blot over the bolts
        if (deep)
        {
            int px = (int)Math.Round(x), py = (int)Math.Round(y);
            Put(px, py, failed ? Grey : CoreWhite);
            if (r > radius * 0.5)
            {
                Blend(px + 1, py, failed ? Grey : 0xE8D6FFu, 0.8);
                Blend(px, py + 1, failed ? Grey : 0xE8D6FFu, 0.8);
            }

            return;
        }

        DrawSprite(sprite, x, y, scale, failed ? Grey : 0xE8D6FFu, light);
    }

    private static int Pick(int id, int count) => (int)(Hash(id, 12) * count) % count;

    /// <summary>
    /// The finale: the world thrown out changed. Three rings of eighteen, one after another, evenly spaced and curling the other way,
    /// growing as they fly; and rings of light opening with them.
    /// </summary>
    private void Burst(IReadOnlyList<RoomSprite> sprites, double f, double cx, double cy)
    {
        if (sprites.Count == 0 || f < 0.72) return;
        var s = f - 0.72;
        var reach = Math.Sqrt((Width * Width) + (Height * Height)) / 2 + 30;
        for (var w = 0; w < 3; w++)
        {
            var k = (s - (w * 0.14)) / 1.1;
            if (k is <= 0 or >= 1) continue;
            var e = 1 - Math.Pow(1 - k, 2.6);
            for (var j = 0; j < 18; j++)
            {
                var angle = (j * Math.PI / 9) + (w * Math.PI / 18) - (e * 0.9);
                var r = 6 + (reach * e);
                var x = cx + (Math.Cos(angle) * r);
                var y = cy + (Math.Sin(angle) * r * Squash);
                DrawSprite(sprites[Pick(500_000 + (w * 18) + j, sprites.Count)], x, y, 0.35 + (e * 0.75), CoreWhite, Math.Max(0, 1 - (k * 3.5)));
            }
        }

        for (var w = 0; w < 3; w++)
        {
            var k = (s - (w * 0.12)) / 0.8;
            if (k is <= 0 or >= 1) continue;
            var r = 20 + (Ease(k) * reach * 1.05);
            for (var i = 0; i < 720; i++)
            {
                if (Bayer[i & 15] > (1 - k) * 16) continue;
                var a = i * Math.PI / 360;
                Put((int)(cx + (Math.Cos(a) * r)), (int)(cy + (Math.Sin(a) * r * Squash)), w == 0 ? CoreWhite : HaloHot);
            }
        }
    }

    /// <summary>
    /// The very first instant: a point of light that flares into a four-pointed star with shorter diagonals, a ring of light
    /// running out when it tears open, and the star fading as the wormhole takes its place.
    /// </summary>
    private void Opening(double t, double cx, double cy)
    {
        if (t > 0.75) return;
        var x0 = (int)Math.Floor(cx);
        var y0 = (int)Math.Floor(cy);

        // the star: grows to 0.3 s, then shrinks back into the core
        var size = t < 0.3 ? Ease(t / 0.3) : 1 - Ease(Math.Clamp((t - 0.3) / 0.45, 0, 1));
        var arm = (int)Math.Round(size * 22);
        var diag = (int)Math.Round(size * 9);
        for (var d = 0; d <= arm; d++)
        {
            var c = d < arm * 0.45 ? CoreWhite : d < arm * 0.8 ? CoreIce : CoreCyan;
            foreach (var (ux, uy) in (ReadOnlySpan<(int, int)>)[(1, 0), (-1, 0), (0, 1), (0, -1)])
            {
                Put(x0 + (ux * d), y0 + (uy * d), c);
                if (d < arm * 0.35)
                {
                    Put(x0 + (ux * d) + uy, y0 + (uy * d) + ux, HaloHot);
                    Put(x0 + (ux * d) - uy, y0 + (uy * d) - ux, HaloHot);
                }
            }
        }

        for (var d = 0; d <= diag; d++)
        {
            var c = d < diag * 0.5 ? CoreIce : HaloHot;
            Put(x0 + d, y0 + d, c);
            Put(x0 - d, y0 + d, c);
            Put(x0 + d, y0 - d, c);
            Put(x0 - d, y0 - d, c);
        }

        // its bright heart
        var heart = (int)Math.Round(size * 3);
        for (var y = -heart; y <= heart; y++)
        for (var x = -heart; x <= heart; x++)
        {
            if ((x * x) + (y * y) <= heart * heart) Put(x0 + x, y0 + y, CoreWhite);
        }

        // the ring that runs out as it tears (0.4-0.75 s), through the dither as it fades
        if (t >= 0.4)
        {
            var k = (t - 0.4) / 0.35;
            var r = 8 + (Ease(k) * 70);
            var steps = (int)(r * 7);
            for (var i = 0; i < steps; i++)
            {
                if (Bayer[i & 15] > (1 - k) * 16) continue;
                var ang = i * 2 * Math.PI / steps;
                Put((int)Math.Round(cx + (Math.Cos(ang) * r)), (int)Math.Round(cy + (Math.Sin(ang) * r)), k < 0.5 ? CoreIce : HaloHot);
            }
        }
    }

    /// <summary>The flash of the implosion: the screen goes white through the dither and comes back.</summary>
    private void WhiteOut(double f)
    {
        var amount = f < 0.62 ? 0 : f < 0.74 ? (f - 0.62) / 0.12 : f < 1.0 ? 1 - ((f - 0.74) / 0.26) : 0;
        if (amount <= 0) return;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (Bayer[((y & 3) * 4) + (x & 3)] < amount * 16) Put(x, y, CoreWhite);
        }
    }

    /// <summary>
    /// What the randomizer is doing, in words, under the portal: the last step done, brightening at each one. The portal says
    /// nothing else; the steps are real, the pictures are not the world.
    /// </summary>
    private void Caption(WormholeState state, double t, double f)
    {
        string text;
        var since = state.PulseTimes.Count > 0 ? t - state.PulseTimes[^1] : t;
        if (f >= 0) text = state.Failed ? "HA FALLADO" : "MUNDO NUEVO";
        else if (state.LastStep is { Length: > 0 } step) text = Plain(step);
        else if (t > 0.9) text = "ABRIENDO EL CARTUCHO";
        else return;

        if (f >= 0) since = f - (state.Failed ? 0 : 0.75);
        if (since < 0) return;
        if (f > (state.Failed ? FailLength : FinaleLength) - 0.4) return;

        var top = Height - 18;
        var width = BigWidth(text);
        var left = (Width - width) / 2;
        var bright = since < 0.35;
        // BigText counts from the design area; these are cells of the whole canvas.
        left -= Ox;
        top -= Oy;
        BigText(text, left + 1, top + 1, AlolaPalette.Rgb(0x05, 0x04, 0x0B));
        BigText(text, left, top, bright ? AlolaPalette.Rgb(0xFC, 0xFE, 0xFF) : AlolaPalette.Rgb(0x8C, 0xF2, 0xFF));
    }

    /// <summary>Capitals without accents: the pixel font has Ñ but no accented vowels.</summary>
    private static string Plain(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.ToUpperInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                // The tilde of the Ñ is kept: recompose it.
                if (ch == '̃' && sb.Length > 0 && sb[^1] == 'N') sb[^1] = 'Ñ';
                continue;
            }

            sb.Append(ch);
        }

        var plain = sb.ToString();
        return plain.Length > 40 ? plain[..40] : plain;
    }

    // ============================================================================================= drawing

    /// <summary>
    /// A sprite at any size, sampled cell by cell, going to <paramref name="tint"/> through the dither as <paramref name="light"/>
    /// goes from 0 to 1, with a dark outline so it reads over the light.
    /// </summary>
    private void DrawSprite(RoomSprite sprite, double cx, double cy, double scale, uint tint, double light)
    {
        if (scale <= 0.05) return;
        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var width = (maxX - minX + 1) * scale;
        var height = (maxY - minY + 1) * scale;
        var left = cx - (width / 2);
        var top = cy - (height / 2);
        var bytes = sprite.Bgra;

        for (var y = (int)Math.Floor(top); y < (int)Math.Ceiling(top + height); y++)
        {
            if (y < 0 || y >= Height) continue;
            for (var x = (int)Math.Floor(left); x < (int)Math.Ceiling(left + width); x++)
            {
                if (x < 0 || x >= Width) continue;
                var sx = minX + (int)Math.Floor((x + 0.5 - left) / scale);
                var sy = minY + (int)Math.Floor((y + 0.5 - top) / scale);
                if (sx < minX || sy < minY || sx > maxX || sy > maxY || !sprite.Solid(sx, sy)) continue;
                var at = ((sy * sprite.Width) + sx) * 4;
                // No dither on a picture: it turns to light all at once, a silhouette, so it never reads as noise.
                var colour = light >= 0.5 ? tint : ((uint)bytes[at + 2] << 16) | ((uint)bytes[at + 1] << 8) | bytes[at];
                Put(x, y, colour);
            }
        }
    }

    private void Line(int x0, int y0, int x1, int y1, uint core, uint glow)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            Blend(x0 + 1, y0, glow, 0.6);
            Blend(x0, y0 + 1, glow, 0.6);
            Put(x0, y0, core);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Calls <paramref name="paint"/> for every cell of an ellipse, with its distance from the centre (0 to 1).</summary>
    private void ForEllipse(double cx, double cy, double rx, double ry, Action<int, int, double> paint)
    {
        if (rx < 0.5 || ry < 0.5) return;
        var x0 = Math.Max(0, (int)Math.Floor(cx - rx));
        var x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + rx));
        var y0 = Math.Max(0, (int)Math.Floor(cy - ry));
        var y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + ry));
        for (var y = y0; y <= y1; y++)
        {
            var ny = (y + 0.5 - cy) / ry;
            for (var x = x0; x <= x1; x++)
            {
                var nx = (x + 0.5 - cx) / rx;
                var d = (nx * nx) + (ny * ny);
                if (d <= 1) paint(x, y, Math.Sqrt(d));
            }
        }
    }

    private void Put(int x, int y, uint rgb)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var at = ((y * Width) + x) * 4;
        Canvas[at] = (byte)rgb;
        Canvas[at + 1] = (byte)(rgb >> 8);
        Canvas[at + 2] = (byte)(rgb >> 16);
        Canvas[at + 3] = 255;
    }

    private void Blend(int x, int y, uint rgb, double a)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || a <= 0) return;
        var at = ((y * Width) + x) * 4;
        var k = (int)(Math.Min(1, a) * 256);
        Canvas[at] = (byte)(Canvas[at] + ((((int)(rgb & 255)) - Canvas[at]) * k >> 8));
        Canvas[at + 1] = (byte)(Canvas[at + 1] + ((((int)((rgb >> 8) & 255)) - Canvas[at + 1]) * k >> 8));
        Canvas[at + 2] = (byte)(Canvas[at + 2] + ((((int)((rgb >> 16) & 255)) - Canvas[at + 2]) * k >> 8));
        Canvas[at + 3] = 255;
    }

    private static uint Brighter(uint rgb) => rgb switch
    {
        PanelDark => PanelLow,
        PanelLow => PanelMid,
        PanelMid => PanelHigh,
        PanelHigh => PanelShine,
        PanelShine => PanelOutline,
        _ => rgb,
    };

    private static uint Desaturate(uint rgb)
    {
        var grey = (((rgb >> 16) & 255) * 30 + ((rgb >> 8) & 255) * 59 + (rgb & 255) * 11) / 100;
        grey = (grey * 3) / 4;
        return (grey << 16) | (grey << 8) | grey;
    }

    private static double Ease(double k) => k < 0.5 ? 4 * k * k * k : 1 - (Math.Pow((-2 * k) + 2, 3) / 2);

    /// <summary>Ease out with a small overshoot: the ring opens a touch too far and settles.</summary>
    private static double Overshoot(double k)
    {
        k = Math.Clamp(k, 0, 1);
        const double c1 = 1.6;
        const double c3 = c1 + 1;
        return 1 + (c3 * Math.Pow(k - 1, 3)) + (c1 * Math.Pow(k - 1, 2));
    }

    private static double Frac(double v) => v - Math.Floor(v);

    private static double Hash(int n, int salt)
    {
        unchecked
        {
            var h = (uint)((n * 73856093) ^ (salt * 19349663));
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h % 10007) / 10007.0;
        }
    }
}
