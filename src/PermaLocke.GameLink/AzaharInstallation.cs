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

    /// <summary>What was said last, so the same answer is not written to the log again.</summary>
    /// <remarks>
    /// This is asked several times a second — every save read, every tick of the battle mode — and it used to write a
    /// line each time: 8.672 of the 14.120 lines of a day's log, 61 % of it, all saying the same thing. A log that
    /// drowns its own findings is worse than a quiet one, and this was in the way of reading a player's report (§167).
    /// The look-up itself stays: it is one <c>File.Exists</c>, and an emulator that appears mid-session has to be seen.
    /// </remarks>
    private string? _said;

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
            Say($"Azahar propio encontrado en {executable}");
            return new AzaharLocation(portableUser, executable, IsPortable: true);
        }

        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar");
        Say($"Sin emulador propio; se usa el instalado en {installed}");
        return new AzaharLocation(installed, null, IsPortable: false);
    }

    private void Say(string what)
    {
        if (_said == what)
        {
            return;
        }

        _said = what;
        logger.LogInformation("{What}", what);
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
    /// Stops Azahar asking «Would you like to exit now?» when its window is closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the launcher (§125). PermaLocke closes the game by asking its window to close, which is the
    /// clean way — Azahar stops the emulation and saves its own settings — but with this on the
    /// request ends in a dialog nobody is looking at and the game stays open. The launcher asks its
    /// own question first, in Spanish and saying what is lost, so the emulator's second one is only
    /// in the way.
    /// </para>
    /// <para>
    /// Written with the emulator closed, like the RPC setting: Azahar saves its settings on exit, and
    /// one running would put the old value back.
    /// </para>
    /// </remarks>
    /// <returns>True when the file ends up with the confirmation off.</returns>
    public bool DisableCloseConfirmation(AzaharLocation location)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = SetValue(lines, "confirmClose", "false", "[UI]");
            changed |= SetValue(lines, @"confirmClose\default", "false", "[UI]");

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Confirmación de cierre de Azahar desactivada en {Path}", configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo desactivar la confirmación de cierre en {Path}", configPath);
            return false;
        }
    }

    /// <summary>
    /// Stops Azahar putting «Jugando a Azahar» in the player's Discord profile, so PermaLocke's own
    /// «Jugando a PermaLocke» is the one shown (§185).
    /// </summary>
    /// <remarks>
    /// Written with the emulator closed, like the RPC setting, and with its <c>\default</c> flag, or Azahar would put its
    /// default back. A build without Discord support ignores the setting.
    /// </remarks>
    /// <returns>True when the file ends up with the emulator's presence off.</returns>
    public bool DisableDiscordPresence(AzaharLocation location)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = SetValue(lines, "enable_discord_presence", "false", "[UI]");
            changed |= SetValue(lines, @"enable_discord_presence\default", "false", "[UI]");

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Estado de Discord de Azahar desactivado en {Path}", configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo desactivar el estado de Discord de Azahar en {Path}", configPath);
            return false;
        }
    }

    /// <summary>File name of the follower plugin, as its author ships it.</summary>
    public const string FollowerPluginName = "Gen7FieldFollower.3gx";

    /// <summary>
    /// Turns the follower Pokémon on or off: the 3GX plugin that makes the lead of the party walk behind the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is someone else's mod («Pokemon Follower Mod», by Aqua_, gamebanana.com/mods/694400, CC BY-NC-ND), shipped
    /// unmodified in <c>Emulator/follower</c>. It runs inside the emulated console and touches only the model on the
    /// map: not the party, not the battle, not the save. Tested with PermaLocke reading the party and the zone live,
    /// and by the player with Pokémon of generations 8 and 9 and wild battles.
    /// </para>
    /// <para>
    /// On: the plugin is copied to <c>sdmc/luma/plugins/&lt;title&gt;</c> when missing or different, and the loader is
    /// switched on. Both <c>plugin_loader</c> and <c>plugin_loader\default</c> are written: with the second left at
    /// true, Azahar ignores the value and loads nothing — measured. Off: only the loader goes off; the file stays, and
    /// nothing is deleted. Written with the emulator closed, like the RPC setting.
    /// </para>
    /// </remarks>
    /// <param name="plugin">The shipped plugin; when it is missing the loader is not switched on.</param>
    /// <returns>True when the configuration ends up as asked.</returns>
    public bool SetFollower(AzaharLocation location, string plugin, bool on, string titleId = "00040000001B5100")
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            if (on)
            {
                if (!File.Exists(plugin))
                {
                    logger.LogWarning("Sin Pokémon que te sigue: falta el plugin en {Path}", plugin);
                    return false;
                }

                var folder = Path.Combine(location.UserDirectory, "sdmc", "luma", "plugins", titleId);
                var target = Path.Combine(folder, FollowerPluginName);
                Directory.CreateDirectory(folder);

                if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(plugin)))
                {
                    File.Copy(plugin, target, overwrite: true);
                    logger.LogInformation("Plugin del Pokémon que te sigue instalado en {Path}", target);
                }

                // Azahar carga un solo 3GX por juego: si hay otro, puede que se cargue ese y no este.
                foreach (var other in Directory.GetFiles(folder, "*.3gx").Where(f => !f.EndsWith(FollowerPluginName, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Hay otro plugin 3GX junto al del Pokémon que te sigue: {Path}", other);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = SetValue(lines, "plugin_loader", on ? "true" : "false", "[System]");
            changed |= SetValue(lines, @"plugin_loader\default", "false", "[System]");
            changed |= SetValue(lines, "allow_plugin_loader", "true", "[System]");

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Pokémon que te sigue {State} en {Path}", on ? "activado" : "desactivado", configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo cambiar el Pokémon que te sigue en {Path}", configPath);
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
