using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Randomizer.Sprites;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// Every item the game draws an icon for has one the animation can use (2026-10-09). The icon comes the way the app has
/// always taken it: the game's own item to icon table in <c>code.bin</c> (<see cref="ItemIconIndex.UseCartridgeTable"/>), the
/// Z-Crystals' own reader and the mod's painted items, all behind <see cref="PokemonSpriteService.HasItemIcon"/>.
/// </summary>
[Collection("ItemIconIndex")]
public sealed class ItemIconCoverageTests
{
    /// <summary>
    /// The only items of the expansion's table that have a name and no icon, and why: the game's own table says «?» (768)
    /// for every one of them. They are slots of other games or of nothing: the bag pockets and the Box Link (121-128), two
    /// HMs of generation 4 (426-427), the Data Cards that the mod did not turn into Mega Stones (521-531), key items of
    /// Let's Go (717, 745-750, 861-866, 872-878), its collectibles (885-896) and its lures (900-903).
    /// </summary>
    private static readonly int[] DrawnAsQuestionMark =
    [
        121, 122, 123, 124, 125, 126, 127, 128, 426, 427, 521, 522, 523, 524, 525, 526, 527, 528, 529, 530, 531, 717, 745, 746,
        747, 748, 749, 750, 861, 862, 863, 864, 865, 866, 872, 873, 874, 875, 876, 877, 878, 885, 886, 887, 888, 889, 890, 891,
        892, 893, 894, 895, 896, 900, 901, 902, 903
    ];

    private static IEnumerable<int> Named(RealItemData real) =>
        Enumerable.Range(1, 1023).Where(id => id < real.Names.Length && real.Names[id] != "???");

    [Fact]
    public void Every_item_with_a_name_has_an_icon_except_the_ones_the_game_itself_draws_as_a_question_mark()
    {
        if (RealItemData.Load() is not { } real) return;

        try
        {
            ItemIconIndex.UseCartridgeTable(real.Code);

            var without = Named(real).Where(id => !PokemonSpriteService.HasItemIcon(id)).ToArray();

            Assert.Equal(DrawnAsQuestionMark, without);
        }
        finally
        {
            ItemIconIndex.UseCartridgeTable([]);
        }
    }

    [Fact]
    public void The_ones_without_an_icon_are_the_ones_the_cartridge_table_marks_as_blank()
    {
        if (RealItemData.Load() is not { } real) return;

        foreach (var id in DrawnAsQuestionMark)
        {
            var entry = BitConverter.ToUInt32(real.Code, ItemIconIndex.CartridgeTableOffset + (4 * id));

            Assert.True(entry == 768, $"El objeto {id} ({real.Names[id]}) tiene en la tabla del cartucho el icono {entry}: debería dibujarse.");
        }
    }

    [Fact]
    public void Every_category_has_the_icons_that_were_counted_when_this_was_written()
    {
        if (RealItemData.Load() is not { } real) return;

        try
        {
            ItemIconIndex.UseCartridgeTable(real.Code);

            var counted = Named(real)
                .GroupBy(id => real.Catalog.Classify(id).Category)
                .ToDictionary(group => group.Key, group => (With: group.Count(id => PokemonSpriteService.HasItemIcon(id)), Total: group.Count()));

            var expected = new Dictionary<ItemCategory, (int With, int Total)>
            {
                [ItemCategory.Misc] = (130, 163), [ItemCategory.Machine] = (107, 107), [ItemCategory.Berry] = (67, 67),
                [ItemCategory.Healing] = (38, 42), [ItemCategory.Boost] = (17, 17), [ItemCategory.Evolution] = (27, 27),
                [ItemCategory.MegaStone] = (94, 94), [ItemCategory.Battle] = (199, 199), [ItemCategory.PokeBall] = (27, 27),
                [ItemCategory.Key] = (168, 188), [ItemCategory.ZCrystal] = (70, 70)
            };

            foreach (var (category, numbers) in expected) Assert.Equal(numbers, counted[category]);
        }
        finally
        {
            ItemIconIndex.UseCartridgeTable([]);
        }
    }
}
