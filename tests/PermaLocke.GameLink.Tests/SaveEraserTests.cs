using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Erasing the partida.
/// </summary>
/// <remarks>
/// Every test here is really the same one, asked from a different starting point: <b>afterwards the
/// game must see no save data at all</b>. The bug that made these necessary looked harmless -- the
/// eraser deleted <c>main</c> and left <c>00000001.metadata</c> -- but that metadata is how Azahar
/// records the archive as formatted, so the game found a partida that was declared and missing and
/// reported the save as corrupted. Not deleted enough is not the safe side of this operation: it is
/// a state the player cannot start a new game from either.
/// </remarks>
public sealed class SaveEraserTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "permalocke-eraser-" + Guid.NewGuid().ToString("N"));

    private readonly AzaharRpcClient _client = new(port: 1);

    /// <summary>The title's save archive: the folder holding main, as Azahar lays it out.</summary>
    private string Archive => Path.Combine(_root, "Emulator", "user", "sdmc", "Nintendo 3DS",
        "00000000000000000000000000000000", "00000000000000000000000000000000",
        "title", "00040000", "001b5100", "data", "00000001");

    private string Metadata => Archive + ".metadata";

    private string Backups => Path.Combine(_root, "backup");

    /// <summary>Builds a portable Azahar with a partida in it.</summary>
    private void GivenASave(bool withMain = true, bool withMetadata = true)
    {
        var emulator = Path.Combine(_root, "Emulator");
        Directory.CreateDirectory(emulator);
        File.WriteAllText(Path.Combine(emulator, "azahar.exe"), string.Empty);

        Directory.CreateDirectory(Archive);

        if (withMain)
        {
            File.WriteAllBytes(Path.Combine(Archive, "main"), new byte[445440]);
        }

        if (withMetadata)
        {
            File.WriteAllBytes(Metadata, [0, 0, 4, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]);
        }
    }

    private SaveEraser Eraser() => new(
        new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), _client, _root),
        Backups, new FixedClock(), NullLogger<SaveEraser>.Instance);

    /// <summary>The regression: the archive's metadata must go with the save.</summary>
    [Fact]
    public void Erasing_takes_the_save_the_folder_and_the_metadata()
    {
        GivenASave();

        var erased = Eraser().Erase();

        Assert.True(erased.Erased);
        Assert.False(File.Exists(Path.Combine(Archive, "main")));
        Assert.False(File.Exists(Metadata));
        Assert.False(Directory.Exists(Archive));
    }

    /// <summary>
    /// The state the bug left behind: no save, but an archive still declaring one. The eraser has
    /// to be able to finish the job, because that is the state the game calls corrupted.
    /// </summary>
    [Fact]
    public void An_archive_left_without_its_save_is_cleaned_up_too()
    {
        GivenASave(withMain: false);

        var erased = Eraser().Erase();

        Assert.True(erased.Erased);
        Assert.False(File.Exists(Metadata));
        Assert.False(Directory.Exists(Archive));
    }

    /// <summary>Nothing is deleted that was not copied first, metadata included.</summary>
    [Fact]
    public void The_copy_holds_everything_needed_to_put_it_back()
    {
        GivenASave();

        var erased = Eraser().Erase();

        Assert.True(File.Exists(Path.Combine(erased.BackupFolder, "main")));
        Assert.True(File.Exists(Path.Combine(erased.BackupFolder, "00000001.metadata")));
        Assert.Equal(445440, new FileInfo(Path.Combine(erased.BackupFolder, "main")).Length);
    }

    [Fact]
    public void With_no_partida_at_all_it_says_so_and_deletes_nothing()
    {
        var emulator = Path.Combine(_root, "Emulator");
        Directory.CreateDirectory(emulator);
        File.WriteAllText(Path.Combine(emulator, "azahar.exe"), string.Empty);

        var erased = Eraser().Erase();

        Assert.False(erased.Erased);
        Assert.Contains("no hay nada que borrar", erased.Message);
        Assert.False(Directory.Exists(Backups));
    }

    public void Dispose()
    {
        _client.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => new(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);
    }
}
