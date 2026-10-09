using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>What every item is for its animation (2026-10-09): the rules, and then the whole item table of the game.</summary>
public sealed class ItemCatalogTests
{
    private static ItemCatalog Synthetic(params (int Id, int Pocket)[] pockets)
    {
        var table = new int[1024];
        foreach (var (id, pocket) in pockets) table[id] = pocket;
        return new ItemCatalog(table, new HashSet<int> { 700 });
    }

    [Fact]
    public void The_pocket_of_the_cartridge_tells_the_category_and_the_mega_table_beats_it()
    {
        var catalog = Synthetic((328, 2), (149, 3), (216, 4), (807, 5), (776, 7), (17, 1), (700, 0), (400, 6));

        Assert.Equal(ItemCategory.Machine, catalog.Classify(328).Category);
        Assert.Equal(ItemCategory.Berry, catalog.Classify(149).Category);
        Assert.Equal(ItemCategory.Key, catalog.Classify(216).Category);
        Assert.Equal(ItemCategory.ZCrystal, catalog.Classify(807).Category);
        Assert.Equal(ItemCategory.ZCrystal, catalog.Classify(776).Category);
        Assert.Equal(ItemCategory.Healing, catalog.Classify(17).Category);
        Assert.Equal(ItemCategory.MegaStone, catalog.Classify(700).Category);
        Assert.Equal(ItemCategory.Misc, catalog.Classify(400).Category);
    }

    [Fact]
    public void What_nobody_knows_is_the_plainest_animation_and_never_breaks()
    {
        Assert.Equal(ItemCategory.Misc, ItemCatalog.Empty.Classify(4).Category);
        Assert.Equal(ItemCategory.Misc, Synthetic().Classify(-1).Category);
        Assert.Equal(ItemCategory.Misc, Synthetic().Classify(5000).Category);
        Assert.False(ItemCatalog.Empty.HasData);
    }

    [Fact]
    public void The_items_the_mod_made_keep_their_category_whatever_their_entry_says()
    {
        var catalog = Synthetic((113, 0), (114, 0), (115, 0));

        Assert.Equal(new ItemClass(ItemCategory.Boost, 3), catalog.Classify(113));
        Assert.Equal(ItemCategory.Key, catalog.Classify(114).Category);
        Assert.Equal(ItemCategory.Key, catalog.Classify(115).Category);
    }

    [Fact]
    public void A_stronger_healing_item_has_more_power()
    {
        var catalog = Synthetic((17, 1), (26, 1), (25, 1), (24, 1), (28, 1), (29, 1));

        Assert.True(catalog.Classify(17).Power < catalog.Classify(26).Power);
        Assert.True(catalog.Classify(26).Power < catalog.Classify(25).Power);
        Assert.True(catalog.Classify(25).Power < catalog.Classify(24).Power);
        Assert.True(catalog.Classify(28).Power < catalog.Classify(29).Power);
    }

    [Fact]
    public void Rare_Candy_beats_the_vitamins_and_the_vitamins_beat_PP_Up()
    {
        var catalog = Synthetic((50, 1), (45, 1), (51, 1));

        Assert.Equal(ItemCategory.Boost, catalog.Classify(51).Category);
        Assert.True(catalog.Classify(50).Power > catalog.Classify(45).Power);
        Assert.True(catalog.Classify(45).Power > catalog.Classify(51).Power);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void No_animation_is_shorter_than_one_and_a_third_seconds_or_longer_than_its_ceiling(int power)
    {
        foreach (var category in Enum.GetValues<ItemCategory>())
        {
            var length = ItemTimeline.For(category, power).Length;
            var ceiling = category == ItemCategory.MegaStone ? 4.5 : category == ItemCategory.Key ? 3.2 : 3.0;

            Assert.InRange(length, 1.3, ceiling);
        }
    }

    [Fact]
    public void The_dither_comes_in_and_goes_out_whatever_the_category()
    {
        foreach (var category in Enum.GetValues<ItemCategory>())
        {
            var phases = ItemTimeline.For(category);

            Assert.Equal(0, phases.Alpha(0));
            Assert.Equal(1, phases.Alpha(phases.Length / 2));
            Assert.True(phases.Alpha(phases.Length - 0.01) < 0.1);
        }
    }

    [Fact]
    public void The_seed_of_an_item_changes_with_how_many_came_before_it()
    {
        Assert.Equal(ItemScene.SeedFor(17, 3), ItemScene.SeedFor(17, 3));
        Assert.NotEqual(ItemScene.SeedFor(17, 3), ItemScene.SeedFor(17, 4));
        Assert.NotEqual(ItemScene.SeedFor(17, 3), ItemScene.SeedFor(18, 3));
    }

    // ============================================================ EL CARTUCHO ENTERO

    private static string[] NamesOf(ItemCatalog catalog, string[] names, ItemCategory category) =>
        [.. Enumerable.Range(1, 1023).Where(id => catalog.Classify(id).Category == category).Select(id => names[id])];

    [Fact]
    public void The_whole_item_table_of_the_expansion_is_told_apart_as_measured()
    {
        if (RealItemData.Load() is not { } real) return;
        var (catalog, names) = (real.Catalog, real.Names);

        Assert.Equal(107, NamesOf(catalog, names, ItemCategory.Machine).Length);   // the hundred TMs and seven HMs
        Assert.Equal(67, NamesOf(catalog, names, ItemCategory.Berry).Length);
        Assert.Equal(94, NamesOf(catalog, names, ItemCategory.MegaStone).Length);  // 47 of the game and 47 of the mod
        Assert.Equal(70, NamesOf(catalog, names, ItemCategory.ZCrystal).Length);
        Assert.Equal(27, NamesOf(catalog, names, ItemCategory.PokeBall).Length);
        Assert.Equal(201, NamesOf(catalog, names, ItemCategory.Key).Length);       // and the two key items of the mod
    }

    [Fact]
    public void Every_ball_is_a_ball_and_every_mega_stone_of_the_game_is_a_stone()
    {
        if (RealItemData.Load() is not { } real) return;
        var (catalog, names) = (real.Catalog, real.Names);

        Assert.All(NamesOf(catalog, names, ItemCategory.PokeBall), name => Assert.EndsWith(" Ball", name));
        Assert.Equal(47, NamesOf(catalog, names, ItemCategory.MegaStone).Count(name => name.EndsWith("ite", StringComparison.Ordinal)
            || name.EndsWith("ite X", StringComparison.Ordinal) || name.EndsWith("ite Y", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_evolution_items_are_the_stones_and_the_trade_items_and_nothing_else()
    {
        if (RealItemData.Load() is not { } real) return;
        var (catalog, names) = (real.Catalog, real.Names);

        string[] expected =
        [
            "Sun Stone", "Moon Stone", "Fire Stone", "Thunder Stone", "Water Stone", "Leaf Stone", "Shiny Stone", "Dusk Stone",
            "Dawn Stone", "Ice Stone", "Oval Stone", "King’s Rock", "Deep Sea Tooth", "Deep Sea Scale", "Metal Coat",
            "Dragon Scale", "Upgrade", "Protector", "Electirizer", "Magmarizer", "Dubious Disc", "Reaper Cloth", "Razor Claw",
            "Razor Fang", "Prism Scale", "Whipped Dream", "Sachet"
        ];

        Assert.Equal(expected.Order(), NamesOf(catalog, names, ItemCategory.Evolution).Order());
    }

    [Fact]
    public void The_boosts_are_the_candy_the_vitamins_the_PP_items_and_the_feathers()
    {
        if (RealItemData.Load() is not { } real) return;
        var (catalog, names) = (real.Catalog, real.Names);

        string[] expected =
        [
            "Rare Candy", "HP Up", "Protein", "Iron", "Carbos", "Calcium", "Zinc", "PP Up", "PP Max", "Ability Capsule",
            "Health Feather", "Muscle Feather", "Resist Feather", "Genius Feather", "Clever Feather", "Swift Feather",
            "Tea"   // the mod's Super Candy sits in the slot PKHeX calls "Tea"
        ];

        Assert.Equal(expected.Order(), NamesOf(catalog, names, ItemCategory.Boost).Order());
    }

    [Fact]
    public void Potions_cures_and_revives_are_healing_and_a_Choice_Band_is_not()
    {
        if (RealItemData.Load() is not { } real) return;
        var (catalog, names) = (real.Catalog, real.Names);

        var healing = NamesOf(catalog, names, ItemCategory.Healing);
        foreach (var name in new[] { "Potion", "Super Potion", "Hyper Potion", "Max Potion", "Full Restore", "Revive", "Max Revive", "Antidote", "Full Heal", "Elixir" })
        {
            Assert.Contains(name, healing);
        }

        Assert.DoesNotContain("Rare Candy", healing);
        Assert.DoesNotContain("Choice Band", healing);
        Assert.Contains("Choice Band", NamesOf(catalog, names, ItemCategory.Battle));
        Assert.Contains("Fairy Gem", NamesOf(catalog, names, ItemCategory.Battle));
        Assert.Contains("Pearl", NamesOf(catalog, names, ItemCategory.Misc));
    }
}
