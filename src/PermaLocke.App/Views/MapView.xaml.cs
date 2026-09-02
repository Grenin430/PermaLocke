using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class MapView : UserControl
{
    public MapView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Tells the markers how much the map shrank, so they can undo it on themselves.
    /// </summary>
    /// <remarks>
    /// It has to be code-behind: how much a Viewbox scaled its child is a fact about the rendered
    /// surface, and it publishes no property to bind to. What happens here is a division; what to
    /// do with it belongs to the view model, which the markers bind to.
    /// <para>
    /// Both sizes are listened to. The frame changes when the window does, and the surface changes
    /// when the island picture finishes loading — which is later, and would otherwise leave the
    /// markers scaled for whatever was there before.
    /// </para>
    /// </remarks>
    private void OnMapSized(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MapViewModel map
            || MapSurface.ActualWidth <= 0 || MapSurface.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(MapFrame.ActualWidth / MapSurface.ActualWidth,
            MapFrame.ActualHeight / MapSurface.ActualHeight);

        map.MarkerScale = scale > 0.01 ? 1 / scale : 1;
    }

    /// <summary>
    /// Fades the markers in one after another when an island is drawn.
    /// </summary>
    /// <remarks>
    /// In code because a Storyboard's <c>BeginTime</c> cannot be bound, and a cascade is nothing
    /// but each one starting a little later than the last. Switching island regenerates the
    /// containers, so this fires exactly when it should without anybody telling it to.
    /// </remarks>
    private void OnMarkersLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ItemsControl markers)
        {
            return;
        }

        markers.Dispatcher.BeginInvoke(() =>
        {
            for (var i = 0; i < markers.Items.Count; i++)
            {
                if (markers.ItemContainerGenerator.ContainerFromIndex(i) is not UIElement container)
                {
                    continue;
                }

                container.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    BeginTime = TimeSpan.FromMilliseconds(i * 14)
                });
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
