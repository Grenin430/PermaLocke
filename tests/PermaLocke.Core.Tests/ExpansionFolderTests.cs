using PermaLocke.Infrastructure;

namespace PermaLocke.Core.Tests;

/// <summary>
/// One copy of the gen 8-9 mod serves every copy of the app beside it (§148): a copy uses its own mod when
/// it has one, and the folder above's otherwise.
/// </summary>
public sealed class ExpansionFolderTests : IDisposable
{
    private readonly string _competition = Path.Combine(Path.GetTempPath(), $"permalocke-exp-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_competition))
        {
            Directory.Delete(_competition, recursive: true);
        }
    }

    private string App(string name) => Directory.CreateDirectory(Path.Combine(_competition, name)).FullName;

    private void Mod(string folder) => Directory.CreateDirectory(Path.Combine(folder, "Expansion", "romfs"));

    [Fact]
    public void A_copy_without_its_own_mod_uses_the_shared_one()
    {
        Mod(_competition);

        Assert.Equal(Path.Combine(_competition, "Expansion"), new AppPaths(App("3")).Expansion);
    }

    /// <summary>
    /// Every copy gets an empty Expansion folder the first time it starts; that must not hide the shared mod.
    /// </summary>
    [Fact]
    public void An_empty_expansion_folder_does_not_hide_the_shared_one()
    {
        Mod(_competition);
        var app = App("4");
        Directory.CreateDirectory(Path.Combine(app, "Expansion"));

        Assert.Equal(Path.Combine(_competition, "Expansion"), new AppPaths(app).Expansion);
    }

    [Fact]
    public void Its_own_mod_wins()
    {
        Mod(_competition);
        var app = App("5");
        Mod(app);

        Assert.Equal(Path.Combine(app, "Expansion"), new AppPaths(app).Expansion);
    }

    [Fact]
    public void With_no_mod_anywhere_it_is_its_own_folder()
    {
        var app = App("6");

        Assert.Equal(Path.Combine(app, "Expansion"), new AppPaths(app).Expansion);
    }
}
