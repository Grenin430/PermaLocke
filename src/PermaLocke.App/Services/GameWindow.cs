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
