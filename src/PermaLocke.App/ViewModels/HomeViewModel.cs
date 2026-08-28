using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.ViewModels;

/// <param name="Timestamp">Local time, preformatted so the view needs no converter.</param>
/// <param name="IsGain">Set so the row can be coloured without a converter or a value parse.</param>
public sealed record EventRow(
    string Timestamp, string Type, string Description, string Points, bool IsGain, bool IsLoss);

/// <param name="Hp">Preformatted as "25/25" for the view.</param>
/// <param name="HpRatio">0 to 1, for the bar the view draws next to the number.</param>
/// <param name="HpState">"ok", "low", "critical" or "fainted" — the colour band, decided here
/// rather than by four thresholds copied into XAML.</param>
public sealed record TeamRow(
    string Name, string Level, string Hp, double HpRatio, string HpState, bool IsShiny);

/// <param name="State">Already in Spanish: the domain enum never reaches the screen.</param>
public sealed record IslandRow(string Name, string State);

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
        ILogger<HomeViewModel> logger) : base("HOME", "Estado de la run, equipo en vivo y últimos movimientos")
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
        _logger = logger;

        gameLink.SnapshotChanged += (_, snapshot) => _ = _ui.InvokeAsync(() =>
        {
            ApplySnapshot(snapshot);
            CapWarning = gameLink.CapProblem;
            return Task.CompletedTask;
        });

        gameLink.UnregisteredDetected += (_, members) => _ = _ui.InvokeAsync(() =>
        {
            ApplyDetections(members);
            return Task.CompletedTask;
        });

        // Un premio que llega solo tiene que decirlo: lo contrario es que aparezcan diez Super
        // Balls en la mochila y nadie sepa de dónde han salido.
        gameLink.RewardGiven += (_, given) => _ = _ui.InvokeAsync(() =>
        {
            AutoNotice = given.Message;
            return Task.CompletedTask;
        });

        gameLink.TeamWiped += (_, penalty) => _ = _ui.InvokeAsync(() =>
        {
            AutoNotice = $"Equipo caído. {penalty.Points} puntos"
                         + (penalty.Capped ? " (tope alcanzado)." : ".");
            return Task.CompletedTask;
        });

        // Lo que la app hace sola tiene que verse sola. Sin esto una muerte quedaba registrada y
        // cobrada bien, y HOME seguía enseñando el saldo viejo hasta que el jugador salía de la
        // sección y volvía a entrar: el trabajo hecho y sin verse, que se lee igual que no hecho.
        gameLink.RunDataChanged += (_, _) => _ = SafeRefreshAsync();

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

    /// <summary>The last thing PermaLocke did on its own, so nothing happens behind the player.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoNotice))]
    private string _autoNotice = string.Empty;

    public bool HasAutoNotice => AutoNotice.Length > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeRoleCommand))]
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

    public ObservableCollection<EventRow> RecentEvents { get; } = [];

    /// <summary>Shows exactly what the link can and cannot see, instead of an empty panel.</summary>
    private void ApplySnapshot(GameSnapshot snapshot)
    {
        GameLinkConnected = snapshot.Connected;
        LiveTeam.Clear();

        if (!snapshot.Connected)
        {
            GameLinkStatus = snapshot.Problem ?? "Sin conexión con el juego.";
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
                member.IsShiny));
        }

        GameLinkStatus = snapshot.Party.Count == 0
            ? "Conectado, pero el equipo está vacío."
            : $"En vivo · {snapshot.Party.Count} en el equipo · {snapshot.ReadAt:HH:mm:ss}";

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
            1 => "Detectado y sin registrar: " + Detected[0].SpeciesName
                 + " Nv. " + Detected[0].Level + " · " + Detected[0].MetLocationName
                 + (Detected[0].IsShiny ? " ✨" : string.Empty),
            _ => $"{Detected.Count} Pokémon del equipo están sin registrar. "
                 + $"El primero es {Detected[0].SpeciesName}."
        };
    }

    public override GameNeed Needs => GameNeed.Running;

    public override Task ActivateAsync() => SafeRefreshAsync();

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
                "Esto BORRA dos cosas, y no se pueden deshacer desde la aplicación:\n\n"
                + $"· La run «{run.Name}»: sus puntos, sus Pokémon registrados y todo su historial.\n"
                + "· Tu partida de Pokémon Ultra Luna. Se hace una copia antes, y al terminar se te "
                + "dice dónde ha quedado.\n\n"
                + "Lo que NO borra: la randomización instalada sigue puesta. La run nueva tendrá "
                + "otra seed, así que si quieres otro mundo hay que generar e instalar otra vez "
                + "desde RANDOMIZADOR.\n\n"
                + "Azahar tiene que estar cerrado del todo.\n\n¿Seguir?"))
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

        StartOverStatus = $"Borrado: {deleted.Events} eventos y {deleted.Pokemon} Pokémon de la run, "
                          + $"y la partida. {erased.Message}";

        await RefreshAsync();

        if (_dialogs.ShowCreateRun())
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// Opens the role migration.
    /// </summary>
    /// <remarks>
    /// Not something a run should normally do -- the role is chosen once, before the ROM is
    /// randomized -- but a role with no way in is a role nobody can play, and that is worse.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasRun))]
    private async Task ChangeRoleAsync()
    {
        if (_dialogs.ShowChangeRole())
        {
            await RefreshAsync();
        }
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
            ? $"{cleared} etapas superadas, contadas por los logros."
            : cleared == 0
                ? "Ninguna etapa superada todavía."
                : $"{cleared} etapas superadas.";

        PointsBalance = await _points.GetBalanceAsync(run.Id);

        var team = await _pokemon.GetAllAsync(run.Id);
        AliveCount = team.Count(p => p.Status == PokemonStatus.Alive);
        DeadCount = team.Count(p => p.Status == PokemonStatus.Dead);
        EncounterCount = team.Count;

        foreach (var row in await _events.GetLatestAsync(run.Id, 15))
        {
            RecentEvents.Add(new EventRow(
                row.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"),
                DisplayNames.Of(row.Type),
                row.Description,
                row.PointsDelta == 0 ? string.Empty : row.PointsDelta.ToString("+#;-#;0"),
                row.PointsDelta > 0,
                row.PointsDelta < 0));
        }

        var integrity = await _events.VerifyChainAsync(run.Id);
        IntegrityOk = integrity.IsValid;
        IntegrityStatus = integrity.IsValid
            ? $"Historial íntegro · {integrity.CheckedEvents} eventos verificados"
            : $"HISTORIAL ALTERADO · {integrity.Message}";
    }
}
