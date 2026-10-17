using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Services;

/// <summary>
/// What every window drawn on top of the game has to do so the game keeps being playable.
/// </summary>
/// <remarks>
/// Out of <see cref="Notifier"/>, where it lived alone, because a second window now needs exactly
/// the same: the death ceremony covers the whole emulator, and a cover that took the mouse or the
/// focus would stop the player mid-battle.
/// </remarks>
public static class OverlayWindows
{
    /// <summary>Makes the window ignore the mouse, never take the focus and stay out of Alt+Tab.</summary>
    public static void MakeUntouchable(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle);

        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }

    /// <summary>Puts the window exactly over a box of screen pixels, on top, without activating it.</summary>
    /// <remarks>
    /// With <c>SetWindowPos</c> and not <c>Left</c>/<c>Top</c>, because the box comes in screen pixels
    /// (<see cref="GameWindow.ClientBox"/>) and WPF's properties are in its own units: the two only
    /// agree at 100% display scaling.
    /// </remarks>
    public static bool PlaceOver(Window window, (int Left, int Top, int Width, int Height) box)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();

        // Solo si ha cambiado algo (2026-09-28): el panel del cap cada medio segundo y el fantasma o la lluvia cada décima se
        // ponían encima el uno del otro sin parar, y donde se tocaban parpadeaba. Siendo todas «siempre encima» del juego,
        // volver a pedirlo no hace falta.
        if (window.IsVisible && Placed.TryGetValue(window, out var last) && last.Box == box)
        {
            return true;
        }

        var done = SetWindowPos(handle, TopMost, box.Left, box.Top, box.Width, box.Height, NoActivate | ShowWindow);
        if (done) Placed.AddOrUpdate(window, new PlacedBox(box));
        return done;
    }

    /// <summary>
    /// Puts the window over a box of screen pixels as part of another window, the game's (2026-10-16): owned by it and not «always on top».
    /// Windows then keeps it just above the game in the stack, so another window in front covers the panel as it covers the game, the panel
    /// goes and comes back with the game when it is minimised and restored, and nothing has to hide it or bring it back by hand.
    /// </summary>
    /// <remarks>
    /// <paramref name="force"/> places it again even if the box has not changed: after the game comes back from being minimised, the box can be
    /// the same while the window is not where it was in the stack, and the panel stayed away until the game was resized.
    /// </remarks>
    public static bool PlaceOnOwner(Window window, (int Left, int Top, int Width, int Height) box, IntPtr owner, bool force = false)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        if (handle == IntPtr.Zero || owner == IntPtr.Zero) return false;

        var ownerChanged = GetWindowLongPtr(handle, GwlpHwndParent) != owner;
        if (ownerChanged) SetWindowLongPtr(handle, GwlpHwndParent, owner);

        if (!force && !ownerChanged && window.IsVisible && Placed.TryGetValue(window, out var last) && last.Box == box) return true;

        // Not topmost any more, and straight above the game in the stack: under whatever window is just above the game (the top when none is).
        var done = SetWindowPos(handle, NotTopMost, box.Left, box.Top, box.Width, box.Height, NoActivate | ShowWindow);
        var above = GetWindow(owner, GwHwndPrev);
        while (above == handle && above != IntPtr.Zero) above = GetWindow(above, GwHwndPrev);
        SetWindowPos(handle, above == IntPtr.Zero ? Top : above, 0, 0, 0, 0, NoActivate | NoMove | NoSize);
        if (done) Placed.AddOrUpdate(window, new PlacedBox(box));
        return done;
    }

    private sealed record PlacedBox((int Left, int Top, int Width, int Height) Box);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, PlacedBox> Placed = new();

    /// <summary>
    /// What is on screen inside a box of screen pixels, as a frozen image, or null if it could not be
    /// read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the death ceremony, which freezes the game on the frame the death was read and drains it
    /// of colour. Kept in memory only: nothing is written anywhere.
    /// </para>
    /// <para>
    /// Plain <c>SRCCOPY</c> without <c>CAPTUREBLT</c>. This said once that leaving the flag out kept
    /// PermaLocke's layered windows out of the copy, and it does not: with the desktop composited, the copy
    /// is what is on the screen, and the first real killcam recorded the ceremony itself (§115). It works
    /// here for another reason — the copy is taken before the ceremony's window is shown.
    /// </para>
    /// </remarks>
    public static BitmapSource? Capture((int Left, int Top, int Width, int Height) box)
    {
        if (box.Width <= 0 || box.Height <= 0)
        {
            return null;
        }

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, box.Width, box.Height);

        try
        {
            var previous = SelectObject(memory, bitmap);
            var copied = BitBlt(memory, 0, 0, box.Width, box.Height, screen, box.Left, box.Top, SrcCopy);
            SelectObject(memory, previous);

            if (!copied)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            image.Freeze();
            return image;
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>The primary screen's work area in screen pixels, for when there is no window to cover.</summary>
    public static (int Left, int Top, int Width, int Height) WorkArea()
    {
        var area = new Box();

        return SystemParametersInfo(GetWorkArea, 0, ref area, 0) && area.Right > area.Left
            ? (area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top)
            : (0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
    }

    private const uint SrcCopy = 0x00CC0020;
    private const uint GetWorkArea = 0x0030;

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr context, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr context, IntPtr item);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source,
        int sourceX, int sourceY, uint operation);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr item);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr context);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref Box value, uint winIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x8000000;
    private const int WsExToolWindow = 0x80;

    private static readonly IntPtr TopMost = new(-1);
    private static readonly IntPtr NotTopMost = new(-2);
    private static readonly IntPtr Top = IntPtr.Zero;
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const int GwlpHwndParent = -8;
    private const uint GwHwndPrev = 3;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint relation);
    private const uint NoActivate = 0x0010;
    private const uint ShowWindow = 0x0040;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width,
        int height, uint flags);
}
