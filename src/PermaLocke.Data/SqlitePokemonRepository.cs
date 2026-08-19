using Microsoft.Data.Sqlite;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>
/// Pokémon of every run, in the same database as the event log. Unlike the log this table is
/// mutable — a Pokémon dies, is released, changes level — but every mutation is paired with
/// an event by the calling service, so the history still explains the current state.
/// </summary>
public sealed class SqlitePokemonRepository : IPokemonRepository
{
    private readonly string _connectionString;

    public SqlitePokemonRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureSchema();
    }

    public async Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE run_id = $run ORDER BY obtained_at ASC;";
        command.Parameters.AddWithValue("$run", runId.ToString("N"));

        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }

    public async Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", pokemonId.ToString("N"));

        var all = await ReadAllAsync(command, ct).ConfigureAwait(false);
        return all.Count == 0 ? null : all[0];
    }

    public async Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO pokemon
                (id, run_id, species, species_name, nickname, level, is_shiny, status, origin,
                 encounter_type, location_id, obtained_at, died_at, origin_event_id,
                 by_exception, consumed_zone, pid)
            VALUES
                ($id, $run, $species, $name, $nick, $level, $shiny, $status, $origin,
                 $encounter, $location, $obtained, $died, $event,
                 $exception, $consumed, $pid)
            ON CONFLICT(id) DO UPDATE SET
                species = excluded.species, species_name = excluded.species_name,
                nickname = excluded.nickname, level = excluded.level, is_shiny = excluded.is_shiny,
                status = excluded.status, origin = excluded.origin,
                encounter_type = excluded.encounter_type, location_id = excluded.location_id,
                died_at = excluded.died_at, by_exception = excluded.by_exception,
                consumed_zone = excluded.consumed_zone, pid = excluded.pid;
            """;

        command.Parameters.AddWithValue("$id", pokemon.Id.ToString("N"));
        command.Parameters.AddWithValue("$run", pokemon.RunId.ToString("N"));
        command.Parameters.AddWithValue("$species", pokemon.Species);
        command.Parameters.AddWithValue("$name", pokemon.SpeciesName);
        command.Parameters.AddWithValue("$nick", (object?)pokemon.Nickname ?? DBNull.Value);
        command.Parameters.AddWithValue("$level", pokemon.Level);
        command.Parameters.AddWithValue("$shiny", pokemon.IsShiny ? 1 : 0);
        command.Parameters.AddWithValue("$status", (int)pokemon.Status);
        command.Parameters.AddWithValue("$origin", (int)pokemon.Origin);
        command.Parameters.AddWithValue("$encounter", (int)pokemon.EncounterType);
        command.Parameters.AddWithValue("$location", (object?)pokemon.LocationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$obtained", pokemon.ObtainedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$died",
            (object?)pokemon.DiedAt?.ToUniversalTime().ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$event", (object?)pokemon.OriginEventId?.ToString("N") ?? DBNull.Value);
        command.Parameters.AddWithValue("$exception", pokemon.ObtainedByRuleException ? 1 : 0);
        command.Parameters.AddWithValue("$consumed", pokemon.ConsumedZoneEncounter ? 1 : 0);
        command.Parameters.AddWithValue("$pid", (object?)pokemon.Pid ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private const string SelectColumns =
        """
        SELECT id, run_id, species, species_name, nickname, level, is_shiny, status, origin,
               encounter_type, location_id, obtained_at, died_at, origin_event_id,
               by_exception, consumed_zone, pid
        FROM pokemon
        """;

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS pokemon (
                id              TEXT PRIMARY KEY,
                run_id          TEXT    NOT NULL,
                species         INTEGER NOT NULL,
                species_name    TEXT    NOT NULL,
                nickname        TEXT,
                level           INTEGER NOT NULL,
                is_shiny        INTEGER NOT NULL,
                status          INTEGER NOT NULL,
                origin          INTEGER NOT NULL,
                encounter_type  INTEGER NOT NULL,
                location_id     TEXT,
                obtained_at     TEXT    NOT NULL,
                died_at         TEXT,
                origin_event_id TEXT,
                by_exception    INTEGER NOT NULL,
                consumed_zone   INTEGER NOT NULL,
                pid             INTEGER
            );
            CREATE INDEX IF NOT EXISTS idx_pokemon_run ON pokemon (run_id);
            """;
        command.ExecuteNonQuery();
    }

    private static async Task<IReadOnlyList<PokemonEntry>> ReadAllAsync(SqliteCommand command, CancellationToken ct)
    {
        var results = new List<PokemonEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new PokemonEntry
            {
                Id = Guid.ParseExact(reader.GetString(0), "N"),
                RunId = Guid.ParseExact(reader.GetString(1), "N"),
                Species = reader.GetInt32(2),
                SpeciesName = reader.GetString(3),
                Nickname = reader.IsDBNull(4) ? null : reader.GetString(4),
                Level = reader.GetInt32(5),
                IsShiny = reader.GetInt32(6) == 1,
                Status = (PokemonStatus)reader.GetInt32(7),
                Origin = (PokemonOrigin)reader.GetInt32(8),
                EncounterType = (EncounterType)reader.GetInt32(9),
                LocationId = reader.IsDBNull(10) ? null : reader.GetString(10),
                ObtainedAt = DateTimeOffset.Parse(reader.GetString(11)),
                DiedAt = reader.IsDBNull(12) ? null : DateTimeOffset.Parse(reader.GetString(12)),
                OriginEventId = reader.IsDBNull(13) ? null : Guid.ParseExact(reader.GetString(13), "N"),
                ObtainedByRuleException = reader.GetInt32(14) == 1,
                ConsumedZoneEncounter = reader.GetInt32(15) == 1,
                Pid = reader.IsDBNull(16) ? null : (uint)reader.GetInt64(16)
            });
        }

        return results;
    }
}
