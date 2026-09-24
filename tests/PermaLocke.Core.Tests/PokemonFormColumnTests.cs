using Microsoft.Data.Sqlite;
using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The run remembers the form each Pokémon arrived in, and an old run gains the column untouched (§140).
/// </summary>
public sealed class PokemonFormColumnTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"permalocke-forma-{Guid.NewGuid():N}");

    private string Database => Path.Combine(_folder, "permalocke.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static PokemonEntry Vulpix(Guid run, int form) => new()
    {
        Id = Guid.NewGuid(),
        RunId = run,
        Species = 37,
        SpeciesName = form == 1 ? "Vulpix de Alola" : "Vulpix",
        Level = 12,
        Origin = PokemonOrigin.Gacha,
        EncounterType = EncounterType.Special,
        ObtainedAt = DateTimeOffset.Now,
        Pid = 0xCAFE,
        Form = form
    };

    [Fact]
    public async Task The_form_goes_in_and_comes_back()
    {
        var run = Guid.NewGuid();
        var repository = new SqlitePokemonRepository(Database);

        await repository.SaveAsync(Vulpix(run, 1));

        Assert.Equal(1, (await repository.GetAllAsync(run)).Single().Form);
    }

    /// <summary>
    /// A database from before the column: it is added, the old rows read form 0, and nothing else in
    /// them moves.
    /// </summary>
    [Fact]
    public async Task An_old_run_gains_the_column_and_keeps_every_row()
    {
        Directory.CreateDirectory(_folder);
        var run = Guid.NewGuid().ToString("N");

        using (var old = new SqliteConnection($"Data Source={Database}"))
        {
            old.Open();
            using var command = old.CreateCommand();
            command.CommandText =
                $"""
                CREATE TABLE pokemon (
                    id TEXT PRIMARY KEY, run_id TEXT NOT NULL, species INTEGER NOT NULL,
                    species_name TEXT NOT NULL, nickname TEXT, level INTEGER NOT NULL,
                    is_shiny INTEGER NOT NULL, status INTEGER NOT NULL, origin INTEGER NOT NULL,
                    encounter_type INTEGER NOT NULL, location_id TEXT, obtained_at TEXT NOT NULL,
                    died_at TEXT, origin_event_id TEXT, by_exception INTEGER NOT NULL,
                    consumed_zone INTEGER NOT NULL, pid INTEGER);
                INSERT INTO pokemon VALUES ('{Guid.NewGuid():N}', '{run}', 555, 'Darmanitan', 'Pepe', 40, 0, 1, 0,
                    0, 'Ruta 1', '2026-09-01T10:00:00.0000000+00:00', NULL, NULL, 0, 1, 305419896);
                """;
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();

        var repository = new SqlitePokemonRepository(Database);
        var entry = (await repository.GetAllAsync(Guid.ParseExact(run, "N"))).Single();

        Assert.Equal(0, entry.Form);
        Assert.Equal(555, entry.Species);
        Assert.Equal("Pepe", entry.Nickname);
        Assert.Equal(PokemonStatus.Dead, entry.Status);
        Assert.Equal(0x12345678u, entry.Pid);

        // Y una segunda apertura no intenta añadirla otra vez.
        _ = new SqlitePokemonRepository(Database);
    }
}
