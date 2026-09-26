using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>
/// What the full pixel-art scenes share: a canvas of cells anchored to the floor, a font, the Poké Balls, and the
/// Pokémon coming out of one with its light.
/// </summary>
/// <remarks>
/// <para>
/// Pulled out of the capsule machine (§171) when the wonder trade got a scene of its own (§174), so the two draw the
/// same balls, the same letters and the same reveal, and a fix in one is a fix in both.
/// </para>
/// <para>
/// The rules are the ones of all the pixel art in the application (§116, §120, §169): whole cells, flat colours,
/// ordered dithering instead of gradients, no blur. A scene is designed on a fixed number of cells and anchored to the
/// bottom: a taller panel gets more wall above, a wider one more room at the sides, and nothing is squeezed.
/// </para>
/// </remarks>
public abstract class PixelScene
{
    protected static readonly Color Outline = Rgb(0x0B, 0x09, 0x10);
    protected static readonly Color Void = Rgb(0x06, 0x05, 0x0B);
    protected static readonly Color White = Rgb(0xFA, 0xF8, 0xFF);
    protected static readonly Color GoldHi = Rgb(0xFF, 0xF3, 0xC4);
    protected static readonly Color GoldLight = Rgb(0xFF, 0xDC, 0x7A);
    protected static readonly Color Gold = Rgb(0xD9, 0xA4, 0x41);
    protected static readonly Color GoldDark = Rgb(0x8E, 0x62, 0x1E);

    // La sala recreativa donde están las máquinas, y los materiales que comparten: cromo, cristal, placas y bombillas.
    protected static readonly Color WallDeep = Rgb(0x10, 0x0C, 0x1B);
    protected static readonly Color Wall = Rgb(0x17, 0x12, 0x27);
    protected static readonly Color WallPanel = Rgb(0x1C, 0x16, 0x30);
    protected static readonly Color WallLit = Rgb(0x21, 0x1A, 0x38);
    protected static readonly Color WallLitPanel = Rgb(0x27, 0x1F, 0x42);
    protected static readonly Color Baseboard = Rgb(0x0D, 0x0A, 0x16);
    protected static readonly Color BaseboardTop = Rgb(0x2E, 0x25, 0x4B);
    protected static readonly Color Neon = Rgb(0xD2, 0xAD, 0xFF);
    protected static readonly Color NeonMid = Rgb(0xB0, 0x7B, 0xF0);
    protected static readonly Color NeonGlow = Rgb(0x4A, 0x33, 0x7A);
    protected static readonly Color NeonHalo = Rgb(0x2A, 0x1E, 0x48);
    protected static readonly Color FloorA = Rgb(0x1A, 0x14, 0x28);
    protected static readonly Color FloorB = Rgb(0x21, 0x1A, 0x32);
    protected static readonly Color FloorLine = Rgb(0x10, 0x0C, 0x1A);
    protected static readonly Color FloorLitA = Rgb(0x28, 0x20, 0x3E);
    protected static readonly Color FloorLitB = Rgb(0x31, 0x28, 0x4A);
    protected static readonly Color Shadow = Rgb(0x0C, 0x09, 0x14);
    protected static readonly Color ChromeHi = Rgb(0xF2, 0xEE, 0xFA);
    protected static readonly Color ChromeLight = Rgb(0xC4, 0xBE, 0xD8);
    protected static readonly Color ChromeMid = Rgb(0x8E, 0x88, 0xA6);
    protected static readonly Color ChromeDark = Rgb(0x5C, 0x56, 0x74);
    protected static readonly Color ChromeDeep = Rgb(0x36, 0x31, 0x4A);
    protected static readonly Color GlassDeep = Rgb(0x0E, 0x12, 0x22);
    protected static readonly Color Glass = Rgb(0x15, 0x1C, 0x33);
    protected static readonly Color GlassLight = Rgb(0x22, 0x2E, 0x4E);
    protected static readonly Color GlassRim = Rgb(0x3E, 0x52, 0x7C);
    protected static readonly Color GlassShine = Rgb(0xA8, 0xC2, 0xE6);
    protected static readonly Color Plate = Rgb(0x0E, 0x0A, 0x17);
    protected static readonly Color PlateLight = Rgb(0x1C, 0x15, 0x2C);
    protected static readonly Color BulbOn = Rgb(0xFF, 0xF4, 0xCC);
    protected static readonly Color BulbWarm = Rgb(0xFF, 0xC8, 0x5A);
    protected static readonly Color BulbOff = Rgb(0x5A, 0x44, 0x2A);

    // Cada ball: cuatro tonos de su mitad de arriba (brillo, claro, base, sombra) y, si la lleva, su dibujo.
    private static readonly Color[][] BallTop =
    [
        [Rgb(0xFF, 0x9A, 0x8A), Rgb(0xF0, 0x4A, 0x3C), Rgb(0xCC, 0x2A, 0x2E), Rgb(0x8C, 0x1A, 0x24)],
        [Rgb(0x8C, 0xC0, 0xFF), Rgb(0x44, 0x86, 0xE8), Rgb(0x2A, 0x62, 0xC8), Rgb(0x1A, 0x3C, 0x88)],
        [Rgb(0x7A, 0x78, 0x88), Rgb(0x4A, 0x48, 0x58), Rgb(0x2E, 0x2C, 0x38), Rgb(0x1A, 0x18, 0x22)],
        [Rgb(0xFF, 0x8A, 0x7A), Rgb(0xE8, 0x40, 0x3C), Rgb(0xB8, 0x24, 0x28), Rgb(0x78, 0x14, 0x20)],
        [Rgb(0xF4, 0xA6, 0xF0), Rgb(0xD0, 0x60, 0xD8), Rgb(0xA8, 0x38, 0xB8), Rgb(0x6A, 0x1C, 0x80)]
    ];

    private static readonly Color[] BallWhite = [Rgb(0xFF, 0xFF, 0xFF), Rgb(0xE6, 0xE2, 0xEE), Rgb(0xB8, 0xB2, 0xC8), Rgb(0x7C, 0x76, 0x90)];
    private static readonly Color[] GreatStripe = [Rgb(0xFF, 0x7A, 0x6A), Rgb(0xE8, 0x3A, 0x34), Rgb(0xB0, 0x24, 0x28), Rgb(0x78, 0x18, 0x20)];
    private static readonly Color[] UltraYellow = [Rgb(0xFF, 0xF0, 0x8A), Rgb(0xFF, 0xD2, 0x3C), Rgb(0xD8, 0xA0, 0x20), Rgb(0x8C, 0x64, 0x10)];
    private static readonly Color[] CherishBottom = [Rgb(0xE8, 0x5A, 0x50), Rgb(0xC0, 0x34, 0x30), Rgb(0x92, 0x22, 0x22), Rgb(0x5C, 0x14, 0x1A)];
    private static readonly Color[] MasterBump = [Rgb(0xFF, 0xC0, 0xE8), Rgb(0xF8, 0x7C, 0xC8), Rgb(0xD0, 0x4E, 0xA0), Rgb(0x84, 0x24, 0x68)];
    private static readonly Color Band = Rgb(0x24, 0x20, 0x2C);
    private static readonly Color BandLight = Rgb(0x3A, 0x35, 0x44);

    /// <summary>Uppercase, five cells wide and seven tall.</summary>
    private static readonly Dictionary<char, string[]> Big = new()
    {
        ['A'] = [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
        ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
        ['F'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
        ['G'] = [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
        ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['I'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
        ['J'] = ["..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.."],
        ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['L'] = ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
        ['Ñ'] = [".###.", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
        ['O'] = [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
        ['Q'] = [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
        ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
        ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['V'] = ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['W'] = ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
        ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
        ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['Z'] = ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = [".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."],
        ['+'] = [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
        ['-'] = [".....", ".....", ".....", "#####", ".....", ".....", "....."],
        ['%'] = ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
        ['!'] = ["..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."],
        ['¡'] = ["..#..", ".....", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['.'] = [".....", ".....", ".....", ".....", ".....", ".....", "..#.."],
        ['?'] = [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
        ['×'] = [".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "....."],
        [' '] = [".....", ".....", ".....", ".....", ".....", ".....", "....."],
    };

    /// <summary>Digits and capitals three cells wide and five tall: <see cref="SmallFont"/>, shared with the album's cards.</summary>
    internal static string[]? SmallGlyph(char ch) => SmallFont.Glyph(ch);

    /// <param name="width">Columns the panel can show; never fewer than the design.</param>
    /// <param name="height">Rows the panel can show; never fewer than the design. The extra goes above.</param>
    protected PixelScene(int width, int height, int designWidth, int designRows)
    {
        Width = Math.Max(designWidth, width);
        Height = Math.Max(designRows, height);
        Ox = (Width - designWidth) / 2;
        Oy = Height - designRows;
        Back = new byte[Width * Height * 4];
        Canvas = new byte[Width * Height * 4];
        Bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
    }

    /// <summary>A bare square canvas with nothing painted behind it, for a single drawing such as an icon.</summary>
    protected PixelScene(int size)
    {
        Width = size;
        Height = size;
        Back = new byte[size * size * 4];
        Canvas = new byte[size * size * 4];
        Bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
    }

    public int Width { get; }

    public int Height { get; }

    public WriteableBitmap Bitmap { get; }

    /// <summary>The frame as BGRA, for a picture of the scene outside the screen.</summary>
    public byte[] Pixels => Canvas;

    /// <summary>What is painted once: walls, floor, anything that does not move.</summary>
    protected byte[] Back { get; }

    /// <summary>The frame being drawn.</summary>
    protected byte[] Canvas { get; }

    /// <summary>Where the design's column 0 is, once the panel is wider than the design.</summary>
    protected int Ox { get; set; }

    /// <summary>Where the design's row 0 is, once the panel is taller than the design: the extra wall goes above.</summary>
    protected int Oy { get; }

    /// <summary>A shift applied to everything drawn on the frame, for a jolt or a shake.</summary>
    protected int Dx { get; set; }

    protected int Dy { get; set; }

    protected void BeginFrame() => Buffer.BlockCopy(Back, 0, Canvas, 0, Canvas.Length);

    protected void Present() => Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), Canvas, Width * 4, 0);

    // ============================================================================================= the room

    /// <summary>
    /// The arcade room the machines stand in, painted once behind them: a panelled wall with a neon strip at the real
    /// ceiling, a spot on the machine, and a tiled floor in perspective with a pool of light under it.
    /// </summary>
    /// <param name="floorTop">Design row where the wall meets the floor.</param>
    /// <param name="vanishX">Column the floor tiles run towards.</param>
    /// <param name="lightX">Column the spot and the pool of light are centred on: where the machine stands.</param>
    protected void PaintRoom(int floorTop, int designRows, double vanishX, double lightX)
    {
        for (var y = -Oy; y < designRows; y++)
        {
            for (var x = -Ox; x < Width - Ox; x++)
            {
                var colour = y < floorTop - 4 ? WallAt(x, y, y + Oy, floorTop, lightX)
                    : y < floorTop ? (y == floorTop - 4 ? BaseboardTop : Baseboard)
                    : FloorAt(x, y, floorTop, designRows, vanishX, lightX);
                Put(Back, x, y, colour);
            }
        }
    }

    /// <summary>Wall panels, a neon strip, and the light of the spot over the machine.</summary>
    private static Color WallAt(int x, int y, int fromTop, int floorTop, double lightX)
    {
        // Tira de neón pegada al techo de verdad, no al del diseño: un panel más alto tiene más pared encima de la
        // máquina, y la tira a media pared se leía como una raya que partía la escena.
        if (fromTop is 2 or 3) return fromTop == 2 ? Neon : NeonMid;
        var fromNeon = fromTop < 2 ? 2 - fromTop : fromTop - 3;
        if (fromNeon <= 2) return NeonGlow;
        if (fromNeon <= 5 && Bayer[y & 3, x & 3] < (6 - fromNeon) * 3) return NeonHalo;

        var lit = Spot(x, y, lightX);
        var column = ((x % 36) + 36) % 36;
        var panel = column is >= 3 and <= 32 && fromTop > 12 && y < floorTop - 10;
        var seam = column == 0 || fromTop == 12;

        if (seam) return lit > 0.5 ? WallPanel : WallDeep;
        var basis = panel ? (lit > 0 ? WallLitPanel : WallPanel) : (lit > 0 ? WallLit : Wall);

        // Borde del cono tramado, para que la luz no corte en seco.
        if (lit is > 0 and < 1 && Bayer[y & 3, x & 3] >= lit * 16) basis = panel ? WallPanel : Wall;
        return basis;
    }

    /// <summary>A tiled floor in perspective, lit under the machine.</summary>
    private static Color FloorAt(int x, int y, int floorTop, int designRows, double vanishX, double lightX)
    {
        const double vanishY = 40;
        var depth = (y + 0.5 - vanishY) / (designRows - vanishY);
        var column = ((x + 0.5 - vanishX) / depth) / 26.0;
        var row = Math.Pow((y + 0.5 - floorTop) / (designRows - floorTop), 0.62) * 6;

        var lineX = Math.Abs(column - Math.Round(column)) * 26.0 * depth < 0.5;
        var lineY = Math.Abs(row - Math.Round(row)) * (designRows - floorTop) / 6.0 < 0.55 && y > floorTop;
        if (y == floorTop) return Baseboard;
        if (lineX || lineY) return FloorLine;

        var checker = ((int)Math.Floor(column) + (int)Math.Floor(row)) % 2 == 0;
        var pool = Math.Pow((x + 0.5 - lightX) / 80.0, 2) + Math.Pow((y + 0.5 - 144) / 18.0, 2);
        var lit = pool < 0.75 || (pool < 1 && Bayer[y & 3, x & 3] < (1 - pool) * 60);

        return lit ? (checker ? FloorLitA : FloorLitB) : (checker ? FloorA : FloorB);
    }

    /// <summary>How much of the spot falls on a cell of the wall: 1 inside the cone, 0 outside, a ramp at its edge.</summary>
    private static double Spot(int x, int y, double lightX)
    {
        var half = 26 + (y * 0.55);
        var off = Math.Abs(x + 0.5 - lightX) - half;
        return off < -5 ? 1 : off > 0 ? 0 : -off / 5.0;
    }

    // ============================================================================================= balls

    /// <summary>How a ball is drawn beyond where and which: flashing white, opening, squashed on a bounce, button blinking.</summary>
    protected readonly record struct BallLook(double Flash, double Open, double Squash, bool Blink)
    {
        public static BallLook Plain => new(0, 0, 1, false);
    }

    /// <summary>
    /// A Poké Ball at any angle, computed cell by cell so a tilted ball is still clean pixels.
    /// </summary>
    /// <param name="clip">Where the ball may be drawn, when something in front hides part of it: a glass, a tube.</param>
    /// <param name="tint">A colour everything takes a little of, for a ball seen through glass.</param>
    protected void DrawBall(double cx, double cy, double r, double angle, CapsuleBall type, BallLook look,
        Func<int, int, bool>? clip = null, Color? tint = null)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var ry = r * look.Squash;
        var rx = r / Math.Sqrt(Math.Max(0.5, look.Squash));
        var lift = look.Open * r * 1.1;
        var outline = 1.0 / r;

        var x0 = (int)Math.Floor(cx - rx - 1);
        var x1 = (int)Math.Ceiling(cx + rx + 1);
        var y0 = (int)Math.Floor(cy - ry - lift - 1);
        var y1 = (int)Math.Ceiling(cy + ry + 1);

        // Dos pasadas cuando se abre: la tapa (atrás, levantada) y luego el cuerpo con el hueco encendido.
        for (var pass = look.Open > 0 ? 0 : 1; pass < 2; pass++)
        {
            var oy = pass == 0 ? -lift : 0;

            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (clip is not null && !clip(x, y)) continue;

                    var dx = (x + 0.5 - cx) / rx;
                    var dy = (y + 0.5 - cy - oy) / ry;
                    var d = Math.Sqrt((dx * dx) + (dy * dy));
                    if (d > 1) continue;

                    var u = (dx * cos) + (dy * sin);
                    var v = (-dx * sin) + (dy * cos);

                    if (look.Open > 0)
                    {
                        if (pass == 0 && v > -0.02) continue;
                        if (pass == 1 && v < -0.02)
                        {
                            // El hueco de la ball abierta: luz.
                            if (v > -0.3 && Math.Abs(u) < 0.92) Put(Canvas, x, y, v > -0.15 ? White : GoldHi);
                            continue;
                        }
                    }

                    var colour = BallPixel(type, u, v, dx, dy, d, outline, x, y, look);

                    if (tint is { } glass && colour != Outline) colour = Lerp(colour, glass, 0.22);
                    if (look.Flash > 0 && Bayer[(y + Oy) & 3, (x + Ox) & 3] < look.Flash * 17 && colour != Outline) colour = White;
                    Put(Canvas, x, y, colour);
                }
            }
        }
    }

    private static Color BallPixel(CapsuleBall type, double u, double v, double sx, double sy, double d, double outline, int x, int y, BallLook look)
    {
        if (d > 1 - outline) return Outline;

        var jitter = ((Bayer[y & 3, x & 3] / 16.0) - 0.47) * 0.3;
        var light = -((sx * 0.55) + (sy * 0.75)) + 0.12 + jitter;
        if (d > 0.82) light -= 0.35;
        var shade = light > 0.62 ? 0 : light > 0.12 ? 1 : light > -0.4 ? 2 : 3;

        // Destello duro arriba a la izquierda.
        var spec = ((sx + 0.42) * (sx + 0.42)) + ((sy + 0.48) * (sy + 0.48));
        if (spec < (outline > 0.15 ? 0.03 : 0.012)) return White;

        // Botón y banda.
        var rb = Math.Sqrt((u * u) + (v * v));
        var ring = outline > 0.15 ? 0.36 : 0.29;
        var face = outline > 0.15 ? 0.2 : 0.18;

        if (rb < face)
        {
            if (look.Blink) return type == CapsuleBall.Cherish ? White : Rgb(0xFF, 0x5A, 0x4A);
            return (u + v) < -0.08 ? White : BallWhite[1];
        }

        if (rb < ring) return rb < ring - 0.06 && (u + v) < 0 ? BandLight : Band;
        if (Math.Abs(v) < 0.1) return (u < -0.3 && v < 0) ? BandLight : Band;

        if (v > 0)
        {
            return type == CapsuleBall.Cherish ? CherishBottom[shade] : BallWhite[shade];
        }

        var au = Math.Abs(u);

        switch (type)
        {
            case CapsuleBall.Great when au is > 0.38 and < 0.8 && v is > -0.74 and < -0.2:
                return GreatStripe[shade];

            case CapsuleBall.Ultra when au > 0.34 && v < -0.14:
                return UltraYellow[shade];

            case CapsuleBall.Cherish when ((au - 0.42) * (au - 0.42)) + ((v + 0.5) * (v + 0.5)) < 0.018:
                return CherishBottom[0];

            case CapsuleBall.Master:
            {
                var bump = ((au - 0.5) * (au - 0.5)) + ((v + 0.42) * (v + 0.42));
                if (bump < 0.042) return MasterBump[Math.Min(3, shade + (bump > 0.03 ? 1 : 0))];

                if (outline < 0.15)
                {
                    // La M, en una rejilla de cinco por cuatro sobre la mitad de arriba.
                    var gx = (int)Math.Floor((u + 0.26) / 0.52 * 5);
                    var gy = (int)Math.Floor((v + 0.66) / 0.36 * 4);
                    string[] m = ["#...#", "##.##", "#.#.#", "#...#"];
                    if (gx is >= 0 and < 5 && gy is >= 0 and < 4 && m[gy][gx] == '#') return White;
                }

                break;
            }
        }

        return BallTop[(int)type][shade];
    }

    // ============================================================================================= the reveal

    /// <summary>
    /// A Pokémon standing on its feet: a white silhouette that turns into its colours through the dither, cell by
    /// cell — the way the games bring one out of its ball.
    /// </summary>
    /// <param name="colour">0 is all silhouette, 1 all colour.</param>
    protected void DrawPokemon(RoomSprite sprite, double footX, double footY, int scale, double colour, Color? silhouette = null)
    {
        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var width = (maxX - minX + 1) * scale;
        var height = (maxY - minY + 1) * scale;
        var left = (int)Math.Round(footX - (width / 2.0));
        var top = (int)Math.Round(footY - height);
        var glow = silhouette ?? White;

        for (var sy = minY; sy <= maxY; sy++)
        {
            for (var sx = minX; sx <= maxX; sx++)
            {
                if (!sprite.Solid(sx, sy)) continue;

                for (var k = 0; k < scale * scale; k++)
                {
                    var x = left + ((sx - minX) * scale) + (k % scale);
                    var y = top + ((sy - minY) * scale) + (k / scale);
                    var painted = Bayer[(y + Oy) & 3, (x + Ox) & 3] < colour * 16;
                    Put(Canvas, x, y, painted ? sprite.At(sx, sy) : glow);
                }
            }
        }
    }

    /// <summary>
    /// A sprite at any size, centred on a point and sampled cell by cell, so a Pokémon shrinking into its ball stays whole
    /// cells at every step instead of going soft.
    /// </summary>
    /// <param name="flat">One colour for every solid cell, for a silhouette.</param>
    protected void DrawSpriteScaled(RoomSprite sprite, double cx, double cy, double scale, Color? flat = null)
    {
        if (scale <= 0) return;

        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var width = (maxX - minX + 1) * scale;
        var height = (maxY - minY + 1) * scale;
        var left = cx - (width / 2);
        var top = cy - (height / 2);

        for (var y = (int)Math.Floor(top); y < (int)Math.Ceiling(top + height); y++)
        {
            for (var x = (int)Math.Floor(left); x < (int)Math.Ceiling(left + width); x++)
            {
                var sx = minX + (int)Math.Floor((x + 0.5 - left) / scale);
                var sy = minY + (int)Math.Floor((y + 0.5 - top) / scale);
                if (sx < minX || sy < minY || sx > maxX || sy > maxY || !sprite.Solid(sx, sy)) continue;
                Put(Canvas, x, y, flat ?? sprite.At(sx, sy));
            }
        }
    }

    /// <summary>The column of light a ball shoots up when it opens.</summary>
    /// <param name="s">Seconds since it opened; it grows, then thins out and is gone before a second.</param>
    protected void DrawBeam(double x, double y, double s, Color colour)
    {
        if (s is < 0 or > 0.9) return;

        var width = s < 0.25 ? s / 0.25 * 9 : 9 * (1 - ((s - 0.25) / 0.65));
        var bottom = (int)Math.Round(y);
        var light = Lerp(colour, White, 0.55);

        for (var yy = -Oy; yy < bottom; yy++)
        {
            for (var xx = (int)Math.Floor(x - width - 2); xx <= (int)Math.Ceiling(x + width + 2); xx++)
            {
                var off = Math.Abs(xx + 0.5 - x);
                if (off < width * 0.35) Put(Canvas, xx, yy, White);
                else if (off < width * 0.7) Put(Canvas, xx, yy, light);
                else if (off < width) Put(Canvas, xx, yy, colour);
                else if (off < width + 2 && Bayer[yy & 3, xx & 3] < 6) Put(Canvas, xx, yy, colour);
            }
        }
    }

    /// <summary>
    /// Rays turning slowly behind a Pokémon that has just come out, and a ring opening from it.
    /// </summary>
    /// <param name="s">Seconds since the rays started.</param>
    /// <param name="strength">0 to 4: how far they reach and how many there are — more for something rarer.</param>
    /// <param name="legendary">Gold rays between the others.</param>
    protected void DrawRays(double cx, double cy, double s, Color colour, int strength, bool legendary)
    {
        if (s < 0) return;

        var grow = Math.Clamp(s / 0.4, 0, 1);
        var reach = (40 + (strength * 12)) * grow;
        var count = 8 + (strength * 2);
        var turn = s * 0.35;
        var faint = Lerp(colour, Void, 0.45);

        for (var y = (int)(cy - reach); y <= (int)(cy + reach); y++)
        {
            for (var x = (int)(cx - reach); x <= (int)(cx + reach); x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > reach || r < 6) continue;

                var a = ((Math.Atan2(dy, dx) / (Math.PI * 2)) + turn) * count;
                var frac = a - Math.Floor(a);
                var fade = r / reach;

                if (frac < 0.22 && Bayer[y & 3, x & 3] >= fade * 16) Put(Canvas, x, y, fade < 0.5 ? colour : faint);
                else if (legendary && frac is > 0.5 and < 0.6 && Bayer[y & 3, x & 3] >= fade * 18) Put(Canvas, x, y, Gold);
            }
        }

        var ring = s - 0.25;
        if (ring is >= 0 and < 0.6)
        {
            var radius = 8 + (ring / 0.6 * 80);
            Circle(cx, cy, radius, ring < 0.3 ? White : Lerp(colour, White, 0.4), ring > 0.3);
        }
    }

    /// <summary>A ring of four-point sparks opening out from a ball, for the moment it changes into a better one.</summary>
    protected void DrawSparkRing(double x, double y, double r, double since, Color colour)
    {
        var radius = r + 2 + (since * 34);
        for (var i = 0; i < 12; i++)
        {
            var a = (i / 12.0 * Math.PI * 2) + 0.2;
            var px = x + (Math.Cos(a) * radius);
            var py = y + (Math.Sin(a) * radius * 0.85);
            Star(px, py, since < 0.2 ? 2 : 1, since < 0.25 ? White : Lerp(colour, White, 0.4), colour);
        }
    }

    /// <summary>Sparks thrown out when a Pokémon comes out, falling as they fade.</summary>
    protected void DrawParticles(double x, double y, double s, Color colour, int seed, bool legendary)
    {
        if (s is < 0 or > 1.6) return;

        var random = new Random(seed);
        var count = legendary ? 34 : 22;
        for (var i = 0; i < count; i++)
        {
            var a = random.NextDouble() * Math.PI * 2;
            var speed = 30 + (random.NextDouble() * 50);
            var life = 0.8 + (random.NextDouble() * 0.8);
            if (s > life) continue;

            var px = x + (Math.Cos(a) * speed * s);
            var py = y + (Math.Sin(a) * speed * s * 0.8) + (28 * s * s);
            var twinkle = ((int)((s * 20) + i) % 3) != 0;
            if (!twinkle) continue;

            var c = i % 3 == 0 ? White : i % 3 == 1 ? Lerp(colour, White, 0.4) : (legendary ? GoldLight : colour);
            if (s < life * 0.4) Star(px, py, 1, c, colour);
            else Put(Canvas, (int)Math.Floor(px), (int)Math.Floor(py), c);
        }
    }

    /// <summary>The four-point stars a shiny comes out with, a few at a time around it, over and over.</summary>
    protected void DrawShinySparkles(double x, double y, double s, int seed)
    {
        if (s < 0) return;

        var round = (int)(s / 1.8);
        var within = s - (round * 1.8);
        var random = new Random(seed ^ (round * 7919));

        for (var i = 0; i < 6; i++)
        {
            var px = x + ((random.NextDouble() - 0.5) * 80);
            var py = y + ((random.NextDouble() - 0.5) * 60);
            var at = i * 0.12;
            var local = within - at;
            if (local is < 0 or > 0.45) continue;

            var size = local < 0.15 ? 1 : local < 0.3 ? 3 : 2;
            Star(px, py, size, White, GoldLight);
        }
    }

    /// <summary>A small hop every so often, like the party menu, once a Pokémon is out.</summary>
    protected static double HopAt(double s)
    {
        if (s < 0) return 0;
        var p = s % 1.6;
        return p < 0.3 ? Math.Round(Math.Sin(p / 0.3 * Math.PI) * 3) : 0;
    }

    // ============================================================================================= helpers

    /// <summary>0 outside the window, rising to 1 in its middle and back: how hard something shakes.</summary>
    protected static double Envelope(double t, double from, double to)
    {
        if (t <= from || t >= to) return 0;
        var p = (t - from) / (to - from);
        return Math.Sin(p * Math.PI);
    }

    /// <summary>Darkens the whole frame drawn so far, for the moment the light comes from the ball.</summary>
    protected void Darken(double amount)
    {
        var keep = 1 - amount;
        for (var i = 0; i < Canvas.Length; i += 4)
        {
            Canvas[i] = (byte)(Canvas[i] * keep);
            Canvas[i + 1] = (byte)(Canvas[i + 1] * keep);
            Canvas[i + 2] = (byte)(Canvas[i + 2] * keep);
        }
    }

    protected void Star(double cx, double cy, int size, Color core, Color arms)
    {
        var x = (int)Math.Floor(cx);
        var y = (int)Math.Floor(cy);
        Put(Canvas, x, y, core);
        for (var i = 1; i <= size; i++)
        {
            var c = i == size ? arms : core;
            Put(Canvas, x + i, y, c);
            Put(Canvas, x - i, y, c);
            Put(Canvas, x, y + i, c);
            Put(Canvas, x, y - i, c);
        }
    }

    protected void Circle(double cx, double cy, double radius, Color colour, bool dither)
    {
        var steps = (int)(radius * 7);
        for (var i = 0; i < steps; i++)
        {
            var a = i / (double)steps * Math.PI * 2;
            var x = (int)Math.Floor(cx + (Math.Cos(a) * radius));
            var y = (int)Math.Floor(cy + (Math.Sin(a) * radius * 0.85));
            if (!dither || (x + y) % 2 == 0) Put(Canvas, x, y, colour);
        }
    }

    protected void Ellipse(double cx, double cy, double rx, double ry, Color colour, bool dither)
    {
        for (var y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
        {
            for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
            {
                var d = Math.Pow((x + 0.5 - cx) / rx, 2) + Math.Pow((y + 0.5 - cy) / ry, 2);
                if (d <= 1 && (!dither || (x + y) % 2 == 0)) Put(Canvas, x, y, colour);
            }
        }
    }

    protected void Disc(double cx, double cy, double radius, Func<double, double, double, Color> paint)
    {
        for (var y = (int)Math.Floor(cy - radius); y <= (int)Math.Ceiling(cy + radius); y++)
        {
            for (var x = (int)Math.Floor(cx - radius); x <= (int)Math.Ceiling(cx + radius); x++)
            {
                var nx = (x + 0.5 - cx) / radius;
                var ny = (y + 0.5 - cy) / radius;
                var d = Math.Sqrt((nx * nx) + (ny * ny));
                if (d <= 1) Put(Canvas, x, y, paint(nx, ny, d));
            }
        }
    }

    protected static (int MinX, int MinY, int MaxX, int MaxY) Bounds(RoomSprite sprite)
    {
        int minX = sprite.Width, minY = sprite.Height, maxX = -1, maxY = -1;
        for (var y = 0; y < sprite.Height; y++)
        {
            for (var x = 0; x < sprite.Width; x++)
            {
                if (!sprite.Solid(x, y)) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < 0 ? (0, 0, 0, 0) : (minX, minY, maxX, maxY);
    }

    protected static int StableHash(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in text) hash = (hash * 31) + ch;
            return hash;
        }
    }

    /// <summary>Cells a line of the big font takes.</summary>
    protected static int BigWidth(string text, int scale = 1) => text.Length == 0 ? 0 : ((text.Length * 6) - 1) * scale;

    /// <param name="scale">Cells per font cell: 2 draws every letter twice as big.</param>
    protected void BigText(string text, int left, int top, Color colour, int scale = 1)
    {
        var x = left;
        foreach (var raw in text)
        {
            // Las vocales con tilde son la letra sin ella y la tilde encima, separada una celda para que no se funda.
            var ch = raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', '−' => '-', _ => raw };
            if (ch != raw && ch != '-')
            {
                Rect(x + (3 * scale), top - (3 * scale), scale, scale, colour);
                Rect(x + (2 * scale), top - (2 * scale), scale, scale, colour);
            }

            if (Big.TryGetValue(ch, out var glyph))
            {
                for (var gy = 0; gy < 7; gy++)
                {
                    for (var gx = 0; gx < 5; gx++)
                    {
                        if (glyph[gy][gx] == '#') Rect(x + (gx * scale), top + (gy * scale), scale, scale, colour);
                    }
                }
            }

            x += 6 * scale;
        }
    }

    /// <summary>Cells a line of the small font takes.</summary>
    protected static int SmallWidth(string text) => text.Length == 0 ? 0 : (text.Length * 4) - 1;

    /// <remarks>
    /// Accented vowels are the plain letter with one cell above it, two rows up so it does not touch; the Ñ gets a
    /// bar. The typographic minus is the hyphen: the cartridge's figures use «−» and a glyph for each would be two
    /// drawings of the same stroke.
    /// </remarks>
    protected void SmallText(string text, int left, int top, Color colour)
    {
        var x = left;
        foreach (var raw in text)
        {
            var ch = raw switch { 'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', '−' => '-', _ => raw };
            if (ch != raw && ch != '-') Put(Canvas, x + 1, top - 2, colour);
            if (raw == 'Ñ') Rect(x, top - 2, 3, 1, colour);

            if (SmallFont.Glyph(ch) is { } glyph)
            {
                for (var gy = 0; gy < 5; gy++)
                {
                    for (var gx = 0; gx < 3; gx++)
                    {
                        if (glyph[gy][gx] == '#') Put(Canvas, x + gx, top + gy, colour);
                    }
                }
            }

            x += 4;
        }
    }

    /// <summary>A bevelled plate edge: light on top and left, dark on the other two, outlined.</summary>
    protected void Frame(int x, int y, int width, int height, Color light, Color dark)
    {
        Rect(x, y, width, 1, light);
        Rect(x, y + height - 1, width, 1, dark);
        Rect(x, y, 1, height, light);
        Rect(x + width - 1, y, 1, height, dark);
        Border(x - 1, y - 1, width + 2, height + 2, Outline);
    }

    protected void Border(int x, int y, int width, int height, Color colour)
    {
        Rect(x, y, width, 1, colour);
        Rect(x, y + height - 1, width, 1, colour);
        Rect(x, y, 1, height, colour);
        Rect(x + width - 1, y, 1, height, colour);
    }

    protected void Rect(int x, int y, int width, int height, Color colour)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            for (var xx = x; xx < x + width; xx++) Put(Canvas, xx, yy, colour);
        }
    }

    /// <summary>What is already painted in a cell, in design coordinates: for glass that lets the wall show through.</summary>
    protected Color At(byte[] source, int x, int y)
    {
        x += Ox + (ReferenceEquals(source, Canvas) ? Dx : 0);
        y += Oy + (ReferenceEquals(source, Canvas) ? Dy : 0);
        if (x < 0 || y < 0 || x >= Width || y >= Height) return Void;

        var at = ((y * Width) + x) * 4;
        return Color.FromRgb(source[at + 2], source[at + 1], source[at]);
    }

    /// <summary>Paints one cell, in design coordinates; on the frame, with the current jolt.</summary>
    protected void Put(byte[] target, int x, int y, Color colour)
    {
        x += Ox + (ReferenceEquals(target, Canvas) ? Dx : 0);
        y += Oy + (ReferenceEquals(target, Canvas) ? Dy : 0);
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        var at = ((y * Width) + x) * 4;
        target[at] = colour.B;
        target[at + 1] = colour.G;
        target[at + 2] = colour.R;
        target[at + 3] = 255;
    }
}

/// <summary>The five balls, one per gacha tier, cheapest first.</summary>
public enum CapsuleBall
{
    Poke,
    Great,
    Ultra,
    Cherish,
    Master,
}
