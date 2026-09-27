using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PermaLocke.App.ViewModels;

using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// Shows <see cref="CemeteryScene"/> at a whole number of screen pixels per cell, puts the names under the
/// monuments and turns a click on a grave into the pick command.
/// </summary>
/// <remarks>
/// <para>
/// The scene is rebuilt to the panel's size in cells, so it fills the panel instead of leaving bands and
/// every cell lands on whole screen pixels. The size of a cell is the largest that still fits a row of
/// ten graves; the display scaling is taken out first, because at 125% a WPF unit is not a pixel.
/// </para>
/// <para>
/// The scene and the names share one surface under one scale, so a name cannot drift away from its
/// grave whatever the window's size. It only animates while it is on screen.
/// </para>
/// </remarks>
public sealed class CemeteryCanvas : ContentControl
{
    public static readonly DependencyProperty GravesProperty = DependencyProperty.Register(
        nameof(Graves), typeof(IEnumerable), typeof(CemeteryCanvas),
        new PropertyMetadata(null, (d, e) => ((CemeteryCanvas)d).OnGravesChanged(e.OldValue, e.NewValue)));

    public static readonly DependencyProperty PickCommandProperty = DependencyProperty.Register(
        nameof(PickCommand), typeof(ICommand), typeof(CemeteryCanvas));

    private static readonly Color NameColour = Color.FromRgb(0x8C, 0x84, 0xA3);
    private static readonly Color NameHover = Color.FromRgb(0xB9, 0xB0, 0xCF);
    private static readonly Color NameSelected = Color.FromRgb(0xEC, 0xE6, 0xF7);

    /// <summary>How fast a picked ghost rises, and an unpicked one settles, per second.</summary>
    private const double RiseSpeed = 2.4;

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Canvas _names = new() { IsHitTestVisible = false };
    private readonly Grid _surface = new() { Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<GraveViewModel, Sprites?> _sprites = [];
    private readonly Dictionary<GraveViewModel, double> _rise = [];

    private CemeteryScene _scene = new(CemeteryScene.MinWidth, CemeteryScene.MinHeight);
    private int _cell = 1;
    private List<GraveViewModel> _shown = [];
    private int _hovered = -1;
    private double _lastFrame;

    public CemeteryCanvas()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

        _surface.Children.Add(_image);
        _surface.Children.Add(_names);
        _surface.LayoutTransform = _zoom;
        _surface.MouseMove += (_, e) => Hover(Slot(e.GetPosition(_surface)));
        _surface.MouseLeave += (_, _) => Hover(-1);
        _surface.MouseLeftButtonUp += (_, e) => Pick(Slot(e.GetPosition(_surface)));

        // Sin redondeo de maquetación, centrar una escena impar deja medio píxel y el vecino más
        // cercano se come una columna.
        Content = new Grid { UseLayoutRounding = true, ClipToBounds = true, Children = { _surface } };
        UseScene(_scene, 1);

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

    public IEnumerable? Graves
    {
        get => (IEnumerable?)GetValue(GravesProperty);
        set => SetValue(GravesProperty, value);
    }

    public ICommand? PickCommand
    {
        get => (ICommand?)GetValue(PickCommandProperty);
        set => SetValue(PickCommandProperty, value);
    }

    private sealed record Sprites(byte[] Ghost, byte[] Colour, int Width, int Height);

    /// <summary>Moving the window to a monitor with another scaling changes how many pixels a cell is.</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void Reshape()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelsWide = ActualWidth * dpi.DpiScaleX;
        var pixelsHigh = ActualHeight * dpi.DpiScaleY;

        var cell = Math.Max(1, (int)Math.Floor(pixelsWide / CemeteryScene.MinWidth));
        var width = (int)Math.Floor(pixelsWide / cell);
        var height = (int)Math.Floor(pixelsHigh / cell);

        _zoom.ScaleX = cell / dpi.DpiScaleX;
        _zoom.ScaleY = cell / dpi.DpiScaleY;

        if (cell != _cell || width != _scene.Width || Math.Clamp(height, CemeteryScene.MinHeight, CemeteryScene.MaxHeight) != _scene.Height)
        {
            UseScene(new CemeteryScene(width, height), cell);
        }
    }

    private void UseScene(CemeteryScene scene, int cell)
    {
        _scene = scene;
        _cell = cell;
        _image.Source = scene.Bitmap;
        _surface.Width = scene.Width;
        _surface.Height = scene.Height;
        _names.Width = scene.Width;
        _names.Height = scene.Height;
        Rebuild();
    }

    private void OnGravesChanged(object? oldValue, object? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= OnCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += OnCollectionChanged;
        }

        Rebuild();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        foreach (var grave in _shown)
        {
            grave.PropertyChanged -= OnGraveChanged;
        }

        _shown = Graves?.OfType<GraveViewModel>().Take(CemeteryScene.PerPage).ToList() ?? [];
        _names.Children.Clear();

        for (var i = 0; i < _shown.Count; i++)
        {
            var grave = _shown[i];
            grave.PropertyChanged += OnGraveChanged;

            if (!_sprites.ContainsKey(grave))
            {
                _sprites[grave] = grave.Sprite is { } sprite
                    ? new Sprites(CemeteryScene.Ghost(sprite).Pixels, CemeteryScene.Pixels(sprite), sprite.PixelWidth, sprite.PixelHeight)
                    : null;
            }

            // El que ya estaba elegido no vuelve a subir cada vez que se rehace la página.
            _rise.TryAdd(grave, grave.IsSelected ? 1 : 0);

            var anchor = _scene.NameAnchor(i);
            // En la letra de píxeles a una celda por punto: la escena entera se amplía por enteros, así que sale nítida.
            var name = new PixelText
            {
                Text = grave.Name,
                Scale = 1,
                Tight = true,
                Trim = true,
                Width = _scene.NameWidth,
                TextAlignment = TextAlignment.Center,
                Colour = NameColour
            };

            Canvas.SetLeft(name, anchor.X - (_scene.NameWidth / 2));
            Canvas.SetTop(name, anchor.Y);
            _names.Children.Add(name);
        }

        Paint(0);
    }

    private void OnGraveChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraveViewModel.IsSelected))
        {
            Paint(0);
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // Treinta veces por segundo sobran para una niebla y unos fantasmas que flotan.
        var now = _clock.Elapsed.TotalMilliseconds;

        if (now - _lastFrame >= 33)
        {
            var elapsed = Math.Min(0.1, (now - _lastFrame) / 1000);
            _lastFrame = now;
            Paint(elapsed);
        }
    }

    private void Paint(double elapsed)
    {
        var plots = new List<CemeteryScene.Plot>(_shown.Count);

        for (var i = 0; i < _shown.Count; i++)
        {
            var grave = _shown[i];
            var sprites = _sprites.GetValueOrDefault(grave);

            var target = grave.IsSelected ? 1.0 : 0.0;
            var rise = _rise.GetValueOrDefault(grave);
            rise = rise < target ? Math.Min(target, rise + (elapsed * RiseSpeed)) : Math.Max(target, rise - (elapsed * RiseSpeed));
            _rise[grave] = rise;

            plots.Add(new CemeteryScene.Plot((CemeteryScene.Monument)grave.MonumentSize, grave.MonumentVariant,
                sprites?.Ghost, sprites?.Colour, sprites?.Width ?? 0, sprites?.Height ?? 0, grave.FloatPhase,
                grave.IsSelected, i == _hovered, rise, grave.IsShiny, grave.IsFresh));
        }

        _scene.Render(plots, _clock.Elapsed.TotalSeconds);

        for (var i = 0; i < _names.Children.Count && i < _shown.Count; i++)
        {
            ((PixelText)_names.Children[i]).Colour =
                _shown[i].IsSelected ? NameSelected : i == _hovered ? NameHover : NameColour;
        }
    }

    private int Slot(Point point)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            var rect = _scene.SlotRect(i);

            if (point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height + 8)
            {
                return i;
            }
        }

        return -1;
    }

    private void Hover(int slot)
    {
        // El ratón se mueve sin parar; cambiar la ayuda en cada movimiento la haría parpadear.
        if (slot == _hovered)
        {
            return;
        }

        _hovered = slot;
        _surface.Cursor = slot >= 0 ? Cursors.Hand : Cursors.Arrow;
        _surface.ToolTip = slot >= 0 ? _shown[slot].HoverText : null;
    }

    private void Pick(int slot)
    {
        if (slot >= 0 && PickCommand is { } command && command.CanExecute(_shown[slot]))
        {
            command.Execute(_shown[slot]);
        }
    }
}
