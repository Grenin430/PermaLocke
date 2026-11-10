using PermaLocke.App.Games;
using PKHeX.Core;

namespace PermaLocke.App.Tests;

public sealed class HeartGoldRareCandyTests
{
    [Fact]
    public void Rare_candies_land_in_the_medicine_pocket_and_add_up()
    {
        var save = new SAV4HGSS(new byte[0x80000]);

        Assert.Equal(10, HeartGold.PutRareCandies(save, 10));
        Assert.Equal(20, HeartGold.PutRareCandies(save, 10));

        var medicine = save.Inventory.Pouches.First(p => p.Type == InventoryType.Medicine);
        Assert.Equal(20, medicine.Items.Where(i => i.Index == HeartGold.RareCandy).Sum(i => i.Count));
    }

    [Fact]
    public void The_pocket_cap_holds()
    {
        var save = new SAV4HGSS(new byte[0x80000]);
        var cap = save.Inventory.Pouches.First(p => p.Type == InventoryType.Medicine).MaxCount;

        Assert.Equal(cap, HeartGold.PutRareCandies(save, cap + 50));
    }
}
