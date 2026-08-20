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
/// screen is showing — and this says <em>how</em>: acceleration, braking, overshoot and blur.
/// </para>
/// </remarks>
public partial class GachaView : UserControl
{
    /// <summary>Width of one reel cell. Fixed in <c>ReelCellTemplate</c>; both must agree.</summary>
    private const double CellWidth = 72;

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

    /// <summary>Every climb of the fake-out gets its own flash and a kick of the marker.</summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GachaViewModel.DisplayedTier) || _model is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_model.DisplayedTier))
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            FlashOnce(0.34, TimeSpan.FromSeconds(0.5));
            KickMarker();
        });
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

        // Cuatro tramos, y el largo es el tercero a propósito: arranque corto, crucero a toda
        // velocidad, y una frenada exponencial que ocupa más de la mitad de la tirada, para que se
        // vea a los Pokémon pasar cada vez más despacio. Al final se pasa media casilla y vuelve.
        var slide = new DoubleAnimationUsingKeyFrames { Duration = request.Duration };
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target * 0.02, KeyTime.FromPercent(0.05))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        });
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(target * 0.34, KeyTime.FromPercent(0.26)));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target - (CellWidth * 0.45), KeyTime.FromPercent(0.96))
        {
            // Exponente bajo a propósito: con uno alto la rueda se planta a mitad de tirada y el
            // último segundo se queda muerto. Así sigue arrastrándose hasta el final.
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.6 },
        });
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(target, KeyTime.FromPercent(1))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        // El aviso sale de aquí, del final real del movimiento: es lo que garantiza que el
        // ganador quede dentro de su marco cuando se revela.
        slide.Completed += (_, _) => request.Stopped();
        shift.BeginAnimation(TranslateTransform.XProperty, slide);

        // El desenfoque hace de velocímetro, y se retira pronto para que los iconos se vean a
        // color mientras la rueda todavía se está parando.
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

        // Y el remate: fogonazo grande justo cuando la rueda se planta.
        FlashOnce(0.6, TimeSpan.FromSeconds(1.1), request.Duration);
    }

    private void FlashOnce(double from, TimeSpan fade, TimeSpan? delay = null)
    {
        var flash = new DoubleAnimation(from, 0, new Duration(fade))
        {
            BeginTime = delay,
        };
        Flash.BeginAnimation(OpacityProperty, flash);
    }

    /// <summary>A short punch of the marker, so a tier climb is felt and not just seen.</summary>
    private void KickMarker()
    {
        if (Marker.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            Marker.RenderTransform = scale;
        }

        var kick = new DoubleAnimation(1.18, 1, new Duration(TimeSpan.FromSeconds(0.45)))
        {
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 },
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, kick);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, kick);
    }
}
