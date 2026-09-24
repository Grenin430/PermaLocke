using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PermaLocke.App.Views;

/// <summary>
/// A bar in cells, like the health bars of the handheld games: an ink outline with square ends, a dark track, and the
/// fill with a lighter top row. It fills in whole cells, so a value never shows as a smudge between two.
/// </summary>
public sealed class PixelBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(PixelBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(PixelBar),
        new FrameworkPropertyMetadata(Color.FromRgb(0xB0, 0x7B, 0xF0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Color), typeof(PixelBar),
        new FrameworkPropertyMetadata(Color.FromRgb(0x1E, 0x18, 0x30), FrameworkPropertyMetadataOptions.AffectsRender));

    public PixelBar()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>From 0 to 1.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color Fill
    {
        get => (Color)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Color Track
    {
        get => (Color)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
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

        if (columns < 4 || rows < 3)
        {
            return;
        }

        var canvas = new ToastPixels.Canvas(columns, rows);
        canvas.Box(0, 0, columns, rows, ToastPixels.Ink);
        canvas.Box(1, 1, columns - 2, rows - 2, Track, notched: false);

        var inner = columns - 2;
        var filled = (int)Math.Round(inner * Math.Clamp(double.IsFinite(Value) ? Value : 0, 0, 1));

        // Algo que no es cero se ve, aunque sea una celda.
        if (filled == 0 && Value > 0)
        {
            filled = 1;
        }

        var light = ToastPixels.Mix(Fill, Colors.White, 0.35);
        var dark = ToastPixels.Mix(Fill, Colors.Black, 0.25);

        for (var x = 1; x <= filled; x++)
        {
            for (var y = 1; y < rows - 1; y++)
            {
                var colour = y == 1 && rows > 3 ? light : y == rows - 2 && rows > 4 ? dark : Fill;
                canvas.Put(x, y, colour);
            }
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }
}

/// <summary>
/// The PC cursor: four corner brackets round the chosen hole that breathe one cell in and out, in steps.
/// </summary>
/// <remarks>
/// Stepped on purpose. A smooth pulse is the tell of something generated; the handheld cursor jumps between two
/// frames, and so does this one, only while it is on screen.
/// </remarks>
public sealed class PixelCursor : FrameworkElement
{
    public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(
        nameof(Colour), typeof(Color), typeof(PixelCursor),
        new FrameworkPropertyMetadata(Color.FromRgb(0xE8, 0xC2, 0x4A), FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(420) };
    private bool _in;

    public PixelCursor()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);

        _timer.Tick += (_, _) =>
        {
            _in = !_in;
            InvalidateVisual();
        };

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _in = false;
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        };

        Unloaded += (_, _) => _timer.Stop();
    }

    public Color Colour
    {
        get => (Color)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Floor(ActualHeight * dpi.DpiScaleY / cell);

        if (columns < 10 || rows < 10)
        {
            return;
        }

        var canvas = new ToastPixels.Canvas(columns, rows);
        var inset = _in ? 1 : 0;
        const int arm = 4;

        void Corner(int x, int y, int dx, int dy)
        {
            // Primero la tinta, un contorno de una celda, y encima el color: se lee sobre cualquier fondo.
            for (var i = -1; i <= arm; i++)
            {
                for (var t = -1; t <= 1; t++)
                {
                    canvas.Put(x + (dx * i), y + (dy * t), ToastPixels.Ink);
                    canvas.Put(x + (dx * t), y + (dy * i), ToastPixels.Ink);
                }
            }

            for (var i = 0; i < arm; i++)
            {
                canvas.Put(x + (dx * i), y, Colour);
                canvas.Put(x, y + (dy * i), Colour);
            }
        }

        var left = 1 + inset;
        var top = 1 + inset;
        var right = columns - 2 - inset;
        var bottom = rows - 2 - inset;

        Corner(left, top, 1, 1);
        Corner(right, top, -1, 1);
        Corner(left, bottom, 1, -1);
        Corner(right, bottom, -1, -1);

        ToastPixels.Draw(drawingContext, this, canvas);
    }
}
