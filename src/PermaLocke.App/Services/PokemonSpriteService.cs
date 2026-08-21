using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Rom;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.App.Services;

/// <summary>
/// The Pokémon icons, taken from the player's own cartridge.
/// </summary>
/// <remarks>
/// <para>
/// PermaLocke ships no sprites: they are Nintendo's. The first time one is asked for, the icons
/// are extracted from the ROM the player already provided — the same one the randomizer reads —
/// and cached as PNG under <c>Data/sprites</c>, which is in <c>.gitignore</c>.
/// </para>
/// <para>
/// All 807 species have their icon (ARCHITECTURE.md §28 and §30). Anything the index does not
/// know — a cartridge whose counts stop adding up, so the table refuses to build — returns null
/// and the screen simply shows no picture: better nothing than somebody else's Pokémon.
/// </para>
/// </remarks>
public sealed class PokemonSpriteService(AppPaths paths, ILogger<PokemonSpriteService> logger)
{
    /// <summary>Loaded icons, keyed by icon index rather than species: forms share a picture.</summary>
    private readonly Dictionary<int, BitmapSource?> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyDictionary<int, int>? _index;
    private bool _prepared;
    private string SpriteDirectory => Path.Combine(paths.Data, "sprites");

    /// <summary>True when the icons are on disk and the index loaded.</summary>
    public bool IsAvailable => _prepared && _index is { Count: > 0 };

    /// <summary>
    /// Makes sure the icons and the index are ready. Safe to call repeatedly; the work happens
    /// once. Never throws: without sprites the app carries on, it just draws none.
    /// </summary>
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

            var rom = RomInspector.ScanFolder(paths.Rom).FirstOrDefault(r => r.IsSupported);
            if (rom is null)
            {
                logger.LogInformation("Sin sprites: no hay ROM compatible en {Folder}", paths.Rom);
                _prepared = true;
                return;
            }

            var scratch = Path.Combine(Path.GetTempPath(), "permalocke-sprites");
            _index = await PokemonIconIndex.BuildAsync(rom.Path, scratch, ct);

            if (!Directory.Exists(SpriteDirectory) || Directory.GetFiles(SpriteDirectory, "*.png").Length == 0)
            {
                await Task.Run(() => Extract(rom.Path, scratch), ct);
            }

            _prepared = true;
            logger.LogInformation("Sprites listos: {Count} especies con icono conocido", _index.Count);
        }
        catch (Exception ex)
        {
            // Los sprites son decoración: que fallen no puede tumbar una pantalla.
            logger.LogWarning(ex, "No se han podido preparar los sprites; se seguirá sin ellos");
            _index = null;
            _prepared = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Any icon, for decoration. Used by the gacha reel, where the silhouettes flying past mean
    /// nothing: they are not candidates and never were.
    /// </summary>
    public BitmapSource? GetRandom(Random random)
    {
        if (_index is not { Count: > 0 })
        {
            return null;
        }

        var species = _index.Keys.ElementAt(random.Next(_index.Count));
        return Get(species);
    }


    /// <summary>The icon of a species, or null when it is not one of the known ones.</summary>
    public BitmapSource? Get(int species) =>
        _index is not null && _index.TryGetValue(species, out var icon) ? Load(icon) : null;

    /// <summary>
    /// The egg, which is the one icon of the container that needed no working out: it is the
    /// first one.
    /// </summary>
    public BitmapSource? GetEgg() => _prepared ? Load(PokemonIconIndex.EggIcon) : null;

    private BitmapSource? Load(int icon)
    {
        if (_cache.TryGetValue(icon, out var cached))
        {
            return cached;
        }

        BitmapSource? image = null;
        var file = Path.Combine(SpriteDirectory, $"{icon:0000}.png");

        if (File.Exists(file))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                image = bitmap;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo leer el sprite {File}", file);
            }
        }

        _cache[icon] = image;
        return image;
    }

    private void Extract(string romPath, string scratch)
    {
        var reader = PokemonIconReader.Open(romPath, scratch);
        Directory.CreateDirectory(SpriteDirectory);

        for (var i = 0; i < reader.Count; i++)
        {
            var destination = Path.Combine(SpriteDirectory, $"{i:0000}.png");
            if (File.Exists(destination))
            {
                continue;
            }

            var icon = reader.Read(i);
            File.WriteAllBytes(destination, PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
        }

        logger.LogInformation("{Count} iconos extraídos de la ROM a {Folder}", reader.Count, SpriteDirectory);
    }
}
