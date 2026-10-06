using System.IO;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;
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
    private readonly ConcurrentDictionary<int, BitmapSource> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _romPath;
    private string? _baseLayer;

    /// <summary>Cuántos iconos de objeto tiene el contenedor cargado, que dice si hay mod.</summary>
    private int _itemIcons = ItemIconIndex.CartridgeIcons;
    private string _scratch = string.Empty;
    private IReadOnlyDictionary<int, int>? _index;
    private IReadOnlyDictionary<(int Species, int Form), int> _formIndex =
        new Dictionary<(int Species, int Form), int>();
    private bool _prepared;
    private string _sourceKey = "pending";
    private string SpriteDirectory => Path.Combine(paths.Data, "sprites", "v2", _sourceKey);

    /// <summary>The balls live apart so the Pokémon folder stays one file per icon index.</summary>
    private string BallDirectory => Path.Combine(SpriteDirectory, "balls");

    /// <summary>Item icons, named by <b>item id</b> and not by icon index: the two are not the same.</summary>
    private string ItemDirectory => Path.Combine(SpriteDirectory, "items");

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
                return;
            }

            // Si hay un mod base, los iconos de Pokemon salen de EL: es donde estan los que anade,
            // y el cartucho no los tiene. Los de objeto se siguen leyendo del cartucho a proposito,
            // porque la tabla medida del §45 cubre los ids de vanilla, que son los unicos que la
            // tienda vende.
            var expansion = Path.Combine(paths.Expansion, "romfs");
            var baseLayer = Directory.Exists(expansion) ? expansion : null;

            // El temporal lleva el nombre del mundo: si compartiera nombre, instalar un mod dejaria
            // activo el contenedor del cartucho y las especies nuevas no dibujarian nada, sin que
            // nada fallara (§27).
            _sourceKey = SourceKey(rom.Path, baseLayer);
            var scratch = Path.Combine(SpriteDirectory, "source");
            _romPath = rom.Path;
            _scratch = scratch;
            _baseLayer = baseLayer;
            _index = await PokemonIconIndex.BuildAsync(rom.Path, scratch, baseLayer, ct);

            // Siempre, no solo cuando la carpeta esta vacia. Extract salta lo que ya existe, asi que
            // cuesta poco; y el guardia de antes -"si hay algun png, no extraigas"- habria dejado sin
            // dibujo a las 354 especies nuevas de quien instalara el mod DESPUES de haber jugado,
            // que es el caso normal y el que menos se prueba.
            var icons = await Task.Run(() => Extract(rom.Path, scratch), ct);

            // Las formas con icono propio: las de Alola del cartucho y las del mod, estas solo si el
            // contenedor es el que describen sus claves (§139). Si no, cada forma dibuja su especie.
            _formIndex = PokemonIconIndex.FormIcons(_index, icons);

            if (!Directory.Exists(BallDirectory) || Directory.GetFiles(BallDirectory, "*.png").Length == 0)
            {
                await Task.Run(() => ExtractBalls(rom.Path, scratch), ct);
            }

            // Cuantos iconos de objeto hay decide si valen las reglas del mod. Se lee del propio
            // contenedor, no de si la carpeta existe: la carpeta puede estar y el fichero no.
            _itemIcons = await Task.Run(() => ItemIconReader.Open(rom.Path, scratch, baseLayer).Count, ct);

            await Task.Run(() => ExtractItems(rom.Path, scratch), ct);

            _cache.Clear();
            _prepared = true;
            logger.LogInformation("Sprites listos: {Count} especies con icono conocido", _index.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Los sprites son decoración: que fallen no puede tumbar una pantalla.
            logger.LogWarning(ex, "No se han podido preparar los sprites; se seguirá sin ellos");
            _index = null;
            _prepared = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Each installation and source revision gets its own extraction cache.</summary>
    public static string SourceKey(string romPath, string? baseLayer)
    {
        static string Stamp(string file)
        {
            var info = new FileInfo(file);
            return info.Exists ? $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}" : file + "|missing";
        }
        var sources = new List<string> { Stamp(romPath) };
        if (baseLayer is not null)
            foreach (var file in new[] { PokemonIconReader.IconGarcPath, "a/0/6/1", GameFiles.Personal })
                sources.Add(Stamp(Path.Combine(baseLayer, file.Replace('/', Path.DirectorySeparatorChar))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", sources))))[..20];
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
    /// The icon of a species in a given form: its own when it has one, its species' otherwise.
    /// </summary>
    /// <remarks>
    /// Until §139 every picture in the app was the species', so an Alolan Vulpix was drawn as a
    /// Kantonian one. A form with no icon of its own — a Vivillon's wings — still draws its species.
    /// </remarks>
    public BitmapSource? Get(int species, int form) => IconOf(species, form) is { } icon ? Load(icon) : null;

    /// <summary>
    /// The icon of a species in a form, in its shiny colours when <paramref name="shiny"/> is set.
    /// </summary>
    /// <remarks>
    /// The cartridge has no shiny icons. <c>Data/variocolor.json</c> carries, per icon, what each colour turns into
    /// (<see cref="ShinyPalette"/>, measured against reference renders), and the player's own icon is repainted
    /// with it. An icon without a table — six have no usable reference — is drawn in its normal colours: the shiny
    /// is still said in words wherever the screen says it.
    /// </remarks>
    public BitmapSource? Get(int species, int form, bool shiny)
    {
        if (!shiny || IconOf(species, form) is not { } icon)
        {
            return Get(species, form);
        }

        if (_shinyCache.TryGetValue(icon, out var cached))
        {
            return cached;
        }

        var normal = Load(icon);
        if (normal is null || !ShinyTables.Value.TryGetValue(icon, out var table))
        {
            return normal;
        }

        var source = new FormatConvertedBitmap(normal, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);

        for (var p = 0; p < pixels.Length; p += 4)
        {
            if (pixels[p + 3] != 0 && table.TryGetValue((pixels[p + 2] << 16) | (pixels[p + 1] << 8) | pixels[p], out var colour))
            {
                pixels[p] = (byte)colour;
                pixels[p + 1] = (byte)(colour >> 8);
                pixels[p + 2] = (byte)(colour >> 16);
            }
        }

        var shinyIcon = BitmapSource.Create(source.PixelWidth, source.PixelHeight, normal.DpiX, normal.DpiY,
            System.Windows.Media.PixelFormats.Bgra32, null, pixels, stride);
        shinyIcon.Freeze();
        _shinyCache[icon] = shinyIcon;
        return shinyIcon;
    }

    private int? IconOf(int species, int form) =>
        _index is null ? null
        : form > 0 && _formIndex.TryGetValue((species, form), out var formIcon) ? formIcon
        : _index.TryGetValue(species, out var icon) ? icon
        : null;

    private readonly ConcurrentDictionary<int, BitmapSource> _shinyCache = new();

    /// <summary>Colour tables of <c>Data/variocolor.json</c> by icon index, read once. Missing or broken: no tables.</summary>
    private Lazy<IReadOnlyDictionary<int, Dictionary<int, int>>> ShinyTables => _shinyTables ??= new(ReadShinyTables);
    private Lazy<IReadOnlyDictionary<int, Dictionary<int, int>>>? _shinyTables;

    private IReadOnlyDictionary<int, Dictionary<int, int>> ReadShinyTables()
    {
        var file = Path.Combine(paths.Data, "variocolor.json");
        var tables = new Dictionary<int, Dictionary<int, int>>();

        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
            foreach (var entry in json.RootElement.GetProperty("iconos").EnumerateObject())
            {
                tables[int.Parse(entry.Name)] = entry.Value.GetString()!
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair => pair.Split('>'))
                    .ToDictionary(pair => Convert.ToInt32(pair[0], 16), pair => Convert.ToInt32(pair[1], 16));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sin colores variocolor: no se pudo leer {File}", file);
        }

        return tables;
    }

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
        if (image is not null) _cache[icon] = image;
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
        if (image is not null) _cache[key] = image;
        return image;
    }

    /// <summary>
    /// The sixteen balls, and only those: the rest of the 769 item icons are not wanted yet and
    /// writing them all would be a folder nobody asked for.
    /// </summary>
    private void ExtractBalls(string romPath, string scratch)
    {
        var reader = ItemIconReader.Open(romPath, scratch, _baseLayer);
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
        // Los cristales Z no están en el contenedor de objetos, así que van por su propia puerta.
        // Se atiende aquí y no en un método aparte para que quien pide un dibujo solo tenga que
        // saber el id del objeto, que es lo único que hay escrito en Data/achievements.json.
        var crystal = ZCrystalIndex.TryGet(itemId, out _);
        var superCandy = itemId == SuperCandy.ItemId;

        if (!_prepared || (!crystal && !superCandy && !ItemIconIndex.TryGet(itemId, out _, _itemIcons)))
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
            ExtractItem(itemId, file, crystal);
        }

        var image = Read(file);
        if (image is not null) _cache[key] = image;
        return image;
    }

    /// <summary>The official move-category icons, apart from the rest: three files, named by category.</summary>
    private string CategoryDirectory => Path.Combine(SpriteDirectory, "categorias");

    /// <summary>
    /// The official icon of a move category (§144): the red-orange burst for physical, the blue rings for special,
    /// the grey one for status. By the numbering of <c>MoveSheet</c>: 1 physical, 2 special, 0 status.
    /// </summary>
    /// <remarks>
    /// Taken from the player's own cartridge the first time it is asked for, like every other sprite. Null when there
    /// is no ROM or the sheet could not be read, and the screen then says the category in words.
    /// </remarks>
    public BitmapSource? GetCategory(int category)
    {
        var name = CategoryFile(category);

        if (!_prepared || name is null)
        {
            return null;
        }

        var key = -200_000 - category;
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var file = Path.Combine(CategoryDirectory, name);
        if (!File.Exists(file))
        {
            ExtractCategories();
        }

        var image = Read(file);
        if (image is not null) _cache[key] = image;
        return image;
    }

    private static string? CategoryFile(int category) => category switch
    {
        0 => "estado.png",
        1 => "fisico.png",
        2 => "especial.png",
        _ => null
    };

    private void ExtractCategories()
    {
        if (_romPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(CategoryDirectory);

            foreach (var (category, icon) in MoveCategoryIconReader.Open(_romPath, _scratch))
            {
                var name = CategoryFile(category switch
                {
                    MoveCategoryIcon.Physical => 1,
                    MoveCategoryIcon.Special => 2,
                    _ => 0
                })!;

                File.WriteAllBytes(Path.Combine(CategoryDirectory, name),
                    PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron extraer los iconos de categoría de movimiento");
        }
    }

    /// <summary>Pulls one item icon out of the cartridge, on demand.</summary>
    private void ExtractItem(int itemId, string destination, bool crystal)
    {
        if (_romPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(ItemDirectory);

            // El SuperCarameloraro se pinta desde el Caramelo Raro, igual que el icono que se pone en el juego.
            var sprite = crystal
                ? ZCrystalIconReader.Open(_romPath, _scratch).Read(itemId)
                : itemId == SuperCandy.ItemId
                    ? ItemIconReader.Open(_romPath, _scratch, _baseLayer).ReadSuperCandy()
                    : ItemIconReader.Open(_romPath, _scratch, _baseLayer).Read(ItemIconIndex.Of(itemId, _itemIcons) + 1);

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
        var wanted = ItemIconIndex.KnownItems(_itemIcons)
            .Where(item => !File.Exists(Path.Combine(ItemDirectory, $"{item:0000}.png")))
            .ToList();

        if (wanted.Count == 0)
        {
            return;
        }

        var reader = ItemIconReader.Open(romPath, scratch, _baseLayer);
        Directory.CreateDirectory(ItemDirectory);

        foreach (var item in wanted)
        {
            var icon = reader.Read(ItemIconIndex.Of(item, _itemIcons) + 1);
            File.WriteAllBytes(Path.Combine(ItemDirectory, $"{item:0000}.png"),
                PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
        }

        logger.LogInformation("{Count} iconos de objeto extraídos a {Folder}", wanted.Count, ItemDirectory);
    }

    /// <returns>How many icons the container holds, which says which form icons exist.</returns>
    private int Extract(string romPath, string scratch)
    {
        var reader = PokemonIconReader.Open(romPath, scratch, _baseLayer);
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
        return reader.Count;
    }
}
