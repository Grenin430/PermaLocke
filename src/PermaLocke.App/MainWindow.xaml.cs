using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App;

/// <summary>
/// The shell window. Holds no logic beyond making its own frame match the theme and fit the screen.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _shell;

    /// <summary>What the counter said last time, to know whether the change was good or bad.</summary>
    private int _lastPoints;

    public MainWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        DataContextChanged += OnDataContextChanged;

        Sidebar.SizeChanged += (_, _) => FitAlolaCorner();

        // La franja de Alola acaba justo en la raya de la cabecera: los 20 de margen del contenido más lo que mida.
        Header.SizeChanged += (_, _) => AlolaStrip.Height = Header.ActualHeight + 20;
        ((INotifyCollectionChanged)NavList.Items).CollectionChanged +=
            (_, _) => Dispatcher.BeginInvoke(FitAlolaCorner, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Shows the Alola corner only when the list of sections still fits whole above it.
    /// </summary>
    /// <remarks>
    /// Measured, not decided by window size: at NORMAL (760 tall) the list alone takes almost the whole
    /// sidebar, and it grows by one when the run plays with the wheel. A corner that pushes a scroll bar
    /// onto the navigation is a decoration in the way of the one thing the sidebar is for.
    /// </remarks>
    private void FitAlolaCorner()
    {
        if (Sidebar.ActualHeight <= 0)
        {
            return;
        }

        var width = Math.Max(1, Sidebar.ActualWidth);
        NavList.Measure(new Size(width, double.PositiveInfinity));

        var corner = AlolaCorner.Margin.Top + AlolaCorner.Margin.Bottom;

        foreach (UIElement child in AlolaCorner.Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            corner += child.DesiredSize.Height;
        }

        var room = Sidebar.ActualHeight - Sidebar.RowDefinitions[0].ActualHeight - NavList.DesiredSize.Height;
        AlolaCorner.Visibility = room >= corner ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// The little unfold when the window comes back from the tab.
    /// </summary>
    /// <remarks>
    /// The content and not the window: <c>Window.Opacity</c> only does anything with
    /// <c>AllowsTransparency</c>, which this window does not have and does not need. Scaling from
    /// 0.97 and fading in over a sixth of a second is enough to read as «se despliega» without
    /// making you wait for it — the window is already usable while it plays.
    /// </remarks>
    public void PlayUnfold()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        Shell.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = ease
        });

        foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            ShellScale.BeginAnimation(axis, new DoubleAnimation
            {
                From = 0.97,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(230),
                EasingFunction = ease
            });
        }
    }

    private void OnMinimise(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
        }

        _shell = DataContext as MainViewModel;

        if (_shell is null)
        {
            return;
        }

        _shell.PropertyChanged += OnShellChanged;
        _lastPoints = _shell.PointsValue;

    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedSection))
        {
            EnterSection();
        }

        if (e.PropertyName == nameof(MainViewModel.PointsValue) && _shell is not null)
        {
            FlashPoints(_shell.PointsValue - _lastPoints);
            _lastPoints = _shell.PointsValue;
        }
    }

    /// <summary>
    /// The new section fades in and settles down a few pixels.
    /// </summary>
    /// <remarks>
    /// Short and small on purpose. This runs on every click of the sidebar, and an animation you
    /// have to wait for stops being a flourish and becomes a toll — a hundred and forty
    /// milliseconds is under the threshold where a change of screen feels like a delay.
    /// </remarks>
    private void EnterSection()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        SectionHost.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = ease
        });

        SectionSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = ease
        });
    }

    /// <summary>Tints the points for a moment, green when they went up and red when they went down.</summary>
    private void FlashPoints(int change)
    {
        if (change == 0)
        {
            return;
        }

        var flash = (Color)FindResource(change > 0 ? "PxGood" : "PxBad");

        // La cifra es texto en píxeles (§176): se anima su color, que la vuelve a dibujar en cada paso.
        PointsNumber.BeginAnimation(Views.Pixel.PixelText.ColourProperty, new ColorAnimation
        {
            From = flash,
            To = (Color)FindResource("PxAccent"),
            Duration = TimeSpan.FromMilliseconds(900),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }

    /// <summary>
    /// Sets the window to the chosen size, shrinking it only if this screen cannot take it.
    /// </summary>
    /// <remarks>
    /// The shrinking is not a loophole in «fixed», it is what stops a chosen size from being a bug
    /// on somebody else's machine. 860 plus a title bar plus a taskbar is taller than a 1366×768
    /// laptop, so without this the window would open with its own bottom edge off the screen —
    /// buttons included. A size that does not fit is not a size.
    /// <para>
    /// It reads the <b>working area</b> rather than the screen, because the taskbar is not screen
    /// the window can use.
    /// </para>
    /// </remarks>
    public void Resize(WindowSize size)
    {
        var area = SystemParameters.WorkArea;

        // El minimo y el maximo se sueltan antes de medir: si no, el tamaño anterior impide crecer.
        MinWidth = MinHeight = 0;
        MaxWidth = MaxHeight = double.PositiveInfinity;

        Width = Math.Min(size.Width, area.Width);
        Height = Math.Min(size.Height, area.Height);

        // Y se fija ahí: sin margen de redimensión, la única medida posible es la que se acaba de
        // calcular, de modo que nadie ve una ventana a medio estirar.
        MinWidth = MaxWidth = Width;
        MinHeight = MaxHeight = Height;

        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top + ((area.Height - Height) / 2);
    }
}
