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

    [ObservableProperty]
    private BitmapSource? _icon = icon;

    /// <summary>Shown when the cartridge has no icon for it, so the card is never empty.</summary>
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    /// <summary>How many the player is carrying, or empty while the game cannot be read.</summary>
    [ObservableProperty]
    private string _carried = string.Empty;

    /// <summary>What a nature herb raises and lowers (2026-09-27); empty for everything else.</summary>
    public string Raises => Item.IsHerb && Item.Nature / 5 != Item.Nature % 5 ? $"+ {StatNames[Item.Nature / 5]}" : string.Empty;

    public string Lowers => Item.IsHerb && Item.Nature / 5 != Item.Nature % 5 ? $"- {StatNames[Item.Nature % 5]}" : string.Empty;

    public string Neutral => Item.IsHerb && Item.Nature / 5 == Item.Nature % 5 ? "NO CAMBIA NADA" : string.Empty;

    /// <summary>The game's nature order: raised = nature / 5, lowered = nature % 5.</summary>
    private static readonly string[] StatNames = ["ATAQUE", "DEFENSA", "VELOCIDAD", "AT. ESP.", "DEF. ESP."];

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
    private readonly IBoxReader _boxes;
    private readonly PermaLocke.Rules.Services.ProgressService? _progress;

    public ShopViewModel(ShopService shop, IRunContext runs, PokemonSpriteService sprites,
        IAppDialogs dialogs, IBoxReader boxes, ILogger<ShopViewModel> logger,
        PermaLocke.Rules.Services.ProgressService? progress = null)
        : base("TIENDA", "Objetos a cambio de puntos")
    {
        Fleeting.Fade(this, nameof(Status), nameof(Problem));

        _shop = shop;
        _runs = runs;
        _sprites = sprites;
        _dialogs = dialogs;
        _logger = logger;
        _boxes = boxes;
        _progress = progress;
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

    /// <summary>Which counter is on screen: <see cref="ShopItem.Battle"/>, <see cref="ShopItem.MegaStones"/> or <see cref="ShopItem.Herbs"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingBattle), nameof(ShowingMegaStones), nameof(ShowingHerbs), nameof(IsClosed))]
    private string _counter = ShopItem.Battle;

    public bool ShowingBattle => Counter == ShopItem.Battle;

    public bool ShowingMegaStones => Counter == ShopItem.MegaStones;

    public bool ShowingHerbs => Counter == ShopItem.Herbs;

    /// <summary>Leaving the shop puts it back on the tab it opens with.</summary>
    public override void ResetState()
    {
        Counter = ShopItem.Battle;
        HerbToUse = null;
    }

    /// <summary>Counts on the tabs, so nobody has to open one to find out it is empty.</summary>
    public string BattleTab => $"COMBATE ({Items.Count(i => CounterOf(i) == ShopItem.Battle)})";

    public string MegaTab => $"MEGAPIEDRAS ({Items.Count(i => CounterOf(i) == ShopItem.MegaStones)})";

    public string HerbTab => $"HIERBAS ({Items.Count(i => CounterOf(i) == ShopItem.Herbs)})";

    private static string CounterOf(ShopItemViewModel card) =>
        card.Item.IsHerb ? ShopItem.Herbs
        : string.Equals(card.Item.Category, ShopItem.MegaStones, StringComparison.OrdinalIgnoreCase) ? ShopItem.MegaStones
        : ShopItem.Battle;

    [RelayCommand]
    private void ShowBattle() => Show(ShopItem.Battle);

    [RelayCommand]
    private void ShowMegaStones() => Show(ShopItem.MegaStones);

    [RelayCommand]
    private void ShowHerbs() => Show(ShopItem.Herbs);

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

    partial void OnSearchChanged(string value) => Show(Counter);

    [RelayCommand]
    private void ClearSearch() => Search = string.Empty;

    private void Show(string counter)
    {
        Counter = counter;
        Visible.Clear();

        foreach (var card in Items.Where(card => CounterOf(card) == counter && Matches(card)))
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

    /// <summary>Why nothing can be bought now (2026-09-28): closed by the organiser or not open until a trial. Empty when open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosed))]
    private string _closed = string.Empty;

    /// <summary>The veil, only over COMBATE: Mega Stones and herbs stay open (the organiser asked).</summary>
    public bool IsClosed => Closed.Length > 0 && ShowingBattle;

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
                Items.Add(new ShopItemViewModel(item, IconOf(item)));
            }

            OnPropertyChanged(nameof(BattleTab));
            OnPropertyChanged(nameof(MegaTab));
            OnPropertyChanged(nameof(HerbTab));
            Show(Counter);
        }

        foreach (var card in Items.Where(card => card.Icon is null))
        {
            card.Icon = IconOf(card.Item);
        }

        if (Items.Count == 0)
        {
            Problem = "No hay nada a la venta: falta Data/shop.json o está vacío.";
            return;
        }

        await RefreshAsync();
    }

    /// <summary>An unlock has no item to draw: it shows the Pokémon it is for (Rayquaza for Ascenso Draco).</summary>
    private BitmapSource? IconOf(ShopItem item) =>
        item.IsHerb ? HerbArt.Make(item.Nature) : item.IsUnlock ? _sprites.Get(item.UnlockSpecies, 0) : _sprites.GetItem(item.Id);

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
            Closed = _shop.ClosedReason(_progress is null ? int.MaxValue : await _progress.ClearedAsync(run)) ?? string.Empty;

            foreach (var card in Items)
            {
                card.Affordable = Balance >= card.Price;
            }

            // Una sola lectura de la mochila para las dieciocho. Preguntar una por una era
            // dieciocho localizaciones del bloque, y con el emulador cerrado, dieciocho fracasos
            // lentos seguidos: la pantalla se quedaba muerta al abrirla.
            using var cancel = new CancellationTokenSource(GameTimeout);
            var carried = await _shop.CarriedAllAsync([.. Items.Where(i => !i.Item.IsUnlock && !i.Item.IsHerb).Select(i => i.Item.Id)], cancel.Token);
            var unlocked = await _shop.UnlockedAsync(run.Id);

            foreach (var card in Items)
            {
                card.Carried = card.Item.IsUnlock
                    ? unlocked.Contains((card.Item.UnlockSpecies, card.Item.UnlockMove)) ? "comprado" : string.Empty
                    : carried.TryGetValue(card.Item.Id, out var count) ? $"llevas {count}" : string.Empty;
            }

            Problem = carried.Count > 0
                ? string.Empty
                : "Abre Azahar con la partida cargada y pulsa ACTUALIZAR.";
        }
        catch (OperationCanceledException)
        {
            Problem = "Azahar no responde. Comprueba que está abierto.";
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

        if (Closed.Length > 0 && card.Item.IsBattle)
        {
            Problem = Closed;
            return;
        }

        if (card.Item.IsHerb)
        {
            await ChooseTargetAsync(card);
            return;
        }

        // SE PREGUNTA ANTES. La tarjeta entera es el boton, que es lo que quita las dieciocho
        // barras violetas de la pantalla, pero tambien hace que un clic de mas cueste puntos: lo
        // que se pulsa paso de un boton pequeño a un objeto grande. Y una compra no se deshace
        // -el objeto se escribe en la mochila del juego-, asi que aqui se pregunta.
        var confirmed = _dialogs.Confirm("Comprar",
            $"¿Comprar {card.Name} por {card.Price} puntos?"
            + $"{Environment.NewLine}{Environment.NewLine}"
            + $"Tienes {Balance} puntos.");

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
            Problem = "Azahar no responde. No se ha comprado nada.";
        }
        finally
        {
            Busy = false;
        }

        await RefreshAsync();
    }

    /// <summary>The herb waiting for a Pokémon (2026-09-27), or null when the picker is closed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPickingTarget), nameof(PickTitle))]
    private ShopItemViewModel? _herbToUse;

    public bool IsPickingTarget => HerbToUse is not null;

    public string PickTitle => HerbToUse is { } herb ? $"¿A QUIÉN LE DAS LA {herb.Name.ToUpperInvariant()}?" : string.Empty;

    /// <summary>The party of the save, for the herb.</summary>
    public ObservableCollection<HerbTargetViewModel> Targets { get; } = [];

    [RelayCommand]
    private void CancelHerb() => HerbToUse = null;

    /// <summary>
    /// Opens the picker with the party of the save. The herb writes the save, so the game has to be closed: said here,
    /// before choosing, and not after.
    /// </summary>
    private async Task ChooseTargetAsync(ShopItemViewModel card)
    {
        if (!_shop.CanChangeNatureNow(out var reason))
        {
            Status = string.Empty;
            Problem = reason;
            return;
        }

        var snapshot = await _boxes.ReadAsync();
        var party = snapshot.Boxes.FirstOrDefault(box => box.IsParty)?.Pokemon ?? [];

        if (party.Count == 0)
        {
            Problem = snapshot.Problem ?? "No se ha podido leer tu equipo de la partida.";
            return;
        }

        Targets.Clear();
        foreach (var pokemon in party.Where(p => !p.IsEgg))
        {
            Targets.Add(new HerbTargetViewModel(pokemon, _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny),
                pokemon.Nature == card.Item.Nature));
        }

        Problem = string.Empty;
        HerbToUse = card;
    }

    [RelayCommand]
    private async Task UseHerbAsync(HerbTargetViewModel? target)
    {
        if (target is null || HerbToUse is not { } herb || Busy || _runs.Current is not { } run)
        {
            return;
        }

        var confirmed = _dialogs.Confirm("Hierba",
            $"¿Dar la {herb.Name} a {target.Name} por {herb.Price} puntos?"
            + $"{Environment.NewLine}{Environment.NewLine}"
            + $"Ahora es {target.Pokemon.NatureName}. El cambio es para siempre. Tienes {Balance} puntos.");

        _logger.LogInformation("Tienda: {Herb} para {Pokemon}, {Answer}", herb.Name, target.Name, confirmed ? "confirmado" : "cancelado");

        if (!confirmed)
        {
            return;
        }

        Busy = true;
        try
        {
            var result = await _shop.ChangeNatureAsync(run, herb.Item.Id, target.Pokemon);
            Status = result.Succeeded ? result.Message : string.Empty;
            Problem = result.Succeeded ? string.Empty : result.Message;
            Balance = result.Balance;

            if (result.Succeeded)
            {
                HerbToUse = null;
            }
        }
        finally
        {
            Busy = false;
        }

        foreach (var card in Items)
        {
            card.Affordable = Balance >= card.Price;
        }
    }
}

/// <summary>One Pokémon of the party in the herb picker.</summary>
public sealed class HerbTargetViewModel(BoxedPokemon pokemon, BitmapSource? sprite, bool alreadyHasIt)
{
    public BoxedPokemon Pokemon { get; } = pokemon;

    public string Name => Pokemon.DisplayName;

    public BitmapSource? Sprite { get; } = sprite;

    public string Nature => AlreadyHasIt ? $"{Pokemon.NatureName.ToUpperInvariant()} (YA LA TIENE)" : Pokemon.NatureName.ToUpperInvariant();

    public bool AlreadyHasIt { get; } = alreadyHasIt;

    public bool CanPick => !AlreadyHasIt;
}
