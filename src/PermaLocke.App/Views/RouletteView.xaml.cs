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

    /// <summary>Where the wheel was last frame, to know how fast it is actually going.</summary>
    private double _lastAngle = double.NaN;

    /// <summary>
    /// Degrees per frame below which the panel starts taking the colour of the passing wedge.
    /// </summary>
    /// <remarks>
    /// Four degrees a frame is a wedge every quarter of a second, which is about as fast as a
    /// colour change can arrive and still read as one. Faster than that it would be a strobe, and
    /// it would queue an opacity animation per frame for nothing.
    /// </remarks>
    private const double SlowEnough = 4;

    /// <summary>How long the wheel takes to settle back onto the winner after carrying past it.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(420);

    public RouletteView()
    {
        InitializeComponent();
        BuildBulbs();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            Detach();
            StopWatching();
        };
    }

    /// <summary>How many lamps go round the rim.</summary>
    /// <remarks>
    /// Twenty and not eighteen so they are evenly spaced without ever lining up with a seam: six
    /// wedges do not divide twenty, so no lamp sits exactly on a join. The lamps are on the frame
    /// and the frame does not turn, so this is only about how it looks standing still.
    /// </remarks>
    private const int Bulbs = 20;

    /// <summary>
    /// The lamps around the gold band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built here rather than written into the XAML for the same reason the wedges are computed
    /// and not drawn by hand: twenty circles on a circumference is twenty chances to mistype a
    /// coordinate, and one lamp four pixels out of line is exactly the sort of thing that is
    /// obvious on screen and invisible in the markup.
    /// </para>
    /// <para>
    /// Each lamp is a radial gradient and not a flat circle with a glow effect: twenty
    /// <see cref="System.Windows.Media.Effects.DropShadowEffect"/> instances would be twenty
    /// render passes every frame of a twelve second spin, and the wheel behind them is already
    /// animating. The brushes are shared and frozen, so the whole ring is one brush and twenty
    /// shapes.
    /// </para>
    /// </remarks>
    private void BuildBulbs()
    {
        // El radio es el centro de la banda dorada: 740 por fuera y 666 el labio interior, o sea
        // que la banda va de 333 a 370 y su centro cae en 351.
        const double stage = 740, ring = 351, size = 21;

        var glass = new RadialGradientBrush(
            (Color)ColorConverter.ConvertFromString("#FFFFFFFF"),
            TryColour("WheelBulbColor", Colors.Cornsilk))
        {
            GradientOrigin = new Point(0.35, 0.3),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.6,
            RadiusY = 0.6
        };

        var rim = new SolidColorBrush(TryColour("WheelGoldDeepColor", Colors.DarkGoldenrod));

        glass.Freeze();
        rim.Freeze();

        for (var i = 0; i < Bulbs; i++)
        {
            var radians = ((i * 360.0 / Bulbs) - 90) * Math.PI / 180;

            var lamp = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = glass,
                Stroke = rim,
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(lamp, (stage / 2) + (ring * Math.Cos(radians)) - (size / 2));
            Canvas.SetTop(lamp, (stage / 2) + (ring * Math.Sin(radians)) - (size / 2));

            Lamps.Children.Add(lamp);
        }
    }

    /// <summary>A colour from the theme, or a stand-in.</summary>
    /// <remarks>
    /// Same rule as the wedges: a fallback that is obviously not the theme rather than a guessed
    /// palette that looks deliberate.
    /// </remarks>
    private static Color TryColour(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) is Color found ? found : fallback;

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
    /// Starts the spin: one sweep onto the winner, and nothing after it but the settle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It lands on a POSITION and does not travel a distance, which is the whole point. It used to
    /// be handed the whole sweep — eleven turns minus an offset — and added it to wherever the
    /// wheel happened to be, so the first spin of a session looked perfect and every one after it
    /// carried the previous spin's error.
    /// </para>
    /// <para>
    /// One sweep and not the staged notches it had before. Those planted the wheel two or three
    /// wedges short and walked it in, which read as the wheel stopping on one face and then
    /// changing its mind — «se ha parado en el medio de IV AL MÁXIMO y ha pasado a la siguiente».
    /// Where it stops, it stopped: from the moment the wheel is slow enough to read, the wedge
    /// under the marker is the winner.
    /// </para>
    /// </remarks>
    private void OnSpinRequested(object? sender, SpinTheWheel request)
    {
        var ending = request.Ending;
        var from = Wrap(WheelSpin.Angle);

        var resting = from + (360 * (request.Turns + ending.ExtraTurns))
                      + Wrap(request.FinalAngle - from);

        // El barrido va un pelin MAS ALLA del ganador cuando el perfil lleva rebote, y ese pelin
        // esta acotado a menos de media cuña, asi que la marca no se sale de la ganadora ni en el
        // punto mas lejano. Sin rebote, va exactamente al ganador y ahi se queda.
        var target = resting + ending.Bounce;

        var sweep = ending.Bounce > 0
            ? request.Duration - SettleTime
            : request.Duration;

        var animation = new DoubleAnimation
        {
            From = from,
            To = target,
            Duration = sweep,
            EasingFunction = new PowerEase { Power = ending.Power, EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            // Se quita la animación y se deja el ángulo puesto a mano: lo que venga después
            // partiría si no de una propiedad que todavía está animada.
            WheelSpin.BeginAnimation(RotateTransform.AngleProperty, null);
            WheelSpin.Angle = target;

            if (ending.Bounce > 0)
            {
                Settle(resting, request);
            }
            else
            {
                Finish(resting, request);
            }
        };

        Douse();
        StartWatching();
        WheelSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    /// <summary>The last few degrees back onto the winner, like a wheel dropping into its notch.</summary>
    private void Settle(double resting, SpinTheWheel request)
    {
        var back = new DoubleAnimation
        {
            From = WheelSpin.Angle,
            To = resting,
            Duration = SettleTime,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };

        back.Completed += (_, _) =>
        {
            WheelSpin.BeginAnimation(RotateTransform.AngleProperty, null);
            WheelSpin.Angle = resting;
            Finish(resting, request);
        };

        WheelSpin.BeginAnimation(RotateTransform.AngleProperty, back);
    }

    private void Finish(double resting, SpinTheWheel request)
    {
        StopWatching();
        Land(resting);
        request.Stopped();
    }

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
        _lastAngle = double.NaN;
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
        var angle = WheelSpin.Angle;
        var wedge = Under(angle);

        // Cuanto se ha movido desde el fotograma anterior, que es la velocidad de verdad. Antes
        // esto era una bandera que se encendia al acabar el primer tramo; sin tramos hay que
        // preguntarselo a la rueda, y de paso sale mejor: el ambiente entra cuando la rueda va
        // despacio, y no cuando a un perfil le tocaba decir que iba despacio.
        var moved = double.IsNaN(_lastAngle) ? double.MaxValue : Math.Abs(angle - _lastAngle);
        _lastAngle = angle;

        if (wedge == _lastWedge)
        {
            return;
        }

        if (_lastWedge >= 0)
        {
            Kick();
        }

        _lastWedge = wedge;

        if (moved <= SlowEnough)
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
