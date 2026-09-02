using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Rom;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.App.Services;

/// <summary>
/// The four island maps, taken from the player's own cartridge the first time they are needed.
/// </summary>
/// <remarks>
/// <para>
/// Same rule as the sprites (§28): PermaLocke ships no map art, because it is Nintendo's. The
/// pictures are stitched out of the ROM the player already provided and cached as PNG under
/// <c>Data/mapa-islas</c>, which is in <c>.gitignore</c>. What PermaLocke does ship is where the
/// markers go, which is its own work.
/// </para>
/// <para>
/// It never throws. Without the maps the screen falls back to the numbered board, which works
/// perfectly well — a missing picture must not cost anybody their zone tracking.
/// </para>
/// </remarks>
public sealed class IslandMapService(AppPaths paths, ILogger<IslandMapService> logger)
{
    private readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _prepared;

    private string MapDirectory => Path.Combine(paths.Data, "mapa-islas");

    /// <summary>True once at least one island picture is on disk.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>The key a map file is named by, from the island's display name.</summary>
    public static string KeyFor(string island) =>
        island.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    public async Task PrepareAsync(CancellationToken ct = default)
    {
        if (_prepared)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_prepared)
            {
                return;
            }

            Directory.CreateDirectory(MapDirectory);

            // Si ya estan, no se vuelven a coser: son varios segundos de descomprimir 866 laminas.
            if (Directory.GetFiles(MapDirectory, "*.png").Length >= IslandMapReader.Islands.Length)
            {
                IsAvailable = true;
                _prepared = true;
                return;
            }

            var rom = RomInspector.ScanFolder(paths.Rom).FirstOrDefault(r => r.IsSupported);

            if (rom is null)
            {
                logger.LogInformation("Sin mapas: no hay ROM compatible en {Folder}", paths.Rom);
                _prepared = true;
                return;
            }

            var scratch = Path.Combine(Path.GetTempPath(), "permalocke-mapa");

            await Task.Run(() =>
            {
                var reader = IslandMapReader.Open(rom.Path, scratch);

                foreach (var island in reader.ReadAll())
                {
                    File.WriteAllBytes(Path.Combine(MapDirectory, $"{island.Name}.png"),
                        PngImage.Encode(island.Pixels, island.Width, island.Height));
                }
            }, ct);

            IsAvailable = true;
            _prepared = true;
            logger.LogInformation("Mapas de isla listos en {Folder}", MapDirectory);
        }
        catch (Exception ex)
        {
            // El mapa es presentacion: que falle no puede tumbar una pantalla ni perder una zona.
            logger.LogWarning(ex, "No se han podido preparar los mapas; se seguirá sin ellos");
            _prepared = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>One island's picture, or null when it is not there.</summary>
    public BitmapSource? Map(string island)
    {
        var key = KeyFor(island);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(MapDirectory, $"{key}.png");

        if (!File.Exists(path))
        {
            _cache[key] = null;
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();

            _cache[key] = image;
            return image;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo cargar el mapa de {Island}", island);
            _cache[key] = null;
            return null;
        }
    }
}
