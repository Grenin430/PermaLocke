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
    ZoneOutcomeService zones,
    BallControlService balls,
    IItemWithholder bag,
    IItemLookup items,
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

            new("Sin reconocer", withoutPid.ToString(),
                withoutPid == 0 ? "ok" : "bad",
                withoutPid == 0 ? string.Empty : "Arréglalo más abajo."),

            new("Historial", integrity.IsValid ? "correcto" : "DAÑADO",
                integrity.IsValid ? "ok" : "bad",
                integrity.IsValid ? string.Empty : "Restaura una copia de la run."),

            new("Saldo de puntos", balance.ToString(), "ok"),

            new("Copias de seguridad", CountBackups().ToString(), CountBackups() > 0 ? "ok" : "warn"),
        };

        var problems = rows.Count(r => r.State == "bad");

        return new AuditReport(rows, problems == 0,
            problems == 0
                ? "Todo correcto."
                : $"{problems} cosa(s) que arreglar.");
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

    /// <summary>A death marked by hand has just been recorded.</summary>
    public event EventHandler<DeathNotice>? MarkedDead;

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

    /// <summary>Everything the run counts as fallen, most recent death first.</summary>
    public async Task<IReadOnlyList<PokemonEntry>> FallenAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return [];
        }

        var all = await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false);

        return [.. all.Where(p => p.Status == PokemonStatus.Dead).OrderByDescending(p => p.DiedAt)];
    }

    /// <summary>
    /// Undoes a death PermaLocke should not have recorded.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="MarkDeadAsync"/>: a death marked by hand on the wrong Pokémon
    /// takes points and holds it at zero, and there was no way back short of editing the database.
    /// The history keeps the death and adds the revocation beside it
    /// (<see cref="GameWatcher.RevokeDeathAsync"/>); nothing is edited.
    /// </remarks>
    public async Task<string> RevokeDeathAsync(Guid pokemonId, string why, CancellationToken ct = default)
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

        var name = entry.Nickname ?? entry.SpeciesName;
        var refused = await watcher.RevokeDeathAsync(entry, run.PlayerName, why, ct).ConfigureAwait(false);

        if (refused is not null)
        {
            return refused;
        }

        logger.LogInformation("Muerte revocada a mano: {Pokemon} ({Motivo})", name, why);

        return $"{name} vuelve a estar vivo y has recuperado los puntos. Cúralo en un Centro Pokémon.";
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
            return $"{entry.Nickname ?? entry.SpeciesName} ya no está vivo.";
        }

        var reason = string.IsNullOrWhiteSpace(why) ? "sin motivo anotado" : why.Trim();

        var charged = await watcher.RecordDeathAsync(entry, run.PlayerName, EventSource.Player,
            $"a mano: {reason}", ct).ConfigureAwait(false);

        logger.LogInformation("Muerte marcada a mano: {Pokemon} ({Motivo})",
            entry.Nickname ?? entry.SpeciesName, reason);

        // Una muerte marcada a mano también tiene su momento: son justo las que la app no llegó a
        // ver, y quedarse sin la pantalla por eso sería castigar dos veces al mismo Pokémon.
        try
        {
            MarkedDead?.Invoke(this, new DeathNotice(entry.Nickname ?? entry.SpeciesName, entry.Species,
                charged.Points, entry.Form));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Un oyente falló al recibir una muerte marcada a mano");
        }

        return $"{entry.Nickname ?? entry.SpeciesName} marcado como caído.";
    }

    /// <summary>Counts the dead still standing in the save's party, writing nothing.</summary>
    public async Task<DeathEnforcementReport> InspectDeathsAsync(CancellationToken ct = default) =>
        NewDeathEnforcer().Inspect(await RegisteredAsync(ct).ConfigureAwait(false));

    /// <summary>
    /// Leaves every dead Pokémon of the run at zero HP in the save, as themselves.
    /// </summary>
    /// <remarks>
    /// The whole death marker. The monitor calls it on its own when the emulator closes, which is
    /// the only moment the save can be written, and the button in MANTENIMIENTO is for when that
    /// did not happen — the application was shut, or the write failed. It is idempotent, so both
    /// paths can run over each other without doing anything twice.
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
            return $"Ya está en {wanted}.";
        }

        await progress.AdvanceAsync(run, wanted - run.ClearedStages, run.PlayerName, ct);

        // Se relee del contexto: no se da por buena una correccion que no se ha vuelto a ver.
        var after = runContext.Current;
        var readout = await ReadStagesAsync(ct);

        logger.LogInformation("Etapas a mano corregidas a {Wanted} (quedan en {Actual})",
            wanted, after?.ClearedStages);

        return $"Corregido. Nivel máximo: {readout.Cap?.ToString() ?? "sin límite"}.";
    }

    // ================================================== UNA ZONA MARCADA POR ERROR

    /// <summary>Every zone the map currently has marked, newest first.</summary>
    /// <remarks>
    /// The name comes from the event that marked it, because a zone id is not something to show anybody: the one
    /// this exists for reads <c>ruta-1-afueras-de-hauoli</c>.
    /// </remarks>
    public async Task<IReadOnlyList<MarkedZone>> MarkedZonesAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return [];
        }

        var marks = await zones.GetMarksAsync(run.Id, ct).ConfigureAwait(false);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var gameEvent in await events.GetAllAsync(run.Id, ct).ConfigureAwait(false))
        {
            if (gameEvent is { Type: GameEventType.ZoneOutcomeSet, LocationId: { } id }
                && gameEvent.Data.GetValueOrDefault("zona") is { Length: > 0 } name)
            {
                names[id] = name;
            }
        }

        return
        [
            .. marks
                .Select(pair => new MarkedZone(pair.Key, names.GetValueOrDefault(pair.Key, pair.Key),
                    ZoneOutcomeService.Label(pair.Value.Outcome), !pair.Value.ByPlayer, pair.Value.At))
                .OrderByDescending(zone => zone.When)
        ];
    }

    /// <summary>
    /// Gives a zone back its encounter, for when PermaLocke put one in the wrong place.
    /// </summary>
    /// <remarks>
    /// The route stops counting as spent and the mark comes off the map, both by adding one event and editing
    /// nothing (§67). What it does <b>not</b> do is touch the bag: if balls are still owed for that run they come
    /// back on their own once the guard sees the route free, and if they never left the saved game there is
    /// <see cref="ForgetWithheldAsync"/>, which is a different decision and has its own button.
    /// </remarks>
    public async Task<string> FreeZoneAsync(string locationId, string why, CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return "No hay ninguna run cargada.";
        }

        var zone = (await MarkedZonesAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(marked => string.Equals(marked.LocationId, locationId, StringComparison.Ordinal));

        if (zone is null)
        {
            return "Esa zona ya no está marcada.";
        }

        await zones.ClearAsync(run.Id, locationId, zone.Name, run.PlayerName, why, ct).ConfigureAwait(false);

        // Se relee: no se da por buena una corrección que no se ha vuelto a ver.
        var still = (await MarkedZonesAsync(ct).ConfigureAwait(false))
            .Any(marked => string.Equals(marked.LocationId, locationId, StringComparison.Ordinal));

        if (still)
        {
            logger.LogError("Se liberó {Zona} y sigue marcada", zone.Name);
            return "Se ha escrito la corrección y la zona sigue marcada.";
        }

        logger.LogInformation("Zona liberada a mano: {Zona} ({Motivo})", zone.Name, why);

        return $"{zone.Name} vuelve a estar libre.";
    }

    // ================================================== LO RETENIDO QUE NO SE DEBE

    /// <summary>What the ledger says this run took away and has not given back.</summary>
    public IReadOnlyList<WithheldItem> Withheld()
    {
        if (runContext.Current is not { } run)
        {
            return [];
        }

        return
        [
            .. balls.BallItemIds
                .Select(id => new WithheldItem(id, items.GetName(id), bag.Owed(run.Id, id)))
                .Where(item => item.Amount > 0)
                .OrderByDescending(item => item.Amount)
        ];
    }

    /// <summary>
    /// Writes off what is owed without giving anything back.
    /// </summary>
    /// <remarks>
    /// Only right when the withholding never reached the saved game, which is the case it was written for: the
    /// balls come out of the <b>live bag</b>, and a player who closed the emulator without saving still has them.
    /// Giving them back then would hand over balls nobody ever took (§147, and the measurement in
    /// <see cref="IItemWithholder.Forget"/>).
    /// </remarks>
    public string ForgetWithheld()
    {
        if (runContext.Current is not { } run)
        {
            return "No hay ninguna run cargada.";
        }

        var forgotten = Withheld()
            .Select(item => (item.Name, Amount: bag.Forget(run.Id, item.ItemId)))
            .Where(pair => pair.Amount > 0)
            .ToList();

        if (forgotten.Count == 0)
        {
            return "No había nada retenido.";
        }

        if (Withheld().Count > 0)
        {
            logger.LogError("Se dio por saldado lo retenido y el registro sigue debiendo algo");
            return "Se ha escrito y el registro sigue debiendo algo.";
        }

        logger.LogInformation("Retenciones dadas por saldadas: {Detalle}",
            string.Join(", ", forgotten.Select(pair => $"{pair.Name} x{pair.Amount}")));

        return "Saldado: " + string.Join(", ", forgotten.Select(pair => $"{pair.Name} x{pair.Amount}")) + ".";
    }
}

/// <param name="LocationId">The zone id the run stores, which is not for showing.</param>
/// <param name="Name">What the player reads.</param>
/// <param name="Outcome">How it is marked, already in Spanish.</param>
/// <param name="Detected">True when PermaLocke marked it; false for a click from before §118.</param>
/// <param name="When">When it was marked.</param>
public sealed record MarkedZone(string LocationId, string Name, string Outcome, bool Detected, DateTimeOffset When);

/// <param name="ItemId">The cartridge's id.</param>
/// <param name="Name">Its name in the game.</param>
/// <param name="Amount">How many are owed.</param>
public sealed record WithheldItem(int ItemId, string Name, int Amount);

/// <param name="Manual">Stages somebody marked by hand.</param>
/// <param name="Detected">Stages the achievements work out from the cartridge.</param>
/// <param name="InForce">The higher of the two, which is what the cap comes from.</param>
public sealed record StageReadout(int Manual, int Detected, int InForce, int? Cap);
