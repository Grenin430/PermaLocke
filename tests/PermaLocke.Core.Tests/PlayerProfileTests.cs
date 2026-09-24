using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// This machine's player (§123): created once, renamed without losing who it is, and tied to runs
/// that have no owner — never to runs that have one.
/// </summary>
public sealed class PlayerProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-profile-{Guid.NewGuid():N}");
    private readonly SqliteEventStore _events;
    private readonly JsonRunRepository _runs;
    private readonly JsonPlayerProfileStore _store;
    private readonly RunContext _context = new();
    private readonly PlayerProfileService _service;

    public PlayerProfileTests()
    {
        Directory.CreateDirectory(_root);
        _events = new SqliteEventStore(Path.Combine(_root, "permalocke.db"));
        _runs = new JsonRunRepository(Path.Combine(_root, "Saves"));
        _store = new JsonPlayerProfileStore(Path.Combine(_root, "Config"));
        _service = new PlayerProfileService(_store, _runs, _events, _context, new FixedClock());
    }

    public void Dispose()
    {
        _events.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 9, 14, 18, 0, 0, TimeSpan.Zero);
    }

    private async Task<Run> LoadedRun(Guid? owner = null)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = "Mi PermaLocke",
            Game = GameVersion.UltraMoon,
            SeedLabel = "PERMA-1",
            Seed = 1,
            RoleId = "normal",
            PlayerName = "Grenin",
            PlayerId = owner
        };

        await _runs.SaveAsync(run);
        _context.SetCurrent(run);
        return run;
    }

    [Fact]
    public async Task The_profile_is_created_once_with_the_name_given_and_then_kept()
    {
        var first = await _service.EnsureAsync("Grenin");
        var second = await _service.EnsureAsync("Otro nombre");

        Assert.Equal("Grenin", first.Name);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Grenin", second.Name);
        Assert.True(File.Exists(Path.Combine(_root, "Config", JsonPlayerProfileStore.FileName)));
    }

    [Fact]
    public async Task Renaming_keeps_the_id()
    {
        var before = await _service.EnsureAsync("Grenin");
        var after = await _service.RenameAsync("  Grenin430  ");

        Assert.NotNull(after);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal("Grenin430", (await _store.LoadAsync())!.Name);
    }

    [Fact]
    public async Task An_empty_name_is_not_a_rename()
    {
        await _service.EnsureAsync("Grenin");

        Assert.Null(await _service.RenameAsync("   "));
        Assert.Equal("Grenin", (await _store.LoadAsync())!.Name);
    }

    [Theory]
    [InlineData("  Ash  ", "Ash")]
    [InlineData("Un nombre larguísimo que no cabe en el podio", "Un nombre larguísimo que")]
    [InlineData("\t\n", null)]
    [InlineData(null, null)]
    public void Names_are_cleaned(string? typed, string? expected) =>
        Assert.Equal(expected, PlayerProfileService.CleanName(typed));

    [Fact]
    public async Task A_run_without_owner_is_linked_once_with_its_event()
    {
        var run = await LoadedRun();

        Assert.Equal(RunOwnership.Linked, await _service.LinkCurrentRunAsync());
        Assert.Equal(RunOwnership.Mine, await _service.LinkCurrentRunAsync());

        var profile = await _store.LoadAsync();
        var saved = await _runs.GetAsync(run.Id);
        var linked = Assert.Single(await _events.GetAllAsync(run.Id));

        Assert.Equal(profile!.Id, saved!.PlayerId);
        Assert.Equal(profile.Id, _context.Current!.PlayerId);
        Assert.Equal(GameEventType.PlayerLinked, linked.Type);
        Assert.Equal(profile.Id.ToString("N"), linked.Data["playerId"]);

        // Sin nadie configurado, el perfil nace con el nombre que el jugador ya puso en su run.
        Assert.Equal("Grenin", profile.Name);
    }

    /// <summary>
    /// Measured on the real run: start-up and COMPETICIÓN linked the same run in the same second and the history got
    /// two events. Five at once must still be one link, one event and one profile.
    /// </summary>
    [Fact]
    public async Task Linking_at_the_same_time_from_several_places_links_once()
    {
        var run = await LoadedRun();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => _service.LinkCurrentRunAsync())));

        Assert.Single(results, r => r == RunOwnership.Linked);
        Assert.All(results, r => Assert.Contains(r, new[] { RunOwnership.Linked, RunOwnership.Mine }));
        Assert.Single(await _events.GetAllAsync(run.Id), e => e.Type == GameEventType.PlayerLinked);
        Assert.Equal((await _store.LoadAsync())!.Id, (await _runs.GetAsync(run.Id))!.PlayerId);
    }

    /// <summary>
    /// A run copied from a friend's PC carries its owner. It is never taken over: it would otherwise
    /// be published as this player's.
    /// </summary>
    [Fact]
    public async Task A_run_that_belongs_to_someone_else_is_never_taken()
    {
        await _service.EnsureAsync("Grenin");
        var foreign = Guid.NewGuid();
        var run = await LoadedRun(foreign);

        Assert.Equal(RunOwnership.Foreign, await _service.LinkCurrentRunAsync());
        Assert.Equal(foreign, (await _runs.GetAsync(run.Id))!.PlayerId);
        Assert.Empty(await _events.GetAllAsync(run.Id));
    }

    [Fact]
    public async Task No_run_loaded_is_said_and_creates_nothing()
    {
        Assert.Equal(RunOwnership.NoRun, await _service.LinkCurrentRunAsync());
        Assert.Null(await _store.LoadAsync());
    }

    [Fact]
    public async Task A_new_run_is_born_with_its_owner()
    {
        var profile = await _service.EnsureAsync("Grenin");
        var runs = new RunService(_runs, _events, new SqlitePokemonRepository(Path.Combine(_root, "permalocke.db")),
            _context, new FixedClock());

        var run = await runs.CreateAsync(new CreateRunRequest("Nueva", "Grenin", "normal", GameVersion.UltraMoon,
            PlayerId: profile.Id));

        Assert.Equal(profile.Id, run.PlayerId);
        Assert.Equal(RunOwnership.Mine, await _service.LinkCurrentRunAsync());
    }
}
