using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// One card out of the album and in the hand (§186, §187): it flies out of its pocket spinning once, lands big with a
/// burst if it is rare, leans towards the mouse so its foil catches the light, and turns over in 3D with a click.
/// </summary>
/// <remarks>
/// <para>
/// The drawing is <see cref="HandScene"/>'s, at the screen's own resolution; here only what moves it. The tilt follows
/// the mouse through a spring, so the card lags a little and settles instead of snapping; with the mouse away it sways
/// by itself, as if held. The turn over is half a turn with a small overshoot.
/// </para>
/// <para>
/// Which face is up is only how the card is being looked at: a new card always comes out face up.
/// </para>
/// </remarks>
public sealed class CardStage : ContentControl
{
    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(
        nameof(Card), typeof(TcgCard), typeof(CardStage),
        new PropertyMetadata(null, (d, _) => ((CardStage)d).OnCardChanged()));

    /// <summary>How long flying out of the pocket takes.</summary>
    private const double FlyLength = 0.62;

    /// <summary>How long turning the card over takes.</summary>
    private const double FlipLength = 0.58;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private HandScene? _scene;
    private WriteableBitmap? _bitmap;
    private TcgRender? _front;
    private TcgRender? _back;
    private double _dpiX = 1;
    private double _dpiY = 1;

    private (Rect Rect, Visual Relative)? _from;
    private (Rect Rect, Visual Relative)? _pending;
    private Rect? _flightFrom;
    private double _arrivedAt = -99;
    private double _flipFrom;
    private double _flipTo;
    private double _flipStart = -99;
    private double _yaw;
    private double _pitch;
    private double _yawSpeed;
    private double _pitchSpeed;
    private Point? _mouse;
    private double _lastTime;
    private long _lastStep = -1;
    private bool _broken;

    /// <summary>What a frame costs to paint, in milliseconds, averaged over the last ones.</summary>
    private double _paintCost = 8;

    /// <summary>At 60 frames a second instead of 30, while this computer paints one quickly enough (§188).</summary>
    private bool _smooth;

    public CardStage()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
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

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>
    /// Where the next card comes from: its pocket in the album, in the coordinates of <paramref name="relativeTo"/>.
    /// Without it the card arrives without flying, as when going from card to card with the arrows.
    /// </summary>
    /// <remarks>Turned into this control's coordinates on the first frame it is on screen: it may still be hidden now.</remarks>
    public void FlyFrom(Rect pocket, Visual relativeTo) => _from = (pocket, relativeTo);

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnCardChanged()
    {
        _flipFrom = 0;
        _flipTo = 0;
        _flipStart = -99;

        if (Card is { } card)
        {
            _front = TcgCardArt.Render(card, TcgLayout.Full);
            _back = card.Egg ? TcgCardArt.RenderBack(TcgLayout.Full) : TcgCardArt.Render(card, TcgLayout.Full, back: true);

            // Sale volando desde su funda si se sabe cuál; si se pasa de carta con las flechas, llega sin volar.
            _pending = _from;
            _flightFrom = null;
            _from = null;
            _arrivedAt = Now + (_pending is null ? 0 : FlyLength);
            _yaw = 0;
            _pitch = 0;
            _yawSpeed = 0;
            _pitchSpeed = 0;
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
        _dpiX = dpi.DpiScaleX;
        _dpiY = dpi.DpiScaleY;
        var width = (int)Math.Floor(ActualWidth * _dpiX);
        var height = (int)Math.Floor(ActualHeight * _dpiY);

        if (_scene is null || _scene.Width != width || _scene.Height != height)
        {
            _scene = new HandScene(width, height);
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            _image.Source = _bitmap;
            _image.Width = width / _dpiX;
            _image.Height = height / _dpiY;
        }

        Paint();
    }

    /// <summary>Screen pixels per cell of the card, whole, as big as fits with room for the tilt and the shadow.</summary>
    private int RestScale()
    {
        if (_scene is null) return 1;
        var fit = Math.Min(_scene.Width * 0.78 / TcgCardArt.FullWidth, _scene.Height * 0.84 / TcgCardArt.FullHeight);
        return Math.Clamp((int)Math.Floor(fit), 2, 6);
    }

    private (double X, double Y) RestCentre() => _scene is null ? (0, 0) : (_scene.Width / 2.0, _scene.Height * 0.47);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var at = e.GetPosition(_image);
        _mouse = new Point(at.X * _dpiX, at.Y * _dpiY);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _mouse = null;
    }

    /// <summary>Whether a screen pixel is on the card as it lies at rest: the part of the screen that is the card's.</summary>
    private bool OverCard(Point pixel)
    {
        var scale = RestScale();
        var (cx, cy) = RestCentre();
        return Math.Abs(pixel.X - cx) < TcgCardArt.FullWidth * scale / 2.0 * 1.05
               && Math.Abs(pixel.Y - cy) < TcgCardArt.FullHeight * scale / 2.0 * 1.05;
    }

    /// <summary>A press on the card is the card's: it must not reach whoever closes the hand on a click outside.</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (Card is not null && _mouse is { } mouse && OverCard(mouse))
        {
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (Card is null || _mouse is not { } mouse || !OverCard(mouse))
        {
            return;
        }

        // Media vuelta más desde donde esté, aunque la anterior no haya terminado.
        _flipFrom = FlipAngle(Now);
        _flipTo += Math.PI;
        _flipStart = Now;
        e.Handled = true;
    }

    /// <summary>The half turns done so far, with a small overshoot at the end of each, like a card flicked over.</summary>
    private double FlipAngle(double now)
    {
        var t = (now - _flipStart) / FlipLength;
        if (t >= 1)
        {
            return _flipTo;
        }

        return _flipFrom + ((_flipTo - _flipFrom) * BackOut(Math.Max(0, t), 1.4));
    }

    /// <summary>Eases out past the end and back: the little bounce of something that lands.</summary>
    private static double BackOut(double t, double overshoot)
    {
        var u = t - 1;
        return 1 + (u * u * (((overshoot + 1) * u) + overshoot));
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // A 60 imágenes por segundo si pintar una cuesta poco en este ordenador; si no, a 30. Con margen entre los dos
        // umbrales para no saltar de uno a otro a cada rato.
        _smooth = _smooth ? _paintCost < 9 : _paintCost < 6;
        var step = _clock.ElapsedMilliseconds / (_smooth ? 16 : 33);
        if (IsVisible && step != _lastStep)
        {
            _lastStep = step;
            var started = Stopwatch.GetTimestamp();
            Paint();
            _paintCost = (_paintCost * 0.9) + (Stopwatch.GetElapsedTime(started).TotalMilliseconds * 0.1);
        }
    }

    private void Paint()
    {
        if (_broken || _scene is null || _bitmap is null)
        {
            return;
        }

        try
        {
            if (_front is not { } front || _back is not { } back || Card is not { } card)
            {
                _scene.Clear();
                _bitmap.WritePixels(new Int32Rect(0, 0, _scene.Width, _scene.Height), _scene.Pixels, _scene.Width * 4, 0);
                return;
            }

            var now = Now;
            var dt = Math.Clamp(now - _lastTime, 0, 0.1);
            _lastTime = now;

            // La funda de donde sale, en coordenadas propias, en cuanto esta capa está en pantalla.
            if (_pending is { } pending && IsVisible)
            {
                _pending = null;
                try
                {
                    _flightFrom = new Rect(pending.Relative.TransformToVisual(this).Transform(pending.Rect.TopLeft), pending.Rect.Size);
                }
                catch (InvalidOperationException)
                {
                    // Si no se puede situar, llega sin volar.
                    _arrivedAt = now;
                }
            }

            // Hacia dónde quiere inclinarse: hacia el ratón si está encima; si no, se mece sola, como sujeta en la mano.
            var scale = RestScale();
            var (cx, cy) = RestCentre();
            double targetYaw;
            double targetPitch;
            if (_mouse is { } mouse && OverCard(mouse))
            {
                var mx = Math.Clamp((mouse.X - cx) / (TcgCardArt.FullWidth * scale / 2.0), -1, 1);
                var my = Math.Clamp((mouse.Y - cy) / (TcgCardArt.FullHeight * scale / 2.0), -1, 1);
                targetYaw = -mx * 0.42;
                targetPitch = my * 0.3;
            }
            else
            {
                targetYaw = Math.Sin(now * 0.9) * 0.07;
                targetPitch = Math.Cos(now * 0.7) * 0.05;
            }

            // Un muelle amortiguado: se queda un poco atrás y se asienta sin golpe.
            const double stiffness = 70;
            const double damping = 11;
            _yawSpeed += (((targetYaw - _yaw) * stiffness) - (_yawSpeed * damping)) * dt;
            _pitchSpeed += (((targetPitch - _pitch) * stiffness) - (_pitchSpeed * damping)) * dt;
            _yaw += _yawSpeed * dt;
            _pitch += _pitchSpeed * dt;

            var pose = new HandPose(_yaw + FlipAngle(now), _pitch, 0, scale, cx, cy, 0.35);

            // Saliendo de su funda: vuela en arco, crece, gira una vez entera y se asienta con un poco de rebote.
            var flight = 1 - ((_arrivedAt - now) / FlyLength);
            if (flight < 1 && _flightFrom is { } from)
            {
                var p = Math.Clamp(flight, 0, 1);
                var fromCx = (from.X + (from.Width / 2)) * _dpiX;
                var fromCy = (from.Y + (from.Height / 2)) * _dpiY;
                var fromScale = Math.Max(0.5, from.Height * _dpiY / TcgCardArt.FullHeight);
                var move = 1 - Math.Pow(1 - p, 3);
                var grow = BackOut(p, 1.5);

                pose = pose with
                {
                    CentreX = fromCx + ((cx - fromCx) * move),
                    CentreY = fromCy + ((cy - fromCy) * move) - (Math.Sin(p * Math.PI) * scale * 18),
                    Scale = Math.Max(0.5, fromScale + ((scale - fromScale) * grow)),
                    Yaw = pose.Yaw + ((1 - move) * Math.PI * 2),
                    Roll = Math.Sin(p * Math.PI) * 0.22 * (1 - p),
                    Lift = 1 - (0.65 * p)
                };
            }

            _scene.Render(front, back, pose, now, card.Seed, now - _arrivedAt);

            var (x, y, w, h) = _scene.Dirty;
            if (w > 0 && h > 0)
            {
                _bitmap.WritePixels(new Int32Rect(x, y, w, h), _scene.Pixels, _scene.Width * 4, ((y * _scene.Width) + x) * 4);
            }
        }
        catch (Exception ex)
        {
            // Dibujar no puede tumbar la pantalla: se avisa una vez y la carta se queda quieta.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
