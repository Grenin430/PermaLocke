using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Turning the ordinary counters' status medicines into Poké Balls.
/// </summary>
/// <remarks>
/// The six ids were read off the cartridge (17 Poción, 18 Antídoto, 19 Antiquemar, 20 Antihielo,
/// 21 Despertar, 22 Antiparalizador) and they happen to sit together, which is why "from Potion to
/// Ice Heal" and the list of six name the same thing.
/// </remarks>
public sealed class RegularMartTests
{
    private const int PokeBall = 4;

    /// <summary>An item table long enough to hold the ids these tests use.</summary>
    private static string[] Names()
    {
        var names = new string[64];
        Array.Fill(names, string.Empty);

        names[PokeBall] = "Poké Ball";
        names[5] = "Super Ball";
        names[17] = "Poción";
        names[18] = "Antídoto";
        names[19] = "Antiquemar";
        names[20] = "Antihielo";
        names[21] = "Despertar";
        names[22] = "Antiparalizador";
        names[26] = "Superpoción";
        names[55] = "Cuerda Huida";

        return names;
    }

    private static RandomizerOptions Options() => new();

    /// <summary>Builds a Shop.cro shaped buffer whose inventories hold the given items.</summary>
    private static (byte[] Cro, IReadOnlyList<ShopInventory> Shops) Cro(params int[][] inventories)
    {
        var cro = new byte[0x6000];

        for (var i = 0; i < inventories.Length; i++)
        {
            cro[0x52DD + i] = (byte)inventories[i].Length;
        }

        var shops = ShopTable.Read(cro);

        for (var i = 0; i < inventories.Length; i++)
        {
            for (var slot = 0; slot < inventories[i].Length; slot++)
            {
                ShopTable.SetItem(cro, shops[i], slot, inventories[i][slot]);
            }
        }

        return (cro, shops);
    }

    private static int[] Items(byte[] cro, ShopInventory shop) =>
        [.. Enumerable.Range(0, shop.Count).Select(slot => ShopTable.GetItem(cro, shop, slot))];

    /// <summary>The first ordinary counter of the cartridge, item for item.</summary>
    [Fact]
    public void The_six_medicines_become_balls_and_nothing_else_moves()
    {
        var (cro, shops) = Cro([PokeBall, 17, 18, 22, 21, 19, 20, 55, 26]);

        var changed = ShopRandomizer.ReplaceMedicines(Options(), cro, shops, Names());

        Assert.Equal(6, changed);
        Assert.Equal([PokeBall, PokeBall, PokeBall, PokeBall, PokeBall, PokeBall, PokeBall, 55, 26],
            Items(cro, shops[0]));
    }

    /// <summary>
    /// They sit in a different place in each of the eight counters, so the match is by id.
    /// </summary>
    [Fact]
    public void It_does_not_matter_where_in_the_shop_they_are()
    {
        var (cro, shops) = Cro([PokeBall, 5, 26, 17, 55, 18]);

        Assert.Equal(2, ShopRandomizer.ReplaceMedicines(Options(), cro, shops, Names()));
        Assert.Equal([PokeBall, 5, 26, PokeBall, 55, PokeBall], Items(cro, shops[0]));
    }

    /// <summary>
    /// Only the eight ordinary counters. The special ones are the shop randomizer's business, and
    /// one of them sells exactly these six medicines — reaching into it here would rewrite a shop
    /// twice and make which pass won depend on the order.
    /// </summary>
    [Fact]
    public void The_special_counters_are_left_alone()
    {
        var inventories = new int[9][];
        for (var i = 0; i < 9; i++)
        {
            inventories[i] = [17, 18];
        }

        var (cro, shops) = Cro(inventories);

        Assert.Equal(16, ShopRandomizer.ReplaceMedicines(Options(), cro, shops, Names()));
        Assert.Equal([17, 18], Items(cro, shops[8]));
    }

    /// <summary>
    /// A name that does not match the cartridge stops everything, before a byte is written.
    /// </summary>
    /// <remarks>
    /// The §52 guard. An id that lands on the wrong item stocks the shop with the wrong thing and
    /// the game works perfectly, which is the kind of mistake nobody ever notices.
    /// </remarks>
    [Fact]
    public void A_wrong_id_stops_everything_instead_of_stocking_the_wrong_item()
    {
        var (cro, shops) = Cro([17, 18]);
        var before = cro.ToArray();

        var options = Options() with { RegularMartReplaced = [new MartItem(17, "Caramelo Raro")] };

        Assert.Throws<InvalidDataException>(
            () => ShopRandomizer.ReplaceMedicines(options, cro, shops, Names()));

        Assert.Equal(before, cro);
    }

    /// <summary>The replacement is checked too, not only what is being taken out.</summary>
    [Fact]
    public void A_wrong_replacement_is_caught_as_well()
    {
        var (cro, shops) = Cro([17]);
        var options = Options() with { RegularMartReplacement = new MartItem(4, "Ultra Ball") };

        Assert.Throws<InvalidDataException>(
            () => ShopRandomizer.ReplaceMedicines(options, cro, shops, Names()));
    }

    /// <summary>An empty list is how an admin turns this off, and it must touch nothing.</summary>
    [Fact]
    public void An_empty_list_leaves_the_shops_exactly_as_they_were()
    {
        var (cro, shops) = Cro([17, 18, 19]);
        var before = cro.ToArray();

        var changed = ShopRandomizer.ReplaceMedicines(
            Options() with { RegularMartReplaced = [] }, cro, shops, Names());

        Assert.Equal(0, changed);
        Assert.Equal(before, cro);
    }
}
