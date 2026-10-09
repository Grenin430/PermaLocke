using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A healing item going into the bag (2026-10-09): warmth and relief. The item floats up out of the bag and hangs there while
/// something good happens round it; there are four of those, and the one that is played is never the one that was played the time
/// before: bubbles of the bottle rising, crosses popping up, feathers coming down, or a heart that beats. Then it goes in, and a
/// band of colour goes over the leather as if it were being healed. Every fifth pickup the bag adds a small gesture of its own.
/// The stronger the item (a Potion, a Hyper Potion, a Max Revive), the more of it there is.
/// </summary>
/// <remarks>
/// 72 rows tall: what rises and what falls needs the room. The order of the four is a function of how many items have been picked
/// up (Item.Count), so that it is the same given the same history and never repeats at the seams.
/// </remarks>
public sealed class HealingStyle : ItemStyle
{
    public static HealingStyle Instance { get; } = new();

    public const int SceneHeight = 72;

    private const int Cx = 26;
    private const double RestY = 28;
    private const double MouthY = 54;
    private const int BagBottom = 66;
    private const int PlateLeft = 58;
    private const int PlateTop = 22;

    private static readonly uint Fallback = ItemCanvas.Bgra(0x70, 0xE0, 0x90);
    private static readonly uint Heart = ItemCanvas.Bgra(0xF0, 0x60, 0x80);
    private static readonly uint HeartLight = ItemCanvas.Bgra(0xFF, 0xB0, 0xC0);

    private static readonly int[] Factorials = [6, 2, 1, 1];

    /// <summary>A plus, five cells by five, for the corner of the plate.</summary>
    private static readonly (int X, int Y)[] Badge = [(2, 0), (2, 1), (2, 2), (2, 3), (2, 4), (0, 2), (1, 2), (3, 2), (4, 2)];

    public override int Height => SceneHeight;

    /// <summary>
    /// Which of the four plays: 0 bubbles, 1 crosses, 2 feathers, 3 the heart. Every four pickups are the four of them in an order
    /// that depends on the block, and the first of a block is never the last of the one before.
    /// </summary>
    public static int VariantFor(int count)
    {
        var block = Math.Max(0, count) / 4;
        var position = Math.Max(0, count) % 4;

        Span<int> order = stackalloc int[4];
        Order(block, order);

        // The first of a block is never the last of the one before: the first two swap, for the whole block, not only for the first.
        if (block > 0)
        {
            Span<int> before = stackalloc int[4];
            Order(block - 1, before);
            if (order[0] == before[3]) (order[0], order[1]) = (order[1], order[0]);
        }

        return order[position];
    }

    /// <summary>The order of the four in a block: one of the twenty-four permutations, by the block's number.</summary>
    private static void Order(int block, Span<int> order)
    {
        var index = (int)(ItemSeed.Mix(unchecked((uint)block * 2654435761u) + 17) % 24);
        Span<int> pool = stackalloc int[4];
        for (var k = 0; k < 4; k++) pool[k] = k;
        var size = 4;

        for (var i = 0; i < 4; i++)
        {
            var digit = index / Factorials[i];
            index %= Factorials[i];
            order[i] = pool[digit];

            for (var j = digit; j < size - 1; j++) pool[j] = pool[j + 1];
            size--;
        }
    }

    /// <summary>True on the fifth pickup of every five: the bag has something extra to say.</summary>
    public static bool ExtraFor(int count) => count % 5 == 4;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var strength = Math.Clamp(item.Power, 0, 3);
        var variant = VariantFor(item.Count);
        var extra = ExtraFor(item.Count);
        var sweepRight = seed.Pick(1, 2) == 0;

        var fallStart = p.WrapAt - 0.10;
        var fallEnd = fallStart + 0.25;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var u = Math.Clamp((t - p.ClimaxAt) / p.Climax, 0, 1);
        var landed = t - fallEnd;
        var offset = Rise(t, p.In);

        // The item floats up and hangs there, and comes down at the end.
        var shown = a >= 0.25 && t < fallEnd;
        var y = RestY;
        var scale = 1.0;

        if (shown)
        {
            if (t < p.ClimaxAt)
            {
                var e = (a - 0.25) / 0.75;
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Ease(e));
                scale = 0.5 + (0.5 * ItemCanvas.Ease(e));
            }
            else if (t < fallStart)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 3));
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                y = RestY + ((BagBottom - 9 - RestY) * g * g);
                scale = 1 - (0.3 * g);
            }
        }

        // The bag.
        var bagY = 1.0;
        var open = false;
        var shiver = 0;
        var hop = 0;
        uint stitch = 0;
        var wash = -1.0;

        if (t >= p.In && t < p.ClimaxAt)
        {
            bagY = a < 0.4 ? 1 - (0.12 * ItemCanvas.Smooth(a / 0.4)) : 0.88 + (0.12 * ItemCanvas.Ease((a - 0.4) / 0.6));
            open = a >= 0.25;
        }
        else if (t >= p.ClimaxAt && t < fallEnd)
        {
            open = true;
        }
        else if (t >= fallEnd)
        {
            bagY = Spring(landed, 0.14, 6, 22);
            open = landed < 0.10;

            // The band of colour going over the leather: it is being healed.
            if (landed is >= 0.04 and < 0.55)
            {
                var w = (landed - 0.04) / 0.51;
                wash = (sweepRight ? w : 1 - w) * 23;
                stitch = prism.Pale;
            }

            if (extra)
            {
                // Every fifth: two hops, or a quick wiggle, by seed, and a star over the buckle.
                if (seed.Pick(2, 2) == 0)
                {
                    hop = landed is >= 0.10 and < 0.40 ? (int)Math.Round(Math.Abs(Math.Sin((landed - 0.10) / 0.15 * Math.PI)) * 3) : 0;
                }
                else
                {
                    shiver = landed is >= 0.10 and < 0.45 ? (int)Math.Round(Math.Sin(landed * 70) * 2) : 0;
                }
            }
        }

        var bagX = 1 + ((1 - bagY) * 0.5);

        // The stage: a pad with a plus on it, which brightens while the good thing is happening.
        Ground(c, Cx, BagBottom + offset, 15, 3, prism.Deep, 8 + (u * 4));
        for (var k = -5; k <= 5; k++)
        {
            if ((k & 1) == 0)
            {
                Dot(c, Cx + k, BagBottom + 1 + offset, prism.Base);
                if (Math.Abs(k) <= 1) Dot(c, Cx, BagBottom + 1 + offset + (k == 0 ? 1 : k), prism.Base);
            }
        }

        if (variant == 3) Hearts(c, item, strength, u, y);
        if (shown) c.Icon(item, Cx, y, scale, scale);

        if (t >= p.ClimaxAt && t < fallEnd)
        {
            switch (variant)
            {
                case 0: Bubbles(c, prism, seed, strength, t - p.ClimaxAt, y); break;
                case 1: Crosses(c, prism, seed, strength, t - p.ClimaxAt, y); break;
                case 2: Feathers(c, prism, seed, strength, t - p.ClimaxAt, y); break;
            }
        }

        c.Bag(Cx - 12, BagBottom + offset - hop, open, bagX, bagY, shiver, stitch, -1, 0, wash, prism.Light, 5);

        if (extra && landed is >= 0.12 and < 0.45) ItemFx.Star(c, Cx + 9, BagBottom - 22 + offset, 3, ItemCanvas.White, prism.Pale);

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop, "CURATIVO", prism.Base, prism.Pale, Badge);
    }

    /// <summary>The bubbles of the bottle: small rings that rise from the item, swaying, and pop at the top.</summary>
    private static void Bubbles(ItemCanvas c, ItemPrism prism, ItemSeed seed, int strength, double s, double y)
    {
        var count = 5 + (2 * strength);
        for (var i = 0; i < count; i++)
        {
            var born = 0.04 * i;
            var life = (s - born) / 0.55;
            if (life is <= 0 or >= 1) continue;

            var gx = (int)Math.Round(Cx + (((seed.Unit(100 + i) * 2) - 1) * 9) + (Math.Sin((life * 7) + i) * 2));
            var gy = (int)Math.Round(y - 6 - (24 * life));
            var size = 1 + (i % 2);

            if (life > 0.88)
            {
                // The pop: a plus that stays for a moment.
                ItemFx.Cross(c, gx, gy, 1, ItemCanvas.White);
                continue;
            }

            // A ring of three cells by three, or a single bright cell for the small ones.
            if (size == 2)
            {
                Dot(c, gx - 1, gy, prism.Light);
                Dot(c, gx + 1, gy, prism.Light);
                Dot(c, gx, gy - 1, prism.Pale);
                Dot(c, gx, gy + 1, prism.Light);
            }
            else
            {
                Dot(c, gx, gy, prism.Pale);
            }
        }
    }

    /// <summary>Crosses that pop up round the item and go up while they thin out.</summary>
    private static void Crosses(ItemCanvas c, ItemPrism prism, ItemSeed seed, int strength, double s, double y)
    {
        var count = 3 + strength;
        for (var i = 0; i < count; i++)
        {
            var life = (s - (0.07 * i)) / 0.5;
            if (life is <= 0 or >= 1) continue;

            var gx = (int)Math.Round(Cx + (((seed.Unit(120 + i) * 2) - 1) * 12));
            var gy = (int)Math.Round(y - 2 - (16 * life) - (6 * seed.Unit(140 + i)));
            var arm = strength >= 2 ? 3 : 2;

            // Whole while it rises; the last third goes out through the dither.
            if (life > 0.66 && ItemCanvas.Bayer[gy & 3, gx & 3] >= (1 - life) * 3 * 16) continue;
            ItemFx.Cross(c, gx, gy, arm, i % 2 == 0 ? ItemCanvas.White : prism.Pale, ItemCanvas.White);
        }
    }

    /// <summary>Feathers that come down slowly from above and sway, white, one cell wider each.</summary>
    private static void Feathers(ItemCanvas c, ItemPrism prism, ItemSeed seed, int strength, double s, double y)
    {
        var count = 3 + strength;
        for (var i = 0; i < count; i++)
        {
            var life = (s - (0.08 * i)) / 0.7;
            if (life is <= 0 or >= 1) continue;

            var gx = (int)Math.Round(Cx + (((seed.Unit(160 + i) * 2) - 1) * 12) + (Math.Sin((life * 6) + (i * 2)) * 4));
            var gy = (int)Math.Round(y - 18 + (26 * life));

            // A feather is a slanted stroke of five cells, the quill darker.
            for (var k = 0; k < 5; k++)
            {
                Dot(c, gx + k, gy - (k / 2), k == 0 ? prism.Light : ItemCanvas.White);
            }

            Dot(c, gx + 1, gy, prism.Pale);
            if (life > 0.8 && ((int)(s * 30) & 1) == 0) Dot(c, gx + 2, gy - 2, ItemCanvas.White);
        }
    }

    /// <summary>A heart over the item that beats two or three times, with a small ring at each beat.</summary>
    private static void Hearts(ItemCanvas c, ItemScene.Item item, int strength, double u, double y)
    {
        if (u <= 0 || u >= 1) return;

        var beats = 2 + (strength >= 2 ? 1 : 0);
        var phase = u * beats;
        var beat = (int)Math.Floor(phase);
        var along = phase - beat;
        var big = along < 0.35;
        var top = (int)Math.Round(y - 20 - (u * 4));

        // The heart: seven cells wide, in two sizes, the bigger one on the beat.
        for (var row = 0; row < 6; row++)
        {
            var left = big ? HeartBig[row] : HeartSmall[row];
            for (var col = left; col < 7 - left; col++)
            {
                if (row == 0 && col == 3) continue;   // the notch between the two lobes
                Dot(c, Cx - 3 + col, top + row, row == 1 && col == 1 ? HeartLight : Heart);
            }
        }

        if (along < 0.5) Ring(c, Cx, top + 3, 6 + (12 * along * 2), HeartLight, (1 - (along * 2)) * 16, 1, 0, 0, 0.8);
    }

    private static readonly int[] HeartBig = [1, 0, 0, 1, 2, 3];
    private static readonly int[] HeartSmall = [2, 1, 1, 2, 3, 3];
}
