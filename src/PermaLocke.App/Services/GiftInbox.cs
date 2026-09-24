using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.App.Services;

/// <summary>
/// The gifts waiting for this player in the shared folder, and collecting them (§129).
/// </summary>
/// <remarks>
/// <para>
/// It reads the folder and nothing else on its own: <b>nothing is applied until the player presses the button</b>.
/// That is the shape the player asked for — a gift in the corner of the window that fills up — and it is also the
/// honest one, because a gift can change the balance of a run and a run is somebody's work.
/// </para>
/// <para>
/// What is already collected comes from the run's own history, so reinstalling, restoring a copy of the shared folder
/// or reading the same gift on two machines cannot give it twice.
/// </para>
/// </remarks>
public sealed class GiftInbox : INotifyPropertyChanged
{
    private static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(20);

    private readonly SyncService _sync;
    private readonly GiftStore _store;
    private readonly GiftService _gifts;
    private readonly PlayerProfileService _profiles;
    private readonly IRunContext _runContext;
    private readonly ILogger<GiftInbox> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = LookEvery };
    private bool _busy;

    public GiftInbox(SyncService sync, GiftStore store, GiftService gifts, PlayerProfileService profiles,
        IRunContext runContext, ILogger<GiftInbox> logger)
    {
        _sync = sync;
        _store = store;
        _gifts = gifts;
        _profiles = profiles;
        _runContext = runContext;
        _logger = logger;

        _timer.Tick += (_, _) => _ = LookAsync();
        _runContext.CurrentChanged += (_, _) => _ = LookAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>What is there to collect, newest first.</summary>
    public IReadOnlyList<AdminGift> Pending { get; private set; } = [];

    /// <summary>The last few collected, so the inbox is not empty the moment after pressing the button.</summary>
    public IReadOnlyList<AdminGift> Collected { get; private set; } = [];

    public void Start()
    {
        _timer.Start();
        _ = LookAsync();
    }

    /// <summary>Reads the folder and works out what is still to collect.</summary>
    public async Task LookAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var root = _sync.SharedFolder;
            var profile = await _profiles.CurrentAsync();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || profile is null
                || _runContext.Current is not { } run)
            {
                Set([], []);
                return;
            }

            var all = await Task.Run(() => _store.ReadAll(root));
            var mine = all.Where(gift => gift.IsFor(profile.Id)).ToList();
            var collected = await _gifts.CollectedAsync(run.Id);

            Set([.. mine.Where(gift => !collected.Contains(gift.Id))],
                [.. mine.Where(gift => collected.Contains(gift.Id)).Take(6)]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo leyendo los regalos de la carpeta compartida");
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Collects one gift, and says what happened.</summary>
    public async Task<GiftResult> ClaimAsync(AdminGift gift)
    {
        if (_runContext.Current is not { } run)
        {
            return new GiftResult(false, "No hay ninguna run cargada.");
        }

        try
        {
            var result = await _gifts.ClaimAsync(run, gift);

            if (result.Collected)
            {
                _logger.LogInformation("Regalo {Id} de {From} recogido: {What}", gift.Id, gift.From, gift.Say());
            }

            await LookAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló recoger el regalo {Id}", gift.Id);
            return new GiftResult(false, "No se ha podido recoger.");
        }
    }

    private void Set(IReadOnlyList<AdminGift> pending, IReadOnlyList<AdminGift> collected)
    {
        Pending = pending;
        Collected = collected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
