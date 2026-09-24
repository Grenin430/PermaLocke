using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The cemetery, painted cell by cell: night, hills, a fence, fog, and one grave per fallen Pokémon with
/// its ghost above it.
/// </summary>
/// <remarks>
/// <para>
/// Pixels for the same reason as the blood (§113): the only real thing in it is each Pokémon's sprite
/// from the cartridge, and smooth vector graves around a pixel sprite read as a stock scene. Everything
/// here is drawn at the sprites' own scale, in the application's violet-tinted darks, with ordered
/// dithering between tones instead of gradients.
/// </para>
/// <para>
/// The size of the grid follows the panel, so that every cell is a whole number of screen pixels: a
/// pixel scene stretched by 1.85 draws some cells two pixels wide and others one, and the dithering
/// turns to moiré. The graves keep their size; a bigger panel gets more sky and more room around them.
/// </para>
/// <para>
/// The monument says how long the Pokémon lasted in the run — a wooden cross for one that fell the day
/// it came, up to an obelisk — which is read from the run and not invented. So is the turned earth of a
/// fall from the last day and the glint of a variocolor. Everything else that moves (fog, wisps, stars,
/// the ghosts' float) is decoration with no meaning. Presentation only: the
/// background is painted once and each frame draws on a copy of it.
/// </para>
/// </remarks>
internal sealed class CemeteryScene
{
    /// <summary>The narrowest the grid gets: ten graves of the narrowest slot.</summary>
    public const int MinWidth = 400;

    /// <summary>The shortest the scene gets; taller panels get more sky, never bigger graves.</summary>
    public const int MinHeight = 270;
    public const int MaxHeight = 700;

    /// <summary>Graves per row and rows per page.</summary>
    public const int PerRow = 10;
    public const int Rows = 3;
    public const int PerPage = PerRow * Rows;

    /// <summary>How far the picked ghost rises over its grave.</summary>
    private const int Lift = 9;

    private static readonly Color Outline = Color.FromRgb(0x08, 0x07, 0x0C);
    private static readonly Color HoverOutline = Color.FromRgb(0x8E, 0x82, 0xB4);
    private static readonly Color StoneDark = Color.FromRgb(0x33, 0x2E, 0x40);
    private static readonly Color StoneMid = Color.FromRgb(0x50, 0x49, 0x62);
    private static readonly Color StoneLight = Color.FromRgb(0x73, 0x6B, 0x88);
    private static readonly Color MarbleDark = Color.FromRgb(0x4A, 0x45, 0x5C);
    private static readonly Color MarbleMid = Color.FromRgb(0x74, 0x6E, 0x8A);
    private static readonly Color MarbleLight = Color.FromRgb(0x9E, 0x97, 0xB4);
    private static readonly Color WoodDark = Color.FromRgb(0x2A, 0x1E, 0x1E);
    private static readonly Color WoodMid = Color.FromRgb(0x4B, 0x36, 0x30);
    private static readonly Color WoodLight = Color.FromRgb(0x68, 0x4C, 0x40);
    private static readonly Color Earth = Color.FromRgb(0x1A, 0x16, 0x20);
    private static readonly Color EarthLight = Color.FromRgb(0x25, 0x20, 0x2C);
    private static readonly Color FreshEarth = Color.FromRgb(0x38, 0x28, 0x24);
    private static readonly Color FreshClod = Color.FromRgb(0x52, 0x3C, 0x32);
    private static readonly Color Grass = Color.FromRgb(0x1C, 0x2A, 0x24);
    private static readonly Color GrassLight = Color.FromRgb(0x2A, 0x3C, 0x32);
    private static readonly Color Flame = Color.FromRgb(0xF2, 0xA6, 0x3B);
    private static readonly Color FlameCore = Color.FromRgb(0xFF, 0xE9, 0xA8);
    private static readonly Color Glow = Color.FromRgb(0x3A, 0x2A, 0x3E);
    private static readonly Color Sparkle = Color.FromRgb(0xFF, 0xF1, 0xB0);
    private static readonly Color Wisp = Color.FromRgb(0xC4, 0xBC, 0xFF);
    private static readonly Color Haze = Color.FromRgb(0x1F, 0x18, 0x2E);

    private static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 }
    };

    private readonly byte[] _background;
    private readonly byte[] _frame;
    private readonly List<(int X, int Y, double Phase)> _twinkles = [];

    public CemeteryScene(int width, int height)
    {
        Width = Math.Max(MinWidth, width);
        Height = Math.Clamp(height, MinHeight, MaxHeight);
        SlotWidth = Math.Clamp((Width - 12) / PerRow, 40, 48);
        FirstSlotX = (Width - (SlotWidth * PerRow)) / 2;
        RowGap = Math.Clamp((Height - 150) / Rows, 70, 82);

        _background = new byte[Width * Height * 4];
        _frame = new byte[Width * Height * 4];
        Bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        PaintBackground();
    }

    public int Width { get; }

    public int Height { get; }

    private int SlotWidth { get; }

    private int FirstSlotX { get; }

    /// <summary>Distance between rows: room for a ghost, a monument and a name without touching the row in front.</summary>
    private int RowGap { get; }

    public WriteableBitmap Bitmap { get; }

    /// <summary>How big a Pokémon's monument is, from how long it lasted.</summary>
    public enum Monument
    {
        Cross,
        Stone,
        Headstone,
        Obelisk
    }

    /// <summary>A grave as the scene draws it.</summary>
    /// <param name="Rise">How far into rising its ghost is, from 0 at rest to 1 picked.</param>
    /// <param name="Fresh">Fallen recently: the earth is still turned.</param>
    public sealed record Plot(Monument Monument, int Variant, byte[]? Ghost, byte[]? Colour, int GhostWidth, int GhostHeight,
        double Phase, bool Selected, bool Hovered, double Rise, bool Shiny, bool Fresh);

    /// <summary>Where a row's ground line is: the rows sit at the bottom and the rest is sky.</summary>
    private int RowGround(int row) => Height - 16 - ((Rows - 1 - row) * RowGap);

    /// <summary>The back row's ground: hills, fence and the start of the earth hang from it.</summary>
    private int Horizon => RowGround(0);

    /// <summary>The rectangle a slot takes on the grid, for names and clicks.</summary>
    public Int32Rect SlotRect(int index)
    {
        var row = index / PerRow;
        var column = index % PerRow;
        var ground = RowGround(row);

        return new Int32Rect(FirstSlotX + (column * SlotWidth), ground - 62, SlotWidth, 68);
    }

    /// <summary>Where a slot's name goes: centred under its monument.</summary>
    public Point NameAnchor(int index)
    {
        var rect = SlotRect(index);
        return new Point(rect.X + (rect.Width / 2.0), RowGround(index / PerRow) + 1);
    }

    /// <summary>The width a name gets, so that two neighbours never touch.</summary>
    public double NameWidth => SlotWidth - 2;

    public void Render(IReadOnlyList<Plot> plots, double seconds)
    {
        Buffer.BlockCopy(_background, 0, _frame, 0, _frame.Length);

        Twinkle(seconds);
        Fog(seconds, Horizon - 30, 14, 9, 0.30);

        // De atrás adelante, para que las filas de delante tapen a las de detrás.
        for (var i = 0; i < plots.Count && i < PerPage; i++)
        {
            DrawGrave(i, plots[i], seconds);
        }

        Wisps(seconds);
        Fog(seconds * 1.7, Height - 34, 22, 21, 0.22);

        Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), _frame, Width * 4, 0);
    }

    private void DrawGrave(int index, Plot plot, double seconds)
    {
        var row = index / PerRow;
        var rect = SlotRect(index);
        var ground = RowGround(row);
        var centre = rect.X + (rect.Width / 2);

        // Perspectiva de aire: la fila de atrás se hunde en la bruma.
        var haze = row switch { 0 => 0.30, 1 => 0.12, _ => 0.0 };

        if (plot.Selected)
        {
            // Un resplandor de vela en el suelo, a trama.
            for (var y = ground - 14; y < ground + 5; y++)
            {
                for (var x = centre - 24; x <= centre + 24; x++)
                {
                    var d = (Math.Abs(x - centre) / 24.0) + (Math.Abs(y - ground) / 15.0);

                    if (d < 1 && Bayer[(y + 400) % 4, (x + 400) % 4] < (1 - d) * 10)
                    {
                        Put(_frame, x, y, Glow);
                    }
                }
            }
        }

        Mound(centre, ground, plot.Fresh, index, haze);

        var tall = MonumentHeight(plot.Monument);
        DrawMonument(centre, ground - 2, plot.Monument, plot.Variant, plot.Hovered || plot.Selected, haze);

        if (plot.Selected)
        {
            var flicker = (int)(seconds * 9) % 3;
            var candle = centre + 10;
            Put(_frame, candle, ground - 3, Outline);
            Put(_frame, candle, ground - 4, StoneLight);
            Put(_frame, candle, ground - 5, StoneLight);
            Put(_frame, candle, ground - 6, Flame);
            Put(_frame, candle, ground - 7 - (flicker == 0 ? 1 : 0), FlameCore);
        }

        if (plot.Shiny)
        {
            ShinySparkles(centre, ground - 2 - tall, seconds, plot.Phase);
        }

        if (plot.Ghost is { } ghost)
        {
            var bob = (int)Math.Round(Math.Sin((seconds * 1.5) + (plot.Phase * Math.PI * 2)) * 2);
            var eased = 1 - Math.Pow(1 - plot.Rise, 3);
            var left = centre - (plot.GhostWidth / 2);
            var top = ground - 2 - tall - 4 - plot.GhostHeight + bob - (int)Math.Round(eased * Lift);

            // El elegido vuelve a tener sus colores: es el que se está recordando.
            var ghostAlpha = (plot.Hovered ? 0.78 : 0.5) * (1 - plot.Rise);

            if (ghostAlpha > 0.02)
            {
                Blit(ghost, plot.GhostWidth, plot.GhostHeight, left, top, ghostAlpha * (1 - haze));
            }

            if (plot.Colour is { } colour && plot.Rise > 0.02)
            {
                Blit(colour, plot.GhostWidth, plot.GhostHeight, left, top, 0.94 * plot.Rise);
            }
        }
    }

    /// <summary>The heap of earth: turned and higher when the fall is recent, grown over otherwise.</summary>
    private void Mound(int centre, int ground, bool fresh, int seed, double haze)
    {
        for (var x = centre - 10; x <= centre + 10; x++)
        {
            var distance = Math.Abs(x - centre);
            var top = ground - (fresh ? (distance < 4 ? 3 : distance < 8 ? 2 : 1) : (distance < 6 ? 2 : 1));

            for (var y = top; y <= ground; y++)
            {
                var colour = fresh
                    ? ((x * 7) + (y * 3) + seed) % 6 == 0 ? FreshClod : FreshEarth
                    : (x + y) % 5 == 0 ? EarthLight : Earth;

                Put(_frame, x, y, Hazed(colour, haze));
            }
        }

        if (fresh)
        {
            return;
        }

        // Briznas en los bordes: lo que lleva tiempo bajo tierra ya tiene hierba encima.
        foreach (var side in new[] { -9, -6, 7, 10 })
        {
            var x = centre + side + ((seed + side) % 2);
            Put(_frame, x, ground - 1, Hazed(Grass, haze));
            Put(_frame, x, ground - 2, Hazed(GrassLight, haze));

            if ((seed + side) % 3 == 0)
            {
                Put(_frame, x + 1, ground - 1, Hazed(Grass, haze));
            }
        }
    }

    private static int MonumentHeight(Monument monument) => monument switch
    {
        Monument.Cross => 16,
        Monument.Stone => 12,
        Monument.Headstone => 18,
        _ => 29
    };

    private void DrawMonument(int centre, int bottom, Monument monument, int variant, bool lit, double haze)
    {
        var tall = MonumentHeight(monument);
        var (light, mid, dark) = monument switch
        {
            Monument.Cross => (WoodLight, WoodMid, WoodDark),
            Monument.Obelisk => (MarbleLight, MarbleMid, MarbleDark),
            _ => (StoneLight, StoneMid, StoneDark)
        };

        for (var up = 0; up <= tall; up++)
        {
            for (var dx = -9; dx <= 9; dx++)
            {
                if (!Solid(monument, variant, dx, up))
                {
                    continue;
                }

                var edge = !Solid(monument, variant, dx - 1, up) || !Solid(monument, variant, dx + 1, up)
                           || !Solid(monument, variant, dx, up + 1);

                // La luz viene de la luna, arriba a la derecha... pero la cara que se ve es la de la
                // izquierda, que es la que da al que mira. Tres tonos, sin degradado.
                var colour = edge ? (lit ? HoverOutline : Outline)
                    : dx < -1 ? light
                    : dx > 1 ? dark
                    : mid;

                Put(_frame, centre + dx, bottom - up, edge && lit ? colour : Hazed(colour, haze));
            }
        }

        // Lo grabado: una raya en la piedra, dos en la lápida, una cruz en el obelisco.
        switch (monument)
        {
            case Monument.Stone:
                for (var x = centre - 2; x <= centre + 2; x += 2)
                {
                    Put(_frame, x, bottom - 6, Hazed(StoneDark, haze));
                }

                break;

            case Monument.Headstone:
                for (var x = centre - 2; x <= centre + 2; x++)
                {
                    Put(_frame, x, bottom - 10, Hazed(StoneDark, haze));
                    Put(_frame, x - (x % 2), bottom - 7, Hazed(StoneDark, haze));
                }

                break;

            case Monument.Obelisk:
                for (var y = bottom - 19; y <= bottom - 13; y++)
                {
                    Put(_frame, centre, y, Hazed(MarbleDark, haze));
                }

                for (var x = centre - 2; x <= centre + 2; x++)
                {
                    Put(_frame, x, bottom - 17, Hazed(MarbleDark, haze));
                }

                break;
        }
    }

    /// <summary>Whether a cell belongs to the monument, measured from its centre and up from its foot.</summary>
    private static bool Solid(Monument monument, int variant, int dx, int up)
    {
        if (up < 0)
        {
            return true;
        }

        switch (monument)
        {
            case Monument.Cross:
            {
                // Las viejas se tuercen: la mitad de las cruces se inclina un píxel por arriba.
                var lean = variant == 1 && up > 8 ? 1 : 0;
                var x = dx - lean;
                return up <= 16 && ((Math.Abs(x) <= 1) || (up is >= 10 and <= 12 && Math.Abs(x) <= 5));
            }

            case Monument.Stone:
            {
                const int Tall = 12;
                var fromTop = Tall - up;
                return up <= Tall && Math.Abs(dx) <= 5
                       && (fromTop >= 3 || (dx * dx) + ((fromTop - 3) * (fromTop - 3) * 2) <= 27);
            }

            case Monument.Headstone:
            {
                const int Tall = 18;
                var fromTop = Tall - up;

                return up <= Tall && (variant == 0
                    ? Math.Abs(dx) <= 6 && (fromTop >= 3 || (dx * dx) + ((fromTop - 3) * (fromTop - 3) * 2) <= 38)
                    : Math.Abs(dx) <= 5 && fromTop >= Math.Abs(dx));
            }

            default:
            {
                // Peana ancha, fuste que se estrecha y punta.
                if (up <= 3)
                {
                    return Math.Abs(dx) <= (up <= 1 ? 7 : 6);
                }

                var half = up <= 25 ? 3 : 3 - (up - 25);
                return up <= 29 && half >= 0 && Math.Abs(dx) <= half;
            }
        }
    }

    /// <summary>A variocolor keeps a glint: two small stars that take turns around the top.</summary>
    private void ShinySparkles(int centre, int top, double seconds, double phase)
    {
        (int X, int Y)[] spots = [(-8, 2), (8, -2), (-6, -5), (7, 5)];
        var beat = (seconds * 1.6) + (phase * 4);

        for (var i = 0; i < spots.Length; i++)
        {
            var strength = Math.Sin(beat + (i * 1.7));

            if (strength < 0.35)
            {
                continue;
            }

            var x = centre + spots[i].X;
            var y = top + spots[i].Y;
            Put(_frame, x, y, Sparkle);

            if (strength > 0.75)
            {
                Mix(_frame, x - 1, y, Sparkle, 0.6);
                Mix(_frame, x + 1, y, Sparkle, 0.6);
                Mix(_frame, x, y - 1, Sparkle, 0.6);
                Mix(_frame, x, y + 1, Sparkle, 0.6);
            }
        }
    }

    /// <summary>A few pale wisps drifting over the graves, each fading in and out on its own beat.</summary>
    private void Wisps(double seconds)
    {
        const int Count = 9;
        var top = Horizon - 20;
        var span = Math.Max(1, Height - 12 - top);

        for (var i = 0; i < Count; i++)
        {
            var strength = Math.Sin((seconds * 0.55) + (i * 2.3));

            if (strength <= 0)
            {
                continue;
            }

            var baseX = ((i * 137) + 31) % Width;
            var baseY = top + (((i * 71) + 17) % span);
            var x = (int)Math.Round(baseX + (Math.Sin((seconds * 0.23) + (i * 1.1)) * 26));
            var y = (int)Math.Round(baseY + (Math.Sin((seconds * 0.41) + (i * 0.7)) * 7));

            Mix(_frame, x, y, Wisp, strength);
            Mix(_frame, x - 1, y, Wisp, 0.55 * strength);
            Mix(_frame, x + 1, y, Wisp, 0.55 * strength);
            Mix(_frame, x, y - 1, Wisp, 0.55 * strength);
            Mix(_frame, x, y + 1, Wisp, 0.55 * strength);

            // Un halo a trama alrededor, que es lo que hace que se lea como luz y no como un punto.
            for (var dy = -3; dy <= 3; dy++)
            {
                for (var dx = -3; dx <= 3; dx++)
                {
                    var d = Math.Abs(dx) + Math.Abs(dy);

                    if (d is >= 2 and <= 4 && Bayer[(y + dy) & 3, (x + dx) & 3] < (5 - d) * 3 * strength)
                    {
                        Mix(_frame, x + dx, y + dy, Wisp, 0.18);
                    }
                }
            }
        }
    }

    /// <summary>The brightest stars breathe; the rest stay painted in the background.</summary>
    private void Twinkle(double seconds)
    {
        foreach (var (x, y, phase) in _twinkles)
        {
            var strength = 0.5 + (0.5 * Math.Sin((seconds * 1.3) + phase));
            Mix(_frame, x, y, Color.FromRgb(0x06, 0x05, 0x0A), 0.85 * (1 - strength));
        }
    }

    /// <summary>A band of fog, dithered, drifting sideways.</summary>
    private void Fog(double seconds, int centreY, int thickness, int seed, double strength)
    {
        var drift = (int)(seconds * 6);

        for (var y = centreY - thickness; y <= centreY + thickness; y++)
        {
            var across = 1 - (Math.Abs(y - centreY) / (double)thickness);

            for (var x = 0; x < Width; x++)
            {
                var wave = 0.5 + (0.5 * Math.Sin(((x + drift) * 0.045) + seed) * Math.Sin(((x - drift) * 0.021) + (seed * 0.7)));
                var amount = across * wave * strength * 16;

                if (Bayer[(y + seed) & 3, (x + drift) & 3] < amount)
                {
                    Mix(_frame, x, y, Color.FromRgb(0x9A, 0x92, 0xB4), 0.22);
                }
            }
        }
    }

    private void PaintBackground()
    {
        var random = new Random(20260913);

        // Cielo: cuatro tonos a trama, sin degradados.
        Color[] sky =
        [
            Color.FromRgb(0x06, 0x05, 0x0A), Color.FromRgb(0x0C, 0x0A, 0x14),
            Color.FromRgb(0x14, 0x10, 0x20), Color.FromRgb(0x1F, 0x18, 0x2E)
        ];

        for (var y = 0; y < Height; y++)
        {
            var position = Math.Clamp(y / (double)Horizon, 0, 0.999) * (sky.Length - 1);
            var band = (int)position;
            var mix = position - band;

            for (var x = 0; x < Width; x++)
            {
                var colour = Bayer[y & 3, x & 3] < mix * 16 ? sky[Math.Min(band + 1, sky.Length - 1)] : sky[band];
                Put(_background, x, y, colour);
            }
        }

        var stars = Width * Math.Max(20, Horizon - 50) / 2400;

        for (var i = 0; i < stars; i++)
        {
            var x = random.Next(Width);
            var y = random.Next(Math.Max(20, Horizon - 50));
            var bright = random.NextDouble() < 0.25;
            Put(_background, x, y, bright ? Color.FromRgb(0xC8, 0xC2, 0xDA) : Color.FromRgb(0x55, 0x50, 0x66));

            if (bright && random.NextDouble() < 0.5)
            {
                _twinkles.Add((x, y, random.NextDouble() * Math.PI * 2));
            }
        }

        // Luna menguante con halo a trama, siempre a la misma distancia del borde derecho.
        var moonX = Width - 76;
        const int MoonY = 42, Radius = 15;

        for (var y = MoonY - 32; y <= MoonY + 32; y++)
        {
            for (var x = moonX - 32; x <= moonX + 32; x++)
            {
                var d = Math.Sqrt(((x - moonX) * (x - moonX)) + ((y - MoonY) * (y - MoonY)));

                if (d <= Radius)
                {
                    var cut = Math.Sqrt(((x - moonX + 7) * (x - moonX + 7)) + ((y - MoonY - 3) * (y - MoonY - 3)));

                    if (cut > Radius - 1)
                    {
                        Put(_background, x, y, x > moonX + 8 ? Color.FromRgb(0xA9, 0x9F, 0xBF) : Color.FromRgb(0xDB, 0xD4, 0xEA));
                    }
                }
                else if (d <= 30 && Bayer[y & 3, x & 3] < (30 - d) / 15 * 5)
                {
                    Mix(_background, x, y, Color.FromRgb(0x3A, 0x32, 0x52), 0.5);
                }
            }
        }

        // Colinas: lejanas y cercanas.
        for (var x = 0; x < Width; x++)
        {
            var far = Horizon - 36 + (int)(Math.Sin(x * 0.013) * 7) + (int)(Math.Sin((x * 0.041) + 1) * 3);
            var near = Horizon - 20 + (int)(Math.Sin((x * 0.009) + 2) * 6) + (int)(Math.Sin(x * 0.033) * 2);

            for (var y = far; y < Height; y++)
            {
                Put(_background, x, y, Color.FromRgb(0x11, 0x0E, 0x1A));
            }

            for (var y = near; y < Height; y++)
            {
                Put(_background, x, y, Color.FromRgb(0x0B, 0x09, 0x10));
            }
        }

        // Árboles secos a los lados, pegados a cada borde.
        Tree(Math.Max(18, FirstSlotX - 6), Horizon + 2);
        Tree(Width - Math.Max(18, FirstSlotX - 6), Horizon - 2);

        // La verja, detrás de las tumbas.
        for (var x = 0; x < Width; x++)
        {
            Put(_background, x, Horizon - 17, Color.FromRgb(0x1C, 0x18, 0x26));
            Put(_background, x, Horizon - 10, Color.FromRgb(0x1C, 0x18, 0x26));

            if (x % 7 == 0)
            {
                for (var y = Horizon - 22; y <= Horizon - 6; y++)
                {
                    Put(_background, x, y, Color.FromRgb(0x22, 0x1D, 0x2E));
                }

                Put(_background, x, Horizon - 23, Color.FromRgb(0x2E, 0x28, 0x3C));
            }
        }

        // El suelo, con briznas a trama.
        for (var y = Horizon - 5; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var speck = random.Next(40);
                Put(_background, x, y, speck == 0 ? Color.FromRgb(0x17, 0x14, 0x1E) : speck == 1 ? Color.FromRgb(0x10, 0x14, 0x12) : Color.FromRgb(0x0D, 0x0B, 0x12));
            }
        }
    }

    private void Tree(int rootX, int ground)
    {
        var bark = Color.FromRgb(0x06, 0x05, 0x09);

        for (var y = ground - 58; y <= ground; y++)
        {
            var thick = y > ground - 12 ? 2 : 1;

            for (var dx = -thick; dx <= thick - 1; dx++)
            {
                Put(_background, rootX + dx + ((ground - y) / 23), y, bark);
            }
        }

        // Ramas en diagonal, a escalones.
        (int FromY, int Direction, int Length)[] branches = [(ground - 40, -1, 12), (ground - 30, 1, 10), (ground - 50, 1, 8), (ground - 20, -1, 7)];

        foreach (var (fromY, direction, length) in branches)
        {
            var x = rootX + ((ground - fromY) / 23);

            for (var i = 0; i < length; i++)
            {
                Put(_background, x + (direction * i), fromY - (i / 2), bark);
            }
        }
    }

    private static Color Hazed(Color colour, double amount) => amount <= 0
        ? colour
        : Color.FromRgb(
            (byte)(colour.R + ((Haze.R - colour.R) * amount)),
            (byte)(colour.G + ((Haze.G - colour.G) * amount)),
            (byte)(colour.B + ((Haze.B - colour.B) * amount)));

    private void Blit(byte[] bgra, int width, int height, int left, int top, double alpha)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                var a = bgra[i + 3] / 255.0 * alpha;

                if (a <= 0.02)
                {
                    continue;
                }

                Mix(_frame, left + x, top + y, Color.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]), a);
            }
        }
    }

    private void Put(byte[] target, int x, int y, Color colour)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        var i = ((y * Width) + x) * 4;
        target[i] = colour.B;
        target[i + 1] = colour.G;
        target[i + 2] = colour.R;
        target[i + 3] = 255;
    }

    private void Mix(byte[] target, int x, int y, Color colour, double amount)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        amount = Math.Clamp(amount, 0, 1);
        var i = ((y * Width) + x) * 4;
        target[i] = (byte)(target[i] + ((colour.B - target[i]) * amount));
        target[i + 1] = (byte)(target[i + 1] + ((colour.G - target[i + 1]) * amount));
        target[i + 2] = (byte)(target[i + 2] + ((colour.R - target[i + 2]) * amount));
        target[i + 3] = 255;
    }

    /// <summary>The sprite as it is, in BGRA.</summary>
    public static byte[] Pixels(BitmapSource sprite)
    {
        var bgra = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
        return pixels;
    }

    /// <summary>A sprite turned into its ghost: pale violet by brightness, the transparency kept.</summary>
    public static (byte[] Pixels, int Width, int Height) Ghost(BitmapSource sprite)
    {
        var bgra = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        var pixels = new byte[width * height * 4];

        bgra.CopyPixels(pixels, width * 4, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var luma = ((pixels[i + 2] * 0.299) + (pixels[i + 1] * 0.587) + (pixels[i] * 0.114)) / 255.0;
            var tone = 0.45 + (luma * 0.55);

            pixels[i] = (byte)(0xE6 * tone);
            pixels[i + 1] = (byte)(0xC6 * tone);
            pixels[i + 2] = (byte)(0xCC * tone);
        }

        return (pixels, width, height);
    }
}
