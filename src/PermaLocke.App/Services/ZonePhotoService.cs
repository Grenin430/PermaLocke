using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PermaLocke.Data;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// The picture of each zone, shown on the card that appears when hovering a marker.
/// </summary>
/// <remarks>
/// <para>
/// Which file belongs to which zone is decided by <c>Probe --fotos</c>, which refuses to write
/// <c>Data/fotos.json</c> unless every zone on the map has a picture and every picture has a zone.
/// A mismatch there would put Ruta 4 on Ruta 5 and look perfectly right, so it is the kind of thing
/// that has to close rather than mostly work.
/// </para>
/// <para>
/// Loaded lazily and cached: sixty-one pictures is fourteen megabytes, and most sessions hover over
/// a handful. Anything that fails to load returns null and the card shows the name alone.
/// </para>
/// </remarks>
public sealed class ZonePhotoService(AppPaths paths, ILogger<ZonePhotoService> logger)
{
    private readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.Ordinal);
    private readonly JsonZonePhotos _index =
        JsonZonePhotos.Load(Path.Combine(paths.Data, "fotos.json"));

    public int Count => _index.Count;

    public BitmapSource? Photo(string zoneId)
    {
        if (_cache.TryGetValue(zoneId, out var cached))
        {
            return cached;
        }

        var relative = _index.For(zoneId);
        var path = relative is null ? null : Path.Combine(paths.Root, relative);

        if (path is null || !File.Exists(path))
        {
            _cache[zoneId] = null;
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;

            // Se carga reducida: la carta mide 280 y algunas fotos vienen a 1200 de ancho.
            image.DecodePixelWidth = 280;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();

            _cache[zoneId] = image;
            return image;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo cargar la foto de {Zone}", zoneId);
            _cache[zoneId] = null;
            return null;
        }
    }
}
