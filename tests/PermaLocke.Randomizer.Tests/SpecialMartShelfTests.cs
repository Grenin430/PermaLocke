using PermaLocke.Core.Services;
using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Special counters with a list of their own (§145): the classic evolution items in the towns the
/// player chose, with the gen 8-9 list moving on to the next counter instead of being overwritten.
/// </summary>
public sealed class SpecialMartShelfTests
{
    private const int PokeBall = 4;

    /// <summary>Eight ordinary counters and then four special ones: 8 and 10 sell things, 9 sells TMs.</summary>
    private static (byte[] Cro, IReadOnlyList<ShopInventory> Shops) Cro()
    {
        int[][] inventories =
        [
            [17], [17], [17], [17], [17], [17], [17], [17],
            [254, 255, 318],
            [328, 329],
            [59, 57, 58, 55],
            [26, 14],
        ];

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

    private static readonly int[] Machines = [.. Enumerable.Range(328, 20)];

    private static int[] Items(byte[] cro, ShopInventory shop) =>
        [.. Enumerable.Range(0, shop.Count).Select(slot => ShopTable.GetItem(cro, shop, slot))];

    private static MartShelf Shelf(int shop, params int[] items) =>
        new(shop, $"tienda {shop}", 30000, [.. items.Select(id => new MartItem(id, $"objeto {id}"))]);

    private static RandomizerOptions Options(params MartShelf[] shelves) => new()
    {
        NonMachineMartItem = PokeBall,
        SpecialMartItems = [new MartItem(971, "a"), new MartItem(972, "b"), new MartItem(973, "c")],
        SpecialMartShelves = shelves,
    };

    /// <summary>
    /// The counter sells its list in order and fills what is left with the filler; the spilling list
    /// jumps over it and lands on the next counter.
    /// </summary>
    [Fact]
    public void A_shelf_sells_its_list_and_the_spilling_list_skips_it()
    {
        var (cro, shops) = Cro();
        var options = Options(Shelf(8, 80, 81));

        var stock = ShopRandomizer.StockSpecials(options, cro, shops, new SeededRandomSource(1), Machines);

        Assert.Equal([80, 81, PokeBall], Items(cro, shops[8]));
        Assert.Equal([971, 972, 973, PokeBall], Items(cro, shops[10]));
        Assert.Equal([PokeBall, PokeBall], Items(cro, shops[11]));
        Assert.Equal(3, stock.Stocked);
        Assert.Equal(2, stock.Shelved);
    }

    /// <summary>
    /// A shelf takes no random numbers, so giving a counter one does not move anybody's TMs.
    /// </summary>
    [Fact]
    public void A_shelf_does_not_change_the_TMs()
    {
        var (without, shops) = Cro();
        ShopRandomizer.StockSpecials(Options(), without, shops, new SeededRandomSource(20260919), Machines);

        var (with, _) = Cro();
        ShopRandomizer.StockSpecials(Options(Shelf(8, 80), Shelf(11, 81)), with, shops,
            new SeededRandomSource(20260919), Machines);

        Assert.Equal(Items(without, shops[9]), Items(with, shops[9]));
    }

    /// <summary>The spilling list follows the configured order, which is how the gen 8-9 items move on.</summary>
    [Fact]
    public void The_spilling_list_follows_the_order_and_skips_the_shelves()
    {
        var (cro, shops) = Cro();
        var options = Options(Shelf(10, 80)) with { SpecialMartOrder = [10, 11, 8] };

        ShopRandomizer.StockSpecials(options, cro, shops, new SeededRandomSource(1), Machines);

        Assert.Equal([80, PokeBall, PokeBall, PokeBall], Items(cro, shops[10]));
        Assert.Equal([971, 972], Items(cro, shops[11]));
        Assert.Equal([973, PokeBall, PokeBall], Items(cro, shops[8]));
    }

    public static TheoryData<string, MartShelf[]> Refused => new()
    {
        { "no existe", [Shelf(40, 80)] },
        { "mostrador normal", [Shelf(3, 80)] },
        { "de MT", [Shelf(9, 80)] },
        { "no cabe", [Shelf(11, 80, 81, 82)] },
        { "vacia", [Shelf(11)] },
        { "dos listas en la misma", [Shelf(8, 80), Shelf(8, 81)] },
        { "un objeto en dos listas", [Shelf(8, 80), Shelf(10, 80)] },
        { "un objeto tambien en la que se derrama", [Shelf(8, 971)] },
        { "precio que no es multiplo de 10", [Shelf(8, 80) with { Price = 30005 }] },
        { "precio que no cabe", [Shelf(8, 80) with { Price = 700000 }] },
        { "precio propio que no es multiplo de 10", [Shelf(8, 80) with { Items = [new MartItem(80, "x", 35)] }] },
    };

    /// <summary>
    /// Anything that would leave an item unbuyable, or priced twice, is refused before a byte moves.
    /// </summary>
    [Theory]
    [MemberData(nameof(Refused))]
    public void A_shelf_that_cannot_be_placed_is_refused(string why, MartShelf[] shelves)
    {
        var (cro, shops) = Cro();
        var before = cro.ToArray();

        var error = Assert.Throws<InvalidDataException>(
            () => ShopRandomizer.ValidateShelves(Options(shelves), cro, shops));

        Assert.False(string.IsNullOrWhiteSpace(error.Message), why);
        Assert.Equal(before, cro);
    }

    [Fact]
    public void A_good_shelf_passes()
    {
        var (cro, shops) = Cro();

        ShopRandomizer.ValidateShelves(Options(Shelf(8, 80, 81, 82), Shelf(11, 83)), cro, shops);
    }

    /// <summary>What the player asked for, as the file says it: three lists, 29 items, 30000 each.</summary>
    [Fact]
    public void The_configured_lists_are_the_ones_the_player_asked_for()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(FindRoot(), "Data", "randomizer.json"));
        var shelves = options.SpecialMartShelves.ToDictionary(shelf => shelf.Shop);

        Assert.Equal(11, shelves[14].Items.Count);
        Assert.Equal(12, shelves[11].Items.Count);
        Assert.Equal(6, shelves[8].Items.Count);
        Assert.All(shelves.Values, shelf => Assert.Equal(30000, shelf.Price));

        // Ninguno repetido, ni entre listas ni con los de gen 8-9, y ninguno de estos mostradores en
        // el orden de la lista que se derrama: los de gen 8-9 tienen que irse a otro sitio.
        var ids = shelves.Values.SelectMany(shelf => shelf.Items).Select(item => item.Id)
            .Concat(options.SpecialMartItems.Select(item => item.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(options.SpecialMartOrder, shelves.ContainsKey);

        Assert.Contains(shelves[14].Items, item => item.Id == 849 && item.Name == "Piedra Hielo");
        Assert.Contains(shelves[11].Items, item => item.Id == 235 && item.Name == "Escama Dragón");
        Assert.Contains(shelves[8].Items, item => item.Id == 174 && item.Name == "Baya Tamate");

        // Gholdengo pide 999 monedas: a 50000 no se podria comprar nunca, asi que la moneda va a 30.
        Assert.Contains(options.SpecialMartItems, item => item.Id == 994 && item.Price == 30);
        Assert.All(options.SpecialMartItems.Where(item => item.Id != 994), item => Assert.Equal(0, item.Price));

        // El 24 NO: pk3DS lo llama Ruta 3 y alli no hay Centro Pokemon, asi que no se sabe donde esta.
        Assert.DoesNotContain(24, options.SpecialMartOrder);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encuentra la raíz.");
    }
}
