using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>
/// The strip behind every section's header: the sea and the sky over Alola at the game's hour, in pixels.
/// </summary>
/// <remarks>
/// <para>
/// It replaces a first attempt that painted the whole window with smooth gradients, a soft travelling glow and
/// stars scattered over the interface. The player said it looked made by an AI, and it did: that is the stock
/// look of a generated background. What had worked here was the opposite — the cemetery and the blood, drawn
/// cell by cell (§113, §115) — so this is that: a contained strip that ends on the header's rule, whole pixels,
/// a handful of flat colours with ordered dithering, and shapes typed in by hand. The four islands have their
/// own silhouettes (Akala's volcano glows at night, Mount Lanakila has snow), the clouds and the birds are
/// little sprites written as text, and everything moves in steps at a few frames per second, never smoothly.
/// </para>
/// <para>
/// The sky is darker than in the sidebar's window, because the section's title, the points and the badges sit
/// on it. With the hour unknown the strip is not drawn at all.
/// </para>
/// </remarks>
public sealed class AlolaBanner : ContentControl
{
    public static readonly DependencyProperty HourProperty = DependencyProperty.Register(
        nameof(Hour), typeof(double), typeof(AlolaBanner),
        new PropertyMetadata(0.0, (d, e) => ((AlolaBanner)d).Paint()));

    /// <summary>Screen pixels per cell.</summary>
    private const int Cell = 2;

    /// <summary>Rows of sea under the horizon.</summary>
    private const int Sea = 5;

    /// <summary>How much the sky is pushed towards black so the header stays readable on it.</summary>
    private const double Dim = 0.32;

    private static readonly string[] CloudLarge =
    [
        "......###.......",
        "...#########....",
        "..###########.##",
        ".###############",
        "++++++++++++++++"
    ];

    private static readonly string[] CloudSmall =
    [
        "...####...",
        ".########.",
        "##########",
        "++++++++++"
    ];

    private static readonly string[] CloudLong =
    [
        "....####.....###......",
        "..#########.######....",
        "#####################.",
        "++++++++++++++++++++++"
    ];

    private static readonly string[][] Bird =
    [
        ["#...#", ".#.#."],
        [".....", "##.##"]
    ];

    /// <summary>
    /// The islands, west to east, as column heights typed by hand: Melemele low and round, Akala with the crater
    /// of Wela, Ula'ula rising to Mount Lanakila, and Poni with its cliffs.
    /// </summary>
    private static readonly (double Centre, int[] Heights, bool Snow, int Crater)[] Islands =
    [
        // Entre el título, a la izquierda, y los puntos y distintivos, a la derecha: ahí no tapan nada.
        (0.22, [1, 2, 2, 3, 4, 4, 5, 5, 4, 4, 3, 3, 3, 4, 5, 6, 5, 3, 2, 2, 1, 1], false, -1),
        (0.38, [1, 2, 3, 3, 4, 5, 6, 7, 8, 9, 10, 11, 11, 10, 10, 11, 11, 10, 9, 8, 7, 6, 5, 4, 3, 3, 2, 2, 1, 1], false, 13),
        (0.55, [1, 1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 19, 18, 16, 14, 12, 10, 9, 8, 7, 6, 6, 5, 4, 4, 3, 3, 2, 2, 1, 1, 1], true, -1),
        (0.70, [2, 5, 6, 6, 6, 7, 7, 6, 6, 6, 5, 5, 5, 6, 6, 6, 5, 5, 4, 4, 4, 3, 3, 2, 2, 1], false, -1)
    ];

    private static readonly Color Snow = Rgb(0xD6, 0xDE, 0xEA);
    private static readonly Color Lava = Rgb(0xE8, 0x62, 0x2A);

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = [];
    private int _columns;
    private int _rows;
    private long _lastStep = -1;

    public AlolaBanner()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Children = { _image } };

        SizeChanged += (_, _) => Reshape();
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    public double Hour
    {
        get => (double)GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void Reshape()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / Cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / Cell);

        if (columns <= 0 || rows <= Sea + 4)
        {
            return;
        }

        _zoom.ScaleX = Cell / dpi.DpiScaleX;
        _zoom.ScaleY = Cell / dpi.DpiScaleY;

        if (columns == _columns && rows == _rows)
        {
            return;
        }

        _columns = columns;
        _rows = rows;
        _pixels = new byte[columns * rows * 4];
        _bitmap = new WriteableBitmap(columns, rows, 96, 96, PixelFormats.Bgra32, null);
        _image.Source = _bitmap;
        _image.Width = columns;
        _image.Height = rows;
        Paint();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // A saltos, cuatro veces por segundo: se mueve como un juego de píxeles, no como un salvapantallas.
        var step = _clock.ElapsedMilliseconds / 250;

        if (IsVisible && step != _lastStep)
        {
            _lastStep = step;
            Paint();
        }
    }

    private void Paint()
    {
        if (_bitmap is null)
        {
            return;
        }

        var hour = ((Hour % 24) + 24) % 24;
        var step = _clock.ElapsedMilliseconds / 250;
        var (rawTop, rawMiddle, rawLow) = SkyAt(hour);
        var top = Lerp(rawTop, Ink, Dim);
        var middle = Lerp(rawMiddle, Ink, Dim);
        var low = Lerp(rawLow, Ink, Dim);
        var horizon = _rows - Sea - 1;
        var night = NightAmount(hour);

        SkyBands(top, middle, low, horizon);
        Stars(horizon, night, step);

        // El arco acaba antes del borde derecho: ahí van los puntos y los distintivos, y un sol poniéndose detrás de
        // «PUNTOS» se comía la palabra.
        var (isDay, along) = Body(hour);
        var bodyX = (int)Math.Round((0.03 + (along * 0.76)) * (_columns - 1));
        var bodyY = horizon - 1 - (int)Math.Round(Math.Sin(Math.PI * along) * (horizon - 7));

        if (isDay)
        {
            Sun(bodyX, bodyY, horizon, Lerp(SunLow, SunHigh, Math.Sin(Math.PI * along)), step);
        }
        else
        {
            Moon(bodyX, bodyY, horizon);
        }

        Clouds(rawMiddle, rawLow, night, horizon, step);

        if (hour is >= 7 and < 17)
        {
            Birds(top, step);
        }

        Water(middle, low, horizon, bodyX, bodyY <= horizon, isDay ? Lerp(SunLow, SunHigh, 0.5) : MoonLight, step);
        IslandsAt(top, horizon, night, step);

        _bitmap.WritePixels(new Int32Rect(0, 0, _columns, _rows), _pixels, _columns * 4, 0);
    }

    private void SkyBands(Color top, Color middle, Color low, int horizon)
    {
        Color[] ramp = [top, Lerp(top, middle, 0.5), middle, Lerp(middle, low, 0.5), low];

        for (var y = 0; y <= horizon; y++)
        {
            var position = y / (double)horizon * (ramp.Length - 1);
            var band = Math.Min((int)position, ramp.Length - 2);
            var mix = position - band;

            for (var x = 0; x < _columns; x++)
            {
                Put(x, y, Bayer[y & 3, x & 3] < mix * 16 ? ramp[band + 1] : ramp[band]);
            }
        }
    }

    /// <summary>Fixed stars that come out with the night; now and then one blinks off for a step.</summary>
    private void Stars(int horizon, double night, long step)
    {
        if (night <= 0)
        {
            return;
        }

        var random = new Random(1311);
        var count = _columns / 7;

        for (var i = 0; i < count; i++)
        {
            var x = random.Next(_columns);
            var y = random.Next(Math.Max(1, horizon - 6));
            var bright = random.Next(4) == 0;
            var appearsAt = random.NextDouble();

            if (appearsAt > night || (step + (i * 7)) % 29 == 0)
            {
                continue;
            }

            Put(x, y, bright ? Star : Lerp(Star, Ink, 0.45));
        }
    }

    private void Sun(int cx, int cy, int horizon, Color colour, long step)
    {
        Disc(cx, cy, 4.2, horizon, (dx, dy) => dx + dy >= 4 ? Lerp(colour, SunLow, 0.5) : colour);

        // Los rayos, ocho píxeles sueltos que se alternan cada segundo entre los rectos y los diagonales.
        (int X, int Y)[] straight = [(0, -7), (7, 0), (0, 7), (-7, 0)];
        (int X, int Y)[] diagonal = [(5, -5), (5, 5), (-5, 5), (-5, -5)];

        foreach (var (x, y) in step / 4 % 2 == 0 ? straight : diagonal)
        {
            if (cy + y <= horizon)
            {
                Put(cx + x, cy + y, colour);
            }
        }
    }

    private void Moon(int cx, int cy, int horizon)
    {
        Disc(cx, cy, 3.6, horizon, (dx, dy) => dx + dy >= 3 ? MoonShade : MoonLight);

        // Tres cráteres puestos a mano.
        foreach (var (x, y) in new[] { (-1, -1), (1, 1), (-2, 1) })
        {
            if (cy + y <= horizon)
            {
                Put(cx + x, cy + y, MoonShade);
            }
        }
    }

    private void Disc(int cx, int cy, double radius, int horizon, Func<int, int, Color> colourAt)
    {
        var reach = (int)Math.Ceiling(radius);

        for (var y = cy - reach; y <= Math.Min(horizon, cy + reach); y++)
        {
            for (var x = cx - reach; x <= cx + reach; x++)
            {
                var dx = x - cx;
                var dy = y - cy;

                if ((dx * dx) + (dy * dy) <= radius * radius)
                {
                    Put(x, y, colourAt(dx, dy));
                }
            }
        }
    }

    /// <summary>Three clouds drifting east one cell at a time, lit by the sky around them.</summary>
    private void Clouds(Color rawMiddle, Color rawLow, double night, int horizon, long step)
    {
        var light = Lerp(Lerp(rawLow, Colors.White, 0.35), Ink, Dim + (night * 0.45));
        var shadow = Lerp(Lerp(rawMiddle, rawLow, 0.4), Ink, Dim + (night * 0.45));

        (string[] Sprite, double Start, int Row, int Every)[] clouds =
        [
            (CloudLarge, 0.08, 3, 5),
            (CloudLong, 0.47, 7, 7),
            (CloudSmall, 0.74, 2, 4)
        ];

        foreach (var (sprite, start, row, every) in clouds)
        {
            if (row + sprite.Length >= horizon - 2)
            {
                continue;
            }

            var width = sprite[0].Length;
            var travel = _columns + width;
            var left = (int)(((long)(start * travel) + (step / every)) % travel) - width;

            for (var y = 0; y < sprite.Length; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    switch (sprite[y][x])
                    {
                        case '#':
                            Put(left + x, row + y, light);
                            break;
                        case '+':
                            Put(left + x, row + y, shadow);
                            break;
                    }
                }
            }
        }
    }

    /// <summary>Two birds crossing the day sky, flapping every other step.</summary>
    private void Birds(Color top, long step)
    {
        var ink = Lerp(top, Ink, 0.6);
        var frame = Bird[step % 2];
        var travel = _columns + 60;

        foreach (var (offset, row) in new[] { (0, 5), (7, 8) })
        {
            var left = (int)((step + offset) % travel) - 30;

            for (var y = 0; y < frame.Length; y++)
            {
                for (var x = 0; x < frame[y].Length; x++)
                {
                    if (frame[y][x] == '#')
                    {
                        Put(left + x, row + y, ink);
                    }
                }
            }
        }
    }

    private void Water(Color middle, Color low, int horizon, int bodyX, bool bodyUp, Color glint, long step)
    {
        for (var y = horizon + 1; y < _rows; y++)
        {
            var depth = (y - horizon) / (double)Sea;
            var water = Lerp(Lerp(low, middle, 0.5), Ink, 0.35 + (depth * 0.3));
            var crest = Lerp(water, low, 0.4);
            var drift = (int)(step / 2) * ((y & 1) == 0 ? 1 : -1);

            for (var x = 0; x < _columns; x++)
            {
                Put(x, y, ((((x + drift + (y * 5)) % 11) + 11) % 11) < 2 ? crest : water);

                if (bodyUp && Math.Abs(x - bodyX) <= 1 + (y - horizon) && ((x + y + step) % 3) == 0)
                {
                    Put(x, y, Lerp(water, glint, 0.6));
                }
            }
        }
    }

    private void IslandsAt(Color top, int horizon, double night, long step)
    {
        var land = Lerp(top, Ink, 0.62);
        var tallest = Islands.Max(island => island.Heights.Max());
        var scale = Math.Min(1.0, (horizon - 8) / (double)tallest);

        foreach (var (centre, heights, snow, crater) in Islands)
        {
            var left = (int)Math.Round(centre * _columns) - (heights.Length / 2);
            var peak = heights.Max();

            for (var i = 0; i < heights.Length; i++)
            {
                var height = Math.Max(1, (int)Math.Round(heights[i] * scale));

                // La base entra una fila en el mar, para que la isla se apoye y no flote.
                for (var y = horizon - height + 1; y <= horizon + 1; y++)
                {
                    Put(left + i, y, land);
                }

                if (snow && heights[i] >= peak - 3)
                {
                    Put(left + i, horizon - height + 1, Lerp(Snow, Ink, Dim + (night * 0.35)));

                    if (heights[i] >= peak - 1)
                    {
                        Put(left + i, horizon - height + 2, Lerp(Snow, Ink, Dim + (night * 0.35)));
                    }
                }
            }

            // Wela de noche: dos píxeles de lava en el cráter que se apagan un paso de cada cuatro.
            if (crater >= 0 && night > 0.5 && step % 4 != 0)
            {
                var height = (int)Math.Round(heights[crater] * scale);
                Put(left + crater, horizon - height + 1, Lava);
                Put(left + crater + 1, horizon - height + 1, Lerp(Lava, land, 0.4));
            }
        }
    }

    private void Put(int x, int y, Color colour)
    {
        if (x < 0 || x >= _columns || y < 0 || y >= _rows)
        {
            return;
        }

        var i = ((y * _columns) + x) * 4;
        _pixels[i] = colour.B;
        _pixels[i + 1] = colour.G;
        _pixels[i + 2] = colour.R;
        _pixels[i + 3] = 255;
    }
}
