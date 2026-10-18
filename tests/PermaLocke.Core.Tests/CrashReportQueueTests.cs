using PermaLocke.Infrastructure;

namespace PermaLocke.Core.Tests;

/// <summary>The crash reports still to send (§199): new ones, once each, and never the old ones.</summary>
public sealed class CrashReportQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-informes-" + Guid.NewGuid().ToString("N"));

    private string Folder => Path.Combine(_root, "Diagnosticos");

    private string Config => Path.Combine(_root, "Config");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Report(string name, DateTime written)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path, "zip");
        File.SetLastWriteTimeUtc(path, written);
        return path;
    }

    [Fact]
    public void Sends_each_new_report_once_and_skips_old_ones_and_anything_else()
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        var first = Report("cierre-azahar-20260925-100000.zip", now.UtcDateTime.AddDays(-1));
        var second = Report("cierre-azahar-20260926-110000.zip", now.UtcDateTime.AddHours(-1));
        Report("cierre-azahar-20260801-100000.zip", now.UtcDateTime.AddDays(-50));
        Report("otra-cosa.zip", now.UtcDateTime.AddHours(-1));
        Directory.CreateDirectory(Path.Combine(Folder, "cierre-azahar-20260926-110000"));

        Assert.Equal([first, second], CrashReportQueue.Pending(Folder, Config, now));

        CrashReportQueue.MarkSent(Config, first);
        Assert.Equal([second], CrashReportQueue.Pending(Folder, Config, now));

        CrashReportQueue.MarkSent(Config, second);
        Assert.Empty(CrashReportQueue.Pending(Folder, Config, now));
        Assert.True(File.Exists(first));
    }

    [Fact]
    public void Nothing_to_send_without_the_folder()
    {
        Assert.Empty(CrashReportQueue.Pending(Folder, Config, DateTimeOffset.UtcNow));
    }
}
