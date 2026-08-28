using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public override Task ActivateAsync() => AuditAsync();

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
}
