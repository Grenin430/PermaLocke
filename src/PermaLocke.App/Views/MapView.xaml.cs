using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class MapView : UserControl
{
    /// <summary>How far the island slides away from the cursor, in pixels at each edge.</summary>
    /// <remarks>
    /// Small on purpose. WPF cannot tilt flat content without dragging in the 3D pipeline, so this
    /// is a shift and not a tilt — and a shift that anybody notices stops reading as the map being
    /// alive and starts reading as the map being loose.
    /// </remarks>
    private const double ParallaxReach = 7;

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

    /// <summary>The island drifts a little away from the cursor, which makes it feel like a map.</summary>
    private void OnMapHover(object sender, MouseEventArgs e)
    {
        if (Parallax is null || MapFrame.ActualWidth <= 0 || MapFrame.ActualHeight <= 0)
        {
            return;
        }

        var point = e.GetPosition(MapFrame);

        Parallax.X = -((point.X / MapFrame.ActualWidth) - 0.5) * 2 * ParallaxReach;
        Parallax.Y = -((point.Y / MapFrame.ActualHeight) - 0.5) * 2 * ParallaxReach;
    }

    /// <summary>And goes back to where it was when the cursor leaves, rather than staying askew.</summary>
    private void OnMapLeft(object sender, MouseEventArgs e)
    {
        if (Parallax is null)
        {
            return;
        }

        var home = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Parallax.BeginAnimation(TranslateTransform.XProperty, home);
        Parallax.BeginAnimation(TranslateTransform.YProperty, home);
    }
}
