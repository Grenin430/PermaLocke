using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The gacha screen. The only code here drives the reel animation.
/// </summary>
/// <remarks>
/// <para>
/// This is presentation, not logic: it decides nothing and touches no data. It exists because
/// where the reel has to stop depends on the width of the viewport at that moment, and a
/// Storyboard written in XAML cannot know that.
/// </para>
/// <para>
/// The view model says <em>what</em> — which cell wins, how long the spin lasts, which tier the
/// screen is showing — and this says <em>how</em>: acceleration, braking, the clicks at the end
/// and the punch when it lands.
/// </para>
/// </remarks>
public partial class GachaView : UserControl
{
    /// <summary>Width of one reel cell. Fixed in <c>ReelCellTemplate</c>; both must agree.</summary>
    private const double CellWidth = 72;

    /// <summary>
    /// How much longer than the spin the click timeline runs, so the last click has room to bounce.
    /// </summary>
    /// <remarks>
    /// Every ending finishes on the winner, so its last click is at 1.00 of the spin and the
    /// bounce that follows it would land past the end of the timeline. Six per cent of a twelve
    /// second spin is about seven hundred milliseconds, which is more than the thirty thousandths
    /// the bounce needs and short enough that nobody sees the reel waiting.
    /// </remarks>
    private const double ClickTail = 1.06;

    /// <summary>A fraction of the spin, expressed on the slightly longer click timeline.</summary>
    private static KeyTime OnTail(double fraction) =>
        KeyTime.FromPercent(Math.Clamp(fraction / ClickTail, 0, 1));

    /// <summary>
    /// Where the reel stops dead and then advances one cell at a time, as a fraction of the spin.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the ending. A wheel that glides to a halt resolves the roll in
    /// one moment; a wheel that plants itself three cells short and then walks the last three
    /// resolves it three times, and the player reads each Pokémon as it passes under the marker.
    /// The number of clicks is the same for every tier on purpose: making the rare ones click more
    /// would give the result away before the reel gets there.
    /// </remarks>
    private GachaViewModel? _model;

    /// <summary>The background loops, kept so their speed can be changed while they run.</summary>
    private Storyboard? _near;

    private Storyboard? _far;

    private Storyboard? _vortex;

    /// <summary>The tier's colour washed over the tunnel. Built in code so it is never frozen.</summary>
    private RadialGradientBrush? _wash;

    /// <summary>Steps the field back down to its resting speed instead of dropping it.</summary>
    private DispatcherTimer? _settle;


    public GachaView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => StartBackground();
        Loaded += (_, _) => BreatheWhileWaiting();
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        if (e.NewValue is not GachaViewModel model)
        {
            return;
        }

        _model = model;
        model.SpinRequested += OnSpinRequested;
        model.PropertyChanged += OnModelPropertyChanged;
    }

    private void Detach()
    {
        if (_model is null)
        {
            return;
        }

        _model.SpinRequested -= OnSpinRequested;
        _model.PropertyChanged -= OnModelPropertyChanged;
        _model = null;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            // Cada peldaño del engaño se nota: fogonazo, golpe de marcador y sacudida.
            case nameof(GachaViewModel.DisplayedTier) when !string.IsNullOrEmpty(_model.DisplayedTier):
                Dispatcher.BeginInvoke(() =>
                {
                    TierReaction(_model.DisplayedTier);
                    FlashOnce(0.36, TimeSpan.FromSeconds(0.5));
                    KickMarker();
                    Shake(5, 0.3);
                });
                break;

            // Shiny o legendario: una segunda onda encima de la primera. El ruido de más va
            // atado a algo que de verdad ha pasado y es raro, no repartido a voleo.
            case nameof(GachaViewModel.HasResult) when _model.HasResult && _model.HasBadge:
                Dispatcher.BeginInvoke(() =>
                {
                    PlayShockwave(1.25, TimeSpan.FromSeconds(0.3));
                    FlashOnce(0.55, TimeSpan.FromSeconds(1.3), TimeSpan.FromSeconds(0.32));
                    Shake(9, 0.45);
                });
                break;
        }
    }

    private void OnSpinRequested(object? sender, SpinRequest request)
    {
        // La colección acaba de cambiar: hay que dejar que el ItemsControl construya sus celdas
        // antes de medir nada, o el visor todavía mide cero.
        Dispatcher.BeginInvoke(
            () =>
            {
                // Lo de debajo YA HA PASADO: el Pokémon está escrito en la partida y la tirada
                // registrada antes de que se anime un solo fotograma. Así que una animación rota
                // puede costar la animación y nada más.
                //
                // Esto no estaba, y se noto: un KeyTime fuera de rango subia hasta el manejador de
                // la aplicacion, le enseñaba al jugador «ha habido un error inesperado» por algo
                // que habia salido bien, y ademas dejaba al ViewModel esperando a que la rueda
                // parase hasta que saltaba su red de seguridad OCHO SEGUNDOS despues. La misma
                // leccion que ya se aprendio en la ruleta, en la pantalla de al lado.
                try
                {
                    StartSpin(request);
                }
                catch (Exception ex)
                {
                    (DataContext as GachaViewModel)?.AnimationFailed(ex);
                    request.Stopped();
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The tunnel and the vortex: the resting state of the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stars come <b>towards</b> the viewer instead of sliding sideways. Sideways drift reads
    /// as wallpaper passing behind the panel; Ultra Space is somewhere you go into, and the only
    /// thing that changes is which property is animated — same tiles, same cost.
    /// </para>
    /// <para>
    /// The two layers run at <b>different periods</b> rather than the same one offset, because a
    /// tiled layer that fades to nothing and restarts makes the whole field blink in time with
    /// itself. Six and nine and a half seconds never line up, so there is no beat.
    /// </para>
    /// <para>
    /// They are Storyboards and not <c>BeginAnimation</c> calls for one reason: a Storyboard
    /// started as controllable can have its <c>SpeedRatio</c> changed while it runs, which is what
    /// lets the field accelerate with the reel without restarting anything. Restarting would snap
    /// every star back to the centre in the middle of a spin.
    /// </para>
    /// </remarks>
    private void StartBackground()
    {
        _near = Tunnel(StarsNear, 6.0, 2.4, 0.40);
        _far = Tunnel(StarsFar, 9.5, 1.9, 0.26);

        var turn = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(90)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        Storyboard.SetTarget(turn, VortexSpin);
        Storyboard.SetTargetProperty(turn, new PropertyPath(RotateTransform.AngleProperty));

        _vortex = new Storyboard();
        _vortex.Children.Add(turn);
        _vortex.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);

        // El lavado del tier: un degradado radial creado AQUI, para que no venga congelado.
        _wash = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.75,
            RadiusY = 0.75,
        };
        _wash.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        _wash.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        TierWash.Fill = _wash;
    }

    private Storyboard Tunnel(FrameworkElement layer, double seconds, double to, double peak)
    {
        var duration = new Duration(TimeSpan.FromSeconds(seconds));
        var board = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        foreach (var axis in (string[])["ScaleX", "ScaleY"])
        {
            var grow = new DoubleAnimation(1, to, duration);
            Storyboard.SetTarget(grow, layer);
            Storyboard.SetTargetProperty(grow,
                new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{axis})"));
            board.Children.Add(grow);
        }

        // Aparece y se apaga dentro del propio ciclo: una estrella que llega al borde a plena luz
        // y desaparece de golpe delata el bucle.
        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromPercent(0.30)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromPercent(0.68)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        Storyboard.SetTarget(fade, layer);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        board.Children.Add(fade);

        board.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);
        return board;
    }

    /// <summary>
    /// The background answers the tier ladder: the vortex winds up and the tunnel takes its colour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The escalation used to happen entirely in front of the background — flash, marker, shake —
    /// while the vortex turned at the same lazy speed whatever was coming. Tying it in costs
    /// nothing and means a tier five <b>looks</b> different from a tier one before the reel stops,
    /// which is the tease doing its job with the whole panel instead of a corner of it.
    /// </para>
    /// <para>
    /// It reads the step from the portals, which are already ordered by tier, rather than from a
    /// second table that could disagree with them. A tier the portals do not know leaves the
    /// background alone instead of guessing a step.
    /// </para>
    /// </remarks>
    private void TierReaction(string tierId)
    {
        if (_model is null)
        {
            return;
        }

        var step = _model.Portals.ToList().FindIndex(p =>
            string.Equals(p.TierId, tierId, StringComparison.OrdinalIgnoreCase));

        if (step < 0)
        {
            return;
        }

        // Del 1 al 5. El uno casi no se nota y el cinco se nota mucho: es la asimetria que hace
        // que noventa tiradas normales no pesen.
        var rung = step + 1;

        _vortex?.SetSpeedRatio(this, 1 + (rung * 1.6));

        var open = new DoubleAnimation(1 + (rung * 0.045), new Duration(TimeSpan.FromSeconds(0.7)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        VortexScale.BeginAnimation(ScaleTransform.ScaleXProperty, open);
        VortexScale.BeginAnimation(ScaleTransform.ScaleYProperty, open);

        Vortex.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.16 + (rung * 0.045), new Duration(TimeSpan.FromSeconds(0.7))));

        if (_wash is null
            || TryFindResource(_model.Portals[step].BrushKey) is not SolidColorBrush tint)
        {
            return;
        }

        _wash.GradientStops[0].Color = tint.Color;
        _wash.GradientStops[1].Color = Color.FromArgb(0, tint.Color.R, tint.Color.G, tint.Color.B);

        TierWash.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.05 + (rung * 0.035), new Duration(TimeSpan.FromSeconds(0.6))));
    }

    /// <summary>Puts the background back to its resting state after a spin.</summary>
    private void CalmBackground()
    {
        _vortex?.SetSpeedRatio(this, 1);

        var shut = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(2.2)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        VortexScale.BeginAnimation(ScaleTransform.ScaleXProperty, shut);
        VortexScale.BeginAnimation(ScaleTransform.ScaleYProperty, shut);
        Vortex.BeginAnimation(OpacityProperty, new DoubleAnimation(0.16, new Duration(TimeSpan.FromSeconds(2.2))));

        // El lavado se queda un rato con el color del tier que salio, y se va despues: apagarlo a
        // la vez que para la rueda le quitaria el unico eco que deja el resultado en el fondo.
        TierWash.BeginAnimation(OpacityProperty, new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(3.5)))
        {
            BeginTime = TimeSpan.FromSeconds(1.6),
        });
    }

    /// <summary>How fast the field is travelling. One is standing still and watching.</summary>

    private void Field(double ratio)
    {
        _near?.SetSpeedRatio(this, ratio);
        _far?.SetSpeedRatio(this, ratio);
    }

    /// <summary>
    /// Brings the field back down in two steps instead of one.
    /// </summary>
    /// <remarks>
    /// Dropping straight from cruise to rest looks like the animation broke. Two steps read as
    /// something heavy losing its momentum, which is what the reel is doing at the same moment.
    /// </remarks>
    private void SettleField()
    {
        Field(1.9);

        _settle?.Stop();
        _settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _settle.Tick += (_, _) =>
        {
            _settle?.Stop();
            Field(1);
        };
        _settle.Start();
    }

    /// <summary>Whether the strip is already doing something, idle drift included.</summary>
    private bool _reelMoving;

    /// <summary>
    /// Before the first pull the strip drifts, very slowly, and always to the left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to go and come back, because a seamless loop needs the strip to repeat and the
    /// comment here said a change of direction every couple of minutes is not something anybody
    /// sees. Somebody saw it, and it reads as a fault rather than as a flourish: a machine of this
    /// kind turns one way. So the strip now repeats its head at its tail
    /// (<see cref="GachaViewModel.IdleLoopCells"/>) and the drift snaps back to the origin onto
    /// identical cells, which is invisible.
    /// </para>
    /// <para>
    /// Once only, and never again after a pull: when a roll lands, the strip is parked ON the
    /// winner. Drifting away from it afterwards would take the result off the screen — which is
    /// also why a reel built for a roll declares no loop and is left alone here.
    /// </para>
    /// </remarks>
    private void OnStripSized(object sender, SizeChangedEventArgs e)
    {
        if (_reelMoving || _model is null || _model.IdleLoopCells <= 0)
        {
            return;
        }

        var travel = _model.IdleLoopCells * CellWidth;

        // Lo que sobra por detrás de una vuelta es lo que tapa el salto. Si no llega a cubrir el
        // visor -una ventana absurdamente ancha- se vería el corte, así que no se mueve: quieta es
        // peor que girando, pero mejor que dando un tirón cada tres minutos.
        if (ReelStrip.ActualWidth - travel < ReelViewport.ActualWidth)
        {
            return;
        }

        _reelMoving = true;

        var shift = new TranslateTransform();
        ReelStrip.RenderTransform = shift;

        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = 0,
            To = -travel,
            // Mas rapido de lo que estaba: a travel/14 pasaba un Pokemon cada tres segundos y
            // parecia parado. Sigue siendo un desplazamiento de fondo, no un carrete girando.
            Duration = TimeSpan.FromSeconds(Math.Max(14, travel / 45)),
            RepeatBehavior = RepeatBehavior.Forever
        });
    }

    private void StartSpin(SpinRequest request)
    {
        _reelMoving = true;

        var viewport = ReelViewport.ActualWidth;
        if (viewport <= 0)
        {
            request.Stopped();
            return;
        }

        // Dónde tiene que quedarse la tira para que la celda ganadora caiga bajo el marcador.
        var target = -(request.WinnerIndex * CellWidth) + ((viewport - CellWidth) / 2);

        var shift = new TranslateTransform();
        ReelStrip.RenderTransform = shift;

        // Arranque corto, crucero a toda velocidad, frenada exponencial que ocupa casi la mitad
        // de la tirada —para que se vea a los Pokémon pasar cada vez más despacio— y un final a
        // clics: se planta tres casillas antes y avanza de una en una, con su pausa entre cada
        // una, porque la pausa es la tensión.
        var slide = new DoubleAnimationUsingKeyFrames { Duration = request.Duration };
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target * 0.02, KeyTime.FromPercent(0.05))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        });
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(target * 0.34, KeyTime.FromPercent(0.26)));
        // El cierre lo dicta el perfil sorteado, no una escalera fija: se planta donde diga su
        // primera parada y va dando clics por las suyas. Cinco perfiles con el mismo final, para
        // que jugar mucho no enseñe a leer dónde va a parar. Ver ReelEnding.
        var ending = request.Ending;

        slide.KeyFrames.Add(new EasingDoubleKeyFrame(
            target + (CellWidth * ending.Stops[0]), KeyTime.FromPercent(ending.At[0]))
        {
            // Exponente bajo a propósito: con uno alto la rueda se planta a mitad de tirada y el
            // tramo siguiente se queda muerto. Así sigue arrastrándose hasta el primer clic.
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.2 },
        });

        for (var i = 1; i < ending.Stops.Length; i++)
        {
            // La pausa ANTES de cada clic es la tensión: sin ella son varios movimientos seguidos
            // y no varias decisiones. Ocupa el primer tercio del hueco entre parada y parada.
            var from = ending.At[i - 1];
            var to = ending.At[i];

            slide.KeyFrames.Add(new LinearDoubleKeyFrame(
                target + (CellWidth * ending.Stops[i - 1]),
                KeyTime.FromPercent(from + ((to - from) * 0.34))));

            var last = i == ending.Stops.Length - 1;

            slide.KeyFrames.Add(new EasingDoubleKeyFrame(
                target + (CellWidth * ending.Stops[i]), KeyTime.FromPercent(to))
            {
                // El rebote del último clic hace pensar por un instante que se iba una casilla
                // más. No lo llevan todos: en el que se pasa de verdad sobraría.
                EasingFunction = last && ending.Overshoot
                    ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 }
                    : new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        }
        // El aviso sale de aquí, del final real del movimiento: es lo que garantiza que el
        // ganador quede dentro de su marco cuando se revela.
        slide.Completed += (_, _) => Land(request);
        shift.BeginAnimation(TranslateTransform.XProperty, slide);

        // El desenfoque hace de velocímetro, y se retira antes de los clics para que se pueda
        // leer qué Pokémon pasa por el marcador en cada uno.
        var blur = new BlurEffect { Radius = 0, KernelType = KernelType.Box };
        ReelStrip.Effect = blur;

        var blurring = new DoubleAnimationUsingKeyFrames { Duration = request.Duration };
        blurring.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromPercent(0.06)));
        blurring.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromPercent(0.26)));
        blurring.KeyFrames.Add(new LinearDoubleKeyFrame(3.5, KeyTime.FromPercent(0.45)));
        blurring.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.62)));
        blurring.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        // El desenfoque de una tira de 118 celdas no es gratis: en cuanto llega a cero se quita.
        blurring.Completed += (_, _) => ReelStrip.Effect = null;
        blur.BeginAnimation(BlurEffect.RadiusProperty, blurring);

        // El campo se lanza con la rueda: antes las rayas se encendian y las estrellas seguian a
        // su ritmo de siempre, que es justo la desconexion que hacia que el fondo pareciera otra
        // pantalla pegada detras.
        Field(4.5);

        StartClickFeedback(request.Duration, ending.Clicks);
        StartApproach(request.Duration);
    }

    /// <summary>
    /// The marker punch and the jolt of the reel at each click.
    /// </summary>
    /// <remarks>
    /// One key-framed animation per property covering the whole spin, instead of one animation per
    /// click with its own delay: two animations on the same property replace each other, so the
    /// second click would cancel the first mid-bounce.
    /// </remarks>
    private void StartClickFeedback(TimeSpan duration, double[] clicks)
    {
        // Esta linea de tiempo dura un pelin MAS que la tirada, y las fracciones se reescalan a
        // ella. Sin esa cola, el ultimo clic -- que cae exactamente en el 1,00 de la tirada, porque
        // todo cierre acaba en el ganador -- pedia su rebote en el 1,006, y KeyTime.FromPercent
        // LANZA por encima de 1. Reventaba en CADA tirada: el Pokemon se entregaba igual, pero al
        // jugador le salia «ha habido un error inesperado» por algo que habia funcionado.
        //
        // La cola no es un parche: ese ultimo rebote es el golpe del aterrizaje, y el aterrizaje
        // ocurre justo cuando la rueda ya ha parado.
        var span = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * ClickTail);

        var kick = new DoubleAnimationUsingKeyFrames { Duration = span };
        var jolt = new DoubleAnimationUsingKeyFrames { Duration = span };
        kick.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        jolt.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));

        foreach (var click in clicks)
        {
            kick.KeyFrames.Add(new LinearDoubleKeyFrame(1, OnTail(click - 0.004)));
            kick.KeyFrames.Add(new LinearDoubleKeyFrame(1.18, OnTail(click + 0.006)));
            kick.KeyFrames.Add(new EasingDoubleKeyFrame(1, OnTail(click + 0.03))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });

            jolt.KeyFrames.Add(new LinearDoubleKeyFrame(0, OnTail(click - 0.004)));
            jolt.KeyFrames.Add(new LinearDoubleKeyFrame(3.5, OnTail(click + 0.006)));
            jolt.KeyFrames.Add(new EasingDoubleKeyFrame(0, OnTail(click + 0.03))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        }

        kick.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(1)));
        jolt.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        var scale = MarkerScale();
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, kick);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, kick);
        ReelJolt.BeginAnimation(TranslateTransform.YProperty, jolt);
    }

    /// <summary>The screen leaning in: streaks while it flies, and a slow zoom while it brakes.</summary>
    private void StartApproach(TimeSpan duration)
    {
        var streaks = new DoubleAnimationUsingKeyFrames { Duration = duration };
        streaks.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        streaks.KeyFrames.Add(new LinearDoubleKeyFrame(0.28, KeyTime.FromPercent(0.10)));
        streaks.KeyFrames.Add(new LinearDoubleKeyFrame(0.28, KeyTime.FromPercent(0.30)));
        streaks.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.58)));
        streaks.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        Streaks.BeginAnimation(OpacityProperty, streaks);

        var zoom = new DoubleAnimationUsingKeyFrames { Duration = duration };
        zoom.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.2)));
        zoom.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromPercent(0.72))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        });
        zoom.KeyFrames.Add(new LinearDoubleKeyFrame(1.06, KeyTime.FromPercent(1)));
        ReelZoom.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
        ReelZoom.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
    }

    /// <summary>Everything that happens the instant the wheel plants itself.</summary>
    private void Land(SpinRequest request)
    {
        SettleField();
        CalmBackground();
        request.Stopped();

        FlashOnce(0.75, TimeSpan.FromSeconds(1.2));
        PlayShockwave(1);
        PlayBurst(1);
        Shake(11, 0.42);

        var settle = new DoubleAnimation(1.13, 1, new Duration(TimeSpan.FromSeconds(0.7)))
        {
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 5 },
        };
        ReelZoom.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
        ReelZoom.BeginAnimation(ScaleTransform.ScaleYProperty, settle);

        var scale = MarkerScale();
        var punch = new DoubleAnimation(1.45, 1, new Duration(TimeSpan.FromSeconds(0.65)))
        {
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, punch);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, punch);
    }

    private void FlashOnce(double from, TimeSpan fade, TimeSpan? delay = null)
    {
        var flash = new DoubleAnimation(from, 0, new Duration(fade))
        {
            BeginTime = delay,
        };
        Flash.BeginAnimation(OpacityProperty, flash);
    }

    /// <summary>The ring that opens out from the reel when it lands.</summary>
    private void PlayShockwave(double strength, TimeSpan? delay = null)
    {
        var life = new Duration(TimeSpan.FromSeconds(0.8));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = life, BeginTime = delay };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.95, KeyTime.FromPercent(0.04)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        Shockwave.BeginAnimation(OpacityProperty, fade);

        // Hasta 2,4: más allá el aro se sale del panel y se ve recortado en vez de abrirse.
        var grow = new DoubleAnimation(0.2, 2.4 * strength, life)
        {
            BeginTime = delay,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ShockwaveScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ShockwaveScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>The glow and the star over the cell that won.</summary>
    private void PlayBurst(double strength)
    {
        var fade = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(1.1)) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.6, KeyTime.FromPercent(0.07)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        Burst.BeginAnimation(OpacityProperty, fade);

        // Nunca por debajo de 0,55: más pequeño y los rayos entrarían en la celda, tapando al
        // Pokémon en el único momento en que se le quiere ver.
        var grow = new DoubleAnimation(0.55, 1.3 * strength, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 },
        };
        BurstScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        BurstScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

        var spin = new DoubleAnimation(-28, 22, new Duration(TimeSpan.FromSeconds(1.1)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        BurstSpin.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// <summary>A decaying two-axis shake of everything on the panel except the starfield.</summary>
    private void Shake(double amount, double seconds)
    {
        StageShake.BeginAnimation(TranslateTransform.XProperty, Jolt(amount, seconds, 1));
        StageShake.BeginAnimation(TranslateTransform.YProperty, Jolt(amount * 0.55, seconds, -1));
    }

    private static DoubleAnimationUsingKeyFrames Jolt(double amount, double seconds, double sign)
    {
        double[] steps = [1, -0.76, 0.54, -0.35, 0.2, -0.09, 0];
        var shake = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(seconds)) };

        for (var i = 0; i < steps.Length; i++)
        {
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(
                amount * steps[i] * sign, KeyTime.FromPercent((i + 1) / (double)steps.Length)));
        }

        return shake;
    }

    /// <summary>A short punch of the marker, so a tier climb is felt and not just seen.</summary>
    /// <summary>
    /// Keeps the marker breathing while nothing is happening.
    /// </summary>
    /// <remarks>
    /// The viewport at rest was an empty bordered box with an empty outline inside it, and that
    /// reads as «broken» or «still loading», not as «ready». A gacha at rest has to invite the
    /// pull. Very slow and very shallow on purpose — four seconds a cycle, a twentieth of the
    /// element — because this is the resting state of a screen somebody leaves open, and anything
    /// faster stops being an invitation and becomes a distraction.
    /// <para>
    /// It runs for ever and is never stopped: the spin animates the marker's <b>scale</b> through
    /// the same transform, and an animation started later on the same property simply takes over,
    /// so the two cannot fight. What comes back afterwards is this one, which is what should
    /// happen.
    /// </para>
    /// </remarks>
    private void BreatheWhileWaiting() => MarkerScale().BeginAnimation(
        ScaleTransform.ScaleYProperty,
        new DoubleAnimation
        {
            From = 1,
            To = 1.05,
            Duration = TimeSpan.FromSeconds(2),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });

    private void KickMarker()
    {
        var kick = new DoubleAnimation(1.22, 1, new Duration(TimeSpan.FromSeconds(0.45)))
        {
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 },
        };

        var scale = MarkerScale();
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, kick);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, kick);
    }

    private ScaleTransform MarkerScale()
    {
        if (Marker.RenderTransform is ScaleTransform existing)
        {
            return existing;
        }

        var scale = new ScaleTransform(1, 1);
        Marker.RenderTransform = scale;
        return scale;
    }
}
