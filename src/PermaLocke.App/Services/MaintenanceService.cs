using PermaLocke.Infrastructure;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Rules.Services;

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
    TradedAwayReconciler traded,
    ProgressService progress,
    GameWatcher watcher,
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
        var tradedAway = team.Count(p => p.Status == PokemonStatus.Traded);

        var rows = new List<AuditRow>
        {
            new("Pokémon registrados", team.Count.ToString(), "ok"),
            new("En pie", alive.ToString(), "ok"),
            new("Caídos", dead.ToString(), "ok"),
            new("Entregados en wonder trade", tradedAway.ToString(), "ok"),

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

    /// <summary>Everything the run still counts as standing, newest first.</summary>
    public async Task<IReadOnlyList<PokemonEntry>> AliveAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return [];
        }

        var all = await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false);

        return [.. all.Where(p => p.Status == PokemonStatus.Alive).OrderByDescending(p => p.ObtainedAt)];
    }

    /// <summary>
    /// Marks one as fallen by hand, charging the penalty and writing the event like any other death.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists because the watcher <b>cannot</b> see every death, and that is structural rather
    /// than a bug to fix: fainting is an <em>event</em> and what PermaLocke reads is a <em>state</em>,
    /// sampled every few seconds. After a Totem battle the game heals the party before handing
    /// control back, so the Pokémon is at full HP again long before the next read. No amount of
    /// polling closes that window.
    /// </para>
    /// <para>
    /// This is not the §58 mistake of adding a button that duplicates detection. It covers a case
    /// detection is blind to, and the event says so: the source is the <b>player</b> and not
    /// AutoDetect, so the historial never claims PermaLocke saw something it did not.
    /// </para>
    /// </remarks>
    public async Task<string> MarkDeadAsync(Guid pokemonId, string why, CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return "No hay ninguna run cargada.";
        }

        var entry = (await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false))
            .FirstOrDefault(p => p.Id == pokemonId);

        if (entry is null)
        {
            return "Ese Pokémon ya no está en la run.";
        }

        if (entry.Status != PokemonStatus.Alive)
        {
            // Cobrar dos veces por la misma muerte es peor que no cobrar: el saldo deja de
            // reconstruirse sumando el historial.
            return $"{entry.Nickname ?? entry.SpeciesName} ya figura como {entry.Status}.";
        }

        var reason = string.IsNullOrWhiteSpace(why) ? "sin motivo anotado" : why.Trim();

        await watcher.RecordDeathAsync(entry, run.PlayerName, EventSource.Player,
            $"a mano: {reason}", ct).ConfigureAwait(false);

        logger.LogInformation("Muerte marcada a mano: {Pokemon} ({Motivo})",
            entry.Nickname ?? entry.SpeciesName, reason);

        return $"{entry.Nickname ?? entry.SpeciesName} marcado como caído. "
               + "La penalización se ha cobrado y queda en el historial como marca del jugador.";
    }

    /// <summary>Counts the dead that are still whole in the save, writing nothing.</summary>
    public async Task<DeathEnforcementReport> InspectDeathsAsync(CancellationToken ct = default) =>
        NewDeathEnforcer().Inspect(await RegisteredAsync(ct).ConfigureAwait(false));

    /// <summary>
    /// Turns every dead Pokémon of the run into a Shedinja inside the save, for good.
    /// </summary>
    /// <remarks>
    /// The watcher writes the marker into the running game, but that is memory: it survives only if
    /// the player saves, and it never happens for a death the watcher could not see. This writes the
    /// save itself, so it is permanent, and it is idempotent — one already marked is skipped.
    /// </remarks>
    public async Task<DeathEnforcementReport> EnforceDeathsAsync(CancellationToken ct = default) =>
        NewDeathEnforcer().Apply(await RegisteredAsync(ct).ConfigureAwait(false));

    private async Task<IReadOnlyList<PokemonEntry>> RegisteredAsync(CancellationToken ct) =>
        runContext.Current is { } run
            ? await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false)
            : [];

    private SaveDeathEnforcer NewDeathEnforcer() =>
        new(save, paths.SaveBackups, loggers.CreateLogger<SaveDeathEnforcer>());

    /// <summary>Works out which wonder-trade records can be closed, and writes nothing.</summary>
    public Task<TradedAwayReport> InspectTradedAsync(CancellationToken ct = default) =>
        runContext.Current is { } run
            ? traded.InspectAsync(run, ct)
            : Task.FromResult(NoRunTraded);

    /// <summary>Closes the ones it can place without guessing, leaving an event for each.</summary>
    public Task<TradedAwayReport> RepairTradedAsync(CancellationToken ct = default) =>
        runContext.Current is { } run
            ? traded.RepairAsync(run, ct)
            : Task.FromResult(NoRunTraded);

    private static readonly TradedAwayReport NoRunTraded =
        new(0, 0, [], [], false, "No hay ninguna run cargada.");

    /// <summary>
    /// The three numbers behind the level cap, which is the one figure that governs how the run is
    /// played and the one nobody can see the workings of.
    /// </summary>
    /// <remarks>
    /// The cap in force is the higher of two: what the achievements detect, and what somebody
    /// pressed back when HOME still had a button for it. Read as one number they are
    /// indistinguishable, and a run whose manual count ran ahead has a cap nothing justifies — this
    /// run reached 40 with two trials detected. Split apart, it is obvious which one is wrong.
    /// </remarks>
    public async Task<StageReadout> ReadStagesAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return new StageReadout(0, 0, 0, null);
        }

        return new StageReadout(
            run.ClearedStages,
            await progress.DetectedAsync(run, ct),
            await progress.ClearedAsync(run, ct),
            await progress.CurrentCapAsync(run, ct));
    }

    /// <summary>
    /// Sets the by-hand stage count, through the domain so the correction lands in the history.
    /// </summary>
    /// <remarks>
    /// Goes through <see cref="ProgressService.AdvanceAsync"/> and not near <c>run.json</c>:
    /// correcting a cap by editing the run file would be exactly the silent state change rule 4
    /// forbids. The detected count is never touched — it is what the cartridge says.
    /// </remarks>
    public async Task<string> SetManualStagesAsync(int wanted, CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return "No hay ninguna run cargada.";
        }

        if (wanted == run.ClearedStages)
        {
            return $"Ya está en {wanted}. No hay nada que cambiar.";
        }

        await progress.AdvanceAsync(run, wanted - run.ClearedStages, run.PlayerName, ct);

        // Se relee del contexto: no se da por buena una correccion que no se ha vuelto a ver.
        var after = runContext.Current;
        var readout = await ReadStagesAsync(ct);

        logger.LogInformation("Etapas a mano corregidas a {Wanted} (quedan en {Actual})",
            wanted, after?.ClearedStages);

        return $"Etapas a mano: {run.ClearedStages} → {after?.ClearedStages}. "
               + $"Tope en vigor: {readout.Cap?.ToString() ?? "sin definir"}.";
    }
}

/// <param name="Manual">Stages somebody marked by hand.</param>
/// <param name="Detected">Stages the achievements work out from the cartridge.</param>
/// <param name="InForce">The higher of the two, which is what the cap comes from.</param>
public sealed record StageReadout(int Manual, int Detected, int InForce, int? Cap);
