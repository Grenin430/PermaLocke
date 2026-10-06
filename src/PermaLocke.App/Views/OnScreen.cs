using System.Windows;

namespace PermaLocke.App.Views;

/// <summary>Whether an animated control is worth a new frame.</summary>
internal static class OnScreen
{
    /// <summary>
    /// Shown, and its window not minimised. <see cref="UIElement.IsVisible"/> alone stays true in a minimised window, and
    /// PermaLocke minimises itself into the edge tab when the game opens: the sky, the room, the cemetery and the podium
    /// kept drawing behind the emulator for nobody (2026-10-03).
    /// </summary>
    public static bool Showing(FrameworkElement element) =>
        element.IsVisible && Window.GetWindow(element) is not { WindowState: WindowState.Minimized };
}
