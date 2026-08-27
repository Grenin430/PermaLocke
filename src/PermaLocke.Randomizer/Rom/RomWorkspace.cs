using pk3DS.Core;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// A scratch copy of the handful of RomFS files the randomizer needs, plus the pk3DS
/// <see cref="GameConfig"/> that reads them.
/// <para>
/// The vanilla cartridge is opened read-only and never written to. Extracting only these files
/// takes under a second, against several minutes and 3,5 GB for a full RomFS dump.
/// </para>
/// </summary>
public sealed class RomWorkspace : IDisposable
{
    /// <summary>pk3DS language index. 6 is Spanish; see its own language list.</summary>
    public const int SpanishLanguage = 6;

    private readonly bool _owned;

    private RomWorkspace(string directory, GameConfig config, bool owned)
    {
        Directory = directory;
        Config = config;
        _owned = owned;
    }

    /// <summary>Where the extracted files live. One subdirectory per RomFS path.</summary>
    public string Directory { get; }

    public GameConfig Config { get; }

    /// <summary>
    /// Extracts the needed files out of <paramref name="romPath"/> into
    /// <paramref name="directory"/> and loads pk3DS on top of them.
    /// </summary>
    /// <param name="deleteOnDispose">
    /// True when the directory is a temporary of ours and should be cleaned up.
    /// </param>
    public static async Task<RomWorkspace> ExtractAsync(string romPath, string directory,
        int language = SpanishLanguage, bool deleteOnDispose = false, CancellationToken ct = default)
    {
        var reader = new RomFsReader(romPath);
        System.IO.Directory.CreateDirectory(directory);

        foreach (var file in GameFiles.All)
        {
            ct.ThrowIfCancellationRequested();
            var destination = Path.Combine(directory, file.Replace('/', Path.DirectorySeparatorChar));
            if (!await Task.Run(() => reader.ExtractTo(file, destination), ct))
            {
                throw new InvalidDataException(
                    $"La ROM no contiene {file}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
            }
        }

        var config = new GameConfig(GameVersion.USUM) { Language = language };
        await Task.Run(() => config.Initialize(directory, directory, language), ct);
        return new RomWorkspace(directory, config, deleteOnDispose);
    }

    /// <summary>Absolute path of one extracted RomFS file.</summary>
    public string PathOf(string romfsPath) =>
        Path.Combine(Directory, romfsPath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (!_owned || !System.IO.Directory.Exists(Directory))
        {
            return;
        }

        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover scratch directory is not worth failing a randomization over.
        }
    }
}
