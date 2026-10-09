using PermaLocke.App.Services;
using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A TM or an HM going into the bag (2026-10-09): knowledge being downloaded. The disc comes out of the bag spinning like a
/// coin, opens into rings of data that turn the other way from each other, bits of data run from them down into the bag, and a line
/// of scan sweeps the leather from top to bottom; a tick, the disc goes in and the bag hops. The whole scene is the colour of the
/// type of the move it teaches in this world, and the plate says it.
/// </summary>
/// <remarks>
/// An HM (power 1) has a fourth ring and bars at the sides of the disc: it is the one that is never lost. The colour of the type is
/// the game's own table (<see cref="TypeColours"/>); without a type, the colour of the icon.
/// </remarks>
public sealed class MachineStyle : ItemStyle
{
    public static MachineStyle Instance { get; } = new();

    public const int SceneHeight = 64;

    private const int Cx = 24;
    private const double RestY = 22;
    private const double MouthY = 44;
    private const int BagBottom = 58;
    private const int PlateLeft = 52;
    private const int PlateTop = 18;

    private static readonly uint Fallback = ItemCanvas.Bgra(0x80, 0xB0, 0xE0);

    private static readonly (int X, int Y)[] DiscBadge =
        [(1, 0), (2, 0), (3, 0), (0, 1), (4, 1), (0, 2), (2, 2), (4, 2), (0, 3), (4, 3), (1, 4), (2, 4), (3, 4)];

    public override int Height => SceneHeight;

    /// <summary>The colour a machine is played in: the type of its move when it is known, the colour of its icon when not.</summary>
    public static uint ColourOf(ItemScene.Item item)
    {
        if (item.Kind is >= 0 and < 18)
        {
            var colour = TypeColours.Of(item.Kind);
            return ItemCanvas.Bgra(colour.R, colour.G, colour.B);
        }

        return item.Tint;
    }

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(ColourOf(item), Fallback);
        var hidden = item.Power > 0;
        var rings = hidden ? 4 : 3;
        var spin = seed.Pick(1, 2) == 0 ? 1 : -1;
        var bits = 9 + seed.Pick(2, 4);

        var fallStart = p.WrapAt - 0.05;
        var fallEnd = fallStart + 0.25;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var u = Math.Clamp((t - p.ClimaxAt) / p.Climax, 0, 1);
        var landed = t - fallEnd;
        var offset = Rise(t, p.In);

        // The disc: out with a coin spin, a quiet bob while it is read, and down.
        var shown = a >= 0.25 && t < fallEnd;
        var y = RestY;
        var sx = 1.0;
        var sy = 1.0;

        if (shown)
        {
            if (t < p.ClimaxAt)
            {
                var e = (a - 0.25) / 0.75;
                var scale = 0.5 + (0.5 * ItemCanvas.Ease(e));
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Back(e));
                sx = scale * Math.Max(0.3, Math.Abs(Math.Cos(Math.PI * 2 * e)));
                sy = scale;
            }
            else if (t < fallStart)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 5) * 1.0);
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                y = RestY + ((BagBottom - 8 - RestY) * g * g);
                sx = 1 - (0.25 * g);
            }
        }

        // The bag: it reads, with its stitching going on and off like bits, and hops when it has it.
        var bagY = 1.0;
        var open = false;
        uint stitch = 0;
        var shiver = 0;
        var hop = 0;

        if (t >= p.In && t < p.ClimaxAt)
        {
            bagY = a < 0.4 ? 1 - (0.14 * ItemCanvas.Smooth(a / 0.4)) : 0.86 + (0.14 * ItemCanvas.Ease((a - 0.4) / 0.6));
            open = a >= 0.25;
        }
        else if (t >= p.ClimaxAt && t < fallEnd)
        {
            open = true;
            if (u > 0.3 && u < 0.9 && ((int)(t * 24) & 1) == 0) stitch = prism.Light;
        }
        else if (t >= fallEnd)
        {
            bagY = Spring(landed, 0.14, 7, 26);
            open = landed < 0.08;
            shiver = landed < 0.3 ? (int)Math.Round(Math.Sin(36 * landed) * Math.Exp(-8 * landed) * 1.2) : 0;
            hop = landed is >= 0 and < 0.16 ? (int)Math.Round(Math.Sin(landed / 0.16 * Math.PI) * 2) : 0;
            if (landed < 0.25) stitch = prism.Pale;
        }

        var bagX = 1 + ((1 - bagY) * 0.5);

        // The stage: a table of lines under the bag, which is lit while the scan lasts.
        Ground(c, Cx, BagBottom + offset, 14, 3, prism.Deep, 8);
        for (var row = 0; row < 2; row++)
        {
            Ring(c, Cx, BagBottom + offset - 1 + (row * 3), 13 - (row * 3), prism.Base, 6 + (u > 0.3 && u < 0.9 ? 5 : 0), 1, 8, 0.5 * row, 0.12);
        }

        if (shown)
        {
            c.Icon(item, Cx, y, sx, sy, -1, ItemCanvas.White, u > 0.85 && u < 0.95 ? 0.6 : 0);
        }

        // The rings of data: they open one after the other, and each turns the other way from the one inside it.
        if (t >= p.ClimaxAt && t < fallEnd)
        {
            var close = t >= fallStart ? 1 - ItemCanvas.Smooth((t - fallStart) / (fallEnd - fallStart)) : 1;
            for (var ring = 0; ring < rings; ring++)
            {
                var born = Math.Clamp((u - (0.18 * ring)) / 0.25, 0, 1);
                if (born <= 0) continue;

                var radius = (11 + (4.5 * ring)) * ItemCanvas.Ease(born) * close;
                var turn = ((ring & 1) == 0 ? 1 : -1) * spin * (t - p.ClimaxAt) * (0.5 + (0.2 * ring));
                var colour = ring == 0 ? prism.Pale : ring == 1 ? prism.Light : ring == 2 ? prism.Base : prism.Hue(2);
                Ring(c, Cx, y, radius, colour, 16, hidden && ring == 0 ? 2 : 1, 7 + ring, turn, 1);
            }

            // The sides of an HM: two short bars that hold the disc, as if it were fixed.
            if (hidden && u > 0.2)
            {
                for (var k = 0; k < 4; k++)
                {
                    Dot(c, (int)Cx - 17, (int)y - 3 + (k * 2), prism.Light);
                    Dot(c, (int)Cx + 17, (int)y - 3 + (k * 2), prism.Light);
                }
            }
        }

        c.Bag(Cx - 12, BagBottom + offset - hop, open, bagX, bagY, shiver, stitch);

        Bits(c, prism, seed, bits, u, t);
        Scan(c, prism, u, offset);

        // The tick when it has been read: a small check over the disc.
        if (u is >= 0.88 and < 1)
        {
            Segment(c, (int)Cx - 3, (int)RestY - 14, (int)Cx - 1, (int)RestY - 12, 1, ItemCanvas.White);
            Segment(c, (int)Cx - 1, (int)RestY - 12, (int)Cx + 4, (int)RestY - 18, 1, ItemCanvas.White);
        }

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop, Caption(item), prism.Base, prism.Pale, DiscBadge);
    }

    /// <summary>«MT FUEGO» or «MO FUEGO»: what it is and the type of its move, when the type is known.</summary>
    public static string Caption(ItemScene.Item item)
    {
        // Made once for an item, not at every frame: nothing is allocated per frame. Per thread, so that two scenes at once
        // (the tests, the lab) never see each other's.
        if (!ReferenceEquals(item, _captionOf) || _caption is null)
        {
            var kind = item.Power > 0 ? "MO" : "MT";
            _caption = string.IsNullOrWhiteSpace(item.Detail) ? kind : $"{kind} {item.Detail}";
            _captionOf = item;
        }

        return _caption;
    }

    [ThreadStatic]
    private static ItemScene.Item? _captionOf;

    [ThreadStatic]
    private static string? _caption;

    /// <summary>Bits of data going from the rings down into the bag: short dashes in columns.</summary>
    private static void Bits(ItemCanvas c, ItemPrism prism, ItemSeed seed, int bits, double u, double t)
    {
        if (u < 0.3 || u >= 1) return;

        for (var i = 0; i < bits; i++)
        {
            var begin = 0.3 + (0.45 * seed.Unit(30 + i));
            var life = (u - begin) / 0.28;
            if (life is <= 0 or >= 1) continue;

            var column = (int)Math.Round(Cx - 9 + (18 * seed.Unit(50 + i)));
            var row = (int)Math.Round(RestY + 8 + ((MouthY - RestY - 4) * life));
            var colour = (i & 1) == 0 ? prism.Light : prism.Pale;

            // A bit is a one or a zero: a cell or two side by side, flickering.
            Dot(c, column, row, colour);
            if (((int)(t * 20) + i & 1) == 0) Dot(c, column + 1, row, colour);
        }
    }

    /// <summary>The line of the scan: bright in the middle of the bag's width, a dithered trail above it.</summary>
    private static void Scan(ItemCanvas c, ItemPrism prism, double u, int offset)
    {
        if (u is < 0.35 or >= 0.88) return;

        var w = (u - 0.35) / 0.53;
        var y = (BagBottom - 20) + offset + (int)Math.Round(20 * w);

        for (var x = Cx - 15; x <= Cx + 15; x++)
        {
            Dot(c, x, y, x % 3 == 0 ? ItemCanvas.White : prism.Pale);
            if (ItemCanvas.Bayer[(y - 1) & 3, x & 3] < 9) Dot(c, x, y - 1, prism.Light);
            if (ItemCanvas.Bayer[(y - 2) & 3, x & 3] < 4) Dot(c, x, y - 2, prism.Base);
        }
    }
}
