using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// One of the <see cref="PixelIcons"/>, in whole screen pixels. Dimmed when it is not the one that matters, and
/// bobbing up and down when it is, the way the Pokémon of the party menu do.
/// </summary>
public sealed class PixelIcon : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(PixelIcon),
        new FrameworkPropertyMetadata("IconDot", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(int), typeof(PixelIcon),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DimProperty = DependencyProperty.Register(
        nameof(Dim), typeof(bool), typeof(PixelIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BobProperty = DependencyProperty.Register(
        nameof(Bob), typeof(bool), typeof(PixelIcon),
        new FrameworkPropertyMetadata(false, (d, _) => ((PixelIcon)d).OnBobChanged()));

    private DispatcherTimer? _timer;
    private bool _up;

    public PixelIcon()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Unloaded += (_, _) => StopBobbing();
        Loaded += (_, _) => OnBobChanged();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public int Scale
    {
        get => (int)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Drawn darker and duller: not the section on screen.</summary>
    public bool Dim
    {
        get => (bool)GetValue(DimProperty);
        set => SetValue(DimProperty, value);
    }

    /// <summary>Hops a cell up and down, for the section on screen.</summary>
    public bool Bob
    {
        get => (bool)GetValue(BobProperty);
        set => SetValue(BobProperty, value);
    }

    private int Cell
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            // Hacia abajo y no al más cercano (2026-09-24): con Windows al 125 % el 2,5 se iba a 3 y todo el texto
            // salía un 20 % más ancho de lo diseñado, cortado en la barra y en las listas. Así nunca pasa de su
            // tamaño: como mucho sale algo más pequeño, y sigue en píxeles enteros.
            return Math.Max(Scale, (int)Math.Floor(Scale * dpi.DpiScaleX));
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnBobChanged()
    {
        if (Bob && IsLoaded)
        {
            _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(260), DispatcherPriority.Render, (_, _) =>
            {
                _up = !_up;
                InvalidateVisual();
            }, Dispatcher);
            _timer.Start();
        }
        else
        {
            StopBobbing();
        }
    }

    private void StopBobbing()
    {
        _timer?.Stop();
        if (_up)
        {
            _up = false;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dip = Cell / VisualTreeHelper.GetDpi(this).DpiScaleX;
        return new Size(PixelIcons.Size * dip, (PixelIcons.Size + 1) * dip);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = Cell;
        var rows = PixelIcons.For(Icon);
        var canvas = new ToastPixels.Canvas(PixelIcons.Size, PixelIcons.Size + 1);
        var top = _up ? 0 : 1;

        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                if (!PixelIcons.TryColour(rows[y][x], out var colour)) continue;

                // Apagado: el color se va hacia el violeta oscuro de la barra, sin perder el dibujo.
                if (Dim && rows[y][x] != 'o') colour = ToastPixels.Mix(colour, Color.FromRgb(0x2A, 0x22, 0x46), 0.55);
                canvas.Put(x, y + top, colour);
            }
        }

        drawingContext.DrawImage(canvas.ToBitmap(),
            new Rect(0, 0, PixelIcons.Size * cell / dpi.DpiScaleX, (PixelIcons.Size + 1) * cell / dpi.DpiScaleY));
    }
}
