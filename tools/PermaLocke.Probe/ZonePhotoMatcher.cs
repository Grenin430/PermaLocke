using System.Text.Encodings.Web;
using System.Text.Json;
using PermaLocke.Data;
using PermaLocke.Rules.Services;

namespace PermaLocke.Probe;

/// <summary>
/// Matches the zone photographs in <c>islas/</c> to the zones they belong to.
/// </summary>
/// <remarks>
/// <para>
/// The files come named as a wiki names them — <c>285px-Ruta_4_(Alola).png</c>,
/// <c>1200px-Ciudad_Konikoni_USUL.png</c> — so most of the work is stripping the decoration and
/// most of them then match by themselves. What does not match is <b>listed and fixed by hand</b>
/// here rather than guessed at, and the whole thing <b>has to close</b>: every marker gets a photo
/// and every photo gets used, or the tool says which ones did not.
/// </para>
/// <para>
/// That closure is the point. A quiet mismatch would put a picture of Ruta 4 on Ruta 5 and look
/// perfectly fine — the same failure shape as the icon table of §30, which is why that one counts
/// its own leftovers too.
/// </para>
/// <para>
/// A file marked <c>(COMPLETO)</c> covers a zone and its sub-zones in one picture, so it is matched
/// by prefix: <c>Ciudad_Hauoli(COMPLETO)</c> answers for «Ciudad Hauoli (Puerto)» and «Ciudad
/// Hauoli (Zona Comercial)» alike.
/// </para>
/// </remarks>
public static class ZonePhotoMatcher
{
    /// <summary>
    /// The ones whose filename does not resemble the zone's name at all. Written out rather than
    /// pattern-matched: five special cases in a rule are a rule nobody can read.
    /// </summary>
    private static readonly Dictionary<string, string> ByHand = new(StringComparer.Ordinal)
    {
        ["escuela de entrenadores"] = "ruta-1-escuela-entrenadores",
        ["afueras de hauoli"] = "ruta-1-afueras-de-hauoli",
        ["area volcanica del wela"] = "area-volcanica-wela",
        ["bocetos parque de malie"] = "parque-de-malie",
        ["cabo de las afueras(ciudad malie)"] = "ciudad-malie-cabo-de-las-afueras",
        ["supermercado ultraganga (local abandonado)"] = "super-ultraganga-local-abandonado",
        ["monte lanakila exterior"] = "monte-lanakila"
    };

    public static int Run(string root)
    {
        var folder = Path.Combine(root, "islas");

        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"No existe {folder}.");
            return 1;
        }

        var markers = JsonZoneMarkers.Load(Path.Combine(root, "Data", "marcadores.json"));
        var islands = JsonIslandMap.Load(Path.Combine(root, "Data", "islas.json"));

        // Solo las zonas que estan EN el mapa: una foto de un sitio sin marcador no se puede ver.
        var wanted = islands.Zones
            .Select(zone => (zone.Island, zone.Name, Id: EncounterService.NormaliseLocationId(zone.Name)))
            .Where(zone => markers.For(zone.Id) is not null)
            .ToArray();

        var photos = new Dictionary<string, string>(StringComparer.Ordinal);
        var unused = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var island = Path.GetFileName(Path.GetDirectoryName(file))!;
            var key = Simplify(Path.GetFileNameWithoutExtension(file));
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            var complete = Path.GetFileNameWithoutExtension(file)
                .Contains("(COMPLETO)", StringComparison.Ordinal);

            var hit = ByHand.TryGetValue(key, out var forced)
                ? wanted.Where(zone => zone.Id == forced).ToArray()
                : Matches(wanted, island, key, complete);

            if (hit.Length == 0)
            {
                unused.Add($"{island}/{Path.GetFileName(file)}   (buscaba «{key}»)");
                continue;
            }

            foreach (var zone in hit)
            {
                photos[zone.Id] = relative;
            }
        }

        var missing = wanted.Where(zone => !photos.ContainsKey(zone.Id)).ToArray();

        Console.WriteLine($"{wanted.Length} zonas con marcador, {photos.Count} con foto");
        Console.WriteLine();

        foreach (var zone in missing)
        {
            Console.WriteLine($"    SIN FOTO   {zone.Island,-9} {zone.Name}  ({zone.Id})");
        }

        foreach (var file in unused)
        {
            Console.WriteLine($"    SIN ZONA   {file}");
        }

        if (missing.Length > 0 || unused.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("No cierra: no se escribe nada hasta que cada zona tenga su foto y");
            Console.WriteLine("cada foto su zona. Una que casara mal pondria Ruta 4 sobre Ruta 5.");
            return 1;
        }

        var path = Path.Combine(root, "Data", "fotos.json");
        var document = new
        {
            comentario = "Foto de cada zona, relativa a la carpeta de la aplicacion. Generado por "
                + "«Probe --fotos»: empareja los ficheros de islas/ con las zonas del mapa y se "
                + "niega a escribir si alguna se queda sin pareja.",
            fotos = photos.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value)
        };

        File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        Console.WriteLine($"Cierra: {photos.Count} fotos escritas en {path}");
        return 0;
    }

    /// <summary>
    /// The zones a file answers for: the one whose name it is, or all those it is a prefix of.
    /// </summary>
    private static (string Island, string Name, string Id)[] Matches(
        (string Island, string Name, string Id)[] zones, string island, string key, bool complete)
    {
        var id = EncounterService.NormaliseLocationId(key);
        var here = zones.Where(zone => string.Equals(zone.Island, island, StringComparison.Ordinal)).ToArray();

        var exact = here.Where(zone => zone.Id == id).ToArray();

        // Un «(COMPLETO)» responde por la zona Y por sus sublugares, asi que no se para en el
        // exacto: Colina Dequilate y su Caldera Remota estan las dos en la misma foto, y quedarse
        // con la primera dejaba a la segunda sin nada.
        if (exact.Length > 0 && !complete)
        {
            return exact;
        }

        return [.. exact.Concat(
            here.Where(zone => zone.Id.StartsWith(id + "-", StringComparison.Ordinal)))];
    }

    /// <summary>Strips everything a wiki filename carries that the zone's name does not.</summary>
    private static string Simplify(string name)
    {
        var text = name;

        // «285px-», «1200px-»
        var dash = text.IndexOf("px-", StringComparison.Ordinal);

        if (dash > 0 && text[..dash].All(char.IsAsciiDigit))
        {
            text = text[(dash + 3)..];
        }

        text = text.Replace("(COMPLETO)", string.Empty, StringComparison.Ordinal)
            .Replace("_USUL", string.Empty, StringComparison.Ordinal)
            .Replace("_(Alola)", string.Empty, StringComparison.Ordinal)
            .Replace('_', ' ')
            .Trim();

        return Fold(text).ToLowerInvariant();
    }

    private static string Fold(string text) => text
        .Replace("á", "a", StringComparison.OrdinalIgnoreCase)
        .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
        .Replace("í", "i", StringComparison.OrdinalIgnoreCase)
        .Replace("ó", "o", StringComparison.OrdinalIgnoreCase)
        .Replace("ú", "u", StringComparison.OrdinalIgnoreCase)
        .Replace("ñ", "n", StringComparison.OrdinalIgnoreCase)
        .Replace("Á", "A", StringComparison.Ordinal)
        .Replace("É", "E", StringComparison.Ordinal)
        .Replace("Í", "I", StringComparison.Ordinal)
        .Replace("Ó", "O", StringComparison.Ordinal)
        .Replace("Ú", "U", StringComparison.Ordinal);
}
