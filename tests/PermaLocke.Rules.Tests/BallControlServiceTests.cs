using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// Guards the rule that takes the player's Poké Balls away.
/// </summary>
/// <remarks>
/// Most of these check that it does nothing. That is the point: emptying someone's bag for a
/// zone they are not standing in is worse than not enforcing the rule, so every case where
/// PermaLocke is not certain has to end in no write at all.
/// </remarks>
public sealed class BallControlServiceTests
{
    private const int PokeBall = 4;
    private const int UltraBall = 2;

    private sealed class FakeZones(int? area) : IZoneProvider
    {
        public int? Area { get; set; } = area;

        public int? CurrentArea() => Area;
    }

    private sealed class FakeBag : IItemWithholder
    {
        public Dictionary<int, int> Carrying { get; } = [];
        public Dictionary<int, int> Withheld { get; } = [];
        public List<string> Writes { get; } = [];

        public int Carried(int itemId) => Carrying.GetValueOrDefault(itemId);

        public int Owed(int itemId) => Withheld.GetValueOrDefault(itemId);

        public bool Withhold(int itemId)
        {
            var carried = Carried(itemId);

            if (carried <= 0)
            {
                return false;
            }

            Withheld[itemId] = carried;
            Carrying[itemId] = 0;
            Writes.Add($"retira {itemId} x{carried}");
            return true;
        }

        public bool GiveBack(int itemId)
        {
            var owed = Owed(itemId);

            if (owed <= 0)
            {
                return false;
            }

            Carrying[itemId] = owed;
            Withheld.Remove(itemId);
            Writes.Add($"devuelve {itemId} x{owed}");
            return true;
        }
    }

    private sealed class RecordingEvents : IEventStore
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

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Area 10 is Route 1 and area 1 is Pueblo Lilii, both measured in game.</summary>
    private static ZoneTable Table() => new(
    [
        new ZoneArea(10, ["Ruta 1"], true),
        new ZoneArea(1, ["Pueblo Lilii"], false),
        new ZoneArea(0, ["Ruta 1", "Mar de Melemele"], true)
    ]);

    private static Run SampleRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Nuzlocke de prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260819",
        Seed = 20260819,
        RoleId = "player",
        PlayerName = "Grenin"
    };

    private static (BallControlService Service, FakeBag Bag, RecordingEvents Events) Build(
        FakeZones zones, bool enabled = true)
    {
        var bag = new FakeBag();
        var events = new RecordingEvents();

        var service = new BallControlService(zones, bag, Table(), events, new FixedClock())
        {
            Enabled = enabled,
            BallItemIds = [PokeBall, UltraBall]
        };

        return (service, bag, events);
    }

    [Fact]
    public async Task A_spent_zone_takes_every_kind_of_ball_away()
    {
        var (service, bag, events) = Build(new FakeZones(10));
        bag.Carrying[PokeBall] = 5;
        bag.Carrying[UltraBall] = 2;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.Withheld, result.Outcome);
        Assert.Equal("Ruta 1", result.LocationName);

        // Dos clases de bola retiradas: ni una mas. El contador conto tres una vez, porque el
        // evento anadia sus propias claves al mismo diccionario.
        Assert.Equal(2, result.ItemsAffected);
        Assert.Equal(0, bag.Carried(PokeBall));
        Assert.Equal(0, bag.Carried(UltraBall));
        Assert.Equal(5, bag.Owed(PokeBall));

        var recorded = Assert.Single(events.Appended);
        Assert.Equal(GameEventType.BallsWithheld, recorded.Type);
        Assert.Equal("10", recorded.Data["area"]);
    }

    [Fact]
    public async Task Leaving_for_a_free_zone_gives_back_exactly_what_was_taken()
    {
        var zones = new FakeZones(10);
        var (service, bag, events) = Build(zones);
        bag.Carrying[PokeBall] = 7;

        await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });
        zones.Area = 1;
        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.Returned, result.Outcome);
        Assert.Equal(7, bag.Carried(PokeBall));
        Assert.Equal(0, bag.Owed(PokeBall));
        Assert.Equal(GameEventType.BallsReturned, events.Appended[^1].Type);
    }

    [Fact]
    public async Task A_zone_that_still_has_its_encounter_is_left_alone()
    {
        var (service, bag, events) = Build(new FakeZones(10));
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string>());

        Assert.Equal(BallControlOutcome.ZoneAvailable, result.Outcome);
        Assert.Empty(bag.Writes);
        Assert.Empty(events.Appended);
    }

    /// <summary>Standing still in a spent zone must not rewrite the bag on every tick.</summary>
    [Fact]
    public async Task Staying_in_a_spent_zone_writes_only_once()
    {
        var (service, bag, events) = Build(new FakeZones(10));
        bag.Carrying[PokeBall] = 5;
        var spent = new HashSet<string> { "ruta-1" };

        await service.ApplyAsync(SampleRun(), spent);
        await service.ApplyAsync(SampleRun(), spent);
        await service.ApplyAsync(SampleRun(), spent);

        Assert.Single(bag.Writes);
        Assert.Single(events.Appended);
    }

    /// <summary>
    /// The whole reason the zone reader is allowed to answer "I do not know": when it does,
    /// nothing may be written.
    /// </summary>
    [Fact]
    public async Task An_unknown_zone_touches_nothing()
    {
        var (service, bag, events) = Build(new FakeZones(null));
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.ZoneUnknown, result.Outcome);
        Assert.Empty(bag.Writes);
        Assert.Empty(events.Appended);
    }

    /// <summary>
    /// Area 0 is Route 1 and the Melemele Sea at once. Route 1 being spent says nothing about
    /// the sea, so the rule must not act on it.
    /// </summary>
    [Fact]
    public async Task An_area_covering_two_locations_touches_nothing()
    {
        var (service, bag, events) = Build(new FakeZones(0));
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.ZoneAmbiguous, result.Outcome);
        Assert.Empty(bag.Writes);
        Assert.Empty(events.Appended);
    }

    [Fact]
    public async Task An_area_the_table_does_not_know_touches_nothing()
    {
        var (service, bag, _) = Build(new FakeZones(333));
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.ZoneUnknown, result.Outcome);
        Assert.Empty(bag.Writes);
    }

    [Fact]
    public async Task The_rule_turned_off_touches_nothing()
    {
        var (service, bag, _) = Build(new FakeZones(10), enabled: false);
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.Disabled, result.Outcome);
        Assert.Empty(bag.Writes);
    }

    /// <summary>No list of balls means the rule disables itself instead of guessing.</summary>
    [Fact]
    public async Task Without_a_list_of_balls_nothing_is_confiscated()
    {
        var bag = new FakeBag();
        bag.Carrying[PokeBall] = 5;

        var service = new BallControlService(new FakeZones(10), bag, Table(), new RecordingEvents(),
            new FixedClock()) { Enabled = true };

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.Disabled, result.Outcome);
        Assert.Empty(bag.Writes);
    }

    [Fact]
    public void The_table_refuses_to_name_an_ambiguous_area()
    {
        var table = Table();

        Assert.Equal("Ruta 1", table.LocationNameFor(10));
        Assert.Null(table.LocationNameFor(0));
        Assert.True(table.IsAmbiguous(0));
        Assert.Null(table.LocationNameFor(999));
        Assert.False(table.IsAmbiguous(999));
    }

    /// <summary>
    /// The bridge between both numbering systems: the name the table gives has to normalise to
    /// the same id the run stores for a capture made there.
    /// </summary>
    [Fact]
    public void The_table_name_normalises_to_the_id_the_run_uses()
    {
        Assert.Equal(EncounterService.NormaliseLocationId("Ruta 1"),
            EncounterService.NormaliseLocationId(Table().LocationNameFor(10)!));
    }

    /// <summary>
    /// The hole a real run found: leaving Route 1 crosses area 0, which covers Route 1 and the
    /// Melemele Sea at once. The first version did nothing there, so the player was left with
    /// no balls and no explanation. Doubt has to resolve in their favour.
    /// </summary>
    [Fact]
    public async Task Walking_into_an_ambiguous_area_gives_the_balls_back()
    {
        var zones = new FakeZones(10);
        var (service, bag, events) = Build(zones);
        bag.Carrying[PokeBall] = 5;

        await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });
        Assert.Equal(0, bag.Carried(PokeBall));

        zones.Area = 0;
        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.Returned, result.Outcome);
        Assert.Equal(5, bag.Carried(PokeBall));
        Assert.Equal(GameEventType.BallsReturned, events.Appended[^1].Type);
    }

    /// <summary>
    /// An unreadable zone is often just the instant of a map change, so one tick of it must not
    /// hand the balls back and forth. Two in a row does.
    /// </summary>
    [Fact]
    public async Task One_unreadable_tick_holds_but_two_release()
    {
        var zones = new FakeZones(10);
        var (service, bag, _) = Build(zones);
        bag.Carrying[PokeBall] = 5;

        await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });
        zones.Area = null;

        var first = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });
        Assert.Equal(BallControlOutcome.ZoneUnknown, first.Outcome);
        Assert.Equal(0, bag.Carried(PokeBall));

        var second = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });
        Assert.Equal(BallControlOutcome.Returned, second.Outcome);
        Assert.Equal(5, bag.Carried(PokeBall));
    }

    /// <summary>With nothing withheld, an uncertain zone still writes nothing at all.</summary>
    [Fact]
    public async Task An_uncertain_zone_with_nothing_withheld_writes_nothing()
    {
        var (service, bag, events) = Build(new FakeZones(0));
        bag.Carrying[PokeBall] = 5;

        var result = await service.ApplyAsync(SampleRun(), new HashSet<string> { "ruta-1" });

        Assert.Equal(BallControlOutcome.ZoneAmbiguous, result.Outcome);
        Assert.Empty(bag.Writes);
        Assert.Empty(events.Appended);
        Assert.Equal(5, bag.Carried(PokeBall));
    }
}
