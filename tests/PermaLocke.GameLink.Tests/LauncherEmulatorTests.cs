using Microsoft.Extensions.Logging.Abstractions;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Which <c>azahar.exe</c> the launcher starts, and the one setting it changes before starting it (§125).
/// </summary>
public sealed class LauncherEmulatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-lanzador-{Guid.NewGuid():N}");

    public LauncherEmulatorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Exe(string folder, DateTime built, bool portable = false)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);

        if (portable)
        {
            Directory.CreateDirectory(Path.Combine(dir, "user"));
        }

        var path = Path.Combine(dir, "azahar.exe");
        File.WriteAllText(path, "exe");
        File.SetLastWriteTimeUtc(path, built);
        return path;
    }

    private static AzaharLocation Installed => new(@"C:\AppData\Azahar", null, IsPortable: false);

    [Fact]
    public void The_bundled_emulator_always_wins()
    {
        var bundled = Exe("Emulator", new DateTime(2026, 1, 1));
        var newer = Exe("Nuevo_azahar", new DateTime(2026, 9, 1));

        var choice = AzaharExecutable.Choose(new AzaharLocation(Path.Combine(_root, "Emulator", "user"), bundled, true),
            null, [newer]);

        Assert.Equal(bundled, choice.Executable);
    }

    /// <summary>The repository holds the official Azahar from August next to the fork: the newest build is the fork.</summary>
    [Fact]
    public void Among_installed_ones_the_newest_build_wins()
    {
        var official = Exe("Azahar", new DateTime(2026, 8, 10));
        var fork = Exe("Nuevo_azahar", new DateTime(2026, 9, 6));

        Assert.Equal(fork, AzaharExecutable.Choose(Installed, null, [official, fork]).Executable);
    }

    /// <summary>
    /// A portable copy keeps its saves in its own folder while PermaLocke reads AppData: starting it would boot a
    /// different save from the one every screen shows.
    /// </summary>
    [Fact]
    public void A_portable_copy_is_never_started_when_saves_are_read_from_appdata()
    {
        var portable = Exe("Portatil", new DateTime(2026, 9, 10), portable: true);
        var installed = Exe("Nuevo_azahar", new DateTime(2026, 9, 1));

        Assert.Equal(installed, AzaharExecutable.Choose(Installed, null, [portable, installed]).Executable);
        Assert.Null(AzaharExecutable.Choose(Installed, null, [portable]).Executable);
    }

    [Fact]
    public void The_one_picked_by_hand_wins_unless_it_is_gone_or_portable()
    {
        var picked = Exe("Elegido", new DateTime(2026, 1, 1));
        var newer = Exe("Nuevo_azahar", new DateTime(2026, 9, 6));
        var portable = Exe("Portatil", new DateTime(2026, 9, 10), portable: true);

        Assert.Equal(picked, AzaharExecutable.Choose(Installed, picked, [newer]).Executable);

        var gone = AzaharExecutable.Choose(Installed, Path.Combine(_root, "no-existe", "azahar.exe"), [newer]);
        Assert.Equal(newer, gone.Executable);
        Assert.Contains("ya no está", gone.Detail);

        var refused = AzaharExecutable.Choose(Installed, portable, [newer]);
        Assert.Equal(newer, refused.Executable);
        Assert.Contains("no sirve", refused.Detail);
    }

    [Fact]
    public void Nothing_found_is_said()
    {
        var choice = AzaharExecutable.Choose(Installed, null, [Path.Combine(_root, "nada", "azahar.exe")]);

        Assert.Null(choice.Executable);
        Assert.Contains("Elige azahar.exe", choice.Detail);
    }

    /// <summary>
    /// The launcher closes the window cleanly, and Azahar's own «exit now?» would swallow that request. Only that
    /// setting and its default flag change; the rest of the file stays as the player had it.
    /// </summary>
    [Fact]
    public void Closing_without_the_emulators_question_touches_only_that_setting()
    {
        var user = Path.Combine(_root, "user");
        var config = Path.Combine(user, "config", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllLines(config,
        [
            "[UI]",
            @"fullscreen\default=true",
            "fullscreen=false",
            @"confirmClose\default=true",
            "confirmClose=true",
            "[Core]",
            "cpu_clock_percentage=100"
        ]);

        var installation = new AzaharInstallation(NullLogger<AzaharInstallation>.Instance);
        Assert.True(installation.DisableCloseConfirmation(new AzaharLocation(user, null, false)));

        var lines = File.ReadAllLines(config);
        Assert.Contains("confirmClose=false", lines);
        Assert.Contains(@"confirmClose\default=false", lines);
        Assert.Contains("fullscreen=false", lines);
        Assert.Contains("cpu_clock_percentage=100", lines);
        Assert.Equal(7, lines.Length);
    }

    /// <summary>
    /// Azahar's «Jugando a Azahar» goes off so PermaLocke's own Discord status is the one shown (§185), with its default
    /// flag, and nothing else in the file moves.
    /// </summary>
    [Fact]
    public void The_emulators_discord_status_goes_off_and_nothing_else_moves()
    {
        var user = Path.Combine(_root, "user");
        var config = Path.Combine(user, "config", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllLines(config,
        [
            "[UI]",
            @"enable_discord_presence\default=true",
            "enable_discord_presence=true",
            "confirmClose=false",
            "[Core]",
            "cpu_clock_percentage=100"
        ]);

        var installation = new AzaharInstallation(NullLogger<AzaharInstallation>.Instance);
        Assert.True(installation.DisableDiscordPresence(new AzaharLocation(user, null, false)));

        var lines = File.ReadAllLines(config);
        Assert.Contains("enable_discord_presence=false", lines);
        Assert.Contains(@"enable_discord_presence\default=false", lines);
        Assert.Contains("confirmClose=false", lines);
        Assert.Contains("cpu_clock_percentage=100", lines);
        Assert.Equal(6, lines.Length);

        // Otra vez no cambia nada.
        Assert.True(installation.DisableDiscordPresence(new AzaharLocation(user, null, false)));
        Assert.Equal(lines, File.ReadAllLines(config));
    }

    /// <summary>The save state and restart keys go, with their default flags, and a state left in reach is moved out (2026-09-26).</summary>
    [Fact]
    public void Save_states_lose_their_keys_and_are_moved_out_of_reach()
    {
        var user = Path.Combine(_root, "user");
        var config = Path.Combine(user, "config", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllLines(config,
        [
            "[UI]",
            @"Shortcuts\Main%20Window\Quick%20Load\KeySeq\default=true",
            @"Shortcuts\Main%20Window\Quick%20Load\KeySeq=F7",
            @"Shortcuts\Main%20Window\Capture%20Screenshot\KeySeq=Ctrl+P",
            "[Core]"
        ]);

        var installation = new AzaharInstallation(NullLogger<AzaharInstallation>.Instance);
        var location = new AzaharLocation(user, null, false);
        Assert.True(installation.DisableSaveStates(location));

        var lines = File.ReadAllLines(config);
        Assert.Contains(@"Shortcuts\Main%20Window\Quick%20Load\KeySeq=", lines);
        Assert.Contains(@"Shortcuts\Main%20Window\Quick%20Load\KeySeq\default=false", lines);
        Assert.Contains(@"Shortcuts\Main%20Window\Restart%20Emulation\KeySeq=", lines);
        Assert.Contains(@"Shortcuts\Main%20Window\Capture%20Screenshot\KeySeq=Ctrl+P", lines);

        var states = Path.Combine(user, "states");
        Directory.CreateDirectory(states);
        File.WriteAllText(Path.Combine(states, "00040000001B5100.01.cst"), "estado");

        var aside = Path.Combine(_root, "retirados");
        Assert.Equal(["00040000001B5100.01.cst"], installation.SetAsideSaveStates(location, aside));
        Assert.Empty(Directory.GetFiles(states));
        Assert.Single(Directory.GetFiles(aside));
    }
}
