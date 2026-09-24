using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The move reminder (§142): which moves it offers, and what it lets reach the save.
/// </summary>
public sealed class MoveReminderTests
{
    // ============================================================ LA REGLA

    private static readonly IReadOnlyList<LevelUpMove> Learnset =
    [
        new(10, 1),
        new(20, 1),
        new(30, 0),
        new(40, 12),
        new(50, 25),
        new(60, 40)
    ];

    /// <summary>
    /// By level means up to and including the level it is at, not beyond: a level 25 Pokémon has not reached what
    /// its species learns at 40.
    /// </summary>
    [Fact]
    public void Level_up_moves_stop_at_its_level()
    {
        var options = MoveReminder.Options(Learnset, 25, [], []);

        Assert.Contains(options, o => o.Move == 50 && o.From == RememberedFrom.Level && o.Level == 25);
        Assert.DoesNotContain(options, o => o.Move == 60);
    }

    /// <summary>Level 0 is what the species learns on evolving: always within reach, and listed first.</summary>
    [Fact]
    public void Evolution_moves_are_always_offered_and_come_first()
    {
        var options = MoveReminder.Options(Learnset, 2, [], []);

        Assert.Equal(RememberedFrom.Evolution, options[0].From);
        Assert.Equal(30, options[0].Move);
        Assert.DoesNotContain(options, o => o.Move == 40);
    }

    /// <summary>What it already knows is not something to remember, as in the game.</summary>
    [Fact]
    public void Moves_it_knows_are_left_out()
    {
        var options = MoveReminder.Options(Learnset, 50, [10, 40, 0, 0], []);

        Assert.DoesNotContain(options, o => o.Move is 10 or 40 or 0);
        Assert.Contains(options, o => o.Move == 60);
    }

    /// <summary>
    /// Añil's «iniciales»: what it knew on arrival is always offered, even when its current species does not learn
    /// it — that is the whole point once it has evolved in a randomlocke.
    /// </summary>
    [Fact]
    public void What_it_knew_on_arrival_is_offered_even_if_its_species_does_not_learn_it()
    {
        var options = MoveReminder.Options(Learnset, 5, [], [777, 888]);

        Assert.Equal([777, 888], options.Take(2).Select(o => o.Move));
        Assert.All(options.Take(2), o => Assert.Equal(RememberedFrom.FirstKnown, o.From));
    }

    /// <summary>A move reachable two ways appears once, by the road that says at what level.</summary>
    [Fact]
    public void A_move_reachable_two_ways_appears_once_by_its_level()
    {
        var options = MoveReminder.Options(Learnset, 30, [], [40, 40, 0]);

        var forty = Assert.Single(options, o => o.Move == 40);
        Assert.Equal(RememberedFrom.Level, forty.From);
        Assert.Equal(12, forty.Level);
    }

    /// <summary>A learnset that repeats a move (the randomizer's can) does not repeat it on screen.</summary>
    [Fact]
    public void A_repeated_learnset_move_is_listed_once()
    {
        var options = MoveReminder.Options([new(10, 1), new(10, 20), new(0, 5)], 30, [], []);

        Assert.Equal([10], options.Select(o => o.Move));
    }

    [Fact]
    public void Move_lists_survive_the_round_trip_and_rubbish()
    {
        Assert.Equal([33, 45, 857], MoveReminder.ParseList(MoveReminder.FormatList([33, 0, 45, 857])));
        Assert.Equal([12], MoveReminder.ParseList("x, 12, -3,,0"));
        Assert.Empty(MoveReminder.ParseList(null));
    }

    // ============================================================ EL SERVICIO

    private sealed class Catalog : IMoveCatalog
    {
        public Dictionary<(int, int), IReadOnlyList<LevelUpMove>> Learnsets { get; } = [];

        public string Source => "mundo de prueba";

        public HashSet<int> Banned { get; } = [];

        public bool IsBanned(int move) => Banned.Contains(move);

        public IReadOnlyList<LevelUpMove>? LevelUp(int species, int form) =>
            Learnsets.TryGetValue((species, form), out var list) ? list : null;

        /// <summary>Every move exists with 10 PP, except 999, which is a hole of the table with none.</summary>
        public MoveSheet? Describe(int move) =>
            move <= 0 ? null : new MoveSheet(move, $"Mov{move}", 0, "Normal", MoveSheet.Physical, 50, 100,
                move == 999 ? 0 : 10);
    }

    private sealed class Teacher(bool works = true) : IMoveTeacher
    {
        public List<MoveChange> Applied { get; } = [];

        public bool CanTeachNow(out string reason)
        {
            reason = works ? string.Empty : "juego abierto";
            return works;
        }

        public Task<DeliveryResult> ApplyAsync(MoveChange change, CancellationToken ct = default)
        {
            if (!works)
            {
                return Task.FromResult(new DeliveryResult(DeliveryOutcome.GameRunning, "juego abierto"));
            }

            Applied.Add(change);
            return Task.FromResult(new DeliveryResult(DeliveryOutcome.Delivered, "hecho"));
        }
    }

    private sealed class Repository : IPokemonRepository
    {
        public List<PokemonEntry> Entries { get; } = [];

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>(Entries);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult(Entries.FirstOrDefault(e => e.Id == pokemonId));

        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
        {
            Entries.Add(pokemon);
            return Task.CompletedTask;
        }

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private const uint Pid = 0xBEEF0001;

    private static BoxedPokemon Pokemon(int[] moves, int level = 25, int[]? relearn = null) => new(
        Box: BoxedPokemon.PartyBox, Slot: 1, Species: 700, Form: 0, SpeciesName: "Sylveon", Nickname: string.Empty,
        Level: level, IsShiny: false, IsEgg: false, GenderMark: string.Empty, NatureName: "Firme",
        AbilityName: "Piel Feérica", HeldItemName: string.Empty, BallName: "Poké Ball", TrainerName: "Grenin",
        MetLocationName: "Ruta 1", MetLevel: 5, Moves: [], Stats: [100, 100, 100, 100, 100, 100],
        Ivs: [31, 31, 31, 31, 31, 31], Evs: [0, 0, 0, 0, 0, 0], Friendship: 70, Pid: Pid,
        MoveIds: moves, RelearnMoveIds: relearn ?? [0, 0, 0, 0]);

    private static (MoveReminderService Service, Teacher Teacher, InMemoryEventStore Log, Repository Registered, Run Run)
        Build(bool works = true, params int[] banned)
    {
        var catalog = new Catalog();
        catalog.Learnsets[(700, 0)] = Learnset;
        catalog.Banned.UnionWith(banned);

        var teacher = new Teacher(works);
        var log = new InMemoryEventStore();
        var registered = new Repository();

        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = "Prueba",
            Game = GameVersion.UltraMoon,
            SeedLabel = "20260919",
            Seed = 20260919,
            RoleId = "player",
            PlayerName = "Grenin"
        };

        return (new MoveReminderService(catalog, teacher, log, registered, new FixedClock()), teacher, log,
            registered, run);
    }

    /// <summary>The shop's order: the save first, the log after, and nothing logged when the save said no.</summary>
    [Fact]
    public async Task It_writes_first_and_records_after()
    {
        var (service, teacher, log, _, run) = Build();

        var result = await service.TeachAsync(run, Pokemon([10, 20, 30, 40]), 50, 1);

        Assert.True(result.Delivered);
        var change = Assert.Single(teacher.Applied);
        Assert.Equal((1, 20, 50, 10), (change.MoveSlot, change.Replaced, change.Move, change.PP));

        var recorded = Assert.Single(await log.GetAllAsync(run.Id));
        Assert.Equal(GameEventType.MoveRemembered, recorded.Type);
        Assert.Equal("50", recorded.Data["movimiento"]);
        Assert.Equal("20", recorded.Data["olvida"]);
        Assert.Equal("BEEF0001", recorded.Data["pid"]);
    }

    [Fact]
    public async Task Nothing_is_recorded_when_the_save_refuses()
    {
        var (service, _, log, _, run) = Build(works: false);

        var result = await service.TeachAsync(run, Pokemon([10, 20, 30, 40]), 50, 1);

        Assert.False(result.Delivered);
        Assert.Empty(await log.GetAllAsync(run.Id));
    }

    /// <summary>
    /// The list is worked out again by the service, never taken from the screen: a move the rule does not allow —
    /// above its level here — cannot be written whatever asks for it.
    /// </summary>
    [Fact]
    public async Task A_move_it_cannot_remember_never_reaches_the_save()
    {
        var (service, teacher, log, _, run) = Build();

        var result = await service.TeachAsync(run, Pokemon([10, 20, 30, 40], level: 25), 60, 0);

        Assert.False(result.Delivered);
        Assert.Empty(teacher.Applied);
        Assert.Empty(await log.GetAllAsync(run.Id));
    }

    /// <summary>
    /// A banned move (§162) is offered by no road — not from its level, not as one it knew on arrival — and cannot be
    /// written whatever asks for it.
    /// </summary>
    [Fact]
    public async Task A_banned_move_is_never_offered_nor_taught()
    {
        var (service, teacher, log, _, run) = Build(banned: [20, 90]);
        var target = Pokemon([10, 30, 40, 0], relearn: [90, 0, 0, 0]);

        var options = await service.OptionsAsync(run.Id, target);

        Assert.DoesNotContain(options.Moves, option => option.Move is 20 or 90);
        Assert.Contains(options.Moves, option => option.Move == 50);

        var result = await service.TeachAsync(run, target, 90, 3);

        Assert.False(result.Delivered);
        Assert.Empty(teacher.Applied);
        Assert.Empty(await log.GetAllAsync(run.Id));
    }

    /// <summary>A fallen Pokémon learns nothing, the same way the wonder trade will not take one.</summary>
    [Fact]
    public async Task A_fallen_pokemon_learns_nothing()
    {
        var (service, teacher, _, registered, run) = Build();

        registered.Entries.Add(new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = 700,
            SpeciesName = "Sylveon",
            Origin = PokemonOrigin.Capture,
            EncounterType = EncounterType.Wild,
            Status = PokemonStatus.Dead,
            Pid = Pid
        });

        var result = await service.TeachAsync(run, Pokemon([10, 20, 30, 40]), 50, 1);

        Assert.False(result.Delivered);
        Assert.Contains("caído", result.Message);
        Assert.Empty(teacher.Applied);
    }

    /// <summary>A Huevo Malo is shown and never written (§97), and neither is an egg.</summary>
    [Fact]
    public async Task A_damaged_entry_or_an_egg_is_never_written()
    {
        var (service, teacher, _, _, run) = Build();

        var damaged = await service.TeachAsync(run, Pokemon([10, 20, 30, 40]) with { IsIntact = false }, 50, 1);
        var egg = await service.TeachAsync(run, Pokemon([10, 20, 30, 40]) with { IsEgg = true }, 50, 1);

        Assert.False(damaged.Delivered);
        Assert.False(egg.Delivered);
        Assert.Empty(teacher.Applied);
    }

    /// <summary>
    /// Moves have no holes in the game: asking for the fourth slot of a Pokémon that knows two fills the third.
    /// </summary>
    [Fact]
    public async Task An_empty_slot_is_filled_from_the_front()
    {
        var (service, teacher, _, _, run) = Build();

        await service.TeachAsync(run, Pokemon([10, 20, 0, 0]), 50, 3);

        var change = Assert.Single(teacher.Applied);
        Assert.Equal(2, change.MoveSlot);
        Assert.Equal(0, change.Replaced);
    }

    /// <summary>
    /// What it knew when PermaLocke registered it comes from the capture event, and the game's own relearn slots
    /// count the same way.
    /// </summary>
    [Fact]
    public async Task What_it_knew_on_arrival_comes_from_its_capture_event_and_its_relearn_slots()
    {
        var (service, _, log, registered, run) = Build();

        var caught = await log.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = DateTimeOffset.Now,
            Type = GameEventType.PokemonCaught,
            Source = EventSource.AutoDetect,
            Actor = "Grenin",
            Description = "Eevee capturado.",
            Data = new Dictionary<string, string> { [MoveReminderService.FirstMovesKey] = "701,702" }
        });

        registered.Entries.Add(new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = 700,
            SpeciesName = "Sylveon",
            Origin = PokemonOrigin.Capture,
            EncounterType = EncounterType.Wild,
            Pid = Pid,
            OriginEventId = caught.Id
        });

        var options = await service.OptionsAsync(run.Id, Pokemon([10, 20, 30, 40], relearn: [703, 0, 0, 0]));

        Assert.True(options.FirstKnownRecorded);
        Assert.Equal([701, 702, 703],
            options.Moves.Where(o => o.From == RememberedFrom.FirstKnown).Select(o => o.Move));
    }

    /// <summary>A move with no PP cannot be picked in battle; offering it would hand out Struggle.</summary>
    [Fact]
    public async Task A_move_without_PP_is_never_offered()
    {
        var (service, _, _, _, run) = Build();

        var options = await service.OptionsAsync(run.Id, Pokemon([10, 20, 30, 40], relearn: [999, 0, 0, 0]));

        Assert.DoesNotContain(options.Moves, o => o.Move == 999);
    }

    /// <summary>Without a learnset the list is incomplete, and it says so instead of looking complete.</summary>
    [Fact]
    public async Task An_unreadable_learnset_is_said_not_hidden()
    {
        var (service, _, _, _, run) = Build();

        var options = await service.OptionsAsync(run.Id, Pokemon([10, 20, 30, 40]) with { Species = 1 });

        Assert.False(options.Learnset);
        Assert.Empty(options.Moves);
    }
}
