using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
    /// Where the reel stops dead and then advances one cell at a time, as a fraction of the spin.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the ending. A wheel that glides to a halt resolves the roll in
    /// one moment; a wheel that plants itself three cells short and then walks the last three
    /// resolves it three times, and the player reads each Pokémon as it passes under the marker.
    /// The number of clicks is the same for every tier on purpose: making the rare ones click more
    /// would give the result away before the reel gets there.
    /// </remarks>
    private static readonly double[] Clicks = [0.72, 0.84, 0.93];

    private GachaViewModel? _model;

    public GachaView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
        Dispatcher.BeginInvoke(() => StartSpin(request), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void StartSpin(SpinRequest request)
    {
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
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target + (CellWidth * 3), KeyTime.FromPercent(0.72))
        {
            // Exponente bajo a propósito: con uno alto la rueda se planta a mitad de tirada y el
            // tramo siguiente se queda muerto. Así sigue arrastrándose hasta el primer clic.
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.2 },
        });
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(target + (CellWidth * 3), KeyTime.FromPercent(0.78)));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target + (CellWidth * 2), KeyTime.FromPercent(0.84))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(target + (CellWidth * 2), KeyTime.FromPercent(0.88)));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target + CellWidth, KeyTime.FromPercent(0.93))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(target + CellWidth, KeyTime.FromPercent(0.96)));
        // El último clic se pasa de largo y vuelve, que es lo que hace pensar por un instante que
        // se iba a una casilla más. El BackEase mete ese rebote dentro del propio tramo.
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target, KeyTime.FromPercent(1))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 },
        });
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

        StartClickFeedback(request.Duration);
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
    private void StartClickFeedback(TimeSpan duration)
    {
        var kick = new DoubleAnimationUsingKeyFrames { Duration = duration };
        var jolt = new DoubleAnimationUsingKeyFrames { Duration = duration };
        kick.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        jolt.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));

        foreach (var click in Clicks)
        {
            kick.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(click - 0.004)));
            kick.KeyFrames.Add(new LinearDoubleKeyFrame(1.18, KeyTime.FromPercent(click + 0.006)));
            kick.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(click + 0.03))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });

            jolt.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(click - 0.004)));
            jolt.KeyFrames.Add(new LinearDoubleKeyFrame(3.5, KeyTime.FromPercent(click + 0.006)));
            jolt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(click + 0.03))
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
