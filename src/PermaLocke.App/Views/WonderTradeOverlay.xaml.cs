using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The wonder trade animation: the Pokémon goes into its ball, the ball leaves, another arrives.
/// </summary>
/// <remarks>
/// <para>
/// Presentation only. It decides nothing: by the time the first frame runs, the trade has been
/// drawn, written into the save and recorded in the log. The view model says <em>when</em> each
/// reveal happens; this says <em>how</em> everything moves.
/// </para>
/// <para>
/// It lives in code rather than in a Storyboard because where a ball has to fly to depends on how
/// wide the panel is at that moment, and XAML cannot know that.
/// </para>
/// </remarks>
public partial class WonderTradeOverlay : UserControl
{
    /// <summary>How long the Pokémon takes to shrink into its ball.</summary>
    private static readonly Duration Swallow = new(TimeSpan.FromSeconds(1.3));

    /// <summary>How long a ball takes to cross the panel.</summary>
    private static readonly Duration Travel = new(TimeSpan.FromSeconds(1.7));

    private WonderTradeViewModel? _model;

    public WonderTradeOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        if (e.NewValue is not WonderTradeViewModel model)
        {
            return;
        }

        _model = model;
        model.AnimationRequested += OnAnimationRequested;
        model.OpenRequested += OnOpenRequested;
    }

    private void Detach()
    {
        if (_model is null)
        {
            return;
        }

        _model.AnimationRequested -= OnAnimationRequested;
        _model.OpenRequested -= OnOpenRequested;
        _model = null;
    }

    private void OnAnimationRequested(object? sender, TradeAnimation request) =>
        Dispatcher.BeginInvoke(() => Play(request), System.Windows.Threading.DispatcherPriority.Loaded);

    private void OnOpenRequested(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Open);

    private void Play(TradeAnimation request)
    {
        // Media pantalla más un margen: la bola tiene que salirse del todo, no quedarse en el
        // borde, y el ancho real solo se conoce ahora.
        var distance = (ActualWidth / 2) + 200;

        if (distance <= 200)
        {
            request.BallArrived();
            return;
        }

        Reset();
        SwallowGiven();
        SendOut(distance);
        BringIn(distance, request);
    }

    /// <summary>Everything back to its starting mark, so a second trade does not inherit the first.</summary>
    private void Reset()
    {
        GivenSprite.BeginAnimation(OpacityProperty, null);
        GivenSprite.Opacity = 1;
        GivenScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        GivenScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        GivenScale.ScaleX = GivenScale.ScaleY = 1;

        TypeFlood.BeginAnimation(OpacityProperty, null);
        TypeFlood.Opacity = 0;
        OutgoingBall.Opacity = 0;
        IncomingBall.Opacity = 0;
        OutgoingShift.X = 0;
        IncomingShift.X = 0;
    }

    /// <summary>The Pokémon shrinks into the ball, and the ball snaps shut with a flash.</summary>
    private void SwallowGiven()
    {
        var appear = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.35)));
        OutgoingBall.BeginAnimation(OpacityProperty, appear);

        var shrink = new DoubleAnimationUsingKeyFrames { Duration = Swallow };
        shrink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.25)));
        shrink.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.75))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.4 },
        });
        shrink.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        GivenScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        GivenScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);

        var fade = new DoubleAnimationUsingKeyFrames { Duration = Swallow };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.6)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.78)));
        GivenSprite.BeginAnimation(OpacityProperty, fade);

        // El apretón de la bola al cerrarse, justo cuando el Pokémon acaba de entrar.
        var squeeze = new DoubleAnimationUsingKeyFrames { Duration = Swallow };
        squeeze.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.7)));
        squeeze.KeyFrames.Add(new LinearDoubleKeyFrame(1.35, KeyTime.FromPercent(0.82)));
        squeeze.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1))
        {
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 },
        });
        OutgoingScale.BeginAnimation(ScaleTransform.ScaleXProperty, squeeze);
        OutgoingScale.BeginAnimation(ScaleTransform.ScaleYProperty, squeeze);

        FlashOnce(0.5, TimeSpan.FromSeconds(0.7), TimeSpan.FromSeconds(0.82 * Swallow.TimeSpan.TotalSeconds));
    }

    /// <summary>The ball leaves to the right, spinning, once the Pokémon is inside.</summary>
    private void SendOut(double distance)
    {
        var start = Swallow.TimeSpan;

        var fly = new DoubleAnimation(0, distance, Travel)
        {
            BeginTime = start,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        OutgoingShift.BeginAnimation(TranslateTransform.XProperty, fly);

        var spin = new DoubleAnimation(0, 900, Travel) { BeginTime = start };
        OutgoingSpin.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// <summary>
    /// The other ball arrives from the left while the first is still on its way out, so the two
    /// cross in the middle of the panel — which is the moment the trade actually reads as a trade.
    /// </summary>
    private void BringIn(double distance, TradeAnimation request)
    {
        // Arranca a mitad del viaje de la otra: es el cruce.
        var start = Swallow.TimeSpan + TimeSpan.FromSeconds(Travel.TimeSpan.TotalSeconds * 0.45);

        IncomingShift.X = -distance;

        var appear = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.2))) { BeginTime = start };
        IncomingBall.BeginAnimation(OpacityProperty, appear);

        var fly = new DoubleAnimation(-distance, 0, Travel)
        {
            BeginTime = start,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        // El aviso sale del final real del movimiento, no de un reloj: es lo que garantiza que
        // los tres avisos no empiecen con la bola todavía en el aire.
        fly.Completed += (_, _) =>
        {
            // Aterriza: golpe, onda y sacudida, y solo entonces empiezan los avisos.
            Shake(16, 0.45);
            PlayShockwave();
            FlashOnce(0.5, TimeSpan.FromSeconds(0.8));
            Wiggle();
            request.BallArrived();
        };
        IncomingShift.BeginAnimation(TranslateTransform.XProperty, fly);

        var spin = new DoubleAnimation(-900, 0, Travel)
        {
            BeginTime = start,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        IncomingSpin.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// <summary>The three shakes of a ball that has something inside.</summary>
    private void Wiggle()
    {
        var wiggle = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.FromSeconds(1.2)),
        };

        double[] angles = [0, -16, 16, -12, 12, -7, 7, 0];

        for (var step = 0; step < angles.Length; step++)
        {
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(angles[step],
                KeyTime.FromPercent((step + 1) / (double)angles.Length))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        }

        IncomingSpin.BeginAnimation(RotateTransform.AngleProperty, wiggle);
    }

    /// <summary>The ball bursts open, the type floods the screen and the Pokémon lands.</summary>
    private void Open()
    {
        FlashOnce(1, TimeSpan.FromSeconds(1.4));
        PlayShockwave(3.2);
        Shake(24, 0.6);

        // El color del tipo se queda de fondo, bajito, para que la ficha final tenga su ambiente.
        var flood = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(1.6)) };
        flood.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0.12)));
        flood.KeyFrames.Add(new EasingDoubleKeyFrame(0.22, KeyTime.FromPercent(1))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        TypeFlood.BeginAnimation(OpacityProperty, flood);

        var burst = new DoubleAnimation(1, 3.4, new Duration(TimeSpan.FromSeconds(0.5)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        IncomingScale.BeginAnimation(ScaleTransform.ScaleXProperty, burst);
        IncomingScale.BeginAnimation(ScaleTransform.ScaleYProperty, burst);

        var vanish = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.4)));
        IncomingBall.BeginAnimation(OpacityProperty, vanish);

        var land = new DoubleAnimation(0.25, 1, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            BeginTime = TimeSpan.FromSeconds(0.15),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 },
        };
        ResultScale.BeginAnimation(ScaleTransform.ScaleXProperty, land);
        ResultScale.BeginAnimation(ScaleTransform.ScaleYProperty, land);
    }

    /// <summary>The ring that opens out from the middle on every impact.</summary>
    private void PlayShockwave(double strength = 2.2)
    {
        var life = new Duration(TimeSpan.FromSeconds(0.9));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = life };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.05)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        Shockwave.BeginAnimation(OpacityProperty, fade);

        var grow = new DoubleAnimation(0.15, strength, life)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ShockwaveScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ShockwaveScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>A decaying two-axis shake of the whole scene.</summary>
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

    private void FlashOnce(double from, TimeSpan fade, TimeSpan? delay = null)
    {
        var flash = new DoubleAnimation(from, 0, new Duration(fade)) { BeginTime = delay };
        Flash.BeginAnimation(OpacityProperty, flash);
    }
}
