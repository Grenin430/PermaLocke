using PKHeX.Core;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// Where the player's Ultra Moon save lives, and whether the emulator is holding it right now.
/// </summary>
/// <remarks>
/// Shared by everything that touches the save so the answer is the same for all of them: the
/// delivery, which refuses to write while the game is loaded, and the box reader, which does
/// read but warns that what it shows is the last thing the player saved.
/// </remarks>
public sealed class PlayerSave(AzaharInstallation installation, AzaharRpcClient client, string appDirectory)
{
    /// <summary>Ultra Moon (Europe), the only title PermaLocke targets.</summary>
    private const string TitleFolder = "001b5100";

    /// <summary>Where the 3DS keeps a title's save inside the emulated SD card.</summary>
    private static string SavePattern => Path.Combine("sdmc", "Nintendo 3DS");

    /// <summary>The save file, or null when it cannot be found.</summary>
    public string? Find()
    {
        var root = Path.Combine(installation.Locate(appDirectory).UserDirectory, SavePattern);

        if (!Directory.Exists(root))
        {
            return null;
        }

        try
        {
            // Las dos carpetas intermedias son identificadores de consola; se buscan en vez de
            // suponerlas, porque Azahar las genera y no siempre son ceros.
            return Directory
                .EnumerateFiles(root, "main", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Contains(TitleFolder, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// The title's save-data folder, <c>…/001b5100/data</c>, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Not the same question as <see cref="Find"/>, and the difference is the whole reason this
    /// exists. Azahar keeps the save as an archive: a folder holding <c>main</c>, plus a sibling
    /// <c>&lt;name&gt;.metadata</c> recording that the archive is <b>formatted</b>. A save can
    /// therefore be missing while the archive still claims to exist, and that combination is what
    /// the game reports as corrupted data. Deleting a partida means clearing both, so whatever
    /// does the deleting has to be able to see the folder even when there is no <c>main</c> in it.
    /// </remarks>
    public string? FindArchive()
    {
        var root = Path.Combine(installation.Locate(appDirectory).UserDirectory, SavePattern);

        if (!Directory.Exists(root))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateDirectories(root, TitleFolder, SearchOption.AllDirectories)
                .Select(title => Path.Combine(title, "data"))
                .FirstOrDefault(Directory.Exists);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The play time the game wrote in the save the last time it saved, or null when it cannot be read.</summary>
    /// <remarks>What the integrity check compares (2026-09-26): it only grows while somebody plays and saves.</remarks>
    public TimeSpan? PlayTime()
    {
        try
        {
            return Find() is { } path && SaveUtil.TryGetSaveFile(path, out var loaded) && loaded is not null
                ? new TimeSpan(loaded.PlayedHours, loaded.PlayedMinutes, loaded.PlayedSeconds)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>True when the game is loaded in the emulator.</summary>
    public bool IsGameLoaded()
    {
        try
        {
            return client.ListProcesses()
                .Any(process => process.TitleId == AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception)
        {
            // Sin servidor RPC no hay emulador escuchando, así que no hay juego cargado.
            return false;
        }
    }
}
