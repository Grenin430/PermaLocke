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

/// <summary>One item already added to the gift being written.</summary>
public sealed record ChosenItem(int Id, string Name, int Amount)
{
    public string Label => Amount == 1 ? Name : $"{Amount} × {Name}";
}

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
/// Everything it does is write a file in the shared folder. It cannot touch anybody's run — that is the player's own
/// application, on the player's own machine — so what it shows about a gift is what the players themselves have
/// published about it.
/// </remarks>
public sealed partial class AdminViewModel : ObservableObject
{
    private readonly GiftDesk _desk;
    private readonly DiscordLogin _discord;
    private readonly IItemLookup _items;
    private readonly ILogger<AdminViewModel> _logger;

    /// <summary>Every item of the game with its name, built once so the search box is instant.</summary>
    private readonly List<ChosenItem> _catalogue = [];

    /// <summary>The tournament audit, in its own window.</summary>
    public AuditViewModel Audit { get; }

    public AdminViewModel(GiftDesk desk, DiscordLogin discord, IItemLookup items, IGachaCatalog gacha, AuditViewModel audit, ILogger<AdminViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(gacha);

        _desk = desk;
        Audit = audit;
        _discord = discord;
        _items = items;
        _logger = logger;

        foreach (var banner in gacha.Banners)
        {
            Banners.Add(new BannerRow(banner.Id, banner.Name));
        }

    }

    // ============================================================ LA CARPETA Y QUIÉN HAY

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
        AdminName = _discord.Saved?.Name ?? "Organizador";
        await RefreshAsync();
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
            Status = "No se ha podido leer el servidor. Entra con Discord desde AUDITORÍA DEL TORNEO.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ============================================================ EL REGALO QUE SE ESTÁ ESCRIBIENDO

    /// <summary>The players the gift goes to; several at once. Empty with <see cref="ToEverybody"/> off sends nothing.</summary>
    /// <remarks>
    /// «A todos» used to start ticked and choosing somebody did not untick it, so a gift meant for one player reached
    /// everybody. Now it starts unticked, and choosing anybody unticks it.
    /// </remarks>
    public ObservableCollection<PlayerLine> Chosen { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private bool _toEverybody;

    /// <summary>Called by the list when its selection changes.</summary>
    public void Choose(IEnumerable<PlayerLine> players)
    {
        Chosen.Clear();
        foreach (var player in players) Chosen.Add(player);
        if (Chosen.Count > 0) ToEverybody = false;
        OnPropertyChanged(nameof(CanSend));
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

    public ObservableCollection<ChosenItem> Items { get; } = [];

    [ObservableProperty]
    private string _itemSearch = string.Empty;

    public ObservableCollection<ChosenItem> Matches { get; } = [];

    [ObservableProperty]
    private ChosenItem? _match;

    [ObservableProperty]
    private int _amount = 1;

    /// <summary>A gift needs a reason and something inside it. Both, always.</summary>
    public bool CanSend => Reason.Trim().Length > 0
                           && (ToEverybody || Chosen.Count > 0)
                           && (Points != 0 || Items.Count > 0 || WonderTrades > 0
                               || Banners.Any(banner => banner.Rolls > 0));

    partial void OnItemSearchChanged(string value) => Search(value);

    private void Search(string text)
    {
        Matches.Clear();

        if (text.Trim().Length < 2)
        {
            return;
        }

        // La tabla de objetos del juego, una vez: 960 nombres que no cambian mientras la app está abierta.
        if (_catalogue.Count == 0)
        {
            for (var id = 1; id <= 960; id++)
            {
                var name = _items.GetName(id);

                if (!string.IsNullOrWhiteSpace(name) && name != "???")
                {
                    _catalogue.Add(new ChosenItem(id, name, 1));
                }
            }
        }

        foreach (var item in _catalogue
                     .Where(item => item.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase))
                     .Take(25))
        {
            Matches.Add(item);
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        if (Match is null || Amount <= 0)
        {
            return;
        }

        Items.Add(Match with { Amount = Amount });
        OnPropertyChanged(nameof(CanSend));
        ItemSearch = string.Empty;
        Amount = 1;
    }

    [RelayCommand]
    private void RemoveItem(ChosenItem? item)
    {
        if (item is not null)
        {
            Items.Remove(item);
            OnPropertyChanged(nameof(CanSend));
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        // Un regalo por destinatario: el servidor enseña a cada jugador solo los suyos.
        List<string> to = ToEverybody ? [AdminGift.Everybody] : [.. Chosen.Select(player => player.Id.ToString())];
        var gifts = to.Select(recipient => new AdminGift
        {
            Id = Guid.NewGuid(),
            From = AdminName,
            To = recipient,
            Reason = Reason.Trim(),
            CreatedAt = DateTimeOffset.Now,
            Points = Points,
            Items = [.. Items.Select(item => new GiftItem(item.Id, item.Name, item.Amount))],
            Rolls = Banners.Where(banner => banner.Rolls > 0)
                .ToDictionary(banner => banner.Id, banner => banner.Rolls),
            WonderTrades = WonderTrades
        }).ToList();

        try
        {
            foreach (var gift in gifts) await _desk.SendAsync(gift);
            Status = ToEverybody
                ? $"Mandado a todos: {gifts[0].Say()}."
                : $"Mandado a {string.Join(", ", Chosen.Select(p => p.Name))}: {gifts[0].Say()}.";

            Clear();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mandar el regalo");
            Status = "No se ha podido mandar.";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        ToEverybody = false;
        Points = 0;
        WonderTrades = 0;
        Reason = string.Empty;
        Items.Clear();
        ItemSearch = string.Empty;
        Amount = 1;

        foreach (var banner in Banners)
        {
            banner.Rolls = 0;
        }

        OnPropertyChanged(nameof(CanSend));
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
