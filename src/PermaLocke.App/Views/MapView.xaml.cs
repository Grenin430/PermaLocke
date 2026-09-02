using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

public partial class MapView : UserControl
{
    public MapView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Drops the chosen zone's marker where the map was clicked.
    /// </summary>
    /// <remarks>
    /// This has to be code-behind: where a click landed is a fact about the rendered surface, and
    /// the view model has no business knowing there is one. What it does is convert the point and
    /// hand it over — the deciding, the saving and the clamping all happen in the view model.
    /// </remarks>
    private void OnMapClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MapViewModel map || !map.IsPlacingMode || sender is not IInputElement surface)
        {
            return;
        }

        var point = e.GetPosition(surface);
        _ = map.PlaceAtAsync(point.X, point.Y);
    }
}
