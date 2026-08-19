using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class ShopTableTests
{
    /// <summary>Builds a Shop.cro shaped buffer with the given inventory sizes.</summary>
    private static byte[] MakeCro(params int[] counts)
    {
        var cro = new byte[0x6000];
        for (var i = 0; i < counts.Length; i++)
        {
            cro[0x52DD + i] = (byte)counts[i];
        }
        return cro;
    }

    [Fact]
    public void Inventories_are_laid_out_back_to_back_after_the_first()
    {
        var shops = ShopTable.Read(MakeCro(9, 12, 4));

        Assert.Equal(3, shops.Count);
        Assert.Equal(0x50BC, shops[0].Offset);
        Assert.Equal(0x50BC + (9 * 2), shops[1].Offset);
        Assert.Equal(0x50BC + ((9 + 12) * 2), shops[2].Offset);
        Assert.Equal([9, 12, 4], shops.Select(s => s.Count));
    }

    [Fact]
    public void The_list_ends_at_the_first_empty_count()
    {
        Assert.Equal(2, ShopTable.Read(MakeCro(5, 5, 0, 7)).Count);
    }

    /// <summary>
    /// Verified against the cartridge item list: 328 is MT01 and 419 is MT92. 420 onwards are
    /// the gen 6 HM slots, which do nothing in Ultra Moon, and then two unnamed ids. pk3DS bans
    /// up to 427, which would stock a shop with items that sell the player nothing.
    /// </summary>
    [Fact]
    public void The_technical_machine_range_is_the_one_the_cartridge_has()
    {
        Assert.Equal(328, ShopTable.FirstTechnicalMachine);
        Assert.Equal(419, ShopTable.LastTechnicalMachine);
        Assert.Equal(92, ShopTable.LastTechnicalMachine - ShopTable.FirstTechnicalMachine + 1);

        Assert.True(ShopTable.IsTechnicalMachine(328));
        Assert.True(ShopTable.IsTechnicalMachine(419));
        Assert.False(ShopTable.IsTechnicalMachine(420)); // MO01
        Assert.False(ShopTable.IsTechnicalMachine(427)); // sin nombre
        Assert.False(ShopTable.IsTechnicalMachine(4));   // Poké Ball
    }

    [Fact]
    public void A_shop_counts_as_a_machine_shop_only_when_everything_in_it_is_one()
    {
        var cro = MakeCro(3);
        var shop = ShopTable.Read(cro)[0];

        ShopTable.SetItem(cro, shop, 0, 328);
        ShopTable.SetItem(cro, shop, 1, 400);
        ShopTable.SetItem(cro, shop, 2, 419);
        Assert.True(ShopTable.SellsTechnicalMachines(cro, shop));

        ShopTable.SetItem(cro, shop, 1, 4); // una Poké Ball entre las MT
        Assert.False(ShopTable.SellsTechnicalMachines(cro, shop));
    }

    [Fact]
    public void Items_survive_a_round_trip_and_stay_in_their_own_slot()
    {
        var cro = MakeCro(4, 4);
        var first = ShopTable.Read(cro)[0];
        var second = ShopTable.Read(cro)[1];

        ShopTable.SetItem(cro, first, 3, 350);
        ShopTable.SetItem(cro, second, 0, 4);

        Assert.Equal(350, ShopTable.GetItem(cro, first, 3));
        Assert.Equal(4, ShopTable.GetItem(cro, second, 0));
        Assert.Equal(0, ShopTable.GetItem(cro, first, 2));
    }

    /// <summary>The eight ordinary counters are where potions and balls come from.</summary>
    [Fact]
    public void The_ordinary_counters_are_the_first_eight()
    {
        Assert.Equal(8, ShopTable.RegularMartCount);
    }
}
