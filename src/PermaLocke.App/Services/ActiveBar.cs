using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink.Battle;

namespace PermaLocke.App.Services;

/// <summary>
/// How much life the Pokémon that is on the field has, read from the game's own HP bar on the screen (2026-10-11): memory does not say which of the party
/// is out (the battle's blocks keep the order the battle began with), but the bar on the screen is always that one's. Thirty looks a second (eight until 2026-10-15: the life shown follows this bar as it drains), only while there
/// is a battle, a few milliseconds each (measured on the desktop: 4 ms mean). The last look with colour is kept for <see cref="HoldSeconds"/>: the box hides
/// under an attack animation and a message, and a hidden box says nothing, so the panel keeps what it knew until the box is back. Without the window, with a
/// layout that is not computed, or with the box hidden for long, it says nothing (null) and the sea is not in danger.
/// </summary>
public sealed class ActiveBar : IDisposable
{
    public const double HoldSeconds = 3;

    private readonly ManualResetEventSlim _on = new(false);
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private readonly ILogger<ActiveBar>? _log;
    private double _fill = -1;
    private long _seenAt;
    private string _lastWhat = "";
    private bool _had;
    private bool _seenInBattle;
    private long _loggedAt;

    /// <summary>The life of the one on the field moved by a notch: the panel looks again at once and does not wait for its own beat.</summary>
    public event Action? Moved;

    public ActiveBar(ILogger<ActiveBar>? log = null) => _log = log;

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
            Volatile.Write(ref _seenInBattle, false);
        }
    }

    /// <summary>
    /// Whether the bar has been seen with colour at all in this battle (2026-10-15). The box hides for seconds at a time (the move menu, an attack
    /// animation, a message), longer than <see cref="HoldSeconds"/>: what to hold back is decided by this and not by the last look.
    /// </summary>
    public bool SeenInBattle => Volatile.Read(ref _seenInBattle);

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
                var sample = HpBarWatcher.Sample();
                if (sample is { State: HpBarState.Filled } reading)
                {
                    var before = Volatile.Read(ref _fill);
                    Volatile.Write(ref _fill, reading.Fill);
                    if (Math.Abs(before - reading.Fill) >= 0.01) Moved?.Invoke();
                    Interlocked.Exchange(ref _seenAt, Stopwatch.GetTimestamp());
                    Volatile.Write(ref _seenInBattle, true);
                }

                // What the look says is logged when it changes, so a bar that is never seen leaves the reason in the log.
                var what = sample is null ? "sin ventana o con un diseño de pantallas que no se calcula" : sample.Value.State.ToString();
                var stamp = Stopwatch.GetTimestamp();
                if (what != _lastWhat || (what == "Hidden" && (stamp - _loggedAt) / (double)Stopwatch.Frequency > 5))
                {
                    _lastWhat = what;
                    _loggedAt = stamp;
                    _log?.LogDebug("ActiveBar: la barra de PS del que está en el campo se ve así: {What} ({Why})", what, HpBarWatcher.LastWhy);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A look that fails is a look that did not happen.
            }

            // The held look ran out (the box stayed away): the panel is told, so the sea does not wait for its own beat to let go of it.
            var has = Current() is not null;
            if (_had && !has) Moved?.Invoke();
            _had = has;

            timer.Sleep(33);
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
