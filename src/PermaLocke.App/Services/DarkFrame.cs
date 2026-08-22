using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PermaLocke.App.Services;

/// <summary>
/// Asks the desktop window manager for a dark title bar.
/// </summary>
/// <remarks>
/// WPF does not style the non-client area, so a dark application ships with a white caption bar
/// bolted on top of it — and every dialog opens with one too. This is the documented flag that
/// fixes it. Attribute 20 is the one Windows 10 20H1 and later use; 19 was the pre-release
/// spelling, tried as a fallback.
///
/// Purely cosmetic: if the call fails the window opens exactly as it did before, so nothing here
/// is worth reporting to the player.
/// </remarks>
public static class DarkFrame
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    /// <summary>Applies the dark frame as soon as the window has a handle.</summary>
    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            var on = 1;

            if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref on, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref on, sizeof(int));
            }
        };
    }
}
