using Microsoft.Extensions.Logging;

namespace PermaLocke.Data;

/// <param name="File">The copy that was written, or null when there was nothing to copy.</param>
/// <param name="Removed">Old copies deleted to keep the folder within <see cref="RunBackup.Keep"/>.</param>
public sealed record RunBackupResult(string? File, int Removed, string Message);

/// <summary>
/// Copies the run database before anything opens it, and keeps the last few copies.
/// </summary>
/// <remarks>
/// <para>
/// This existed nowhere until now, and the asymmetry is what makes it worth writing down:
/// PermaLocke copies <b>somebody else's</b> file — the Ultra Moon save — before every single write,
/// and had made <b>319</b> such copies. Of its own database, which holds the hash-chained event
/// log, the points and every registered Pokémon, it had made <b>none</b>. Months of a competition
/// lived in one SQLite file with no copy anywhere.
/// </para>
/// <para>
/// It runs <b>at startup, before the store opens the file</b>. That is the only moment the database
/// is guaranteed to be at rest: copying a SQLite file that something else is writing can capture a
/// torn page, and a backup that might be corrupt is worse than none because it is trusted.
/// </para>
/// <para>
/// It never throws. A run that cannot be backed up is a run that still has to start: the player
/// came to play, not to be told about a full disk. Failures go to the log and to the caller's
/// message, and everything downstream carries on.
/// </para>
/// </remarks>
public sealed class RunBackup(string savesRoot, ILogger<RunBackup>? logger = null)
{
    /// <summary>How many copies are kept. Older ones are deleted, oldest first.</summary>
    /// <remarks>
    /// Ten is chosen for what it covers rather than for tidiness: the database is small — a few
    /// hundred kilobytes after months of play — so ten costs nothing, and it reaches back far
    /// enough to survive a bad session that nobody noticed until the next day.
    /// </remarks>
    public const int Keep = 10;

    private const string Prefix = "permalocke-auto-";

    public string DatabasePath { get; } = Path.Combine(savesRoot, "permalocke.db");

    public string Folder { get; } = Path.Combine(savesRoot, "backup", "run");

    /// <summary>
    /// Copies the database if there is one, then trims the folder to <see cref="Keep"/>.
    /// </summary>
    public RunBackupResult Run(DateTimeOffset now)
    {
        try
        {
            if (!File.Exists(DatabasePath))
            {
                // Primer arranque, o una run recien borrada. No es un fallo.
                return new RunBackupResult(null, 0, "No hay base de datos que copiar todavía.");
            }

            Directory.CreateDirectory(Folder);

            var target = Path.Combine(Folder, $"{Prefix}{now:yyyyMMdd-HHmmss}.db");

            // Dos arranques dentro del mismo segundo darian el mismo nombre. Sobrescribir la copia
            // que se acaba de hacer no pierde nada, pero perderia una de las diez.
            if (File.Exists(target))
            {
                return new RunBackupResult(null, 0, "Ya hay una copia de este segundo.");
            }

            File.Copy(DatabasePath, target);

            var removed = Trim();

            logger?.LogInformation("Copia de la run en {File} ({Removed} antiguas borradas)",
                target, removed);

            return new RunBackupResult(target, removed,
                $"Copia de la run guardada. Se conservan las {Keep} últimas.");
        }
        catch (Exception ex)
        {
            // Nunca impide arrancar. Ver el remark de la clase.
            logger?.LogError(ex, "No se ha podido copiar la run");
            return new RunBackupResult(null, 0,
                "No se ha podido copiar la run. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>Deletes the oldest copies until only <see cref="Keep"/> remain.</summary>
    /// <remarks>
    /// Ordered by <b>name</b> and not by write time. The name carries a sortable timestamp, and it
    /// is the only ordering that survives the folder being copied, restored or synced — all of
    /// which rewrite the file times and would otherwise make the trimmer delete the wrong ones.
    /// Only files this class wrote are ever considered: a copy someone made by hand is theirs.
    /// </remarks>
    private int Trim()
    {
        var mine = Directory.EnumerateFiles(Folder, $"{Prefix}*.db")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var removed = 0;

        foreach (var old in mine.Take(Math.Max(0, mine.Count - Keep)))
        {
            try
            {
                File.Delete(old);
                removed++;
            }
            catch (Exception ex)
            {
                // Una copia que no se deja borrar no vale una excepcion: la siguiente vez habra
                // una mas de la cuenta y no ha pasado nada.
                logger?.LogWarning(ex, "No se ha podido borrar la copia {File}", old);
            }
        }

        return removed;
    }
}
