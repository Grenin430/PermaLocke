using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
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
        ILogger<HomeViewModel> logger) : base("HOME", "Estado de la run, equipo en vivo y últimos movimientos")
    {
        _runContext = runContext;
        _events = events;
        _points = points;
        _pokemon = pokemon;
        _dialogs = dialogs;
        _ui = ui;
        _progress = progress;
        _logger = logger;

        gameLink.SnapshotChanged += (_, snapshot) => _ = _ui.InvokeAsync(() =>
        {
            ApplySnapshot(snapshot);
            return Task.CompletedTask;
        });

        gameLink.UnregisteredDetected += (_, members) => _ = _ui.InvokeAsync(() =>
        {
            ApplyDetections(members);
            return Task.CompletedTask;
        });

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCaptureCommand))]
    private bool _hasRun;

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

    public override Task ActivateAsync() => SafeRefreshAsync();

    [RelayCommand]
    private async Task CreateRunAsync()
    {
        if (_dialogs.ShowCreateRun())
        {
            await RefreshAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasRun))]
    private async Task RegisterCaptureAsync()
    {
        if (_dialogs.ShowRegisterCapture())
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// Records that a trial or milestone has been cleared, which lowers the ceiling the run
    /// enforces. Explicit on purpose: the flags that would let this be detected from memory
    /// have not been located, and moving the cap by guesswork is exactly what must not happen.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasRun))]
    private async Task AdvanceStageAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        await _progress.AdvanceAsync(run, 1, run.PlayerName);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var run = _runContext.Current;
        HasRun = run is not null;

        RecentEvents.Clear();

        if (run is null)
        {
            RunName = GameName = SeedLabel = PlayerName = string.Empty;
            PointsBalance = AliveCount = DeadCount = EncounterCount = 0;
            Islands = [];
            IntegrityStatus = string.Empty;
            return;
        }

        RunName = run.Name;
        GameName = run.Game == GameVersion.UltraMoon ? "Pokémon Ultra Luna" : "Pokémon Ultra Sol";
        SeedLabel = run.SeedLabel;
        PlayerName = run.PlayerName;
        Islands = [.. run.Islands.Select(i => new IslandRow(i.Name, DisplayNames.Of(i.State)))];

        // La etapa la deducen los logros: en cuanto el cristal Z de la prueba entra en la
        // mochila, el cap sube solo. El botón de abajo es red de seguridad, no el camino normal.
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
