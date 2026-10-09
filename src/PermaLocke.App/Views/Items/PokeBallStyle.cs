using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A Poké Ball going into the bag (2026-10-09): the sound of a ball. It is thrown out of the bag spinning, lands on the edge of the
/// mouth and bounces two or three times, each smaller than the last, with a ring of dust and a tick at every landing and the bag
/// giving under it; on the last landing its button clicks, two frames of white at the middle of the ball and three strokes, and it
/// goes in; the bag boings, stretching tall first and then settling. The colour is the ball's own, and a Master Ball or a Beast Ball
/// has a ring of stars turning round it.
/// </summary>
public sealed class PokeBallStyle : ItemStyle
{
    public static PokeBallStyle Instance { get; } = new();

    public const int SceneHeight = 64;

    private const int Cx = 24;
    private const double RimY = 33;
    private const double MouthY = 46;
    private const int BagBottom = 58;
    private const int PlateLeft = 52;
    private const int PlateTop = 18;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xE0, 0x30, 0x30);
    private static readonly double[] Lengths = [0.28, 0.20, 0.15];
    private static readonly double[] Heights = [11, 6, 3];

    /// <summary>A ball, five cells by five.</summary>
    private static readonly (int X, int Y)[] Badge =
        [(1, 0), (2, 0), (3, 0), (0, 1), (4, 1), (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (0, 3), (4, 3), (1, 4), (2, 4), (3, 4)];

    public override int Height => SceneHeight;

    /// <summary>How many times it bounces on the edge before the click: two or three, by seed.</summary>
    public static int BouncesFor(int seed) => 2 + new ItemSeed(seed).Pick(1, 2);

    /// <summary>The moment the button clicks: the last landing.</summary>
    public static double ClickAt(ItemPhases p, int bounces) => LandingAt(p, bounces);

    /// <summary>When the ball comes down for the k-th time: 0 is the first, on the edge; <c>bounces</c> is the click.</summary>
    private static double LandingAt(ItemPhases p, int k)
    {
        var at = p.ClimaxAt + 0.18;
        for (var i = 0; i < k; i++) at += Lengths[i];
        return at;
    }

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var bounces = BouncesFor(item.Seed);
        var side = seed.Pick(2, 2) == 0 ? 1 : -1;
        var spin = 1.6 + (seed.Unit(3) * 0.8);
        var master = item.Power >= 3;
        var offset = Rise(t, p.In);

        var pop = p.In + 0.15;
        var first = LandingAt(p, 0);
        var click = LandingAt(p, bounces);
        var inside = click + 0.15;
        var landed = t - inside;
        var sinceClick = t - click;
        var flash = sinceClick is >= 0 and < 2 * FlashPhase ? (sinceClick < FlashPhase ? 1 : 2) : 0;
        var restX = (double)(Cx + (side * 8));

        // The ball: an arc out of the bag, the bounces on the edge, and the step into the mouth.
        var shown = t >= pop && t < inside;
        var x = (double)Cx;
        var y = MouthY;
        var sx = 1.0;
        var sy = 1.0;
        var tick = false;

        if (shown)
        {
            // Which arc it is on: 0 the throw, and 1 to `bounces` the bounces; the last one ends in the mouth.
            var arc = 0;
            var begin = pop;
            var end = first;
            for (var i = 1; i <= bounces && t >= end; i++)
            {
                begin = end;
                end = begin + Lengths[i - 1];
                arc = i;
            }

            var height = arc == 0 ? 30 : Heights[arc - 1];
            var startX = arc == 0 ? Cx : restX;
            var endX = arc == bounces ? Cx : restX;
            var startY = arc == 0 ? MouthY - 2 : RimY;
            var endY = arc == bounces ? MouthY - 4 : RimY;
            var s = Math.Clamp((t - begin) / (end - begin), 0, 1);

            x = startX + ((endX - startX) * s);
            y = startY + ((endY - startY) * s) - (4 * height * s * (1 - s));

            // Spinning like a coin about its vertical axis, and flat against the edge for an instant after each landing.
            sx = Math.Max(0.4, Math.Abs(Math.Cos(Math.PI * spin * (t - pop) * 2)));
            sy = 1 + (0.12 * Math.Abs(Math.Cos(Math.PI * s)));

            if (t >= click)
            {
                // The step into the mouth, after the click.
                var w = (t - click) / (inside - click);
                x = Cx;
                y = MouthY - 4 + (10 * w);
                sx = 1;
                sy = 1 - (0.2 * w);
            }
            else if (arc >= 1 && t - begin < 0.06)
            {
                sx = 1.25;
                sy = 0.78;
                tick = true;
            }
            else if (arc == 0 && t >= first - 0.0 && t - first < 0.06)
            {
                tick = true;
            }
        }

        // The bag: crouch for the throw, give under every landing, boing when it has the ball.
        var bagY = 1.0;
        var open = t >= p.In + 0.1 && t < inside + 0.05;
        var shiver = 0;
        uint stitch = 0;

        if (t >= p.In && t < pop + 0.1) bagY = 1 - (0.14 * Math.Sin(Math.Clamp((t - p.In) / 0.25, 0, 1) * Math.PI));
        for (var k = 0; k < bounces; k++)
        {
            var after = t - LandingAt(p, k);
            if (after is >= 0 and < 0.10) bagY = Math.Min(bagY, 1 - (0.08 * Math.Sin(after / 0.10 * Math.PI)));
        }

        if (t >= inside)
        {
            bagY = Spring(landed, -0.25, 6, 24);
            shiver = landed < 0.35 ? (int)Math.Round(Math.Sin(36 * landed) * Math.Exp(-8 * landed) * 1.4) : 0;
            if (landed < 0.2) stitch = prism.Pale;
        }

        var bagX = 1 + ((1 - bagY) * 0.6);

        // The stage: a patch of ground and, at every landing, a flat ring of dust round the point on the edge.
        Ground(c, Cx, BagBottom + offset, 14, 3, prism.Deep, 8);
        for (var k = 0; k < bounces; k++)
        {
            var after = t - LandingAt(p, k);
            if (after is > 0 and < 0.28)
            {
                var w = after / 0.28;
                Ring(c, restX, RimY + 4, 3 + (9 * ItemCanvas.Ease(w)), prism.Light, (1 - w) * 16, 1, 6, 0, 0.3);
            }
        }

        if (shown) c.Icon(item, x, y, sx, sy, -1, ItemCanvas.White, flash > 0 ? 0.5 : 0);

        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, shiver, stitch);

        var bx = (int)Math.Round(x);
        var by = (int)Math.Round(y);

        // The tick of each landing: two short strokes at each side of the ball.
        if (tick)
        {
            Dot(c, bx - 12, by + 2, ItemCanvas.White);
            Dot(c, bx - 13, by + 1, ItemCanvas.White);
            Dot(c, bx + 12, by + 2, ItemCanvas.White);
            Dot(c, bx + 13, by + 1, ItemCanvas.White);
        }

        // The click: two frames of white at the middle of the ball, and three strokes over it.
        if (flash > 0)
        {
            if (flash == 1) Disc(c, bx, by, 4.5, ItemCanvas.White);
            else Ring(c, bx, by, 7, prism.Pale, 16, 2, 0, 0, 1);

            for (var k = -1; k <= 1; k++)
            {
                for (var d = 0; d < 3; d++)
                {
                    Dot(c, bx + (k * (d + 5)), by - 9 - d - (k == 0 ? 2 : 0), ItemCanvas.White);
                }
            }
        }

        // A ring of four stars turning round a Master Ball or a Beast Ball.
        if (master && shown)
        {
            for (var k = 0; k < 4; k++)
            {
                var angle = (2 * Math.PI * k / 4) + (t * 5);
                ItemFx.Cross(c, (int)Math.Round(x + (Math.Cos(angle) * 15)), (int)Math.Round(y + (Math.Sin(angle) * 9)), 1,
                    k % 2 == 0 ? ItemCanvas.White : prism.Pale);
            }
        }

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop, "CAPTURA", prism.Base, prism.Pale, Badge);
    }
}
