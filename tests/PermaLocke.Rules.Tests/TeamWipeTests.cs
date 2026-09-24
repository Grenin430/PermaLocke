using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The −100 for the whole party going down, which had no test at all until it nearly broke.
/// </summary>
/// <remarks>
/// It charges points off a reading of the game's memory, which is the combination that deserves
/// covering most: a wrong reading spends the player's score and nothing says so. What forced these
/// tests was making the party provider prefer the authoritative structure over the mirror — the
/// right call for spotting deaths, and it leaves short reads now and then, so «nobody standing»
/// stopped being proof on its own.
/// </remarks>
public sealed class TeamWipeTests
{
    private static readonly Guid Run = Guid.NewGuid();

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    }

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

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoPokemon : IPokemonRepository
    {
        public Task SaveAsync(PokemonEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([]);

        public Task<PokemonEntry?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<PokemonEntry?>(null);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Penalties : IPenaltyCatalog
    {
        public PenaltyRules Rules { get; } = new(PerDeath: 25, PerWipe: 100, MaxWipes: 4);
    }

    /// <summary>Sin rol: la tarifa base, que es la que dicen los numeros de la competicion.</summary>
    private sealed class NoRole : IRunRoles
    {
        public Role? Of(Guid runId) => null;
    }

    private static (GameWatcher Watcher, Events Log) Build()
    {
        var events = new Events();
        var clock = new Clock();
        var penalties = new PenaltyService(new Penalties(), events, clock, new NoRole());

        return (new GameWatcher(new NoPokemon(), events, clock, penalties), events);
    }

    private static LivePartyMember Member(int slot, int hp) =>
        new(slot, 25, "Pikachu", string.Empty, 30, hp, 100, false, (uint)(0xA000 + slot), 0, "Ruta 1", "Grenin");

    private static GameSnapshot Party(params int[] hp) =>
        new(true, null, [.. hp.Select((points, slot) => Member(slot, points))],
            new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));

    /// <summary>A party with somebody up is not a wipe, however many are down.</summary>
    [Fact]
    public async Task One_standing_is_not_a_wipe()
    {
        var (watcher, log) = Build();

        for (var poll = 0; poll < 5; poll++)
        {
            Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0, 0, 0, 7)));
        }

        Assert.Empty(log.Appended);
    }

    /// <summary>
    /// A single look with nobody standing does not charge; three in a row does.
    /// </summary>
    /// <remarks>
    /// The delay is what separates a wipe from a short read. It costs three seconds — the watcher
    /// looks once a second — and a real wipe lasts until the player reaches a Pokémon Centre, so
    /// waiting cannot miss one.
    /// </remarks>
    [Fact]
    public async Task A_wipe_is_charged_once_it_has_been_seen_three_times()
    {
        var (watcher, log) = Build();

        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0)));
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0)));

        var charged = await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));

        Assert.NotNull(charged);
        Assert.True(charged.Applied);
        Assert.Equal(100, charged.Points);

        var wipe = Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);
        Assert.Equal(-100, wipe.PointsDelta);
    }

    /// <summary>
    /// A short read that shows only fallen Pokémon, and then goes back to normal, costs nothing.
    /// </summary>
    /// <remarks>
    /// This is the one the change had to not break. Preferring the authoritative structure means a
    /// poll can read one slot instead of six; if that slot happens to be a Pokémon that is down,
    /// «nobody standing» is true and the party is perfectly fine.
    /// </remarks>
    [Fact]
    public async Task A_short_read_of_the_fallen_does_not_cost_a_hundred_points()
    {
        var (watcher, log) = Build();

        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0, 0, 0, 91)));
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0)));
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0)));
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0, 0, 0, 91)));

        Assert.Empty(log.Appended);
    }

    /// <summary>One wipe is charged once, however long the party stays down.</summary>
    [Fact]
    public async Task The_same_disaster_is_not_charged_twice()
    {
        var (watcher, log) = Build();

        for (var poll = 0; poll < 30; poll++)
        {
            await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));
        }

        Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);
    }

    /// <summary>
    /// Getting up and falling again is a second wipe, and the counter starts over.
    /// </summary>
    [Fact]
    public async Task Standing_up_and_falling_again_is_a_second_wipe()
    {
        var (watcher, log) = Build();

        for (var poll = 0; poll < 3; poll++)
        {
            await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));
        }

        await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 44));

        // Y otra vez tiene que volver a costar tres vueltas, no una.
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0)));
        Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0)));
        Assert.NotNull(await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0)));

        Assert.Equal(2, log.Appended.Count(e => e.Type == GameEventType.TeamWiped));
    }

    /// <summary>The run's Pokémon, alive or dead, that a test can change as it goes.</summary>
    private sealed class Roster : IPokemonRepository
    {
        public List<PokemonEntry> Entries { get; } = [];

        public Task SaveAsync(PokemonEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([.. Entries]);

        public Task<PokemonEntry?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<PokemonEntry?>(null);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Add(int slot, PokemonStatus status) => Entries.Add(new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = Run,
            Species = 25,
            SpeciesName = "Pikachu",
            Origin = PokemonOrigin.Capture,
            EncounterType = EncounterType.Wild,
            Status = status,
            Pid = (uint)(0xA000 + slot)
        });

        public void KillAll()
        {
            for (var i = 0; i < Entries.Count; i++) Entries[i] = Entries[i] with { Status = PokemonStatus.Dead };
        }
    }

    private static (GameWatcher Watcher, Events Log, Roster Roster) BuildWithRoster()
    {
        var events = new Events();
        var clock = new Clock();
        var roster = new Roster();
        var penalties = new PenaltyService(new Penalties(), events, clock, new NoRole());

        return (new GameWatcher(roster, events, clock, penalties), events, roster);
    }

    /// <summary>What a run looks like once a wipe has been recorded: the deaths first, the wipe after them.</summary>
    private static void AlreadyWiped(Events log, Roster roster, int members)
    {
        var at = new Clock().Now;

        for (var slot = 0; slot < members; slot++)
        {
            roster.Add(slot, PokemonStatus.Dead);
            log.Appended.Add(new GameEvent
            {
                Id = Guid.NewGuid(), RunId = Run, Timestamp = at.AddMinutes(-10), Type = GameEventType.PokemonDied,
                Source = EventSource.AutoDetect, Actor = "Grenin", Description = "cayó"
            });
        }

        log.Appended.Add(new GameEvent
        {
            Id = Guid.NewGuid(), RunId = Run, Timestamp = at.AddMinutes(-9), Type = GameEventType.TeamWiped,
            Source = EventSource.AutoDetect, Actor = "Grenin", Description = "equipo caído"
        });
    }

    /// <summary>
    /// Found playing on 2026-09-21: after a wipe the game takes the player to a Pokémon Centre and heals everybody,
    /// PermaLocke puts the fallen back down, and that was charged as a second wipe — −100 every time they were healed.
    /// </summary>
    [Fact]
    public async Task Healed_at_the_centre_and_put_back_down_is_not_a_second_wipe()
    {
        var (watcher, log, roster) = BuildWithRoster();
        for (var slot = 0; slot < 3; slot++) roster.Add(slot, PokemonStatus.Alive);

        await watcher.CheckWipeAsync(Run, "Grenin", Party(30, 30, 30));
        for (var poll = 0; poll < 3; poll++) await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));
        Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);

        // Las muertes quedan apuntadas, el Centro Pokémon los cura y PermaLocke los devuelve a cero.
        roster.KillAll();
        await watcher.CheckWipeAsync(Run, "Grenin", Party(50, 50, 50));
        for (var poll = 0; poll < 5; poll++) await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));

        Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);
    }

    /// <summary>Opening PermaLocke again after a wipe finds the same party down: it is the same wipe.</summary>
    [Fact]
    public async Task Opening_the_app_after_a_wipe_does_not_charge_it_again()
    {
        var (watcher, log, roster) = BuildWithRoster();
        AlreadyWiped(log, roster, members: 3);

        for (var poll = 0; poll < 5; poll++) await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));

        Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);
    }

    /// <summary>And a Pokémon that is alive in the run, standing and then falling, is a new wipe as it always was.</summary>
    [Fact]
    public async Task Someone_alive_falling_after_a_wipe_is_a_new_wipe()
    {
        var (watcher, log, roster) = BuildWithRoster();
        AlreadyWiped(log, roster, members: 3);
        roster.Add(3, PokemonStatus.Alive);

        await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0, 30));
        for (var poll = 0; poll < 3; poll++) await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0, 0));

        Assert.Equal(2, log.Appended.Count(e => e.Type == GameEventType.TeamWiped));
    }

    /// <summary>
    /// A wipe charged by mistake is taken back: its points come back and it stops counting towards the four, so the
    /// next real one is charged as the one it is.
    /// </summary>
    [Fact]
    public async Task A_revoked_wipe_pays_back_and_stops_counting()
    {
        var (watcher, log) = Build();
        var penalties = new PenaltyService(new Penalties(), log, new Clock(), new NoRole());

        for (var poll = 0; poll < 3; poll++) await watcher.CheckWipeAsync(Run, "Grenin", Party(0, 0, 0));
        var wipe = Assert.Single(log.Appended, e => e.Type == GameEventType.TeamWiped);

        Assert.Null(await penalties.RevokeWipeAsync(Run, wipe.Id, "Grenin", "segundo equipo caído al curar"));

        var revoked = Assert.Single(log.Appended, e => e.Type == GameEventType.WipeRevoked);
        Assert.Equal(100, revoked.PointsDelta);
        Assert.Equal(0, log.Appended.Sum(e => e.PointsDelta));
        Assert.Equal(0, await penalties.CountWipesAsync(Run));

        // Y no se devuelve dos veces.
        Assert.NotNull(await penalties.RevokeWipeAsync(Run, wipe.Id, "Grenin", "otra vez"));
        Assert.Single(log.Appended, e => e.Type == GameEventType.WipeRevoked);
    }

    /// <summary>Only a wipe of this run can be taken back.</summary>
    [Fact]
    public async Task Something_that_is_not_a_wipe_is_not_revoked()
    {
        var (_, log) = Build();
        var penalties = new PenaltyService(new Penalties(), log, new Clock(), new NoRole());

        Assert.NotNull(await penalties.RevokeWipeAsync(Run, Guid.NewGuid(), "Grenin", "no existe"));
        Assert.Empty(log.Appended);
    }

    /// <summary>Nothing is decided without a reading: no link and no party are «no se sabe».</summary>
    [Fact]
    public async Task Without_a_reading_nothing_is_decided()
    {
        var (watcher, log) = Build();
        var at = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        for (var poll = 0; poll < 5; poll++)
        {
            Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", GameSnapshot.Disconnected("no", at)));
            Assert.Null(await watcher.CheckWipeAsync(Run, "Grenin", new GameSnapshot(true, null, [], at)));
        }

        Assert.Empty(log.Appended);
    }
}
