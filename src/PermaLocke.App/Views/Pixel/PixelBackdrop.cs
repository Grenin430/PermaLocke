using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// The floor the screens sit on: a flat violet with a faint lattice of lighter cells, like the patterned backgrounds
/// of a handheld's menus. Faint on purpose — it is texture, not decoration, and it sits under everything.
/// </summary>
public sealed class PixelBackdrop : FrameworkElement
{
    public static readonly DependencyProperty BaseProperty = DependencyProperty.Register(
        nameof(Base), typeof(Color), typeof(PixelBackdrop),
        new FrameworkPropertyMetadata(Color.FromRgb(0x12, 0x0E, 0x20), FrameworkPropertyMetadataOptions.AffectsRender));

    public PixelBackdrop()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        SizeChanged += (_, _) => InvalidateVisual();
    }

    public Color Base
    {
        get => (Color)GetValue(BaseProperty);
        set => SetValue(BaseProperty, value);
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
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / cell);
        if (columns <= 0 || rows <= 0) return;

        var canvas = new ToastPixels.Canvas(columns, rows);
        var dot = ToastPixels.Mix(Base, Colors.White, 0.05);
        var dotDark = ToastPixels.Mix(Base, Colors.Black, 0.25);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                // Una celiosía de rombos cada ocho celdas: el punto claro y su sombra debajo a la derecha.
                var lit = ((x + y) & 7) == 0 && ((x - y) & 7) == 0;
                var shade = ((x - 1 + y - 1) & 7) == 0 && ((x - 1 - (y - 1)) & 7) == 0;
                canvas.Put(x, y, lit ? dot : shade ? dotDark : Base);
            }
        }

        drawingContext.DrawImage(canvas.ToBitmap(),
            new Rect(0, 0, columns * cell / dpi.DpiScaleX, rows * cell / dpi.DpiScaleY));
    }
}
