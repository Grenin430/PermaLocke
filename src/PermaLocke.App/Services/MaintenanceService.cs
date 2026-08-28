using PermaLocke.Infrastructure;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink;

namespace PermaLocke.App.Services;

/// <param name="Label">Row label, already in Spanish.</param>
/// <param name="Value">The figure.</param>
/// <param name="State">"ok", "warn" or "bad" — the colour band, decided here and not in XAML.</param>
/// <param name="Note">Why it matters, or empty when the number speaks for itself.</param>
public sealed record AuditRow(string Label, string Value, string State, string Note = "");

/// <param name="Rows">What the audit found.</param>
/// <param name="Healthy">True when nothing needs attention.</param>
/// <param name="Headline">One line saying whether the run is in order.</param>
public sealed record AuditReport(IReadOnlyList<AuditRow> Rows, bool Healthy, string Headline);

/// <summary>
/// The checks and repairs that until now only existed as <c>Probe</c> commands in a terminal.
/// </summary>
/// <remarks>
/// <para>
/// This is the fix for a real gap, not a convenience. <c>Probe</c> has sixty-two commands and the
/// folder handed to another player contains <c>PermaLocke.exe</c> and nothing else. When this run
/// drifted — 28 entries with no PID, 26 never closed after a wonder trade — it was repaired from a
/// command line. Somebody else's run would have drifted the same way and stayed broken, because
/// they have neither the tool nor anyone to ask.
/// </para>
/// <para>
/// Only the handful a player can actually act on are exposed. The rest of <c>Probe</c> is
/// investigation — memory sweeps, flag diffs, pattern hunts — and belongs in a terminal.
/// </para>
/// <para>
/// Nothing here decides anything about the competition: it counts what is stored and delegates
/// repairs to the services that already own them. The PID repair is
/// <see cref="SavePidRepair"/> exactly as the probe calls it, so it keeps its backup and its
/// re-read.
/// </para>
/// </remarks>
public sealed class MaintenanceService(
    IRunContext runContext,
    IPokemonRepository pokemon,
    IEventStore events,
    IPointsService points,
    RunBackup backup,
    PlayerSave save,
    AppPaths paths,
    ILoggerFactory loggers,
    ILogger<MaintenanceService> logger)
{
    /// <summary>
    /// Counts what is stored and says which figures mean something is wrong.
    /// </summary>
    /// <remarks>
    /// The one that matters most is <b>PID coverage</b>. The watcher matches the live party against
    /// the run by PID and by nothing else, so an entry without one is invisible to it: it can faint
    /// in front of the application and nothing is recorded. HOME cannot tell that apart from
    /// "nobody has died", which is why it gets its own row and its own warning.
    /// </remarks>
    public async Task<AuditReport> AuditAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return new AuditReport([], true, "No hay ninguna run cargada.");
        }

        var team = await pokemon.GetAllAsync(run.Id);
        var history = await events.GetAllAsync(run.Id);
        var balance = await points.GetBalanceAsync(run.Id);
        var integrity = await events.VerifyChainAsync(run.Id, ct);

        var withPid = team.Count(p => p.Pid is not null and not 0);
        var withoutPid = team.Count - withPid;

        var alive = team.Count(p => p.Status == PokemonStatus.Alive);
        var dead = team.Count(p => p.Status == PokemonStatus.Dead);
        var traded = team.Count(p => p.Status == PokemonStatus.Traded);

        var rows = new List<AuditRow>
        {
            new("Pokémon registrados", team.Count.ToString(), "ok"),
            new("En pie", alive.ToString(), "ok"),
            new("Caídos", dead.ToString(), "ok"),
            new("Entregados en wonder trade", traded.ToString(), "ok"),

            new("Detectables por el vigilante", withPid.ToString(),
                withPid == team.Count ? "ok" : "warn",
                "El vigilante empareja por PID y por nada más."),

            new("SIN PID", withoutPid.ToString(),
                withoutPid == 0 ? "ok" : "bad",
                withoutPid == 0
                    ? "Todos se pueden detectar."
                    : "Estos no se pueden detectar muertos: pueden caer delante de la aplicación "
                      + "y no se registra nada. Se arreglan aquí abajo."),

            new("Eventos en el historial", history.Count.ToString(), "ok"),

            new("Cadena de eventos", integrity.IsValid ? "íntegra" : "ROTA",
                integrity.IsValid ? "ok" : "bad",
                integrity.IsValid
                    ? "Cada evento encadena con el anterior por hash."
                    : "Los hashes no cuadran. Restaura una copia de la run."),

            new("Saldo de puntos", balance.ToString(), "ok"),

            new("Copias de la run", CountBackups().ToString(),
                CountBackups() > 0 ? "ok" : "warn",
                CountBackups() > 0
                    ? $"Se guarda una al arrancar y se conservan las {RunBackup.Keep} últimas."
                    : "Todavía no hay ninguna. Se hace la próxima vez que abras la aplicación."),
        };

        var problems = rows.Count(r => r.State == "bad");

        return new AuditReport(rows, problems == 0,
            problems == 0
                ? "La run cuadra: nada que reparar."
                : $"{problems} cosa(s) que arreglar. Están marcadas en rojo.");
    }

    private int CountBackups()
    {
        try
        {
            return System.IO.Directory.Exists(backup.Folder)
                ? System.IO.Directory.GetFiles(backup.Folder, "*.db").Length
                : 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se ha podido contar las copias de la run");
            return 0;
        }
    }

    /// <summary>
    /// Looks at the save and says how many registered Pokémon it could give a PID to, writing
    /// nothing.
    /// </summary>
    public PidRepairReport InspectPids() => NewPidRepair().Inspect();

    /// <summary>
    /// Writes the PIDs into the save. Keeps the guarantees the probe had: a copy of the save first
    /// and a re-read afterwards, both inside <see cref="SavePidRepair"/>.
    /// </summary>
    /// <remarks>
    /// Matches by the six IVs plus the shiny flag, which is the only signature that never changes,
    /// and accepts only signatures unique on both sides. A Pokémon it cannot place unambiguously is
    /// left alone rather than guessed at.
    /// </remarks>
    public PidRepairReport RepairPids() => NewPidRepair().Repair();

    private SavePidRepair NewPidRepair() =>
        new(save, paths.SaveBackups, loggers.CreateLogger<SavePidRepair>());
}
