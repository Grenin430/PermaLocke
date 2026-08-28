using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The run database is the competition: the hash-chained log, the points and every registered
/// Pokémon. Until now it was the one file PermaLocke never copied.
/// </summary>
public sealed class RunBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "permalocke-backup-" + Guid.NewGuid().ToString("N"));

    private RunBackup NewBackup()
    {
        Directory.CreateDirectory(_root);
        return new RunBackup(_root);
    }

    private void WriteDatabase(string content = "run")
        => File.WriteAllText(Path.Combine(_root, "permalocke.db"), content);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Copies_the_database_and_the_copy_is_the_same_bytes()
    {
        var backup = NewBackup();
        WriteDatabase("los eventos de la run");

        var result = backup.Run(DateTimeOffset.Parse("2026-08-28T10:00:00Z"));

        Assert.NotNull(result.File);
        Assert.True(File.Exists(result.File));
        Assert.Equal("los eventos de la run", File.ReadAllText(result.File));
    }

    /// <summary>
    /// The original must still be there afterwards. Obvious, and exactly the kind of thing that is
    /// only obvious until someone writes Move instead of Copy.
    /// </summary>
    [Fact]
    public void Leaves_the_original_where_it_was()
    {
        var backup = NewBackup();
        WriteDatabase();

        backup.Run(DateTimeOffset.Parse("2026-08-28T10:00:00Z"));

        Assert.True(File.Exists(backup.DatabasePath));
    }

    /// <summary>First run of a fresh install: nothing to copy, and that is not a failure.</summary>
    [Fact]
    public void Says_so_instead_of_failing_when_there_is_no_database_yet()
    {
        var backup = NewBackup();

        var result = backup.Run(DateTimeOffset.Parse("2026-08-28T10:00:00Z"));

        Assert.Null(result.File);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void Keeps_the_last_ten_and_deletes_the_oldest_first()
    {
        var backup = NewBackup();
        var start = DateTimeOffset.Parse("2026-08-28T10:00:00Z");

        // Catorce arranques, uno por minuto.
        for (var i = 0; i < 14; i++)
        {
            WriteDatabase($"arranque {i}");
            backup.Run(start.AddMinutes(i));
        }

        var kept = Directory.GetFiles(backup.Folder)
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(RunBackup.Keep, kept.Count);

        // Se van los cuatro primeros, no cuatro cualesquiera.
        Assert.DoesNotContain("permalocke-auto-20260828-100000.db", kept);
        Assert.DoesNotContain("permalocke-auto-20260828-100300.db", kept);
        Assert.Contains("permalocke-auto-20260828-100400.db", kept);
        Assert.Contains("permalocke-auto-20260828-101300.db", kept);
    }

    /// <summary>
    /// A copy the player made by hand is theirs. The trimmer only ever counts and deletes files it
    /// wrote itself, which is what the name prefix is for.
    /// </summary>
    [Fact]
    public void Never_deletes_a_copy_somebody_else_put_there()
    {
        var backup = NewBackup();
        Directory.CreateDirectory(backup.Folder);

        var mine = Path.Combine(backup.Folder, "antes-de-la-final.db");
        File.WriteAllText(mine, "una copia a mano");

        var start = DateTimeOffset.Parse("2026-08-28T10:00:00Z");
        for (var i = 0; i < 14; i++)
        {
            WriteDatabase();
            backup.Run(start.AddMinutes(i));
        }

        Assert.True(File.Exists(mine));
    }

    /// <summary>
    /// Two starts inside the same second must not overwrite the copy just made: that would silently
    /// cost one of the ten.
    /// </summary>
    [Fact]
    public void Does_not_overwrite_a_copy_from_the_same_second()
    {
        var backup = NewBackup();
        var moment = DateTimeOffset.Parse("2026-08-28T10:00:00Z");

        WriteDatabase("primera");
        var first = backup.Run(moment);

        WriteDatabase("segunda");
        var second = backup.Run(moment);

        Assert.NotNull(first.File);
        Assert.Null(second.File);
        Assert.Equal("primera", File.ReadAllText(first.File));
    }

    /// <summary>
    /// It must never be the reason the application does not start. The player came to play.
    /// </summary>
    [Fact]
    public void Reports_the_problem_instead_of_throwing_when_it_cannot_write()
    {
        Directory.CreateDirectory(_root);
        WriteDatabase();

        // La carpeta de copias existe como FICHERO, asi que crear el directorio fallara.
        var blocked = Path.Combine(_root, "backup");
        Directory.CreateDirectory(_root);
        File.WriteAllText(blocked, "no soy una carpeta");

        var result = new RunBackup(_root).Run(DateTimeOffset.Parse("2026-08-28T10:00:00Z"));

        Assert.Null(result.File);
        Assert.Contains("Logs", result.Message);
    }
}
