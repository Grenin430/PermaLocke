using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The album on screen (§186, §187): <see cref="AlbumScene"/> in whole cells, the card under the mouse lifted, the tabs
/// of its edge, the wheel to turn pages and a click to take a card out.
/// </summary>
/// <remarks>
/// <para>
/// As many screen pixels per cell as fit, and the picture placed on whole device pixels so nothing is blurred. The
/// cover and its tabs are worn only when they fit at the same size: if dressing the album would drop it to fewer
/// pixels per cell, it goes without, because the cards are what must stay big.
/// </para>
/// <para>
/// It decides nothing: which spread is open is the view model's, and a new one arriving is what starts a page turning,
/// forwards or backwards by its <see cref="AlbumSpread.Index"/>. A click hands the card to <see cref="OpenCommand"/>,
/// telling first where on screen it was (<see cref="CardOpening"/>) so the hand can take it from there; a tab goes to
/// <see cref="TabCommand"/>.
/// </para>
/// </remarks>
public sealed class AlbumStage : ContentControl
{
    public static readonly DependencyProperty SpreadProperty = DependencyProperty.Register(
        nameof(Spread), typeof(AlbumSpread), typeof(AlbumStage),
        new PropertyMetadata(null, (d, e) => ((AlbumStage)d).OnSpreadChanged((AlbumSpread?)e.OldValue, (AlbumSpread?)e.NewValue)));

    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand), typeof(ICommand), typeof(AlbumStage));

    public static readonly DependencyProperty TabCommandProperty = DependencyProperty.Register(
        nameof(TabCommand), typeof(ICommand), typeof(AlbumStage));

    public static readonly DependencyProperty NextCommandProperty = DependencyProperty.Register(
        nameof(NextCommand), typeof(ICommand), typeof(AlbumStage));

    public static readonly DependencyProperty PreviousCommandProperty = DependencyProperty.Register(
        nameof(PreviousCommand), typeof(ICommand), typeof(AlbumStage));

    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused), typeof(bool), typeof(AlbumStage),
        new PropertyMetadata(false, (d, _) => ((AlbumStage)d)._dirty = true));

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private AlbumScene? _scene;
    private WriteableBitmap? _bitmap;
    private int _cell = 1;
    private int _hover = -1;
    private int _hoverTab = -1;
    private AlbumSpread? _turnFrom;
    private bool _turnForward;
    private double _turnStart;
    private long _lastStep = -1;
    private bool _dirty = true;
    private bool _broken;

    public AlbumStage()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _image.LayoutTransform = _zoom;
        _image.HorizontalAlignment = HorizontalAlignment.Left;
        _image.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ClipToBounds = true, UseLayoutRounding = true, Background = Brushes.Transparent, Children = { _image } };
        Focusable = false;

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

    /// <summary>Raised once if drawing fails, so the screen can log it.</summary>
    public event Action<Exception>? RenderFailed;

    /// <summary>
    /// A card is being taken out: the card and where its pocket is, in this control's coordinates, so the hand can
    /// make it fly from there. Raised just before <see cref="OpenCommand"/>.
    /// </summary>
    public event Action<TcgCard, Rect>? CardOpening;

    public AlbumSpread? Spread
    {
        get => (AlbumSpread?)GetValue(SpreadProperty);
        set => SetValue(SpreadProperty, value);
    }

    /// <summary>Takes a card out of its pocket: called with the <see cref="TcgCard"/> clicked.</summary>
    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    /// <summary>Opens a box by its tab: called with the tab's index.</summary>
    public ICommand? TabCommand
    {
        get => (ICommand?)GetValue(TabCommandProperty);
        set => SetValue(TabCommandProperty, value);
    }

    public ICommand? NextCommand
    {
        get => (ICommand?)GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public ICommand? PreviousCommand
    {
        get => (ICommand?)GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    /// <summary>
    /// Stops what moves on its own while something covers the album, as the card in the hand does (§188): under the
    /// veil it would not be seen, and each frame of it is a frame less for the card. A change still paints once.
    /// </summary>
    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reshape();
    }

    private void OnSpreadChanged(AlbumSpread? old, AlbumSpread? spread)
    {
        _hover = -1;
        _hoverTab = -1;

        // Sin nada que enseñar no queda la imagen de antes: la pantalla dice por qué debajo.
        _image.Visibility = spread is null ? Visibility.Hidden : Visibility.Visible;

        if (spread is null)
        {
            _turnFrom = null;
            _dirty = true;
            return;
        }

        if (_scene is null || _scene.Layout != spread.Layout)
        {
            // Otro tamaño de carta, otro álbum: se monta de nuevo, sin girar la página.
            _turnFrom = null;
            _scene = null;
            Reshape();
        }
        else if (old is not null && old.Layout == spread.Layout && old.Index != spread.Index)
        {
            _turnFrom = old;
            _turnForward = spread.Index > old.Index;
            _turnStart = Now;
        }
        else
        {
            // La misma página releída: sus cartas son otras, y lo guardado de las de antes ya no sirve.
            _turnFrom = null;
            _scene.Forget();
        }

        _dirty = true;
    }

    /// <summary>
    /// Picks the album's size for the room there is: the dressed album if it fits at as many pixels per cell as the
    /// bare one, the bare one otherwise.
    /// </summary>
    private void Reshape()
    {
        if (Spread is not { } spread || ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var deviceWidth = ActualWidth * dpi.DpiScaleX;
        var deviceHeight = ActualHeight * dpi.DpiScaleY;

        int CellFor(bool dressed)
        {
            var (w, h) = AlbumScene.SizeOf(spread.Layout, dressed);
            return (int)Math.Floor(Math.Min(deviceWidth / w, deviceHeight / h));
        }

        var dressedCell = CellFor(true);
        var bareCell = CellFor(false);
        var dressed = dressedCell >= Math.Max(1, bareCell);
        var cell = Math.Max(1, dressed ? dressedCell : bareCell);

        if (_scene is null || _scene.Layout != spread.Layout || _scene.Dressed != dressed)
        {
            _scene = new AlbumScene(spread.Layout, dressed);
            _turnFrom = null;
        }

        _cell = cell;
        _zoom.ScaleX = cell / dpi.DpiScaleX;
        _zoom.ScaleY = cell / dpi.DpiScaleY;

        // Centrado, pero en píxeles de pantalla enteros: medio píxel de desfase ensancha una columna de cada tantas.
        var left = Math.Floor((deviceWidth - (_scene.Width * cell)) / 2);
        var top = Math.Floor((deviceHeight - (_scene.Height * cell)) / 2);
        _image.Margin = new Thickness(Math.Max(0, left) / dpi.DpiScaleX, Math.Max(0, top) / dpi.DpiScaleY, 0, 0);

        if (_bitmap is null || _bitmap.PixelWidth != _scene.Width || _bitmap.PixelHeight != _scene.Height)
        {
            _bitmap = new WriteableBitmap(_scene.Width, _scene.Height, 96, 96, PixelFormats.Bgra32, null);
            _image.Source = _bitmap;
            _image.Width = _scene.Width;
            _image.Height = _scene.Height;
        }

        _dirty = true;
        Paint();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!IsVisible || (IsPaused && !_dirty && _turnFrom is null))
        {
            return;
        }

        var step = _clock.ElapsedMilliseconds / 33;
        if (step == _lastStep)
        {
            return;
        }

        _lastStep = step;

        // Una página girando, a 30 imágenes por segundo; lo demás que se mueve (acabados, brasas, el destello del
        // plástico, la funda que late), a 15.
        if (_dirty || _turnFrom is not null || step % 2 == 0)
        {
            Paint();
        }
    }

    private void Paint()
    {
        if (_scene is null || _bitmap is null || Spread is not { } spread || _broken)
        {
            return;
        }

        try
        {
            AlbumTurn? turn = null;
            if (_turnFrom is { } from)
            {
                var progress = (Now - _turnStart) / AlbumScene.TurnLength;
                if (progress >= 1)
                {
                    // Terminada la vuelta, lo guardado de las páginas de antes ya no hace falta.
                    _turnFrom = null;
                    _scene.Forget();
                }
                else
                {
                    turn = new AlbumTurn(from, _turnForward, progress);
                }
            }

            _scene.Render(spread, Now, turn is null ? _hover : -1, turn, _hoverTab);
            _bitmap.WritePixels(new Int32Rect(0, 0, _scene.Width, _scene.Height), _scene.Canvas.Bgra, _scene.Width * 4, 0);
            _dirty = false;
        }
        catch (Exception ex)
        {
            // Dibujar no puede tumbar la pantalla: se avisa una vez y el álbum se queda quieto.
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }

    private TcgCard? CardAt(int pocket)
    {
        if (_scene is null || Spread is not { } spread || pocket < 0)
        {
            return null;
        }

        var page = pocket < _scene.PocketsPerPage ? spread.Left : spread.Right;
        var index = pocket % _scene.PocketsPerPage;
        return index < page.Pockets.Count ? page.Pockets[index] : null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_scene is null || _turnFrom is not null || Spread is not { } spread)
        {
            return;
        }

        // La imagen mide una celda por unidad antes de ampliarla: la posición sobre ella ya viene en celdas.
        var at = e.GetPosition(_image);
        var x = (int)Math.Floor(at.X);
        var y = (int)Math.Floor(at.Y);
        var pocket = _scene.PocketAt(x, y);
        var hover = CardAt(pocket) is null ? -1 : pocket;
        var tab = _scene.TabAt(x, y, spread.TabList.Count);

        if (hover != _hover || tab != _hoverTab)
        {
            _hover = hover;
            _hoverTab = tab;
            Cursor = hover >= 0 || tab >= 0 ? Cursors.Hand : null;
            _dirty = true;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hover >= 0 || _hoverTab >= 0)
        {
            _hover = -1;
            _hoverTab = -1;
            Cursor = null;
            _dirty = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_turnFrom is not null || _scene is null)
        {
            return;
        }

        if (_hoverTab >= 0 && TabCommand?.CanExecute(_hoverTab) == true)
        {
            TabCommand.Execute(_hoverTab);
            e.Handled = true;
            return;
        }

        if (CardAt(_hover) is { } card && OpenCommand?.CanExecute(card) == true)
        {
            // Dónde está su funda en pantalla, para que salga volando desde ahí.
            var (px, py) = _scene.PocketInScene(_hover);
            var dpi = VisualTreeHelper.GetDpi(this);
            var origin = _image.TranslatePoint(new Point(0, 0), this);
            var unit = _cell / dpi.DpiScaleX;
            var rect = new Rect(origin.X + (px * unit), origin.Y + (py * _cell / dpi.DpiScaleY),
                _scene.PocketWidth * unit, _scene.PocketHeight * _cell / dpi.DpiScaleY);

            CardOpening?.Invoke(card, rect);
            OpenCommand.Execute(card);
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var command = e.Delta < 0 ? NextCommand : PreviousCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }
}
