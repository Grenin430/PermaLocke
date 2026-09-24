using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// Text in the application's pixel font (<see cref="PixelFont"/>), every cell a whole number of screen pixels.
/// </summary>
/// <remarks>
/// A real element with a measured size, so it lays out, wraps and trims like a <c>TextBlock</c> and takes a binding;
/// what it does not do is select or scale by fractions. <see cref="Scale"/> is the size: 2 is body text, 3 a heading.
/// At other screen scalings the cell grows by whole pixels, the way the scenes do.
/// </remarks>
public sealed class PixelText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PixelText),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(
        nameof(Colour), typeof(Color), typeof(PixelText),
        new FrameworkPropertyMetadata(Color.FromRgb(0xE8, 0xE4, 0xF4), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowProperty = DependencyProperty.Register(
        nameof(Shadow), typeof(Color), typeof(PixelText),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(int), typeof(PixelText),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(bool), typeof(PixelText),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrimProperty = DependencyProperty.Register(
        nameof(Trim), typeof(bool), typeof(PixelText),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(PixelText),
        new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Leaves out the rows above the capitals, for text that never carries an accented capital.</summary>
    public static readonly DependencyProperty TightProperty = DependencyProperty.Register(
        nameof(Tight), typeof(bool), typeof(PixelText),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public PixelText()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Color Colour
    {
        get => (Color)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>A hard shadow one cell down and to the right; transparent for none.</summary>
    public Color Shadow
    {
        get => (Color)GetValue(ShadowProperty);
        set => SetValue(ShadowProperty, value);
    }

    public int Scale
    {
        get => (int)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public bool Wrap
    {
        get => (bool)GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    public bool Trim
    {
        get => (bool)GetValue(TrimProperty);
        set => SetValue(TrimProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public bool Tight
    {
        get => (bool)GetValue(TightProperty);
        set => SetValue(TightProperty, value);
    }

    private bool HasShadow => Shadow.A > 0;

    /// <summary>Tight leaves out the top row, and the accents of capitals sit right on the letter instead.</summary>
    private int Top => Tight ? 1 : 0;

    private int LineRows => PixelFont.LineRows - Top;

    /// <summary>Screen pixels per cell at this element's scaling.</summary>
    private int Cell
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return Math.Max(Scale, (int)Math.Floor((Scale * dpi.DpiScaleX) + 0.5));
        }
    }

    private double CellDip => Cell / VisualTreeHelper.GetDpi(this).DpiScaleX;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private List<string> Lines(double width)
    {
        var text = Text ?? string.Empty;
        if (text.Length == 0) return [];

        var shadow = HasShadow ? 1 : 0;
        var cells = double.IsInfinity(width) ? int.MaxValue : Math.Max(1, (int)Math.Floor(width / CellDip) - shadow);

        if (Wrap) return PixelFont.Wrap(text, cells);
        return [Trim ? PixelFont.Trim(text, cells) : text];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lines = Lines(availableSize.Width);
        if (lines.Count == 0) return new Size(0, 0);

        var shadow = HasShadow ? 1 : 0;
        var columns = lines.Max(PixelFont.Measure) + shadow;
        var rows = (lines.Count * LineRows) + ((lines.Count - 1) * PixelFont.Leading) + shadow;
        return new Size(columns * CellDip, rows * CellDip);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var lines = Lines(ActualWidth);
        if (lines.Count == 0) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = Cell;
        var shadow = HasShadow ? 1 : 0;
        var columns = Math.Max(lines.Max(PixelFont.Measure) + shadow, (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell));
        var rows = (lines.Count * LineRows) + ((lines.Count - 1) * PixelFont.Leading) + shadow;
        var canvas = new ToastPixels.Canvas(columns, rows);

        for (var pass = shadow; pass >= 0; pass--)
        {
            var colour = pass == 1 ? Shadow : Colour;
            for (var i = 0; i < lines.Count; i++)
            {
                var width = PixelFont.Measure(lines[i]) + shadow;
                var left = TextAlignment switch
                {
                    TextAlignment.Center => (columns - width) / 2,
                    TextAlignment.Right => columns - width,
                    _ => 0,
                };

                var top = (i * (LineRows + PixelFont.Leading)) - Top;
                PixelFont.Draw(lines[i], left + pass, top + pass, (x, y) => canvas.Put(x, y, colour, colour.A), Tight);
            }
        }

        drawingContext.DrawImage(canvas.ToBitmap(), new Rect(0, 0, columns * cell / dpi.DpiScaleX, rows * cell / dpi.DpiScaleY));
    }
}
