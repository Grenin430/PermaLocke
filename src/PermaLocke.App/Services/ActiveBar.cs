using System.Diagnostics;
using PermaLocke.GameLink.Battle;

namespace PermaLocke.App.Services;

/// <summary>
/// How much life the Pokémon that is on the field has, read from the game's own HP bar on the screen (2026-10-11): memory does not say which of the party
/// is out (the battle's blocks keep the order the battle began with), but the bar on the screen is always that one's. Four looks a second, only while there
/// is a battle, a few milliseconds each (measured on the desktop: 4 ms mean). The last look with colour is kept for <see cref="HoldSeconds"/>: the box hides
/// under an attack animation and a message, and a hidden box says nothing, so the panel keeps what it knew until the box is back. Without the window, with a
/// layout that is not computed, or with the box hidden for long, it says nothing (null) and the panel falls back to the party's lowest life.
/// </summary>
public sealed class ActiveBar : IDisposable
{
    public const double HoldSeconds = 8;

    private readonly ManualResetEventSlim _on = new(false);
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private double _fill = -1;
    private long _seenAt;

    /// <summary>Starts or stops looking: on while a battle goes on.</summary>
    public void Follow(bool on)
    {
        if (on)
        {
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "PermaLocke active bar" };
                _thread.Start();
            }

            _on.Set();
        }
        else
        {
            _on.Reset();
            Volatile.Write(ref _fill, -1);
        }
    }

    /// <summary>The life of the Pokémon on the field, 0 to 1, or null when it is not known.</summary>
    public double? Current()
    {
        var fill = Volatile.Read(ref _fill);
        if (fill < 0 || (Stopwatch.GetTimestamp() - Interlocked.Read(ref _seenAt)) / (double)Stopwatch.Frequency > HoldSeconds) return null;
        return fill;
    }

    private void Run()
    {
        var timer = new HighResolutionTimer();
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                _on.Wait(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                // Only a bar with colour counts: an empty one is a box coming back from hiding as grey (measured at the move menu), not a fall.
                if (HpBarWatcher.Sample() is { State: HpBarState.Filled } reading)
                {
                    Volatile.Write(ref _fill, reading.Fill);
                    Interlocked.Exchange(ref _seenAt, Stopwatch.GetTimestamp());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A look that fails is a look that did not happen.
            }

            timer.Sleep(250);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _on.Set();
        _thread?.Join(500);
        _stop.Dispose();
        _on.Dispose();
    }
}
