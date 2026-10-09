using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A held item or a battle item going into the bag (2026-10-09): strength and presence, and quickly. The item appears at once,
/// a dry glint crosses it in a single sweep, and it comes down hard with streaks behind it; the blow shakes the bag three rows
/// one way and the other, a shock ring goes out flat over the ground with a second thin one after it, cracks open in the floor and
/// metallic sparks fly. No float, no bounce: the whole thing is over in about a second and a half.
/// </summary>
public sealed class BattleStyle : ItemStyle
{
    public static BattleStyle Instance { get; } = new();

    public const int SceneHeight = 64;

    private const int Cx = 24;
    private const double RestY = 14;
    private const double MouthY = 46;
    private const int BagBottom = 58;
    private const int PlateLeft = 52;
    private const int PlateTop = 18;

    private static readonly uint SteelDeep = ItemCanvas.Bgra(0x30, 0x38, 0x48);
    private static readonly uint SteelBase = ItemCanvas.Bgra(0x70, 0x80, 0x98);
    private static readonly uint SteelLight = ItemCanvas.Bgra(0xB8, 0xC4, 0xD8);
    private static readonly uint Fallback = ItemCanvas.Bgra(0xA0, 0xB0, 0xC8);

    /// <summary>A sword, five cells by five.</summary>
    private static readonly (int X, int Y)[] Badge = [(2, 0), (2, 1), (2, 2), (0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (2, 4)];

    public override int Height => SceneHeight;

    /// <summary>The moment the item hits the bottom of the bag.</summary>
    public static double HitAt(ItemPhases p) => p.ClimaxAt + 0.23;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var seed = new ItemSeed(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var side = seed.Pick(1, 2) == 0 ? 1 : -1;
        var sparks = 5 + seed.Pick(2, 3);
        var offset = Rise(t, p.In);

        var drop = p.ClimaxAt + 0.10;
        var hit = HitAt(p);
        var since = t - hit;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);

        // The item: out of the bag at once, a sweep of glint, and down hard.
        var shown = t >= p.In + 0.03 && t < hit + 0.02;
        var x = (double)Cx;
        var y = RestY;
        var sx = 1.0;
        var sy = 1.0;
        var shine = -1.0;

        if (shown)
        {
            if (t < p.In + 0.15)
            {
                var e = (t - p.In - 0.03) / 0.12;
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Ease(e));
                sx = 0.6 + (0.4 * ItemCanvas.Ease(e));
                sy = sx;
            }
            else if (t < drop)
            {
                y = RestY;
                shine = (t - (p.In + 0.15)) / Math.Max(0.05, drop - (p.In + 0.15)) * 1.3;
            }
            else
            {
                var s = Math.Clamp((t - drop) / (hit - drop), 0, 1);
                x = Cx + (side * 5 * (1 - s));
                y = RestY + ((MouthY - 4 - RestY) * s * s);
                sy = 1 + (0.25 * s);
                sx = 1 - (0.12 * s);
            }
        }

        // The bag: a quick crouch, the blow, and the shaking of three rows to each side that dies down.
        var bagY = 1.0;
        var open = t >= p.In + 0.05 && t < hit + 0.04;
        var shiver = 0;
        uint stitch = 0;

        if (t >= p.In && t < drop) bagY = 1 - (0.10 * Math.Sin(Math.Clamp((t - p.In) / 0.2, 0, 1) * Math.PI));
        if (since >= 0)
        {
            bagY = Spring(since, 0.22, 9, 30);
            var step = (int)Math.Floor(since * 30);
            if (step < 8) shiver = ((step & 1) == 0 ? 1 : -1) * (3 - (step / 3)) * side;
            if (since < 0.2) stitch = SteelLight;
        }

        var bagX = 1 + ((1 - bagY) * 0.7);

        // The stage: a crater of dark ground and, at the blow, six cracks that open in it.
        Ground(c, Cx, BagBottom + offset, 15, 3, SteelDeep, 10);
        if (since >= 0)
        {
            var reach = (int)Math.Round(7 * ItemCanvas.Ease(since / 0.12));
            for (var k = 0; k < 6; k++)
            {
                var run = (k - 2.5) * 5;
                var gx = (int)Math.Round(Cx + run);
                for (var d = 0; d < reach; d++)
                {
                    var gy = BagBottom + 1 + offset + (d / 3);
                    Dot(c, gx + (int)Math.Round(Math.Sign(run) * d * 0.8), gy, d == reach - 1 ? SteelBase : SteelDeep);
                }
            }
        }

        // The streaks behind the item while it comes down.
        if (t >= drop && t < hit)
        {
            for (var k = -1; k <= 1; k++)
            {
                var gx = (int)Math.Round(x) + (k * 4) + (side * 2);
                var gy = (int)Math.Round(y) - 10 - (k == 0 ? 4 : 0);
                for (var d = 0; d < 7; d++) Dot(c, gx - (side * (d / 3)), gy - d, d < 3 ? SteelLight : SteelBase);
            }
        }

        if (shown) c.Icon(item, x, y, sx, sy, shine, ItemCanvas.White, shine is > 0.4 and < 0.8 ? 0.0 : 0);

        c.Bag(Cx - 12, BagBottom + offset, open, bagX, bagY, shiver, stitch);

        // The shock: a thick ring flat over the ground and a thin one behind it, white going to the item's colour.
        if (since is >= 0 and < 0.4)
        {
            var w = since / 0.4;
            Ring(c, Cx, BagBottom + offset - 2, 4 + (30 * ItemCanvas.Ease(w)), w < 0.25 ? ItemCanvas.White : prism.Light, (1 - w) * 16, 2, 0, 0, 0.35);
            if (since >= 0.06)
            {
                var v = (since - 0.06) / 0.34;
                Ring(c, Cx, BagBottom + offset - 2, 3 + (36 * ItemCanvas.Ease(v)), SteelLight, (1 - v) * 16, 1, 14, 0, 0.35);
            }

            // The glint on the buckle at the blow.
            if (since < 0.1) ItemFx.Cross(c, Cx, (int)MouthY - 2 + offset, 5, ItemCanvas.White, SteelLight);
        }

        // The sparks: small and dry, thrown out low and fast and falling.
        if (since is >= 0 and < 0.45)
        {
            for (var k = 0; k < sparks; k++)
            {
                var angle = Math.PI * (1.1 + (0.8 * k / (sparks - 1.0)));
                var speed = 34 + (22 * seed.Unit(70 + k));
                var gx = (int)Math.Round(Cx + (Math.Cos(angle) * speed * since));
                var gy = (int)Math.Round(MouthY + offset + (Math.Sin(angle) * speed * since) + (90 * since * since));
                if (since > 0.3 && ((int)(since * 30) & 1) == 0) continue;

                Dot(c, gx, gy, k % 2 == 0 ? SteelLight : ItemCanvas.White);
            }
        }

        PlateIn(c, item, t, 0.12, PlateLeft, PlateTop, "COMBATE", SteelBase, SteelLight, Badge);
    }
}
