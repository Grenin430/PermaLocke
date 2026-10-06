using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// JUGAR's cover: the trainer's room (<see cref="TrainerRoomScene"/>) on screen, in whole cells.
/// </summary>
/// <remarks>
/// Built like the beach it replaces (§125, §169): three screen pixels per cell whatever the DPI, the scene rebuilt only
/// when the panel changes size, and a new frame four times a second while it is visible. The hour comes apart from the
/// rest because it changes every minute, and the room should not be read again for it.
/// </remarks>
public sealed class TrainerRoom : ContentControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(TrainerRoomState), typeof(TrainerRoom), new PropertyMetadata(null, Repaint));

    public static readonly DependencyProperty HourProperty = DependencyProperty.Register(
        nameof(Hour), typeof(double), typeof(TrainerRoom), new PropertyMetadata(12.0, Repaint));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(TrainerRoom), new PropertyMetadata(false, Repaint));

    /// <summary>Pixels at the bottom drawn over by something else: JUGAR's play bar overlaps the cover.</summary>
    public static readonly DependencyProperty CoveredBottomProperty = DependencyProperty.Register(
        nameof(CoveredBottom), typeof(double), typeof(TrainerRoom), new PropertyMetadata(0.0, (d, _) => ((TrainerRoom)d).Reshape()));

    /// <summary>Screen pixels per cell, the same as the beach and the notices.</summary>
    private const int Cell = 3;

    /// <summary>What the room shows with no run yet: an empty room, which is true.</summary>
    private static readonly TrainerRoomState Empty = new(12, [], [], 0, [], false, 0, 0, 25, 0, null);

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TrainerRoomScene? _scene;
    private int _covered = -1;
    private long _lastStep = -1;

    public TrainerRoom()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Children = { _image } };

        SizeChanged += (_, _) => Reshape();
        // Un temporizador a su ritmo y no CompositionTarget.Rendering, que obliga a pintar a 60 fps (ver AlolaBanner).
        _timer.Tick += OnFrame;
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public TrainerRoomState? State
    {
        get => (TrainerRoomState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public double Hour
    {
        get => (double)GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double CoveredBottom
    {
        get => (double)GetValue(CoveredBottomProperty);
        set => SetValue(CoveredBottomProperty, value);
    }

    private static void Repaint(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TrainerRoom)d).Paint();

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void Reshape()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / Cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / Cell);

        if (columns < 40 || rows < 40)
        {
            return;
        }

        _zoom.ScaleX = Cell / dpi.DpiScaleX;
        _zoom.ScaleY = Cell / dpi.DpiScaleY;

        var covered = (int)Math.Ceiling(CoveredBottom * dpi.DpiScaleY / Cell);

        if (_scene is { } scene && covered == _covered && scene.Width == Math.Max(TrainerRoomScene.DesignWidth, columns)
            && scene.Height == Math.Max(TrainerRoomScene.DesignRows + covered, rows))
        {
            return;
        }

        // Más estrecha que el diseño no se encoge: se recorta por los lados, centrada.
        _covered = covered;
        _scene = new TrainerRoomScene(columns, rows, covered);
        _image.Source = _scene.Bitmap;
        _image.Width = _scene.Width;
        _image.Height = _scene.Height;
        _image.Margin = new Thickness(Math.Min(0, (columns - _scene.Width) / 2.0 * Cell / dpi.DpiScaleX), 0, 0, 0);
        Paint();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var step = _clock.ElapsedMilliseconds / 250;

        if (OnScreen.Showing(this) && step != _lastStep)
        {
            _lastStep = step;
            Paint();
        }
    }

    private void Paint()
    {
        if (_scene is null)
        {
            return;
        }

        var state = (State ?? Empty) with { Hour = Hour };
        _scene.Render(state, _clock.ElapsedMilliseconds / 1000.0, still: IsPlaying);
    }
}
