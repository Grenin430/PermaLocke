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

    private string? _romPath;
    private string _scratch = string.Empty;
    private IReadOnlyDictionary<int, int>? _index;
    private bool _prepared;
    private string SpriteDirectory => Path.Combine(paths.Data, "sprites");

    /// <summary>The balls live apart so the Pokémon folder stays one file per icon index.</summary>
    private string BallDirectory => Path.Combine(paths.Data, "sprites", "balls");

    /// <summary>Item icons, named by <b>item id</b> and not by icon index: the two are not the same.</summary>
    private string ItemDirectory => Path.Combine(paths.Data, "sprites", "items");

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
            _romPath = rom.Path;
            _scratch = scratch;
            _index = await PokemonIconIndex.BuildAsync(rom.Path, scratch, ct);

            if (!Directory.Exists(SpriteDirectory) || Directory.GetFiles(SpriteDirectory, "*.png").Length == 0)
            {
                await Task.Run(() => Extract(rom.Path, scratch), ct);
            }

            if (!Directory.Exists(BallDirectory) || Directory.GetFiles(BallDirectory, "*.png").Length == 0)
            {
                await Task.Run(() => ExtractBalls(rom.Path, scratch), ct);
            }

            await Task.Run(() => ExtractItems(rom.Path, scratch), ct);

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

        var image = Read(Path.Combine(SpriteDirectory, $"{icon:0000}.png"));
        _cache[icon] = image;
        return image;
    }

    private BitmapSource? Read(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(file);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer el sprite {File}", file);
            return null;
        }
    }

    /// <summary>
    /// The icon of a Poké Ball, straight from the cartridge. Item 4 is the ordinary one.
    /// </summary>
    public BitmapSource? GetBall(int itemId = ItemIconReader.PokeBallItemId)
    {
        if (!_prepared)
        {
            return null;
        }

        var key = -itemId;   // negativo para no chocar con los índices de icono de Pokémon
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = Read(Path.Combine(BallDirectory, $"{itemId:000}.png"));
        _cache[key] = image;
        return image;
    }

    /// <summary>
    /// The sixteen balls, and only those: the rest of the 769 item icons are not wanted yet and
    /// writing them all would be a folder nobody asked for.
    /// </summary>
    private void ExtractBalls(string romPath, string scratch)
    {
        var reader = ItemIconReader.Open(romPath, scratch);
        Directory.CreateDirectory(BallDirectory);

        for (var item = 1; item <= ItemIconReader.LastBallItemId; item++)
        {
            if (!reader.Has(item))
            {
                continue;
            }

            var destination = Path.Combine(BallDirectory, $"{item:000}.png");
            if (File.Exists(destination))
            {
                continue;
            }

            var icon = reader.Read(item);
            File.WriteAllBytes(destination, PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
        }

        logger.LogInformation("Iconos de Poké Ball extraídos a {Folder}", BallDirectory);
    }

    /// <summary>
    /// The icon of any item whose index has been measured, straight from the cartridge.
    /// </summary>
    /// <remarks>
    /// Null for an item nobody has checked. The shop only asks for what it sells, and every one of
    /// those was identified on screen: see <see cref="ItemIconIndex"/>.
    /// </remarks>
    public BitmapSource? GetItem(int itemId)
    {
        if (!_prepared || !ItemIconIndex.TryGet(itemId, out var icon))
        {
            return null;
        }

        var key = -100_000 - itemId;   // otro tramo de claves, para no chocar con nada
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Se extrae el que falte, uno a uno. La lista previa solo cubría los objetos medidos, así
        // que todo lo que va por la regla directa —la Master Ball es el objeto 1— se quedaba sin
        // fichero y sin dibujo. Pedirlo es ahora lo que lo trae.
        var file = Path.Combine(ItemDirectory, $"{itemId:0000}.png");
        if (!File.Exists(file))
        {
            ExtractItem(itemId, icon, file);
        }

        var image = Read(file);
        _cache[key] = image;
        return image;
    }

    /// <summary>Pulls one item icon out of the cartridge, on demand.</summary>
    private void ExtractItem(int itemId, int icon, string destination)
    {
        if (_romPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(ItemDirectory);
            var sprite = ItemIconReader.Open(_romPath, _scratch).Read(icon + 1);
            File.WriteAllBytes(destination, PngImage.Encode(sprite.Pixels, sprite.Width, sprite.Height));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo extraer el icono del objeto {Item}", itemId);
        }
    }

    /// <summary>
    /// Extracts the icons of the items PermaLocke actually shows, and only those.
    /// </summary>
    /// <remarks>
    /// Cheap enough to check every time: it writes only what is missing, so adding an item to the
    /// shop makes its icon appear on the next start without anyone clearing a cache.
    /// </remarks>
    private void ExtractItems(string romPath, string scratch)
    {
        var wanted = ItemIconIndex.KnownItems
            .Where(item => !File.Exists(Path.Combine(ItemDirectory, $"{item:0000}.png")))
            .ToList();

        if (wanted.Count == 0)
        {
            return;
        }

        var reader = ItemIconReader.Open(romPath, scratch);
        Directory.CreateDirectory(ItemDirectory);

        foreach (var item in wanted)
        {
            var icon = reader.Read(ItemIconIndex.Of(item) + 1);
            File.WriteAllBytes(Path.Combine(ItemDirectory, $"{item:0000}.png"),
                PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
        }

        logger.LogInformation("{Count} iconos de objeto extraídos a {Folder}", wanted.Count, ItemDirectory);
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
