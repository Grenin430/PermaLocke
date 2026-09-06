using System.Windows;
using System.Windows.Input;

namespace PermaLocke.App.Views;

/// <summary>
/// The little tab that stays on the edge of the screen while PermaLocke is out of the way.
/// </summary>
/// <remarks>
/// <para>
/// Clicking it and dragging it are the same gesture until the mouse moves, so the two are told
/// apart by <b>how far it moved</b> and not by which button or how long: a tab you can drag but
/// that opens on any release would be impossible to move, and one that needs a double click to
/// open would be a puzzle.
/// </para>
/// <para>
/// It never steals the focus — that flag is set from outside, in <c>EdgeTab</c> — so hovering it
/// mid-battle does nothing to the game.
/// </para>
/// </remarks>
public partial class EdgeTabWindow : Window
{
    private const double DragThreshold = 4;

    private Point _grabbed;
    private double _grabbedTop;
    private bool _dragging;

    public EdgeTabWindow() => InitializeComponent();

    /// <summary>Somebody wants the application back.</summary>
    public event EventHandler? Opened;

    /// <summary>Where the player dragged it to, so it can be remembered.</summary>
    public event EventHandler<double>? Moved;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _grabbed = PointToScreen(e.GetPosition(this));
        _grabbedTop = Top;
        _dragging = false;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!IsMouseCaptured)
        {
            return;
        }

        var moved = PointToScreen(e.GetPosition(this)).Y - _grabbed.Y;

        if (!_dragging && Math.Abs(moved) < DragThreshold)
        {
            return;
        }

        _dragging = true;

        // Se limita a la pantalla en la que ya esta, no a la principal.
        var area = Services.GameWindow.Screen();
        Top = Math.Clamp(_grabbedTop + moved, area.Top, area.Bottom - Height);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!IsMouseCaptured)
        {
            return;
        }

        ReleaseMouseCapture();

        if (_dragging)
        {
            Moved?.Invoke(this, Top);
            return;
        }

        Opened?.Invoke(this, EventArgs.Empty);
    }
}
