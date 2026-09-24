using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The LUDÓPATA wheel on screen: <see cref="RouletteMachineScene"/> in whole cells.
/// </summary>
/// <remarks>
/// <para>
/// Built like the capsule machine (§171) and the trade cabin (§174): the scene rebuilt only when the panel changes
/// size, thirty frames a second while it is on screen, and no clock of its own for the spin — <see cref="Play"/>
/// carries the moment it was confirmed and every frame is drawn from how long ago that was.
/// </para>
/// <para>
/// Three screen pixels per cell like the rest, unless the panel is too short to hold the whole wheel at three; then
/// two. Always a whole number, so the pixels stay pixels: the old wheel had to shrink through a <c>Viewbox</c>
/// because at full size its rim was cut off (§84), and a scaled bitmap would blur.
/// </para>
/// </remarks>
public sealed class RouletteMachine : ContentControl
{
    public static readonly DependencyProperty PlayProperty = DependencyProperty.Register(
        nameof(Play), typeof(RoulettePlay), typeof(RouletteMachine),
        new PropertyMetadata(null, (d, e) => ((RouletteMachine)d).OnPlayChanged((RoulettePlay?)e.NewValue)));

    public static readonly DependencyProperty BoardProperty = DependencyProperty.Register(
        nameof(Board), typeof(IReadOnlyList<RouletteBoardItem>), typeof(RouletteMachine),
        new PropertyMetadata(null, (d, _) => ((RouletteMachine)d).Paint()));

    public static readonly DependencyProperty OwedProperty = DependencyProperty.Register(
        nameof(Owed), typeof(int), typeof(RouletteMachine),
        new PropertyMetadata(0, (d, _) => ((RouletteMachine)d).Paint()));

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private RouletteMachineScene? _scene;
    private RoulettePlay? _play;
    private RouletteShow? _show;
    private int _cell = 3;
    private long _lastStep = -1;
    private bool _broken;

    public RouletteMachine()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Children = { _image } };

        SizeChanged += (_, _) => Reshape();
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>Raised once if drawing fails, so the screen can log it; the spin itself is never affected.</summary>
    public event Action<Exception>? RenderFailed;

    public RoulettePlay? Play
    {
        get => (RoulettePlay?)GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    public IReadOnlyList<RouletteBoardItem>? Board
    {
        get => (IReadOnlyList<RouletteBoardItem>?)GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public int Owed
    {
        get => (int)GetValue(OwedProperty);
        set => SetValue(OwedProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnPlayChanged(RoulettePlay? play)
    {
        _play = play;
        _show = play is null ? null : ShowOf(play);
        Paint();
    }

    /// <summary>What the scene draws, converted once: the pictures into cells.</summary>
    public static RouletteShow ShowOf(RoulettePlay play) => new(
        [.. play.Wedges.Select(w => new RouletteWedge(w.Label, w.Figure, w.Good, RoomSprite.From(w.Icon), w.FaceId))],
        play.WinningIndex,
        play.Ending,
        play.StartAngle,
        play.Seed,
        play.OwedBefore);

    private void Reshape()
    {
        var dpi = VisualTreeHelper.GetDpi(this);

        // Tres píxeles por celda si la rueda entera cabe así; si no, dos.
        _cell = Math.Ceiling(ActualHeight * dpi.DpiScaleY / 3) >= RouletteMachineScene.DesignRows ? 3 : 2;

        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / _cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / _cell);

        if (columns < 40 || rows < 40)
        {
            return;
        }

        _zoom.ScaleX = _cell / dpi.DpiScaleX;
        _zoom.ScaleY = _cell / dpi.DpiScaleY;

        if (_scene is { } scene && scene.Width == Math.Max(RouletteMachineScene.DesignWidth, columns)
            && scene.Height == Math.Max(RouletteMachineScene.DesignRows, rows))
        {
            return;
        }

        // Más estrecho que el diseño no se encoge: se recorta por los lados, centrado.
        _scene = new RouletteMachineScene(columns, rows);
        _image.Source = _scene.Bitmap;
        _image.Width = _scene.Width;
        _image.Height = _scene.Height;
        _image.Margin = new Thickness(Math.Min(0, (columns - _scene.Width) / 2.0 * _cell / dpi.DpiScaleX), 0, 0, 0);
        Paint();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var step = _clock.ElapsedMilliseconds / 33;

        if (IsVisible && step != _lastStep)
        {
            _lastStep = step;
            Paint();
        }
    }

    private void Paint()
    {
        if (_scene is null || _broken)
        {
            return;
        }

        try
        {
            _scene.Render(new RouletteSceneState(Board ?? [], _show, _play?.Elapsed ?? 0, Owed), _clock.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Dibujar no puede romper una tirada que ya está escrita: se avisa una vez y la escena se queda quieta.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
