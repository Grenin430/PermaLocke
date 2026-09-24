using System.Diagnostics;
using System.IO;
using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

/// <summary>
/// What PermaLocke does on its own when Azahar falls over (§168): know it was a crash and not a close, and keep what
/// only exists at that moment, so nobody has to go through a friend's computer with them.
/// </summary>
public sealed class EmulatorCrashTests
{
    /// <summary>
    /// A process PermaLocke did not start still leaves its exit code behind, because the handle is held while it runs.
    /// Without that, a crash and the player closing the window both look like «the process is gone».
    /// </summary>
    [Fact]
    public void The_exit_code_of_a_process_it_did_not_start_survives_the_process()
    {
        // Espera un segundo y sale con 0xC0000005, que es lo que deja un acceso a memoria no válido.
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul & exit -1073741819")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

        using var watch = new ProcessExitWatch(process.Id);

        Assert.True(watch.IsHeld);
        Assert.Null(watch.ExitCode());

        process.WaitForExit(10_000);

        Assert.Equal(0xC0000005u, watch.ExitCode());
    }

    [Fact]
    public void A_process_that_does_not_exist_is_not_held_and_has_no_exit_code()
    {
        using var watch = new ProcessExitWatch(int.MaxValue - 1);

        Assert.False(watch.IsHeld);
        Assert.Null(watch.ExitCode());
    }

    /// <summary>Closing the window, PermaLocke's own close and a kill from outside are not crashes; the rest are.</summary>
    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    [InlineData(0xC000013Au, false)]
    [InlineData(0xC0000005u, true)]
    [InlineData(0xC0000409u, true)]
    [InlineData(0xC0000135u, true)]
    [InlineData(0xC0000139u, true)]
    public void Only_a_real_failure_is_a_crash(uint code, bool crash) =>
        Assert.Equal(crash, EmulatorCrashReport.IsCrash(code));

    /// <summary>The two codes that point at the Visual C++ runtime say so, because that is what the player can fix.</summary>
    [Fact]
    public void The_codes_that_point_at_the_runtime_say_so()
    {
        Assert.Null(EmulatorCrashReport.Meaning(0));
        Assert.Contains("DLL", EmulatorCrashReport.Meaning(0xC0000135));
        Assert.Contains("Visual C++", EmulatorCrashReport.Meaning(0xC0000139));
        Assert.Equal("cierre inesperado", EmulatorCrashReport.Meaning(0x12345678));
    }

    /// <summary>
    /// The logs are still open when the report is written — Azahar's by nobody, PermaLocke's by its own logger — and
    /// only the last lines go in.
    /// </summary>
    [Fact]
    public void The_end_of_a_log_another_program_is_writing_can_be_read()
    {
        var path = Path.Combine(Path.GetTempPath(), $"permalocke-log-{Guid.NewGuid():N}.txt");

        try
        {
            using (var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
            {
                for (var line = 1; line <= 1000; line++)
                {
                    writer.WriteLine($"línea {line}");
                }

                writer.Flush();

                var tail = EmulatorCrashReport.Tail(path, 400);

                Assert.Equal(400, tail.Count);
                Assert.Equal("línea 1000", tail[^1]);
                Assert.Equal("línea 601", tail.First());
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>OneDrive locks files while it uploads them: a PermaLocke inside it is worth a warning.</summary>
    [Theory]
    [InlineData(@"C:\Users\Ana\OneDrive\Escritorio\PermaLocke", true)]
    [InlineData(@"C:\Users\Ana\OneDrive - Empresa\PermaLocke", true)]
    [InlineData(@"C:\Juegos\PermaLocke", false)]
    [InlineData(@"D:\OneDriveNo\PermaLocke", false)]
    public void A_folder_inside_onedrive_is_recognised(string path, bool inside) =>
        Assert.Equal(inside, Machine.IsInOneDrive(path));

    /// <summary>The emulator loads its own runtime when it carries one, and that one decides.</summary>
    [Fact]
    public void The_runtime_carried_with_the_emulator_is_the_one_that_counts()
    {
        var needed = new Version(14, 51);

        Assert.True(new VisualCppStatus(needed, new Version(14, 51), new Version(14, 29)).Enough);
        Assert.False(new VisualCppStatus(needed, null, new Version(14, 29)).Enough);
        Assert.True(new VisualCppStatus(needed, null, new Version(14, 51)).Enough);
        Assert.False(new VisualCppStatus(needed, null, null).Enough);
    }

    /// <summary>The linker version is read from a real executable: this test's own host is built with Microsoft's toolset.</summary>
    [Fact]
    public void The_linker_version_is_read_from_a_real_executable()
    {
        var version = VisualCppRuntime.LinkerVersion(Environment.ProcessPath!);

        Assert.NotNull(version);
        Assert.Equal(14, version!.Major);
    }
}
