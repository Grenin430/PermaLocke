using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The island each zone belongs to, checked against the file the cartridge generated.
/// </summary>
/// <remarks>
/// These are the anchors from the measurement, kept as tests because the grouping is the kind of
/// thing that looks obvious and is not: from memory, Playa Big Wave and Colina Saltagua were both
/// put on the wrong island before <c>RomTool mundos</c> read the answer out of zone data.
/// </remarks>
public sealed class IslandMapTests
{
    private static JsonIslandMap Real()
    {
        var root = AppContext.BaseDirectory;

        while (root is not null && !File.Exists(Path.Combine(root, "PermaLocke.slnx")))
        {
            root = Path.GetDirectoryName(root);
        }

        Assert.NotNull(root);
        return JsonIslandMap.Load(Path.Combine(root!, "Data", "islas.json"));
    }

    [Fact]
    public void The_file_covers_the_four_islands_and_every_zone_once()
    {
        var map = Real();

        Assert.Equal(4, map.Zones.Select(zone => zone.Island).Distinct().Count());
        Assert.Equal<IEnumerable<string>>(JsonIslandMap.Order,
            [.. map.Zones.Select(zone => zone.Island).Distinct()]);

        // Ninguna zona en dos islas: un nombre repartido dos veces se pintaría dos veces en el
        // mapa y una de las dos casillas mentiría.
        Assert.Equal(map.Zones.Count, map.Zones.Select(zone => zone.Name).Distinct().Count());

        // Los 116 lugares que PKHeX sabe nombrar menos tres que no son sitios: «Lugar misterioso»,
        // «Lugar lejano (-)» y un guion suelto. Son marcadores suyos y no casillas de un mapa.
        Assert.Equal(113, map.Zones.Count);

        // Y son los nombres LARGOS, los que la run guarda en cada captura. Con los cortos del
        // cartucho, pinchar la casilla daría un identificador que no casa con ningún Pokémon.
        Assert.Contains(map.Zones, zone => zone.Name == "Ruta 1 (Escuela Entrenadores)");
        Assert.Contains(map.Zones, zone => zone.Name == "Ciudad Hauoli (Puerto)");
    }

    [Theory]
    [InlineData("Ruta 1", "Melemele")]
    [InlineData("Ruta 3", "Melemele")]
    [InlineData("Cementerio de Hauoli", "Melemele")]
    [InlineData("Playa Big Wave", "Melemele")]
    [InlineData("Ruta 4", "Akala")]
    [InlineData("Ruta 9", "Akala")]
    [InlineData("Colina Saltagua", "Akala")]
    [InlineData("Valle de los Pikachu", "Akala")]
    [InlineData("Ruta 10", "Ula-Ula")]
    [InlineData("Ruta 17", "Ula-Ula")]
    [InlineData("Monte Lanakila", "Ula-Ula")]
    [InlineData("Pueblo Po", "Ula-Ula")]
    [InlineData("Isla Exeggutor", "Poni")]
    [InlineData("Aldea Marina", "Poni")]
    [InlineData("Árbol de Combate", "Poni")]
    public void Zones_land_on_the_island_the_cartridge_puts_them_on(string zone, string island)
    {
        var map = Real();

        Assert.Equal(island, Assert.Single(map.Zones, z => z.Name == zone).Island);
    }

    /// <summary>The routes are the check that cannot be fudged: each island owns a run of them.</summary>
    [Fact]
    public void Each_island_owns_its_own_run_of_routes()
    {
        var map = Real();

        static int[] Routes(JsonIslandMap map, string island) =>
        [
            .. map.Of(island)
                .Where(zone => zone.Name.StartsWith("Ruta ", StringComparison.Ordinal))
                .Select(zone => zone.Name["Ruta ".Length..].Split(' ')[0])
                .Where(number => number.All(char.IsAsciiDigit))
                .Select(int.Parse)
                .Distinct()
                .Order()
        ];

        Assert.Equal([1, 2, 3], Routes(map, "Melemele"));
        Assert.Equal([4, 5, 6, 7, 8, 9], Routes(map, "Akala"));
        Assert.Equal([10, 11, 12, 13, 14, 15, 16, 17], Routes(map, "Ula-Ula"));
        Assert.Empty(Routes(map, "Poni"));
    }

    /// <summary>A missing file is not a crash: the map says so and draws nothing.</summary>
    [Fact]
    public void A_missing_file_gives_an_empty_map()
    {
        var map = JsonIslandMap.Load(Path.Combine(Path.GetTempPath(), "no-existe-islas.json"));

        Assert.Empty(map.Zones);
        Assert.Empty(map.Of("Melemele"));
    }
}
