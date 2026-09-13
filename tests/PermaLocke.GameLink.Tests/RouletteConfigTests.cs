using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The shipped <c>Data/roulette.json</c>, checked against the cartridge's own tables.
/// </summary>
/// <remarks>
/// This is the test the file exists for. Every ability on the wheel is written as a name, and a
/// name that the game does not have would otherwise vanish silently: the face would land, pick an
/// ability from a shorter list than intended, and nobody would ever know. Here it fails.
/// </remarks>
public sealed class RouletteConfigTests
{
    private static JsonRouletteCatalog Shipped() =>
        JsonRouletteCatalog.Load(Path.Combine(Root(), "Data", "roulette.json"), new PkhexAbilityLookup());

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void The_wheel_has_eight_good_faces_and_eight_bad()
    {
        var catalog = Shipped();

        Assert.Equal(16, catalog.Faces.Count);
        Assert.Equal(8, catalog.Faces.Count(f => f.Good));
        Assert.Equal(8, catalog.Faces.Count(f => !f.Good));
        Assert.Equal(16, catalog.Faces.Select(f => f.Id).Distinct().Count());
    }

    /// <summary>
    /// Not one ability name is silently dropped.
    /// </summary>
    /// <remarks>
    /// The names were checked one by one against the game and ten of the originals turned out to
    /// be wrong — «Absorbe Electricidad» is «Absorbe Elec», «Foco Interno» is «Fuerza Mental» — and
    /// four more are not abilities of this generation at all. This is what keeps the corrected
    /// list correct.
    /// </remarks>
    [Fact]
    public void Every_ability_named_in_the_file_exists_in_this_game()
    {
        var catalog = Shipped();

        Assert.Empty(catalog.UnknownAbilities);
        Assert.NotEmpty(catalog.GoodAbilities);
        Assert.NotEmpty(catalog.BadAbilities);
    }

    /// <summary>
    /// Nothing past the last ability Ultra Moon knows, or the game would store a number it cannot
    /// name.
    /// </summary>
    [Fact]
    public void No_ability_comes_from_a_later_generation()
    {
        var catalog = Shipped();

        Assert.All(catalog.GoodAbilities, id => Assert.InRange(id, 1, PkhexAbilityLookup.LastGen7Ability));
        Assert.All(catalog.BadAbilities, id => Assert.InRange(id, 1, PkhexAbilityLookup.LastGen7Ability));
    }

    /// <summary>Every healing item is what the file says it is, checked against the item table.</summary>
    [Fact]
    public void The_healing_items_are_the_ones_the_file_names()
    {
        var catalog = Shipped();
        var items = new PkhexItemLookup();

        Assert.NotEmpty(catalog.HealingItems);
        Assert.Equal(catalog.HealingItems.Count, catalog.HealingItems.Distinct().Count());

        // La Poción y la Cura Total son las dos que cualquiera reconoce; si estas dos cuadran, el
        // fichero está leyendo ids de objeto y no de otra cosa.
        Assert.Contains(catalog.HealingItems, id => items.GetName(id) == "Poción");
        Assert.Contains(catalog.HealingItems, id => items.GetName(id) == "Cura Total");
        Assert.DoesNotContain(catalog.HealingItems, id => items.GetName(id) == "Caramelo Raro");
    }

    /// <summary>Each effect the wheel knows appears at least once, or a face was left unwired.</summary>
    [Fact]
    public void Every_effect_is_on_the_wheel()
    {
        var used = Shipped().Faces.Select(f => f.Effect).Distinct().ToHashSet();

        Assert.Equal(Enum.GetValues<RouletteEffect>().Length, used.Count);
    }

    [Fact]
    public void The_milestones_that_owe_a_spin_are_the_twelve_trials_and_the_league()
    {
        var catalog = Shipped();

        Assert.Equal(12, catalog.TrialAchievements.Count);
        Assert.Equal(1, catalog.SpinsPerTrial);
        Assert.Equal(3, catalog.SpinsForLeague);
        Assert.Equal(2, catalog.SpinsForRematch);
        Assert.Equal("alto-mando-campeon", catalog.LeagueAchievement);
        Assert.Equal("alto-mando-otra-vez", catalog.RematchAchievement);
    }

    /// <summary>
    /// Every face carries what the wedge needs to draw itself.
    /// </summary>
    /// <remarks>
    /// The wedge shows a short label, a figure and a picture. All three fall back quietly when
    /// they are missing — the label to the long name, the figure and the picture to nothing — so a
    /// face added without them looks like a face that works and reads like a blank. Here it fails.
    /// </remarks>
    [Fact]
    public void Every_face_says_what_to_put_on_its_wedge()
    {
        var catalog = Shipped();

        Assert.All(catalog.Faces, face =>
        {
            Assert.False(string.IsNullOrWhiteSpace(face.Short), $"«{face.Id}» no tiene corto");
            Assert.False(string.IsNullOrWhiteSpace(face.Figure), $"«{face.Id}» no tiene cifra");
            Assert.True(face.ItemIcon > 0 || face.SpeciesIcon > 0, $"«{face.Id}» no tiene icono");
        });
    }

    /// <summary>The short label really is short, or it will not fit on a wedge.</summary>
    [Fact]
    public void The_short_label_fits_a_wedge()
    {
        Assert.All(Shipped().Faces, face => Assert.InRange(face.Label.Length, 1, 16));
    }

    /// <summary>
    /// The two death faces are drawn as Shedinja, which is not a decoration.
    /// </summary>
    /// <remarks>
    /// It is what the game literally turns a fallen Pokémon into, so the wedge shows the thing
    /// that is about to happen. If the mark ever changes species, this is what says so.
    /// </remarks>
    [Fact]
    public void Death_is_drawn_as_what_death_actually_writes()
    {
        var deaths = Shipped().Faces.Where(f => f.Effect == RouletteEffect.Muerte).ToList();

        Assert.Equal(2, deaths.Count);
        Assert.All(deaths, face => Assert.Equal(292, face.SpeciesIcon));
    }

    /// <summary>A missing file leaves an empty wheel, not a half-invented one.</summary>
    [Fact]
    public void A_missing_file_gives_no_faces()
    {
        var catalog = JsonRouletteCatalog.Load(
            Path.Combine(Path.GetTempPath(), $"no-existe-{Guid.NewGuid():N}.json"), new PkhexAbilityLookup());

        Assert.Empty(catalog.Faces);
    }
}

/// <summary>
/// The shipped <c>Data/grants.json</c>: the competition's table of free rolls and wonder trades.
/// </summary>
/// <remarks>
/// A table typed in by hand is a table with a typo in it. These check the totals the competition
/// stated, so a banner id that drifts or a milestone that gets dropped fails here instead of
/// quietly paying nothing on the day somebody clears that trial.
/// </remarks>
public sealed class GrantConfigTests
{
    private static JsonCreditCatalog Shipped() =>
        JsonCreditCatalog.Load(Path.Combine(Root(), "Data", "grants.json"));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void Every_milestone_of_the_competition_is_there()
    {
        var catalog = Shipped();

        Assert.Equal(14, catalog.Milestones.Count);
        Assert.Equal(12, catalog.Milestones.Count(m => m.Achievement.StartsWith("prueba-")));
        Assert.Contains(catalog.Milestones, m => m.Achievement == "alto-mando-campeon");
        Assert.Contains(catalog.Milestones, m => m.Achievement == "alto-mando-otra-vez");
    }

    /// <summary>One wonder trade per trial and four for each league run, as stated.</summary>
    [Fact]
    public void The_wonder_trades_add_up()
    {
        var catalog = Shipped();

        Assert.All(catalog.Milestones.Where(m => m.Achievement.StartsWith("prueba-")),
            m => Assert.Equal(1, m.WonderTrades));

        Assert.Equal(4, catalog.Milestones.First(m => m.Achievement == "alto-mando-campeon").WonderTrades);
        Assert.Equal(4, catalog.Milestones.First(m => m.Achievement == "alto-mando-otra-vez").WonderTrades);
        Assert.Equal(20, catalog.Milestones.Sum(m => m.WonderTrades));
    }

    /// <summary>
    /// The rolls, banner by banner: pocho for the first three trials, decente for the next four,
    /// one of each for the four after that, and bueno from the twelfth on.
    /// </summary>
    [Theory]
    [InlineData("prueba-01", "pocho", 2)]
    [InlineData("prueba-03", "pocho", 2)]
    [InlineData("prueba-04", "decente", 2)]
    [InlineData("prueba-07", "decente", 2)]
    [InlineData("prueba-08", "bueno", 1)]
    [InlineData("prueba-08", "decente", 1)]
    [InlineData("prueba-11", "bueno", 1)]
    [InlineData("prueba-12", "bueno", 2)]
    [InlineData("alto-mando-campeon", "bueno", 3)]
    [InlineData("alto-mando-otra-vez", "bueno", 3)]
    public void The_rolls_are_the_ones_the_competition_stated(string achievement, string banner, int count)
    {
        var milestone = Shipped().Milestones.First(m => m.Achievement == achievement);

        Assert.Equal(count, milestone.Rolls[banner]);
    }

    /// <summary>Every banner named must be one the gacha actually has.</summary>
    [Fact]
    public void No_milestone_pays_into_a_banner_that_does_not_exist()
    {
        var banners = JsonGachaCatalog.Load(Path.Combine(Root(), "Data", "gacha.json"))
            .Banners.Select(b => b.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(banners);

        foreach (var banner in Shipped().Milestones.SelectMany(m => m.Rolls.Keys))
        {
            Assert.Contains(banner, banners);
        }
    }

    /// <summary>
    /// One tier hands out legendaries and one banner can reach it.
    /// </summary>
    /// <remarks>
    /// «Los legendarios solo salen del BUENO» is held up by two separate facts in the shipped file
    /// -- one tier with a chance above zero, and one banner whose odds reach that tier -- and
    /// neither is written down anywhere as a rule. Editing a percentage in
    /// <c>Data/gacha.json</c> could quietly hand legendaries to the cheap banner, and the roll
    /// would look perfectly normal. Here it fails.
    /// </remarks>
    [Fact]
    public void Only_the_top_tier_of_the_shipped_gacha_pays_legendaries()
    {
        var catalog = JsonGachaCatalog.Load(Path.Combine(Root(), "Data", "gacha.json"));

        var paying = catalog.Tiers.Where(t => t.LegendaryChance > 0).ToList();
        Assert.Single(paying);
        Assert.Equal(catalog.Tiers.Max(t => t.MaxBaseStatTotal), paying[0].MaxBaseStatTotal);

        var reaching = catalog.Banners
            .Where(b => b.TierChances.TryGetValue(paying[0].Id, out var chance) && chance > 0)
            .ToList();

        Assert.Single(reaching);
        Assert.Equal("bueno", reaching[0].Id, ignoreCase: true);
    }

    /// <summary>
    /// No species is in two evolution families, and every one of them is in one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves cost something real, and both were broken while this was being built. Alolan
    /// forms have families of their own — Alolan Rattata evolves into Alolan Raticate — whose base
    /// is a <b>form</b> entry with an index above every species. Keeping only the known ids left a
    /// family whose sole rung was Raticate, so <b>24 species</b> were being handed out as if they
    /// were first stages: the gacha gave finished Arcanines and Golems out of a cheap tier and
    /// nothing looked wrong.
    /// </para>
    /// <para>
    /// Discarding those families then left <b>ten</b> species in none at all — Obstagoon,
    /// Sirfetch'd, Basculegion and the rest, which only evolve from a regional form — and a species
    /// in no family simply cannot come out. They get a one-rung family of their own, and that is
    /// what the second half of this checks.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_species_is_in_exactly_one_evolution_family()
    {
        var species = JsonSpeciesStatsCatalog.Load(Path.Combine(Root(), "Data", "species.json"));

        Assert.NotEmpty(species.All);
        Assert.NotEmpty(species.Lines);

        var counted = species.Lines
            .SelectMany(line => line.AllSpecies)
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());

        var twice = counted.Where(entry => entry.Value > 1).Select(entry => entry.Key).ToList();
        Assert.True(twice.Count == 0, $"repartidas desde dos familias: {string.Join(", ", twice)}");

        var missing = species.All.Where(s => !counted.ContainsKey(s.Id)).Select(s => s.Id).ToList();
        Assert.True(missing.Count == 0, $"sin familia y por tanto imposibles: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The odds table starts at «always the first stage» and never promises more than everything.
    /// </summary>
    [Fact]
    public void The_stage_table_starts_at_the_beginning_of_the_game()
    {
        var gacha = JsonGachaCatalog.Load(Path.Combine(Root(), "Data", "gacha.json"));

        Assert.NotEmpty(gacha.StageOdds);

        var start = gacha.StageOdds[0];
        Assert.Equal(0, start.Cleared);
        Assert.Equal(100, start.First);

        foreach (var row in gacha.StageOdds)
        {
            Assert.InRange(row.Second + row.Final, 0, 100);
        }
    }

    [Fact]
    public void Wonder_trades_are_limited()
    {
        Assert.True(Shipped().LimitWonderTrades);
    }

    /// <summary>A missing file must not lock a feature that used to work.</summary>
    [Fact]
    public void Without_the_file_nothing_is_limited()
    {
        var catalog = JsonCreditCatalog.Load(
            Path.Combine(Path.GetTempPath(), $"no-existe-{Guid.NewGuid():N}.json"));

        Assert.Empty(catalog.Milestones);
        Assert.False(catalog.LimitWonderTrades);
    }
}
