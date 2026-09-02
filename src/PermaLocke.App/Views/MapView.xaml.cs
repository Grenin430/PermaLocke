using System.Windows;
using System.Windows.Controls;
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
}
