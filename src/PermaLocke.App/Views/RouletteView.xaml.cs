using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The wheel's animation. The view owns the how; the view model owns the when.
/// </summary>
/// <remarks>
/// Nothing here decides anything: by the time the wheel turns, the spin has already been written
/// into the save and recorded. The easing is a plain deceleration so the last turn is slow enough
/// to read the wedges going past.
/// </remarks>
public partial class RouletteView : UserControl
{
    private RouletteViewModel? _model;

    /// <summary>Which wedge was under the marker last frame, to notice when the next one arrives.</summary>
    private int _lastWedge = -1;

    private bool _watching;

    public RouletteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SpinTheHub();
        Unloaded += (_, _) =>
        {
            Detach();
            StopWatching();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        if (DataContext is RouletteViewModel model)
        {
            _model = model;
            model.SpinRequested += OnSpinRequested;
            model.RevealRequested += OnRevealRequested;
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.SpinRequested -= OnSpinRequested;
            _model.RevealRequested -= OnRevealRequested;
            _model = null;
        }
    }

    /// <summary>
    /// The hub turns the other way, for ever and very slowly.
    /// </summary>
    /// <remarks>
    /// Half a turn a minute, which is under the speed at which motion draws the eye: what it buys
    /// is that the screen is never completely dead while the player reads the sixteen faces. Going
    /// against the wheel is deliberate — turning with it, it would just look like part of the wheel.
    /// </remarks>
    private void SpinTheHub() => HubSpin.BeginAnimation(RotateTransform.AngleProperty,
        new DoubleAnimation
        {
            From = 0,
            To = -360,
            Duration = TimeSpan.FromSeconds(120),
            RepeatBehavior = RepeatBehavior.Forever
        });

    /// <summary>
    /// Turns a wedge's label over as it is revealed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In code because of what a scale in the template costs. The transform has to be built here
    /// and assigned fresh: a <see cref="ScaleTransform"/> written into a DataTemplate can be frozen
    /// by WPF, and an animation on a frozen Freezable throws. That is the actual bug this replaces,
    /// and it did not fail at build time or at startup â it fired on the first wedge of a real spin.
    /// </para>
    /// <para>
    /// It scales the text and not the container: the container already carries the counter-rotation
    /// that keeps the label the right way up, and the two compose without either knowing about the
    /// other.
    /// </para>
    /// </remarks>
    private void OnRevealRequested(object? sender, int index)
    {
        if (Labels.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container
            || FindText(container) is not { } text)
        {
            return;
        }

        var flip = new ScaleTransform(0, 1);
        text.RenderTransformOrigin = new Point(0.5, 0.5);
        text.RenderTransform = flip;

        flip.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450),
            EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>The label's TextBlock, wherever the template put it.</summary>
    private static TextBlock? FindText(DependencyObject from)
    {
        if (from is TextBlock found)
        {
            return found;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            if (FindText(VisualTreeHelper.GetChild(from, i)) is { } text)
            {
                return text;
            }
        }

        return null;
    }

    private void OnSpinRequested(object? sender, SpinTheWheel request)
    {
        // Se parte del ángulo en el que quedó la vez anterior, así que la rueda no da un salto
        // antes de empezar a girar; y se llega a una POSICIÓN, no se avanza una distancia. Sumar
        // un recorrido fijo a donde estuviera hacía que cada tirada heredase el desvío de la
        // anterior: la primera de la sesión caía bien y la segunda media cuña corrida.
        var from = Wrap(WheelSpin.Angle);
        var to = from + (360 * request.Turns) + Wrap(request.FinalAngle - from);

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = request.Duration,
            // Potencia 5 en vez de un cubico: frena antes y se arrastra al final, que es donde
            // esta la gracia -- ver pasar las cunas una a una y poder leerlas.
            EasingFunction = new PowerEase { Power = 5, EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            StopWatching();
            Land(to);
            request.Stopped();
        };

        StartWatching();
        WheelSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    /// <summary>An angle brought into [0, 360). C#'s remainder keeps the sign of the dividend, so
    /// a plain % leaves negatives negative and the sweep short by a turn.</summary>
    private static double Wrap(double angle) => ((angle % 360) + 360) % 360;

    /// <summary>Watches the wheel go past so the marker can be knocked by each wedge.</summary>
    /// <remarks>
    /// Per frame and not on a timer, because the whole point is the last two seconds, when the
    /// wedges arrive further and further apart: a fixed cadence would tick at the wrong moments
    /// exactly where anybody is looking.
    /// </remarks>
    private void StartWatching()
    {
        if (_watching)
        {
            return;
        }

        _lastWedge = -1;
        _watching = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopWatching()
    {
        if (_watching)
        {
            CompositionTarget.Rendering -= OnFrame;
            _watching = false;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // Sesenta grados por cuña. El módulo se corrige porque el ángulo crece sin límite y en C#
        // el resto de un negativo es negativo.
        var angle = WheelSpin.Angle % 360;
        var wedge = (int)Math.Floor(((angle + 360) % 360) / 60);

        if (wedge == _lastWedge)
        {
            return;
        }

        if (_lastWedge >= 0)
        {
            Kick();
        }

        _lastWedge = wedge;
    }

    /// <summary>A short flick of the marker, as if a wedge had just pushed past it.</summary>
    private void Kick() => MarkerKick.BeginAnimation(RotateTransform.AngleProperty,
        new DoubleAnimation
        {
            From = 0,
            To = 13,
            Duration = TimeSpan.FromMilliseconds(70),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

    /// <summary>
    /// What happens the instant it stops: a wave off the hub, and the wheel rocking back.
    /// </summary>
    /// <remarks>
    /// The rock is two and a half degrees, which is a twenty-fourth of a wedge — enough to read as
    /// weight settling, nowhere near enough to leave the winning wedge off the marker.
    /// </remarks>
    private void Land(double settled)
    {
        Landing.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0.85,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(620),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        var grow = new DoubleAnimation
        {
            From = 0.3,
            To = 2.3,
            Duration = TimeSpan.FromMilliseconds(620),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        LandingScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        LandingScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

        WheelSpin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
        {
            From = settled,
            To = settled + 2.5,
            Duration = TimeSpan.FromMilliseconds(150),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
