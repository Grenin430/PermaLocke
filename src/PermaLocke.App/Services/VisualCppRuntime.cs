using System.Diagnostics;
using System.IO;

namespace PermaLocke.App.Services;

/// <param name="Needed">The toolset the emulator was built with: the least runtime it can live with.</param>
/// <param name="Local">The runtime carried next to the emulator, or null when it carries none.</param>
/// <param name="System">The runtime installed in Windows, or null when there is none.</param>
public sealed record VisualCppStatus(Version Needed, Version? Local, Version? System)
{
    /// <summary>The runtime the emulator will load: its own when it has one, Windows's otherwise.</summary>
    public Version? InUse => Local ?? System;

    /// <summary>True when the emulator will load a runtime at least as new as the one it was built for.</summary>
    public bool Enough => InUse is { } used && used >= Needed;
}

/// <summary>
/// Which Microsoft Visual C++ runtime the emulator is going to load on this computer, and whether it is new enough.
/// </summary>
/// <remarks>
/// <para>
/// The friend who reported Azahar closing on its own had the distribution without its runtime, so the emulator took
/// whatever Windows had. Built with toolset 14.51, anything from 14.40 up crashes the first time it locks a mutex with
/// an older <c>msvcp140.dll</c> — documented by Microsoft — and one missing <c>msvcp140_atomic_wait.dll</c> does not even
/// start. The player who builds PermaLocke has the newest runtime and never saw it (§168).
/// </para>
/// <para>
/// Distributions built since then carry the six DLLs next to <c>azahar.exe</c>, and Windows loads those first. This
/// says which one wins and whether it is enough, so an older distribution, or an Azahar picked by hand, still says so.
/// </para>
/// </remarks>
public static class VisualCppRuntime
{
    /// <summary>Every runtime DLL the emulator's binaries import, measured on the fork's build.</summary>
    public static readonly string[] Files =
        ["msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll", "msvcp140_atomic_wait.dll", "vcruntime140.dll", "vcruntime140_1.dll"];

    public static VisualCppStatus Check(string emulatorExecutable)
    {
        var folder = Path.GetDirectoryName(emulatorExecutable) ?? ".";
        var needed = LinkerVersion(emulatorExecutable) ?? new Version(14, 0);

        return new VisualCppStatus(needed,
            Lowest(Files.Select(name => Path.Combine(folder, name))),
            Lowest(Files.Select(name => Path.Combine(Environment.SystemDirectory, name))));
    }

    /// <summary>The linker version in a PE header, which for Microsoft's toolset is the runtime it needs.</summary>
    public static Version? LinkerVersion(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            var header = new byte[0x40];
            if (file.Read(header, 0, header.Length) < header.Length) return null;

            var pe = BitConverter.ToInt32(header, 0x3C);
            var linker = new byte[2];
            file.Seek(pe + 26, SeekOrigin.Begin);
            return file.Read(linker, 0, 2) == 2 ? new Version(linker[0], linker[1]) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// The oldest of a set of DLLs, or null when one is missing: a runtime with a hole is no runtime, and the emulator
    /// would take the missing file from Windows.
    /// </summary>
    private static Version? Lowest(IEnumerable<string> paths)
    {
        Version? lowest = null;

        foreach (var path in paths)
        {
            if (!File.Exists(path)) return null;

            var info = FileVersionInfo.GetVersionInfo(path);
            var version = new Version(info.FileMajorPart, info.FileMinorPart);
            if (lowest is null || version < lowest) lowest = version;
        }

        return lowest;
    }
}
