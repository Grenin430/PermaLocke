using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The wonder trade's stage: the trade cabin (<see cref="TradeMachineScene"/>) on screen, in whole cells.
/// </summary>
/// <remarks>
/// Built like the capsule machine (§171): three screen pixels per cell whatever the DPI, the scene rebuilt only when the
/// panel changes size, thirty frames a second while it is on screen. It keeps no clock of its own for the trade:
/// <see cref="Play"/> carries the moment it was confirmed and every frame is drawn from how long ago that was.
/// </remarks>
public sealed class TradeMachine : ContentControl
{
    public static readonly DependencyProperty PlayProperty = DependencyProperty.Register(
        nameof(Play), typeof(TradePlay), typeof(TradeMachine),
        new PropertyMetadata(null, (d, e) => ((TradeMachine)d).OnPlayChanged((TradePlay?)e.NewValue)));

    /// <summary>Screen pixels per cell, the same as the room, the gacha and the notices.</summary>
    private const int Cell = 3;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TradeMachineScene? _scene;
    private TradePlay? _play;
    private TradeShow? _show;
    private long _lastStep = -1;
    private bool _broken;

    public TradeMachine()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Children = { _image } };

        SizeChanged += (_, _) => Reshape();
        // WPF puede lanzar Loaded otra vez sin Unloaded entre medias (al volver a la sección): sin quitarlo antes, el
        // fotograma se apuntaba dos o tres veces y la animación iba x2 o x3 (1.0.4.6).
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            CompositionTarget.Rendering += OnFrame;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>Raised once if drawing fails, so the screen can log it; the trade itself is never affected.</summary>
    public event Action<Exception>? RenderFailed;

    public TradePlay? Play
    {
        get => (TradePlay?)GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnPlayChanged(TradePlay? play)
    {
        _play = play;
        _show = play is null ? null : ShowOf(play);
        Paint();
    }

    /// <summary>What the scene draws, converted once: sprites into cells and the ball into one the scene knows.</summary>
    public static TradeShow ShowOf(TradePlay play) => new(
        RoomSprite.From(play.GivenSprite),
        TradeMachineScene.BallFor(play.GivenBall),
        RoomSprite.From(play.GivenBallIcon),
        RoomSprite.From(play.ReceivedSprite),
        play.Generation,
        play.Types,
        play.GivenTotal,
        play.Total,
        play.Difference,
        play.Shiny,
        play.Legendary,
        play.Seed);

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

        if (_scene is { } scene && scene.Width == Math.Max(TradeMachineScene.DesignWidth, columns)
            && scene.Height == Math.Max(TradeMachineScene.DesignRows, rows))
        {
            return;
        }

        // Más estrecho que el diseño no se encoge: se recorta por los lados, centrado.
        _scene = new TradeMachineScene(columns, rows);
        _image.Source = _scene.Bitmap;
        _image.Width = _scene.Width;
        _image.Height = _scene.Height;
        _image.Margin = new Thickness(Math.Min(0, (columns - _scene.Width) / 2.0 * Cell / dpi.DpiScaleX), 0, 0, 0);
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
            _scene.Render(_show, _play?.Elapsed ?? 0, _clock.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Dibujar no puede romper un intercambio que ya está escrito: se avisa una vez y la escena se queda quieta.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
