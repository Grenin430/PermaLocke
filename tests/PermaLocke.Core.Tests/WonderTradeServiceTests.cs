using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Guards the wonder trade: that what comes back really is inside the band, that a trade is
/// reproducible from the run seed, and that the level cannot be laundered upwards.
/// </summary>
public sealed class WonderTradeServiceTests
{
    private sealed class Catalog(WonderTradeWindow window) : IWonderTradeCatalog
    {
        public WonderTradeWindow Window => window;
    }

    private sealed class Species(IReadOnlyList<SpeciesStats> all) : ISpeciesStatsCatalog
    {
        public IReadOnlyList<SpeciesStats> All => all;

        public IReadOnlyList<string> Natures { get; } =
            [.. Enumerable.Range(0, 25).Select(n => $"Naturaleza {n}")];

        public IReadOnlyList<string> Abilities { get; } = ["", "Levitación", "Impostor", "Presión"];

        /// <summary>Una familia por especie: el wonder trade no reparte lineas, reparte especies.</summary>
        public IReadOnlyList<EvolutionLine> Lines =>
            [.. all.Select(s => new EvolutionLine([new[] { s.Id }]))];
    }

    private sealed class Types : ITypeLookup
    {
        public TypePair GetTypes(int species) => new(0, "Normal", 0, "Normal");

        public string GetName(int type) => "Normal";
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

    private sealed class NoRepository : IPokemonRepository
    {
        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([]);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult<PokemonEntry?>(null);

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>

            throw new NotSupportedException();


        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 21, 1, 0, 0, TimeSpan.Zero);
    }

    /// <summary>A spread of totals from 200 to 700, so any band has neighbours and outsiders.</summary>
    private static SpeciesStats[] Catalogue() =>
    [
        .. Enumerable.Range(0, 51).Select(i =>
            new SpeciesStats(i + 1, $"Especie {i + 1}", 200 + (i * 10), i == 50, ["Levitación"]))
    ];

    /// <summary>Keeps what it is given, so a status change can be looked at afterwards.</summary>
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

    private static PokemonEntry Living(uint pid, string name = "Vanilluxe", int species = 584) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Species = species,
        SpeciesName = name,
        Origin = PokemonOrigin.Gacha,
        EncounterType = EncounterType.Special,
        Pid = pid
    };

    private static WonderTradeService Build(double below = 0.08, double above = 0.10,
        bool legendaries = true, Events? events = null, IPokemonRepository? repository = null) =>
        new(new Catalog(new WonderTradeWindow(below, above, legendaries)),
            new Species(Catalogue()), new Types(), events ?? new Events(),
            repository ?? new NoRepository(), new FixedClock());

    private static Run SampleRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260821",
        Seed = 20260821,
        RoleId = "player",
        PlayerName = "Grenin"
    };

    /// <summary>-8% and +10% of 500 is 460 to 550, and those are the ends it must use.</summary>
    [Fact]
    public void The_band_is_the_one_configured()
    {
        var (min, max) = new WonderTradeWindow(0.08, 0.10, true).Band(500);

        Assert.Equal(460, min);
        Assert.Equal(550, max);
    }

    /// <summary>
    /// The band the competition plays with, -8% / +20%, which is asymmetric on purpose and has to
    /// stay that way: the species population thins out sharply above 550, so a symmetric band
    /// hands back something worse nearly every time.
    /// </summary>
    [Theory]
    [InlineData(500, 460, 600)]
    [InlineData(600, 552, 720)]
    [InlineData(460, 423, 552)]
    public void The_wide_band_reaches_further_up_than_down(int given, int min, int max)
    {
        var band = new WonderTradeWindow(0.08, 0.20, true).Band(given);

        Assert.Equal(min, band.Min);
        Assert.Equal(max, band.Max);
        Assert.True(band.Max - given > given - band.Min);
    }

    /// <summary>
    /// The whole mechanic in one assertion: whatever comes back is worth about what went in.
    /// Checked over many trades because a single one could land inside the band by luck.
    /// </summary>
    [Fact]
    public void Everything_that_comes_back_is_inside_the_band()
    {
        var service = Build();
        var gift = new WonderTradeGift(31, "Especie 31", 24, 0, 0);   // total 500
        var (min, max) = service.Window.Band(500);

        for (var number = 0; number < 200; number++)
        {
            var offer = service.Preview(gift, 20260821, number);

            Assert.NotNull(offer);
            Assert.InRange(offer.BaseStatTotal, min, max);
            Assert.Equal(min, offer.MinBaseStatTotal);
            Assert.Equal(max, offer.MaxBaseStatTotal);
        }
    }

    /// <summary>
    /// The ability is drawn from the whole game, not from the species' own list.
    /// </summary>
    /// <remarks>
    /// Every species in this fixture declares exactly one ability, «Levitación», so the old
    /// behaviour — draw from what the species has — could only ever produce that one. It is the
    /// same shape as what the player hit in the real run: a Victini with Victory Star, a Togekiss
    /// with Serene Grace, a Heracross with its own hidden ability. With abilities left alone in the
    /// ROM, every trade handed back exactly what that Pokémon already is, which is the opposite of
    /// what a wonder trade is for.
    /// </remarks>
    [Fact]
    public void The_ability_is_drawn_from_the_whole_game_and_not_from_the_species()
    {
        var service = Build();
        var gift = new WonderTradeGift(31, "Especie 31", 24, 0, 0);

        var seen = Enumerable.Range(0, 200)
            .Select(number => service.Preview(gift, 20260821, number))
            .Where(offer => offer is not null)
            .Select(offer => offer!.Ability)
            .Distinct()
            .ToList();

        Assert.Contains("Impostor", seen);
        Assert.Contains("Presión", seen);
    }

    /// <summary>
    /// The received Pokémon keeps the level of the one handed over. Without this the trade would
    /// be a laundry: hand over a level 1 from the gacha, get a level 50 back.
    /// </summary>
    [Fact]
    public void The_level_of_what_is_handed_over_is_the_level_of_what_comes_back()
    {
        var service = Build();

        foreach (var level in new[] { 1, 24, 100 })
        {
            var offer = service.Preview(new WonderTradeGift(31, "Especie 31", level, 0, 0), 20260821, 0);
            Assert.Equal(level, offer!.Level);
        }
    }

    /// <summary>Same seed, same trade number, same Pokémon: that is what makes it auditable.</summary>
    [Fact]
    public void A_trade_can_be_recomputed_from_the_run_seed()
    {
        var service = Build();
        var gift = new WonderTradeGift(31, "Especie 31", 24, 0, 0);

        var first = service.Preview(gift, 20260821, 7);
        var again = service.Preview(gift, 20260821, 7);
        var other = service.Preview(gift, 20260821, 8);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    /// <summary>Turning legendaries off has to actually keep them out of the pool.</summary>
    [Fact]
    public void Legendaries_can_be_kept_out()
    {
        // La especie 51 vale 700 y es legendaria; con la banda abierta es la única candidata.
        var withThem = Build();
        var without = Build(legendaries: false);
        var gift = new WonderTradeGift(51, "Especie 51", 50, 0, 0);

        Assert.Contains(withThem.PoolFor(700), s => s.Legendary);
        Assert.DoesNotContain(without.PoolFor(700), s => s.Legendary);
        Assert.All(without.PoolFor(700), s => Assert.False(s.Legendary));
    }

    /// <summary>A species the catalogue does not know is refused, not guessed at.</summary>
    [Fact]
    public async Task An_unknown_species_is_refused_with_a_reason()
    {
        var result = await Build().TradeAsync(SampleRun(), new WonderTradeGift(999, "Fantasma", 5, 0, 0));

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Null(result.Offer);
    }

    /// <summary>The trade is written to the log with both sides and the band, or it is not auditable.</summary>
    [Fact]
    public async Task The_event_records_both_sides_and_the_band()
    {
        var events = new Events();
        var result = await Build(events: events)
            .TradeAsync(SampleRun(), new WonderTradeGift(31, "Especie 31", 24, 2, 9));

        Assert.True(result.Success);
        var recorded = Assert.Single(events.Appended);
        Assert.Equal(GameEventType.WonderTrade, recorded.Type);
        Assert.Equal("31", recorded.Data["entregado"]);
        Assert.Equal("500", recorded.Data["entregadoTotal"]);
        Assert.Equal(result.Offer!.Species.ToString(), recorded.Data["recibido"]);
        Assert.Equal("460-550", recorded.Data["banda"]);
    }

    /// <summary>Consecutive trades of the same run must not repeat the same draw.</summary>
    [Fact]
    public async Task The_trade_number_advances_with_every_trade()
    {
        var events = new Events();
        var service = Build(events: events);
        var run = SampleRun();

        var first = await service.TradeAsync(run, new WonderTradeGift(31, "Especie 31", 24, 0, 0));
        var second = await service.TradeAsync(run, new WonderTradeGift(31, "Especie 31", 24, 0, 1));

        Assert.Equal(0, first.Offer!.Number);
        Assert.Equal(1, second.Offer!.Number);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(151, 1)]
    [InlineData(152, 2)]
    [InlineData(386, 3)]
    [InlineData(494, 5)]
    [InlineData(650, 6)]
    [InlineData(722, 7)]
    [InlineData(807, 7)]
    public void Generations_are_the_national_dex_blocks(int species, int generation) =>
        Assert.Equal(generation, Generations.Of(species));

    /// <summary>
    /// The one that leaves stops being alive. Recording only the arrival is how a run ends up
    /// counting twenty-eight Pokémon that are not in the game.
    /// </summary>
    [Fact]
    public async Task The_one_handed_over_is_marked_as_traded()
    {
        var given = Living(0xAABBCCDD);
        var repository = new Repository(given, Living(0x11223344, "Politoed", 186));
        var events = new Events();
        var trades = Build(events: events, repository: repository);

        var marked = await trades.MarkGivenAsTradedAsync(SampleRun(), 0xAABBCCDD, "Kommo-o");

        Assert.Equal(PokemonStatus.Traded, marked?.Status);
        Assert.Equal(PokemonStatus.Traded, repository.Entries.Single(p => p.Id == given.Id).Status);

        // El otro sigue vivo: se marca a quien se fue, no a la caja entera.
        Assert.Equal(PokemonStatus.Alive, repository.Entries.Single(p => p.Pid == 0x11223344).Status);

        var recorded = Assert.Single(events.Appended);
        Assert.Equal(GameEventType.PokemonTraded, recorded.Type);
        Assert.Equal("AABBCCDD", recorded.Data["pid"]);
        Assert.Contains("Kommo-o", recorded.Description);
    }

    /// <summary>
    /// Zero is what a Pokémon with no recorded identity has, and there are plenty of those. Acting
    /// on it would mark whichever one happened to come first.
    /// </summary>
    [Fact]
    public async Task A_pid_of_zero_marks_nobody()
    {
        var repository = new Repository(Living(0) with { Pid = 0 });
        var events = new Events();
        var trades = Build(events: events, repository: repository);

        Assert.Null(await trades.MarkGivenAsTradedAsync(SampleRun(), 0, "Kommo-o"));
        Assert.Equal(PokemonStatus.Alive, repository.Entries.Single().Status);
        Assert.Empty(events.Appended);
    }

    /// <summary>A Pokémon the run never registered is not an error, and not something to invent.</summary>
    [Fact]
    public async Task An_unknown_pid_changes_nothing()
    {
        var repository = new Repository(Living(0xAABBCCDD));
        var events = new Events();
        var trades = Build(events: events, repository: repository);

        Assert.Null(await trades.MarkGivenAsTradedAsync(SampleRun(), 0xDEADBEEF, "Kommo-o"));
        Assert.Equal(PokemonStatus.Alive, repository.Entries.Single().Status);
        Assert.Empty(events.Appended);
    }

    /// <summary>Marking the same one twice would write a second departure for one Pokémon.</summary>
    [Fact]
    public async Task Marking_twice_only_records_once()
    {
        var repository = new Repository(Living(0xAABBCCDD));
        var events = new Events();
        var trades = Build(events: events, repository: repository);

        await trades.MarkGivenAsTradedAsync(SampleRun(), 0xAABBCCDD, "Kommo-o");
        Assert.Null(await trades.MarkGivenAsTradedAsync(SampleRun(), 0xAABBCCDD, "Kommo-o"));

        Assert.Single(events.Appended);
    }
}
