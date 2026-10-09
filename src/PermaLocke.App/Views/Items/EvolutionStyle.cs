using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// An evolution item going into the bag (2026-10-09): a gesture, not an event, so that it can never be taken for a Mega Stone. The
/// stone hops out of the bag and beats three or four times, faster each time, with its outline pulsing and particles going round
/// it on tilted orbits, in front of it and behind it; then it drops into the bag, and for two frames (35 ms each) the bag is a white
/// silhouette: the transformation. A small ring and a few sparks, and the bag settles.
/// </summary>
/// <remarks>
/// 72 rows tall: the orbits are flat and need little more than the common scene. No vignette, no strands, no glyph, no shock rings:
/// those are the Mega Stones'. A stone (power 1) has two orbits and a trade item (power 0) one, with fewer particles.
/// </remarks>
public sealed class EvolutionStyle : ItemStyle
{
    public static EvolutionStyle Instance { get; } = new();

    public const int SceneHeight = 72;

    private const int Cx = 26;
    private const double RestY = 26;
    private const double MouthY = 54;
    private const int BagBottom = 66;
    private const int PlateLeft = 58;
    private const int PlateTop = 22;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xF0, 0xE0, 0x90);

    private static readonly (int X, int Y)[] StoneBadge = [(2, 0), (1, 1), (3, 1), (0, 2), (4, 2), (1, 3), (3, 3), (2, 4), (2, 2)];
    private static readonly (int X, int Y)[] TradeBadge = [(1, 0), (2, 0), (3, 0), (0, 1), (4, 1), (0, 2), (4, 2), (0, 3), (4, 3), (1, 4), (2, 4), (3, 4)];

    private static readonly double[] Beats = [0.10, 0.42, 0.68, 0.86];

    public override int Height => SceneHeight;

    /// <summary>Two orbits for a stone, one for a trade item.</summary>
    public static int OrbitsFor(int power) => power > 0 ? 2 : 1;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var orbits = OrbitsFor(item.Power);
        var particles = (item.Power > 0 ? 8 : 5) + seed.Pick(1, 2);
        var spin = seed.Pick(2, 2) == 0 ? 1 : -1;
        var tilt = 0.45 + (0.5 * seed.Unit(3));

        var fallStart = p.WrapAt - 0.05;
        var fallEnd = fallStart + 0.30;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var u = Math.Clamp((t - p.ClimaxAt) / p.Climax, 0, 1);
        var landed = t - fallEnd;
        var offset = Rise(t, p.In);

        // The beats: four, closer and closer, each a knock that falls away.
        var pulse = 0.0;
        if (t >= p.ClimaxAt && t < fallStart)
        {
            foreach (var at in Beats)
            {
                var since = t - (p.ClimaxAt + (p.Climax * at));
                if (since >= 0) pulse = Math.Max(pulse, Math.Exp(-since * 10));
            }
        }

        // The stone.
        var shown = a >= 0.30 && t < fallEnd;
        var y = RestY;
        var sx = 1.0;
        var sy = 1.0;

        if (shown)
        {
            if (t < p.ClimaxAt)
            {
                var e = (a - 0.30) / 0.70;
                var scale = 0.5 + (0.5 * ItemCanvas.Ease(e));
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Ease(e)) - (Math.Sin(e * Math.PI) * 5);
                sx = scale;
                sy = scale;
            }
            else if (t < fallStart)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 3));
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                y = RestY + ((BagBottom - 8 - RestY) * g * g);
                sx = 1 - (0.2 * g);
                sy = 1 + (0.15 * g);
            }
        }

        // The bag.
        var bagY = 1.0;
        var open = false;
        var shiver = 0;
        var bulgeRow = -1.0;
        var bulge = 0.0;
        uint flat = 0;

        if (t >= p.In && t < p.ClimaxAt)
        {
            bagY = a < 0.35 ? 1 - (0.16 * ItemCanvas.Smooth(a / 0.35))
                : a < 0.5 ? 0.84 + (0.24 * ItemCanvas.Smooth((a - 0.35) / 0.15))
                : 1 + (0.08 * (1 - ItemCanvas.Ease((a - 0.5) / 0.5)));
            open = a >= 0.30;
        }
        else if (t >= p.ClimaxAt && t < fallEnd)
        {
            open = true;
            shiver = pulse > 0.5 ? ((int)(t * 20) & 1) == 0 ? 1 : -1 : 0;
        }
        else if (t >= fallEnd)
        {
            bagY = Spring(landed, 0.18, 6, 24);
            open = landed < 0.08;
            shiver = landed < 0.35 ? (int)Math.Round(Math.Sin(36 * landed) * Math.Exp(-8 * landed) * 1.4) : 0;

            // The transformation: two frames of silhouette, white and then in the stone's light colour.
            if (landed < FlashPhase) flat = ItemCanvas.White;
            else if (landed < 2 * FlashPhase) flat = prism.Light;

            if (landed is >= 0.05 and < 0.28)
            {
                var w = (landed - 0.05) / 0.23;
                bulgeRow = 5 + (13 * w);
                bulge = 0.16 * Math.Sin(Math.PI * w);
            }
        }

        var bagX = 1 + ((1 - bagY) * 0.6);

        // The stage: the ground and a ring of marks that turns and wakes with the beats.
        Ground(c, Cx, BagBottom + offset, 14, 3, prism.Deep, 9);
        Ring(c, Cx, BagBottom + offset, 16, prism.Base, 7 + (pulse * 8), 1, 6, spin * t * 0.12, 0.25);

        // The orbits, behind the stone first and in front of it after.
        var phase = spin * ((0.35 * Math.Max(0, t - p.In)) + (1.2 * u * u * Math.Max(0, t - p.ClimaxAt)));
        var reach = 16 + (4 * u) + (3 * pulse);
        if (t >= fallEnd) reach = 0;
        else if (t >= fallStart) reach *= 1 - ItemCanvas.Smooth((t - fallStart) / (fallEnd - fallStart));

        Orbits(c, prism, orbits, particles, tilt, phase, reach, a, (int)Cx, y, front: false);

        if (shown)
        {
            if (pulse > 0.08)
            {
                c.IconOutline(item, Cx, y, sx, sy, pulse > 0.66 ? prism.Pale : pulse > 0.33 ? prism.Light : prism.Base, pulse > 0.7 ? 2 : 1);
            }

            c.Icon(item, Cx, y, sx, sy, -1, ItemCanvas.White, pulse * 0.8);
        }

        Orbits(c, prism, orbits, particles, tilt, phase, reach, a, Cx, y, front: true);

        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, shiver, 0, bulgeRow, bulge, -1, 0, 6, 0, flat);

        // After the transformation a small ring over the mouth and a few sparks.
        if (landed is >= 0.06 and < 0.40)
        {
            var w = (landed - 0.06) / 0.34;
            Ring(c, Cx, MouthY + 4, 4 + (14 * ItemCanvas.Ease(w)), prism.Pale, (1 - w) * 16, 1, 10, 0, 0.5);

            for (var i = 0; i < 4; i++)
            {
                var angle = Math.PI * (1.2 + (0.6 * i / 3.0));
                var r = 5 + (16 * ItemCanvas.Ease(w));
                Dot(c, (int)Math.Round(Cx + (Math.Cos(angle) * r)), (int)Math.Round(MouthY + 4 + (Math.Sin(angle) * r)), prism.Hue(i * 2));
            }
        }

        PlateIn(c, item, t, 0.25, PlateLeft, PlateTop, "EVOLUCION", prism.Base, prism.Pale, item.Power > 0 ? StoneBadge : TradeBadge);
    }

    /// <summary>
    /// The particles on tilted ellipses: the half of an orbit that is nearer goes in front of the stone, the rest behind it, which
    /// is all the depth there is. They come in one by one while the stone rises.
    /// </summary>
    private static void Orbits(ItemCanvas c, ItemPrism prism, int orbits, int particles, double tilt, double phase, double reach,
        double a, int cx, double cy, bool front)
    {
        if (reach < 1) return;

        for (var orbit = 0; orbit < orbits; orbit++)
        {
            var slant = orbit == 0 ? 0 : tilt;
            var cos = Math.Cos(slant);
            var sin = Math.Sin(slant);

            for (var k = 0; k < particles; k++)
            {
                if (a < 0.45 + (0.55 * (k + 1) / particles)) continue;

                var theta = 2 * Math.PI * ((k / (double)particles) + phase + (orbit * 0.5));
                var ex = reach * Math.Cos(theta);
                var ey = reach * 0.33 * Math.Sin(theta);
                if ((Math.Sin(theta) > 0) != front) continue;

                var gx = (int)Math.Round(cx + (ex * cos) - (ey * sin));
                var gy = (int)Math.Round(cy + (ex * sin) + (ey * cos));
                var colour = front ? (k % 2 == 0 ? prism.Pale : prism.Light) : ItemCanvas.Lerp(prism.Base, prism.Deep, 0.4);
                Block(c, gx, gy, front ? 2 : 1, colour);
            }
        }
    }
}
