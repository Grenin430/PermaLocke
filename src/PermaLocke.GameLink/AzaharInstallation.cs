using Microsoft.Extensions.Logging;

namespace PermaLocke.GameLink;

/// <param name="UserDirectory">Where Azahar keeps config, mods and saves.</param>
/// <param name="ExecutablePath">The emulator itself, when PermaLocke ships one.</param>
/// <param name="IsPortable">True when the user directory sits next to the executable.</param>
public sealed record AzaharLocation(string UserDirectory, string? ExecutablePath, bool IsPortable);

/// <summary>
/// Finds the Azahar the player is going to use and makes sure it is configured for PermaLocke.
/// <para>
/// Azahar picks its user directory itself, and the rule is in its own source: if a folder named
/// <c>user</c> sits next to the executable it wins, and otherwise it falls back to
/// <c>%APPDATA%\Azahar</c>. Creating that folder is the whole of "portable mode", which is what
/// lets PermaLocke ship an emulator that does not fight with one the player already had.
/// </para>
/// </summary>
public sealed class AzaharInstallation(ILogger<AzaharInstallation> logger)
{
    /// <summary>Folder next to the executable that turns Azahar portable.</summary>
    private const string UserFolderName = "user";

    /// <summary>Where a bundled emulator is expected to live, relative to the app.</summary>
    private const string BundledFolderName = "Emulator";

    private const string ExecutableName = "azahar.exe";

    /// <summary>
    /// Prefers the emulator PermaLocke ships, so the player never has to pick one, and falls
    /// back to whatever Azahar they already had installed.
    /// </summary>
    public AzaharLocation Locate(string appDirectory)
    {
        var bundled = Path.Combine(appDirectory, BundledFolderName);
        var executable = Path.Combine(bundled, ExecutableName);

        if (File.Exists(executable))
        {
            var portableUser = Path.Combine(bundled, UserFolderName);
            logger.LogInformation("Azahar propio encontrado en {Path}", executable);
            return new AzaharLocation(portableUser, executable, IsPortable: true);
        }

        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar");
        logger.LogInformation("Sin emulador propio; se usa el instalado en {Path}", installed);
        return new AzaharLocation(installed, null, IsPortable: false);
    }

    /// <summary>
    /// Turns the RPC server on, which PermaLocke needs to see the game at all and which Azahar
    /// ships disabled. Doing it here saves every player from a setting they can only change with
    /// the emulation stopped.
    /// </summary>
    /// <returns>True when the file ends up with the server enabled.</returns>
    public bool EnsureRpcEnabled(AzaharLocation location)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath)
                ? File.ReadAllLines(configPath).ToList()
                : [];

            // Azahar stores a companion "\default" flag beside each setting; leaving it saying
            // "this is the default" would let the emulator write the value back to false.
            var changed = SetValue(lines, "enable_rpc_server", "true", "[Debugging]");
            changed |= SetValue(lines, @"enable_rpc_server\default", "false", "[Debugging]");
            changed |= LetTheRpcServerSpeak(lines);

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Servidor RPC activado en {Path}", configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo activar el servidor RPC en {Path}", configPath);
            return false;
        }
    }

    /// <summary>
    /// Raises the RPC server's logging so what it does can be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Azahar ships <c>log_filter=*:Info RPC_Server:Error …</c>, which silences everything the RPC
    /// server says short of a failure. That is a sensible default for a server nobody watches — and
    /// it makes the fork's block watcher completely invisible, including the one line that proves
    /// it is doing its job.
    /// </para>
    /// <para>
    /// Only that one word is changed, and only when it is <c>Error</c>. Whatever else the player
    /// has in their filter is left exactly as it was, and a filter that has already been raised is
    /// not touched again.
    /// </para>
    /// </remarks>
    private static bool LetTheRpcServerSpeak(List<string> lines)
    {
        var at = lines.FindIndex(l => l.TrimStart().StartsWith("log_filter=", StringComparison.Ordinal));

        if (at < 0)
        {
            return false; // sin filtro escrito, el emulador usa el suyo y ya deja hablar a todos
        }

        var updated = lines[at].Replace("RPC_Server:Error", "RPC_Server:Info", StringComparison.Ordinal);

        if (updated == lines[at])
        {
            return false;
        }

        lines[at] = updated;
        return true;
    }

    /// <summary>
    /// Sets a key inside an INI section, creating the section or the key when missing.
    /// </summary>
    /// <returns>True when the file needed changing.</returns>
    private static bool SetValue(List<string> lines, string key, string value, string section)
    {
        var wanted = $"{key}={value}";
        var sectionStart = lines.FindIndex(l => l.Trim().Equals(section, StringComparison.OrdinalIgnoreCase));

        if (sectionStart < 0)
        {
            lines.Add(string.Empty);
            lines.Add(section);
            lines.Add(wanted);
            return true;
        }

        // The section ends where the next one starts.
        var sectionEnd = lines.FindIndex(sectionStart + 1, l => l.TrimStart().StartsWith('['));
        if (sectionEnd < 0)
        {
            sectionEnd = lines.Count;
        }

        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            if (!lines[i].TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lines[i] == wanted)
            {
                return false;
            }

            lines[i] = wanted;
            return true;
        }

        lines.Insert(sectionEnd, wanted);
        return true;
    }

    /// <summary>
    /// Where a randomization has to be written for this installation to load it. LayeredFS turns
    /// itself on merely because the folder exists.
    /// </summary>
    public static string ModDirectory(AzaharLocation location, string programId) =>
        Path.Combine(location.UserDirectory, "load", "mods", programId);
}
