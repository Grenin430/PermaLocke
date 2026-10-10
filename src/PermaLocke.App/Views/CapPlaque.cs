using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The level cap over the game (§239, redone 2026-10-10 as a sea): a window onto a bioluminescent sea beside the screens, where the party are
/// creatures of light, the cap is written on a buoy, the fuse is a line let down to the deepest of them, and the fallen lie as fossils in the
/// floor. It fills the black band of the emulator, in the mode that fits it (<see cref="CapGeometry"/>).
/// </summary>
/// <remarks>
/// <para>
/// Drawn whole, cell by cell: text in the pixel fonts, hard outlines, no blur. <see cref="CellPixels"/> is a whole number of screen pixels so
/// every cell stays square; whoever places it picks it for the size of the game.
/// </para>
/// <para>
/// A frame is cheap by construction: <see cref="CapWorldView"/> keeps the pictures that change rarely (the scene of each mood, the buoy, each
/// creature) and every frame paints the few things that move over a copy of the scene, into one bitmap that is reused. <see cref="Render"/> and
/// <see cref="Replay"/> are the same pipeline without WPF's layout, so a test or the lab can look at any frame.
/// </para>
/// </remarks>
public sealed class CapPlaque : FrameworkElement
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
    private readonly CapAnim _anim = new();

    private CapPlaqueData? _data;
    private CapWorldView? _view;
    private CapSheet? _frame;
    private WriteableBitmap? _bitmap;
    private byte[]? _shaded;

    /// <summary>False keeps the panel still: no waves, no snow, no flashes; the HP are the real ones.</summary>
    public bool Animated { get; set; } = true;

    private CapMode _mode = CapMode.Column;
    private (int X0, int X1, int Y1) _notch;
    private int _cols = 132;
    private int _rows = 250;
    private int _cell = 2;
    private int _worldX;
    private int _worldWidth;

    public CapPlaque(CapPanelKind panel = CapPanelKind.Party)
    {
        Panel = panel;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        AddVisualChild(_image);
    }

    public CapPanelKind Panel { get; }

    public CapPlaqueData? Data => _data;

    /// <summary>The animation state, told about every change of the data.</summary>
    public CapAnim Anim => _anim;

    public CapMode Mode => _mode;

    public int Cols => _cols;

    public int Rows => _rows;

    /// <summary>Screen pixels per cell.</summary>
    public int CellPixels => _cell;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _image;

    /// <summary>Whether something moves that wants frames more often than the ambient drift.</summary>
    public bool Busy(double now) => Animated && _data is not null && _anim.Busy(now);

    /// <summary>
    /// Sets what to draw and where this window sits: the grid it has, its notch, and the column of the whole sea it starts at, so that the floor
    /// and what swims behind the game join up with the other window's. Repaints only what changed.
    /// </summary>
    public void Set(CapPlaqueData? data, CapSlot slot, double now, int worldX = 0, int worldWidth = 0)
    {
        var grid = slot.Mode != _mode || slot.Cols != _cols || slot.Rows != _rows || slot.Notch != _notch || worldX != _worldX || worldWidth != _worldWidth;
        var cell = Math.Max(1, slot.Cell);
        var dataChanged = !ReferenceEquals(data, _data) && !Equals(data, _data);
        if (!grid && cell == _cell && !dataChanged) return;

        _mode = slot.Mode;
        _cols = slot.Cols;
        _rows = slot.Rows;
        _notch = slot.Notch;
        _worldX = worldX;
        _worldWidth = worldWidth;
        _cell = cell;
        _data = data;

        if (data is null)
        {
            _view = null;
        }
        else
        {
            // A new grid is a new sea: nothing the animations remember is true of it.
            if (grid || _view is null)
            {
                _anim.Reset();
                _view = new CapWorldView(_mode, Panel, _cols, _rows, _notch);
                _frame = new CapSheet(_cols, _rows) { NotchX0 = _notch.X0, NotchX1 = _notch.X1, NotchY1 = _notch.Y1 };
                _bitmap = new WriteableBitmap(_cols, _rows, 96, 96, PixelFormats.Bgra32, null);
                _shaded = new byte[_frame.B.Length];
                _image.Source = _bitmap;
            }

            _anim.Update(data, now);
            _view.SetData(data, _worldX, _worldWidth);
        }

        InvalidateMeasure();
        Tick(now);
    }

    /// <summary>Paints the frame for a moment.</summary>
    public void Tick(double now)
    {
        if (_data is null || _view is null || _frame is null || _bitmap is null) return;

        _view.Paint(_frame, Animated ? now : 0, Animated ? _anim : null);

        var pixels = _frame.B;
        if (CapSheet.ThemeShades)
        {
            Buffer.BlockCopy(pixels, 0, _shaded!, 0, pixels.Length);
            CapSheet.Shade(_shaded!);
            pixels = _shaded!;
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, _cols, _rows), pixels, _cols * 4, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_data is null) return new Size(0, 0);
        var dpi = VisualTreeHelper.GetDpi(this);
        var size = new Size(_cols * _cell / dpi.DpiScaleX, _rows * _cell / dpi.DpiScaleY);
        _image.Width = size.Width;
        _image.Height = size.Height;
        _image.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _image.Arrange(new Rect(finalSize));
        return finalSize;
    }

    // ---------------------------------------------------------------- one frame, without WPF

    /// <summary>
    /// The whole panel at a moment, without WPF's layout: what a test or the lab looks at. A new sheet each call; the panel itself never does this.
    /// </summary>
    public static CapSheet Render(CapPlaqueData data, CapMode mode, int cols, int rows, double t = 0, CapPanelKind panel = CapPanelKind.Party,
        CapAnim? anim = null, (int X0, int X1, int Y1) notch = default, int worldX = 0, int worldWidth = 0)
    {
        var view = new CapWorldView(mode, panel, cols, rows, notch);
        view.SetData(data, worldX, worldWidth);
        var frame = new CapSheet(cols, rows) { NotchX0 = notch.X0, NotchX1 = notch.X1, NotchY1 = notch.Y1 };
        view.Paint(frame, t, anim);
        return frame;
    }

    /// <summary>
    /// A scenario at a moment: the animation state is built by telling it each step up to <paramref name="t"/>, in order, so any moment can be
    /// asked for in any order and the answer is always the same.
    /// </summary>
    public static CapSheet Replay(CapScenario scenario, CapMode mode, int cols, int rows, double t, CapPanelKind panel = CapPanelKind.Party,
        (int X0, int X1, int Y1) notch = default, int worldX = 0, int worldWidth = 0)
    {
        var anim = new CapAnim();
        var current = scenario.Steps[0].Data;
        foreach (var step in scenario.Steps)
        {
            if (step.At > t) break;
            anim.Update(step.Data, step.At);
            current = step.Data;
        }

        return Render(current, mode, cols, rows, t, panel, anim, notch, worldX, worldWidth);
    }
}
