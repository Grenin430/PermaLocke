using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// A line of hearts to drag (2026-10-29): the way to choose how many lives a run starts with, 1 to <see cref="Maximum"/>. The hearts up to
/// <see cref="Value"/> are lit and the rest are dark; press and drag along the line, or use the arrow keys.
/// </summary>
public sealed class HeartSlider : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(HeartSlider),
        new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            null, (_, v) => Math.Clamp((int)v, 1, MaxLives)));

    public static readonly DependencyProperty IsLitProperty = DependencyProperty.Register(
        nameof(IsLit), typeof(bool), typeof(HeartSlider),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The same limit the run keeps (<c>Run.MaxLives</c>), so the line is never longer than what can be stored.</summary>
    public const int Maximum = MaxLives;

    private const int MaxLives = 30;

    private static readonly string[] Shape =
    [
        ".XX.XX.",
        "XXXXXXX",
        "XXXXXXX",
        ".XXXXX.",
        "..XXX..",
        "...X..."
    ];

    private static readonly Color Lit = Color.FromRgb(0xE0, 0x30, 0x4A), LitLight = Color.FromRgb(0xFF, 0x86, 0x9C), LitDark = Color.FromRgb(0xA0, 0x18, 0x30);
    private static readonly Color Off = Color.FromRgb(0x2E, 0x2E, 0x3C), OffDark = Color.FromRgb(0x1E, 0x1E, 0x2A);

    public HeartSlider()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>False while lives are off: the hearts are drawn dark and the line does not answer.</summary>
    public bool IsLit
    {
        get => (bool)GetValue(IsLitProperty);
        set => SetValue(IsLitProperty, value);
    }

    private double Pitch => ActualWidth / Maximum;

    /// <summary>Whole pixels per cell of the heart, so every heart is the same crisp size.</summary>
    private int Cell => Math.Max(1, (int)Math.Floor(Pitch / (Shape[0].Length + 1)));

    protected override Size MeasureOverride(Size available)
    {
        var width = double.IsInfinity(available.Width) ? 480 : available.Width;
        var cell = Math.Max(1, (int)Math.Floor(width / Maximum / (Shape[0].Length + 1)));
        return new Size(width, (Shape.Length * cell) + 8);
    }

    protected override void OnRender(DrawingContext dc)
    {
        // transparent, so the whole strip takes the mouse and not only the hearts
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var cell = Cell;
        var heartW = Shape[0].Length * cell;
        var pitch = Pitch;

        for (var i = 0; i < Maximum; i++)
        {
            var on = IsLit && i < Value;
            var left = Math.Round((i * pitch) + ((pitch - heartW) / 2));
            for (var y = 0; y < Shape.Length; y++)
            {
                for (var x = 0; x < Shape[y].Length; x++)
                {
                    if (Shape[y][x] != 'X') continue;
                    var colour = on ? (y <= 1 && x < 3 ? LitLight : y >= 3 ? LitDark : Lit) : (y >= 3 ? OffDark : Off);
                    dc.DrawRectangle(new SolidColorBrush(colour), null, new Rect(left + (x * cell), 4 + (y * cell), cell, cell));
                }
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (!IsLit) return;
        Focus();
        CaptureMouse();
        Follow(e.GetPosition(this));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured) Follow(e.GetPosition(this));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        ReleaseMouseCapture();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsLit) return;
        switch (e.Key)
        {
            case Key.Left or Key.Down: Value--; e.Handled = true; break;
            case Key.Right or Key.Up: Value++; e.Handled = true; break;
            case Key.Home: Value = 1; e.Handled = true; break;
            case Key.End: Value = Maximum; e.Handled = true; break;
        }
    }

    private void Follow(Point at) => Value = (int)Math.Ceiling(Math.Max(0.01, at.X) / Pitch);
}
