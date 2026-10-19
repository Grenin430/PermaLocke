namespace PermaLocke.App.Views;

/// <summary>
/// The Gimmighoul ruins of Scarlet and Violet sunk on the floor of the abyss (2026-10-18, prototype, after the first one the player chose;
/// references in Desktop/imagenes/revision/prototipo_ruinas): worn walls of stone bricks with moss along the whole floor, a tall broken one with
/// an arch and what is left of an upper storey on it, lower ones with windows, stubs of wall, a fallen block and rubble on the sand, a stone floor
/// of cracked flags, and on it Gimmighoul in its chest, chest-sized, with a diving mask and a snorkel (<see cref="CapGimmighoulArt"/>). Live:
/// its bubbles, the glint of its coins, the weed on the walls swaying. The Z-Crystals are not placed yet (the next step of the plan).
/// </summary>
public static class CapGimmighoulRuins
{
    private const int Wanted = 240, Ground = 3;

    private static readonly uint[] Brick = [0x8A9282, 0x7C8676, 0x6E786A, 0x969C88];
    private const uint BrickHi = 0xB2B8A4, Mortar = 0x3C443C, Side = 0x525C4E, SideDark = 0x3E4840, Recess = 0x262E2A, Outline = 0x1C221E;
    private static readonly uint[] Moss = [0x78B052, 0x5A9444, 0x417838, 0x2E5A30];
    private const uint Flag = 0x908C78, FlagHi = 0xAAA690, FlagDark = 0x6E6C5C, Crack = 0x3E3E34;

    /// <summary>A wall: where, how wide and high, its seed, an arch (column, width, height) or a window, and how high its foot is (an upper storey).</summary>
    private sealed record Piece(int Left, int Width, int Height, int Seed, int ArchAt = -1, int ArchWidth = 0, int ArchHeight = 0,
        bool Window = false, int WindowAt = 12, int Base = 0);

    private static readonly Piece[] Pieces =
    [
        new(2, 46, 46, 1, ArchAt: 22, ArchWidth: 12, ArchHeight: 22),
        new(6, 26, 15, 9, Window: true, WindowAt: 9, Base: 43),
        new(46, 40, 27, 2, Window: true),
        new(104, 14, 36, 3),
        new(124, 36, 24, 4, Window: true, WindowAt: 14),
        new(166, 12, 30, 5),
        new(182, 22, 18, 6, Window: true, WindowAt: 7),
        new(198, 8, 12, 7),
    ];

    private static readonly (int X, int Y, int W)[] Rubble =
        [(24, 2, 4), (30, 1, 3), (36, 2, 2), (98, 2, 5), (106, 1, 3), (120, 2, 3), (2, 1, 3), (90, 1, 2), (114, 3, 4), (44, 0, 2), (126, 1, 2),
         (150, 1, 4), (162, 2, 3), (180, 1, 5), (204, 2, 3), (214, 1, 2), (230, 1, 4)];

    private static readonly (int X, int Y, int Length, int Seed)[] Weeds =
        [(8, 44, 6, 0), (33, 41, 5, 1), (58, 26, 4, 2), (110, 35, 7, 3), (14, 58, 5, 4), (138, 23, 5, 5), (170, 29, 6, 6), (196, 15, 4, 7)];

    private static readonly (int X, int Y)[] Spilt = [(-3, 1), (-6, 3), (21, 2), (24, 4), (18, 5)];

    private static readonly (int X, int Y) Snorkel = SnorkelTip();

    /// <summary>The row of the art the chest stands on: the bottom of its box.</summary>
    private const int ChestFoot = CapGimmighoulArt.Foot;

    private static (int X, int Y) SnorkelTip()
    {
        for (var y = 0; y < CapGimmighoulArt.Rows.Length; y++)
        {
            var x = CapGimmighoulArt.Rows[y].IndexOf('B');                                       // the top of the blue snorkel
            if (x >= 0) return (x, y);
        }

        return (5, 10);
    }

    private static uint Hash(int a, int b) => (uint)CapArt.Hash(a, b);

    private static double Noise(int x, int y, int seed)
    {
        // value noise on a coarse grid, smoothed: patches of moss rather than speckle
        int gx = x / 4, gy = y / 3;
        double fx = (x % 4) / 4.0, fy = (y % 3) / 3.0;
        double V(int a, int b) => Hash(a + seed, b * 7919) % 1000 / 1000.0;
        var top = V(gx, gy) + ((V(gx + 1, gy) - V(gx, gy)) * fx);
        var bottom = V(gx, gy + 1) + ((V(gx + 1, gy + 1) - V(gx, gy + 1)) * fx);
        return top + ((bottom - top) * fy);
    }

    /// <summary>
    /// Where the ruins stand in the abyss's window, or null when they are not drawn: their first column, the row their walls stand on and how wide
    /// they are. They stand on the seabed: their foot a little into its highest point along them.
    /// </summary>
    public static (int X0, int Floor, int Width)? Anchor(CapWorldLayout l, CapSeaContext ctx)
    {
        if (l.Panel != CapPanelKind.Deep || l.Mode is not (CapMode.Column or CapMode.Slim)) return null;
        var width = Math.Min(Wanted, l.Cols - l.Main.Left - 2);
        if (width < 124) return null;

        var x0 = l.Main.Left + 3;
        var floor = int.MaxValue;
        for (var x = 0; x < width; x++) floor = Math.Min(floor, CapSea.Relief(l, ctx, x0 + x));
        // standing on the storey that rose out of the sand after the Electrostal, once it has
        floor += Ground - Lift(ctx);
        return floor - 62 < l.SurfaceY + 6 ? null : (x0, floor, width);
    }

    /// <summary>How many rows the risen storey lifts the ruins by now.</summary>
    private static int Lift(CapSeaContext ctx) => (int)Math.Round(Storey * Math.Clamp(ctx.Rise, 0, 1));

    /// <summary>Where the chest stands: its art's first column in the ruins and its top row in the window (its foot on the stone floor).</summary>
    public const int ChestLeft = 68;

    public static int ChestTop(int floor) => floor - 4 - 6 + 3 - ChestFoot;

    /// <summary>How high the tops of the walls are over their floor at a column of the ruins (0 where there is no wall): what a crystal rests on.</summary>
    public static int Surface(int x)
    {
        var top = 0;
        foreach (var piece in Pieces)
        {
            if (x >= piece.Left && x < piece.Left + piece.Width) top = Math.Max(top, piece.Base + TopOf(piece, x - piece.Left));
        }

        return top;
    }

    /// <summary>A wall's height at one of its columns: broken, full at the left, stepping down by bricks towards the right, a bite out of it.</summary>
    private static int TopOf(Piece piece, int x)
    {
        var step = (int)(Hash(x / 3, piece.Seed) % 4);
        var drop = Math.Max(0, (x - (piece.Width * 0.55)) / piece.Width * piece.Height * 0.7);
        return piece.Height - step - (int)drop - (x < 2 ? 3 : 0);
    }

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase)
    {
        // a mound of sand under them down to the bed
        if (Anchor(l, ctx) is not var (x0, floor, width)) return;
        var murk = 0.16 + (0.4 * pal.Murk);
        var water = pal.Water[5];
        void Put(int x, int y, uint c) => p.Set(x0 + x, y, CapSheet.Mix(c, water, murk));

        // the storey that rose under them (as much of it as is out of the sand)
        var foot = floor + Lift(ctx);
        if (foot > floor) Lower(p, x0, floor, foot, width, murk, water);

        // the walls, back to front, and the ledge of the upper storey on the tall one
        foreach (var piece in Pieces)
        {
            if (piece.Left + piece.Width > width) continue;
            Wall(p, x0, piece, floor - piece.Base, murk, water);
            if (piece.Base > 0) Ledge(p, x0, piece.Left - 1, piece.Width + 3, floor - piece.Base + 1, murk, water);
        }

        // the weed growing out of the tops of the walls, swaying
        foreach (var (wx, wy, length, seed) in Weeds) if (wx < width) WeedOn(p, x0, wx, floor - wy, length, phase, seed, murk, water);

        // the stone floor in front of them: a slab of cracked flags seen from above a little, its front face of bricks
        const int fx = 40, fw = 64, depth = 6;
        var fy = floor - 4;
        for (var row = 0; row < depth; row++)
        {
            var y = fy - depth + row;
            var shift = depth - row;
            for (var x = 0; x < fw; x++)
            {
                var cx = fx + x + shift;
                var edge = row == 0 || x == 0 || x == fw - 1;
                var cell = ((cx + (row * 3)) / 7) + ((row / 3) * 31);
                uint c = edge ? Outline : (Hash(cell, 5) % 3) switch { 0 => Flag, 1 => FlagHi, _ => FlagDark };
                if (!edge && ((cx + (row * 3)) % 7 == 0 || row == 3)) c = Crack;
                if (!edge && Noise(cx, row + 40, 3) > 0.62) c = Moss[(int)(Hash(cx, row) % 3)];
                Put(cx, y, c);
            }
        }

        for (var row = 0; row < 4; row++)
        {
            for (var x = 0; x < fw; x++)
            {
                var edge = x == 0 || x == fw - 1 || row == 3;
                var mortar = row == 1 || ((x + ((row / 2) * 3)) % 6 == 5);
                Put(fx + x, fy + row, edge ? Outline : mortar ? Mortar : row == 0 ? Brick[3] : Side);
            }
        }

        // the chest on the floor, a little right of its middle, its shadow under it
        var rows = CapGimmighoulArt.Rows;
        var chestX = ChestLeft;
        var chestY = fy - depth + 3 - ChestFoot;
        for (var x = 0; x < rows[0].Length; x++) for (var r = 0; r < 2; r++) Put(chestX + x + 2 - r, fy - depth + 3 + r, CapSheet.Mix(Flag, Outline, 0.7));
        for (var y = 0; y < rows.Length; y++)
        {
            var line = rows[y];
            for (var x = 0; x < line.Length; x++)
            {
                var k = CapGimmighoulArt.Keys.IndexOf(line[x]);
                if (k >= 0) Put(chestX + x, chestY + y, CapGimmighoulArt.Colours[k]);
            }
        }

        // a glint running over the coins now and then, and the bubbles from the snorkel
        var glint = phase * 0.7 % 4;
        if (glint < 1)
        {
            var gx = chestX + 3 + (int)(glint * 14);
            Put(gx, chestY + 17, 0xFFFFFF);
            Put(gx + 1, chestY + 16, 0xFFF6C0);
        }

        for (var n = 0; n < 3; n++)
        {
            var u = ((phase * 0.45) + (n / 3.0)) % 1;
            var bx = chestX + Snorkel.X + (int)Math.Round(Math.Sin((u * 9) + n) * 1.2);
            var by = chestY + Snorkel.Y - 2 - (int)(u * 30);
            var c = CapSheet.Mix(0xE8FAFF, water, 0.2 + (u * 0.5));
            p.Set(x0 + bx, by, c);
            if (n != 1) { p.Set(x0 + bx + 1, by, c); p.Set(x0 + bx, by - 1, c); p.Set(x0 + bx + 1, by - 1, CapSheet.Mix(c, 0xFFFFFF, 0.5)); }
        }

        // a few coins spilt on the floor
        foreach (var (cx, cy) in Spilt) { Put(chestX + cx, fy - depth + cy, 0xFFD448); Put(chestX + cx + 1, fy - depth + cy, 0xC48E1E); }

        // the sand drifted against their foot, running back into the seabed either side
        var sand = CapSheet.Mix(0xB8B07Au, pal.Floor[0], 0.35);
        var sandDark = CapSheet.Mix(0x9A9468u, pal.Floor[1], 0.35);
        for (var x = -6; x < width + 4; x++)
        {
            var bed = CapSea.Relief(l, ctx, x0 + x);
            var top = foot - 2 + (int)Math.Round((0.9 * Math.Sin(x * 0.17)) + (0.6 * Math.Sin(x * 0.53)));
            if (x < 0 || x >= width) top += (int)Math.Round((Math.Min(Math.Abs(x < 0 ? x : x - width + 1), 8) / 8.0) * Math.Max(0, bed - top));
            for (var y = top; y <= bed; y++) p.Set(x0 + x, y, y == top ? sand : sandDark);
        }

        if (ctx.Rising >= 0) Rising(p, x0, floor, foot, width, ctx.Rising, sand, sandDark);

        // the fallen blocks and the rubble, on the sand (on the risen storey's top once it has risen)
        Block(p, x0, 6, floor - 8, 14, 7, murk, water);
        if (width > 170) Block(p, x0, 150, floor - 7, 12, 6, murk, water);
        foreach (var (rx, ry, rw) in Rubble) if (rx + rw < width) Stone(p, x0, rx, floor - 1 - ry, (rw * 2) - 1, murk, water);
    }

    /// <summary>
    /// A wall of worn stone bricks standing on <paramref name="floor"/>: a broken top that steps down by bricks, a darker side face to the right,
    /// bricks of four tones lit on their top and left, a missing brick here and there, moss over its top dripping down and in patches, and an
    /// arch or a window cut through it.
    /// </summary>
    private static void Wall(CapSheet p, int x0, Piece piece, int floor, double murk, uint water)
    {
        var (left, width, height, seed) = (piece.Left, piece.Width, piece.Height, piece.Seed);

        int Top(int x) => TopOf(piece, x);

        bool Opening(int x, int ly)
        {
            if (piece.ArchAt >= 0)
            {
                var dx = x - piece.ArchAt;
                var r = piece.ArchWidth / 2.0;
                if (Math.Abs(dx + 0.5) < r && ly < piece.ArchHeight - r) return true;
                if (ly >= piece.ArchHeight - r && Math.Pow((dx + 0.5) / r, 2) + Math.Pow((ly - (piece.ArchHeight - r)) / r, 2) < 1) return true;
            }

            var wx = piece.WindowAt;
            return piece.Window && x >= wx && x < wx + 8 && ly >= Math.Min(12, height / 2 - 2) && ly < Math.Min(12, height / 2 - 2) + 7;
        }

        for (var x = 0; x < width; x++)
        {
            var h = Top(x);
            for (var ly = 0; ly < h; ly++)
            {
                var y = floor - ly;
                if (Opening(x, ly)) continue;
                var outline = x == 0 || ly == h - 1 || !(x + 1 < width && ly < Top(x + 1)) || Opening(x - 1, ly) || Opening(x + 1, ly) || Opening(x, ly + 1) || Opening(x, ly - 1);
                var course = ly / 3;
                var off = (course % 2) * 3;
                var mortar = ly % 3 == 0 || (x + off) % 6 == 5;
                var brick = Brick[Hash(((x + off) / 6) + (seed * 100), course) % 4];
                uint c = mortar ? Mortar : ly % 3 == 2 || (x + off) % 6 == 0 ? CapSheet.Mix(brick, BrickHi, 0.5) : brick;
                if (!mortar && Hash(((x + off) / 6) + (seed * 200), course) % 19 == 0) c = Recess;
                if (x >= width - 3) c = mortar ? SideDark : Side;
                if (Opening(x - 1, ly) || Opening(x, ly + 1)) c = Side;
                var mossy = (h - ly <= 2 && Noise(x, ly, seed) > 0.3) || (h - ly <= 2 + (int)(Hash(x, seed + 9) % 6) && Hash(x, seed + 9) % 4 == 0)
                    || Noise(x + 50, ly, seed + 5) > 0.74;
                if (mossy) c = Moss[Math.Min(3, (h - ly <= 1 ? 0 : 1) + (int)(Hash(x, ly) % 2))];
                if (outline) c = Outline;
                p.Set(x0 + left + x, y, CapSheet.Mix(c, water, murk));
            }
        }
    }

    /// <summary>The ledge under what is left of the upper storey: a row of long stones standing out, lit on top, a shadow under it.</summary>
    private static void Ledge(CapSheet p, int x0, int left, int width, int y, double murk, uint water)
    {
        for (var x = 0; x < width; x++)
        {
            for (var r = 0; r < 3; r++)
            {
                var edge = r == 0 || r == 2 || x == 0 || x == width - 1;
                uint c = edge ? Outline : x % 8 == 0 ? Mortar : BrickHi;
                if (!edge && Noise(x, 7, 4) > 0.62) c = Moss[0];
                p.Set(x0 + left + x, y + r, CapSheet.Mix(c, water, murk));
            }
        }
    }

    private static void WeedOn(CapSheet p, int x0, int x, int y, int length, double phase, int seed, double murk, uint water)
    {
        for (var n = 0; n < 3; n++)
        {
            var bx = x + (n * 2) - 2;
            for (var k = 0; k < length - n; k++)
            {
                var sway = (int)Math.Round(Math.Sin((phase * 1.3) + (seed * 2) + (n * 0.8) + (k * 0.35)) * (k / (double)length) * 2);
                p.Set(x0 + bx + sway, y - k, CapSheet.Mix(k % 3 == 0 ? Moss[1] : Moss[2], water, murk));
            }
        }
    }

    private static void Block(CapSheet p, int x0, int left, int top, int w, int h, double murk, uint water)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var broken = (x == w - 1 && y < 2) || (x == 0 && y == 0);
                if (broken) continue;
                var edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 || (x == w - 2 && y < 2);
                var mortar = y % 3 == 0 || (x + ((y / 3) % 2 * 3)) % 6 == 5;
                uint c = edge ? Outline : y == 1 ? BrickHi : mortar ? Mortar : x >= w - 3 ? Side : Brick[(x / 6 + y / 3) % 4];
                if (!edge && y == 1 && Hash(x, 33) % 2 == 0) c = Moss[1];
                p.Set(x0 + left + x, top + y, CapSheet.Mix(c, water, murk));
            }
        }
    }

    private static void Stone(CapSheet p, int x0, int x, int y, int w, double murk, uint water)
    {
        var h = Math.Max(2, (w * 2) / 3);
        for (var dy = 0; dy < h; dy++)
        {
            for (var dx = 0; dx < w; dx++)
            {
                var corner = (dx == 0 || dx == w - 1) && (dy == 0);
                if (corner) continue;
                var edge = dx == 0 || dx == w - 1 || dy == 0 || dy == h - 1;
                uint c = edge ? Outline : dy == 1 && dx < w - 2 ? BrickHi : Brick[2];
                p.Set(x0 + x + dx, y - h + dy + 1, CapSheet.Mix(c, water, murk));
            }
        }
    }

    // ---------------------------------------------------------------- the storey that rises (2026-10-19)

    /// <summary>
    /// How high the buried storey stands once it has risen: the ruins on it go up by as much. It rises after the Electrostal's film (the player
    /// asked for room for the crystals still to come): the ground rumbles and it comes up out of the sand in four shoves, sand pouring off it.
    /// </summary>
    public const int Storey = 30;

    /// <summary>When it starts rising, on the Electrostal's film clock (Raikou has left the abyss's window), and how long it takes.</summary>
    public const double RiseStart = 7.4, RiseSpan = 4.4;

    private const int Shoves = 4;

    /// <summary>How far it has risen (0 to 1), some seconds after it started: four shoves, each a jolt up and a settle.</summary>
    public static double RiseOf(double since)
    {
        if (since <= 0) return 0;
        if (since >= RiseSpan) return 1;
        var each = RiseSpan / Shoves;
        var k = Math.Floor(since / each);
        var u = Math.Clamp((since - (k * each)) / (each * 0.6), 0, 1);
        // fast at first, slowing to a stop: a heavy thing pushed up
        return (k + (1 - Math.Pow(1 - u, 3))) / Shoves;
    }

    /// <summary>The ground shaking while it rises, a jolt at each shove: over the whole window, once everything is painted.</summary>
    public static void Rumble(CapSheet p, CapWorldLayout l, CapSeaContext ctx)
    {
        if (l.Panel != CapPanelKind.Deep || ctx.Rising < 0 || ctx.Rising >= RiseSpan + 0.3) return;
        var each = RiseSpan / Shoves;
        CapTrial.Shake(p, ctx.Rising % each, 2.4, each * 0.8);
    }

    /// <summary>The arches of the risen storey: where along it (each a niche for a crystal still to come).</summary>
    internal static readonly int[] Arches = [16, 52, 88, 124, 160, 196, 228];

    internal const int ArchW = 14, ArchH = 19, Cornice = 3, Plinth = 2;

    /// <summary>
    /// The risen storey, its top under the ruins' floor and as much of it as is out of the sand: a cornice of long stones lit on top, then a
    /// wall of big worn blocks with a row of round arches (dark niches, their inner side lit, a keystone over each), pilasters between them,
    /// moss in patches and dripping from the cornice, cracks, and the plinth at its foot.
    /// </summary>
    private static void Lower(CapSheet p, int x0, int top, int bed, int width, double murk, uint water)
    {
        void Put(int x, int y, uint c) => p.Set(x0 + x, y, CapSheet.Mix(c, water, murk));

        for (var y = top + 1; y <= bed; y++)
        {
            var ly = y - top - 1;                                       // rows down from its top
            for (var x = 0; x < width; x++)
            {
                uint c;
                var edge = x == 0 || x == width - 1;
                if (ly < Cornice)
                {
                    // the cornice: long stones, standing out a cell either side
                    c = ly == 0 || ly == Cornice - 1 || edge ? Outline : x % 10 == 0 ? Mortar : BrickHi;
                    if (ly == 1 && c == BrickHi && Noise(x, 3, 11) > 0.6) c = Moss[0];
                    Put(x, y, c);
                    continue;
                }

                var fromFoot = Storey - 1 - ly;                         // rows up from its foot
                var arch = ArchAt(x, fromFoot);
                if (arch is { } a)
                {
                    // inside a niche: dark, its left side and its top lit a little by the light from the way in
                    c = a.Rim ? Outline : a.Dx <= 1 ? Side : a.Top ? SideDark : Recess;
                    Put(x, y, c);
                    continue;
                }

                if (fromFoot < Plinth)
                {
                    c = fromFoot == Plinth - 1 ? Outline : x % 12 == 11 ? Mortar : Side;
                    Put(x, y, edge ? Outline : c);
                    continue;
                }

                // big blocks, four cells high and eight long, every other course set half over
                var course = (ly - Cornice) / 4;
                var off = (course % 2) * 4;
                var mortar = (ly - Cornice) % 4 == 3 || (x + off) % 8 == 7;
                var stone = Brick[Hash(((x + off) / 8) + 900, course) % 4];
                c = mortar ? Mortar : (ly - Cornice) % 4 == 0 || (x + off) % 8 == 0 ? CapSheet.Mix(stone, BrickHi, 0.5) : stone;
                if (!mortar && Hash(((x + off) / 8) + 950, course) % 23 == 0) c = Recess;
                if (Pilaster(x)) c = mortar ? SideDark : (x % 36) == 33 ? BrickHi : Brick[3];
                // the keystone over each arch
                if (Keystone(x, fromFoot)) c = (Hash(x, fromFoot) % 2 == 0) ? BrickHi : Brick[3];
                // moss: patches, and dripping down from under the cornice
                var drip = ly - Cornice < 1 + (int)(Hash(x, 17) % 5) && Hash(x, 19) % 3 == 0;
                if (drip || Noise(x + 300, ly, 21) > 0.76) c = Moss[1 + (int)(Hash(x, ly) % 2)];
                // a crack or two
                if (Crack(x, ly)) c = Outline;
                if (edge) c = Outline;
                Put(x, y, c);
            }
        }

        static bool Pilaster(int x) => x % 36 >= 32 && x % 36 < 35;

        static bool Keystone(int x, int fromFoot)
        {
            foreach (var ax in Arches)
            {
                if (Math.Abs(x - (ax + (ArchW / 2))) <= 1 && fromFoot >= ArchH && fromFoot < ArchH + 3) return true;
            }

            return false;
        }

        static bool Crack(int x, int ly) =>
            (x == 70 + (ly / 3) && ly > 6 && ly < 18) || (x == 182 - (ly / 4) && ly > 4 && ly < 14) || (x == 112 + (ly % 3 == 0 ? 1 : 0) && ly > 12 && ly < 22);
    }

    /// <summary>Whether a cell of the storey (a column, and how far up from its foot) is in an arch: its rim, how far in from its left, whether in its top.</summary>
    internal static (bool Rim, int Dx, bool Top)? ArchAt(int x, int fromFoot)
    {
        foreach (var ax in Arches)
        {
            var dx = x - ax;
            if (dx < 0 || dx >= ArchW || fromFoot < Plinth) continue;
            var r = ArchW / 2.0;
            var cx = dx + 0.5 - r;
            var spring = ArchH - r;
            var inside = fromFoot < spring ? Math.Abs(cx) < r : (cx * cx) + ((fromFoot - spring) * (fromFoot - spring)) < r * r;
            if (!inside) continue;
            var rimIn = fromFoot < spring ? Math.Abs(cx) >= r - 1 : (cx * cx) + ((fromFoot - spring) * (fromFoot - spring)) >= (r - 1) * (r - 1);
            return (rimIn, dx, fromFoot >= spring);
        }

        return null;
    }

    /// <summary>
    /// What it throws off as it rises: sand pouring off its cornice in streams and off its ends, clouds of sand billowing out along its foot at
    /// each shove, and stones knocked off the walls falling.
    /// </summary>
    private static void Rising(CapSheet p, int x0, int top, int foot, int width, double since, uint sand, uint sandDark)
    {
        var each = RiseSpan / Shoves;
        var shove = since % each;

        // streams of sand pouring off the cornice while it moves, and running dry after: a column a cell wide, its grains running down in
        // light and dark pairs; bigger ones off both ends
        for (var s = 0; s < 12; s++)
        {
            var x = s == 0 ? 0 : s == 1 ? width - 1 : 8 + (int)(Hash(s, 71) % (uint)Math.Max(1, width - 16));
            var start = (s % Shoves) * each;
            var age = since - start;
            if (age < 0 || age > each * 0.9) continue;
            var thick = s < 2 ? 2 : 1;
            // the head of the stream falls; its tail follows once the shove stops
            var head = top + 2 + (age * 70);
            var tail = top + 2 + Math.Max(0, (age - (each * 0.5)) * 70);
            for (var y = (int)tail; y < Math.Min(head, foot); y++)
            {
                var grain = (y + (int)(since * 40)) % 4;
                if (grain == 3) continue;
                for (var w = 0; w < thick; w++) p.Set(x0 + x + (s == 0 ? -w - 1 : s == 1 ? w + 1 : w), y, grain == 0 ? sandDark : sand);
            }

            // where it lands, a little heap splashing
            if (head >= foot - 1) { p.Set(x0 + x - 1, foot - 2, sand); p.Set(x0 + x + 1, foot - 2, sand); p.Set(x0 + x, foot - 3, sandDark); }
        }

        // puffs of sand rolling out along its foot at each shove: round billows, lit on top, shrinking away as they go
        if (since < RiseSpan + 1)
        {
            var age = since >= RiseSpan ? since - ((Shoves - 1) * each) : shove;
            for (var n = 0; n < 14; n++)
            {
                const double Life = 0.9;
                if (age > Life) continue;
                var k = age / Life;
                var dir = n % 2 == 0 ? -1 : 1;
                var x = (n * width / 14.0) + 6 + (dir * k * 10);
                var r = (int)Math.Round((2 + (Hash(n, 75) % 2)) * (k < 0.3 ? 0.6 + k : 1.3 - (k * 1.1)));
                if (r < 1) continue;
                var cy = foot - 2 - (int)(k * 3);
                for (var oy = -r; oy <= r; oy++)
                {
                    for (var ox = -r - 1; ox <= r + 1; ox++)
                    {
                        if ((ox * ox * 0.7) + (oy * oy) > (r * r) + 0.5) continue;
                        p.Set(x0 + (int)Math.Round(x) + ox, cy + oy, oy < 0 ? sand : oy == r ? CapSheet.Mix(sandDark, Outline, 0.3) : sandDark);
                    }
                }
            }
        }

        // stones knocked off the walls by the shoves, falling with a bounce
        for (var n = 0; n < 8 && since < RiseSpan + 1.2; n++)
        {
            var at = (n % Shoves) * each + 0.05 + (Hash(n, 77) % 30 / 100.0);
            var t = since - at;
            if (t < 0 || t > 1.2) continue;
            var x = 10 + (int)(Hash(n, 79) % (uint)Math.Max(1, width - 20));
            var y0 = top - 20 - (int)(Hash(n, 81) % 16);
            var y = y0 + (60 * t * t);
            if (y > foot - 2) y = foot - 2 - (Math.Abs(Math.Sin(t * 9)) * 3 * (1.2 - t));
            for (var dy = 0; dy < 2; dy++) for (var dx = 0; dx < 2; dx++) p.Set(x0 + x + dx + (int)(t * (n % 2 == 0 ? 6 : -6)), (int)y + dy, dy == 0 && dx == 0 ? BrickHi : Brick[2]);
        }
    }
}
