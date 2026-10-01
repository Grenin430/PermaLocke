using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// A groove in cells: a dark line and a lit one under it, the way the frames of a handheld menu are cut. Optionally
/// starts in the accent colour for a few cells, which is what turns a divider into a heading's underline.
/// </summary>
public sealed class PixelRule : FrameworkElement
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(PixelRule),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentCellsProperty = DependencyProperty.Register(
        nameof(AccentCells), typeof(int), typeof(PixelRule),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(PixelRule),
        new FrameworkPropertyMetadata(Color.FromRgb(0xB0, 0x7B, 0xF0), FrameworkPropertyMetadataOptions.AffectsRender));

    private static Color Dark => PixelTheme.Current.RuleDark;
    private static Color Light => PixelTheme.Current.RuleLight;

    public PixelRule()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public int AccentCells
    {
        get => (int)GetValue(AccentCellsProperty);
        set => SetValue(AccentCellsProperty, value);
    }

    public Color Accent
    {
        get => (Color)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var thick = ToastPixels.Units(this, 2);
        return Orientation == Orientation.Horizontal ? new Size(0, thick) : new Size(thick, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var horizontal = Orientation == Orientation.Horizontal;
        var length = (int)Math.Floor((horizontal ? ActualWidth * dpi.DpiScaleX : ActualHeight * dpi.DpiScaleY) / cell);
        if (length <= 0) return;

        var canvas = horizontal ? new ToastPixels.Canvas(length, 2) : new ToastPixels.Canvas(2, length);
        for (var i = 0; i < length; i++)
        {
            var accent = i < AccentCells;
            var first = accent ? Accent : Dark;
            var second = accent ? ToastPixels.Mix(Accent, Colors.Black, 0.45) : Light;
            if (horizontal)
            {
                canvas.Put(i, 0, first);
                canvas.Put(i, 1, second);
            }
            else
            {
                canvas.Put(0, i, first);
                canvas.Put(1, i, second);
            }
        }

        drawingContext.DrawImage(canvas.ToBitmap(),
            new Rect(0, 0, canvas.Columns * cell / dpi.DpiScaleX, canvas.Rows * cell / dpi.DpiScaleY));
    }
}
