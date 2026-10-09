using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A key item going into the bag (2026-10-09): the most solemn of all. A beam of light comes down from above into the open bag,
/// which leans towards it as in a bow; the item rises along it slowly, without a bounce; a gentle white, one slow ring and a few
/// stars; and it goes down again just as slowly while the beam withdraws upwards and the bag straightens and shuts. The plate
/// has a second frame and says «OBJETO CLAVE».
/// </summary>
/// <remarks>
/// 88 rows high, like the Z-Crystals: the beam wants the whole height. Nothing here is quick and nothing bounces.
/// </remarks>
public sealed class KeyItemStyle : ItemStyle
{
    public static KeyItemStyle Instance { get; } = new();

    public const int SceneHeight = 88;

    private const int Cx = 36;
    private const double RestY = 36;
    private const double MouthY = 68;
    private const int BagBottom = 82;
    private const int PlateLeft = 68;
    private const int PlateTop = 30;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xFF, 0xDC, 0x7A);

    /// <summary>A key, five cells by five, for the corner of the plate.</summary>
    private static readonly (int X, int Y)[] Badge =
        [(0, 1), (1, 0), (2, 1), (1, 2), (0, 1), (1, 1), (2, 2), (3, 3), (4, 4), (3, 4), (4, 2)];

    public override int Height => SceneHeight;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var bow = seed.Pick(1, 2) == 0 ? 1 : -1;
        var rings = 1 + seed.Pick(2, 2);
        var stars = 3 + seed.Pick(3, 3);

        var flashAt = p.ClimaxAt + 0.25;
        var downAt = p.WrapAt - 0.10;
        var landedAt = downAt + 0.55;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var landed = t - landedAt;
        var flash = t - flashAt is >= 0 and < 2 * FlashPhase ? (t - flashAt < FlashPhase ? 1 : 2) : 0;
        var offset = Rise(t, p.In);

        // The beam: grows down from the top of the scene into the mouth, and withdraws upwards after the landing.
        var beamBottom = (int)Math.Round((MouthY - 1) * ItemCanvas.Smooth((t - 0.15) / 0.65));
        var beamTop = landed > 0.15 ? (int)Math.Round((MouthY - 1) * ItemCanvas.Smooth((landed - 0.15) / 0.5)) : 0;
        var wide = t >= p.ClimaxAt && landed < 0.15;

        // The item: out of the bag on the beam, slow and straight, and down again.
        var shown = a >= 0.30 && landed < 0;
        var x = (double)Cx;
        var y = RestY;
        var scale = 1.0;
        var mix = 0.0;

        if (shown)
        {
            if (t < p.ClimaxAt)
            {
                var e = (a - 0.30) / 0.70;
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Smooth(e));
                scale = 0.5 + (0.5 * ItemCanvas.Smooth(e));
            }
            else if (t < downAt)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 2.2));
            }
            else
            {
                var g = Math.Clamp((t - downAt) / (landedAt - downAt), 0, 1);
                y = RestY + ((BagBottom - 8 - RestY) * ItemCanvas.Smooth(g));
                scale = 1 - (0.25 * g);
            }

            if (flash == 1) mix = 0.9;
            else if (flash == 2) mix = 0.5;
        }

        // The bag: open under the beam, it bows towards the item and straightens after.
        var open = a >= 0.20 && landed < 0.18;
        var lean = 0.0;
        var bagY = 1.0;
        uint stitch = 0;
        var bulgeRow = -1.0;
        var bulge = 0.0;

        if (t >= p.In + (0.15 * p.Anticipation) && landed < 0)
        {
            lean = bow * 3 * ItemCanvas.Smooth((t - 0.5) / 0.9);
        }
        else if (landed >= 0)
        {
            lean = bow * 3 * Math.Exp(-4 * landed) * Math.Cos(9 * landed);
            bagY = Spring(landed, 0.10, 5, 18);
            if (landed is >= 0.03 and < 0.35)
            {
                var u = (landed - 0.03) / 0.32;
                bulgeRow = 5 + (13 * u);
                bulge = 0.12 * Math.Sin(Math.PI * u);
            }

            if (landed < 0.5) stitch = prism.Light;
        }

        var bagX = 1 + ((1 - bagY) * 0.5);

        // The stage under the bag: a patch of ground and the pool of light where the beam lands.
        Ground(c, Cx, BagBottom + offset, 16, 3, prism.Deep, 8);
        if (beamBottom > 0 && beamTop < beamBottom) Ground(c, Cx, BagBottom + offset - 1, 11, 2, prism.Light, 6 + (wide ? 4 : 0));

        Beam(c, prism, Cx, beamTop, beamBottom, wide, t);

        if (shown)
        {
            c.IconOutline(item, x, y, scale, scale, ((int)(t * 6) & 1) == 0 ? prism.Light : prism.Base, 1);
            c.Icon(item, x, y, scale, scale, -1, ItemCanvas.White, mix);
        }

        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, 0, stitch, bulgeRow, bulge, -1, 0, 6, lean);

        // One or two slow rings from the item, a few stars on the beam.
        for (var ring = 0; ring < rings; ring++)
        {
            var u = (t - flashAt - (ring * 0.35)) / 0.9;
            if (u is > 0 and < 1) Ring(c, Cx, RestY, 32 * ItemCanvas.Ease(u), ring == 0 ? prism.Pale : prism.Light, (1 - u) * 16, 1, 0);
        }

        for (var star = 0; star < stars; star++)
        {
            var begin = flashAt + 0.15 + (star * 0.16);
            var u = (t - begin) / 0.30;
            if (u is <= 0 or >= 1) continue;

            var angle = 2 * Math.PI * seed.Unit(10 + star);
            var radius = 12 + (10 * seed.Unit(20 + star));
            var gx = (int)Math.Round(Cx + (Math.Cos(angle) * radius));
            var gy = (int)Math.Round(RestY + (Math.Sin(angle) * radius * 0.8));
            ItemFx.Cross(c, gx, gy, u < 0.5 ? 2 : 1, prism.Pale, ItemCanvas.White);
        }

        // A glint on the buckle when it lands, and the white of the item before.
        if (landed is >= 0 and < 0.30) ItemFx.Cross(c, Cx, (int)MouthY + 6 + offset, landed < 0.15 ? 4 : 2, ItemCanvas.White, prism.Pale);
        if (flash > 0) Disc(c, x, y, flash == 1 ? 11 : 15, flash == 1 ? ItemCanvas.White : prism.Pale, flash == 1 ? 16 : 7);

        PlateIn(c, item, t, 0.45, PlateLeft, PlateTop, "OBJETO CLAVE", 0, 0, Badge, true);
    }

    /// <summary>The column of light: whole in the middle, dithered at the sides, with a pattern that runs down it.</summary>
    private static void Beam(ItemCanvas c, ItemPrism prism, int x, int top, int bottom, bool wide, double t)
    {
        if (bottom <= top) return;

        var run = (int)Math.Floor(t * 14);
        var half = wide ? 2 : 1;

        for (var y = top; y < bottom; y++)
        {
            for (var dx = -half - 1; dx <= half + 1; dx++)
            {
                var gx = x + dx;
                var inner = Math.Abs(dx) <= half;
                if (!c.Contains(gx, y)) continue;
                if (!inner && ItemCanvas.Bayer[y & 3, gx & 3] >= 5) continue;

                var bright = ((y + run) % 7) == 0;
                c.Put(gx, y, !inner ? prism.Base : bright ? ItemCanvas.White : Math.Abs(dx) == 0 ? prism.Pale : prism.Light);
            }
        }
    }
}
