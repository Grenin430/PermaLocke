namespace PermaLocke.App.Views;

/// <summary>
/// What lies on the sand of the deep's window (2026-10-15, option 4 of the prototypes): a wall of the Unown ruins with the inscription of the
/// reference cut in it («NANIKAGA / OKORU?» in Unown letters), a cluster of Terastal crystals of Area Zero, and the fossils of the reference sheet
/// (Helix, Old Amber, Dome, Claw, Armor). The stone and the glass are painted once with the scene; what glows and moves (the letters, the light
/// under the crystals, their sheen, the gold dust, the sparkles) every frame. Only in the tall band, and each piece only where there is room for
/// it. The fossils of the fallen lie in front of all this; the kelp keeps clear of it.
/// </summary>
public static class CapRuins
{
    private enum Kind { Wall, Helix, Crystals, Amber, Dome, Claw, Skull }

    // Each piece: the column it stands on in the deep's window, and the columns it takes either side of that (the approved layout).
    private static readonly (Kind Kind, int X, int Left, int Right)[] Pieces =
    [
        (Kind.Wall, 2, 0, 84), (Kind.Helix, 76, -8, 8), (Kind.Crystals, 110, -26, 28), (Kind.Amber, 144, -4, 4),
        (Kind.Dome, 162, -9, 9), (Kind.Claw, 182, -6, 6), (Kind.Skull, 200, -6, 6),
    ];

    private const int WallWidth = 71;
    private const int WallHeight = 37;
    private const uint Outline = 0x2A1C0Eu;

    /// <summary>The pieces this window has room for.</summary>
    private static IEnumerable<(Kind Kind, int X, int Left, int Right)> Placed(CapWorldLayout l)
    {
        if (l.Panel != CapPanelKind.Deep || l.Mode is not (CapMode.Column or CapMode.Slim)) yield break;

        foreach (var piece in Pieces)
        {
            if (piece.X + piece.Left < 0 || piece.X + piece.Right > l.Cols - 1) continue;
            // The wall stands 37 rows: it needs that much water over the floor, and none of it under the game.
            if (piece.Kind == Kind.Wall && (l.FloorTop - WallHeight - 4 <= l.SurfaceY + 8 || l.InNotch(piece.X + piece.Right, l.FloorTop - WallHeight - 4))) continue;
            yield return piece;
        }
    }

    /// <summary>Whether a column is taken by one of the pieces (with a cell to spare): the kelp does not grow there.</summary>
    public static bool Covers(CapWorldLayout l, int x) => Placed(l).Any(p => x >= p.X + p.Left - 2 && x <= p.X + p.Right + 2);

    /// <summary>The stone and the glass, into the scene, over the floor and under the fossils of the fallen.</summary>
    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, IReadOnlyList<int> fossils)
    {
        var b = new Brush(p, l, ctx, fossils, pal.Water[4], 0);
        foreach (var piece in Placed(l))
        {
            switch (piece.Kind)
            {
                case Kind.Wall: Wall(b, piece.X, false); break;
                case Kind.Helix: Helix(b, piece.X); break;
                case Kind.Crystals: Crystals(b, piece.X, 0.8, false); break;
                case Kind.Amber: Amber(b, piece.X, false); break;
                case Kind.Dome: Dome(b, piece.X); break;
                case Kind.Claw: Claw(b, piece.X); break;
                case Kind.Skull: Skull(b, piece.X); break;
            }
        }
    }

    /// <summary>What glows and moves, every frame: on a 3 s loop of the mood's clock.</summary>
    public static void Live(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, IReadOnlyList<int> fossils, double phase)
    {
        var b = new Brush(p, l, ctx, fossils, pal.Water[4], phase / 3);
        foreach (var piece in Placed(l))
        {
            switch (piece.Kind)
            {
                case Kind.Wall: Wall(b, piece.X, true); break;
                case Kind.Crystals: Crystals(b, piece.X, 0.8, true); break;
                case Kind.Amber: Amber(b, piece.X, true); break;
            }
        }
    }

    /// <summary>Where and how to paint: a stone cell takes a little of the sea's blue, a light does not; the sand's top at a column; the loop's time.</summary>
    private readonly struct Brush(CapSheet p, CapWorldLayout l, CapSeaContext ctx, IReadOnlyList<int> fossils, uint sea, double t)
    {
        public double T => t;

        public void Cell(int x, int y, uint c) => p.Set(x, y, CapSheet.Mix(c, sea, 0.16));

        public void Glow(int x, int y, uint c, int a) => p.Set(x, y, c, a);

        public int G(int x) => CapSea.Relief(l, ctx, x, fossils);

        /// <summary>A star of light that flashes once a loop: a white point and a cross of pale gold round it.</summary>
        public void Sparkle(int cx, int cy, double offset, int size)
        {
            var k = Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * (t + offset))), 8);
            if (k < 0.05) return;
            var a = R(235 * k);
            Glow(cx, cy, 0xFFFFFFu, a);
            for (var d = 1; d <= size; d++)
            {
                foreach (var (dx, dy) in Cross) Glow(cx + (dx * d), cy + (dy * d), 0xFFF4C0u, R(a * (1 - (d / (size + 1.0)))));
            }
        }
    }

    private static readonly (int, int)[] Cross = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Rounds as the prototype did (halves up), so the shapes are the ones approved.</summary>
    private static int R(double v) => (int)Math.Floor(v + 0.5);

    private static uint Shade3(uint light, uint mid, uint dark, double dx, double dy, double r)
    {
        var k = (dx + dy) / (2 * r);
        return k < -0.35 ? light : k > 0.3 ? dark : mid;
    }

    // ---------------------------------------------------------------- the Unown ruins

    // The letters of the reference, one cell a stroke: the Unown eye (a ring with a dot) and the strokes that make its letter. Row 0 of a
    // map is the top of the eye (Top says where the map starts from it), so the eyes of a line stand level.
    private static readonly Dictionary<char, (int Top, string[] Rows)> Unown = new()
    {
        ['N'] = (-2, ["#....", "#....", "####.", "#...#", "#.#.#", "#...#", ".####", "....#", "....#"]),
        ['A'] = (-1, ["..#..", ".###.", "#...#", "#.#.#", "#...#", ".###.", ".#.#.", "#...#"]),
        ['I'] = (-2, ["..#..", "..#..", ".###.", "#...#", "#.#.#", "#...#", ".###.", "..#..", "..#.."]),
        ['K'] = (-1, ["..#....", ".###..#", "#...##.", "#.#.#..", "#...##.", ".###..#", "..#...."]),
        ['G'] = (-2, ["#####", "#...#", ".###.", "#...#", "#.#.#", "#...#", ".###.", "..##.", ".#...", "####."]),
        ['O'] = (-2, ["..#####..", ".#.....#.", "#..###..#", "#.#...#.#", "#.#.#.#.#", "#.#...#.#", "#..###..#", ".#.....#.", "..#####..", "....#...."]),
        ['R'] = (0, [".###.", "#...#", "#.#.#", "#...#", "####.", "#..#.", "#...#"]),
        ['U'] = (0, ["..###..", "##...##", "#.#.#.#", "#.....#", "##...##", ".#.#.#.", "..###.."]),
        ['?'] = (-4, [".###.", "#...#", "....#", "...#.", ".###.", "#...#", "#.#.#", "#...#", ".###."]),
    };

    private const string Line1 = "NANIKAGA";
    private const string Line2 = "OKORU?";

    private static int LineWidth(string line) => line.Sum(ch => Unown[ch].Rows[0].Length) + line.Length - 1;

    /// <summary>
    /// A wall of sandstone bricks stepping down to the right, an engraved band above and below, and the inscription cut into it in two lines;
    /// a couple of fallen blocks at its foot. <paramref name="live"/> paints only the warm glow that breathes in the letters.
    /// </summary>
    private static void Wall(Brush b, int x0, bool live)
    {
        static int Height(int i) => i < 7 ? 37 : i < 58 ? 33 : i < 63 ? 24 : i < 67 ? 16 : i < 71 ? 9 : 0;
        uint[] brick = [0xC8903C, 0xD8A54E, 0xB07A30];
        const uint mortar = 0x8A5C24u, edge = 0x3A2410u;

        var floor = int.MinValue;
        for (var i = 0; i < WallWidth; i++) floor = Math.Max(floor, b.G(x0 + i) + 1);

        if (!live)
        {
            for (var i = 0; i < WallWidth; i++)
            {
                var x = x0 + i;
                var h = Height(i);
                for (var j = 0; j < h; j++)
                {
                    var row = j / 4;
                    var off = (row % 2) * 4;
                    var col = (i + off) / 8;
                    var seam = j % 4 == 0 || (i + off) % 8 == 0;
                    // behind the inscription the mortar is faint, so the cut letters read over it
                    var behind = i >= 8 && i < 57 && j >= 5 && j <= 30;
                    b.Cell(x, floor - j, seam ? (behind ? CapSheet.Mix(mortar, brick[1], 0.65) : mortar) : brick[((col * 7) + (row * 3)) % 3]);
                }

                // the outline: the top, and the sides where the next column is lower
                if (h > 0) b.Cell(x, floor - h, edge);
                var hl = i > 0 ? Height(i - 1) : 0;
                var hr = i < WallWidth - 1 ? Height(i + 1) : 0;
                for (var j = hl; j < h; j++) b.Cell(x, floor - j, edge);
                for (var j = hr; j < h; j++) b.Cell(x, floor - j, edge);
                if (h > 0 && i % 3 == 1) b.Cell(x, floor - h, 0x4C8A52u);                // moss on the top
            }

            for (var i = 8; i < 57; i++)
            {
                b.Cell(x0 + i, floor - 3, 0x7A3A18u);
                b.Cell(x0 + i, floor - 32, 0x7A3A18u);
            }
        }

        var pulse = 0.5 + (0.5 * Math.Sin(2 * Math.PI * b.T));
        foreach (var (line, eyeAbove) in new[] { (Line1, 28), (Line2, 15) })
        {
            var gx = x0 + 8 + (int)Math.Floor((49 - LineWidth(line)) / 2.0);
            foreach (var ch in line)
            {
                Letter(b, ch, gx, floor - eyeAbove, live, pulse);
                gx += Unown[ch].Rows[0].Length + 1;
            }
        }

        if (live) return;

        // fallen blocks at the foot
        foreach (var (bx, bw) in new[] { (x0 + 72, 6), (x0 + 80, 4) })
        {
            for (var i = 0; i < bw; i++)
            {
                for (var j = 0; j < 3; j++) b.Cell(bx + i, b.G(bx + i) + 1 - j, j == 2 ? 0xD8A54Eu : i == 0 || j == 0 ? edge : brick[(i + j) % 3]);
            }
        }
    }

    /// <summary>A letter cut into the brick: the stroke in red-brown with a dark rim on its upper-left side, the shadow above and left of the cut and the lit lip below and right of it.</summary>
    private static void Letter(Brush b, char ch, int gx, int eyeTop, bool live, double pulse)
    {
        var (top, rows) = Unown[ch];
        var width = rows[0].Length;
        bool On(int x, int y) => y - top >= 0 && y - top < rows.Length && x >= 0 && x < width && rows[y - top][x] == '#';

        if (live)
        {
            for (var y = top; y < top + rows.Length; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (On(x, y)) b.Glow(gx + x, eyeTop + y, 0xFF9A50u, R(10 + (26 * pulse)));
                }
            }

            return;
        }

        // a gap inside the letter (stroke on both sides of it in its row or in its column) keeps the plain brick
        bool Inside(int x, int y)
        {
            var across = Enumerable.Range(0, width).Where(c => On(c, y)).ToArray();
            var down = Enumerable.Range(top, rows.Length).Where(r => On(x, r)).ToArray();
            return (across.Any(c => c < x) && across.Any(c => c > x)) || (down.Any(r => r < y) && down.Any(r => r > y));
        }

        for (var y = top - 1; y <= top + rows.Length; y++)
        {
            for (var x = -1; x <= width; x++)
            {
                if (On(x, y) || Inside(x, y)) continue;
                if (On(x - 1, y) || On(x, y - 1)) b.Cell(gx + x, eyeTop + y, 0xF2CC84u);
                else if (On(x + 1, y) || On(x, y + 1)) b.Cell(gx + x, eyeTop + y, 0x8E5A22u);
            }
        }

        for (var y = top; y < top + rows.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!On(x, y)) continue;
                var rim = !On(x - 1, y) || !On(x, y - 1);
                b.Cell(gx + x, eyeTop + y, rim ? 0x5A1E0Cu : 0x8A3418u);
            }
        }
    }

    // ---------------------------------------------------------------- the Terastal crystals of Area Zero

    // Each crystal: across from the centre of the cluster, height, width, lean, and its number for the sheen's hue.
    private static readonly (int Dx, int H, int W, double Tilt, int Idx)[] Cluster =
        [.. new (int, int, int, double, int)[] { (0, 28, 9, 0, 0), (-9, 19, 7, -0.12, 1), (10, 22, 8, 0.1, 2), (18, 12, 5, 0.2, 3), (-17, 13, 5, -0.18, 4), (26, 7, 4, 0.25, 5), (-24, 7, 4, -0.25, 6) }.OrderBy(c => c.Item2)];

    private sealed record Crystal(int Bottom, int H, int W, int TipH, Func<int, int> CentreAt, Dictionary<(int X, int Y), double> Shape, int Idx);

    private static Crystal ShapeOf(int cx, int bottom, int w, int h, double tilt, int idx)
    {
        var tipH = (int)Math.Ceiling(w * 0.9);
        int CentreAt(int y) => cx + R(tilt * (h - y));
        var shape = new Dictionary<(int, int), double>();
        for (var y = 0; y < h; y++)
        {
            var half = y < tipH ? ((y + 0.5) / tipH) * (w / 2.0) : w / 2.0;
            var c0 = CentreAt(y);
            for (var x = (int)Math.Ceiling(c0 - half); x <= (int)Math.Floor(c0 + half - 0.01); x++) shape[(x, y)] = x - c0;
        }

        return new Crystal(bottom, h, w, tipH, CentreAt, shape, idx);
    }

    /// <summary>
    /// Glass: each crystal a gradient from cyan at the top to violet at the foot, the lit face brighter and the sea showing through, white
    /// specular streaks, gold seams and a gold outline. Live: a pool of blue light that breathes on the sand round them, a sheen of shifting hue
    /// that travels up each crystal, gold dust rising and a sparkle at the top.
    /// </summary>
    private static void Crystals(Brush b, int cx, double scale, bool live)
    {
        var crystals = Cluster.Select(c => ShapeOf(cx + c.Dx, b.G(cx + c.Dx) + 2, c.W, R(c.H * scale), c.Tilt, c.Idx)).ToArray();
        var t = b.T;

        if (live)
        {
            var glass = new HashSet<(int, int)>(crystals.SelectMany(c => c.Shape.Keys.Select(k => (k.X, c.Bottom - c.H + 1 + k.Y))));
            var pulse = 0.5 + (0.5 * Math.Sin(2 * Math.PI * t));
            for (var dy = -6; dy <= 4; dy++)
            {
                for (var dx = -26; dx <= 26; dx++)
                {
                    var d = Math.Sqrt(Math.Pow(dx / 26.0, 2) + Math.Pow(dy / 5.0, 2));
                    var y = b.G(cx + dx) - 1 + R(dy * 0.3);
                    if (d < 1 && !glass.Contains((cx + dx, y))) b.Glow(cx + dx, y, 0x78A8FFu, R(34 * (1 - d) * (0.6 + (0.4 * pulse))));
                }
            }

            uint[] hues = [0xFFC8F4, 0xC8FFFF, 0xFFF0C0, 0xE0C8FF];
            var band = (t * 16) % 16;
            foreach (var c in crystals)
            {
                var hue = hues[(int)Math.Floor(((t * 4) + c.Idx) % 4)];
                foreach (var (x, y) in c.Shape.Keys)
                {
                    var s = ((((x * 0.7) + (c.H - y) - band) % 16) + 16) % 16;
                    if (s < 3) b.Glow(x, c.Bottom - c.H + 1 + y, hue, R(90 * (1 - (s / 3))));
                }
            }

            for (var i = 0; i < 7; i++)
            {
                var age = (t + (i * 0.143)) % 1;
                b.Glow(cx - 16 + (i * 5) + R(Math.Sin(2 * Math.PI * (t + (i * 0.2))) * 1.5), b.G(cx) - 6 - R(age * 30 * scale), 0xFFE070u, R(220 * Math.Sin(Math.PI * age)));
            }

            b.Sparkle(cx + 1, b.G(cx) - R(26 * scale), 0.1, 3);
            return;
        }

        uint[] lit = [0x8CF0FF, 0x9AB8FF, 0xB08CFF];
        uint[] shaded = [0x38A8E0, 0x4C68D8, 0x6A40C8];
        static uint Ramp(uint[] stops, double k)
        {
            var s = Math.Min(1.999, Math.Max(0, k) * 2);
            var i = (int)Math.Floor(s);
            return CapSheet.Mix(stops[i], stops[i + 1], s - i);
        }

        foreach (var c in crystals)
        {
            var top = c.Bottom - c.H + 1;
            foreach (var ((x, y), dx) in c.Shape)
            {
                var down = y / (double)Math.Max(1, c.H - 1);
                var colour = dx < -0.5 ? Ramp(lit, down) : dx > 0.5 ? Ramp(shaded, down) : CapSheet.Mix(Ramp(lit, down), 0xFFFFFFu, 0.45);
                if (y < c.TipH && dx < 0) colour = CapSheet.Mix(colour, 0xFFFFFFu, 0.25);
                b.Glow(x, top + y, colour, 200);                                   // translucent: a little of the sea behind shows through
            }

            // specular whites: a short streak and a point on the lit face, below the tip
            for (var s = 0; s < 3; s++)
            {
                var y = c.TipH + 1 + s;
                if (y < c.H - 1) b.Glow(c.CentreAt(y) - Math.Max(1, c.W / 3) + (s > 1 ? 1 : 0), top + y, 0xFFFFFFu, 230 - (s * 50));
            }

            if (c.H > 10)
            {
                var y = (int)Math.Floor(c.H * 0.62);
                b.Glow(c.CentreAt(y) - 1, top + y, 0xFFFFFFu, 200);
            }

            // the gold seam of the facets: a chevron where the tip meets the body, and a line down the middle edge
            for (var d = -(c.W / 2) + 1; d <= (c.W / 2) - 1; d++)
            {
                var y = c.TipH + (Math.Abs(d) / 2);
                if (y < c.H) b.Cell(c.CentreAt(y) + d, top + y, 0xF0C030u);
            }

            for (var y = c.TipH; y < c.H; y++)
            {
                if (y % 3 != 2) b.Cell(c.CentreAt(y), top + y, 0xFFE070u);
            }

            // the gold outline, bright on the left and top, dark on the right and below
            foreach (var (x, y) in c.Shape.Keys)
            {
                foreach (var (ox, oy) in Cross)
                {
                    if (c.Shape.ContainsKey((x + ox, y + oy))) continue;
                    b.Cell(x + ox, top + y + oy, ox > 0 || oy > 0 ? 0xA87818u : 0xF0C030u);
                }
            }
        }
    }

    // ---------------------------------------------------------------- the fossils of the reference sheet
    // All lit from the top left: a pale highlight there, a darker side to the bottom right, a dark outline round them.

    /// <summary>A jagged slab of layered rock for a fossil to lie on.</summary>
    private static void Slab(Brush b, int cx, int halfW)
    {
        uint[] rock = [0xA07A4A, 0x76563A, 0x543A22];
        for (var i = -halfW; i <= halfW; i++)
        {
            var x = cx + i;
            var top = b.G(cx) - 2 - (((i * 7) + 3) % 3 == 0 ? 1 : 0);
            for (var y = top; y <= b.G(x) + 1; y++) b.Cell(x, y, Math.Abs(i) == halfW || y == top ? Outline : y == top + 1 ? rock[0] : (y + i) % 3 == 0 ? rock[2] : rock[1]);
        }
    }

    /// <summary>Helix Fossil: a golden-brown ammonite, the spiral cut into its face, on its rock.</summary>
    private static void Helix(Brush b, int cx)
    {
        const int r = 6;
        var cy = b.G(cx) - 8;
        Slab(b, cx, 8);
        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > r + 0.35) continue;
                var band = ((((Math.Atan2(dy, dx) / (2 * Math.PI)) + (d / 2.2)) % 1) + 1) % 1;
                var c = d > r - 0.75 ? Outline : band < 0.2 ? 0x6E4E20u : Shade3(0xF2DA98, 0xD2AC60, 0xA07A38, dx, dy, r);
                if (d < 1.2 && d <= r - 0.75) c = 0x6E4E20u;
                b.Cell(cx + dx, cy + dy, c);
            }
        }

        b.Cell(cx - 3, cy - 3, 0xFFF4D0u);
        b.Cell(cx - 2, cy - 4, 0xFFF4D0u);
    }

    /// <summary>Old Amber: a glossy golden-yellow oval with something asleep inside; live, a sparkle on its gloss.</summary>
    private static void Amber(Brush b, int cx, bool live)
    {
        var cy = b.G(cx) - 4;
        if (live)
        {
            b.Sparkle(cx - 2, cy - 4, 0.35, 2);
            return;
        }

        for (var dy = -5; dy <= 5; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var d = Math.Sqrt(Math.Pow(dx / 4.0, 2) + Math.Pow(dy / 5.0, 2));
                if (d > 1.05 || cy + dy > b.G(cx)) continue;
                b.Cell(cx + dx, cy + dy, d > 0.84 ? 0x5A3006u : d < 0.45 ? 0xE89A10u : Shade3(0xFFE070, 0xF4B820, 0xC07808, dx, dy, 4));
            }
        }

        foreach (var (dx, dy) in new[] { (0, 0), (-1, 1), (1, 1), (0, 1), (0, -1) }) b.Cell(cx + dx, cy + dy, 0x4A2808u);   // what sleeps inside
        foreach (var (dx, dy, a) in new[] { (-2, -3, 240), (-3, -2, 200), (-1, -4, 170) }) b.Glow(cx + dx, cy + dy, 0xFFFBE0u, a);   // the gloss
    }

    /// <summary>Claw Fossil: a moss-green rock with a pale, curved claw standing out of it.</summary>
    private static void Claw(Brush b, int cx)
    {
        int[] widths = [6, 10, 12, 12, 12];
        for (var r = 0; r < widths.Length; r++)
        {
            var w = widths[r];
            for (var i = 0; i < w; i++)
            {
                var x = cx - (w / 2) + i;
                var y = b.G(x) + 1 - (widths.Length - 1 - r);
                b.Cell(x, y, i == 0 || i == w - 1 || r == 0 ? 0x26341Au : r < 2 || i < 3 ? 0xB0C878u : i > w - 4 ? 0x5A7238u : 0x86A052u);
            }
        }

        var topY = b.G(cx) - 3;
        (int, int)[] path = [(-1, 0), (-1, -1), (0, -2), (1, -3), (2, -4), (3, -4), (4, -3)];
        foreach (var (dx, dy) in path)
        {
            b.Cell(cx + dx, topY + dy - 1, Outline);
            b.Cell(cx + dx + 1, topY + dy, Outline);
        }

        for (var i = 0; i < path.Length; i++)
        {
            var (dx, dy) = path[i];
            b.Cell(cx + dx, topY + dy, i < 3 ? 0xFFF6D8u : 0xEADFB0u);
            b.Cell(cx + dx, topY + dy + 1, 0x9A9468u);
        }
    }

    /// <summary>Armor Fossil: a blue-violet plate in layers, three dark slots on its back.</summary>
    private static void Skull(Brush b, int cx)
    {
        string[] rows = [".##########.", "############", "############", "############", "############", "############", ".##########."];
        bool Hash(int ri, int ci) => ci >= 0 && ci < rows[ri].Length && rows[ri][ci] == '#';
        for (var ri = 0; ri < rows.Length; ri++)
        {
            for (var ci = 0; ci < rows[ri].Length; ci++)
            {
                if (!Hash(ri, ci)) continue;
                var y = b.G(cx) + 1 - (rows.Length - 1 - ri);
                var edge = ri == 0 || ri == rows.Length - 1 || ci == 0 || ci == 11 || !Hash(ri, ci - 1) || !Hash(ri, ci + 1);
                var c = edge ? 0x22245Au : ri == 1 ? 0xB0B4F4u : ri == 2 ? 0x8C90E0u : ri is 3 or 5 ? 0x5458B0u : 0x6C70C8u;
                if (!edge && ci >= 9) c = CapSheet.Mix(c, 0x22245Au, 0.35);           // the side away from the light
                b.Cell(cx - 6 + ci, y, c);
            }
        }

        foreach (var sx in new[] { 3, 6, 9 })
        {
            b.Cell(cx - 6 + sx, b.G(cx) + 1 - 5, 0x14163Au);
            b.Cell(cx - 6 + sx, b.G(cx) + 1 - 4, 0x14163Au);
        }
    }

    /// <summary>Dome Fossil: a brown dome with its ridges, on its rock.</summary>
    private static void Dome(Brush b, int cx)
    {
        const int rx = 8, ry = 7;
        Slab(b, cx, 9);
        for (var dy = 0; dy < ry; dy++)
        {
            var half = R(rx * Math.Sqrt(1 - Math.Pow(dy / (double)ry, 2)));
            for (var dx = -half; dx <= half; dx++)
            {
                var y = b.G(cx + dx) - 2 - dy;
                var ridge = dy % 2 == 1 && Math.Abs(dx) < half - 1;
                b.Cell(cx + dx, y, dx == -half || dx == half || dy == ry - 1 ? Outline : ridge ? 0x8A6428u : Shade3(0xF0D488, 0xC89C50, 0x9A7032, dx, -dy, rx));
            }
        }

        b.Cell(cx - 3, b.G(cx) - 7, 0xFFF0C8u);
        b.Cell(cx - 2, b.G(cx) - 8, 0xFFF0C8u);
    }
}
