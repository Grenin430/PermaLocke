using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Infrastructure;

namespace PermaLocke.PlaqueLab;

/// <summary>What the lab takes from the project: the scenarios, with the icons the way the app takes them (from the player's cartridge) or, without a ROM, coloured blobs.</summary>
public sealed class LabData
{
    private readonly PokemonSpriteService? _sprites;
    private readonly Dictionary<int, BitmapSource> _blobs = [];

    private LabData(PokemonSpriteService? sprites)
    {
        _sprites = sprites;
        Scenarios = CapScenarios.All(Icon, item => _sprites?.GetItem(item));
    }

    public IReadOnlyList<CapScenario> Scenarios { get; }

    /// <summary>Whether the icons are the cartridge's.</summary>
    public bool RealIcons { get; private set; }

    public static LabData Load()
    {
        var root = Root();
        if (root is null) return new LabData(null);

        try
        {
            var sprites = new PokemonSpriteService(new AppPaths(root), NullLogger<PokemonSpriteService>.Instance);
            Task.Run(() => sprites.PrepareAsync()).GetAwaiter().GetResult();
            var data = new LabData(sprites);
            data.RealIcons = sprites.Get(130, 0, false) is not null;
            return data;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new LabData(null);
        }
    }

    /// <summary>The icon of a species: the cartridge's, or a blob of a colour taken from the number.</summary>
    public BitmapSource? Icon(int species)
    {
        if (_sprites?.Get(species, 0, false) is { } real) return real;
        if (_blobs.TryGetValue(species, out var blob)) return blob;

        var tint = (byte)(((species * 53) % 200) + 40);
        var pixels = new byte[40 * 30 * 4];
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var i = ((y * 40) + x) * 4;
                if (Math.Pow((x - 20) / 12.0, 2) + Math.Pow((y - 16) / 11.0, 2) > 1) continue;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(tint / 2), (byte)(255 - tint), tint, 255);
            }
        }

        var bitmap = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        bitmap.Freeze();
        return _blobs[species] = bitmap;
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
