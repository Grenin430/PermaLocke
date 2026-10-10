using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Views;

namespace PermaLocke.ItemLab;

/// <summary>
/// The previewer of the item animations (2026-10-09): every category playing in a loop side by side, or one alone, with the
/// speed, the size of the game's pixel, the power, the counter that makes the seed, and frame by frame. Space pauses,
/// the arrows step a frame, Home starts again.
/// </summary>
public sealed class LabWindow : Window
{
    /// <summary>Seconds of nothing between two loops of a tile, so that the dither going out is seen before it starts again.</summary>
    private const double Rest = 0.8;
    private const double Frame = 1 / 60.0;

    private static readonly Dictionary<ItemCategory, string> Names = new()
    {
        [ItemCategory.Misc] = "Varios", [ItemCategory.Machine] = "MT / MO", [ItemCategory.Berry] = "Bayas",
        [ItemCategory.Healing] = "Curativos", [ItemCategory.Boost] = "Mejoras", [ItemCategory.Evolution] = "Evolutivas",
        [ItemCategory.MegaStone] = "Megapiedras", [ItemCategory.Battle] = "Combate", [ItemCategory.PokeBall] = "Poke Balls",
        [ItemCategory.Key] = "Clave", [ItemCategory.ZCrystal] = "Cristales Z"
    };

    /// <summary>The item each category shows when nobody picks one: something everybody knows.</summary>
    private static readonly Dictionary<ItemCategory, int> Usual = new()
    {
        [ItemCategory.Misc] = 92, [ItemCategory.Machine] = 328, [ItemCategory.Berry] = 157, [ItemCategory.Healing] = 17,
        [ItemCategory.Boost] = 50, [ItemCategory.Evolution] = 83, [ItemCategory.MegaStone] = 656, [ItemCategory.Battle] = 220,
        [ItemCategory.PokeBall] = 4, [ItemCategory.Key] = 797, [ItemCategory.ZCrystal] = 807
    };

    /// <summary>The three stones the Megapiedra view shows: fire (Blazikenita), water (Swampertita) and one of the mod (Chesnaughtita).</summary>
    private static readonly int[] MegaPicks = [664, 752, 1011];

    /// <summary>
    /// What each category shows when it is looked at alone (--solo, or the category chosen with no item): a few items that
    /// differ in what the style varies with: power, type, kind of item.
    /// </summary>
    private static readonly Dictionary<ItemCategory, int[]> Picks = new()
    {
        [ItemCategory.Misc] = [92, 88, 63],
        [ItemCategory.Machine] = [328, 340, 420],
        [ItemCategory.Berry] = [157, 174, 149],
        [ItemCategory.Healing] = [17, 26, 25, 24, 28],
        [ItemCategory.Boost] = [50, 45, 51, 113],
        [ItemCategory.Evolution] = [83, 80, 81, 221],
        [ItemCategory.MegaStone] = MegaPicks,
        [ItemCategory.Battle] = [220, 270, 548],
        [ItemCategory.PokeBall] = [4, 3, 2, 1, 576],
        [ItemCategory.Key] = [216, 114, 115],
        [ItemCategory.ZCrystal] = [807, 808, 809, 825]
    };

    /// <summary>The names --solo understands.</summary>
    private static readonly Dictionary<string, ItemCategory> SoloNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["varios"] = ItemCategory.Misc, ["mt"] = ItemCategory.Machine, ["bayas"] = ItemCategory.Berry,
        ["curativos"] = ItemCategory.Healing, ["mejoras"] = ItemCategory.Boost, ["evolutivas"] = ItemCategory.Evolution,
        ["mega"] = ItemCategory.MegaStone, ["combate"] = ItemCategory.Battle, ["balls"] = ItemCategory.PokeBall,
        ["clave"] = ItemCategory.Key, ["z"] = ItemCategory.ZCrystal
    };

    /// <summary>Moments are fractions of the length of each item (0 to 1) and not seconds: categories last different times.</summary>
    private bool _relative;

    private sealed record Tile(ItemScene Scene, ItemScene.Item Item, WriteableBitmap Bitmap, double Length);

    private readonly LabData _data;
    private readonly List<Tile> _tiles = [];
    private readonly WrapPanel _board = new() { Margin = new Thickness(8) };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(8) };
    private readonly ComboBox _category = new() { Width = 130 };
    private readonly ComboBox _item = new() { Width = 260 };
    private readonly ComboBox _power = new() { Width = 70 };
    private readonly ComboBox _amount = new() { Width = 55 };
    private readonly TextBox _counter = new() { Width = 40, Text = "0", VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Slider _speed = new() { Minimum = 0.05, Maximum = 2, Value = 1, Width = 130 };
    private readonly Slider _scale = new() { Minimum = 1, Maximum = 6, Value = 2, Width = 130, SmallChange = 0.1, TickFrequency = 0.1, IsSnapToTickEnabled = true };
    private readonly Slider _scrub = new() { Minimum = 0, Maximum = 5, Width = 320 };
    private readonly TextBlock _info = new() { Foreground = Brushes.Gainsboro, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _play = new() { Content = "Pausa", Width = 60 };

    private DateTime _last = DateTime.UtcNow;
    private double _clock;
    private bool _paused;
    private bool _loading = true;

    /// <summary>What the scene is shown over: the lab's own dark, pure black like the emulator's band, or a stand-in for the game's picture.</summary>
    private string _backdrop = "lab";

    /// <summary>
    /// The background of a scene. «negro» is what the band beside the emulator's bottom screen is: what only darkens shows nothing
    /// there, and whatever lights a cell there is plain to see. «juego» is a field of grass and path in two greens and a sand,
    /// which the scene may overlap in a small window of the emulator.
    /// </summary>
    private Brush Backdrop(double scale)
    {
        if (_backdrop == "negro") return Brushes.Black;
        if (_backdrop != "juego") return new SolidColorBrush(Color.FromRgb(0x1C, 0x1A, 0x24));

        static GeometryDrawing Block(double x, double y, Color colour) =>
            new(new SolidColorBrush(colour), null, new RectangleGeometry(new Rect(x, y, 8, 8)));

        var tile = new DrawingGroup();
        tile.Children.Add(Block(0, 0, Color.FromRgb(0x58, 0xA8, 0x40)));
        tile.Children.Add(Block(8, 0, Color.FromRgb(0x4C, 0x98, 0x38)));
        tile.Children.Add(Block(0, 8, Color.FromRgb(0x4C, 0x98, 0x38)));
        tile.Children.Add(Block(8, 8, Color.FromRgb(0xD8, 0xC8, 0x90)));

        return new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewbox = new Rect(0, 0, 16, 16),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 16 * scale, 16 * scale),
            ViewportUnits = BrushMappingMode.Absolute
        };
    }

    public LabWindow(string[] args, LabData data)
    {
        _data = data;

        // --fondo negro | juego: sobre qué se ve la escena (la banda negra del emulador, o algo parecido al juego).
        if (Array.IndexOf(args, "--fondo") is var behind and >= 0 && behind + 1 < args.Length) _backdrop = args[behind + 1];
        Title = "PermaLocke - ItemLab";
        Width = 1500;
        Height = 900;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x18));
        UseLayoutRounding = true;
        Foreground = Brushes.Gainsboro;

        _category.Items.Add("Todas");
        foreach (var category in Enum.GetValues<ItemCategory>()) _category.Items.Add(Names[category]);
        _category.SelectedIndex = 0;

        _power.Items.Add("auto");
        for (var p = 0; p <= 3; p++) _power.Items.Add(p);
        _power.SelectedIndex = 0;

        foreach (var amount in new[] { 1, 3, 12 }) _amount.Items.Add(amount);
        _amount.SelectedIndex = 0;

        var again = new Button { Content = "Otra", Width = 44 };
        var restart = new Button { Content = "|<", Width = 30 };
        var back = new Button { Content = "<", Width = 30 };
        var forward = new Button { Content = ">", Width = 30 };
        var mega = new Button { Content = "Solo Megapiedra, camara lenta", Padding = new Thickness(8, 0, 8, 0) };

        var top = new WrapPanel { Margin = new Thickness(8) };
        void Add(string label, UIElement control)
        {
            top.Children.Add(new TextBlock { Text = label, Margin = new Thickness(10, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(control);
        }

        Add("Categoria", _category);
        Add("Objeto", _item);
        Add("Potencia", _power);
        Add("Cantidad", _amount);
        Add("Contador", _counter);
        top.Children.Add(again);
        Add("Velocidad", _speed);
        Add("Escala", _scale);
        top.Children.Add(new Border { Width = 10 });
        top.Children.Add(restart);
        top.Children.Add(back);
        top.Children.Add(_play);
        top.Children.Add(forward);
        top.Children.Add(new Border { Width = 10 });
        top.Children.Add(_scrub);
        top.Children.Add(new Border { Width = 10 });
        top.Children.Add(mega);
        top.Children.Add(_info);

        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        dock.Children.Add(new ScrollViewer { Content = _board, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;

        FillItems();

        _category.SelectionChanged += (_, _) => { FillItems(); Rebuild(); };
        _item.SelectionChanged += (_, _) => Rebuild();
        _power.SelectionChanged += (_, _) => Rebuild();
        _amount.SelectionChanged += (_, _) => Rebuild();
        _counter.TextChanged += (_, _) => Rebuild();
        _scale.ValueChanged += (_, _) => Rebuild();
        again.Click += (_, _) => _counter.Text = (Counter + 1).ToString();
        restart.Click += (_, _) => _clock = 0;
        back.Click += (_, _) => Step(-Frame);
        forward.Click += (_, _) => Step(Frame);
        _play.Click += (_, _) => Toggle();
        mega.Click += (_, _) => OnlyMega();
        _scrub.ValueChanged += (_, e) =>
        {
            if (_scrub.IsMouseCaptureWithin || _scrub.IsKeyboardFocusWithin)
            {
                SetPaused(true);
                _clock = e.NewValue;
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space) { Toggle(); e.Handled = true; }
            else if (e.Key == Key.Left) { Step(-Frame); e.Handled = true; }
            else if (e.Key == Key.Right) { Step(Frame); e.Handled = true; }
            else if (e.Key == Key.Home) { _clock = 0; e.Handled = true; }
        };

        _loading = false;
        _relative = args.Contains("--rel");

        // --solo z|clave|evolutivas|mt|bayas|curativos|mejoras|balls|combate|varios|mega: esa categoria sola, a camara lenta.
        var soloAt = Array.IndexOf(args, "--solo");
        if (soloAt >= 0 && soloAt + 1 < args.Length && SoloNames.TryGetValue(args[soloAt + 1], out var solo)) Solo(solo);
        else if (args.Contains("--mega")) OnlyMega();
        else Rebuild();

        // --escala 2.3: el tamaño del píxel del juego, con decimales, igual que el deslizador.
        if (Array.IndexOf(args, "--escala") is var s and >= 0 && s + 1 < args.Length
            && double.TryParse(args[s + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size))
        {
            _scale.Value = size;
        }

        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        // --captura carpeta [--en 0.5,1.2,...] [--mega]: deja un PNG por instante y se cierra, para mirar sin tocar la ventana.
        var at = Array.IndexOf(args, "--captura");
        if (at >= 0 && at + 1 < args.Length)
        {
            var folder = args[at + 1];
            var moments = Array.IndexOf(args, "--en") is var m and >= 0 && m + 1 < args.Length
                ? args[m + 1].Split(',').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray()
                : [0.6];
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { Capture(folder, moments); Close(); });
        }

        // --tira archivo.png --en 0.9,1.7,...: una fila por objeto y una columna por instante, en un solo PNG, y se cierra.
        var strip = Array.IndexOf(args, "--tira");
        if (strip >= 0 && strip + 1 < args.Length)
        {
            var file = args[strip + 1];
            var moments = Array.IndexOf(args, "--en") is var m and >= 0 && m + 1 < args.Length
                ? args[m + 1].Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray()
                : _relative ? [0.06, 0.18, 0.32, 0.46, 0.6, 0.74, 0.88] : [0.9, 1.7, 2.45, 2.9, 3.6, 4.2];
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { Strip(file, moments); Close(); });
        }
    }

    /// <summary>One PNG with a row per item shown and a column per moment, each frame as the app would draw it.</summary>
    private void Strip(string file, double[] moments)
    {
        _timer.Stop();
        const int Gap = 8;
        const int Label = 22;
        var cell = _tiles[0].Scene;
        var width = (moments.Length * (cell.Width + Gap)) + Gap;
        var height = (_tiles.Count * (cell.Height + Label + Gap)) + Gap;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface("Consolas");

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x18)), null, new Rect(0, 0, width, height));

            for (var row = 0; row < _tiles.Count; row++)
            {
                var tile = _tiles[row];
                var top = Gap + (row * (cell.Height + Label + Gap));

                for (var col = 0; col < moments.Length; col++)
                {
                    var left = Gap + (col * (cell.Width + Gap));
                    var moment = _relative ? moments[col] * tile.Length : moments[col];
                    tile.Scene.Render(tile.Item, moment);

                    var frame = BitmapSource.Create(tile.Scene.Width, tile.Scene.Height, 96, 96, PixelFormats.Pbgra32, null,
                        tile.Scene.Pixels, tile.Scene.Width * 4);
                    dc.DrawRectangle(Backdrop(tile.Scene.Pixel), null, new Rect(left, top, cell.Width, cell.Height));
                    dc.DrawImage(frame, new Rect(left, top, tile.Scene.Width, tile.Scene.Height));

                    var text = new FormattedText($"{tile.Item.Name} t={moment:0.00}s", System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, face, 13, Brushes.Gainsboro, dpi);
                    dc.DrawText(text, new Point(left + 2, top + cell.Height + 3));
                }
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        using var output = File.Create(file);
        encoder.Save(output);
    }

    private void Capture(string folder, double[] moments)
    {
        Directory.CreateDirectory(folder);
        SetPaused(true);
        _timer.Stop();

        foreach (var moment in moments)
        {
            _clock = moment;
            Draw();
            UpdateLayout();
            var width = (int)Math.Ceiling(ActualWidth);
            var height = (int)Math.Ceiling(ActualHeight);
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(folder, $"lab-{moment.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}.png"));
            encoder.Save(file);
        }
    }

    private int Counter => int.TryParse(_counter.Text, out var n) ? n : 0;

    private ItemCategory? Chosen => _category.SelectedIndex <= 0 ? null : Enum.GetValues<ItemCategory>()[_category.SelectedIndex - 1];

    private void FillItems()
    {
        var wasLoading = _loading;
        _loading = true;
        _item.Items.Clear();
        _item.Items.Add("(el de siempre)");
        foreach (var entry in _data.Entries.Where(e => Chosen is null || e.Class.Category == Chosen)) _item.Items.Add(entry);
        _item.SelectedIndex = 0;
        _loading = wasLoading;
    }

    private void OnlyMega() => Solo(ItemCategory.MegaStone);

    private void Solo(ItemCategory category)
    {
        _loading = true;
        _category.SelectedIndex = 1 + Array.IndexOf(Enum.GetValues<ItemCategory>(), category);
        FillItems();
        _speed.Value = 0.25;
        _scale.Value = 3;
        _loading = false;
        _clock = 0;
        SetPaused(false);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_loading) return;

        _board.Children.Clear();
        _tiles.Clear();
        var dpi = VisualTreeHelper.GetDpi(this);

        var wanted = new List<Entry>();
        foreach (var category in Chosen is { } one ? [one] : Enum.GetValues<ItemCategory>())
        {
            if (_item.SelectedItem is Entry picked && picked.Class.Category == category)
            {
                wanted.Add(picked);
            }
            else if (Chosen is not null && Picks.TryGetValue(category, out var ids))
            {
                // Varios objetos que difieren en lo que el estilo varia (potencia, tipo, clase): que se vea funcionar.
                wanted.AddRange(ids.Select(id => _data.Entries.FirstOrDefault(e => e.Id == id)).OfType<Entry>());
            }
            else if ((_data.Entries.FirstOrDefault(e => e.Id == Usual[category]) ?? _data.Entries.FirstOrDefault(e => e.Class.Category == category)) is { } usual)
            {
                wanted.Add(usual);
            }
        }

        foreach (var entry in wanted)
        {
            var category = entry.Class.Category;
            var (pixels, width, height) = _data.Icon(entry.Id);
            var power = _power.SelectedIndex <= 0 ? entry.Class.Power : _power.SelectedIndex - 1;

            // Each tile is the next pickup of the one before: a style that alternates (the healing items) shows another entry in
            // each, and the seed is the app's own for that id and that count. The type of a stone is read with the catalog.
            var count = Counter + _tiles.Count;
            var item = new ItemScene.Item(pixels, width, height, entry.Name, (int)_amount.SelectedItem, category, power,
                ItemTint.Of(pixels), ItemScene.SeedFor(entry.Id, count), null, entry.Class.Kind, count);

            var scene = new ItemScene(_scale.Value, ItemScene.HeightFor(item));
            var bitmap = new WriteableBitmap(scene.Width, scene.Height, 96, 96, PixelFormats.Pbgra32, null);
            var image = new Image { Source = bitmap, Width = scene.Width / dpi.DpiScaleX, Height = scene.Height / dpi.DpiScaleY };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            var tile = new Tile(scene, item, bitmap, scene.LengthFor(item));
            _tiles.Add(tile);
            _board.Children.Add(new StackPanel
            {
                Margin = new Thickness(6),
                Children =
                {
                    new Border { Background = Backdrop(scene.Pixel), Child = image },
                    new TextBlock
                    {
                        Text = $"{Names[category]} - {entry.Name} - p{power} - {tile.Length:0.0} s{(_data.HasIcon(entry.Id) ? string.Empty : " (sin icono)")}",
                        Foreground = Brushes.Gainsboro, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(2, 3, 0, 0)
                    }
                }
            });
        }

        _scrub.Maximum = _tiles.Count == 0 ? 1 : _tiles.Max(t => t.Length) + Rest;
        Draw();
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _last).TotalSeconds;
        _last = now;

        if (!_paused) _clock += elapsed * _speed.Value;
        Draw();
    }

    private void Draw()
    {
        foreach (var tile in _tiles)
        {
            var t = _paused ? (_relative ? _clock * tile.Length : _clock) : _clock % (tile.Length + Rest);
            tile.Scene.Render(tile.Item, t);
            tile.Bitmap.WritePixels(new Int32Rect(0, 0, tile.Scene.Width, tile.Scene.Height), tile.Scene.Pixels, tile.Scene.Width * 4, 0);
        }

        if (!_scrub.IsMouseCaptureWithin && !_scrub.IsKeyboardFocusWithin)
        {
            _scrub.Value = Math.Min(_scrub.Maximum, _paused ? _clock : _clock % _scrub.Maximum);
        }

        _info.Text = $"t={_clock:0.000}s  x{_speed.Value:0.00}  pixel={_scale.Value:0.0}  frame={(int)Math.Round(_clock / Frame)}";
    }

    private void Toggle() => SetPaused(!_paused);

    private void SetPaused(bool paused)
    {
        if (paused == _paused) return;
        _paused = paused;
        _play.Content = paused ? "Seguir" : "Pausa";
    }

    private void Step(double seconds)
    {
        SetPaused(true);
        _clock = Math.Max(0, _clock + seconds);
    }
}
