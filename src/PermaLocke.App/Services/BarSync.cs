using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink.Battle;

namespace PermaLocke.App.Services;

/// <summary>
/// Watches the HP bar of the game on the screen while the panel holds a change back (2026-10-11), and says when the bar starts to move and when it stops:
/// memory says what changed the instant it changes, but only the bar says when the game shows it. It looks only while it is armed and for at most
/// <see cref="HpSyncTable.GiveUpSeconds"/>, twenty-five times a second, on a thread of its own; reading the screen costs a few milliseconds a time (measured
/// on the desktop: 4 ms mean, 7 ms p95), so it is a few percent of one core for the second or two a hit lasts, and nothing the rest of the time.
/// What it needs is the emulator's window in front, uncovered, and a layout it can compute (<see cref="HpBarGeometry"/>); without them it never signals and
/// the panel falls back to the delay it computes.
/// </summary>
public sealed class BarSync : IDisposable
{
    private const double SampleMilliseconds = 25;

    private readonly ILogger<BarSync> _logger;
    private readonly ManualResetEventSlim _armed = new(false);
    private readonly CancellationTokenSource _stop = new();
    private readonly BarTracker _tracker = new();
    private readonly Lock _gate = new();
    private Thread? _thread;

    public BarSync(ILogger<BarSync> logger) => _logger = logger;

    /// <summary>Raised on the watcher's own thread, with the ticks (<see cref="Stopwatch.GetTimestamp"/>) of the reading that saw it.</summary>
    public event Action<BarSignal>? Signal;

    /// <summary>The panel is holding a change back: start looking at the bar.</summary>
    public void Arm()
    {
        lock (_gate)
        {
            _tracker.Arm();
        }

        if (_thread is null)
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "PermaLocke bar", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        _armed.Set();
    }

    public void Disarm()
    {
        lock (_gate)
        {
            _tracker.Disarm();
        }

        _armed.Reset();
    }

    private void Run()
    {
        var timer = new HighResolutionTimer();
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                _armed.Wait(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var started = Stopwatch.GetTimestamp();
            while (_armed.IsSet && !_stop.IsCancellationRequested)
            {
                var now = Stopwatch.GetTimestamp();
                if ((now - started) / (double)Stopwatch.Frequency > HpSyncTable.GiveUpSeconds)
                {
                    Disarm();
                    break;
                }

                BarSignal signal;
                try
                {
                    var reading = HpBarWatcher.Sample() ?? new HpBarReading(HpBarState.Hidden, 0);
                    lock (_gate)
                    {
                        signal = _tracker.Observe(reading, Stopwatch.GetTimestamp());
                        if (signal.Kind == BarSignalKind.Ended || !_tracker.Armed) _armed.Reset();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "No se ha podido mirar la barra de PS");
                    Disarm();
                    break;
                }

                if (signal.Kind != BarSignalKind.None) Signal?.Invoke(signal);
                timer.Sleep(SampleMilliseconds);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _armed.Set();
        _thread?.Join(500);
        _stop.Dispose();
        _armed.Dispose();
    }
}
