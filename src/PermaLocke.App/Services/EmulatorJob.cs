using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PermaLocke.App.Services;

/// <summary>
/// Ties the emulator to PermaLocke: when PermaLocke ends, however it ends, Windows ends the emulator too (2026-09-26).
/// </summary>
/// <remarks>
/// A Windows job object with «kill on job close». Its handle belongs to this process, so Windows closes it when
/// PermaLocke goes, from the X, a crash or the Task Manager, and everything in the job goes with it. That is what makes
/// «close PermaLocke and keep playing unwatched» impossible. The window refuses the X while the game is open; this
/// covers the ways the window never hears about.
/// </remarks>
public static class EmulatorJob
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private static IntPtr _job;

    /// <summary>Puts the process in the job. False when Windows refused; the game still runs, only unbound.</summary>
    public static bool Attach(Process process)
    {
        if (_job == IntPtr.Zero)
        {
            var job = CreateJobObject(IntPtr.Zero, null);

            var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = KillOnJobClose } };
            var size = Marshal.SizeOf<ExtendedLimits>();
            var buffer = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(limits, buffer, false);

                if (job == IntPtr.Zero || !SetInformationJobObject(job, ExtendedLimitInformation, buffer, (uint)size))
                {
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            _job = job;
        }

        return AssignProcessToJobObject(_job, process.Handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll")]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
}
