using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PermaLocke.App.Services;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// Plays a killcam: the frames at their own times, at real speed or slowed down, looping, with a mark on
/// the timeline where the bar reached zero.
/// </summary>
/// <remarks>
/// <para>
/// Presentation only: what to play is the path the view model gives, and the clip is read on a
/// background thread. Every frame carries its time relative to the fall, so a slowed replay stays true
/// to what happened and a frame the recorder missed leaves a pause instead of shifting the rest.
/// </para>
/// <para>
/// The frames are shown as they were recorded. The red flash when playback passes the mark and the time
/// over the picture are drawn on top of them, never into them.
/// </para>
/// </remarks>
public sealed class KillcamPlayer : ContentControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(KillcamPlayer),
        new PropertyMetadata(null, (d, e) => { _ = ((KillcamPlayer)d).LoadAsync(); }));

    /// <summary>What to do to see it big; without one there is no button to ask for it.</summary>
    public static readonly DependencyProperty ExpandCommandProperty = DependencyProperty.Register(
        nameof(ExpandCommand), typeof(ICommand), typeof(KillcamPlayer),
        new PropertyMetadata(null, (d, e) => ((KillcamPlayer)d).UpdateExpand()));

    /// <summary>Stopped because another player is showing the same clip.</summary>
    public static readonly DependencyProperty SuspendedProperty = DependencyProperty.Register(
        nameof(Suspended), typeof(bool), typeof(KillcamPlayer),
        new PropertyMetadata(false, (d, e) => ((KillcamPlayer)d).OnSuspendedChanged((bool)e.NewValue)));

    private const double SlowSpeed = 0.35;

    /// <summary>The pause on the last frame before starting again.</summary>
    private const double HoldAtEnd = 1200;

    /// <summary>How long the red flash lasts when playback passes the fall.</summary>
    private const double FlashLength = 700;

    private static readonly Color Blood = Color.FromRgb(0xC2, 0x1E, 0x2A);
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Rectangle _progress = new() { Height = 3, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle _deathMark = new() { Width = 2, Height = 11, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _timeline = new() { Height = 18, Margin = new Thickness(0, 6, 0, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
    private readonly Button _play = new() { MinWidth = 96, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Button _back = new() { Content = "◀", MinWidth = 40, Margin = new Thickness(8, 0, 0, 6), ToolTip = "Fotograma anterior" };
    private readonly Button _forward = new() { Content = "▶", MinWidth = 40, Margin = new Thickness(4, 0, 0, 6), ToolTip = "Fotograma siguiente" };
    private readonly ToggleButton _slow = new() { Content = "CÁMARA LENTA", Margin = new Thickness(8, 0, 0, 6) };
    private readonly Button _expand = new() { Content = "AMPLIAR", Padding = new Thickness(10, 5, 16, 10) };
    private readonly Border _expandChip;
    private readonly PixelText _time = new() { Scale = 2, Tight = true, Colour = Color.FromRgb(0xEC, 0xE6, 0xF7) };
    private readonly Border _timeChip;
    private readonly Border _flash = new() { BorderThickness = new Thickness(4), Opacity = 0, IsHitTestVisible = false };
    private readonly PixelText _loading = new() { Text = "Cargando…", Scale = 2, Colour = Color.FromRgb(0x9E, 0x96, 0xB8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private IReadOnlyList<KillcamFrame> _frames = [];
    private double _position;
    private double _lastTick;
    private double _flashAt = double.NegativeInfinity;
    private bool _playing;
    private int _load;

    public KillcamPlayer()
    {
        Focusable = true;
        FocusVisualStyle = null;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

        _progress.SetResourceReference(Shape.FillProperty, "AccentBrush");
        _deathMark.Fill = new SolidColorBrush(Blood);
        _flash.BorderBrush = new SolidColorBrush(Blood);
        _flash.Background = new SolidColorBrush(Color.FromArgb(0x38, Blood.R, Blood.G, Blood.B));

        foreach (var button in new ButtonBase[] { _play, _back, _forward, _expand })
        {
            button.SetResourceReference(StyleProperty, "PxSubtleButton");
            button.Padding = new Thickness(10, 5, 16, 10);
        }

        _slow.SetResourceReference(StyleProperty, "PxToggleButton");
        _slow.Padding = new Thickness(10, 5, 16, 10);

        _timeChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xB8, 0x05, 0x04, 0x08)),
            Padding = new Thickness(7, 5, 7, 5),
            Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = _time
        };

        // Fondo oscuro propio: el botón fantasma es transparente y sobre un cielo de día no se leería.
        _expandChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x05, 0x04, 0x08)),
                        Margin = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
            Child = _expand
        };

        var track = new Rectangle { Height = 3, VerticalAlignment = VerticalAlignment.Center };
        track.SetResourceReference(Shape.FillProperty, "BorderBrush");
        _timeline.Children.Add(track);
        _timeline.Children.Add(_progress);
        _timeline.Children.Add(_deathMark);
        _timeline.SizeChanged += (_, _) => UpdateTimeline();
        _timeline.MouseLeftButtonDown += (_, e) =>
        {
            _timeline.CaptureMouse();
            SeekTo(e.GetPosition(_timeline).X);
            e.Handled = true;
        };
        _timeline.MouseMove += (_, e) =>
        {
            if (_timeline.IsMouseCaptured)
            {
                SeekTo(e.GetPosition(_timeline).X);
            }
        };
        _timeline.MouseLeftButtonUp += (_, _) => _timeline.ReleaseMouseCapture();

        // La pantalla de arriba del juego: 400×240. Se respeta la proporción y no se estira.
        var screen = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x04, 0x08)),
            ClipToBounds = true,
            Child = new Grid { Children = { _image, _loading } }
        };

        var picture = new Viewbox { Stretch = Stretch.Uniform, Child = new Grid { Width = 400, Height = 240, Children = { screen, _flash } } };
        // Centrada y del tamaño de la imagen, para que el tiempo y AMPLIAR vayan en las esquinas del vídeo
        // y no en las de la franja negra cuando sobra sitio.
        var frame = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { picture, _timeChip, _expandChip }
        };
        // En la sala el vídeo mide un metro: el tiempo y el botón crecen con él.
        frame.SizeChanged += (_, _) =>
        {
            var big = frame.ActualWidth > 640;
            _time.Scale = big ? 3 : 2;
            _timeChip.Padding = big ? new Thickness(9, 6, 9, 6) : new Thickness(7, 5, 7, 5);
        };

        frame.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2 && ExpandCommand is { } command && command.CanExecute(null))
            {
                command.Execute(null);
                e.Handled = true;
            }
        };

        _play.Click += (_, _) => Toggle();
        _back.Click += (_, _) => Step(-1);
        _forward.Click += (_, _) => Step(1);
        _expand.Click += (_, _) =>
        {
            if (ExpandCommand is { } command && command.CanExecute(null))
            {
                command.Execute(null);
            }
        };

        // Envuelve en vez de cortar: en la columna estrecha del cementerio CÁMARA LENTA no cabe en la misma línea.
        var controls = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        controls.Children.Add(_play);
        controls.Children.Add(_back);
        controls.Children.Add(_forward);
        controls.Children.Add(_slow);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(controls, Dock.Bottom);
        DockPanel.SetDock(_timeline, Dock.Bottom);
        layout.Children.Add(controls);
        layout.Children.Add(_timeline);
        layout.Children.Add(frame);
        Content = layout;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => Tick(), Dispatcher);

        Unloaded += (_, _) => Stop();
        Loaded += (_, _) =>
        {
            if (_frames.Count > 0 && !Suspended)
            {
                Play();
            }
        };

        ShowPlayLabel();
    }

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ICommand? ExpandCommand
    {
        get => (ICommand?)GetValue(ExpandCommandProperty);
        set => SetValue(ExpandCommandProperty, value);
    }

    public bool Suspended
    {
        get => (bool)GetValue(SuspendedProperty);
        set => SetValue(SuspendedProperty, value);
    }

    private double Start => _frames.Count > 0 ? _frames[0].Milliseconds : 0;

    private double End => _frames.Count > 0 ? _frames[^1].Milliseconds : 0;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Space:
                Toggle();
                e.Handled = true;
                break;
            case Key.Left:
                Step(-1);
                e.Handled = true;
                break;
            case Key.Right:
                Step(1);
                e.Handled = true;
                break;
        }
    }

    private async Task LoadAsync()
    {
        var load = ++_load;
        Stop();
        _frames = [];
        _image.Source = null;
        _loading.Visibility = Visibility.Visible;
        _loading.Text = "Cargando…";
        _timeChip.Visibility = Visibility.Collapsed;
        UpdateTimeline();

        if (Source is not { } path)
        {
            return;
        }

        var frames = await Task.Run(() => KillcamClip.Read(path));

        // Si mientras se leía se eligió otro Pokémon, este clip ya no toca.
        if (load != _load)
        {
            return;
        }

        _frames = frames;
        _loading.Visibility = frames.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _loading.Text = frames.Count == 0 ? "No se ha podido leer la killcam." : string.Empty;
        _timeChip.Visibility = frames.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _position = Start;

        if (frames.Count > 0)
        {
            Show();

            if (!Suspended)
            {
                Play();
            }
        }
    }

    private void UpdateExpand() => _expandChip.Visibility = ExpandCommand is null ? Visibility.Collapsed : Visibility.Visible;

    private void OnSuspendedChanged(bool suspended)
    {
        if (suspended)
        {
            Stop();
        }
        else if (IsLoaded)
        {
            Play();
        }
    }

    private void Toggle()
    {
        if (_playing)
        {
            Stop();
        }
        else
        {
            Play();
        }
    }

    private void Play()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        _playing = true;
        _clock.Restart();
        _lastTick = 0;
        _timer.Start();
        ShowPlayLabel();
    }

    private void Stop()
    {
        _playing = false;
        _timer.Stop();
        _flash.Opacity = 0;
        ShowPlayLabel();
    }

    private void ShowPlayLabel() => _play.Content = _playing ? "PAUSA" : "REPRODUCIR";

    private void Tick()
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        var elapsed = now - _lastTick;
        _lastTick = now;

        var before = _position;
        _position += elapsed * (_slow.IsChecked == true ? SlowSpeed : 1);

        // El fogonazo va en tiempo de pantalla y no del clip: a cámara lenta dura lo mismo.
        if (before < 0 && _position >= 0)
        {
            _flashAt = now;
        }

        _flash.Opacity = Math.Clamp(1 - ((now - _flashAt) / FlashLength), 0, 1);

        // Al final se queda un momento en la caída y vuelve a empezar.
        if (_position > End + HoldAtEnd)
        {
            _position = Start;
        }

        Show();
    }

    /// <summary>One frame back or forward, paused there.</summary>
    private void Step(int direction)
    {
        if (_frames.Count == 0)
        {
            return;
        }

        Stop();

        var current = IndexAt(Math.Min(_position, End));
        var next = Math.Clamp(current + direction, 0, _frames.Count - 1);
        _position = _frames[next].Milliseconds;
        Show();
    }

    private void SeekTo(double x)
    {
        var width = _timeline.ActualWidth;

        if (_frames.Count == 0 || width <= 0)
        {
            return;
        }

        _position = Start + (Math.Clamp(x / width, 0, 1) * (End - Start));
        _flash.Opacity = 0;
        Show();
    }

    private int IndexAt(double target)
    {
        var index = 0;

        for (var i = 0; i < _frames.Count; i++)
        {
            if (_frames[i].Milliseconds > target)
            {
                break;
            }

            index = i;
        }

        return index;
    }

    private void Show()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        var target = Math.Min(_position, End);
        var frame = _frames[IndexAt(target)];

        _image.Source = frame.Image;

        // El tiempo respecto a la caída: negativo antes, cero cuando la barra llegó a cero.
        var seconds = frame.Milliseconds / 1000.0;
        _time.Text = seconds.ToString("+0.0 's';-0.0 's';0.0 's'", Spanish);
        _time.Colour = frame.Milliseconds >= 0 ? Color.FromRgb(0xFF, 0x6B, 0x74) : Color.FromRgb(0xEC, 0xE6, 0xF7);

        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        var width = _timeline.ActualWidth;
        var span = End - Start;

        if (width <= 0 || span <= 0)
        {
            _progress.Width = 0;
            _deathMark.Visibility = Visibility.Collapsed;
            return;
        }

        _progress.Width = Math.Clamp((Math.Min(_position, End) - Start) / span, 0, 1) * width;
        _deathMark.Visibility = Visibility.Visible;
        _deathMark.Margin = new Thickness((Math.Clamp((0 - Start) / span, 0, 1) * width) - 1, 0, 0, 0);
    }
}
