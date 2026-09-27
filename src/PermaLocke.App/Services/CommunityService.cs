using System.Net.Http;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <param name="Name">What the friend is called.</param>
/// <param name="Detail">"Jugando a Ultra Luna · 12 min", "En la app", "Desconectado · hace 2 h".</param>
/// <param name="Avatar">The species leading their party when they last published, or null.</param>
public sealed record FriendStatus(Guid PlayerId, string Name, PresenceState State, string Detail, int? Avatar,
    string? Photo = null);

/// <param name="PlayerName">Who claimed it.</param>
/// <param name="State">Their presence now, which colours their name like a friends list does.</param>
/// <param name="Icon">The item whose picture stands for the achievement, or null.</param>
public sealed record ClaimedAchievement(string PlayerName, PresenceState State, int? Avatar, bool IsMine,
    string Name, string Description, int? Icon, int Points, DateTimeOffset At, string? Photo = null);

/// <summary>
/// The launcher's friends list and activity: everyone's presence and the achievements they claim, through the
/// tournament server (the shared folder of §126 is gone).
/// </summary>
/// <remarks>
/// <para>
/// <b>Presence.</b> This application writes its row in <c>presencia</c> every <see cref="Presence.HeartbeatEvery"/>,
/// straight away when the game opens or closes, and «desconectado» when it closes. The server stamps the time, and the
/// <c>amigos</c> view says how old each row is by the server's clock, so a PC with a wrong clock cannot look online.
/// </para>
/// <para>
/// <b>Activity.</b> The achievements come from the histories <see cref="TournamentUpload"/> already sends (the
/// <c>logros</c> view reads them out of <c>runs</c>), so nothing extra is uploaded. One's own come from this PC's
/// database, which is ahead of what has been sent. Who may read or write is decided on the server by the whitelist
/// (<c>tools/supabase/03-amigos.sql</c>).
/// </para>
/// </remarks>
public sealed class CommunityService : INotifyPropertyChanged
{
    // Cada minuto: con 20 jugadores, cada 15 s acercaba el tráfico al límite del plan gratuito. Abrir o cerrar el
    // juego se sigue diciendo en el momento.
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(60);
    private const int FeedLength = 30;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly DiscordLogin _discord;
    private readonly EmulatorLauncher _launcher;
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IAchievementCatalog _catalog;
    private readonly Notifier _notifier;
    private readonly ILogger<CommunityService> _logger;
    private long _lastAnnouncement = -1;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };

    private bool _writes;
    private bool _busy;
    private PresenceState? _lastWritten;
    private DateTimeOffset _lastBeat = DateTimeOffset.MinValue;

    public CommunityService(DiscordLogin discord, EmulatorLauncher launcher, IRunContext runContext, IEventStore events,
        IAchievementCatalog catalog, Notifier notifier, ILogger<CommunityService> logger)
    {
        _discord = discord;
        _launcher = launcher;
        _runContext = runContext;
        _events = events;
        _catalog = catalog;
        _notifier = notifier;
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

    /// <summary>The organiser's latest announcement to every player, or empty (10-control.sql).</summary>
    public string Announcement { get; private set; } = string.Empty;

    /// <summary>
    /// The organizer restarted this run from Admin: on the server it is archived, and here it still is until the player
    /// starts over (HOME → EMPEZAR DE CERO). Nothing is deleted from here on its own: the save and the run are the
    /// player's, and wiping them is their call.
    /// </summary>
    public bool RunRestarted { get; private set; }

    /// <summary>The run whose restart was already told, so the notice comes up once.</summary>
    private Guid? _restartTold;

    /// <param name="writes">
    /// False for a copy opened only to look at screens (<c>--sin-juego</c>): it reads, but it does not tell the others
    /// it is here.
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
            if (_writes)
            {
                await BeatAsync();
            }

            await ReadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo leyendo amigos y actividad del servidor del torneo");
            Set(Friends, Feed, "Sin conexión con el servidor del torneo.");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task BeatAsync()
    {
        var now = DateTimeOffset.Now;
        var state = Mine;

        if (state == _lastWritten && now - _lastBeat < Presence.HeartbeatEvery)
        {
            return;
        }

        if (await WriteAsync(state, state == PresenceState.Playing ? _launcher.SessionStart : null))
        {
            _lastWritten = state;
            _lastBeat = now;
        }
    }

    private Task<bool> WriteAsync(PresenceState state, DateTimeOffset? playingSince) =>
        _discord.PostAsync("presencia?on_conflict=user_id", JsonSerializer.Serialize(new
        {
            nombre = _discord.Saved?.Name ?? "Jugador",
            avatar_url = _discord.Saved?.AvatarUrl,
            estado = (int)state,
            jugando_desde = playingSince
        }), "resolution=merge-duplicates");

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
            Task.Run(() => WriteAsync(PresenceState.Offline, null)).Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido escribir la desconexión");
        }
    }

    private sealed record FriendRow(Guid User_id, string Nombre, int Estado, int Segundos, int? Jugando_segundos,
        int? Avatar, bool Es_mio, string? Avatar_url);

    private sealed record AchievementRow(Guid User_id, string? Jugador, string? Logro, string? Nombre, int Puntos,
        DateTimeOffset Cuando);

    private async Task ReadAsync()
    {
        var now = DateTimeOffset.Now;
        await ReadAnnouncementAsync();
        await ReadRunStateAsync();
        var friendsJson = await _discord.GetAsync("amigos?select=*");
        var feedJson = await ReadFeedAsync();

        if (friendsJson is null || feedJson is null)
        {
            Set([], [], "Entra con Discord para ver a los demás.");
            return;
        }

        var rows = JsonSerializer.Deserialize<List<FriendRow>>(friendsJson, Json) ?? [];
        var presence = rows.ToDictionary(r => r.User_id, r => new PlayerPresence
        {
            PlayerId = r.User_id,
            Name = r.Nombre,
            State = (PresenceState)r.Estado,
            UpdatedAt = now.AddSeconds(-r.Segundos),
            PlayingSince = r.Jugando_segundos is { } playing ? now.AddSeconds(-playing) : null
        });
        var me = rows.FirstOrDefault(r => r.Es_mio);

        var friends = rows
            .Where(r => !r.Es_mio)
            .Select(r => new FriendStatus(r.User_id, r.Nombre, Presence.StateOf(presence[r.User_id], now),
                Presence.Say(presence[r.User_id], now), r.Avatar, r.Avatar_url))
            .OrderByDescending(f => f.State)
            .ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var byId = _catalog.All.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var feed = new List<ClaimedAchievement>();

        foreach (var row in JsonSerializer.Deserialize<List<AchievementRow>>(feedJson, Json) ?? [])
        {
            if (row.User_id == me?.User_id || row.Logro is null)
            {
                continue;
            }

            presence.TryGetValue(row.User_id, out var theirs);
            byId.TryGetValue(row.Logro, out var achievement);

            feed.Add(new ClaimedAchievement(row.Jugador ?? "Jugador", Presence.StateOf(theirs, now),
                rows.FirstOrDefault(r => r.User_id == row.User_id)?.Avatar, false,
                achievement?.Name ?? row.Nombre ?? row.Logro, achievement?.Description ?? string.Empty,
                achievement?.Icon, row.Puntos, row.Cuando, rows.FirstOrDefault(r => r.User_id == row.User_id)?.Avatar_url));
        }

        // Lo propio sale de la base de datos de esta máquina, que va por delante de lo subido.
        if (_runContext.Current is { } run)
        {
            foreach (var e in (await _events.GetAllAsync(run.Id))
                         .Where(e => e.Type == GameEventType.AchievementUnlocked && e.Data.ContainsKey("logro")))
            {
                byId.TryGetValue(e.Data["logro"], out var achievement);

                feed.Add(new ClaimedAchievement(_discord.Saved?.Name ?? "Tú", Mine, me?.Avatar, true,
                    achievement?.Name ?? e.Data.GetValueOrDefault("nombre") ?? e.Data["logro"],
                    achievement?.Description ?? string.Empty,
                    achievement?.Icon,
                    int.TryParse(e.Data.GetValueOrDefault("puntos"), out var points) ? points : e.PointsDelta,
                    e.Timestamp, _discord.Saved?.AvatarUrl));
            }
        }

        Set(friends, [.. feed.OrderByDescending(f => f.At).Take(FeedLength)],
            friends.Count == 0 ? "Todavía no hay nadie más." : string.Empty);
    }

    /// <summary>The server has no <c>logros_todos</c> (SQL 15 not run yet): the old view.</summary>
    private bool _oldFeed;

    /// <summary>
    /// The achievements of everybody's run, from <c>logros_todos</c>, which also reads the runs uploaded event by event
    /// (§194); <c>logros</c> only reads the whole histories of before.
    /// </summary>
    private async Task<string?> ReadFeedAsync()
    {
        if (!_oldFeed)
        {
            try
            {
                return await _discord.GetAsync("logros_todos?select=*");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _oldFeed = true;
            }
        }

        return await _discord.GetAsync("logros?select=*");
    }

    private sealed record AnnouncementRow(long Id, string Texto);

    private sealed record RunStateRow(bool Activa);

    /// <summary>
    /// Whether this run is still the active one on the server. A run never uploaded is not restarted: there is no row.
    /// </summary>
    private async Task ReadRunStateAsync()
    {
        if (_runContext.Current is not { } run
            || await _discord.GetAsync($"runs?run_id=eq.{run.Id}&select=activa") is not { } json)
        {
            RunRestarted = false;
            return;
        }

        RunRestarted = (JsonSerializer.Deserialize<List<RunStateRow>>(json, Json) ?? []).FirstOrDefault() is { Activa: false };

        if (RunRestarted && _restartTold != run.Id)
        {
            _restartTold = run.Id;
            _logger.LogWarning("El organizador ha reiniciado la run {Run} en el servidor", run.Name);
            _notifier.Say(ToastKind.Announcement, "Tu run se ha reiniciado",
                "El organizador la ha reiniciado. Ve a HOME y pulsa EMPEZAR DE CERO.");
        }
    }

    /// <summary>
    /// The newest announcement, shown in JUGAR. A new one also comes up as a notice over the game, once: the first
    /// read after starting only remembers which one is current, so reopening PermaLocke does not repeat it.
    /// </summary>
    private async Task ReadAnnouncementAsync()
    {
        if (await _discord.GetAsync("anuncios?select=id,texto&order=creado.desc&limit=1") is not { } json)
        {
            return;
        }

        var latest = (JsonSerializer.Deserialize<List<AnnouncementRow>>(json, Json) ?? []).FirstOrDefault();
        Announcement = latest?.Texto ?? string.Empty;

        if (latest is not null && _lastAnnouncement >= 0 && latest.Id != _lastAnnouncement)
        {
            _notifier.Say(ToastKind.Announcement, "Anuncio", latest.Texto);
        }

        _lastAnnouncement = latest?.Id ?? 0;
    }

    /// <summary>How each friend was last time, to see who has just started playing; null before the first read.</summary>
    private Dictionary<Guid, PresenceState>? _lastStates;

    /// <summary>
    /// «X está jugando a PermaLocke», like a friend starting a game on Steam (1.0.4.5): small, only over the emulator, and
    /// only for a friend who was not playing at the last read. Nothing at the first read, or everybody already playing
    /// would pop up at once.
    /// </summary>
    private void TellWhoStartedPlaying(IReadOnlyList<FriendStatus> friends)
    {
        if (_lastStates is not null && _launcher.IsRunning)
        {
            foreach (var friend in friends.Where(f => f.State == PresenceState.Playing
                                                      && _lastStates.GetValueOrDefault(f.PlayerId) != PresenceState.Playing))
            {
                _notifier.Say(ToastKind.FriendPlaying, friend.Name, "está jugando a PermaLocke", Photo(friend.Photo));
            }
        }

        _lastStates = friends.ToDictionary(f => f.PlayerId, f => f.State);
    }

    private static System.Windows.Media.Imaging.BitmapSource? Photo(string? url)
    {
        try
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new System.Windows.Media.Imaging.BitmapImage(uri) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Set(IReadOnlyList<FriendStatus> friends, IReadOnlyList<ClaimedAchievement> feed, string note)
    {
        TellWhoStartedPlaying(friends);
        Friends = friends;
        Feed = feed;
        Note = note;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
