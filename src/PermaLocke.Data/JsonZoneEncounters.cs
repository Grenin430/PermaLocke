using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermaLocke.Data;

/// <summary>
/// Which places the cartridge gives an encounter table, read from <c>Data/zones.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// It exists to <b>check a player's claim, not to replace it</b>. When somebody says «the zones I
/// left unmarked are the ones where nothing can be caught», that is knowledge the cartridge
/// partly has: every area in <c>encdata</c> either carries tables or does not. Agreement is worth
/// nothing to say out loud; a disagreement is worth stopping for.
/// </para>
/// <para>
/// The join is lossy and that is why the answer can be «no lo sé». The cartridge names places
/// short — «Ciudad Hauoli» — and the run names them long — «Ciudad Hauoli (Puerto)» — so a
/// sub-location inherits its parent's answer, and a parent that has both kinds of area cannot
/// speak for the child. Rather than guess, <see cref="HasEncounters"/> returns null.
/// </para>
/// </remarks>
public sealed class JsonZoneEncounters
{
    private readonly Dictionary<string, bool?> _byName;

    private JsonZoneEncounters(Dictionary<string, bool?> byName) => _byName = byName;

    public static JsonZoneEncounters Empty => new([]);

    public int Count => _byName.Count;

    public static JsonZoneEncounters Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<ZoneFile>(stream);

            if (file?.Areas is not { Count: > 0 } areas)
            {
                return Empty;
            }

            // Un nombre que sale en areas con tabla y en areas sin ella no puede contestar por sus
            // sublugares: se marca como desconocido en vez de inventar una respuesta.
            var seen = new Dictionary<string, (bool With, bool Without)>(StringComparer.Ordinal);

            foreach (var area in areas)
            {
                foreach (var name in area.Names ?? [])
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var key = name.Trim();
                    var current = seen.GetValueOrDefault(key);

                    seen[key] = area.HasEncounters
                        ? (true, current.Without)
                        : (current.With, true);
                }
            }

            var byName = seen.ToDictionary(
                pair => pair.Key,
                pair => pair.Value switch
                {
                    (true, true) => (bool?)null,
                    (true, false) => true,
                    _ => false
                },
                StringComparer.Ordinal);

            return new JsonZoneEncounters(byName);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>
    /// True when the cartridge gives this place wild encounters, false when it does not, and null
    /// when it cannot say — no entry, or a parent whose areas disagree.
    /// </summary>
    public bool? HasEncounters(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName))
        {
            return null;
        }

        var name = zoneName.Trim();

        if (_byName.TryGetValue(name, out var direct))
        {
            return direct;
        }

        // «Ciudad Hauoli (Puerto)» hereda de «Ciudad Hauoli», que es como lo nombra el cartucho.
        var cut = name.IndexOf(" (", StringComparison.Ordinal);

        return cut > 0 && _byName.TryGetValue(name[..cut], out var parent) ? parent : null;
    }

    private sealed record ZoneFile(
        [property: JsonPropertyName("areas")] IReadOnlyList<AreaEntry>? Areas);

    private sealed record AreaEntry(
        [property: JsonPropertyName("names")] IReadOnlyList<string>? Names,
        [property: JsonPropertyName("hasEncounters")] bool HasEncounters);
}
