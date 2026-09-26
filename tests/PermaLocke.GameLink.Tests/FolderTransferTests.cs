using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Bringing a player's PermaLocke from an old folder (§195): everything that makes their run is copied, the old folder is
/// left exactly as it was, and what the new one already had is set aside, never overwritten.
/// </summary>
public sealed class FolderTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-traspaso-" + Guid.NewGuid().ToString("N"));

    private string Old => Path.Combine(_root, "vieja");

    private string New => Path.Combine(_root, "nueva");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A PermaLocke folder as the local distribution leaves it, with a run played in it.</summary>
    private void OldFolder(bool rom = true)
    {
        Write(Path.Combine(Old, "Saves", "permalocke.db"), "db vieja");
        Write(Path.Combine(Old, "Saves", "runs", "abc", "run.json"), "{}");
        Write(Path.Combine(Old, "Saves", "killcams", "k.bin"), "killcam");
        Write(Path.Combine(Old, "Emulator", "azahar.exe"), "exe");
        Write(Path.Combine(Old, "Emulator", "user", "sdmc", "Nintendo 3DS", "0000", "1111", "title", "00040000", "001b5100", "data", "00000001", "main"), "partida");
        Write(Path.Combine(Old, "Emulator", "user", "load", "mods", "00040000001B5100", "romfs", "a.bin"), "mundo");
        Write(Path.Combine(Old, "Emulator", "user", "config", "qt-config.ini"), "config del emulador viejo");
        Write(Path.Combine(Old, "Config", "jugador.json"), "yo");
        Write(Path.Combine(Old, "Config", "discord.json"), "sesion");
        Write(Path.Combine(Old, "Config", "ajustes.json"), "ajustes");
        if (rom) Write(Path.Combine(Old, "ROM", "ultraluna.3ds"), "rom");
    }

    /// <summary>A fresh folder that has been opened once: an empty database, no run.</summary>
    private void NewFolder()
    {
        Write(Path.Combine(New, "Saves", "permalocke.db"), "db vacia");
        Write(Path.Combine(New, "Emulator", "azahar.exe"), "exe nuevo");
        Write(Path.Combine(New, "Emulator", "user", "config", "qt-config.ini"), "config nueva");
        Directory.CreateDirectory(Path.Combine(New, "ROM"));
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    [Fact]
    public void Brings_the_run_the_save_the_world_the_player_and_the_rom()
    {
        OldFolder();
        NewFolder();

        var plan = FolderTransfer.Plan(Old, New);
        Assert.True(plan.CanGo, string.Join(" / ", plan.Problems));

        var result = FolderTransfer.Run(plan, new DateTimeOffset(2026, 9, 26, 20, 0, 0, TimeSpan.Zero), NullLogger.Instance);

        Assert.True(result.Done);
        Assert.Equal("db vieja", Read(New, "Saves", "permalocke.db"));
        Assert.Equal("killcam", Read(New, "Saves", "killcams", "k.bin"));
        Assert.Equal("partida", Read(New, "Emulator", "user", "sdmc", "Nintendo 3DS", "0000", "1111", "title", "00040000", "001b5100", "data", "00000001", "main"));
        Assert.Equal("mundo", Read(New, "Emulator", "user", "load", "mods", "00040000001B5100", "romfs", "a.bin"));
        Assert.Equal("yo", Read(New, "Config", "jugador.json"));
        Assert.Equal("sesion", Read(New, "Config", "discord.json"));
        Assert.Equal("rom", Read(New, "ROM", "ultraluna.3ds"));

        // Lo que no es de la run no se trae: los ajustes y la configuración del emulador nuevo se quedan como estaban.
        Assert.False(File.Exists(Path.Combine(New, "Config", "ajustes.json")));
        Assert.Equal("config nueva", Read(New, "Emulator", "user", "config", "qt-config.ini"));
    }

    [Fact]
    public void Leaves_the_old_folder_as_it_was_and_sets_aside_what_was_here()
    {
        OldFolder();
        NewFolder();
        var before = Directory.EnumerateFiles(Old, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllText);

        var result = FolderTransfer.Run(FolderTransfer.Plan(Old, New), new DateTimeOffset(2026, 9, 26, 20, 0, 0, TimeSpan.Zero),
            NullLogger.Instance);

        var after = Directory.EnumerateFiles(Old, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText);
        Assert.Equal(before, after);

        // La base de datos vacía que había aquí no se pisa: se aparta.
        Assert.NotNull(result.SetAside);
        Assert.Equal("db vacia", Read(result.SetAside!, "Saves", "permalocke.db"));
    }

    [Fact]
    public void Never_replaces_a_run_this_folder_already_has()
    {
        OldFolder();
        NewFolder();
        Write(Path.Combine(New, "Saves", "runs", "mia", "run.json"), "{}");

        var plan = FolderTransfer.Plan(Old, New);

        Assert.False(plan.CanGo);
        Assert.Contains(plan.Problems, problem => problem.Contains("ya tienes una run"));
        Assert.False(FolderTransfer.Run(plan, DateTimeOffset.Now, NullLogger.Instance).Done);
        Assert.Equal("db vacia", Read(New, "Saves", "permalocke.db"));
    }

    [Fact]
    public void Refuses_this_same_folder_and_one_without_a_run()
    {
        NewFolder();
        Assert.False(FolderTransfer.Plan(New, New).CanGo);

        Directory.CreateDirectory(Old);
        Assert.Contains(FolderTransfer.Plan(Old, New).Problems, problem => problem.Contains("no hay ninguna run"));
    }

    [Fact]
    public void Leaves_a_rom_already_here_alone()
    {
        OldFolder();
        NewFolder();
        Write(Path.Combine(New, "ROM", "mia.3ds"), "la mia");

        var plan = FolderTransfer.Plan(Old, New);

        Assert.DoesNotContain(plan.Items, item => item.What.Contains("ROM"));
    }

    [Fact]
    public void A_pending_transfer_is_done_once_at_start()
    {
        OldFolder();
        NewFolder();
        var config = Path.Combine(New, "Config");
        FolderTransfer.Schedule(config, Old);

        var result = FolderTransfer.RunPending(New, config, NullLogger.Instance);

        Assert.True(result?.Done);
        Assert.False(File.Exists(Path.Combine(config, FolderTransfer.PendingFile)));
        Assert.True(File.Exists(Path.Combine(config, FolderTransfer.DoneFile)));
        Assert.Null(FolderTransfer.RunPending(New, config, NullLogger.Instance));
    }
}
