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
}
