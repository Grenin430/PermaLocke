using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The MONOTYPE roles (§220): what a type admits, the gacha and the wonder trade that only hand out those species, and the
/// gacha that refuses to roll while the run owns a living Pokémon the type does not admit.
/// </summary>
public sealed class MonotypeRuleTests
{
    private const int Fire = 9, Grass = 11, Poison = 3, Flying = 2, Psychic = 13, Rock = 5;

    private sealed class Roles : IRoleCatalog
    {
        public IReadOnlyList<Role> All { get; } =
        [
            new("normal", "NORMAL", "", "", 1.0, 1.0, 20, 0, 1),
            new("monotype_fuego", "MONOTYPE FUEGO", "", "", 1.5, 1.0, 20, 0, 1, MonoType: Fire)
        ];

        public IReadOnlySet<int> ImportantTrainerClasses { get; } = new HashSet<int>();

        public Role? Find(string? id) => All.FirstOrDefault(r => r.Id == id);
    }

    /// <summary>Types by species, and a Ponyta-like species that is Fire and, in form 1, Psychic.</summary>
    private sealed class Types : ITypeLookup
    {
        private static readonly Dictionary<int, (int, int)> Table = new()
        {
            [1] = (Fire, Fire), [2] = (Fire, Fire), [3] = (Fire, Flying), [4] = (Grass, Grass), [5] = (Grass, Poison),
            [6] = (Rock, Rock), [7] = (Fire, Fire), [8] = (Fire, Fire)
        };

        public TypePair GetTypes(int species) => Pair(Table[species]);

        public TypePair GetTypes(int species, int form) => species == 8 && form == 1 ? Pair((Psychic, Psychic)) : GetTypes(species);

        public string GetName(int type) => $"Tipo {type}";

        private static TypePair Pair((int First, int Second) t) => new(t.First, $"Tipo {t.First}", t.Second, $"Tipo {t.Second}");
    }

    private sealed class Species(IReadOnlyList<SpeciesStats> all) : ISpeciesStatsCatalog
    {
        public IReadOnlyList<SpeciesStats> All => all;

        public IReadOnlyList<string> Natures { get; } = [.. Enumerable.Range(0, 25).Select(n => $"Naturaleza {n}")];

        public IReadOnlyList<string> Abilities { get; } = ["", "Levitación", "Impostor", "Presión"];

        public IReadOnlyList<EvolutionLine> Lines { get; } =
        [
            new([[1], [2], [3]]),   // 300 -> 400 -> 534: Fuego, y Fuego/Volador al final
            new([[4], [5]]),        // 300 -> 450: Planta, Planta/Veneno
            new([[6]]),             // 500: Roca
            new([[7]]),             // 680 legendario, Fuego
            new([[8]])              // 350: Fuego, y en la forma 1 Psíquico
        ];

        public IReadOnlyCollection<int> BannedAbilities { get; } = [];
    }

    private static SpeciesStats[] Table() =>
    [
        new(1, "Llama A", 300, false, ["Habilidad A"]),
        new(2, "Llama B", 400, false, ["Habilidad A"]),
        new(3, "Llama C", 534, false, ["Habilidad A"]),
        new(4, "Hoja A", 300, false, ["Habilidad A"]),
        new(5, "Hoja B", 450, false, ["Habilidad A"]),
        new(6, "Roca", 500, false, ["Habilidad A"]),
        new(7, "Legendario de fuego", 680, true, ["Habilidad A"]),
        new(8, "Ponyta", 350, false, ["Habilidad A"], [new SpeciesForm(1, "Galar")])
    ];

    private static GachaTier[] Tiers() =>
    [
        new("tier1", "Tier 1", 400, 0.0, 5, 5, 0, 0),
        new("tier2", "Tier 2", 490, 0.0, 5, 5, 0, 0),
        new("tier3", "Tier 3", 535, 0.0, 5, 5, 0, 0),
        new("tier4", "Tier 4", 590, 0.0, 5, 5, 0, 0),
        new("tier5", "Tier 5", 9999, 0.40, 5, 5, 0, 0)
    ];

    private sealed class GachaCatalog(GachaBanner banner) : IGachaCatalog
    {
        public IReadOnlyList<GachaTier> Tiers => MonotypeRuleTests.Tiers();

        public IReadOnlyList<GachaBanner> Banners => [banner];

        public IReadOnlyList<StageOdds> StageOdds { get; } = [new(0, 0, 0), new(12, 45, 20)];
    }

    private sealed class WonderCatalog(WonderTradeWindow window) : IWonderTradeCatalog
    {
        public WonderTradeWindow Window => window;
    }

    private sealed class Repository(params PokemonEntry[] initial) : IPokemonRepository
    {
        public List<PokemonEntry> Entries { get; } = [.. initial];

        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>(Entries);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult(Entries.FirstOrDefault(p => p.Id == pokemonId));

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default)
        {
            Entries.RemoveAll(p => p.Id == pokemon.Id);
            Entries.Add(pokemon);
            return Task.CompletedTask;
        }
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

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ZeroPoints : IPointsService
    {
        public Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<PointsResult> EarnAsync(Guid runId, int amount, string description, EventSource source, string actor,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<PointsResult> SpendAsync(Guid runId, int amount, string description, EventSource source, string actor,
            CancellationToken ct = default) => throw new InvalidOperationException("la tirada es gratis");

        public Task<PointsResult> AdjustAsync(Guid runId, int delta, string reason, string adminName,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now => new(2026, 10, 7, 1, 0, 0, TimeSpan.Zero);
    }

    private static readonly GachaBanner Free = new("todo", "TODO", string.Empty, 0,
        new Dictionary<string, double> { ["tier1"] = 1, ["tier2"] = 1, ["tier3"] = 1, ["tier4"] = 1, ["tier5"] = 1 });

    private static Run FireRun(string role = "monotype_fuego") => new()
    {
        Id = Guid.NewGuid(), Name = "Prueba", Game = GameVersion.UltraMoon, SeedLabel = "1", Seed = 20261007,
        RoleId = role, PlayerName = "Grenin"
    };

    private static PokemonEntry Owned(int species, string name, PokemonStatus status = PokemonStatus.Alive, int form = 0) => new()
    {
        Id = Guid.NewGuid(), RunId = Guid.NewGuid(), Species = species, SpeciesName = name, Form = form,
        Origin = PokemonOrigin.Capture, EncounterType = EncounterType.Wild, Status = status
    };

    private static MonotypeRule Rule(Repository? repository = null) =>
        new(new Roles(), new Types(), new Species(Table()), repository ?? new Repository());

    private static GachaService Gacha(Repository? repository = null, Events? events = null) =>
        new(new GachaCatalog(Free), new Species(Table()), new ZeroPoints(), events ?? new Events(),
            repository ?? new Repository(), new Clock(), Rule(repository));

    private static WonderTradeService Wonder(Repository? repository = null, Events? events = null, double below = 0.08, double above = 0.08) =>
        new(new WonderCatalog(new WonderTradeWindow(below, above, true)), new Species(Table()), new Types(),
            events ?? new Events(), repository ?? new Repository(), new Clock(), Rule(repository));

    [Fact]
    public void A_species_is_admitted_when_the_type_is_one_of_its_two()
    {
        var rule = Rule();

        Assert.True(rule.Allows(Fire, 1));
        Assert.True(rule.Allows(Fire, 3));      // Fuego/Volador
        Assert.True(rule.Allows(Flying, 3));
        Assert.False(rule.Allows(Fire, 4));
        Assert.False(rule.Allows(Fire, 5));     // Planta/Veneno
        Assert.True(rule.Allows(Poison, 5));
    }

    [Fact]
    public void A_form_counts_with_its_own_types()
    {
        var rule = Rule();

        Assert.True(rule.Allows(Fire, 8));
        Assert.False(rule.Allows(Fire, 8, form: 1));
        Assert.True(rule.Allows(Psychic, 8, form: 1));

        // La especie entra en una run Psíquico por su forma, y se da en esa forma.
        Assert.Contains(8, rule.SpeciesOf(Psychic));
        var ponyta = Table().Single(s => s.Id == 8);
        Assert.Equal((1, "Galar"), rule.FormFor(Psychic, ponyta, drawn: 0, drawnName: string.Empty));
        Assert.Equal((0, string.Empty), rule.FormFor(Fire, ponyta, drawn: 1, drawnName: "Galar"));
        Assert.Equal((1, "Galar"), rule.FormFor(Psychic, ponyta, drawn: 1, drawnName: "Galar"));
    }

    [Fact]
    public void The_roles_type_comes_from_the_role_and_other_roles_have_none()
    {
        var rule = Rule();
        Assert.Equal(Fire, rule.TypeOf(FireRun()));
        Assert.Null(rule.TypeOf(FireRun("normal")));
    }

    [Fact]
    public void The_gacha_only_lands_on_species_of_the_type_even_in_tiers_that_have_none()
    {
        var gacha = Gacha();
        var admitted = new[] { 1, 2, 3, 7, 8 };
        var seen = new HashSet<int>();

        for (var number = 0; number < 600; number++)
        {
            var pull = gacha.Preview(Free, 20261007, number, cleared: 12, monoType: Fire);

            // Los tiers 2 y 4 no tienen ninguna familia de fuego: la tirada baja al más cercano que sí, no se pierde.
            Assert.NotNull(pull);
            Assert.Contains(pull!.Species, admitted);
            seen.Add(pull.Species);
        }

        Assert.True(seen.Count >= 4, $"Salieron solo {string.Join(",", seen)}");
    }

    [Fact]
    public void A_roll_without_a_type_is_what_it_always_was()
    {
        var gacha = Gacha();

        // Los tiers 2 y 4 de esta tabla no tienen familias: sin tipo, esas tiradas no existen, como siempre.
        var species = Enumerable.Range(0, 300).Select(n => gacha.Preview(Free, 20261007, n, cleared: 12)?.Species)
            .Where(s => s is not null).Select(s => s!.Value).ToHashSet();

        Assert.Contains(4, species);
        Assert.Contains(6, species);
        Assert.Equal(gacha.Preview(Free, 20261007, 3, cleared: 12), gacha.Preview(Free, 20261007, 3, cleared: 12, monoType: null));
    }

    [Fact]
    public void The_pool_a_screen_lists_only_has_the_type()
    {
        var gacha = Gacha();
        var tier5 = gacha.Tiers.Single(t => t.Id == "tier5");

        Assert.All(gacha.PoolOf(tier5, legendary: true, Fire), s => Assert.True(gacha.Admits(Fire, s.Id)));
        Assert.Equal([7], gacha.PoolOf(tier5, legendary: true, Fire).Select(s => s.Id));
        Assert.False(gacha.Admits(Fire, 4));
        Assert.True(gacha.Admits(null, 4));
    }

    [Fact]
    public async Task The_gacha_refuses_to_roll_while_a_living_pokemon_is_not_of_the_type()
    {
        var wrong = Owned(4, "Hoja A");
        var repository = new Repository(Owned(1, "Llama A"), wrong, Owned(5, "Hoja B", PokemonStatus.Dead),
            Owned(6, "Roca", PokemonStatus.Traded));
        var events = new Events();
        var gacha = Gacha(repository, events);

        var blocked = await gacha.RollAsync(FireRun(), "todo", cleared: 0);

        Assert.False(blocked.Success);
        Assert.Contains("MONOTYPE FUEGO", blocked.Error);
        Assert.Contains("Hoja A", blocked.Error);
        Assert.DoesNotContain("Hoja B", blocked.Error);     // muerto: no cuenta
        Assert.DoesNotContain("Roca", blocked.Error);       // ya no está en la run
        Assert.Empty(events.Appended);

        // Soltarlo (o cambiarlo) deja volver a tirar, y la tirada apunta el tipo para poder recomputarla.
        repository.Entries.Remove(wrong);
        var rolled = await gacha.RollAsync(FireRun(), "todo", cleared: 0);

        Assert.True(rolled.Success);
        Assert.Equal(Fire.ToString(), events.Appended.Single().Data["monotipo"]);
    }

    [Fact]
    public async Task Another_role_is_never_blocked_and_leaves_no_trace_in_the_event()
    {
        var events = new Events();
        var gacha = Gacha(new Repository(Owned(4, "Hoja A")), events);
        var rolled = await gacha.RollAsync(FireRun("normal"), "todo", cleared: 0);

        Assert.True(rolled.Success);
        Assert.DoesNotContain("monotipo", events.Appended.Single().Data.Keys);
    }

    [Fact]
    public void The_wonder_trade_only_gives_the_type_and_opens_the_band_when_nothing_of_it_fits()
    {
        var wonder = Wonder();

        // 300: en ±8 % cae Llama A (300).
        Assert.Equal([1], wonder.PoolFor(300, Fire).Select(s => s.Id));

        // 450: en 414-486 no hay ninguna de fuego; la banda se abre de 2 en 2 puntos hasta alcanzar Llama B (400).
        var (min, max) = wonder.BandFor(450, Fire);
        Assert.True(min <= 400 && max < 534, $"{min}-{max}");
        Assert.Equal([2], wonder.PoolFor(450, Fire).Select(s => s.Id));

        // Sin tipo, la banda es la del archivo, sin abrirse.
        Assert.Equal((414, 487), wonder.BandFor(450, null));
    }

    [Fact]
    public async Task A_trade_in_a_monotype_run_returns_the_type_and_writes_it_down()
    {
        var events = new Events();
        var wonder = Wonder(new Repository(), events);
        var gift = new WonderTradeGift(4, "Hoja A", 20, 0, 1000);

        for (var i = 0; i < 20; i++)
        {
            var offer = wonder.Preview(gift, 20261007, i, Fire);
            Assert.NotNull(offer);
            Assert.True(new Types().GetTypes(offer!.Species, offer.Form).First == Fire
                        || new Types().GetTypes(offer.Species, offer.Form).Second == Fire);
        }

        var result = await wonder.TradeAsync(FireRun(), gift);

        Assert.True(result.Success);
        Assert.Equal(Fire.ToString(), events.Appended.Single().Data["monotipo"]);
    }

    // ============================================================ LA GUARDERÍA (§221)

    private sealed class NurseryCatalog(int minCandidates = 1) : INurseryCatalog
    {
        public int SpinsPerTrial => 1;

        public int SpinsForLeague => 3;

        public int SpinsForRematch => 2;

        public IReadOnlyList<string> TrialAchievements { get; } = ["prueba-01", "prueba-02"];

        public string LeagueAchievement => "alto-mando-campeon";

        public string RematchAchievement => "alto-mando-otra-vez";

        public int TopBaseStatTotal => 540;

        public int Divisions => 12;

        public int MinCandidates => minCandidates;
    }

    private sealed class Achievements : IAchievementCatalog
    {
        public IReadOnlyList<Achievement> All { get; } =
        [
            new("prueba-01", "Primera", "", null, "sin disparador", 1, 100, Item: 807),
            new("prueba-02", "Segunda", "", null, "sin disparador", 1, 100, Item: 813),
            new("alto-mando-campeon", "Campeón", "", null, "sin disparador", 1, 300, Item: 900),
            new("alto-mando-otra-vez", "Otra vez", "", null, "sin disparador", 1, 300, Item: 901)
        ];
    }

    private sealed class Records(params int[] itemsHeld) : IGameRecords
    {
        public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult(new GameRecordSnapshot(true, null, null, new Dictionary<int, int>(), DateTimeOffset.UnixEpoch,
                itemsHeld.ToHashSet(), new Dictionary<int, int>()));
    }

    private static (NurseryService Service, Events Log, Repository Team) BuildNursery(int[] held, int minCandidates = 1)
    {
        var log = new Events();
        var clock = new Clock();
        var team = new Repository();
        var achievements = new AchievementService(new Achievements(), new PointsService(log, clock), log, clock,
            new Records(held), new FixedRole(FixedRole.Normal));

        return (new NurseryService(new NurseryCatalog(minCandidates), new Species(Table()), achievements, Rule(team), log, team, clock),
            log, team);
    }

    [Fact]
    public async Task The_nursery_owes_what_the_roulette_would_and_spends_it_egg_by_egg()
    {
        var (service, log, _) = BuildNursery([807, 813, 900, 901]);
        var run = FireRun();

        // Dos pruebas a una, la liga a tres y el rematch a dos.
        Assert.Equal(7, await service.EarnedAsync(run));
        Assert.Equal(7, await service.OwedAsync(run));
        Assert.Equal(2, await service.ProgressAsync(run));
        Assert.True(service.PlaysWithTheNursery(run));
        Assert.False(service.PlaysWithTheNursery(FireRun("normal")));

        var eggs = await service.PrepareAsync(run, 7);
        Assert.Equal(7, eggs.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], eggs.Select(e => e.Number));
        Assert.Empty(log.Appended);                      // preparar no gasta nada

        var delivered = new DeliveryResult(DeliveryOutcome.Delivered, "ok", 1, 1, 0xCAFEBABE);
        var entry = await service.RecordAsync(run, eggs[0], delivered, progress: 2);

        Assert.Equal(PokemonOrigin.Nursery, entry.Origin);
        Assert.Equal(0xCAFEBABEu, entry.Pid);
        Assert.Equal(1, entry.Level);

        var recorded = Assert.Single(log.Appended);
        Assert.Equal(GameEventType.NurseryEgg, recorded.Type);
        Assert.Equal(Fire.ToString(), recorded.Data["monotipo"]);
        Assert.Equal("CAFEBABE", recorded.Data["pid"]);
        Assert.Equal(6, await service.OwedAsync(run));

        // Un huevo no dice qué hay dentro: sin especie hasta que eclosiona, y entonces queda anotado.
        Assert.Equal(0, entry.Species);
        Assert.Equal("Huevo", entry.SpeciesName);
        var hatched = await service.HatchedAsync(run, 0xCAFEBABE, 126, "Magmar", 0, 5);
        Assert.Equal(126, hatched!.Species);
        Assert.Equal(GameEventType.NurseryHatch, log.Appended.Last().Type);
        Assert.Null(await service.HatchedAsync(run, 0xCAFEBABE, 126, "Magmar", 0, 5));

        // El siguiente huevo es el número 1: lo gastado sale del historial.
        Assert.Equal(1, (await service.PrepareAsync(run, 1))[0].Number);

        // Sin entrega no hay huevo apuntado ni tirada gastada.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordAsync(run, eggs[1], new DeliveryResult(DeliveryOutcome.GameRunning, "abierto"), 2));
        Assert.Equal(6, await service.OwedAsync(run));

        // Otro rol no tiene guardería.
        Assert.Empty(await service.PrepareAsync(FireRun("normal"), 3));
    }

    [Fact]
    public void The_eggs_aim_higher_with_every_trial_and_never_past_the_top()
    {
        var (service, _, _) = BuildNursery([]);

        // Fuego sin legendarios: 300, 350, 400, 534. Del más flojo, un doceavo (19) del camino al más fuerte por prueba.
        Assert.Equal(300, service.TargetFor(Fire, 0));
        Assert.Equal(357, service.TargetFor(Fire, 3));
        Assert.Equal(528, service.TargetFor(Fire, 12));
        Assert.Equal(540, service.TargetFor(Fire, 99));      // el tope
        Assert.Equal(0, service.TargetFor(18, 3));           // un tipo sin especies
    }

    [Fact]
    public void An_egg_is_of_the_type_never_legendary_and_level_one_and_comes_out_the_same_every_time()
    {
        var (service, _, _) = BuildNursery([]);

        for (var number = 0; number < 200; number++)
        {
            var egg = service.Preview(20261007, number, progress: number % 13, Fire)!;

            Assert.Contains(egg.Species, new[] { 1, 2, 3, 8 });     // el 7, el legendario de fuego, no
            Assert.Equal(1, egg.Level);
            Assert.False(egg.IsShiny);
            Assert.Equal(PermaLocke.Core.Services.NurseryService.Banner, egg.TierId);
            Assert.Equal(egg, service.Preview(20261007, number, number % 13, Fire));
        }

        // Con un candidato basta, el que está justo en el objetivo (300 sin pruebas) y el más cercano con la banda abierta (534).
        Assert.All(Enumerable.Range(0, 30), n => Assert.Equal(1, service.Preview(20261007, n, 0, Fire)!.Species));
        Assert.All(Enumerable.Range(0, 30), n => Assert.Equal(3, service.Preview(20261007, n, 12, Fire)!.Species));
    }

    [Fact]
    public void The_band_opens_one_percent_at_a_time_until_enough_species_fall_inside()
    {
        // Pidiendo cuatro candidatos, con el objetivo en 300 la banda tiene que abrirse hasta cubrir 350 y 400 y 534 (casi todo).
        var (service, _, _) = BuildNursery([], minCandidates: 3);

        var seen = Enumerable.Range(0, 300).Select(n => service.Preview(20261007, n, 0, Fire)!.Species).ToHashSet();

        Assert.True(seen.Count >= 2, $"Salieron solo {string.Join(",", seen)}");
        Assert.Contains(1, seen);
    }

    // ============================================== LO QUE SE TIENE ES LO QUE DICE LA PARTIDA, NO EL REGISTRO

    private sealed class Boxes(BoxSnapshot snapshot) : IBoxReader
    {
        public Task<BoxSnapshot> ReadAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    }

    private static BoxedPokemon Held(int species, string name, uint pid, string nickname = "", int box = 0, int form = 0) => new(
        box, 0, species, form, name, nickname, 10, false, false, "", "Firme", "Habilidad", "", "Poké Ball", "Grenin", "Ruta 1", 5,
        [], [], [], [], 70, pid);

    private static BoxSnapshot Save(params BoxedPokemon[] pokemon) => new(true, null, null,
        [new BoxContents(1, "Caja 1", pokemon)], 30, "Grenin", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task A_pokemon_that_was_released_stops_blocking_even_though_the_run_never_hears_of_it()
    {
        // El registro lo sigue dando por vivo (nada apunta una liberación); la partida ya no lo tiene.
        var wrong = Owned(4, "Hoja A");
        var repository = new Repository(wrong);
        var rule = new MonotypeRule(new Roles(), new Types(), new Species(Table()), repository,
            new Boxes(Save(Held(1, "Llama A", 0x1))));

        Assert.Empty(await rule.InvalidAsync(FireRun(), Fire));
    }

    [Fact]
    public async Task A_pokemon_the_save_holds_that_is_not_of_the_type_blocks_with_the_name_the_player_gave_it()
    {
        var rule = new MonotypeRule(new Roles(), new Types(), new Species(Table()), new Repository(),
            new Boxes(Save(Held(1, "Llama A", 0x1), Held(4, "Hoja A", 0x2, nickname: "Pepe"), Held(6, "Roca", 0x3))));

        var invalid = await rule.InvalidAsync(FireRun(), Fire);

        Assert.Equal(["Pepe", "Roca"], invalid);
        Assert.Contains("Pepe, Roca", rule.BlockedMessage(FireRun(), invalid));
        Assert.Contains("guarda la partida", rule.BlockedMessage(FireRun(), invalid));
    }

    [Fact]
    public async Task A_fallen_one_still_in_a_box_and_a_damaged_entry_do_not_block()
    {
        var corpse = Owned(4, "Hoja A", PokemonStatus.Dead) with { Pid = 0x2 };
        var damaged = Held(5, "Basura", 0x9) with { IsIntact = false };
        var rule = new MonotypeRule(new Roles(), new Types(), new Species(Table()), new Repository(corpse),
            new Boxes(Save(Held(4, "Hoja A", 0x2), damaged)));

        Assert.Empty(await rule.InvalidAsync(FireRun(), Fire));
    }

    [Fact]
    public async Task A_form_is_judged_by_its_own_types_and_an_egg_by_its_species()
    {
        var rule = new MonotypeRule(new Roles(), new Types(), new Species(Table()), new Repository(),
            new Boxes(Save(Held(8, "Ponyta", 0x1, form: 1), Held(1, "Llama A", 0x2) with { IsEgg = true })));

        // La Ponyta de Galar es Psíquica: en una run de fuego no vale. El huevo de una especie de fuego sí.
        Assert.Equal(["Ponyta"], await rule.InvalidAsync(FireRun(), Fire));
    }

    [Fact]
    public async Task Without_a_readable_save_it_falls_back_on_the_registry()
    {
        var repository = new Repository(Owned(4, "Hoja A"));
        var unavailable = new Boxes(BoxSnapshot.Unavailable("sin partida", DateTimeOffset.UnixEpoch));
        var rule = new MonotypeRule(new Roles(), new Types(), new Species(Table()), repository, unavailable);

        Assert.Equal(["Hoja A"], await rule.InvalidAsync(FireRun(), Fire));
    }
}
