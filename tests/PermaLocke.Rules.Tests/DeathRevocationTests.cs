using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// Undoing a death that should not be in the run.
/// </summary>
/// <remarks>
/// The case it is for is a hand mark gone wrong — MARCAR COMO CAÍDO on the wrong Pokémon — and not a
/// misread of the game: the one death once blamed on a bad reading turned out to be real (§111). The
/// chain cannot lose an event, so undoing is an addition, and what is paid back is what was charged.
/// </remarks>
public sealed class DeathRevocationTests
{
    private static readonly Guid Run = Guid.NewGuid();
    private const uint OkidogiPid = 0x5DE14D71;
    private const uint SlakingPid = 0x13B76CF2;

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 10, 21, 39, 0, TimeSpan.Zero);
    }

    private sealed class Events : IEventStore
    {
        public List<GameEvent> All { get; } = [];

        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
        {
            All.Add(gameEvent);
            return Task.FromResult(gameEvent);
        }

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(All);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(All);

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public int Balance => All.Sum(e => e.PointsDelta);
    }

    private sealed class Pokemon : IPokemonRepository
    {
        public Dictionary<Guid, PokemonEntry> Entries { get; } = [];

        public Task SaveAsync(PokemonEntry entry, CancellationToken ct = default)
        {
            Entries[entry.Id] = entry;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([.. Entries.Values]);

        public Task<PokemonEntry?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Entries.GetValueOrDefault(id));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Penalties(int perDeath) : IPenaltyCatalog
    {
        public PenaltyRules Rules { get; } = new(PerDeath: perDeath, PerWipe: 100, MaxWipes: 4);
    }

    private sealed class NoRole : IRunRoles
    {
        public Role? Of(Guid runId) => null;
    }

    private sealed record World(GameWatcher Watcher, Events Log, Pokemon Team, Clock Clock, PokemonEntry Okidogi);

    private static World Build(int perDeath = 25, Events? log = null, Pokemon? team = null, Clock? clock = null)
    {
        log ??= new Events();
        team ??= new Pokemon();
        clock ??= new Clock();

        var okidogi = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = Run,
            Species = 1014,
            SpeciesName = "Okidogi",
            Origin = PokemonOrigin.Gacha,
            EncounterType = EncounterType.Special,
            Pid = OkidogiPid,
            Status = PokemonStatus.Alive
        };

        if (!team.Entries.Values.Any(e => e.Pid == OkidogiPid))
        {
            team.Entries[okidogi.Id] = okidogi;
        }
        else
        {
            okidogi = team.Entries.Values.Single(e => e.Pid == OkidogiPid);
        }

        var penalties = new PenaltyService(new Penalties(perDeath), log, clock, new NoRole());
        return new World(new GameWatcher(team, log, clock, penalties), log, team, clock, okidogi);
    }

    private static async Task<PokemonEntry> Kill(World world)
    {
        await world.Watcher.RecordDeathAsync(world.Okidogi, "Grenin");
        return world.Team.Entries[world.Okidogi.Id];
    }

    /// <summary>
    /// Revoking brings the Pokémon back and pays back exactly what its death cost.
    /// </summary>
    [Fact]
    public async Task Revoking_a_death_brings_it_back_and_pays_back_the_penalty()
    {
        var world = Build();
        var dead = await Kill(world);

        Assert.Equal(PokemonStatus.Dead, dead.Status);
        Assert.Equal(-25, world.Log.Balance);

        Assert.Null(await world.Watcher.RevokeDeathAsync(dead, "Grenin", "lectura con el juego a medio cargar"));

        Assert.Equal(PokemonStatus.Alive, world.Team.Entries[dead.Id].Status);
        Assert.Null(world.Team.Entries[dead.Id].DiedAt);
        Assert.Equal(0, world.Log.Balance);

        var revocation = Assert.Single(world.Log.All, e => e.Type == GameEventType.DeathRevoked);
        var death = Assert.Single(world.Log.All, e => e.Type == GameEventType.PokemonDied);

        // La muerte se queda en el historial; la revocación dice cuál y quién.
        Assert.Equal(death.Id.ToString(), revocation.Data["muerte"]);
        Assert.Equal(EventSource.Player, revocation.Source);
        Assert.Equal("lectura con el juego a medio cargar", revocation.Reason);
    }

    /// <summary>
    /// The refund is what was charged, not today's price.
    /// </summary>
    /// <remarks>
    /// Charged at 25 and revoked with the configuration saying 100: it pays back 25. Reading the
    /// price again would hand out points that were never taken.
    /// </remarks>
    [Fact]
    public async Task The_refund_is_what_was_charged_and_not_todays_price()
    {
        var charged = Build(perDeath: 25);
        var dead = await Kill(charged);

        var repriced = Build(perDeath: 100, log: charged.Log, team: charged.Team, clock: charged.Clock);

        Assert.Null(await repriced.Watcher.RevokeDeathAsync(dead, "Grenin", "falsa"));
        Assert.Equal(0, charged.Log.Balance);
    }

    /// <summary>The same death cannot be revoked twice, so it cannot be paid back twice.</summary>
    [Fact]
    public async Task A_death_cannot_be_revoked_twice()
    {
        var world = Build();
        var dead = await Kill(world);

        Assert.Null(await world.Watcher.RevokeDeathAsync(dead, "Grenin", "falsa"));

        // Aunque alguien le pase la entrada vieja, que todavía dice «muerto».
        Assert.NotNull(await world.Watcher.RevokeDeathAsync(dead, "Grenin", "otra vez"));

        Assert.Single(world.Log.All, e => e.Type == GameEventType.DeathRevoked);
        Assert.Equal(0, world.Log.Balance);
    }

    /// <summary>A Pokémon that is not dead has no death to revoke.</summary>
    [Fact]
    public async Task A_living_pokemon_has_nothing_to_revoke()
    {
        var world = Build();

        Assert.NotNull(await world.Watcher.RevokeDeathAsync(world.Okidogi, "Grenin", "por si acaso"));
        Assert.Empty(world.Log.All);
    }
}
