using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// A box drawn cell by cell in any colour: one-cell outline, one-cell bevel lit on top, notched corners and a hard
/// shadow. The same box as the notices (<see cref="ToastFrame"/>), for places whose colour is data — a type badge is
/// the type's colour.
/// </summary>
/// <remarks>
/// <see cref="IsSunken"/> turns the bevel round, dark on top and lit underneath, which reads as a hole instead of a
/// plate: the holes of the PC. The fill's own alpha is honoured, so a hole can let the box wallpaper show through.
/// </remarks>
public sealed class PixelPanel : FrameworkElement
{
    /// <summary>How far the shadow falls, in cells.</summary>
    private const int Shadow = 2;

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(ToastPixels.Face, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasShadowProperty = DependencyProperty.Register(
        nameof(HasShadow), typeof(bool), typeof(PixelPanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSunkenProperty = DependencyProperty.Register(
        nameof(IsSunken), typeof(bool), typeof(PixelPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderHeightProperty = DependencyProperty.Register(
        nameof(HeaderHeight), typeof(double), typeof(PixelPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderFillProperty = DependencyProperty.Register(
        nameof(HeaderFill), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(Color.FromRgb(0x21, 0x1A, 0x3A), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderAccentProperty = DependencyProperty.Register(
        nameof(HeaderAccent), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// A title band across the top, this tall in units; zero for none. It is how a panel becomes a window of a game's
    /// menu: the band in its own colour, an ink line under it, and a short accent under the start of the line.
    /// </summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public Color HeaderFill
    {
        get => (Color)GetValue(HeaderFillProperty);
        set => SetValue(HeaderFillProperty, value);
    }

    /// <summary>The short stripe under the start of the band; transparent for none.</summary>
    public Color HeaderAccent
    {
        get => (Color)GetValue(HeaderAccentProperty);
        set => SetValue(HeaderAccentProperty, value);
    }

    public PixelPanel()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public Color Fill
    {
        get => (Color)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    public bool IsSunken
    {
        get => (bool)GetValue(IsSunkenProperty);
        set => SetValue(IsSunkenProperty, value);
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

        if (columns < 6 || rows < 5)
        {
            return;
        }

        var shadow = HasShadow ? Shadow : 0;
        var width = columns - shadow;
        var height = rows - shadow;
        var canvas = new ToastPixels.Canvas(columns, rows);
        var alpha = Fill.A;
        var light = ToastPixels.Mix(Fill, Colors.White, 0.22);
        var dark = ToastPixels.Mix(Fill, Colors.Black, 0.30);
        var top = IsSunken ? dark : light;
        var bottom = IsSunken ? light : dark;

        if (shadow > 0)
        {
            canvas.Box(shadow, shadow, width, height, Colors.Black, alpha: 150);
        }

        canvas.Box(0, 0, width, height, ToastPixels.Ink, alpha);
        canvas.Box(1, 1, width - 2, height - 2, Fill, alpha, notched: false);

        for (var x = 2; x < width - 2; x++)
        {
            canvas.Put(x, 1, top, alpha);
            canvas.Put(x, height - 2, bottom, alpha);
        }

        for (var y = 2; y < height - 2; y++)
        {
            canvas.Put(1, y, top, alpha);
            canvas.Put(width - 2, y, bottom, alpha);
        }

        // La banda del título: su color con su propio brillo arriba, una línea de tinta debajo y el acento que arranca.
        var band = (int)Math.Round(HeaderHeight * dpi.DpiScaleY / cell);
        if (band > 2 && band < height - 4)
        {
            var bandLight = ToastPixels.Mix(HeaderFill, Colors.White, 0.16);
            for (var y = 1; y <= band; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var corner = y == 1 && (x == 1 || x == width - 2);
                    if (!corner) canvas.Put(x, y, y == 1 ? bandLight : HeaderFill, alpha);
                }
            }

            for (var x = 1; x < width - 1; x++)
            {
                canvas.Put(x, band + 1, ToastPixels.Ink, alpha);
            }

            if (HeaderAccent.A > 0)
            {
                for (var x = 1; x < Math.Min(width - 1, 16); x++)
                {
                    canvas.Put(x, band + 2, HeaderAccent, alpha);
                }
            }
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }
}
