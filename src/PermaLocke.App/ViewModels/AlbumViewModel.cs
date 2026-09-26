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
    private readonly ITypeLookup _types;
    private readonly IMoveCatalog _moves;
    private readonly IStatForecast _forecast;
    private readonly GachaService _gacha;
    private readonly ISpeciesStatsCatalog _species;
    private readonly ILogger<AlbumViewModel> _logger;

    /// <summary>The cards of each binder by slot, in the order of <see cref="Binders"/>.</summary>
    private readonly List<TcgCard?[]> _cards = [];

    private IReadOnlyList<AlbumSpreadPlace> _spreads = [];
    private int _position;
    private Dictionary<int, int>? _stages;
    private List<TcgCard> _inspecting = [];
    private int _inspectedIndex;
    private bool _moving;

    public AlbumViewModel(IBoxReader boxes, PokemonSpriteService sprites, IRunContext runContext,
        IPokemonRepository registered, IEventStore events, ITypeLookup types, IMoveCatalog moves, IStatForecast forecast,
        GachaService gacha, ISpeciesStatsCatalog species, ILogger<AlbumViewModel> logger)
        : base("ÁLBUM", "Tus Pokémon como cartas")
    {
        _boxes = boxes;
        _sprites = sprites;
        _runContext = runContext;
        _registered = registered;
        _events = events;
        _types = types;
        _moves = moves;
        _forecast = forecast;
        _gacha = gacha;
        _species = species;
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
            _cards.Clear();
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
                    slots[pokemon.Slot] = Card(pokemon, fallen, pulled);
                }

                _cards.Add(slots);
            }

            IsAvailable = true;
            OnPropertyChanged(nameof(IsEmpty));

            var all = _cards.SelectMany(slots => slots).OfType<TcgCard>().ToList();
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
    private TcgCard Card(BoxedPokemon pokemon, HashSet<uint> fallen, Dictionary<uint, int> pulled)
    {
        var fromGacha = pulled.TryGetValue(pokemon.Pid, out var pulledTier);
        var sprite = RoomSprite.From(pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny));

        if (pokemon.IsEgg)
        {
            return new TcgCard("Huevo", "Huevo", pokemon.Species, string.Empty, 0, 0, [], [], string.Empty, string.Empty,
                -1, -1, string.Empty, string.Empty, 0, -1, fromGacha, false, true, fallen.Contains(pokemon.Pid), sprite,
                [], [], [], pokemon.Pid);
        }

        var pair = _types.GetTypes(pokemon.Species, pokemon.Form);
        IReadOnlyList<int> types = pair.IsDual ? [pair.First, pair.Second] : [pair.First];

        // Las mismas cifras que el visor: en caja, las del mundo instalado; en el equipo, las que guarda el juego.
        var stats = (pokemon.IsInParty ? null : _forecast.With(pokemon, pokemon.Evs)) ?? pokemon.Stats;

        var up = -1;
        var down = -1;
        for (var stat = 0; stat < 6; stat++)
        {
            var effect = _forecast.NatureEffect(pokemon, stat);
            if (effect > 0) up = stat;
            if (effect < 0) down = stat;
        }

        var moves = new List<TcgMove>();
        var ids = pokemon.MoveIds ?? [];
        for (var slot = 0; slot < 4; slot++)
        {
            var id = slot < ids.Count ? ids[slot] : 0;
            if (id != 0 && _moves.Describe(id) is { } sheet)
            {
                moves.Add(new TcgMove(sheet.Name, sheet.Type, sheet.Power, sheet.Accuracy, sheet.PP, sheet.CategoryName));
            }
            else if (id != 0 && slot < pokemon.Moves.Count && pokemon.Moves[slot].Length > 0)
            {
                moves.Add(new TcgMove(pokemon.Moves[slot], -1, 0, 0, 0, string.Empty));
            }
        }

        var name = string.IsNullOrWhiteSpace(pokemon.Nickname) ? pokemon.SpeciesName : pokemon.Nickname;

        return new TcgCard(
            name,
            pokemon.SpeciesName,
            pokemon.Species,
            StageOf(pokemon.Species),
            pokemon.Level,
            stats.Count > 0 ? stats[0] : 0,
            types,
            moves,
            pokemon.AbilityName,
            pokemon.NatureName,
            up,
            down,
            pokemon.HeldItemName,
            pokemon.MetLocationName,
            pokemon.MetLevel,
            fromGacha ? pulledTier : _gacha.TierIndexOf(pokemon.Species),
            fromGacha,
            pokemon.IsShiny,
            false,
            fallen.Contains(pokemon.Pid),
            sprite,
            stats,
            pokemon.Ivs,
            pokemon.Evs,
            pokemon.Pid);
    }

    /// <summary>«BÁSICO», «FASE 1» or «FASE 2»: the rung of its family it stands on, from the cartridge's families.</summary>
    private string StageOf(int species)
    {
        if (_stages is null)
        {
            _stages = [];
            foreach (var line in _species.Lines)
            {
                for (var stage = 0; stage < line.Stages.Count; stage++)
                {
                    foreach (var id in line.Stages[stage])
                    {
                        _stages.TryAdd(id, stage);
                    }
                }
            }
        }

        return _stages.GetValueOrDefault(species) switch
        {
            0 => "Básico",
            1 => "Fase 1",
            _ => "Fase 2"
        };
    }

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
        var slots = _cards[place.Binder];
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
    private void Open(TcgCard? card)
    {
        if (card is null || _spreads.Count == 0)
        {
            return;
        }

        _inspecting = [.. _cards[_spreads[_position].Binder].OfType<TcgCard>()];
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
