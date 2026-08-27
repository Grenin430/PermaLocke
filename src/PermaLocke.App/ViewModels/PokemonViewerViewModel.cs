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

/// <summary>A box as the selector lists it, or the party.</summary>
public sealed record BoxTabViewModel(int Number, string Name, int Count, int Slots, bool IsParty)
{
    public string Label => IsParty ? Name : $"{Number}. {Name}";

    public string Occupancy => $"{Count}/{Slots}";
}

/// <summary>
/// One row of the stat table: what the game shows, what it is made of, and the one part of it
/// the player is allowed to move.
/// </summary>
/// <remarks>
/// <para>
/// The EV is the only editable field in the whole viewer, so it is the only thing here that is
/// not a plain record. It reports every change to the owner, because the six rows share one
/// budget of 510 and each of them needs to know when a sibling has spent some of it.
/// </para>
/// <para>
/// The stat column keeps showing what the save holds. It is not recomputed as the EV moves:
/// working out a stat needs the species base values, which PermaLocke reads from the cartridge and
/// does not keep per stat, and guessing it from the current number would put an invented figure on
/// screen. It updates when the change is written and the partida re-read, which is also when it
/// becomes true.
/// </para>
/// </remarks>
public sealed partial class StatRowViewModel : ObservableObject
{
    private readonly Action<int, int>? _changed;

    public StatRowViewModel(int index, string name, int value, int iv, int ev,
        Action<int, int>? changed = null)
    {
        Index = index;
        Name = name;
        Value = value;
        Iv = iv;
        _ev = ev;
        _changed = changed;
    }

    public int Index { get; }

    public string Name { get; }

    /// <summary>The stat as the save holds it.</summary>
    public int Value { get; }

    public int Iv { get; }

    [ObservableProperty]
    private int _ev;

    /// <summary>True while this row holds EVs that are not yet in the partida.</summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Set while the owner is the one writing, so its own write does not echo back.</summary>
    private bool _quiet;

    partial void OnEvChanged(int value)
    {
        OnPropertyChanged(nameof(Fill));

        if (!_quiet)
        {
            _changed?.Invoke(Index, value);
        }
    }

    /// <summary>Fraction of the per-stat maximum, for the bar the row draws.</summary>
    public double Fill => (double)Ev / EvSpread.PerStatMax;

    /// <summary>
    /// Sets the value without telling the owner, for when the owner is the one setting it.
    /// </summary>
    /// <remarks>
    /// The six rows share one budget, so moving one makes the owner rewrite all six. Without this,
    /// that rewrite would come straight back as six more edits.
    /// </remarks>
    public void Silently(int ev, bool dirty)
    {
        _quiet = true;

        try
        {
            Ev = ev;
        }
        finally
        {
            _quiet = false;
        }

        IsDirty = dirty;
    }
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
    private readonly EvTrainingService _training;
    private readonly IRunContext _runContext;
    private readonly ILogger<PokemonViewerViewModel> _logger;

    private static readonly string[] StatNames =
        ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    private BoxSnapshot? _snapshot;

    /// <summary>What the save holds for the selected Pokémon, to compare edits against.</summary>
    private EvSpread _savedEvs = EvSpread.Empty;

    public PokemonViewerViewModel(IBoxReader boxes, PokemonSpriteService sprites,
        WonderTradeViewModel trade, EvTrainingService training, IRunContext runContext,
        ILogger<PokemonViewerViewModel> logger) : base("VISOR POKÉMON", "El equipo y las 32 cajas de la partida, con EV editables")
    {
        _boxes = boxes;
        _sprites = sprites;
        _training = training;
        _runContext = runContext;
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

    public override GameNeed Needs => GameNeed.Closed;

    public override string NeedsDetail =>
        "Mirar se puede siempre, pero verás lo último que guardaste. Editar EV o hacer un wonder "
        + "trade escribe en el fichero de partida y exige el juego cerrado.";

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

            // El selector es solo del PC: el equipo tiene su propia rejilla y está siempre visible.
            foreach (var box in _snapshot.Boxes.Where(box => !box.IsParty))
            {
                Boxes.Add(new BoxTabViewModel(box.Number, box.Name, box.Count, box.Slots, box.IsParty));
            }

            ShowParty();

            var party = _snapshot.Party?.Count ?? 0;
            Summary = $"{party} en el equipo · {_snapshot.Stored} en el PC de {_snapshot.TrainerName}";

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

    partial void OnSelectedBoxChanged(BoxTabViewModel? value) => ShowBox(value);

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
            Slots.Add(new BoxSlotViewModel(pokemon, pokemon is null ? null : SpriteFor(pokemon)));
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
            Party.Add(new BoxSlotViewModel(member, member is null ? null : SpriteFor(member)));
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
        TrainStatus = string.Empty;

        // Con el intercambio armado, elegir en la caja es elegir a quién se entrega. Un huevo no:
        // lo que hay dentro no se sabe, así que no se puede decir qué vale. Y un miembro del
        // equipo tampoco: el intercambio escribe en las cajas, y el equipo es otro almacén.
        Trade.Choose(pokemon is { IsEgg: false, IsInParty: false } ? pokemon : null);

        Stats.Clear();
        _savedEvs = EvSpread.Empty;
        Evs = EvSpread.Empty;

        if (pokemon is null)
        {
            RefreshEvTotals();
            return;
        }

        // Un huevo no entrena: el juego no le reparte EV y lo que hay dentro no se conoce.
        CanEditEvs = !pokemon.IsEgg;

        _savedEvs = EvSpread.Of(pokemon.Evs);
        Evs = _savedEvs;

        for (var index = 0; index < StatNames.Length; index++)
        {
            Stats.Add(new StatRowViewModel(
                index,
                StatNames[index],
                pokemon.Stats[index],
                pokemon.Ivs[index],
                _savedEvs[index],
                EvEdited));
        }

        RefreshEvTotals();
    }

    // ============================================================ EV EDITOR

    /// <summary>The spread on screen, which is the saved one until the player moves something.</summary>
    private EvSpread Evs { get; set; } = EvSpread.Empty;

    /// <summary>False for an egg, which the game never trains.</summary>
    [ObservableProperty]
    private bool _canEditEvs;

    [ObservableProperty]
    private int _evTotal;

    [ObservableProperty]
    private int _evRemaining;

    /// <summary>How far past the 510 the reparto currently is, or zero.</summary>
    [ObservableProperty]
    private int _evOver;

    /// <summary>True while the reparto is over 510 and could not be written.</summary>
    [ObservableProperty]
    private bool _evOverBudget;

    /// <summary>Fraction of the 510 budget spent, for the bar over the editor.</summary>
    [ObservableProperty]
    private double _evFill;

    /// <summary>True while the screen holds EVs that are not in the partida yet.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertEvsCommand))]
    private bool _evsChanged;

    /// <summary>
    /// Changed <b>and</b> legal. Over 510 the reparto is a work in progress, not something to
    /// write: the button goes dead and the panel says by how much.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEvsCommand))]
    private bool _canSaveEvs;

    /// <summary>What happened to the last attempt to write EVs. Empty when nothing has been tried.</summary>
    [ObservableProperty]
    private string _trainStatus = string.Empty;

    [ObservableProperty]
    private bool _trainFailed;

    [ObservableProperty]
    private bool _isTraining;

    /// <summary>One row moved. Reapplies the whole spread, since the six share one budget.</summary>
    private void EvEdited(int index, int value) => Apply(Evs.With(index, value));

    /// <summary>Pushes a spread back into the six rows and recomputes the totals.</summary>
    private void Apply(EvSpread spread)
    {
        Evs = spread;

        foreach (var row in Stats)
        {
            row.Silently(spread[row.Index], spread[row.Index] != _savedEvs[row.Index]);
        }

        RefreshEvTotals();
    }

    private void RefreshEvTotals()
    {
        EvTotal = Evs.Total;
        EvRemaining = Math.Max(0, Evs.Remaining);
        EvOver = Evs.Over;
        EvOverBudget = !Evs.IsLegal;

        // La barra se llena y se queda llena: pasarse no la hace crecer, lo dice el color.
        EvFill = Math.Min(1d, (double)Evs.Total / EvSpread.TotalMax);

        EvsChanged = !Evs.Equals(_savedEvs);
        CanSaveEvs = EvsChanged && Evs.IsLegal;
    }

    /// <summary>
    /// Fills the stat to 252, even if that puts the reparto over 510.
    /// </summary>
    /// <remarks>
    /// Going over is the point. Moving 252 points from PS to Velocidad is two edits, and refusing
    /// the first one until the second has happened would force the player to work in an order
    /// nobody would guess. The panel turns red, says by how much, and blocks the save instead.
    /// </remarks>
    [RelayCommand]
    private void MaxEv(StatRowViewModel? row)
    {
        if (row is not null)
        {
            Apply(Evs.With(row.Index, EvSpread.PerStatMax));
        }
    }

    [RelayCommand]
    private void ClearEv(StatRowViewModel? row)
    {
        if (row is not null)
        {
            Apply(Evs.With(row.Index, 0));
        }
    }

    [RelayCommand]
    private void ClearAllEvs() => Apply(EvSpread.Empty);

    /// <summary>Throws the edit away and puts back what the partida holds.</summary>
    [RelayCommand(CanExecute = nameof(EvsChanged))]
    private void RevertEvs()
    {
        Apply(_savedEvs);
        TrainStatus = string.Empty;
        TrainFailed = false;
    }

    /// <summary>
    /// Writes the EVs on screen into the partida.
    /// </summary>
    /// <remarks>
    /// Everything that can refuse does so before anything is opened: no run to record it against,
    /// nothing selected, nothing changed, or the game still loaded in the emulator. What gets past
    /// all four goes through the service, which writes first and records afterwards.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveEvs))]
    private async Task SaveEvsAsync()
    {
        if (Selected is not { } target || IsTraining)
        {
            return;
        }

        if (_runContext.Current is not { } run)
        {
            TrainFailed = true;
            TrainStatus = "No hay ninguna run activa, así que no habría dónde registrar el cambio.";
            return;
        }

        if (!_training.CanTrainNow(out var reason))
        {
            TrainFailed = true;
            TrainStatus = reason;
            return;
        }

        IsTraining = true;

        try
        {
            var result = await _training.TrainAsync(run, target, Evs);

            TrainFailed = !result.Delivered;
            TrainStatus = result.Message;

            if (result.Delivered)
            {
                _logger.LogInformation("EV escritos para {Name}: {Evs}", target.DisplayName, Evs);

                // La partida ha cambiado por debajo, y con ella las estadísticas que dependen de
                // los EV. Releerla es lo único que hace que la columna de la izquierda sea cierta.
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al escribir los EV de {Name}", target.DisplayName);
            TrainFailed = true;
            TrainStatus = "No se han podido escribir los EV. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsTraining = false;
        }
    }

    /// <summary>
    /// An egg shows the egg icon, not the icon of what is inside: the game does not tell the
    /// player either, and showing the species would give away something the game hides.
    /// </summary>
    private BitmapSource? SpriteFor(BoxedPokemon pokemon) =>
        pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species);
}
