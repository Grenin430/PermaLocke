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

        /// <summary>Sólo dos filas: el principio y el final. Es lo que hace falta para probar.</summary>
        public IReadOnlyList<StageOdds> StageOdds { get; init; } =
            [new StageOdds(0, 0, 0), new StageOdds(12, 45, 20)];
    }

    private sealed class Species(IReadOnlyList<SpeciesStats> all) : ISpeciesStatsCatalog
    {
        public IReadOnlyList<SpeciesStats> All => all;

        public IReadOnlyList<string> Natures { get; } = [.. Enumerable.Range(0, 25).Select(n => $"Naturaleza {n}")];

        public IReadOnlyList<string> Abilities { get; init; } = ["Levitación", "Impostor", "Presión"];

        public IReadOnlyList<EvolutionLine> Lines { get; init; } = [];

        public IReadOnlyCollection<int> BannedAbilities { get; init; } = [];
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

        // Las dos primeras etapas del pseudolegendario. Existen para que el tier 5 pueda entregar
        // un 210: la banda habla de en qué acaba la línea, no de lo que te dan.
        new(12, "Pseudo bebé", 210, false, ["Habilidad A", "Habilidad B"]),
        new(13, "Pseudo medio", 330, false, ["Habilidad A", "Habilidad B"]),
        new(10, "Legendario", 680, true, ["Habilidad A", "Habilidad B"]),

        // Un legendario POR DEBAJO del techo del tier 4. Está aquí porque era imposible que
        // saliera: el saco de legendarios se recortaba por banda igual que los demás, así que las
        // aves, los perros, los Tapu, los regis o Type: Null no podían tocar nunca, y nada lo decía.
        new(11, "Legendario flojo", 580, true, ["Habilidad A", "Habilidad B"])
    ];

    private static GachaBanner Pocho() =>
        new("pocho", "POCHO", string.Empty, 100,
            new Dictionary<string, double> { ["tier1"] = 0.15, ["tier2"] = 0.60, ["tier3"] = 0.25 });

    private static GachaBanner Bueno() =>
        new("bueno", "BUENO", string.Empty, 300,
            new Dictionary<string, double> { ["tier3"] = 0.15, ["tier4"] = 0.60, ["tier5"] = 0.25 });

    /// <summary>
    /// The families, which is what a tier is really a band of.
    /// </summary>
    /// <remarks>
    /// Shaped like the cartridge's: a three-stage line ending in each band, a shorter one beside
    /// it, and one that does not evolve at all. The tier of a family is decided by the total of its
    /// LAST rung, so «Flojo A 300» sits in tier 1 through what it becomes and not through what it
    /// is. Ids match <see cref="SpeciesTable"/>.
    /// </remarks>
    private static EvolutionLine[] Families() =>
    [
        new([[1], [2]]),            // 300 -> 400, acaba en tier 1
        new([[3], [4]]),            // 450 -> 490, acaba en tier 2
        new([[5], [6]]),            // 500 -> 535, acaba en tier 3
        new([[7], [8]]),            // 560 -> 590, acaba en tier 4
        new([[12], [13], [9]]),     // 210 -> 330 -> 600: la forma del pseudolegendario
        new([[10]]),                // el legendario de 680, que no evoluciona
        new([[11]])                 // y el legendario flojo de 580
    ];

    private static GachaService Build(IReadOnlyList<StageOdds>? odds = null) =>
        new(new Catalog(Tiers(), [Pocho(), Bueno()])
            {
                StageOdds = odds ?? [new StageOdds(0, 0, 0), new StageOdds(12, 45, 20)]
            },
            new Species(SpeciesTable()) { Lines = Families() },
            null!, null!, null!, null!);

    /// <summary>
    /// A tier is a band of <b>endings</b>: every family in it finishes inside the band.
    /// </summary>
    /// <remarks>
    /// The lower bound is the previous tier's ceiling, so a family belongs to exactly one tier —
    /// the same guarantee the old species-by-species version gave, moved up a level.
    /// </remarks>
    [Theory]
    [InlineData("tier1", 0, 400)]
    [InlineData("tier2", 400, 490)]
    [InlineData("tier3", 490, 535)]
    [InlineData("tier4", 535, 590)]
    [InlineData("tier5", 590, 9999)]
    public void A_tier_holds_the_families_that_END_inside_its_band(string tierId, int floor, int roof)
    {
        var service = Build();
        var tier = Tiers().Single(t => t.Id == tierId);
        var table = SpeciesTable().ToDictionary(s => s.Id);

        var lines = service.LinesOf(tier, legendary: false);

        Assert.NotEmpty(lines);

        foreach (var line in lines)
        {
            var end = line.Stages[^1].Max(id => table[id].BaseStatTotal);
            Assert.InRange(end, floor + 1, roof);
        }
    }

    /// <summary>
    /// What a tier hands over is graded by where the family ends, not by what is handed over.
    /// </summary>
    /// <remarks>
    /// This is the whole point of the change, so it is pinned with the awkward case: the top tier
    /// can produce a 210 — the first rung of the family that reaches 600 — and that is correct.
    /// Under the old rule a 210 was a tier one and the top tier only ever gave finished Pokémon.
    /// </remarks>
    /// <summary>
    /// The album's rarity (§186): any species gets the tier of the band its family ENDS in, the same the banner sells
    /// it from, so the baby of the pseudo-legendary is a tier five and the first rung of a weak line a tier one.
    /// </summary>
    [Theory]
    [InlineData(1, "tier1")]
    [InlineData(2, "tier1")]
    [InlineData(3, "tier2")]
    [InlineData(5, "tier3")]
    [InlineData(8, "tier4")]
    [InlineData(12, "tier5")]
    [InlineData(13, "tier5")]
    [InlineData(9, "tier5")]
    public void Every_species_has_the_tier_its_family_ends_in(int species, string tierId)
    {
        Assert.Equal(tierId, Build().TierOf(species)?.Id);
    }

    /// <summary>A legendary is the tier that deals legendaries, even one whose total sits in a cheaper band.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void A_legendary_is_always_the_tier_that_deals_them(int species)
    {
        var service = Build();

        Assert.Equal("tier5", service.TierOf(species)?.Id);
        Assert.Equal(4, service.TierIndexOf(species));
    }

    /// <summary>
    /// A species outside every family is graded on its own total; one nobody knows has no tier at all, and the
    /// album shows no rarity instead of an invented one.
    /// </summary>
    [Fact]
    public void Without_a_family_the_species_is_graded_alone_and_unknown_ones_have_no_tier()
    {
        var service = new GachaService(new Catalog(Tiers(), [Pocho()]),
            new Species([.. SpeciesTable(), new SpeciesStats(20, "Suelto", 480, false, ["Habilidad A"])]) { Lines = Families() },
            null!, null!, null!, null!);

        Assert.Equal("tier2", service.TierOf(20)?.Id);
        Assert.Null(service.TierOf(999));
        Assert.Equal(-1, service.TierIndexOf(999));
    }

    [Fact]
    public void The_top_tier_can_hand_over_the_weakest_species_in_the_game()
    {
        var service = Build();
        var top = Tiers().Single(t => t.Id == "tier5");

        var pool = service.PoolOf(top, legendary: false);

        Assert.Contains(pool, s => s.BaseStatTotal == 210);
        Assert.Contains(pool, s => s.BaseStatTotal == 600);
    }

    /// <summary>
    /// A legendary can only come out of the top tier, whatever its base stat total is.
    /// </summary>
    /// <remarks>
    /// What keeps them out of the cheap banners is the <b>flag</b>, not the band: the ordinary pool
    /// of every tier -- the top one included -- excludes legendaries outright, and only the top
    /// tier carries a non-zero <c>legendaryChance</c>. That is what this pins, and it is what makes
    /// it safe for the legendary pool to ignore the bands entirely.
    /// </remarks>
    [Fact]
    public void A_legendary_can_only_come_out_of_the_top_tier()
    {
        var service = Build();

        foreach (var tier in Tiers().Where(t => t.Id != "tier5"))
        {
            Assert.Equal(0.0, tier.LegendaryChance);
            Assert.DoesNotContain(service.PoolOf(tier, legendary: false), s => s.Legendary);

            // Y su saco de legendarios está vacío, que es lo que la lista de «quién puede salir»
            // lee: un tier que no los reparte no debe nombrarlos.
            Assert.Empty(service.PoolOf(tier, legendary: true));
        }

        var top = Tiers().Single(t => t.Id == "tier5");
        Assert.DoesNotContain(service.PoolOf(top, legendary: false), s => s.Legendary);
    }

    /// <summary>
    /// The top tier draws from <b>every</b> legendary, not only the ones above its floor.
    /// </summary>
    /// <remarks>
    /// The legendary pool used to be cut by band like any other, which quietly made every legendary
    /// under 590 unobtainable. Nothing announced it: the roll worked, it just could never land on
    /// them.
    /// </remarks>
    [Fact]
    public void The_top_tier_draws_from_every_legendary_in_the_game()
    {
        var service = Build();
        var top = Tiers().Single(t => t.Id == "tier5");

        var pool = service.PoolOf(top, legendary: true);

        Assert.Equal(2, pool.Count);
        Assert.All(pool, s => Assert.True(s.Legendary));
        Assert.Contains(pool, s => s.BaseStatTotal == 680);
        Assert.Contains(pool, s => s.BaseStatTotal == 580);
    }

    /// <summary>
    /// With nothing cleared, every roll is the first rung of its family.
    /// </summary>
    /// <remarks>
    /// The start of a run is the case the table's zeroes describe, and it is also what a missing or
    /// broken table falls back to — so this is testing the safe default as much as the rule.
    /// </remarks>
    [Fact]
    public void At_the_start_of_the_run_only_first_stages_come_out()
    {
        var service = Build();
        var firsts = Families().Select(line => line.Stages[0][0]).ToHashSet();

        for (var n = 0; n < 400; n++)
        {
            var pull = service.Preview(Bueno(), 20260907, n, cleared: 0);

            Assert.NotNull(pull);
            Assert.Contains(pull.Species, firsts);
        }
    }

    /// <summary>
    /// Once the run is far enough along, the later rungs turn up at the configured rate.
    /// </summary>
    /// <remarks>
    /// Only the three-stage family can show a third rung, so this counts within it. The margin is
    /// wide because the point is that the table is being read at all, not that a thousand rolls
    /// land on the nose.
    /// </remarks>
    [Fact]
    public void Further_along_the_run_the_later_stages_appear()
    {
        var service = Build();

        int first = 0, second = 0, final = 0;

        for (var n = 0; n < 4000; n++)
        {
            var pull = service.Preview(Bueno(), 20260907, n, cleared: 12)!;

            if (pull.Species == 12) { first++; }
            if (pull.Species == 13) { second++; }
            if (pull.Species == 9) { final++; }
        }

        var inFamily = first + second + final;
        Assert.True(inFamily > 200, $"la familia de tres etapas salió {inFamily} veces");

        Assert.InRange(second / (double)inFamily, 0.35, 0.55);
        Assert.InRange(final / (double)inFamily, 0.12, 0.28);
        Assert.InRange(first / (double)inFamily, 0.25, 0.45);
    }

    /// <summary>
    /// A family shorter than the rung asked for gives its last one, not nothing.
    /// </summary>
    /// <remarks>
    /// Without the clamp the short families would quietly refuse their share of the late rolls, and
    /// the rarest outcome of every tier would land only on the longest lines. Farfetch'd is always
    /// Farfetch'd.
    /// </remarks>
    [Fact]
    public void A_family_with_no_third_rung_gives_its_last_one()
    {
        var service = Build();
        var top = Tiers().Single(t => t.Id == "tier5");

        var single = service.LinesOf(top, legendary: true)[0];

        Assert.Equal(single.Stages[^1], single.StageAt(2));
        Assert.Equal(single.Stages[^1], single.StageAt(9));
    }

    /// <summary>
    /// The same seed and number give the same Pokémon only for the same progress.
    /// </summary>
    /// <remarks>
    /// Which is why the event carries the stages cleared: without it, clearing a trial would make
    /// every past roll recompute into something else and an audit would call an honest roll a lie.
    /// </remarks>
    [Fact]
    public void A_roll_is_reproducible_for_the_progress_it_was_made_at()
    {
        var service = Build();

        Assert.Equal(service.Preview(Bueno(), 20260907, 3, cleared: 0),
            service.Preview(Bueno(), 20260907, 3, cleared: 0));

        Assert.Equal(service.Preview(Bueno(), 20260907, 3, cleared: 12),
            service.Preview(Bueno(), 20260907, 3, cleared: 12));
    }

    /// <summary>The odds table applies the highest row at or below the progress.</summary>
    [Fact]
    public void The_odds_are_the_last_row_the_run_has_reached()
    {
        var service = Build([new StageOdds(0, 0, 0), new StageOdds(4, 12, 4), new StageOdds(9, 32, 13)]);

        Assert.Equal(0, service.OddsAt(3).Second);
        Assert.Equal(12, service.OddsAt(4).Second);
        Assert.Equal(12, service.OddsAt(8).Second);
        Assert.Equal(32, service.OddsAt(9).Second);

        // Y una run mas adelantada que la ultima fila se queda en la ultima fila.
        Assert.Equal(32, service.OddsAt(40).Second);

        // Lo que no es segunda ni final es la primera, que es lo que hace que una tabla vacia
        // signifique «siempre la forma base» en vez de un fallo.
        Assert.Equal(55, service.OddsAt(9).First);
        Assert.Equal(100, service.OddsAt(0).First);
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

    /// <summary>
    /// The expansion mod's abilities are dealt; its holes and the banned ones never are.
    /// </summary>
    /// <remarks>
    /// Until §136 this test demanded the opposite — nothing above 233 — on the belief that a gen 7
    /// Pokémon keeps its ability in one byte, so 293 «General Supremo» came out as 37 «Potencia».
    /// That was the builder writing only the byte; the mod keeps a ninth bit (§134) and the builder
    /// now writes it. What still must never come out is an id with no name — the mod leaves 301-304
    /// and 317-318 as «-» — or one of the form abilities the randomizer also keeps out.
    /// </remarks>
    [Fact]
    public void The_mods_abilities_are_dealt_but_never_a_hole_or_a_banned_one()
    {
        // Una lista como la del mod 1.4: nombres hasta la 319, con sus huecos.
        int[] holes = [301, 302, 303, 304, 317, 318];
        var names = new List<string> { "-" };
        for (var id = 1; id < 320; id++)
        {
            names.Add(holes.Contains(id) ? "-" : $"Habilidad {id}");
        }

        int[] banned = [278, 279];
        var service = new GachaService(new Catalog(Tiers(), [Pocho()]),
            new Species(SpeciesTable()) { Abilities = names, Lines = Families(), BannedAbilities = banned },
            null!, null!, null!, null!);

        var fromTheMod = 0;

        for (var number = 0; number < 400; number++)
        {
            var pull = service.Preview(Pocho(), 1, number);

            Assert.NotNull(pull);
            Assert.DoesNotContain(pull.AbilityId, holes);
            Assert.DoesNotContain(pull.AbilityId, banned);
            Assert.Equal(names[pull.AbilityId], pull.Ability);

            if (pull.AbilityId > 233)
            {
                fromTheMod++;
            }
        }

        // Una de cada cuatro, más o menos: si volviera el tope de la 233, esto sería cero.
        Assert.True(fromTheMod > 50, $"solo {fromTheMod} de 400 con habilidad del mod");
    }

    /// <summary>
    /// Regional forms change the form and nothing else: every other number of every pull is what it
    /// was before they existed (§139).
    /// </summary>
    [Fact]
    public void A_regional_form_changes_nothing_else_in_the_pull()
    {
        var plain = new GachaService(new Catalog(Tiers(), [Pocho()]),
            new Species(SpeciesTable()) { Lines = Families() }, null!, null!, null!, null!);
        var withForms = new GachaService(new Catalog(Tiers(), [Pocho()]),
            new Species([.. SpeciesTable().Select(s => s with { Forms = [new SpeciesForm(1, "Alola")] })])
            { Lines = Families() }, null!, null!, null!, null!);

        var regional = 0;

        for (var number = 0; number < 200; number++)
        {
            var before = plain.Preview(Pocho(), 1, number)!;
            var after = withForms.Preview(Pocho(), 1, number)!;

            Assert.Equal(before, after with { Form = 0, FormName = "" });

            if (after.Form == 1)
            {
                regional++;
                Assert.EndsWith(" de Alola", after.DisplayName);
            }
        }

        Assert.InRange(regional, 60, 140);
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

        var service = new GachaService(new Catalog(Tiers(), [free]),
            new Species(SpeciesTable()) { Lines = Families() },
            new ZeroPoints(), events, new NoRepository(), new FixedClock());

        var result = await service.RollAsync(SampleRun(), "gratis", cleared: 0);

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

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>

            throw new NotSupportedException();


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

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

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
