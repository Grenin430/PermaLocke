using PermaLocke.Infrastructure;

namespace PermaLocke.Core.Tests;

/// <summary>The update window's numbers (§201): size, speed over the last seconds, time left.</summary>
public sealed class DownloadMeterTests
{
    private const long MB = 1024 * 1024;

    [Fact]
    public void Speed_follows_the_last_seconds_not_the_whole_download()
    {
        var meter = new DownloadMeter(100 * MB);

        // 10 s a 1 MB/s y luego 3 s a 5 MB/s: la velocidad es la de ahora.
        for (var s = 0; s <= 10; s++) meter.Sample(s * MB, TimeSpan.FromSeconds(s));
        DownloadProgress last = null!;
        for (var s = 1; s <= 3; s++) last = meter.Sample((10 + 5 * s) * MB, TimeSpan.FromSeconds(10 + s));

        Assert.Equal(5.0, last.BytesPerSecond / MB, 1);
        Assert.Equal("5,0 MB/S", last.Speed);
        Assert.Equal("25,0 / 100,0 MB", last.Size);
        Assert.Equal("25 %", last.Percent);
        Assert.Equal("QUEDAN 15 S", last.Left);
    }

    [Fact]
    public void Says_calculating_until_there_is_a_speed_and_minutes_when_long()
    {
        var meter = new DownloadMeter(100 * MB);
        Assert.Equal("CALCULANDO", meter.Sample(0, TimeSpan.Zero).Left);

        var slow = meter.Sample(MB / 2, TimeSpan.FromSeconds(1));
        Assert.Equal("512 KB/S", slow.Speed);
        Assert.Equal("QUEDAN 4 MIN", slow.Left);
    }

    [Fact]
    public void Without_a_size_it_shows_what_has_arrived()
    {
        var progress = new DownloadMeter(0).Sample(3 * MB, TimeSpan.FromSeconds(1));
        Assert.Equal(0, progress.Fraction);
        Assert.Equal("3,0 MB", progress.Size);
        Assert.Equal("CALCULANDO", progress.Left);
    }
}

/// <summary>The download loop itself (§204): the 1.0.2 and 1.0.3 one overflowed on the first piece.</summary>
public sealed class DownloadCopyTests
{
    [Fact]
    public async Task Copies_everything_and_reports_with_a_real_clock()
    {
        var data = new byte[3 * 1024 * 1024 + 17];
        new Random(7).NextBytes(data);
        using var from = new MemoryStream(data);
        using var to = new MemoryStream();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var reports = new List<DownloadProgress>();

        var copied = await DownloadCopy.CopyAsync(from, to, new DownloadMeter(data.Length), reports.Add,
            () => clock.Elapsed, TimeSpan.FromMilliseconds(100));

        Assert.Equal(data.Length, copied);
        Assert.Equal(data, to.ToArray());
        Assert.Equal(0, reports[0].Received);
        Assert.Equal(1.0, reports[^1].Fraction);
    }

    [Fact]
    public async Task Reports_at_most_every_interval()
    {
        var data = new byte[128 * 1024 * 10];
        using var from = new MemoryStream(data);
        using var to = new MemoryStream();
        var tick = TimeSpan.Zero;
        var reports = new List<DownloadProgress>();

        // Cada trozo tarda 30 ms: con un aviso cada 100 ms, uno de cada cuatro trozos más o menos.
        await DownloadCopy.CopyAsync(from, to, new DownloadMeter(data.Length), reports.Add,
            () => tick += TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(100));

        // El de salida, el del primer trozo, los de cada 100 ms (a los 150, 270) y el final.
        Assert.InRange(reports.Count, 4, 6);
        Assert.Equal(data.Length, reports[^1].Received);
    }
}
