using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.ViewModels;

/// <summary>A banner of the gacha with how many free rolls this gift gives on it.</summary>
public sealed partial class BannerRow(string id, string name) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    private int _rolls;
}

/// <summary>Which players the list shows.</summary>
public enum PlayerFilter
{
    Todos,
    Conectados,
    ARevisar,
    Suspendidos,
    SinRun
}

/// <summary>One line of INICIO's «requiere atención»: a count read from the server, what it means and where to act.</summary>
/// <param name="Section">The menu page that deals with it.</param>
/// <param name="Filter">For JUGADORES, the filter to open it with.</param>
public sealed record HomeAlert(int Count, string Text, string Detail, string Section, bool Severe, PlayerFilter? Filter = null);

/// <summary>How Admin is running: against the tournament, or against made-up players to look at it (<c>--demo</c>).</summary>
public sealed record AdminMode(bool Demo);

/// <summary>
/// Admin's main view model: the menu, the players, the gift being written and INICIO (§129, control centre 2026-10-08).
/// </summary>
/// <remarks>
/// Everything it does goes through the tournament server (<see cref="GiftDesk"/>). It cannot touch anybody's run: points,
/// rolls and orders are applied by the player's own application, with their event. Every change asks through
/// <see cref="Confirm"/> and, with 21-admin.sql, the server writes it in REGISTRO with who did it.
/// </remarks>
public sealed partial class AdminViewModel : ObservableObject
{
    private readonly GiftDesk _desk;
    private readonly ITournamentServer _discord;
    private readonly ILogger<AdminViewModel> _logger;

    public AuditViewModel Audit { get; }

    public WhitelistViewModel Whitelist { get; }

    public AnnouncementsViewModel Announcements { get; }

    public UsageViewModel Usage { get; }

    /// <summary>The server cleanup (§194).</summary>
    public CleanupViewModel Cleanup { get; }

    public ReportsViewModel Reports { get; }

    public SuggestionsViewModel Suggestions { get; }

    public MotesViewModel Motes { get; }

    public FallenViewModel Fallen { get; }

    /// <summary>The official rules (2026-09-26).</summary>
    public RulesViewModel Rules { get; }

    /// <summary>What the organisers changed (2026-10-08).</summary>
    public LogViewModel Log { get; }

    /// <summary>Made-up players (<c>--demo</c>): the window says so on every page.</summary>
    public bool Demo { get; }

    private readonly IReadOnlyList<Pick> _species;
    private readonly IReadOnlyList<Pick> _items;

    public AdminViewModel(GiftDesk desk, ITournamentServer discord, IGachaCatalog gacha, ISpeciesStatsCatalog species, IShopCatalog shop,
        AuditViewModel audit,
        WhitelistViewModel whitelist, AnnouncementsViewModel announcements, UsageViewModel usage, RulesViewModel rules,
        CleanupViewModel cleanup, ReportsViewModel reports, SuggestionsViewModel suggestions,
        MotesViewModel motes, FallenViewModel fallen, LogViewModel log, AdminMode mode, ILogger<AdminViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(gacha);
        ArgumentNullException.ThrowIfNull(mode);

        _desk = desk;
        Audit = audit;
        Whitelist = whitelist;
        Announcements = announcements;
        Usage = usage;
        Rules = rules;
        Cleanup = cleanup;
        Reports = reports;
        Suggestions = suggestions;
        Motes = motes;
        Fallen = fallen;
        Log = log;
        Demo = mode.Demo;
        _discord = discord;
        _logger = logger;
        _species = [.. species.All.Where(s => s.Id > 0).Select(s => new Pick(s.Id, s.Name))];
        _items = [.. shop.Items.GroupBy(i => i.Id).Select(g => new Pick(g.Key, g.First().Name)).OrderBy(i => i.Name)];

        foreach (var banner in gacha.Banners)
        {
            Banners.Add(new BannerRow(banner.Id, banner.Name));
        }

        Sections =
        [
            new("", "INICIO", "IconHome", new HomeSection(this), "Lo que pide atención ahora y lo último que se ha cambiado."),
            new("JUGADORES", "JUGADORES", "IconPeople", new PlayersSection(this), "Buscar, filtrar y abrir la ficha de cada jugador; acciones a varios a la vez."),
            new("JUGADORES", "REGALOS", "IconGift", new GiftsSection(this), "Mandar regalos (puntos, tiradas, wonder trades) o ajustar puntos a los que marques."),
            new("JUGADORES", "CEMENTERIO", "IconGrave", Fallen, "Todos los caídos del torneo."),
            new("TORNEO", "CONTROL", "IconSwords", new TournamentSection(this), "Pausar o reanudar el torneo y abrir o cerrar la TIENDA."),
            new("TORNEO", "REGLAS", "IconGrid", Rules, "Las reglas oficiales que bajan todas las apps."),
            new("TORNEO", "ANUNCIOS", "IconWarning", Announcements, "Un mensaje que ven todos en JUGAR."),
            new("VIGILANCIA", "AUDITORÍA", "IconCheck", Audit, "Cada run comprobada y los avisos de trampas."),
            new("VIGILANCIA", "INFORMES", "IconTools", Reports, "Los cierres de Azahar que mandan las apps."),
            new("VIGILANCIA", "REGISTRO", "IconDocument", Log, "Quién ha cambiado qué, cuándo y a quién: lo apunta el servidor."),
            new("COMUNIDAD", "BUZÓN", "IconMailbox", Suggestions, "Lo que escriben los jugadores en su buzón."),
            new("COMUNIDAD", "MOTES", "IconStar", Motes, "Las votaciones de motes, y anularlas."),
            new("SERVIDOR", "LISTA", "IconTick", Whitelist, "Quién puede entrar al torneo."),
            new("SERVIDOR", "CONSUMO", "IconChart", Usage, "Cuánto del plan gratis se usa."),
            new("SERVIDOR", "LIMPIEZA", "IconRefresh", Cleanup, "Borrar del servidor lo que ya no hace falta.")
        ];
        _selectedSection = Sections[0];
    }

    // ============================================================ QUIÉN HAY

    [ObservableProperty]
    private string _adminName = "Admin";

    /// <summary>Everybody the server knows, as read; the list on screen is <see cref="VisiblePlayers"/>.</summary>
    public ObservableCollection<PlayerLine> Players { get; } = [];

    /// <summary>Those who can be sent something: they have signed in at least once.</summary>
    public ObservableCollection<PlayerLine> Recipients { get; } = [];

    public ObservableCollection<SentGift> Sent { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>When everything was last read, for the header («hace 2 min»).</summary>
    [ObservableProperty]
    private DateTimeOffset? _lastRefresh;

    public string LastRefreshText => LastRefresh is null ? "sin leer" : $"leído {PlayerLine.Ago(LastRefresh)}";

    partial void OnLastRefreshChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastRefreshText));

    /// <summary>A strip over every page when something about the server or the mode must be known; empty hides it.</summary>
    public string Notice => Demo
        ? "MODO DEMO · jugadores inventados para ver Admin. Nada se lee ni se manda al servidor del torneo."
        : !_desk.ControlCentre
            ? "Falta ejecutar tools/supabase/21-admin.sql en Supabase: sin REGISTRO, notas ni envíos de una vez. La lista sale de la clasificación (solo jugadores con run)."
            : string.Empty;

    public bool HasNotice => Notice.Length > 0;

    private void Say(string text, bool error = false)
    {
        Status = text;
        StatusIsError = error;
    }

    public async Task StartAsync()
    {
        ShowAccount();
        await RefreshAsync();

        // Quién está conectado cambia solo: se relee cada minuto.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (_, _) => _ = RefreshAsync();
        timer.Start();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // Todo se lee antes de tocar las listas: si algo falla a medias, la pantalla sigue con la lectura anterior entera.
            var players = await _desk.PlayersAsync();
            // Solo pueden recoger quienes están en la lista, sin suspender y con run: los demás no cuentan como «sin recoger».
            var sent = await _desk.SentAsync([.. players.Where(p => p.HasAccount && p.InList && !p.Suspended && p.HasRun)]);

            Players.Clear();
            Recipients.Clear();
            foreach (var player in players)
            {
                Players.Add(player);
                if (player.HasAccount) Recipients.Add(player);
            }

            Sent.Clear();
            foreach (var gift in sent)
            {
                Sent.Add(gift);
            }

            ApplyFilter();
            LastRefresh = DateTimeOffset.Now;
            OnPropertyChanged(nameof(Notice));
            OnPropertyChanged(nameof(HasNotice));
            Say(Players.Count == 0 ? "Todavía no hay nadie en la lista ni con run." : $"{Players.Count} jugador(es).");

            await ShowTodayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la lectura del servidor del torneo");
            Say(IsSignedIn ? $"No se ha podido leer el servidor: {ex.Message}" : "No se ha podido leer el servidor. Pulsa ENTRAR CON DISCORD.", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ============================================================ INICIO

    /// <summary>What needs the organiser today, in one line at the bottom (2026-09-28, «Admin a prueba de niños»).</summary>
    [ObservableProperty]
    private string _today = "Pulsa ACTUALIZAR para ver lo de hoy.";

    public ObservableCollection<HomeAlert> Alerts { get; } = [];

    public bool AllClear => Alerts.Count == 0;

    /// <summary>The figures of INICIO.</summary>
    public int Online => Players.Count(p => p.State != PresenceState.Offline);

    public int PlayingNow => Players.Count(p => p.State == PresenceState.Playing);

    public int InTournament => Players.Count(p => p.InList);

    public int PendingGifts => Sent.Count(s => s.Waiting.Count > 0);

    [ObservableProperty]
    private int _mailbox;

    [ObservableProperty]
    private int _crashReports;

    /// <summary>Which version each player's app says, «1.0.16 · 4», for INICIO; only from 1.0.16 on.</summary>
    public string Versions => string.Join("   ", Players.Where(p => p.Version is not null).GroupBy(p => p.Version!)
        .OrderByDescending(g => Version.TryParse(g.Key, out var v) ? v : new Version()).Select(g => $"{g.Key} · {g.Count()}"));

    private async Task ShowTodayAsync()
    {
        Mailbox = await CountAsync("sugerencias?select=id");
        CrashReports = await CountCallAsync("informes_lista");
        if (_desk.ControlCentre) await Log.LoadLatestAsync();

        BuildAlerts();
        Today = $"HOY: {Online} de {Players.Count} conectados · {PendingGifts} envío(s) sin recoger del todo · "
                + (Mailbox == 0 ? "buzón vacío" : $"{Mailbox} sugerencia(s) en el BUZÓN");

        foreach (var name in new[] { nameof(Online), nameof(PlayingNow), nameof(InTournament), nameof(PendingGifts), nameof(Versions), nameof(AllClear) })
        {
            OnPropertyChanged(name);
        }
    }

    private async Task<int> CountAsync(string path)
    {
        try
        {
            var json = await _discord.GetAsync(path);
            return json is null ? 0 : System.Text.Json.JsonDocument.Parse(json).RootElement.GetArrayLength();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo contar {Path}", path);
            return 0;
        }
    }

    private async Task<int> CountCallAsync(string function)
    {
        try
        {
            var json = await _discord.CallAsync(function, "{}");
            return json is null ? 0 : System.Text.Json.JsonDocument.Parse(json).RootElement.GetArrayLength();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo contar {Function}", function);
            return 0;
        }
    }

    /// <summary>
    /// INICIO's list, only from what was just read: each line says where it comes from in its detail. Nothing is shown
    /// when its count is zero, so an empty list means there is nothing to do.
    /// </summary>
    private void BuildAlerts()
    {
        Alerts.Clear();
        var now = DateTimeOffset.Now;

        void Add(int count, string text, string detail, string section, bool severe, PlayerFilter? filter = null)
        {
            if (count > 0) Alerts.Add(new HomeAlert(count, text, detail, section, severe, filter));
        }

        Add(Players.Count(p => p.Rewinds > 0), "runs con retrocesos",
            "Su historial bajó o se reescribió entre dos subidas: una partida restaurada o tocada. Míralo en su ficha y en AUDITORÍA.", "JUGADORES", true, PlayerFilter.ARevisar);
        Add(Players.Count(p => !p.InList), "con run fuera de la lista",
            "Ya no pueden entrar, pero su run sigue en la clasificación. Reiníciala o vuelve a darles de alta.", "JUGADORES", true, PlayerFilter.ARevisar);
        Add(Sent.Count(s => s.Waiting.Count > 0 && now - s.Gift.CreatedAt > TimeSpan.FromMinutes(30)), "envíos sin aplicar desde hace más de 30 min",
            "Su app no lo ha cerrado: no la han abierto, o no puede aplicarlo (un objeto necesita el juego abierto).", "REGALOS", false);
        Add(CrashReports, "informes de cierre de Azahar", "Los mandan las apps al cerrarse el emulador de golpe.", "INFORMES", false);
        Add(Mailbox, "sugerencias en el buzón", "Lo que han escrito los jugadores.", "BUZÓN", false);
        Add(Players.Count(p => p.Suspended), "cuentas suspendidas", "Siguen en la lista pero no entran ni suben nada.", "JUGADORES", false, PlayerFilter.Suspendidos);
        Add(Players.Count(p => p.InList && p.HasRun && p.LastUpload is { } u && now - u > TimeSpan.FromDays(2)), "no suben su run desde hace más de 2 días",
            "Puede que hayan dejado de jugar, o que su app no llegue al servidor.", "JUGADORES", false, PlayerFilter.Todos);
        Add(Players.Count(p => p.InList && !p.HasAccount), "de la lista no han entrado nunca",
            "Su Discord está dado de alta pero aún no han abierto PermaLocke con él.", "JUGADORES", false, PlayerFilter.SinRun);

        var newest = Players.Select(p => p.Version).Where(v => v is not null).Select(v => Version.TryParse(v, out var parsed) ? parsed : null)
            .Where(v => v is not null).DefaultIfEmpty(null).Max();
        if (newest is not null)
        {
            Add(Players.Count(p => p.Version is not null && Version.TryParse(p.Version, out var v) && v < newest), $"con una versión anterior a la {newest}",
                "Se actualizan solos al abrir PermaLocke; si siguen así, no la han abierto.", "JUGADORES", false, PlayerFilter.Todos);
        }

        OnPropertyChanged(nameof(AllClear));

        Section("JUGADORES").Badge = Players.Count(p => p.NeedsLook);
        Section("REGALOS").Badge = PendingGifts;
        Section("INFORMES").Badge = CrashReports;
        Section("BUZÓN").Badge = Mailbox;
    }

    private AdminSection Section(string title) => Sections.First(s => s.Title == title);

    [RelayCommand]
    private void OpenAlert(HomeAlert? alert)
    {
        if (alert is null) return;
        if (alert.Filter is { } filter) Filter = filter;
        Go(alert.Section);
    }

    /// <summary>Who is signed in, for the header: their Discord name and picture, or nobody.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(IsSignedOut))]
    private string? _photo;

    public bool IsSignedIn => _discord.Saved is not null;

    public bool IsSignedOut => !IsSignedIn;

    private void ShowAccount()
    {
        AdminName = _discord.Saved?.Name ?? "Organizador";
        Photo = _discord.Saved?.AvatarUrl;
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(IsSignedOut));
    }

    /// <summary>Forgets the session on this PC; the next action asks to sign in again.</summary>
    [RelayCommand]
    private void SignOut()
    {
        _discord.SignOut();
        ShowAccount();
        Players.Clear();
        Recipients.Clear();
        Sent.Clear();
        ApplyFilter();
        Sheet = null;
        Say("Sesión cerrada.");
    }

    /// <summary>Signs the organiser in with Discord, then reads everything again.</summary>
    [RelayCommand]
    private async Task SignInAsync()
    {
        Say("Termina de entrar en el navegador...");

        try
        {
            await _discord.SignInAsync();
            ShowAccount();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el inicio de sesión con Discord");
            Say("No se ha podido entrar con Discord.", true);
            return;
        }

        await RefreshAsync();
    }

    // ============================================================ LA LISTA DE JUGADORES (2026-10-08)

    /// <summary>The players the list shows: <see cref="Players"/> through the search and the filter.</summary>
    public ObservableCollection<PlayerLine> VisiblePlayers { get; } = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private PlayerFilter _filter = PlayerFilter.Todos;

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(PlayerFilter value) => ApplyFilter();

    public string ShownText => VisiblePlayers.Count == Players.Count
        ? $"{Players.Count} jugadores"
        : $"{VisiblePlayers.Count} de {Players.Count}";

    /// <summary>How many each filter would show, for its button.</summary>
    public string CountTodos => Players.Count.ToString(CultureInfo.InvariantCulture);

    public string CountConectados => Players.Count(p => Matches(p, PlayerFilter.Conectados)).ToString(CultureInfo.InvariantCulture);

    public string CountARevisar => Players.Count(p => Matches(p, PlayerFilter.ARevisar)).ToString(CultureInfo.InvariantCulture);

    public string CountSuspendidos => Players.Count(p => Matches(p, PlayerFilter.Suspendidos)).ToString(CultureInfo.InvariantCulture);

    public string CountSinRun => Players.Count(p => Matches(p, PlayerFilter.SinRun)).ToString(CultureInfo.InvariantCulture);

    public static bool Matches(PlayerLine player, PlayerFilter filter) => filter switch
    {
        PlayerFilter.Conectados => player.State != PresenceState.Offline,
        PlayerFilter.ARevisar => player.NeedsLook,
        PlayerFilter.Suspendidos => player.Suspended,
        PlayerFilter.SinRun => !player.HasRun,
        _ => true
    };

    /// <summary>The search looks at the name, the Discord id and the role, ignoring case and accents.</summary>
    public static bool Matches(PlayerLine player, string search) =>
        string.IsNullOrWhiteSpace(search)
        || new[] { player.Name, player.DiscordId, player.Role ?? string.Empty }.Any(field =>
            CultureInfo.InvariantCulture.CompareInfo.IndexOf(field, search.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    private void ApplyFilter()
    {
        VisiblePlayers.Clear();
        foreach (var player in Players.Where(p => Matches(p, Filter) && Matches(p, Search))) VisiblePlayers.Add(player);

        foreach (var name in new[] { nameof(ShownText), nameof(CountTodos), nameof(CountConectados), nameof(CountARevisar), nameof(CountSuspendidos), nameof(CountSinRun) })
        {
            OnPropertyChanged(name);
        }
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        if (Enum.TryParse<PlayerFilter>(filter, out var value)) Filter = value;
    }

    // ============================================================ A VARIOS A LA VEZ (desde JUGADORES)

    /// <summary>The players the gift or the bulk action goes to: one, several or all. Each gets their own.</summary>
    /// <remarks>
    /// There is no «a todos» any more (removed on 2026-09-24 at the organiser's request): it used to start ticked and
    /// override the selection, so a gift meant for one player reached everybody.
    /// </remarks>
    public ObservableCollection<PlayerLine> Chosen { get; } = [];

    /// <summary>Called by the lists when their selection changes. Only players who have signed in can be sent anything.</summary>
    public void Choose(IEnumerable<PlayerLine> players)
    {
        Chosen.Clear();
        foreach (var player in players.Where(p => p.HasAccount)) Chosen.Add(player);
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanAdjust));
        OnPropertyChanged(nameof(HasBulk));
        OnPropertyChanged(nameof(BulkText));
    }

    /// <summary>Two or more marked in JUGADORES: the bar of actions for all of them shows.</summary>
    public bool HasBulk => Chosen.Count > 1;

    public string BulkText => $"{Chosen.Count} marcados";

    [ObservableProperty]
    private string _bulkMessage = string.Empty;

    [ObservableProperty]
    private int _bulkPoints;

    private string Names(IReadOnlyCollection<PlayerLine> players) =>
        players.Count <= 6 ? string.Join(", ", players.Select(p => p.Name)) : $"{string.Join(", ", players.Take(5).Select(p => p.Name))} y {players.Count - 5} más";

    /// <summary>One order to each chosen player, in one send: all of them get it, or none. True when it was sent.</summary>
    /// <remarks>
    /// <paramref name="players"/> is a copy taken before asking: the minute's refresh can empty <see cref="Chosen"/> while the
    /// confirmation is open.
    /// </remarks>
    private async Task<bool> SendOrderToAsync(IReadOnlyList<PlayerLine> players, string kind, Dictionary<string, string> args, string summary,
        IReadOnlyList<string> consequences, bool danger = false)
    {
        if (players.Count == 0)
        {
            Say("Marca a alguien que haya entrado alguna vez.", true);
            return false;
        }

        if (Confirm.Ask(new ConfirmRequest(summary, $"{players.Count} jugador(es): {Names(players)}",
                [.. consequences, "Va a todos en un solo envío: o les llega a todos o a ninguno.", "Cada app lo aplica sola y deja su evento."],
                "MANDAR A TODOS", Danger: danger)) is not { } answer)
        {
            return false;
        }

        var orders = players.Select(player => new AdminGift
        {
            Schema = AdminGift.OrderSchema,
            Id = Guid.NewGuid(),
            From = AdminName,
            To = player.Id.ToString(),
            Reason = answer.Reason,
            CreatedAt = DateTimeOffset.Now,
            Order = new AdminOrder(kind, args, summary)
        }).ToList();

        try
        {
            var added = await _desk.SendManyAsync(orders);
            Say($"{summary}: mandado a {added} jugador(es).");
            await RefreshAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar {Kind} a varios", kind);
            Say($"No se ha mandado a nadie: {ex.Message}", true);
            return false;
        }
    }

    [RelayCommand]
    private async Task BulkMessageAsync()
    {
        var text = BulkMessage.Trim();
        if (text.Length == 0)
        {
            Say("Escribe el mensaje.", true);
            return;
        }

        // Si se cancela o falla, el mensaje escrito se queda.
        if (await SendOrderToAsync([.. Chosen], AdminOrderKinds.Message, new() { ["texto"] = text }, "Mensaje privado",
                [$"«{text}»", "Le sale encima del juego y en su buzón."]))
        {
            BulkMessage = string.Empty;
        }
    }

    [RelayCommand]
    private Task BulkLockAsync() => SendOrderToAsync([.. Chosen], AdminOrderKinds.PlayLock, new() { ["cerrado"] = "true" },
        "Cerrar el juego", ["No podrán pulsar JUGAR hasta que lo vuelvas a abrir."], danger: true);

    [RelayCommand]
    private Task BulkUnlockAsync() => SendOrderToAsync([.. Chosen], AdminOrderKinds.PlayLock, new() { ["cerrado"] = "false" },
        "Volver a abrir el juego", ["Vuelven a poder pulsar JUGAR."]);

    /// <summary>The adjustment of JUGADORES' bar: its own points, never REGALOS' fields.</summary>
    [RelayCommand]
    private async Task BulkAdjustAsync()
    {
        if (await AdjustWithAsync([.. Chosen], BulkPoints, null)) BulkPoints = 0;
    }

    // ============================================================ EL REGALO QUE SE ESTÁ ESCRIBIENDO

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private int _points;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private string _reason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private int _wonderTrades;

    public ObservableCollection<BannerRow> Banners { get; } = [];

    /// <summary>A gift needs a reason and something inside it. Both, always.</summary>
    public bool CanSend => Reason.Trim().Length > 0
                           && Chosen.Count > 0
                           && (Points != 0 || WonderTrades > 0 || Banners.Any(banner => banner.Rolls > 0));

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        if (Points is < -100_000 or > 100_000 || WonderTrades is < 0 or > 100 || Banners.Any(b => b.Rolls is < 0 or > 500))
        {
            Say("Algún número se sale de lo razonable (puntos ±100.000, wonder trades hasta 100, tiradas hasta 500).", true);
            return;
        }

        var rolls = Banners.Where(banner => banner.Rolls > 0).ToDictionary(banner => banner.Id, banner => banner.Rolls);
        var gifts = Chosen.Select(player => new AdminGift
        {
            Id = Guid.NewGuid(),
            From = AdminName,
            To = player.Id.ToString(),
            Reason = Reason.Trim(),
            CreatedAt = DateTimeOffset.Now,
            Points = Points,
            Rolls = rolls,
            WonderTrades = WonderTrades
        }).ToList();

        if (Confirm.Ask(new ConfirmRequest("Mandar regalo", $"{Chosen.Count} jugador(es): {Names(Chosen)}",
                [gifts[0].Say(), $"Motivo: {Reason.Trim()}", "Cada uno lo recoge al abrir PermaLocke.", "Mientras nadie lo haya recogido se puede retirar."],
                "MANDAR", NeedsReason: false)) is null)
        {
            return;
        }

        try
        {
            var added = await _desk.SendManyAsync(gifts);
            Say($"Mandado a {added} jugador(es): {gifts[0].Say()}.");

            Clear();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar el regalo");
            Say($"No se ha mandado a nadie: {ex.Message}", true);
        }
    }

    /// <summary>An adjustment is points and a reason, nothing else: the player cannot refuse it, so it carries no prizes.</summary>
    public bool CanAdjust => AdjustReason.Trim().Length > 0 && Chosen.Count > 0 && AdjustPoints != 0;

    /// <summary>The adjustment has its own fields, apart from the gift's, so one is never sent as the other.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private int _adjustPoints;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjust))]
    private string _adjustReason = string.Empty;

    /// <summary>
    /// Adds or takes points from the chosen players on the organiser's say, applied by their application without asking
    /// (2026-09-24). It travels as a gift marked <see cref="AdminGift.Adjustment"/>, so it lands in their history with
    /// the reason, like everything else.
    /// </summary>
    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (await AdjustWithAsync([.. Chosen], AdjustPoints, AdjustReason.Trim()))
        {
            AdjustPoints = 0;
            AdjustReason = string.Empty;
        }
    }

    /// <param name="players">A copy of the marked players taken before asking (the minute's refresh can empty <see cref="Chosen"/>).</param>
    /// <param name="reason">The reason already written (REGALOS), or null to ask it in the confirmation (JUGADORES).</param>
    /// <returns>True when it was sent.</returns>
    private async Task<bool> AdjustWithAsync(IReadOnlyList<PlayerLine> players, int points, string? reason)
    {
        if (players.Count == 0 || points == 0 || Math.Abs(points) > 100_000)
        {
            Say("Marca a alguien y pon los puntos (en negativo para quitar, como mucho 100.000).", true);
            return false;
        }

        if (Confirm.Ask(new ConfirmRequest($"{points:+#;-#} puntos", $"{players.Count} jugador(es): {Names(players)}",
                ["Se aplica solo, sin que lo recojan, y queda en su historial con el motivo.", "Se deshace con otro ajuste del signo contrario."],
                "AJUSTAR", NeedsReason: reason is null, Danger: points < 0, Reason: reason)) is not { } answer)
        {
            return false;
        }

        var why = reason ?? answer.Reason;
        var adjustments = players.Select(player => new AdminGift
        {
            Schema = AdminGift.AdjustmentSchema,
            Id = Guid.NewGuid(),
            From = AdminName,
            To = player.Id.ToString(),
            Reason = why,
            CreatedAt = DateTimeOffset.Now,
            Points = points,
            Adjustment = true
        }).ToList();

        try
        {
            var added = await _desk.SendManyAsync(adjustments);
            Say($"Ajuste de {points:+#;-#} para {added} jugador(es). Su PermaLocke lo aplica solo en unos segundos, o al abrirlo.");
            await RefreshAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar el ajuste");
            Say($"No se ha mandado a nadie: {ex.Message}", true);
            return false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Points = 0;
        WonderTrades = 0;
        Reason = string.Empty;

        foreach (var banner in Banners)
        {
            banner.Rolls = 0;
        }

        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanAdjust));
    }

    // ============================================================ EL MENÚ (2026-09-28)

    /// <summary>The side menu, grouped: every page of Admin.</summary>
    public IReadOnlyList<AdminSection> Sections { get; }

    [ObservableProperty]
    private AdminSection _selectedSection;

    /// <summary>A page opened from the menu reads its data at once, so nothing on screen is stale.</summary>
    partial void OnSelectedSectionChanged(AdminSection value) => RefreshPage(value);

    /// <summary>ACTUALIZAR of the header (F5): the players and gifts, and the page on screen.</summary>
    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await RefreshAsync();
        RefreshPage(SelectedSection);
        if (Sheet is not null && SelectedSection.Page is PlayersSection) await Sheet.LoadCommand.ExecuteAsync(null);
    }

    private void RefreshPage(AdminSection value)
    {
        switch (value.Page)
        {
            case RulesViewModel rules: rules.LoadCommand.Execute(null); break;
            case LogViewModel log: log.RefreshCommand.Execute(null); break;
            case ObservableObject page when page != this
                && page.GetType().GetProperty("RefreshCommand")?.GetValue(page) is System.Windows.Input.ICommand refresh:
                refresh.Execute(null);
                break;
        }
    }

    /// <summary>Opens a page of the menu by its name.</summary>
    [RelayCommand]
    private void Go(string title)
    {
        if (Sections.FirstOrDefault(s => s.Title == title) is { } section) SelectedSection = section;
    }

    /// <summary>The player open in JUGADORES, beside the list.</summary>
    [ObservableProperty]
    private PlayerSheetViewModel? _sheet;

    [RelayCommand]
    private void OpenSheet(PlayerLine? player)
    {
        if (player is null) return;
        if (Sheet?.Player == player && SelectedSection.Page is PlayersSection) return;

        Sheet = new PlayerSheetViewModel(_desk, player, AdminName, _species, _items, _logger, ReopenAsync);
        Sheet.LoadCommand.Execute(null);
        Go("JUGADORES");
    }

    [RelayCommand]
    private void CloseSheet() => Sheet = null;

    /// <summary>After a change to the account (suspend, reset), the list and the sheet are read again.</summary>
    private async Task ReopenAsync()
    {
        var old = Sheet?.Player;
        await RefreshAsync();
        // Sin 21-admin.sql no hay Discord en la lista: se busca por la cuenta.
        if (old is not null && Players.FirstOrDefault(p => old.DiscordId.Length > 0 ? p.DiscordId == old.DiscordId : p.Id == old.Id) is { } fresh)
        {
            Sheet = new PlayerSheetViewModel(_desk, fresh, AdminName, _species, _items, _logger, ReopenAsync);
            await Sheet.LoadCommand.ExecuteAsync(null);
        }
    }

    // ============================================================ CONTROL DEL TORNEO

    /// <summary>Closes JUGAR for every player at once (a pause, or the end of the tournament).</summary>
    [RelayCommand]
    private Task PauseAllAsync() => SendOrderToAsync([.. Recipients], AdminOrderKinds.PlayLock, new() { ["cerrado"] = "true" },
        "Pausar el torneo", ["Nadie podrá pulsar JUGAR hasta que lo reanudes.", "Quien esté jugando recibe el aviso y vale para la próxima vez."], danger: true);

    [RelayCommand]
    private Task ResumeAllAsync() => SendOrderToAsync([.. Recipients], AdminOrderKinds.PlayLock, new() { ["cerrado"] = "false" },
        "Reanudar el torneo", ["Todos vuelven a poder pulsar JUGAR."]);

    /// <summary>Takes back a gift nobody has collected yet.</summary>
    [RelayCommand]
    private async Task WithdrawAsync(SentGift? sent)
    {
        if (sent is null)
        {
            return;
        }

        if (sent.Collected.Count > 0)
        {
            Say("Ya lo ha recogido alguien: retirarlo no se lo quita.", true);
            return;
        }

        if (Confirm.Ask(new ConfirmRequest("Retirar el envío", sent.Gift.Reason,
                [sent.Gift.Say(), "Nadie podrá recogerlo.", "Queda apuntado en el REGISTRO."], "RETIRAR", NeedsReason: false, Danger: true)) is null)
        {
            return;
        }

        try
        {
            await _desk.WithdrawAsync(sent.Gift.Id);
            Say("Envío retirado.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló retirar el regalo {Id}", sent.Gift.Id);
            Say($"No se ha podido retirar: {ex.Message}", true);
        }
    }
}
