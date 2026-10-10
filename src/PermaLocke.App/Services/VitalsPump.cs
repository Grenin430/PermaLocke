using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;
using PermaLocke.GameLink.Vitals;

namespace PermaLocke.App.Services;

/// <summary>What the pump is doing, for the log and the tests.</summary>
public enum PumpState
{
    /// <summary>The game is not there: nothing is read.</summary>
    Away,

    /// <summary>Out of a battle: the party, four times a second.</summary>
    Idle,

    /// <summary>In a battle: the party and the battle's blocks, a hundred and twenty times a second.</summary>
    Battle
}

/// <summary>
/// The fast reading of the game's life (2026-10-11): one thread of its own, a high resolution timer, and only what changes while playing — the HP and
/// the status of the six, and the HP of the battle's blocks — in one batch of small reads. The slow things (the cap, the cemetery, the run, the party
/// rebuilt every second) stay where they were. What it finds it leaves in <see cref="Latest"/> as a whole frame that is replaced, never changed, so the
/// interface takes it without a lock, and it says <see cref="Changed"/> so that the interface does not wait for its own timer.
/// </summary>
/// <remarks>
/// <para>
/// It costs the emulator what it asks for: at a hundred and twenty a second, six small reads in a batch, a few hundred bytes a second. The emulator
/// reads them without touching its graphics (patch 4, §173). If it ever answers slowly the pump slows itself down (the period is never shorter than
/// four times the time of a reading), and it gives up and says so in the log if the reads keep failing.
/// </para>
/// <para>
/// What is read is checked, never assumed: the party structure is read in the clear only after it agreed with the full read of the same moment, a battle
/// block that is not the one it was drops the reading, and a change counts when a second reading says the same.
/// </para>
/// </remarks>
public sealed class VitalsPump : IDisposable
{
    public const double BattleHz = 120, IdleHz = 4;

    private readonly GameLinkMonitor _monitor;
    private readonly AzaharGameStateProvider _provider;
    private readonly BattleTableReader _battle;
    private readonly ILogger<VitalsPump> _logger;
    private readonly Func<bool> _enabled;
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private VitalsFrame? _latest;
    private long _epoch;

    public VitalsPump(GameLinkMonitor monitor, AzaharGameStateProvider provider, BattleTableReader battle, AppSettings settings, ILogger<VitalsPump> logger)
        : this(monitor, provider, battle, () => settings.Current.CapPanel, logger)
    {
    }

    public VitalsPump(GameLinkMonitor monitor, AzaharGameStateProvider provider, BattleTableReader battle, Func<bool> enabled, ILogger<VitalsPump> logger)
    {
        _monitor = monitor;
        _provider = provider;
        _battle = battle;
        _enabled = enabled;
        _logger = logger;
    }

    /// <summary>The last frame read and agreed on, or null when nothing has been read (the game is closed, or the reading is off).</summary>
    public VitalsFrame? Latest => Volatile.Read(ref _latest);

    /// <summary>
    /// Counts the times the reading started again from nothing (the game came back, the party was found anew): a consumer that sees it change takes the
    /// next frame as it is, without announcing what is different from before.
    /// </summary>
    public long Epoch => Interlocked.Read(ref _epoch);

    public PumpState State { get; private set; } = PumpState.Away;

    /// <summary>Raised on the pump's thread each time the frame changes. Keep the handler short: it should hand the work to the interface and return.</summary>
    public event Action<VitalsFrame>? Changed;

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "PermaLocke vitals", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Run()
    {
        var timer = new HighResolutionTimer();
        using var memory = new AzaharBatchReader();
        var reader = new VitalsReader(memory);
        VitalsFrame? accepted = null;
        PartyLayoutKey? calibratedFor = null;
        var failures = 0;
        var nextCalibration = 0L;
        var lastReadMs = 0.0;

        while (!_stop.IsCancellationRequested)
        {
            double wait;
            try
            {
                var snapshot = _monitor.Latest;
                if (!_enabled() || snapshot is not { Connected: true } || _provider.Layout is not { } layout || snapshot.Party.Count == 0)
                {
                    if (accepted is not null || State != PumpState.Away) Restart(ref accepted, ref calibratedFor, reader);
                    State = PumpState.Away;
                    wait = 250;
                }
                else
                {
                    var live = BattleFaintTracker.InProgress(_battle.LastTables);
                    State = live.Count > 0 ? PumpState.Battle : PumpState.Idle;

                    // The party is read in the clear only once it has agreed with the full read; again when the structure or the members change.
                    var key = new PartyLayoutKey(layout.Address, layout.Stride, snapshot.Party.Count, [.. snapshot.Party.Select(m => m.Pid)]);
                    if (!key.Equals(calibratedFor) && Stopwatch.GetTimestamp() >= nextCalibration)
                    {
                        nextCalibration = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);
                        if (reader.CalibrateParty(layout, snapshot.Party)) calibratedFor = key;
                        else _logger.LogDebug("La lectura rápida del equipo no coincide con la lectura completa: se sigue con la lenta");
                    }

                    var started = Stopwatch.GetTimestamp();
                    var frame = reader.ReadConfirmed(accepted, live, reader.PartyReady ? layout : null, snapshot.Party.Count);
                    lastReadMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

                    if (frame is null)
                    {
                        // Nothing coherent: the blocks are being freed, or the emulator is gone. The next full reading says which.
                        if (++failures >= 3 && accepted is not null)
                        {
                            _logger.LogInformation("La lectura rápida se queda sin datos: se vuelve a empezar desde cero");
                            Restart(ref accepted, ref calibratedFor, reader);
                        }
                    }
                    else
                    {
                        failures = 0;
                        if (!ReferenceEquals(frame, accepted) && (accepted is null || !frame.SameAs(accepted)))
                        {
                            accepted = frame;
                            Volatile.Write(ref _latest, frame);
                            Changed?.Invoke(frame);
                        }
                        else if (accepted is not null && !ReferenceEquals(frame, accepted))
                        {
                            // The same life as before, read later: keep the news of when, so that the age of the data is true.
                            accepted = frame;
                            Volatile.Write(ref _latest, frame);
                        }
                    }

                    // A hundred and twenty a second in a battle, four out of one; never faster than four times what a reading takes.
                    var period = 1000.0 / (State == PumpState.Battle ? BattleHz : IdleHz);
                    wait = Math.Max(period - lastReadMs, Math.Min(250, lastReadMs * 3));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Fallo en la lectura rápida");
                wait = 500;
            }

            timer.Sleep(wait);
        }
    }

    private void Restart(ref VitalsFrame? accepted, ref PartyLayoutKey? calibratedFor, VitalsReader reader)
    {
        accepted = null;
        calibratedFor = null;
        reader.ForgetParty();
        Volatile.Write(ref _latest, null);
        Interlocked.Increment(ref _epoch);
    }

    private sealed record PartyLayoutKey(uint Address, uint Stride, int Count, uint[] Pids)
    {
        public bool Equals(PartyLayoutKey? other) => other is not null && Address == other.Address && Stride == other.Stride && Count == other.Count && Pids.SequenceEqual(other.Pids);

        public override int GetHashCode() => HashCode.Combine(Address, Stride, Count);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _thread?.Join(500);
        _stop.Dispose();
    }
}
