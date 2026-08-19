using System.Text.Json;
using Microsoft.Data.Sqlite;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Data;

/// <summary>
/// Append-only event log on SQLite. One database for every run, so PermaLocke.Admin can
/// query across them. Inserts are serialized: the hash chain requires reading the tail and
/// writing the new row as one operation.
/// </summary>
public sealed class SqliteEventStore : IEventStore, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public SqliteEventStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureSchema();
    }

    public async Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            var previousHash = await ReadTailHashAsync(connection, transaction, gameEvent.RunId, ct)
                .ConfigureAwait(false);

            var sealedEvent = EventHasher.Seal(gameEvent, previousHash);

            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO events
                    (id, run_id, timestamp, type, source, actor, description, points_delta,
                     pokemon_id, location_id, seed, reason, data, previous_hash, hash)
                VALUES
                    ($id, $run, $ts, $type, $source, $actor, $desc, $delta,
                     $pokemon, $location, $seed, $reason, $data, $prev, $hash);
                """;

            Bind(command, sealedEvent);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return sealedEvent;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE run_id = $run ORDER BY seq ASC;";
        command.Parameters.AddWithValue("$run", runId.ToString("N"));

        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE run_id = $run ORDER BY seq DESC LIMIT $count;";
        command.Parameters.AddWithValue("$run", runId.ToString("N"));
        command.Parameters.AddWithValue("$count", count);

        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }

    public async Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await GetAllAsync(runId, ct).ConfigureAwait(false);

        var expectedPrevious = string.Empty;
        var checkedCount = 0;

        foreach (var storedEvent in all)
        {
            checkedCount++;

            if (storedEvent.PreviousHash != expectedPrevious)
            {
                return new IntegrityReport(false, checkedCount, storedEvent.Id,
                    "El encadenado se rompe: falta un evento anterior o fue alterado.");
            }

            if (EventHasher.Compute(storedEvent, expectedPrevious) != storedEvent.Hash)
            {
                return new IntegrityReport(false, checkedCount, storedEvent.Id,
                    "El contenido del evento no coincide con su hash: fue modificado.");
            }

            expectedPrevious = storedEvent.Hash;
        }

        return new IntegrityReport(true, checkedCount, null, null);
    }

    public void Dispose() => _writeGate.Dispose();

    private const string SelectColumns =
        """
        SELECT id, run_id, timestamp, type, source, actor, description, points_delta,
               pokemon_id, location_id, seed, reason, data, previous_hash, hash
        FROM events
        """;

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS events (
                seq           INTEGER PRIMARY KEY AUTOINCREMENT,
                id            TEXT    NOT NULL UNIQUE,
                run_id        TEXT    NOT NULL,
                timestamp     TEXT    NOT NULL,
                type          INTEGER NOT NULL,
                source        INTEGER NOT NULL,
                actor         TEXT    NOT NULL,
                description   TEXT    NOT NULL,
                points_delta  INTEGER NOT NULL,
                pokemon_id    TEXT,
                location_id   TEXT,
                seed          TEXT,
                reason        TEXT,
                data          TEXT    NOT NULL,
                previous_hash TEXT    NOT NULL,
                hash          TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_events_run ON events (run_id, seq);
            """;
        command.ExecuteNonQuery();
    }

    private static async Task<string> ReadTailHashAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, Guid runId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT hash FROM events WHERE run_id = $run ORDER BY seq DESC LIMIT 1;";
        command.Parameters.AddWithValue("$run", runId.ToString("N"));

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string ?? string.Empty;
    }

    private static void Bind(SqliteCommand command, GameEvent e)
    {
        command.Parameters.AddWithValue("$id", e.Id.ToString("N"));
        command.Parameters.AddWithValue("$run", e.RunId.ToString("N"));
        command.Parameters.AddWithValue("$ts", e.Timestamp.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$type", (int)e.Type);
        command.Parameters.AddWithValue("$source", (int)e.Source);
        command.Parameters.AddWithValue("$actor", e.Actor);
        command.Parameters.AddWithValue("$desc", e.Description);
        command.Parameters.AddWithValue("$delta", e.PointsDelta);
        command.Parameters.AddWithValue("$pokemon", (object?)e.PokemonId?.ToString("N") ?? DBNull.Value);
        command.Parameters.AddWithValue("$location", (object?)e.LocationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$seed", (object?)e.Seed?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$reason", (object?)e.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(e.Data));
        command.Parameters.AddWithValue("$prev", e.PreviousHash);
        command.Parameters.AddWithValue("$hash", e.Hash);
    }

    private static async Task<IReadOnlyList<GameEvent>> ReadAllAsync(SqliteCommand command, CancellationToken ct)
    {
        var results = new List<GameEvent>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new GameEvent
            {
                Id = Guid.ParseExact(reader.GetString(0), "N"),
                RunId = Guid.ParseExact(reader.GetString(1), "N"),
                Timestamp = DateTimeOffset.Parse(reader.GetString(2)),
                Type = (GameEventType)reader.GetInt32(3),
                Source = (EventSource)reader.GetInt32(4),
                Actor = reader.GetString(5),
                Description = reader.GetString(6),
                PointsDelta = reader.GetInt32(7),
                PokemonId = reader.IsDBNull(8) ? null : Guid.ParseExact(reader.GetString(8), "N"),
                LocationId = reader.IsDBNull(9) ? null : reader.GetString(9),
                Seed = reader.IsDBNull(10) ? null : ulong.Parse(reader.GetString(10)),
                Reason = reader.IsDBNull(11) ? null : reader.GetString(11),
                Data = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(12)) ?? [],
                PreviousHash = reader.GetString(13),
                Hash = reader.GetString(14)
            });
        }

        return results;
    }
}
