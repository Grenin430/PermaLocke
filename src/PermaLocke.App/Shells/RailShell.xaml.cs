using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Shells;

/// <summary>
/// The look PermaLocke has always had: the side rail with the names, the header with the sky of Alola. What used to be
/// the code of the main window, moved here so that it is one shell among the others.
/// </summary>
public partial class RailShell : ShellBase
{
    /// <summary>What the counter said last time, to know whether the change was good or bad.</summary>
    private int _lastPoints;

    public RailShell()
    {
        InitializeComponent();

        Sidebar.SizeChanged += (_, _) => FitAlolaCorner();

        // La franja de Alola acaba justo en la raya de la cabecera: los 20 de margen del contenido más lo que mida.
        Header.SizeChanged += (_, _) => AlolaStrip.Height = Header.ActualHeight + 20;
        ((INotifyCollectionChanged)NavList.Items).CollectionChanged +=
            (_, _) => Dispatcher.BeginInvoke(FitAlolaCorner, DispatcherPriority.Loaded);

        // Un ListBox captura el ratón al pulsar y va seleccionando lo que pisa mientras el botón siga abajo: arrastrar
        // por la barra cambiaba de sección sin soltar. Sin captura, solo cambia lo que se pulsa. Vale también para la
        // lista de pestañas de EQUIPO y TORNEO, que avisa por aquí al ser hija.
        NavList.AddHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler((_, e) =>
        {
            if (e.OriginalSource is System.Windows.Controls.ListBox list)
            {
                list.ReleaseMouseCapture();
            }
        }), handledEventsToo: true);
    }

    protected override UIElement SectionHost => Section;

    protected override void OnAttached(MainViewModel main) => _lastPoints = main.PointsValue;

    protected override void OnMainPropertyChanged(string? property)
    {
        if (property == nameof(MainViewModel.PointsValue) && Main is { } main)
        {
            FlashPoints(main.PointsValue - _lastPoints);
            _lastPoints = main.PointsValue;
        }
    }

    private void OnGallery(object sender, RoutedEventArgs e) => DesignGalleryWindow.Open(Window.GetWindow(this));

    private void OnMinimise(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window) window.WindowState = WindowState.Minimized;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

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

    /// <summary>Tints the points for a moment, green when they went up and red when they went down.</summary>
    private void FlashPoints(int change)
    {
        if (change == 0 || ShellSupport.ReducedMotion)
        {
            return;
        }

        var flash = (Color)FindResource(change > 0 ? "PxGood" : "PxBad");

        // La cifra es texto en píxeles (§176): se anima su color, que la vuelve a dibujar en cada paso. Al acabar vuelve a
        // lo que diga el diseño (FillBehavior.Stop): no se queda con el color que tenía.
        PointsNumber.BeginAnimation(PixelText.ColourProperty, new ColorAnimation
        {
            From = flash,
            To = (Color)FindResource("PxAccent"),
            Duration = TimeSpan.FromMilliseconds(900),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }
}
