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

    /// <summary>
    /// Empties the mod folder, keeping the one that was there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Emptying is not optional: a file left over from a previous generation stays active and the
    /// report cheerfully says the module is off while the game plays it (§27). But the folder that
    /// gets emptied <b>is the world somebody is playing in</b>, and once it is gone there is no way
    /// to prove a reinstall changed nothing — which is exactly the position a reinstall of one
    /// fixed module should never leave anyone in.
    /// </para>
    /// <para>
    /// So the old one is moved aside first, next to <c>mods</c> rather than inside it: the emulator
    /// picks a mod by a folder named exactly the program id, and nothing here should tempt it.
    /// A move, not a copy, because this is half a gigabyte and both live on the same volume. Only
    /// the last one is kept; older ones would fill the disk for no one.
    /// </para>
    /// </remarks>
    public string? Clear()
    {
        var root = Directory.GetParent(RomFsDirectory)!.FullName;

        if (!Directory.Exists(root))
        {
            _staged.Clear();
            return null;
        }

        var saved = KeepAside(root);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        _staged.Clear();
        return saved;
    }

    /// <summary>Moves the installed mod out of the way, and returns where it went.</summary>
    private static string? KeepAside(string root)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(root).Any())
            {
                return null;
            }

            var mods = Directory.GetParent(root);
            var shelf = Path.Combine(mods?.Parent?.FullName ?? root, "permalocke-mod-anterior");

            if (Directory.Exists(shelf))
            {
                Directory.Delete(shelf, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(shelf)!);
            Directory.Move(root, shelf);
            return shelf;
        }
        catch (Exception)
        {
            // Guardar la anterior es una red de seguridad, no un requisito: si no se puede -otro
            // volumen, permisos-, la randomización sigue. Lo que no puede es dejar de vaciarse.
            return null;
        }
    }
}
