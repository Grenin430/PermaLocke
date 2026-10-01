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
        var style = PixelTheme.Current.Backdrop;

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                canvas.Put(x, y, style switch
                {
                    BackdropStyle.Stripes => Stripes(x, y),
                    BackdropStyle.LcdGrid => LcdGrid(x, y),
                    BackdropStyle.Stars => Stars(x, y),
                    BackdropStyle.Grille => Grille(x, y),
                    _ => Lattice(x, y)
                });
            }
        }

        drawingContext.DrawImage(canvas.ToBitmap(),
            new Rect(0, 0, columns * cell / dpi.DpiScaleX, rows * cell / dpi.DpiScaleY));
    }

    /// <summary>The original: a lattice of diamonds every eight cells, the lit dot and its shadow below and right.</summary>
    private Color Lattice(int x, int y)
    {
        var lit = ((x + y) & 7) == 0 && ((x - y) & 7) == 0;
        var shade = ((x - 1 + y - 1) & 7) == 0 && ((x - 1 - (y - 1)) & 7) == 0;
        return lit ? ToastPixels.Mix(Base, Colors.White, 0.05) : shade ? ToastPixels.Mix(Base, Colors.Black, 0.25) : Base;
    }

    /// <summary>ESMERALDA: wide diagonal bands, two tones, with a thin light line on each edge — the bag's background.</summary>
    private Color Stripes(int x, int y)
    {
        var d = (x + y) % 24;
        return d == 0 ? ToastPixels.Mix(Base, Colors.White, 0.28)
            : d < 12 ? ToastPixels.Mix(Base, Colors.White, 0.12)
            : d == 12 ? ToastPixels.Mix(Base, Colors.Black, 0.12)
            : Base;
    }

    /// <summary>GAME BOY: the screen's own grid, one darker cell every two in each direction, faint.</summary>
    private Color LcdGrid(int x, int y) =>
        (x & 1) == 1 && (y & 1) == 1 ? ToastPixels.Mix(Base, Colors.Black, 0.12) : Base;

    /// <summary>ULTRAUMBRAL: a field of stars, three brightnesses, fixed — the same sky at every size.</summary>
    private Color Stars(int x, int y)
    {
        var hash = unchecked((uint)((x * 73856093) ^ (y * 19349663)) * 2654435761u) >> 20;
        return hash switch
        {
            < 3 => ToastPixels.Mix(Base, Colors.White, 0.85),
            < 10 => ToastPixels.Mix(Base, Color.FromRgb(0x9A, 0xF6, 0xFF), 0.45),
            < 26 => ToastPixels.Mix(Base, Color.FromRgb(0xFF, 0x4F, 0xD8), 0.22),
            _ => Base
        };
    }

    /// <summary>ROTOM DEX: the speaker grille of the device — holes in staggered rows, each with a lit lower edge.</summary>
    private Color Grille(int x, int y)
    {
        var row = y / 4;
        var column = (x + ((row & 1) * 2)) % 4;
        var within = y % 4;
        if (column == 0 && within == 1) return ToastPixels.Mix(Base, Colors.Black, 0.45);
        if (column == 0 && within == 2) return ToastPixels.Mix(Base, Colors.White, 0.08);
        return Base;
    }
}
