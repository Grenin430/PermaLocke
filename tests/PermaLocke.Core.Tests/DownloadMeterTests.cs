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
