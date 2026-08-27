using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>End-to-end over the real persistence: JSON run files plus the SQLite event log.</summary>
public sealed class RunServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-run-{Guid.NewGuid():N}");
    private readonly SqliteEventStore _events;
    private readonly SqlitePokemonRepository _pokemon;
    private readonly RunService _service;
    private readonly RunContext _context = new();

    public RunServiceTests()
    {
        Directory.CreateDirectory(_root);
        _events = new SqliteEventStore(Path.Combine(_root, "permalocke.db"));
        _pokemon = new SqlitePokemonRepository(Path.Combine(_root, "permalocke.db"));
        _service = new RunService(new JsonRunRepository(_root), _events, _pokemon,
            _context, new FixedClock());
    }

    private static CreateRunRequest Request(string name = "Run de prueba") =>
        new(name, "javi", "experto", GameVersion.UltraMoon, TitleId: "00040000001B5100");

    [Fact]
    public async Task Creating_a_run_writes_run_json_and_a_RunCreated_event()
    {
        var run = await _service.CreateAsync(Request());

        Assert.True(File.Exists(Path.Combine(_root, run.Id.ToString("N"), "run.json")));

        var single = Assert.Single(await _events.GetAllAsync(run.Id));
        Assert.Equal(GameEventType.RunCreated, single.Type);
        Assert.Equal(run.Seed, single.Seed);
        Assert.Equal("00040000001B5100", single.Data["titleId"]);
    }

    [Fact]
    public async Task A_new_run_starts_on_Melemele_with_the_rest_locked()
    {
        var run = await _service.CreateAsync(Request());

        Assert.Equal(4, run.Islands.Count);
        Assert.Equal("Melemele", run.Islands[0].Name);
        Assert.Equal(IslandState.InProgress, run.Islands[0].State);
        Assert.All(run.Islands.Skip(1), island => Assert.Equal(IslandState.Locked, island.State));
    }

    [Fact]
    public async Task An_explicit_seed_is_honoured_and_labelled()
    {
        var run = await _service.CreateAsync(Request() with { Seed = 839421UL });

        Assert.Equal(839421UL, run.Seed);
        Assert.Equal("PERMA-839421", run.SeedLabel);
    }

    [Fact]
    public async Task Two_runs_created_in_a_row_get_different_seeds()
    {
        var first = await _service.CreateAsync(Request("una"));
        var second = await _service.CreateAsync(Request("otra"));

        Assert.NotEqual(first.Seed, second.Seed);
    }

    [Fact]
    public async Task The_most_recent_run_is_the_one_reloaded()
    {
        await _service.CreateAsync(Request("antigua"));
        var newest = await _service.CreateAsync(Request("reciente") with { });

        _context.SetCurrent(null);
        var loaded = await _service.LoadMostRecentAsync();

        Assert.NotNull(loaded);
        Assert.Equal(newest.Id, loaded!.Id);
        Assert.Equal(loaded, _context.Current);
    }

    [Fact]
    public async Task A_saved_run_round_trips_through_run_json()
    {
        var created = await _service.CreateAsync(Request());

        var reloaded = await new JsonRunRepository(_root).GetAsync(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(created.Name, reloaded!.Name);
        Assert.Equal(created.Seed, reloaded.Seed);
        Assert.Equal(created.RoleId, reloaded.RoleId);
        Assert.Equal(GameVersion.UltraMoon, reloaded.Game);
        Assert.Equal(created.Islands.Count, reloaded.Islands.Count);
    }

    [Fact]
    public async Task Changing_a_role_is_saved_and_leaves_an_audit_event()
    {
        var created = await _service.CreateAsync(Request());

        var changed = await _service.ChangeRoleAsync(created, "ludopata",
            "El mod LayeredFS se regeneró para el nuevo rol.");

        var reloaded = await new JsonRunRepository(_root).GetAsync(created.Id);
        var migration = Assert.Single(await _events.GetAllAsync(created.Id),
            e => e.Type == GameEventType.RoleChanged);

        Assert.Equal("ludopata", changed.RoleId);
        Assert.Equal("ludopata", reloaded!.RoleId);
        Assert.Equal(changed, _context.Current);
        Assert.Equal("experto", migration.Data["desde"]);
        Assert.Equal("ludopata", migration.Data["hasta"]);
    }

    /// <summary>
    /// Deleting a run takes the whole thing: folder, events and Pokémon.
    /// </summary>
    /// <remarks>
    /// The three are checked separately on purpose. Each lives in a different place -- a folder, a
    /// table, another table -- and a deletion that forgot one of them would leave the app looking
    /// clean while the rows it no longer names sat in the database forever.
    /// </remarks>
    [Fact]
    public async Task Deleting_a_run_takes_its_folder_its_events_and_its_pokemon()
    {
        var run = await _service.CreateAsync(Request());

        await _pokemon.SaveAsync(new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = 25,
            SpeciesName = "Pikachu",
            Level = 5,
            Origin = PokemonOrigin.Capture,
            EncounterType = EncounterType.Wild,
            ObtainedAt = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero)
        });

        var gone = await _service.DeleteAsync(run.Id);

        Assert.True(gone.Deleted);
        Assert.Equal("Run de prueba", gone.Name);
        Assert.Equal(1, gone.Events);
        Assert.Equal(1, gone.Pokemon);

        Assert.False(Directory.Exists(Path.Combine(_root, run.Id.ToString("N"))));
        Assert.Empty(await _events.GetAllAsync(run.Id));
        Assert.Empty(await _pokemon.GetAllAsync(run.Id));
    }

    /// <summary>A deleted run must not stay loaded, or the app points at something gone.</summary>
    [Fact]
    public async Task Deleting_the_loaded_run_leaves_the_app_with_none()
    {
        var run = await _service.CreateAsync(Request());
        Assert.Equal(run, _context.Current);

        await _service.DeleteAsync(run.Id);

        Assert.Null(_context.Current);
    }

    /// <summary>Only the run asked for: a second run is not collateral damage.</summary>
    [Fact]
    public async Task Deleting_one_run_leaves_the_others_alone()
    {
        var first = await _service.CreateAsync(Request("La primera"));
        var second = await _service.CreateAsync(Request("La segunda"));

        await _service.DeleteAsync(first.Id);

        Assert.Single(await _events.GetAllAsync(second.Id));
        Assert.True(File.Exists(Path.Combine(_root, second.Id.ToString("N"), "run.json")));
        Assert.Equal(second, _context.Current);
    }

    [Fact]
    public async Task Deleting_a_run_that_is_not_there_says_so_instead_of_throwing()
    {
        var gone = await _service.DeleteAsync(Guid.NewGuid());

        Assert.False(gone.Deleted);
        Assert.Equal(0, gone.Events);
    }

    public void Dispose()
    {
        _events.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Advances one second per read so run creation order is unambiguous.</summary>
    private sealed class FixedClock : IClock
    {
        private DateTimeOffset _now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Now => _now = _now.AddSeconds(1);
    }
}
