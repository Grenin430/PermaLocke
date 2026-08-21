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
