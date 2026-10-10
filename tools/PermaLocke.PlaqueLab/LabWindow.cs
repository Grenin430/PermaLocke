using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Views;

namespace PermaLocke.PlaqueLab;

/// <summary>
/// The previewer of the cap panel (2026-10-10): an imitated emulator in any window size and layout of the screens, with the panel over
/// its black band through the app's own geometry and pipeline. A scenario plays in a loop; space pauses, the arrows step a frame (a
/// fortieth of a second), Home starts again, P leaves a PNG.
/// </summary>
public sealed class LabWindow : Window
{
    private const double Step = 1.0 / 25;

    private readonly LabData _data;
    private readonly ComboBox _scenario = new() { Width = 300 };
    private readonly ComboBox _window = new() { Width = 300 };
    private readonly ComboBox _mode = new() { Width = 100 };
    private readonly ComboBox _cell = new() { Width = 70 };
    private readonly ComboBox _background = new() { Width = 70 };
    private readonly CheckBox _memorial = new() { Content = "Cementerio", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Slider _zoom = new() { Minimum = 0.2, Maximum = 3, Value = 0.6, Width = 140, TickFrequency = 0.1, IsSnapToTickEnabled = false };
    private readonly Slider _speed = new() { Minimum = 0.05, Maximum = 3, Value = 1, Width = 110 };
    private readonly TextBlock _info = new() { Margin = new Thickness(8, 4, 8, 4), FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
    private readonly Image _image = new() { Stretch = Stretch.None, SnapsToDevicePixels = true };
    private readonly Button _pause = new() { Content = "Pausa", Width = 60 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private WriteableBitmap? _bitmap;
    private double _t;
    private double _last;
    private bool _paused;
    private CapMode? _previous;

    public LabWindow(string[] args, LabData data)
    {
        _data = data;
        Title = "Laboratorio del panel del cap";
        Width = 1400;
        Height = 900;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 34));

        foreach (var s in data.Scenarios) _scenario.Items.Add(s.Name + " · " + s.Title);
        foreach (var p in LabScreen.Presets) _window.Items.Add(p);
        foreach (var m in new[] { "Solo", "Compacto", "Estrecho", "Completo" }) _mode.Items.Add(m);
        foreach (var c in new[] { "Solo", "x2", "x3", "x4", "x5" }) _cell.Items.Add(c);
        foreach (var b in new[] { "Negro", "Juego" }) _background.Items.Add(b);
        _scenario.SelectedIndex = Pick(args, "--escenario", data.Scenarios.Select(s => s.Name).ToList());
        _window.SelectedIndex = 0;
        _mode.SelectedIndex = 0;
        _cell.SelectedIndex = 0;
        _background.SelectedIndex = 1;
        if (Array.IndexOf(args, "--escala") is var z and >= 0 && z + 1 < args.Length && double.TryParse(args[z + 1], System.Globalization.CultureInfo.InvariantCulture, out var zoom))
            _zoom.Value = Math.Clamp(zoom, 0.2, 3);

        var bar = new WrapPanel { Margin = new Thickness(6) };
        void Add(string label, UIElement control)
        {
            bar.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Gainsboro, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) });
            bar.Children.Add(control);
        }

        Add("Escenario", _scenario);
        Add("Ventana", _window);
        Add("Modo", _mode);
        Add("Celda", _cell);
        Add("Fondo", _background);
        bar.Children.Add(_memorial);
        Add("Zoom", _zoom);
        Add("Velocidad", _speed);
        var restart = new Button { Content = "Inicio", Width = 56, Margin = new Thickness(8, 0, 0, 0) };
        var back = new Button { Content = "◀", Width = 30, Margin = new Thickness(4, 0, 0, 0) };
        var forward = new Button { Content = "▶", Width = 30, Margin = new Thickness(4, 0, 0, 0) };
        var save = new Button { Content = "PNG", Width = 46, Margin = new Thickness(8, 0, 0, 0) };
        var all = new Button { Content = "Todo a PNG", Width = 80, Margin = new Thickness(4, 0, 0, 0) };
        _pause.Margin = new Thickness(8, 0, 0, 0);
        foreach (var b in new UIElement[] { _pause, restart, back, forward, save, all }) bar.Children.Add(b);
        _info.Foreground = Brushes.Gainsboro;

        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _image, Background = Brushes.Black
        };
        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_info, Dock.Bottom);
        dock.Children.Add(bar);
        dock.Children.Add(_info);
        dock.Children.Add(scroll);
        Content = dock;

        _zoom.ValueChanged += (_, _) => ApplyZoom();
        _pause.Click += (_, _) => TogglePause();
        restart.Click += (_, _) => Restart();
        back.Click += (_, _) => Move(-Step);
        forward.Click += (_, _) => Move(Step);
        save.Click += (_, _) => SavePng();
        all.Click += (_, _) => ExportAll();
        foreach (var c in new System.Windows.Controls.Primitives.Selector[] { _scenario, _window, _mode, _cell, _background })
        {
            c.SelectionChanged += (_, _) => { if (ReferenceEquals(c, _scenario)) Restart(); else Redraw(); };
        }

        _memorial.Click += (_, _) => Redraw();
        PreviewKeyDown += OnKey;
        _timer.Tick += (_, _) => Advance();
        Loaded += (_, _) =>
        {
            ApplyZoom();
            _last = _clock.Elapsed.TotalSeconds;
            _timer.Start();
        };
    }

    private static int Pick(string[] args, string flag, List<string> names)
    {
        var at = Array.IndexOf(args, flag);
        return at >= 0 && at + 1 < args.Length && names.IndexOf(args[at + 1]) is var i and >= 0 ? i : 0;
    }

    private CapScenario Scenario => _data.Scenarios[Math.Max(0, _scenario.SelectedIndex)];

    private Preset Preset => (Preset)_window.SelectedItem;

    private CapOptions Options() => new(
        _mode.SelectedIndex switch { 1 => CapMode.Compact, 2 => CapMode.Slim, 3 => CapMode.Column, _ => (CapMode?)null },
        _cell.SelectedIndex == 0 ? 0 : _cell.SelectedIndex + 1,
        _memorial.IsChecked == true);

    private void ApplyZoom() => _image.LayoutTransform = new ScaleTransform(_zoom.Value, _zoom.Value);

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space: TogglePause(); break;
            case Key.Left: Move(-Step); break;
            case Key.Right: Move(Step); break;
            case Key.Home: Restart(); break;
            case Key.P: SavePng(); break;
            default: return;
        }

        e.Handled = true;
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pause.Content = _paused ? "Seguir" : "Pausa";
    }

    private void Restart()
    {
        _t = 0;
        _previous = null;
        Redraw();
    }

    private void Move(double seconds)
    {
        _paused = true;
        _pause.Content = "Seguir";
        _t = Math.Max(0, _t + seconds);
        Redraw();
    }

    private void Advance()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _last;
        _last = now;
        if (_paused) return;

        _t += dt * _speed.Value;
        var length = Scenario.Length + 1;
        if (_t > length) _t -= length;
        Redraw();
    }

    private void Redraw()
    {
        if (_window.SelectedItem is null) return;

        var watch = Stopwatch.StartNew();
        var preset = Preset;
        var (bgra, plan) = LabScreen.Compose(Scenario, _t, preset, Options(), _background.SelectedIndex == 1, 1, _previous);
        _previous = plan.Main?.Mode;
        var cost = watch.Elapsed.TotalMilliseconds;

        if (_bitmap is null || _bitmap.PixelWidth != preset.Width || _bitmap.PixelHeight != preset.Height)
        {
            _bitmap = new WriteableBitmap(preset.Width, preset.Height, 96, 96, PixelFormats.Bgra32, null);
            _image.Source = _bitmap;
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, preset.Width, preset.Height), bgra, preset.Width * 4, 0);

        var main = plan.Main;
        var memorial = plan.Memorial;
        _info.Text = $"t = {_t,5:F2} s de {Scenario.Length:F1}   ·   {plan.Why}\n"
            + $"party: {(main is null ? "—" : $"{main.Mode} {main.Cols}×{main.Rows} celdas de {main.Cell} px = {main.Box.Width}×{main.Box.Height} px en ({main.Box.Left},{main.Box.Top})")}"
            + $"   ·   memorial: {(memorial is null ? "—" : $"{memorial.Mode} {memorial.Cols}×{memorial.Rows} en ({memorial.Box.Left},{memorial.Box.Top})")}\n"
            + $"juego: ({plan.Content.Left},{plan.Content.Top}) {plan.Content.Width}×{plan.Content.Height}   ·   fotograma del laboratorio: {cost:F1} ms (incluye pintar la imagen fija de cada panel; el coste real es --medir)"
            + (_data.RealIcons ? string.Empty : "   ·   iconos de relleno (sin ROM)");
    }

    private void SavePng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "permalocke-plaque");
        Directory.CreateDirectory(dir);
        var (bgra, _) = LabScreen.Compose(Scenario, _t, Preset, Options(), _background.SelectedIndex == 1);
        var file = Path.Combine(dir, $"{Scenario.Name}-{_t:F2}.png".Replace(',', '.'));
        CapSnap.Save(file, bgra, Preset.Width, Preset.Height);
        _info.Text = "Guardado: " + file;
    }

    private void ExportAll()
    {
        var dir = Path.Combine(Path.GetTempPath(), "permalocke-plaque");
        var count = Exports.All(dir, _data);
        _info.Text = $"{count} PNG en {dir}";
    }
}
