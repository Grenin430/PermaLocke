using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Output;

/// <summary>
/// The mod folder Azahar reads: <c>&lt;user&gt;/load/mods/&lt;ProgramId&gt;/romfs/</c>.
/// <para>
/// Azahar turns LayeredFS on merely because that folder exists — there is no setting to enable
/// (<c>ncch_container.cpp</c>, <c>use_layered_fs</c> defaults to true) — and it logs
/// <c>LayeredFS replacement file in use for /path</c> for each file it takes, which is the
/// cheapest way to confirm a mod was loaded without looking at the screen.
/// </para>
/// </summary>
public sealed class LayeredFsMod(RomWorkspace workspace, string modDirectory)
{
    /// <summary>Ultra Moon (Europe). Read from the ROM's NCCH header, not assumed.</summary>
    public const string UltraMoonProgramId = "00040000001B5100";

    private readonly List<string> _staged = [];

    public string RomFsDirectory { get; } = Path.Combine(modDirectory, "romfs");

    /// <summary>Files written so far, as RomFS paths.</summary>
    public IReadOnlyList<string> StagedFiles => _staged;

    /// <summary>Builds the mod path for an Azahar user directory.</summary>
    public static string DirectoryFor(string azaharUserDirectory) =>
        Path.Combine(azaharUserDirectory, "load", "mods", UltraMoonProgramId);

    /// <summary>
    /// Copies the vanilla file into the mod folder so it can be patched there, and returns its
    /// path. The extracted workspace copy stays pristine.
    /// </summary>
    public string Stage(string romfsPath)
    {
        var destination = Path.Combine(RomFsDirectory, romfsPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(workspace.PathOf(romfsPath), destination, overwrite: true);
        if (!_staged.Contains(romfsPath))
        {
            _staged.Add(romfsPath);
        }
        return destination;
    }

    /// <summary>Puts a file back to vanilla and forgets it. Used when a verification fails.</summary>
    public void Revert(string romfsPath)
    {
        var destination = Path.Combine(RomFsDirectory, romfsPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }
        _staged.Remove(romfsPath);
    }

    /// <summary>Writes bytes straight into the mod folder, for files rebuilt whole.</summary>
    public async Task WriteAsync(string romfsPath, byte[] data, CancellationToken ct = default)
    {
        var destination = Path.Combine(RomFsDirectory, romfsPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(destination, data, ct);
        if (!_staged.Contains(romfsPath))
        {
            _staged.Add(romfsPath);
        }
    }

    /// <summary>Deletes the whole mod folder, returning the game to vanilla.</summary>
    public void Clear()
    {
        var root = Directory.GetParent(RomFsDirectory)!.FullName;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        _staged.Clear();
    }
}
