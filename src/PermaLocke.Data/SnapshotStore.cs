using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <param name="Snapshot">What was read, or null when the file could not be understood.</param>
/// <param name="File">Name of the file it came from, for saying which one is wrong.</param>
/// <param name="Problem">Why it was rejected, or empty.</param>
public sealed record SnapshotRead(RunSnapshot? Snapshot, string File, string Problem = "");

/// <summary>
/// Reads and writes run snapshots in a folder everybody shares.
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

    /// <summary>Only files that look like ours are even opened.</summary>
    private const string Pattern = "permalocke-*.json";

    /// <summary>Writes the snapshot into the shared folder, replacing this run's previous one.</summary>
    public void Publish(string folder, RunSnapshot snapshot)
    {
        Directory.CreateDirectory(folder);

        var target = Path.Combine(folder, snapshot.FileName);

        // A un fichero temporal y luego mover: la carpeta la esta sincronizando otro programa, y
        // escribir en el sitio significa que Drive puede subir una version a medio escribir.
        var temporary = target + ".tmp";

        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, Options));
        File.Move(temporary, target, overwrite: true);

        logger?.LogInformation("Instantánea publicada en {File}", target);
    }

    /// <summary>
    /// Reads every snapshot in the folder, saying which files it could not understand.
    /// </summary>
    public IReadOnlyList<SnapshotRead> ReadAll(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var results = new List<SnapshotRead>();

        foreach (var path in Directory.EnumerateFiles(folder, Pattern).OrderBy(p => p))
        {
            results.Add(Read(path));
        }

        return results;
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
}
