using System.Runtime.InteropServices;
using System.Windows;

namespace PermaLocke.App.Services;

/// <summary>
/// Where the emulator is, and which screen it is on.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the notices and by the edge tab because both had the same job and got it half right
/// on their own: the notices already looked for Azahar's window, and the tab did not — it always
/// went to the left edge of the <b>primary</b> screen. With two monitors and the game on the
/// second one, that puts it where nobody is looking.
/// </para>
/// <para>
/// The screen is found from the emulator's window and not from its corner: a window that straddles
/// two monitors belongs to the one it is mostly on, which is what <c>MonitorFromWindow</c> answers.
/// </para>
/// </remarks>
public static class GameWindow
{
    /// <summary>The emulator's window, or null when it is not running.</summary>
    public static IntPtr Handle()
    {
        foreach (var name in new[] { "azahar", "citra" })
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>Its window box, or the primary work area when there is no emulator.</summary>
    public static Rect Area()
    {
        var window = Handle();

        return window != IntPtr.Zero && GetWindowRect(window, out var box) && box.Right > box.Left
            ? new Rect(box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top)
            : SystemParameters.WorkArea;
    }

    /// <summary>
    /// The usable area of the screen the emulator is on — taskbar excluded.
    /// </summary>
    /// <remarks>
    /// The <b>screen</b> and not the emulator's own box: the tab lives on the edge of the monitor,
    /// so hugging a small windowed emulator would leave it floating in the middle of the desktop.
    /// </remarks>
    public static Rect Screen()
    {
        var window = Handle();

        if (window == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }

        var monitor = MonitorFromWindow(window, NearestMonitor);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info)
            ? new Rect(info.Work.Left, info.Work.Top,
                info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top)
            : SystemParameters.WorkArea;
    }

    /// <summary>
    /// The inside of a window — title bar and borders excluded — in screen pixels, or null when
    /// the window is gone or minimised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For anything that has to <b>cover</b> a window rather than sit near it. <see cref="Area"/> is
    /// the outer box, which is fine for a notice in a corner and wrong for a veil over the game: it
    /// would darken the emulator's title bar and hang over the desktop by the width of the border.
    /// </para>
    /// <para>
    /// Screen pixels and not WPF units, on purpose: the caller places its window with
    /// <c>SetWindowPos</c>, which speaks the same units as this, so the two agree whatever the
    /// display scaling is. Handing these to <c>Window.Left</c> would be right at 100% and off by a
    /// quarter at 125%.
    /// </para>
    /// </remarks>
    public static (int Left, int Top, int Width, int Height)? ClientBox(IntPtr window)
    {
        if (window == IntPtr.Zero || IsIconic(window) || !GetClientRect(window, out var inside))
        {
            return null;
        }

        var corner = new Spot { X = 0, Y = 0 };

        if (!ClientToScreen(window, ref corner) || inside.Right <= 0 || inside.Bottom <= 0)
        {
            return null;
        }

        return (corner.X, corner.Y, inside.Right, inside.Bottom);
    }

    /// <summary>
    /// Where the emulator actually draws the game, in screen pixels, or null when it cannot be found.
    /// </summary>
    /// <remarks>
    /// <see cref="ClientBox"/> is not it: Windows counts Azahar's menu bar inside the client area, so
    /// the picture starts 33 pixels lower than the client does — measured on the player's window, the
    /// top screen began at y 56 with the client at 23. Qt draws the game in a child window of its own
    /// with an OpenGL surface (class <c>Qt…QWindowOwnDC…</c>), and that window's box is exactly the
    /// picture: (0,56)-(1920,1032) there. Found by class and not by position, because the menu bar's
    /// height changes with the Windows theme and scaling.
    /// </remarks>
    public static (int Left, int Top, int Width, int Height)? RenderBox(IntPtr window)
    {
        if (window == IntPtr.Zero || IsIconic(window))
        {
            return null;
        }

        (int Left, int Top, int Width, int Height)? found = null;
        var name = new System.Text.StringBuilder(128);

        EnumChildWindows(window, (child, _) =>
        {
            name.Clear();

            if (IsWindowVisible(child) && GetClassName(child, name, name.Capacity) > 0
                && name.ToString().Contains("OwnDC", StringComparison.Ordinal)
                && GetWindowRect(child, out var box) && box.Right > box.Left && box.Bottom > box.Top)
            {
                found = (box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumChildProc(IntPtr child, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);

    [StructLayout(LayoutKind.Sequential)]
    private struct Spot
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Box box);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref Spot spot);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    private const uint NearestMonitor = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Box Whole;
        public Box Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Box box);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
