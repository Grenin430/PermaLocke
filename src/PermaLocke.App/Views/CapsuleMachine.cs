using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The gacha's stage: the capsule machine (<see cref="CapsuleMachineScene"/>) on screen, in whole cells.
/// </summary>
/// <remarks>
/// <para>
/// Built like the trainer's room (§169): three screen pixels per cell whatever the DPI, the scene rebuilt only when the
/// panel changes size. It moves at thirty frames a second while it is on screen, because a ball rolling and wobbling at
/// four steps a second, like the room, stutters.
/// </para>
/// <para>
/// It decides nothing and keeps no clock of its own for the pull: <see cref="Play"/> carries the moment the coin dropped,
/// and every frame is drawn from how long ago that was. So leaving the gacha mid-pull and coming back finds the ball
/// exactly where it should be, and the view model — which owns the timeline — never waits on an animation.
/// </para>
/// </remarks>
public sealed class CapsuleMachine : ContentControl
{
    public static readonly DependencyProperty BannerProperty = DependencyProperty.Register(
        nameof(Banner), typeof(CapsuleBanner), typeof(CapsuleMachine),
        new PropertyMetadata(null, (d, _) => ((CapsuleMachine)d).OnBannerChanged()));

    public static readonly DependencyProperty PlayProperty = DependencyProperty.Register(
        nameof(Play), typeof(CapsulePlay), typeof(CapsuleMachine),
        new PropertyMetadata(null, (d, e) => ((CapsuleMachine)d).OnPlayChanged((CapsulePlay?)e.NewValue)));

    public static readonly DependencyProperty ShelfProperty = DependencyProperty.Register(
        nameof(Shelf), typeof(IReadOnlyList<CapsuleShelfItem>), typeof(CapsuleMachine),
        new PropertyMetadata(null, (d, e) => ((CapsuleMachine)d).OnShelfChanged((IReadOnlyList<CapsuleShelfItem>?)e.NewValue)));

    /// <summary>Screen pixels per cell, the same as the room, the beach and the notices.</summary>
    /// <summary>Screen pixels per cell: 3, or 2 when the machine would not fit whole at 3 (like the roulette).</summary>
    private int _cell = 3;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private CapsuleMachineScene? _scene;
    private long _lastStep = -1;
    private double _loadedAt;
    private bool _broken;

    private CapsulePlay? _play;
    private CapsuleRoll? _roll;
    private CapsuleRoll? _departed;
    private double _departedAt;
    private IReadOnlyList<CapsuleShelfItem> _shelf = [];
    private IReadOnlyList<CapsuleShelfItem>? _pendingShelf;
    private double _newestAt = -99;

    public CapsuleMachine()
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

    /// <summary>Raised once if drawing fails, so the screen can log it; the pull itself is never affected.</summary>
    public event Action<Exception>? RenderFailed;

    public CapsuleBanner? Banner
    {
        get => (CapsuleBanner?)GetValue(BannerProperty);
        set => SetValue(BannerProperty, value);
    }

    public CapsulePlay? Play
    {
        get => (CapsulePlay?)GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    public IReadOnlyList<CapsuleShelfItem>? Shelf
    {
        get => (IReadOnlyList<CapsuleShelfItem>?)GetValue(ShelfProperty);
        set => SetValue(ShelfProperty, value);
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnBannerChanged()
    {
        // Otro banner, otra máquina: la cúpula se vuelve a llenar delante del jugador.
        _loadedAt = Now;
        Paint();
    }

    private void OnPlayChanged(CapsulePlay? play)
    {
        if (play is not null)
        {
            // Una tirada nueva: lo que siguiera volando aterriza ya.
            Land();
            _play = play;
            _roll = new CapsuleRoll(play.Steps, RoomSprite.From(play.Sprite), play.Shiny, play.Legendary, play.Seed, play.Streak);
        }
        else if (_play is { } ended && _roll is { } shown && ended.Elapsed >= CapsuleTimeline.Revealed)
        {
            // Ha tenido su tiempo: la ball vuela a la estantería y la figura nueva sube cuando llega.
            _departed = shown;
            _departedAt = Now;
            _play = null;
            _roll = null;
        }
        else
        {
            _play = null;
            _roll = null;
            Land();
        }

        Paint();
    }

    private void OnShelfChanged(IReadOnlyList<CapsuleShelfItem>? shelf)
    {
        // Mientras hay una tirada en escena o volando, la estantería sigue como estaba: la figura nueva sube cuando
        // su ball llega, no en cuanto el historial la apunta.
        if (_roll is not null || _departed is not null)
        {
            _pendingShelf = shelf ?? [];
            return;
        }

        _shelf = shelf ?? [];
        Paint();
    }

    /// <summary>Ends a flight at once, putting the waiting figure on the shelf without its drop.</summary>
    private void Land()
    {
        _departed = null;

        if (_pendingShelf is { } pending)
        {
            _shelf = pending;
            _pendingShelf = null;
        }
    }

    private void Reshape()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        // A 3 la máquina pide 166 filas; en una pantalla baja (portátil con escalado, 2026-09-24) no cabían y se
        // cortaba la cúpula por arriba. Entonces se pinta a 2: más pequeña, entera y en píxeles enteros.
        _cell = Math.Ceiling(ActualHeight * dpi.DpiScaleY / 3) >= CapsuleMachineScene.DesignRows ? 3 : 2;
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / _cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / _cell);

        if (columns < 40 || rows < 40)
        {
            return;
        }

        _zoom.ScaleX = _cell / dpi.DpiScaleX;
        _zoom.ScaleY = _cell / dpi.DpiScaleY;

        if (_scene is { } scene && scene.Width == Math.Max(CapsuleMachineScene.DesignWidth, columns)
            && scene.Height == Math.Max(CapsuleMachineScene.DesignRows, rows))
        {
            return;
        }

        // Más estrecho que el diseño no se encoge: se recorta por los lados, centrado.
        _scene = new CapsuleMachineScene(columns, rows);
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
        if (_scene is null || Banner is not { } banner || _broken)
        {
            return;
        }

        var now = Now;

        if (_departed is not null && now - _departedAt >= CapsuleMachineScene.DepartLength)
        {
            _departed = null;

            if (_pendingShelf is { } pending)
            {
                _shelf = pending;
                _pendingShelf = null;
                _newestAt = now;
            }
        }

        try
        {
            _scene.Render(new CapsuleSceneState(banner, now - _loadedAt, _roll, _play?.Elapsed ?? 0, _shelf,
                _departed, now - _departedAt, now - _newestAt), now);
        }
        catch (Exception ex)
        {
            // Dibujar no puede romper una tirada que ya está escrita: se avisa una vez y la escena se queda quieta.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
