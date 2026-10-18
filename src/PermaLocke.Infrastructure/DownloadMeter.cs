using System.Globalization;

namespace PermaLocke.Infrastructure;

/// <summary>How a download is going, in the words the update window shows.</summary>
/// <param name="BytesPerSecond">Over the last seconds, not since the start: a download that slows down shows it.</param>
/// <param name="Remaining">Null until there is a speed to reckon it with, or when the size is unknown.</param>
public sealed record DownloadProgress(long Received, long Total, double BytesPerSecond, TimeSpan? Remaining)
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>From 0 to 1; 0 while the size is unknown.</summary>
    public double Fraction => Total > 0 ? Math.Clamp((double)Received / Total, 0, 1) : 0;

    /// <summary>«34,2 / 71,5 MB», or «34,2 MB» while the size is unknown.</summary>
    public string Size => Total > 0 ? $"{Megabytes(Received)} / {Megabytes(Total)} MB" : $"{Megabytes(Received)} MB";

    /// <summary>«42 %».</summary>
    public string Percent => $"{(int)Math.Floor(Fraction * 100)} %";

    /// <summary>«5,3 MB/S», or «850 KB/S» under a megabyte a second.</summary>
    public string Speed => BytesPerSecond >= 1024 * 1024
        ? $"{(BytesPerSecond / (1024 * 1024)).ToString("0.0", Spanish)} MB/S"
        : $"{(BytesPerSecond / 1024).ToString("0", Spanish)} KB/S";

    /// <summary>«QUEDAN 7 S», «QUEDAN 2 MIN», or «CALCULANDO».</summary>
    public string Left => Remaining switch
    {
        null => "CALCULANDO",
        { TotalSeconds: < 60 } time => $"QUEDAN {Math.Max(1, (int)Math.Ceiling(time.TotalSeconds))} S",
        { } time => $"QUEDAN {(int)Math.Ceiling(time.TotalMinutes)} MIN"
    };

    private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024)).ToString("0.0", Spanish);
}

/// <summary>
/// Measures a download as it goes (§201): the speed over a sliding window of the last few seconds, so it follows the
/// network as it changes instead of averaging the whole download, and the time left from that speed. Pure: the caller
/// passes the time, so the tests do not wait.
/// </summary>
public sealed class DownloadMeter(long total)
{
    /// <summary>How far back the speed looks.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Queue<(TimeSpan At, long Received)> _samples = new();

    public long Total { get; } = total;

    /// <summary>Records that <paramref name="received"/> bytes have arrived <paramref name="elapsed"/> after the start.</summary>
    public DownloadProgress Sample(long received, TimeSpan elapsed)
    {
        _samples.Enqueue((elapsed, received));

        // Se queda con lo que cae dentro de la ventana, más la muestra justo anterior como punto de partida.
        while (_samples.Count > 2 && elapsed - _samples.ElementAt(1).At >= Window)
        {
            _samples.Dequeue();
        }

        var (fromAt, fromReceived) = _samples.Peek();
        var seconds = (elapsed - fromAt).TotalSeconds;
        var speed = seconds > 0.2 ? Math.Max(0, received - fromReceived) / seconds : 0;

        TimeSpan? remaining = Total > 0 && speed > 0
            ? TimeSpan.FromSeconds(Math.Max(0, Total - received) / speed)
            : null;

        return new DownloadProgress(received, Total, speed, remaining);
    }
}

/// <summary>
/// The copy loop of a download (§201, arreglado en §204): from the network stream to the file, telling how it goes at
/// most every <c>every</c>. Here and not inside <c>UpdateService</c> so it runs in the tests: in 1.0.2 and 1.0.3 this loop
/// started its «last shown» at <see cref="TimeSpan.MinValue"/>, and «elapsed − MinValue» overflowed on the first piece,
/// so no update could ever be downloaded.
/// </summary>
public static class DownloadCopy
{
    /// <param name="elapsed">The time since the download started; a stopwatch in the app, a fake clock in the tests.</param>
    /// <returns>The bytes copied.</returns>
    public static async Task<long> CopyAsync(Stream from, Stream to, DownloadMeter meter, Action<DownloadProgress> report,
        Func<TimeSpan> elapsed, TimeSpan every, CancellationToken cancel = default)
    {
        var buffer = new byte[128 * 1024];
        long received = 0;
        TimeSpan? shown = null;

        report(meter.Sample(0, TimeSpan.Zero));

        int read;
        while ((read = await from.ReadAsync(buffer, cancel)) > 0)
        {
            await to.WriteAsync(buffer.AsMemory(0, read), cancel);
            received += read;

            var now = elapsed();
            if (shown is not { } last || now - last >= every)
            {
                shown = now;
                report(meter.Sample(received, now));
            }
        }

        report(meter.Sample(received, elapsed()));
        return received;
    }
}
