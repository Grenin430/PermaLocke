using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.App.Services;

/// <param name="Name">What the friend is called.</param>
/// <param name="Detail">"Jugando a Ultra Luna · 12 min", "En la app", "Desconectado · hace 2 h".</param>
/// <param name="Avatar">The species leading their party when they last published, or null.</param>
public sealed record FriendStatus(Guid PlayerId, string Name, PresenceState State, string Detail, int? Avatar);

/// <param name="PlayerName">Who claimed it.</param>
/// <param name="State">Their presence now, which colours their name like a friends list does.</param>
/// <param name="Icon">The item whose picture stands for the achievement, or null.</param>
public sealed record ClaimedAchievement(string PlayerName, PresenceState State, int? Avatar, bool IsMine,
    string Name, string Description, int? Icon, int Points, DateTimeOffset At);

/// <summary>
/// The launcher's friends list and activity: everyone's presence and the achievements they claim, through the
/// shared folder (§126).
/// </summary>
/// <remarks>
/// <para>
/// <b>Presence.</b> This application writes its own <c>presencia.json</c> every <see cref="Presence.HeartbeatEvery"/>,
/// straight away when the game opens or closes, and «desconectado» when it closes. Others read it and judge it by its
/// age (<see cref="Presence.StateOf"/>). It is a synchronised folder and not a connection, so a change takes as long
/// as Drive takes to carry it — typically under a minute — and the screen never pretends otherwise.
/// </para>
/// <para>
/// <b>Publishing on its own.</b> The activity is read from each player's published history, and a history that is
/// only published when somebody remembers to press a button is not an activity feed. So the run is published again
/// whenever its history has grown, at most every <see cref="PublishEvery"/>: the same publish as the button, with the
/// same read-back and the same check.
/// </para>
/// <para>
/// Histories are read only when their file changed since the last read. They are the one large file in the folder
/// and the list is refreshed every few seconds.
/// </para>
/// </remarks>
public sealed class CommunityService : INotifyPropertyChanged
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PublishEvery = TimeSpan.FromSeconds(90);
    private const int FeedLength = 30;

    private readonly SyncService _sync;
    private readonly SnapshotStore _store;
    private readonly PlayerProfileService _profiles;
    private readonly EmulatorLauncher _launcher;
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IAchievementCatalog _catalog;
    private readonly ILogger<CommunityService> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private readonly Dictionary<string, (DateTime Written, RunHistory? History)> _histories = [];

    private bool _writes;
    private bool _busy;
    private PresenceState? _lastWritten;
    private DateTimeOffset _lastBeat = DateTimeOffset.MinValue;
    private DateTimeOffset _lastPublish = DateTimeOffset.MinValue;
    private int _publishedCount = -1;

    public CommunityService(SyncService sync, SnapshotStore store, PlayerProfileService profiles,
        EmulatorLauncher launcher, IRunContext runContext, IEventStore events, IAchievementCatalog catalog,
        ILogger<CommunityService> logger)
    {
        _sync = sync;
        _store = store;
        _profiles = profiles;
        _launcher = launcher;
        _runContext = runContext;
        _events = events;
        _catalog = catalog;
        _logger = logger;

        _timer.Tick += (_, _) => _ = TickAsync();

        // Abrir o cerrar el juego se dice en el momento, sin esperar al siguiente latido.
        _launcher.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EmulatorLauncher.IsRunning))
            {
                _ = TickAsync();
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<FriendStatus> Friends { get; private set; } = [];

    public IReadOnlyList<ClaimedAchievement> Feed { get; private set; } = [];

    /// <summary>Why there is nothing to show, or empty.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <param name="writes">
    /// False for a copy opened only to look at screens (<c>--sin-juego</c>): it reads, but it does not tell the others
    /// it is here and it does not publish the run the real application is publishing.
    /// </param>
    public void Start(bool writes)
    {
        _writes = writes;
        _timer.Start();
        _ = TickAsync();
    }

    private PresenceState Mine => _launcher.IsRunning ? PresenceState.Playing : PresenceState.InApp;

    private async Task TickAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var root = _sync.SharedFolder;

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                Set([], [], "Elige la carpeta compartida en COMPETICIÓN.");
                return;
            }

            var profile = await _profiles.CurrentAsync();

            if (_writes && profile is not null)
            {
                await BeatAsync(root, profile);
                await PublishIfGrownAsync();
            }

            await ReadAsync(root, profile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo leyendo la carpeta compartida para amigos y actividad");
        }
        finally
        {
            _busy = false;
        }
    }

    private Task BeatAsync(string root, PlayerProfile profile)
    {
        var now = DateTimeOffset.Now;
        var state = Mine;

        if (state == _lastWritten && now - _lastBeat < Presence.HeartbeatEvery)
        {
            return Task.CompletedTask;
        }

        _store.WritePresence(root, profile, new PlayerPresence
        {
            PlayerId = profile.Id,
            Name = profile.Name,
            State = state,
            UpdatedAt = now,
            PlayingSince = state == PresenceState.Playing ? _launcher.SessionStart : null
        });

        _lastWritten = state;
        _lastBeat = now;
        return Task.CompletedTask;
    }

    private async Task PublishIfGrownAsync()
    {
        if (_runContext.Current is not { } run || DateTimeOffset.Now - _lastPublish < PublishEvery)
        {
            return;
        }

        var count = (await _events.GetAllAsync(run.Id)).Count;

        if (count == _publishedCount)
        {
            return;
        }

        _lastPublish = DateTimeOffset.Now;
        var result = await _sync.PublishAsync();
        _publishedCount = count;
        _logger.LogInformation("Publicación automática: {Result}", result);
    }

    /// <summary>
    /// Says «desconectado» on the way out, so friends see it now and not three minutes later.
    /// </summary>
    public void SignOff()
    {
        if (!_writes)
        {
            return;
        }

        try
        {
            var root = _sync.SharedFolder;
            var profile = _profiles.CurrentAsync().GetAwaiter().GetResult();

            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && profile is not null)
            {
                _store.WritePresence(root, profile, new PlayerPresence
                {
                    PlayerId = profile.Id,
                    Name = profile.Name,
                    State = PresenceState.Offline,
                    UpdatedAt = DateTimeOffset.Now
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido escribir la desconexión");
        }
    }

    private async Task ReadAsync(string root, PlayerProfile? me)
    {
        var now = DateTimeOffset.Now;
        var players = await Task.Run(() => _store.ReadPlayers(root));
        var byId = _catalog.All.ToDictionary(a => a.Id, StringComparer.Ordinal);

        var friends = players
            .Where(p => me is null || p.Profile.Id != me.Id)
            .Select(p => new FriendStatus(p.Profile.Id, p.Profile.Name, Presence.StateOf(p.Presence, now),
                Presence.Say(p.Presence, now), p.Snapshot?.AvatarSpecies))
            .OrderByDescending(f => f.State)
            .ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var feed = new List<ClaimedAchievement>();

        void Add(IEnumerable<GameEvent> events, string name, PresenceState state, int? avatar, bool isMine)
        {
            foreach (var e in events.Where(e => e.Type == GameEventType.AchievementUnlocked && e.Data.ContainsKey("logro")))
            {
                byId.TryGetValue(e.Data["logro"], out var achievement);

                feed.Add(new ClaimedAchievement(name, state, avatar, isMine,
                    achievement?.Name ?? e.Data.GetValueOrDefault("nombre") ?? e.Data["logro"],
                    achievement?.Description ?? string.Empty,
                    achievement?.Icon,
                    int.TryParse(e.Data.GetValueOrDefault("puntos"), out var points) ? points : e.PointsDelta,
                    e.Timestamp));
            }
        }

        foreach (var player in players.Where(p => me is null || p.Profile.Id != me.Id))
        {
            Add((await HistoryAsync(player.HistoryPath))?.Events ?? [], player.Profile.Name,
                Presence.StateOf(player.Presence, now), player.Snapshot?.AvatarSpecies, isMine: false);
        }

        // Lo propio sale de la base de datos de esta máquina, que va por delante de lo publicado y está aunque todavía
        // no se haya publicado nada.
        if (me is not null && _runContext.Current is { } run)
        {
            var avatar = players.FirstOrDefault(p => p.Profile.Id == me.Id)?.Snapshot?.AvatarSpecies;
            Add(await _events.GetAllAsync(run.Id), me.Name, Mine, avatar, isMine: true);
        }

        Set(friends, [.. feed.OrderByDescending(f => f.At).Take(FeedLength)],
            players.Count <= (me is null ? 0 : 1) ? "Todavía no hay nadie más." : string.Empty);
    }

    private async Task<RunHistory?> HistoryAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var written = File.GetLastWriteTimeUtc(path);

        if (_histories.TryGetValue(path, out var cached) && cached.Written == written)
        {
            return cached.History;
        }

        var history = await Task.Run(() => _store.ReadHistory(path));
        _histories[path] = (written, history);
        return history;
    }

    private void Set(IReadOnlyList<FriendStatus> friends, IReadOnlyList<ClaimedAchievement> feed, string note)
    {
        Friends = friends;
        Feed = feed;
        Note = note;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
