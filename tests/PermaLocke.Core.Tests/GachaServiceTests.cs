using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Guards the gacha: that the odds are the ones configured, that a roll is reproducible from the
/// run seed, and that nothing is charged for a Pokémon that was not produced.
/// </summary>
public sealed class GachaServiceTests
{
    private sealed class Catalog(IReadOnlyList<GachaTier> tiers, IReadOnlyList<GachaBanner> banners)
        : IGachaCatalog
    {
        public IReadOnlyList<GachaTier> Tiers => tiers;

        public IReadOnlyList<GachaBanner> Banners => banners;
    }

    private sealed class Species(IReadOnlyList<SpeciesStats> all) : ISpeciesStatsCatalog
    {
        public IReadOnlyList<SpeciesStats> All => all;

        public IReadOnlyList<string> Natures { get; } = [.. Enumerable.Range(0, 25).Select(n => $"Naturaleza {n}")];

        public IReadOnlyList<string> Abilities { get; } = ["Levitación", "Impostor", "Presión"];
    }

    /// <summary>The five bands the run defines, by base stat total.</summary>
    private static GachaTier[] Tiers() =>
    [
        new("tier1", "Tier 1", 400, 0.0, 5, 5, 0, 0),
        new("tier2", "Tier 2", 490, 0.0, 5, 5, 0, 0),
        new("tier3", "Tier 3", 535, 0.0, 5, 5, 0, 0),
        new("tier4", "Tier 4", 590, 0.0, 5, 5, 0, 0),
        new("tier5", "Tier 5", 9999, 0.40, 5, 5, 0, 0)
    ];

    /// <summary>Two species per band, one of them legendary in the top one.</summary>
    private static SpeciesStats[] SpeciesTable() =>
    [
        new(1, "Flojo A", 300, false, ["Habilidad A", "Habilidad B"]),
        new(2, "Flojo B", 400, false, ["Habilidad A", "Habilidad B"]),
        new(3, "Medio A", 450, false, ["Habilidad A", "Habilidad B"]),
        new(4, "Medio B", 490, false, ["Habilidad A", "Habilidad B"]),
        new(5, "Bueno A", 500, false, ["Habilidad A", "Habilidad B"]),
        new(6, "Bueno B", 535, false, ["Habilidad A", "Habilidad B"]),
        new(7, "Muy bueno A", 560, false, ["Habilidad A", "Habilidad B"]),
        new(8, "Muy bueno B", 590, false, ["Habilidad A", "Habilidad B"]),
        new(9, "Pseudo", 600, false, ["Habilidad A", "Habilidad B"]),
        new(10, "Legendario", 680, true, ["Habilidad A", "Habilidad B"])
    ];

    private static GachaBanner Pocho() =>
        new("pocho", "POCHO", string.Empty, 100,
            new Dictionary<string, double> { ["tier1"] = 0.15, ["tier2"] = 0.60, ["tier3"] = 0.25 });

    private static GachaBanner Bueno() =>
        new("bueno", "BUENO", string.Empty, 300,
            new Dictionary<string, double> { ["tier3"] = 0.15, ["tier4"] = 0.60, ["tier5"] = 0.25 });

    private static GachaService Build() =>
        new(new Catalog(Tiers(), [Pocho(), Bueno()]), new Species(SpeciesTable()),
            null!, null!, null!, null!);

    /// <summary>Every species falls in exactly one band, and the bands do not overlap.</summary>
    [Theory]
    [InlineData("tier1", 300, 400)]
    [InlineData("tier2", 450, 490)]
    [InlineData("tier3", 500, 535)]
    [InlineData("tier4", 560, 590)]
    public void Each_tier_draws_only_from_its_own_band(string tierId, int low, int high)
    {
        var service = Build();
        var tier = Tiers().Single(t => t.Id == tierId);

        var pool = service.PoolOf(tier, legendary: false);

        Assert.Equal(2, pool.Count);
        Assert.Contains(pool, s => s.BaseStatTotal == low);
        Assert.Contains(pool, s => s.BaseStatTotal == high);
    }

    /// <summary>
    /// Legendaries are kept out of the cheap tiers: their chance there is zero, and the pool is
    /// split by that flag, so no legendary can turn up in a hundred-point banner.
    /// </summary>
    [Fact]
    public void Only_the_top_tier_holds_legendaries()
    {
        var service = Build();

        foreach (var tier in Tiers().Where(t => t.Id != "tier5"))
        {
            Assert.Empty(service.PoolOf(tier, legendary: true));
        }

        var top = Tiers().Single(t => t.Id == "tier5");
        Assert.Single(service.PoolOf(top, legendary: true));
        Assert.Single(service.PoolOf(top, legendary: false));
    }

    /// <summary>The same run seed and roll number must give the same Pokémon, always.</summary>
    [Fact]
    public void A_roll_is_reproducible_from_the_run_seed()
    {
        var service = Build();

        var first = service.Preview(Pocho(), 20260819, 7);
        var again = service.Preview(Pocho(), 20260819, 7);

        Assert.NotNull(first);
        Assert.Equal(first, again);
    }

    [Fact]
    public void Different_rolls_of_the_same_run_differ()
    {
        var service = Build();
        var pulls = Enumerable.Range(0, 40)
            .Select(n => service.Preview(Bueno(), 20260819, n)!)
            .ToList();

        Assert.True(pulls.Select(p => p.Species).Distinct().Count() > 1,
            "cuarenta tiradas seguidas no pueden dar siempre la misma especie");
    }

    /// <summary>
    /// The odds the run asked for: 15/60/25. Measured over enough rolls that a real deviation
    /// shows up, with a tolerance wide enough not to fail on chance alone.
    /// </summary>
    [Fact]
    public void The_banner_respects_the_configured_odds()
    {
        var service = Build();
        const int Rolls = 20000;

        var counts = new Dictionary<string, int>();

        for (var n = 0; n < Rolls; n++)
        {
            var pull = service.Preview(Pocho(), 20260819, n)!;
            counts[pull.TierId] = counts.GetValueOrDefault(pull.TierId) + 1;
        }

        Assert.InRange(counts["tier1"] / (double)Rolls, 0.13, 0.17);
        Assert.InRange(counts["tier2"] / (double)Rolls, 0.575, 0.625);
        Assert.InRange(counts["tier3"] / (double)Rolls, 0.23, 0.27);
    }

    /// <summary>Tier 5 gives a legendary 40% of the time, which is what the run specified.</summary>
    [Fact]
    public void The_top_tier_is_legendary_forty_percent_of_the_time()
    {
        var service = Build();
        const int Rolls = 20000;
        var legendary = 0;
        var fromTop = 0;

        for (var n = 0; n < Rolls; n++)
        {
            var pull = service.Preview(Bueno(), 20260819, n)!;

            if (pull.TierId != "tier5")
            {
                continue;
            }

            fromTop++;

            if (pull.Legendary)
            {
                legendary++;
            }
        }

        Assert.True(fromTop > 4000, $"el tier 5 debería salir un 25% de las veces, salió {fromTop}");
        Assert.InRange(legendary / (double)fromTop, 0.37, 0.43);
    }

    [Fact]
    public void A_banner_with_no_species_to_offer_produces_nothing()
    {
        var service = new GachaService(new Catalog(Tiers(), [Pocho()]), new Species([]),
            null!, null!, null!, null!);

        Assert.Null(service.Preview(Pocho(), 1, 0));
    }

    /// <summary>
    /// A free banner must roll. Charging zero is not charging, and the points service rightly
    /// refuses to spend nothing — so the gacha has to skip the charge, not ask for it.
    /// </summary>
    [Fact]
    public async Task A_banner_that_costs_nothing_still_rolls()
    {
        var events = new RecordingEvents();
        var free = new GachaBanner("gratis", "GRATIS", string.Empty, 0,
            new Dictionary<string, double> { ["tier1"] = 1.0 });

        var service = new GachaService(new Catalog(Tiers(), [free]), new Species(SpeciesTable()),
            new ZeroPoints(), events, new NoRepository(), new FixedClock());

        var result = await service.RollAsync(SampleRun(), "gratis");

        Assert.True(result.Success);
        Assert.NotNull(result.Pull);
        Assert.Equal(0, result.Balance);

        // Un solo evento, el de la tirada: no hay gasto que registrar.
        var recorded = Assert.Single(events.Appended);
        Assert.Equal(GameEventType.GachaRoll, recorded.Type);
    }

    private sealed class ZeroPoints : IPointsService
    {
        public Task<int> GetBalanceAsync(Guid runId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<PointsResult> EarnAsync(Guid runId, int amount, string description,
            EventSource source, string actor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PointsResult> SpendAsync(Guid runId, int amount, string description,
            EventSource source, string actor, CancellationToken ct = default) =>
            throw new InvalidOperationException("una tirada gratis no debe intentar gastar");

        public Task<PointsResult> AdjustAsync(Guid runId, int delta, string reason, string adminName,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoRepository : IPokemonRepository
    {
        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([]);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult<PokemonEntry?>(null);

        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default) => Task.CompletedTask;
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
        public DateTimeOffset Now => new(2026, 8, 20, 1, 0, 0, TimeSpan.Zero);
    }

    private static Run SampleRun() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Prueba",
        Game = GameVersion.UltraMoon,
        SeedLabel = "20260820",
        Seed = 20260820,
        RoleId = "player",
        PlayerName = "Grenin"
    };
}
