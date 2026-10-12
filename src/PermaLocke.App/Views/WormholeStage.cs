using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// One opening of the portal, as the randomizer's screen tells it (2026-10-12): when it started, the steps the randomizer has
/// finished and how it ended. Only touched from the window's thread.
/// </summary>
/// <remarks>
/// The show has a fixed length (the player asked for it: a quick randomizer squeezed the whole thing into a couple of seconds and
/// it looked worse than the first, slow one). The steps are the real ones, in their order, but each is shown at least
/// <see cref="StepGap"/> after the one before and never before <see cref="FirstStep"/>; the finale is never before
/// <see cref="FinaleNotBefore"/>. A randomizer slower than that is waited for: the portal never says it is done before it is.
/// </remarks>
public sealed class WormholeShow(IReadOnlyList<RoomSprite> sprites)
{
    public const double FirstStep = 2.2;
    public const double StepGap = 0.75;
    public const double FinaleNotBefore = 9.0;
    private const double AfterLastStep = 0.7;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<(double At, string Name)> _steps = [];
    private double? _finaleAt;

    public IReadOnlyList<RoomSprite> Sprites { get; } = sprites;
    public double Since => _clock.Elapsed.TotalSeconds;
    public bool Failed { get; private set; }

    /// <summary>A step of the randomizer is done: it gets its pulse on the show's own clock.</summary>
    public void Step(string name)
    {
        var at = Math.Max(Since, _steps.Count == 0 ? FirstStep : _steps[^1].At + StepGap);
        _steps.Add((at, name));
    }

    /// <summary>The randomizer ended; returns how long until the portal is gone.</summary>
    public double End(bool failed)
    {
        Failed = failed;
        var afterSteps = _steps.Count > 0 ? _steps[^1].At + AfterLastStep : 0;
        _finaleAt = failed
            ? Math.Max(Math.Max(Since, UltraWormholeScene.OpenLength), afterSteps)
            : Math.Max(Math.Max(Since, FinaleNotBefore), afterSteps);
        return _finaleAt.Value - Since + (failed ? UltraWormholeScene.FailLength : UltraWormholeScene.FinaleLength);
    }

    public WormholeState State()
    {
        var now = Since;
        var shown = _steps.Where(s => s.At <= now).ToList();
        var finale = _finaleAt is { } at && now >= at ? at : (double?)null;
        return new WormholeState(now, [.. shown.Select(s => s.At)], finale, Failed && finale is not null, shown.Count > 0 ? shown[^1].Name : null, Sprites);
    }
}

/// <summary>
/// The portal of the randomizer on screen, over the section, in whole cells: three screen pixels per cell (two on a low panel),
/// thirty frames a second while there is a show, nothing at all when there is none.
/// </summary>
public sealed class WormholeStage : ContentControl
{
    public static readonly DependencyProperty ShowProperty = DependencyProperty.Register(
        nameof(Show), typeof(WormholeShow), typeof(WormholeStage),
        new PropertyMetadata(null, (d, _) => ((WormholeStage)d).OnShowChanged()));

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private UltraWormholeScene? _scene;
    private long _lastStep = -1;
    private bool _hooked;

    public WormholeStage()
    {
        IsHitTestVisible = true;
        Visibility = Visibility.Collapsed;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Background = new SolidColorBrush(Color.FromRgb(0x07, 0x05, 0x0F)), Children = { _image } };
        SizeChanged += (_, _) => Reshape();
        Unloaded += (_, _) => Unhook();
    }

    public WormholeShow? Show
    {
        get => (WormholeShow?)GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    private void OnShowChanged()
    {
        if (Show is null)
        {
            Visibility = Visibility.Collapsed;
            Unhook();
            return;
        }

        Visibility = Visibility.Visible;
        if (!_hooked)
        {
            CompositionTarget.Rendering += OnFrame;
            _hooked = true;
        }

        Reshape();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        CompositionTarget.Rendering -= OnFrame;
        _hooked = false;
    }

    private void Reshape()
    {
        if (ActualWidth < 10 || ActualHeight < 10) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        // As many screen pixels per cell as fit the design whole: the scene stays near 320x180 cells whatever the window, so a
        // frame costs the same few milliseconds on a big screen (at three pixels per cell a maximized window was three times the
        // cells and the frames came late: the stutter of 2026-10-12).
        var cell = (int)Math.Max(2, Math.Floor(Math.Min(ActualWidth * dpi.DpiScaleX / UltraWormholeScene.DesignWidth,
            ActualHeight * dpi.DpiScaleY / UltraWormholeScene.DesignRows)));
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / cell);
        _zoom.ScaleX = cell / dpi.DpiScaleX;
        _zoom.ScaleY = cell / dpi.DpiScaleY;

        var width = Math.Max(UltraWormholeScene.DesignWidth, columns);
        var height = Math.Max(UltraWormholeScene.DesignRows, rows);
        if (_scene is { } scene && scene.Width == width && scene.Height == height) return;

        _scene = new UltraWormholeScene(columns, rows);
        _image.Source = _scene.Bitmap;
        _image.Width = _scene.Width;
        _image.Height = _scene.Height;
        _image.Margin = new Thickness(Math.Min(0, (columns - _scene.Width) / 2.0 * cell / dpi.DpiScaleX),
            Math.Min(0, (rows - _scene.Height) / 2.0 * cell / dpi.DpiScaleY), 0, 0);
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var step = _clock.ElapsedMilliseconds / 33;
        if (step == _lastStep || _scene is null || Show is not { } show) return;
        _lastStep = step;

        try
        {
            _scene.Render(show.State());
        }
        catch (Exception)
        {
            // Dibujar nunca puede romper la randomización: el portal se quita y el trabajo sigue.
            Visibility = Visibility.Collapsed;
            Unhook();
        }
    }
}
