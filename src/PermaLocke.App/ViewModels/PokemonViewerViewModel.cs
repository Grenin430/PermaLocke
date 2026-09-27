using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.ViewModels;

/// <summary>One hole of the PC: a Pokémon and its icon, or nothing at all.</summary>
/// <remarks>
/// Empty holes are view models too. The PC of the game has thirty of them whether they are full
/// or not, and drawing only the occupied ones would turn a box into a ragged list.
/// </remarks>
public sealed partial class BoxSlotViewModel(BoxedPokemon? pokemon, BitmapSource? sprite,
    bool isDead = false) : ObservableObject
{
    public BoxedPokemon? Pokemon { get; } = pokemon;

    public BitmapSource? Sprite { get; } = sprite;

    public bool IsEmpty => Pokemon is null;

    /// <summary>The run counts this one as fallen.</summary>
    /// <remarks>
    /// By PID against the run and never by «parece un Shedinja llamado MUERTO»: the marker is what
    /// a death is turned INTO, and this run has held a real Shedinja that was never a death (§59).
    /// A tile is only a picture, but a picture that guesses is still a picture that lies.
    /// </remarks>
    public bool IsDead { get; } = isDead;

    public bool IsShiny => Pokemon?.IsShiny == true;

    public bool HasSprite => Sprite is not null;

    /// <summary>Shown when there is a Pokémon but no icon for it, so the hole is never silent.</summary>
    public string Fallback => Pokemon is null ? string.Empty : "?";
}

/// <param name="Value">Preformatted: the view only prints it.</param>
public sealed record OverviewRow(string Label, string Value);

/// <summary>A type of the selected Pokémon, as a coloured plate.</summary>
public sealed record ViewerTypeBadge(string Name, System.Windows.Media.Color Colour);

/// <summary>A box as the selector lists it, or the party.</summary>
public sealed record BoxTabViewModel(int Number, string Name, int Count, int Slots, bool IsParty)
{
    public string Label => IsParty ? Name : $"{Number}. {Name}";

    /// <summary>How full the box is, 0 to 1, for the little gauge of the box selector.</summary>
    public double Share => Slots == 0 ? 0 : (double)Count / Slots;

    public bool IsEmpty => Count == 0;

    public string Occupancy => $"{Count}/{Slots}";
}

/// <summary>
/// One row of the viewer's stat table: what the game shows and the IV under it.
/// </summary>
/// <remarks>
/// Read only. The EVs moved to their own section, ENTRENAR EV, which has the room to show what a change would do;
/// here they would be one more number in a card that is about the Pokémon, not about training it.
/// </remarks>
public sealed class StatRowViewModel(int index, string name, int value, int iv, int natureEffect = 0)
{
    /// <summary>"up", "down" or "none": the nature's mark on this stat, coloured as the summary screen colours it.</summary>
    public string NatureState { get; } = natureEffect > 0 ? "up" : natureEffect < 0 ? "down" : "none";

    public int Index { get; } = index;

    public string Name { get; } = name;

    /// <summary>The stat as the save holds it.</summary>
    public int Value { get; } = value;

    public int Iv { get; } = iv;

    /// <summary>The IV as a fraction of 31, for its bar.</summary>
    public double IvShare => Iv / 31d;
}

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
    private readonly IRunContext _runContext;
    private readonly IPokemonRepository _registered;
    private readonly ITypeLookup _types;
    private readonly IMoveCatalog _moves;
    private readonly IStatForecast _forecast;
    private readonly ILogger<PokemonViewerViewModel> _logger;

    /// <summary>PID of everything the run has lost, so a tile can say so.</summary>
    private HashSet<uint> _fallen = [];

    private static readonly string[] StatNames =
        ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    private BoxSnapshot? _snapshot;

    public PokemonViewerViewModel(IBoxReader boxes, PokemonSpriteService sprites,
        IRunContext runContext,
        IPokemonRepository registered, ITypeLookup types, IMoveCatalog moves, IStatForecast forecast,
        ILogger<PokemonViewerViewModel> logger, RenameService? rename = null) : base("VISOR POKÉMON", "El equipo y las 32 cajas de la partida")
    {
        _boxes = boxes;
        _sprites = sprites;
        _runContext = runContext;
        _registered = registered;
        _types = types;
        _moves = moves;
        _forecast = forecast;
        _logger = logger;
        _rename = rename;
        Fleeting.Fade(this, nameof(RenameStatus));

    }


    private readonly RenameService? _rename;

    /// <summary>What the MOTE box holds; set to the current nickname whenever the selection changes.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    private string _newNickname = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRenameStatus))]
    private string _renameStatus = string.Empty;

    public bool HasRenameStatus => RenameStatus.Length > 0;

    private bool CanRename() => _rename is not null && Selected is { IsEgg: false, IsIntact: true } pokemon
                                && NewNickname.Trim() != (pokemon.Nickname ?? string.Empty);

    /// <summary>Writes the new nickname into the save (game closed), records it, and reads the boxes again.</summary>
    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task RenameAsync()
    {
        if (_rename is null || Selected is not { } pokemon || _runContext.Current is not { } run)
        {
            return;
        }

        if (!_rename.CanRenameNow(out var reason))
        {
            RenameStatus = reason;
            return;
        }

        var result = await _rename.RenameAsync(run, pokemon, NewNickname);
        RenameStatus = result.Message;

        if (result.Delivered)
        {
            await LoadAsync();
        }
    }

    partial void OnSelectedChanged(BoxedPokemon? value)
    {
        NewNickname = value?.Nickname ?? string.Empty;
        RenameCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// What the save holds, in five figures, for the panel that had nothing in it.
    /// </summary>
    /// <remarks>
    /// The card panel is a third of the screen and it was empty until you clicked something. These
    /// are counts of what is already loaded, so they cost nothing and answer the question you have
    /// before you click anything: cuanto llevas y cuanto has perdido.
    /// </remarks>
    public ObservableCollection<OverviewRow> Overview { get; } = [];

    /// <summary>Every box of the PC, in the order the game numbers them.</summary>
    public ObservableCollection<BoxTabViewModel> Boxes { get; } = [];

    /// <summary>The thirty holes of the box on screen, empty ones included.</summary>
    public ObservableCollection<BoxSlotViewModel> Slots { get; } = [];

    /// <summary>
    /// The six the player is carrying, always on screen.
    /// </summary>
    /// <remarks>
    /// Its own list rather than one more box in the selector: the party is what the run is playing
    /// with, and having to page round to it to see whether somebody is hurt is a step too many.
    /// </remarks>
    public ObservableCollection<BoxSlotViewModel> Party { get; } = [];

    /// <summary>
    /// Which of the two grids the selection came from.
    /// </summary>
    /// <remarks>
    /// Two <c>ListBox</c>es, one selection. Choosing in one clears the other, so the highlight
    /// never sits in two places claiming to be the Pokémon on the right.
    /// </remarks>
    [ObservableProperty]
    private BoxSlotViewModel? _selectedPartySlot;

    /// <summary>Guards the two selections from clearing each other in a loop.</summary>
    private bool _switchingSelection;

    /// <summary>The six stats of whatever is selected.</summary>
    public ObservableCollection<StatRowViewModel> Stats { get; } = [];

    /// <summary>One or two types of whatever is selected, from the installed world through <see cref="ITypeLookup"/>.</summary>
    public ObservableCollection<ViewerTypeBadge> SelectedTypes { get; } = [];

    /// <summary>
    /// The four moves of whatever is selected, as the move reminder draws them: type colour, category and power.
    /// </summary>
    /// <remarks>Described by the same catalogue as MOVIMIENTOS, so a move shows the same numbers on both screens.</remarks>
    public ObservableCollection<MoveCardViewModel> SelectedMoves { get; } = [];

    /// <summary>What the nature raises and lowers, «▲ At. Esp. ▼ Ataque», or «neutra».</summary>
    [ObservableProperty]
    private string _natureEffect = string.Empty;

    /// <summary>The run counts the selected Pokémon as fallen, by PID.</summary>
    [ObservableProperty]
    private bool _isSelectedDead;

    /// <summary>Leaving the viewer closes the open detail: it is a panel, and there is nothing to edit in it.</summary>
    public override void ResetState()
    {
        SelectedSlot = null;
        SelectedPartySlot = null;
    }

    /// <summary>
    /// The card's ENTRENAR EV button: asks the shell to open the training section on this Pokémon.
    /// </summary>
    /// <remarks>
    /// An event and not a reference to the other section, so the viewer does not need to know how the shell
    /// navigates; the shell already does the same for JUGAR's links.
    /// </remarks>
    public event Action<BoxedPokemon>? TrainRequested;

    private bool CanTrain() => Selected is { IsEgg: false, IsIntact: true };

    [RelayCommand(CanExecute = nameof(CanTrain))]
    private void Train()
    {
        if (Selected is { IsEgg: false, IsIntact: true } pokemon)
        {
            TrainRequested?.Invoke(pokemon);
        }
    }

    [ObservableProperty]
    private BoxTabViewModel? _selectedBox;

    [ObservableProperty]
    private BoxSlotViewModel? _selectedSlot;

    /// <summary>The Pokémon whose detail is on screen, or null when nothing is selected.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TrainCommand))]
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

    public override string IconKey => "IconGrid";

    public override GameNeed Needs => GameNeed.Closed;

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
            OnPropertyChanged(nameof(PartyBallSprite));

            _snapshot = await _boxes.ReadAsync();

            // Quien ha caido, de la run y por PID. Si no hay run, nadie: el visor lee la partida
            // y la partida no sabe nada de muertes.
            _fallen = _runContext.Current is { } current
                ? (await _registered.GetAllAsync(current.Id))
                    .Where(p => p.Status == PokemonStatus.Dead && p.Pid is not null)
                    .Select(p => p.Pid!.Value)
                    .ToHashSet()
                : [];

            Problem = _snapshot.Problem ?? string.Empty;
            Notice = _snapshot.Notice ?? string.Empty;
            IsAvailable = _snapshot.Available;

            Boxes.Clear();
            Select(null);

            if (!_snapshot.Available)
            {
                Slots.Clear();
                Overview.Clear();
                Summary = string.Empty;
                return;
            }

            // El selector es solo del PC: el equipo tiene su propia rejilla y está siempre visible.
            foreach (var box in _snapshot.Boxes.Where(box => !box.IsParty))
            {
                Boxes.Add(new BoxTabViewModel(box.Number, box.Name, box.Count, box.Slots, box.IsParty));
            }

            ShowParty();

            var party = _snapshot.Party?.Count ?? 0;
            Summary = $"{party} en el equipo · {_snapshot.Stored} en el PC de {_snapshot.TrainerName}";

            var everyone = _snapshot.Boxes.SelectMany(b => b.Pokemon).ToList();

            Overview.Clear();
            Overview.Add(new OverviewRow("En la partida", _snapshot.Total.ToString()));
            Overview.Add(new OverviewRow("En el equipo", party.ToString()));
            Overview.Add(new OverviewRow("En el PC", _snapshot.Stored.ToString()));
            Overview.Add(new OverviewRow("Caídos de la run",
                everyone.Count(p => _fallen.Contains(p.Pid)).ToString()));
            Overview.Add(new OverviewRow("Variocolor", everyone.Count(p => p.IsShiny).ToString()));
            Overview.Add(new OverviewRow("Entrenador", _snapshot.TrainerName));

            // La primera caja con algo dentro: abrir en una vacía teniendo Pokémon en la
            // siguiente hace pensar que no se ha leído nada.
            SelectedBox = Boxes.FirstOrDefault(box => box.Count > 0) ?? Boxes.FirstOrDefault();

            // Y se redibuja a mano, sin esperar a que SelectedBox "cambie". Un intercambio deja
            // la caja con el mismo número, el mismo nombre y la misma cuenta, y como
            // BoxTabViewModel es un record, el objeto nuevo es IGUAL al viejo: la propiedad no
            // notifica nada y la rejilla se quedaba enseñando el Pokémon que ya no está.
            ShowBox(SelectedBox);

            _logger.LogInformation("Visor: {Total} Pokémon en {Boxes} cajas",
                _snapshot.Total, _snapshot.Boxes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer las cajas del PC");
            Problem = "No se han podido leer las cajas.";
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
        OnPropertyChanged(nameof(BoxTheme));
        ShowBox(value);
    }

    /// <summary>The wallpaper of the box on screen, by its number.</summary>
    public int BoxTheme => SelectedBox?.Number ?? 1;

    /// <summary>The wallpaper behind the selected Pokémon: its box's, or the party's.</summary>
    public int SelectedTheme => Selected is { IsInParty: true } || Selected is null
        ? Views.BoxWallpaper.PartyTheme
        : Selected.Box + 1;

    /// <summary>The ball the selected Pokémon was caught in, when the cartridge has its icon.</summary>
    public BitmapSource? SelectedBallSprite => Selected is { Ball: > 0 and <= 16 } pokemon
        ? _sprites.GetBall(pokemon.Ball)
        : null;

    public bool HasSelectedBall => SelectedBallSprite is not null;

    /// <summary>An ordinary Poké Ball, one per party hole, for the row above the party.</summary>
    public BitmapSource? PartyBallSprite => _sprites.GetBall();

    /// <summary>
    /// Fills the thirty holes with whatever the last reading says is in that box.
    /// </summary>
    /// <remarks>
    /// Called both when the selected box changes and after every reload, because the second one
    /// does not imply the first: <see cref="BoxTabViewModel"/> compares by value, so re-reading a
    /// box whose number, name and count are unchanged produces an equal object and no
    /// notification at all — which is exactly what a wonder trade leaves behind.
    /// </remarks>
    private void ShowBox(BoxTabViewModel? box)
    {
        Slots.Clear();

        // Pasar de caja no debe soltar al del equipo que se está mirando: son dos rejillas y solo
        // una ficha, y la que manda es la última que se tocó.
        _switchingSelection = true;
        SelectedSlot = null;
        _switchingSelection = false;

        if (SelectedPartySlot is null)
        {
            Select(null);
        }

        if (box is null || _snapshot is null)
        {
            return;
        }

        var contents = _snapshot.Boxes.FirstOrDefault(b => b.Number == box.Number);
        if (contents is null)
        {
            return;
        }

        // Los huecos los pone la caja, no el PC: el equipo tiene seis y una caja treinta.
        for (var slot = 0; slot < contents.Slots; slot++)
        {
            var pokemon = contents.Pokemon.FirstOrDefault(p => p.Slot == slot);
            Slots.Add(new BoxSlotViewModel(pokemon, pokemon is null ? null : SpriteFor(pokemon),
                pokemon is not null && _fallen.Contains(pokemon.Pid)));
        }
    }

    /// <summary>Draws the six party holes, empty ones included.</summary>
    private void ShowParty()
    {
        Party.Clear();

        if (_snapshot?.Party is not { } party)
        {
            return;
        }

        for (var slot = 0; slot < party.Slots; slot++)
        {
            var member = party.Pokemon.FirstOrDefault(p => p.Slot == slot);
            Party.Add(new BoxSlotViewModel(member, member is null ? null : SpriteFor(member),
                member is not null && _fallen.Contains(member.Pid)));
        }
    }

    partial void OnSelectedSlotChanged(BoxSlotViewModel? value)
    {
        if (_switchingSelection)
        {
            return;
        }

        if (value is not null)
        {
            _switchingSelection = true;
            SelectedPartySlot = null;
            _switchingSelection = false;
        }

        Select(value?.Pokemon);
    }

    partial void OnSelectedPartySlotChanged(BoxSlotViewModel? value)
    {
        if (_switchingSelection)
        {
            return;
        }

        if (value is not null)
        {
            _switchingSelection = true;
            SelectedSlot = null;
            _switchingSelection = false;
        }

        Select(value?.Pokemon);
    }

    private void Select(BoxedPokemon? pokemon)
    {
        Selected = pokemon;
        HasSelection = pokemon is not null;
        SelectedSprite = pokemon is null ? null : SpriteFor(pokemon);
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedBallSprite));
        OnPropertyChanged(nameof(HasSelectedBall));


        Stats.Clear();
        SelectedTypes.Clear();
        SelectedMoves.Clear();
        NatureEffect = string.Empty;
        IsSelectedDead = pokemon is not null && _fallen.Contains(pokemon.Pid);

        if (pokemon is null)
        {
            return;
        }

        // Las mismas cifras que MOVIMIENTOS: en caja se calculan con la tabla del mundo instalado (IStatForecast), no
        // con la de PKHeX, que con las estadísticas barajadas da otros números (2026-09-24).
        var computed = pokemon.IsEgg || pokemon.IsInParty ? null : _forecast.With(pokemon, pokemon.Evs);

        string? raised = null;
        string? lowered = null;

        for (var index = 0; index < StatNames.Length; index++)
        {
            var effect = pokemon.IsEgg ? 0 : _forecast.NatureEffect(pokemon, index);
            if (effect > 0) raised = StatNames[index];
            if (effect < 0) lowered = StatNames[index];
            Stats.Add(new StatRowViewModel(index, StatNames[index], computed?[index] ?? pokemon.Stats[index], pokemon.Ivs[index], effect));
        }

        NatureEffect = raised is null || lowered is null ? "neutra" : $"▲ {raised}  ▼ {lowered}";

        // Un huevo no enseña ni tipos ni ataques: el juego tampoco lo hace hasta que eclosiona.
        if (pokemon.IsEgg)
        {
            return;
        }

        foreach (var badge in TypeBadges.For(_types, pokemon))
        {
            SelectedTypes.Add(badge);
        }

        var moves = pokemon.MoveIds ?? [];
        for (var slot = 0; slot < 4; slot++)
        {
            var move = slot < moves.Count ? moves[slot] : 0;
            var sheet = move == 0 ? null : _moves.Describe(move);
            SelectedMoves.Add(MoveCardViewModel.Known(slot, move, sheet,
                sheet is { Category: MoveSheet.Physical or MoveSheet.Special } ? _sprites.GetCategory(sheet.Category) : null));
        }
    }

    /// <summary>
    /// An egg shows the egg icon, not the icon of what is inside: the game does not tell the
    /// player either, and showing the species would give away something the game hides.
    /// </summary>
    private BitmapSource? SpriteFor(BoxedPokemon pokemon) =>
        pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny);
}
