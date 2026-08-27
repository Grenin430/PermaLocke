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
