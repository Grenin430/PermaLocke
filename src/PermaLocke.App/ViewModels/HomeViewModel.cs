using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.ViewModels;

/// <param name="Timestamp">Local time, preformatted so the view needs no converter.</param>
/// <param name="IsGain">Set so the row can be coloured without a converter or a value parse.</param>
/// <param name="IconKey">The pixel icon of the kind of event (<see cref="EventIcons"/>).</param>
public sealed record EventRow(
    string Timestamp, string Type, string Description, string Points, bool IsGain, bool IsLoss,
    string IconKey = "IconDot");

/// <param name="Hp">Preformatted as "25/25" for the view.</param>
/// <param name="HpRatio">0 to 1, for the bar the view draws next to the number.</param>
/// <param name="HpState">"ok", "low", "critical" or "fainted" — the colour band, decided here
/// rather than by four thresholds copied into XAML.</param>
/// <param name="Sprite">The species icon out of the player's own cartridge, or null when the
/// sprites have not been extracted yet. The view falls back to the name, never to a blank.</param>
public sealed record TeamRow(
    string Name, string Level, string Hp, double HpRatio, string HpState, bool IsShiny,
    System.Windows.Media.Imaging.BitmapSource? Sprite = null);

/// <param name="State">Already in Spanish: the domain enum never reaches the screen.</param>
public sealed record IslandRow(string Name, string State);

/// <param name="Detail">Level and where it fell, on one line.</param>
/// <param name="Sprite">The cartridge icon, drawn washed out. Null when the sprites are not out.</param>
public sealed record FallenRow(string Name, string Detail,
    System.Windows.Media.Imaging.BitmapSource? Sprite = null);

/// <summary>One stop of the island tour on HOME's track: a trial, the league or the rematch.</summary>
/// <param name="State">"cleared", "current" (the one about to be faced, whose cap is in force) or "ahead".</param>
/// <param name="Icon">The picture its achievement declares — the trial's Z-Crystal — out of the player's cartridge.</param>
public sealed record StageMark(string Name, string Level, string State,
    System.Windows.Media.Imaging.BitmapSource? Icon = null)
{
    public bool IsCleared => State == "cleared";

    public bool IsCurrent => State == "current";

    public string Tip => $"{Name} · tope {Level}";
}

/// <summary>
/// The run dashboard. With no run it shows an explicit empty state and the button to create
/// one; it never displays zeroed-out statistics as if a run existed.
/// </summary>
public sealed partial class HomeViewModel : SectionViewModel
{
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IPointsService _points;
    private readonly IPokemonRepository _pokemon;
    private readonly IAppDialogs _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly ProgressService _progress;
    private readonly IRunRoles _roles;
    private readonly RunService _runs;
    private readonly SaveEraser _eraser;
    private readonly PokemonSpriteService _sprites;
    private readonly LevelCapTable _caps;
    private readonly IAchievementCatalog _achievements;
    private readonly ILogger<HomeViewModel> _logger;

    public HomeViewModel(
        IRunContext runContext,
        IEventStore events,
        IPointsService points,
        IPokemonRepository pokemon,
        IAppDialogs dialogs,
        IUiDispatcher ui,
        GameLinkMonitor gameLink,
        ProgressService progress,
        IRunRoles roles,
        RunService runs,
        SaveEraser eraser,
        PokemonSpriteService sprites,
        RunActivity activity,
        LevelCapTable caps,
        IAchievementCatalog achievements,
        ILogger<HomeViewModel> logger) : base("HOME", "Tu run y tu equipo")
    {
        _runContext = runContext;
        _events = events;
        _points = points;
        _pokemon = pokemon;
        _dialogs = dialogs;
        _ui = ui;
        _progress = progress;
        _roles = roles;
        _runs = runs;
        _eraser = eraser;
        _sprites = sprites;
        _caps = caps;
        _achievements = achievements;
        _logger = logger;

        gameLink.SnapshotChanged += (_, snapshot) => _ = _ui.InvokeAsync(() =>
        {
            ApplySnapshot(snapshot);
            CapWarning = gameLink.CapProblem;
            EncounterWarning = gameLink.EncounterProblem;
            return Task.CompletedTask;
        });

        gameLink.UnregisteredDetected += (_, members) => _ = _ui.InvokeAsync(() =>
        {
            ApplyDetections(members);
            return Task.CompletedTask;
        });

        // Lo que la app hace sola -premio entregado, caídos marcados al cerrar el emulador, equipo
        // caído- ya NO se dice aquí en un recuadro verde. Lo dicen los avisos flotantes
        // (PlayNotifications), que salen encima del juego en el momento en que pasa, que es cuando
        // se leen. Tenerlo en los dos sitios era decir lo mismo dos veces, y el de HOME además se
        // quedaba puesto hasta el siguiente aviso, contando algo de hace una hora como si fuera
        // nuevo.

        // Lo que la app hace sola tiene que verse sola. Sin esto una muerte quedaba registrada y
        // cobrada bien, y HOME seguía enseñando el saldo viejo hasta que el jugador salía de la
        // sección y volvía a entrar: el trabajo hecho y sin verse, que se lee igual que no hecho.
        gameLink.RunDataChanged += (_, _) => _ = SafeRefreshAsync();

        // Y CUALQUIER COSA QUE ESCRIBA EN LA CADENA. Comprar en la tienda, tirar del gacha o
        // cobrar un logro cambian el saldo, y hasta ahora la cifra de la cabecera -que sale de
        // aqui- no se enteraba hasta que cambiabas de seccion. Todo lo que mueve un punto escribe
        // un evento, asi que escuchar la cadena los cubre a todos, incluidos los que se añadan
        // despues sin acordarse de avisar.
        activity.Appended += (_, _) => _ = SafeRefreshAsync();

        // Fired from whatever thread finished loading the run, so the refresh has to be
        // marshalled to the UI thread before it touches the bound collections.
        _runContext.CurrentChanged += (_, _) => _ = SafeRefreshAsync();
    }

    /// <summary>Never lets a refresh failure escape as an unobserved exception and kill the app.</summary>
    private async Task SafeRefreshAsync()
    {
        try
        {
            await _ui.InvokeAsync(RefreshAsync);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al refrescar HOME");
        }
    }

    /// <summary>What EMPEZAR DE CERO did, or why it refused. Empty the rest of the time.</summary>
    [ObservableProperty]
    private string _startOverStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartOverCommand))]
    private bool _hasRun;

    /// <summary>Role of the run, as the catalogue names it. Empty while there is no run.</summary>
    [ObservableProperty]
    private string _roleName = string.Empty;

    [ObservableProperty]
    private string _runName = string.Empty;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _seedLabel = string.Empty;

    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private int _pointsBalance;

    [ObservableProperty]
    private int _aliveCount;

    [ObservableProperty]
    private int _deadCount;

    [ObservableProperty]
    private int _encounterCount;

    /// <summary>Stage the player is about to face, and the level ceiling it imposes.</summary>
    [ObservableProperty]
    private string _stageText = "—";

    [ObservableProperty]
    private string _levelCapText = "—";

    /// <summary>Says how many stages are cleared and who worked it out.</summary>
    [ObservableProperty]
    private string _stageSourceText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<IslandRow> _islands = [];

    /// <summary>Integrity of the event chain, checked every time HOME is shown.</summary>
    [ObservableProperty]
    private string _integrityStatus = string.Empty;

    [ObservableProperty]
    private bool _integrityOk = true;

    /// <summary>Live party read from the running game, empty when there is no link.</summary>
    public ObservableCollection<TeamRow> LiveTeam { get; } = [];

    /// <summary>
    /// Everyone the run has lost, newest first.
    /// </summary>
    /// <remarks>
    /// It fills the left column, which was half a screen of nothing, and it deliberately is NOT the
    /// live team: that strip already exists above and needs the emulator open. This comes out of the
    /// run's own database, so it is there with the game closed — and in a Nuzlocke the list of who
    /// is gone is the other half of the story, not a footnote.
    /// </remarks>
    public ObservableCollection<FallenRow> Fallen { get; } = [];

    [ObservableProperty]
    private bool _hasFallen;

    /// <summary>
    /// The island tour as a row of stops, lit up to where the run is.
    /// </summary>
    /// <remarks>
    /// The same count that decides the level cap, so the two can never disagree: a stop is cleared when
    /// <see cref="ProgressService.ClearedAsync"/> says so, and the one after it is the stage in force.
    /// </remarks>
    public ObservableCollection<StageMark> StageTrack { get; } = [];

    [ObservableProperty]
    private string _trialsText = string.Empty;

    [ObservableProperty]
    private string _gameLinkStatus = "Buscando el juego...";

    [ObservableProperty]
    private bool _gameLinkConnected;

    /// <summary>
    /// Why the level cap is not taking effect, or empty when it is.
    /// </summary>
    /// <remarks>
    /// A cap that quietly does nothing is worse than no cap: the player believes the run is being
    /// policed. If the correction cannot be written, or the game keeps undoing it, HOME says so.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCapWarning))]
    private string _capWarning = string.Empty;

    public bool HasCapWarning => CapWarning.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEncounterWarning))]
    private string _encounterWarning = string.Empty;
    public bool HasEncounterWarning => EncounterWarning.Length > 0;

    public ObservableCollection<EventRow> RecentEvents { get; } = [];

    /// <summary>Shows exactly what the link can and cannot see, instead of an empty panel.</summary>
    private void ApplySnapshot(GameSnapshot snapshot)
    {
        GameLinkConnected = snapshot.Connected;
        LiveTeam.Clear();

        if (!snapshot.Connected)
        {
            GameLinkStatus = snapshot.Problem ?? "Azahar no está abierto.";
            return;
        }

        foreach (var member in snapshot.Party)
        {
            var ratio = member.MaxHp > 0 ? (double)member.CurrentHp / member.MaxHp : 0d;

            LiveTeam.Add(new TeamRow(
                string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname,
                $"Nv. {member.Level}",
                $"{member.CurrentHp}/{member.MaxHp}",
                Math.Clamp(ratio, 0d, 1d),
                member.IsFainted ? "fainted" : ratio <= 0.2 ? "critical" : ratio <= 0.5 ? "low" : "ok",
                member.IsShiny,
                _sprites.Get(member.Species, member.Form)));
        }

        GameLinkStatus = snapshot.Party.Count == 0
            ? "Tu equipo está vacío."
            : "En directo";

        if (snapshot.Notice is { } notice)
        {
            GameLinkStatus += Environment.NewLine + notice;
        }
    }

    /// <summary>Pokémon the game shows but the run has not registered yet, oldest first.</summary>
    public ObservableCollection<LivePartyMember> Detected { get; } = [];

    [ObservableProperty]
    private string _detectionMessage = string.Empty;

    [ObservableProperty]
    private bool _hasDetection;

    /// <summary>
    /// Opens the capture form already filled from the game, zone included. The player still
    /// confirms: the rules may block or warn, and that decision is theirs to see and take.
    /// </summary>
    [RelayCommand]
    private async Task RegisterDetectedAsync()
    {
        if (Detected.FirstOrDefault() is not { } member)
        {
            return;
        }

        if (_dialogs.ShowRegisterCapture(member))
        {
            await RefreshAsync();
        }
    }

    private void ApplyDetections(IReadOnlyList<LivePartyMember> members)
    {
        Detected.Clear();

        foreach (var member in members)
        {
            Detected.Add(member);
        }

        HasDetection = Detected.Count > 0;

        DetectionMessage = Detected.Count switch
        {
            0 => string.Empty,
            1 => "Sin registrar: " + Detected[0].SpeciesName
                 + " Nv. " + Detected[0].Level + " · " + Detected[0].MetLocationName
                 + (Detected[0].IsShiny ? " · variocolor" : string.Empty),
            _ => $"{Detected.Count} Pokémon sin registrar."
        };
    }

    public override string IconKey => "IconHome";

    public override GameNeed Needs => GameNeed.Running;

    /// <summary>
    /// Extracts the cartridge icons before the first refresh, so the team strip has sprites the
    /// first time it is drawn instead of popping them in a second later.
    /// </summary>
    /// <remarks>
    /// Preparing twice is free -- the service keeps its index -- and it must not be fatal: with no
    /// ROM there are no sprites, and HOME still has everything else to show.
    /// </remarks>
    public override async Task ActivateAsync()
    {
        try
        {
            await _sprites.PrepareAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se han podido preparar los sprites para HOME");
        }

        await SafeRefreshAsync();
    }

    [RelayCommand]
    private async Task CreateRunAsync()
    {
        if (_dialogs.ShowCreateRun())
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// Wipes the run and the saved game, then opens the flow to create a new one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one destructive thing in PermaLocke, and it destroys two separate things that live in
    /// two separate places: the run -- events, Pokémon, points, folder -- and the player's Ultra
    /// Moon save. Both are gone for good. The save is copied first and the message says where the
    /// copy went, because "start over" and "lose a playthrough you meant to keep" look identical
    /// until the moment after.
    /// </para>
    /// <para>
    /// Two confirmations on purpose, and the second one is short. A long warning gets skimmed; a
    /// blunt second question after it does not. What it does <b>not</b> ask twice about is the
    /// randomization: the mod stays installed and the new run has a different seed, which is worth
    /// saying but is not the part that loses anything.
    /// </para>
    /// <para>
    /// Order: save first, run second. The save is the deletion that can refuse -- Azahar may be
    /// holding it -- so failing there leaves everything intact, whereas deleting the run first
    /// would leave a player with no run and the old partida still sitting on disk.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasRun))]
    private async Task StartOverAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Empezar de cero",
                $"Se borrarán tu run «{run.Name}» y tu partida de Ultra Luna. No se puede deshacer.\n\n"
                + "Azahar tiene que estar cerrado.\n\n¿Seguir?"))
        {
            return;
        }

        if (!_dialogs.Confirm("Última pregunta",
                $"Se borra la run «{run.Name}» y la partida. ¿Seguro?"))
        {
            return;
        }

        // Primero la partida: es la que puede negarse -Azahar puede tenerla abierta-, y fallar ahi
        // lo deja todo como estaba. Al reves, el jugador se quedaria sin run y con la partida vieja.
        var erased = await Task.Run(_eraser.Erase);

        if (!erased.Erased)
        {
            StartOverStatus = erased.Message;
            _logger.LogWarning("Empezar de cero cancelado: {Problem}", erased.Message);
            return;
        }

        var deleted = await _runs.DeleteAsync(run.Id);

        _logger.LogWarning("Run {Name} borrada: {Events} eventos, {Pokemon} Pokémon. {Save}",
            deleted.Name, deleted.Events, deleted.Pokemon, erased.Message);

        StartOverStatus = "Run y partida borradas.";

        await RefreshAsync();

        if (_dialogs.ShowCreateRun())
        {
            await RefreshAsync();
        }
    }

    private void BuildTrack(int cleared)
    {
        StageTrack.Clear();

        var stages = _caps.Stages.OrderBy(stage => stage.Order).ToList();
        var byId = _achievements.All.ToDictionary(achievement => achievement.Id);

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            var achievement = stage.Achievement is { } id && byId.TryGetValue(id, out var found) ? found : null;
            var icon = (achievement?.Icon ?? achievement?.Item) is { } item ? _sprites.GetItem(item) : null;
            var state = i < cleared ? "cleared" : i == cleared ? "current" : "ahead";
            StageTrack.Add(new StageMark(stage.Name, stage.Level.ToString(), state, icon));
        }

        // Las pruebas son las etapas que se ganan con un cristal Z; la liga y el rematch van aparte.
        var trials = stages.Count(stage => stage.Name.EndsWith("prueba", StringComparison.OrdinalIgnoreCase));
        TrialsText = trials == 0 ? string.Empty : $"{Math.Min(cleared, trials)} DE {trials} PRUEBAS";
    }

    private async Task RefreshAsync()
    {
        var run = _runContext.Current;
        HasRun = run is not null;

        RecentEvents.Clear();

        if (run is null)
        {
            RunName = GameName = SeedLabel = PlayerName = RoleName = string.Empty;
            PointsBalance = AliveCount = DeadCount = EncounterCount = 0;
            Fallen.Clear();
            StageTrack.Clear();
            TrialsText = string.Empty;
            HasFallen = false;
            Islands = [];
            IntegrityStatus = string.Empty;
            return;
        }

        RunName = run.Name;
        GameName = run.Game == GameVersion.UltraMoon ? "Pokémon Ultra Luna" : "Pokémon Ultra Sol";
        SeedLabel = run.SeedLabel;
        PlayerName = run.PlayerName;
        RoleName = _roles.Of(run.Id)?.Name ?? run.RoleId.ToUpperInvariant();
        Islands = [.. run.Islands.Select(i => new IslandRow(i.Name, DisplayNames.Of(i.State)))];

        // La etapa la deducen los logros: en cuanto el cristal Z de la prueba entra en la mochila,
        // el cap sube solo. No hay forma de moverla a mano, y es deliberado: el botón que había
        // hacía lo mismo que la detección y solo servía para adelantarla.
        var cleared = await _progress.ClearedAsync(run);
        var stage = await _progress.CurrentStageAsync(run);
        StageText = stage is null ? "sin definir" : stage.Name;
        LevelCapText = stage is null ? "—" : stage.Level.ToString();
        StageSourceText = cleared > run.ClearedStages
            ? $"{cleared} pruebas superadas."
            : cleared == 0
                ? "Ninguna prueba superada todavía."
                : $"{cleared} pruebas superadas.";

        BuildTrack(cleared);

        PointsBalance = await _points.GetBalanceAsync(run.Id);

        var team = await _pokemon.GetAllAsync(run.Id);
        AliveCount = team.Count(p => p.Status == PokemonStatus.Alive);
        DeadCount = team.Count(p => p.Status == PokemonStatus.Dead);
        EncounterCount = team.Count;

        Fallen.Clear();

        foreach (var dead in team
                     .Where(p => p.Status == PokemonStatus.Dead)
                     .OrderByDescending(p => p.DiedAt ?? p.ObtainedAt))
        {
            // Nivel y fecha, y NO la zona: LocationId es el identificador normalizado -«bahia-kalae»-
            // y en pantalla se leia como lo que es, un dato interno. Ponerlo bonito quitando guiones
            // perderia los acentos, o sea que enseñaria un nombre que no es el de la zona; el nombre
            // de verdad esta en zones.json y traerlo aqui es otra cosa, no un retoque visual.
            var when = dead.DiedAt?.LocalDateTime.ToString("dd/MM");

            Fallen.Add(new FallenRow(
                string.IsNullOrWhiteSpace(dead.Nickname) ? dead.SpeciesName : dead.Nickname,
                string.Join(" · ", new[] { $"Nv. {dead.Level}", when }
                    .Where(part => !string.IsNullOrWhiteSpace(part))),
                _sprites.Get(dead.Species, dead.Form)));
        }

        HasFallen = Fallen.Count > 0;

        foreach (var row in await _events.GetLatestAsync(run.Id, 15))
        {
            RecentEvents.Add(new EventRow(
                row.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"),
                DisplayNames.Of(row.Type),
                row.Description,
                row.PointsDelta == 0 ? string.Empty : row.PointsDelta.ToString("+#;-#;0"),
                row.PointsDelta > 0,
                row.PointsDelta < 0,
                EventIcons.Of(row.Type)));
        }

        var integrity = await _events.VerifyChainAsync(run.Id);
        IntegrityOk = integrity.IsValid;
        IntegrityStatus = integrity.IsValid
            ? "Historial correcto"
            : "HISTORIAL DAÑADO";
    }
}
