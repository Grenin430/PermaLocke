using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The altar of the trials (2026-10-18, prototype): an old carved stone half buried in the sand at the lower left of the abyss, with a niche for
/// each of the twelve trial Z-Crystals, always the same one for the same crystal, in the order of the trials (two rows of six). A crystal won
/// sits in its niche glowing, with a glint now and then; a niche still empty stays a dark hollow. It replaces the crystals strewn over the sand.
/// </summary>
public static class CapAltar
{
    /// <summary>The twelve trial crystals in the order of the trials: Normalium, Fightinium, Waterium, Firium, Grassium, Rockium, Electrium,
    /// Ghostium, Darkinium, Dragonium, Fairium, Groundium.</summary>
    public static readonly int[] Order = [807, 813, 809, 808, 811, 819, 810, 820, 822, 821, 824, 815];

    private const int Width = 104, Height = 33, Columns = 6, Pitch = 16, Crest = 7;
    private const double Scale = 0.4;

    private static readonly (uint Hi, uint Lit, uint Mid, uint Shade, uint Dark, uint Edge, uint Hollow) Stone =
        (0xB4BAA6, 0x989F8C, 0x7E8676, 0x646C60, 0x4A5248, 0x262C28, 0x1C2224);

    private static readonly uint[] Moss = [0x5A9A62, 0x3C7450];

    /// <summary>The middle of a niche, from the stone's top left.</summary>
    private static (int X, int Y) Niche(int slot) => (12 + ((slot % Columns) * Pitch), 14 + ((slot / Columns) * 12));

    /// <summary>The icon of a crystal if it has been won (a loop, so a frame allocates nothing).</summary>
    private static BitmapSource? Won(IReadOnlyList<(int Item, BitmapSource? Icon)>? crystals, int item)
    {
        if (crystals is null) return null;
        for (var i = 0; i < crystals.Count; i++) if (crystals[i].Item == item) return crystals[i].Icon;
        return null;
    }

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, IReadOnlyList<(int Item, BitmapSource? Icon)>? crystals,
        CapTrialTurn? turn, int filming, double phase)
    {
        if (l.Panel != CapPanelKind.Deep || l.Mode is not (CapMode.Column or CapMode.Slim) || l.Main.Width < Width + 10) return;

        var x0 = l.Main.Left + 5;
        var y0 = l.Rows - Height - 1;
        if (y0 - Crest < l.SurfaceY + 4) return;
        var murk = 0.18 + (0.4 * pal.Murk);
        void Put(int x, int y, uint c) => p.Set(x0 + x, y0 + y, CapSheet.Mix(c, pal.Water[5], murk));

        // the stone: lit top and left, shaded right, courses of blocks, a carved band of triangles along the top
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var edge = x == 0 || y == 0 || x == Width - 1;
                uint c = edge ? Stone.Edge : y == 1 ? Stone.Hi : x <= 2 ? Stone.Lit : x >= Width - 4 ? Stone.Shade : Stone.Mid;
                if (!edge && y > 7 && y % 11 == 7) c = Stone.Shade;
                if (!edge && y > 7 && (x + ((y / 11) * 7)) % 22 == 0) c = Stone.Shade;
                if (!edge && y is >= 3 and <= 6)
                {
                    // the band: a row of triangles cut into the stone, light on one face and dark on the other
                    var k = (x - 2) % 6;
                    var tri = y - 3 >= Math.Abs(k - 3) - 0;
                    c = tri ? (k < 3 ? Stone.Hi : Stone.Dark) : Stone.Shade;
                }

                if (!edge && y == 7) c = Stone.Dark;
                if ((uint)CapArt.Hash(x + 400, y) % 53 == 0 && !edge && y > 8) c = Stone.Dark;
                Put(x, y, c);
            }
        }

        // the crest in the middle of the top: a stepped block with the Z of the trials cut into it
        string[] z = ["#######", "....##.", "...##..", "..##...", ".##....", "#######"];
        var cx = Width / 2;
        for (var y = -Crest; y <= 0; y++)
        {
            var half = y == -Crest ? 9 : 11;
            for (var x = cx - half; x <= cx + half; x++)
            {
                var edge = y == -Crest || x == cx - half || x == cx + half;
                uint c = edge ? Stone.Edge : y == -Crest + 1 ? Stone.Hi : x <= cx - half + 2 ? Stone.Lit : x >= cx + half - 2 ? Stone.Shade : Stone.Mid;
                var zx = x - (cx - 3);
                var zy = y + Crest - 1;
                if (!edge && zx is >= 0 and < 7 && zy is >= 0 and < 6 && z[zy][zx] == (char)35) c = Stone.Dark;
                else if (!edge && zx is >= 1 and < 8 && zy is >= 1 and < 7 && z[zy - 1][zx - 1] == (char)35) c = Stone.Hi;
                Put(x, y, c);
            }
        }

        // moss over the top corners
        for (var x = 0; x < 9; x++) if ((uint)CapArt.Hash(x, 77) % 3 != 0) Put(x, x < 5 ? 0 : 1, Moss[x % 2]);
        for (var x = Width - 7; x < Width; x++) if ((uint)CapArt.Hash(x, 78) % 3 != 0) Put(x, 0, Moss[x % 2]);
        Put(1, 1, Moss[1]);
        Put(2, 2, Moss[0]);

        // the niches: a hollow the shape of a crystal, its upper left in shadow and a lit lip at its lower right
        for (var slot = 0; slot < Order.Length; slot++)
        {
            var (nx, ny) = Niche(slot);
            for (var dy = -6; dy <= 6; dy++)
            {
                for (var dx = -10; dx <= 10; dx++)
                {
                    var d = (Math.Abs(dx) / 10.0) + (Math.Abs(dy) / 5.5);
                    if (d > 1) continue;
                    var rim = d > 0.72;
                    Put(nx + dx, ny + dy, !rim ? Stone.Hollow : dx + dy < 0 ? Stone.Edge : Stone.Hi);
                }
            }
        }

        // the crystals won, each in its own niche; the newest comes in as its film's crystal goes
        for (var slot = 0; slot < Order.Length; slot++)
        {
            if (Won(crystals, Order[slot]) is not { } icon || CapTrialCrystal.Of(Order[slot], icon) is not { } crystal) continue;
            var alpha = 1.0;
            if (turn is not null && Order[slot] == filming) alpha = CapDeath.Clamp01((turn.CrystalT - CapTrial.CrystalEnd - 1.5) / 1.5);
            if (alpha <= 0) continue;

            var (nx, ny) = Niche(slot);
            var breath = 0.7 + (0.3 * Math.Sin((phase * 0.9) + (slot * 1.7)));
            CapTrial.Shine(p, x0 + nx, y0 + ny, 9, 6, crystal.Glow, 70 * breath * alpha);
            var glint = ((phase * 0.25) + (slot * 0.37)) % 6;
            crystal.Paint(p, x0 + nx, y0 + ny, Scale, 0, 0.25 + (0.15 * breath), glint < 1 ? glint : -1, int.MaxValue, (int)(255 * alpha));
        }

        // the sand drifted against its foot
        var sand = CapSheet.Mix(0xB8B07Au, pal.Floor[0], 0.35);
        var sandDark = CapSheet.Mix(0x9A9468u, pal.Floor[1], 0.35);
        for (var x = -4; x < Width + 6; x++)
        {
            var top = Height - 3 + (int)Math.Round(1.3 * Math.Sin(x * 0.21) + (x < 0 || x >= Width ? 1 : 0));
            for (var y = top; y <= Height; y++) p.Set(x0 + x, y0 + y, y == top ? sand : sandDark);
        }
    }
}
