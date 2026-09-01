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
public sealed record StandingRow(RunSnapshot Snapshot, bool IsMine, int Position, string Age);

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
/// sitting in a folder they can open. It is not verified and it is not verifiable without a server
/// nobody is going to run. Rule 3 of this project forbids a fake anti-cheat, so there is not one:
/// what the screen says is "this is what their application reported", and the fingerprint exists
/// so that a run being rewound or rewritten is at least <em>visible</em> to people who can then
/// deal with it themselves.
/// </para>
/// </remarks>
public sealed class SyncService(
    IRunContext runContext,
    IEventStore events,
    IPokemonRepository pokemon,
    IPointsService points,
    ProgressService progress,
    AchievementService achievements,
    IRunRoles roles,
    SnapshotStore store,
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

    public string SharedFolder
    {
        get
        {
            try
            {
                return File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<SyncSettings>(File.ReadAllText(SettingsPath), Json)
                        ?.SharedFolder ?? string.Empty
                    : string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se ha podido leer la configuración de sincronización");
                return string.Empty;
            }
        }
    }

    public void SetSharedFolder(string folder)
    {
        Directory.CreateDirectory(paths.Config);
        File.WriteAllText(SettingsPath,
            JsonSerializer.Serialize(new SyncSettings { SharedFolder = folder }, Json));

        logger.LogInformation("Carpeta compartida: {Folder}", folder);
    }

    /// <summary>
    /// Builds the summary of the loaded run. Reads only; publishing is a separate step.
    /// </summary>
    public async Task<RunSnapshot?> BuildAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return null;
        }

        var team = await pokemon.GetAllAsync(run.Id, ct);
        var history = await events.GetAllAsync(run.Id, ct);
        var unlocked = (await achievements.GetProgressAsync(run.Id, ct)).Count(a => a.Unlocked);

        return new RunSnapshot
        {
            RunId = run.Id,
            PlayerName = run.PlayerName,
            RunName = run.Name,
            RoleName = roles.Of(run.Id)?.Name ?? run.RoleId,
            SeedLabel = run.SeedLabel,
            Points = await points.GetBalanceAsync(run.Id, ct),
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
            RunCreatedAt = run.CreatedAt,
            PublishedAt = DateTimeOffset.Now
        };
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

    /// <summary>Writes this run's summary into the shared folder.</summary>
    public async Task<string> PublishAsync(CancellationToken ct = default)
    {
        var folder = SharedFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return "Elige primero la carpeta compartida.";
        }

        if (await BuildAsync(ct) is not { } snapshot)
        {
            return "No hay ninguna run que publicar.";
        }

        store.Publish(folder, snapshot);

        // Se relee: no se da por publicado lo que no se ha vuelto a ver en la carpeta.
        var back = store.ReadAll(folder).FirstOrDefault(r => r.Snapshot?.RunId == snapshot.RunId);

        return back?.Snapshot is null
            ? "Se ha escrito, pero al releer la carpeta no aparece. Comprueba que existe y que se puede escribir en ella."
            : $"Publicado. Los demás verán {snapshot.Points} puntos y {snapshot.Alive} en pie "
              + $"la próxima vez que abran su carpeta.";
    }

    /// <summary>Reads everyone's summaries and puts them in order.</summary>
    public Standings Read()
    {
        var folder = SharedFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new Standings([], [],
                "Elige una carpeta compartida: la que uséis en Drive, Dropbox o en red.");
        }

        if (!Directory.Exists(folder))
        {
            return new Standings([], [],
                "La carpeta configurada no existe. ¿La has movido, o no se ha sincronizado todavía?");
        }

        var read = store.ReadAll(folder);
        var mine = runContext.Current?.Id;

        var good = read
            .Where(r => r.Snapshot is not null)
            .Select(r => r.Snapshot!)
            .OrderByDescending(s => s.Points)
            .ThenBy(s => s.Dead)
            .ToList();

        var rows = new List<StandingRow>();

        for (var i = 0; i < good.Count; i++)
        {
            // Empatados a puntos y a bajas comparten puesto: el orden entre ellos lo decide el
            // fichero, y dar el 3 y el 4 a dos runs idénticas seria inventar una diferencia.
            var position = i > 0 && good[i].Points == good[i - 1].Points && good[i].Dead == good[i - 1].Dead
                ? rows[i - 1].Position
                : i + 1;

            rows.Add(new StandingRow(good[i], good[i].RunId == mine, position, Age(good[i].PublishedAt)));
        }

        var broken = read
            .Where(r => r.Snapshot is null)
            .Select(r => $"{r.File}: {r.Problem}")
            .ToList();

        var (note, state) = BattleSummary(good);

        return new Standings(rows, broken,
            good.Count == 0
                ? "La carpeta está vacía. Publica la tuya y dile a los demás que hagan lo mismo."
                : $"{good.Count} run(s) en la carpeta.",
            note, state);
    }

    /// <summary>
    /// Who can link-battle whom, in one line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists to catch a failure that would otherwise surface a month too late: somebody
    /// randomizes with the shuffled stats left on, plays for weeks, and only finds out they cannot
    /// battle anybody the day the group tries. The information was always there in each snapshot;
    /// it just needed saying out loud.
    /// </para>
    /// <para>
    /// The ones with no data are counted apart and never lumped in with the incompatible. A run
    /// randomized before this was recorded is unknown, and calling it a problem would be a verdict
    /// nobody measured.
    /// </para>
    /// </remarks>
    private static (string Note, string State) BattleSummary(IReadOnlyList<RunSnapshot> good)
    {
        if (good.Count == 0)
        {
            return (string.Empty, "none");
        }

        var ready = good.Where(s => s.BattleReady == true).ToList();
        var blocked = good.Where(s => s.BattleReady == false).ToList();
        var unknown = good.Where(s => s.BattleReady is null).ToList();

        var parts = new List<string>();

        if (blocked.Count > 0)
        {
            parts.Add($"{Names(blocked)} no {(blocked.Count == 1 ? "puede" : "pueden")}: su mundo "
                      + "baraja estadísticas base o randomiza habilidades, y eso hace que las dos "
                      + "consolas calculen el combate distinto.");
        }

        if (unknown.Count > 0)
        {
            parts.Add($"De {Names(unknown)} no hay dato: "
                      + (unknown.Count == 1 ? "randomizó" : "randomizaron")
                      + " antes de que esto se guardara. Que "
                      + (unknown.Count == 1 ? "vuelva" : "vuelvan")
                      + " a generar y a publicar.");
        }

        var head = ready.Count switch
        {
            0 => "Nadie puede combatir por link ahora mismo.",
            1 => "Solo una run está preparada para combatir por link, así que no hay contrincante.",
            _ => $"{ready.Count} runs pueden combatir por link entre sí."
        };

        return (string.Join(" ", parts.Prepend(head)),
            blocked.Count == 0 && unknown.Count == 0 && ready.Count > 1 ? "ok" : "warn");
    }

    private static string Names(IReadOnlyList<RunSnapshot> who) =>
        string.Join(", ", who.Select(s => s.PlayerName));

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
