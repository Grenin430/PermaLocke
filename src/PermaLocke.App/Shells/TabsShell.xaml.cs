using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PermaLocke.App.Shells;

/// <summary>Pocket tabs along the top (ESMERALDA). See the XAML for the composition.</summary>
public partial class TabsShell : ShellBase
{
    private readonly DispatcherTimer _blink = new() { Interval = TimeSpan.FromMilliseconds(520) };

    public TabsShell()
    {
        InitializeComponent();

        Tabs.SizeChanged += (_, _) => FitTabs();
        ((INotifyCollectionChanged)Tabs.Items).CollectionChanged +=
            (_, _) => Dispatcher.BeginInvoke(FitTabs, DispatcherPriority.Loaded);

        // La flecha del cuadro de diálogo parpadea como la del juego, salvo que se pidan menos animaciones.
        _blink.Tick += (_, _) => Blink.Visibility = Blink.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        Loaded += (_, _) =>
        {
            if (!ShellSupport.ReducedMotion) _blink.Start();
            FitTabs();
        };
        Unloaded += (_, _) => _blink.Stop();
    }

    protected override UIElement SectionHost => Section;

    /// <summary>
    /// Whether the tabs fit with their names: measured, not decided by the window's size, because a ninth section (the
    /// wheel) or a long label changes how much room they need.
    /// </summary>
    private void FitTabs()
    {
        if (Tabs.ActualWidth <= 0) return;

        ShellSupport.SetCompact(Tabs, false);
        Tabs.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ShellSupport.SetCompact(Tabs, Tabs.DesiredSize.Width > Tabs.ActualWidth);
    }

    private void OnPageChosen(object sender, SelectionChangedEventArgs e) => ChoosePage(e);
}
