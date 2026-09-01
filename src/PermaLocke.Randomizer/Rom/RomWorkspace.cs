using pk3DS.Core;
using PermaLocke.Randomizer.Modules;

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
    /// Which of the workspace's files came from a base layer rather than from the cartridge.
    /// </summary>
    /// <remarks>
    /// Empty for an ordinary randomization. It is reported and written into the run's
    /// <c>RomRandomized</c> event, because "which world was this generated from" is exactly the
    /// kind of thing that must not be deduced later from today's configuration — the same lesson as
    /// §64, where a credit had to be read from what the event said rather than from the settings.
    /// </remarks>
    public IReadOnlyList<string> BaseLayerFiles { get; init; } = [];

    /// <summary>How many species the loaded tables actually describe.</summary>
    /// <remarks>
    /// Read from the personal table and <b>not</b> from <c>Config.Info.MaxSpeciesID</c>, which is a
    /// pk3DS constant fixed at 807 that never looks at the loaded files. Vanilla answers 807; the
    /// gen 8-9 expansion answers 1025. See <see cref="PersonalEntry7.SpeciesCount"/>.
    /// </remarks>
    public int MaxSpecies => _maxSpecies ??= ReadMaxSpecies();

    private int? _maxSpecies;

    private int ReadMaxSpecies()
    {
        using var personal = new GarcPatcher(PathOf(GameFiles.Personal));
        return PersonalEntry7.SpeciesCount(personal.Read(personal.FileCount - 1));
    }

    /// <summary>
    /// Extracts the needed files out of <paramref name="romPath"/> into
    /// <paramref name="directory"/> and loads pk3DS on top of them.
    /// </summary>
    /// <param name="deleteOnDispose">
    /// True when the directory is a temporary of ours and should be cleaned up.
    /// </param>
    /// <param name="baseLayer">
    /// A romfs folder whose files take precedence over the cartridge's, for randomizing on top of
    /// another mod instead of on top of vanilla. Null is the ordinary case.
    /// </param>
    public static async Task<RomWorkspace> ExtractAsync(string romPath, string directory,
        int language = SpanishLanguage, bool deleteOnDispose = false, string? baseLayer = null,
        CancellationToken ct = default)
    {
        var reader = new RomFsReader(romPath);
        System.IO.Directory.CreateDirectory(directory);

        var fromLayer = new List<string>();

        foreach (var file in GameFiles.All)
        {
            ct.ThrowIfCancellationRequested();
            var destination = Path.Combine(directory, file.Replace('/', Path.DirectorySeparatorChar));

            if (TakeFromLayer(baseLayer, file, destination))
            {
                fromLayer.Add(file);
                continue;
            }

            if (!await Task.Run(() => reader.ExtractTo(file, destination), ct))
            {
                throw new InvalidDataException(
                    $"La ROM no contiene {file}. ¿Es realmente Pokémon Ultra Luna sin encriptar?");
            }
        }

        var config = new GameConfig(GameVersion.USUM) { Language = language };
        await Task.Run(() => config.Initialize(directory, directory, language), ct);
        return new RomWorkspace(directory, config, deleteOnDispose) { BaseLayerFiles = fromLayer };
    }

    /// <summary>
    /// Copies one file out of the base layer, if that layer has it.
    /// </summary>
    /// <remarks>
    /// A layer that only replaces some of the tables is the normal case, not an error: the
    /// expansion mod ships eighteen of the files the randomizer reads and leaves the rest alone, so
    /// whatever it does not carry has to come from the cartridge or the workspace would be a
    /// mixture with holes in it. What is <em>not</em> tolerated is a file that exists and cannot be
    /// read: that is a broken layer, and falling back to vanilla there would quietly randomize the
    /// wrong world.
    /// </remarks>
    private static bool TakeFromLayer(string? baseLayer, string file, string destination)
    {
        if (baseLayer is null)
        {
            return false;
        }

        var source = Path.Combine(baseLayer, file.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(source))
        {
            return false;
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
        return true;
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
