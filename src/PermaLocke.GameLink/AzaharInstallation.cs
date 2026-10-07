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
    /// The emulator's internal resolution (1 to 4 times the console's) and, when asked, the Vulkan renderer (2026-10-07). Written
    /// with the emulator closed, at every launch: changing the resolution inside a running game leaves a black or zoomed
    /// picture that flickers, and Azahar saves its own settings on exit and would undo it.
    /// </summary>
    public bool SetGraphics(AzaharLocation location, int resolution, bool vulkan)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = false;
            var factor = Math.Clamp(resolution, 1, 4);

            // 0 es «la que tenga el emulador»: no se toca.
            if (resolution > 0)
            {
                changed |= SetValue(lines, "resolution_factor", factor.ToString(), "[Renderer]");
                changed |= SetValue(lines, @"resolution_factor\default", factor == 1 ? "true" : "false", "[Renderer]");
            }

            if (vulkan)
            {
                changed |= SetValue(lines, "graphics_api", "2", "[Renderer]");
                changed |= SetValue(lines, @"graphics_api\default", "false", "[Renderer]");
            }

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Gráficos de Azahar: resolución x{Factor}{Api} en {Path}", factor, vulkan ? ", Vulkan" : string.Empty, configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron poner los gráficos de Azahar en {Path}", configPath);
            return false;
        }
    }

    /// <summary>
    /// The emulation speed limit, in percent of the console (2026-10-06, asked by the organiser: 200 % every time the game
    /// opens). Written with the emulator closed, like the other settings: Azahar saves its own on exit.
    /// </summary>
    public bool SetSpeedLimit(AzaharLocation location, int percent)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = SetValue(lines, "frame_limit", percent.ToString(System.Globalization.CultureInfo.InvariantCulture), "[Renderer]");
            changed |= SetValue(lines, @"frame_limit\default", "false", "[Renderer]");

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Velocidad de Azahar al {Percent} % en {Path}", percent, configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo poner la velocidad de Azahar en {Path}", configPath);
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

    /// <summary>Azahar's shortcuts that save or load a state, or restart the game without closing the window.</summary>
    private static readonly string[] ReloadShortcuts =
    [
        "Quick%20Save", "Quick%20Load", "Save%20to%20Oldest%20Non-Quicksave%20Slot",
        "Load%20from%20Newest%20Non-Quicksave%20Slot", "Restart%20Emulation"
    ];

    /// <summary>
    /// Takes the keys off Azahar's save state and restart shortcuts, so a death cannot be undone with one key (2026-09-26).
    /// </summary>
    /// <remarks>
    /// The menu entries stay, because Azahar has no setting for them: a state saved from the menu is what
    /// <see cref="SetAsideSaveStates"/> catches. Each key is written with its <c>\default</c> flag, or Azahar puts its
    /// default back, and with the emulator closed, like the rest.
    /// </remarks>
    public bool DisableSaveStates(AzaharLocation location)
    {
        var configPath = Path.Combine(location.UserDirectory, "config", "qt-config.ini");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            var lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : [];
            var changed = false;

            foreach (var shortcut in ReloadShortcuts)
            {
                var key = $@"Shortcuts\Main%20Window\{shortcut}";
                changed |= SetValue(lines, $@"{key}\KeySeq\default", "false", "[UI]");
                changed |= SetValue(lines, $@"{key}\KeySeq", string.Empty, "[UI]");
                changed |= SetValue(lines, $@"{key}\controller_keyseq\default", "false", "[UI]");
                changed |= SetValue(lines, $@"{key}\controller_keyseq", string.Empty, "[UI]");
            }

            // Las cámaras en blanco, en cada arranque (1.0.5.6): el tutorial de fotos de Hauoli dejó a un jugador con la
            // pantalla en negro, y una cámara del PC que no contesta cuelga el juego ahí. En el Nuzlocke nadie la usa.
            foreach (var camera in new[] { "camera_outer_right_name", "camera_inner_name", "camera_outer_left_name" })
            {
                changed |= SetValue(lines, $@"{camera}\default", "false", "[Camera]");
                changed |= SetValue(lines, camera, "blank", "[Camera]");
            }

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
                logger.LogInformation("Atajos de estados guardados y reinicio de Azahar desactivados en {Path}", configPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron desactivar los estados guardados de Azahar en {Path}", configPath);
            return false;
        }
    }

    /// <summary>
    /// Moves every save state of the emulator out of its reach, into <paramref name="destination"/> (2026-09-26).
    /// </summary>
    /// <remarks>
    /// Moved and not deleted: they are the player's files. A state Azahar is still writing cannot be moved yet and is
    /// tried again on the next call.
    /// </remarks>
    /// <returns>The names of the states moved.</returns>
    public IReadOnlyList<string> SetAsideSaveStates(AzaharLocation location, string destination)
    {
        var folder = Path.Combine(location.UserDirectory, "states");
        var moved = new List<string>();

        if (!Directory.Exists(folder))
        {
            return moved;
        }

        foreach (var state in Directory.EnumerateFiles(folder))
        {
            try
            {
                Directory.CreateDirectory(destination);
                var name = Path.GetFileName(state);
                File.Move(state, Path.Combine(destination, $"{DateTime.Now:yyyyMMdd-HHmmss}-{name}"));
                moved.Add(name);
            }
            catch (IOException ex)
            {
                logger.LogDebug(ex, "El estado {State} sigue en uso; se retira en la próxima vuelta", state);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "No se puede retirar el estado {State}", state);
            }
        }

        return moved;
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
