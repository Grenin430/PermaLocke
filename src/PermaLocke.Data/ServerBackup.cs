using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

namespace PermaLocke.Data;

/// <summary>
/// The copy of a player's PermaLocke that goes to the tournament's Storage (§198, plan del próximo torneo, paso 5): the run
/// and the Ultra Moon save, small enough to keep five of each of twenty players in the free plan's gigabyte.
/// </summary>
/// <remarks>
/// <para>
/// Inside the zip: <c>Saves/permalocke.db</c> taken with SQLite's own online backup, which gives a consistent copy even
/// while the application has the database open (a file copy would not); the <c>.json</c> of each run folder
/// (<c>Saves/&lt;run&gt;/run.json</c>, the play time…); the game's save as <c>Partida/main</c>; and a <c>LEEME.txt</c> saying
/// where each thing goes back. Not the killcams, the local backups nor the retired save states: they are big and none of
/// them is the run.
/// </para>
/// <para>
/// Reads only. Nothing in the player's folder is written or changed.
/// </para>
/// </remarks>
public static class ServerBackup
{
    public static byte[] Build(string savesRoot, string? gameSave, DateTimeOffset now)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var database = Path.Combine(savesRoot, "permalocke.db");
            if (File.Exists(database))
            {
                var copy = Path.Combine(Path.GetTempPath(), $"permalocke-copia-{Guid.NewGuid():N}.db");
                try
                {
                    // La copia en caliente de SQLite: coherente aunque la aplicación esté escribiendo.
                    using (var source = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False"))
                    using (var target = new SqliteConnection($"Data Source={copy};Pooling=False"))
                    {
                        source.Open();
                        target.Open();
                        source.BackupDatabase(target);
                    }

                    zip.CreateEntryFromFile(copy, "Saves/permalocke.db", CompressionLevel.Optimal);
                }
                finally
                {
                    if (File.Exists(copy)) File.Delete(copy);
                }
            }

            if (Directory.Exists(savesRoot))
            {
                foreach (var folder in Directory.EnumerateDirectories(savesRoot).Where(IsRunFolder))
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        zip.CreateEntryFromFile(file, $"Saves/{Path.GetFileName(folder)}/{Path.GetFileName(file)}", CompressionLevel.Optimal);
                    }
                }
            }

            if (gameSave is not null && File.Exists(gameSave))
            {
                using var stream = new FileStream(gameSave, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var entry = zip.CreateEntry("Partida/main", CompressionLevel.Optimal).Open();
                stream.CopyTo(entry);
            }

            using var readme = new StreamWriter(zip.CreateEntry("LEEME.txt").Open(), Encoding.UTF8);
            readme.Write($"""
                Copia de PermaLocke del {now:dd/MM/yyyy HH:mm}.

                Para recuperarla, con PermaLocke y el emulador cerrados:
                - Saves\permalocke.db y las carpetas de Saves: a la carpeta Saves de PermaLocke.
                - Partida\main: es la partida de Ultra Luna. Va a
                  Emulator\user\sdmc\Nintendo 3DS\<id>\<id>\title\00040000\001b5100\data\00000001\main
                  (las dos carpetas <id> son las que ya tenga ese emulador).
                Antes de sustituir nada, guarda aparte lo que haya.
                """);
        }

        return buffer.ToArray();
    }

    /// <summary>A run's own folder in Saves: named by its id.</summary>
    private static bool IsRunFolder(string folder) => Guid.TryParseExact(Path.GetFileName(folder), "N", out _);
}
