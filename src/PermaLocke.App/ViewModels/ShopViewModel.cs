using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ShopViewModel(ShopService shop, IRunContext runs, PokemonSpriteService sprites)
        : base("TIENDA", "Objetos a cambio de puntos, entregados a la mochila del juego")
    {
        _shop = shop;
        _runs = runs;
        _sprites = sprites;
    }

    public ObservableCollection<ShopItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private int _balance;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _problem = string.Empty;

    [ObservableProperty]
    private bool _busy;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();

        if (Items.Count == 0)
        {
            foreach (var item in _shop.Items)
            {
                Items.Add(new ShopItemViewModel(item, _sprites.GetItem(item.Id)));
            }
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
