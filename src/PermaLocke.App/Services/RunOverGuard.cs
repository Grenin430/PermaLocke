using System.Windows;
using PermaLocke.App.Views;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// The end of a run with lives (2026-10-29): when the last heart breaks the run is over for good. It writes one <see cref="GameEventType.RunEnded"/>,
/// says so, closes the game, and the launcher refuses to open it again for that run. Lives only live in the run, so nothing here can be undone
/// from the app: the only way on is a new run.
/// </summary>
public sealed class RunOverGuard(IRunContext runs, CemeteryService cemetery, IEventStore events, EmulatorLauncher launcher,
    IClock clock, ILogger<RunOverGuard> logger)
{
    private const string Message = "Te has quedado sin vidas. Esta run ha terminado y no se puede seguir jugando. Solo puedes empezar una run nueva.";

    private Guid _over;
    private Guid _told;
    private bool _busy;
    private Guid _alive;
    private DateTime _closeAt;
    private Guid _closed;
    private Guid _endedChecked;

    public void Start()
    {
        launcher.LaunchBlocker = () => runs.Current is { } run && run.Id == _over ? Message : null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) => await CheckAsync();
        timer.Start();
    }

    private async Task CheckAsync()
    {
        if (_busy || runs.Current is not { Lives: { } } run) return;
        _busy = true;
        try
        {
            if (run.Id != _over)
            {
                // a run that already ended stays ended, even if a death is undone later (Admin, a false positive)
                var ended = false;
                if (_endedChecked != run.Id)
                {
                    _endedChecked = run.Id;
                    ended = (await events.GetAllAsync(run.Id)).Any(e => e.Type == GameEventType.RunEnded);
                }

                var fallen = run.FallenCounted((await cemetery.GravesAsync()).Select(g => g.DiedAt));
                if (!ended && run.LivesLeft(fallen) != 0) { _alive = run.Id; return; }
                _over = run.Id;
                // lost while playing: the last death and the loss of the Locke play in the overlay before the game closes
                CapLockLoss.Finished = false;
                _closeAt = _alive == run.Id && launcher.IsRunning ? DateTime.UtcNow.AddSeconds(30) : DateTime.UtcNow;
            }

            if (_told != run.Id)
            {
                _told = run.Id;
                var all = await events.GetAllAsync(run.Id);
                if (!all.Any(e => e.Type == GameEventType.RunEnded))
                {
                    await events.AppendAsync(new GameEvent
                    {
                        Id = Guid.NewGuid(), RunId = run.Id, Timestamp = clock.Now, Type = GameEventType.RunEnded, Source = EventSource.System,
                        Actor = run.PlayerName, Description = "Se rompió la última vida: la run ha terminado.",
                        Data = new Dictionary<string, string> { ["vidas"] = run.Lives.ToString()! }
                    });
                }


            }

            if (!CapLockLoss.Finished && DateTime.UtcNow < _closeAt) return;
            if (_closed != run.Id) { _closed = run.Id; _ = Application.Current.Dispatcher.BeginInvoke(() => PixelDialog.Show("Fin de la run", Message, PixelDialogKind.Error, onTop: true)); }
            if (launcher.IsRunning) { launcher.Close(); await Task.Delay(4000); if (launcher.IsRunning) launcher.ForceClose(); }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se ha podido comprobar el fin de la run por vidas");
        }
        finally { _busy = false; }
    }
}
