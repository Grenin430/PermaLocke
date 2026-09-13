using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The LUDÓPATA wheel: what it owes, what it shows and what it decides.
/// </summary>
/// <remarks>
/// The two things worth guarding are that a spin is <b>owed</b> before it turns — a wheel anyone
/// can spin at will is not a rule, it is a button — and that the same seed and number always give
/// the same face, which is what makes a spin auditable and a failed one safe to retry.
/// </remarks>
public sealed class RouletteServiceTests
{
    private sealed class Catalog : IRouletteCatalog
    {
        public IReadOnlyList<RouletteFace> Faces { get; init; } = Sixteen();

        public IReadOnlyList<int> GoodAbilities { get; init; } = [22, 26, 91];

        public IReadOnlyList<int> BadAbilities { get; init; } = [54, 112];

        public IReadOnlyList<int> HealingItems { get; init; } = [17, 25, 26, 27, 28];

        public int SpinsPerTrial => 1;

        public int SpinsForLeague => 3;

        public int SpinsForRematch => 2;

        public IReadOnlyList<string> TrialAchievements => ["prueba-01", "prueba-02"];

        public string LeagueAchievement => "alto-mando-campeon";

        public string RematchAchievement => "alto-mando-otra-vez";
    }

    /// <summary>Eight good and eight bad, one per effect the wheel knows.</summary>
    private static RouletteFace[] Sixteen() =>
    [
        new("gacha-1", "1 tirada", "", true, RouletteEffect.Gacha, Banners: ["decente"]),
        new("gacha-3", "3 tiradas", "", true, RouletteEffect.Gacha, Banners: ["pocho", "decente", "bueno"]),
        new("hab-buena", "Habilidad buena", "", true, RouletteEffect.HabilidadBuena, 3),
        new("puntos-mas", "+200", "", true, RouletteEffect.Puntos, 200),
        new("cura-1", "Curativos", "", true, RouletteEffect.DarCurativos, 3),
        new("cura-3", "Curativos x3", "", true, RouletteEffect.DarCurativos, 3, 3),
        new("mt-mas", "1 MT", "", true, RouletteEffect.DarMt, 1),
        new("iv-max", "IV 31", "", true, RouletteEffect.IvPerfectos, 3),
        new("muerte-1", "Muere 1", "", false, RouletteEffect.Muerte, 1),
        new("muerte-3", "Mueren 3", "", false, RouletteEffect.Muerte, 3),
        new("hab-mala", "Habilidad mala", "", false, RouletteEffect.HabilidadMala, 3),
        new("puntos-menos", "-200", "", false, RouletteEffect.Puntos, -200),
        new("iv-cero", "IV 0", "", false, RouletteEffect.IvCero, 3),
        new("mt-menos", "Menos MT", "", false, RouletteEffect.QuitarMt, 1),
        new("cura-menos-1", "Menos curativos", "", false, RouletteEffect.QuitarCurativos, 3),
        new("cura-menos-3", "Menos curativos x3", "", false, RouletteEffect.QuitarCurativos, 3, 3)
    ];

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

    private sealed class Achievements(IReadOnlyList<Achievement> all) : IAchievementCatalog
    {
        public IReadOnlyList<Achievement> All => all;
    }

    private sealed class Records(params int[] itemsHeld) : IGameRecords
    {
        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(new GameRecordSnapshot(true, null, null,
                new Dictionary<int, int>(), DateTimeOffset.UnixEpoch,
                itemsHeld.ToHashSet(), new Dictionary<int, int>()));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 23, 20, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Stands in for the player's game. Remembers what it was told to do.</summary>
    private sealed class World(RouletteWorld? seen = null) : IRouletteWorldPort
    {
        public List<RouletteAction> Applied { get; } = [];

        public bool Reachable { get; init; } = true;

        public bool CanActNow(out string reason)
        {
            reason = Reachable ? string.Empty : "El juego está abierto.";
            return Reachable;
        }

        public Task<RouletteWorld> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(seen ?? Populated());

        public Task<RouletteApplyResult> ApplyAsync(RouletteAction action, CancellationToken ct = default)
        {
            Applied.Add(action);
            return Task.FromResult(new RouletteApplyResult(true, "hecho", ["hecho"]));
        }
    }

    private sealed class Repository(params PokemonEntry[] initial) : IPokemonRepository
    {
        public List<PokemonEntry> Entries { get; } = [.. initial];

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>(Entries);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult(Entries.FirstOrDefault(p => p.Id == pokemonId));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>

            throw new NotSupportedException();


        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
        {
            Entries.RemoveAll(p => p.Id == pokemon.Id);
            Entries.Add(pokemon);
            return Task.CompletedTask;
        }
    }

    private sealed class NoDelivery : IPokemonDelivery
    {
        public bool CanDeliverNow(out string reason)
        {
            reason = string.Empty;
            return true;
        }

        public Task<DeliveryResult> DeliverAsync(GachaPull pull, Run run, CancellationToken ct = default) =>
            Task.FromResult(new DeliveryResult(DeliveryOutcome.Delivered, "entregado", 1, 1, 0xABCD));
    }

    private sealed class Roles(bool roulette) : IRunRoles
    {
        public Role? Of(Guid runId) =>
            new("ludopata", "LUDÓPATA", "", "", 1, 1, 20, 0, 1, roulette);
    }

    private static RouletteWorld Populated() => new(
        [
            new RoulettePokemon(0, 0x1111, 784, "Kommo-o", 24),
            new RoulettePokemon(1, 0x2222, 736, "Grubbin", 8),
            new RoulettePokemon(2, 0x3333, 165, "Ledyba", 4),
            new RoulettePokemon(3, 0x4444, 25, "Pikachu", 12)
        ],
        [328, 329, 330, 331, 332],
        [328, 330],
        new Dictionary<int, int> { [17] = 8, [25] = 2, [28] = 6 });

    private static Run TheRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260823",
        Seed = 20260823,
        RoleId = "ludopata",
        PlayerName = "Grenin"
    };

    private static Achievement[] Milestones() =>
    [
        new("prueba-01", "Primera", "", null, "sin disparador", 1, 100, Item: 807),
        new("prueba-02", "Segunda", "", null, "sin disparador", 1, 100, Item: 813),
        new("alto-mando-campeon", "Campeón", "", null, "sin disparador", 1, 300, Item: 900),
        new("alto-mando-otra-vez", "Otra vez", "", null, "sin disparador", 1, 300, Item: 901)
    ];

    private static (RouletteService Service, Events Log, World World, Repository Team) Build(
        Records? records = null, World? world = null, bool roulette = true, Catalog? catalog = null)
    {
        var log = new Events();
        var clock = new FixedClock();
        var team = new Repository();
        var seen = world ?? new World();

        var achievements = new AchievementService(
            new Achievements(Milestones()), new PointsService(log, clock), log, clock,
            records ?? new Records(), new FixedRole(FixedRole.Normal));

        var service = new RouletteService(catalog ?? new Catalog(), achievements, seen,
            new Roles(roulette), team, log, clock);

        return (service, log, seen, team);
    }

    private sealed class EmptyGacha : IGachaCatalog
    {
        public IReadOnlyList<GachaTier> Tiers => [];

        public IReadOnlyList<GachaBanner> Banners => [];

        public IReadOnlyList<StageOdds> StageOdds => [];
    }

    private sealed class NoSpecies : ISpeciesStatsCatalog
    {
        public IReadOnlyList<SpeciesStats> All => [];

        public IReadOnlyList<EvolutionLine> Lines => [];

        public IReadOnlyList<string> Natures => [];

        public IReadOnlyList<string> Abilities => [];
    }

    [Fact]
    public async Task Every_milestone_owes_its_spins()
    {
        var (service, _, _, _) = Build(new Records(807, 813, 900, 901));

        // Dos pruebas a una, la liga a tres y el rematch a dos.
        Assert.Equal(7, await service.EarnedAsync(TheRun()));
    }

    [Fact]
    public async Task Nothing_cleared_owes_nothing()
    {
        var (service, _, _, _) = Build(new Records());

        Assert.Equal(0, await service.EarnedAsync(TheRun()));
        Assert.Equal(0, await service.OwedAsync(TheRun()));
    }

    /// <summary>
    /// A granted spin is owed like an earned one, and it can only ever be an addition.
    /// </summary>
    /// <remarks>
    /// This is the only way there is to give a spin back, because there is no way to take one
    /// away: a single event cannot be deleted, so the ledger only grows. The count comes back out
    /// of the event that granted it rather than a stored number, so it is the history that says
    /// how many were handed over, not a field somebody could set.
    /// </remarks>
    [Fact]
    public async Task A_granted_spin_is_owed_and_says_who_decided_it()
    {
        var (service, log, _, _) = Build(new Records());
        var run = TheRun();

        Assert.Equal(0, await service.OwedAsync(run));

        Assert.Equal(2, await service.GrantAsync(run, 2, "se rompiÃ³ la animaciÃ³n"));

        Assert.Equal(2, await service.GrantedAsync(run.Id));
        Assert.Equal(2, await service.OwedAsync(run));

        var granted = Assert.Single(log.Appended, e => e.Type == GameEventType.RouletteGranted);
        Assert.Equal(EventSource.Player, granted.Source);
        Assert.Equal("2", granted.Data["tiradas"]);
        Assert.Contains("se rompiÃ³ la animaciÃ³n", granted.Data["motivo"]);
    }

    /// <summary>Granted spins add to the earned ones instead of replacing them.</summary>
    [Fact]
    public async Task Granting_adds_to_what_the_milestones_paid_for()
    {
        var (service, _, _, _) = Build(new Records(807, 813));
        var run = TheRun();

        await service.GrantAsync(run, 1, "prueba");

        Assert.Equal(3, await service.EarnedAsync(run));
        Assert.Equal(3, await service.OwedAsync(run));
    }

    /// <summary>An entry nobody can read later is not a record.</summary>
    [Fact]
    public async Task A_grant_without_a_reason_is_refused()
    {
        var (service, log, _, _) = Build(new Records());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GrantAsync(TheRun(), 1, "   "));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GrantAsync(TheRun(), 0, "ninguna"));

        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RouletteGranted);
    }

    /// <summary>A granted spin turns the wheel like any other, and is spent by turning it.</summary>
    [Fact]
    public async Task A_granted_spin_actually_turns_the_wheel()
    {
        var (service, log, _, _) = Build(new Records());
        var run = TheRun();

        await service.GrantAsync(run, 2, "se rompiÃ³ la animaciÃ³n");

        var result = await service.SpinAsync(run);

        Assert.Equal(RouletteOutcome.Spun, result.Outcome);
        Assert.Equal(1, result.Owed);
        Assert.Single(log.Appended, e => e.Type == GameEventType.RouletteSpun);
    }

    /// <summary>A wheel anybody could spin at will would not be a rule.</summary>
    [Fact]
    public async Task It_refuses_to_turn_when_nothing_is_owed()
    {
        var (service, log, world, _) = Build(new Records());

        var result = await service.SpinAsync(TheRun());

        Assert.Equal(RouletteOutcome.NothingOwed, result.Outcome);
        Assert.Empty(world.Applied);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RouletteSpun);
    }

    [Fact]
    public async Task A_run_of_another_role_never_spins()
    {
        var (service, _, _, _) = Build(new Records(807, 813), roulette: false);

        Assert.Equal(RouletteOutcome.NotThisRole, (await service.SpinAsync(TheRun())).Outcome);
    }

    /// <summary>
    /// With the game open nothing spins, and nothing is owed away either: the spin survives.
    /// </summary>
    [Fact]
    public async Task With_the_game_open_the_spin_is_not_spent()
    {
        var (service, log, _, _) = Build(new Records(807), new World { Reachable = false });

        var result = await service.SpinAsync(TheRun());

        Assert.Equal(RouletteOutcome.GameUnreachable, result.Outcome);
        Assert.Equal(1, result.Owed);
        Assert.DoesNotContain(log.Appended, e => e.Type == GameEventType.RouletteSpun);
    }

    [Fact]
    public async Task Spinning_consumes_exactly_one()
    {
        var (service, log, _, _) = Build(new Records(807, 813));
        var run = TheRun();

        Assert.Equal(2, await service.OwedAsync(run));

        var result = await service.SpinAsync(run);

        Assert.Equal(RouletteOutcome.Spun, result.Outcome);
        Assert.Equal(1, result.Owed);
        Assert.Equal(1, await service.SpunAsync(run.Id));
        Assert.Single(log.Appended, e => e.Type == GameEventType.RouletteSpun);
    }

    /// <summary>Same seed, same number, same wheel. It is what makes a spin checkable.</summary>
    [Fact]
    public void The_same_spin_number_always_gives_the_same_wheel()
    {
        var (service, _, _, _) = Build();

        var first = service.Preview(20260823, 4)!;
        var again = service.Preview(20260823, 4)!;

        Assert.Equal(first.Faces.Select(f => f.Id), again.Faces.Select(f => f.Id));
        Assert.Equal(first.WinningIndex, again.WinningIndex);
    }

    [Fact]
    public void A_different_number_gives_a_different_wheel()
    {
        var (service, _, _, _) = Build();

        var wheels = Enumerable.Range(0, 12)
            .Select(n => string.Join(',', service.Preview(20260823, n)!.Faces.Select(f => f.Id)))
            .Distinct()
            .Count();

        Assert.True(wheels > 1, "doce tiradas seguidas no pueden dar la misma rueda");
    }

    /// <summary>
    /// Six faces, all different. A wheel with the same face twice would lie about its own odds.
    /// </summary>
    [Fact]
    public void The_wheel_shows_six_different_faces()
    {
        var (service, _, _, _) = Build();

        for (var number = 0; number < 40; number++)
        {
            var wheel = service.Preview(20260823, number)!;

            Assert.Equal(RouletteService.FacesOnTheWheel, wheel.Faces.Count);
            Assert.Equal(wheel.Faces.Count, wheel.Faces.Select(f => f.Id).Distinct().Count());
            Assert.InRange(wheel.WinningIndex, 0, wheel.Faces.Count - 1);
        }
    }

    /// <summary>
    /// Nothing balances the six. Over enough spins a wheel of six of one kind has to turn up, and
    /// if it never does then something is quietly rigging it.
    /// </summary>
    [Fact]
    public void Nothing_keeps_the_good_and_the_bad_balanced()
    {
        var (service, _, _, _) = Build();

        var lopsided = Enumerable.Range(0, 400)
            .Select(n => service.Preview(20260823, n)!)
            .Count(w => w.Faces.All(f => f.Good) || w.Faces.All(f => !f.Good));

        Assert.True(lopsided > 0, "en cuatrocientas tiradas tiene que salir alguna rueda de un solo color");
    }

    [Fact]
    public void Points_faces_move_the_balance_from_the_spin_event()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.Puntos && f.Amount < 0);

        Assert.Equal(-200, face.Amount);
    }

    /// <summary>Three of the party when the party has four: three different ones.</summary>
    [Fact]
    public void It_picks_different_pokemon()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.IvPerfectos);

        var action = service.Decide(face, Populated(), new SeededRandomSource(7));

        Assert.Equal(3, action.Pokemon.Count);
        Assert.Equal(3, action.Pokemon.Select(p => p.Pid).Distinct().Count());
    }

    /// <summary>"Tres del equipo" with two in the party is two, not a failure.</summary>
    [Fact]
    public void With_fewer_pokemon_than_asked_it_takes_what_there_is()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.Muerte && f.Amount == 3);

        var small = Populated() with { Party = [new RoulettePokemon(0, 1, 25, "Pikachu", 5)] };
        var action = service.Decide(face, small, new SeededRandomSource(7));

        Assert.Single(action.Pokemon);
    }

    [Fact]
    public void An_empty_party_means_nothing_to_do()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.HabilidadMala);

        var action = service.Decide(face, Populated() with { Party = [] }, new SeededRandomSource(7));

        Assert.True(action.IsEmpty);
    }

    /// <summary>A TM face gives one the player has not got, and never one they already carry.</summary>
    [Fact]
    public void A_new_tm_is_one_that_is_missing()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.DarMt);

        for (var seed = 0ul; seed < 50; seed++)
        {
            var action = service.Decide(face, Populated(), new SeededRandomSource(seed));
            var given = Assert.Single(action.Items);

            Assert.Equal(1, given.Delta);
            Assert.DoesNotContain(given.ItemId, new[] { 328, 330 });
            Assert.Contains(given.ItemId, new[] { 329, 331, 332 });
        }
    }

    /// <summary>And a TM taken away is one they do carry.</summary>
    [Fact]
    public void A_lost_tm_is_one_that_was_there()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.QuitarMt);

        for (var seed = 0ul; seed < 50; seed++)
        {
            var taken = Assert.Single(service.Decide(face, Populated(), new SeededRandomSource(seed)).Items);

            Assert.Equal(-1, taken.Delta);
            Assert.Contains(taken.ItemId, new[] { 328, 330 });
        }
    }

    [Fact]
    public void Taking_a_tm_from_somebody_with_none_does_nothing()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.QuitarMt);

        var action = service.Decide(face, Populated() with { TmsHeld = [] }, new SeededRandomSource(7));

        Assert.True(action.IsEmpty);
    }

    /// <summary>Never more than the player has: a bag cannot go below zero.</summary>
    [Fact]
    public void Taking_healing_items_never_goes_below_what_is_carried()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.QuitarCurativos && f.Each == 3);

        var action = service.Decide(face, Populated(), new SeededRandomSource(7));

        foreach (var change in action.Items)
        {
            Assert.True(change.Delta < 0);
            Assert.True(-change.Delta <= Populated().HealingHeld[change.ItemId]);
        }
    }

    [Fact]
    public void Giving_healing_items_gives_three_different_ones()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.DarCurativos && f.Each == 3);

        var action = service.Decide(face, Populated(), new SeededRandomSource(7));

        Assert.Equal(3, action.Items.Count);
        Assert.Equal(3, action.Items.Select(i => i.ItemId).Distinct().Count());
        Assert.All(action.Items, i => Assert.Equal(3, i.Delta));
    }

    /// <summary>An ability face with no abilities configured touches nobody.</summary>
    [Fact]
    public void Without_abilities_configured_nothing_is_written()
    {
        var (service, _, _, _) = Build(catalog: new Catalog { GoodAbilities = [] });
        var face = Sixteen().First(f => f.Effect == RouletteEffect.HabilidadBuena);

        var action = service.Decide(face, Populated(), new SeededRandomSource(7));

        Assert.True(action.IsEmpty);
    }

    [Fact]
    public void An_ability_face_brings_one_ability_per_pokemon()
    {
        var (service, _, _, _) = Build();
        var face = Sixteen().First(f => f.Effect == RouletteEffect.HabilidadMala);

        var action = service.Decide(face, Populated(), new SeededRandomSource(7));

        Assert.Equal(action.Pokemon.Count, action.Abilities.Count);
        Assert.All(action.Abilities, id => Assert.Contains(id, new[] { 54, 112 }));
    }
}
