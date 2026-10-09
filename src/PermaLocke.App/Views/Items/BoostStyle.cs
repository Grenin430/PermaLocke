using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// An upgrade (Rare Candy, vitamins, PP Up, feathers, the Super Candy) going into the bag (2026-10-09): progress and reward, so as
/// never to be taken for a healing item. The item rises and arrows climb a ladder above it one at a time, in beats, with a tick at
/// each, a meter beside the bag fills step by step, and when the last one is on the item goes in and the bag grows for an instant,
/// as a level does. The stronger the upgrade, the more arrows: two for a feather or a PP Up, five for the Super Candy.
/// </summary>
/// <remarks>
/// 72 rows tall: the ladder needs the room. The colours are gold and lime, always, and not the item's: that is what tells it from
/// a potion at a glance.
/// </remarks>
public sealed class BoostStyle : ItemStyle
{
    public static BoostStyle Instance { get; } = new();

    public const int SceneHeight = 72;

    private const int Cx = 24;
    private const double RestY = 36;
    private const double MouthY = 54;
    private const int BagBottom = 66;
    private const int PlateLeft = 58;
    private const int PlateTop = 22;

    private static readonly uint Gold = ItemCanvas.Bgra(0xFF, 0xD8, 0x40);
    private static readonly uint Lime = ItemCanvas.Bgra(0xA8, 0xE8, 0x40);
    private static readonly uint LimeDark = ItemCanvas.Bgra(0x58, 0x98, 0x20);

    /// <summary>An arrow going up, five cells by six.</summary>
    private static readonly string[] Arrow = ["..#..", ".###.", "#####", "..#..", "..#..", "..#.."];

    private static readonly (int X, int Y)[] Badge = [(2, 0), (1, 1), (3, 1), (0, 2), (4, 2), (2, 2), (2, 3), (2, 4)];

    public override int Height => SceneHeight;

    /// <summary>How many arrows an upgrade of a power has: 2, 3, 4 or 5.</summary>
    public static int ArrowsFor(int power) => 2 + Math.Clamp(power, 0, 3);

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var arrows = ArrowsFor(item.Power);
        var side = seed.Pick(1, 2) == 0 ? 1 : -1;
        var warm = seed.Pick(2, 2) == 0;
        var offset = Rise(t, p.In);

        var fallStart = p.WrapAt - 0.02;
        var fallEnd = fallStart + 0.22;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var landed = t - fallEnd;
        var step = Math.Min(0.14, (p.Climax - 0.1) / arrows);

        // How many arrows are on: one every beat from just after the climax begins, whole numbers, no in-betweens.
        var on = t < p.ClimaxAt + 0.05 ? 0 : Math.Min(arrows, 1 + (int)((t - p.ClimaxAt - 0.05) / step));
        var lastAt = p.ClimaxAt + 0.05 + ((arrows - 1) * step);

        // The item, which goes up a row with each beat.
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
                y = RestY - on;
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                y = RestY - on + ((BagBottom - 9 - (RestY - on)) * g * g);
                scale = 1 - (0.25 * g);
            }
        }

        // The bag grows for an instant when it has it.
        var bagY = 1.0;
        var open = false;
        uint stitch = 0;

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
            bagY = Spring(landed, -0.18, 7, 20);
            open = landed < 0.08;
            if (landed < 0.3) stitch = Gold;
        }

        var bagX = 1 + ((1 - bagY) * 0.5);

        // The stage: a meter of three bars beside the bag that fills as the arrows come.
        Ground(c, Cx, BagBottom + offset, 14, 3, LimeDark, 7);
        var filled = t >= fallStart ? arrows : on;
        for (var bar = 0; bar < 3; bar++)
        {
            var full = 4 + (3 * bar);
            var height = (int)Math.Round(full * filled / (double)arrows);
            for (var row = 0; row < height; row++)
            {
                Dot(c, Cx + 15 + (bar * 3), BagBottom + offset - row, row == height - 1 ? Gold : Lime);
                Dot(c, Cx + 16 + (bar * 3), BagBottom + offset - row, row == height - 1 ? Gold : LimeDark);
            }
        }

        // The item takes a little of the gold at each beat.
        var sinceBeat = on > 0 ? t - (p.ClimaxAt + 0.05 + ((on - 1) * step)) : 1;
        if (shown) c.Icon(item, Cx, y, scale, scale, -1, Gold, t < fallStart && sinceBeat < 0.06 ? 0.45 : 0);

        // The ladder: a staircase of arrows above the item, climbing to one side, one more on every beat; each is whole as it
        // comes and gone at the end.
        if (t >= p.ClimaxAt && t < fallStart + 0.1)
        {
            var thin = t >= fallStart ? Math.Clamp((t - fallStart) / 0.1, 0, 1) : 0;

            for (var i = 0; i < on; i++)
            {
                var born = p.ClimaxAt + 0.05 + (i * step);
                var left = side > 0 ? Cx - 17 + (i * 7) : Cx + 12 - (i * 7);
                var top = 17 - (i * 4);

                var colour = (i & 1) == (warm ? 0 : 1) ? Gold : Lime;
                var dither = (1 - thin) * 16;

                DrawArrow(c, left, top, colour, ItemCanvas.White, dither, t - born < 0.06);

                // The tick of the beat: a stroke on each side of the arrow, for the instant it comes.
                if (t - born < 0.08)
                {
                    for (var k = 0; k < 2; k++)
                    {
                        Dot(c, left - 3 - k, top + 2, ItemCanvas.White);
                        Dot(c, left + 7 + k, top + 2, ItemCanvas.White);
                    }
                }
            }

            // The last one is on: a small ring and a star over the top of the ladder.
            if (on == arrows && t - lastAt is >= 0 and < 0.35)
            {
                var w = (t - lastAt) / 0.35;
                var topY = Math.Max(3, 17 - ((arrows - 1) * 4) - 4);
                var topX = (side > 0 ? Cx - 17 + ((arrows - 1) * 7) : Cx + 12 - ((arrows - 1) * 7)) + 2;
                Ring(c, topX, topY, 4 + (8 * ItemCanvas.Ease(w)), Gold, (1 - w) * 16, 1, 8, 0, 0.7);
                ItemFx.Star(c, topX, topY, w < 0.5 ? 3 : 2, ItemCanvas.White, Gold);
            }
        }

        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, 0, stitch);

        // The level: three short strokes going up from the bag when it takes the item.
        if (landed is >= 0 and < 0.35)
        {
            var w = landed / 0.35;
            for (var k = -1; k <= 1; k++)
            {
                var gy = (int)Math.Round(MouthY + 2 - (14 * ItemCanvas.Ease(w)));
                if (((int)(landed * 30) & 1) == 0 && w > 0.6) continue;
                Dot(c, Cx + (k * 5), gy, Gold);
                Dot(c, Cx + (k * 5), gy + 1, Lime);
            }
        }

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop, "MEJORA", Gold, Lime, Badge);
    }

    /// <summary>One arrow, through the dither, with its tip lit for the instant it comes on.</summary>
    private static void DrawArrow(ItemCanvas c, int left, int top, uint colour, uint tip, double density, bool fresh)
    {
        for (var row = 0; row < Arrow.Length; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                if (Arrow[row][col] != '#') continue;

                var gx = left + col;
                var gy = top + row;
                if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

                c.Put(gx, gy, fresh && row < 3 ? tip : colour);
            }
        }
    }
}
