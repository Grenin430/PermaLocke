using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PermaLocke.App.ViewModels;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Views;

public partial class MapView : UserControl
{
    private MapViewModel? _map;

    /// <summary>The island whose entrance has already played, so it does not play twice.</summary>
    private object? _cascadedFor;

    public MapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnVisibleChanged;
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
            _cascadedFor = null;
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
    /// Drops the markers onto the map one after another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In code because a Storyboard's <c>BeginTime</c> cannot be bound, and a cascade is nothing
    /// but each one starting a little later than the last.
    /// </para>
    /// <para>
    /// It is written with key frames, and that is half the fix. A plain <c>DoubleAnimation</c> with
    /// <c>From = 0</c> and a <c>BeginTime</c> does <b>not</b> hold the marker at zero while it
    /// waits its turn: until its clock starts, the property shows its own value, which is one. So
    /// the markers were all on screen from the first frame and then popped out and back in one by
    /// one — the opposite of a cascade. A discrete frame at zero, held until the marker's turn, is
    /// what actually keeps it hidden.
    /// </para>
    /// <para>
    /// The other half is that it has to be <b>seen</b>. The first version was right and still read
    /// as nothing: a 240 ms fade every 16 ms over fifteen markers is over in half a second, and a
    /// fade with no movement in it does not look like an entrance, it looks like the screen
    /// finishing loading. So the marker now lands — it scales up past its size and settles — and
    /// the turns are far enough apart to see them arrive one by one.
    /// </para>
    /// <para>
    /// The transform origin is the container's <b>top left</b> and not its centre, which looks
    /// wrong and is not. The button carries <c>Margin="-13,-13"</c> so that the circle sits on the
    /// point that was clicked instead of hanging off its corner, which puts the pin's centre
    /// exactly on the container's origin. Scaling about the middle of the container would grow the
    /// pin off its own zone.
    /// </para>
    /// <para>
    /// And <see cref="FrameworkElement.UpdateLayout"/> first, because swapping the items source
    /// does not generate the containers there and then: without it <c>ContainerFromIndex</c>
    /// answers null for every index and the cascade silently animates nothing.
    /// </para>
    /// </remarks>
    private void Cascade()
    {
        if (_map?.SelectedIsland is not { } island || ReferenceEquals(island, _cascadedFor))
        {
            return;
        }

        Markers.UpdateLayout();

        var landing = TimeSpan.FromMilliseconds(320);
        var animated = 0;

        for (var i = 0; i < Markers.Items.Count; i++)
        {
            if (Markers.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            var turn = TimeSpan.FromMilliseconds(i * 34);

            var scale = new ScaleTransform(Landing, Landing);
            container.RenderTransformOrigin = new Point(0, 0);
            container.RenderTransform = scale;

            container.BeginAnimation(OpacityProperty, Entrance(0, 1, turn, landing, null));

            var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 1.1 };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Entrance(Landing, 1, turn, landing, settle));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Entrance(Landing, 1, turn, landing, settle));

            animated++;
        }

        // Solo se da por hecha si de verdad se animó algo: marcarla habiendo animado cero dejaría
        // a la isla sin entrada para siempre, que es peor que repetirla.
        if (animated > 0)
        {
            _cascadedFor = island;
        }
    }

    /// <summary>How small a marker starts before it lands.</summary>
    private const double Landing = 0.3;

    /// <summary>Holds <paramref name="from"/> until this marker's turn, then moves to it.</summary>
    private static DoubleAnimationUsingKeyFrames Entrance(double from, double to, TimeSpan turn,
        TimeSpan travel, IEasingFunction? easing)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = turn + travel };

        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(turn)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(turn + travel), easing));

        return animation;
    }

    /// <summary>
    /// The very first island, whose containers do not exist yet when it is selected.
    /// </summary>
    /// <remarks>
    /// <c>Loaded</c> fires once, when the control enters the tree, so it cannot drive the cascade
    /// on its own — that was the previous bug. It is still needed for the first island, and the
    /// guard in <see cref="Cascade"/> is what keeps the two from running it twice.
    /// </remarks>
    private void OnMarkersLoaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(Cascade, System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>Coming back to the map plays the entrance again, as if it had just opened.</summary>
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _cascadedFor = null;
            Dispatcher.BeginInvoke(EnterIsland, System.Windows.Threading.DispatcherPriority.Loaded);
        }
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
