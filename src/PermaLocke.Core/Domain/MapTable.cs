namespace PermaLocke.Core.Domain;

/// <summary>One map of the game, as the cartridge describes it.</summary>
/// <param name="Map">Index into <c>zonedata</c>.</param>
/// <param name="World">The world the map belongs to, from <c>worlddata</c>.</param>
/// <param name="LocationId">The normalised id of its location, the same the run and the map screen use.</param>
public sealed record MapInfo(int Map, int World, string LocationId, string LocationName);

/// <summary>
/// The game's maps: which world each belongs to and which zone of the run it is.
/// </summary>
/// <remarks>
/// Generated from the cartridge by <c>RomTool mapas --escribir</c> into <c>Data/mapas.json</c>, never written by
/// hand. It does two jobs: translating the map number the game keeps into the zone the run records, and
/// telling a real reading from rubbish, because a map read from memory has to come with the world the
/// cartridge says it belongs to (§117).
/// </remarks>
public sealed class MapTable
{
    private readonly Dictionary<int, MapInfo> _maps;

    public MapTable(IEnumerable<MapInfo> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        _maps = maps.ToDictionary(map => map.Map);
    }

    public static MapTable Empty { get; } = new([]);

    public int Count => _maps.Count;

    public MapInfo? For(int map) => _maps.GetValueOrDefault(map);

    /// <summary>True when the map exists and belongs to exactly that world.</summary>
    public bool Matches(int map, int world) => _maps.TryGetValue(map, out var info) && info.World == world;
}
