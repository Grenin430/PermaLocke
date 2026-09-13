using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One card in the shop grid.</summary>
public sealed partial class ShopItemViewModel(ShopItem item, BitmapSource? icon) : ObservableObject
{
    public ShopItem Item { get; } = item;

    public string Name => Item.Name;

    public int Price => Item.Price;

    public string PriceText => $"{Item.Price} pts.";

    public BitmapSource? Icon { get; } = icon;

    /// <summary>Shown when the cartridge has no icon for it, so the card is never empty.</summary>
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    /// <summary>How many the player is carrying, or empty while the game cannot be read.</summary>
    [ObservableProperty]
    private string _carried = string.Empty;

    /// <summary>False while the balance does not reach, so the button says why by being off.</summary>
    [ObservableProperty]
    private bool _affordable = true;
}

/// <summary>
/// The shop: what the competition sells, for run points, delivered into the game's bag.
/// </summary>
/// <remarks>
/// Buying needs the emulator open with the run loaded, because the item is written straight into
/// the bag. The screen says so rather than letting a click fail with no explanation.
/// </remarks>
public sealed partial class ShopViewModel : SectionViewModel
{
    private readonly ShopService _shop;
    private readonly IRunContext _runs;
    private readonly PokemonSpriteService _sprites;
    private readonly IAppDialogs _dialogs;
    private readonly ILogger<ShopViewModel> _logger;

    public ShopViewModel(ShopService shop, IRunContext runs, PokemonSpriteService sprites,
        IAppDialogs dialogs, ILogger<ShopViewModel> logger)
        : base("TIENDA", "Objetos a cambio de puntos, entregados a la mochila del juego")
    {
        _shop = shop;
        _runs = runs;
        _sprites = sprites;
        _dialogs = dialogs;
        _logger = logger;
    }

    /// <summary>Everything on sale, both counters. What the bag is asked about in one go.</summary>
    public ObservableCollection<ShopItemViewModel> Items { get; } = [];

    /// <summary>
    /// The counter the player is looking at.
    /// </summary>
    /// <remarks>
    /// Refilled rather than filtered through a view, because the grid is bound straight to it and
    /// a CollectionView touched from anywhere but the UI thread takes the window down (§62).
    /// </remarks>
    public ObservableCollection<ShopItemViewModel> Visible { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BattleTab))]
    [NotifyPropertyChangedFor(nameof(MegaTab))]
    private bool _showingMegaStones;

    /// <summary>Leaving the shop puts it back on the tab it opens with.</summary>
    public override void ResetState() => ShowingMegaStones = false;

    /// <summary>Counts on the tabs, so nobody has to open one to find out it is empty.</summary>
    public string BattleTab => $"COMBATE ({Items.Count(i => !IsMega(i))})";

    public string MegaTab => $"MEGAPIEDRAS ({Items.Count(IsMega)})";

    private static bool IsMega(ShopItemViewModel card) =>
        string.Equals(card.Item.Category, ShopItem.MegaStones, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void ShowBattle() => Show(mega: false);

    [RelayCommand]
    private void ShowMegaStones() => Show(mega: true);

    /// <summary>What the player has typed in the box. Empty shows the whole counter.</summary>
    /// <remarks>
    /// It earns its place on the mega stone tab, which went from 47 items to 93 when the gen 8-9
    /// expansion arrived: finding Dragoninite in that grid by scrolling is a chore. The battle
    /// counter has eighteen and does not need it, but the box is on both because a search that
    /// appears and disappears is worse than one that is always in the same place.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NothingFound))]
    private string _search = string.Empty;

    /// <summary>True when the box has something in it and nothing matches.</summary>
    public bool NothingFound => Search.Length > 0 && Visible.Count == 0;

    partial void OnSearchChanged(string value) => Show(ShowingMegaStones);

    [RelayCommand]
    private void ClearSearch() => Search = string.Empty;

    private void Show(bool mega)
    {
        ShowingMegaStones = mega;
        Visible.Clear();

        foreach (var card in Items.Where(card => IsMega(card) == mega && Matches(card)))
        {
            Visible.Add(card);
        }

        OnPropertyChanged(nameof(NothingFound));
    }

    /// <summary>
    /// Whether the card's name contains what was typed, ignoring case and accents.
    /// </summary>
    /// <remarks>
    /// Accents are ignored on purpose and it is not politeness: the counter mixes the cartridge's
    /// Spanish — «Poción», «Protección X» — with the mod's English names, which are the only ones
    /// the mod publishes. Somebody typing "pocion" is not making a mistake, and a search that
    /// answers nothing to that would look broken.
    /// </remarks>
    private bool Matches(ShopItemViewModel card) =>
        Search.Length == 0
        || CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            card.Item.Name, Search.Trim(),
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    [ObservableProperty]
    private int _balance;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _problem = string.Empty;

    [ObservableProperty]
    private bool _busy;

    public override string IconKey => "IconShop";

    public override GameNeed Needs => GameNeed.Running;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();

        if (Items.Count == 0)
        {
            foreach (var item in _shop.Items)
            {
                Items.Add(new ShopItemViewModel(item, _sprites.GetItem(item.Id)));
            }

            OnPropertyChanged(nameof(BattleTab));
            OnPropertyChanged(nameof(MegaTab));
            Show(ShowingMegaStones);
        }

        if (Items.Count == 0)
        {
            Problem = "No hay nada a la venta: falta Data/shop.json o está vacío.";
            return;
        }

        await RefreshAsync();
    }

    /// <summary>
    /// Nothing that talks to the emulator is allowed to run longer than this.
    /// </summary>
    /// <remarks>
    /// Locating the bag can mean sweeping the game's memory, and if the emulator is not there the
    /// sweep can only end in failure — slowly. A purchase that never returns leaves the whole grid
    /// disabled, because the command is still running, and the screen looks broken rather than
    /// busy. This is the backstop; the ping in the delivery is what makes it rarely needed.
    /// </remarks>
    private static readonly TimeSpan GameTimeout = TimeSpan.FromSeconds(20);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_runs.Current is not { } run)
        {
            Problem = "No hay ninguna run creada.";
            return;
        }

        Busy = true;
        try
        {
            Balance = await _shop.GetBalanceAsync(run.Id);

            foreach (var card in Items)
            {
                card.Affordable = Balance >= card.Price;
            }

            // Una sola lectura de la mochila para las dieciocho. Preguntar una por una era
            // dieciocho localizaciones del bloque, y con el emulador cerrado, dieciocho fracasos
            // lentos seguidos: la pantalla se quedaba muerta al abrirla.
            using var cancel = new CancellationTokenSource(GameTimeout);
            var carried = await _shop.CarriedAllAsync([.. Items.Select(i => i.Item.Id)], cancel.Token);

            foreach (var card in Items)
            {
                card.Carried = carried.TryGetValue(card.Item.Id, out var count) ? $"llevas {count}" : string.Empty;
            }

            Problem = carried.Count > 0
                ? string.Empty
                : "El juego no está abierto, así que no se puede entregar nada. Abre Azahar con la "
                  + "partida cargada y pulsa ACTUALIZAR.";
        }
        catch (OperationCanceledException)
        {
            Problem = "El juego ha tardado demasiado en contestar. Comprueba que Azahar está abierto.";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private async Task BuyAsync(ShopItemViewModel? card)
    {
        if (card is null || Busy || _runs.Current is not { } run)
        {
            return;
        }

        // SE PREGUNTA ANTES. La tarjeta entera es el boton, que es lo que quita las dieciocho
        // barras violetas de la pantalla, pero tambien hace que un clic de mas cueste puntos: lo
        // que se pulsa paso de un boton pequeño a un objeto grande. Y una compra no se deshace
        // -el objeto se escribe en la mochila del juego-, asi que aqui se pregunta.
        var confirmed = _dialogs.Confirm("Comprar",
            $"¿Comprar {card.Name} por {card.Price} puntos?"
            + $"{Environment.NewLine}{Environment.NewLine}"
            + $"Tienes {Balance} puntos. El objeto se escribirá en la mochila de tu partida.");

        _logger.LogInformation("Tienda: {Item} por {Price} puntos, {Answer}",
            card.Name, card.Price, confirmed ? "confirmado" : "cancelado");

        if (!confirmed)
        {
            return;
        }

        Busy = true;
        Status = string.Empty;
        Problem = $"Comprando {card.Name}...";

        try
        {
            using var cancel = new CancellationTokenSource(GameTimeout);
            var result = await _shop.BuyAsync(run, card.Item.Id, cancel.Token);

            Status = result.Succeeded ? result.Message : string.Empty;
            Problem = result.Succeeded ? string.Empty : result.Message;
            Balance = result.Balance;
        }
        catch (OperationCanceledException)
        {
            Status = string.Empty;
            Problem = "El juego ha tardado demasiado en contestar, así que no se ha comprado nada "
                      + "ni se te ha quitado ningún punto.";
        }
        finally
        {
            Busy = false;
        }

        await RefreshAsync();
    }
}
