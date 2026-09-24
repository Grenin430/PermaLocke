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

/// <summary>
/// One stat on the training bench: what it is now, the EVs being given to it, and what it would become.
/// </summary>
/// <remarks>
/// It reports every change to the owner, because the six rows share one budget of 510 and each of them
/// needs to know when a sibling has spent some of it.
/// </remarks>
public sealed partial class EvRowViewModel : ObservableObject
{
    private readonly Action<int, int>? _changed;

    /// <summary>Set while the owner is the one writing, so its own write does not echo back.</summary>
    private bool _quiet;

    public EvRowViewModel(int index, string name, int iv, int natureEffect, int now, bool nowIsEstimate, int ev,
        Action<int, int>? changed = null)
    {
        Index = index;
        Name = name;
        Iv = iv;
        NatureState = natureEffect > 0 ? "up" : natureEffect < 0 ? "down" : "none";
        Now = now;
        NowIsEstimate = nowIsEstimate;
        _ev = ev;
        _changed = changed;
    }

    public int Index { get; }

    public string Name { get; }

    public int Iv { get; }

    /// <summary>«up», «down» or «none»: what the nature does to this stat, for the colour of its name.</summary>
    public string NatureState { get; }

    /// <summary>The stat today: the party's own, or what the game will work out for one in a box.</summary>
    public int Now { get; }

    /// <summary>True when <see cref="Now"/> could only be estimated, so the screen marks it.</summary>
    public bool NowIsEstimate { get; }

    public string NowText => NowIsEstimate ? $"≈{Now}" : Now.ToString();

    [ObservableProperty]
    private int _ev;

    /// <summary>True while this row holds EVs that are not yet in the partida.</summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>What the stat would be with the EVs on screen. Null when unchanged or unknowable.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAfter), nameof(Trend))]
    private int? _after;

    public bool ShowsAfter => After is not null;

    /// <summary>«up», «down» or «same», for the colour of the projected number.</summary>
    public string Trend => After is not { } after ? "same" : after > Now ? "up" : after < Now ? "down" : "same";

    /// <summary>Fraction of the per-stat maximum, for the bar the row draws.</summary>
    public double Fill => (double)Ev / EvSpread.PerStatMax;

    partial void OnEvChanged(int value)
    {
        OnPropertyChanged(nameof(Fill));

        if (!_quiet)
        {
            _changed?.Invoke(Index, value);
        }
    }

    /// <summary>
    /// Sets the value without telling the owner, for when the owner is the one setting it.
    /// </summary>
    /// <remarks>
    /// The six rows share one budget, so moving one makes the owner rewrite all six. Without this, that rewrite
    /// would come straight back as six more edits.
    /// </remarks>
    public void Silently(int ev, bool dirty, int? after)
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
        After = dirty ? after : null;
    }
}

/// <summary>
/// ENTRENAR EV: pick a Pokémon from the party or a box and share out its effort values.
/// </summary>
/// <remarks>
/// <para>
/// The editor that used to live inside the viewer's detail card, with the room it needed. The rules did not
/// move with it and did not change: 252 per stat is clamped, 510 in total is only checked, and writing goes
/// through <see cref="EvTrainingService"/>, which writes first and records the <c>EvsTrained</c> event after.
/// </para>
/// <para>
/// What it adds is the answer to «¿y eso qué me da?»: each row shows the stat it would leave, worked out by
/// <see cref="IStatForecast"/> from the installed world's own base stats — the same numbers the writer puts on
/// disk. When that cannot be known the row shows nothing rather than a plausible figure.
/// </para>
/// </remarks>
public sealed partial class EvTrainingViewModel : SectionViewModel
{
    private static readonly string[] StatNames =
        ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    private readonly IBoxReader _boxes;
    private readonly PokemonSpriteService _sprites;
    private readonly EvTrainingService _training;
    private readonly IStatForecast _forecast;
    private readonly IRunContext _runContext;
    private readonly IPokemonRepository _registered;
    private readonly ITypeLookup _types;

    /// <summary>One or two types of the Pokémon picked, as coloured plates (§176).</summary>
    public System.Collections.ObjectModel.ObservableCollection<ViewerTypeBadge> SelectedTypes { get; } = [];
    private readonly ILogger<EvTrainingViewModel> _logger;

    private BoxSnapshot? _snapshot;

    /// <summary>PID of everything the run has lost, so a tile can say so.</summary>
    private HashSet<uint> _fallen = [];

    /// <summary>What the save holds for the selected Pokémon, to compare edits against.</summary>
    private EvSpread _savedEvs = EvSpread.Empty;

    /// <summary>The spread on screen, which is the saved one until the player moves something.</summary>
    private EvSpread _evs = EvSpread.Empty;

    /// <summary>A Pokémon asked for from another screen, picked as soon as the partida has been read.</summary>
    private uint? _wanted;

    /// <summary>Guards the two selections from clearing each other in a loop.</summary>
    private bool _switchingSelection;

    public EvTrainingViewModel(IBoxReader boxes, PokemonSpriteService sprites, EvTrainingService training,
        IStatForecast forecast, IRunContext runContext, IPokemonRepository registered, ITypeLookup types,
        ILogger<EvTrainingViewModel> logger) : base("ENTRENAR EV", "Los EV de tu equipo y de tus cajas")
    {
        Fleeting.Fade(this, nameof(TrainStatus));

        _boxes = boxes;
        _sprites = sprites;
        _training = training;
        _forecast = forecast;
        _runContext = runContext;
        _registered = registered;
        _types = types;
        _logger = logger;
    }

    public override string IconKey => "IconDumbbell";

    public override GameNeed Needs => GameNeed.Closed;

    /// <summary>
    /// Reads the partida each time the section opens -- unless there is an edit in flight, which a re-read
    /// would throw away without asking.
    /// </summary>
    public override Task ActivateAsync() => EvsChanged || IsTraining ? Task.CompletedTask : LoadAsync();

    /// <summary>
    /// Leaving closes the bench, unless it holds EVs typed in and not yet written: those are somebody's work,
    /// and dropping them because a tab was pressed would be a silent state change.
    /// </summary>
    public override void ResetState()
    {
        if (EvsChanged || IsTraining)
        {
            return;
        }

        _switchingSelection = true;
        SelectedSlot = null;
        SelectedPartySlot = null;
        _switchingSelection = false;
        Select(null);
    }

    /// <summary>
    /// Opens the bench on a given Pokémon, for the viewer's ENTRENAR EV button.
    /// </summary>
    /// <remarks>
    /// Refused while another one has unsaved EVs on the bench, for the same reason as <see cref="ResetState"/>:
    /// swapping the Pokémon would quietly bin the edit.
    /// </remarks>
    public void Focus(BoxedPokemon pokemon)
    {
        if (EvsChanged && Selected is { } current && current.Pid != pokemon.Pid)
        {
            TrainFailed = true;
            TrainStatus = $"Antes guarda o deshaz los EV de {current.DisplayName}.";
            return;
        }

        _wanted = pokemon.Pid;
    }

    // ============================================================ EL PC

    public ObservableCollection<BoxSlotViewModel> Party { get; } = [];

    public ObservableCollection<BoxTabViewModel> Boxes { get; } = [];

    public ObservableCollection<BoxSlotViewModel> Slots { get; } = [];

    [ObservableProperty]
    private BoxTabViewModel? _selectedBox;

    [ObservableProperty]
    private BoxSlotViewModel? _selectedSlot;

    [ObservableProperty]
    private BoxSlotViewModel? _selectedPartySlot;

    /// <summary>The wallpaper of the box on screen, by its number.</summary>
    public int BoxTheme => SelectedBox?.Number ?? 1;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isAvailable;

    /// <summary>Why the PC cannot be shown. Empty when it can.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message), nameof(HasMessage))]
    private string _problem = string.Empty;

    /// <summary>Something true and worth saying that is not a failure, such as a stale reading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message), nameof(HasMessage))]
    private string _notice = string.Empty;

    public string Message => Problem.Length > 0 ? Problem : Notice;

    public bool HasMessage => Message.Length > 0;

    /// <summary>RELEER is off while there is an edit on the bench: re-reading would drop it without asking.</summary>
    private bool CanReload() => !EvsChanged && !IsTraining;

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        // Lo que estaba elegido se vuelve a elegir después de leer, que es lo que hace que guardar no te
        // eche del Pokémon que acabas de entrenar.
        var keep = _wanted ?? Selected?.Pid;
        _wanted = null;

        try
        {
            await _sprites.PrepareAsync();

            _snapshot = await _boxes.ReadAsync();

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
                Party.Clear();
                Slots.Clear();
                return;
            }

            foreach (var box in _snapshot.Boxes.Where(box => !box.IsParty))
            {
                Boxes.Add(new BoxTabViewModel(box.Number, box.Name, box.Count, box.Slots, box.IsParty));
            }

            Party.Clear();

            if (_snapshot.Party is { } party)
            {
                for (var slot = 0; slot < party.Slots; slot++)
                {
                    Party.Add(Tile(party.Pokemon.FirstOrDefault(p => p.Slot == slot)));
                }
            }

            var target = keep is { } pid
                ? _snapshot.Boxes.SelectMany(box => box.Pokemon).FirstOrDefault(p => p.Pid == pid)
                : null;

            // La caja del que se busca, o la primera con algo dentro.
            SelectedBox = target is { IsInParty: false }
                ? Boxes.FirstOrDefault(box => box.Number == target.Box + 1)
                : Boxes.FirstOrDefault(box => box.Count > 0) ?? Boxes.FirstOrDefault();

            // A mano y no por el cambio de SelectedBox: un record igual al anterior no notifica (ver el visor).
            ShowBox(SelectedBox);

            if (target is not null)
            {
                var tile = target.IsInParty
                    ? Party.FirstOrDefault(t => t.Pokemon?.Pid == target.Pid)
                    : Slots.FirstOrDefault(t => t.Pokemon?.Pid == target.Pid);

                if (target.IsInParty)
                {
                    SelectedPartySlot = tile;
                }
                else
                {
                    SelectedSlot = tile;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer la partida para entrenar EV");
            Problem = "No se ha podido leer la partida.";
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
        SelectedBox = Boxes[(index + direction + Boxes.Count) % Boxes.Count];
    }

    partial void OnSelectedBoxChanged(BoxTabViewModel? value)
    {
        OnPropertyChanged(nameof(BoxTheme));
        ShowBox(value);
    }

    /// <summary>
    /// Fills the thirty holes of a box. The bench stays on whoever is on it: pasar de caja es buscar al siguiente,
    /// no soltar al que estás entrenando.
    /// </summary>
    private void ShowBox(BoxTabViewModel? box)
    {
        _switchingSelection = true;

        try
        {
            SelectedSlot = null;
            Slots.Clear();

            if (box is null || _snapshot?.Boxes.FirstOrDefault(b => b.Number == box.Number) is not { } contents)
            {
                return;
            }

            for (var slot = 0; slot < contents.Slots; slot++)
            {
                Slots.Add(Tile(contents.Pokemon.FirstOrDefault(p => p.Slot == slot)));
            }

            // Volver a la caja del que está en el banco lo vuelve a enmarcar.
            if (Selected is { IsInParty: false } current && current.Box + 1 == box.Number)
            {
                SelectedSlot = Slots.FirstOrDefault(t => t.Pokemon?.Pid == current.Pid);
            }
        }
        finally
        {
            _switchingSelection = false;
        }
    }

    partial void OnSelectedSlotChanged(BoxSlotViewModel? value) => Picked(value, fromParty: false);

    partial void OnSelectedPartySlotChanged(BoxSlotViewModel? value) => Picked(value, fromParty: true);

    /// <summary>
    /// A tile was clicked in one of the two grids. Clears the other, and moves the bench to it -- unless the bench
    /// holds an unsaved edit of somebody else, which a click must not throw away.
    /// </summary>
    private void Picked(BoxSlotViewModel? value, bool fromParty)
    {
        if (_switchingSelection || value is null)
        {
            return;
        }

        // Un hueco vacío no es nadie, y otro Pokémon con una edición a medias en el banco tampoco: en los dos
        // casos el marco vuelve a quien está en el banco.
        if (value.Pokemon is not { } chosen
            || (EvsChanged && Selected is { } current && chosen.Pid != current.Pid))
        {
            if (value.Pokemon is not null && Selected is { } editing)
            {
                TrainFailed = true;
                TrainStatus = $"Antes guarda o deshaz los EV de {editing.DisplayName}.";
            }

            Reframe();
            return;
        }

        _switchingSelection = true;

        if (fromParty)
        {
            SelectedSlot = null;
        }
        else
        {
            SelectedPartySlot = null;
        }

        _switchingSelection = false;

        if (chosen.Pid != Selected?.Pid)
        {
            Select(chosen);
        }
    }

    /// <summary>Puts the frame back on whoever is on the bench, in whichever grid holds it.</summary>
    private void Reframe()
    {
        _switchingSelection = true;

        try
        {
            SelectedPartySlot = Selected is { IsInParty: true } inParty
                ? Party.FirstOrDefault(t => t.Pokemon?.Pid == inParty.Pid)
                : null;
            SelectedSlot = Selected is { IsInParty: false } inBox
                ? Slots.FirstOrDefault(t => t.Pokemon?.Pid == inBox.Pid)
                : null;
        }
        finally
        {
            _switchingSelection = false;
        }
    }

    private BoxSlotViewModel Tile(BoxedPokemon? pokemon) =>
        new(pokemon, pokemon is null ? null : SpriteFor(pokemon), pokemon is not null && _fallen.Contains(pokemon.Pid));

    // ============================================================ EL BANCO

    /// <summary>The Pokémon on the bench, or null when nothing is chosen.</summary>
    [ObservableProperty]
    private BoxedPokemon? _selected;

    [ObservableProperty]
    private BitmapSource? _selectedSprite;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>False for an egg, which the game never trains.</summary>
    [ObservableProperty]
    private bool _canEdit;

    [ObservableProperty]
    private bool _isEgg;

    /// <summary>The entry does not match its checksum in the save (§97): shown, never written.</summary>
    [ObservableProperty]
    private bool _isDamaged;

    /// <summary>The run counts the Pokémon on the bench as fallen.</summary>
    [ObservableProperty]
    private bool _isFallen;

    /// <summary>The wallpaper behind the portrait: its box's, or the party's.</summary>
    public int SelectedTheme => Selected is { IsInParty: true } || Selected is null
        ? Views.BoxWallpaper.PartyTheme
        : Selected.Box + 1;

    /// <summary>Where it is, the way the game would say it.</summary>
    public string Where => Selected is not { } pokemon
        ? string.Empty
        : pokemon.IsInParty
            ? $"Equipo · puesto {pokemon.Slot + 1}"
            : $"{BoxName(pokemon.Box + 1)} · hueco {pokemon.Slot + 1}";

    /// <summary>The stat the nature raises, or empty for a neutral one.</summary>
    [ObservableProperty]
    private string _raised = string.Empty;

    /// <summary>The stat the nature lowers, or empty for a neutral one.</summary>
    [ObservableProperty]
    private string _lowered = string.Empty;

    [ObservableProperty]
    private bool _isNeutralNature = true;

    public ObservableCollection<EvRowViewModel> Rows { get; } = [];

    /// <summary>The six EVs on screen, a new array on every change so the hexagon redraws.</summary>
    [ObservableProperty]
    private IReadOnlyList<int> _evValues = EvSpread.Empty.Values;

    /// <summary>The six in the partida, for the outline the hexagon draws over an edit.</summary>
    [ObservableProperty]
    private IReadOnlyList<int> _savedValues = EvSpread.Empty.Values;

    [ObservableProperty]
    private int _evTotal;

    [ObservableProperty]
    private int _evRemaining;

    /// <summary>How far past the 510 the reparto currently is, or zero.</summary>
    [ObservableProperty]
    private int _evOver;

    [ObservableProperty]
    private bool _evOverBudget;

    /// <summary>Fraction of the 510 budget spent, for the long bar at the bottom.</summary>
    [ObservableProperty]
    private double _evFill;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand), nameof(LoadCommand))]
    private bool _evsChanged;

    /// <summary>Changed <b>and</b> legal. Over 510 the reparto is a work in progress, not something to write.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave;

    /// <summary>What happened to the last attempt to write. Empty when nothing has been tried.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrainStatus))]
    private string _trainStatus = string.Empty;

    public bool HasTrainStatus => TrainStatus.Length > 0;

    [ObservableProperty]
    private bool _trainFailed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(LoadCommand))]
    private bool _isTraining;

    private void Select(BoxedPokemon? pokemon)
    {
        Selected = pokemon;
        HasSelection = pokemon is not null;
        SelectedSprite = pokemon is null ? null : SpriteFor(pokemon);
        SelectedTypes.Clear();
        foreach (var badge in TypeBadges.For(_types, pokemon))
        {
            SelectedTypes.Add(badge);
        }
        IsEgg = pokemon?.IsEgg == true;
        IsDamaged = pokemon is { IsIntact: false };
        CanEdit = pokemon is { IsEgg: false, IsIntact: true };
        IsFallen = pokemon is not null && _fallen.Contains(pokemon.Pid);
        TrainStatus = string.Empty;
        TrainFailed = false;

        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(Where));

        Rows.Clear();
        _savedEvs = pokemon is null ? EvSpread.Empty : EvSpread.Of(pokemon.Evs);
        _evs = _savedEvs;

        Raised = string.Empty;
        Lowered = string.Empty;

        if (pokemon is not null)
        {
            // Lo que la naturaleza hace, sacado de la misma tabla que la cuenta de las estadísticas.
            for (var stat = 1; stat < StatNames.Length; stat++)
            {
                switch (_forecast.NatureEffect(pokemon, stat))
                {
                    case > 0:
                        Raised = StatNames[stat];
                        break;
                    case < 0:
                        Lowered = StatNames[stat];
                        break;
                }
            }

            // El equipo lleva sus estadísticas de verdad. De una caja, las que el juego calculará al sacarlo,
            // y si no se pueden saber, la estimación de siempre marcada como tal.
            var computed = pokemon.IsInParty ? null : _forecast.With(pokemon, _savedEvs.Values);

            for (var index = 0; index < StatNames.Length; index++)
            {
                var now = computed?[index] ?? pokemon.Stats[index];
                var estimate = !pokemon.IsInParty && computed is null;

                Rows.Add(new EvRowViewModel(index, StatNames[index], pokemon.Ivs[index],
                    _forecast.NatureEffect(pokemon, index), now, estimate, _savedEvs[index], Edited));
            }
        }

        IsNeutralNature = Raised.Length == 0;
        Refresh();
    }

    private string BoxName(int number) =>
        Boxes.FirstOrDefault(box => box.Number == number)?.Name ?? $"Caja {number}";

    /// <summary>One row moved. Reapplies the whole spread, since the six share one budget.</summary>
    private void Edited(int index, int value) => Apply(_evs.With(index, value));

    /// <summary>Pushes a spread back into the six rows and recomputes everything that hangs off it.</summary>
    private void Apply(EvSpread spread)
    {
        _evs = spread;

        var projected = Selected is { } pokemon ? _forecast.With(pokemon, spread.Values) : null;

        foreach (var row in Rows)
        {
            row.Silently(spread[row.Index], spread[row.Index] != _savedEvs[row.Index], projected?[row.Index]);
        }

        Refresh();
    }

    private void Refresh()
    {
        EvValues = [.. _evs.Values];
        SavedValues = [.. _savedEvs.Values];
        EvTotal = _evs.Total;
        EvRemaining = Math.Max(0, _evs.Remaining);
        EvOver = _evs.Over;
        EvOverBudget = !_evs.IsLegal;

        // La barra se llena y se queda llena: pasarse no la hace crecer, lo dice el color.
        EvFill = Math.Min(1d, (double)_evs.Total / EvSpread.TotalMax);

        EvsChanged = !_evs.Equals(_savedEvs);
        CanSave = EvsChanged && _evs.IsLegal;
    }

    /// <summary>Four EVs are one point of stat at level 100, so that is what one press moves.</summary>
    private const int Step = EvSpread.PerStatPoint;

    [RelayCommand]
    private void Add(EvRowViewModel? row)
    {
        if (row is not null && CanEdit)
        {
            Apply(_evs.With(row.Index, row.Ev + Step));
        }
    }

    [RelayCommand]
    private void Subtract(EvRowViewModel? row)
    {
        if (row is not null && CanEdit)
        {
            Apply(_evs.With(row.Index, row.Ev - Step));
        }
    }

    /// <summary>
    /// Fills the stat to 252, even if that puts the reparto over 510: moving 252 points from one stat to another is
    /// two edits, and refusing the first until the second has happened would force an order nobody would guess.
    /// </summary>
    [RelayCommand]
    private void Max(EvRowViewModel? row)
    {
        if (row is not null && CanEdit)
        {
            Apply(_evs.With(row.Index, EvSpread.PerStatMax));
        }
    }

    [RelayCommand]
    private void Clear(EvRowViewModel? row)
    {
        if (row is not null && CanEdit)
        {
            Apply(_evs.With(row.Index, 0));
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (CanEdit)
        {
            Apply(EvSpread.Empty);
        }
    }

    /// <summary>Throws the edit away and puts back what the partida holds.</summary>
    [RelayCommand(CanExecute = nameof(EvsChanged))]
    private void Revert()
    {
        Apply(_savedEvs);
        TrainStatus = string.Empty;
        TrainFailed = false;
    }

    private bool CanSaveNow() => CanSave && !IsTraining;

    /// <summary>
    /// Writes the EVs on screen into the partida, then reads it back and stays on the same Pokémon.
    /// </summary>
    /// <remarks>
    /// Everything that can refuse does so before anything is opened: no run to record it against, nothing chosen,
    /// nothing changed, or the game still loaded in the emulator. What gets past goes through the service, which
    /// writes first and records afterwards.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveNow))]
    private async Task SaveAsync()
    {
        if (Selected is not { } target || IsTraining)
        {
            return;
        }

        if (_runContext.Current is not { } run)
        {
            TrainFailed = true;
            TrainStatus = "No hay ninguna run cargada.";
            return;
        }

        if (!_training.CanTrainNow(out var reason))
        {
            TrainFailed = true;
            TrainStatus = reason;
            return;
        }

        IsTraining = true;
        var wanted = _evs;

        try
        {
            var result = await _training.TrainAsync(run, target, wanted);

            if (!result.Delivered)
            {
                TrainFailed = true;
                TrainStatus = result.Message;
                return;
            }

            _logger.LogInformation("EV escritos para {Name}: {Evs}", target.DisplayName, wanted);

            // La partida ha cambiado por debajo, y con ella las estadísticas. Releerla es lo único que hace que
            // los números de la izquierda sean ciertos; se vuelve al mismo Pokémon por su PID.
            await LoadAsync();

            TrainFailed = false;
            TrainStatus = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al escribir los EV de {Name}", target.DisplayName);
            TrainFailed = true;
            TrainStatus = "No se han podido escribir los EV.";
        }
        finally
        {
            IsTraining = false;
        }
    }

    /// <summary>
    /// An egg shows the egg icon, not the icon of what is inside: the game does not tell the player either.
    /// </summary>
    private BitmapSource? SpriteFor(BoxedPokemon pokemon) =>
        pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species, pokemon.Form);
}
