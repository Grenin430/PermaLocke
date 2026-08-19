using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>Exercises the real store against a throwaway database file.</summary>
public sealed class SqliteEventStoreTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"permalocke-test-{Guid.NewGuid():N}", "events.db");

    private readonly Guid _runId = Guid.NewGuid();

    private GameEvent Sample(int delta, string description) => new()
    {
        Id = Guid.NewGuid(),
        RunId = _runId,
        Timestamp = DateTimeOffset.UtcNow,
        Type = GameEventType.PointsEarned,
        Source = EventSource.Player,
        Actor = "javi",
        Description = description,
        PointsDelta = delta,
        Data = new Dictionary<string, string> { ["origen"] = "test" }
    };

    [Fact]
    public async Task Events_survive_a_reopen_and_keep_their_order_and_chain()
    {
        using (var store = new SqliteEventStore(_databasePath))
        {
            await store.AppendAsync(Sample(10, "uno"));
            await store.AppendAsync(Sample(20, "dos"));
            await store.AppendAsync(Sample(-5, "tres"));
        }

        using var reopened = new SqliteEventStore(_databasePath);
        var all = await reopened.GetAllAsync(_runId);

        Assert.Equal(["uno", "dos", "tres"], all.Select(e => e.Description));
        Assert.Equal(25, all.Sum(e => e.PointsDelta));
        Assert.Equal("test", all[0].Data["origen"]);

        var report = await reopened.VerifyChainAsync(_runId);
        Assert.True(report.IsValid);
        Assert.Equal(3, report.CheckedEvents);
    }

    [Fact]
    public async Task Editing_a_row_by_hand_breaks_the_chain()
    {
        using var store = new SqliteEventStore(_databasePath);
        await store.AppendAsync(Sample(10, "uno"));
        await store.AppendAsync(Sample(20, "dos"));

        // Simulates someone opening the database with a SQLite editor and adding points.
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE events SET points_delta = 9999 WHERE description = 'uno';";
            await command.ExecuteNonQueryAsync();
        }

        var report = await store.VerifyChainAsync(_runId);

        Assert.False(report.IsValid);
        Assert.NotNull(report.Message);
    }

    [Fact]
    public async Task Latest_returns_the_newest_events_first()
    {
        using var store = new SqliteEventStore(_databasePath);
        await store.AppendAsync(Sample(1, "viejo"));
        await store.AppendAsync(Sample(2, "medio"));
        await store.AppendAsync(Sample(3, "nuevo"));

        var latest = await store.GetLatestAsync(_runId, 2);

        Assert.Equal(["nuevo", "medio"], latest.Select(e => e.Description));
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections, so the file stays open until the pool
        // is cleared. Without this the temporary folder cannot be deleted on Windows.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var folder = Path.GetDirectoryName(_databasePath)!;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
