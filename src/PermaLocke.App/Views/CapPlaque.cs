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
/// reused. When the data change, the old still part is kept for a moment and dissolved into the new one with the app's dither, only
/// where something changed (<see cref="CapAnim.Dissolve"/>). <see cref="Render"/> and <see cref="Replay"/> are the same pipeline
/// without WPF's layout, so a test or the lab can look at any frame.
/// </para>
/// </remarks>
public sealed class CapPlaque : FrameworkElement
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
    private readonly CapAnim _anim = new();

    private CapPlaqueData? _data;
    private CapLayout? _layout;
    private CapSheet? _still;
    private CapSheet? _previous;
    private bool _hasPrevious;
    private CapSheet? _frame;
    private WriteableBitmap? _bitmap;
    private byte[]? _shaded;

    /// <summary>False keeps the panel still: no flame, no flashes, no trails; the HP are the real ones.</summary>
    public bool Animated { get; set; } = true;

    private (int X0, int X1, int Y1) _notch;
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

    /// <summary>Whether something moves that wants frames more often than the ambient flicker.</summary>
    public bool Busy(double now) => Animated && _data is not null && _anim.Busy(now);

    /// <summary>Sets what to draw and on what grid; repaints the still part only if something changed.</summary>
    public void Set(CapPlaqueData? data, CapMode mode, int cols, int rows, int cell, bool gravesInside, double now, (int X0, int X1, int Y1) notch = default)
    {
        var sizeChanged = mode != _mode || cols != _cols || rows != _rows || cell != _cell || gravesInside != _gravesInside || notch != _notch;
        _notch = notch;
        var dataChanged = !ReferenceEquals(data, _data) && !Equals(data, _data);
        if (!sizeChanged && !dataChanged) return;

        var resized = mode != _mode || cols != _cols || rows != _rows || gravesInside != _gravesInside;
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
            // A new grid is a new panel: nothing to dissolve from, and nothing the animations remember is true of it.
            if (resized) _anim.Reset();
            _anim.Update(data, now);
            Rebuild(data, resized);
        }

        InvalidateMeasure();
        Tick(now);
    }

    private void Rebuild(CapPlaqueData data, bool resized)
    {
        if (_still is null || _still.W != _cols || _still.H != _rows)
        {
            _still = new CapSheet(_cols, _rows);
            _previous = null;
            _frame = new CapSheet(_cols, _rows);
            _bitmap = new WriteableBitmap(_cols, _rows, 96, 96, PixelFormats.Bgra32, null);
            _shaded = new byte[_still.B.Length];
            _image.Source = _bitmap;
            _hasPrevious = false;
        }
        else if (resized)
        {
            _hasPrevious = false;
        }
        else
        {
            // The still part that was becomes the one to dissolve from, and its sheet is painted over with the new one.
            _previous ??= new CapSheet(_cols, _rows);
            (_previous, _still) = (_still, _previous);
            _hasPrevious = true;
        }

        foreach (var sheet in new[] { _still, _previous, _frame })
        {
            if (sheet is null) continue;
            (sheet.NotchX0, sheet.NotchX1, sheet.NotchY1) = _notch;
        }

        _layout = CapComposer.Arrange(_mode, Panel, _cols, _rows, data, _gravesInside);
        CapComposer.Paint(_still, _layout, data);
    }

    /// <summary>Paints the frame for a moment: the still part, with the moving part over it.</summary>
    public void Tick(double now)
    {
        if (_data is null || _layout is null || _still is null || _frame is null || _bitmap is null) return;

        Compose(_frame, _still, _hasPrevious ? _previous : null, _layout, _data, Animated ? _anim : null, Animated ? now : 0);

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
    /// A frame: the still part, the dissolve of what has just changed from the old still part into it (when there is one), and the
    /// moving part over all of it. The same for the panel, the lab and the tests.
    /// </summary>
    public static void Compose(CapSheet frame, CapSheet still, CapSheet? previous, CapLayout layout, CapPlaqueData data, CapAnim? anim, double t)
    {
        frame.CopyFrom(still);

        if (anim is not null && previous is not null && layout.Panel == CapPanelKind.Party)
        {
            for (var i = 0; i < layout.Niches.Length; i++)
            {
                var progress = anim.Dissolve(i, t);
                if (progress < 1) Mix(frame, previous, layout.Niches[i], progress);
            }

            var head = anim.HeaderDissolve(t);
            if (head < 1)
            {
                var from = layout.Mode == CapMode.Strip ? layout.Inner.Top : layout.Shield.Top;
                var to = layout.Mode == CapMode.Strip ? layout.Inner.Bottom : layout.Fuse.Bottom;
                var left = layout.Mode == CapMode.Strip ? layout.Inner.Left : layout.Inner.Left;
                var width = layout.Mode == CapMode.Strip ? layout.Niches[0].Left - left : layout.Inner.Width;
                Mix(frame, previous, new CapRect(left, from, width, to - from), head);
            }
        }

        CapLive.Paint(frame, layout, data, anim, t);
    }

    /// <summary>The 4×4 ordered dither: a cell takes the old drawing while its number is above the progress, so a change eats into the old one in a pattern.</summary>
    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    private static void Mix(CapSheet frame, CapSheet previous, CapRect area, double progress)
    {
        var cut = progress * 16;
        for (var y = Math.Max(0, area.Top); y < Math.Min(frame.H, area.Bottom); y++)
        {
            for (var x = Math.Max(0, area.Left); x < Math.Min(frame.W, area.Right); x++)
            {
                if (Bayer[((y & 3) * 4) + (x & 3)] < cut) continue;
                Buffer.BlockCopy(previous.B, ((y * previous.W) + x) * 4, frame.B, ((y * frame.W) + x) * 4, 4);
            }
        }
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
        var frame = new CapSheet(cols, rows);
        Compose(frame, still, null, layout, data, anim, t);
        return frame;
    }

    /// <summary>
    /// A scenario at a moment: the animation state is built by telling it each step up to <paramref name="t"/>, in order, so any moment
    /// can be asked for in any order and the answer is always the same.
    /// </summary>
    public static CapSheet Replay(CapScenario scenario, CapMode mode, int cols, int rows, double t, CapPanelKind panel = CapPanelKind.Party,
        bool gravesInside = true)
    {
        var anim = new CapAnim();
        CapPlaqueData? previous = null;
        var current = scenario.Steps[0].Data;
        var first = true;

        foreach (var step in scenario.Steps)
        {
            if (step.At > t) break;
            anim.Update(step.Data, step.At);
            if (!first) previous = current;
            current = step.Data;
            first = false;
        }

        var layout = CapComposer.Arrange(mode, panel, cols, rows, current, gravesInside);
        var still = new CapSheet(cols, rows);
        CapComposer.Paint(still, layout, current);

        CapSheet? before = null;
        if (previous is not null)
        {
            before = new CapSheet(cols, rows);
            CapComposer.Paint(before, CapComposer.Arrange(mode, panel, cols, rows, previous, gravesInside), previous);
        }

        var frame = new CapSheet(cols, rows);
        Compose(frame, still, before, layout, current, anim, t);
        return frame;
    }
}
