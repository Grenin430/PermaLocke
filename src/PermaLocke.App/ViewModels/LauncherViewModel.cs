using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>A friend in the list, ready to draw.</summary>
public sealed record FriendItem(string Name, PresenceState State, string Detail, BitmapSource? Avatar)
{
    public bool HasAvatar => Avatar is not null;

    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";
}

/// <summary>One claimed achievement in the activity.</summary>
public sealed record ActivityItem(string PlayerName, PresenceState State, BitmapSource? Avatar,
    string Name, string Description, BitmapSource? Icon, int Points, string Time)
{
    public string Verb => " ha conseguido un logro";

    public bool HasAvatar => Avatar is not null;

    public bool HasIcon => Icon is not null;

    public bool HasDescription => Description.Length > 0;

    public string Initial => PlayerName.Length > 0 ? PlayerName[..1].ToUpperInvariant() : "?";

    public string PointsText => Points > 0 ? $"+{Points}" : Points.ToString();
}

/// <summary>A day of activity under its heading, the way a game library groups it.</summary>
public sealed record ActivityDay(string Heading, IReadOnlyList<ActivityItem> Items);

/// <summary>
/// JUGAR: the page the application opens on, laid out like a game library (§125, §126).
/// </summary>
/// <remarks>
/// The cover and the play bar on top; under them, on the left, the achievements everybody in the competition claims
/// and, on the right, the friends list and this player's achievements. The emulator is <see cref="EmulatorLauncher"/>
/// and the other players come from <see cref="CommunityService"/>; this only asks and shows.
/// </remarks>
public sealed partial class LauncherViewModel : SectionViewModel
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly IRunContext _runContext;
    private readonly IPlaytimeStore _playtime;
    private readonly IBoxReader _boxes;
    private readonly PokemonSpriteService _sprites;
    private readonly AchievementService _achievements;
    private readonly IRunRoles _roles;
    private readonly CommunityService _community;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<LauncherViewModel> _logger;
    private readonly IPokemonRepository _pokemon;
    private readonly PermaLocke.Rules.Services.ProgressService _progress;
    private readonly string _saves;
    private BitmapSource? _myAvatar;

    public LauncherViewModel(EmulatorLauncher launcher, CommunityService community, AlolaSky sky, IRunContext runContext,
        IPlaytimeStore playtime, IBoxReader boxes, PokemonSpriteService sprites, AchievementService achievements,
        IRunRoles roles, IUiDispatcher ui, ILogger<LauncherViewModel> logger, PermaLocke.Infrastructure.AppPaths paths,
        IPokemonRepository pokemon, PermaLocke.Rules.Services.ProgressService progress)
        : base("JUGAR")
    {
        _pokemon = pokemon;
        _progress = progress;
        _saves = paths.Saves;
        Launcher = launcher;
        ShowCommunity = !paths.LocalOnly;
        Sky = sky;
        _community = community;
        _runContext = runContext;
        _playtime = playtime;
        _boxes = boxes;
        _sprites = sprites;
        _achievements = achievements;
        _roles = roles;
        _ui = ui;
        _logger = logger;

        Launcher.PropertyChanged += OnLauncherChanged;
        Launcher.PlaytimeChanged += (_, _) => _ = _ui.InvokeAsync(LoadPlaytimeAsync);
        _community.PropertyChanged += (_, _) => _ = _ui.InvokeAsync(ShowCommunityAsync);
        _runContext.CurrentChanged += (_, _) => _ = _ui.InvokeAsync(SafeRefreshAsync);
    }

    public override string IconKey => "IconPlay";

    public EmulatorLauncher Launcher { get; }

    public bool ShowCommunity { get; }

    public AlolaSky Sky { get; }

    /// <summary>Raised with a section's title when one of the links under the play bar is pressed.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>The trainer's room on the cover (§169), or null with no run: then the room is empty.</summary>
    [ObservableProperty]
    private PermaLocke.App.Views.TrainerRoomState? _room;

    public ObservableCollection<FriendItem> Friends { get; } = [];

    public ObservableCollection<ActivityDay> Activity { get; } = [];

    public ObservableCollection<BitmapSource> ClaimedIcons { get; } = [];

    [ObservableProperty]
    private string _runLine = string.Empty;

    [ObservableProperty]
    private string _totalPlayed = "—";

    [ObservableProperty]
    private string _lastPlayed = "—";

    [ObservableProperty]
    private string _achievementsText = "—";

    [ObservableProperty]
    private double _achievementsShare;

    [ObservableProperty]
    private string _achievementsLine = string.Empty;

    [ObservableProperty]
    private string _moreClaimed = string.Empty;

    [ObservableProperty]
    private string _friendsHeading = "AMIGOS";

    [ObservableProperty]
    private string _communityNote = string.Empty;

    [ObservableProperty]
    private string _problem = string.Empty;

    /// <summary>What the play bar wants the player to know: something missing, or a world set aside for battles.</summary>
    [ObservableProperty]
    private string _notice = string.Empty;

    [ObservableProperty]
    private bool _noticeBlocks;

    public bool HasFriends => Friends.Count > 0;

    public bool HasActivity => Activity.Count > 0;

    public bool ShowPlay => Launcher.State is EmulatorState.Ready or EmulatorState.Unavailable;

    public bool ShowBusy => Launcher.State is EmulatorState.Starting or EmulatorState.Closing;

    public bool ShowClose => Launcher.State == EmulatorState.Running;

    public bool ShowForce => Launcher.State == EmulatorState.WontClose;

    public string BusyText => Launcher.State == EmulatorState.Starting ? "ABRIENDO…" : "CERRANDO…";

    /// <summary>The small button in every page's header: JUGAR, or the session clock while playing.</summary>
    public string HeaderText => Launcher.IsRunning
        ? Launcher.SessionClock.Length > 0 ? $"EN JUEGO · {Launcher.SessionClock}" : "EN JUEGO"
        : Launcher.State == EmulatorState.Starting ? "ABRIENDO…" : "JUGAR";

    /// <summary>ÚLTIMA SESIÓN, which while playing is the session running now.</summary>
    public string LastSessionText => Launcher.IsRunning && Launcher.SessionClock.Length > 0
        ? $"Ahora · {Launcher.SessionClock}"
        : LastPlayed;

    public bool IsRunning => Launcher.IsRunning;

    public override Task ActivateAsync() => SafeRefreshAsync();

    private void OnLauncherChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(LastSessionText));

        if (e.PropertyName == nameof(EmulatorLauncher.State))
        {
            OnPropertyChanged(nameof(ShowPlay));
            OnPropertyChanged(nameof(ShowBusy));
            OnPropertyChanged(nameof(ShowClose));
            OnPropertyChanged(nameof(ShowForce));
            OnPropertyChanged(nameof(BusyText));
            PlayCommand.NotifyCanExecuteChanged();

            // Una pregunta sobre un juego que ya no está abierto no tiene respuesta que valga.
            if (!Launcher.IsRunning)
            {
                Asking = string.Empty;
            }

            // Al cerrarse el juego la partida guardada puede haber cambiado: el equipo de la portada se lee otra vez.
            if (Launcher.State == EmulatorState.Ready)
            {
                _ = _ui.InvokeAsync(SafeRefreshAsync);
            }
        }

        if (e.PropertyName == nameof(EmulatorLauncher.Checks))
        {
            _ = _ui.InvokeAsync(() =>
            {
                ShowNotice();
                return Task.CompletedTask;
            });
        }
    }

    partial void OnLastPlayedChanged(string value) => OnPropertyChanged(nameof(LastSessionText));

    private async Task SafeRefreshAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se ha podido refrescar JUGAR");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Launcher.Refresh();
        ShowNotice();

        if (_runContext.Current is not { } run)
        {
            RunLine = "Sin run · créala en HOME";
            TotalPlayed = LastPlayed = AchievementsText = "—";
            AchievementsShare = 0;
            AchievementsLine = string.Empty;
            ClaimedIcons.Clear();
            MoreClaimed = string.Empty;
            Room = null;
            return;
        }

        RunLine = $"{run.Name} · {_roles.Of(run.Id)?.Name ?? run.RoleId} · {run.SeedLabel}";

        await _sprites.PrepareAsync();
        var progress = await LoadAchievementsAsync(run.Id);
        await LoadPlaytimeAsync();
        await LoadRoomAsync(run, progress);
        await ShowCommunityAsync();
    }

    private void ShowNotice()
    {
        var first = Launcher.Checks.FirstOrDefault(c => c.Level == CheckLevel.Blocking)
                    ?? Launcher.Checks.FirstOrDefault(c => c.Level == CheckLevel.Warning);

        Notice = first is null ? string.Empty : $"{first.Title}: {first.Detail}";
        NoticeBlocks = first?.Level == CheckLevel.Blocking;
    }

    /// <summary>
    /// The LOGROS panel: how many of the competition's achievements this run has claimed, and the pictures of the
    /// last ones. Claimed and not merely reached, like a game library counts what it has awarded.
    /// </summary>
    private async Task<IReadOnlyList<AchievementProgress>> LoadAchievementsAsync(Guid runId)
    {
        var progress = await _achievements.GetProgressAsync(runId);
        var claimed = progress.Where(p => p.Claimed).ToList();
        var total = Math.Max(1, progress.Count);

        AchievementsText = $"{claimed.Count}/{progress.Count}";
        AchievementsShare = claimed.Count / (double)total;
        AchievementsLine = $"Has desbloqueado {claimed.Count}/{progress.Count} ({(int)Math.Round(AchievementsShare * 100)} %)";

        ClaimedIcons.Clear();

        foreach (var item in claimed.Select(p => p.Achievement.Icon).OfType<int>().Take(5))
        {
            if (_sprites.GetItem(item) is { } icon)
            {
                ClaimedIcons.Add(icon);
            }
        }

        MoreClaimed = claimed.Count > ClaimedIcons.Count ? $"+{claimed.Count - ClaimedIcons.Count}" : string.Empty;
        return progress;
    }

    private async Task LoadPlaytimeAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        var sessions = await _playtime.LoadAsync(run.Id);
        TotalPlayed = Playtime.Say(Playtime.Total(sessions));
        LastPlayed = Playtime.Ago(Playtime.LastPlayed(sessions), DateTimeOffset.Now);
    }

    private async Task ShowCommunityAsync()
    {
        // La primera lectura de la carpeta llega antes que la primera de la pantalla: sin esto, avatares vacíos.
        await _sprites.PrepareAsync();

        Friends.Clear();

        foreach (var friend in _community.Friends)
        {
            Friends.Add(new FriendItem(friend.Name, friend.State, friend.Detail,
                friend.Avatar is { } species ? _sprites.Get(species) : null));
        }

        var playing = _community.Friends.Count(f => f.State == PresenceState.Playing);
        FriendsHeading = playing > 0 ? $"AMIGOS · {playing} JUGANDO" : "AMIGOS";

        Activity.Clear();

        foreach (var day in _community.Feed.GroupBy(f => f.At.LocalDateTime.Date))
        {
            Activity.Add(new ActivityDay(
                day.Key.ToString("d 'DE' MMMM", Spanish).ToUpper(Spanish),
                [.. day.Select(f => new ActivityItem(
                    f.PlayerName, f.State,
                    f.Avatar is { } species ? _sprites.Get(species) : f.IsMine ? _myAvatar : null,
                    f.Name, f.Description,
                    f.Icon is { } item ? _sprites.GetItem(item) : null,
                    f.Points, f.At.LocalDateTime.ToString("HH:mm")))]));
        }

        CommunityNote = _community.Note;
        OnPropertyChanged(nameof(HasFriends));
        OnPropertyChanged(nameof(HasActivity));
    }

    /// <summary>The achievement whose unlock is winning the league, for the trophy on the television.</summary>
    private const string ChampionAchievement = "alto-mando-campeon";

    /// <summary>The Totem Sticker achievements share this prefix and one counter; each has its own target.</summary>
    private const string StickersPrefix = "pegatinas-";

    /// <summary>
    /// The trainer's room on the cover (§169): every object in it read from the run, the save and the cartridge.
    /// Decoration — whatever cannot be read leaves its object empty, and the cover never fails for it.
    /// </summary>
    private async Task LoadRoomAsync(Run run, IReadOnlyList<AchievementProgress> progress)
    {
        try
        {
            BoxSnapshot? snapshot = null;

            try
            {
                snapshot = await _boxes.ReadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se ha podido leer la partida para el cuarto de la portada");
            }

            var party = snapshot?.Party?.Pokemon ?? [];

            // Tu avatar en la actividad cuando todavía no has publicado: el mismo que verían los demás.
            _myAvatar = party.FirstOrDefault(m => !m.IsEgg) is { } lead ? _sprites.Get(lead.Species, lead.Form) : null;

            var entries = await _pokemon.GetAllAsync(run.Id);
            var dead = entries.Where(p => p.Status == PokemonStatus.Dead).ToList();
            var deadPids = dead.Where(p => p.Pid is not null).Select(p => p.Pid!.Value).ToHashSet();

            // En la alfombra, los vivos del equipo; los caídos de la run van a la estantería aunque sigan en él.
            var team = party
                .Where(member => !member.IsEgg && !deadPids.Contains(member.Pid))
                .Select(member => Views.RoomSprite.From(_sprites.Get(member.Species, member.Form)))
                .OfType<Views.RoomSprite>()
                .ToList();

            var fallen = dead
                .OrderByDescending(p => p.DiedAt ?? p.ObtainedAt)
                .Select(p => Views.RoomSprite.From(_sprites.Get(p.Species, p.Form)))
                .OfType<Views.RoomSprite>()
                .Take(8)
                .ToList();

            // Las pruebas son los logros que se desbloquean con un cristal Z: sin nombres escritos aquí.
            var crystals = progress
                .Where(p => p.Achievement.Item is { } item && PermaLocke.Randomizer.Sprites.ZCrystalIndex.TryGet(item, out _))
                .Select(p => new Views.RoomCrystal(Views.RoomSprite.From(_sprites.GetItem(p.Achievement.Item!.Value)), p.Unlocked))
                .ToList();

            var stickers = progress
                .Where(p => p.Achievement.Id.StartsWith(StickersPrefix, StringComparison.Ordinal))
                .OrderBy(p => p.Achievement.Target)
                .ToList();

            var room = new Views.TrainerRoomState(
                Hour: Sky.Hour,
                Team: team,
                Fallen: fallen,
                FallenCount: dead.Count,
                Crystals: crystals,
                Champion: progress.Any(p => p.Achievement.Id == ChampionAchievement && p.Unlocked),
                LevelCap: (await _progress.CurrentStageAsync(run))?.Level ?? 0,
                Stickers: stickers.Select(p => p.Count).DefaultIfEmpty(0).Max(),
                StickersTarget: stickers.FirstOrDefault(p => !p.Unlocked)?.Achievement.Target
                                ?? stickers.LastOrDefault()?.Achievement.Target ?? 25,
                InPc: snapshot?.Stored ?? 0,
                Tv: LastKillcam(run.Id));

            Room = room;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido montar el cuarto de la portada");
        }
    }

    /// <summary>
    /// A frame of the run's most recent killcam, for the television: a moment before the fall, while the bar is still
    /// draining and the battle is recognisable. Null when there is none, and the television shows static.
    /// </summary>
    private Views.RoomSprite? LastKillcam(Guid runId)
    {
        var folder = Path.Combine(_saves, "killcam", runId.ToString("N"));

        if (!Directory.Exists(folder))
        {
            return null;
        }

        var newest = new DirectoryInfo(folder).GetFiles("*.killcam").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();

        if (newest is null)
        {
            return null;
        }

        var frame = KillcamClip.Read(newest.FullName).OrderBy(f => Math.Abs(f.Milliseconds + 900)).FirstOrDefault();
        return Views.RoomSprite.From(frame?.Image);
    }

    [RelayCommand]
    private void Go(string title) => NavigateRequested?.Invoke(title);

    private bool CanPlay() => Launcher.State == EmulatorState.Ready;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        Problem = Launcher.Launch() ?? string.Empty;
    }

    /// <summary>Which question the play bar is asking instead of showing the numbers: "", "close" or "force".</summary>
    /// <remarks>
    /// In the bar and not a Windows dialog. A grey system box in the middle of a pixel launcher read as a different
    /// program, and a question inside the page is also one the player can look away from without it blocking the
    /// whole window (§125).
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsking), nameof(AskText), nameof(AskYes))]
    private string _asking = string.Empty;

    public bool IsAsking => Asking.Length > 0;

    public string AskText => Asking == "force"
        ? "Azahar no se ha cerrado. Si lo fuerzas, se pierde lo que no hayas guardado en el juego."
        : "¿Cerrar Ultra Luna? Se pierde lo que no hayas guardado en el juego.";

    public string AskYes => Asking == "force" ? "SÍ, FORZAR" : "SÍ, CERRAR";

    [RelayCommand]
    private void Close() => Asking = "close";

    [RelayCommand]
    private void Force() => Asking = "force";

    [RelayCommand]
    private void Answer()
    {
        if (Asking == "force")
        {
            _logger.LogWarning("Cierre forzado confirmado desde JUGAR");
            Launcher.ForceClose();
        }
        else if (Asking == "close")
        {
            _logger.LogInformation("Cierre del juego confirmado desde JUGAR");
            Problem = string.Empty;
            Launcher.Close();
        }

        Asking = string.Empty;
    }

    [RelayCommand]
    private void Dismiss()
    {
        _logger.LogInformation("Cierre del juego cancelado en la confirmación");
        Asking = string.Empty;
    }

    [RelayCommand]
    private void ChooseEmulator()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elige azahar.exe",
            Filter = "Azahar|azahar.exe"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Problem = Launcher.UseEmulator(dialog.FileName) ?? string.Empty;
        ShowNotice();
    }
}
