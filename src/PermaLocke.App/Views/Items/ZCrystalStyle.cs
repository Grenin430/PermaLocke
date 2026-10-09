using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A Z-Crystal going into the bag (2026-10-09): the power of the move being gathered. The crystal rises out of the bag
/// spinning like a coin and settles facing us; a ring of tips appears round it one by one and turns; a pause, charged; the tips
/// close on it, two frames of white and a cross of light; an emblem, a Z in a diamond, is traced over it in its colours and
/// dissolves; the crystal drops into the bag and a band of its colour runs across the leather.
/// </summary>
/// <remarks>
/// Taller than the common scene (88 against 64) because the ring, the cross and the emblem need the room; the pixel is still
/// computed from the width. The colour is the icon's own, which the game paints by type: eighteen palettes with no table.
/// </remarks>
public sealed class ZCrystalStyle : ItemStyle
{
    public static ZCrystalStyle Instance { get; } = new();

    public const int SceneHeight = 88;

    private const int Cx = 40;
    private const double RestY = 34;
    private const double MouthY = 66;
    private const int BagBottom = 82;
    private const int PlateLeft = 70;
    private const int PlateTop = 30;
    private const double RingRadius = 23;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xE0, 0x90, 0x30);
    private static readonly (int X, int Y)[] Emblem = BuildEmblem();

    /// <summary>The five cells by five of the plate's mark: a Z.</summary>
    private static readonly (int X, int Y)[] Badge =
        [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (3, 1), (2, 2), (1, 3), (0, 4), (1, 4), (2, 4), (3, 4), (4, 4)];

    public override int Height => SceneHeight;

    /// <summary>The moment the tips reach the crystal and the white comes.</summary>
    public static double ImpactAt(ItemPhases p) => p.ClimaxAt + (0.62 * p.Climax);

    /// <summary>How many tips a seed gives the ring: six, eight or ten.</summary>
    public static int TipsFor(int seed) => 6 + (2 * new ItemSeed(seed).Pick(1, 3));

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var tips = TipsFor(item.Seed);
        var spin = seed.Pick(2, 2) == 0 ? 1 : -1;
        var sweepRight = seed.Pick(3, 2) == 0;

        var impact = ImpactAt(p);
        var fallStart = impact + 0.45;
        var fallEnd = fallStart + 0.30;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var since = t - impact;
        var landed = t - fallEnd;
        var flash = since is >= 0 and < 2 * FlashPhase ? (since < FlashPhase ? 1 : 2) : 0;

        // The crystal: coin spin on the way up, charge, hover, fall.
        var crystal = false;
        var x = (double)Cx;
        var y = RestY;
        var sx = 1.0;
        var sy = 1.0;
        var mix = 0.0;

        if (a >= 0.45 && t < fallEnd)
        {
            crystal = true;

            if (t < p.ClimaxAt)
            {
                var e = (a - 0.45) / 0.55;
                var scale = 0.4 + (0.6 * ItemCanvas.Ease(e));
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Back(e));
                sx = scale * Math.Max(0.28, Math.Abs(Math.Cos(Math.PI * 3 * e)));
                sy = scale;
            }
            else if (t < impact)
            {
                var u = (t - p.ClimaxAt) / (impact - p.ClimaxAt);
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 6));
                mix = 0.55 * u;
                x += Math.Round((seed.Noise(t, 1) - 0.5) * 3 * u * u);
            }
            else if (t < fallStart)
            {
                y = RestY - Math.Round(2 * ItemCanvas.Ease(since / 0.25));
                mix = 0.8 * (1 - Math.Clamp(since / 0.4, 0, 1));
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                y = RestY - 2 + ((BagBottom - 6 - (RestY - 2)) * g * g);
                sx = 1 - (0.2 * g);
                sy = 1 + (0.2 * g);
            }

            if (flash > 0) mix = 1;
        }

        // The bag: the crouch, the open mouth, and after the landing a spring, a swallow and the band of colour.
        var bagY = 1.0;
        var open = false;
        var shiver = 0;
        uint stitch = 0;
        var bulgeRow = -1.0;
        var bulge = 0.0;
        var wash = -1.0;
        var offset = Rise(t, p.In);

        if (t >= p.In && t < p.ClimaxAt)
        {
            bagY = a < 0.40 ? 1 - (0.18 * ItemCanvas.Smooth(a / 0.40))
                : a < 0.55 ? 0.82 + (0.28 * ItemCanvas.Smooth((a - 0.40) / 0.15))
                : 1 + (0.10 * (1 - ItemCanvas.Ease((a - 0.55) / 0.45)));
            open = a >= 0.45;
        }
        else if (t >= p.ClimaxAt && t < fallEnd)
        {
            open = true;
        }
        else if (t >= fallEnd)
        {
            bagY = Spring(landed, 0.2, 6, 26);
            shiver = landed < 0.4 ? (int)Math.Round(Math.Sin(40 * landed) * Math.Exp(-7 * landed) * 1.5) : 0;
            open = landed < 0.10;

            if (landed is >= 0.04 and < 0.28)
            {
                var u = (landed - 0.04) / 0.24;
                bulgeRow = 5 + (13 * u);
                bulge = 0.18 * Math.Sin(Math.PI * u);
            }

            // The colour of the crystal runs across the leather, from one side to the other.
            if (landed is >= 0.05 and < 0.65)
            {
                var u = (landed - 0.05) / 0.60;
                wash = (sweepRight ? u : 1 - u) * 23;
                stitch = prism.Hue((int)(landed * 14));
            }
        }

        var bagX = 1 + ((1 - bagY) * 0.6);
        var charge = t >= p.ClimaxAt && t < impact ? (t - p.ClimaxAt) / (impact - p.ClimaxAt) : 0;

        // The stage under the bag: a patch of ground and a ring of marks that turns slowly and wakes with the charge.
        Ground(c, Cx, BagBottom + offset, 15, 3, prism.Deep, 9);
        Ring(c, Cx, BagBottom + offset, 18, prism.Base, 6 + (8 * charge) + (a * 3), 1, 8, spin * t * 0.15, 0.22);

        if (crystal)
        {
            // The outline pulses in two tones through the charge: that is the pause of power.
            if (t >= p.ClimaxAt && t < impact)
            {
                var tone = ((int)(charge * 14) & 1) == 0 ? prism.Light : prism.Pale;
                c.IconOutline(item, x, y, sx, sy, tone, charge > 0.5 ? 2 : 1);
            }

            c.Icon(item, x, y, sx, sy, -1, ItemCanvas.White, mix);
        }

        Tips(c, prism, tips, spin, t, p, a, impact, x, y);
        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, shiver, stitch, bulgeRow, bulge, wash, prism.Light, 5);

        Sparks(c, prism, landed);
        Glint(c, prism, since);
        EmblemAt(c, prism, seed, since);
        if (flash > 0) Flash(c, flash, x, y);

        PlateIn(c, item, t, 0.35, PlateLeft, PlateTop, "CRISTAL Z", prism.Base, prism.Pale, Badge);
    }

    /// <summary>The ring of tips: they come in one by one, turn, wait, and close on the crystal.</summary>
    private static void Tips(ItemCanvas c, ItemPrism prism, int tips, int spin, double t, ItemPhases p, double a, double impact,
        double x, double y)
    {
        if (t >= impact || a < 0.55) return;

        var close = Math.Clamp((t - (p.ClimaxAt + 0.25)) / (impact - (p.ClimaxAt + 0.25)), 0, 1);
        var radius = RingRadius * (1 - (0.72 * close * close * close));
        var turn = spin * (t - p.In) * 0.22;

        for (var k = 0; k < tips; k++)
        {
            // Each tip is on from a moment of the anticipation, in order.
            if (a < 0.55 + (0.45 * (k + 1) / tips * (t < p.ClimaxAt ? 1 : 0.0001))) continue;

            var angle = 2 * Math.PI * ((k / (double)tips) + turn);
            var ux = Math.Cos(angle);
            var uy = Math.Sin(angle);

            // A tip is a short stroke that points at the crystal: three cells, the outer one the brightest.
            for (var d = 0; d < 4; d++)
            {
                var gx = (int)Math.Round(x + (ux * (radius - d)));
                var gy = (int)Math.Round(y + (uy * (radius - d)));
                Dot(c, gx, gy, d == 0 ? prism.Pale : d < 3 ? prism.Light : prism.Base);
            }
        }
    }

    /// <summary>The cross of light of the impact: the long arms of the plus, the diagonals shorter, thinning out through the dither.</summary>
    private static void Glint(ItemCanvas c, ItemPrism prism, double since)
    {
        const double Length = 0.22;
        if (since < 0 || since >= Length) return;

        var u = since / Length;
        var arm = (int)Math.Round(20 * ItemCanvas.Ease(Math.Min(1, since / 0.07)));
        var density = (1 - u) * 16;

        for (var d = 1; d <= arm; d++)
        {
            var colour = d < arm * 0.5 ? ItemCanvas.White : prism.Pale;
            Spoke(c, d, 0, density, colour);
            Spoke(c, -d, 0, density, colour);
            Spoke(c, 0, d, density, colour);
            Spoke(c, 0, -d, density, colour);
        }

        var diagonal = (int)Math.Round(arm * 0.55);
        for (var d = 1; d <= diagonal; d++)
        {
            Spoke(c, d, d, density, prism.Light);
            Spoke(c, -d, d, density, prism.Light);
            Spoke(c, d, -d, density, prism.Light);
            Spoke(c, -d, -d, density, prism.Light);
        }
    }

    /// <summary>One cell of the cross, at an offset from the crystal, through the dither.</summary>
    private static void Spoke(ItemCanvas c, int dx, int dy, double density, uint colour)
    {
        var gx = Cx + dx;
        var gy = (int)RestY + dy;
        if (c.Contains(gx, gy) && ItemCanvas.Bayer[gy & 3, gx & 3] < density) c.Put(gx, gy, colour);
    }

    /// <summary>The emblem, a Z in a diamond, traced cell by cell over where the crystal is and dissolved.</summary>
    private static void EmblemAt(ItemCanvas c, ItemPrism prism, ItemSeed seed, double since)
    {
        const double Start = 0.07, Trace = 0.25, Hold = 0.50, Gone = 0.85;
        if (since < Start || since >= Gone) return;

        var drawn = (int)(Math.Clamp((since - Start) / Trace, 0, 1) * Emblem.Length);
        var density = since < Hold ? 16 : (1 - ItemCanvas.Smooth((since - Hold) / (Gone - Hold))) * 16;
        var first = seed.Pick(4, 4) * (Emblem.Length / 4);

        for (var n = 0; n < drawn; n++)
        {
            var (ex, ey) = Emblem[(n + first) % Emblem.Length];
            var gx = Cx + ex;
            var gy = (int)RestY - 2 + ey;
            if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

            c.Put(gx, gy, n >= drawn - 3 && drawn < Emblem.Length ? ItemCanvas.White : prism.Hue(n / 4));
        }
    }

    /// <summary>The sparks of the landing: a fan of cells that goes up from the mouth and goes out.</summary>
    private static void Sparks(ItemCanvas c, ItemPrism prism, double landed)
    {
        const double Length = 0.55;
        if (landed < 0.05 || landed >= Length) return;

        var u = (landed - 0.05) / (Length - 0.05);
        for (var i = 0; i < 9; i++)
        {
            var angle = Math.PI * (1.12 + (0.76 * i / 8.0));
            var r = 4 + (22 * ItemCanvas.Ease(u));
            var gx = (int)Math.Round(Cx + (Math.Cos(angle) * r));
            var gy = (int)Math.Round(MouthY + 4 + (Math.Sin(angle) * r));
            if (u > 0.6 && ((int)(landed * 30) & 1) == 0) continue;

            Dot(c, gx, gy, (i & 1) == 0 ? prism.Light : prism.Hue(i));
            if (u < 0.35) Dot(c, gx, gy - 1, prism.Pale);
        }
    }

    /// <summary>Two frames of white of <see cref="ItemFx.FlashPhase"/> each: a disc, then a broken ring.</summary>
    private static void Flash(ItemCanvas c, int frame, double x, double y)
    {
        var reach = frame == 1 ? 15 : 24;
        var inner = frame == 1 ? 0 : 17;

        for (var gy = (int)y - reach; gy <= (int)y + reach; gy++)
        {
            for (var gx = (int)x - reach; gx <= (int)x + reach; gx++)
            {
                var d = Math.Sqrt(((gx - x) * (gx - x)) + ((gy - y) * (gy - y)));
                if (d > reach || d < inner || !c.Contains(gx, gy)) continue;
                if (frame == 2 && ItemCanvas.Bayer[gy & 3, gx & 3] >= 9) continue;

                c.Put(gx, gy, ItemCanvas.White);
            }
        }
    }

    /// <summary>A Z inside a diamond, as the cells to trace in order, round the point (0, 0).</summary>
    private static (int X, int Y)[] BuildEmblem()
    {
        var cells = new List<(int X, int Y)>();
        const int R = 9;

        for (var i = 0; i < R; i++) cells.Add((i, -R + i));
        for (var i = 0; i < R; i++) cells.Add((R - i, i));
        for (var i = 0; i < R; i++) cells.Add((-i, R - i));
        for (var i = 0; i < R; i++) cells.Add((-R + i, -i));

        // The Z: a bar above, the diagonal and a bar below.
        for (var x = -4; x <= 4; x++) cells.Add((x, -4));
        for (var k = 1; k <= 7; k++) cells.Add((4 - (k * 8 / 8), -4 + k));
        for (var x = -4; x <= 4; x++) cells.Add((x, 4));

        return [.. cells];
    }
}
