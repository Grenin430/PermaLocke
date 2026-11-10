using PermaLocke.Core.Domain;
using PermaLocke.Infrastructure;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The launcher (2026-11-10): each game reads its data from its own folder under Data/, and falls back to Data/ itself for what it has not
/// got there, so the folders made before the launcher keep working.
/// </summary>
public sealed class GameDataFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"permalocke-data-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private AppPaths Paths()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.Data);
        return paths;
    }

    [Fact]
    public void Without_a_game_it_is_the_shared_folder()
    {
        var paths = Paths();
        Assert.Equal(Path.Combine(paths.Data, "rules.json"), paths.DataFile("rules.json"));
    }

    [Fact]
    public void The_game_s_own_file_wins()
    {
        var paths = Paths();
        paths.UseGame("ultraluna");
        Directory.CreateDirectory(Path.Combine(paths.Data, "ultraluna"));
        File.WriteAllText(Path.Combine(paths.Data, "ultraluna", "rules.json"), "{}");
        File.WriteAllText(Path.Combine(paths.Data, "rules.json"), "{}");

        Assert.Equal(Path.Combine(paths.Data, "ultraluna", "rules.json"), paths.DataFile("rules.json"));
    }

    [Fact]
    public void A_file_the_game_has_not_got_comes_from_the_shared_folder()
    {
        var paths = Paths();
        paths.UseGame("ultraluna");
        File.WriteAllText(Path.Combine(paths.Data, "roles.json"), "{}");

        Assert.Equal(Path.Combine(paths.Data, "roles.json"), paths.DataFile("roles.json"));
    }

    [Fact]
    public void Folders_are_looked_for_the_same_way()
    {
        var paths = Paths();
        paths.UseGame("ultraluna");
        Directory.CreateDirectory(Path.Combine(paths.Data, "ultraluna", "mapa-islas"));

        Assert.Equal(Path.Combine(paths.Data, "ultraluna", "mapa-islas"), paths.DataFile("mapa-islas"));
        Assert.Equal(Path.Combine(paths.Data, "sprites"), paths.DataFile("sprites"));
    }


    [Fact]
    public void Every_game_has_its_own_folder_and_old_runs_stay_ultra_moon()
    {
        Assert.Equal(GameCatalog.All.Count, GameCatalog.All.Select(g => g.Folder).Distinct().Count());
        Assert.True(GameCatalog.Of(GameVersion.UltraMoon).Playable);
        Assert.True(GameCatalog.Of(GameVersion.HeartGold).Playable);
        Assert.Equal(GameVersion.UltraMoon, Enum.Parse<GameVersion>("UltraMoon"));
    }
}
