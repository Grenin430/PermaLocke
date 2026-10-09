using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A berry going into the bag (2026-10-09): organic and playful. It is thrown out of the bag, stretched while it is fast and
/// squashed when it lands, bounces on the rim of the bag once or twice with a squirt of its juice each time, and goes in while
/// leaves flutter down; then the bag chews it two or three times and ends with a hiccup. The juice is the colour of the berry; the
/// tufts of grass under the bag are the cheap stage.
/// </summary>
public sealed class BerryStyle : ItemStyle
{
    public static BerryStyle Instance { get; } = new();

    public const int SceneHeight = 64;

    private const int Cx = 24;
    private const double RimY = 38;
    private const double MouthY = 46;
    private const int BagBottom = 58;
    private const int PlateLeft = 52;
    private const int PlateTop = 18;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xE0, 0x50, 0x70);
    private static readonly uint LeafLight = ItemCanvas.Bgra(0x78, 0xC8, 0x48);
    private static readonly uint LeafDark = ItemCanvas.Bgra(0x3C, 0x8C, 0x2C);

    /// <summary>A small leaf, five cells by five, for the corner of the plate.</summary>
    private static readonly (int X, int Y)[] Badge = [(0, 4), (1, 3), (2, 2), (3, 1), (4, 0), (2, 3), (3, 2), (1, 2), (3, 3)];

    public override int Height => SceneHeight;

    /// <summary>How many times the bag chews: two or three, by seed.</summary>
    public static int ChewsFor(int seed) => 2 + new ItemSeed(seed).Pick(3, 2);

    /// <summary>How many times it bounces on the rim before going in: one or two, by seed.</summary>
    public static int BouncesFor(int seed) => 1 + new ItemSeed(seed).Pick(2, 2);

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var bounces = BouncesFor(item.Seed);
        var chews = ChewsFor(item.Seed);
        var leaves = 2 + seed.Pick(1, 3);
        var side = seed.Pick(4, 2) == 0 ? 1 : -1;
        var offset = Rise(t, p.In);

        // The flight: out of the bag, down onto the rim, the small bounces, and in.
        var pop = p.In + (0.35 * p.Anticipation);
        var first = p.ClimaxAt + 0.12;
        var gap = bounces == 2 ? 0.20 : 0.0;
        var second = first + gap;
        var inside = second + 0.16;

        var shown = t >= pop && t < inside;
        var x = (double)Cx;
        var y = MouthY;
        var sx = 1.0;
        var sy = 1.0;

        if (shown)
        {
            double s;
            if (t < first)
            {
                s = (t - pop) / (first - pop);
                y = MouthY - 2 + ((RimY - (MouthY - 2)) * s) - (4 * 30 * s * (1 - s));
            }
            else if (bounces == 2 && t < second)
            {
                s = (t - first) / (second - first);
                y = RimY - (4 * 9 * s * (1 - s));
            }
            else
            {
                var from = bounces == 2 ? second : first;
                s = (t - from) / (inside - from);
                y = RimY + ((MouthY + 4 - RimY) * s) - (4 * 5 * s * (1 - s));
            }

            // Stretched while it is fast, near the ends of an arc, and flat against the rim for a moment after each landing.
            sy = 1 + (0.25 * Math.Abs(Math.Cos(Math.PI * s)));
            sx = 1 / sy;
            x += side * Math.Sin(s * Math.PI) * 2;

            if (Squashed(t, first) || (bounces == 2 && Squashed(t, second)))
            {
                sy = 0.7;
                sx = 1.3;
            }
        }

        // The bag: the crouch before the throw, a dip at every landing, and when it has it, the chewing and the hiccup.
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var open = t >= p.In + (0.25 * p.Anticipation) && t < inside + 0.05;
        var bagX = 1.0;
        var bagY = 1.0;
        var hop = 0;
        var hiccup = false;

        if (t >= p.In && t < pop) bagY = 1 - (0.16 * ItemCanvas.Smooth(a / 0.35));
        else if (t >= pop && t < pop + 0.12) bagY = 0.84 + (0.30 * ItemCanvas.Smooth((t - pop) / 0.12));

        Dip(t, first, ref bagY);
        if (bounces == 2) Dip(t, second, ref bagY);

        var chewStart = inside + 0.04;
        var chewEnd = chewStart + (chews * 0.12);
        if (t >= chewStart && t < chewEnd)
        {
            var w = Math.Sin(2 * Math.PI * (t - chewStart) / 0.12);
            bagX = 1 + (0.10 * w);
            bagY = 1 - (0.07 * w);
        }
        else if (t >= chewEnd && t < chewEnd + 0.30)
        {
            // The hiccup: a hop of two or three rows and two small marks beside the bag.
            var w = (t - chewEnd - 0.04) / 0.14;
            if (w is >= 0 and <= 1) hop = (int)Math.Round(Math.Sin(w * Math.PI) * 3);
            hiccup = t - chewEnd < 0.24;
        }

        // The stage: grass under the bag, swaying.
        Ground(c, Cx, BagBottom + offset, 15, 3, ItemTint.Shade(LeafDark, 0, 0.18), 9);
        for (var blade = 0; blade < 8; blade++)
        {
            var gx = Cx - 15 + (blade * 4) + (int)Math.Round(Math.Sin((t * 3) + blade));
            var height = 2 + (blade % 3);
            for (var h = 0; h < height; h++) Dot(c, gx, BagBottom + 1 + offset - h, h == height - 1 ? LeafLight : LeafDark);
        }

        if (shown) c.Icon(item, x, y, sx, sy);

        c.Bag(Cx - 12, BagBottom + offset - hop, open, bagX, bagY, 0, 0);

        Leaves(c, seed, leaves, side, pop, t);
        Juice(c, prism, seed, first, t, 1);
        if (bounces == 2) Juice(c, prism, seed, second, t, 2);
        Juice(c, prism, seed, inside, t, 3);

        if (hiccup)
        {
            var w = t - chewEnd;
            var tick = w < 0.12 ? 3 : 2;
            for (var k = 0; k < tick; k++)
            {
                Dot(c, Cx + 15, BagBottom - 22 + offset - k, ItemCanvas.White);
                Dot(c, Cx + 18, BagBottom - 19 + offset - k, ItemCanvas.White);
            }
        }

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop, "BAYA", prism.Base, prism.Pale, Badge);
    }

    /// <summary>The berry is flat against the rim for a moment after it lands.</summary>
    private static bool Squashed(double t, double landing) => t - landing is >= 0 and < 0.07;

    /// <summary>The bag gives a little under the blow of the landing.</summary>
    private static void Dip(double t, double landing, ref double bagY)
    {
        var after = t - landing;
        if (after is >= 0 and < 0.10) bagY = Math.Min(bagY, 1 - (0.10 * Math.Sin(after / 0.10 * Math.PI)));
    }

    /// <summary>Leaves that come away with the throw and flutter down, swaying.</summary>
    private static void Leaves(ItemCanvas c, ItemSeed seed, int leaves, int side, double pop, double t)
    {
        for (var i = 0; i < leaves; i++)
        {
            var born = pop + (0.04 * i);
            var life = (t - born) / 0.85;
            if (life is <= 0 or >= 1) continue;

            var x0 = Cx + (side * 3) + (int)Math.Round(((seed.Unit(40 + i) * 2) - 1) * 9);
            var gx = (int)Math.Round(x0 + (Math.Sin((life * 5.5) + (i * 1.7)) * 5));
            var gy = (int)Math.Round(MouthY - 6 - (8 * Math.Sin(life * Math.PI * 0.6)) + (30 * life * life));

            // A leaf is four cells in a slant, a darker one at its tip.
            if (life > 0.8 && ((int)(t * 30) & 1) == 0) continue;
            Dot(c, gx, gy, LeafLight);
            Dot(c, gx + 1, gy, LeafLight);
            Dot(c, gx + 1, gy + 1, LeafLight);
            Dot(c, gx + 2, gy + 1, LeafDark);
        }
    }

    /// <summary>Three drops of juice that jump from the berry at every landing and fall.</summary>
    private static void Juice(ItemCanvas c, ItemPrism prism, ItemSeed seed, double at, double t, int salt)
    {
        var life = (t - at) / 0.35;
        if (life is <= 0 or >= 1) return;

        for (var i = 0; i < 3; i++)
        {
            var vx = (i - 1) * (5 + (4 * seed.Unit(60 + i + (salt % 7))));
            var gx = (int)Math.Round(Cx + (vx * life * 2));
            var gy = (int)Math.Round(RimY - 2 - (16 * life) + (38 * life * life));
            if (life > 0.7 && ((int)(t * 30) & 1) == 0) continue;

            Dot(c, gx, gy, life < 0.5 ? prism.Light : prism.Base);
            if (life < 0.3) Dot(c, gx, gy - 1, prism.Pale);
        }
    }
}
