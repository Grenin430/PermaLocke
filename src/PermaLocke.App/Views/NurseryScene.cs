using Color = PermaLocke.App.Views.PixelColour;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>The moments of the nursery's eggs arriving, in seconds from the button (§225, redone in §228 and §229).</summary>
public static class NurseryTimeline
{
    /// <summary>The room darkens and the first stars cross the sky.</summary>
    public const double Lead = 0.8;

    /// <summary>The rain of stars: it starts as a few and ends as a downpour, and the egg is built out of what falls.</summary>
    public const double Rain = 3.2;

    /// <summary>The egg is whole, made of stardust.</summary>
    public static double Landed(int eggs) => Lead + Rain;

    /// <summary>Kept for the callers that asked when the shake began: there is none now, the pulse follows at once.</summary>
    public static double Shake(int eggs) => Landed(eggs);

    /// <summary>The pulse of light: the dust turns into shell from the bottom up and a wave runs out.</summary>
    public static double Flash(int eggs) => Landed(eggs) + 0.2;

    /// <summary>From here it rests: the egg floats with its halo, and the stars drift.</summary>
    public static double Rest(int eggs) => Flash(eggs) + 1.3;
}

/// <summary>
/// The animation of the nursery's eggs arriving in the boxes (§229): pixel art at the screen's own resolution, drawn a frame at
/// a time from a moment, so it is tested without a window.
/// </summary>
/// <remarks>
/// <para>
/// A rain of shooting stars in the colour of the run's type. They start as a few and become a downpour, and every one that lands
/// leaves a spark and lights one more block of an egg that is being sculpted out of stardust from the bottom up, over a dim
/// outline that tells where it will be. When the last block lights, a pulse of light runs out, the dust turns into shell from the
/// floor to the top, two waves leave and the egg floats up with a golden halo while the stars keep drifting.
/// </para>
/// <para>
/// The eggs are all alike on purpose: what is inside is never shown. Several at once work too (they are built together, from
/// the same rain). The spots are in the colour of the type, in a pattern that comes from the seed and the egg's number.
/// </para>
/// </remarks>
public sealed class NurseryScene
{
    private static readonly Color Ink = Rgb(0x0A, 0x08, 0x0C);
    private static readonly Color White = Rgb(0xFF, 0xFB, 0xF0);
    private static readonly Color Gold = Rgb(0xFF, 0xD2, 0x4A);
    private static readonly Color Shell = Rgb(0xF2, 0xE8, 0xBE);
    private static readonly Color ShellShade = Rgb(0xD2, 0xC4, 0x8E);
    private static readonly Color ShellDeep = Rgb(0xA4, 0x94, 0x62);
    private static readonly Color Outline = Rgb(0x4A, 0x3B, 0x22);

    /// <summary>Cells of an egg's picture.</summary>
    private const int EggCellsWide = 12, EggCellsHigh = 16;

    /// <summary>Which way the stars come from: up and to the right, falling down and to the left.</summary>
    private const double SlopeX = -0.5, SlopeY = 0.87;

    private readonly bool[,] _mask = Shape();
    private readonly (int X, int Y)[] _order;
    private readonly Dictionary<int, Color[,]> _patterns = [];
    private (uint Seed, Color Type) _patternsFor;
    private int _total = 1;

    /// <summary>The shake of the screen, in pixels, applied to everything drawn after the room.</summary>
    private int _ox, _oy;

    public NurseryScene(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new byte[Width * Height * 4];
        Cell = Math.Max(2, (int)Math.Round(Height / 200.0));

        // El orden en que se encienden los bloques: de abajo arriba, con un poco de desorden para que parezca polvo que se posa.
        var cells = new List<(int X, int Y, double Key)>();
        for (var y = 0; y < EggCellsHigh; y++)
        {
            for (var x = 0; x < EggCellsWide; x++)
            {
                if (_mask[x, y]) cells.Add((x, y, ((EggCellsHigh - y) * 1.0) + (Hash(x, y) % 1000 / 1000.0 * 2.6)));
            }
        }

        _order = [.. cells.OrderBy(c => c.Key).Select(c => (c.X, c.Y))];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Screen pixels per block of the effects.</summary>
    public int Cell { get; }

    /// <summary>The frame, premultiplied BGRA, opaque.</summary>
    public byte[] Pixels { get; }

    private double CentreX => Width / 2.0;

    /// <summary>Draws the moment <paramref name="t"/> seconds after the button.</summary>
    /// <param name="eggs">How many eggs come.</param>
    /// <param name="type">The colour of the run's type: stars, dust, waves and the spots of the shell.</param>
    public void Render(int eggs, Color type, uint seed, double t)
    {
        eggs = Math.Max(1, eggs);
        _total = eggs;

        if (_patternsFor != (seed, type))
        {
            _patterns.Clear();
            _patternsFor = (seed, type);
        }

        var layout = Layout(eggs);
        var lead = Ease(t / NurseryTimeline.Lead);
        var pulseAt = NurseryTimeline.Flash(eggs);
        var pulse = t - pulseAt;
        var built = Ease((t - (NurseryTimeline.Lead + 0.15)) / (NurseryTimeline.Rain - 0.35));

        Room(lead, type, built, pulse >= 0 ? 1 - Ease(pulse / 1.5) : 0);

        // La pantalla da un respingo con el pulso.
        (_ox, _oy) = ScreenShake(pulse);

        Motes(lead, type, t);
        Pool(layout, type, built, t);

        for (var e = 0; e < eggs; e++)
        {
            Egg(layout, e, t, built, pulse, type, seed);
        }

        Stars(layout, eggs, type, seed, t);

        if (pulse >= 0)
        {
            Shockwaves(layout, type, pulse);
            Confetti(layout, type, seed, pulse);
        }

        if (t >= NurseryTimeline.Rest(eggs))
        {
            Halo(layout, eggs, t - NurseryTimeline.Rest(eggs));
            Sparkles(layout, eggs, t - NurseryTimeline.Rest(eggs), type);
        }

        _ox = _oy = 0;

        if (pulse >= -0.02)
        {
            Flash(pulse, type);
        }
    }

    // ===================================================================================================== LAYOUT

    private readonly record struct EggLayout(int K, int Columns, int Rows, double Left, double Top, double StepX, double StepY);

    /// <summary>
    /// Rows of eggs, as even as they can be, with the biggest egg that lets them all fit on the screen: whole blocks, so
    /// its pixels stay square. One egg is drawn big.
    /// </summary>
    private EggLayout Layout(int eggs)
    {
        var biggest = eggs == 1 ? 0.5 : 0.27;

        for (var k = Math.Max(Cell, (int)(Height * biggest / EggCellsHigh / Cell) * Cell); k >= Cell; k -= Cell)
        {
            var eggW = EggCellsWide * k;
            var eggH = EggCellsHigh * k;
            var maxColumns = Math.Max(1, (int)((Width * 0.84) / (eggW * 1.3)));
            var rows = (int)Math.Ceiling(eggs / (double)maxColumns);
            var columns = (int)Math.Ceiling(eggs / (double)rows);

            if (rows * eggH * 1.25 <= Height * (eggs == 1 ? 0.6 : 0.52) || k == Cell)
            {
                var stepX = eggW * 1.3;
                var stepY = eggH * 1.2;
                var top = eggs == 1 ? (Height * 0.5) - (eggH / 2.0) : (Height * 0.52) - ((rows - 1) * stepY / 2);

                return new EggLayout(k, columns, rows, CentreX - ((columns - 1) * stepX / 2), top, stepX, stepY);
            }
        }

        return new EggLayout(Cell, 1, eggs, CentreX, Height * 0.5, 0, Height * 0.1);
    }

    /// <summary>Where egg <paramref name="i"/> rests: the middle of it. The last row is centred.</summary>
    private (double X, double Y) Slot(in EggLayout layout, int eggs, int i)
    {
        var row = i / layout.Columns;
        var inRow = row == layout.Rows - 1 ? eggs - (row * layout.Columns) : layout.Columns;
        var x = CentreX + (((i % layout.Columns) - ((inRow - 1) / 2.0)) * layout.StepX);

        return (x, layout.Top + (row * layout.StepY) + (EggCellsHigh * layout.K / 2.0));
    }

    private double FloorOf(in EggLayout layout, int i) => Slot(layout, _total, i).Y + (EggCellsHigh * layout.K / 2.0);

    /// <summary>The top left of an egg's picture, for a cell: where that block of the egg lands on the screen.</summary>
    private (double X, double Y) CellAt(in EggLayout layout, int egg, int gx, int gy, double lift = 0)
    {
        var (cx, cy) = Slot(layout, _total, egg);
        var k = layout.K;

        return (cx + ((gx - (EggCellsWide / 2.0) + 0.5) * k), cy - (EggCellsHigh * k / 2.0) + ((gy + 0.5) * k) - lift);
    }

    // ======================================================================================================= ROOM

    /// <summary>Dark from the edges, a glow in the middle in the type's colour that grows as the egg is built; a wash after the pulse.</summary>
    private void Room(double lead, Color type, double built, double afterglow)
    {
        var glow = (0.1 * lead) + (0.26 * built) + (0.28 * afterglow);
        var maxR = Math.Sqrt((CentreX * CentreX) + (Height * Height * 0.3));

        for (var y = 0; y < Height; y += Cell)
        {
            for (var x = 0; x < Width; x += Cell)
            {
                var dx = x - CentreX;
                var dy = (y - (Height * 0.5)) * 1.25;
                var d = Math.Sqrt((dx * dx) + (dy * dy)) / maxR;

                // Bandas de un bloque, como una viñeta de consola, no un degradado.
                var band = Math.Floor(Math.Min(1, d) * 7) / 7;
                var shade = (0.5 + (band * 0.5)) * Math.Max(lead, 0.0001);
                var warm = Math.Max(0, 1 - (d * 2.1)) * glow;
                var b = (byte)Math.Clamp((0x14 * (1 - shade)) + (type.B * warm), 0, 255);
                var g = (byte)Math.Clamp((0x0E * (1 - shade)) + (type.G * warm), 0, 255);
                var r = (byte)Math.Clamp((0x1C * (1 - shade)) + (type.R * warm), 0, 255);
                FillRaw(x, y, b, g, r, Cell, Cell);
            }
        }
    }

    private (int X, int Y) ScreenShake(double pulse)
    {
        if (pulse is < 0 or >= 0.4)
        {
            return (0, 0);
        }

        var strength = 1.4 * (1 - (pulse / 0.4));
        var n = (int)(pulse * 60);

        return ((int)Math.Round(Math.Sin(n * 2.7) * Cell * strength), (int)Math.Round(Math.Cos(n * 3.1) * Cell * strength));
    }

    /// <summary>Little motes of light rising through the room, from the moment it lights up.</summary>
    private void Motes(double lead, Color type, double t)
    {
        if (lead < 0.3)
        {
            return;
        }

        for (var i = 0; i < 30; i++)
        {
            var phase = (i * 0.6180339) % 1;
            var life = ((t * (0.04 + (0.03 * ((i * 7) % 5)))) + phase) % 1;
            var x = Width * (((i * 0.37) % 1) + (Math.Sin((t * 0.8) + i) * 0.015));
            var y = Height * (1.05 - (life * 1.1));
            var colour = life < 0.15 || life > 0.85 ? Mix(type, Ink, 0.7) : i % 4 == 0 ? Mix(type, White, 0.5) : Mix(type, Ink, 0.4);

            Block(x, y, colour);
        }
    }

    /// <summary>The pool of light on the floor under each egg: it grows with the egg, and it is where the stars that miss end up.</summary>
    private void Pool(in EggLayout layout, Color type, double built, double t)
    {
        for (var e = 0; e < _total; e++)
        {
            var (x, _) = Slot(layout, _total, e);
            var floor = FloorOf(layout, e) - (layout.K * 0.4);
            var rx = EggCellsWide * layout.K * (0.35 + (0.65 * built));

            for (var dy = -3; dy <= 3; dy++)
            {
                var span = rx * Math.Sqrt(Math.Max(0, 1 - ((dy * dy) / 10.0)));

                for (var dx = -span; dx <= span; dx += Cell)
                {
                    var edge = Math.Abs(dx) > span - (Cell * 2);
                    var twinkle = Hash((int)(dx / Cell), dy + (int)(t * 8)) % 7 == 0;

                    Block(x + dx, floor + (dy * Cell * 0.7), twinkle ? Mix(type, White, 0.5) : edge ? Mix(type, Ink, 0.45) : Mix(type, Ink, 0.7));
                }
            }
        }
    }

    // ======================================================================================================== EGG

    /// <summary>
    /// One egg: a dim outline of where it will be, then the blocks that the stars have lit, in stardust, and then, with the pulse,
    /// shell from the bottom up; after it, floating.
    /// </summary>
    private void Egg(in EggLayout layout, int i, double t, double built, double pulse, Color type, uint seed)
    {
        var k = layout.K;
        var lift = 0.0;
        var tilt = 0.0;

        if (pulse >= 0)
        {
            // Tras el pulso flota despacio y se mece.
            var rise = Ease((pulse - 0.5) / 1.1);
            lift = (rise * k * 3.6) + (Math.Sin((t * 2.1) + i) * k * 0.4 * rise);
            tilt = Math.Sin((t * 1.7) + (i * 0.9)) * 0.03 * rise;
        }

        var count = (int)(_order.Length * built);
        var sweep = pulse >= 0 ? Ease(pulse / 0.55) : 0.0;
        var pattern = PatternOf(i, type, seed);
        var lit = Mix(type, White, 0.75);

        // El contorno tenue: dónde va a estar el huevo, desde el principio.
        if (count < _order.Length)
        {
            for (var gy = 0; gy < EggCellsHigh; gy++)
            {
                for (var gx = 0; gx < EggCellsWide; gx++)
                {
                    if (_mask[gx, gy] && IsEdge(gx, gy) && (gx + gy) % 2 == 0)
                    {
                        var (x, y) = CellAt(layout, i, gx, gy);
                        Block(x, y, Mix(type, Ink, 0.72));
                    }
                }
            }
        }

        for (var n = 0; n < count; n++)
        {
            var (gx, gy) = _order[n];
            var shear = tilt * ((EggCellsHigh - 1) - gy) * k;
            var (cx, cy) = CellAt(layout, i, gx, gy, lift);
            var left = (int)(cx - (k / 2.0) + shear);
            var top = (int)(cy - (k / 2.0));

            // Con el pulso los bloques se vuelven cascarón de abajo arriba; hasta entonces son polvo de estrellas.
            var solid = pulse >= 0 && ((EggCellsHigh - 1 - gy) / (double)(EggCellsHigh - 1)) <= sweep;
            Color colour;

            if (solid)
            {
                colour = IsEdge(gx, gy) ? Outline : Shaded(pattern[gx, gy], gx, gy);

                if (pulse < 1.4)
                {
                    colour = Mix(colour, White, Math.Clamp(0.5 - (pulse * 0.4), 0, 0.5));
                }
            }
            else
            {
                // Los últimos bloques en encenderse brillan más: son los que acaban de recibir una estrella.
                var fresh = count - n < 14;
                var twinkle = Hash(gx + (i * 31), gy + (int)(t * 14)) % 5;
                colour = fresh ? White : twinkle == 0 ? White : twinkle == 1 ? lit : twinkle == 2 ? Mix(type, White, 0.4) : Mix(type, Ink, 0.15);
            }

            FillRect(left, top, k, k, colour);
        }
    }

    private bool IsEdge(int gx, int gy) =>
        gx == 0 || gy == 0 || gx == EggCellsWide - 1 || gy == EggCellsHigh - 1
        || !_mask[gx - 1, gy] || !_mask[gx + 1, gy] || !_mask[gx, Math.Max(0, gy - 1)] || !_mask[gx, Math.Min(EggCellsHigh - 1, gy + 1)];

    private Color[,] PatternOf(int i, Color type, uint seed)
    {
        if (_patterns.TryGetValue(i, out var cached))
        {
            return cached;
        }

        var random = new Random(unchecked((int)(seed * 2654435761u) ^ (i * 40503)));
        var grid = new Color[EggCellsWide, EggCellsHigh];
        var spot = Mix(type, Shell, 0.2);
        var spotDark = Mix(type, Ink, 0.4);

        for (var y = 0; y < EggCellsHigh; y++)
        {
            for (var x = 0; x < EggCellsWide; x++)
            {
                grid[x, y] = Shell;
            }
        }

        // Manchas: cuatro o cinco, de uno a tres bloques, solo donde hay cascarón.
        var spots = 4 + random.Next(2);
        for (var s = 0; s < spots; s++)
        {
            var cx = 2 + random.Next(EggCellsWide - 4);
            var cy = 3 + random.Next(EggCellsHigh - 6);
            var size = 1 + random.Next(2);

            for (var dy = 0; dy <= size; dy++)
            {
                for (var dx = 0; dx <= size + random.Next(2); dx++)
                {
                    var gx = cx + dx;
                    var gy = cy + dy;
                    if (gx < EggCellsWide && gy < EggCellsHigh && _mask[gx, gy])
                    {
                        grid[gx, gy] = dx == 0 && dy == size ? spotDark : spot;
                    }
                }
            }
        }

        _patterns[i] = grid;
        return grid;
    }

    /// <summary>Light from the top left: a highlight, a shade to the lower right.</summary>
    private static Color Shaded(Color colour, int gx, int gy)
    {
        var light = ((EggCellsWide - gx) + ((EggCellsHigh - gy) * 0.9)) / (EggCellsWide + (EggCellsHigh * 0.9));

        return light > 0.62 ? Mix(colour, White, 0.4) : light < 0.3 ? Mix(colour, ShellDeep, 0.55) : light < 0.42 ? Mix(colour, ShellShade, 0.5) : colour;
    }

    /// <summary>The silhouette of an egg: narrower at the top, rounder at the bottom.</summary>
    private static bool[,] Shape()
    {
        var mask = new bool[EggCellsWide, EggCellsHigh];

        for (var y = 0; y < EggCellsHigh; y++)
        {
            var v = (((y + 0.5) / EggCellsHigh) * 2) - 1;
            var half = EggCellsWide / 2.0 * Math.Sqrt(Math.Max(0, 1 - (v * v))) * (1 + (0.16 * v));

            for (var x = 0; x < EggCellsWide; x++)
            {
                mask[x, y] = Math.Abs((x + 0.5) - (EggCellsWide / 2.0)) <= half;
            }
        }

        return mask;
    }

    // ===================================================================================================== EFFECTS

    /// <summary>
    /// The rain: shooting stars with a long trail that come in from the upper right and land, one after another, on the egg that is
    /// being built, each on the block it lights; the later, the denser. A spark goes off where each one lands.
    /// </summary>
    private void Stars(in EggLayout layout, int eggs, Color type, uint seed, double t)
    {
        var rainEnd = NurseryTimeline.Lead + NurseryTimeline.Rain;
        var count = 100 + (Math.Min(eggs, 6) * 30);
        const double fall = 0.5;
        var span = rainEnd - NurseryTimeline.Lead - 0.45;

        for (var s = 0; s < count; s++)
        {
            var depart = NurseryTimeline.Lead + 0.1 + (Math.Pow(s / (double)count, 1.7) * span);
            var u = (t - depart) / fall;

            if (u is < 0 or > 1.5)
            {
                continue;
            }

            // Cae sobre el bloque que le toca de ese huevo: el orden de las llegadas es el orden en que se encienden.
            var egg = s % eggs;
            var cell = _order[Math.Min(_order.Length - 1, (int)(s / (double)count * _order.Length))];
            var (tx, ty) = CellAt(layout, egg, cell.X, cell.Y);

            // Las que no llegan al huevo (una de cada cinco) acaban en el charco de luz del suelo.
            if (s % 5 == 4)
            {
                var (fx, _) = Slot(layout, eggs, egg);
                tx = fx + (((Hash(s, 3) % 200) - 100) / 100.0 * EggCellsWide * layout.K * 0.55);
                ty = FloorOf(layout, egg) - (layout.K * 0.4);
            }

            var reach = Height * 0.9;

            if (u <= 1)
            {
                var x = tx - (SlopeX * reach * (1 - u));
                var y = ty - (SlopeY * reach * (1 - u));
                DrawStar(x, y, type, Hash(s, 9) % 3 == 0);
            }
            else
            {
                Spark(tx, ty, (u - 1) / 0.5, type, s);
            }
        }

        // Pasada la lluvia siguen cruzando de vez en cuando, despacio, por el fondo.
        if (t >= rainEnd)
        {
            for (var s = 0; s < 4; s++)
            {
                var phase = ((t - rainEnd) * 0.35 + (s * 0.27)) % 1;
                var x = Width * (1.1 - (phase * 1.3) - (s * 0.11));
                var y = Height * (-0.1 + (phase * 1.0) + ((s * 37) % 20) / 100.0);
                DrawStar(x, y, type, s % 2 == 0);
            }
        }
    }

    /// <summary>A shooting star: a white head and a trail that goes from white to the type's colour to nothing.</summary>
    private void DrawStar(double x, double y, Color type, bool big)
    {
        for (var n = 0; n < 20; n++)
        {
            var colour = n < 2 ? White : n < 5 ? Mix(type, White, 0.65) : n < 11 ? type : Mix(type, Ink, 0.35 + (n - 11) * 0.07);
            var tx = x - (SlopeX * n * Cell * 1.4);
            var ty = y - (SlopeY * n * Cell * 1.4);

            Block(tx, ty, colour);
            if (n < (big ? 14 : 9)) Block(tx + Cell, ty, colour);
            if (big && n < 5) Block(tx - Cell, ty, colour);
        }

        if (big)
        {
            Block(x, y - Cell, White);
            Block(x - Cell, y, White);
        }
    }

    /// <summary>The spark where a star landed: four blocks out and fading; <paramref name="p"/> is 0 to 1.</summary>
    private void Spark(double x, double y, double p, Color type, int s)
    {
        if (p >= 1)
        {
            return;
        }

        var reach = Cell * (1 + (p * 4));
        var colour = p < 0.4 ? White : p < 0.7 ? Mix(type, White, 0.5) : Mix(type, Ink, (p - 0.7) / 0.3);

        Block(x + reach, y, colour);
        Block(x - reach, y, colour);
        Block(x, y + reach, colour);
        Block(x, y - reach, colour);

        if (p < 0.5 && s % 2 == 0)
        {
            Block(x + reach, y - reach, colour);
            Block(x - reach, y + reach, colour);
        }
    }

    /// <summary>Two waves leaving the egg when the pulse comes: a ring on the floor and a circle round the egg itself.</summary>
    private void Shockwaves(in EggLayout layout, Color type, double pulse)
    {
        var (cx, midY) = Slot(layout, _total, 0);
        var floor = FloorOf(layout, 0);

        foreach (var (delay, speed) in new[] { (0.0, 1.0), (0.2, 0.8) })
        {
            var s = pulse - delay;
            if (s is < 0 or > 1.2) continue;

            var p = s / 1.2;
            var r = (Width * 0.04) + (p * Width * 0.52 * speed);
            var colour = p < 0.3 ? White : Mix(White, type, Math.Min(1, (p - 0.3) / 0.5));
            colour = p > 0.7 ? Mix(colour, Ink, (p - 0.7) / 0.3) : colour;

            Ring(cx, floor - (layout.K * 0.4), r, r * 0.22, 1, 0, colour);
            Ring(cx, midY, r * 0.55, r * 0.55, 1, 0.3, colour);
        }
    }

    private void Ring(double cx, double cy, double rx, double ry, double drawn, double spin, Color colour)
    {
        var steps = (int)Math.Max(60, rx * 2 * Math.PI / Cell);
        var upto = (int)(steps * drawn);

        for (var i = 0; i < upto; i++)
        {
            var angle = (i / (double)steps * Math.PI * 2) + spin;
            Block(cx + (Math.Cos(angle) * rx), cy + (Math.Sin(angle) * ry), colour);
        }
    }

    /// <summary>Stardust flying out of the egg with the pulse and falling again: the type's colour, white and gold.</summary>
    private void Confetti(in EggLayout layout, Color type, uint seed, double pulse)
    {
        if (pulse > 2.2)
        {
            return;
        }

        var (cx, midY) = Slot(layout, _total, 0);
        var random = new Random(unchecked((int)seed) ^ 0x51ED);
        var colours = new[] { type, White, Gold, Mix(type, White, 0.5) };

        for (var i = 0; i < 60; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var speed = (0.2 + (random.NextDouble() * 0.6)) * Height * 0.8;
            var delay = random.NextDouble() * 0.12;
            var s = pulse - delay;
            if (s < 0) continue;

            var x = cx + (Math.Cos(angle) * speed * s * 1.2);
            var y = midY + (Math.Sin(angle) * speed * s) - (Height * 0.06 * s) + (Height * 0.4 * s * s);
            var colour = colours[i % colours.Length];

            Block(x, y, s > 1.4 ? Mix(colour, Ink, (s - 1.4) / 0.8) : colour);
        }
    }

    /// <summary>A golden halo hovering over the egg after the pulse.</summary>
    private void Halo(in EggLayout layout, int eggs, double since)
    {
        var fade = Ease(since / 0.6);

        for (var e = 0; e < eggs; e++)
        {
            var (cx, midY) = Slot(layout, eggs, e);
            var floating = layout.K * (3.6 + (Math.Sin((since * 2.1) + e) * 0.4));
            var top = midY - (EggCellsHigh * layout.K * 0.5) - floating - (layout.K * 1.1);
            var rx = EggCellsWide * layout.K * 0.5 * fade;

            Ring(cx, top, rx, rx * 0.22, 1, since * 0.6, Gold);
            Ring(cx, top - Cell, rx * 0.97, rx * 0.97 * 0.22, 1, since * 0.6, Mix(Gold, White, 0.5));
        }
    }

    private void Flash(double pulse, Color type)
    {
        if (pulse is >= -0.02 and < 0.45)
        {
            var strength = pulse < 0.05 ? 0.55 : 0.55 * (1 - ((pulse - 0.05) / 0.4));
            Wash(Mix(White, type, 0.2), strength);
        }
    }

    /// <summary>Stars drifting up round the eggs, once everything is done.</summary>
    private void Sparkles(in EggLayout layout, int eggs, double since, Color type)
    {
        var count = 10 + Math.Min(24, eggs * 3);

        for (var n = 0; n < count; n++)
        {
            var life = ((since * 0.5) + (n * 0.173)) % 1;
            var (sx, sy) = Slot(layout, eggs, n % eggs);
            var x = sx + (Math.Sin((n * 2.399) + (since * 0.7)) * layout.K * 8);
            var y = sy - (life * layout.K * 15);
            var size = life < 0.3 ? 1 : 0;

            Star(x, y, size, life < 0.5 ? White : n % 3 == 0 ? Gold : type);
        }
    }

    // ===================================================================================================== HELPERS

    private static int Hash(int a, int b)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13;
            h *= 0x5BD1E995;
            h ^= h >> 15;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    private void Wash(Color colour, double strength)
    {
        var a = Math.Clamp(strength, 0, 1);
        if (a <= 0) return;

        for (var i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = (byte)(Pixels[i] + ((colour.B - Pixels[i]) * a));
            Pixels[i + 1] = (byte)(Pixels[i + 1] + ((colour.G - Pixels[i + 1]) * a));
            Pixels[i + 2] = (byte)(Pixels[i + 2] + ((colour.R - Pixels[i + 2]) * a));
        }
    }

    private void Block(double x, double y, Color colour)
    {
        // A la rejilla de bloques, para que todo encaje como pixel art.
        var gx = (int)(Math.Floor(x / Cell) * Cell);
        var gy = (int)(Math.Floor(y / Cell) * Cell);
        FillRect(gx, gy, Cell, Cell, colour);
    }

    /// <summary>A rectangle moved by the screen's shake.</summary>
    private void FillRect(int left, int top, int width, int height, Color colour) =>
        FillRaw(left + _ox, top + _oy, colour.B, colour.G, colour.R, width, height);

    private void FillRaw(int left, int top, byte b, byte g, byte r, int width, int height)
    {
        for (var y = Math.Max(0, top); y < Math.Min(Height, top + height); y++)
        {
            for (var x = Math.Max(0, left); x < Math.Min(Width, left + width); x++)
            {
                var at = ((y * Width) + x) * 4;
                Pixels[at] = b;
                Pixels[at + 1] = g;
                Pixels[at + 2] = r;
                Pixels[at + 3] = 255;
            }
        }
    }

    private void Star(double x, double y, int size, Color colour)
    {
        Block(x, y, colour);

        for (var i = 1; i <= size; i++)
        {
            Block(x + (i * Cell), y, colour);
            Block(x - (i * Cell), y, colour);
            Block(x, y + (i * Cell), colour);
            Block(x, y - (i * Cell), colour);
        }
    }

    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Rgb((byte)(a.R + ((b.R - a.R) * t)), (byte)(a.G + ((b.G - a.G) * t)), (byte)(a.B + ((b.B - a.B) * t)));
    }
}
