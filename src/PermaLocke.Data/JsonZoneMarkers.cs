using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermaLocke.Data;

/// <param name="X">Across the island's picture, 0 at the left edge and 1 at the right.</param>
/// <param name="Y">Down the island's picture, 0 at the top and 1 at the bottom.</param>
/// <remarks>
/// The names are spelled out because the file is written in Spanish, like every other one under
/// <c>Data/</c>. Left to the defaults, «isla» would not match <c>Island</c> and every marker would
/// load as null — quietly, with an empty map and nothing in the log.
/// </remarks>
public sealed record ZoneMarker(
    [property: JsonPropertyName("isla")] string Island,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

/// <summary>
/// Where each zone's marker sits on its island, in <c>Data/marcadores.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one piece of the map the cartridge does not provide. The island art is in there and
/// comes out fine, but nothing in the RomFS says where Ruta 3 is on the picture — the candidate
/// table turned out to be Pokémon skeletons — so the positions are placed by hand, by somebody who
/// has played the game, and this file is what that produces.
/// </para>
/// <para>
/// Stored as <b>fractions of the picture</b> and not pixels, so a marker keeps its place if the
/// map is ever re-extracted at another size or cropped differently. The keys are the same
/// normalised zone ids the run stores against each capture, so a marker and a Pokémon are talking
/// about the same place by construction.
/// </para>
/// <para>
/// Unlike the map images, this file <b>is</b> PermaLocke's own work and travels with it: one
/// player places the markers and everybody in the competition gets them.
/// </para>
/// </remarks>
public sealed class JsonZoneMarkers
{
    private readonly Dictionary<string, ZoneMarker> _markers;

    private JsonZoneMarkers(Dictionary<string, ZoneMarker> markers) => _markers = markers;

    public static JsonZoneMarkers Empty => new([]);

    public IReadOnlyDictionary<string, ZoneMarker> All => _markers;

    public int Count => _markers.Count;

    public static JsonZoneMarkers Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<MarkerFile>(stream);

            if (file?.Markers is not { Count: > 0 } entries)
            {
                return Empty;
            }

            var markers = new Dictionary<string, ZoneMarker>(StringComparer.Ordinal);

            foreach (var (zone, marker) in entries)
            {
                // Una posicion fuera del cuadro no se corrige, se descarta: un marcador pegado al
                // borde porque alguien edito el fichero a mano miente sobre donde esta esa zona.
                if (marker is null || string.IsNullOrWhiteSpace(marker.Island)
                    || marker.X is < 0 or > 1 || marker.Y is < 0 or > 1)
                {
                    continue;
                }

                markers[zone] = new ZoneMarker(marker.Island.Trim(), marker.X, marker.Y);
            }

            return new JsonZoneMarkers(markers);
        }
        catch (JsonException)
        {
            // Un fichero roto no puede tumbar el mapa: se dibuja sin marcadores.
            return Empty;
        }
    }

    public ZoneMarker? For(string zoneId) => _markers.GetValueOrDefault(zoneId);

    /// <summary>Places or moves one marker. Returns a new store; the old one is left alone.</summary>
    public JsonZoneMarkers With(string zoneId, ZoneMarker marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);

        var markers = new Dictionary<string, ZoneMarker>(_markers, StringComparer.Ordinal)
        {
            [zoneId] = marker
        };

        return new JsonZoneMarkers(markers);
    }

    public JsonZoneMarkers Without(string zoneId)
    {
        var markers = new Dictionary<string, ZoneMarker>(_markers, StringComparer.Ordinal);
        markers.Remove(zoneId);
        return new JsonZoneMarkers(markers);
    }

    /// <summary>
    /// Writes the file, to a temporary first and then moved into place.
    /// </summary>
    /// <remarks>
    /// The move is not ceremony: this is written every time a marker is dropped, and a crash
    /// halfway through a plain write leaves a truncated file, which loads as no markers at all.
    /// Placing them is the one part of this nobody can automate, so losing them costs real work.
    /// </remarks>
    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        var document = new MarkerFile(
            "Donde va el marcador de cada zona sobre el mapa de su isla, en fraccion de la imagen. "
            + "Lo coloca una persona desde la aplicacion: el cartucho no lo dice en ninguna parte.",
            _markers.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => (ZoneMarker?)pair.Value));

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json, ct).ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed record MarkerFile(
        [property: JsonPropertyName("comentario")] string? Comment,
        [property: JsonPropertyName("marcadores")] IReadOnlyDictionary<string, ZoneMarker?>? Markers);
}
