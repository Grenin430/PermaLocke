using System.IO.Compression;
using Microsoft.Data.Sqlite;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>The copy that goes to the server (§198): the run and the save, consistent, small, and nothing else.</summary>
public sealed class ServerBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-copia-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Carries_the_run_and_the_save_and_leaves_the_rest()
    {
        var saves = Path.Combine(_root, "Saves");
        var run = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(saves, run));
        File.WriteAllText(Path.Combine(saves, run, "run.json"), "{\"name\":\"mi run\"}");
        Directory.CreateDirectory(Path.Combine(saves, "backup", "run"));
        File.WriteAllText(Path.Combine(saves, "backup", "run", "vieja.db"), "no");
        Directory.CreateDirectory(Path.Combine(saves, "estados-retirados"));
        File.WriteAllText(Path.Combine(saves, "estados-retirados", "e.st"), "no");
        var game = Path.Combine(_root, "main");
        File.WriteAllText(game, "partida");

        // La base de datos abierta y escribiéndose mientras se copia.
        var database = Path.Combine(saves, "permalocke.db");
        using var open = new SqliteConnection($"Data Source={database}");
        open.Open();
        using (var command = open.CreateCommand())
        {
            command.CommandText = "create table events (x int); insert into events values (1), (2), (3);";
            command.ExecuteNonQuery();
        }

        var bytes = ServerBackup.Build(saves, game, DateTimeOffset.Now);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        var names = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["LEEME.txt", "Partida/main", $"Saves/{run}/run.json", "Saves/permalocke.db"], names);

        var restored = Path.Combine(_root, "restaurada.db");
        zip.GetEntry("Saves/permalocke.db")!.ExtractToFile(restored);
        using var check = new SqliteConnection($"Data Source={restored};Pooling=False");
        check.Open();
        using var count = check.CreateCommand();
        count.CommandText = "select count(*) from events";
        Assert.Equal(3L, count.ExecuteScalar());
    }
}
