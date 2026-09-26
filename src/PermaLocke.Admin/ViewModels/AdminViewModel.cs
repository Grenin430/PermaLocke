using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Admin.ViewModels;

/// <summary>A banner of the gacha with how many free rolls this gift gives on it.</summary>
public sealed partial class BannerRow(string id, string name) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    private int _rolls;
}

/// <summary>
/// The admin's only screen: who is in the competition, and the gift being written for them (§129).
/// </summary>
/// <remarks>
/// Everything it does goes through the tournament server (<see cref="GiftDesk"/>). It cannot touch anybody's run:
/// points, rolls and wonder trades are applied by the player's own application when they collect the gift. Items were
/// removed on 2026-09-24 at the organiser's request.
/// </remarks>
public sealed partial class AdminViewModel : ObservableObject
{
    private readonly GiftDesk _desk;
    private readonly DiscordLogin _discord;
    private readonly ILogger<AdminViewModel> _logger;

    /// <summary>The tournament audit, in its own window.</summary>
    public AuditViewModel Audit { get; }

    /// <summary>The tournament's whitelist, in its own window.</summary>
    public WhitelistViewModel Whitelist { get; }

    /// <summary>The announcements to every player, in their own window.</summary>
    public AnnouncementsViewModel Announcements { get; }

    public UsageViewModel Usage { get; }

    /// <summary>The server cleanup, in its own window (§194).</summary>
    public CleanupViewModel Cleanup { get; }

    /// <summary>The official rules, in their own window (2026-09-26).</summary>
    public RulesViewModel Rules { get; }

    private readonly IReadOnlyList<Pick> _species;
    private readonly IReadOnlyList<Pick> _items;

    public AdminViewModel(GiftDesk desk, DiscordLogin discord, IGachaCatalog gacha, ISpeciesStatsCatalog species, IShopCatalog shop,
        AuditViewModel audit,
        WhitelistViewModel whitelist, AnnouncementsViewModel announcements, UsageViewModel usage, RulesViewModel rules,
        CleanupViewModel cleanup, ILogger<AdminViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(gacha);

        _desk = desk;
        Audit = audit;
        Whitelist = whitelist;
        Announcements = announcements;
        Usage = usage;
        Rules = rules;
        Cleanup = cleanup;
        _discord = discord;
        _logger = logger;
        _species = [.. species.All.Where(s => s.Id > 0).Select(s => new Pick(s.Id, s.Name))];
        _items = [.. shop.Items.GroupBy(i => i.Id).Select(g => new Pick(g.Key, g.First().Name)).OrderBy(i => i.Name)];

        foreach (var banner in gacha.Banners)
        {
            Banners.Add(new BannerRow(banner.Id, banner.Name));
        }
    }

    // ============================================================ QUIÉN HAY

    [ObservableProperty]
    private string _adminName = "Admin";

    public ObservableCollection<PlayerLine> Players { get; } = [];

    public ObservableCollection<SentGift> Sent { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

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
        IsBusy = true;

        try
        {
            var players = await _desk.PlayersAsync();

            Players.Clear();
            foreach (var player in players)
            {
                Players.Add(player);
            }

            Sent.Clear();
            foreach (var gift in await _desk.SentAsync(players))
            {
                Sent.Add(gift);
            }

            Status = Players.Count == 0
                ? "Todavía no ha subido nadie su run."
                : $"{Players.Count} jugador(es).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la lectura del servidor del torneo");
            Status = "No se ha podido leer el servidor. Pulsa ENTRAR CON DISCORD.";
        }
        finally
        {
            IsBusy = false;
        }
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
        Sent.Clear();
        Status = "Sesión cerrada.";
    }

    /// <summary>Signs the organiser in with Discord, then reads everything again.</summary>
    [RelayCommand]
    private async Task SignInAsync()
    {
        Status = "Termina de entrar en el navegador...";

        try
        {
            await _discord.SignInAsync();
            ShowAccount();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el inicio de sesión con Discord");
            Status = "No se ha podido entrar con Discord.";
            return;
        }

        await RefreshAsync();
    }

    // ============================================================ EL REGALO QUE SE ESTÁ ESCRIBIENDO

    /// <summary>The players the gift goes to: one, several or all (SELECCIONAR TODOS). Each gets their own gift.</summary>
    /// <remarks>
    /// There is no «a todos» any more (removed on 2026-09-24 at the organiser's request): it used to start ticked and
    /// override the selection, so a gift meant for one player reached everybody.
    /// </remarks>
    public ObservableCollection<PlayerLine> Chosen { get; } = [];

    /// <summary>Called by the list when its selection changes.</summary>
    public void Choose(IEnumerable<PlayerLine> players)
    {
        Chosen.Clear();
        foreach (var player in players) Chosen.Add(player);
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanAdjust));
    }

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

        // Un regalo por destinatario: el servidor enseña a cada jugador solo los suyos.
        var gifts = Chosen.Select(player => player.Id.ToString()).Select(recipient => new AdminGift
        {
            Id = Guid.NewGuid(),
            From = AdminName,
            To = recipient,
            Reason = Reason.Trim(),
            CreatedAt = DateTimeOffset.Now,
            Points = Points,
            Rolls = Banners.Where(banner => banner.Rolls > 0)
                .ToDictionary(banner => banner.Id, banner => banner.Rolls),
            WonderTrades = WonderTrades
        }).ToList();

        try
        {
            foreach (var gift in gifts) await _desk.SendAsync(gift);
            Status = $"Mandado a {string.Join(", ", Chosen.Select(p => p.Name))}: {gifts[0].Say()}.";

            Clear();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar el regalo");
            Status = "No se ha podido mandar.";
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
        if (!CanAdjust || System.Windows.MessageBox.Show(
                $"{AdjustPoints:+#;-#} puntos a {string.Join(", ", Chosen.Select(p => p.Name))}.\n\nMotivo: {AdjustReason.Trim()}\n\n"
                + "Se aplica solo, sin que el jugador lo recoja, y queda en su historial.",
                "Ajustar puntos", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var adjustments = Chosen.Select(player => new AdminGift
        {
            Schema = AdminGift.AdjustmentSchema,
            Id = Guid.NewGuid(),
            From = AdminName,
            To = player.Id.ToString(),
            Reason = AdjustReason.Trim(),
            CreatedAt = DateTimeOffset.Now,
            Points = AdjustPoints,
            Adjustment = true
        }).ToList();

        try
        {
            foreach (var adjustment in adjustments) await _desk.SendAsync(adjustment);
            Status = $"Ajuste para {string.Join(", ", Chosen.Select(p => p.Name))}: {adjustments[0].Say()}. Su PermaLocke lo aplica solo en unos segundos, o al abrirlo.";

            AdjustPoints = 0;
            AdjustReason = string.Empty;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar el ajuste");
            Status = "No se ha podido mandar el ajuste.";
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

    // ============================================================ FICHA Y CONTROL DEL TORNEO (2026-09-26)

    /// <summary>Raised when a player's sheet should open; the window creates it.</summary>
    public event EventHandler<PlayerSheetViewModel>? SheetRequested;

    [RelayCommand]
    private void OpenSheet(PlayerLine? player)
    {
        if (player is not null)
        {
            SheetRequested?.Invoke(this, new PlayerSheetViewModel(_desk, player, AdminName, _species, _items, _logger));
        }
    }

    /// <summary>Closes JUGAR for every player at once (a pause, or the end of the tournament), with the reason of the gift box.</summary>
    [RelayCommand]
    private Task PauseAllAsync() => LockAllAsync(true);

    [RelayCommand]
    private Task ResumeAllAsync() => LockAllAsync(false);

    private async Task LockAllAsync(bool closed)
    {
        if (Reason.Trim().Length == 0)
        {
            Status = "Escribe el MOTIVO en MANDAR UN REGALO: lo leen todos.";
            return;
        }

        if (System.Windows.MessageBox.Show(
                $"{(closed ? "Cerrar el juego" : "Volver a abrir el juego")} a los {Players.Count} jugadores.\n\nMotivo: {Reason.Trim()}",
                closed ? "Pausar el torneo" : "Reanudar el torneo", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var player in Players)
            {
                await _desk.SendOrderAsync(player.Id, AdminName, Reason.Trim(), new AdminOrder(AdminOrderKinds.PlayLock,
                    new Dictionary<string, string> { ["cerrado"] = closed ? "true" : "false" },
                    closed ? "Torneo en pausa: juego cerrado" : "Torneo reanudado: juego abierto"));
            }

            Status = closed ? "Torneo en pausa: nadie puede pulsar JUGAR." : "Torneo reanudado.";
            Reason = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló pausar o reanudar el torneo");
            Status = "No se ha podido mandar a todos.";
        }
    }

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
            Status = "Ya lo ha recogido alguien: retirarlo no se lo quita.";
            return;
        }

        await _desk.WithdrawAsync(sent.Gift.Id);
        Status = "Regalo retirado.";
        await RefreshAsync();
    }
}
