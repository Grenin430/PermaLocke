using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>
/// Reads <c>Data/mapas.json</c>, the game's maps with their world and zone, into a <see cref="MapTable"/>.
/// </summary>
/// <remarks>
/// Written by <c>RomTool mapas --escribir</c> from the cartridge. A missing or broken file gives the empty
/// table, and with an empty table no map reading validates: the ball rule then never knows where the player
/// is and never takes anything away, which is the direction that cannot hurt anybody.
/// </remarks>
public static class JsonMapTable
{
    public static MapTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return MapTable.Empty;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<MapFile>(stream);

            return file?.Maps is { Count: > 0 } maps
                ? new MapTable(maps
                    .Where(map => map is not null && !string.IsNullOrWhiteSpace(map.LocationId))
                    .Select(map => new MapInfo(map!.Map, map.World, map.LocationId!, map.Name ?? map.LocationId!)))
                : MapTable.Empty;
        }
        catch (JsonException)
        {
            return MapTable.Empty;
        }
    }

    private sealed record MapFile(
        [property: JsonPropertyName("mapas")] IReadOnlyList<MapEntry?>? Maps);

    private sealed record MapEntry(
        [property: JsonPropertyName("mapa")] int Map,
        [property: JsonPropertyName("mundo")] int World,
        [property: JsonPropertyName("zona")] string? LocationId,
        [property: JsonPropertyName("nombre")] string? Name);
}
