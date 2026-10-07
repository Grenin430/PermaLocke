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
    /// <remarks>
    /// The window found last time is kept, never a process: holding a <c>Process</c> keeps a closed emulator in the table
    /// (see <see cref="EmulatorProcess"/>). While it is still a visible window of the same process it is the answer, and
    /// nothing is listed: looking through the process table costs about 6 ms of CPU each time (390 processes, measured
    /// 2026-10-03), and the cap, the edge tab and the notices asked several times a second, beside the emulator.
    /// </remarks>
    public static IntPtr Handle()
    {
        var (known, owner) = _known;

        if (known != IntPtr.Zero && IsWindow(known) && IsWindowVisible(known)
            && GetWindowThreadProcessId(known, out var process) != 0 && process == owner)
        {
            return known;
        }

        _known = (IntPtr.Zero, 0);

        foreach (var candidate in EmulatorProcess.Candidates())
        {
            try
            {
                var handle = candidate.MainWindowHandle;
                if (handle != IntPtr.Zero && _known.Window == IntPtr.Zero) _known = (handle, (uint)candidate.Id);
            }
            catch (InvalidOperationException) { } // Closed during the lookup.
            catch (System.ComponentModel.Win32Exception) { }
            finally
            {
                candidate.Dispose();
            }
        }

        return _known.Window;
    }

    /// <summary>Whether the emulator's window found last time is still there, without listing processes.</summary>
    internal static bool KnownAlive()
    {
        var (known, owner) = _known;
        return known != IntPtr.Zero && IsWindow(known) && GetWindowThreadProcessId(known, out var process) != 0
               && process == owner;
    }

    private static (IntPtr Window, uint Process) _known;

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

    /// <summary>The window's monitor work area, always in screen pixels, including the fallback.</summary>
    public static (int Left, int Top, int Width, int Height) WorkArea(IntPtr window)
    {
        var monitor = MonitorFromWindow(window, NearestMonitor);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info)
            && info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top
            ? (info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top)
            : OverlayWindows.WorkArea();
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
        (int Left, int Top, int Width, int Height)? largest = null;
        var name = new System.Text.StringBuilder(128);

        EnumChildWindows(window, (child, _) =>
        {
            name.Clear();

            if (!IsWindowVisible(child) || GetClassName(child, name, name.Capacity) <= 0
                || !GetWindowRect(child, out var box) || box.Right <= box.Left || box.Bottom <= box.Top)
            {
                return true;
            }

            var area = (box.Right - box.Left) * (box.Bottom - box.Top);
            if (largest is not { } best || area > best.Width * best.Height)
            {
                largest = (box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
            }

            if (name.ToString().Contains("OwnDC", StringComparison.Ordinal))
            {
                found = (box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        // Con Vulkan Qt no crea esa ventana de clase OwnDC (medido con Azahar 0dfe782, 2026-10-07): la superficie de dibujo es una
        // ventana «QWindowIcon» del mismo tamaño y en el mismo sitio, la mayor de todas. Sin esto, el panel del cap, los avisos
        // sobre el juego y la barra de PS no encontraban el juego y callaban (§235).
        return found ?? largest;
    }

    /// <summary>
    /// Where the 3DS top screen is drawn, as the corner in screen pixels and the size of one native
    /// pixel, or null when the emulator's picture cannot be found.
    /// </summary>
    /// <remarks>
    /// Azahar's default layout, which is the player's: 400×480 — the 400×240 top screen with the bottom
    /// one under it — scaled to fit the picture and centred. Shared by everything that reads the game off
    /// the screen, so the bar watcher and the killcam cannot disagree about where the game is.
    /// </remarks>
    public static (double Left, double Top, double Scale)? TopScreen() => TopScreen(Handle());

    /// <inheritdoc cref="TopScreen()"/>
    public static (double Left, double Top, double Scale)? TopScreen(IntPtr window)
    {
        if (RenderBox(window) is not { } box)
        {
            return null;
        }

        var scale = Math.Min(box.Width / 400.0, box.Height / 480.0);

        return scale <= 0
            ? null
            : (box.Left + ((box.Width - (400 * scale)) / 2), box.Top + ((box.Height - (480 * scale)) / 2), scale);
    }

    /// <summary>
    /// Whether what is on screen over a box, in screen pixels, really is the emulator: no other window in
    /// front of it and none of it off the monitor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The killcam and the bar watcher copy the screen, not the emulator, so whatever sits in front of
    /// Azahar gets read as if it were the game. Found on the first killcam rehearsal: 93 frames recorded
    /// and read back without a fault, and every one of them was another game running full screen over the
    /// emulator. A replay like that next to a death would be a false record.
    /// </para>
    /// <para>
    /// Nine points of the box — corners, edges and centre — are asked which window is there, and all nine
    /// have to belong to the emulator. PermaLocke's overlays do not answer: they are layered and
    /// click-through, so hit testing goes past them — measured with such a window over Azahar, the point
    /// still answered <c>azahar</c>. That is not the same as the capture leaving them out, which it does
    /// not: the death ceremony tells the killcam itself when it covers the game. A window that is not
    /// click-through does answer, so the check errs towards losing a frame, never towards keeping a wrong
    /// one.
    /// </para>
    /// </remarks>
    public static bool Shows(IntPtr window, double left, double top, double width, double height) =>
        FirstBlocked(window, left, top, width, height) is null;

    /// <summary>
    /// What is in front of the emulator over a box, in words for the log, or null when it is the emulator
    /// that shows.
    /// </summary>
    public static string? Hiding(IntPtr window, double left, double top, double width, double height)
    {
        if (FirstBlocked(window, left, top, width, height) is not { } blocked)
        {
            return null;
        }

        if (blocked.Found == IntPtr.Zero)
        {
            return window == IntPtr.Zero ? "no hay ventana del emulador" : $"ninguna ventana en {blocked.Spot.X},{blocked.Spot.Y}";
        }

        var root = GetAncestor(blocked.Found, RootWindow);
        GetWindowThreadProcessId(root, out var process);
        var name = new System.Text.StringBuilder(128);
        GetClassName(blocked.Found, name, name.Capacity);

        string owner;

        try
        {
            owner = System.Diagnostics.Process.GetProcessById((int)process).ProcessName;
        }
        catch (ArgumentException)
        {
            owner = $"proceso {process}";
        }

        return $"{owner} ({name}, raíz {root}, emulador {window}) en {blocked.Spot.X},{blocked.Spot.Y}";
    }

    private static (Spot Spot, IntPtr Found)? FirstBlocked(IntPtr window, double left, double top, double width, double height)
    {
        if (window == IntPtr.Zero || width <= 0 || height <= 0)
        {
            return (new Spot(), IntPtr.Zero);
        }

        foreach (var fy in Probes)
        {
            foreach (var fx in Probes)
            {
                var spot = new Spot { X = (int)Math.Floor(left + (width * fx)), Y = (int)Math.Floor(top + (height * fy)) };
                var found = WindowFromPoint(spot);

                if (found == IntPtr.Zero || GetAncestor(found, RootWindow) != window)
                {
                    return (spot, found);
                }
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    /// <summary>Where in the box to look: just inside each edge, and the middle.</summary>
    private static readonly double[] Probes = [0.02, 0.5, 0.98];

    private const uint RootWindow = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Spot point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    private delegate bool EnumChildProc(IntPtr child, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);

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
