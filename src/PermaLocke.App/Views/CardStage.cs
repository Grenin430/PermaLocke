using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>
/// One card out of the album and in the hand (§186): big, floating a little over its shadow, and turned over with a
/// click to read its back.
/// </summary>
/// <remarks>
/// The full card as big as whole cells allow, up to five screen pixels per cell. Turning it is the card narrowing to its
/// edge and opening again with the other face, in whole columns like the album's page. Which face shows is only how it
/// is being looked at: a new card always comes out face up.
/// </remarks>
public sealed class CardStage : ContentControl
{
    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(
        nameof(Card), typeof(TcgCard), typeof(CardStage),
        new PropertyMetadata(null, (d, _) => ((CardStage)d).OnCardChanged()));

    /// <summary>How long turning the card over takes.</summary>
    private const double FlipLength = 0.34;

    private const int Pad = 3;
    private const int ShadowRows = 5;

    private static readonly Color Shadow = Rgb(0x06, 0x05, 0x0B);

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CellCanvas _canvas = new(TcgCardArt.FullWidth + (Pad * 2), TcgCardArt.FullHeight + (Pad * 2) + ShadowRows);
    private readonly WriteableBitmap _bitmap;

    private TcgRender? _front;
    private TcgRender? _back;
    private bool _showingBack;
    private double _flipStart = -99;
    private long _lastStep = -1;
    private bool _broken;

    public CardStage()
    {
        _bitmap = new WriteableBitmap(_canvas.Width, _canvas.Height, 96, 96, PixelFormats.Bgra32, null);
        _image.Source = _bitmap;
        _image.Width = _canvas.Width;
        _image.Height = _canvas.Height;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        _image.Cursor = Cursors.Hand;
        Content = new Grid { UseLayoutRounding = true, Background = Brushes.Transparent, Children = { _image } };

        SizeChanged += (_, _) => Reshape();
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>Raised once if drawing fails, so the screen can log it.</summary>
    public event Action<Exception>? RenderFailed;

    public TcgCard? Card
    {
        get => (TcgCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    /// <summary>Whether the back is the face up now, for the hint under the card.</summary>
    public bool ShowingBack => _showingBack;

    private double Now => _clock.Elapsed.TotalSeconds;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnCardChanged()
    {
        _showingBack = false;
        _flipStart = -99;

        if (Card is { } card)
        {
            _front = TcgCardArt.Render(card, TcgLayout.Full);
            _back = card.Egg ? TcgCardArt.RenderBack(TcgLayout.Full) : TcgCardArt.Render(card, TcgLayout.Full, back: true);
        }
        else
        {
            _front = null;
            _back = null;
        }

        Paint();
    }

    private void Reshape()
    {
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var deviceWidth = ActualWidth * dpi.DpiScaleX;
        var deviceHeight = ActualHeight * dpi.DpiScaleY;
        var cell = Math.Clamp((int)Math.Floor(Math.Min(deviceWidth / _canvas.Width, deviceHeight / _canvas.Height)), 1, 5);

        _zoom.ScaleX = cell / dpi.DpiScaleX;
        _zoom.ScaleY = cell / dpi.DpiScaleY;

        var left = Math.Floor((deviceWidth - (_canvas.Width * cell)) / 2);
        var top = Math.Floor((deviceHeight - (_canvas.Height * cell)) / 2);
        _image.Margin = new Thickness(Math.Max(0, left) / dpi.DpiScaleX, Math.Max(0, top) / dpi.DpiScaleY, 0, 0);
        Paint();
    }

    /// <summary>A press on the card is the card's: it must not reach whoever closes the hand on a click outside.</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (Card is not null && _image.IsMouseOver)
        {
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        // Solo la carta se da la vuelta: pulsar fuera es de quien la contiene (cerrar).
        if (Card is null || !_image.IsMouseOver)
        {
            return;
        }

        if (Now - _flipStart >= FlipLength)
        {
            _flipStart = Now;
            _showingBack = !_showingBack;
        }

        e.Handled = true;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var step = _clock.ElapsedMilliseconds / 33;
        if (IsVisible && step != _lastStep)
        {
            _lastStep = step;
            Paint();
        }
    }

    private void Paint()
    {
        if (_broken)
        {
            return;
        }

        try
        {
            _canvas.Clear();

            if (_front is { } front && _back is { } back && Card is { } card)
            {
                var now = Now;
                var width = TcgCardArt.FullWidth;

                // Flota: sube y baja una celda, despacio, sobre su sombra.
                var bob = (int)(now / 0.9) % 2;
                var top = Pad - bob;

                // Dándose la vuelta: se estrecha hasta el canto con la cara de antes y se abre con la nueva.
                var t = (now - _flipStart) / FlipLength;
                var face = _showingBack ? back : front;
                var shown = width;
                if (t < 1)
                {
                    var closing = t < 0.5;
                    face = closing ? (_showingBack ? front : back) : face;
                    var share = closing ? 1 - (t * 2) : (t - 0.5) * 2;
                    shown = Math.Max(1, (int)Math.Round(width * share));
                }

                var left = Pad + ((width - shown) / 2);

                // La sombra en la mesa, a medio tono, tan ancha como la carta de frente.
                var shadowTop = Pad + TcgCardArt.FullHeight + 1;
                for (var y = shadowTop; y < shadowTop + 3; y++)
                {
                    for (var x = left + 2; x < left + shown - 2; x++)
                    {
                        if (Dither(x, y, bob == 0 ? 0.55 : 0.4))
                        {
                            _canvas.Put(x, y, Shadow);
                        }
                    }
                }

                if (shown == width)
                {
                    _canvas.Stamp(face.Canvas, left, top);
                    TcgCardArt.Animate(face, _canvas, left, top, now, card.Seed);
                }
                else
                {
                    _canvas.StampColumns(face.Canvas, left, top, shown);
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _canvas.Width, _canvas.Height), _canvas.Bgra, _canvas.Width * 4, 0);
        }
        catch (Exception ex)
        {
            // Dibujar no puede tumbar la pantalla: se avisa una vez y la carta se queda quieta.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
