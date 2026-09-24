using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// Guards what the first-encounter rule does to the bag and the run.
/// </summary>
/// <remarks>
/// The decision itself is tested in <see cref="EncounterPolicyTests"/>. These check the writes: every one leaves
/// its event, an unreadable bag is never written, and what counts as spent.
/// </remarks>
public sealed class BallControlServiceTests
{
    private const int PokeBall = 4;
    private const int UltraBall = 2;

    private static readonly FieldZone Route2 = new(7, 0, "ruta-2", "Ruta 2");

    /// <summary>A bag whose debts, like the real one's, belong to the run that took the balls (§147).</summary>
    private sealed class FakeBag : IItemWithholder
    {
        public Dictionary<int, int> Carrying { get; } = [];
        public Dictionary<(Guid Run, int Item), int> Withheld { get; } = [];
        public bool Unreadable { get; set; }

        public int Carried(int itemId) => Carrying.GetValueOrDefault(itemId);

        public IReadOnlyDictionary<int, int> CarriedAll(IReadOnlyCollection<int> itemIds) => Unreadable
            ? new Dictionary<int, int>()
            : itemIds.ToDictionary(id => id, Carried);

        public int Owed(Guid run, int itemId) => Withheld.GetValueOrDefault((run, itemId));

        public int Forget(Guid run, int itemId)
        {
            var owed = Owed(run, itemId);
            Withheld.Remove((run, itemId));
            return owed;
        }

        public bool Withhold(Guid run, int itemId)
        {
            var carried = Carried(itemId);

            if (carried <= 0)
            {
                return false;
            }

            Withheld[(run, itemId)] = Owed(run, itemId) + carried;
            Carrying[itemId] = 0;
            return true;
        }

        public bool GiveBack(Guid run, int itemId)
        {
            var owed = Owed(run, itemId);

            if (owed <= 0)
            {
                return false;
            }

            Carrying[itemId] = Carried(itemId) + owed;
            Withheld.Remove((run, itemId));
            return true;
        }
    }

    private static Run SampleRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Nuzlocke de prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260914",
        Seed = 20260914,
        RoleId = "player",
        PlayerName = "Grenin"
    };

    private static (BallControlService Service, FakeBag Bag, FakeEvents Events, FakePokemon Pokemon) Build(bool enabled = true)
    {
        var bag = new FakeBag();
        var events = new FakeEvents();
        var pokemon = new FakePokemon();
        var clock = new StepClock();

        var service = new BallControlService(bag, events, pokemon, new ZoneOutcomeService(events, clock), clock)
        {
            Enabled = enabled,
            BallItemIds = [PokeBall, UltraBall]
        };

        return (service, bag, events, pokemon);
    }

    /// <summary>
    /// Lo mismo, pero devolviendo el servicio de zonas que comparte reloj con él. Hace falta para todo lo que
    /// depende del ORDEN: con dos <see cref="StepClock"/> distintos las horas empiezan de cero cada una y una
    /// corrección puede quedar antes que lo que corrige, que es un fallo de la prueba y no del código.
    /// </summary>
    private static (BallControlService Service, ZoneOutcomeService Zones, FakeEvents Events) BuildWithZones()
    {
        var bag = new FakeBag();
        var events = new FakeEvents();
        var clock = new StepClock();
        var zones = new ZoneOutcomeService(events, clock);

        var service = new BallControlService(bag, events, new FakePokemon(), zones, clock)
        {
            Enabled = true,
            BallItemIds = [PokeBall, UltraBall]
        };

        return (service, zones, events);
    }

    private static EncounterDecision Withhold => new(BallAction.Withhold, false, "Ruta 2 ya gastó su encuentro.");

    private static EncounterDecision GiveBack => new(BallAction.GiveBack, false, "Ruta 3 conserva su encuentro.");

    [Fact]
    public async Task Withholding_takes_every_kind_of_ball_and_says_why()
    {
        var (service, bag, events, _) = Build();
        bag.Carrying[PokeBall] = 5;
        bag.Carrying[UltraBall] = 2;

        var run = SampleRun();
        var affected = await service.ApplyAsync(run, Withhold, Route2);

        Assert.Equal(2, affected);
        Assert.Equal(0, bag.Carried(PokeBall));
        Assert.Equal(5, bag.Owed(run.Id, PokeBall));

        var withheld = Assert.Single(events.All);
        Assert.Equal(GameEventType.BallsWithheld, withheld.Type);
        Assert.Equal(EventSource.AutoDetect, withheld.Source);
        Assert.Equal("ruta-2", withheld.LocationId);
        Assert.Equal("5", withheld.Data[PokeBall.ToString()]);
        Assert.Equal("7", withheld.Data["mapa"]);
        Assert.Contains("ya gastó", withheld.Description);
    }

    [Fact]
    public async Task A_bag_that_cannot_be_read_is_never_written()
    {
        var (service, bag, events, _) = Build();
        bag.Carrying[PokeBall] = 5;
        bag.Unreadable = true;

        var affected = await service.ApplyAsync(SampleRun(), Withhold, Route2);

        Assert.Equal(0, affected);
        Assert.Equal(5, bag.Carried(PokeBall));
        Assert.Empty(events.All);
    }

    [Fact]
    public async Task Giving_back_adds_to_what_was_picked_up_meanwhile()
    {
        var (service, bag, events, _) = Build();
        var run = SampleRun();
        bag.Carrying[PokeBall] = 5;
        await service.ApplyAsync(run, Withhold, Route2);

        // Cinco más compradas en la tienda con las otras retiradas.
        bag.Carrying[PokeBall] = 5;
        await service.ApplyAsync(run, Withhold, Route2);
        Assert.Equal(10, bag.Owed(run.Id, PokeBall));

        bag.Carrying[PokeBall] = 3;
        await service.ApplyAsync(run, GiveBack, null);

        Assert.Equal(13, bag.Carried(PokeBall));
        Assert.Equal(GameEventType.BallsReturned, events.All[^1].Type);
    }

    /// <summary>
    /// Routes count from the first Poké Ball on, and keep counting after every ball is thrown (§149).
    /// </summary>
    [Fact]
    public async Task Routes_count_from_the_first_ball_and_keep_counting()
    {
        var (service, bag, events, _) = Build();
        var run = SampleRun();

        Assert.False(await service.HasHadBallsAsync(run));
        Assert.Empty(events.All);

        bag.Carrying[PokeBall] = 5;
        Assert.True(await service.HasHadBallsAsync(run));
        Assert.Equal(GameEventType.FirstPokeBallSeen, Assert.Single(events.All).Type);

        bag.Carrying[PokeBall] = 0;
        Assert.True(await service.HasHadBallsAsync(run));
        Assert.Single(events.All);
    }

    /// <summary>
    /// What an old run took is not given to a new one. After «empezar de cero» a brand new game on Route 1
    /// received the previous game's twelve kinds of ball, a Master Ball among them (§147).
    /// </summary>
    [Fact]
    public async Task Balls_withheld_by_another_run_are_not_given_to_this_one()
    {
        var (service, bag, events, _) = Build();
        var old = SampleRun();
        bag.Carrying[PokeBall] = 7;
        await service.ApplyAsync(old, Withhold, Route2);

        var affected = await service.ApplyAsync(SampleRun(), GiveBack, null);

        Assert.Equal(0, affected);
        Assert.Equal(0, bag.Carried(PokeBall));
        Assert.Equal(GameEventType.BallsWithheld, Assert.Single(events.All).Type);
    }

    [Fact]
    public async Task Nothing_to_give_back_writes_nothing()
    {
        var (service, _, events, _) = Build();

        var affected = await service.ApplyAsync(SampleRun(), GiveBack, Route2);

        Assert.Equal(0, affected);
        Assert.Empty(events.All);
    }

    [Fact]
    public async Task A_rule_without_balls_or_turned_off_does_nothing()
    {
        var (service, bag, events, _) = Build(enabled: false);
        bag.Carrying[PokeBall] = 5;

        Assert.False(service.IsActive);
        Assert.Equal(0, await service.ApplyAsync(SampleRun(), Withhold, Route2));
        Assert.Equal(5, bag.Carried(PokeBall));
        Assert.Empty(events.All);
    }

    [Fact]
    public async Task A_spent_zone_is_recorded_as_detected_not_claimed()
    {
        var (service, _, events, _) = Build();
        var run = SampleRun();

        await service.SpendZoneAsync(run, Route2, 506, "Lillipup", "Primer encuentro en Ruta 2.");

        var spent = Assert.Single(events.All);
        Assert.Equal(GameEventType.ZoneEncounterSpent, spent.Type);
        Assert.Equal(EventSource.AutoDetect, spent.Source);
        Assert.Equal("ruta-2", spent.LocationId);
        Assert.Equal("506", spent.Data["especie"]);
        Assert.Contains(await service.SpentZonesAsync(run.Id), zone => zone == "ruta-2");
    }

    [Fact]
    public async Task Spent_zones_come_from_captures_detected_battles_and_the_map()
    {
        var (service, _, events, pokemon) = Build();
        var run = SampleRun();

        await pokemon.SaveAsync(new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = 10,
            SpeciesName = "Caterpie",
            Origin = PokemonOrigin.Capture,
            EncounterType = EncounterType.Wild,
            LocationId = "ruta-1",
            ConsumedZoneEncounter = true,
            ObtainedAt = DateTimeOffset.UtcNow
        });

        await service.SpendZoneAsync(run, Route2, 506, "Lillipup", "Primer encuentro en Ruta 2.");
        await new ZoneOutcomeService(events, new StepClock())
            .SetAsync(run.Id, "ruta-3", "Ruta 3", ZoneOutcome.Fled, run.PlayerName, EventSource.AutoDetect);

        var spent = await service.SpentZonesAsync(run.Id);

        Assert.Equal(["ruta-1", "ruta-2", "ruta-3"], spent.Order(StringComparer.Ordinal));
    }

    /// <summary>An old «libre» clicked on the map, from before §118, cannot free a route PermaLocke saw spent.</summary>
    [Fact]
    public async Task An_old_manual_clear_on_the_map_does_not_undo_a_detected_battle()
    {
        var (service, _, events, _) = Build();
        var run = SampleRun();

        await service.SpendZoneAsync(run, Route2, 506, "Lillipup", "Primer encuentro en Ruta 2.");
        await new ZoneOutcomeService(events, new StepClock())
            .SetAsync(run.Id, "ruta-2", "Ruta 2", ZoneOutcome.Free, run.PlayerName, EventSource.Player);

        Assert.Contains("ruta-2", await service.SpentZonesAsync(run.Id));
    }

    /// <summary>
    /// Liberar una zona sí deshace el gasto, al revés que el «libre» de arriba: es la corrección explícita del
    /// §67, y es lo que faltaba el 2026-09-21 cuando PermaLocke gastó la ruta equivocada.
    /// </summary>
    [Fact]
    public async Task Freeing_a_zone_undoes_a_detected_spend()
    {
        var (service, zones, _) = BuildWithZones();
        var run = SampleRun();

        await service.SpendZoneAsync(run, Route2, 506, "Lillipup", "Primer encuentro en Ruta 2.");
        await service.RecordOutcomeAsync(run, Route2, ZoneOutcome.Fled, "Lillipup", "huyó");
        Assert.Contains("ruta-2", await service.SpentZonesAsync(run.Id));

        await zones.ClearAsync(run.Id, "ruta-2", "Ruta 2", run.PlayerName, "se leyó la ruta de al lado");

        Assert.DoesNotContain("ruta-2", await service.SpentZonesAsync(run.Id));
    }

    /// <summary>El orden manda: lo que pase después de la corrección vuelve a gastar la ruta.</summary>
    [Fact]
    public async Task A_zone_freed_and_then_met_again_is_spent_again()
    {
        var (service, zones, _) = BuildWithZones();
        var run = SampleRun();

        await service.SpendZoneAsync(run, Route2, 506, "Lillipup", "Primer encuentro en Ruta 2.");
        await zones.ClearAsync(run.Id, "ruta-2", "Ruta 2", run.PlayerName, "se leyó mal");
        await service.SpendZoneAsync(run, Route2, 19, "Rattata", "Primer encuentro en Ruta 2.");

        Assert.Contains("ruta-2", await service.SpentZonesAsync(run.Id));
    }

    [Fact]
    public async Task How_the_battle_ended_goes_on_the_map_as_detected()
    {
        var (service, _, events, _) = Build();
        var run = SampleRun();

        await service.RecordOutcomeAsync(run, Route2, ZoneOutcome.Caught, "Lillipup", "El juego contó una captura en este combate.");

        var mark = Assert.Single(events.All);
        Assert.Equal(GameEventType.ZoneOutcomeSet, mark.Type);
        Assert.Equal(EventSource.AutoDetect, mark.Source);
        Assert.Equal("Lillipup", mark.Data["especie"]);
        Assert.Contains("captura", mark.Reason);
        Assert.Equal(ZoneOutcome.Caught, (await new ZoneOutcomeService(events, new StepClock()).GetAsync(run.Id))["ruta-2"]);
    }
    [Fact]
    public async Task First_ball_is_announced_after_recording_and_only_once()
    {
        var (service, bag, events, _) = Build();
        var run = SampleRun();
        var notices = 0;
        service.FirstBallDetected += (_, detected) =>
        {
            Assert.Equal(run.Id, detected.Id);
            Assert.Equal(GameEventType.FirstPokeBallSeen, Assert.Single(events.All).Type);
            notices++;
        };
        bag.Carrying[PokeBall] = 5;
        Assert.True(await service.HasHadBallsAsync(run));
        bag.Carrying[PokeBall] = 0;
        Assert.True(await service.HasHadBallsAsync(run));
        Assert.Equal(1, notices);
    }

    [Fact]
    public async Task Reopening_a_run_does_not_replay_its_first_ball_notice()
    {
        var (service, bag, events, pokemon) = Build();
        var run = SampleRun();
        bag.Carrying[PokeBall] = 5;
        await service.HasHadBallsAsync(run);
        var clock = new StepClock();
        var reopened = new BallControlService(bag, events, pokemon, new ZoneOutcomeService(events, clock), clock)
        {
            BallItemIds = [PokeBall, UltraBall]
        };
        var notices = 0;
        reopened.FirstBallDetected += (_, _) => notices++;
        Assert.True(await reopened.HasHadBallsAsync(run));
        Assert.Equal(0, notices);
        Assert.Single(events.All);
    }

    [Fact]
    public async Task Empty_or_unreadable_bag_does_not_announce_first_balls()
    {
        var (service, bag, events, _) = Build();
        var run = SampleRun();
        var notices = 0;
        service.FirstBallDetected += (_, _) => notices++;
        Assert.False(await service.HasHadBallsAsync(run));
        bag.Unreadable = true;
        await service.HasHadBallsAsync(run);
        Assert.Empty(events.All);
        Assert.Equal(0, notices);
    }

    private sealed class DelayedEvents : IEventStore
    {
        public FakeEvents Inner { get; } = new();
        public Func<Task> BeforeAppend { get; set; } = () => Task.CompletedTask;
        public async Task<GameEvent> AppendAsync(GameEvent entry, CancellationToken ct = default)
        {
            await BeforeAppend();
            return await Inner.AppendAsync(entry, ct);
        }
        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid id, CancellationToken ct = default) => Inner.GetAllAsync(id, ct);
        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid id, int count, CancellationToken ct = default) => Inner.GetLatestAsync(id, count, ct);
        public Task<int> DeleteRunAsync(Guid id, CancellationToken ct = default) => Inner.DeleteRunAsync(id, ct);
        public Task<IntegrityReport> VerifyChainAsync(Guid id, CancellationToken ct = default) => Inner.VerifyChainAsync(id, ct);
    }

    [Fact]
    public async Task Simultaneous_monitors_save_and_announce_the_first_ball_once()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new DelayedEvents { BeforeAppend = () => gate.Task };
        var bag = new FakeBag();
        bag.Carrying[PokeBall] = 5;
        var clock = new StepClock();
        var service = new BallControlService(bag, events, new FakePokemon(), new ZoneOutcomeService(events, clock), clock)
        {
            BallItemIds = [PokeBall]
        };
        var run = SampleRun();
        var notices = 0;
        service.FirstBallDetected += (_, _) => Interlocked.Increment(ref notices);
        var first = service.HasHadBallsAsync(run);
        var second = service.HasHadBallsAsync(run);
        Assert.Empty(events.Inner.All);
        Assert.Equal(0, notices);
        gate.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, Assert.True);
        Assert.Single(events.Inner.All);
        Assert.Equal(1, notices);
    }

    [Fact]
    public async Task Failed_history_write_does_not_announce_and_can_be_retried()
    {
        var events = new DelayedEvents { BeforeAppend = () => throw new IOException("Disk unavailable") };
        var bag = new FakeBag();
        bag.Carrying[PokeBall] = 5;
        var clock = new StepClock();
        var service = new BallControlService(bag, events, new FakePokemon(), new ZoneOutcomeService(events, clock), clock)
        {
            BallItemIds = [PokeBall]
        };
        var run = SampleRun();
        var notices = 0;
        service.FirstBallDetected += (_, _) => notices++;
        await Assert.ThrowsAsync<IOException>(() => service.HasHadBallsAsync(run));
        Assert.Equal(0, notices);
        events.BeforeAppend = () => Task.CompletedTask;
        Assert.True(await service.HasHadBallsAsync(run));
        Assert.Equal(1, notices);
    }
}
