using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// A wonder trade registered the Pokémon that arrived and said nothing about the one that left, so
/// its record stayed alive for ever. HOME counting it among the living is the same class of error
/// as not counting a death.
/// </summary>
public sealed class TradedAwayReconcilerTests
{
    private static readonly Run TheRun = new()
    {
        Id = Guid.NewGuid(),
        Name = "PRUEBA",
        PlayerName = "Grenin",
        Seed = 1,
        SeedLabel = "PERMA-000001",
        Game = GameVersion.UltraMoon,
        RoleId = "normal",
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    private static PokemonEntry Registered(int species, string name, uint? pid,
        PokemonStatus status = PokemonStatus.Alive, int minute = 0) => new()
    {
        Id = Guid.NewGuid(),
        RunId = TheRun.Id,
        Species = species,
        SpeciesName = name,
        Status = status,
        Pid = pid,
        Origin = PokemonOrigin.WonderTrade,
        EncounterType = EncounterType.Unknown,
        ObtainedAt = DateTimeOffset.UnixEpoch.AddMinutes(minute)
    };

    private static GameEvent Trade(int handedOver) => new()
    {
        Id = Guid.NewGuid(),
        RunId = TheRun.Id,
        Timestamp = DateTimeOffset.UnixEpoch,
        Type = GameEventType.WonderTrade,
        Source = EventSource.Player,
        Actor = "Grenin",
        Description = "wonder trade",
        Data = new Dictionary<string, string> { ["entregado"] = handedOver.ToString() }
    };

    private static TradedAwayReconciler Build(
        IEnumerable<PokemonEntry> registered, IEnumerable<GameEvent> history,
        out FakePokemonRepository repository)
    {
        repository = new FakePokemonRepository(registered);
        return new TradedAwayReconciler(repository, new FakeEventStore(history), new FixedClock());
    }

    [Fact]
    public async Task Closes_the_one_the_history_says_was_handed_over()
    {
        var gone = Registered(335, "Pumpkaboo", pid: null);

        var reconciler = Build([gone, Registered(390, "Munchlax", pid: 42u)],
            [Trade(335)], out var repository);

        var report = await reconciler.RepairAsync(TheRun);

        Assert.Single(report.Matched);
        Assert.Equal(335, report.Matched[0].Species);
        Assert.True(report.Written);
        Assert.Equal(PokemonStatus.Traded, repository.Stored.Single(p => p.Id == gone.Id).Status);
    }

    /// <summary>
    /// The whole point of the design. Two of a species handed over and three with no PID means the
    /// counts disagree, and choosing which two left would be inventing history inside a log chained
    /// by hash. It reports and touches nothing.
    /// </summary>
    [Fact]
    public async Task Refuses_to_guess_when_the_two_counts_disagree()
    {
        var reconciler = Build(
            [
                Registered(487, "Giratina", pid: null, minute: 1),
                Registered(487, "Giratina", pid: null, minute: 2),
                Registered(487, "Giratina", pid: null, minute: 3)
            ],
            [Trade(487), Trade(487)],
            out var repository);

        var report = await reconciler.RepairAsync(TheRun);

        Assert.Empty(report.Matched);
        Assert.Single(report.Disputed);
        Assert.Contains("487", report.Disputed[0]);
        Assert.All(repository.Stored, p => Assert.Equal(PokemonStatus.Alive, p.Status));
    }

    /// <summary>A Pokémon that is still in the save has a PID, so it was never a candidate.</summary>
    [Fact]
    public async Task Never_touches_one_that_is_still_in_the_save()
    {
        var alive = Registered(392, "Infernape", pid: 7u);

        var reconciler = Build([alive], [Trade(392)], out var repository);

        var report = await reconciler.RepairAsync(TheRun);

        Assert.Empty(report.Matched);
        Assert.Equal(PokemonStatus.Alive, repository.Stored.Single().Status);
    }

    [Fact]
    public async Task Inspecting_writes_nothing()
    {
        var reconciler = Build([Registered(335, "Pumpkaboo", pid: null)], [Trade(335)],
            out var repository);

        var report = await reconciler.InspectAsync(TheRun);

        Assert.Single(report.Matched);
        Assert.False(report.Written);
        Assert.Equal(PokemonStatus.Alive, repository.Stored.Single().Status);
    }

    /// <summary>
    /// Every closure leaves its own event. Rule 4 of the project: no state moves without one.
    /// </summary>
    [Fact]
    public async Task Leaves_an_event_for_every_one_it_closes()
    {
        var store = new FakeEventStore([Trade(335), Trade(390)]);

        var reconciler = new TradedAwayReconciler(
            new FakePokemonRepository([
                Registered(335, "Pumpkaboo", pid: null),
                Registered(390, "Munchlax", pid: null)
            ]),
            store, new FixedClock());

        await reconciler.RepairAsync(TheRun);

        var written = store.Appended.Where(e => e.Type == GameEventType.PokemonTraded).ToList();

        Assert.Equal(2, written.Count);
        Assert.All(written, e => Assert.Equal(EventSource.System, e.Source));
        Assert.All(written, e => Assert.Contains("reparacion", e.Data["motivo"]));
    }

    /// <summary>A run that already adds up must come back with nothing to do and say so.</summary>
    [Fact]
    public async Task Says_the_run_already_adds_up_when_there_is_nothing_to_close()
    {
        var reconciler = Build([Registered(390, "Munchlax", pid: 42u)], [], out _);

        var report = await reconciler.InspectAsync(TheRun);

        Assert.Empty(report.Matched);
        Assert.Empty(report.Disputed);
        Assert.Contains("cuadra", report.Message);
    }

    private sealed class FakePokemonRepository(IEnumerable<PokemonEntry> seed) : IPokemonRepository
    {
        public List<PokemonEntry> Stored { get; } = seed.ToList();

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PokemonEntry>>(Stored);

        public Task SaveAsync(PokemonEntry entry, CancellationToken ct = default)
        {
            Stored.RemoveAll(p => p.Id == entry.Id);
            Stored.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PokemonEntry?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Stored.FirstOrDefault(p => p.Id == id));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private sealed class FakeEventStore(IEnumerable<GameEvent> seed) : IEventStore
    {
        private readonly List<GameEvent> _all = seed.ToList();

        public List<GameEvent> Appended { get; } = [];

        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
        {
            Appended.Add(gameEvent);
            _all.Add(gameEvent);
            return Task.FromResult(gameEvent);
        }

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameEvent>>(_all);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameEvent>>(_all.TakeLast(count).ToList());

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default)
            => Task.FromResult(new IntegrityReport(true, _all.Count, null, null));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UnixEpoch;
    }
}
