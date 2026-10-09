namespace PermaLocke.App.Views;

/// <summary>
/// The scene of 2026-09-28 and the one every category plays until it has a style of its own: the bag with the item's icon
/// jumping out of it with a glint across, floating, dropping in, and the bag hopping shut with a few sparks. Beside it a plate
/// says what it was and how many. Stretched to the length of the category it plays for.
/// </summary>
public sealed class ClassicStyle : ItemStyle
{
    public static ClassicStyle Instance { get; } = new();

    private const int BagLeft = 6;
    private const int BagBottom = 58;
    private const int BagWidth = 24;
    private const double IconX = BagLeft + (BagWidth / 2.0);
    private const double IconRestY = 20;
    private const int PlateLeft = 38;
    private const int PlateTop = 24;

    public override int Height => ItemScene.SceneHeight;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases phases, double t)
    {
        t *= ItemTimeline.Length / phases.Length;

        var rise = (int)Math.Round((1 - ItemCanvas.Ease(Math.Min(1, t / ItemTimeline.In))) * 6);
        var hop = t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.2
            ? (int)Math.Round(Math.Sin((t - ItemTimeline.Inside) / 0.2 * Math.PI) * 3)
            : 0;

        c.ShadowPatch(IconX, BagBottom + rise, 13 - hop);
        c.Bag(BagLeft, BagBottom + rise - hop, open: t is >= ItemTimeline.Pop and < ItemTimeline.Inside + 0.05);

        if (t is >= ItemTimeline.Pop and < ItemTimeline.Inside)
        {
            var (y, scale) = IconPose(t);

            // Un brillo en diagonal que cruza el objeto justo al salir.
            c.Icon(item, IconX, y + rise, scale, scale, (t - ItemTimeline.Shown + 0.05) / 0.35);
        }

        if (t is >= ItemTimeline.Pop and < ItemTimeline.Pop + 0.45)
        {
            Sparks(c, IconX, IconRestY + rise, t - ItemTimeline.Pop, 5, 16);
        }

        if (t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.5)
        {
            Sparks(c, IconX, BagBottom - 16 + rise, t - ItemTimeline.Inside, 6, 12);
        }

        var plateIn = Math.Clamp((t - 0.2) / 0.3, 0, 1);
        if (plateIn > 0) c.Plate(item, PlateLeft, PlateTop + rise, (int)Math.Round((1 - ItemCanvas.Ease(plateIn)) * -8));
    }

    /// <summary>Where the icon's middle is and how big, in game pixels per icon pixel.</summary>
    private static (double Y, double Scale) IconPose(double t)
    {
        if (t < ItemTimeline.Shown)
        {
            // Sale de la mochila hacia arriba con un rebote.
            var p = (t - ItemTimeline.Pop) / (ItemTimeline.Shown - ItemTimeline.Pop);
            var y = (BagBottom - 14) + ((IconRestY - (BagBottom - 14)) * ItemCanvas.Ease(p)) - (Math.Sin(p * Math.PI) * 4);
            return (y, 0.4 + (0.6 * ItemCanvas.Ease(p)));
        }

        if (t < ItemTimeline.Drop)
        {
            return (IconRestY + Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.5), 1);
        }

        // Cae dentro, encogiendo.
        var f = (t - ItemTimeline.Drop) / (ItemTimeline.Inside - ItemTimeline.Drop);
        return (IconRestY - (Math.Sin(f * Math.PI) * 3) + ((BagBottom - 14 - IconRestY) * f * f), 1 - (f * 0.6));
    }

    /// <summary>Four-point sparks thrown out from a point, fading.</summary>
    private static void Sparks(ItemCanvas c, double cx, double cy, double since, int count, double reach)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (i / (double)count * Math.PI * 2) + 0.6;
            var r = 4 + (reach * ItemCanvas.Ease(since / 0.45));
            var x = (int)Math.Floor(cx + (Math.Cos(a) * r));
            var y = (int)Math.Floor(cy + (Math.Sin(a) * r * 0.8));
            var big = since < 0.2;

            Dot(c, x, y, ItemCanvas.White);
            if (big || i % 2 == 0)
            {
                var arm = big ? ItemCanvas.White : ItemCanvas.Gold;
                Dot(c, x - 1, y, arm);
                Dot(c, x + 1, y, arm);
                Dot(c, x, y - 1, arm);
                Dot(c, x, y + 1, arm);
            }
        }
    }

    /// <summary>A spark that leaves the scene is not drawn, instead of being asked of the canvas.</summary>
    private static void Dot(ItemCanvas c, int x, int y, uint colour)
    {
        if (c.Contains(x, y)) c.Put(x, y, colour);
    }
}
