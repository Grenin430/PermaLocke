using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Where the competition's shared folder is. The copies for friends live inside it and say «..», so a
/// relative path has to mean «from the application's folder», whatever the drive letter and however the
/// program was started.
/// </summary>
public sealed class SharedFolderSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-sync-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void A_relative_folder_is_taken_from_the_application_folder()
    {
        var app = Path.Combine(_root, "Competicion", "3");
        var settings = new SharedFolderSettings(Path.Combine(app, "Config"));
        settings.Write("..");

        Assert.Equal(Path.Combine(_root, "Competicion"), settings.Read());
    }

    [Fact]
    public void An_absolute_folder_is_kept_as_written()
    {
        var settings = new SharedFolderSettings(Path.Combine(_root, "Config"));
        settings.Write(@"G:\Mi unidad\PermaLocke Competición");

        Assert.Equal(@"G:\Mi unidad\PermaLocke Competición", settings.Read());
    }

    [Fact]
    public void Nothing_written_is_nothing()
    {
        Assert.Equal(string.Empty, new SharedFolderSettings(Path.Combine(_root, "Config")).Read());
    }
}
