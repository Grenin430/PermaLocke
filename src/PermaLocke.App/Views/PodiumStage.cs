using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// COMPETICIÓN's podium on screen: <see cref="PodiumScene"/> in whole cells, like the roulette (§175), with each
/// player's photo turned into <see cref="PodiumScene.PhotoSize"/> cells once it has downloaded.
/// </summary>
public sealed class PodiumStage : ContentControl
{
    public static readonly DependencyProperty PlayersProperty = DependencyProperty.Register(
        nameof(Players), typeof(IEnumerable), typeof(PodiumStage),
        new PropertyMetadata(null, (d, e) => ((PodiumStage)d).OnPlayersChanged(e.OldValue, e.NewValue)));

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<object, byte[]?> _photos = [];
    private PodiumScene? _scene;
    private TopRow?[] _slots = [null, null, null];
    private int _cell = 3;
    private long _lastStep = -1;

    public PodiumStage()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Background = Brushes.Transparent, Children = { _image } };

        MouseMove += (_, e) => Hover(e.GetPosition(_image));
        SizeChanged += (_, _) => Reshape();
        // Como la ruleta: quitar antes de poner, porque Loaded puede repetirse sin Unloaded (1.0.4.6).
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            CompositionTarget.Rendering += OnFrame;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>The podium as <see cref="SyncViewModel.Podium"/> lists it: second, first, third.</summary>
    public IEnumerable? Players
    {
        get => (IEnumerable?)GetValue(PlayersProperty);
        set => SetValue(PlayersProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnPlayersChanged(object? oldValue, object? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldList) oldList.CollectionChanged -= OnListChanged;
        if (newValue is INotifyCollectionChanged newList) newList.CollectionChanged += OnListChanged;
        Rebuild();
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        var rows = Players?.OfType<TopRow>().ToList() ?? [];

        // Con uno solo, al centro; con dos, segundo a la izquierda y primero al centro.
        _slots = rows.Count switch
        {
            0 => [null, null, null],
            1 => [null, rows[0], null],
            2 => [rows[0], rows[1], null],
            _ => [rows[0], rows[1], rows[2]]
        };

        foreach (var row in _slots)
        {
            if (row?.Picture is { } picture && !_photos.ContainsKey(picture))
            {
                _photos[picture] = null;
                _ = LoadAsync(picture);
            }
        }

        Paint();
    }

    private async Task LoadAsync(object picture)
    {
        try
        {
            BitmapSource? source = picture as BitmapSource;

            if (picture is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = uri;
                image.DecodePixelWidth = PodiumScene.PhotoSize * 2;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();

                if (image.IsDownloading)
                {
                    var done = new TaskCompletionSource();
                    image.DownloadCompleted += (_, _) => done.TrySetResult();
                    image.DownloadFailed += (_, _) => done.TrySetResult();
                    await done.Task;
                }

                source = image;
            }

            if (source is not null) _photos[picture] = Pixelate(source, picture is string);
        }
        catch (Exception)
        {
            // Sin foto se ve el marco vacío: no es motivo para romper la pantalla.
        }
    }

    /// <summary>
    /// A picture as <see cref="PodiumScene.PhotoSize"/> square cells: sampled to the nearest cell, and a photo also
    /// reduced to a few tones per channel so it reads as pixel art next to the sprites.
    /// </summary>
    private static byte[] Pixelate(BitmapSource source, bool posterize)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bgra.PixelWidth, height = bgra.PixelHeight;
        var pixels = new byte[width * height * 4];
        bgra.CopyPixels(pixels, width * 4, 0);

        const int Size = PodiumScene.PhotoSize;
        var result = new byte[Size * Size * 4];
        var scale = Math.Max(width, height) / (double)Size;
        var offsetX = (Size - (width / scale)) / 2;
        var offsetY = (Size - (height / scale)) / 2;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var sx = (int)((x + 0.5 - offsetX) * scale);
                var sy = (int)((y + 0.5 - offsetY) * scale);
                if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;

                var from = ((sy * width) + sx) * 4;
                var to = ((y * Size) + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    var value = pixels[from + c];
                    result[to + c] = posterize ? (byte)(Math.Round(value / 51.0) * 51) : value;
                }

                result[to + 3] = pixels[from + 3];
            }
        }

        return result;
    }

    private void Reshape()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        _cell = Math.Floor(ActualWidth * dpi.DpiScaleX / 3) >= PodiumScene.DesignWidth ? 3 : 2;

        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / _cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / _cell);
        if (columns < 40 || rows < 40) return;

        _zoom.ScaleX = _cell / dpi.DpiScaleX;
        _zoom.ScaleY = _cell / dpi.DpiScaleY;

        if (_scene is { } scene && scene.Width == Math.Max(PodiumScene.DesignWidth, columns)
            && scene.Height == Math.Max(PodiumScene.DesignRows, rows))
        {
            return;
        }

        _scene = new PodiumScene(columns, rows);
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
        if (_scene is null) return;

        var steps = _slots.Select(row => row is null
            ? null
            : new PodiumScene.Step(row.Position, row.Player, row.Points, row.IsMine, row.State,
                $"ETAPA {row.Stages}", $"{row.Alive} VIVOS", row.Picture is { } p ? _photos.GetValueOrDefault(p) : null)).ToList();

        _scene.Render(steps, _clock.Elapsed.TotalSeconds);
    }

    private void Hover(Point point)
    {
        if (_scene is null) return;

        for (var slot = 0; slot < _slots.Length; slot++)
        {
            var (x, y, width, height) = _scene.SlotRect(slot);
            if (point.X >= x && point.X < x + width && point.Y >= y && point.Y < y + height && _slots[slot] is { } row)
            {
                ToolTip = $"{row.Player}\n{row.Points} puntos\n{row.Line}\n{row.Team}";
                return;
            }
        }

        ToolTip = null;
    }
}
