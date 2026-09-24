using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Training EVs. What matters here is what never reaches the save, and what the log ends up
/// saying about what did.
/// </summary>
public sealed class EvTrainingServiceTests
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
        public DateTimeOffset Now => new(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Stands in for the save. Records what it was asked to write, if anything.</summary>
    private sealed class Trainer(bool works = true, string problem = "") : IEvTrainer
    {
        public List<EvChange> Applied { get; } = [];

        public bool CanTrainNow(out string reason)
        {
            reason = problem;
            return works;
        }

        public Task<DeliveryResult> ApplyAsync(EvChange change, CancellationToken ct = default)
        {
            if (!works)
            {
                return Task.FromResult(new DeliveryResult(DeliveryOutcome.GameRunning, problem));
            }

            Applied.Add(change);
            return Task.FromResult(new DeliveryResult(DeliveryOutcome.Delivered, "hecho"));
        }
    }

    private static BoxedPokemon Pokemon(params int[] evs) => new(
        Box: 2, Slot: 5, Species: 25, Form: 0, SpeciesName: "Pikachu", Nickname: string.Empty,
        Level: 50, IsShiny: false, IsEgg: false, GenderMark: string.Empty, NatureName: "Firme",
        AbilityName: "Estática", HeldItemName: string.Empty, BallName: "Poké Ball",
        TrainerName: "Grenin", MetLocationName: "Ruta 1", MetLevel: 5, Moves: [],
        Stats: [100, 100, 100, 100, 100, 100], Ivs: [31, 31, 31, 31, 31, 31], Evs: evs,
        Friendship: 70, Pid: 0xABCD1234);

    private static (EvTrainingService Service, Events Log, Run Run) Build(IEvTrainer trainer)
    {
        var log = new Events();

        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = "Prueba",
            Game = GameVersion.UltraMoon,
            SeedLabel = "20260822",
            Seed = 20260822,
            RoleId = "player",
            PlayerName = "Grenin"
        };

        return (new EvTrainingService(trainer, log, new FixedClock()), log, run);
    }

    /// <summary>
    /// Over 510 is a reparto still being worked on, not something to write. The editor lets it
    /// happen so the player can move points in any order; this is where it stops.
    /// </summary>
    [Fact]
    public async Task A_spread_over_510_never_reaches_the_save()
    {
        var trainer = new Trainer();
        var (service, log, run) = Build(trainer);

        var result = await service.TrainAsync(run, Pokemon(0, 0, 0, 0, 0, 0),
            EvSpread.Of([252, 252, 252, 0, 0, 0]));

        Assert.False(result.Delivered);
        Assert.Contains("246", result.Message);
        Assert.Empty(trainer.Applied);
        Assert.Empty(log.Appended);
    }

    /// <summary>
    /// A Huevo Malo is never written back (§97): through PKHeX it would come out with a valid
    /// checksum and whatever species its broken bytes say, which the game may hang on.
    /// </summary>
    [Fact]
    public async Task A_damaged_entry_is_never_written()
    {
        var trainer = new Trainer();
        var (service, log, run) = Build(trainer);

        var result = await service.TrainAsync(run, Pokemon(0, 0, 0, 0, 0, 0) with { IsIntact = false },
            EvSpread.Of([4, 0, 0, 0, 0, 0]));

        Assert.False(result.Delivered);
        Assert.Contains("dañado", result.Message);
        Assert.Empty(trainer.Applied);
        Assert.Empty(log.Appended);
    }

    [Fact]
    public async Task Exactly_510_is_written()
    {
        var trainer = new Trainer();
        var (service, log, run) = Build(trainer);

        var result = await service.TrainAsync(run, Pokemon(0, 0, 0, 0, 0, 0),
            EvSpread.Of([252, 252, 6, 0, 0, 0]));

        Assert.True(result.Delivered);
        var written = Assert.Single(trainer.Applied);
        Assert.Equal([252, 252, 6, 0, 0, 0], written.Evs);
        Assert.Single(log.Appended);
    }

    /// <summary>
    /// A spread equal to the one already stored is refused before anything is opened: writing it
    /// would rewrite the whole save and log a change that did not happen.
    /// </summary>
    [Fact]
    public async Task Saving_the_same_spread_touches_nothing()
    {
        var trainer = new Trainer();
        var (service, log, run) = Build(trainer);

        var result = await service.TrainAsync(run, Pokemon(4, 8, 0, 0, 0, 0),
            EvSpread.Of([4, 8, 0, 0, 0, 0]));

        Assert.False(result.Delivered);
        Assert.Empty(trainer.Applied);
        Assert.Empty(log.Appended);
    }

    /// <summary>The write happens first: a refusal from the save leaves no event behind.</summary>
    [Fact]
    public async Task A_write_that_fails_is_not_recorded_as_having_happened()
    {
        var trainer = new Trainer(works: false, problem: "el juego está abierto");
        var (service, log, run) = Build(trainer);

        var result = await service.TrainAsync(run, Pokemon(0, 0, 0, 0, 0, 0),
            EvSpread.Of([252, 0, 0, 0, 0, 252]));

        Assert.False(result.Delivered);
        Assert.Empty(log.Appended);
    }

    /// <summary>The log has to say what moved, not just dump six numbers.</summary>
    [Fact]
    public async Task The_event_says_which_stats_moved_and_where_the_pokemon_was()
    {
        var trainer = new Trainer();
        var (service, log, run) = Build(trainer);

        await service.TrainAsync(run, Pokemon(4, 0, 0, 0, 0, 0), EvSpread.Of([252, 0, 0, 0, 0, 252]));

        var recorded = Assert.Single(log.Appended);
        Assert.Equal(GameEventType.EvsTrained, recorded.Type);
        Assert.Equal(EventSource.Player, recorded.Source);
        Assert.Equal(0, recorded.PointsDelta);

        Assert.Contains("PS 4→252", recorded.Description);
        Assert.Contains("Velocidad 0→252", recorded.Description);
        Assert.DoesNotContain("Defensa", recorded.Description);

        Assert.Equal("4/0/0/0/0/0", recorded.Data["antes"]);
        Assert.Equal("252/0/0/0/0/252", recorded.Data["despues"]);
        Assert.Equal("la caja 3, hueco 6", recorded.Data["donde"]);
    }

    /// <summary>Training is an edit, not a purchase: it never moves the balance.</summary>
    [Fact]
    public async Task Training_costs_nothing()
    {
        var (service, log, run) = Build(new Trainer());

        await service.TrainAsync(run, Pokemon(0, 0, 0, 0, 0, 0), EvSpread.Of([252, 0, 0, 0, 0, 0]));

        Assert.All(log.Appended, recorded => Assert.Equal(0, recorded.PointsDelta));
    }
}
