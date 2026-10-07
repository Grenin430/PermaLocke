using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>A box of the PC, or the party, as a section of the album.</summary>
public sealed record AlbumBinder(int Number, string Name, int Count, int Slots, bool IsParty)
{
    public string Label => IsParty ? "EQUIPO" : $"CAJA {Number} · {Name}";

    public override string ToString() => Label;
}

/// <summary>
/// ÁLBUM (§186): the Pokémon of the save as trading cards in a binder, box by box. Only to look at: it writes nothing,
/// and has no wonder trade or training of its own.
/// </summary>
/// <remarks>
/// <para>
/// Read from the save file, like the viewer, so it shows the last thing saved and works with the game open or closed.
/// Which cards are burnt is the run's, by PID. The rarity is the gacha tier the Pokémon came out of when it came out of
/// the gacha (the <c>rareza</c> of its <c>GachaRoll</c>), and otherwise the tier its species is by the gacha's own rule
/// (<see cref="GachaService.TierOf"/>).
/// </para>
/// </remarks>
public sealed partial class AlbumViewModel : SectionViewModel
{
    private readonly IBoxReader _boxes;
    private readonly PokemonSpriteService _sprites;
    private readonly IRunContext _runContext;
    private readonly IPokemonRepository _registered;
    private readonly IEventStore _events;
    private readonly TcgCardFactory _cards;
    private readonly GachaService _gacha;
    private readonly WonderTradeService _trades;
    private readonly IPokemonSwap _swap;
    private readonly PokemonIdentityService _identity;
    private readonly CreditService _credits;

    /// <summary>The Pokémon of the save behind each card, for the trade (1.0.4.7).</summary>
    private readonly Dictionary<TcgCard, BoxedPokemon> _owners = new(ReferenceEqualityComparer.Instance);
    private readonly ILogger<AlbumViewModel> _logger;

    /// <summary>The cards of each binder by slot, in the order of <see cref="Binders"/>.</summary>
    private readonly List<TcgCard?[]> _pockets = [];

    private IReadOnlyList<AlbumSpreadPlace> _spreads = [];
    private int _position;
    private List<TcgCard> _inspecting = [];
    private int _inspectedIndex;
    private bool _moving;

    public AlbumViewModel(IBoxReader boxes, PokemonSpriteService sprites, IRunContext runContext,
        IPokemonRepository registered, IEventStore events, TcgCardFactory cards, GachaService gacha,
        WonderTradeService trades, IPokemonSwap swap, PokemonIdentityService identity, CreditService credits,
        ILogger<AlbumViewModel> logger)
        : base("ÁLBUM", "Tus Pokémon como cartas")
    {
        _boxes = boxes;
        _sprites = sprites;
        _runContext = runContext;
        _registered = registered;
        _events = events;
        _cards = cards;
        _gacha = gacha;
        _trades = trades;
        _swap = swap;
        _identity = identity;
        _credits = credits;
        _logger = logger;
    }

    public override string IconKey => "IconStar";

    /// <summary>
    /// Only reads the save, open or closed, so it asks nothing of the emulator: without the band on top, the album gets
    /// the height it needs to stay at two screen pixels per cell in GRANDE (§187). When the game is open the save may be
    /// older than what is on screen, and <see cref="Notice"/> says so in the bar.
    /// </summary>
    public override GameNeed Needs => GameNeed.None;

    /// <summary>The party and every box with something in it, in the album's order.</summary>
    public ObservableCollection<AlbumBinder> Binders { get; } = [];

    [ObservableProperty]
    private AlbumBinder? _selectedBinder;

    /// <summary>3×3 pages of the full card (true) or 4×4 of the small one (false).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layout))]
    [NotifyPropertyChangedFor(nameof(SmallCards))]
    private bool _bigCards = true;

    /// <summary>The other of the two, for the 4×4 button.</summary>
    public bool SmallCards => !BigCards;

    public TcgLayout Layout => BigCards ? TcgLayout.Full : TcgLayout.Mini;

    private int PerPage => BigCards ? 9 : 16;

    /// <summary>The two pages open now.</summary>
    [ObservableProperty]
    private AlbumSpread? _spread;

    /// <summary>«PÁGINAS 1-2 DE 4».</summary>
    [ObservableProperty]
    private string _pageLabel = string.Empty;

    /// <summary>The card out of its pocket, big, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspecting))]
    private TcgCard? _inspected;

    public bool IsInspecting => Inspected is not null;

    /// <summary>«3 DE 18» for the card in the hand, among the cards of its box.</summary>
    [ObservableProperty]
    private string _inspectedCaption = string.Empty;

    /// <summary>«DRAGONITE · RARA DORADA · VARIOCOLOR»: what the card in the hand is, in words.</summary>
    [ObservableProperty]
    private string _inspectedTitle = string.Empty;

    /// <summary>The name of each rarity, as the ★ of its card says it.</summary>
    private static readonly string[] RarityNames = ["COMÚN", "POCO COMÚN", "RARA HOLO", "RARA HOLO INVERSA", "RARA DORADA"];

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isAvailable;

    /// <summary>The save was read and there is not a single Pokémon in it.</summary>
    public bool IsEmpty => IsAvailable && Binders.Count == 0;

    [ObservableProperty]
    private string _problem = string.Empty;

    [ObservableProperty]
    private string _notice = string.Empty;

    public string Message => Problem.Length > 0 ? Problem : Notice;

    public bool HasMessage => Message.Length > 0;

    /// <summary>A failure to read the save: said big, where the album would be.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Something true that is not a failure, such as the save being older than the game: said small in the bar.</summary>
    public bool HasNotice => Problem.Length == 0 && Notice.Length > 0;

    partial void OnProblemChanged(string value) => MessageChanged();

    partial void OnNoticeChanged(string value) => MessageChanged();

    private void MessageChanged()
    {
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(HasMessage));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(HasNotice));
    }

    public override Task ActivateAsync() => LoadAsync();

    /// <summary>Leaving the album puts the card in the hand back in its pocket.</summary>
    public override void ResetState() => Close();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        // Se vuelve a la misma caja y a la misma página si sigue ahí.
        var keepBinder = SelectedBinder?.Number;
        var keepPage = _spreads.Count > 0 ? _spreads[Math.Clamp(_position, 0, _spreads.Count - 1)].Page : 0;

        try
        {
            await _sprites.PrepareAsync();
            var snapshot = await _boxes.ReadAsync();

            Problem = snapshot.Problem ?? string.Empty;
            Notice = snapshot.Notice ?? string.Empty;

            _moving = true;
            Binders.Clear();
            _pockets.Clear();
            _owners.Clear();
            Inspected = null;

            if (!snapshot.Available)
            {
                IsAvailable = false;
                Spread = null;
                PageLabel = string.Empty;
                Summary = string.Empty;
                OnPropertyChanged(nameof(IsEmpty));
                return;
            }

            var (fallen, pulled) = await ReadRunAsync();

            // El equipo primero, como la primera página del álbum; luego las cajas que tienen algo.
            var ordered = snapshot.Boxes.Where(box => box.IsParty).Concat(snapshot.Boxes.Where(box => !box.IsParty));
            foreach (var box in ordered.Where(box => box.Count > 0))
            {
                Binders.Add(new AlbumBinder(box.Number, box.Name, box.Count, box.Slots, box.IsParty));

                var slots = new TcgCard?[box.Slots];
                foreach (var pokemon in box.Pokemon.Where(p => p.Slot >= 0 && p.Slot < box.Slots))
                {
                    var card = Card(pokemon, fallen, pulled);
                    slots[pokemon.Slot] = card;
                    _owners[card] = pokemon;
                }

                _pockets.Add(slots);
            }

            IsAvailable = true;
            OnPropertyChanged(nameof(IsEmpty));

            var all = _pockets.SelectMany(slots => slots).OfType<TcgCard>().ToList();
            var shiny = all.Count(card => card.Shiny);
            var burnt = all.Count(card => card.Fallen);
            Summary = $"{all.Count} {(all.Count == 1 ? "carta" : "cartas")} · {shiny} variocolor · {burnt} {(burnt == 1 ? "quemada" : "quemadas")}";

            Repage();
            var binder = Binders.ToList().FindIndex(b => b.Number == keepBinder);
            GoTo(binder >= 0 ? SpreadOf(binder, keepPage) : 0);

            _logger.LogInformation("Álbum: {Cards} cartas en {Binders} carpetas", all.Count, Binders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al montar el álbum");
            Problem = "No se ha podido leer la partida para el álbum.";
            IsAvailable = false;
            Spread = null;
        }
        finally
        {
            _moving = false;
            IsLoading = false;
        }
    }

    /// <summary>Who the run lost, by PID, and the gacha tier of what came out of the gacha, by PID.</summary>
    private async Task<(HashSet<uint> Fallen, Dictionary<uint, int> Pulled)> ReadRunAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return ([], []);
        }

        var entries = await _registered.GetAllAsync(run.Id);
        var fallen = entries.Where(p => p.Status == PokemonStatus.Dead && p.Pid is not null).Select(p => p.Pid!.Value).ToHashSet();
        var pidOf = entries.Where(p => p.Pid is not null).ToDictionary(p => p.Id, p => p.Pid!.Value);
        var tiers = _gacha.Tiers.Select(tier => tier.Id).ToList();

        var pulled = new Dictionary<uint, int>();
        foreach (var roll in (await _events.GetAllAsync(run.Id)).Where(e => e.Type == GameEventType.GachaRoll))
        {
            if (roll.PokemonId is { } id && pidOf.TryGetValue(id, out var pid)
                && roll.Data.TryGetValue("rareza", out var tierId) && tiers.IndexOf(tierId) is var index and >= 0)
            {
                pulled[pid] = index;
            }
        }

        return (fallen, pulled);
    }

    /// <summary>One Pokémon of the save as its card.</summary>
    private TcgCard Card(BoxedPokemon pokemon, HashSet<uint> fallen, Dictionary<uint, int> pulled) =>
        _cards.Make(pokemon, fallen.Contains(pokemon.Pid), pulled.TryGetValue(pokemon.Pid, out var tier) ? tier : null);

    // ====================================================================================================== PAGES

    private void Repage() => _spreads = AlbumPaging.Spreads([.. Binders.Select(b => b.Slots)], PerPage);

    private int SpreadOf(int binder, int page)
    {
        var index = _spreads.ToList().FindLastIndex(s => s.Binder == binder && s.Page <= page);
        return index >= 0 ? index : Math.Max(0, _spreads.ToList().FindIndex(s => s.Binder == binder));
    }

    private void GoTo(int position)
    {
        if (_spreads.Count == 0)
        {
            Spread = null;
            PageLabel = string.Empty;
            return;
        }

        _position = Math.Clamp(position, 0, _spreads.Count - 1);
        var place = _spreads[_position];
        var slots = _pockets[place.Binder];
        var binder = Binders[place.Binder];

        // Las pestañas del canto: «EQ» para el equipo y el número de cada caja.
        var tabs = Binders.Select(b => new AlbumTab(b.IsParty ? "EQ" : b.Number.ToString(), b.IsParty)).ToList();

        Spread = new AlbumSpread(
            new AlbumPage(AlbumPaging.Page(slots, place.Page, PerPage), binder.Label, place.Page + 1),
            new AlbumPage(AlbumPaging.Page(slots, place.Page + 1, PerPage), string.Empty, place.Page + 2),
            Layout,
            _position,
            tabs,
            place.Binder);

        var pages = AlbumPaging.PagesOf(Binders[place.Binder].Slots, PerPage);
        PageLabel = $"PÁGINAS {place.Page + 1}-{place.Page + 2} DE {pages}";

        _moving = true;
        SelectedBinder = Binders[place.Binder];
        _moving = false;

        NextSpreadCommand.NotifyCanExecuteChanged();
        PreviousSpreadCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoNext() => _position < _spreads.Count - 1;

    private bool CanGoBack() => _position > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextSpread() => GoTo(_position + 1);

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousSpread() => GoTo(_position - 1);

    partial void OnSelectedBinderChanged(AlbumBinder? value)
    {
        if (_moving || value is null)
        {
            return;
        }

        var binder = Binders.IndexOf(value);
        if (binder >= 0)
        {
            GoTo(SpreadOf(binder, 0));
        }
    }

    partial void OnBigCardsChanged(bool value)
    {
        if (_spreads.Count == 0)
        {
            return;
        }

        // El mismo sitio con el otro tamaño: la página nueva que tiene el primer hueco que se veía.
        var place = _spreads[_position];
        var oldPerPage = value ? 16 : 9;
        var slot = place.Page * oldPerPage;

        Repage();
        GoTo(SpreadOf(place.Binder, slot / PerPage));
    }

    /// <summary>A tab of the album's edge: opens that box at its first page.</summary>
    [RelayCommand]
    private void OpenTab(int index)
    {
        if (index >= 0 && index < Binders.Count)
        {
            GoTo(SpreadOf(index, 0));
        }
    }

    [RelayCommand]
    private void ShowBig() => BigCards = true;

    [RelayCommand]
    private void ShowSmall() => BigCards = false;

    // ====================================================================================================== HAND

    [RelayCommand]
    private async Task OpenAsync(TcgCard? card)
    {
        if (card is null || _spreads.Count == 0)
        {
            return;
        }

        // Con el intercambio abierto, un clic en la página elige la carta (o la quita) en vez de sacarla a la mano (1.0.4.7).
        if (IsTrading)
        {
            await TogglePickAsync(card);
            return;
        }

        _inspecting = [.. _pockets[_spreads[_position].Binder].OfType<TcgCard>()];
        _inspectedIndex = Math.Max(0, _inspecting.IndexOf(card));
        Show();
    }

    [RelayCommand]
    private void Close()
    {
        Inspected = null;
        InspectedCaption = string.Empty;
        InspectedTitle = string.Empty;
    }

    [RelayCommand]
    private void NextCard() => Step(1);

    [RelayCommand]
    private void PreviousCard() => Step(-1);

    private void Step(int direction)
    {
        if (_inspecting.Count == 0 || Inspected is null)
        {
            return;
        }

        // Da la vuelta dentro de la caja, como el PC del juego.
        _inspectedIndex = (_inspectedIndex + direction + _inspecting.Count) % _inspecting.Count;
        Show();
    }

    /// <summary>The left arrow: the previous card with one in the hand, the previous spread otherwise.</summary>
    [RelayCommand]
    private void Left()
    {
        if (IsInspecting) Step(-1);
        else if (CanGoBack()) GoTo(_position - 1);
    }

    [RelayCommand]
    private void Right()
    {
        if (IsInspecting) Step(1);
        else if (CanGoNext()) GoTo(_position + 1);
    }

    // ===================================================================================================== TRADE

    /// <summary>The trade bar is open: the card in the hand can be picked, one (2026-10-07; before, two for one).</summary>
    [ObservableProperty]
    private bool _isTrading;

    /// <summary>The card handed over, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TradeHint), nameof(CanTrade), nameof(TradeMarked))]
    private TcgCard? _tradeFirst;

    /// <summary>Card trades left, from the credits of the run.</summary>
    [ObservableProperty]
    private int _tradesLeft;

    [ObservableProperty]
    private string _tradeProblem = string.Empty;

    [ObservableProperty]
    private bool _tradeBusy;

    /// <summary>What the animation plays; null when nothing is playing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTradePlaying))]
    private CardTradePlay? _tradePlay;

    public bool IsTradePlaying => TradePlay is not null;

    /// <summary>The result is on screen: its name and CONTINUAR come up.</summary>
    [ObservableProperty]
    private bool _tradeRevealed;

    [ObservableProperty]
    private string _tradeResultText = string.Empty;

    public bool TradesLimited => _credits.LimitsWonderTrades;

    public bool CanTrade => TradeFirst is not null && !TradeBusy;

    /// <summary>The picked card, for the gold frame on the page.</summary>
    public IReadOnlyList<TcgCard> TradeMarked => [.. new[] { TradeFirst }.OfType<TcgCard>()];

    /// <summary>What the bar says: which card goes, and what band comes back.</summary>
    public string TradeHint
    {
        get
        {
            if (TradeFirst is null)
            {
                return "Pulsa una carta del álbum para elegirla. Entregas una y recibes otra.";
            }

            var gift = Gift(_owners[TradeFirst]);
            var monoType = _runContext.Current is { } run ? _trades.MonoTypeOf(run) : null;
            var (min, max) = _trades.BandFor(_trades.BaseStatTotalOf(gift.Species), monoType);
            return $"Recibirás un Pokémon de nivel {gift.Level} con estadísticas totales entre {min} y {max}"
                   + (monoType is null ? "." : ", de tu tipo.");
        }
    }

    private static WonderTradeGift Gift(BoxedPokemon p) => new(p.Species, p.DisplayName, p.Level, p.Box, p.Slot, p.Pid);

    [RelayCommand]
    private async Task ToggleTradeAsync()
    {
        IsTrading = !IsTrading;
        TradeFirst = null;
        TradeProblem = string.Empty;

        if (IsTrading)
        {
            await RefreshTradesAsync();
            if (!_swap.CanSwapNow(out var reason)) TradeProblem = reason;
        }
    }

    private async Task RefreshTradesAsync()
    {
        try
        {
            TradesLeft = _runContext.Current is { } run ? (await _credits.AvailableAsync(run)).WonderTrades : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al contar los intercambios disponibles");
        }
    }

    /// <summary>The card in the hand is the one to hand over, or comes out if it already was.</summary>
    [RelayCommand]
    private Task PickAsync() => Inspected is { } card ? TogglePickAsync(card) : Task.CompletedTask;

    private async Task TogglePickAsync(TcgCard card)
    {
        if (!IsTrading || !_owners.TryGetValue(card, out var pokemon))
        {
            return;
        }

        TradeProblem = string.Empty;

        if (ReferenceEquals(card, TradeFirst)) { TradeFirst = null; return; }

        if (card.Egg)
        {
            TradeProblem = "Un huevo no se puede intercambiar.";
            return;
        }

        if (_runContext.Current is { } run && await _trades.IsFallenAsync(run.Id, pokemon.Pid))
        {
            TradeProblem = WonderTradeService.FallenMessage(pokemon.DisplayName);
            return;
        }

        TradeFirst = card;

        if (IsInspecting) Close();
    }

    [RelayCommand]
    private void Unpick() => TradeFirst = null;

    [RelayCommand]
    private async Task TradeAsync()
    {
        if (TradeFirst is not { } one || _runContext.Current is not { } run)
        {
            return;
        }

        TradeBusy = true;
        OnPropertyChanged(nameof(CanTrade));

        try
        {
            if (!_swap.CanSwapNow(out var reason))
            {
                TradeProblem = reason;
                return;
            }

            if (_credits.LimitsWonderTrades && TradesLeft <= 0)
            {
                TradeProblem = "No te quedan intercambios. Consigues más superando pruebas.";
                return;
            }

            var given = _owners[one];
            var result = await _trades.TradeAsync(run, Gift(given), free: _credits.LimitsWonderTrades);

            if (!result.Success || result.Offer is not { } offer)
            {
                TradeProblem = result.Error ?? "El intercambio no se ha podido completar.";
                return;
            }

            // Primero la partida; sin escritura no hay animación que enseñe un Pokémon que no tienes.
            var written = await _swap.SwapAsync(offer, given.Box, given.Slot);
            if (!written.Delivered)
            {
                TradeProblem = written.Message;
                return;
            }

            if (result.Entry is { } entry)
            {
                await _identity.RememberDeliveryAsync(run, entry, written.Pid, written.Box, written.Slot);
            }

            await _trades.MarkGivenAsTradedAsync(run, given.Pid, offer.DisplayName);
            _logger.LogInformation("Intercambio de cartas: {A} por {C}", given.DisplayName, offer.DisplayName);

            // La carta nueva, leída de la partida recién escrita, con su rareza de verdad.
            var snapshot = await _boxes.ReadAsync();
            var received = snapshot.Boxes.SelectMany(box => box.Pokemon).FirstOrDefault(p => p.Pid == written.Pid);
            var resultCard = received is null ? one : _cards.Make(received);

            TradeResultText = $"{offer.DisplayName.ToUpperInvariant()} · NV. {offer.Level} · {offer.BaseStatTotal} "
                              + $"({(offer.Difference >= 0 ? "+" : string.Empty)}{offer.Difference} %)"
                              + (offer.IsShiny ? " · VARIOCOLOR" : string.Empty);
            TradeRevealed = false;
            TradePlay = new CardTradePlay(one, resultCard);

            await Task.Delay(TimeSpan.FromSeconds(CardTradeTimeline.Rest + 0.3));
            TradeRevealed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el intercambio de cartas");
            TradeProblem = "Ha fallado el intercambio.";
        }
        finally
        {
            TradeBusy = false;
            OnPropertyChanged(nameof(CanTrade));
            await RefreshTradesAsync();
        }
    }

    /// <summary>
    /// <c>--ensayar-intercambio</c>: the animation with two cards of the save and nothing written, to look at it.
    /// </summary>
    public async Task RehearseTradeAsync()
    {
        // La sección puede estar leyendo la partida al abrirse: se espera a que acabe.
        while (IsLoading) await Task.Delay(100);
        if (_owners.Count == 0) await LoadAsync();
        var cards = _owners.Keys.Where(card => !card.Egg && !card.Fallen).Take(2).ToList();

        if (cards.Count < 2)
        {
            TradeProblem = "Hacen falta dos cartas en la partida para el ensayo.";
            return;
        }

        TradeResultText = $"{cards[1].Name.ToUpperInvariant()} · ENSAYO: NO SE HA CAMBIADO NADA";
        TradeRevealed = false;
        TradePlay = new CardTradePlay(cards[0], cards[1]);
        await Task.Delay(TimeSpan.FromSeconds(CardTradeTimeline.Rest + 0.3));
        TradeRevealed = true;
    }

    /// <summary>After the reveal: the animation goes, the trade bar empties and the album is read again.</summary>
    [RelayCommand]
    private async Task EndTradeAsync()
    {
        TradePlay = null;
        TradeRevealed = false;
        TradeFirst = null;
        IsTrading = false;
        await LoadAsync();
    }

    /// <summary>The album or the card in the hand could not be drawn; the screen stays still and the log says why.</summary>
    public void AnimationFailed(Exception ex) => _logger.LogError(ex, "Falló el dibujo del álbum");

    private void Show()
    {
        var card = _inspecting[_inspectedIndex];
        Inspected = card;
        InspectedCaption = $"{_inspectedIndex + 1} DE {_inspecting.Count}";
        InspectedTitle = TitleOf(card);
    }

    /// <summary>What a card is, in words: its name, its rarity and what else makes it special.</summary>
    public static string TitleOf(TcgCard card)
    {
        if (card.Egg)
        {
            return "HUEVO";
        }

        var parts = new List<string> { card.Name.ToUpperInvariant() };
        if (card.Rarity >= 0 && card.Rarity < RarityNames.Length)
        {
            parts.Add(RarityNames[card.Rarity]);
        }

        if (card.Shiny) parts.Add("VARIOCOLOR");
        if (card.FromGacha) parts.Add("DEL GACHA");
        if (card.Fallen) parts.Add("CAÍDO");
        return string.Join(" · ", parts);
    }
}
