using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PermaLocke.Core.Domain;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Checking that the run adds up, and the one repair a player can need.
/// </summary>
/// <remarks>
/// Exists because the maintenance that keeps a run honest lived only in a terminal tool that is
/// not even shipped. See <see cref="MaintenanceService"/> for why that mattered.
/// </remarks>
public sealed partial class MaintenanceViewModel : SectionViewModel
{
    private readonly MaintenanceService _maintenance;
    private readonly IRunContext _runContext;
    private readonly IAppDialogs _dialogs;
    private readonly ILogger<MaintenanceViewModel> _logger;

    public MaintenanceViewModel(
        MaintenanceService maintenance,
        IRunContext runContext,
        IAppDialogs dialogs,
        ILogger<MaintenanceViewModel> logger)
        : base("MANTENIMIENTO", "Comprobar que la run cuadra y reparar lo que se haya desajustado")
    {
        _maintenance = maintenance;
        _runContext = runContext;
        _dialogs = dialogs;
        _logger = logger;

        _runContext.CurrentChanged += (_, _) => _ = AuditAsync();
    }

    public override string IconKey => "IconCheck";

    /// <summary>
    /// Closed. The PID repair writes the save file, so the game must not be running: see §51.
    /// The audit alone would not need it, but the badge answers for the whole screen and it has to
    /// fail towards the side that breaks nothing.
    /// </summary>
    public override GameNeed Needs => GameNeed.Closed;

    public ObservableCollection<AuditRow> Rows { get; } = [];

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private bool _healthy = true;

    [ObservableProperty]
    private string _pidStatus = string.Empty;

    /// <summary>
    /// How many PIDs the repair would write. Below zero means nobody has looked yet, which is a
    /// different thing from "there are none to write" and the buttons need to tell them apart.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairPidsCommand))]
    private int _pidsToWrite = -1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuditCommand))]
    [NotifyCanExecuteChangedFor(nameof(InspectPidsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RepairPidsCommand))]
    private bool _isBusy;

    private bool CanWork => !IsBusy && _runContext.Current is not null;

    private bool CanRepair => CanWork && PidsToWrite > 0;

    public override async Task ActivateAsync()
    {
        await AuditAsync();
        await LoadStagesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task AuditAsync()
    {
        IsBusy = true;

        try
        {
            var report = await _maintenance.AuditAsync();

            Rows.Clear();
            foreach (var row in report.Rows)
            {
                Rows.Add(row);
            }

            Headline = report.Headline;
            Healthy = report.Healthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la auditoría de la run");
            Headline = "No se ha podido auditar la run. El detalle está en la carpeta Logs.";
            Healthy = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reads the save and says what it would do, writing nothing.</summary>
    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task InspectPidsAsync()
    {
        IsBusy = true;
        PidStatus = "Leyendo la partida...";

        try
        {
            // Abre el fichero de partida y cruza 157 firmas: fuera del hilo de la interfaz.
            var report = await Task.Run(_maintenance.InspectPids);

            PidsToWrite = report.Given.Count;
            PidStatus = report.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la inspección de PID");
            PidsToWrite = 0;
            PidStatus = "No se ha podido leer la partida. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Writes the PIDs into the save, after saying exactly how many and asking.
    /// </summary>
    /// <remarks>
    /// It only ever runs after an inspection, so the number in the question is measured and not
    /// estimated. The copy of the save and the re-read are inside <see cref="SavePidRepair"/>,
    /// where the probe had them.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task RepairPidsAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Reparar los PID",
            $"Se van a escribir {PidsToWrite} PID en el fichero de partida.\n\n"
            + "Azahar tiene que estar CERRADO. Se hace una copia de la partida antes y se vuelve "
            + "a leer después para confirmarlo.\n\n¿Seguir?");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        PidStatus = "Escribiendo en la partida...";

        try
        {
            var report = await Task.Run(_maintenance.RepairPids);

            PidStatus = report.Message;
            PidsToWrite = report.Written ? 0 : report.Given.Count;

            await AuditAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la reparación de PID");
            PidStatus = "La reparación ha fallado. La copia previa está en Saves\\backup.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ============================================================ INTERCAMBIADOS

    [ObservableProperty]
    private string _tradedStatus = string.Empty;

    /// <summary>Records that can be closed. Below zero means nobody has looked yet.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairTradedCommand))]
    private int _tradedToClose = -1;

    private bool CanCloseTraded => CanWork && TradedToClose > 0;

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task InspectTradedAsync()
    {
        IsBusy = true;

        try
        {
            var report = await _maintenance.InspectTradedAsync();

            TradedToClose = report.Matched.Count;
            TradedStatus = Describe(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la inspección de intercambiados");
            TradedToClose = 0;
            TradedStatus = "No se ha podido mirar. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloseTraded))]
    private async Task RepairTradedAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Cerrar los entregados",
            $"Se van a marcar {TradedToClose} registro(s) como entregados en un wonder trade.\n\n"
            + "Esto no toca la partida: solo la run. Cada uno deja su propio evento en el "
            + "historial.\n\n¿Seguir?");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var report = await _maintenance.RepairTradedAsync();

            TradedStatus = report.Message;
            TradedToClose = report.Written ? 0 : report.Matched.Count;

            await AuditAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el cierre de intercambiados");
            TradedStatus = "No se ha podido escribir. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Says what was found, and names what it refuses to touch.
    /// </summary>
    /// <remarks>
    /// The disputed ones are spelled out rather than summarised: a player who is told "2 in
    /// dispute" learns nothing, and the whole reason they are left alone is that only a person can
    /// know which of two identical Giratina actually left.
    /// </remarks>
    private static string Describe(Core.Services.TradedAwayReport report)
    {
        var text = report.Message;

        if (report.Disputed.Count > 0)
        {
            text += Environment.NewLine + string.Join(Environment.NewLine, report.Disputed);
        }

        return text;
    }

    // ============================================================ ETAPAS

    [ObservableProperty]
    private string _stageSummary = string.Empty;

    /// <summary>What the player is about to set the by-hand count to.</summary>
    [ObservableProperty]
    private int _manualStages;

    [ObservableProperty]
    private string _stageStatus = string.Empty;

    private async Task LoadStagesAsync()
    {
        var readout = await _maintenance.ReadStagesAsync();

        ManualStages = readout.Manual;
        StageSummary = $"A mano: {readout.Manual}   ·   Por los logros: {readout.Detected}"
                       + $"   ·   En vigor: {readout.InForce}"
                       + $"   ·   Tope: {readout.Cap?.ToString() ?? "sin definir"}";
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task SetStagesAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Corregir las etapas a mano",
            $"Se va a poner el contador de etapas marcadas a mano en {ManualStages}.\n\n"
            + "Las que detectan los logros no se tocan: eso lo dice el cartucho. El tope en vigor "
            + "es el mayor de los dos.\n\nLa corrección queda en el historial.\n\n¿Seguir?");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;

        try
        {
            StageStatus = await _maintenance.SetManualStagesAsync(ManualStages);
            await LoadStagesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la corrección de etapas");
            StageStatus = "No se ha podido corregir. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================================================== MARCAR UNA MUERTE A MANO

    /// <summary>Los que la run cuenta como en pie, para poder señalar uno.</summary>
    public ObservableCollection<PokemonEntry> AlivePokemon { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarkDeadCommand))]
    private PokemonEntry? _selectedAlive;

    /// <summary>Qué pasó. Va al evento, así que dentro de un mes seguirá diciéndolo.</summary>
    [ObservableProperty]
    private string _deathReason = string.Empty;

    [ObservableProperty]
    private string _deathStatus = string.Empty;

    private bool CanMarkDead => !IsBusy && SelectedAlive is not null;

    private async Task LoadAliveAsync()
    {
        var alive = await _maintenance.AliveAsync();

        AlivePokemon.Clear();

        foreach (var entry in alive)
        {
            AlivePokemon.Add(entry);
        }
    }

    /// <summary>
    /// Marks one as fallen when the watcher could not see it.
    /// </summary>
    /// <remarks>
    /// Asks first, and says out loud that the penalty is charged, because this is the one button
    /// here that <b>takes points away</b>. Everything else on this screen repairs bookkeeping.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanMarkDead))]
    private async Task MarkDeadAsync()
    {
        if (SelectedAlive is not { } entry)
        {
            return;
        }

        var name = entry.Nickname ?? entry.SpeciesName;

        if (!_dialogs.Confirm(
                "Marcar como caído",
                $"{name} pasa a contar como muerto.\n\n"
                + "Se cobra la penalización y queda en el historial como marca TUYA, no como algo "
                + "que la aplicación haya visto.\n\n"
                + "Esto no se puede deshacer desde aquí.\n\n¿Seguro?"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            DeathStatus = await _maintenance.MarkDeadAsync(entry.Id, DeathReason);
            DeathReason = string.Empty;
            SelectedAlive = null;
            await LoadAliveAsync();
            await AuditAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló marcar una muerte a mano");
            DeathStatus = "No se ha podido marcar. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================================================== HACER PERMANENTE LA MUERTE

    [ObservableProperty]
    private string _shedinjaSummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnforceDeathsCommand))]
    private bool _hasDeathsToMark;

    [ObservableProperty]
    private string _shedinjaStatus = string.Empty;

    private bool CanEnforceDeaths => !IsBusy && HasDeathsToMark;

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task InspectDeathsAsync()
    {
        IsBusy = true;

        try
        {
            var report = await _maintenance.InspectDeathsAsync();
            HasDeathsToMark = report.Alive > 0;
            ShedinjaSummary = $"Sin marcar: {report.Alive}   ·   Ya son Shedinja: {report.AlreadyMarked}"
                              + $"   ·   No están en la partida: {report.Missing}";
            ShedinjaStatus = report.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló mirar los caídos sin marcar");
            ShedinjaStatus = "No se ha podido mirar. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEnforceDeaths))]
    private async Task EnforceDeathsAsync()
    {
        if (!_dialogs.Confirm(
                "Convertir a los caídos en Shedinja",
                "Se escribe en tu PARTIDA, así que Azahar tiene que estar cerrado.\n\n"
                + "Cada Pokémon caído pasa a ser un Shedinja llamado MUERTO, de nivel 1 y sin "
                + "movimientos. Es PERMANENTE: queda escrito en la partida, no en memoria.\n\n"
                + "Se hace una copia de la partida antes de tocar nada.\n\n¿Seguir?"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var report = await _maintenance.EnforceDeathsAsync();
            ShedinjaStatus = report.Message;
            await InspectDeathsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló marcar a los caídos como Shedinja");
            ShedinjaStatus = "No se ha podido. ¿Está Azahar cerrado? El detalle está en Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
