using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

public class AzaharInstallationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-azahar-" + Guid.NewGuid());
    private readonly AzaharInstallation _installation = new(NullLogger<AzaharInstallation>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// Azahar's own rule: a "user" folder next to the executable wins over %APPDATA%. Shipping
    /// the emulator is what spares the player from configuring anything.
    /// </summary>
    [Fact]
    public void A_bundled_emulator_is_used_and_is_portable()
    {
        var emulator = Path.Combine(_root, "Emulator");
        Directory.CreateDirectory(emulator);
        File.WriteAllText(Path.Combine(emulator, "azahar.exe"), "");

        var location = _installation.Locate(_root);

        Assert.True(location.IsPortable);
        Assert.Equal(Path.Combine(emulator, "user"), location.UserDirectory);
        Assert.NotNull(location.ExecutablePath);
    }

    /// <summary>
    /// Asked several times a second — every save read, every tick of the battle mode — this wrote a line each time:
    /// 8.672 of the 14.120 lines of a day's log said the same thing, and a player's report of crashes arrived with its
    /// log drowned in them (§167). It still looks every time, so an emulator that appears is still seen.
    /// </summary>
    [Fact]
    public void The_same_answer_is_written_to_the_log_once()
    {
        var log = new CountingLog();
        var installation = new AzaharInstallation(log);
        var emulator = Path.Combine(_root, "Emulator");
        Directory.CreateDirectory(_root);

        for (var call = 0; call < 5; call++)
        {
            installation.Locate(_root);
        }

        Assert.Equal(1, log.Lines);

        // Y lo que cambia sí se dice: aparece el emulador propio.
        Directory.CreateDirectory(emulator);
        File.WriteAllText(Path.Combine(emulator, "azahar.exe"), "");

        Assert.True(installation.Locate(_root).IsPortable);
        Assert.Equal(2, log.Lines);

        installation.Locate(_root);
        Assert.Equal(2, log.Lines);
    }

    private sealed class CountingLog : ILogger<AzaharInstallation>
    {
        public int Lines { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> format) => Lines++;
    }

    [Fact]
    public void Without_a_bundled_emulator_the_installed_one_is_used()
    {
        Directory.CreateDirectory(_root);

        var location = _installation.Locate(_root);

        Assert.False(location.IsPortable);
        Assert.Null(location.ExecutablePath);
        Assert.EndsWith("Azahar", location.UserDirectory);
    }

    /// <summary>The setting Azahar ships disabled and only lets you change with emulation stopped.</summary>
    [Fact]
    public void The_rpc_server_is_switched_on_in_an_existing_config()
    {
        var location = new AzaharLocation(_root, null, false);
        var config = Path.Combine(_root, "config", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllLines(config,
        [
            "[Controls]", "profile=0",
            "[Debugging]", @"enable_rpc_server\default=true", "enable_rpc_server=false",
            "[UI]", "theme=dark",
        ]);

        Assert.True(_installation.EnsureRpcEnabled(location));

        var lines = File.ReadAllLines(config);
        Assert.Contains("enable_rpc_server=true", lines);
        Assert.Contains(@"enable_rpc_server\default=false", lines);
        Assert.DoesNotContain("enable_rpc_server=false", lines);
        Assert.Contains("theme=dark", lines);   // el resto del fichero intacto
        Assert.Contains("profile=0", lines);
    }

    [Fact]
    public void A_missing_config_is_created_with_the_setting_on()
    {
        var location = new AzaharLocation(_root, null, false);

        Assert.True(_installation.EnsureRpcEnabled(location));

        var lines = File.ReadAllLines(Path.Combine(_root, "config", "qt-config.ini"));
        Assert.Contains("[Debugging]", lines);
        Assert.Contains("enable_rpc_server=true", lines);
    }

    [Fact]
    public void A_config_that_is_already_right_is_left_alone()
    {
        var location = new AzaharLocation(_root, null, false);
        _installation.EnsureRpcEnabled(location);
        var config = Path.Combine(_root, "config", "qt-config.ini");
        var before = File.GetLastWriteTimeUtc(config);
        var contents = File.ReadAllText(config);

        _installation.EnsureRpcEnabled(location);

        Assert.Equal(contents, File.ReadAllText(config));
        Assert.Equal(before, File.GetLastWriteTimeUtc(config));
    }

    [Fact]
    public void The_mod_folder_is_the_one_layered_fs_reads()
    {
        var location = new AzaharLocation(_root, null, false);

        Assert.Equal(Path.Combine(_root, "load", "mods", "00040000001B5100"),
            AzaharInstallation.ModDirectory(location, "00040000001B5100"));
    }

    /// <summary>
    /// With <c>plugin_loader\default=true</c> Azahar ignores <c>plugin_loader=true</c> and loads nothing: measured with
    /// the follower plugin. Turning it on has to copy the plugin and write both keys.
    /// </summary>
    [Fact]
    public void The_follower_is_installed_and_the_loader_really_turned_on()
    {
        var location = new AzaharLocation(_root, null, false);
        var config = Path.Combine(_root, "config", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllLines(config, ["[System]", @"plugin_loader\default=true", "plugin_loader=false"]);
        var plugin = Path.Combine(_root, "shipped.3gx");
        File.WriteAllBytes(plugin, [1, 2, 3]);

        Assert.True(_installation.SetFollower(location, plugin, on: true));

        var lines = File.ReadAllLines(config);
        Assert.Contains("plugin_loader=true", lines);
        Assert.Contains(@"plugin_loader\default=false", lines);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(_root, "sdmc", "luma", "plugins", "00040000001B5100",
            AzaharInstallation.FollowerPluginName)));
    }

    /// <summary>Off switches the loader off and deletes nothing.</summary>
    [Fact]
    public void Turning_the_follower_off_keeps_the_file()
    {
        var location = new AzaharLocation(_root, null, false);
        var plugin = Path.Combine(_root, "shipped.3gx");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(plugin, [1, 2, 3]);
        _installation.SetFollower(location, plugin, on: true);

        Assert.True(_installation.SetFollower(location, plugin, on: false));

        Assert.Contains("plugin_loader=false", File.ReadAllLines(Path.Combine(_root, "config", "qt-config.ini")));
        Assert.True(File.Exists(Path.Combine(_root, "sdmc", "luma", "plugins", "00040000001B5100", AzaharInstallation.FollowerPluginName)));
    }

    /// <summary>Without the shipped plugin the loader is not turned on: there would be nothing to load.</summary>
    [Fact]
    public void Without_the_plugin_the_loader_stays_as_it_was()
    {
        var location = new AzaharLocation(_root, null, false);

        Assert.False(_installation.SetFollower(location, Path.Combine(_root, "missing.3gx"), on: true));
        Assert.False(File.Exists(Path.Combine(_root, "config", "qt-config.ini")));
    }

    [Fact]
    public void Launching_puts_the_cameras_blank()
    {
        var user = Path.Combine(_root, "user");
        Directory.CreateDirectory(Path.Combine(user, "config"));
        File.WriteAllText(Path.Combine(user, "config", "qt-config.ini"), "[Camera]\ncamera_inner_name\\default=true\ncamera_inner_name=qt\n");

        Assert.True(_installation.DisableSaveStates(new AzaharLocation(user, null, true)));

        var ini = File.ReadAllText(Path.Combine(user, "config", "qt-config.ini"));
        Assert.Contains("camera_inner_name=blank", ini);
        Assert.Contains("camera_outer_left_name=blank", ini);
        Assert.DoesNotContain("camera_inner_name=qt", ini);
    }

    [Fact]
    public void The_graphics_are_written_before_the_game_opens_and_vulkan_only_when_asked()
    {
        // Las claves de Azahar llevan una barra invertida: «clave\default».
        static string Default(string key) => key + (char)92 + "default";

        var user = Path.Combine(_root, "user");
        Directory.CreateDirectory(Path.Combine(user, "config"));
        var ini = Path.Combine(user, "config", "qt-config.ini");
        File.WriteAllLines(ini,
        [
            "[Renderer]", "resolution_factor=1", Default("resolution_factor") + "=true",
            Default("graphics_api") + "=true", "graphics_api=1"
        ]);
        var location = new AzaharLocation(user, null, true);

        Assert.True(_installation.SetGraphics(location, 3, vulkan: false));
        var text = File.ReadAllText(ini);
        Assert.Contains("resolution_factor=3", text);
        Assert.Contains(Default("resolution_factor") + "=false", text);
        Assert.Contains("graphics_api=1", text);                // sin pedirlo, el renderizador no se toca

        Assert.True(_installation.SetGraphics(location, 99, vulkan: true));
        text = File.ReadAllText(ini);
        Assert.Contains("resolution_factor=4", text);           // el tope es x4
        Assert.Contains("graphics_api=2", text);
        Assert.Contains(Default("graphics_api") + "=false", text);

        Assert.True(_installation.SetGraphics(location, 1, vulkan: true));
        Assert.Contains(Default("resolution_factor") + "=true", File.ReadAllText(ini));
// Con 0 la resolución del emulador no se toca.        File.WriteAllLines(ini, ["[Renderer]", "resolution_factor=3"]);        Assert.True(_installation.SetGraphics(location, 0, vulkan: false));        Assert.Contains("resolution_factor=3", File.ReadAllText(ini));
    }
}
