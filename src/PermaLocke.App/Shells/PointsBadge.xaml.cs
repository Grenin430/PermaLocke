using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Shells;

public partial class PointsBadge : UserControl
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(PointsBadge), new PropertyMetadata(false, (d, _) => ((PointsBadge)d).Fit()));

    private MainViewModel? _main;
    private int _last;

    public PointsBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) =>
        {
            Detach();
            Attach();
        };
    }

    /// <summary>One short line instead of the figure with its caption underneath.</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private void Fit()
    {
        var compact = Compact;
        Number.Scale = Dash.Scale = compact ? 2 : 3;
        Caption.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Row.Margin = compact ? new Thickness(8, 4, 11, 7) : new Thickness(9, 6, 12, 8);
        Gem.Margin = new Thickness(0, 0, compact ? 7 : 8, 0);
    }

    private void Attach()
    {
        if (_main is not null || DataContext is not MainViewModel main) return;

        _main = main;
        _last = main.PointsValue;
        main.PropertyChanged += OnChanged;
    }

    private void Detach()
    {
        if (_main is null) return;

        _main.PropertyChanged -= OnChanged;
        _main = null;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.PointsValue) || _main is null) return;

        var change = _main.PointsValue - _last;
        _last = _main.PointsValue;
        if (change == 0 || ShellSupport.ReducedMotion) return;

        // La cifra es texto en píxeles: se anima su color, que la vuelve a dibujar en cada paso. Al acabar vuelve a lo
        // que diga el diseño (FillBehavior.Stop), no se queda con el color que tenía.
        Number.BeginAnimation(PixelText.ColourProperty, new ColorAnimation
        {
            From = (Color)FindResource(change > 0 ? "PxGood" : "PxBad"),
            To = (Color)FindResource("PxAccent"),
            Duration = TimeSpan.FromMilliseconds(900),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }
}
