using System.Runtime.InteropServices;

namespace PermaLocke.App.Services;

/// <summary>
/// A sleep that lasts what it is asked (1 ms and up) on Windows 10 and later: a high resolution waitable timer. .NET's own timers wake on the 15.6 ms
/// tick of the system, which turns a 4 ms wait into 15 (measured: <c>Task.Delay(1)</c> 15.6 ms, this 1.5 ms). Without it (an older Windows) it falls
/// back to the coarse sleep, and the reading is slower, not wrong.
/// </summary>
public sealed class HighResolutionTimer
{
    private const uint HighResolutionFlag = 0x2, AllAccess = 0x1F0003;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll")]
    private static extern bool SetWaitableTimer(IntPtr timer, ref long due, int period, IntPtr routine, IntPtr arg, bool resume);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    private readonly IntPtr _handle = CreateWaitableTimerExW(IntPtr.Zero, null, HighResolutionFlag, AllAccess);

    public bool Available => _handle != IntPtr.Zero;

    public void Sleep(double milliseconds)
    {
        if (milliseconds <= 0) return;
        if (!Available)
        {
            Thread.Sleep((int)Math.Ceiling(milliseconds));
            return;
        }

        var due = -(long)(milliseconds * 10_000);
        SetWaitableTimer(_handle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false);
        WaitForSingleObject(_handle, (uint)Math.Max(50, milliseconds * 2));
    }
}
