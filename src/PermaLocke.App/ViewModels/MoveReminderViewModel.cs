using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One move as the reminder draws it: a known one in its slot, or one it can remember.</summary>
public sealed partial class MoveCardViewModel : ObservableObject
{
    /// <summary>What the game writes where a number does not apply, as in its own summary screen.</summary>
    private const string None = "---";

    private MoveCardViewModel(int slot, int move, MoveSheet? sheet, string badge, BitmapSource? categoryIcon)
    {
        Slot = slot;
        Move = move;
        Badge = badge;

        Name = move == 0 ? "— libre —" : sheet?.Name ?? $"Movimiento {move}";
        TypeName = sheet?.TypeName.ToUpperInvariant() ?? string.Empty;
        TypeBrush = TypePalette.BrushOf(sheet?.Type ?? -1);
        TypeColour = move == 0 ? Color.FromRgb(0xC9, 0xC2, 0xB6) : TypeBrush.Color;

        // El borde de la cápsula, el mismo color muy oscurecido: un tipo claro no puede quedarse sin contorno.
        TypeOutline = Color.FromRgb(
            (byte)(TypeColour.R * 0.38), (byte)(TypeColour.G * 0.38), (byte)(TypeColour.B * 0.38));
        Category = sheet?.CategoryName ?? string.Empty;

        // La categoría con el icono oficial del cartucho, el rojo y el azul de siempre (§144). Uno de estado no lleva
        // icono sino «-----», como pidió el jugador; y si el icono no se pudo sacar de la ROM, la palabra.
        IsStatus = sheet?.Category == MoveSheet.Status;
        CategoryIcon = IsStatus || sheet is not { Category: MoveSheet.Physical or MoveSheet.Special } ? null : categoryIcon;
        CategoryText = IsStatus ? "-----" : CategoryIcon is null ? Category : string.Empty;

        // Potencia 0 es «no hace daño directo» y 1 es «variable»: ninguna de las dos es un número que leer.
        Power = sheet is { Power: > 1 } ? sheet.Power.ToString() : None;
        Accuracy = sheet is { Accuracy: > 0 } ? $"{sheet.Accuracy}%" : None;
        PP = sheet is { PP: > 0 } ? sheet.PP.ToString() : None;
        Description = sheet?.Description ?? string.Empty;
    }

    /// <summary>A slot of the four it knows; <paramref name="move"/> zero for an empty one.</summary>
    public static MoveCardViewModel Known(int slot, int move, MoveSheet? sheet, BitmapSource? categoryIcon) =>
        new(slot, move, sheet, $"{slot + 1}", categoryIcon);

    /// <summary>One it can remember, labelled by why.</summary>
    public static MoveCardViewModel Option(RememberableMove option, MoveSheet? sheet, BitmapSource? categoryIcon) =>
        new(-1, option.Move, sheet,
            option.From switch
            {
                RememberedFrom.Evolution => "EVO",
                RememberedFrom.Level when option.Level <= 1 => "INICIO",
                RememberedFrom.Level => $"Nv {option.Level}",
                _ => "ORIGEN"
            },
            categoryIcon);

    public int Slot { get; }

    public int Move { get; }

    public bool IsEmpty => Move == 0;

    public string Name { get; }

    public string TypeName { get; }

    public SolidColorBrush TypeBrush { get; }

    /// <summary>The same colour for the pixel panels, which take a colour and not a brush.</summary>
    public Color TypeColour { get; }

    public Color TypeOutline { get; }

    /// <summary>«Físico», «Especial», «Estado» or «?»: what the icon says, for anything that reads text.</summary>
    public string Category { get; }

    /// <summary>The cartridge's own icon for a physical or special move; null for status or when it is missing.</summary>
    public BitmapSource? CategoryIcon { get; }

    public bool HasCategoryIcon => CategoryIcon is not null;

    /// <summary>«-----» for a status move, the word when there is no icon, empty when the icon says it.</summary>
    public string CategoryText { get; }

    public bool IsStatus { get; }

    public string Power { get; }

    public string Accuracy { get; }

    public string PP { get; }

    /// <summary>What the game says the move does, as one paragraph; empty when the world has no text for it.</summary>
    public string Description { get; }

    public bool HasDescription => Description.Length > 0;

    /// <summary>The number of the slot, or why it can be remembered: «Nv 12», «EVO», «INICIO», «ORIGEN».</summary>
    public string Badge { get; }
}

/// <summary>One of the six stats in the ficha of MOVIMIENTOS, to weigh a physical move against a special one (§144).</summary>
/// <param name="NatureState">«up», «down» or «none»: what the nature does to it, for the colour of its name.</param>
/// <param name="IsEstimate">True for a Pokémon in a box whose stats could only be estimated; the value then reads «≈».</param>
public sealed record MoveStatViewModel(string Name, int? Value, string NatureState, bool IsEstimate)
{
    public string Text => Value is not { } value ? "?" : IsEstimate ? $"≈{value}" : value.ToString();
}

/// <summary>
/// RECUERDA MOVIMIENTOS: the move reminder of Pokémon Añil's randomlocke, inside the application.
/// </summary>
/// <remarks>
/// <para>
/// The game's own reminder is at the foot of the league and cannot be moved (§142), so this is it, earlier. What it
/// offers is <see cref="MoveReminder"/>'s rule — what it knew on arrival, and what its <b>current</b> species learns
/// on evolving and up to its level — and what it writes goes through <see cref="MoveReminderService"/>, which works
/// the list out again itself, writes first and records the <c>MoveRemembered</c> event after.
/// </para>
/// <para>
/// Same shape as ENTRENAR EV on purpose: party and boxes on the left, the Pokémon on the right. Pick a move it can
/// remember, pick which of its four it forgets, press RECORDAR.
/// </para>
/// </remarks>
public sealed partial class MoveReminderViewModel : SectionViewModel
{
    private readonly IBoxReader _boxes;
    private readonly IStatForecast _forecast;
    private readonly PokemonSpriteService _sprites;
    private readonly MoveReminderService _reminder;
    private readonly IRunContext _runContext;
    private readonly IPokemonRepository _registered;
    private readonly ITypeLookup _types;

    /// <summary>One or two types of the Pokémon picked, as coloured plates (§176).</summary>
    public System.Collections.ObjectModel.ObservableCollection<ViewerTypeBadge> SelectedTypes { get; } = [];
    private readonly ILogger<MoveReminderViewModel> _logger;

    private BoxSnapshot? _snapshot;

    private HashSet<uint> _fallen = [];

    /// <summary>A Pokémon to reselect once the partida has been read again.</summary>
    private uint? _wanted;

    private bool _switchingSelection;

    /// <summary>Bumped on every selection, so a slow read for a Pokémon already left behind is dropped.</summary>
    private int _generation;

    public MoveReminderViewModel(IBoxReader boxes, PokemonSpriteService sprites, MoveReminderService reminder,
        IRunContext runContext, IPokemonRepository registered, ITypeLookup types, IStatForecast forecast,
        ILogger<MoveReminderViewModel> logger)
        : base("MOVIMIENTOS", "El recuerda-movimientos: lo que tu Pokémon puede volver a aprender, como en Añil")
    {
        Fleeting.Fade(this, nameof(Status));

        _boxes = boxes;
        _forecast = forecast;
        _sprites = sprites;
        _reminder = reminder;
        _runContext = runContext;
        _registered = registered;
        _types = types;
        _logger = logger;
    }

    public override string IconKey => "IconRefresh";

    public override GameNeed Needs => GameNeed.Closed;

    public override Task ActivateAsync() => IsTeaching ? Task.CompletedTask : LoadAsync();

    public override void ResetState()
    {
        if (IsTeaching)
        {
            return;
        }

        _switchingSelection = true;
        SelectedSlot = null;
        SelectedPartySlot = null;
        _switchingSelection = false;
        Select(null);
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

    public int BoxTheme => SelectedBox?.Number ?? 1;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message), nameof(HasMessage))]
    private string _problem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message), nameof(HasMessage))]
    private string _notice = string.Empty;

    public string Message => Problem.Length > 0 ? Problem : Notice;

    public bool HasMessage => Message.Length > 0;

    private bool CanReload() => !IsTeaching;

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

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

            // No se suelta al Pokémon abierto antes de leer: si vuelve a estar en la partida, se queda abierto. Soltarlo
            // aquí era lo que cerraba la ficha al pulsar RECORDAR, que relee la partida para enseñar lo aprendido.
            Boxes.Clear();

            if (!_snapshot.Available)
            {
                Party.Clear();
                Slots.Clear();
                Select(null);
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

            SelectedBox = target is { IsInParty: false }
                ? Boxes.FirstOrDefault(box => box.Number == target.Box + 1)
                : Boxes.FirstOrDefault(box => box.Count > 0) ?? Boxes.FirstOrDefault();

            ShowBox(SelectedBox);

            if (target is null)
            {
                Select(null);
                return;
            }

            // El marco se pone a mano, sin pasar por el clic: el clic se niega mientras se escribe, y justo después de
            // escribir es cuando se relee. Y se vuelve a elegir con el registro recién leído, porque el de antes
            // todavía tiene los movimientos que sabía antes de aprender.
            _switchingSelection = true;

            try
            {
                SelectedPartySlot = target.IsInParty ? Party.FirstOrDefault(t => t.Pokemon?.Pid == target.Pid) : null;
                SelectedSlot = target.IsInParty ? null : Slots.FirstOrDefault(t => t.Pokemon?.Pid == target.Pid);
            }
            finally
            {
                _switchingSelection = false;
            }

            Select(target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer la partida para el recuerda-movimientos");
            Problem = "No se ha podido leer la partida.";
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

    private void Picked(BoxSlotViewModel? value, bool fromParty)
    {
        if (_switchingSelection || value is null)
        {
            return;
        }

        if (value.Pokemon is not { } chosen || IsTeaching)
        {
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

    private string BoxName(int number) =>
        Boxes.FirstOrDefault(box => box.Number == number)?.Name ?? $"Caja {number}";

    // ============================================================ EL POKÉMON

    [ObservableProperty]
    private BoxedPokemon? _selected;

    [ObservableProperty]
    private BitmapSource? _selectedSprite;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>False for an egg, a damaged entry or a fallen one: shown, never taught.</summary>
    [ObservableProperty]
    private bool _canEdit;

    [ObservableProperty]
    private bool _isFallen;

    /// <summary>Why this one cannot learn anything, or empty when it can.</summary>
    [ObservableProperty]
    private string _blocked = string.Empty;

    public int SelectedTheme => Selected is { IsInParty: true } || Selected is null
        ? Views.BoxWallpaper.PartyTheme
        : Selected.Box + 1;

    public string Where => Selected is not { } pokemon
        ? string.Empty
        : pokemon.IsInParty
            ? $"Equipo · puesto {pokemon.Slot + 1}"
            : $"{BoxName(pokemon.Box + 1)} · hueco {pokemon.Slot + 1}";

    /// <summary>The level the list is worked out at, as the game shows it.</summary>
    [ObservableProperty]
    private int _level;

    private static readonly string[] StatNames = ["PS", "ATAQUE", "DEFENSA", "AT. ESP.", "DEF. ESP.", "VELOCIDAD"];

    /// <summary>
    /// Its six stats, as ENTRENAR EV shows them: the party's own, and for one in a box what the game will work out from
    /// the installed world's base stats — or the old estimate, marked as one, when those are not available.
    /// </summary>
    public ObservableCollection<MoveStatViewModel> Stats { get; } = [];

    private void ShowStats(BoxedPokemon? pokemon)
    {
        Stats.Clear();

        if (pokemon is null || pokemon.IsEgg)
        {
            return;
        }

        var computed = pokemon.IsInParty ? null : _forecast.With(pokemon, pokemon.Evs);
        var estimate = !pokemon.IsInParty && computed is null;

        for (var stat = 0; stat < StatNames.Length; stat++)
        {
            var value = computed?[stat] ?? (stat < pokemon.Stats.Count ? pokemon.Stats[stat] : (int?)null);
            var nature = _forecast.NatureEffect(pokemon, stat) switch
            {
                > 0 => "up",
                < 0 => "down",
                _ => "none"
            };

            Stats.Add(new MoveStatViewModel(StatNames[stat], value, nature, estimate));
        }
    }

    /// <summary>Its four slots, empty ones included.</summary>
    public ObservableCollection<MoveCardViewModel> Known { get; } = [];

    /// <summary>What it can remember, in the order the groups are listed.</summary>
    public ObservableCollection<MoveCardViewModel> Options { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Plan))]
    [NotifyCanExecuteChangedFor(nameof(RememberCommand))]
    private MoveCardViewModel? _selectedKnown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Plan))]
    [NotifyCanExecuteChangedFor(nameof(RememberCommand))]
    private MoveCardViewModel? _selectedOption;

    [ObservableProperty]
    private bool _hasOptions;

    /// <summary>What the list is made of, and what it could not know. Empty when there is nothing to add.</summary>
    [ObservableProperty]
    private string _sourceNote = string.Empty;

    /// <summary>The sentence over the button: what pressing it will do.</summary>
    public string Plan
    {
        get
        {
            if (SelectedOption is not { } option)
            {
                return "Elige un movimiento de la lista.";
            }

            if (SelectedKnown is not { } slot)
            {
                return $"Elige qué movimiento olvida para aprender {option.Name}.";
            }

            return slot.IsEmpty
                ? $"Aprenderá {option.Name} en un hueco libre."
                : $"Olvidará {slot.Name} y aprenderá {option.Name}.";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    public bool HasStatus => Status.Length > 0;

    [ObservableProperty]
    private bool _failed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RememberCommand), nameof(LoadCommand))]
    private bool _isTeaching;

    private void Select(BoxedPokemon? pokemon)
    {
        _generation++;

        // El mismo Pokémon releído, como después de RECORDAR: sus listas no se vacían aquí sino que las sustituye
        // ShowMovesAsync de una vez. Vaciarlas encogía la columna y la devolvía arriba del todo.
        var refresh = pokemon is not null && Selected?.Pid == pokemon.Pid;

        Selected = pokemon;
        HasSelection = pokemon is not null;
        SelectedSprite = pokemon is null ? null : SpriteFor(pokemon);
        SelectedTypes.Clear();
        foreach (var badge in TypeBadges.For(_types, pokemon))
        {
            SelectedTypes.Add(badge);
        }
        IsFallen = pokemon is not null && _fallen.Contains(pokemon.Pid);
        Level = pokemon?.LevelForStats ?? 0;
        ShowStats(pokemon);

        Blocked = pokemon switch
        {
            null => string.Empty,
            { IsEgg: true } => "Un huevo no aprende movimientos.",
            { IsIntact: false } => "Está dañado en la partida: no se toca.",
            _ when IsFallen => "Está caído. Un caído no aprende nada.",
            _ => string.Empty
        };

        CanEdit = pokemon is not null && Blocked.Length == 0;
        Status = string.Empty;
        Failed = false;

        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(Where));

        if (!refresh)
        {
            Known.Clear();
            Options.Clear();
            SelectedKnown = null;
            SelectedOption = null;
            HasOptions = false;
            SourceNote = string.Empty;
        }

        if (pokemon is not null)
        {
            _ = ShowMovesAsync(pokemon, _generation);
        }
    }

    /// <summary>Asks the service for the list: it reads the run's history, so it is not instant.</summary>
    private async Task ShowMovesAsync(BoxedPokemon pokemon, int generation)
    {
        try
        {
            var runId = _runContext.Current?.Id ?? Guid.Empty;
            var options = await _reminder.OptionsAsync(runId, pokemon);

            if (generation != _generation)
            {
                return;
            }

            // Vaciar y llenar en el mismo paso, sin nada que esperar entre medias: la pantalla no llega a dibujarse
            // vacía, así que la columna no se encoge y el desplazamiento se queda donde estaba.
            Known.Clear();
            Options.Clear();
            SelectedOption = null;

            for (var slot = 0; slot < options.Known.Count; slot++)
            {
                var move = options.Known[slot];
                var sheet = move == 0 ? null : _reminder.Describe(move);
                Known.Add(MoveCardViewModel.Known(slot, move, sheet, CategoryIconOf(sheet)));
            }

            // Todos seguidos, sin apartados: el orden ya dice de dónde viene cada uno, y la placa de al lado también.
            foreach (var option in options.Moves)
            {
                var sheet = _reminder.Describe(option.Move);
                Options.Add(MoveCardViewModel.Option(option, sheet, CategoryIconOf(sheet)));
            }

            HasOptions = Options.Count > 0;

            // El hueco libre se ofrece ya elegido: no hay nada que decidir si no va a olvidar nada.
            SelectedKnown = Known.FirstOrDefault(card => card.IsEmpty);

            var notes = new List<string>
            {
                $"Aprendizajes del {_reminder.Source}, a nivel {options.Level}."
            };

            if (!options.Learnset)
            {
                notes.Add("No se ha podido leer lo que aprende su especie: la lista está incompleta.");
            }

            if (!options.FirstKnownRecorded && pokemon.RelearnMoveIds?.Any(move => move > 0) != true)
            {
                notes.Add("Lo que sabía al llegar no quedó apuntado: se registró antes de que PermaLocke lo guardara.");
            }

            SourceNote = string.Join(" ", notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo preparar la lista de {Name}", pokemon.DisplayName);

            if (generation == _generation)
            {
                Failed = true;
                Status = "No se ha podido preparar la lista de movimientos.";
            }
        }
    }

    private bool CanRemember() => CanEdit && !IsTeaching && SelectedOption is not null && SelectedKnown is not null;

    /// <summary>
    /// Writes the chosen move over the chosen slot, then reads the partida again and stays on the same Pokémon.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemember))]
    private async Task RememberAsync()
    {
        if (Selected is not { } target || SelectedOption is not { } option || SelectedKnown is not { } slot)
        {
            return;
        }

        if (_runContext.Current is not { } run)
        {
            Failed = true;
            Status = "No hay ninguna run cargada.";
            return;
        }

        if (!_reminder.CanTeachNow(out var reason))
        {
            Failed = true;
            Status = reason;
            return;
        }

        IsTeaching = true;

        try
        {
            var result = await _reminder.TeachAsync(run, target, option.Move, slot.Slot);

            if (!result.Delivered)
            {
                Failed = true;
                Status = result.Message;
                return;
            }

            _logger.LogInformation("{Name} recuerda {Move} en el hueco {Slot}", target.DisplayName, option.Name,
                slot.Slot + 1);

            // La partida ha cambiado por debajo: releerla es lo que hace que los cuatro de arriba sean ciertos.
            _wanted = target.Pid;
            await LoadAsync();

            Failed = false;
            Status = slot.IsEmpty
                ? $"{target.DisplayName} ha aprendido {option.Name}."
                : $"{target.DisplayName} ha olvidado {slot.Name} y ha aprendido {option.Name}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al enseñar {Move} a {Name}", option.Name, target.DisplayName);
            Failed = true;
            Status = "No se ha podido escribir el movimiento.";
        }
        finally
        {
            IsTeaching = false;
        }
    }

    partial void OnCanEditChanged(bool value) => RememberCommand.NotifyCanExecuteChanged();

    /// <summary>The cartridge's icon for the move's category; status gets none, it is written as «-----».</summary>
    private BitmapSource? CategoryIconOf(MoveSheet? sheet) =>
        sheet is { Category: MoveSheet.Physical or MoveSheet.Special } ? _sprites.GetCategory(sheet.Category) : null;

    private BitmapSource? SpriteFor(BoxedPokemon pokemon) =>
        pokemon.IsEgg ? _sprites.GetEgg() : _sprites.Get(pokemon.Species, pokemon.Form);
}
