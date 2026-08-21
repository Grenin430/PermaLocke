using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>One hole of the PC: a Pokémon and its icon, or nothing at all.</summary>
/// <remarks>
/// Empty holes are view models too. The PC of the game has thirty of them whether they are full
/// or not, and drawing only the occupied ones would turn a box into a ragged list.
/// </remarks>
public sealed partial class BoxSlotViewModel(BoxedPokemon? pokemon, BitmapSource? sprite) : ObservableObject
{
    public BoxedPokemon? Pokemon { get; } = pokemon;

    public BitmapSource? Sprite { get; } = sprite;

    public bool IsEmpty => Pokemon is null;

    public bool HasSprite => Sprite is not null;

    /// <summary>Shown when there is a Pokémon but no icon for it, so the hole is never silent.</summary>
    public string Fallback => Pokemon is null ? string.Empty : "?";
}

/// <summary>A box as the selector lists it.</summary>
public sealed record BoxTabViewModel(int Number, string Name, int Count)
{
    public string Label => $"{Number}. {Name}";

    public string Occupancy => $"{Count}/30";
}

/// <summary>One row of the stat table: what the game shows, and what it is made of.</summary>
public sealed record StatRowViewModel(string Name, int Value, int Iv, int Ev);

/// <summary>
/// The PC of the player's game: every box, every Pokémon, and the detail of whichever one is
/// clicked.
/// </summary>
/// <remarks>
/// Read only, and it says where the reading comes from. The boxes are read from the save file,
/// so what is on screen is the last thing the player saved; when the game is open the screen
/// says so rather than passing a stale PC off as the live one.
/// </remarks>
public sealed partial class PokemonViewerViewModel : SectionViewModel
{
    private readonly IBoxReader _boxes;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<PokemonViewerViewModel> _logger;

    private static readonly string[] StatNames =
        ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    private BoxSnapshot? _snapshot;

    public PokemonViewerViewModel(IBoxReader boxes, PokemonSpriteService sprites,
        WonderTradeViewModel trade, ILogger<PokemonViewerViewModel> logger) : base("VISOR POKÉMON")
    {
        _boxes = boxes;
        _sprites = sprites;
        _logger = logger;
        Trade = trade;

        // Un intercambio cambia la caja por debajo, así que lo que hay en pantalla deja de ser
        // cierto en cuanto termina.
        Trade.Finished += async (_, _) => await LoadAsync();
    }

    /// <summary>The wonder trade, which picks its victim from the box on this screen.</summary>
    public WonderTradeViewModel Trade { get; }

    /// <summary>Every box of the PC, in the order the game numbers them.</summary>
    public ObservableCollection<BoxTabViewModel> Boxes { get; } = [];

    /// <summary>The thirty holes of the box on screen, empty ones included.</summary>
    public ObservableCollection<BoxSlotViewModel> Slots { get; } = [];

    /// <summary>The six stats of whatever is selected.</summary>
    public ObservableCollection<StatRowViewModel> Stats { get; } = [];

    [ObservableProperty]
    private BoxTabViewModel? _selectedBox;

    [ObservableProperty]
    private BoxSlotViewModel? _selectedSlot;

    /// <summary>The Pokémon whose detail is on screen, or null when nothing is selected.</summary>
    [ObservableProperty]
    private BoxedPokemon? _selected;

    [ObservableProperty]
    private BitmapSource? _selectedSprite;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>Why the PC cannot be shown. Empty when it can.</summary>
    [ObservableProperty]
    private string _problem = string.Empty;

    /// <summary>Something true and worth saying that is not a failure, such as a stale reading.</summary>
    [ObservableProperty]
    private string _notice = string.Empty;

    /// <summary>Whichever of the two there is to say, since only one banner shows them.</summary>
    public string Message => Problem.Length > 0 ? Problem : Notice;

    public bool HasMessage => Message.Length > 0;

    partial void OnProblemChanged(string value) => MessageChanged();

    partial void OnNoticeChanged(string value) => MessageChanged();

    private void MessageChanged()
    {
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(HasMessage));
    }

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isAvailable;

    public override Task ActivateAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        try
        {
            // Los iconos salen de la ROM del propio jugador. Si no se puede, la pantalla sigue
            // funcionando: enseña las cajas sin dibujos.
            await _sprites.PrepareAsync();

            _snapshot = await _boxes.ReadAsync();

            Problem = _snapshot.Problem ?? string.Empty;
            Notice = _snapshot.Notice ?? string.Empty;
            IsAvailable = _snapshot.Available;

            Boxes.Clear();
            Select(null);

            if (!_snapshot.Available)
            {
                Slots.Clear();
                Summary = string.Empty;
                return;
            }

            foreach (var box in _snapshot.Boxes)
            {
                Boxes.Add(new BoxTabViewModel(box.Number, box.Name, box.Count));
            }

            Summary = $"{_snapshot.Total} Pokémon en el PC de {_snapshot.TrainerName}";

            // La primera caja con algo dentro: abrir en una vacía cuando hay Pokémon en la
            // siguiente hace pensar que no se ha leído nada.
            SelectedBox = Boxes.FirstOrDefault(box => box.Count > 0) ?? Boxes.FirstOrDefault();

            _logger.LogInformation("Visor: {Total} Pokémon en {Boxes} cajas",
                _snapshot.Total, _snapshot.Boxes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer las cajas del PC");
            Problem = "No se han podido leer las cajas. El detalle está en la carpeta Logs.";
            IsAvailable = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PreviousBox() => StepBox(-1);

    [RelayCommand]
    private void NextBox() => StepBox(1);

    private void StepBox(int direction)
    {
        if (Boxes.Count == 0 || SelectedBox is null)
        {
            return;
        }

        var index = Boxes.IndexOf(SelectedBox);
        // Da la vuelta, como el PC del juego.
        SelectedBox = Boxes[(index + direction + Boxes.Count) % Boxes.Count];
    }

    partial void OnSelectedBoxChanged(BoxTabViewModel? value)
    {
        Slots.Clear();
        Select(null);

        if (value is null || _snapshot is null)
        {
            return;
        }

        var contents = _snapshot.Boxes.FirstOrDefault(box => box.Number == value.Number);
        if (contents is null)
        {
            return;
        }

        for (var slot = 0; slot < _snapshot.SlotsPerBox; slot++)
        {
            var pokemon = contents.Pokemon.FirstOrDefault(p => p.Slot == slot);
            Slots.Add(new BoxSlotViewModel(pokemon, pokemon is null ? null : SpriteFor(pokemon)));
        }
    }

    partial void OnSelectedSlotChanged(BoxSlotViewModel? value) => Select(value?.Pokemon);

    private void Select(BoxedPokemon? pokemon)
    {
        Selected = pokemon;
        HasSelection = pokemon is not null;
        SelectedSprite = pokemon is null ? null : SpriteFor(pokemon);

        // Con el intercambio armado, elegir en la caja es elegir a quién se entrega. Un huevo no:
        // lo que hay dentro no se sabe, así que no se puede decir qué vale.
        Trade.Choose(pokemon is { IsEgg: false } ? pokemon : null);

        Stats.Clear();

        if (pokemon is null)
        {
            return;
        }

        for (var index = 0; index < StatNames.Length; index++)
        {
            Stats.Add(new StatRowViewModel(StatNames[index],
                pokemon.Stats[index], pokemon.Ivs[index], pokemon.Evs[index]));
        }
    }

    /// <summary>
    /// An egg shows the egg icon, not the icon of what is inside: the game does not tell the
    /// player either, and showing the species would give away something the game hides.
    /// </summary>
    private BitmapSource? SpriteFor(BoxedPokemon pokemon) =>
        pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species);
}
