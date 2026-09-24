using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <param name="Deleted">Names of what is no longer there.</param>
/// <param name="BackupFolder">Where the copy went, so the player can be told without hunting.</param>
public sealed record SaveErasure(
    bool Erased, IReadOnlyList<string> Deleted, string BackupFolder, string Message);

/// <summary>
/// Deletes the player's Ultra Moon save, after copying it somewhere they can get it back from.
/// </summary>
/// <remarks>
/// <para>
/// The only thing in PermaLocke that destroys a partida on purpose. Every other write backs the
/// save up because it is about to change a few bytes; this one backs it up because it is about to
/// delete the whole thing, and <b>no backup means no deletion</b>: a partida that cannot be put
/// back is a partida that does not get erased.
/// </para>
/// <para>
/// What has to be deleted is the <b>archive</b>, not the file. Azahar stores the save as a folder
/// holding <c>main</c> plus a sibling <c>&lt;name&gt;.metadata</c> that records the archive as
/// formatted. Deleting only <c>main</c> leaves the game an archive that says a partida is there
/// and a folder where it is not, and the game calls that <b>corrupted save data</b> -- which is
/// worse than either end state, because the player cannot start a new game from it either. Both
/// go, and then the game formats a fresh archive on its own the next time it saves.
/// </para>
/// <para>
/// Deliberately narrow. It clears the title's own save folder and nothing else -- not Azahar's
/// settings, not other titles, not the emulator's directories -- and it refuses to touch anything
/// beyond the single <c>main</c> file if the folder it is about to empty is not the one belonging
/// to Ultra Moon. A "start over" that reaches further than the game it was pointed at is how
/// somebody loses a save nobody was talking about.
/// </para>
/// <para>
/// The game has to be closed. Azahar holds the file open and writes it back on save, so erasing
/// underneath a running emulator would either fail or be undone a minute later without anybody
/// noticing.
/// </para>
/// </remarks>
public sealed class SaveEraser(PlayerSave save, string backupRoot, IClock clock,
    ILogger<SaveEraser> logger)
{
    /// <summary>Ultra Moon (Europe). The same anchor <see cref="PlayerSave"/> searches by.</summary>
    private const string TitleFolder = "001b5100";

    public SaveErasure Erase()
    {
        if (save.IsGameLoaded())
        {
            return new SaveErasure(false, [], string.Empty,
                "El juego está abierto. Guarda y cierra Azahar.");
        }

        var main = save.Find();
        var folder = main is null ? null : Path.GetDirectoryName(main);

        // La carpeta del archivo se busca aparte, porque puede existir SIN main dentro: es el
        // estado que el juego llama «datos dañados», y también hay que poder salir de él.
        var identified = folder is not null
                         && folder.Contains(TitleFolder, StringComparison.OrdinalIgnoreCase);

        var archive = identified ? folder : save.FindArchive() is { } data
            ? Directory.EnumerateDirectories(data).FirstOrDefault()
            : null;

        var metadata = archive is null ? null : archive + ".metadata";

        var files = archive is not null && Directory.Exists(archive)
            ? Directory.GetFiles(archive)
            : main is not null ? [main] : [];

        if (files.Length == 0 && (metadata is null || !File.Exists(metadata)))
        {
            return new SaveErasure(false, [], string.Empty,
                "No se encuentra ninguna partida de Ultra Luna, así que no hay nada que borrar.");
        }

        var destination = Path.Combine(backupRoot, $"borrada-{clock.Now:yyyyMMdd-HHmmss}");

        try
        {
            Directory.CreateDirectory(destination);

            foreach (var file in files)
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
            }

            // La metadata se copia también: sin ella la copia no se podría devolver a su sitio,
            // porque el juego no reconocería un archivo que nadie declara formateado.
            if (metadata is not null && File.Exists(metadata))
            {
                File.Copy(metadata, Path.Combine(destination, Path.GetFileName(metadata)),
                    overwrite: false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo copiar la partida antes de borrarla");

            return new SaveErasure(false, [], destination,
                "No se ha podido hacer la copia de seguridad, así que no se ha borrado nada.");
        }

        var deleted = new List<string>();

        foreach (var file in files)
        {
            Remove(() => File.Delete(file), Path.GetFileName(file), deleted);
        }

        // Solo cuando la carpeta se ha identificado como la del juego se toca el archivo entero.
        if (identified || (archive is not null && metadata is not null && File.Exists(metadata)))
        {
            if (metadata is not null && File.Exists(metadata))
            {
                Remove(() => File.Delete(metadata), Path.GetFileName(metadata), deleted);
            }

            if (archive is not null && Directory.Exists(archive))
            {
                Remove(() => Directory.Delete(archive), Path.GetFileName(archive), deleted);
            }
        }

        // Releído, como toda escritura de PermaLocke: que Delete no lanzara no es que se haya ido.
        var left = files.Any(File.Exists)
                   || (metadata is not null && File.Exists(metadata))
                   || (archive is not null && Directory.Exists(archive));

        if (left)
        {
            return new SaveErasure(false, deleted, destination,
                "No se ha podido borrar toda la partida. Cierra Azahar y vuelve a intentarlo.");
        }

        logger.LogWarning("Partida borrada: {What}. Copia en {Backup}",
            string.Join(", ", deleted), destination);

        return new SaveErasure(true, deleted, destination,
            "Partida borrada.");
    }

    /// <summary>Deletes one thing, noting it only if it went. A failure is logged, never guessed at.</summary>
    private void Remove(Action delete, string name, List<string> deleted)
    {
        try
        {
            delete();
            deleted.Add(name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo borrar {What}", name);
        }
    }
}
