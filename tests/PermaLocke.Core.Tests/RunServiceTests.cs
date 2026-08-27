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
    private readonly RunService _service;
    private readonly RunContext _context = new();

    public RunServiceTests()
    {
        Directory.CreateDirectory(_root);
        _events = new SqliteEventStore(Path.Combine(_root, "permalocke.db"));
        _service = new RunService(new JsonRunRepository(_root), _events, _context, new FixedClock());
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
