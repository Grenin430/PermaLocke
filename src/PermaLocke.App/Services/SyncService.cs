using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.Infrastructure;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <param name="Snapshot">What that player's application published.</param>
/// <param name="IsMine">True for this machine's own run, so the table can mark it.</param>
/// <param name="Position">Place in the standings, 1 first. Ties share the number.</param>
/// <param name="Age">How long ago it was published, in words.</param>
/// <param name="Audit">Whether the snapshot comes out of the history published beside it.</param>
/// <param name="IsLegacy">Published with the old layout, loose in the root of the folder.</param>
public sealed record StandingRow(RunSnapshot Snapshot, bool IsMine, int Position, string Age,
    AuditResult Audit, bool IsLegacy = false)
{
    /// <summary>The chip on the row: short, and never a word that promises more than was checked.</summary>
    public string AuditLabel => Audit.Verdict switch
    {
        AuditVerdict.Consistent => "CUADRA",
        AuditVerdict.NoHistory => "SIN HISTORIAL",
        AuditVerdict.Rewound => "HA RETROCEDIDO",
        _ => "NO CUADRA"
    };

    /// <summary>"ok", "none" or "bad", for the chip's colour.</summary>
    public string AuditState => Audit.Verdict switch
    {
        AuditVerdict.Consistent => "ok",
        AuditVerdict.NoHistory => "none",
        _ => "bad"
    };
}

/// <param name="Profile">This machine's player, or null before one exists.</param>
/// <param name="Ownership">Whose the loaded run is.</param>
public sealed record PlayerStatus(PlayerProfile? Profile, RunOwnership Ownership);

/// <param name="Rows">Everyone in the shared folder, best first.</param>
/// <param name="Broken">Files that could not be read, by name and reason.</param>
/// <param name="BattleNote">Who can link-battle whom, in one line. Empty with nobody published.</param>
/// <param name="BattleState">"ok", "warn" or "none" — the colour band for the note.</param>
public sealed record Standings(
    IReadOnlyList<StandingRow> Rows, IReadOnlyList<string> Broken, string Message,
    string BattleNote = "", string BattleState = "none");

/// <summary>Where the shared folder is. Per machine, so it lives in <c>Config/</c>.</summary>
public sealed record SyncSettings
{
    public string SharedFolder { get; init; } = string.Empty;
}

/// <summary>
/// Publishing this run so the others can see it, and reading theirs.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shared folder is the whole transport.</b> Drive, Dropbox, OneDrive, a network share, a pen
/// drive. No server, no accounts, no protocol: for a competition between five friends, a folder
/// that already synchronises itself does the job, and it is the only design that works with nobody
/// hosting anything. Whoever has no shared folder can still export the file and send it however
/// they like — it is the same file.
/// </para>
/// <para>
/// <b>What it is not.</b> Everything read here is a file written by somebody else's application,
/// sitting in a folder they can open. Since §123 each player publishes their history beside their
/// snapshot, so the snapshot can be <b>checked against it</b>: points, event count and last hash have
/// to come out of a chain that verifies. That catches an edited number, a rewound run and a deleted
/// death. It does not catch somebody rebuilding a whole history with tools, and the screen says so;
/// rule 3 forbids calling this an anti-cheat.
/// </para>
/// </remarks>
public sealed class SyncService(
    IRunContext runContext,
    IEventStore events,
    IPokemonRepository pokemon,
    ProgressService progress,
    AchievementService achievements,
    IRunRoles roles,
    PlayerProfileService profiles,
    SnapshotStore store,
    SeenMarksStore seenMarks,
    IBoxReader boxes,
    AppPaths paths,
    ILogger<SyncService> logger)
{
    private string SettingsPath => Path.Combine(paths.Config, "sync.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string SharedFolder => paths.LocalOnly ? string.Empty : new SharedFolderSettings(paths.Config).Read();

    public void SetSharedFolder(string folder)
    {
        new SharedFolderSettings(paths.Config).Write(folder);
        logger.LogInformation("Carpeta compartida: {Folder}", folder);
    }

    /// <summary>This machine's player and whose the loaded run is, linking an unowned run on the way.</summary>
    public async Task<PlayerStatus> PlayerAsync(CancellationToken ct = default)
    {
        var ownership = await profiles.LinkCurrentRunAsync(ct);
        var profile = ownership == RunOwnership.NoRun
            ? await profiles.CurrentAsync(ct)
            : await profiles.EnsureAsync(runContext.Current?.PlayerName, ct);

        return new PlayerStatus(profile, ownership);
    }

    /// <summary>
    /// Builds the summary of the loaded run and the history it comes from. Reads only; publishing is
    /// a separate step.
    /// </summary>
    /// <remarks>
    /// Both out of <b>one</b> read of the chain, and the points summed from it rather than asked of
    /// the points service a moment later: an event landing in between would otherwise publish a
    /// snapshot that does not match its own history, and the audit would call an honest run wrong.
    /// </remarks>
    public async Task<(RunSnapshot Snapshot, RunHistory History)?> BuildAsync(PlayerProfile profile,
        CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return null;
        }

        var team = await pokemon.GetAllAsync(run.Id, ct);
        var history = await events.GetAllAsync(run.Id, ct);
        var unlocked = (await achievements.GetProgressAsync(run.Id, ct)).Count(a => a.Unlocked);

        var snapshot = new RunSnapshot
        {
            RunId = run.Id,
            PlayerName = profile.Name,
            PlayerId = profile.Id,
            RunName = run.Name,
            RoleName = roles.Of(run.Id)?.Name ?? run.RoleId,
            SeedLabel = run.SeedLabel,
            Points = history.Sum(e => e.PointsDelta),
            Registered = team.Count,
            Alive = team.Count(p => p.Status == PokemonStatus.Alive),
            Dead = team.Count(p => p.Status == PokemonStatus.Dead),
            Traded = team.Count(p => p.Status == PokemonStatus.Traded),
            StagesCleared = await progress.ClearedAsync(run, ct),
            LevelCap = await progress.CurrentCapAsync(run, ct),
            AchievementsUnlocked = unlocked,
            AchievementsTotal = achievements.All.Count,
            EventCount = history.Count,
            ChainHead = history.Count > 0 ? history[^1].Hash : string.Empty,
            BattleReady = BattleReadyFrom(history),
            WorldSpecies = WorldSpeciesFrom(history),
            AvatarSpecies = await AvatarAsync(ct),
            RunCreatedAt = run.CreatedAt,
            PublishedAt = DateTimeOffset.Now
        };

        return (snapshot, new RunHistory
        {
            RunId = run.Id,
            PlayerId = profile.Id,
            ExportedAt = snapshot.PublishedAt,
            Events = history
        });
    }

    /// <summary>
    /// Whether this world can link-battle, worked out from the last randomization that actually
    /// ran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// From the <b>event</b> and never from <c>randomizer.json</c>. The file says how the next
    /// generation would be configured; the event says how the world being played was made. Somebody
    /// who edits the file and does not regenerate has two different answers, and the one that
    /// decides whether they can battle is the second. Same principle as the <c>gratis</c> mark and
    /// the roulette credit (§63, §64): what is charged comes from what the event said happened.
    /// </para>
    /// <para>
    /// Null when the run was randomized before this was recorded. That is "not known", not "no".
    /// </para>
    /// </remarks>
    private static bool? BattleReadyFrom(IReadOnlyList<GameEvent> history)
    {
        var last = history.LastOrDefault(e => e.Type == GameEventType.RomRandomized);

        if (last is null
            || !last.Data.TryGetValue("shuffleBaseStats", out var stats)
            || !last.Data.TryGetValue("randomizeAbilities", out var abilities)
            || !bool.TryParse(stats, out var shuffled)
            || !bool.TryParse(abilities, out var randomAbilities))
        {
            return null;
        }

        // Compatible es que NINGUNA de las dos toque los datos de combate. Que dos mundos con las
        // mismas opciones a true y la misma seed tambien valdrian es cierto y no se modela: nadie
        // juega asi, y una regla que casi nunca se cumple es una regla que confunde.
        return !shuffled && !randomAbilities;
    }

    /// <summary>The species leading the saved party, for the friends list. Decoration: null if the save will not read.</summary>
    private async Task<int?> AvatarAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await boxes.ReadAsync(ct);
            return snapshot.Boxes.FirstOrDefault(b => b.IsParty)?.Pokemon
                .FirstOrDefault(p => !p.IsEgg && p.Species > 0)?.Species;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Sin avatar: no se ha podido leer el equipo");
            return null;
        }
    }

    /// <summary>How many species the last generated world had, from its event. Null when not recorded.</summary>
    private static int? WorldSpeciesFrom(IReadOnlyList<GameEvent> history) =>
        history.LastOrDefault(e => e.Type == GameEventType.RomRandomized) is { } last
        && last.Data.TryGetValue("maxSpecies", out var raw)
        && int.TryParse(raw, out var species)
        && species > 0
            ? species
            : null;

    /// <summary>
    /// Writes this player's run and its history into their own folder of the shared one.
    /// </summary>
    public async Task<string> PublishAsync(CancellationToken ct = default)
    {
        var folder = SharedFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return "Elige primero la carpeta compartida.";
        }

        var player = await PlayerAsync(ct);

        // Una run que ya es de otro jugador no se publica como tuya. Pasa si alguien copia la
        // carpeta Saves de un amigo: la run viaja con su dueño escrito dentro.
        if (player.Ownership == RunOwnership.Foreign)
        {
            return "Esta run es de otro jugador.";
        }

        if (player.Profile is not { } profile || await BuildAsync(profile, ct) is not { } built)
        {
            return "No hay ninguna run que publicar.";
        }

        var written = store.PublishPlayer(folder, profile, built.Snapshot, built.History);

        if (store.RemoveLegacy(folder, built.Snapshot.RunId))
        {
            logger.LogInformation("Quitada la instantánea suelta de la run {Run}: ya vive en {Folder}",
                built.Snapshot.RunId, written);
        }

        // Se relee, y se pasa por la misma comprobacion que ven los demas: publicar algo que al
        // leerlo no cuadra seria un fallo de PermaLocke, no del jugador, y hay que decirlo aqui.
        var back = store.ReadAll(folder).FirstOrDefault(r => r.Snapshot?.RunId == built.Snapshot.RunId);

        if (back?.Snapshot is null)
        {
            return "No se ha podido publicar. Comprueba la carpeta compartida.";
        }

        var audit = SnapshotAudit.Check(back.Snapshot, back.History);

        return audit.Verdict == AuditVerdict.Consistent
            ? "Publicado."
            : $"Publicado, pero algo no cuadra: {audit.Detail}";
    }

    /// <summary>Reads everyone's runs, checks each against its history and puts them in order.</summary>
    public async Task<Standings> ReadAsync(CancellationToken ct = default)
    {
        var folder = SharedFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new Standings([], [],
                "Elige la carpeta compartida.");
        }

        if (!Directory.Exists(folder))
        {
            return new Standings([], [],
                "No se encuentra la carpeta compartida.");
        }

        var read = store.ReadAll(folder);
        var mine = runContext.Current?.Id;
        var me = await profiles.CurrentAsync(ct);

        var marks = seenMarks.Load();

        var audited = read
            .Where(r => r.Snapshot is not null)
            .Select(r =>
            {
                marks.TryGetValue(r.Snapshot!.RunId, out var seen);
                var audit = SnapshotAudit.Check(r.Snapshot, r.History, seen);

                if (SnapshotAudit.Advance(seen, r.Snapshot, audit) is { } advanced)
                {
                    marks[r.Snapshot.RunId] = advanced;
                }

                return (Read: r, Audit: audit);
            })
            // Lo que no cuadra va DETRAS de lo que se puede fiar, con sus propios numeros y su marca
            // roja. Ordenar por los puntos que dice de si mismo le daria el podio a quien edito el
            // fichero: medido en la primera prueba, 900 puntos inventados salian primeros.
            .OrderBy(x => x.Audit.Verdict is AuditVerdict.Consistent or AuditVerdict.NoHistory ? 0 : 1)
            .ThenByDescending(x => x.Read.Snapshot!.Points)
            .ThenBy(x => x.Read.Snapshot!.Dead)
            .ToList();

        var good = audited.Select(x => x.Read).ToList();
        var rows = new List<StandingRow>();

        for (var i = 0; i < audited.Count; i++)
        {
            var (entry, audit) = audited[i];
            var snapshot = entry.Snapshot!;

            // Empatados a puntos y a bajas comparten puesto: el orden entre ellos lo decide el
            // fichero, y dar el 3 y el 4 a dos runs idénticas seria inventar una diferencia.
            var position = i > 0 && snapshot.Points == rows[i - 1].Snapshot.Points
                                 && snapshot.Dead == rows[i - 1].Snapshot.Dead
                                 && audit.Verdict == rows[i - 1].Audit.Verdict
                ? rows[i - 1].Position
                : i + 1;

            var isMine = snapshot.RunId == mine || (me is not null && snapshot.PlayerId == me.Id);

            rows.Add(new StandingRow(snapshot, isMine, position, Age(snapshot.PublishedAt),
                audit, entry.IsLegacy));
        }

        seenMarks.Save(marks);

        var broken = read
            .Where(r => r.Snapshot is null)
            .Select(r => $"{r.File}: {r.Problem}")
            .Concat(read.Where(r => r.HistoryProblem.Length > 0).Select(r => $"{r.File}: {r.HistoryProblem}"))
            .ToList();

        var snapshots = good.Select(r => r.Snapshot!).ToList();
        var battle = LinkBattleAdvice.Summarize(snapshots);

        return new Standings(rows, broken,
            snapshots.Count == 0
                ? "Todavía no ha publicado nadie."
                : $"{snapshots.Count} jugador(es).",
            battle.Note, battle.State);
    }

    /// <summary>
    /// How stale a snapshot is, in words.
    /// </summary>
    /// <remarks>
    /// It matters more than it looks: a scoreboard where somebody published a week ago and nobody
    /// says so reads as somebody who has stopped playing, when they may simply have stopped
    /// publishing. The age is part of the number.
    /// </remarks>
    private static string Age(DateTimeOffset published)
    {
        var span = DateTimeOffset.Now - published;

        return span switch
        {
            { TotalMinutes: < 2 } => "ahora mismo",
            { TotalHours: < 1 } => $"hace {(int)span.TotalMinutes} min",
            { TotalDays: < 1 } => $"hace {(int)span.TotalHours} h",
            { TotalDays: < 30 } => $"hace {(int)span.TotalDays} día(s)",
            _ => published.LocalDateTime.ToString("dd/MM/yyyy")
        };
    }
}
