using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <param name="Deleted">Files that are no longer there.</param>
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
/// Deliberately narrow. It clears the title's own save folder and nothing else -- not Azahar's
/// settings, not other titles, not the emulator's directories -- and it refuses outright if the
/// folder it is about to empty is not the one belonging to Ultra Moon. A "start over" that reaches
/// further than the game it was pointed at is how somebody loses a save nobody was talking about.
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
                "El juego está cargado en Azahar. Guarda, cierra el emulador del todo y vuelve.");
        }

        if (save.Find() is not { } main)
        {
            return new SaveErasure(false, [], string.Empty,
                "No se encuentra ninguna partida de Ultra Luna, así que no hay nada que borrar.");
        }

        var folder = Path.GetDirectoryName(main);

        // Si la carpeta no es la del juego, se borra el fichero y NADA mas. Vaciar una carpeta que
        // no se ha identificado es exactamente el error que este servicio no puede permitirse.
        var targets = folder is not null
                      && folder.Contains(TitleFolder, StringComparison.OrdinalIgnoreCase)
            ? Directory.GetFiles(folder)
            : [main];

        var stamp = clock.Now.ToString("yyyyMMdd-HHmmss");
        var destination = Path.Combine(backupRoot, $"borrada-{stamp}");

        try
        {
            Directory.CreateDirectory(destination);

            foreach (var file in targets)
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo copiar la partida antes de borrarla");

            return new SaveErasure(false, [], destination,
                "No se ha podido hacer la copia de seguridad, así que no se ha borrado nada.");
        }

        var deleted = new List<string>();

        foreach (var file in targets)
        {
            try
            {
                File.Delete(file);
                deleted.Add(Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo borrar {File}", file);
            }
        }

        // Releído, como toda escritura de PermaLocke: que Delete no lanzara no es que el fichero
        // se haya ido.
        if (targets.Any(File.Exists))
        {
            return new SaveErasure(false, deleted, destination,
                "Parte de la partida sigue ahí. Cierra Azahar del todo y vuelve a intentarlo. "
                + $"La copia está en {destination}.");
        }

        logger.LogWarning("Partida borrada: {Count} ficheros. Copia en {Backup}",
            deleted.Count, destination);

        return new SaveErasure(true, deleted, destination,
            $"Partida borrada. La copia está en {destination}.");
    }
}
