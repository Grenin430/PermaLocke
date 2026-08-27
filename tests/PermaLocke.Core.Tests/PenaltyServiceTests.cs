using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Guards what losing costs: 25 por muerte, 100 por equipo caído hasta cuatro veces, y un saldo
/// que puede quedarse en negativo porque una penalización no es una compra.
/// </summary>
public sealed class PenaltyServiceTests
{
    private sealed class Catalog(PenaltyRules rules) : IPenaltyCatalog
    {
        public PenaltyRules Rules => rules;
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

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>(Appended);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    }

    private static readonly Guid Run = Guid.NewGuid();

    private static (PenaltyService Service, Events Log) Build(int death = 25, int wipe = 100, int max = 4,
        Role? role = null)
    {
        var log = new Events();
        return (new PenaltyService(new Catalog(new PenaltyRules(death, wipe, max)), log, new FixedClock(),
            new FixedRole(role ?? FixedRole.Normal)), log);
    }

    private static PokemonEntry Victim(string name = "Chesnaught") => new()
    {
        Id = Guid.NewGuid(),
        RunId = Run,
        Species = 652,
        SpeciesName = name,
        Level = 30,
        Origin = PokemonOrigin.Capture,
        EncounterType = EncounterType.Wild,
        ObtainedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public async Task A_death_costs_twenty_five()
    {
        var (service, log) = Build();

        var result = await service.ChargeDeathAsync(Run, "Grenin", Victim());

        Assert.True(result.Applied);
        Assert.Equal(25, result.Points);
        Assert.Equal(-25, result.NewBalance);

        var recorded = Assert.Single(log.Appended);
        Assert.Equal(GameEventType.PointsPenalty, recorded.Type);
        Assert.Equal(-25, recorded.PointsDelta);
        Assert.Equal("muerte", recorded.Data["motivo"]);
    }

    /// <summary>
    /// The whole point of a penalty: it is not a purchase, so it applies with an empty wallet and
    /// leaves the balance below zero.
    /// </summary>
    [Fact]
    public async Task Points_go_negative_because_a_penalty_is_not_a_purchase()
    {
        var (service, _) = Build();

        await service.ChargeDeathAsync(Run, "Grenin", Victim());
        await service.ChargeDeathAsync(Run, "Grenin", Victim("Talonflame"));
        var third = await service.ChargeDeathAsync(Run, "Grenin", Victim("Lycanroc"));

        Assert.Equal(-75, third.NewBalance);
    }

    [Fact]
    public async Task A_wipe_costs_a_hundred_on_top_of_the_deaths()
    {
        var (service, log) = Build();

        await service.ChargeDeathAsync(Run, "Grenin", Victim());
        var wipe = await service.ChargeWipeAsync(Run, "Grenin", ["Chesnaught", "Talonflame"]);

        Assert.Equal(100, wipe.Points);
        Assert.False(wipe.Capped);

        // 25 de la muerte y 100 del equipo: se suman, no se sustituyen.
        Assert.Equal(-125, wipe.NewBalance);
        Assert.Equal(GameEventType.TeamWiped, log.Appended[^1].Type);
    }

    /// <summary>Four wipes cost 400, and the fifth costs nothing.</summary>
    [Fact]
    public async Task Wipes_stop_costing_after_the_fourth()
    {
        var (service, _) = Build();

        for (var wipe = 1; wipe <= 4; wipe++)
        {
            var charged = await service.ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);
            Assert.False(charged.Capped);
            Assert.Equal(100, charged.Points);
            Assert.Equal(-100 * wipe, charged.NewBalance);
        }

        var fifth = await service.ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);

        Assert.True(fifth.Capped);
        Assert.Equal(0, fifth.Points);
        Assert.Equal(-400, fifth.NewBalance);
    }

    /// <summary>
    /// A wipe past the cap is still written down. It happened, so the history has to say so; it
    /// simply stops costing.
    /// </summary>
    [Fact]
    public async Task A_capped_wipe_is_still_recorded()
    {
        var (service, log) = Build(max: 1);

        await service.ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);
        await service.ChargeWipeAsync(Run, "Grenin", ["Chesnaught"]);

        Assert.Equal(2, log.Appended.Count(e => e.Type == GameEventType.TeamWiped));
        Assert.Equal(0, log.Appended[^1].PointsDelta);
        Assert.Equal(2, await service.CountWipesAsync(Run));
    }

    /// <summary>The number the rules quote, worked out from the parts rather than written twice.</summary>
    [Fact]
    public void The_wipes_cannot_cost_more_than_four_hundred() =>
        Assert.Equal(400, new PenaltyRules(25, 100, 4).MaxWipeCost);
}
