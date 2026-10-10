using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The level cap over the game (§239, redesigned 2026-10-10): a walnut showcase with a brass cup, the shield with the cap, the fuse
/// of the party's level, a niche for each Pokémon and the little graves of the fallen. It fills the black band of the emulator, in
/// the mode that fits it (<see cref="CapGeometry"/>).
/// </summary>
/// <remarks>
/// <para>
/// Drawn whole, cell by cell, like the notices: text in the pixel fonts, hard shadows, no blur. <see cref="CellPixels"/> is a
/// whole number of screen pixels so every cell stays square; whoever places it picks it for the size of the game.
/// </para>
/// <para>
/// A frame is cheap by construction: the still part (<see cref="CapComposer"/>) is painted once, when the data or the size change,
/// and each frame is a copy of it with the moving part (<see cref="CapLive"/>) painted over, written into one bitmap that is
/// reused. <see cref="Render"/> is the same pipeline without WPF's layout, so a test or the lab can look at any frame.
/// </para>
/// </remarks>
public sealed class CapPlaque : FrameworkElement
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
    private readonly CapAnim _anim = new();

    private CapPlaqueData? _data;
    private CapLayout? _layout;
    private CapSheet? _still;
    private CapSheet? _frame;
    private WriteableBitmap? _bitmap;
    private byte[]? _shaded;

    /// <summary>False keeps the panel still: no flame, no flashes, no trails; the HP are the real ones.</summary>
    public bool Animated { get; set; } = true;

    private CapMode _mode = CapMode.Column;
    private int _cols = 132;
    private int _rows = 250;
    private int _cell = 2;
    private bool _gravesInside = true;

    public CapPlaque(CapPanelKind panel = CapPanelKind.Party)
    {
        Panel = panel;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        AddVisualChild(_image);
    }

    public CapPanelKind Panel { get; }

    public CapPlaqueData? Data => _data;

    /// <summary>The animation state, to be told about every change of the data.</summary>
    public CapAnim Anim => _anim;

    public CapMode Mode => _mode;

    public int Cols => _cols;

    public int Rows => _rows;

    /// <summary>Screen pixels per cell.</summary>
    public int CellPixels => _cell;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _image;

    /// <summary>Sets what to draw and on what grid; repaints the still part only if something changed.</summary>
    public void Set(CapPlaqueData? data, CapMode mode, int cols, int rows, int cell, bool gravesInside, double now)
    {
        var sizeChanged = mode != _mode || cols != _cols || rows != _rows || cell != _cell || gravesInside != _gravesInside;
        var dataChanged = !ReferenceEquals(data, _data) && !Equals(data, _data);
        if (!sizeChanged && !dataChanged) return;

        _mode = mode;
        _cols = cols;
        _rows = rows;
        _cell = Math.Max(1, cell);
        _gravesInside = gravesInside;
        _data = data;

        if (data is null)
        {
            _layout = null;
        }
        else
        {
            _anim.Update(data, now);
            Rebuild(data);
        }

        InvalidateMeasure();
        Tick(now);
    }

    private void Rebuild(CapPlaqueData data)
    {
        if (_still is null || _still.W != _cols || _still.H != _rows)
        {
            _still = new CapSheet(_cols, _rows);
            _frame = new CapSheet(_cols, _rows);
            _bitmap = new WriteableBitmap(_cols, _rows, 96, 96, PixelFormats.Bgra32, null);
            _shaded = new byte[_still.B.Length];
            _image.Source = _bitmap;
        }

        _layout = CapComposer.Arrange(_mode, Panel, _cols, _rows, data, _gravesInside);
        CapComposer.Paint(_still, _layout, data);
    }

    /// <summary>Paints the frame for a moment: the still part, with the moving part over it.</summary>
    public void Tick(double now)
    {
        if (_data is null || _layout is null || _still is null || _frame is null || _bitmap is null) return;

        _frame.CopyFrom(_still);
        CapLive.Paint(_frame, _layout, _data, Animated ? _anim : null, Animated ? now : 0);

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

    /// <summary>
    /// The whole panel at a moment, without WPF's layout: what a test or the lab looks at. A new sheet each call; the panel itself
    /// never does this.
    /// </summary>
    public static CapSheet Render(CapPlaqueData data, CapMode mode, int cols, int rows, double t = 0, CapPanelKind panel = CapPanelKind.Party,
        bool gravesInside = true, CapAnim? anim = null)
    {
        var layout = CapComposer.Arrange(mode, panel, cols, rows, data, gravesInside);
        var still = new CapSheet(cols, rows);
        CapComposer.Paint(still, layout, data);
        CapLive.Paint(still, layout, data, anim, t);
        return still;
    }
}
