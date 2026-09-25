using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// The effort hexagon of the summary screen, drawn cell by cell: PS at the top and then Ataque, Defensa, Velocidad,
/// Def. Esp. and At. Esp. going round clockwise, which is the order the handheld games use.
/// </summary>
/// <remarks>
/// <para>
/// Rasterised rather than drawn with a <c>Polygon</c>: a smooth, anti-aliased shape in the middle of a screen made of
/// cells is the tell of something generated. Every cell is decided by whether its centre falls inside, the edge is the
/// cells that have an outside neighbour, and the lines to the corners are Bresenham's.
/// </para>
/// <para>
/// Two shapes when there is an edit: <see cref="Values"/> filled, and <see cref="Saved"/> as an outline over it, so
/// the difference between what is on screen and what is in the partida is visible without reading a number.
/// </para>
/// </remarks>
public sealed class EvHexagon : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<int>), typeof(EvHexagon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SavedProperty = DependencyProperty.Register(
        nameof(Saved), typeof(IReadOnlyList<int>), typeof(EvHexagon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsOverProperty = DependencyProperty.Register(
        nameof(IsOver), typeof(bool), typeof(EvHexagon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(EvHexagon),
        new FrameworkPropertyMetadata(Color.FromRgb(0xB0, 0x7B, 0xF0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverFillProperty = DependencyProperty.Register(
        nameof(OverFill), typeof(Color), typeof(EvHexagon),
        new FrameworkPropertyMetadata(Color.FromRgb(0xCF, 0x50, 0x44), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Most a single stat takes; the corners of the hexagon.</summary>
    private const double Top = 252;

    /// <summary>Which stat of the summary screen sits at each corner, clockwise from the top.</summary>
    private static readonly int[] Corners = [0, 1, 2, 5, 4, 3];

    private static readonly Color Before = ToastPixels.Rgb(0xEA, 0xE7, 0xF2);

    /// <summary>
    /// The colour of each stat in HP/Atk/Def/SpA/SpD/Spe order, the ones the games use in their summary screen. The shape
    /// takes the colour of the corner it leans to, and the rows of the screen use the same, so a stat reads the same
    /// everywhere (2026-09-25: the one-colour hexagon looked poor).
    /// </summary>
    public static readonly Color[] StatColours =
    [
        ToastPixels.Rgb(0xFF, 0x5F, 0x5F), ToastPixels.Rgb(0xF5, 0xA2, 0x5C), ToastPixels.Rgb(0xF2, 0xD4, 0x4E),
        ToastPixels.Rgb(0x6F, 0x9C, 0xF5), ToastPixels.Rgb(0x7F, 0xD0, 0x6A), ToastPixels.Rgb(0xF5, 0x7F, 0xB0)
    ];

    /// <summary>The hexagon's own plate, under the shape. The bag screen (§143) paints it dark teal.</summary>
    public static readonly DependencyProperty PlateProperty = DependencyProperty.Register(
        nameof(Plate), typeof(Color), typeof(EvHexagon),
        new FrameworkPropertyMetadata(ToastPixels.Rgb(0x16, 0x12, 0x28), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The two guide rings and the spokes.</summary>
    public static readonly DependencyProperty GuideProperty = DependencyProperty.Register(
        nameof(Guide), typeof(Color), typeof(EvHexagon),
        new FrameworkPropertyMetadata(ToastPixels.Rgb(0x2A, 0x22, 0x46), FrameworkPropertyMetadataOptions.AffectsRender));

    public Color Plate
    {
        get => (Color)GetValue(PlateProperty);
        set => SetValue(PlateProperty, value);
    }

    public Color Guide
    {
        get => (Color)GetValue(GuideProperty);
        set => SetValue(GuideProperty, value);
    }

    public EvHexagon()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>The six EVs on screen, in HP/Atk/Def/SpA/SpD/Spe order.</summary>
    public IReadOnlyList<int>? Values
    {
        get => (IReadOnlyList<int>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>The six the partida holds. Drawn only when they differ from <see cref="Values"/>.</summary>
    public IReadOnlyList<int>? Saved
    {
        get => (IReadOnlyList<int>?)GetValue(SavedProperty);
        set => SetValue(SavedProperty, value);
    }

    /// <summary>Over 510: the shape turns the colour the rest of the screen uses for «no se puede guardar».</summary>
    public bool IsOver
    {
        get => (bool)GetValue(IsOverProperty);
        set => SetValue(IsOverProperty, value);
    }

    public Color Fill
    {
        get => (Color)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Color OverFill
    {
        get => (Color)GetValue(OverFillProperty);
        set => SetValue(OverFillProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Floor(ActualHeight * dpi.DpiScaleY / cell);

        // Tumbado en punta hacia arriba: mide 2R de alto y R·√3 de ancho.
        var radius = (int)Math.Floor(Math.Min(columns / Math.Sqrt(3), rows / 2d)) - 1;

        if (radius < 6)
        {
            return;
        }

        var centre = new Point(columns / 2d, rows / 2d);
        var canvas = new ToastPixels.Canvas(columns, rows);

        // La placa: el hexágono entero, con dos anillos de guía a un tercio y dos tercios.
        var plate = Mask(columns, rows, Shape(centre, radius, [1, 1, 1, 1, 1, 1]));
        Paint(canvas, plate, Plate);

        foreach (var ring in new[] { radius / 3d, radius * 2 / 3d })
        {
            Paint(canvas, Edge(Mask(columns, rows, Shape(centre, ring, [1, 1, 1, 1, 1, 1]))), Guide);
        }

        var middle = Cell(centre);

        foreach (var corner in Shape(centre, radius - 1, [1, 1, 1, 1, 1, 1]))
        {
            Line(canvas, middle, Cell(corner), Guide);
        }

        // Cada pico del color de su estadística, en el hueco de la placa, para que se sepa de quién es aunque esté a 0.
        var outer = Shape(centre, radius - 1, [1, 1, 1, 1, 1, 1]);

        for (var index = 0; index < Corners.Length; index++)
        {
            Dot(canvas, Cell(outer[index]), ToastPixels.Mix(StatColours[Corners[index]], Plate, 0.35));
        }

        // Lo que hay en pantalla: cada celda del color del pico hacia el que se inclina, algo más oscura hacia el
        // centro, y el borde más claro. Pasado de 510, todo del color de «no se puede guardar».
        var colour = IsOver ? OverFill : Fill;

        if (Values is { Count: 6 } values && values.Any(value => value > 0))
        {
            var shape = Shape(centre, radius - 1, Shares(values));
            var filled = Mask(columns, rows, shape);
            var edge = Edge(filled);

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    if (!filled[x, y])
                    {
                        continue;
                    }

                    var here = IsOver ? OverFill : Blend(centre, radius, x + 0.5, y + 0.5);
                    canvas.Put(x, y, edge[x, y] ? ToastPixels.Mix(here, Colors.White, 0.4) : here);
                }
            }

            for (var index = 0; index < Corners.Length; index++)
            {
                if (values[Corners[index]] > 0)
                {
                    Dot(canvas, Cell(shape[index]), IsOver ? OverFill : ToastPixels.Mix(StatColours[Corners[index]], Colors.White, 0.5));
                }
            }

            // Algo que no es cero se ve, aunque la forma sea tan fina que no cubra el centro de ninguna celda.
            for (var index = 0; index < Corners.Length; index++)
            {
                if (values[Corners[index]] > 0)
                {
                    Line(canvas, middle, Cell(shape[index]), colour);
                }
            }
        }

        // Lo que hay en la partida, como un contorno encima, solo si es otra cosa.
        if (Saved is { Count: 6 } saved && Values is { Count: 6 } current && !saved.SequenceEqual(current)
            && saved.Any(value => value > 0))
        {
            Paint(canvas, Edge(Mask(columns, rows, Shape(centre, radius - 1, Shares(saved)))), Before, alpha: 190);
        }

        // El marco va al final, para que un 252 no se lo coma.
        Paint(canvas, Edge(plate), ToastPixels.Ink);

        ToastPixels.Draw(drawingContext, this, canvas);
    }

    /// <summary>The colour of a cell: between the two corners it sits between, and darker nearer the middle.</summary>
    private Color Blend(Point centre, double radius, double x, double y)
    {
        var degrees = Math.Atan2(y - centre.Y, x - centre.X) * 180 / Math.PI;
        var along = (degrees + 90 + 360) % 360;
        var sector = (int)(along / 60) % Corners.Length;
        var t = (along - (sector * 60)) / 60;
        var colour = ToastPixels.Mix(StatColours[Corners[sector]], StatColours[Corners[(sector + 1) % Corners.Length]], t);
        var distance = Math.Sqrt(Math.Pow(x - centre.X, 2) + Math.Pow(y - centre.Y, 2)) / radius;

        return ToastPixels.Mix(colour, Plate, Math.Clamp((1 - distance) * 0.45, 0, 0.45));
    }

    /// <summary>A small square, three cells across, for a corner.</summary>
    private static void Dot(ToastPixels.Canvas canvas, (int X, int Y) at, Color colour)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                canvas.Put(at.X + dx, at.Y + dy, colour);
            }
        }
    }

    private static double[] Shares(IReadOnlyList<int> values) =>
        [.. Corners.Select(stat => Math.Clamp(values[stat] / Top, 0, 1))];

    /// <summary>The six corners, each at its own share of the radius, clockwise from the top.</summary>
    private static Point[] Shape(Point centre, double radius, IReadOnlyList<double> shares)
    {
        var points = new Point[Corners.Length];

        for (var index = 0; index < points.Length; index++)
        {
            var angle = (-90 + (60 * index)) * Math.PI / 180;
            var reach = radius * shares[index];
            points[index] = new Point(centre.X + (reach * Math.Cos(angle)), centre.Y + (reach * Math.Sin(angle)));
        }

        return points;
    }

    /// <summary>Which cells have their centre inside the shape.</summary>
    private static bool[,] Mask(int columns, int rows, Point[] shape)
    {
        var mask = new bool[columns, rows];

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                mask[x, y] = Inside(shape, x + 0.5, y + 0.5);
            }
        }

        return mask;
    }

    /// <summary>The cells of a mask that touch the outside on any of their four sides.</summary>
    private static bool[,] Edge(bool[,] mask)
    {
        var columns = mask.GetLength(0);
        var rows = mask.GetLength(1);
        var edge = new bool[columns, rows];

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                edge[x, y] = mask[x, y]
                    && (x == 0 || y == 0 || x == columns - 1 || y == rows - 1
                        || !mask[x - 1, y] || !mask[x + 1, y] || !mask[x, y - 1] || !mask[x, y + 1]);
            }
        }

        return edge;
    }

    private static void Paint(ToastPixels.Canvas canvas, bool[,] mask, Color colour, byte alpha = 255)
    {
        for (var y = 0; y < mask.GetLength(1); y++)
        {
            for (var x = 0; x < mask.GetLength(0); x++)
            {
                if (mask[x, y])
                {
                    canvas.Put(x, y, colour, alpha);
                }
            }
        }
    }

    /// <summary>Even-odd: a ray to the right crosses the edge an odd number of times from inside.</summary>
    private static bool Inside(Point[] shape, double x, double y)
    {
        var inside = false;

        for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
        {
            var a = shape[i];
            var b = shape[j];

            if ((a.Y > y) != (b.Y > y) && x < ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static (int X, int Y) Cell(Point point) => ((int)Math.Floor(point.X), (int)Math.Floor(point.Y));

    /// <summary>Bresenham, so a line is a run of whole cells and never a smear between two.</summary>
    private static void Line(ToastPixels.Canvas canvas, (int X, int Y) from, (int X, int Y) to, Color colour)
    {
        var (x, y) = from;
        var dx = Math.Abs(to.X - x);
        var dy = -Math.Abs(to.Y - y);
        var sx = x < to.X ? 1 : -1;
        var sy = y < to.Y ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            canvas.Put(x, y, colour);

            if (x == to.X && y == to.Y)
            {
                return;
            }

            var twice = 2 * error;

            if (twice >= dy)
            {
                error += dy;
                x += sx;
            }

            if (twice <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }
}
