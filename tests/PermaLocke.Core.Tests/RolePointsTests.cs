using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The role applied where it actually matters: to the points a run gains and loses. Checked
/// through the real services, because a multiplier that is right in isolation and never reaches
/// the event log is worth nothing.
/// </summary>
public sealed class RolePointsTests
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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);
    }

    private sealed class Penalties(PenaltyRules rules) : IPenaltyCatalog
    {
        public PenaltyRules Rules => rules;
    }

    private static readonly Guid Run = Guid.NewGuid();

    private static PokemonEntry Victim() => new()
    {
        Id = Guid.NewGuid(),
        RunId = Run,
        Species = 652,
        SpeciesName = "Chesnaught",
        Level = 40,
        Origin = PokemonOrigin.Capture,
        EncounterType = EncounterType.Wild,
        ObtainedAt = DateTimeOffset.UnixEpoch
    };


    private static PenaltyService With(Role role, Events log) =>
        new(new Penalties(new PenaltyRules(25, 100, 4)), log, new FixedClock(), new FixedRole(role));

    [Fact]
    public async Task A_death_costs_the_normal_role_the_full_price()
    {
        var log = new Events();
        var result = await With(FixedRole.Normal, log).ChargeDeathAsync(Run, "Grenin", Victim());

        Assert.Equal(25, result.Points);
        Assert.Equal(-25, log.Appended.Single().PointsDelta);
    }

    /// <summary>The cagoneta never loses, so the death is free — but still recorded.</summary>
    [Fact]
    public async Task A_death_costs_the_cagoneta_nothing()
    {
        var log = new Events();
        var result = await With(FixedRole.Cagoneta, log).ChargeDeathAsync(Run, "Grenin", Victim());

        Assert.Equal(0, result.Points);
        Assert.False(result.Applied);
    }

    [Fact]
    public async Task A_death_costs_the_experto_double()
    {
        var log = new Events();
        var result = await With(FixedRole.Experto, log).ChargeDeathAsync(Run, "Grenin", Victim());

        Assert.Equal(50, result.Points);
        Assert.Equal(-50, log.Appended.Single().PointsDelta);
    }

    /// <summary>
    /// The event has to carry the working: base, role and multiplier. Otherwise a player looking
    /// at "−50" in the history has no way of telling a doubled penalty from a bug.
    /// </summary>
    [Fact]
    public async Task The_event_shows_how_the_number_was_reached()
    {
        var log = new Events();
        await With(FixedRole.Experto, log).ChargeDeathAsync(Run, "Grenin", Victim());

        var data = log.Appended.Single().Data;

        Assert.Equal("25", data["base"]);
        Assert.Equal("experto", data["rol"]);
        Assert.Equal("2", data["multiplicador"]);
    }

    [Fact]
    public async Task A_wipe_doubles_for_the_experto_and_is_free_for_the_cagoneta()
    {
        var hard = new Events();
        var soft = new Events();

        var expert = await With(FixedRole.Experto, hard).ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);
        var easy = await With(FixedRole.Cagoneta, soft).ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);

        Assert.Equal(200, expert.Points);
        Assert.Equal(0, easy.Points);

        // Aunque no cueste nada, el equipo cayó: el historial tiene que decirlo.
        Assert.Single(soft.Appended, e => e.Type == GameEventType.TeamWiped);
    }

    /// <summary>An unresolved role charges the base rate and writes down that it could not tell.</summary>
    [Fact]
    public async Task An_unknown_role_charges_the_base_and_admits_it()
    {
        var log = new Events();
        var service = new PenaltyService(new Penalties(new PenaltyRules(25, 100, 4)), log,
            new FixedClock(), FixedRole.None);

        var result = await service.ChargeDeathAsync(Run, "Grenin", Victim());

        Assert.Equal(25, result.Points);
        Assert.Equal("desconocido", log.Appended.Single().Data["rol"]);
    }
}
