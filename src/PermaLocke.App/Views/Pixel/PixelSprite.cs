using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// A sprite out of the cartridge drawn the way the scenes draw theirs: every pixel a whole number of screen pixels,
/// optionally as a silhouette, with a one-cell outline, and bobbing like the party menu.
/// </summary>
/// <remarks>
/// An <c>Image</c> with a <c>ScaleTransform</c> of 2 is 2.5 screen pixels per pixel at 125 % scaling, so every other
/// column comes out one pixel wider. This one rounds the cell to whole pixels first, like <see cref="PixelText"/>.
/// The silhouette and the outline are baked into a bitmap because WPF's own ways of tinting an image
/// (<c>OpacityMask</c> with an <c>ImageBrush</c>) do not draw anything (CLAUDE.md, §31).
/// </remarks>
public sealed class PixelSprite : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(BitmapSource), typeof(PixelSprite),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Changed));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(int), typeof(PixelSprite),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SilhouetteProperty = DependencyProperty.Register(
        nameof(Silhouette), typeof(Color), typeof(PixelSprite),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender, Changed));

    public static readonly DependencyProperty OutlineProperty = DependencyProperty.Register(
        nameof(Outline), typeof(Color), typeof(PixelSprite),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender, Changed));

    public static readonly DependencyProperty TrimEmptyProperty = DependencyProperty.Register(
        nameof(TrimEmpty), typeof(bool), typeof(PixelSprite),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Changed));

    public static readonly DependencyProperty StoneProperty = DependencyProperty.Register(
        nameof(Stone), typeof(bool), typeof(PixelSprite),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, Changed));

    public static readonly DependencyProperty BobProperty = DependencyProperty.Register(
        nameof(Bob), typeof(bool), typeof(PixelSprite),
        new FrameworkPropertyMetadata(false, (d, _) => ((PixelSprite)d).OnBobChanged()));

    private DispatcherTimer? _timer;
    private bool _up;
    private BitmapSource? _baked;
    private int _bakedWidth;
    private int _bakedHeight;

    public PixelSprite()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Unloaded += (_, _) => StopBobbing();
        Loaded += (_, _) => OnBobChanged();
    }

    public BitmapSource? Source
    {
        get => (BitmapSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Cells per sprite pixel before the screen scaling: 1 as the cartridge draws it, 2 doubled.</summary>
    public int Scale
    {
        get => (int)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Every visible pixel in this colour, for what has not been won yet; transparent for the sprite itself.</summary>
    public Color Silhouette
    {
        get => (Color)GetValue(SilhouetteProperty);
        set => SetValue(SilhouetteProperty, value);
    }

    /// <summary>A one-cell border around the drawing, the way the scenes pick a sprite out; transparent for none.</summary>
    public Color Outline
    {
        get => (Color)GetValue(OutlineProperty);
        set => SetValue(OutlineProperty, value);
    }

    /// <summary>Leaves out the empty rows and columns around the drawing, so it lines up by what is drawn.</summary>
    public bool TrimEmpty
    {
        get => (bool)GetValue(TrimEmptyProperty);
        set => SetValue(TrimEmptyProperty, value);
    }

    /// <summary>
    /// Carved in stone: the drawing kept, the colour gone to the grey violet of the frame. How the fallen are drawn,
    /// the same as the figurines on the shelf of the trainer's room (§169).
    /// </summary>
    public bool Stone
    {
        get => (bool)GetValue(StoneProperty);
        set => SetValue(StoneProperty, value);
    }

    public bool Bob
    {
        get => (bool)GetValue(BobProperty);
        set => SetValue(BobProperty, value);
    }

    private static readonly Color StoneDark = Color.FromRgb(0x2E, 0x28, 0x44);
    private static readonly Color StoneLight = Color.FromRgb(0xB4, 0xAC, 0xC8);

    private int Cell
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return Math.Max(Scale, (int)Math.Floor((Scale * dpi.DpiScaleX) + 0.5));
        }
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PixelSprite)d)._baked = null;

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

    /// <summary>The sprite with its silhouette and outline applied, one bitmap pixel per sprite pixel.</summary>
    private bool Bake()
    {
        if (_baked is not null) return true;
        if (Source is not { } source || source.PixelWidth == 0 || source.PixelHeight == 0) return false;

        var converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var pixels = new byte[width * height * 4];
        converted.CopyPixels(pixels, width * 4, 0);

        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && pixels[((y * width) + x) * 4 + 3] >= 128;

        var (left, top, right, bottom) = (0, 0, width - 1, height - 1);
        if (TrimEmpty)
        {
            (left, top, right, bottom) = (width, height, -1, -1);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!Solid(x, y)) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right < left) return false;
        }

        var border = Outline.A > 0 ? 1 : 0;
        _bakedWidth = right - left + 1 + (border * 2);
        _bakedHeight = bottom - top + 1 + (border * 2);
        var canvas = new ToastPixels.Canvas(_bakedWidth, _bakedHeight);

        for (var y = top - border; y <= bottom + border; y++)
        {
            for (var x = left - border; x <= right + border; x++)
            {
                var cx = x - left + border;
                var cy = y - top + border;

                if (Solid(x, y))
                {
                    var at = ((y * width) + x) * 4;
                    var colour = Color.FromRgb(pixels[at + 2], pixels[at + 1], pixels[at]);
                    if (Silhouette.A > 0)
                    {
                        colour = Silhouette;
                    }
                    else if (Stone)
                    {
                        // A cuatro tonos, como una figura tallada: el dibujo se lee, el color no.
                        var grey = ((0.3 * colour.R) + (0.59 * colour.G) + (0.11 * colour.B)) / 255.0;
                        colour = ToastPixels.Mix(StoneDark, StoneLight, Math.Round(grey * 3) / 3);
                    }
                    canvas.Put(cx, cy, colour);
                }
                else if (border == 1 && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)))
                {
                    canvas.Put(cx, cy, Outline);
                }
            }
        }

        _baked = canvas.ToBitmap();
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!Bake()) return new Size(0, 0);

        var dip = Cell / VisualTreeHelper.GetDpi(this).DpiScaleX;
        return new Size(_bakedWidth * dip, (_bakedHeight + 1) * dip);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!Bake()) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = Cell;
        var top = _up ? 0 : cell;
        drawingContext.DrawImage(_baked, new Rect(
            0, top / dpi.DpiScaleY, _bakedWidth * cell / dpi.DpiScaleX, _bakedHeight * cell / dpi.DpiScaleY));
    }
}
