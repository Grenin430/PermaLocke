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
        : base("TIENDA")
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_runs.Current is not { } run)
        {
            Problem = "No hay ninguna run creada.";
            return;
        }

        Balance = await _shop.GetBalanceAsync(run.Id);

        var reachable = await _shop.CarriedAsync(Items[0].Item.Id) >= 0;

        foreach (var card in Items)
        {
            card.Affordable = Balance >= card.Price;
            card.Carried = reachable ? $"llevas {await _shop.CarriedAsync(card.Item.Id)}" : string.Empty;
        }

        Problem = reachable
            ? string.Empty
            : "El juego no está abierto, así que no se puede entregar nada. Abre Azahar con la partida cargada.";
    }

    [RelayCommand]
    private async Task BuyAsync(ShopItemViewModel? card)
    {
        if (card is null || Busy || _runs.Current is not { } run)
        {
            return;
        }

        Busy = true;
        try
        {
            var result = await _shop.BuyAsync(run, card.Item.Id);

            Status = result.Message;
            Problem = result.Succeeded ? string.Empty : result.Message;
            Balance = result.Balance;

            await RefreshAsync();
        }
        finally
        {
            Busy = false;
        }
    }
}
