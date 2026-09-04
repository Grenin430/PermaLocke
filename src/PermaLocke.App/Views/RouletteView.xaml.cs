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
/// into the save and recorded. What the view chooses is only how it is shown — and it chooses that
/// from a seeded profile, so the same spin always looks the same and can be replayed.
/// </remarks>
public partial class RouletteView : UserControl
{
    private RouletteViewModel? _model;

    /// <summary>Which wedge was under the marker last frame, to notice when the next one arrives.</summary>
    private int _lastWedge = -1;

    private bool _watching;

    /// <summary>
    /// True once the long sweep is over and the wheel is going notch by notch.
    /// </summary>
    /// <remarks>
    /// The panel only takes the colour of the passing wedge from here on. During the sweep a wedge
    /// goes by every fifty milliseconds, so tinting there would be a strobe and would also queue an
    /// opacity animation per frame for nothing.
    /// </remarks>
    private bool _settling;

    public RouletteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
    /// Turns a wedge's label over as it is revealed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In code because of what a scale in the template costs. The transform has to be built here
    /// and assigned fresh: a <see cref="ScaleTransform"/> written into a DataTemplate can be frozen
    /// by WPF, and an animation on a frozen Freezable throws. That is the actual bug this replaces,
    /// and it did not fail at build time or at startup — it fired on the first wedge of a real spin.
    /// </para>
    /// <para>
    /// It scales the content and not the container: the container already carries the
    /// counter-rotation that keeps the label the right way up, and the two compose without either
    /// knowing about the other.
    /// </para>
    /// </remarks>
    private void OnRevealRequested(object? sender, int index)
    {
        // La primera cuna de una tirada nueva apaga el ambiente de la anterior. Si no, el panel
        // se quedaria del color del resultado pasado mientras se desvela la rueda de ahora.
        if (index == 0)
        {
            Douse();
        }

        if (Labels.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container
            || FindContent(container) is not { } content)
        {
            return;
        }

        var flip = new ScaleTransform(0, 1);
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        content.RenderTransform = flip;

        flip.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450),
            EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>
    /// The panel that holds the wedge's picture, figure and label, wherever the template put it.
    /// </summary>
    /// <remarks>
    /// It used to look for the TextBlock, back when a wedge was one line of text. Now it is a stack
    /// of three things and flipping only the last of them would turn the label over while the
    /// picture above it sat still.
    /// </remarks>
    private static FrameworkElement? FindContent(DependencyObject from)
    {
        if (from is StackPanel found)
        {
            return found;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            if (FindContent(VisualTreeHelper.GetChild(from, i)) is { } content)
            {
                return content;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts the spin: one long sweep and then the profile's notches, one wedge at a time.
    /// </summary>
    /// <remarks>
    /// It lands on a POSITION and does not travel a distance, which is the whole point. It used to
    /// be handed the whole sweep — eleven turns minus an offset — and added it to wherever the
    /// wheel happened to be, so the first spin of a session looked perfect and every one after it
    /// carried the previous spin's error.
    /// </remarks>
    private void OnSpinRequested(object? sender, SpinTheWheel request)
    {
        var from = Wrap(WheelSpin.Angle);
        var resting = from + (360 * request.Turns) + Wrap(request.FinalAngle - from);

        var angles = request.Ending.Angles(resting).ToArray();
        var legs = request.Ending.Legs(request.Duration).ToArray();

        _settling = false;
        Douse();
        StartWatching();
        RunLeg(0, from, angles, legs, request);
    }

    /// <summary>
    /// One leg of the ending, which then starts the next.
    /// </summary>
    /// <remarks>
    /// Chained rather than written as one storyboard with keyframes because a leg can go
    /// <b>backwards</b> — the false finish overshoots the winner by a whole wedge and the wheel is
    /// pulled back — and each leg wants its own easing.
    /// </remarks>
    private void RunLeg(int index, double from, double[] angles, TimeSpan[] legs, SpinTheWheel request)
    {
        var to = angles[index];
        var last = index == angles.Length - 1;

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = legs[index],
            EasingFunction = EaseFor(index, last, request.Ending.Overshoot)
        };

        animation.Completed += (_, _) =>
        {
            // Se quita la animación y se deja el ángulo puesto a mano: un tramo que arrancara de
            // una propiedad todavía animada partiría de un valor que está a punto de cambiar.
            WheelSpin.BeginAnimation(RotateTransform.AngleProperty, null);
            WheelSpin.Angle = to;

            if (last)
            {
                StopWatching();
                Land(to);
                request.Stopped();
                return;
            }

            // A partir del primer golpe la rueda ya va cuña a cuña, que es cuando el ambiente
            // puede seguirla sin convertirse en un parpadeo.
            _settling = true;
            RunLeg(index + 1, to, angles, legs, request);
        };

        WheelSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    /// <summary>How each leg decelerates.</summary>
    /// <remarks>
    /// The long sweep gets a fifth power: it brakes early and then crawls, which is where the
    /// interest is — seeing the wedges go past one at a time and being able to read them. The last
    /// notch of an overshooting profile uses a <see cref="BackEase"/>, which goes a touch past the
    /// mark and settles back on its own; that is a real wheel bouncing off the pawl and it costs
    /// nothing to write.
    /// </remarks>
    private static IEasingFunction EaseFor(int index, bool last, bool overshoot) =>
        index == 0 ? new PowerEase { Power = 5, EasingMode = EasingMode.EaseOut }
        : last && overshoot ? new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut }
        : last ? new QuarticEase { EasingMode = EasingMode.EaseOut }
        : new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>An angle brought into [0, 360). C#'s remainder keeps the sign of the dividend, so
    /// a plain % leaves negatives negative and the sweep short by a turn.</summary>
    private static double Wrap(double angle) => ((angle % 360) + 360) % 360;

    /// <summary>Watches the wheel go past so the marker can be knocked by each wedge.</summary>
    /// <remarks>
    /// Per frame and not on a timer, because the whole point is the last seconds, when the wedges
    /// arrive further and further apart: a fixed cadence would tick at the wrong moments exactly
    /// where anybody is looking.
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
        var wedge = Under(WheelSpin.Angle);

        if (wedge == _lastWedge)
        {
            return;
        }

        if (_lastWedge >= 0)
        {
            Kick();
        }

        _lastWedge = wedge;

        if (_settling)
        {
            Tint(wedge, 0.55);
        }
    }

    /// <summary>
    /// Which wedge is under the marker at a given rotation.
    /// </summary>
    /// <remarks>
    /// The marker sits at twelve, so it looks at the wheel's own angle <c>-A</c>, and each wedge
    /// owns sixty degrees from its index. The old version measured which sixty degree sector the
    /// <b>rotation</b> was in, which changes just as often and was fine for punching the marker —
    /// but it is not a wedge index, and the panel now needs to ask that wedge whether it pays or
    /// costs.
    /// </remarks>
    private static int Under(double angle) => (int)Math.Floor(Wrap(-angle) / WheelEnding.WedgeDegrees);

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
    /// Lights the panel with the colour of the wedge under the marker.
    /// </summary>
    /// <remarks>
    /// Green when it pays, red when it costs, and nothing at all while the wedge is still a
    /// question mark. Going notch by notch this makes the whole panel breathe green, red, green,
    /// red slower and slower, which is the tension the wheel used to keep entirely to itself.
    /// </remarks>
    private void Tint(int wedge, double strength)
    {
        if (_model is null || wedge < 0 || wedge >= _model.Slots.Count)
        {
            return;
        }

        var slot = _model.Slots[wedge];

        if (!slot.Revealed)
        {
            return;
        }

        Fade(slot.Good ? GlowGood : GlowBad, strength);
        Fade(slot.Good ? GlowBad : GlowGood, 0);
    }

    /// <summary>Puts both lights out, for the next spin.</summary>
    private void Douse()
    {
        Fade(GlowGood, 0);
        Fade(GlowBad, 0);
    }

    private static void Fade(UIElement layer, double to) =>
        layer.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

    /// <summary>
    /// What happens the instant it stops: a wave off the hub, the wheel rocking back, and the
    /// winner's colour flooding the panel.
    /// </summary>
    /// <remarks>
    /// The rock is two and a half degrees, which is a twenty-fourth of a wedge — enough to read as
    /// weight settling, nowhere near enough to leave the winning wedge off the marker.
    /// </remarks>
    private void Land(double settled)
    {
        _settling = false;

        // El color del ganador entra de golpe y se queda a media fuerza. A tope se quedaría un
        // filtro de color encima de toda la pantalla mientras se lee la tarjeta.
        Tint(Under(settled), 1);
        Flood(Under(settled));

        Landing.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0.85,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(680),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        var grow = new DoubleAnimation
        {
            From = 0.28,
            To = 2.3,
            Duration = TimeSpan.FromMilliseconds(680),
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

    /// <summary>Holds the winner's colour on the panel once the flash has passed.</summary>
    private void Flood(int wedge)
    {
        if (_model is null || wedge < 0 || wedge >= _model.Slots.Count
            || !_model.Slots[wedge].Revealed)
        {
            return;
        }

        var layer = _model.Slots[wedge].Good ? GlowGood : GlowBad;

        layer.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 1,
            To = 0.45,
            BeginTime = TimeSpan.FromMilliseconds(240),
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
