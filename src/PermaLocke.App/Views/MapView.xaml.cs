using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Views;

public partial class MapView : UserControl
{
    private MapViewModel? _map;

    public MapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Listens for the island changing, which is what the entrance animations hang off.
    /// </summary>
    /// <remarks>
    /// It has to be this and not <c>Loaded</c> on the markers, which is where the cascade started
    /// out: <c>Loaded</c> fires once, when the control enters the tree. Swapping the items source
    /// does not fire it again, so the cascade ran on the first island and never afterwards — and it
    /// looked right precisely because the first time is when anybody would check.
    /// </remarks>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_map is not null)
        {
            _map.PropertyChanged -= OnMapChanged;
        }

        _map = DataContext as MapViewModel;

        if (_map is not null)
        {
            _map.PropertyChanged += OnMapChanged;
        }
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.SelectedIsland))
        {
            Dispatcher.BeginInvoke(EnterIsland, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>The island fades in and its markers land one after another.</summary>
    private void EnterIsland()
    {
        MapSurface.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        Cascade();
    }

    /// <summary>
    /// Fades the markers in one after another.
    /// </summary>
    /// <remarks>
    /// In code because a Storyboard's <c>BeginTime</c> cannot be bound, and a cascade is nothing
    /// but each one starting a little later than the last.
    /// </remarks>
    private void Cascade()
    {
        for (var i = 0; i < Markers.Items.Count; i++)
        {
            if (Markers.ItemContainerGenerator.ContainerFromIndex(i) is not UIElement container)
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
    }

    private void OnMarkersLoaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(Cascade, System.Windows.Threading.DispatcherPriority.Loaded);

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
        if (_map is null || MapSurface.ActualWidth <= 0 || MapSurface.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(MapFrame.ActualWidth / MapSurface.ActualWidth,
            MapFrame.ActualHeight / MapSurface.ActualHeight);

        _map.MarkerScale = scale > 0.01 ? 1 / scale : 1;
    }

    /// <summary>Hovering a legend row lights up the zones in that state and dims the rest.</summary>
    /// <remarks>
    /// The only one of the map's flourishes that also answers a question: with sixty-one markers,
    /// «where have I lost Pokémon» is genuinely hard to see, and this turns it into a hover.
    /// </remarks>
    private void OnLegendEnter(object sender, MouseEventArgs e)
    {
        if (_map is not null && sender is FrameworkElement { Tag: string name }
            && Enum.TryParse<ZoneOutcome>(name, out var outcome))
        {
            _map.Highlight(outcome);
        }
    }

    private void OnLegendLeave(object sender, MouseEventArgs e) => _map?.Highlight(null);
}
