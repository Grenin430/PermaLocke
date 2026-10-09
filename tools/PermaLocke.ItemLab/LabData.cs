using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PKHeX.Core;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.ItemLab;

/// <summary>One item of the game as the lab lists it.</summary>
public sealed record Entry(int Id, string Name, ItemClass Class)
{
    public override string ToString() => $"{Id} - {Name}";
}

/// <summary>
/// What the lab reads from the project: the item table, the names, and the icons the way the app takes them (2026-10-09):
/// <see cref="PokemonSpriteService.GetItem"/>, which reads the game's own item to icon table and takes each icon out of the
/// cartridge. The parcel is only what <see cref="ItemScene.Parcel"/> is for the app: the last resort.
/// </summary>
public sealed class LabData
{
    private readonly Dictionary<int, (byte[] Pixels, int Width, int Height)> _icons = [];
    private readonly PokemonSpriteService _sprites;

    /// <summary>The mod's own items, which PKHeX knows by the name of the slot they took.</summary>
    private static readonly Dictionary<int, string> ModNames = new()
    {
        [113] = "SUPER CARAMELO", [114] = "REPELENTE INFINITO", [115] = "INCUBADORA TURBO",
        [997] = "CLEFABLITA", [1000] = "DRAGONINITA", [1011] = "CHESNAUGHTITA", [1019] = "FALINKSITA", [1023] = "DARKRANITA"
    };

    public LabData(PokemonSpriteService sprites, string root)
    {
        _sprites = sprites;
        var names = GameInfo.GetStrings("es").itemlist;
        var inEnglish = GameInfo.GetStrings("en").itemlist;
        var catalog = ItemCatalog.Empty;

        var tables = Path.Combine(root, "Expansion", "romfs", "a", "0", "1");
        if (File.Exists(Path.Combine(tables, "9")) && File.Exists(Path.Combine(tables, "5")))
        {
            using var table = new GarcPatcher(Path.Combine(tables, "9"));
            var pockets = FieldItemRandomizer.ReadPockets([.. Enumerable.Range(0, table.FileCount).Select(table.Read)]);
            catalog = new ItemCatalog(pockets, MegaTrainerRandomizer.ReadStones(Path.Combine(tables, "5")).ToHashSet());
        }

        Catalog = catalog;
        Entries = [.. Enumerable.Range(1, 1023)
            .Where(id => id < names.Length && id < inEnglish.Length && inEnglish[id] != "???" && !string.IsNullOrWhiteSpace(names[id]))
            .Select(id => new Entry(id, ModNames.GetValueOrDefault(id) ?? names[id].ToUpperInvariant(), catalog.Classify(id)))];
    }

    public ItemCatalog Catalog { get; }

    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>True when the game draws an icon for this item, so the lab shows the real one and not the parcel.</summary>
    public bool HasIcon(int id) => PokemonSpriteService.HasItemIcon(id);

    /// <summary>The icon from the cartridge as BGRA, or the parcel the app uses when the game has none.</summary>
    public (byte[] Pixels, int Width, int Height) Icon(int id)
    {
        if (_icons.TryGetValue(id, out var cached)) return cached;

        var icon = ItemScene.Parcel();
        if (_sprites.GetItem(id) is { } bitmap)
        {
            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            icon = (pixels, bgra.PixelWidth, bgra.PixelHeight);
        }

        return _icons[id] = icon;
    }

    /// <summary>The folder of the repository: where <c>PermaLocke.slnx</c>, <c>ROM</c> and <c>Expansion</c> are.</summary>
    public static string? Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PermaLocke.slnx"))) return folder.FullName;
        }

        return null;
    }
}
