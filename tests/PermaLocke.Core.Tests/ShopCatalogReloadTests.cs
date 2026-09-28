using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

public class ShopCatalogReloadTests
{
    [Fact]
    public void The_organisers_rule_closes_the_shop_without_restarting()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shop-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "items": [ { "id": 50, "name": "Caramelo", "price": 10 } ], "abierta": true }""");
        var catalog = JsonShopCatalog.Load(path);
        Assert.True(catalog.Open);

        // Lo que hace RulesSync al bajar la regla oficial después de arrancar.
        File.WriteAllText(path, """{ "items": [ { "id": 50, "name": "Caramelo", "price": 10 } ], "abierta": false, "abreEnPrueba": 3 }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        Assert.False(catalog.Open);
        Assert.Equal(3, catalog.OpensAtTrial);
        File.Delete(path);
    }
}
