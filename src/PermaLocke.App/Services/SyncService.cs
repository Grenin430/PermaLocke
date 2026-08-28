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
public sealed record Standings(
    IReadOnlyList<StandingRow> Rows, IReadOnlyList<string> Broken, string Message);

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
            RunCreatedAt = run.CreatedAt,
            PublishedAt = DateTimeOffset.Now
        };
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

        return new Standings(rows, broken,
            good.Count == 0
                ? "La carpeta está vacía. Publica la tuya y dile a los demás que hagan lo mismo."
                : $"{good.Count} run(s) en la carpeta.");
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
