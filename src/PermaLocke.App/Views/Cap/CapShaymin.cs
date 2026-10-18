using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// The fifth trial's Z-Crystal, the Fitostal, brought to the Gimmighoul ruins by Shaymin (2026-10-18, first simple prototype: the player's
/// choice and sprites). Its own film, starting <see cref="Lead"/> seconds before the film's clock: the Ultra Wormhole beats and bursts in pink,
/// throwing out the crystal (which hangs there glowing), a Gracidea flower and Shaymin in its Land Forme, tumbling down onto a rubber ring that
/// pops up on the sea (the one Machamp stands on); breathing there, the flower drifts down onto its back, glows and goes into it, and it
/// changes, flickering white between its forms in a whirl of petals, into its Sky Forme; beating its wings it rises, takes the crystal and it flies left across both
/// skies, dives into the abyss's sea in a bubble and lays the crystal on the stone floor of the ruins between the chest and the column (the
/// spot the player marked), where a ring of flowers springs up round it; then it rises out of the sea and flies away. The crystal and its
/// flowers stay for the rest of the run (<see cref="PaintRest"/>).
/// </summary>
public static class CapShaymin
{
    /// <summary>The fifth trial's crystal.</summary>
    public const int Fitostal = 811;

    /// <summary>How long it all starts before the film's own clock.</summary>
    public const double Lead = 3.2;

    private const int Land = 492, Sky = 10492, Flower = 20492;
    private const double LandSize = 0.8, SkySize = 0.8, FlowerSize = 0.7, Lying = 0.4;

    // Slower and fuller (2026-10-18, asked by the player): it sits on a ring on the sea, the flower comes down onto it and goes into it.
    private const double Beat1 = -2.9, Beat2 = -2.6, Born = -2.2, Lands = -1.2, Fuse = -0.1, Merge = 0.6, Changed = 1.4, Lift = 1.8, Grab = 2.2,
        OffParty = 2.8, OnDeep = 2.9, Dive = 3.6, Hit = CapTrial.Hit,
        Rise = Hit + 0.5, Gone = Hit + 2.2;

    private const uint White = 0xFFFFFFu, Foam = 0xCFF4FFu, Petal = 0xF76B84u, PetalLight = 0xFFADBDu, PetalDark = 0xAD314Au, Heart = 0xF7EF5Au,
        Stem = 0x529410u, Leaf = 0x63CE08u, Outline = 0x313131u;

    /// <summary>Where it lies: on the stone floor between the chest and the column; null when the ruins are not drawn.</summary>
    public static (int X, int Y)? Spot(CapWorldLayout l, CapSeaContext ctx) =>
        CapGimmighoulRuins.Anchor(l, ctx) is var (x0, floor, _) ? (x0 + 97, floor - 10 - R(CapTrialCrystal.H * Lying)) : null;

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t)
    {
        if (!CritterArt.All.TryGetValue(Land, out var land) || !CritterArt.All.TryGetValue(Sky, out var sky) || !CritterArt.All.TryGetValue(Flower, out var flower)) return;
        if (l.Panel == CapPanelKind.Party) Party(p, l, crystal, t, land, sky, flower);
        else if (l.Panel == CapPanelKind.Deep) Deep(p, l, ctx, crystal, t, sky);
    }

    private static void Party(CapSheet p, CapWorldLayout l, CapTrialCrystal crystal, double t, CritterArt land, CritterArt sky, CritterArt flower)
    {
        if (CapFarSky.WormholeAt(l) is not { } core) return;
        foreach (var beat in (ReadOnlySpan<double>)[Beat1, Beat2])
        {
            var k = Math.Exp(-Math.Pow((t - beat) / 0.1, 2));
            CapTrial.Shine(p, core.X, core.Y, 18 + (10 * k), 15 + (8 * k), Petal, 170 * k);
        }

        var b = t - Born;
        if (b >= 0 && b < 0.6)
        {
            var k = 1 - (b / 0.6);
            CapTrial.Shine(p, core.X, core.Y, 22 * k, 18 * k, White, 255 * k);
            CapTrial.Ring(p, core.X, core.Y, 5 + (b * 90), 0.6, PetalLight, 220 * k);
        }

        if (t < Born || t > OffParty) return;
        var surface = l.SurfaceY;
        var bob = R(Math.Sin(t * 2.2) * 0.7);
        (double X, double Y) stand = (core.X - 42, surface + bob - 9), hover = (core.X - 14, core.Y + 12), back = (stand.X + 2, stand.Y - 9);

        // the ring it sits on: popping up under it as it comes down, popping away as it flies off
        if (t >= Lands - 0.12 && t < Lift + 0.25)
        {
            var pop = t - Lands + 0.12;
            var swell = pop < 0.1 ? 1.25 * (pop / 0.1) : pop < 0.22 ? 1.25 - (0.25 * ((pop - 0.1) / 0.12)) : 1;
            if (t > Lift) swell = 1 - ((t - Lift) / 0.25);
            if (pop < 0.3) CapTrial.Ring(p, stand.X, surface + bob, 6 + (pop * 60), 0.3, White, 220 * (1 - (pop / 0.3)));
            CapMachamp.Float(p, stand.X, surface + bob, 13 * swell, 4 * swell, 7.5 * swell, 1.6 * swell, (int)Math.Round(2 * swell));
        }

        // the crystal: out with them, then hanging in the air by the wormhole, glowing, until Shaymin takes it
        if (t < Grab)
        {
            var u = EaseOut((t - Born) / 0.8);
            double x = core.X + ((hover.X - core.X) * u), y = core.Y + ((hover.Y - core.Y) * u) + (Math.Sin(t * 2.5) * 1.5);
            CapTrial.Shine(p, x, y, 10, 8, crystal.Glow, 110);
            crystal.Paint(p, R(x), R(y), 0.3, t * 3, 0.5, t % 3 < 0.6 ? (t % 3) / 0.6 : -1);
        }

        // the Gracidea: drifting down slowly, swaying and turning, onto its back; then glowing there and going into it
        if (t >= Born + 0.3 && t < Merge)
        {
            double x, y, size = FlowerSize, keep = 1;
            if (t < Fuse)
            {
                var u = (t - Born - 0.3) / (Fuse - Born - 0.3);
                x = core.X + ((back.X - core.X) * u) + (Math.Sin(u * Math.PI * 3) * 10 * (1 - u));
                y = core.Y + ((back.Y - core.Y) * u);
            }
            else
            {
                var u = (t - Fuse) / (Merge - Fuse);
                x = back.X;
                y = back.Y + (u * 3);
                size = FlowerSize * (1 - (u * 0.6));
                keep = 1 - Math.Pow(u, 2);
                CapTrial.Shine(p, x, y, 16, 12, PetalLight, 200 * Math.Sin(Math.PI * u));
                for (var n = 0; n < 12; n++)
                {
                    var a = (n / 12.0 * 2 * Math.PI) + (u * 4);
                    var r = 16 * (1 - u);
                    Put(p, x + (Math.Cos(a) * r), y + (Math.Sin(a) * r * 0.7), n % 2 == 0 ? White : PetalLight, 230 * u);
                }
            }

            CapVisitors.Centred(p, flower, 0.5, R(x), R(y), size, false, White, 0, t * 60, keep);
        }

        // Shaymin: tumbling out, sitting on its ring breathing, changing, then flying up, to the crystal and away left
        if (t < Lands)
        {
            var u = (t - Born) / (Lands - Born);
            var grow = Clamp01(b / 0.3);
            double x = core.X + ((stand.X - core.X) * u), y = core.Y + ((stand.Y - core.Y) * u) - (24 * Math.Sin(Math.PI * u));
            CapVisitors.Centred(p, land, t, R(x), R(y), LandSize * grow, false, White, 0, 60 * Math.Sin(u * 7) * (1 - u));
        }
        else if (t < Merge)
        {
            CapVisitors.Centred(p, land, t, R(stand.X), R(stand.Y), LandSize, false, White, 0);
        }
        else if (t < Changed)
        {
            var T = t - Merge;
            var phase = (3 * T) + (10 * T * T / (2 * (Changed - Merge)));
            if ((int)Math.Floor(phase * 2) % 2 == 0) CapVisitors.Centred(p, land, t, R(stand.X), R(stand.Y), LandSize, false, White, 1);
            else CapVisitors.Centred(p, sky, t, R(stand.X), R(stand.Y - 6), SkySize, false, White, 1);
            Petals(p, stand.X, stand.Y - 2, T, 1);
            CapTrial.Shine(p, stand.X, stand.Y, 26, 20, PetalLight, 160);
        }
        else
        {
            var c = t - Changed;
            if (c < 0.3) Petals(p, stand.X, stand.Y - 6, 0.8 + c, 1 - (c / 0.3));
            (double X, double Y) at;
            if (t < Lift) at = (stand.X, stand.Y - 6 - (EaseOut((t - Changed) / (Lift - Changed)) * 22));
            else if (t < Grab)
            {
                var u = (t - Lift) / (Grab - Lift);
                at = (stand.X + ((hover.X - 2 - stand.X) * u), stand.Y - 28 + ((hover.Y - 14 - stand.Y + 28) * u));
            }
            else
            {
                var v = (t - Grab) / (OffParty - Grab);
                at = (hover.X - 2 + ((-40 - hover.X) * v * v), hover.Y - 14 - (v * 10));
            }

            CapVisitors.Centred(p, sky, t, R(at.X), R(at.Y + (Math.Sin(t * 6) * 1.5)), SkySize, false, White, c < 0.2 ? 1 - (c / 0.2) : 0, -8);
            if (t >= Grab) crystal.Paint(p, R(at.X + 2), R(at.Y + 14), 0.3, 0, 0.5, -1);
        }
    }

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    private static void Deep(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double t, CritterArt sky)
    {
        if (Spot(l, ctx) is not { } spot || t < OnDeep) return;
        var surface = l.SurfaceY;
        (double X, double Y) enter = (l.Cols + 40, surface * 0.5), dive = (spot.X + 50, surface), lay = (spot.X, spot.Y - 14);

        if (t < Gone)
        {
            double x, y;
            var holding = t < Hit;
            if (t < Dive)
            {
                var u = (t - OnDeep) / (Dive - OnDeep);
                x = enter.X + ((dive.X - enter.X) * u);
                y = enter.Y + ((dive.Y - enter.Y) * u * u);
            }
            else if (t < Hit)
            {
                var u = (t - Dive) / (Hit - Dive);
                var e = 1 - Math.Pow(1 - u, 2);
                x = dive.X + ((lay.X - dive.X) * e);
                y = dive.Y + ((lay.Y - dive.Y) * e);
            }
            else if (t < Rise)
            {
                x = lay.X;
                y = lay.Y + (Math.Sin((t - Hit) * 6) * 1.5);
            }
            else
            {
                var u = (t - Rise) / (Gone - Rise);
                x = lay.X - (u * 60);
                y = lay.Y + ((surface - 40 - lay.Y) * u);
            }

            var under = y > surface;
            if (under) Bubble(p, x, y, t);
            CapVisitors.Centred(p, sky, t, R(x), R(y + (Math.Sin(t * 6) * 1.5)), SkySize, false, White, 0, under ? 8 : -8);
            if (holding) crystal.Paint(p, R(x + 2), R(y + 14), 0.3, 0, 0.5, -1);
        }

        Splash(p, dive.X, surface, t - Dive);
        var risen = (surface - lay.Y) / (surface - 40 - lay.Y);
        Splash(p, lay.X - (risen * 60), surface, t - Rise - ((Gone - Rise) * risen));

        if (t >= Hit)
        {
            var f = t - Hit;
            if (f < 0.5) CapTrial.Shine(p, spot.X, spot.Y, 24 * (1 - (f / 0.5)), 12 * (1 - (f / 0.5)), PetalLight, 200 * (1 - (f / 0.5)));
            Rest(p, spot, crystal, t, f);
        }
    }

    /// <summary>The crystal lying on the floor and the garden round it, springing up one by one after it lands.</summary>
    private static void Rest(CapSheet p, (int X, int Y) spot, CapTrialCrystal crystal, double phase, double since)
    {
        var ground = spot.Y + R(CapTrialCrystal.H * Lying);
        foreach (var plant in Garden) if (!plant.Front) Grow(p, spot.X + plant.Dx, ground, plant, since - 0.15 - plant.At, phase);
        var glint = phase % 8;
        crystal.Paint(p, spot.X, spot.Y, Lying, 0, since < 0.6 ? 0.9 : 0.3, glint < 0.6 ? glint / 0.6 : -1);
        foreach (var plant in Garden) if (plant.Front) Grow(p, spot.X + plant.Dx, ground + 1, plant, since - 0.15 - plant.At, phase);
    }

    /// <summary>What grows round it: where from the crystal, how tall, when after it lands, what (Gracidea, daisy, bud, grass), and in front.</summary>
    private readonly record struct Plant(int Dx, int Tall, double At, int Kind, bool Front = false);

    private const int Gracidea = 0, Daisy = 1, Bud = 2, Grass = 3;

    private static readonly Plant[] Garden =
    [
        // (the chest is just to its left: the garden keeps to the right of it)
        new(-10, 3, 0.05, Grass), new(-8, 6, 0.20, Gracidea), new(-5, 3, 0.35, Bud), new(5, 3, 0.15, Bud), new(8, 6, 0.25, Gracidea),
        new(11, 3, 0.45, Daisy), new(13, 4, 0.30, Grass), new(16, 7, 0.12, Gracidea), new(19, 2, 0.38, Daisy), new(21, 3, 0.50, Grass),
        new(-4, 2, 0.55, Grass, true), new(4, 2, 0.60, Daisy, true), new(-1, 1, 0.65, Grass, true), new(2, 1, 0.70, Grass, true),
    ];

    /// <summary>A Gracidea's head, five pink petals round a pale heart: light, mid, dark, pale, yellow, orange.</summary>
    private static readonly string[] Head =
    [
        ".L.L.",
        "PLWLP",
        "LWYWL",
        "PLOLD",
        ".D.D.",
    ];

    /// <summary>
    /// One plant growing: its stem rising from the floor and bending a little with the current (more towards its top), a pair of leaves; then
    /// its head: a Gracidea opening from a bud into five petals with a yellow heart, a small white daisy, or a closed bud; or a tuft of grass
    /// blades swaying. A puff of petals as a flower opens, and now and then a petal comes loose and drifts up.
    /// </summary>
    private static void Grow(CapSheet p, int x, int ground, Plant plant, double age, double phase)
    {
        if (age < 0) return;
        var grow = Clamp01(age / 0.3);
        var current = phase * 1.2 + (x * 0.7);
        int Bend(int h, int height) => R(Math.Sin(current) * h / Math.Max(1.0, height) * 1.2);

        if (plant.Kind == Grass)
        {
            for (var blade = -1; blade <= 1; blade++)
            {
                var height = (int)Math.Round((plant.Tall + (blade == 0 ? 2 : 0)) * grow);
                for (var h = 1; h <= height; h++)
                {
                    var bx = x + blade + R(blade * h * 0.3) + Bend(h, height);
                    p.Set(bx, ground - h, h == height ? 0x9AE04Au : blade == 0 ? Leaf : 0x3E7A0Cu);
                }
            }

            return;
        }

        var tall = (int)Math.Round(plant.Tall * grow) + 1;
        for (var h = 1; h <= tall; h++) p.Set(x + Bend(h, tall), ground - h, h % 3 == 0 ? 0x3E7A0Cu : Stem);
        if (plant.Tall > 2 && grow > 0.6)
        {
            var mid = Math.Max(1, tall / 2);
            p.Set(x + Bend(mid, tall) - 1, ground - mid, Leaf);
            p.Set(x + Bend(mid, tall) - 2, ground - mid + 1, 0x3E7A0Cu);
            p.Set(x + Bend(mid - 1, tall) + 1, ground - mid + 1, Leaf);
            p.Set(x + Bend(mid - 1, tall) + 2, ground - mid + 1, 0x9AE04Au);
        }

        (int X, int Y) top = (x + Bend(tall, tall), ground - tall - 1);
        if (age < 0.3) return;
        if (plant.Kind == Bud || age < 0.45)
        {
            p.Set(top.X, top.Y + 1, 0x3E7A0Cu);
            p.Set(top.X, top.Y, PetalDark);
            p.Set(top.X, top.Y - 1, Petal);
            p.Set(top.X - 1, top.Y, Leaf);
            p.Set(top.X + 1, top.Y, Leaf);
            return;
        }

        if (plant.Kind == Daisy)
        {
            p.Set(top.X, top.Y, Heart);
            p.Set(top.X - 1, top.Y, White);
            p.Set(top.X + 1, top.Y, White);
            p.Set(top.X, top.Y - 1, White);
            p.Set(top.X, top.Y + 1, 0xD8D8E8u);
            p.Set(top.X - 1, top.Y + 1, 0xB8B8D0u, 160);
            p.Set(top.X + 1, top.Y - 1, 0xEEEEF8u, 200);
        }
        else
        {
            for (var row = 0; row < Head.Length; row++)
            {
                for (var col = 0; col < Head[row].Length; col++)
                {
                    uint? c = Head[row][col] switch
                    {
                        'L' => PetalLight, 'P' => Petal, 'D' => PetalDark, 'W' => 0xFFD6DEu, 'Y' => Heart, 'O' => 0xE8A040u, _ => null,
                    };
                    if (c is { } colour) p.Set(top.X - 2 + col, top.Y - 3 + row, colour);
                }
            }
        }

        var open = age - 0.45;
        if (open < 0.4)
        {
            for (var n = 0; n < 5; n++)
            {
                var a = n / 5.0 * 2 * Math.PI;
                Put(p, top.X + (Math.Cos(a) * (2 + (open * 14))), top.Y + (Math.Sin(a) * (2 + (open * 10))) - (open * 6), n % 2 == 0 ? PetalLight : Petal, 230 * (1 - (open / 0.4)));
            }
        }

        // a petal coming loose now and then and drifting up
        if (plant.Kind == Gracidea)
        {
            var drift = ((phase * 0.18) + (Math.Abs(x) * 0.137)) % 1;
            if (drift < 0.5)
            {
                var k = drift / 0.5;
                Put(p, top.X + (Math.Sin((k * 9) + x) * 3), top.Y - 3 - (k * 22), k < 0.5 ? PetalLight : Petal, 230 * (1 - k));
            }
        }
    }

    /// <summary>Petals whirling round a point.</summary>
    private static void Petals(CapSheet p, double cx, double cy, double t, double k)
    {
        for (var n = 0; n < 22; n++)
        {
            var a = (n / 22.0 * 2 * Math.PI) + (t * 5);
            var r = 10 + (n % 4 * 4) + (t * 6);
            double x = cx + (Math.Cos(a) * r), y = cy + (Math.Sin(a) * r * 0.7);
            Put(p, x, y, n % 3 == 0 ? PetalLight : n % 3 == 1 ? Petal : White, 240 * k);
            if (n % 2 == 0) Put(p, x + 1, y, PetalDark, 200 * k);
        }
    }

    /// <summary>The bubble round it under the water: a pale ring lit at the top left, a few bubbles off it.</summary>
    private static void Bubble(CapSheet p, double cx, double cy, double t)
    {
        const double R0 = 17;
        for (var i = 0; i < 90; i++)
        {
            var a = i / 90.0 * 2 * Math.PI;
            var lit = Math.Cos(a + 2.3) > 0.6;
            Put(p, cx + (Math.Cos(a) * R0), cy + (Math.Sin(a) * R0), lit ? White : Foam, lit ? 230 : 140);
        }

        for (var n = 0; n < 5; n++)
        {
            var age = ((t * 1.5) + (n / 5.0)) % 1;
            Put(p, cx + ((n - 2) * 5) + (Math.Sin((age * 8) + n) * 1.5), cy - R0 - (age * 16), Foam, 200 * (1 - age));
        }
    }

    /// <summary>A splash on the surface.</summary>
    private static void Splash(CapSheet p, double x, int surface, double sp)
    {
        if (sp < 0 || sp > 0.8) return;
        for (var n = 0; n < 24; n++)
        {
            var life = 0.4 + (Rnd(n, 9) * 0.4);
            if (sp > life) continue;
            var a = Math.PI * (0.15 + (Rnd(n, 3) * 0.7));
            var v = 20 + (Rnd(n, 5) * 34);
            var y = surface - (Math.Sin(a) * v * sp) + (110 * sp * sp);
            if (y <= surface + 1) Put(p, x - (Math.Cos(a) * v * sp), y, n % 3 != 0 ? Foam : White, 255 * (1 - (sp / life)));
        }
    }

    /// <summary>The Fitostal on the floor among its flowers for the rest of the run.</summary>
    public static void PaintRest(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapTrialCrystal crystal, double phase)
    {
        if (Spot(l, ctx) is { } spot) Rest(p, spot, crystal, phase, 30);
    }
}
