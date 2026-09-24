using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <param name="Snapshot">What was read, or null when the file could not be understood.</param>
/// <param name="File">Name of the file it came from, for saying which one is wrong.</param>
/// <param name="Problem">Why it was rejected, or empty.</param>
/// <param name="History">The history published beside it, when there is one and it could be read.</param>
/// <param name="IsLegacy">
/// A loose <c>permalocke-*.json</c> in the root of the folder, from before the per-player folders.
/// </param>
/// <param name="HistoryProblem">Why a history that is there could not be read, or empty.</param>
public sealed record SnapshotRead(RunSnapshot? Snapshot, string File, string Problem = "",
    RunHistory? History = null, bool IsLegacy = false, string HistoryProblem = "");

/// <param name="Folder">The player's folder in the shared one.</param>
/// <param name="HistoryPath">Where their history would be. It may not exist.</param>
public sealed record PlayerFolderRead(string Folder, PlayerProfile Profile, PlayerPresence? Presence,
    RunSnapshot? Snapshot, string HistoryPath);

/// <summary>
/// Where each thing lives inside the competition's shared folder.
/// </summary>
/// <remarks>
/// <code>
/// Competición/
///   reglas/                   the official rules; only the admin writes here
///   jugadores/
///     Grenin-1a2b3c4d/
///       perfil.json           who
///       run-activa.json       the snapshot of the run being played
///       historial.json        that run's whole event chain, to check the snapshot against
/// </code>
/// <para>
/// One folder per player and <b>each application writes only inside its own</b>. That is what keeps
/// Drive or Dropbox from ever having two people editing the same file, which is how a synchronised
/// folder produces «conflicted copy» files nobody knows what to do with (§123).
/// </para>
/// </remarks>
public static class CompetitionLayout
{
    public const string PlayersFolder = "jugadores";
    public const string AdminFolder = "admin";
    public const string GiftsFolder = "regalos";
    public const string RulesFolder = "reglas";
    public const string ProfileFile = "perfil.json";
    public const string SnapshotFile = "run-activa.json";
    public const string HistoryFile = "historial.json";
    public const string PresenceFile = "presencia.json";

    /// <summary>The loose snapshots written before this layout existed.</summary>
    public const string LegacyPattern = "permalocke-*.json";

    public static string Players(string root) => Path.Combine(root, PlayersFolder);

    public static string Rules(string root) => Path.Combine(root, RulesFolder);

    /// <summary>The folder name a player should have: readable name, then the short id.</summary>
    public static string FolderName(PlayerProfile profile) => $"{SafeName(profile.Name)}-{profile.ShortId}";

    /// <summary>
    /// The folder this player already has, found by the id at the end of its name, or the one they
    /// should have. By id and not by name, so a rename finds its old folder instead of starting
    /// another one.
    /// </summary>
    public static string? Existing(string root, PlayerProfile profile)
    {
        var players = Players(root);

        return Directory.Exists(players)
            ? Directory.EnumerateDirectories(players)
                .FirstOrDefault(d => Path.GetFileName(d).EndsWith("-" + profile.ShortId, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    /// <summary>A name that is safe as a folder on every system the group might sync to.</summary>
    public static string SafeName(string name)
    {
        var kept = new string([.. name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')])
            .Trim('_', '-');

        return kept.Length == 0 ? "jugador" : kept[..Math.Min(kept.Length, PlayerProfile.MaxNameLength)];
    }
}

/// <summary>
/// Reads and writes what each player publishes in a folder everybody shares.
/// </summary>
/// <remarks>
/// <para>
/// The folder is the whole transport: Drive, Dropbox, OneDrive, a network share, or a pen drive
/// passed around. There is no server, no account and no protocol — for five friends, a folder that
/// already syncs itself is the answer, and it is the only one that works with nobody hosting
/// anything.
/// </para>
/// <para>
/// <b>Everything read here is somebody else's file.</b> It is parsed defensively and never trusted:
/// a file that cannot be understood is reported by name and skipped, and one bad file never stops
/// the others from loading. The application must survive a folder full of junk, because sooner or
/// later somebody will drop something in it.
/// </para>
/// </remarks>
public sealed class SnapshotStore(ILogger<SnapshotStore>? logger = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Writes a loose snapshot into the root of the folder, the way it was done before §123.
    /// </summary>
    /// <remarks>Kept for reading and testing the old layout; the application publishes with <see cref="PublishPlayer"/>.</remarks>
    public void Publish(string folder, RunSnapshot snapshot)
    {
        Directory.CreateDirectory(folder);
        WriteJson(Path.Combine(folder, snapshot.FileName), snapshot);
        logger?.LogInformation("Instantánea publicada en {Folder}", folder);
    }

    /// <summary>
    /// Writes this player's profile, run and history into their own folder, and returns that folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A renamed player's folder is renamed too, found by its id. If the new name is already taken
    /// by a folder that is not theirs, the old folder is kept rather than merged into somebody
    /// else's.
    /// </para>
    /// <para>
    /// The history is written before the snapshot. A friend's app reading halfway through then sees
    /// an older snapshot with a newer history, which reads as «does not match» for one refresh,
    /// instead of a snapshot pointing at a history that is not there yet.
    /// </para>
    /// </remarks>
    public string PublishPlayer(string root, PlayerProfile profile, RunSnapshot snapshot, RunHistory history)
    {
        var folder = PlayerFolder(root, profile);

        WriteJson(Path.Combine(folder, CompetitionLayout.ProfileFile), profile);
        WriteJson(Path.Combine(folder, CompetitionLayout.HistoryFile), history);
        WriteJson(Path.Combine(folder, CompetitionLayout.SnapshotFile), snapshot);

        logger?.LogInformation("Publicado en {Folder}: {Events} eventos", folder, history.Events.Count);
        return folder;
    }

    /// <summary>This player's folder, created or renamed to match the profile, found by its id.</summary>
    private static string PlayerFolder(string root, PlayerProfile profile)
    {
        var wanted = Path.Combine(CompetitionLayout.Players(root), CompetitionLayout.FolderName(profile));
        var folder = CompetitionLayout.Existing(root, profile) ?? wanted;

        if (!string.Equals(folder, wanted, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(wanted))
        {
            Directory.Move(folder, wanted);
            folder = wanted;
        }

        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Writes this player's presence into their folder, and the profile beside it the first time (§126).
    /// </summary>
    /// <remarks>
    /// The profile is only rewritten when it changed: this runs every heartbeat, and every file written is a file
    /// Drive uploads again.
    /// </remarks>
    public void WritePresence(string root, PlayerProfile profile, PlayerPresence presence)
    {
        var folder = PlayerFolder(root, profile);
        var profilePath = Path.Combine(folder, CompetitionLayout.ProfileFile);
        var profileJson = JsonSerializer.Serialize(profile, Options);

        if (!File.Exists(profilePath) || File.ReadAllText(profilePath) != profileJson)
        {
            WriteJson(profilePath, profile);
        }

        WriteJson(Path.Combine(folder, CompetitionLayout.PresenceFile), presence);
    }

    /// <summary>
    /// Every player folder: who, their presence, their run, and where their history is.
    /// </summary>
    /// <remarks>
    /// The history is not parsed here. It is the one big file, and the friends list reads the folder every few
    /// seconds; the caller reads a history only when its file has changed. Anything unreadable comes back as null
    /// for that part, and a folder with no profile at all is skipped.
    /// </remarks>
    public IReadOnlyList<PlayerFolderRead> ReadPlayers(string root)
    {
        var players = CompetitionLayout.Players(root);

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(players))
        {
            return [];
        }

        var reads = new List<PlayerFolderRead>();

        foreach (var folder in Directory.EnumerateDirectories(players).OrderBy(p => p, StringComparer.Ordinal))
        {
            var profile = TryRead<PlayerProfile>(Path.Combine(folder, CompetitionLayout.ProfileFile));

            if (profile is null || profile.Id == Guid.Empty)
            {
                continue;
            }

            var snapshot = Path.Combine(folder, CompetitionLayout.SnapshotFile);

            reads.Add(new PlayerFolderRead(folder, profile,
                TryRead<PlayerPresence>(Path.Combine(folder, CompetitionLayout.PresenceFile)),
                File.Exists(snapshot) ? Read(snapshot).Snapshot : null,
                Path.Combine(folder, CompetitionLayout.HistoryFile)));
        }

        return reads;
    }

    /// <summary>A published history, or null when it is missing or cannot be read.</summary>
    public RunHistory? ReadHistory(string path)
    {
        var history = TryRead<RunHistory>(path);
        return history is null || history.Schema > RunHistory.CurrentSchema ? null : history;
    }

    private T? TryRead<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "No se ha podido leer {File}", path);
            return null;
        }
    }

    /// <summary>
    /// Removes this run's loose snapshot from the root, once it lives in the player's folder.
    /// </summary>
    /// <remarks>
    /// Only the file named after this run's own id: nothing else in the root is this application's
    /// to delete. Without this the same run would show up twice, once from each layout.
    /// </remarks>
    public bool RemoveLegacy(string root, Guid runId)
    {
        var path = Path.Combine(root, $"permalocke-{runId:N}.json");

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Reads every player's run, and the loose snapshots of the old layout, saying which files it
    /// could not understand.
    /// </summary>
    /// <remarks>
    /// A run found in both layouts is read once, from the player's folder, which is the newer one.
    /// </remarks>
    public IReadOnlyList<SnapshotRead> ReadAll(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var results = new List<SnapshotRead>();
        var players = CompetitionLayout.Players(folder);

        if (Directory.Exists(players))
        {
            foreach (var player in Directory.EnumerateDirectories(players).OrderBy(p => p))
            {
                var snapshot = Path.Combine(player, CompetitionLayout.SnapshotFile);

                // Una carpeta con perfil y sin run todavia no es un error: es alguien que aun no ha
                // publicado. No sale en la clasificacion y no se queja nadie.
                if (File.Exists(snapshot))
                {
                    results.Add(ReadPlayer(player, snapshot));
                }
            }
        }

        var seen = results.Where(r => r.Snapshot is not null).Select(r => r.Snapshot!.RunId).ToHashSet();

        foreach (var path in Directory.EnumerateFiles(folder, CompetitionLayout.LegacyPattern).OrderBy(p => p))
        {
            var read = Read(path) with { IsLegacy = true };

            if (read.Snapshot is null || seen.Add(read.Snapshot.RunId))
            {
                results.Add(read);
            }
        }

        return results;
    }

    private SnapshotRead ReadPlayer(string player, string snapshotPath)
    {
        var name = $"{CompetitionLayout.PlayersFolder}/{Path.GetFileName(player)}/{CompetitionLayout.SnapshotFile}";
        var read = Read(snapshotPath) with { File = name };

        if (read.Snapshot is null)
        {
            return read;
        }

        var historyPath = Path.Combine(player, CompetitionLayout.HistoryFile);

        if (!File.Exists(historyPath))
        {
            return read;
        }

        try
        {
            var history = JsonSerializer.Deserialize<RunHistory>(File.ReadAllText(historyPath), Options);

            return history is null
                ? read with { HistoryProblem = "El historial está vacío." }
                : history.Schema > RunHistory.CurrentSchema
                    ? read with { HistoryProblem = "El historial lo escribió una versión más nueva de PermaLocke." }
                    : read with { History = history };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "No se ha podido leer el historial {File}", historyPath);
            return read with { HistoryProblem = "El historial no se ha podido leer: no parece un JSON válido." };
        }
    }

    private SnapshotRead Read(string path)
    {
        var name = Path.GetFileName(path);

        try
        {
            var snapshot = JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(path), Options);

            if (snapshot is null)
            {
                return new SnapshotRead(null, name, "El fichero está vacío.");
            }

            if (snapshot.Schema > RunSnapshot.CurrentSchema)
            {
                return new SnapshotRead(null, name,
                    $"Lo escribió una versión más nueva de PermaLocke (formato {snapshot.Schema}). "
                    + "Actualiza la tuya.");
            }

            if (snapshot.RunId == Guid.Empty || string.IsNullOrWhiteSpace(snapshot.PlayerName))
            {
                return new SnapshotRead(null, name, "Le falta el jugador o la run.");
            }

            return new SnapshotRead(snapshot, name);
        }
        catch (Exception ex)
        {
            // Un fichero ajeno que no se entiende no puede tumbar la pantalla ni esconder a los
            // demas: se dice cual es y se sigue con el resto.
            logger?.LogWarning(ex, "No se ha podido leer la instantánea {File}", name);
            return new SnapshotRead(null, name, "No se ha podido leer: no parece un JSON válido.");
        }
    }

    /// <summary>
    /// A un fichero temporal y luego mover: la carpeta la esta sincronizando otro programa, y escribir
    /// en el sitio significa que Drive puede subir una version a medio escribir.
    /// </summary>
    private static void WriteJson<T>(string target, T value)
    {
        var temporary = target + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, target, overwrite: true);
    }
}
