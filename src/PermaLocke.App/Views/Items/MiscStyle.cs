using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// Everything else going into the bag (2026-10-09): treasures, repels, fossils, mail. The simplest and the quickest of all, because
/// it is also what is picked up most: the item hops out of the bag, hangs for a breath, goes back in on a short arc, the bag hops
/// once and a single small star shows where it went. No particles, no rings, nothing that tires.
/// </summary>
/// <remarks>
/// The bag and the plate are where the scene of 2026-09-28 had them, which is what the player has seen for weeks.
/// </remarks>
public sealed class MiscStyle : ItemStyle
{
    public static MiscStyle Instance { get; } = new();

    public const int SceneHeight = 64;

    private const int BagLeft = 6;
    private const int Cx = BagLeft + 12;
    private const double RestY = 22;
    private const double MouthY = 46;
    private const int BagBottom = 58;
    private const int PlateLeft = 38;
    private const int PlateTop = 24;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xF0, 0xD0, 0x80);

    public override int Height => SceneHeight;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var side = seed.Pick(1, 2) == 0 ? 1 : -1;
        var offset = Rise(t, p.In);

        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var drop = p.ClimaxAt + 0.15;
        var inside = drop + 0.18;
        var landed = t - inside;

        // The item: out with a little overshoot, a breath, and back in on a short arc.
        var shown = a >= 0.2 && t < inside;
        var x = (double)Cx;
        var y = RestY;
        var scale = 1.0;

        if (shown)
        {
            if (t < p.ClimaxAt)
            {
                var e = (a - 0.2) / 0.8;
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Back(e));
                scale = 0.5 + (0.5 * ItemCanvas.Ease(e));
            }
            else if (t < drop)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ClimaxAt) * 8));
            }
            else
            {
                var s = (t - drop) / (inside - drop);
                x = Cx + (side * 4 * Math.Sin(s * Math.PI));
                y = RestY + ((MouthY - 4 - RestY) * s * s);
                scale = 1 - (0.35 * s);
            }
        }

        // The bag: a small crouch, and one hop when it has it.
        var bagY = 1.0;
        var open = a >= 0.2 && t < inside + 0.04;
        var hop = 0;

        if (t >= p.In && t < p.ClimaxAt) bagY = 1 - (0.12 * Math.Sin(Math.Clamp(a / 0.5, 0, 1) * Math.PI));
        if (landed >= 0)
        {
            bagY = Spring(landed, 0.12, 8, 26);
            hop = landed < 0.2 ? (int)Math.Round(Math.Sin(landed / 0.2 * Math.PI) * 3) : 0;
        }

        var bagX = 1 + ((1 - bagY) * 0.5);

        c.ShadowPatch(Cx, BagBottom + offset, 13 + (int)Math.Round((1 - bagY) * 6));

        if (shown) c.Icon(item, x, y, scale, scale);

        c.Bag(BagLeft, BagBottom + offset - hop, open, bagX, bagY);

        // The one small star where it went.
        if (landed is >= 0 and < 0.25)
        {
            ItemFx.Cross(c, Cx + (side * 6), (int)MouthY - 6 + offset, landed < 0.1 ? 3 : 1, ItemCanvas.White, prism.Pale);
        }

        PlateIn(c, item, t, 0.2, PlateLeft, PlateTop);
    }
}
