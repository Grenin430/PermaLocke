using System.Windows;
using System.Windows.Controls;
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

    public RouletteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        if (DataContext is RouletteViewModel model)
        {
            _model = model;
            model.SpinRequested += OnSpinRequested;
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.SpinRequested -= OnSpinRequested;
            _model = null;
        }
    }

    private void OnSpinRequested(object? sender, SpinTheWheel request)
    {
        // Se parte del ángulo en el que quedó la vez anterior, así que la rueda no da un salto
        // antes de empezar a girar.
        var from = WheelSpin.Angle % 360;

        var animation = new DoubleAnimation
        {
            From = from,
            To = from + request.FinalAngle,
            Duration = request.Duration,
            // Potencia 5 en vez de un cubico: frena antes y se arrastra al final, que es donde
            // esta la gracia -- ver pasar las cunas una a una y poder leerlas.
            EasingFunction = new PowerEase { Power = 5, EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) => request.Stopped();
        WheelSpin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
    }
}
