using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Tying a Pokémon the run granted to the one that now lives in the player's game.
/// </summary>
/// <remarks>
/// The PID is the whole point: <c>GameWatcher</c> matches the live party against the run by it and
/// by nothing else, so what these check is that a real one gets stored and that a zero — which is
/// "nobody told me", not an identity — never does.
/// </remarks>
public sealed class PokemonIdentityServiceTests
{
    private sealed class Events : IEventStore
    {
        public List<GameEvent> Appended { get; } = [];

        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
        {
            Appended.Add(gameEvent);
            return Task.FromResult(gameEvent);
        }

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Repository : IPokemonRepository
    {
        public List<PokemonEntry> Saved { get; } = [];

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>(Saved);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult(Saved.FirstOrDefault(p => p.Id == pokemonId));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>

            throw new NotSupportedException();


        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
        {
            Saved.Add(pokemon);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
    }

    private static Run TheRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Mi PermaLocke",
        PlayerName = "Grenin",
        Seed = 1234,
        SeedLabel = "PERMA-1234",
        RoleId = "normal",
        Game = GameVersion.UltraMoon
    };

    private static PokemonEntry Entry(uint? pid = null) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Species = 25,
        SpeciesName = "Pikachu",
        Origin = PokemonOrigin.Gacha,
        EncounterType = EncounterType.Special,
        Pid = pid
    };

    [Fact]
    public async Task It_stores_the_pid_and_says_where_it_landed()
    {
        var repository = new Repository();
        var events = new Events();
        var service = new PokemonIdentityService(repository, events, new FixedClock());

        var updated = await service.RememberDeliveryAsync(TheRun(), Entry(), 0xABCD1234, 3, 7);

        Assert.Equal(0xABCD1234u, updated.Pid);
        Assert.Equal(0xABCD1234u, Assert.Single(repository.Saved).Pid);

        var recorded = Assert.Single(events.Appended);
        Assert.Equal(GameEventType.PokemonDelivered, recorded.Type);
        Assert.Equal("ABCD1234", recorded.Data["pid"]);
        Assert.Equal("3", recorded.Data["caja"]);
        Assert.Equal("7", recorded.Data["hueco"]);
    }

    /// <summary>
    /// Zero is not an identity. Storing it would make every Pokémon that has none match every
    /// other one, which is worse than having no PID at all.
    /// </summary>
    [Fact]
    public async Task A_pid_of_zero_is_not_written()
    {
        var repository = new Repository();
        var events = new Events();
        var service = new PokemonIdentityService(repository, events, new FixedClock());

        var entry = Entry();
        var same = await service.RememberDeliveryAsync(TheRun(), entry, 0, 1, 1);

        Assert.Null(same.Pid);
        Assert.Empty(repository.Saved);
        Assert.Empty(events.Appended);
    }

    /// <summary>Nothing changed, so nothing is written: the log is not a place to repeat oneself.</summary>
    [Fact]
    public async Task The_same_pid_twice_does_not_record_anything()
    {
        var repository = new Repository();
        var events = new Events();
        var service = new PokemonIdentityService(repository, events, new FixedClock());

        await service.RememberDeliveryAsync(TheRun(), Entry(0x11112222), 0x11112222, 1, 1);

        Assert.Empty(repository.Saved);
        Assert.Empty(events.Appended);
    }

    /// <summary>
    /// It is the system recording what the game did, not the player doing something, and the log
    /// has to be able to tell the two apart.
    /// </summary>
    [Fact]
    public async Task It_is_recorded_as_the_system_and_costs_nothing()
    {
        var events = new Events();
        var service = new PokemonIdentityService(new Repository(), events, new FixedClock());

        await service.RememberDeliveryAsync(TheRun(), Entry(), 7, 1, 1);

        var recorded = Assert.Single(events.Appended);
        Assert.Equal(EventSource.System, recorded.Source);
        Assert.Equal(0, recorded.PointsDelta);
    }
}
