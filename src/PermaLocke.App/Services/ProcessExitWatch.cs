using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PermaLocke.App.Services;

/// <summary>
/// Holds on to a process PermaLocke did not start, so that once it is gone its exit code can still be read.
/// </summary>
/// <remarks>
/// <para>
/// The launcher finds Azahar by name every second, because the player can open it by hand (§125), and a process
/// found that way has no exit code once it has vanished: nothing kept it open. The exit code is what tells a player
/// closing the window (0) from the emulator falling over (0xC0000005 and friends), which is the whole difference
/// between «sesión terminada» and a crash worth a report (§168).
/// </para>
/// <para>
/// So the handle is opened while it runs, with the least rights that allow it — query and wait — and kept until the
/// session ends. Windows keeps the exit code as long as one handle is open.
/// </para>
/// </remarks>
public sealed class ProcessExitWatch : IDisposable
{
    private const uint QueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x00100000;
    private const uint StillActive = 259;

    private readonly SafeProcessHandle? _handle;

    public ProcessExitWatch(int processId)
    {
        ProcessId = processId;
        var handle = OpenProcess(QueryLimitedInformation | Synchronize, false, (uint)processId);
        _handle = handle.IsInvalid ? null : handle;
    }

    public int ProcessId { get; }

    /// <summary>False when the process could not be opened, so no exit code will ever be known.</summary>
    public bool IsHeld => _handle is not null;

    /// <summary>The exit code once the process has ended; null while it runs or when it could not be held.</summary>
    public uint? ExitCode()
    {
        if (_handle is null || !GetExitCodeProcess(_handle, out var code))
        {
            return null;
        }

        // STILL_ACTIVE es también un código de salida posible, pero ninguno de los que importan aquí.
        return code == StillActive ? null : code;
    }

    public void Dispose() => _handle?.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);
}
